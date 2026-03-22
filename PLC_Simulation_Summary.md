# PLC 模拟模式实现总结

## 背景

项目在没有真实 PLC 硬件的情况下无法进行坐标定位功能测试。通过引入模拟模式，所有 UI 交互和点位操作均可在无硬件环境下完整运行。

## 改动文件

| 文件 | 改动说明 |
|------|----------|
| `Services/ModbusService.cs` | 关键方法改为 `virtual`，`_isConnected` 改为 `protected`，添加 `OnConnectionStateChanged` 方法供子类调用 |
| `Services/SimulationModbusService.cs` | **新建** — 继承 `ModbusService`，使用内存寄存器 + 运动模拟引擎 |
| `Models/Models.cs` | `AppSettings` 增加 `SimulationMode` 属性 |
| `ViewModels/MainViewModel.cs` | 根据 `SimulationMode` 配置自动选择真实/模拟服务实例 |
| `Views/SettingsWindow.xaml` | 设置界面增加"启用 PLC 模拟"复选框 |
| `Views/SettingsWindow.xaml.cs` | 加载/保存 `SimulationMode` 配置 |
| `appsettings.json` | 新增 `"SimulationMode": true` 字段（默认开启） |

## 模拟行为说明

### 连接
- 调用 `ConnectAsync()` 立即返回成功，无需真实 IP/端口
- 状态栏显示 "PLC已连接（模拟模式）"

### 点动（Jog）
- 按住点动按钮时，轴以当前手动速度持续匀速移动
- 松开按钮立即停止
- 正向/反向分别增加/减少坐标值

### 绝对运动（Absolute Move）
- 写入目标位置后触发，轴以自动速度平滑过渡到目标位置
- 到达目标后自动停止
- 支持线圈触发和寄存器触发两种方式

### 回原点（Home）
- 触发后轴平滑归零（目标位置设为 0）

### 位置轮询
- 模拟引擎每 50ms 刷新一次轴位置
- UI 轮询（默认 300ms）正常读取模拟坐标并刷新显示

### 点位保存
- 从模拟轴坐标读取当前位置，保存到 SQLite 数据库
- 与真实硬件行为完全一致

### 夹爪
- 打开/关闭/旋转指令即时完成（写入内存寄存器）

## 技术实现

### SimulationModbusService 架构

```
SimulationModbusService : ModbusService
├── 内存存储
│   ├── ConcurrentDictionary<ushort, bool>   _coils         （线圈）
│   ├── ConcurrentDictionary<ushort, float>  _floatRegisters （浮点寄存器）
│   └── ConcurrentDictionary<ushort, short>  _int16Registers （整数寄存器）
├── 运动引擎
│   ├── Timer _simTimer (50ms 周期)
│   ├── MotionState（每轴独立状态：Idle/JogForward/JogReverse/Absolute）
│   └── SimulationTick() — 根据速度和方向更新位置
└── 重写方法
    ├── ConnectAsync() / Disconnect()
    ├── WriteCoil() / ReadCoil()
    ├── WriteFloat() / ReadFloat()
    ├── WriteInt16() / ReadInt16()
    └── WriteUInt16()
```

### 运动逻辑
- 每个 Tick（50ms）根据当前模式计算位移: `位移 = 速度 × 时间间隔`
- 绝对运动在距离目标 ≤ 单步位移时直接到位
- 最低保底速度 10 mm/s，避免速度为 0 时轴不动

## 使用方法

1. 编辑 `appsettings.json`，设置 `"SimulationMode": true`（或在设置界面勾选）
2. 启动应用，点击 **连接 PLC**
3. 正常使用点动、绝对运动、保存点位、跳转点位等全部功能

> **注意**: 切换模拟/真实模式后需要重启应用，因为 `ModbusService` 实例在 `MainViewModel` 构造时确定。
