using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using IoTGateway.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using WalkingTec.Mvvm.Core;

namespace MQTTnet.Extensions.ManagedClient.Persistence
{
    public sealed class DbCacheQueue : IDisposable
    {
        readonly IServiceScopeFactory _scopeFactory;
        readonly object _syncRoot = new object();

        ManualResetEventSlim _gate = new ManualResetEventSlim(false);

        readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
        {
            Converters = { new ArraySegmentConverter() }
        };

        public DbCacheQueue(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public int Count
        {
            get
            {
                lock (_syncRoot)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var wtmContext = scope.ServiceProvider.GetRequiredService<WTMContext>();
                    using var dc = wtmContext.DC;
                    return dc.Set<MqttCache>().Count();
                }
            }
        }

        public void Enqueue(ManagedMqttApplicationMessage item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            lock (_syncRoot)
            {
                var topic = item.ApplicationMessage.Topic;
                var payloadSegment = item.ApplicationMessage.PayloadSegment;
                string payload = string.Empty;
                string equip = string.Empty;
                if (payloadSegment.Array != null)
                {
                    payload = System.Text.Encoding.UTF8.GetString(payloadSegment.Array);
                    var values = JsonConvert.DeserializeObject<Dictionary<string, object>>(payload);
                    if (values.TryGetValue("equip", out var value)) equip = value.ToString();
                }

                using (var scope = _scopeFactory.CreateScope())
                {
                    var wtmContext = scope.ServiceProvider.GetRequiredService<WTMContext>();
                    using (var dc = wtmContext.DC)
                    {
                        dc.Set<MqttCache>().Add(new MqttCache
                        {
                            ID = item.Id,
                            DeviceName = equip,
                            Topic = topic,
                            Payload = payload,
                            ApplicationMessage = JsonConvert.SerializeObject(item.ApplicationMessage, _jsonSerializerSettings),
                            CreateTime = DateTime.Now
                        });
                        dc.SaveChanges();
                    }
                }
                _gate?.Set();
            }
        }

        public ManagedMqttApplicationMessage PeekAndWait(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                lock (_syncRoot)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var wtmContext = scope.ServiceProvider.GetRequiredService<WTMContext>();
                    using var dc = wtmContext.DC;
                    //先进先出
                    var cache = dc.Set<MqttCache>().Where(x => x.Result == 0).OrderBy(x => x.CreateTime)
                        .FirstOrDefault();
                    if (cache != null)
                    {
                        return new ManagedMqttApplicationMessage
                        {
                            Id = cache.ID,
                            ApplicationMessage =
                                JsonConvert.DeserializeObject<MqttApplicationMessage>(cache.ApplicationMessage, _jsonSerializerSettings)
                        };
                    }
                    _gate?.Reset();
                }

                _gate?.Wait(cancellationToken);
            }

            throw new OperationCanceledException();
        }

        public void RemoveFirst(Guid id)
        {
            lock (_syncRoot)
            {
                using var scope = _scopeFactory.CreateScope();
                var wtmContext = scope.ServiceProvider.GetRequiredService<WTMContext>();
                using var dc = wtmContext.DC;
                dc.Database.ExecuteSqlInterpolated($"update mqttcaches set result=1 where lower(id)=lower({id})");
            }
        }

        public void DbLog(Guid id, string errorMessage)
        {
            lock (_syncRoot)
            {
                using var scope = _scopeFactory.CreateScope();
                var wtmContext = scope.ServiceProvider.GetRequiredService<WTMContext>();
                using var dc = wtmContext.DC;
                dc.Database.ExecuteSqlInterpolated($"update mqttcaches set errormessage='{errorMessage}' where lower(id)=lower({id})");
            }
        }

        public void Dispose()
        {
            _gate?.Dispose();
            _gate = null;
        }
    }
}