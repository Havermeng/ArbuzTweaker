namespace ArbuzTweaker;

public partial class Form1 : Form
{
    private const string DotaWarningConsentFileName = "dota-warning-consent.flag";
    private const int ExpandedSidebarWidth = 250;
    private const int CompactSidebarWidth = 214;
    private const int SidebarButtonHeight = 44;
    private const int SidebarButtonGap = 8;
    private readonly ConfigService _configService;
    private readonly AppSettingsService _appSettingsService;
    private readonly AppLogService _logService;
    private readonly FileBackupService _fileBackupService;
    private readonly RegistryBackupService _registryBackupService;
    private readonly ProfileService _profileService;
    private readonly NvidiaInspectorService _nvidiaInspectorService;
    private readonly MsiAfterburnerService _msiAfterburnerService;
    private readonly IntelXtuService _intelXtuService;
    private readonly UpdateService _updateService;
    private readonly Dota2Service _dota2Service;
    private readonly ScpSlService _scpSlService;
    private readonly string _appVersion;
    private Panel _sidebarPanel = null!;
    private Panel _contentPanel = null!;
    private Label _versionLabel = null!;
    private Dictionary<string, UserControl> _tabs = new();

    public Form1()
    {
        _configService = new ConfigService();
        _configService.EnsureDirectoriesExist();
        _appSettingsService = new AppSettingsService(_configService);
        _logService = new AppLogService(_configService);
        _fileBackupService = new FileBackupService(_configService, _logService);
        _registryBackupService = new RegistryBackupService(_configService, _logService);
        _profileService = new ProfileService(_configService, _logService, _fileBackupService);
        _nvidiaInspectorService = new NvidiaInspectorService(_configService);
        _msiAfterburnerService = new MsiAfterburnerService();
        _intelXtuService = new IntelXtuService();
        _dotaWarningShown = LoadDotaWarningConsent();

        _appVersion = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0";
        _updateService = new UpdateService(_appVersion);
        _dota2Service = new Dota2Service(_fileBackupService, _logService);
        _dota2Service.PreferredSteamAccountId32 = _appSettingsService.Load().PreferredSteamAccountId32;
        _scpSlService = new ScpSlService(_fileBackupService, _logService);
        _scpSlService.PreferredSteamAccountId32 = _appSettingsService.Load().PreferredSteamAccountId32;

        InitializeComponents();
        LoadTabs();

        if (_appSettingsService.Load().CheckForUpdatesOnStartup)
            CheckForUpdatesAsync();
    }

    private void InitializeComponents()
    {
        Text = "ArbuzTweaker";
        AutoScaleMode = AutoScaleMode.Dpi;
        Size = GetDefaultWindowSize();
        MinimumSize = new Size(980, 680);
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        ShowIcon = true;
        BackColor = UiTheme.WindowBackground;
        ForeColor = UiTheme.TextPrimary;
        Font = new Font("Segoe UI", 10);

        var titlePanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = UiTheme.SurfaceAlt,
            Padding = new Padding(16, 0, 16, 0)
        };

        var titleLayout = new FlowLayoutPanel
        {
            Location = new Point(16, 0),
            Size = new Size(260, 52),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        var titleLabel = new Label
        {
            Text = "ArbuzTweaker",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = UiTheme.AccentGreen,
            AutoSize = true,
            Margin = new Padding(0, 10, 8, 0)
        };

        _versionLabel = new Label
        {
            Text = "v" + _appVersion,
            Font = new Font("Segoe UI", 9),
            ForeColor = UiTheme.TextDim,
            AutoSize = true,
            Margin = new Padding(0, 18, 0, 0)
        };

        _sidebarPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = ExpandedSidebarWidth,
            BackColor = UiTheme.SurfaceAlt,
            Padding = new Padding(10, 14, 10, 14)
        };

        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Border,
            Padding = new Padding(1)
        };

        var sidebarDivider = new Panel
        {
            Dock = DockStyle.Left,
            Width = 1,
            BackColor = UiTheme.Border
        };

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Arbuz.ico");
        if (File.Exists(iconPath))
        {
            Icon = new Icon(iconPath);
        }

        titleLayout.Controls.Add(titleLabel);
        titleLayout.Controls.Add(_versionLabel);
        titlePanel.Controls.Add(titleLayout);
        Controls.Add(_contentPanel);
        Controls.Add(sidebarDivider);
        Controls.Add(_sidebarPanel);
        Controls.Add(titlePanel);

        Resize += (s, e) => UpdateWindowModeLayout();
        FormClosing += (s, e) => SaveWindowPlacement();
        RestoreWindowPlacement();
        UpdateWindowModeLayout();
    }

    private static Size GetDefaultWindowSize()
    {
        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        var width = Math.Min(1180, Math.Max(980, workingArea.Width - 120));
        var height = Math.Min(780, Math.Max(680, workingArea.Height - 120));
        return new Size(width, height);
    }

    private void RestoreWindowPlacement()
    {
        var settings = _appSettingsService.Load();
        if (settings.WindowWidth < MinimumSize.Width || settings.WindowHeight < MinimumSize.Height)
            return;

        var bounds = new Rectangle(settings.WindowLeft, settings.WindowTop, settings.WindowWidth, settings.WindowHeight);
        if (!IsVisibleOnAnyScreen(bounds))
            return;

        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        if (settings.WindowMaximized)
            WindowState = FormWindowState.Maximized;
    }

    private void SaveWindowPlacement()
    {
        var settings = _appSettingsService.Load();
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;

        settings.WindowLeft = bounds.Left;
        settings.WindowTop = bounds.Top;
        settings.WindowWidth = bounds.Width;
        settings.WindowHeight = bounds.Height;
        settings.WindowMaximized = WindowState == FormWindowState.Maximized;
        _appSettingsService.Save(settings);
    }

    private static bool IsVisibleOnAnyScreen(Rectangle bounds)
    {
        return Screen.AllScreens.Any(screen => Rectangle.Intersect(screen.WorkingArea, bounds).Width >= 240
            && Rectangle.Intersect(screen.WorkingArea, bounds).Height >= 160);
    }

    private void UpdateWindowModeLayout()
    {
        if (_sidebarPanel == null)
            return;

        _sidebarPanel.Width = ClientSize.Width < 1120 ? CompactSidebarWidth : ExpandedSidebarWidth;
        LayoutSidebarButtons();
    }

    private void LayoutSidebarButtons()
    {
        if (_sidebarPanel == null)
            return;

        var buttonWidth = Math.Max(160, _sidebarPanel.ClientSize.Width - 22);
        var index = 0;
        foreach (Control control in _sidebarPanel.Controls)
        {
            if (control is not Button button)
                continue;

            button.Location = new Point(10, 14 + index * (SidebarButtonHeight + SidebarButtonGap));
            button.Size = new Size(buttonWidth, SidebarButtonHeight);
            index++;
        }
    }

    private void LoadTabs()
    {
        AddTab("Windows", new WindowsTweaksTab(_appSettingsService, _dota2Service, _scpSlService, _registryBackupService));
        AddTab("Dota 2", new DotaTab(_configService, _dota2Service, _appSettingsService, _fileBackupService));
        AddTab("SCP:SL", new ScpSlTab(_scpSlService, _appSettingsService));
        AddTab("Прицел", new CrosshairTab());
        AddTab("Стороннее ПО", new ThirdPartyToolsTab(_nvidiaInspectorService, _msiAfterburnerService, _intelXtuService));
        AddTab("Настройки", new SettingsTab(_appSettingsService, _updateService, _fileBackupService, _registryBackupService, _profileService, _logService, ResetWarningChoices));
    }

    private bool _dotaWarningShown = false;

    public bool ShowDotaWarning()
    {
        if (_dotaWarningShown)
            return true;

        var result = MessageBox.Show(
            $"Для работы этой вкладки программа создаст/отредактирует файл {Dota2Service.AutoexecFileName} в папке игры.\n\n" +
            $"Также в параметры запуска Dota 2 будет добавлена команда: {Dota2Service.AutoexecLaunchCommand}\n\n" +
            "Если путь к игре не задан, программа выполнит поиск на устройстве.\n\n" +
            "Продолжить?",
            "Внимание",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result == DialogResult.Yes)
        {
            _dotaWarningShown = true;
            SaveDotaWarningConsent();
        }

        return result == DialogResult.Yes;
    }

    private bool LoadDotaWarningConsent()
    {
        try
        {
            return File.Exists(GetDotaWarningConsentPath());
        }
        catch
        {
            return false;
        }
    }

    private void SaveDotaWarningConsent()
    {
        try
        {
            File.WriteAllText(GetDotaWarningConsentPath(), "accepted");
        }
        catch
        {
        }
    }

    private string GetDotaWarningConsentPath()
    {
        return Path.Combine(_configService.ConfigsPath, DotaWarningConsentFileName);
    }

    private void ResetWarningChoices()
    {
        _dotaWarningShown = false;

        try
        {
            var consentPath = GetDotaWarningConsentPath();
            if (File.Exists(consentPath))
                File.Delete(consentPath);
        }
        catch
        {
        }
    }

    public void AddTab(string name, UserControl tabControl)
    {
        _tabs[name] = tabControl;

        int startY = 14 + (_sidebarPanel.Controls.Count * (SidebarButtonHeight + SidebarButtonGap));

        var button = new Button
        {
            Text = name,
            Location = new Point(10, startY),
            Size = new Size(Math.Max(160, _sidebarPanel.ClientSize.Width - 22), SidebarButtonHeight),
            Tag = name
        };
        UiTheme.StyleSidebarButton(button, _sidebarPanel.Controls.Count == 0);
        button.Click += TabButton_Click;

        _sidebarPanel.Controls.Add(button);
        LayoutSidebarButtons();

        if (_sidebarPanel.Controls.Count == 1)
        {
            ShowTab(name);
        }
    }

    private void TabButton_Click(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.Tag is string name)
        {
            if (name == "Dota 2" && !ShowDotaWarning())
                return;

            foreach (Control c in _sidebarPanel.Controls)
            {
                if (c is Button b)
                    UiTheme.StyleSidebarButton(b, false);
            }
            UiTheme.StyleSidebarButton(btn, true);
            ShowTab(name);
        }
    }

    private void ShowTab(string name)
    {
        _contentPanel.Controls.Clear();
        if (_tabs.TryGetValue(name, out var tab))
        {
            tab.Dock = DockStyle.Fill;
            tab.BackColor = UiTheme.Surface;
            _contentPanel.Controls.Add(tab);
        }
    }

    private async void CheckForUpdatesAsync()
    {
        try
        {
            var (hasUpdate, newVersion, downloadUrl, assetName) = await _updateService.CheckForUpdateAsync();
            if (hasUpdate && !string.IsNullOrEmpty(downloadUrl))
            {
                var result = MessageBox.Show(
                    $"Доступна новая версия {newVersion}.\nСкачать обновление?",
                    "Обновление",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                {
                    var downloadedPath = await _updateService.DownloadUpdateAsync(downloadUrl);
                    if (!string.IsNullOrWhiteSpace(downloadedPath))
                    {
                        var isInstaller = string.Equals(assetName, UpdateService.InstallerAssetName, StringComparison.OrdinalIgnoreCase)
                            || downloadedPath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)
                            || downloadedPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

                        if (isInstaller)
                        {
                            var sha256 = _updateService.GetFileSha256(downloadedPath);
                            var hasSignature = _updateService.HasAuthenticodeSignature(downloadedPath);
                            var installNowResult = MessageBox.Show(
                                "Обновление скачано.\n\n" +
                                $"SHA256: {sha256}\n\n" +
                                (hasSignature
                                    ? "Цифровая подпись найдена.\n\n"
                                    : "Внимание: цифровая подпись не найдена. Запускайте файл только если доверяете этому релизу.\n\n") +
                                "Установить его сейчас?",
                                "Установка обновления",
                                MessageBoxButtons.YesNo,
                                hasSignature ? MessageBoxIcon.Question : MessageBoxIcon.Warning);

                            if (installNowResult == DialogResult.Yes && _updateService.LaunchDownloadedUpdate(downloadedPath))
                            {
                                Close();
                                return;
                            }
                        }

                        MessageBox.Show("Обновление скачано.", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }
        catch { }
    }
}
