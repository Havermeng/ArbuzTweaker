using System.Diagnostics;
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
    private readonly Label _explorerStatusLabel;
    private readonly Button _restartExplorerButton;

    public FunctionsTab(AppLogService logService)
    {
        _logService = logService;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Surface,
            Padding = new Padding(26, 20, 26, 20),
            ColumnCount = 1,
            RowCount = 5,
            AutoScroll = true
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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

        var explorerPanel = UiTheme.CreateSectionPanel();
        explorerPanel.Padding = new Padding(16);

        var explorerTitleLabel = UiTheme.CreateSectionTitle("Перезапуск Проводника");
        explorerTitleLabel.Margin = new Padding(0, 0, 0, 8);

        var explorerDescriptionLabel = new Label
        {
            Text = "Перезапускает только explorer.exe и обновляет панель задач, значки и рабочий стол. Полезно, если после выхода из сна элементы отображаются неправильно. Открытые окна Проводника будут закрыты.",
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = UiTheme.TextMuted,
            AutoSize = true,
            MaximumSize = new Size(900, 0),
            Margin = new Padding(0, 0, 0, 12)
        };

        _restartExplorerButton = new Button
        {
            Text = "Перезапустить Проводник",
            Size = new Size(210, 36),
            Margin = new Padding(0, 0, 0, 10)
        };
        UiTheme.StyleActionButton(_restartExplorerButton);
        _restartExplorerButton.Click += async (s, e) => await RestartExplorerAsync();

        _explorerStatusLabel = new Label
        {
            Text = "Готово к перезапуску Проводника.",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = UiTheme.TextDim,
            AutoSize = true,
            MaximumSize = new Size(900, 0),
            Margin = new Padding(0)
        };

        var explorerLayout = new FlowLayoutPanel
        {
            Location = new Point(explorerPanel.Padding.Left, explorerPanel.Padding.Top),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        explorerLayout.Controls.Add(explorerTitleLabel);
        explorerLayout.Controls.Add(explorerDescriptionLabel);
        explorerLayout.Controls.Add(_restartExplorerButton);
        explorerLayout.Controls.Add(_explorerStatusLabel);
        explorerPanel.Controls.Add(explorerLayout);

        root.Controls.Add(titleLabel, 0, 0);
        root.Controls.Add(introLabel, 0, 1);
        root.Controls.Add(screenPanel, 0, 2);
        root.Controls.Add(explorerPanel, 0, 3);

        Controls.Add(root);

        UiTheme.EnableDynamicLabelWrap(root, introLabel);
        UiTheme.EnableDynamicLabelWrap(screenPanel, screenDescriptionLabel, _statusLabel);
        UiTheme.EnableDynamicLabelWrap(explorerPanel, explorerDescriptionLabel, _explorerStatusLabel);
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
            // A synchronous broadcast waits for every top-level window. One hung application
            // can therefore freeze the tweaker itself, so this command is posted asynchronously.
            if (!PostMessage(HwndBroadcast, WmSysCommand, ScMonitorPower, MonitorPowerOff))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
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

    private async Task RestartExplorerAsync()
    {
        if (!_restartExplorerButton.Enabled)
            return;

        _restartExplorerButton.Enabled = false;
        _explorerStatusLabel.Text = "Перезапуск Проводника...";
        _explorerStatusLabel.ForeColor = UiTheme.AccentGreen;

        try
        {
            await Task.Run(() =>
            {
                foreach (var explorer in Process.GetProcessesByName("explorer"))
                {
                    using (explorer)
                    {
                        explorer.Kill();
                        explorer.WaitForExit(5000);
                    }
                }

                // Windows с AutoRestartShell=1 сама поднимает оболочку после завершения.
                // Если запустить explorer поверх уже поднявшейся оболочки, откроется
                // лишнее окно «Этот компьютер», поэтому сперва ждём авто-перезапуск.
                var deadline = DateTime.UtcNow.AddSeconds(3);
                while (DateTime.UtcNow < deadline && Process.GetProcessesByName("explorer").Length == 0)
                    Thread.Sleep(250);

                if (Process.GetProcessesByName("explorer").Length == 0)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        UseShellExecute = true
                    });
                }
            });

            _explorerStatusLabel.Text = "Проводник перезапущен. Панель задач и значки обновлены.";
            _explorerStatusLabel.ForeColor = UiTheme.AccentGreen;
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to restart Windows Explorer.", ex);
            _explorerStatusLabel.Text = "Не удалось перезапустить Проводник. Подробности сохранены в журнале.";
            _explorerStatusLabel.ForeColor = Color.OrangeRed;
        }
        finally
        {
            _restartExplorerButton.Enabled = true;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
