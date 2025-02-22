using System;
using System.ServiceProcess;

namespace ScreenCaptureAgent
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        static void Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                var service = new ImageTextCollectionService();
                service.Start(args);
            }
            else
            {
                var servicesToRun = new ServiceBase[]
                {
                    new ImageTextCollectionService()
                };
                ServiceBase.Run(servicesToRun);
            }
        }
    }
}
