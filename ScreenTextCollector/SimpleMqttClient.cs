using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SimpleMqttClient
{
    public class MqttClient : IDisposable
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private string _clientId;
        private string _username;
        private string _password;
        private bool _cleanSession = true;
        private int _keepAliveSeconds = 60;
        private Thread _receiveThread;
        private volatile bool _isConnected = false;
        private ushort _messageIdCounter = 1;

        // 自动重连与连接信息
        private string _host;
        private int _port;
        private bool _autoReconnect = false;
        private int _reconnectInitialSeconds = 2;
        private int _reconnectMaxSeconds = 60;
        private int _currentReconnectSeconds;
        private Timer _reconnectTimer;

        // Ping / KeepAlive
        private Timer _pingTimer;
        private volatile bool _pingOutstanding = false;
        private DateTime _lastReceivedUtc = DateTime.UtcNow;

        // 同步与等待 CONNACK
        private ManualResetEvent _connAckEvent = new ManualResetEvent(false);
        private readonly object _syncRoot = new object();

        // Events
        public event Action<string, byte[]> MessageReceived;
        public event Action Connected;
        public event Action Disconnected;

        public MqttClient(string clientId)
        {
            _clientId = clientId ?? Guid.NewGuid().ToString("N");
            _currentReconnectSeconds = _reconnectInitialSeconds;
        }

        /// <summary>
        /// Connect with optional timeout (seconds) and autoReconnect flag.
        /// </summary>
        public bool Connect(string host, int port = 1883, int timeoutSeconds = 10, bool autoReconnect = false)
        {
            lock (_syncRoot)
            {
                if (_isConnected) return true;

                _host = host;
                _port = port;
                _autoReconnect = autoReconnect;
                _currentReconnectSeconds = _reconnectInitialSeconds;

                return TryConnectWithTimeout(timeoutSeconds * 1000);
            }
        }

        /// <summary>
        /// Disconnect and stop timers / reconnect attempts.
        /// </summary>
        public void Disconnect()
        {
            lock (_syncRoot)
            {
                _autoReconnect = false;
                StopPingTimer();
                StopReconnectTimer();

                if (!_isConnected && _client == null) return;

                try
                {
                    // 发送 DISCONNECT 报文，容错
                    var disconnectPacket = new byte[] { 0xE0, 0x00 };
                    _stream?.Write(disconnectPacket, 0, disconnectPacket.Length);
                }
                catch { }

                _isConnected = false;
                try { _stream?.Close(); } catch { }
                try { _client?.Close(); } catch { }

                try
                {
                    if (_receiveThread != null && _receiveThread.IsAlive)
                    {
                        // 等待接收线程自然退出
                        _receiveThread.Join(1000);
                    }
                }
                catch { }

                Disconnected?.Invoke();
            }
        }

        public void SetCredentials(string username, string password)
        {
            _username = username;
            _password = password;
        }

        public void SetCleanSession(bool clean)
        {
            _cleanSession = clean;
        }

        public void SetKeepAlive(int seconds)
        {
            if (seconds < 0) return;
            _keepAliveSeconds = seconds;
            RestartPingTimer();
        }

        private bool TryConnectWithTimeout(int timeoutMs)
        {
            // 清理旧资源
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }

            _client = new TcpClient();

            try
            {
                // BeginConnect + timeout
                IAsyncResult ar = _client.BeginConnect(_host, _port, null, null);
                bool connected = ar.AsyncWaitHandle.WaitOne(timeoutMs);
                if (!connected)
                {
                    try { _client.Close(); } catch { }
                    ScheduleReconnect();
                    return false;
                }
                _client.EndConnect(ar);
                _stream = _client.GetStream();
            }
            catch
            {
                try { _client.Close(); } catch { }
                ScheduleReconnect();
                return false;
            }

            // 启动接收线程
            _lastReceivedUtc = DateTime.UtcNow;
            _isConnected = true; // TCP 已建立
            _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            _receiveThread.Start();

            // 发送 CONNECT 报文，并等待 CONNACK
            _connAckEvent.Reset();
            SendConnectPacket();

            bool connack = _connAckEvent.WaitOne(Math.Max(1000, timeoutMs)); // 等待 CONNACK
            if (!connack)
            {
                // 未收到 CONNACK，断开并尝试重连（如果开启）
                try { _stream?.Close(); } catch { }
                try { _client?.Close(); } catch { }
                _isConnected = false;
                ScheduleReconnect();
                return false;
            }

            // 成功：启动 ping 定时器并重置重连间隔
            RestartPingTimer();
            _currentReconnectSeconds = _reconnectInitialSeconds;
            StopReconnectTimer(); // 成功后停止重连计时器（如果存在）
            return true;
        }

        private void ScheduleReconnect()
        {
            if (!_autoReconnect) return;

            // 指数退避
            int delay = _currentReconnectSeconds;
            _currentReconnectSeconds = Math.Min(_currentReconnectSeconds * 2, _reconnectMaxSeconds);

            StartReconnectTimer(delay * 1000);
        }

        private void StartReconnectTimer(int dueTimeMs)
        {
            StopReconnectTimer();
            _reconnectTimer = new Timer(state =>
            {
                // 在线程池上尝试重连
                try
                {
                    if (_isConnected) { StopReconnectTimer(); return; }
                    TryConnectWithTimeout(10000);
                }
                catch { }
            }, null, dueTimeMs, Timeout.Infinite);
        }

        private void StopReconnectTimer()
        {
            try
            {
                _reconnectTimer?.Dispose();
                _reconnectTimer = null;
            }
            catch { }
        }

        private void RestartPingTimer()
        {
            StopPingTimer();
            if (_keepAliveSeconds <= 0) return;

            // 定期发送 PINGREQ：在 KeepAlive 的一半频率发送以确保及时心跳
            int periodMs = Math.Max(1000, (_keepAliveSeconds * 1000) / 2);
            _pingTimer = new Timer(state =>
            {
                try
                {
                    // 如果上次发送的 PING 仍未收到回复，并且最后接收时间超过 1.5 * keepAlive，则认为连接失效
                    if (_pingOutstanding || (DateTime.UtcNow - _lastReceivedUtc).TotalSeconds > _keepAliveSeconds * 1.5)
                    {
                        // 标记断开并触发断线逻辑
                        HandleConnectionLost();
                        return;
                    }

                    // 发送 PINGREQ
                    byte[] pingreq = { 0xC0, 0x00 };
                    _stream?.Write(pingreq, 0, pingreq.Length);
                    _pingOutstanding = true;
                }
                catch
                {
                    HandleConnectionLost();
                }
            }, null, periodMs, periodMs);
        }

        private void StopPingTimer()
        {
            try
            {
                _pingTimer?.Dispose();
                _pingTimer = null;
                _pingOutstanding = false;
            }
            catch { }
        }

        private void HandleConnectionLost()
        {
            lock (_syncRoot)
            {
                if (!_isConnected) return;
                _isConnected = false;
                try { _stream?.Close(); } catch { }
                try { _client?.Close(); } catch { }
                StopPingTimer();

                // 触发断开事件
                Disconnected?.Invoke();

                // 如果允许自动重连，则安排重连
                if (_autoReconnect)
                {
                    ScheduleReconnect();
                }
            }
        }

        private void SendConnectPacket()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // 1. 写入协议名 (MSB/LSB big-endian)
                WriteString(writer, "MQTT");

                // 2. 协议级别 (3.1.1 = 4)
                writer.Write((byte)4);

                // 3. 连接标志
                byte connectFlags = 0;
                if (_cleanSession) connectFlags |= 0x02;
                if (!string.IsNullOrEmpty(_username)) connectFlags |= 0x80;
                if (!string.IsNullOrEmpty(_password)) connectFlags |= 0x40;
                writer.Write(connectFlags);

                // 4. Keep Alive (2 字节 big-endian)
                WriteUInt16BE(writer, (ushort)_keepAliveSeconds);

                // 5. ClientId
                WriteString(writer, _clientId);

                // 6. 用户名和密码（如果设置了）
                if (!string.IsNullOrEmpty(_username))
                {
                    WriteString(writer, _username);
                    if (!string.IsNullOrEmpty(_password))
                    {
                        WriteString(writer, _password);
                    }
                }

                byte[] payload = ms.ToArray();

                // 构造完整报文：固定头 + 剩余长度 + payload
                var fixedHeader = new byte[] { 0x10 }; // CONNECT 类型
                var remainingLength = EncodeRemainingLength(payload.Length);

                // 发送
                try
                {
                    _stream.Write(fixedHeader, 0, 1);
                    _stream.Write(remainingLength, 0, remainingLength.Length);
                    _stream.Write(payload, 0, payload.Length);
                }
                catch
                {
                    // 发送失败会在上层触发重连逻辑
                }
            }
        }

        public void Publish(string topic, string message, byte qos = 0, bool retain = false)
        {
            byte[] payloadBytes = Encoding.UTF8.GetBytes(message);
            Publish(topic, payloadBytes, qos, retain);
        }

        public void Publish(string topic, byte[] payload, byte qos = 0, bool retain = false)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // 1. Topic
                WriteString(writer, topic);

                // 2. Message ID（如果 QoS > 0）
                ushort messageId = 0;
                if (qos > 0)
                {
                    messageId = _messageIdCounter++;
                    if (_messageIdCounter == 0) _messageIdCounter = 1;
                    WriteUInt16BE(writer, messageId);
                }

                // 3. Payload
                writer.Write(payload);

                byte[] variableAndPayload = ms.ToArray();

                // 固定头
                byte fixedHeader = 0x30; // PUBLISH
                if (retain) fixedHeader |= 0x01;
                fixedHeader |= (byte)(qos << 1);

                var remainingLength = EncodeRemainingLength(variableAndPayload.Length);

                try
                {
                    _stream.Write(new[] { fixedHeader }, 0, 1);
                    _stream.Write(remainingLength, 0, remainingLength.Length);
                    _stream.Write(variableAndPayload, 0, variableAndPayload.Length);
                }
                catch
                {
                    HandleConnectionLost();
                }
            }
        }

        public void Subscribe(string topic, byte qos = 0)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Message ID
                ushort messageId = _messageIdCounter++;
                if (_messageIdCounter == 0) _messageIdCounter = 1;
                WriteUInt16BE(writer, messageId);

                // Topic Filter
                WriteString(writer, topic);
                writer.Write(qos); // QoS

                byte[] payload = ms.ToArray();

                byte fixedHeader = 0x82; // SUBSCRIBE | QoS1 | DUP=0, RETAIN=0
                var remainingLength = EncodeRemainingLength(payload.Length);

                try
                {
                    _stream.Write(new[] { fixedHeader }, 0, 1);
                    _stream.Write(remainingLength, 0, remainingLength.Length);
                    _stream.Write(payload, 0, payload.Length);
                }
                catch
                {
                    HandleConnectionLost();
                }
            }
        }

        private void ReceiveLoop()
        {
            try
            {
                while (_isConnected)
                {
                    try
                    {
                        if (!_client.Connected)
                        {
                            HandleConnectionLost();
                            break;
                        }

                        // 等待数据
                        if (!_stream.DataAvailable)
                        {
                            Thread.Sleep(100);
                            continue;
                        }

                        int first = _stream.ReadByte();
                        if (first == -1)
                        {
                            HandleConnectionLost();
                            break;
                        }
                        byte firstByte = (byte)first;
                        byte msgType = (byte)((firstByte & 0xF0) >> 4);
                        byte flags = (byte)(firstByte & 0x0F);

                        // 读取剩余长度
                        int remainingLength = ReadRemainingLength(_stream);
                        if (remainingLength < 0)
                        {
                            HandleConnectionLost();
                            break;
                        }

                        byte[] data = new byte[remainingLength];
                        int totalRead = 0;
                        while (totalRead < remainingLength)
                        {
                            int read = _stream.Read(data, totalRead, remainingLength - totalRead);
                            if (read <= 0) break;
                            totalRead += read;
                        }

                        if (totalRead < remainingLength)
                        {
                            // 流提前结束
                            HandleConnectionLost();
                            break;
                        }

                        // 更新最后接收时间
                        _lastReceivedUtc = DateTime.UtcNow;

                        HandleIncomingPacket(firstByte, msgType, flags, data);
                    }
                    catch
                    {
                        Thread.Sleep(100);
                    }
                }
            }
            catch
            {
                // 顶层异常意味着连接丢失
            }
            finally
            {
                if (_isConnected)
                {
                    HandleConnectionLost();
                }
            }
        }

        private void HandleIncomingPacket(byte firstByte, byte msgType, byte flags, byte[] data)
        {
            switch (msgType)
            {
                case 2: // CONNACK
                    // Variable header: 1 byte Connect Acknowledge Flags, 1 byte Return Code
                    if (data.Length >= 2)
                    {
                        byte returnCode = data[1];
                        if (returnCode == 0)
                        {
                            _isConnected = true;
                            _connAckEvent.Set();
                            Connected?.Invoke();
                        }
                        else
                        {
                            // 登录被拒绝，触发断开与重连（如果设置）
                            _connAckEvent.Set();
                            HandleConnectionLost();
                        }
                    }
                    else
                    {
                        _connAckEvent.Set();
                        HandleConnectionLost();
                    }
                    break;

                case 3: // PUBLISH
                    using (var ms = new MemoryStream(data))
                    using (var reader = new BinaryReader(ms))
                    {
                        string topic = ReadString(reader);

                        ushort messageId = 0;
                        byte qos = (byte)((firstByte & 0x06) >> 1);
                        if (qos > 0)
                        {
                            messageId = ReadUInt16BE(reader);
                        }

                        byte[] payload = reader.ReadBytes((int)(ms.Length - ms.Position));
                        MessageReceived?.Invoke(topic, payload);

                        if (qos == 1)
                        {
                            SendPubAck(messageId);
                        }
                    }
                    break;

                case 13: // PINGRESP
                    // 收到 PINGRESP，取消 outstanding 标识
                    _pingOutstanding = false;
                    break;

                // 其它报文类型可扩展处理
            }
        }

        private void SendPubAck(ushort messageId)
        {
            byte[] packet = {
                0x40, // PUBACK fixed header
                0x02, // Remaining length
                (byte)(messageId >> 8),
                (byte)(messageId & 0xFF)
            };
            try
            {
                _stream.Write(packet, 0, packet.Length);
            }
            catch
            {
                HandleConnectionLost();
            }
        }

        private void WriteString(BinaryWriter writer, string s)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            WriteUInt16BE(writer, (ushort)bytes.Length);
            writer.Write(bytes);
        }

        private string ReadString(BinaryReader reader)
        {
            ushort len = ReadUInt16BE(reader);
            byte[] bytes = reader.ReadBytes(len);
            return Encoding.UTF8.GetString(bytes);
        }

        private void WriteUInt16BE(BinaryWriter writer, ushort value)
        {
            writer.Write((byte)((value >> 8) & 0xFF));
            writer.Write((byte)(value & 0xFF));
        }

        private ushort ReadUInt16BE(BinaryReader reader)
        {
            int hi = reader.ReadByte();
            int lo = reader.ReadByte();
            if (hi < 0 || lo < 0) throw new EndOfStreamException();
            return (ushort)((hi << 8) | lo);
        }

        private byte[] EncodeRemainingLength(int length)
        {
            var result = new List<byte>();
            do
            {
                byte b = (byte)(length % 128);
                length /= 128;
                if (length > 0) b |= 0x80;
                result.Add(b);
            } while (length > 0);
            return result.ToArray();
        }

        private int ReadRemainingLength(NetworkStream stream)
        {
            int multiplier = 1;
            int value = 0;
            int i = 0;

            do
            {
                if (i > 3) return -1; // 超过 4 字节，格式错误
                int read = stream.ReadByte();
                if (read == -1) return -1;
                byte b = (byte)read;
                value += (b & 0x7F) * multiplier;
                multiplier *= 128;
                i++;
                if ((b & 0x80) == 0) break;
            } while (true);

            return value;
        }

        public void Ping()
        {
            try
            {
                byte[] pingreq = { 0xC0, 0x00 };
                _stream?.Write(pingreq, 0, pingreq.Length);
            }
            catch
            {
                HandleConnectionLost();
            }
        }

        public bool IsConnected => _isConnected;

        public void Dispose()
        {
            Disconnect();
            try { _connAckEvent?.Close(); } catch { }
            StopPingTimer();
            StopReconnectTimer();
        }
    }
}