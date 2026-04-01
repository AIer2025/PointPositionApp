using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using PointPositionApp.Models;

namespace PointPositionApp.Services
{
    /// <summary>
    /// PLC 模拟服务 — 无需真实硬件即可测试全部功能
    ///
    /// 模拟行为:
    ///   - 内存线圈/寄存器，读写即时生效
    ///   - 点动: 按住期间以手动速度匀速移动，松开停止
    ///   - 绝对运动: 触发后当前位置以自动速度向目标位置平滑过渡
    ///   - 回原点: 当前位置平滑归零
    ///   - 夹爪: 即时完成
    /// </summary>
    public class SimulationModbusService : ModbusService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly ConcurrentDictionary<ushort, bool> _coils = new();
        private readonly ConcurrentDictionary<ushort, float> _floatRegisters = new();
        private readonly ConcurrentDictionary<ushort, short> _int16Registers = new();

        private readonly List<AxisConfig> _axes;
        private readonly Timer _simTimer;
        private const int SimTickMs = 50; // 模拟刷新间隔

        // 每轴的运动状态
        private readonly ConcurrentDictionary<string, MotionState> _motionStates = new();
        private readonly float _maxManualSpeed;
        private readonly float _maxAutoSpeed;

        public SimulationModbusService(AppSettings? settings = null) : base(settings)
        {
            _axes = settings?.Axes ?? new List<AxisConfig>();
            _maxManualSpeed = settings?.MaxManualSpeed ?? 200f;
            _maxAutoSpeed = settings?.MaxAutoSpeed ?? 500f;

            // 初始化每轴运动状态
            foreach (var axis in _axes)
            {
                _motionStates[axis.AxisName] = new MotionState();
                // 初始位置 0
                _floatRegisters[axis.CurrentPosRegister] = 0f;
            }

            // 模拟定时器 — 驱动运动
            _simTimer = new Timer(SimulationTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        #region 连接（模拟）

        public override async Task<bool> ConnectAsync()
        {
            await Task.Delay(200); // 模拟连接延迟
            _isConnected = true;
            OnConnectionStateChanged(true);
            _simTimer.Change(0, SimTickMs);
            Logger.Info("[模拟] PLC 已连接（模拟模式）");
            return true;
        }

        public override void Disconnect()
        {
            _simTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _isConnected = false;
            OnConnectionStateChanged(false);
            Logger.Info("[模拟] PLC 已断开（模拟模式）");
        }

        #endregion

        #region 线圈读写（内存）

        public override bool WriteCoil(ushort address, bool value)
        {
            if (!_isConnected) return false;
            _coils[address] = value;

            // 检查是否触发了点动操作
            HandleJogCoilChange(address, value);

            Logger.Debug("[模拟] 写线圈 M{0} = {1}", address, value);
            return true;
        }

        public override bool ReadCoil(ushort address, out bool value)
        {
            value = false;
            if (!_isConnected) return false;
            _coils.TryGetValue(address, out value);
            return true;
        }

        #endregion

        #region 寄存器读写（内存）

        public override bool WriteFloat(ushort address, float value)
        {
            if (!_isConnected) return false;
            _floatRegisters[address] = value;

            // 检查是否写入了目标位置或触发了绝对运动
            HandleFloatWrite(address, value);

            Logger.Debug("[模拟] 写Float D{0} = {1:F3}", address, value);
            return true;
        }

        public override bool ReadFloat(ushort address, out float value)
        {
            value = 0;
            if (!_isConnected) return false;
            _floatRegisters.TryGetValue(address, out value);
            return true;
        }

        public override bool WriteInt16(ushort address, short value)
        {
            if (!_isConnected) return false;
            _int16Registers[address] = value;
            Logger.Debug("[模拟] 写Int16 D{0} = {1}", address, value);
            return true;
        }

        public override bool ReadInt16(ushort address, out short value)
        {
            value = 0;
            if (!_isConnected) return false;
            _int16Registers.TryGetValue(address, out value);
            return true;
        }

        public override bool WriteUInt16(ushort address, ushort value)
        {
            if (!_isConnected) return false;

            // 检查是否为绝对运动触发（寄存器触发方式）
            HandleUInt16Trigger(address, value);

            Logger.Debug("[模拟] 写UInt16 D{0} = {1}", address, value);
            return true;
        }

        #endregion

        #region 运动模拟逻辑

        private void HandleJogCoilChange(ushort address, bool value)
        {
            foreach (var axis in _axes)
            {
                if (address == axis.JogForwardCoil)
                {
                    var state = _motionStates[axis.AxisName];
                    state.Mode = value ? MotionMode.JogForward : MotionMode.Idle;
                    return;
                }
                if (address == axis.JogReverseCoil)
                {
                    var state = _motionStates[axis.AxisName];
                    state.Mode = value ? MotionMode.JogReverse : MotionMode.Idle;
                    return;
                }
                if (address == axis.HomeCoil && value)
                {
                    var state = _motionStates[axis.AxisName];
                    state.TargetPosition = 0f;
                    state.Mode = MotionMode.Absolute;
                    return;
                }
                if (address == axis.AbsMoveRegister && axis.AbsMoveIsCoil && value)
                {
                    StartAbsoluteMove(axis);
                    return;
                }
            }
        }

        private void HandleFloatWrite(ushort address, float value)
        {
            // 记录目标位置写入（绝对运动的第一步）
            foreach (var axis in _axes)
            {
                if (address == axis.TargetPosRegister)
                {
                    _motionStates[axis.AxisName].PendingTarget = value;
                    return;
                }
            }
        }

        private void HandleUInt16Trigger(ushort address, ushort value)
        {
            foreach (var axis in _axes)
            {
                if (!axis.AbsMoveIsCoil && address == axis.AbsMoveRegister && value == axis.AbsMoveValue)
                {
                    StartAbsoluteMove(axis);
                    return;
                }
            }
        }

        private void StartAbsoluteMove(AxisConfig axis)
        {
            var state = _motionStates[axis.AxisName];
            state.TargetPosition = state.PendingTarget;
            state.Mode = MotionMode.Absolute;
            Logger.Info("[模拟] {0} 开始绝对运动 -> {1:F3}", axis.AxisName, state.TargetPosition);
        }

        private void SimulationTick(object? _)
        {
            if (!_isConnected) return;

            float dt = SimTickMs / 1000f; // 秒

            foreach (var axis in _axes)
            {
                var state = _motionStates[axis.AxisName];
                if (state.Mode == MotionMode.Idle) continue;

                _floatRegisters.TryGetValue(axis.CurrentPosRegister, out float currentPos);

                // 获取速度（mm/s）
                float speed = GetAxisSpeed(axis, state.Mode);

                float newPos = currentPos;

                switch (state.Mode)
                {
                    case MotionMode.JogForward:
                        newPos = currentPos + speed * dt;
                        break;

                    case MotionMode.JogReverse:
                        newPos = currentPos - speed * dt;
                        break;

                    case MotionMode.Absolute:
                        float distance = state.TargetPosition - currentPos;
                        float step = speed * dt;

                        if (Math.Abs(distance) <= step)
                        {
                            // 到达目标
                            newPos = state.TargetPosition;
                            state.Mode = MotionMode.Idle;
                            Logger.Info("[模拟] {0} 到位: {1:F3}", axis.AxisName, newPos);
                        }
                        else
                        {
                            newPos = currentPos + Math.Sign(distance) * step;
                        }
                        break;
                }

                // 软限位钳位 — 模拟真实PLC的限位保护
                if (axis.SoftLimitEnabled)
                {
                    if (newPos < axis.SoftLimitMin)
                    {
                        newPos = axis.SoftLimitMin;
                        state.Mode = MotionMode.Idle;
                        Logger.Warn("[模拟] {0} 触发软下限位: {1:F3}", axis.AxisName, axis.SoftLimitMin);
                    }
                    else if (newPos > axis.SoftLimitMax)
                    {
                        newPos = axis.SoftLimitMax;
                        state.Mode = MotionMode.Idle;
                        Logger.Warn("[模拟] {0} 触发软上限位: {1:F3}", axis.AxisName, axis.SoftLimitMax);
                    }
                }

                _floatRegisters[axis.CurrentPosRegister] = newPos;
            }
        }

        private float GetAxisSpeed(AxisConfig axis, MotionMode mode)
        {
            float speed;
            float maxSpeed;

            if (mode == MotionMode.JogForward || mode == MotionMode.JogReverse)
            {
                // 手动速度
                if (axis.ManualSpeedIsInt16)
                {
                    _int16Registers.TryGetValue(axis.ManualSpeedRegister, out short intSpeed);
                    speed = intSpeed;
                }
                else
                {
                    _floatRegisters.TryGetValue(axis.ManualSpeedRegister, out speed);
                }
                maxSpeed = _maxManualSpeed;
            }
            else
            {
                // 自动速度
                _floatRegisters.TryGetValue(axis.AutoSpeedRegister, out speed);
                maxSpeed = _maxAutoSpeed;
            }

            // 最低速度10mm/s，上限使用配置的最大速度
            return Math.Clamp(Math.Abs(speed), 10f, maxSpeed);
        }

        #endregion

        private class MotionState
        {
            public MotionMode Mode { get; set; } = MotionMode.Idle;
            public float TargetPosition { get; set; }
            public float PendingTarget { get; set; }
        }

        private enum MotionMode
        {
            Idle,
            JogForward,
            JogReverse,
            Absolute
        }
    }
}
