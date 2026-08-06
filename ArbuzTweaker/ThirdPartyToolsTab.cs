using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArbuzTweaker;

public partial class ThirdPartyToolsTab : UserControl
{
    private const int ContentWidth = 900;

    private readonly NvidiaInspectorService _nvidiaInspectorService;
    private readonly MsiAfterburnerService _msiAfterburnerService;
    private readonly IntelXtuService _intelXtuService;
    private Label _nvidiaStateLabel = null!;
    private Label _msiStateLabel = null!;
    private Label _intelXtuStateLabel = null!;
    private Label _statusLabel = null!;
    private int _statusToken;

    public ThirdPartyToolsTab(NvidiaInspectorService nvidiaInspectorService, MsiAfterburnerService msiAfterburnerService, IntelXtuService intelXtuService)
    {
        _nvidiaInspectorService = nvidiaInspectorService;
        _msiAfterburnerService = msiAfterburnerService;
        _intelXtuService = intelXtuService;
        InitializeComponent();
        RefreshState();
    }

    private void InitializeComponent()
    {
        AutoScroll = true;
        BackColor = UiTheme.Surface;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Padding = new Padding(20),
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var titleLabel = new Label
        {
            Text = "Стороннее ПО",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = UiTheme.TextPrimary,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        };

        var infoLabel = CreateDescriptionLabel(
            "Здесь твикер может устанавливать и готовить сторонние инструменты. Позже сюда можно добавить автоматическое применение готовых и пользовательских пресетов для этих программ.",
            new Padding(0, 0, 0, 14));

        var nvidiaPanel = CreateToolPanel(
            "NVIDIA Profile Inspector",
            "Твикер скачивает последнюю доступную версию с официального GitHub-репозитория и распаковывает её в локальную папку инструментов. Позже сюда можно добавить автоматическую настройку профилей NVIDIA Inspector.",
            "Состояние NVIDIA Inspector: не определено",
            out _nvidiaStateLabel,
            CreateActionButton("Установить / обновить NVIDIA Inspector", 280, async (s, e) => await UiTheme.RunButtonOperationAsync(s, InstallNvidiaInspectorAsync), true),
            CreateActionButton("Показать папку NVIDIA Inspector", 250, OpenNvidiaFolderButton_Click));

        var msiPanel = CreateToolPanel(
            "MSI Afterburner",
            "Твикер устанавливает или обновляет MSI Afterburner через winget. Позже сюда можно добавить автоматическое применение пресетов и будущую настройку профилей прямо из твикера.",
            "Состояние MSI Afterburner: не определено",
            out _msiStateLabel,
            CreateActionButton("Установить / обновить MSI Afterburner", 280, async (s, e) => await UiTheme.RunButtonOperationAsync(s, InstallMsiAfterburnerAsync), true),
            CreateActionButton("Показать папку MSI Afterburner", 250, OpenMsiFolderButton_Click),
            CreateActionButton("Открыть официальную страницу MSI Afterburner", 310, OpenMsiOfficialButton_Click));

        var intelXtuPanel = CreateToolPanel(
            "Intel Extreme Tuning Utility (Intel XTU)",
            "Твикер может установить Intel XTU через winget. На официальной странице Intel доступны разные версии под разные поколения процессоров, поэтому для ручного выбора версии лучше открыть сайт Intel.",
            "Состояние Intel XTU: не определено",
            out _intelXtuStateLabel,
            CreateActionButton("Установить Intel XTU", 210, async (s, e) => await UiTheme.RunButtonOperationAsync(s, InstallIntelXtuAsync), true),
            CreateActionButton("Показать папку Intel XTU", 220, OpenIntelXtuFolderButton_Click),
            CreateActionButton("Открыть официальную страницу Intel XTU", 310, OpenIntelXtuOfficialButton_Click));

        _statusLabel = new Label
        {
            Text = string.Empty,
            AutoSize = true,
            MaximumSize = new Size(ContentWidth, 0),
            ForeColor = UiTheme.AccentGreen,
            Margin = new Padding(0, 0, 0, 0)
        };

        root.Controls.Add(titleLabel, 0, 0);
        root.Controls.Add(infoLabel, 0, 1);
        root.Controls.Add(nvidiaPanel, 0, 2);
        root.Controls.Add(msiPanel, 0, 3);
        root.Controls.Add(intelXtuPanel, 0, 4);
        root.Controls.Add(_statusLabel, 0, 5);

        Controls.Add(root);

        UiTheme.EnableDynamicLabelWrap(root, infoLabel, _statusLabel);
    }

    private static Panel CreateToolPanel(string title, string description, string stateText, out Label stateLabel, params Button[] buttons)
    {
        var section = UiTheme.CreateSectionPanel();
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            FlowDirection = FlowDirection.TopDown,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        stateLabel = new Label
        {
            Text = stateText,
            AutoSize = true,
            MaximumSize = new Size(ContentWidth, 0),
            ForeColor = UiTheme.TextPrimary,
            Margin = new Padding(0, 2, 0, 12)
        };

        var buttonsPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            MaximumSize = new Size(ContentWidth, 0),
            Margin = new Padding(0)
        };

        foreach (var button in buttons)
            buttonsPanel.Controls.Add(button);

        var descriptionLabel = CreateDescriptionLabel(description, new Padding(0, 0, 0, 10));
        layout.Controls.Add(UiTheme.CreateSectionTitle(title));
        layout.Controls.Add(descriptionLabel);
        layout.Controls.Add(stateLabel);
        layout.Controls.Add(buttonsPanel);
        section.Controls.Add(layout);
        UiTheme.EnableDynamicLabelWrap(section, descriptionLabel, stateLabel, buttonsPanel);
        return section;
    }

    private static Label CreateDescriptionLabel(string text, Padding margin)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            MaximumSize = new Size(ContentWidth, 0),
            ForeColor = UiTheme.TextMuted,
            Margin = margin
        };
    }

    private static Button CreateActionButton(string text, int width, EventHandler onClick, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            Size = new Size(width, 35),
            Margin = new Padding(0, 0, 10, 8)
        };

        UiTheme.StyleActionButton(button, primary);
        button.Click += onClick;
        return button;
    }

    private async Task InstallNvidiaInspectorAsync()
    {
        ShowStatus("Скачивание NVIDIA Inspector...", Color.Gray, false);
        var result = await _nvidiaInspectorService.InstallLatestAsync();
        ShowStatus(result.Message, result.IsSuccess ? Color.Green : Color.Orange, true);
        RefreshState();
    }

    private async Task InstallMsiAfterburnerAsync()
    {
        ShowStatus("Установка или обновление MSI Afterburner...", Color.Gray, false);
        var result = await _msiAfterburnerService.InstallOrUpdateAsync();
        ShowStatus(result.Message, result.IsSuccess ? Color.Green : Color.Orange, true);
        RefreshState();
    }

    private async Task InstallIntelXtuAsync()
    {
        ShowStatus("Установка Intel XTU...", Color.Gray, false);
        var result = await _intelXtuService.InstallOrUpdateAsync();
        ShowStatus(result.Message, result.IsSuccess ? Color.Green : Color.Orange, true);
        RefreshState();
    }

    private void OpenNvidiaFolderButton_Click(object? sender, EventArgs e)
    {
        if (_nvidiaInspectorService.OpenInstallFolder())
        {
            ShowStatus("Папка NVIDIA Inspector открыта.", Color.Green, true);
            return;
        }

        ShowStatus("Не удалось открыть папку NVIDIA Inspector.", Color.Orange, true);
    }

    private void OpenMsiFolderButton_Click(object? sender, EventArgs e)
    {
        if (_msiAfterburnerService.OpenInstallFolder())
        {
            ShowStatus("Папка MSI Afterburner открыта.", Color.Green, true);
            return;
        }

        ShowStatus("Не удалось открыть папку MSI Afterburner.", Color.Orange, true);
    }

    private void OpenMsiOfficialButton_Click(object? sender, EventArgs e)
    {
        if (_msiAfterburnerService.OpenOfficialPage())
        {
            ShowStatus("Открыта официальная страница MSI Afterburner.", Color.Green, true);
            return;
        }

        ShowStatus("Не удалось открыть официальный сайт MSI Afterburner.", Color.Orange, true);
    }

    private void OpenIntelXtuFolderButton_Click(object? sender, EventArgs e)
    {
        if (_intelXtuService.OpenInstallFolder())
        {
            ShowStatus("Папка Intel XTU открыта.", Color.Green, true);
            return;
        }

        ShowStatus("Не удалось открыть папку Intel XTU.", Color.Orange, true);
    }

    private void OpenIntelXtuOfficialButton_Click(object? sender, EventArgs e)
    {
        if (_intelXtuService.OpenOfficialPage())
        {
            ShowStatus("Открыта официальная страница Intel XTU.", Color.Green, true);
            return;
        }

        ShowStatus("Не удалось открыть официальный сайт Intel XTU.", Color.Orange, true);
    }

    private void RefreshState()
    {
        if (_nvidiaInspectorService.IsInstalled)
        {
            _nvidiaStateLabel.Text = $"Состояние NVIDIA Inspector: установлен ({_nvidiaInspectorService.InstalledVersion})";
            _nvidiaStateLabel.ForeColor = Color.Gainsboro;
        }
        else
        {
            _nvidiaStateLabel.Text = "Состояние NVIDIA Inspector: не установлен";
            _nvidiaStateLabel.ForeColor = Color.Gray;
        }

        if (_msiAfterburnerService.IsInstalled)
        {
            _msiStateLabel.Text = $"Состояние MSI Afterburner: установлен ({_msiAfterburnerService.InstalledVersion})";
            _msiStateLabel.ForeColor = Color.Gainsboro;
        }
        else
        {
            _msiStateLabel.Text = "Состояние MSI Afterburner: не установлен";
            _msiStateLabel.ForeColor = Color.Gray;
        }

        if (_intelXtuService.IsInstalled)
        {
            _intelXtuStateLabel.Text = $"Состояние Intel XTU: установлен ({_intelXtuService.InstalledVersion})";
            _intelXtuStateLabel.ForeColor = Color.Gainsboro;
        }
        else
        {
            _intelXtuStateLabel.Text = "Состояние Intel XTU: не установлен";
            _intelXtuStateLabel.ForeColor = Color.Gray;
        }
    }

    private async void ShowStatus(string message, Color color, bool autoClear)
    {
        // Токен: любой новый статус отменяет отложенную очистку предыдущего,
        // иначе старый таймер стирал сообщение о долгой установке через 2,5 секунды.
        var token = ++_statusToken;
        _statusLabel.Text = message;
        _statusLabel.ForeColor = color;

        if (!autoClear)
            return;

        await Task.Delay(4000);
        if (token == _statusToken)
            _statusLabel.Text = string.Empty;
    }
}
