using System;
using System.IO;
using Newtonsoft.Json;
using NLog;
using PointPositionApp.Models;

namespace PointPositionApp.Services
{
    /// <summary>配置服务 - 读写JSON配置文件</summary>
    public static class ConfigService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly string ConfigPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        /// <summary>加载配置，不存在则创建默认配置</summary>
        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                    if (settings != null)
                    {
                        Logger.Info("配置已加载: {0}", ConfigPath);
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "加载配置失败，使用默认配置");
            }

            var defaults = CreateDefaults();
            Save(defaults);
            return defaults;
        }

        /// <summary>保存配置</summary>
        public static void Save(AppSettings settings)
        {
            try
            {
                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
                Logger.Info("配置已保存: {0}", ConfigPath);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "保存配置失败");
            }
        }

        /// <summary>创建默认配置（含完整的轴信号表地址映射）</summary>
        public static AppSettings CreateDefaults()
        {
            return new AppSettings
            {
                DatabasePath = "pointposition.db",
                PlcIpAddress = "192.168.1.100",
                PlcPort = 502,
                PollingIntervalMs = 300,
                LogLevel = "Info",
                Axes = new()
                {
                    new AxisConfig
                    {
                        AxisName = "X轴",
                        JogForwardCoil = 1450,
                        JogReverseCoil = 1400,
                        CurrentPosRegister = 1000,
                        ManualSpeedRegister = 1500,
                        AutoSpeedRegister = 4000,
                        TargetPosRegister = 3008,
                        AbsMoveRegister = 1200,
                        AbsMoveIsCoil = false,
                        AbsMoveValue = 16,
                        HomeCoil = 1300,
                        EnableCoil = 1350,
                        HomePriority = 2
                    },
                    new AxisConfig
                    {
                        AxisName = "Y轴",
                        JogForwardCoil = 1451,
                        JogReverseCoil = 1401,
                        CurrentPosRegister = 1002,
                        ManualSpeedRegister = 1502,
                        AutoSpeedRegister = 4100,
                        TargetPosRegister = 3108,
                        AbsMoveRegister = 1210,
                        AbsMoveIsCoil = false,
                        AbsMoveValue = 16,
                        HomeCoil = 1301,
                        EnableCoil = 1351,
                        HomePriority = 2
                    },
                    new AxisConfig
                    {
                        AxisName = "R轴",
                        JogForwardCoil = 1452,
                        JogReverseCoil = 1402,
                        CurrentPosRegister = 1004,
                        ManualSpeedRegister = 1504,
                        AutoSpeedRegister = 4200,
                        TargetPosRegister = 3208,
                        AbsMoveRegister = 1220,
                        AbsMoveIsCoil = false,
                        AbsMoveValue = 16,
                        HomeCoil = 1302,
                        EnableCoil = 1352,
                        HomePriority = 3
                    },
                    new AxisConfig
                    {
                        AxisName = "Z1轴",
                        JogForwardCoil = 850,
                        JogReverseCoil = 860,
                        CurrentPosRegister = 800,
                        ManualSpeedRegister = 2550,
                        ManualSpeedIsInt16 = true,
                        AutoSpeedRegister = 2500,
                        TargetPosRegister = 2008,
                        AbsMoveRegister = 904,
                        AbsMoveIsCoil = true,
                        HomeCoil = 840,
                        EnableCoil = 1353,
                        HomePriority = 1
                    },
                    new AxisConfig
                    {
                        AxisName = "Z2轴",
                        JogForwardCoil = 851,
                        JogReverseCoil = 861,
                        CurrentPosRegister = 802,
                        ManualSpeedRegister = 2551,
                        ManualSpeedIsInt16 = true,
                        AutoSpeedRegister = 2502,
                        TargetPosRegister = 2058,
                        AbsMoveRegister = 914,
                        AbsMoveIsCoil = true,
                        HomeCoil = 841,
                        EnableCoil = 1354,
                        HomePriority = 1
                    }
                },
                Claws = new()
                {
                    new ClawModbusConfig
                    {
                        ClawName = "夹爪1",
                        OpenCoil = 890,
                        CloseCoil = 891,
                        RotateCoil = 893,
                        OpenPosRegister = 130,
                        AngleRegister = 136,
                        CloseTorqueRegister = 0,
                        OpenTorqueRegister = 0
                    }
                }
            };
        }
    }
}
