# Modbus TCP 模拟器安装与配置指南

## 概述

本应用支持两种 Modbus 测试方式：

| 方式 | 说明 | 适用场景 |
|-----|------|--------|
| **内置纯软件模拟** | SimulationMode = true，无需外部工具 | 快速开发、界面测试 |
| **外部模拟器工具** | 真实 Modbus TCP 服务，接近实际硬件 | 功能测试、协议验证、团队联调 |

**本指南主要介绍外部模拟器工具的安装和配置。**

---

## 方法一：内置纯软件模拟模式（推荐快速测试）

### 优点
- ✓ 无需安装任何外部工具
- ✓ 开箱即用
- ✓ 支持软限位、速度钳位等安全功能
- ✓ 模拟真实的轴运动

### 使用步骤

#### 1. 打开设置窗口
- 启动应用 → 点击 **⚙ 设置** 按钮

#### 2. 启用模拟模式
- 勾选 **☑ 启用模拟模式** 复选框
- 不需要修改 IP 和端口

#### 3. 保存设置
- 点击 **保存设置** 按钮
- 重启应用

#### 4. 测试
- 打开应用
- 点击 **🔌 连接PLC** 按钮
- 状态栏显示 "PLC已连接（模拟模式）"
- 可直接使用点动、绝对运动、跳转等所有功能

### 模拟器特性
- 模拟 5 个轴：X、Y、R、Z1、Z2
- 支持点动（Jog）、绝对运动（Absolute Move）、回原点（Home）
- 真实的运动衰减曲线、软限位保护
- 支持急停、位置反馈等安全功能

---

## 方法二：外部 Modbus TCP 模拟器工具

### 推荐工具对比

| 工具名 | 平台 | 难度 | 功能 | 费用 |
|-------|------|------|------|------|
| **ModBus Slave** | Windows | ⭐⭐ | 完整 | 免费 |
| **Modbus Poll** | Windows | ⭐⭐ | 完整 | ¥ 付费 |
| **QModMaster** | Windows/Linux | ⭐⭐⭐ | 专业 | 免费 |
| **Modscan32** | Windows | ⭐ | 简易 | 免费 |
| **Node-RED + Modbus** | 跨平台 | ⭐⭐⭐⭐ | 高级 | 免费 |

**推荐初学者使用：ModBus Slave（最简单，功能完整）**

---

## 详细安装步骤

### A. ModBus Slave（推荐）

#### 下载
1. 访问 [官方网站](https://www.modbustools.com/)
2. 下载版本：ModBus Slave v7 或 v8
3. 或直接搜索 "ModBus Slave free download"

#### 安装
1. 双击 ModBusSlaveSetup.exe
2. 跟随安装向导完成安装
3. 启动应用

#### 基础配置

**① 启动 Modbus TCP 服务**

启动 ModBus Slave 后，菜单栏找到：
```
Connection → Connect → TCP
```

出现对话框，配置如下：

| 项 | 值 | 说明 |
|----|-----|------|
| Protocol | TCP/IP | 选择 TCP/IP（Modbus TCP） |
| Network Adapter | 127.0.0.1 或本机IP | 127.0.0.1 = localhost |
| Port | 502 | 标准 Modbus TCP 端口 |
| Device ID | 1 | Modbus 从站地址 |

点击 **OK** 启动服务

**② 初始化寄存器**

在 ModBus Slave 的表格区域，可看到多个区域的寄存器：

- **Coils (0xxxx)**：线圈（1位，读写）→ 用于控制信号
- **Discrete Inputs (1xxxx)**：离散输入（1位，只读）
- **Holding Registers (4xxxx)**：保持寄存器（16位，读写）→ **用于轴位置、速度**
- **Input Registers (3xxxx)**：输入寄存器（16位，只读）

**需要初始化的寄存器**（参考应用配置）：

```
保持寄存器 (Holding Registers 4xxxx)：
├─ 40000-40004  : X轴 相关参数（CtrlReg, MoveReg, SpeedReg等）
├─ 40010-40014  : Y轴 相关参数
├─ 40020-40024  : R轴 相关参数
├─ 40030-40034  : Z1轴 相关参数
├─ 40040-40044  : Z2轴 相关参数
└─ 40100+      : 其他全局参数

输入寄存器 (Input Registers 3xxxx)：
├─ 30000-30004  : X轴 位置反馈、状态
├─ 30010-30014  : Y轴 位置反馈、状态
└─ ...
```

**③ 设置初始值（模拟轴当前位置）**

在 ModBus Slave 中：
1. 找到要修改的寄存器行
2. 双击 Value 列，输入数值
3. 例如：设置 X 轴当前位置为 100mm → 40002 = 100

#### 应用连接到 ModBus Slave

**步骤 1：修改应用配置**

打开应用的设置窗口（⚙ 设置）：

| 配置项 | 值 | 说明 |
|-------|-----|------|
| 启用模拟模式 | ☐ 不勾选 | 关闭纯软件模拟 |
| PLC IP 地址 | 127.0.0.1 | localhost 或本机IP |
| PLC 端口 | 502 | 与 ModBus Slave 设置一致 |
| 轮询间隔 | 300ms | 默认即可 |

点击 **保存设置**，重启应用

**步骤 2：连接测试**

1. 确保 ModBus Slave 已启动且 TCP 连接已激活
2. 启动应用
3. 点击 **🔌 连接PLC** 按钮
4. 状态栏显示 "PLC已连接"（非模拟模式）
5. 右侧轴控制面板 **当前** 值应与 ModBus Slave 中设置的值同步

**步骤 3：测试运动**

1. 勾选某轴的 **使能** 复选框
2. 设置 **目标** 值，例如 150
3. 点击 **▷绝对运动** 按钮
4. 在 ModBus Slave 中观察：
   - 该轴的控制线圈被激活
   - 该轴的保持寄存器不断更新（模拟轴正在移动）
5. 运动完成后，应用显示 "已到位确认"

---

### B. Modbus Poll（付费，专业）

#### 特点
- 图形界面友好，支持数据监视和写入
- 支持批量操作和数据导入/导出
- 高级功能：性能测试、日志记录

#### 下载与安装
1. 访问 [官方网站](https://www.modbustools.com/)
2. 下载 Modbus Poll（有 30 天试用版）
3. 安装并启动

#### 配置连接

菜单 → **Connection** → **Connect** → 选择 **TCP/IP**：

```
Connection Settings:
├─ IP Address: 127.0.0.1
├─ Port: 502
├─ Data Type: Holding Register (40000 系列)
├─ Slave ID: 1
└─ [Connect] 按钮
```

#### 读写寄存器

- **左侧树**：显示所有已连接的寄存器区域
- **右侧表格**：显示寄存器值
- **双击 Value 列**：修改寄存器值（模拟输入或轴位置）

---

### C. QModMaster（开源，跨平台）

#### 特点
- 完全开源
- 支持 Windows、Linux、macOS
- Modbus Master 工具（可读写从站寄存器）

#### 下载
- 官方仓库：[GitHub qmodmaster](https://github.com/ed-ltp/qmodmaster)
- 下载 Release 版本

#### 基本使用

1. **配置连接**
   - 菜单 → **Settings**
   - IP: 127.0.0.1, Port: 502

2. **读取寄存器**
   - 输入起始地址：40000
   - 输入数量：100
   - 点击 **Read coils/registers** 按钮

3. **写入值**
   - 选中寄存器，右键 → **Write**
   - 输入新值

---

## 与应用配套的寄存器映射表

### 应用轴配置

应用内置 5 个轴，每轴占用的寄存器：

```
X 轴 (Index 0):
├─ 1000 (CtrlReg)       : 控制寄存器
├─ 1001 (MoveReg)       : 绝对运动目标
├─ 1002 (ManualSpeed)   : 手动速度
├─ 1003 (AutoSpeed)     : 自动速度
├─ 40000 (JogForward)   : 点动正向线圈
├─ 40001 (JogReverse)   : 点动反向线圈
└─ 30100 (CurrentPos)   : 当前位置读取

Y 轴 (Index 1):
├─ 1010 (CtrlReg)
├─ 1011 (MoveReg)
├─ 1012 (ManualSpeed)
├─ 1013 (AutoSpeed)
├─ 40002 (JogForward)
├─ 40003 (JogReverse)
└─ 30101 (CurrentPos)

R 轴 (Index 2):
├─ 40004 (JogForward)
├─ 40005 (JogReverse)
└─ 30102 (CurrentPos)

Z1 轴 (Index 3):
├─ 40006 (JogForward)
├─ 40007 (JogReverse)
└─ 30103 (CurrentPos)

Z2 轴 (Index 4):
├─ 40008 (JogForward)
├─ 40009 (JogReverse)
└─ 30104 (CurrentPos)
```

*具体数值可在 ConfigService.cs::CreateDefaultSettings() 中查阅*

---

## 测试场景

### 场景 1：点动测试

```
步骤：
1. 在 ModBus Slave 中，设置寄存器 30100 (X当前位置) = 100
2. 启动应用 → 连接PLC
3. 应用右侧显示 X 当前位置 = 100
4. 在应用中点击 X 轴的 ▶ 按钮
5. 在 ModBus Slave 中观察 40000 线圈被激活
6. 松开 ▶ 按钮
7. ModBus Slave 中 40000 线圈恢复为 0
```

### 场景 2：绝对运动测试

```
步骤：
1. 在应用右侧设置 X 轴 目标 = 200
2. 点击 ▷绝对运动 按钮
3. 在 ModBus Slave 中：
   - 观察 40000 激活（开始运动）
   - 观察 30100 逐渐从 100 增加到 200（模拟轴在动）
   - 若要模拟轴运动，需手动增加 30100 的值
4. 当 30100 ≈ 200 时，应用显示 "到位确认"
```

### 场景 3：测试线圈控制（急停）

```
步骤：
1. 点击应用的 ■ 急停 按钮
2. 在 ModBus Slave 中观察所有 Jog 线圈 (40000-40009) 都变为 0
3. 应用状态栏显示 "!!! 急停已触发 !!!"
```

---

## 常见问题

### Q1: 应用连接了，但轴位置一直是 NaN
**原因**：ModBus Slave 中的输入寄存器未初始化为数值（可能是 0 或未读取）

**解决**：
1. 在 ModBus Slave 中，找到每个轴的 CurrentPos 寄存器（30100-30104）
2. 手动设置初值，例如都设为 0
3. 重新连接应用

### Q2: "连接失败" 或超时
**原因**：ip/端口不匹配或 ModBus Slave 服务未启动

**解决**：
1. 检查 ModBus Slave 菜单栏显示 "✓ TCP/IP Connected"
2. 检查应用设置中的 IP 和端口是否与 ModBus Slave 一致
3. 若用 127.0.0.1，确保没有防火墙阻止

### Q3: 点动按钮按了，但轴不动
**原因**：轴未使能，或 ModBus Slave 中的位置寄存器没有变化（模拟器不会自动更新位置）

**解决**：
1. 确保勾选了轴的 **使能** 复选框
2. 在 ModBus Slave 中，手动修改轴的 CurrentPos 寄存器值以模拟运动
   - 例如，按住 X 轴的 ▶，在 ModBus Slave 中持续增加 30100 值

### Q4: 如何模拟实时的轴运动？
**方案**：
- **简单**：手动在 ModBus Slave 中改变寄存器值，应用会及时读取
- **自动**：编写 VB/Python 脚本，定时更新 ModBus Slave 的寄存器值
- **推荐**：使用应用的内置模拟模式（SimulationMode = true），自动模拟轴运动

---

## 推荐工作流

对于不同阶段的开发：

### 初期（功能开发）
```
使用内置模拟模式 (SimulationMode = true)
→ 快速开发，无需外部依赖
→ 支持所有安全功能测试
```

### 中期（功能调试）
```
使用 ModBus Slave + 应用
→ 测试 Modbus 协议实现
→ 验证细节的 Coil / Register 操作
→ 团队联调时演示
```

### 后期（实机测试）
```
连接真实 PLC (汇川 Easy521/Easy523)
→ 完整的硬件测试
```

---

## 附录：快速切换模式

### 只需修改 appsettings.json

创建文件 `appsettings.json` 在应用根目录：

```json
{
  "DatabasePath": "pointposition.db",
  "SimulationMode": true,
  "PlcIpAddress": "127.0.0.1",
  "PlcPort": 502,
  "PollingIntervalMs": 300,
  "LogLevel": "Info"
}
```

| 值 | 含义 |
|-----|------|
| `true` | 使用内置模拟模式 |
| `false` | 使用外部 Modbus TCP |

应用启动时自动加载此配置。

---

## 参考资源

- **Modbus TCP 协议**：https://en.wikipedia.org/wiki/Modbus
- **NModbus 库**（应用使用）：https://github.com/NModbus/NModbus
- **ModBus Slave 官方**：https://www.modbustools.com/
- **汇川 Easy PLC 手册**：搜索 "汇川 Easy521 Modbus" 获取官方文档

---

**最后更新**：2026-04-02
**版本**：1.0

