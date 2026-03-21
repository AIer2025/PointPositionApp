# 点位配置软件 (PointPositionApp)

SmartLabOS 实验室自动化点位配置工具 — C# WPF + SQLite + Modbus TCP

## 📋 功能概览

| 功能模块 | 说明 |
|---------|------|
| 项目树形导航 | 项目 → 模块/托盘 → 区域，三级树形浏览 |
| 点位网格视图 | 按行列动态生成网格，支持鼠标选中、右键菜单 |
| 轴控制面板 | X/Y/R/Z1/Z2 五轴点动、绝对运动、回原点、使能 |
| 夹爪控制 | 打开/关闭/旋转，参数可配 |
| 点位保存 | 当前坐标一键保存到数据库，支持批量行复制 |
| Modbus TCP | 通过 NModbus4 与汇川 Easy PLC 通信 |
| 配置管理 | JSON 配置文件，PLC地址映射、数据库路径、轮询周期 |

## 🏗 项目结构

```
PointPositionApp/
├── App.xaml / App.xaml.cs          # 应用入口 + NLog 配置
├── PointPositionApp.csproj         # .NET 8 项目文件
├── PointPositionApp.sln            # 解决方案文件
├── NLog.config                     # 日志配置
│
├── Models/
│   └── Models.cs                   # 数据模型(Project/Module/Tray/PointPosition/AxisConfig等)
│
├── Services/
│   ├── DatabaseService.cs          # SQLite 数据库服务 (Dapper ORM)
│   ├── ModbusService.cs            # Modbus TCP 通信服务
│   └── ConfigService.cs            # JSON 配置读写
│
├── ViewModels/
│   └── MainViewModel.cs            # 主视图模型 (MVVM)
│
├── Views/
│   ├── Styles.xaml                 # 深色主题样式资源
│   ├── MainWindow.xaml / .cs       # 主窗口
│   └── SettingsWindow.xaml / .cs   # 设置窗口
│
├── Helpers/
│   └── Helpers.cs                  # RelayCommand + 值转换器
│
└── Converters/                     # (转换器包含在 Helpers.cs 中)
```

## 🔧 运行环境

- **.NET 8.0** (SDK 8.0+)
- **Windows** (WPF 框架)
- **Visual Studio 2022** 或 `dotnet` CLI

## 📦 NuGet 依赖

| 包名 | 版本 | 用途 |
|------|------|------|
| Microsoft.Data.Sqlite | 8.0.8 | SQLite 驱动 |
| Dapper | 2.1.35 | 轻量ORM |
| NModbus4 | 2.1.0 | Modbus TCP 通信 |
| Newtonsoft.Json | 13.0.3 | JSON 序列化 |
| NLog | 5.3.2 | 日志框架 |
| CommunityToolkit.Mvvm | 8.2.2 | MVVM 支持 |

## 🚀 构建与运行

```bash
# 还原依赖
dotnet restore

# 编译
dotnet build

# 运行
dotnet run
```

或在 Visual Studio 中打开 `PointPositionApp.sln` → F5 运行。

## ⚙ 配置说明

首次运行自动生成 `appsettings.json`，包含：

```json
{
  "DatabasePath": "pointposition.db",
  "PlcIpAddress": "192.168.1.100",
  "PlcPort": 502,
  "PollingIntervalMs": 300,
  "Axes": [ ... ],   // 五轴 Modbus 地址映射
  "Claws": [ ... ]    // 夹爪 Modbus 地址映射
}
```

### 轴信号地址映射（默认值）

| 轴 | 点动正向 | 点动反向 | 当前位置 | 手动速度 | 自动速度 | 目标位置 | 触发 | 回零 | 使能 |
|----|---------|---------|---------|---------|---------|---------|------|------|------|
| X  | M1450 | M1400 | D1000 | D1500 | D4000 | D3008 | D1200(=16) | M1300 | M1350 |
| Y  | M1451 | M1401 | D1002 | D1502 | D4100 | D3108 | D1210(=16) | M1301 | M1351 |
| R  | M1452 | M1402 | D1004 | D1504 | D4200 | D3208 | D1220(=16) | M1302 | M1352 |
| Z1 | M850  | M860  | D800  | D2550(i16) | D2500 | D2008 | M904 | M840 | M1353 |
| Z2 | M851  | M861  | D802  | D2551(i16) | D2502 | D2058 | M914 | M841 | M1354 |

## 📊 数据库表结构

SQLite 数据库自动初始化，包含以下表：

- **Project** — 项目
- **Module** — 模块(通道组)
- **Tray** — 托盘
- **TrayCoordinateRegion** — 托盘坐标区域
- **ClawInfo** — 夹爪参数
- **PointPosition** — 点位数据（核心）

### 数据流

```
工控软件 ──写入──→ [Project/Module/Tray] ──读取──→ 点位软件
点位软件 ──写入──→ [PointPosition]       ──读取──→ 工控软件
```

## 🎨 界面说明

- **深色主题**，适合工控操作环境
- 左侧：项目树形导航
- 中间：点位网格（绿色=已保存，蓝色边框=选中）
- 右侧：轴控制 + 夹爪控制面板
- 底部：PLC/数据库连接状态 + 系统时钟

## 📝 操作流程

1. 启动软件，自动连接 SQLite 数据库并加载项目树
2. 点击「连接PLC」建立 Modbus TCP 连接
3. 在左侧树中选择模块或托盘，中间自动生成点位网格
4. 使用右侧轴控制面板（点动/绝对运动）将机械臂移动到目标位置
5. 在网格中选择目标单元格，点击「保存点位」
6. 重复 4-5 步完成所有点位配置
7. 可使用「批量复制」将第一行快速复制到其他行

## ⚠ 注意事项

- 回原点操作中，Z1/Z2 轴优先执行（HomePriority=1）
- 点动控制为长按触发，松开即停
- PLC 通信超时默认 2 秒，可在代码中调整
- 首次运行会插入演示数据（1个项目 + 1个模块 + 1个96孔板托盘）
