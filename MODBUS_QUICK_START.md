# Modbus 模拟器快速开始（3分钟版）

## 方案A：5秒启用内置模拟模式（推荐）

### 步骤

**1️⃣ 启动应用**
```
dotnet run
```

**2️⃣ 打开设置**
```
点击界面右上角 ⚙ 设置 按钮
```

**3️⃣ 启用模拟模式**
```
GUI 中勾选：☑ 启用模拟模式
点击：保存设置
重启应用
```

**4️⃣ 验证**
```
应用启动后
点击：🔌 连接PLC
状态栏显示：PLC已连接（模拟模式） ✓
```

**5️⃣ 开始测试**
```
右侧轴控制 → 尝试点动、绝对运动等功能
```

✅ **完成！** 无需任何外部工具。

---

## 方案B：使用外部 ModBus 模拟器

### 步骤

**1️⃣ 下载 ModBus Slave**
```
访问：https://www.modbustools.com/
下载：ModBus Slave 7 或 8（免费）
```

**2️⃣ 安装并启动**
```
- 双击 ModBusSlaveSetup.exe 安装
- 启动应用
```

**3️⃣ 启动 Modbus TCP 服务**
```
菜单栏 → Connection → Connect → TCP/IP

设置：
  IP: 127.0.0.1
  Port: 502
  Device ID: 1

点击 OK
```

**4️⃣ 初始化轴位置**
```
在 ModBus Slave 的 Input Registers 表格中，找到：
  30100 = 100  (X轴当前位置)
  30101 = 100  (Y轴当前位置)
  30102 = 0    (R轴当前位置)
  30103 = 0    (Z1轴当前位置)
  30104 = 0    (Z2轴当前位置)

双击 Value 列，输入初值
```

**5️⃣ 修改应用配置**

编辑 `appsettings.json`：
```json
{
  "SimulationMode": false,  // ← 改成 false
  "PlcIpAddress": "127.0.0.1",
  "PlcPort": 502
}
```

**6️⃣ 启动应用**
```
dotnet run
```

**7️⃣ 连接并测试**
```
点击：🔌 连接PLC
状态栏显示：PLC已连接 ✓

试试看：
  - 点击轴的点动按钮 ▶
  - 在 ModBus Slave 中观察 Coil (4xxxx) 被激活
  - 手动改变 30100-30104 值，应用实时显示轴位置
```

---

## 对比两种方案

| 方案 | 安装步骤 | 精度 | 上手难度 | 何时用 |
|-----|--------|------|--------|--------|
| **内置模拟** | 3 步（设置勾选） | 完整 | ⭐ 最简单 | 🎯 快速开发 |
| **ModBus Slave** | 6 步（安装+配置） | 完整 | ⭐⭐⭐ 需学习 | 🔧 深度调试 |

---

## 文件清单

已为您准备了以下文件在应用目录：

```
PointPositionApp/
├─ appsettings.json              ← 配置文件（已创建）
├─ USAGE_GUIDE.md                ← 应用完整使用手册
├─ MODBUS_SIMULATOR_SETUP.md     ← 模拟器详细安装指南（本文档）
└─ MODBUS_QUICK_START.md         ← 快速开始（你在这里）
```

---

## 推荐使用流程

```
Day 1: 快速验证功能
  → 使用内置模拟模式（SimulationMode=true）
  → 测试界面、点位保存、GotoPoint、急停等

Day 2+: 深度调试
  → 如需验证 Modbus 协议细节
  → 使用 ModBus Slave + 应用
  → 观察线圈、寄存器的确切值

实机测试: 连接真实 PLC
  → SimulationMode=false
  → PlcIpAddress = 真实 PLC IP
  → PlcPort = 502（或实际端口）
```

---

## 常见卡点

### ❌ "连接失败"

**检查清单**：
- [ ] ModBus Slave 的 TCP 连接已启动？
- [ ] appsettings.json 中的 IP 和 Port 是否匹配？
- [ ] 端口 502 是否被防火墙阻止？

**快速修复**：
```
改用 127.0.0.1（本机回环地址）
确保没有其他程序占用 502 端口
```

### ❌ 轴位置显示 "---"（NaN）

**原因**：ModBus Slave 的寄存器未初始化

**修复**：
1. 打开 ModBus Slave
2. 找到 Input Registers 区域
3. 初始化 30100-30104，设为 0 或合理值
4. 重新连接应用

### ❌ 点动按钮按了，轴没反应

**检查**：
- [ ] 轴已勾选 ☑ 使能？
- [ ] 应用已连接 PLC？
- [ ] 在 ModBus Slave 中能看到 Coils 被激活吗？

**调试步骤**：
1. 在应用中点击点动按钮
2. 勾着按钮，同时看 ModBus Slave 的 Coil 4xxxx 是否亮起
3. 如果亮起但轴不动，说明 PLC 模拟需要响应运动
4. 手动在 ModBus Slave 中改变 30100 值模拟轴移动

---

## 下一步

- 详细文档：阅读 `MODBUS_SIMULATOR_SETUP.md`
- 使用手册：阅读 `USAGE_GUIDE.md`
- 提交反馈：遇到问题时查看日志文件（NLog.config）

---

**祝您测试愉快！** 🚀

