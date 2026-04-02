# 汇川 Easy 系列 PLC 设置指南

本文档详细说明如何配置汇川 Easy521/Easy523 PLC 以配合点位配置软件使用。

---

## 一、硬件准备

### 1.1 所需设备
- 汇川 Easy521 或 Easy523 PLC 一台
- 网线（用于 Modbus TCP 通信）
- 伺服驱动器 + 伺服电机（按轴数配置）
- 24V 直流电源
- 电脑（运行点位配置软件）

### 1.2 网络连接
将 PLC 与电脑连接到同一局域网：
- **直连方式**：电脑网口 ↔ 网线 ↔ PLC 网口
- **交换机方式**：电脑和 PLC 都接入同一交换机

---

## 二、PLC 网络配置

### 2.1 PLC 默认 IP
汇川 Easy 系列出厂默认 IP 通常为 `192.168.1.1`。

### 2.2 修改 PLC IP 地址
使用汇川 **AutoShop** 或 **InoProShop** 编程软件：

1. 打开编程软件，通过 USB 或网口连接 PLC
2. 进入 **系统配置** → **以太网设置**
3. 设置 PLC 的 IP 地址，例如：
   - IP 地址: `192.168.1.100`
   - 子网掩码: `255.255.255.0`
   - 网关: `192.168.1.1`
4. 下载配置到 PLC 并重启

### 2.3 电脑网络配置
确保电脑与 PLC 在同一网段：
- 电脑 IP 示例: `192.168.1.26`（或同网段的任意地址）
- 子网掩码: `255.255.255.0`

### 2.4 验证连通性
在命令行中 ping PLC：
```
ping 192.168.1.100
```
收到回复说明网络连通。

---

## 三、PLC 程序配置

### 3.1 Modbus TCP 通信设置
汇川 Easy521/Easy523 内置 Modbus TCP Server 功能，需在 PLC 程序中启用：

1. 在 AutoShop 中打开 PLC 项目
2. 进入 **通信配置** → **Modbus TCP**
3. 确认以下设置：
   - **Modbus TCP Server**: 启用
   - **端口号**: 502（默认）
   - **站号（SlaveId）**: 1

### 3.2 地址映射关系
汇川 Easy 系列的 Modbus 地址映射规则：

| PLC 地址类型 | Modbus 功能码 | 软件中的标记 | 说明 |
|-------------|-------------|------------|------|
| M（线圈）    | 01/05/15    | M+地址     | 开关量，如 M1450 |
| D（数据寄存器）| 03/06/16   | D+地址     | 数值量，如 D1000 |

**重要说明**：
- **线圈（M）**：用于触发动作，如点动、回零、使能
- **寄存器（D）**：用于读写数值，如位置、速度
- **Float 数据**：占用 2 个连续寄存器（32位），如 D1000-D1001
- **字节序**：汇川 PLC 使用 **Little-Endian（低字在前）** 字节序

### 3.3 伺服轴程序要求
PLC 程序中需要为每个伺服轴实现以下功能：

#### 点动（Jog）
```
当 M1450 = ON 时，X轴正向点动，速度读取 D1500（Float）
当 M1400 = ON 时，X轴反向点动，速度读取 D1500（Float）
当线圈断开（OFF）时，停止点动
```

#### 绝对运动
```
方式1 - 寄存器触发（AbsMoveIsCoil = false）：
  1. 软件写入目标位置到 D3008（Float）
  2. 软件写入自动速度到 D4000（Float）
  3. 软件写入触发值 16 到 D1200（UInt16）
  4. PLC 检测到 D1200 = 16 后启动绝对定位
  5. 运动完成后 PLC 将 D1200 清零

方式2 - 线圈触发（AbsMoveIsCoil = true，用于Z轴）：
  1. 软件写入目标位置到 D2008（Float）
  2. 软件写入自动速度到 D2500（Float）
  3. 软件写 M904 = ON（脉冲触发）
  4. PLC 检测到 M904 上升沿后启动绝对定位
```

#### 回零（Home）
```
当 M1300 = ON 时，X轴执行回原点动作
PLC 完成回零后将当前位置清零
```

#### 使能（Enable）
```
当 M1350 = ON 时，X轴伺服使能（上电）
当 M1350 = OFF 时，X轴伺服关闭
```

#### 当前位置反馈
```
PLC 持续将当前位置写入 D1000（Float，2个寄存器）
软件每 300ms 轮询读取一次
```

---

## 四、appsettings.json 配置说明

### 4.1 完整配置示例

```json
{
  "DatabasePath": "pointposition.db",
  "SimulationMode": false,
  "PlcIpAddress": "192.168.1.100",
  "PlcPort": 502,
  "PollingIntervalMs": 300,
  "LogLevel": "Info",
  "SlaveId": 1,
  "ConnectTimeoutMs": 3000,
  "ReadWriteTimeoutMs": 2000,
  "MaxCommErrors": 3,
  "ModbusRetries": 2,
  "Axes": [
    {
      "AxisName": "X轴",
      "JogForwardCoil": 1450,
      "JogReverseCoil": 1400,
      "CurrentPosRegister": 1000,
      "ManualSpeedRegister": 1500,
      "AutoSpeedRegister": 4000,
      "TargetPosRegister": 3008,
      "AbsMoveRegister": 1200,
      "HomeCoil": 1300,
      "EnableCoil": 1350,
      "AbsMoveIsCoil": false,
      "AbsMoveValue": 16,
      "ManualSpeedIsInt16": false,
      "HomePriority": 2,
      "SoftLimitMin": -10.0,
      "SoftLimitMax": 600.0,
      "SoftLimitEnabled": true
    },
    {
      "AxisName": "Y轴",
      "JogForwardCoil": 1451,
      "JogReverseCoil": 1401,
      "CurrentPosRegister": 1002,
      "ManualSpeedRegister": 1502,
      "AutoSpeedRegister": 4100,
      "TargetPosRegister": 3108,
      "AbsMoveRegister": 1210,
      "HomeCoil": 1301,
      "EnableCoil": 1351,
      "AbsMoveIsCoil": false,
      "AbsMoveValue": 16,
      "ManualSpeedIsInt16": false,
      "HomePriority": 2,
      "SoftLimitMin": -10.0,
      "SoftLimitMax": 400.0,
      "SoftLimitEnabled": true
    },
    {
      "AxisName": "R轴",
      "JogForwardCoil": 1452,
      "JogReverseCoil": 1402,
      "CurrentPosRegister": 1004,
      "ManualSpeedRegister": 1504,
      "AutoSpeedRegister": 4200,
      "TargetPosRegister": 3208,
      "AbsMoveRegister": 1220,
      "HomeCoil": 1302,
      "EnableCoil": 1352,
      "AbsMoveIsCoil": false,
      "AbsMoveValue": 16,
      "ManualSpeedIsInt16": false,
      "HomePriority": 3,
      "SoftLimitMin": -360.0,
      "SoftLimitMax": 360.0,
      "SoftLimitEnabled": true
    },
    {
      "AxisName": "Z1轴",
      "JogForwardCoil": 850,
      "JogReverseCoil": 860,
      "CurrentPosRegister": 800,
      "ManualSpeedRegister": 2550,
      "AutoSpeedRegister": 2500,
      "TargetPosRegister": 2008,
      "AbsMoveRegister": 904,
      "HomeCoil": 840,
      "EnableCoil": 1353,
      "AbsMoveIsCoil": true,
      "AbsMoveValue": 16,
      "ManualSpeedIsInt16": true,
      "HomePriority": 1,
      "SoftLimitMin": -200.0,
      "SoftLimitMax": 5.0,
      "SoftLimitEnabled": true
    },
    {
      "AxisName": "Z2轴",
      "JogForwardCoil": 851,
      "JogReverseCoil": 861,
      "CurrentPosRegister": 802,
      "ManualSpeedRegister": 2551,
      "AutoSpeedRegister": 2502,
      "TargetPosRegister": 2058,
      "AbsMoveRegister": 914,
      "HomeCoil": 841,
      "EnableCoil": 1354,
      "AbsMoveIsCoil": true,
      "AbsMoveValue": 16,
      "ManualSpeedIsInt16": true,
      "HomePriority": 1,
      "SoftLimitMin": -200.0,
      "SoftLimitMax": 5.0,
      "SoftLimitEnabled": true
    }
  ],
  "Claws": [
    {
      "ClawName": "夹爪1",
      "OpenCoil": 890,
      "CloseCoil": 891,
      "RotateCoil": 893,
      "OpenPosRegister": 130,
      "AngleRegister": 136,
      "CloseTorqueRegister": 0,
      "OpenTorqueRegister": 0
    }
  ],
  "MaxManualSpeed": 200.0,
  "MaxAutoSpeed": 500.0,
  "SafeZHeight": 0.0,
  "MotionTimeoutMs": 30000,
  "PositionTolerance": 0.5,
  "RequireEnableBeforeMotion": true
}
```

### 4.2 关键参数说明

#### 通信参数

| 参数 | 说明 | 建议值 |
|------|------|--------|
| `SimulationMode` | 模拟模式，true 时无需真实 PLC | 真机测试设为 `false` |
| `PlcIpAddress` | PLC 的 IP 地址 | 与 PLC 实际配置一致 |
| `PlcPort` | Modbus TCP 端口 | `502`（标准端口） |
| `SlaveId` | Modbus 从站号 | `1`（与 PLC 设置一致） |
| `PollingIntervalMs` | 位置轮询间隔（毫秒） | `300`（通常 200-500） |
| `ConnectTimeoutMs` | 连接超时 | `3000` |
| `ReadWriteTimeoutMs` | 读写超时 | `2000` |
| `MaxCommErrors` | 最大连续通信错误数，超过后断开 | `3` |
| `ModbusRetries` | 读写失败重试次数 | `2` |

#### 轴配置参数

| 参数 | 说明 |
|------|------|
| `AxisName` | 轴名称，用于界面显示 |
| `JogForwardCoil` | 点动正向线圈地址（M地址） |
| `JogReverseCoil` | 点动反向线圈地址（M地址） |
| `CurrentPosRegister` | 当前位置寄存器（D地址，Float占2个） |
| `ManualSpeedRegister` | 手动速度寄存器（D地址） |
| `AutoSpeedRegister` | 自动速度寄存器（D地址） |
| `TargetPosRegister` | 目标位置寄存器（D地址，Float占2个） |
| `AbsMoveRegister` | 绝对运动触发地址 |
| `HomeCoil` | 回零线圈地址（M地址） |
| `EnableCoil` | 使能线圈地址（M地址） |
| `AbsMoveIsCoil` | 绝对运动触发方式：`true`=线圈触发，`false`=寄存器写值触发 |
| `AbsMoveValue` | 寄存器触发时写入的值（默认16） |
| `ManualSpeedIsInt16` | 手动速度数据类型：`true`=Int16，`false`=Float32 |
| `HomePriority` | 回零优先级，数值越小越先执行（Z轴设为1优先抬起） |
| `SoftLimitMin/Max` | 软件限位范围（mm） |
| `SoftLimitEnabled` | 是否启用软件限位 |

#### 安全参数

| 参数 | 说明 | 建议值 |
|------|------|--------|
| `MaxManualSpeed` | 手动点动最大速度（mm/s） | `200` |
| `MaxAutoSpeed` | 自动运动最大速度（mm/s） | `500` |
| `SafeZHeight` | Z轴安全高度（mm），跳转点位时先抬到此高度 | `0`（根据实际调整） |
| `MotionTimeoutMs` | 运动超时时间（ms） | `30000` |
| `PositionTolerance` | 到位判定容差（mm） | `0.5` |
| `RequireEnableBeforeMotion` | 运动前是否检查使能 | `true` |

---

## 五、地址对照表（默认配置）

### 5.1 X/Y/R 轴（伺服轴，Float 速度）

| 功能 | X轴 | Y轴 | R轴 |
|------|-----|-----|-----|
| 点动正向 | M1450 | M1451 | M1452 |
| 点动反向 | M1400 | M1401 | M1402 |
| 当前位置 | D1000 | D1002 | D1004 |
| 手动速度 | D1500 | D1502 | D1504 |
| 自动速度 | D4000 | D4100 | D4200 |
| 目标位置 | D3008 | D3108 | D3208 |
| 绝对运动触发 | D1200(写16) | D1210(写16) | D1220(写16) |
| 回零 | M1300 | M1301 | M1302 |
| 使能 | M1350 | M1351 | M1352 |

### 5.2 Z1/Z2 轴（步进轴，Int16 速度，线圈触发）

| 功能 | Z1轴 | Z2轴 |
|------|------|------|
| 点动正向 | M850 | M851 |
| 点动反向 | M860 | M861 |
| 当前位置 | D800 | D802 |
| 手动速度 | D2550(Int16) | D2551(Int16) |
| 自动速度 | D2500 | D2502 |
| 目标位置 | D2008 | D2058 |
| 绝对运动触发 | M904 | M914 |
| 回零 | M840 | M841 |
| 使能 | M1353 | M1354 |

### 5.3 夹爪

| 功能 | 地址 |
|------|------|
| 打开 | M890 |
| 关闭 | M891 |
| 旋转 | M893 |
| 打开位置 | D130 |
| 角度 | D136 |

---

## 六、调试排查步骤

### 6.1 连接不上 PLC
1. 确认网线连接正常
2. `ping` PLC 的 IP 地址
3. 确认 `appsettings.json` 中 `PlcIpAddress` 和 `PlcPort` 正确
4. 确认 `SimulationMode` 为 `false`
5. 检查防火墙是否放行了 502 端口
6. 确认 PLC 的 Modbus TCP Server 已启用

### 6.2 连接成功但无动作
1. 检查日志文件 `logs/PointPosition_*.log` 中的通信记录
2. 确认 PLC 程序已下载并运行（RUN 状态）
3. 确认轴已**使能**（界面上勾选使能，且 PLC 侧伺服已上电）
4. 用 AutoShop 的**监控功能**检查：
   - 软件写入线圈后，PLC 侧对应的 M 地址是否变化
   - 软件写入寄存器后，PLC 侧对应的 D 地址是否有值
5. 检查 Modbus 地址映射：
   - `appsettings.json` 中的地址必须与 PLC 程序中的地址**完全一致**
   - 注意 Float 数据占 2 个寄存器（D1000 实际占 D1000 和 D1001）

### 6.3 位置读取异常（显示 0 或乱码）
1. 确认 PLC 程序正确地将当前位置写入了对应的 D 寄存器
2. 检查字节序：汇川使用 Little-Endian（低字在前）
3. 在 AutoShop 中手动写入一个已知值（如 D1000 = 123.456），看软件是否正确读取

### 6.4 点动无反应
1. 确认使能线圈已写入（日志中应有 "写线圈 M1350 = True"）
2. 确认点动线圈地址正确（日志中应有 "写线圈 M1450 = True"）
3. 检查 PLC 程序是否处理了对应的点动逻辑
4. 确认伺服驱动器无报警

### 6.5 绝对运动无反应
1. 检查日志确认目标位置和触发信号已写入
2. 区分触发方式：
   - X/Y/R 轴使用**寄存器触发**（写值16到 D1200）
   - Z1/Z2 轴使用**线圈触发**（M904 = ON）
3. 在 AutoShop 中监控触发寄存器/线圈是否收到信号

---

## 七、快速上手清单

- [ ] PLC 已上电并处于 RUN 状态
- [ ] PLC IP 地址已配置（如 192.168.1.100）
- [ ] 电脑与 PLC 网络互通（ping 成功）
- [ ] PLC 程序已下载，包含 Modbus 线圈/寄存器处理逻辑
- [ ] `appsettings.json` 中 `SimulationMode` 设为 `false`
- [ ] `appsettings.json` 中 `PlcIpAddress` 与 PLC 实际 IP 一致
- [ ] Modbus 地址与 PLC 程序中的地址一一对应
- [ ] 伺服驱动器无报警
- [ ] 软件中点击"连接PLC"，状态栏显示"PLC已连接"
- [ ] 勾选轴使能后，尝试点动操作
