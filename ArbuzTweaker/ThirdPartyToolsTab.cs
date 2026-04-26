using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArbuzTweaker;

public partial class ThirdPartyToolsTab : UserControl
{
    private readonly NvidiaInspectorService _nvidiaInspectorService;
    private readonly MsiAfterburnerService _msiAfterburnerService;
    private Label _nvidiaStateLabel = null!;
    private Label _msiStateLabel = null!;
    private Label _statusLabel = null!;

    public ThirdPartyToolsTab(NvidiaInspectorService nvidiaInspectorService, MsiAfterburnerService msiAfterburnerService)
    {
        _nvidiaInspectorService = nvidiaInspectorService;
        _msiAfterburnerService = msiAfterburnerService;
        InitializeComponent();
        RefreshState();
    }

    private void InitializeComponent()
    {
        AutoScroll = true;

        var titleLabel = new Label
        {
            Text = "Стороннее ПО",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            Location = new Point(20, 20),
            AutoSize = true
        };

        var infoLabel = new Label
        {
            Text = "Здесь твикер может устанавливать и готовить сторонние инструменты. В будущем сюда добавим автоматическое применение готовых и пользовательских пресетов для этих программ.",
            Location = new Point(20, 55),
            MaximumSize = new Size(900, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        var inspectorLabel = new Label
        {
            Text = "NVIDIA Profile Inspector",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 125),
            AutoSize = true
        };

        var inspectorDescription = new Label
        {
            Text = "Твикер скачивает последнюю доступную версию с официального GitHub-репозитория и распаковывает её в локальную папку инструментов. Позже сюда добавим автоматическую настройку профилей NVIDIA Inspector.",
            Location = new Point(20, 152),
            MaximumSize = new Size(900, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        _nvidiaStateLabel = new Label
        {
            Text = "Состояние NVIDIA Inspector: не определено",
            Location = new Point(20, 220),
            AutoSize = true,
            ForeColor = Color.White
        };

        var installNvidiaButton = new Button
        {
            Text = "Установить / обновить NVIDIA Inspector",
            Location = new Point(20, 255),
            Size = new Size(280, 35)
        };
        installNvidiaButton.Click += async (s, e) => await InstallNvidiaInspectorAsync();

        var openNvidiaFolderButton = new Button
        {
            Text = "Показать папку NVIDIA Inspector",
            Location = new Point(310, 255),
            Size = new Size(250, 35)
        };
        openNvidiaFolderButton.Click += OpenNvidiaFolderButton_Click;

        var msiLabel = new Label
        {
            Text = "MSI Afterburner",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 335),
            AutoSize = true
        };

        var msiDescription = new Label
        {
            Text = "Твикер устанавливает или обновляет MSI Afterburner через winget. Позже сюда добавим автоматическое применение пресетов и будущую настройку профилей прямо из твикера.",
            Location = new Point(20, 362),
            MaximumSize = new Size(900, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        _msiStateLabel = new Label
        {
            Text = "Состояние MSI Afterburner: не определено",
            Location = new Point(20, 430),
            AutoSize = true,
            ForeColor = Color.White
        };

        var installMsiButton = new Button
        {
            Text = "Установить / обновить MSI Afterburner",
            Location = new Point(20, 465),
            Size = new Size(280, 35)
        };
        installMsiButton.Click += async (s, e) => await InstallMsiAfterburnerAsync();

        var openMsiFolderButton = new Button
        {
            Text = "Показать папку MSI Afterburner",
            Location = new Point(310, 465),
            Size = new Size(250, 35)
        };
        openMsiFolderButton.Click += OpenMsiFolderButton_Click;

        var openMsiOfficialButton = new Button
        {
            Text = "Открыть официальную страницу MSI Afterburner",
            Location = new Point(570, 465),
            Size = new Size(310, 35)
        };
        openMsiOfficialButton.Click += OpenMsiOfficialButton_Click;

        _statusLabel = new Label
        {
            Text = string.Empty,
            Location = new Point(20, 525),
            AutoSize = true,
            ForeColor = Color.Green
        };

        Controls.Add(titleLabel);
        Controls.Add(infoLabel);
        Controls.Add(inspectorLabel);
        Controls.Add(inspectorDescription);
        Controls.Add(_nvidiaStateLabel);
        Controls.Add(installNvidiaButton);
        Controls.Add(openNvidiaFolderButton);
        Controls.Add(msiLabel);
        Controls.Add(msiDescription);
        Controls.Add(_msiStateLabel);
        Controls.Add(installMsiButton);
        Controls.Add(openMsiFolderButton);
        Controls.Add(openMsiOfficialButton);
        Controls.Add(_statusLabel);
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
    }

    private async void ShowStatus(string message, Color color, bool autoClear)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = color;

        if (!autoClear)
            return;

        await Task.Delay(2500);
        _statusLabel.Text = string.Empty;
    }
}
