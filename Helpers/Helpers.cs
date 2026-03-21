using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using NLog;

namespace PointPositionApp.Helpers
{
    /// <summary>通用 RelayCommand（同步）</summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
            : this(_ => execute(), canExecute != null ? _ => canExecute() : null) { }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>异步安全的 RelayCommand，避免 async void 导致未捕获异常</summary>
    public class AsyncRelayCommand : ICommand
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly Func<object?, Task> _execute;
        private readonly Func<object?, bool>? _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
            : this(_ => execute(), canExecute != null ? _ => canExecute() : null) { }

        public AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

        public async void Execute(object? parameter)
        {
            if (_isExecuting) return;
            _isExecuting = true;
            RaiseCanExecuteChanged();
            try
            {
                await _execute(parameter);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "AsyncRelayCommand 执行异常");
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }
}

namespace PointPositionApp.Converters
{
    /// <summary>布尔值取反</summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;
    }

    /// <summary>布尔值 -> Visibility</summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v == Visibility.Visible;
    }

    /// <summary>已保存点位 -> 背景色</summary>
    public class HasPointToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool hasPoint && hasPoint)
                return new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
            return new SolidColorBrush(Color.FromRgb(66, 66, 66)); // Dark gray
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>选中状态 -> 边框色</summary>
    public class IsSelectedToBorderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool selected && selected)
                return new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Blue
            return new SolidColorBrush(Color.FromRgb(97, 97, 97)); // Gray
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>连接状态 -> 颜色</summary>
    public class ConnectionStateToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool connected && connected)
                return new SolidColorBrush(Color.FromRgb(76, 175, 80));
            return new SolidColorBrush(Color.FromRgb(244, 67, 54));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>连接状态 -> 文本</summary>
    public class ConnectionStateToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var prefix = parameter as string ?? "";
            if (value is bool connected)
                return connected ? $"{prefix}已连接" : $"{prefix}未连接";
            return $"{prefix}未知";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Float NaN -> "--"</summary>
    public class FloatDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is float f)
                return float.IsNaN(f) ? "--" : f.ToString("F3");
            if (value is double d)
                return double.IsNaN(d) ? "--" : d.ToString("F3");
            return "--";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && float.TryParse(s, out float f))
                return f;
            return 0f;
        }
    }
}
