# PointPositionApp 项目分析报告

**分析日期：** 2026-03-22

---

## 项目概览

| 项目 | 说明 |
|------|------|
| **名称** | PointPositionApp |
| **类型** | WPF 桌面应用 (.NET 8.0) |
| **架构** | MVVM（手动实现，未用框架的 ViewModel 基类） |
| **用途** | **工业设备点位示教系统** — 通过 Modbus TCP 连接汇川 PLC，管理多轴运动控制和点位坐标数据 |

---

## 技术栈

| 依赖 | 用途 |
|------|------|
| **NModbus** | Modbus TCP 通信（连接汇川 Easy 系列 PLC） |
| **Microsoft.Data.Sqlite + Dapper** | 本地 SQLite 数据库存储 |
| **Newtonsoft.Json** | JSON 配置文件读写 |
| **NLog** | 日志记录 |
| **CommunityToolkit.Mvvm** | 已引用但未实际使用（自定义了 RelayCommand） |

---

## 项目结构

```
PointPositionApp/
├── Models/Models.cs         — 所有数据模型（Project, Module, Tray, Region, PointPosition, AxisConfig 等）
├── ViewModels/MainViewModel.cs — 主视图模型（轴控制、点位管理、树形导航）
├── Views/
│   ├── MainWindow.xaml.cs   — 主窗口事件处理（点动、绝对运动、网格点击）
│   └── SettingsWindow.xaml.cs — 设置窗口（PLC IP/端口、轮询周期、轴地址映射展示）
├── Services/
│   ├── ModbusService.cs     — Modbus TCP 通信封装（线圈/寄存器读写、轴控制、夹爪控制）
│   ├── DatabaseService.cs   — SQLite 数据库操作（CRUD、演示数据初始化）
│   └── ConfigService.cs     — JSON 配置文件管理
└── Helpers/Helpers.cs       — RelayCommand、AsyncRelayCommand、值转换器
```

---

## 核心业务逻辑

### 1. 多轴运动控制（5轴 + 夹爪）
- **X/Y/R/Z1/Z2** 五轴，通过 Modbus TCP 控制
- 支持：点动（Jog）、绝对运动、回原点、轴使能
- Z轴回原点优先级高于 XY（`HomePriority`），防止碰撞
- 安全跳转逻辑：先抬Z轴 → 移动XY/R → 再降Z轴

### 2. 点位管理
- 树形结构：**项目 → 模块/托盘 → 区域**
- 每个模块/托盘/区域都有行列网格，每个格子可保存5轴坐标
- 支持"复制行"功能（第1行点位按行间距偏移复制到其他行）

### 3. 数据层
- 6张表：`Project`, `Module`, `Tray`, `TrayCoordinateRegion`, `PointPosition`, `ClawInfo`
- 多态关联：`PointPosition` 通过 `OwnerType + OwnerId` 关联到 Module/Tray/Region
- 启动时自动插入演示数据

### 4. PLC通信细节
- 汇川 PLC 地址映射：M 线圈直接映射，D 寄存器直接映射
- Float32 采用 Little-Endian word order
- 轮询防重入（`Interlocked.CompareExchange`）
- 连续通信错误超阈值自动断开

---

## 值得注意的设计点

1. **CommunityToolkit.Mvvm 引用但未使用** — 自定义了 `RelayCommand` 和 `AsyncRelayCommand`，可以考虑统一使用 Toolkit 提供的版本
2. **`AsyncRelayCommand.Execute` 是 `async void`** — 虽然内部有 try-catch，但这是已知的 WPF ICommand 限制，处理方式是合理的
3. **`AbsoluteMoveAsync` 和 `AbsoluteMoveSyncAsync` 功能重复** — `ModbusService.cs:316-341`，两个方法逻辑完全相同
4. **数据库操作使用 `SemaphoreSlim` 串行化** — 保证了线程安全，但所有DB操作串行执行
5. **跳转等待使用固定 `Task.Delay`** — 而非读取轴到位信号，实际生产中可能不够可靠
