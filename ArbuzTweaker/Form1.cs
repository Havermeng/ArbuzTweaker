namespace ArbuzTweaker;

public partial class Form1 : Form
{
    private const string DotaWarningConsentFileName = "dota-warning-consent.flag";
    private readonly ConfigService _configService;
    private readonly AppSettingsService _appSettingsService;
    private readonly UpdateService _updateService;
    private readonly Dota2Service _dota2Service;
    private Panel _sidebarPanel = null!;
    private Panel _contentPanel = null!;
    private Label _versionLabel = null!;
    private Dictionary<string, UserControl> _tabs = new();

    public Form1()
    {
        _configService = new ConfigService();
        _configService.EnsureDirectoriesExist();
        _appSettingsService = new AppSettingsService(_configService);
        _dotaWarningShown = LoadDotaWarningConsent();

        var version = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0";
        _updateService = new UpdateService(version);
        _dota2Service = new Dota2Service();

        InitializeComponents();
        LoadTabs();

        if (_appSettingsService.Load().CheckForUpdatesOnStartup)
            CheckForUpdatesAsync();
    }

    private void InitializeComponents()
    {
        Text = "ArbuzTweaker";
        Size = new Size(1100, 760);
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        ShowIcon = true;
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);

        var titlePanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            BackColor = Color.FromArgb(20, 20, 20)
        };

        var titleLabel = new Label
        {
            Text = "ArbuzTweaker",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 200, 100),
            Location = new Point(15, 10),
            AutoSize = true
        };

        _versionLabel = new Label
        {
            Text = "v1.0.0",
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.Gray,
            Location = new Point(150, 15),
            AutoSize = true
        };

        _sidebarPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 180,
            BackColor = Color.FromArgb(25, 25, 25)
        };

        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(35, 35, 35)
        };

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Arbuz.ico");
        if (File.Exists(iconPath))
        {
            Icon = new Icon(iconPath);
        }

        titlePanel.Controls.Add(titleLabel);
        titlePanel.Controls.Add(_versionLabel);
        Controls.Add(_contentPanel);
        Controls.Add(_sidebarPanel);
        Controls.Add(titlePanel);
    }

    private void LoadTabs()
    {
        AddTab("Windows", new WindowsTweaksTab());
        AddTab("Dota 2", new DotaTab(_configService, _dota2Service));
        AddTab("Настройки", new SettingsTab(_appSettingsService, _updateService, ResetWarningChoices));
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

        int buttonHeight = 40;
        int startY = 20 + (_sidebarPanel.Controls.Count * (buttonHeight + 5));

        var button = new Button
        {
            Text = name,
            Location = new Point(10, startY),
            Size = new Size(160, buttonHeight),
            FlatStyle = FlatStyle.Flat,
            BackColor = _sidebarPanel.Controls.Count == 0 ? Color.FromArgb(0, 150, 100) : Color.FromArgb(45, 45, 45),
            ForeColor = Color.White,
            Tag = name
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += TabButton_Click;

        _sidebarPanel.Controls.Add(button);

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
                    b.BackColor = Color.FromArgb(45, 45, 45);
            }
            btn.BackColor = Color.FromArgb(0, 150, 100);
            ShowTab(name);
        }
    }

    private void ShowTab(string name)
    {
        _contentPanel.Controls.Clear();
        if (_tabs.TryGetValue(name, out var tab))
        {
            tab.Dock = DockStyle.Fill;
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
                            || downloadedPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

                        if (isInstaller)
                        {
                            var installNowResult = MessageBox.Show(
                                "Обновление скачано. Установить его сейчас?",
                                "Установка обновления",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question);

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
