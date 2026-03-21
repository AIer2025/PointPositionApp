using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PointPositionApp.Models;
using PointPositionApp.ViewModels;

namespace PointPositionApp.Views
{
    public partial class MainWindow : Window
    {
        private MainViewModel VM => (MainViewModel)DataContext;
        private readonly DispatcherTimer _clockTimer;

        public MainWindow()
        {
            InitializeComponent();

            // 系统时钟
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => tbClock.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _clockTimer.Start();
            tbClock.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        #region 树形导航

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeNodeItem node)
            {
                VM.SelectedTreeNode = node;
            }
        }

        #endregion

        #region 轴控制事件

        private void JogForward_Down(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is AxisViewModel axis)
                VM.JogForward(axis, true);
        }

        private void JogForward_Up(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is AxisViewModel axis)
                VM.JogForward(axis, false);
        }

        private void JogReverse_Down(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is AxisViewModel axis)
                VM.JogReverse(axis, true);
        }

        private void JogReverse_Up(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is AxisViewModel axis)
                VM.JogReverse(axis, false);
        }

        private async void AbsoluteMove_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is AxisViewModel axis)
            {
                try
                {
                    await VM.AbsoluteMoveAsync(axis);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"绝对运动异常: {ex.Message}");
                }
            }
        }

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is AxisViewModel axis)
                VM.HomeAxis(axis);
        }

        private void AxisEnable_Changed(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is AxisViewModel axis)
                VM.SetAxisEnable(axis, axis.IsEnabled);
        }

        private void ManualSpeed_LostFocus(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is AxisViewModel axis)
                VM.WriteManualSpeed(axis);
        }

        private void AutoSpeed_LostFocus(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is AxisViewModel axis)
                VM.WriteAutoSpeed(axis);
        }

        #endregion

        #region 点位网格事件

        private void GridCell_Click(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is GridCell cell)
            {
                VM.SelectedCell = cell;
            }
        }

        private void GridCell_RightClick(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is GridCell cell)
            {
                VM.SelectedCell = cell;
            }
        }

        private void SavePoint_Click(object sender, RoutedEventArgs e)
        {
            VM.SavePointCommand.Execute(null);
        }

        private void ClearPoint_Click(object sender, RoutedEventArgs e)
        {
            VM.ClearPointCommand.Execute(null);
        }

        private void GotoPoint_Click(object sender, RoutedEventArgs e)
        {
            VM.GotoPointCommand.Execute(null);
        }

        private void CopyRow_Click(object sender, RoutedEventArgs e)
        {
            VM.CopyRowCommand.Execute(null);
        }

        #endregion

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _clockTimer.Stop();
            VM.Dispose();
        }
    }
}
