# 汇川 PLC 联调注意事项

## 一、硬件与网络准备

| 项目 | 说明 |
|------|------|
| **网络连接** | RJ45 网线直连或同一局域网，PLC 默认 IP 需与上位机同网段 |
| **IP 配置** | 当前默认 `192.168.1.100:502`，需在 InoProShop 中确认 PLC 实际 IP |
| **编程软件** | 安装汇川 InoProShop / AutoShop，用于 PLC 侧程序和参数配置 |
| **急停按钮** | 必须接入硬件急停回路，不能仅依赖软件 |

## 二、代码层面存在的严重风险

### 1. 没有急停机制（严重）
当前代码没有任何紧急停止功能。如果运动过程中出现异常，无法通过软件立即停止所有轴。

### 2. 运动完成靠固定延时（严重）
`MainViewModel.cs` 中 `GotoSelectedPointAsync` 使用硬编码的 1500-2000ms 延时来假设运动完成，真实硬件上可能不够或浪费时间。

### 3. 通信断开时运动不停止（严重）
TCP 连接断开后，PLC 侧电机会继续运行。当前没有 PLC 侧的看门狗超时机制。

### 4. 没有位置到位确认（中等）
发送运动指令后不验证是否实际到达目标位置。

### 5. 使能状态不验证（中等）
`SetAxisEnable` 写线圈后不确认是否成功，运动指令不检查轴是否已使能。

## 三、实机前必须完成的改进

### 软件侧
1. **实现急停功能** — 添加一键停止所有轴的命令
2. **运动到位检测** — 用轮询位置替代固定延时，确认到达目标
3. **看门狗心跳** — 定期写心跳寄存器，PLC 侧超时未收到则自动停机
4. **速度/位置范围校验** — 防止写入超限值损坏机械
5. **使能状态确认** — 运动前验证轴已使能
6. **配置校验** — 启动时检查寄存器地址是否合法、无重叠

### PLC 侧
1. **安全逻辑** — PLC 程序中实现急停、限位、过载保护
2. **看门狗** — PLC 监测通信心跳，超时自动停止所有轴
3. **软限位** — 各轴设置行程范围限制
4. **回原完成信号** — 提供回原完成状态寄存器供上位机读取

### 联调验证
1. **核对寄存器地址映射** — 将 `ConfigService.cs` 中的默认地址与 PLC 实际地址表逐一核对
2. **低速测试优先** — 先用极低速度验证各轴方向和位置正确性
3. **单轴测试** — 逐轴测试 Jog、回原、绝对定位，再测多轴联动
4. **断线测试** — 测试通信中断时 PLC 是否安全停机
5. **Z 轴数据类型确认** — Z 轴速度使用 Int16，需确认 PLC 侧是否一致

## 四、建议的测试顺序

```
1. 网络连通 → ping PLC
2. Modbus 连接 → 验证读写单个寄存器
3. 单轴 Jog → 低速验证方向
4. 单轴回原 → 验证回原逻辑
5. 单轴绝对定位 → 验证位置精度
6. 急停测试 → 验证急停响应
7. 断线测试 → 拔网线验证安全停机
8. 多轴联动 → GotoPoint 完整流程
9. 长时间稳定性 → 连续运行观察
```

## 五、关键配置参数（当前默认值）

| 参数 | 默认值 | 说明 |
|------|--------|------|
| PlcIpAddress | 192.168.1.100 | PLC IP 地址 |
| PlcPort | 502 | Modbus TCP 端口 |
| PollingIntervalMs | 300 | 轮询周期 |
| ConnectTimeoutMs | 3000 | 连接超时 |
| ReadWriteTimeoutMs | 2000 | 读写超时 |
| MaxCommErrors | 3 | 连续错误次数阈值，超出则断开 |
| ModbusRetries | 2 | NModbus 库重试次数 |

## 六、默认寄存器地址映射

```
X 轴:  JogFwd=1450, JogRev=1400, Pos=1000, MSpeed=1500, ASpeed=4000, Target=3008, AbsMove=1200, Home=1300, Enable=1350
Y 轴:  JogFwd=1451, JogRev=1401, Pos=1002, MSpeed=1502, ASpeed=4100, Target=3108, AbsMove=1210, Home=1301, Enable=1351
R 轴:  JogFwd=1452, JogRev=1402, Pos=1004, MSpeed=1504, ASpeed=4200, Target=3208, AbsMove=1220, Home=1302, Enable=1352
Z1轴:  JogFwd=850,  JogRev=860,  Pos=800,  MSpeed=2550(Int16), ASpeed=2500, Target=2008, AbsMove=904, Home=840, Enable=1353
Z2轴:  JogFwd=851,  JogRev=861,  Pos=802,  MSpeed=2551(Int16), ASpeed=2502, Target=2058, AbsMove=914, Home=841, Enable=1354
夹爪:  Open=890, Close=891, Rotate=893, Pos=130, Angle=136
```

## 七、参考资料

- [Easy系列PLC组态及Modbus TCP通讯建立和测试](https://blog.csdn.net/weixin_49863040/article/details/139022917)
- [C#使用ModbusTCP读取汇川Easy521 PLC](https://blog.csdn.net/weixin_67244432/article/details/141429659)
- [汇川EASY系列以太网通讯（MODBUS_TCP做主站）](https://blog.csdn.net/weixin_42946146/article/details/146360569)
