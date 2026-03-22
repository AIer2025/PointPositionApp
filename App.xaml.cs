using System.Windows;
using NLog;

namespace PointPositionApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // NLog 配置由 NLog.config 文件自动加载，无需编程式配置
            LogManager.GetCurrentClassLogger().Info("应用程序启动");
            //为了解决INFO乱码
            //Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
    }
}
