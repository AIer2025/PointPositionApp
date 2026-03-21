using System.Linq;
using System.Text;
using System.Windows;
using PointPositionApp.Models;

namespace PointPositionApp.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings _settings;

        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            LoadSettings();
        }

        private void LoadSettings()
        {
            tbDbPath.Text = _settings.DatabasePath;
            tbPlcIp.Text = _settings.PlcIpAddress;
            tbPlcPort.Text = _settings.PlcPort.ToString();
            tbPollInterval.Text = _settings.PollingIntervalMs.ToString();

            // 日志级别
            for (int i = 0; i < cbLogLevel.Items.Count; i++)
            {
                if ((cbLogLevel.Items[i] as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() == _settings.LogLevel)
                {
                    cbLogLevel.SelectedIndex = i;
                    break;
                }
            }

            // 轴地址映射展示
            var sb = new StringBuilder();
            foreach (var ax in _settings.Axes)
            {
                sb.AppendLine($"[{ax.AxisName}]");
                sb.AppendLine($"  点动正向=M{ax.JogForwardCoil}  反向=M{ax.JogReverseCoil}");
                sb.AppendLine($"  当前位置=D{ax.CurrentPosRegister}  手动速度=D{ax.ManualSpeedRegister}");
                sb.AppendLine($"  自动速度=D{ax.AutoSpeedRegister}  目标位置=D{ax.TargetPosRegister}");
                sb.AppendLine($"  绝对运动={(!ax.AbsMoveIsCoil ? "D" : "M")}{ax.AbsMoveRegister}  回零=M{ax.HomeCoil}  使能=M{ax.EnableCoil}");
                sb.AppendLine();
            }
            foreach (var claw in _settings.Claws)
            {
                sb.AppendLine($"[{claw.ClawName}]");
                sb.AppendLine($"  打开=M{claw.OpenCoil}  关闭=M{claw.CloseCoil}  旋转=M{claw.RotateCoil}");
                sb.AppendLine($"  打开位=D{claw.OpenPosRegister}  角度=D{claw.AngleRegister}");
            }
            tbAxisMapping.Text = sb.ToString();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _settings.DatabasePath = tbDbPath.Text.Trim();
            _settings.PlcIpAddress = tbPlcIp.Text.Trim();

            if (int.TryParse(tbPlcPort.Text, out int port))
                _settings.PlcPort = port;

            if (int.TryParse(tbPollInterval.Text, out int interval))
                _settings.PollingIntervalMs = interval;

            if (cbLogLevel.SelectedItem is System.Windows.Controls.ComboBoxItem item)
                _settings.LogLevel = item.Content?.ToString() ?? "Info";

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
