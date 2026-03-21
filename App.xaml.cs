using System.Windows;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace PointPositionApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ConfigureLogging();
        }

        private void ConfigureLogging()
        {
            var config = new LoggingConfiguration();

            var fileTarget = new FileTarget("logfile")
            {
                FileName = "${basedir}/logs/${shortdate}.log",
                Layout = "${longdate} [${level:uppercase=true}] ${logger} - ${message} ${exception:format=tostring}",
                ArchiveEvery = FileArchivePeriod.Day,
                MaxArchiveFiles = 30
            };

            var consoleTarget = new ConsoleTarget("console")
            {
                Layout = "${time} [${level:uppercase=true}] ${message}"
            };

            config.AddRule(LogLevel.Debug, LogLevel.Fatal, fileTarget);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, consoleTarget);

            LogManager.Configuration = config;
        }
    }
}
