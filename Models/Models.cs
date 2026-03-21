using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PointPositionApp.Models
{
    /// <summary>OwnerType 常量，避免魔术字符串</summary>
    public static class OwnerTypes
    {
        public const string Module = "Module";
        public const string Tray = "Tray";
        public const string Region = "Region";
    }

    /// <summary>项目</summary>
    public class Project
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreateTime { get; set; } = DateTime.Now;

        // 导航属性（非数据库字段）
        public List<Module> Modules { get; set; } = new();
        public List<Tray> Trays { get; set; } = new();
    }

    /// <summary>模块（通道组）</summary>
    public class Module
    {
        public int ModuleId { get; set; }
        public int ProjectId { get; set; }
        public string ChannelGroupName { get; set; } = string.Empty;
        public bool RowFirst { get; set; } = true;
        public int Rows { get; set; }
        public int Cols { get; set; }
        public double SpaceRow { get; set; }
        public double SpaceCol { get; set; }
        public double StartPosition_X { get; set; }
        public double StartPosition_Y { get; set; }
        public double StartPosition_Z { get; set; }
        public double StartPosition_Z1 { get; set; }
        public double StartPosition_R { get; set; }
        public int? ClawInfoId { get; set; }

        public ClawInfo? ClawInfo { get; set; }
    }

    /// <summary>托盘</summary>
    public class Tray
    {
        public int TrayId { get; set; }
        public int ProjectId { get; set; }
        public string LabTrayCode { get; set; } = string.Empty;
        public string LabTrayCategory { get; set; } = string.Empty;
        public string LabTrayName { get; set; } = string.Empty;
        public string? LabTrayDescription { get; set; }
        public int Rows { get; set; }
        public int Cols { get; set; }
        public double SpaceRow { get; set; }
        public double SpaceCol { get; set; }
        public double WellDiameter { get; set; }
        public double LiquidStep { get; set; }
        public string? RowLabels { get; set; }  // JSON数组
        public string? ColLabels { get; set; }  // JSON数组

        public List<TrayCoordinateRegion> Regions { get; set; } = new();
    }

    /// <summary>托盘坐标区域</summary>
    public class TrayCoordinateRegion
    {
        public int RegionId { get; set; }
        public int TrayId { get; set; }
        public string RegionName { get; set; } = string.Empty;
        public double StartPosition_X { get; set; }
        public double StartPosition_Y { get; set; }
        public double StartPosition_Z { get; set; }
        public double StartPosition_Z1 { get; set; }
        public double StartPosition_R { get; set; }
        public int Rows { get; set; }
        public int Cols { get; set; }
        public double SpaceRow { get; set; }
        public double SpaceCol { get; set; }
        public bool RowFirst { get; set; } = true;
        public bool HasCover { get; set; }
        public int CanAspirateCount { get; set; }
        public int CanDispenseCount { get; set; }
        public string? RowLabels { get; set; }
        public string? ColLabels { get; set; }
        public int? ClawInfoId { get; set; }

        public ClawInfo? ClawInfo { get; set; }
    }

    /// <summary>夹爪参数</summary>
    public class ClawInfo
    {
        public int ClawInfoId { get; set; }
        public string? Description { get; set; }
        public double OpenPos { get; set; }
        public double ClosePos { get; set; }
        public double Angle { get; set; }
        public double CloseTorque { get; set; }
        public double OpenTorque { get; set; }
    }

    /// <summary>点位数据</summary>
    public class PointPosition
    {
        public int PointId { get; set; }
        public string OwnerType { get; set; } = string.Empty; // Module / Tray / Region
        public int OwnerId { get; set; }
        public int RowIndex { get; set; }
        public int ColIndex { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Z1 { get; set; }
        public double R { get; set; }
        public int? ClawInfoId { get; set; }

        public ClawInfo? ClawInfo { get; set; }
    }

    /// <summary>轴配置（Modbus地址映射）</summary>
    public class AxisConfig
    {
        public string AxisName { get; set; } = string.Empty;

        // Modbus 地址（均为起始地址）
        public ushort JogForwardCoil { get; set; }      // 点动正向线圈
        public ushort JogReverseCoil { get; set; }       // 点动反向线圈
        public ushort CurrentPosRegister { get; set; }   // 当前位置寄存器
        public ushort ManualSpeedRegister { get; set; }  // 手动速度寄存器
        public ushort AutoSpeedRegister { get; set; }    // 自动速度寄存器
        public ushort TargetPosRegister { get; set; }    // 目标位置寄存器
        public ushort AbsMoveRegister { get; set; }      // 绝对运动触发
        public ushort HomeCoil { get; set; }             // 回原点线圈
        public ushort EnableCoil { get; set; }           // 轴使能线圈

        // 触发方式
        public bool AbsMoveIsCoil { get; set; }          // true=线圈触发, false=寄存器写值触发
        public ushort AbsMoveValue { get; set; } = 16;   // 寄存器触发时写入的值
        public bool ManualSpeedIsInt16 { get; set; }     // 手动速度是否为Int16

        /// <summary>回零优先级，数值小的先回（Z/Z1轴优先）</summary>
        public int HomePriority { get; set; }
    }

    /// <summary>夹爪Modbus配置</summary>
    public class ClawModbusConfig
    {
        public string ClawName { get; set; } = "夹爪1";
        public ushort OpenCoil { get; set; }
        public ushort CloseCoil { get; set; }
        public ushort RotateCoil { get; set; }
        public ushort OpenPosRegister { get; set; }
        public ushort AngleRegister { get; set; }
        public ushort CloseTorqueRegister { get; set; }
        public ushort OpenTorqueRegister { get; set; }
    }

    /// <summary>应用配置</summary>
    public class AppSettings
    {
        public string DatabasePath { get; set; } = "pointposition.db";
        public string PlcIpAddress { get; set; } = "192.168.1.100";
        public int PlcPort { get; set; } = 502;
        public int PollingIntervalMs { get; set; } = 300;
        public string LogLevel { get; set; } = "Info";

        // Modbus 通信参数
        public byte SlaveId { get; set; } = 1;
        public int ConnectTimeoutMs { get; set; } = 3000;
        public int ReadWriteTimeoutMs { get; set; } = 2000;
        public int MaxCommErrors { get; set; } = 3;
        public int ModbusRetries { get; set; } = 2;

        public List<AxisConfig> Axes { get; set; } = new();
        public List<ClawModbusConfig> Claws { get; set; } = new();
    }

    /// <summary>树节点类型</summary>
    public enum TreeNodeType
    {
        Project,
        Module,
        Tray,
        Region
    }

    /// <summary>树节点视图模型</summary>
    public class TreeNodeItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public TreeNodeType NodeType { get; set; }
        public int Id { get; set; }
        public object? Data { get; set; }
        public List<TreeNodeItem> Children { get; set; } = new();
    }

    /// <summary>网格单元格</summary>
    public class GridCell : INotifyPropertyChanged
    {
        public int RowIndex { get; set; }
        public int ColIndex { get; set; }
        public string Label { get; set; } = string.Empty;

        private bool _hasPoint;
        public bool HasPoint
        {
            get => _hasPoint;
            set { _hasPoint = value; OnPropertyChanged(); }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        private PointPosition? _point;
        public PointPosition? Point
        {
            get => _point;
            set { _point = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
