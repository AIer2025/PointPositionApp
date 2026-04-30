# 有关 appsettings.json 的设置

---

## 一、文件生成机制

### 1.1 自动生成
程序启动时，`ConfigService.Load()` 按以下流程处理配置文件：

```
程序启动
  ├─ appsettings.json 存在？
  │    ├─ 是 → 读取并反序列化
  │    │    ├─ 成功 → 检查 Axes/Claws 是否为空
  │    │    │    ├─ 为空 → 用默认值填充，保存并使用
  │    │    │    └─ 正常 → 保存（补全新增字段）并使用
  │    │    └─ 失败（格式错误/null） → 生成默认配置并覆盖
  │    └─ 否 → 生成默认配置并保存
  └─ 完成
```

**关键点**：
- 文件位置：**与 exe 同目录**（`AppDomain.CurrentDomain.BaseDirectory`）
- 首次运行会自动生成包含所有默认参数的 appsettings.json
- 每次加载成功后会回写文件，目的是**补全新版本新增的字段**
- 如果 JSON 格式损坏，程序会用默认配置覆盖原文件

### 1.2 文件位置
根据运行方式不同，appsettings.json 的实际位置不同：

| 运行方式 | 文件位置 |
|---------|---------|
| `dotnet run` / VS 调试 | `bin\Debug\net8.0-windows\appsettings.json` |
| Release 构建 | `bin\Release\net8.0-windows\appsettings.json` |
| 发布部署 | 与 `PointPositionApp.exe` 同目录 |

### 1.3 通过界面修改
点击软件顶部 **"设置"** 按钮可修改部分参数（IP、端口、模拟模式等），保存后会立即写入 appsettings.json。

---

## 二、需要修改的配置项

### 2.1 部署到新环境必须修改的项

| 参数 | 说明 | 默认值 | 修改场景 |
|------|------|--------|---------|
| `PlcIpAddress` | PLC 的 IP 地址 | `192.168.1.100` | **必须**改为实际 PLC 的 IP |
| `SimulationMode` | 模拟模式开关 | `false` | 真机测试设为 `false`，无硬件调试设为 `true` |

### 2.2 根据 PLC 程序调整的项

如果 PLC 程序中的 Modbus 地址与默认值不同，需要修改 `Axes` 数组中对应轴的地址：

```json
{
  "AxisName": "X轴",
  "JogForwardCoil": 1450,      // ← 改为 PLC 程序中实际的点动正向线圈地址
  "JogReverseCoil": 1400,      // ← 点动反向线圈地址
  "CurrentPosRegister": 1000,  // ← 当前位置寄存器地址（Float，占2个）
  "ManualSpeedRegister": 1500, // ← 手动速度寄存器地址
  "AutoSpeedRegister": 4000,   // ← 自动速度寄存器地址
  "TargetPosRegister": 3008,   // ← 目标位置寄存器地址（Float，占2个）
  "AbsMoveRegister": 1200,     // ← 绝对运动触发地址
  "HomeCoil": 1300,            // ← 回零线圈地址
  "EnableCoil": 1350           // ← 使能线圈地址
}
```

**这些地址必须与 PLC 程序中的地址完全一致，否则无法控制！**

### 2.3 通常不需要修改的项

| 参数 | 说明 | 默认值 | 何时修改 |
|------|------|--------|---------|
| `PlcPort` | Modbus TCP 端口 | `502` | PLC 使用非标准端口时 |
| `SlaveId` | Modbus 从站号 | `1` | PLC 从站号非 1 时 |
| `PollingIntervalMs` | 位置轮询间隔 | `300` | 需要更快/更慢刷新时 |
| `ConnectTimeoutMs` | 连接超时 | `3000` | 网络延迟大时适当增加 |
| `ReadWriteTimeoutMs` | 读写超时 | `2000` | 通信不稳定时适当增加 |
| `MaxCommErrors` | 最大连续错误数 | `3` | 网络不稳定时适当增加 |
| `ModbusRetries` | 读写重试次数 | `2` | 一般不需要改 |
| `DatabasePath` | 数据库文件路径 | `pointposition.db` | 需要指定其他路径时 |

### 2.4 安全参数

| 参数 | 说明 | 默认值 | 修改建议 |
|------|------|--------|---------|
| `MaxManualSpeed` | 手动点动最大速度（mm/s） | `200` | 根据设备实际安全速度设定 |
| `MaxAutoSpeed` | 自动运动最大速度（mm/s） | `500` | 根据设备实际安全速度设定 |
| `SafeZHeight` | Z轴安全高度（mm） | `0` | 跳转点位时 Z 轴先抬到此高度 |
| `MotionTimeoutMs` | 运动超时（ms） | `30000` | 长行程运动可适当增加 |
| `PositionTolerance` | 到位容差（mm） | `0.5` | 精度要求高时减小 |
| `RequireEnableBeforeMotion` | 运动前检查使能 | `true` | 建议保持 `true` |
| `SoftLimitMin` / `SoftLimitMax` | 各轴软限位范围 | 见默认配置 | **必须根据实际机械行程设定** |

---

## 三、使用注意事项

### 3.1 JSON 格式要求
- 必须是合法的 JSON 格式，注意**不能有多余逗号**、**不能用中文引号**
- 编码必须是 **UTF-8**
- 修改后可用在线 JSON 校验工具检查格式是否正确

**错误示例**：
```json
{
  "PlcIpAddress": "192.168.1.100",  // ← JSON 不支持注释！
  "PlcPort": 502,                   // ← 最后一项末尾不能有逗号
}
```

**正确示例**：
```json
{
  "PlcIpAddress": "192.168.1.100",
  "PlcPort": 502
}
```

### 3.2 Float 寄存器地址占位
Float32 数据占用**2个连续寄存器**。例如 `CurrentPosRegister: 1000` 实际占用 D1000 和 D1001。配置地址时注意不要让不同参数的寄存器范围重叠。

### 3.3 AbsMoveIsCoil 的区别
- `AbsMoveIsCoil: false`（X/Y/R 轴）：通过向寄存器写值触发绝对运动（D1200 写入 16）
- `AbsMoveIsCoil: true`（Z1/Z2 轴）：通过线圈上升沿触发绝对运动（M904 置 ON）

这取决于 PLC 程序的实现方式，必须与 PLC 侧一致。

### 3.4 ManualSpeedIsInt16 的区别
- `ManualSpeedIsInt16: false`（X/Y/R 轴）：手动速度以 Float32 格式写入（占2个寄存器）
- `ManualSpeedIsInt16: true`（Z1/Z2 轴）：手动速度以 Int16 格式写入（占1个寄存器）

这取决于 PLC 程序中速度寄存器的数据类型。

### 3.5 修改配置后需要重启
修改 appsettings.json 文件后，需要**重启程序**才能生效。通过界面"设置"按钮修改的参数会立即生效。

### 3.6 配置被覆盖的情况
以下情况会导致 appsettings.json 被覆盖：
1. **JSON 格式损坏** — 程序会用默认配置覆盖
2. **反序列化失败（返回 null）** — 同上
3. **正常加载** — 程序会回写文件以补全新版本新增的字段（已有的值不变）

建议修改配置前**备份一份**原文件。

### 3.7 发布部署清单
将程序发给同事部署时，确保以下文件在同一目录：

```
PointPositionApp.exe        ← 主程序
appsettings.json            ← 配置文件（首次运行自动生成，或预先配好）
NLog.config                 ← 日志配置
pointposition.db            ← 数据库（首次运行自动生成）
*.dll                       ← 依赖库
```

### 3.8 常见问题

**Q: 修改了 appsettings.json 但没生效？**
A: 确认修改的是 exe 同目录下的文件，而不是项目源码目录。重启程序后检查日志确认加载的路径。

**Q: 程序启动后 appsettings.json 被还原了？**
A: JSON 格式可能有误，程序无法解析会用默认值覆盖。检查日志中是否有"加载配置失败"或"反序列化返回null"的记录。

**Q: 轴数量能增减吗？**
A: 可以。在 `Axes` 数组中增删轴配置即可，界面会自动适应。但需要确保对应的 PLC 程序也支持。

**Q: Claws 配置为空会怎样？**
A: 程序会自动填充默认夹爪配置并记录警告日志。
