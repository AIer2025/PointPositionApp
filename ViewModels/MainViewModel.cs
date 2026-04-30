using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Newtonsoft.Json;
using NLog;
using PointPositionApp.Helpers;
using PointPositionApp.Models;
using PointPositionApp.Services;

//测试Git的插件功能

namespace PointPositionApp.ViewModels
{
    /// <summary>轴运行时视图模型</summary>
    public class AxisViewModel : INotifyPropertyChanged
    {
        public AxisConfig Config { get; }

        private float _currentPosition = float.NaN;
        public float CurrentPosition
        {
            get => _currentPosition;
            set { _currentPosition = value; OnPropertyChanged(); }
        }

        private float _manualSpeed = 50;
        public float ManualSpeed
        {
            get => _manualSpeed;
            set { _manualSpeed = value; OnPropertyChanged(); }
        }

        private float _autoSpeed = 100;
        public float AutoSpeed
        {
            get => _autoSpeed;
            set { _autoSpeed = value; OnPropertyChanged(); }
        }

        private float _targetPosition;
        public float TargetPosition
        {
            get => _targetPosition;
            set { _targetPosition = value; OnPropertyChanged(); }
        }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        public AxisViewModel(AxisConfig config)
        {
            Config = config;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>主视图模型</summary>
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private ModbusService _modbus;
        private readonly DatabaseService _db;
        private readonly AppSettings _settings;
        private readonly DispatcherTimer _pollTimer;
        private int _isPolling; // 轮询防重入标志（0=空闲，1=忙）
        private bool _disposed;

        // 急停相关
        private CancellationTokenSource? _motionCts;
        private bool _isEmergencyStopped;

        #region 属性

        // 树形导航
        public ObservableCollection<TreeNodeItem> TreeNodes { get; } = new();

        private TreeNodeItem? _selectedTreeNode;
        public TreeNodeItem? SelectedTreeNode
        {
            get => _selectedTreeNode;
            set
            {
                _selectedTreeNode = value;
                OnPropertyChanged();
                OnTreeNodeSelectedSafe(value);
            }
        }

        // 轴
        public ObservableCollection<AxisViewModel> Axes { get; } = new();

        // 夹爪参数
        private double _clawOpenPos = 50;
        public double ClawOpenPos { get => _clawOpenPos; set { _clawOpenPos = value; OnPropertyChanged(); } }

        private double _clawClosePos = 5;
        public double ClawClosePos { get => _clawClosePos; set { _clawClosePos = value; OnPropertyChanged(); } }

        private double _clawAngle;
        public double ClawAngle { get => _clawAngle; set { _clawAngle = value; OnPropertyChanged(); } }

        private double _clawCloseTorque = 20;
        public double ClawCloseTorque { get => _clawCloseTorque; set { _clawCloseTorque = value; OnPropertyChanged(); } }

        private double _clawOpenTorque = 15;
        public double ClawOpenTorque { get => _clawOpenTorque; set { _clawOpenTorque = value; OnPropertyChanged(); } }

        // 点位网格
        public ObservableCollection<GridCell> GridCells { get; } = new();

        private int _gridRows;
        public int GridRows { get => _gridRows; set { _gridRows = value; OnPropertyChanged(); } }

        private int _gridCols;
        public int GridCols { get => _gridCols; set { _gridCols = value; OnPropertyChanged(); } }

        private GridCell? _selectedCell;
        public GridCell? SelectedCell
        {
            get => _selectedCell;
            set
            {
                if (_selectedCell != null) _selectedCell.IsSelected = false;
                _selectedCell = value;
                if (_selectedCell != null) _selectedCell.IsSelected = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedCellInfo));
            }
        }

        public string SelectedCellInfo
        {
            get
            {
                if (_selectedCell == null) return "未选中";
                var cell = _selectedCell;
                if (cell.HasPoint && cell.Point != null)
                    return $"[{cell.RowIndex},{cell.ColIndex}] X={cell.Point.X:F3} Y={cell.Point.Y:F3} Z={cell.Point.Z:F3} Z1={cell.Point.Z1:F3} R={cell.Point.R:F3}";
                return $"[{cell.RowIndex},{cell.ColIndex}] 未配置";
            }
        }

        // 当前选中的归属信息
        private string _currentOwnerType = "";
        private int _currentOwnerId;
        private double _currentSpaceRow;
        private double _currentSpaceCol;

        private string _selectedNodeInfo = "请从左侧选择模块或托盘";
        public string SelectedNodeInfo { get => _selectedNodeInfo; set { _selectedNodeInfo = value; OnPropertyChanged(); } }

        // 状态栏
        private bool _isPlcConnected;
        public bool IsPlcConnected { get => _isPlcConnected; set { _isPlcConnected = value; OnPropertyChanged(); } }

        private bool _isDbConnected;
        public bool IsDbConnected { get => _isDbConnected; set { _isDbConnected = value; OnPropertyChanged(); } }

        private string _statusMessage = "就绪";
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; OnPropertyChanged(); } }

        #endregion

        #region 命令

        // 急停状态属性
        public bool IsEmergencyStopped
        {
            get => _isEmergencyStopped;
            set { _isEmergencyStopped = value; OnPropertyChanged(); }
        }

        public ICommand ConnectPlcCommand { get; }
        public ICommand DisconnectPlcCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand SavePointCommand { get; }
        public ICommand ClearPointCommand { get; }
        public ICommand GotoPointCommand { get; }
        public ICommand CopyRowCommand { get; }
        public ICommand HomeAllCommand { get; }
        public ICommand ClawOpenCommand { get; }
        public ICommand ClawCloseCommand { get; }
        public ICommand ClawRotateCommand { get; }
        public ICommand RefreshTreeCommand { get; }
        public ICommand EmergencyStopCommand { get; }

        #endregion

        public MainViewModel()
        {
            _settings = ConfigService.Load();
            Logger.Info("========== 应用初始化 ==========");
            Logger.Info("模拟模式: {0}, PLC地址: {1}:{2}", _settings.SimulationMode, _settings.PlcIpAddress, _settings.PlcPort);
            Logger.Info("配置: SlaveId={0}, 轮询间隔={1}ms, 连接超时={2}ms, 读写超时={3}ms",
                _settings.SlaveId, _settings.PollingIntervalMs, _settings.ConnectTimeoutMs, _settings.ReadWriteTimeoutMs);
            Logger.Info("安全参数: 最大手动速度={0}, 最大自动速度={1}, 安全Z高度={2}, 运动超时={3}ms, 到位容差={4}",
                _settings.MaxManualSpeed, _settings.MaxAutoSpeed, _settings.SafeZHeight, _settings.MotionTimeoutMs, _settings.PositionTolerance);

            _modbus = _settings.SimulationMode
                ? new SimulationModbusService(_settings)
                : new ModbusService(_settings);
            _modbus.IpAddress = _settings.PlcIpAddress;
            _modbus.Port = _settings.PlcPort;
            _db = new DatabaseService(_settings.DatabasePath);

            // 初始化轴
            foreach (var axCfg in _settings.Axes)
            {
                Logger.Info("加载轴配置: {0} - 点动M{1}/M{2}, 位置D{3}, 使能M{4}, 触发{5}",
                    axCfg.AxisName, axCfg.JogForwardCoil, axCfg.JogReverseCoil,
                    axCfg.CurrentPosRegister, axCfg.EnableCoil,
                    axCfg.AbsMoveIsCoil ? $"M{axCfg.AbsMoveRegister}" : $"D{axCfg.AbsMoveRegister}={axCfg.AbsMoveValue}");
                Axes.Add(new AxisViewModel(axCfg));
            }

            // 连接状态监听
            _modbus.ConnectionStateChanged += connected =>
            {
                Application.Current?.Dispatcher.Invoke(() => IsPlcConnected = connected);
            };

            // 轮询定时器
            _pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_settings.PollingIntervalMs)
            };
            _pollTimer.Tick += PollTimer_Tick;

            // 命令 - 异步命令使用 AsyncRelayCommand 避免 async void 异常
            ConnectPlcCommand = new AsyncRelayCommand(async () => await ConnectPlcAsync());
            DisconnectPlcCommand = new RelayCommand(() => DisconnectPlc());
            OpenSettingsCommand = new RelayCommand(() => OpenSettings());
            SavePointCommand = new AsyncRelayCommand(async () => await SaveCurrentPointAsync(), () => _selectedCell != null);
            ClearPointCommand = new AsyncRelayCommand(async () => await ClearSelectedPointAsync(), () => _selectedCell != null);
            GotoPointCommand = new AsyncRelayCommand(async () => await GotoSelectedPointAsync(), () => _selectedCell?.HasPoint == true);
            CopyRowCommand = new AsyncRelayCommand(async (p) => await CopyRowAsync(p));
            HomeAllCommand = new AsyncRelayCommand(async () => await HomeAllAxesAsync());
            ClawOpenCommand = new AsyncRelayCommand(async () => await ExecuteClawOpenAsync());
            ClawCloseCommand = new AsyncRelayCommand(async () => await ExecuteClawCloseAsync());
            ClawRotateCommand = new AsyncRelayCommand(async () => await ExecuteClawRotateAsync());
            RefreshTreeCommand = new AsyncRelayCommand(async () => await LoadTreeDataAsync());
            EmergencyStopCommand = new RelayCommand(() => EmergencyStop());

            // 初始化（带异常处理，使用 ContinueWith 避免 async void）
            _ = InitializeAsync().ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    var ex = t.Exception?.InnerException ?? t.Exception;
                    Logger.Error(ex, "初始化失败");
                    Application.Current?.Dispatcher.Invoke(() =>
                        StatusMessage = "初始化失败: " + ex?.Message);
                }
            }, TaskScheduler.Default);
        }

        private async Task InitializeAsync()
        {
            var ok = await _db.ConnectAsync();
            IsDbConnected = ok;
            if (ok)
            {
                await _db.InsertDemoDataAsync();
                await LoadTreeDataAsync();
                StatusMessage = "数据库已连接";
            }
            else
            {
                StatusMessage = "数据库连接失败";
            }
        }

        #region PLC连接

        private async Task ConnectPlcAsync()
        {
            Logger.Info("========== 连接PLC开始 ==========");
            Logger.Info("目标地址: {0}:{1}, 模拟模式: {2}, SlaveId: {3}",
                _settings.PlcIpAddress, _settings.PlcPort, _settings.SimulationMode, _settings.SlaveId);
            StatusMessage = "正在连接PLC...";

            // 根据当前设置重建 Modbus 服务实例（解决设置窗口修改模拟模式后不生效的问题）
            bool needSimulation = _settings.SimulationMode;
            bool currentIsSimulation = _modbus is SimulationModbusService;
            if (needSimulation != currentIsSimulation)
            {
                Logger.Info("Modbus 服务类型切换: {0} -> {1}",
                    currentIsSimulation ? "模拟" : "真实", needSimulation ? "模拟" : "真实");
                _modbus.Disconnect();
                _modbus.Dispose();
                _modbus = needSimulation
                    ? new SimulationModbusService(_settings)
                    : new ModbusService(_settings);
                _modbus.ConnectionStateChanged += connected =>
                {
                    Application.Current?.Dispatcher.Invoke(() => IsPlcConnected = connected);
                };
            }

            _modbus.IpAddress = _settings.PlcIpAddress;
            _modbus.Port = _settings.PlcPort;
            var ok = await _modbus.ConnectAsync();
            if (ok)
            {
                StatusMessage = _settings.SimulationMode ? "PLC已连接（模拟模式）" : "PLC已连接";
                Logger.Info("PLC连接成功，开始初始化各轴参数...");
                // 初始化速度并同步使能状态
                foreach (var ax in Axes)
                {
                    Logger.Info("初始化 {0}: 手动速度={1:F1}, 自动速度={2:F1}, 使能={3}",
                        ax.Config.AxisName, ax.ManualSpeed, ax.AutoSpeed, ax.IsEnabled);
                    _modbus.WriteManualSpeed(ax.Config, ax.ManualSpeed);
                    _modbus.WriteAutoSpeed(ax.Config, ax.AutoSpeed);
                    _modbus.SetAxisEnable(ax.Config, ax.IsEnabled);
                }
                Logger.Info("轴初始化完成，启动轮询 (间隔={0}ms)", _settings.PollingIntervalMs);
                _pollTimer.Start();
            }
            else
            {
                Logger.Error("PLC连接失败: {0}:{1}", _settings.PlcIpAddress, _settings.PlcPort);
                StatusMessage = "PLC连接失败";
            }
        }

        private void DisconnectPlc()
        {
            Logger.Info("断开PLC连接");
            _pollTimer.Stop();
            _modbus.Disconnect();
            StatusMessage = "PLC已断开";
            foreach (var ax in Axes)
                ax.CurrentPosition = float.NaN;
        }

        #endregion

        #region 轮询（防重入）

        private void PollTimer_Tick(object? sender, EventArgs e)
        {
            if (!_modbus.IsConnected) return;

            // 使用 Interlocked 防止轮询重入
            if (Interlocked.CompareExchange(ref _isPolling, 1, 0) != 0) return;

            Task.Run(() =>
            {
                try
                {
                    foreach (var ax in Axes)
                    {
                        var pos = _modbus.ReadCurrentPosition(ax.Config);
                        Application.Current?.Dispatcher.Invoke(() => ax.CurrentPosition = pos);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "轮询异常");
                }
                finally
                {
                    Interlocked.Exchange(ref _isPolling, 0);
                }
            });
        }

        #endregion

        #region 急停

        /// <summary>急停 — 立即停止所有运动，取消进行中的运动序列</summary>
        public void EmergencyStop()
        {
            Logger.Warn(">>> 急停触发 <<<");
            IsEmergencyStopped = true;

            // 取消正在进行的运动序列（GotoPoint等）
            _motionCts?.Cancel();

            // 发送停止指令到PLC
            if (_modbus.IsConnected)
            {
                _modbus.EmergencyStop(_settings.Axes);
            }

            StatusMessage = "!!! 急停已触发 - 所有运动已停止 !!!";
        }

        /// <summary>复位急停状态</summary>
        public void ResetEmergencyStop()
        {
            IsEmergencyStopped = false;
            StatusMessage = "急停已复位";
            Logger.Info("急停已复位");
        }

        #endregion

        #region 安全校验

        /// <summary>校验速度是否在安全范围内，超限则钳位</summary>
        private float ClampSpeed(float speed, bool isManual)
        {
            float max = isManual ? _settings.MaxManualSpeed : _settings.MaxAutoSpeed;
            if (speed > max)
            {
                Logger.Warn("速度超限: {0} -> 钳位到 {1}", speed, max);
                return max;
            }
            if (speed < 0) return 0;
            return speed;
        }

        /// <summary>检查目标位置软限位</summary>
        private bool ValidateSoftLimit(AxisConfig axis, float target)
        {
            if (!ModbusService.CheckSoftLimit(axis, target))
            {
                var msg = $"{axis.AxisName} 目标位置 {target:F3}mm 超出软限位范围 [{axis.SoftLimitMin}, {axis.SoftLimitMax}]";
                Logger.Warn(msg);
                StatusMessage = $"安全拦截: {msg}";
                return false;
            }
            return true;
        }

        /// <summary>运动前安全检查（连接、急停、使能）</summary>
        private bool PreMotionCheck(AxisConfig? axis = null)
        {
            if (!_modbus.IsConnected)
            {
                Logger.Warn("PreMotionCheck 失败: PLC未连接");
                StatusMessage = "PLC未连接";
                return false;
            }
            if (IsEmergencyStopped)
            {
                Logger.Warn("PreMotionCheck 失败: 急停状态");
                StatusMessage = "急停状态，请先复位后再操作";
                return false;
            }
            if (axis != null && _settings.RequireEnableBeforeMotion && axis.RequireEnable)
            {
                if (!_modbus.ReadAxisEnable(axis))
                {
                    Logger.Warn("PreMotionCheck 失败: {0} 未使能 (M{1})", axis.AxisName, axis.EnableCoil);
                    StatusMessage = $"{axis.AxisName} 未使能，请先使能轴";
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region 轴控制（供UI事件调用）

        public void JogForward(AxisViewModel axis, bool start)
        {
            if (!PreMotionCheck(start ? axis.Config : null)) return;
            if (start)
            {
                axis.ManualSpeed = ClampSpeed(axis.ManualSpeed, true);
                _modbus.WriteManualSpeed(axis.Config, axis.ManualSpeed);
            }
            _modbus.JogForward(axis.Config, start);
        }

        public void JogReverse(AxisViewModel axis, bool start)
        {
            if (!PreMotionCheck(start ? axis.Config : null)) return;
            if (start)
            {
                axis.ManualSpeed = ClampSpeed(axis.ManualSpeed, true);
                _modbus.WriteManualSpeed(axis.Config, axis.ManualSpeed);
            }
            _modbus.JogReverse(axis.Config, start);
        }

        public async Task AbsoluteMoveAsync(AxisViewModel axis)
        {
            if (!PreMotionCheck(axis.Config)) return;
            if (!ValidateSoftLimit(axis.Config, axis.TargetPosition)) return;

            axis.AutoSpeed = ClampSpeed(axis.AutoSpeed, false);
            _modbus.WriteAutoSpeed(axis.Config, axis.AutoSpeed);
            await _modbus.AbsoluteMoveAsync(axis.Config, axis.TargetPosition);
        }

        public void HomeAxis(AxisViewModel axis)
        {
            if (!PreMotionCheck()) return;
            _modbus.Home(axis.Config);
        }

        public void SetAxisEnable(AxisViewModel axis, bool enable)
        {
            if (!_modbus.IsConnected) return;
            _modbus.SetAxisEnable(axis.Config, enable);
        }

        public void WriteManualSpeed(AxisViewModel axis)
        {
            if (!_modbus.IsConnected) return;
            axis.ManualSpeed = ClampSpeed(axis.ManualSpeed, true);
            _modbus.WriteManualSpeed(axis.Config, axis.ManualSpeed);
        }

        public void WriteAutoSpeed(AxisViewModel axis)
        {
            if (!_modbus.IsConnected) return;
            axis.AutoSpeed = ClampSpeed(axis.AutoSpeed, false);
            _modbus.WriteAutoSpeed(axis.Config, axis.AutoSpeed);
        }

        #endregion

        #region 回原点（Z/Z1轴先回，等待位置反馈到位）

        private async Task HomeAllAxesAsync()
        {
            if (!PreMotionCheck()) return;

            _motionCts?.Cancel();
            _motionCts = new CancellationTokenSource();
            var ct = _motionCts.Token;

            StatusMessage = "全轴回原点中...";

            try
            {
                // 按优先级分组
                var groups = Axes.GroupBy(a => a.Config.HomePriority).OrderBy(g => g.Key);

                foreach (var group in groups)
                {
                    ct.ThrowIfCancellationRequested();

                    var axesInGroup = group.ToList();
                    foreach (var ax in axesInGroup)
                    {
                        _modbus.Home(ax.Config);
                    }

                    // 等待该优先级组所有轴回到原点（位置0）
                    var waitTasks = axesInGroup.Select(ax =>
                        _modbus.WaitForPositionAsync(ax.Config, 0f,
                            _settings.PositionTolerance, _settings.MotionTimeoutMs, ct));
                    var results = await Task.WhenAll(waitTasks);

                    if (results.Any(r => !r))
                    {
                        var failedAxes = axesInGroup.Where((ax, i) => !results[i])
                            .Select(ax => ax.Config.AxisName);
                        StatusMessage = $"回原点超时: {string.Join(", ", failedAxes)}";
                        Logger.Warn("回原点部分超时: {0}", string.Join(", ", failedAxes));
                        return;
                    }
                }

                StatusMessage = "全轴回原点完成";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "回原点已取消（急停）";
            }
        }

        #endregion

        #region 夹爪控制（异步，避免阻塞UI线程）

        private async Task ExecuteClawOpenAsync()
        {
            if (!_modbus.IsConnected || _settings.Claws.Count == 0) return;
            await _modbus.ClawOpenAsync(_settings.Claws[0], (float)ClawOpenPos, (float)ClawOpenTorque);
            StatusMessage = "夹爪打开";
        }

        private async Task ExecuteClawCloseAsync()
        {
            if (!_modbus.IsConnected || _settings.Claws.Count == 0) return;
            await _modbus.ClawCloseAsync(_settings.Claws[0], (float)ClawClosePos, (float)ClawCloseTorque);
            StatusMessage = "夹爪关闭";
        }

        private async Task ExecuteClawRotateAsync()
        {
            if (!_modbus.IsConnected || _settings.Claws.Count == 0) return;
            await _modbus.ClawRotateAsync(_settings.Claws[0], (float)ClawAngle);
            StatusMessage = "夹爪旋转";
        }

        #endregion

        #region 树形导航

        private async Task LoadTreeDataAsync()
        {
            var projects = await _db.GetAllProjectsAsync();
            TreeNodes.Clear();

            foreach (var proj in projects)
            {
                var projNode = new TreeNodeItem
                {
                    DisplayName = $"📁 {proj.ProjectName}",
                    NodeType = TreeNodeType.Project,
                    Id = proj.ProjectId,
                    Data = proj
                };

                // 模块
                var modules = await _db.GetModulesByProjectAsync(proj.ProjectId);
                foreach (var mod in modules)
                {
                    projNode.Children.Add(new TreeNodeItem
                    {
                        DisplayName = $"📦 {mod.ChannelGroupName}",
                        NodeType = TreeNodeType.Module,
                        Id = mod.ModuleId,
                        Data = mod
                    });
                }

                // 托盘
                var trays = await _db.GetTraysByProjectAsync(proj.ProjectId);
                foreach (var tray in trays)
                {
                    var trayNode = new TreeNodeItem
                    {
                        DisplayName = $"🧫 {tray.LabTrayName}",
                        NodeType = TreeNodeType.Tray,
                        Id = tray.TrayId,
                        Data = tray
                    };

                    // 区域
                    var regions = await _db.GetRegionsByTrayAsync(tray.TrayId);
                    foreach (var region in regions)
                    {
                        trayNode.Children.Add(new TreeNodeItem
                        {
                            DisplayName = $"🔲 {region.RegionName}",
                            NodeType = TreeNodeType.Region,
                            Id = region.RegionId,
                            Data = region
                        });
                    }

                    projNode.Children.Add(trayNode);
                }

                TreeNodes.Add(projNode);
            }

            StatusMessage = $"已加载 {projects.Count} 个项目";
        }

        /// <summary>安全的树节点选中处理（捕获异常，避免 async void）</summary>
        private void OnTreeNodeSelectedSafe(TreeNodeItem? node)
        {
            _ = OnTreeNodeSelectedAsync(node).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    var ex = t.Exception?.InnerException ?? t.Exception;
                    Logger.Error(ex, "树节点选中处理异常");
                    Application.Current?.Dispatcher.Invoke(() =>
                        StatusMessage = "加载数据失败: " + ex?.Message);
                }
            }, TaskScheduler.Default);
        }

        private async Task OnTreeNodeSelectedAsync(TreeNodeItem? node)
        {
            if (node == null) return;

            int rows = 0, cols = 0;
            string ownerType = "";
            int ownerId = 0;
            double spaceRow = 0, spaceCol = 0;
            string[]? rowLabels = null, colLabels = null;

            switch (node.NodeType)
            {
                case TreeNodeType.Module:
                    var mod = node.Data as Module;
                    if (mod == null) return;
                    rows = mod.Rows; cols = mod.Cols;
                    spaceRow = mod.SpaceRow; spaceCol = mod.SpaceCol;
                    ownerType = OwnerTypes.Module; ownerId = mod.ModuleId;
                    SelectedNodeInfo = $"模块: {mod.ChannelGroupName} ({rows}×{cols}, 间距 {spaceRow}×{spaceCol}mm)";
                    if (mod.ClawInfoId.HasValue)
                        await LoadClawInfoAsync(mod.ClawInfoId.Value);
                    break;

                case TreeNodeType.Tray:
                    var tray = node.Data as Tray;
                    if (tray == null) return;
                    rows = tray.Rows; cols = tray.Cols;
                    spaceRow = tray.SpaceRow; spaceCol = tray.SpaceCol;
                    ownerType = OwnerTypes.Tray; ownerId = tray.TrayId;
                    SelectedNodeInfo = $"托盘: {tray.LabTrayName} ({rows}×{cols}, 孔径{tray.WellDiameter}mm)";
                    if (!string.IsNullOrEmpty(tray.RowLabels))
                        rowLabels = JsonConvert.DeserializeObject<string[]>(tray.RowLabels);
                    if (!string.IsNullOrEmpty(tray.ColLabels))
                        colLabels = JsonConvert.DeserializeObject<string[]>(tray.ColLabels);
                    break;

                case TreeNodeType.Region:
                    var region = node.Data as TrayCoordinateRegion;
                    if (region == null) return;
                    rows = region.Rows; cols = region.Cols;
                    spaceRow = region.SpaceRow; spaceCol = region.SpaceCol;
                    ownerType = OwnerTypes.Region; ownerId = region.RegionId;
                    SelectedNodeInfo = $"区域: {region.RegionName} ({rows}×{cols})";
                    if (region.ClawInfoId.HasValue)
                        await LoadClawInfoAsync(region.ClawInfoId.Value);
                    if (!string.IsNullOrEmpty(region.RowLabels))
                        rowLabels = JsonConvert.DeserializeObject<string[]>(region.RowLabels);
                    if (!string.IsNullOrEmpty(region.ColLabels))
                        colLabels = JsonConvert.DeserializeObject<string[]>(region.ColLabels);
                    break;

                case TreeNodeType.Project:
                    SelectedNodeInfo = $"项目: {(node.Data as Project)?.ProjectName}（请选择下级模块或托盘）";
                    return;
            }

            _currentOwnerType = ownerType;
            _currentOwnerId = ownerId;
            _currentSpaceRow = spaceRow;
            _currentSpaceCol = spaceCol;
            GridRows = rows;
            GridCols = cols;

            await BuildGridAsync(rows, cols, ownerType, ownerId, rowLabels, colLabels);
        }

        private async Task LoadClawInfoAsync(int clawInfoId)
        {
            var info = await _db.GetClawInfoAsync(clawInfoId);
            if (info != null)
            {
                ClawOpenPos = info.OpenPos;
                ClawClosePos = info.ClosePos;
                ClawAngle = info.Angle;
                ClawCloseTorque = info.CloseTorque;
                ClawOpenTorque = info.OpenTorque;
            }
        }

        #endregion

        #region 点位网格

        private async Task BuildGridAsync(int rows, int cols, string ownerType, int ownerId,
            string[]? rowLabels = null, string[]? colLabels = null)
        {
            if (rows <= 0 || cols <= 0)
            {
                Logger.Warn("网格维度无效: rows={0}, cols={1}", rows, cols);
                GridCells.Clear();
                return;
            }

            var points = await _db.GetPointsAsync(ownerType, ownerId);
            var pointMap = points.ToDictionary(p => (p.RowIndex, p.ColIndex));

            GridCells.Clear();
            _selectedCell = null;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    string label;
                    if (rowLabels != null && r < rowLabels.Length && colLabels != null && c < colLabels.Length)
                        label = $"{rowLabels[r]}{colLabels[c]}";
                    else
                        label = $"{r + 1}-{c + 1}";

                    var hasPoint = pointMap.ContainsKey((r, c));
                    GridCells.Add(new GridCell
                    {
                        RowIndex = r,
                        ColIndex = c,
                        Label = label,
                        HasPoint = hasPoint,
                        Point = hasPoint ? pointMap[(r, c)] : null
                    });
                }
            }

            OnPropertyChanged(nameof(SelectedCellInfo));
        }

        #endregion

        #region 点位操作

        /// <summary>
        /// 保存当前点位
        /// 轴映射: Axes[0]=X, Axes[1]=Y, Axes[2]=R, Axes[3]=Z1, Axes[4]=Z2
        /// 数据库字段: X, Y, Z(对应Z1轴), Z1(对应Z2轴), R
        /// </summary>
        private async Task SaveCurrentPointAsync()
        {
            if (_selectedCell == null || string.IsNullOrEmpty(_currentOwnerType)) return;

            var point = new PointPosition
            {
                OwnerType = _currentOwnerType,
                OwnerId = _currentOwnerId,
                RowIndex = _selectedCell.RowIndex,
                ColIndex = _selectedCell.ColIndex
            };

            if (_modbus.IsConnected)
            {
                // 轴索引: 0=X, 1=Y, 2=R, 3=Z1(映射到DB的Z字段), 4=Z2(映射到DB的Z1字段)
                point.X = Axes.Count > 0 ? Axes[0].CurrentPosition : 0;
                point.Y = Axes.Count > 1 ? Axes[1].CurrentPosition : 0;
                point.R = Axes.Count > 2 ? Axes[2].CurrentPosition : 0;
                point.Z = Axes.Count > 3 ? Axes[3].CurrentPosition : 0;   // Z1轴 -> DB.Z
                point.Z1 = Axes.Count > 4 ? Axes[4].CurrentPosition : 0;  // Z2轴 -> DB.Z1

                // 检查是否有无效位置值（NaN说明读取失败）
                if (float.IsNaN((float)point.X) || float.IsNaN((float)point.Y) ||
                    float.IsNaN((float)point.Z) || float.IsNaN((float)point.Z1) ||
                    float.IsNaN((float)point.R))
                {
                    StatusMessage = "保存失败: 部分轴位置读取无效(NaN)，请检查通信";
                    Logger.Warn("保存点位失败: 存在NaN位置值");
                    return;
                }
            }
            // PLC未连接时所有坐标值保持默认0

            await _db.SavePointAsync(point);

            _selectedCell.HasPoint = true;
            _selectedCell.Point = point;

            OnPropertyChanged(nameof(SelectedCellInfo));
            StatusMessage = $"点位已保存: [{_selectedCell.RowIndex},{_selectedCell.ColIndex}]";
        }

        private async Task ClearSelectedPointAsync()
        {
            if (_selectedCell == null || string.IsNullOrEmpty(_currentOwnerType)) return;

            await _db.DeletePointAsync(_currentOwnerType, _currentOwnerId,
                _selectedCell.RowIndex, _selectedCell.ColIndex);

            _selectedCell.HasPoint = false;
            _selectedCell.Point = null;

            OnPropertyChanged(nameof(SelectedCellInfo));
            StatusMessage = $"点位已清除: [{_selectedCell.RowIndex},{_selectedCell.ColIndex}]";
        }

        /// <summary>
        /// 安全跳转到选中点位（带防撞保护）
        /// 运动顺序: 1.先抬Z轴到安全高度 2.等待Z到位 3.移动XY/R 4.等待XY/R到位 5.下降Z轴
        /// 全程基于位置反馈确认到位，支持急停取消
        /// </summary>
        private async Task GotoSelectedPointAsync()
        {
            if (_selectedCell?.Point == null) return;
            if (!PreMotionCheck()) return;

            var p = _selectedCell.Point;
            var cellLabel = $"[{_selectedCell.RowIndex},{_selectedCell.ColIndex}]";

            // 软限位预检查 — 所有目标位置必须在安全范围内
            var targets = new (int axisIdx, float target, string name)[]
            {
                (0, (float)p.X, "X"), (1, (float)p.Y, "Y"), (2, (float)p.R, "R"),
                (3, (float)p.Z, "Z1"), (4, (float)p.Z1, "Z2")
            };
            foreach (var (axisIdx, target, name) in targets)
            {
                if (Axes.Count > axisIdx && !ValidateSoftLimit(Axes[axisIdx].Config, target))
                    return;
            }

            // 用户确认
            var confirmResult = MessageBox.Show(
                $"即将跳转到点位 {cellLabel}\n" +
                $"X={p.X:F3} Y={p.Y:F3} R={p.R:F3}\n" +
                $"Z1={p.Z:F3} Z2={p.Z1:F3}\n\n" +
                "确认执行运动？",
                "运动确认", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (confirmResult != MessageBoxResult.OK) return;

            // 创建新的取消令牌（急停用）
            _motionCts?.Cancel();
            _motionCts = new CancellationTokenSource();
            var ct = _motionCts.Token;
            float safeZ = _settings.SafeZHeight;
            float tolerance = _settings.PositionTolerance;
            int timeout = _settings.MotionTimeoutMs;

            try
            {
                // === 第1步: 先抬Z轴到安全高度，防止水平移动时碰撞 ===
                StatusMessage = $"跳转中: {cellLabel} - 抬升Z轴到安全高度({safeZ}mm)...";

                var zWaitTasks = new List<Task<bool>>();
                if (Axes.Count > 3)
                {
                    Axes[3].TargetPosition = safeZ;
                    await _modbus.AbsoluteMoveAsync(Axes[3].Config, safeZ);
                    zWaitTasks.Add(_modbus.WaitForPositionAsync(Axes[3].Config, safeZ, tolerance, timeout, ct));
                }
                if (Axes.Count > 4)
                {
                    Axes[4].TargetPosition = safeZ;
                    await _modbus.AbsoluteMoveAsync(Axes[4].Config, safeZ);
                    zWaitTasks.Add(_modbus.WaitForPositionAsync(Axes[4].Config, safeZ, tolerance, timeout, ct));
                }

                if (zWaitTasks.Count > 0)
                {
                    var zResults = await Task.WhenAll(zWaitTasks);
                    if (zResults.Any(r => !r))
                    {
                        StatusMessage = $"跳转失败: Z轴抬升超时，已中止后续运动";
                        Logger.Error("GotoPoint中止: Z轴未能在{0}ms内到达安全高度", timeout);
                        return;
                    }
                }

                ct.ThrowIfCancellationRequested();

                // === 第2步: Z轴已确认到位，开始移动XY和R轴 ===
                StatusMessage = $"跳转中: {cellLabel} - 移动XY/R...";

                var xyWaitTasks = new List<Task<bool>>();
                if (Axes.Count > 0)
                {
                    Axes[0].TargetPosition = (float)p.X;
                    await _modbus.AbsoluteMoveAsync(Axes[0].Config, (float)p.X);
                    xyWaitTasks.Add(_modbus.WaitForPositionAsync(Axes[0].Config, (float)p.X, tolerance, timeout, ct));
                }
                if (Axes.Count > 1)
                {
                    Axes[1].TargetPosition = (float)p.Y;
                    await _modbus.AbsoluteMoveAsync(Axes[1].Config, (float)p.Y);
                    xyWaitTasks.Add(_modbus.WaitForPositionAsync(Axes[1].Config, (float)p.Y, tolerance, timeout, ct));
                }
                if (Axes.Count > 2)
                {
                    Axes[2].TargetPosition = (float)p.R;
                    await _modbus.AbsoluteMoveAsync(Axes[2].Config, (float)p.R);
                    xyWaitTasks.Add(_modbus.WaitForPositionAsync(Axes[2].Config, (float)p.R, tolerance, timeout, ct));
                }

                if (xyWaitTasks.Count > 0)
                {
                    var xyResults = await Task.WhenAll(xyWaitTasks);
                    if (xyResults.Any(r => !r))
                    {
                        StatusMessage = $"跳转失败: XY/R轴运动超时，Z轴保持安全高度";
                        Logger.Error("GotoPoint中止: XY/R轴未能在{0}ms内到位", timeout);
                        return;
                    }
                }

                ct.ThrowIfCancellationRequested();

                // === 第3步: XY/R已到位，Z轴下降到目标位置 ===
                StatusMessage = $"跳转中: {cellLabel} - 下降Z轴...";

                var zDownTasks = new List<Task<bool>>();
                if (Axes.Count > 3)
                {
                    Axes[3].TargetPosition = (float)p.Z;
                    await _modbus.AbsoluteMoveAsync(Axes[3].Config, (float)p.Z);
                    zDownTasks.Add(_modbus.WaitForPositionAsync(Axes[3].Config, (float)p.Z, tolerance, timeout, ct));
                }
                if (Axes.Count > 4)
                {
                    Axes[4].TargetPosition = (float)p.Z1;
                    await _modbus.AbsoluteMoveAsync(Axes[4].Config, (float)p.Z1);
                    zDownTasks.Add(_modbus.WaitForPositionAsync(Axes[4].Config, (float)p.Z1, tolerance, timeout, ct));
                }

                if (zDownTasks.Count > 0)
                {
                    var zDownResults = await Task.WhenAll(zDownTasks);
                    if (zDownResults.Any(r => !r))
                    {
                        StatusMessage = $"跳转警告: Z轴下降超时，请检查位置";
                        Logger.Warn("GotoPoint: Z轴下降超时");
                        return;
                    }
                }

                StatusMessage = $"已跳转到点位: {cellLabel}";
                Logger.Info("GotoPoint完成: {0} X={1:F3} Y={2:F3} Z={3:F3} Z1={4:F3} R={5:F3}",
                    cellLabel, p.X, p.Y, p.Z, p.Z1, p.R);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = $"跳转已取消（急停）- Z轴可能未在安全高度，请手动确认！";
                Logger.Warn("GotoPoint被急停取消");
            }
        }

        private async Task CopyRowAsync(object? parameter)
        {
            if (string.IsNullOrEmpty(_currentOwnerType) || GridRows == 0) return;

            // 将第0行复制到所有其他行
            for (int r = 1; r < GridRows; r++)
            {
                await _db.CopyPointsToRowAsync(_currentOwnerType, _currentOwnerId,
                    0, r, GridCols, _currentSpaceRow);
            }

            // 重建网格
            await BuildGridAsync(GridRows, GridCols, _currentOwnerType, _currentOwnerId);
            StatusMessage = $"已将第1行点位复制到所有行 (行距={_currentSpaceRow}mm)";
        }

        #endregion

        #region 设置

        private void OpenSettings()
        {
            var settingsWindow = new Views.SettingsWindow(_settings);
            if (settingsWindow.ShowDialog() == true)
            {
                ConfigService.Save(_settings);
                _modbus.IpAddress = _settings.PlcIpAddress;
                _modbus.Port = _settings.PlcPort;
                _pollTimer.Interval = TimeSpan.FromMilliseconds(_settings.PollingIntervalMs);
                StatusMessage = "配置已更新";
            }
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _pollTimer.Stop();
            _pollTimer.Tick -= PollTimer_Tick;
            _modbus.Dispose();
            _db.Dispose();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
