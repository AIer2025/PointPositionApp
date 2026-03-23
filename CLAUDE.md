# PointPositionApp

## 项目概述
WPF 桌面应用，用于 PLC 点位管理与控制。通过 Modbus TCP 协议与汇川 PLC（Easy521/Easy523）通信，实现多轴点位的示教、存储和运动控制。

## 技术栈
- **框架**: .NET 8.0 + WPF
- **语言**: C#
- **架构**: MVVM（CommunityToolkit.Mvvm）
- **数据库**: SQLite（Microsoft.Data.Sqlite + Dapper）
- **通信**: Modbus TCP（NModbus）
- **日志**: NLog
- **配置**: Newtonsoft.Json

## 项目结构
```
Models/Models.cs          — 数据模型（轴、点位、配置等）
ViewModels/MainViewModel.cs — 主视图模型，核心业务逻辑
Views/                    — WPF 界面（MainWindow、SettingsWindow、Styles）
Services/
  ModbusService.cs        — Modbus TCP 通信服务
  SimulationModbusService.cs — PLC 模拟服务（无硬件调试用）
  DatabaseService.cs      — SQLite 数据访问
  ConfigService.cs        — JSON 配置读写
Helpers/                  — 辅助工具类
```

## 构建与运行
```bash
dotnet build
dotnet run
```

## 编码规范
- 中文注释
- 命名空间: PointPositionApp.*
- 异步方法以 Async 结尾
- Modbus 方法使用 virtual 修饰，支持模拟服务重写

## 注意事项
- 支持模拟模式（SimulationMode），通过 appsettings.json 中的 SimulationMode 字段切换
- 目标硬件: 汇川 Easy521、Easy523 PLC
- 数据库文件: pointposition.db（SQLite）
- 日志配置: NLog.config
