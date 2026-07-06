using System.Runtime.InteropServices;

namespace ArbuzTweaker;

public sealed class FunctionsTab : UserControl
{
    private const int WmSysCommand = 0x0112;
    private static readonly IntPtr HwndBroadcast = new(0xffff);
    private static readonly IntPtr ScMonitorPower = new(0xf170);
    private static readonly IntPtr MonitorPowerOff = new(2);

    private readonly AppLogService _logService;
    private readonly Label _statusLabel;
    private readonly Button _turnOffScreenButton;

    public FunctionsTab(AppLogService logService)
    {
        _logService = logService;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Surface,
            Padding = new Padding(26, 20, 26, 20),
            ColumnCount = 1,
            RowCount = 4,
            AutoScroll = true
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var titleLabel = new Label
        {
            Text = "Функции",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = UiTheme.TextPrimary,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        };

        var introLabel = new Label
        {
            Text = "Быстрые действия, которые не являются твиками и не меняют системные настройки.",
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = UiTheme.TextMuted,
            AutoSize = true,
            MaximumSize = new Size(980, 0),
            Margin = new Padding(0, 0, 0, 18)
        };

        var screenPanel = UiTheme.CreateSectionPanel();
        screenPanel.Padding = new Padding(16);

        var screenTitleLabel = UiTheme.CreateSectionTitle("Отключение экрана");
        screenTitleLabel.Margin = new Padding(0, 0, 0, 8);

        var screenDescriptionLabel = new Label
        {
            Text = "Отключает монитор обычной командой Windows, как при простое. Компьютер не переходит в спящий режим, программы продолжают работать. Экран включится при движении мыши, нажатии клавиши или касании тачпада.",
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = UiTheme.TextMuted,
            AutoSize = true,
            MaximumSize = new Size(900, 0),
            Margin = new Padding(0, 0, 0, 12)
        };

        _turnOffScreenButton = new Button
        {
            Text = "Отключить экран",
            Size = new Size(180, 36),
            Margin = new Padding(0, 0, 0, 10)
        };
        UiTheme.StyleActionButton(_turnOffScreenButton, true);
        _turnOffScreenButton.Click += async (s, e) => await TurnOffScreenAsync();

        _statusLabel = new Label
        {
            Text = "Готово к отключению экрана.",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = UiTheme.TextDim,
            AutoSize = true,
            MaximumSize = new Size(900, 0),
            Margin = new Padding(0)
        };

        var screenLayout = new FlowLayoutPanel
        {
            Location = new Point(screenPanel.Padding.Left, screenPanel.Padding.Top),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        screenLayout.Controls.Add(screenTitleLabel);
        screenLayout.Controls.Add(screenDescriptionLabel);
        screenLayout.Controls.Add(_turnOffScreenButton);
        screenLayout.Controls.Add(_statusLabel);
        screenPanel.Controls.Add(screenLayout);

        root.Controls.Add(titleLabel, 0, 0);
        root.Controls.Add(introLabel, 0, 1);
        root.Controls.Add(screenPanel, 0, 2);

        Controls.Add(root);
    }

    private async Task TurnOffScreenAsync()
    {
        if (!_turnOffScreenButton.Enabled)
            return;

        _turnOffScreenButton.Enabled = false;
        _statusLabel.Text = "Экран отключится через 1 секунду.";
        _statusLabel.ForeColor = UiTheme.AccentGreen;

        try
        {
            await Task.Delay(1000);
            SendMessage(HwndBroadcast, WmSysCommand, ScMonitorPower, MonitorPowerOff);
            _statusLabel.Text = "Команда отключения экрана отправлена.";
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to turn off display.", ex);
            _statusLabel.Text = "Не получилось отключить экран. Подробности сохранены в журнале.";
            _statusLabel.ForeColor = Color.OrangeRed;
        }
        finally
        {
            await Task.Delay(400);
            _turnOffScreenButton.Enabled = true;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
