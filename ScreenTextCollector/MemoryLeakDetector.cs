using System;
using System.Diagnostics;
using System.Threading;

namespace ScreenTextCollector.MemoryMonitoring
{
    /// <summary>
    /// 内存泄漏检测工具
    /// 用于监控程序运行时的内存占用情况
    /// </summary>
    public class MemoryLeakDetector
    {
        private readonly Process _process;
        private DateTime _startTime;
        private long _initialMemory;
        private long _peakMemory;

        public MemoryLeakDetector()
        {
            _process = Process.GetCurrentProcess();
            _startTime = DateTime.Now;
            _initialMemory = _process.WorkingSet64;
            _peakMemory = _initialMemory;
        }

        /// <summary>
        /// 获取当前内存占用（MB）
        /// </summary>
        public double GetCurrentMemoryMB()
        {
            _process.Refresh();
            return _process.WorkingSet64 / (1024.0 * 1024.0);
        }

        /// <summary>
        /// 获取初始内存占用（MB）
        /// </summary>
        public double GetInitialMemoryMB()
        {
            return _initialMemory / (1024.0 * 1024.0);
        }

        /// <summary>
        /// 获取峰值内存占用（MB）
        /// </summary>
        public double GetPeakMemoryMB()
        {
            _process.Refresh();
            long current = _process.WorkingSet64;
            if (current > _peakMemory)
                _peakMemory = current;
            return _peakMemory / (1024.0 * 1024.0);
        }

        /// <summary>
        /// 获取内存增长（MB）
        /// </summary>
        public double GetMemoryIncreaseMB()
        {
            return GetCurrentMemoryMB() - GetInitialMemoryMB();
        }

        /// <summary>
        /// 获取运行时长（秒）
        /// </summary>
        public double GetElapsedSeconds()
        {
            return (DateTime.Now - _startTime).TotalSeconds;
        }

        /// <summary>
        /// 获取平均每秒内存增长速率（MB/s）
        /// </summary>
        public double GetMemoryGrowthRateMBPerSecond()
        {
            double elapsed = GetElapsedSeconds();
            if (elapsed <= 0) return 0;
            return GetMemoryIncreaseMB() / elapsed;
        }

        /// <summary>
        /// 打印内存统计信息
        /// </summary>
        public void PrintMemoryStats()
        {
            Console.WriteLine("========== 内存监控统计 ==========");
            Console.WriteLine($"运行时长: {GetElapsedSeconds():F1} 秒");
            Console.WriteLine($"初始内存: {GetInitialMemoryMB():F2} MB");
            Console.WriteLine($"当前内存: {GetCurrentMemoryMB():F2} MB");
            Console.WriteLine($"峰值内存: {GetPeakMemoryMB():F2} MB");
            Console.WriteLine($"内存增长: {GetMemoryIncreaseMB():F2} MB");
            Console.WriteLine($"增长速率: {GetMemoryGrowthRateMBPerSecond():F4} MB/s");
            
            // 判断是否可能有内存泄漏
            double growthRate = GetMemoryGrowthRateMBPerSecond();
            if (growthRate > 0.5)
            {
                Console.WriteLine("??  警告: 检测到快速内存增长，可能存在内存泄漏！");
            }
            else if (growthRate > 0.1)
            {
                Console.WriteLine("??  注意: 内存增长速度较快，建议继续观察。");
            }
            else
            {
                Console.WriteLine("? 内存占用正常。");
            }
            Console.WriteLine("================================");
        }

        /// <summary>
        /// 定期记录内存状态（用于后台监控）
        /// </summary>
        public void StartMemoryMonitoring(int intervalSeconds = 10, Action<MemorySnapshot> onSnapshot = null)
        {
            new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(intervalSeconds * 1000);

                    var snapshot = new MemorySnapshot
                    {
                        Timestamp = DateTime.Now,
                        ElapsedSeconds = GetElapsedSeconds(),
                        MemoryMB = GetCurrentMemoryMB(),
                        PeakMemoryMB = GetPeakMemoryMB(),
                        GrowthMB = GetMemoryIncreaseMB(),
                        GrowthRateMBPerSecond = GetMemoryGrowthRateMBPerSecond(),
                        ManagedMemoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0)
                    };

                    onSnapshot?.Invoke(snapshot);
                }
            })
            { IsBackground = true }.Start();
        }
    }

    /// <summary>
    /// 内存快照信息
    /// </summary>
    public class MemorySnapshot
    {
        public DateTime Timestamp { get; set; }
        public double ElapsedSeconds { get; set; }
        public double MemoryMB { get; set; }
        public double PeakMemoryMB { get; set; }
        public double GrowthMB { get; set; }
        public double GrowthRateMBPerSecond { get; set; }
        public double ManagedMemoryMB { get; set; }

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss}] " +
                   $"运行: {ElapsedSeconds:F0}s, " +
                   $"内存: {MemoryMB:F2}MB, " +
                   $"峰值: {PeakMemoryMB:F2}MB, " +
                   $"增长: {GrowthMB:F2}MB, " +
                   $"速率: {GrowthRateMBPerSecond:F4}MB/s, " +
                   $"托管: {ManagedMemoryMB:F2}MB";
        }
    }
}
