using System;
using System.Windows;
using NLog;

namespace PointPositionApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 设置日志文件名中的启动时间戳: PointPosition_YYYYMMDDHHMMSS.log
            LogManager.Configuration.Variables["appStartTime"] = DateTime.Now.ToString("yyyyMMddHHmmss");
            LogManager.ReconfigExistingLoggers();

            LogManager.GetCurrentClassLogger().Info("应用程序启动");
        }
    }
}
