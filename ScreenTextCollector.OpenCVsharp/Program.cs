namespace ScreenTextCollector.OpenCVsharp
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        static void Main(string[] args)
        {
            var service = new ScreenTextCollectorService();
            service.Start(args);
        }
    }
}
