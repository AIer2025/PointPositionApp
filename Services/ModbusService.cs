using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NModbus;
using NLog;
using PointPositionApp.Models;

namespace PointPositionApp.Services
{
    /// <summary>
    /// Modbus TCP 通信服务
    /// 汇川 Easy 系列 PLC 地址映射规则:
    ///   M 线圈: Modbus 地址 = M 编号 (例如 M1450 -> 地址 1450)
    ///   D 寄存器: Modbus 地址 = D 编号 (例如 D1000 -> 地址 1000, float32 占2个寄存器)
    /// </summary>
    public class ModbusService : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private TcpClient? _tcpClient;
        private IModbusMaster? _master;
        private readonly object _lock = new();
        protected bool _isConnected;
        private readonly byte _slaveId;
        private readonly int _connectTimeoutMs;
        private readonly int _readWriteTimeoutMs;
        private readonly int _maxCommErrors;
        private readonly int _modbusRetries;

        public virtual bool IsConnected => _isConnected;

        public string IpAddress { get; set; } = "192.168.1.100";
        public int Port { get; set; } = 502;

        public ModbusService(AppSettings? settings = null)
        {
            _slaveId = settings?.SlaveId ?? 1;
            _connectTimeoutMs = settings?.ConnectTimeoutMs ?? 3000;
            _readWriteTimeoutMs = settings?.ReadWriteTimeoutMs ?? 2000;
            _maxCommErrors = settings?.MaxCommErrors ?? 3;
            _modbusRetries = settings?.ModbusRetries ?? 2;
        }

        public event Action<bool>? ConnectionStateChanged;

        protected void OnConnectionStateChanged(bool connected)
        {
            ConnectionStateChanged?.Invoke(connected);
        }

        /// <summary>
        /// 带指数退避的自动重连（最多重试3次，间隔 1s, 2s, 4s）
        /// </summary>
        public async Task<bool> ConnectWithRetryAsync(int maxRetries = 3)
        {
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                if (attempt > 0)
                {
                    var delay = (int)Math.Pow(2, attempt - 1) * 1000;
                    Logger.Info("第 {0} 次重连，等待 {1}ms...", attempt, delay);
                    await Task.Delay(delay);
                }

                if (await ConnectAsync())
                    return true;
            }
            return false;
        }

        public virtual async Task<bool> ConnectAsync()
        {
            try
            {
                _tcpClient = new TcpClient();
                _tcpClient.ReceiveTimeout = _connectTimeoutMs;
                _tcpClient.SendTimeout = _connectTimeoutMs;
                await _tcpClient.ConnectAsync(IpAddress, Port);
                var factory = new ModbusFactory();
                _master = factory.CreateMaster(_tcpClient);
                _master.Transport.ReadTimeout = _readWriteTimeoutMs;
                _master.Transport.WriteTimeout = _readWriteTimeoutMs;
                _master.Transport.Retries = _modbusRetries;
                _isConnected = true;
                OnConnectionStateChanged(true);
                Logger.Info("Modbus TCP 连接成功: {0}:{1}", IpAddress, Port);
                return true;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                OnConnectionStateChanged(false);
                Logger.Error(ex, "Modbus TCP 连接失败: {0}:{1}", IpAddress, Port);
                return false;
            }
        }

        public virtual void Disconnect()
        {
            try
            {
                _master?.Dispose();
                _tcpClient?.Close();
                _tcpClient?.Dispose();
            }
            catch { }
            _isConnected = false;
            OnConnectionStateChanged(false);
            Logger.Info("Modbus TCP 已断开");
        }

        #region 线圈操作

        /// <summary>写单个线圈</summary>
        public virtual bool WriteCoil(ushort address, bool value)
        {
            if (!_isConnected || _master == null)
            {
                Logger.Warn("写线圈跳过(未连接): M{0}={1}", address, value);
                return false;
            }
            lock (_lock)
            {
                try
                {
                    _master.WriteSingleCoil(_slaveId, address, value);
                    ResetCommErrorCount();
                    Logger.Info("写线圈成功: M{0} = {1}", address, value);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "写线圈失败: M{0}={1}", address, value);
                    HandleCommError();
                    return false;
                }
            }
        }

        /// <summary>读单个线圈</summary>
        public virtual bool ReadCoil(ushort address, out bool value)
        {
            value = false;
            if (!_isConnected || _master == null) return false;
            lock (_lock)
            {
                try
                {
                    var result = _master.ReadCoils(_slaveId, address, 1);
                    value = result[0];
                    ResetCommErrorCount();
                    Logger.Debug("读线圈: M{0} = {1}", address, value);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "读线圈失败: M{0}", address);
                    HandleCommError();
                    return false;
                }
            }
        }

        #endregion

        #region 寄存器操作

        /// <summary>写 Float32 到保持寄存器（占2个寄存器，Little-Endian word order）</summary>
        public virtual bool WriteFloat(ushort address, float value)
        {
            if (!_isConnected || _master == null)
            {
                Logger.Warn("写Float跳过(未连接): D{0}={1:F3}", address, value);
                return false;
            }
            lock (_lock)
            {
                try
                {
                    var bytes = BitConverter.GetBytes(value);
                    ushort low = BitConverter.ToUInt16(bytes, 0);
                    ushort high = BitConverter.ToUInt16(bytes, 2);
                    // 汇川PLC: 低字在前
                    _master.WriteMultipleRegisters(_slaveId, address, new ushort[] { low, high });
                    ResetCommErrorCount();
                    Logger.Info("写Float成功: D{0} = {1:F3} (raw: 0x{2:X4} 0x{3:X4})", address, value, low, high);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "写Float失败: D{0}={1:F3}", address, value);
                    HandleCommError();
                    return false;
                }
            }
        }

        /// <summary>读 Float32</summary>
        public virtual bool ReadFloat(ushort address, out float value)
        {
            value = 0;
            if (!_isConnected || _master == null) return false;
            lock (_lock)
            {
                try
                {
                    var regs = _master.ReadHoldingRegisters(_slaveId, address, 2);
                    var bytes = new byte[4];
                    BitConverter.GetBytes(regs[0]).CopyTo(bytes, 0);
                    BitConverter.GetBytes(regs[1]).CopyTo(bytes, 2);
                    value = BitConverter.ToSingle(bytes, 0);
                    ResetCommErrorCount();
                    Logger.Debug("读Float: D{0} = {1:F3} (raw: 0x{2:X4} 0x{3:X4})", address, value, regs[0], regs[1]);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "读Float失败: D{0}", address);
                    HandleCommError();
                    return false;
                }
            }
        }

        /// <summary>写 Int16 到寄存器</summary>
        public virtual bool WriteInt16(ushort address, short value)
        {
            if (!_isConnected || _master == null)
            {
                Logger.Warn("写Int16跳过(未连接): D{0}={1}", address, value);
                return false;
            }
            lock (_lock)
            {
                try
                {
                    _master.WriteSingleRegister(_slaveId, address, (ushort)value);
                    ResetCommErrorCount();
                    Logger.Info("写Int16成功: D{0} = {1}", address, value);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "写Int16失败: D{0}={1}", address, value);
                    HandleCommError();
                    return false;
                }
            }
        }

        /// <summary>读 Int16</summary>
        public virtual bool ReadInt16(ushort address, out short value)
        {
            value = 0;
            if (!_isConnected || _master == null) return false;
            lock (_lock)
            {
                try
                {
                    var regs = _master.ReadHoldingRegisters(_slaveId, address, 1);
                    value = (short)regs[0];
                    ResetCommErrorCount();
                    Logger.Debug("读Int16: D{0} = {1}", address, value);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "读Int16失败: D{0}", address);
                    HandleCommError();
                    return false;
                }
            }
        }

        /// <summary>写 UInt16 到寄存器</summary>
        public virtual bool WriteUInt16(ushort address, ushort value)
        {
            if (!_isConnected || _master == null)
            {
                Logger.Warn("写UInt16跳过(未连接): D{0}={1}", address, value);
                return false;
            }
            lock (_lock)
            {
                try
                {
                    _master.WriteSingleRegister(_slaveId, address, value);
                    ResetCommErrorCount();
                    Logger.Info("写UInt16成功: D{0} = {1}", address, value);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "写UInt16失败: D{0}={1}", address, value);
                    HandleCommError();
                    return false;
                }
            }
        }

        #endregion

        #region 高级封装

        /// <summary>点动正向</summary>
        public void JogForward(AxisConfig axis, bool start)
        {
            Logger.Info("{0} 点动正向: {1} (线圈 M{2})", axis.AxisName, start ? "开始" : "停止", axis.JogForwardCoil);
            WriteCoil(axis.JogForwardCoil, start);
        }

        /// <summary>点动反向</summary>
        public void JogReverse(AxisConfig axis, bool start)
        {
            Logger.Info("{0} 点动反向: {1} (线圈 M{2})", axis.AxisName, start ? "开始" : "停止", axis.JogReverseCoil);
            WriteCoil(axis.JogReverseCoil, start);
        }

        /// <summary>读取当前位置</summary>
        public float ReadCurrentPosition(AxisConfig axis)
        {
            if (ReadFloat(axis.CurrentPosRegister, out float pos))
                return pos;
            Logger.Warn("{0} 读取位置失败 (D{1})", axis.AxisName, axis.CurrentPosRegister);
            return float.NaN;
        }

        /// <summary>写手动速度</summary>
        public void WriteManualSpeed(AxisConfig axis, float speed)
        {
            Logger.Info("{0} 写手动速度: {1:F1} (D{2}, 类型={3})",
                axis.AxisName, speed, axis.ManualSpeedRegister, axis.ManualSpeedIsInt16 ? "Int16" : "Float");
            if (axis.ManualSpeedIsInt16)
                WriteInt16(axis.ManualSpeedRegister, (short)speed);
            else
                WriteFloat(axis.ManualSpeedRegister, speed);
        }

        /// <summary>写自动速度</summary>
        public void WriteAutoSpeed(AxisConfig axis, float speed)
        {
            Logger.Info("{0} 写自动速度: {1:F1} (D{2})", axis.AxisName, speed, axis.AutoSpeedRegister);
            WriteFloat(axis.AutoSpeedRegister, speed);
        }

        /// <summary>绝对运动（异步，避免阻塞UI线程）</summary>
        public async Task AbsoluteMoveAsync(AxisConfig axis, float target)
        {
            Logger.Info("{0} 绝对运动开始: 目标={1:F3}mm, 目标寄存器=D{2}, 触发={3}",
                axis.AxisName, target, axis.TargetPosRegister,
                axis.AbsMoveIsCoil ? $"M{axis.AbsMoveRegister}" : $"D{axis.AbsMoveRegister}={axis.AbsMoveValue}");
            // 先写目标位置
            WriteFloat(axis.TargetPosRegister, target);
            await Task.Delay(50);
            // 再触发
            if (axis.AbsMoveIsCoil)
                WriteCoil(axis.AbsMoveRegister, true);
            else
                WriteUInt16(axis.AbsMoveRegister, axis.AbsMoveValue);

            Logger.Info("{0} 绝对运动触发完成 -> {1:F3} mm", axis.AxisName, target);
        }

        /// <summary>
        /// 等待轴到达目标位置（基于位置反馈轮询），支持取消
        /// </summary>
        /// <param name="axis">轴配置</param>
        /// <param name="targetPos">目标位置</param>
        /// <param name="tolerance">到位容差 (mm)</param>
        /// <param name="timeoutMs">超时时间 (ms)</param>
        /// <param name="cancellationToken">取消令牌（急停用）</param>
        /// <returns>true=到位, false=超时或取消</returns>
        public virtual async Task<bool> WaitForPositionAsync(AxisConfig axis, float targetPos,
            float tolerance = 0.5f, int timeoutMs = 30000, CancellationToken cancellationToken = default)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var currentPos = ReadCurrentPosition(axis);
                if (!float.IsNaN(currentPos) && Math.Abs(currentPos - targetPos) <= tolerance)
                {
                    Logger.Info("{0} 到位确认: 当前={1:F3} 目标={2:F3} 容差={3}",
                        axis.AxisName, currentPos, targetPos, tolerance);
                    return true;
                }

                await Task.Delay(100, cancellationToken);
            }

            Logger.Warn("{0} 运动超时: 目标={1:F3} 超时={2}ms", axis.AxisName, targetPos, timeoutMs);
            return false;
        }

        /// <summary>急停 — 停止所有轴的点动，写入全零线圈</summary>
        public virtual void EmergencyStop(System.Collections.Generic.List<AxisConfig> axes)
        {
            Logger.Warn(">>> 急停触发 <<<");
            foreach (var axis in axes)
            {
                WriteCoil(axis.JogForwardCoil, false);
                WriteCoil(axis.JogReverseCoil, false);
                // 对线圈触发型绝对运动也复位
                if (axis.AbsMoveIsCoil)
                    WriteCoil(axis.AbsMoveRegister, false);
            }
        }

        /// <summary>检查目标位置是否在软限位范围内</summary>
        public static bool CheckSoftLimit(AxisConfig axis, float target)
        {
            if (!axis.SoftLimitEnabled) return true;
            return target >= axis.SoftLimitMin && target <= axis.SoftLimitMax;
        }

        /// <summary>绝对运动（同步版本，内部使用异步等待避免阻塞）</summary>
        public async Task AbsoluteMoveSyncAsync(AxisConfig axis, float target)
        {
            WriteFloat(axis.TargetPosRegister, target);
            await Task.Delay(50);
            if (axis.AbsMoveIsCoil)
                WriteCoil(axis.AbsMoveRegister, true);
            else
                WriteUInt16(axis.AbsMoveRegister, axis.AbsMoveValue);

            Logger.Info("{0} 绝对运动 -> {1:F3} mm", axis.AxisName, target);
        }

        /// <summary>回原点</summary>
        public void Home(AxisConfig axis)
        {
            Logger.Info("{0} 回原点触发 (线圈 M{1})", axis.AxisName, axis.HomeCoil);
            WriteCoil(axis.HomeCoil, true);
        }

        /// <summary>使能/禁用轴</summary>
        public void SetAxisEnable(AxisConfig axis, bool enable)
        {
            Logger.Info("{0} 使能={1} (线圈 M{2})", axis.AxisName, enable, axis.EnableCoil);
            WriteCoil(axis.EnableCoil, enable);
        }

        /// <summary>读轴使能状态</summary>
        public bool ReadAxisEnable(AxisConfig axis)
        {
            if (ReadCoil(axis.EnableCoil, out bool val))
            {
                Logger.Debug("{0} 读使能状态: M{1} = {2}", axis.AxisName, axis.EnableCoil, val);
                return val;
            }
            Logger.Warn("{0} 读使能状态失败: M{1}", axis.AxisName, axis.EnableCoil);
            return false;
        }

        /// <summary>夹爪打开（异步）</summary>
        public async Task ClawOpenAsync(ClawModbusConfig claw, float openPos, float openTorque)
        {
            Logger.Info("夹爪打开: Pos={0:F1}, Torque={1:F1} (位置D{2}, 线圈M{3})",
                openPos, openTorque, claw.OpenPosRegister, claw.OpenCoil);
            WriteFloat(claw.OpenPosRegister, openPos);
            if (claw.OpenTorqueRegister > 0)
                WriteFloat(claw.OpenTorqueRegister, openTorque);
            await Task.Delay(30);
            WriteCoil(claw.OpenCoil, true);
        }

        /// <summary>夹爪关闭（异步）</summary>
        public async Task ClawCloseAsync(ClawModbusConfig claw, float closePos, float closeTorque)
        {
            Logger.Info("夹爪关闭: Pos={0:F1}, Torque={1:F1} (位置D{2}, 线圈M{3})",
                closePos, closeTorque, claw.OpenPosRegister, claw.CloseCoil);
            WriteFloat(claw.OpenPosRegister, closePos);
            if (claw.CloseTorqueRegister > 0)
                WriteFloat(claw.CloseTorqueRegister, closeTorque);
            await Task.Delay(30);
            WriteCoil(claw.CloseCoil, true);
        }

        /// <summary>夹爪旋转（异步）</summary>
        public async Task ClawRotateAsync(ClawModbusConfig claw, float angle)
        {
            Logger.Info("夹爪旋转: Angle={0:F1} (角度D{1}, 线圈M{2})",
                angle, claw.AngleRegister, claw.RotateCoil);
            WriteFloat(claw.AngleRegister, angle);
            await Task.Delay(30);
            WriteCoil(claw.RotateCoil, true);
        }

        #endregion

        private int _commErrorCount;

        private void HandleCommError()
        {
            _commErrorCount++;
            var currentCount = _commErrorCount;

            // TCP断开或连续通信错误超过阈值，标记为断开
            if ((_tcpClient != null && !_tcpClient.Connected) || _commErrorCount >= _maxCommErrors)
            {
                _isConnected = false;
                _commErrorCount = 0;
                OnConnectionStateChanged(false);
                Logger.Warn("PLC 连接已断开（连续错误 {0} 次）", currentCount);
            }
        }

        /// <summary>重置通信错误计数（成功通信后调用）</summary>
        private void ResetCommErrorCount()
        {
            _commErrorCount = 0;
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
