using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArbuzTweaker;

public partial class SettingsTab : UserControl
{
    private const int SettingsContentWidth = 800;
    private const string SupportUrl = "https://dalink.to/arbuznymagnat";
    private const string LatestReleaseUrl = "https://github.com/Havermeng/ArbuzTweaker/releases/latest";

    private readonly AppSettingsService _appSettingsService;
    private readonly UpdateService _updateService;
    private readonly FileBackupService _fileBackupService;
    private readonly RegistryBackupService _registryBackupService;
    private readonly ProfileService _profileService;
    private readonly AppLogService _logService;
    private readonly Action _resetWarningChoices;
    private Label _currentVersionValueLabel = null!;
    private Label _updateAvailabilityValueLabel = null!;
    private CheckBox _updateCheckBox = null!;
    private CheckBox _safeModeCheckBox = null!;
    private Label _statusLabel = null!;
    private bool _isLoadingSettings;

    public SettingsTab(
        AppSettingsService appSettingsService,
        UpdateService updateService,
        FileBackupService fileBackupService,
        RegistryBackupService registryBackupService,
        ProfileService profileService,
        AppLogService logService,
        Action resetWarningChoices)
    {
        _appSettingsService = appSettingsService;
        _updateService = updateService;
        _fileBackupService = fileBackupService;
        _registryBackupService = registryBackupService;
        _profileService = profileService;
        _logService = logService;
        _resetWarningChoices = resetWarningChoices;

        InitializeComponent();
        LoadSettings();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (Visible)
            SyncSafeModeFromSettings();
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
            Text = "Настройки твикера",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = UiTheme.TextPrimary,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 18)
        };

        var updatesPanel = UiTheme.CreateSectionPanel();
        var updatesLayout = CreateVerticalSectionLayout();
        updatesLayout.Controls.Add(UiTheme.CreateSectionTitle("Обновления"));

        var currentVersionRow = CreateKeyValueRow("Текущая версия:", out _currentVersionValueLabel, _updateService.CurrentVersion);
        var updateAvailabilityRow = CreateKeyValueRow("Доступность обновления:", out _updateAvailabilityValueLabel, "Проверка не выполнялась");

        _updateCheckBox = new CheckBox
        {
            Text = "Автоматически проверять обновления при запуске",
            AutoSize = true,
            MaximumSize = new Size(SettingsContentWidth, 0),
            ForeColor = UiTheme.TextPrimary,
            Margin = new Padding(0, 4, 0, 12)
        };
        _updateCheckBox.CheckedChanged += UpdateCheckBox_CheckedChanged;

        var checkNowButton = new Button
        {
            Text = "Проверить",
            Size = new Size(170, 35),
            Margin = new Padding(0, 0, 10, 8)
        };
        UiTheme.StyleActionButton(checkNowButton, true);
        checkNowButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, CheckForUpdatesNowAsync);

        var whatsNewButton = new Button
        {
            Text = "Что нового",
            Size = new Size(150, 35),
            Margin = new Padding(0, 0, 0, 8)
        };
        UiTheme.StyleActionButton(whatsNewButton);
        whatsNewButton.Click += (s, e) => OpenLatestReleasePage();

        var updateButtonsPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            MaximumSize = new Size(SettingsContentWidth, 0),
            Margin = new Padding(0)
        };
        updateButtonsPanel.Controls.Add(checkNowButton);
        updateButtonsPanel.Controls.Add(whatsNewButton);

        updatesLayout.Controls.Add(currentVersionRow);
        updatesLayout.Controls.Add(updateAvailabilityRow);
        updatesLayout.Controls.Add(_updateCheckBox);
        updatesLayout.Controls.Add(updateButtonsPanel);
        updatesPanel.Controls.Add(updatesLayout);

        var warningsPanel = UiTheme.CreateSectionPanel();
        var warningsLayout = CreateVerticalSectionLayout();
        warningsLayout.Controls.Add(UiTheme.CreateSectionTitle("Предупреждения"));

        var warningsHint = new Label
        {
            Text = "По умолчанию безопасный режим включён: пользовательские конфиги доступны, а небезопасные системные твики блокируются. Отключение безопасного режима требует отдельного подтверждения рисков.",
            MaximumSize = new Size(SettingsContentWidth, 0),
            AutoSize = true,
            ForeColor = UiTheme.TextMuted,
            Margin = new Padding(0, 0, 0, 12)
        };

        var resetWarningsButton = new Button
        {
            Text = "Сбросить выборы",
            Size = new Size(180, 35)
        };
        UiTheme.StyleActionButton(resetWarningsButton);
        resetWarningsButton.Click += ResetWarningsButton_Click;

        _safeModeCheckBox = new CheckBox
        {
            Text = "Безопасный режим включён: блокировать небезопасные системные Windows-твики",
            AutoSize = true,
            MaximumSize = new Size(SettingsContentWidth, 0),
            ForeColor = UiTheme.TextPrimary,
            Margin = new Padding(0, 0, 0, 12)
        };
        _safeModeCheckBox.CheckedChanged += SafeModeCheckBox_CheckedChanged;

        warningsLayout.Controls.Add(warningsHint);
        warningsLayout.Controls.Add(_safeModeCheckBox);
        warningsLayout.Controls.Add(resetWarningsButton);
        warningsPanel.Controls.Add(warningsLayout);

        var maintenancePanel = UiTheme.CreateSectionPanel();
        var maintenanceLayout = CreateVerticalSectionLayout();
        maintenanceLayout.Controls.Add(UiTheme.CreateSectionTitle("Бэкапы и журнал"));

        var maintenanceHint = new Label
        {
            Text = "Перед изменением конфигов и реестра твикер сохраняет резервные копии. Здесь можно открыть папки с бэкапами и журнал действий.",
            MaximumSize = new Size(SettingsContentWidth, 0),
            AutoSize = true,
            ForeColor = UiTheme.TextMuted,
            Margin = new Padding(0, 0, 0, 12)
        };

        var maintenanceButtonsPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            MaximumSize = new Size(SettingsContentWidth, 0),
            Margin = new Padding(0)
        };

        var openFileBackupsButton = new Button
        {
            Text = "Файловые бэкапы",
            Size = new Size(180, 35),
            Margin = new Padding(0, 0, 10, 8)
        };
        UiTheme.StyleActionButton(openFileBackupsButton);
        openFileBackupsButton.Click += (s, e) => OpenFileBackupsButton_Click();

        var restoreFileBackupButton = new Button
        {
            Text = "Восстановить файл",
            Size = new Size(180, 35),
            Margin = new Padding(0, 0, 10, 8)
        };
        UiTheme.StyleActionButton(restoreFileBackupButton);
        restoreFileBackupButton.Click += (s, e) => RestoreFileBackupButton_Click();

        var openRegistryBackupsButton = new Button
        {
            Text = "Бэкап реестра",
            Size = new Size(170, 35),
            Margin = new Padding(0, 0, 10, 8)
        };
        UiTheme.StyleActionButton(openRegistryBackupsButton);
        openRegistryBackupsButton.Click += (s, e) => OpenRegistryBackupsButton_Click();

        var openLogButton = new Button
        {
            Text = "Журнал",
            Size = new Size(130, 35),
            Margin = new Padding(0, 0, 10, 8)
        };
        UiTheme.StyleActionButton(openLogButton);
        openLogButton.Click += (s, e) => OpenLogButton_Click();

        var exportProfileButton = new Button
        {
            Text = "Экспорт профиля",
            Size = new Size(170, 35),
            Margin = new Padding(0, 0, 10, 8)
        };
        UiTheme.StyleActionButton(exportProfileButton);
        exportProfileButton.Click += (s, e) => ExportProfileButton_Click();

        var importProfileButton = new Button
        {
            Text = "Импорт профиля",
            Size = new Size(165, 35),
            Margin = new Padding(0, 0, 10, 8)
        };
        UiTheme.StyleActionButton(importProfileButton);
        importProfileButton.Click += (s, e) => ImportProfileButton_Click();

        maintenanceButtonsPanel.Controls.Add(openFileBackupsButton);
        maintenanceButtonsPanel.Controls.Add(restoreFileBackupButton);
        maintenanceButtonsPanel.Controls.Add(openRegistryBackupsButton);
        maintenanceButtonsPanel.Controls.Add(openLogButton);
        maintenanceButtonsPanel.Controls.Add(exportProfileButton);
        maintenanceButtonsPanel.Controls.Add(importProfileButton);
        maintenanceLayout.Controls.Add(maintenanceHint);
        maintenanceLayout.Controls.Add(maintenanceButtonsPanel);
        maintenancePanel.Controls.Add(maintenanceLayout);

        var supportPanel = UiTheme.CreateSectionPanel();
        var supportLayout = CreateVerticalSectionLayout();
        supportLayout.Controls.Add(UiTheme.CreateSectionTitle("Поддержать автора"));

        var supportHint = new Label
        {
            Text = "Если твикер оказался полезен, можно поддержать автора денежно. Кнопка откроет страницу сбора в браузере.",
            MaximumSize = new Size(SettingsContentWidth, 0),
            AutoSize = true,
            ForeColor = UiTheme.TextMuted,
            Margin = new Padding(0, 0, 0, 12)
        };

        var supportButton = new Button
        {
            Text = "Поддержать автора",
            Size = new Size(190, 35),
            Margin = new Padding(0)
        };
        UiTheme.StyleActionButton(supportButton, true);
        supportButton.Click += (s, e) => OpenSupportPage();

        supportLayout.Controls.Add(supportHint);
        supportLayout.Controls.Add(supportButton);
        supportPanel.Controls.Add(supportLayout);

        var aboutPanel = UiTheme.CreateSectionPanel();
        var aboutLayout = CreateVerticalSectionLayout();
        aboutLayout.Controls.Add(UiTheme.CreateSectionTitle("О твикере"));

        var aboutTextLabel = new Label
        {
            Text = "ArbuzTweaker - open-source Windows utility для понятной настройки Windows, игровых конфигов и параметров запуска.\n\n" +
                   "Используемые методы твика в играх, по крайней мере автор на это надеется, легальные: программа не является читерским ПО, не даёт нечестного преимущества и работает через редактирование пользовательских конфигов, разрешённых файлов и настройку параметров запуска.\n\n" +
                   "Также твикер не лезет в память процесса, не делает DLL-инжект, не трогает сеть, античит или HWID, не занимается бан-обходом и не автоматизирует игру.\n\n" +
                   "Программа не является вирусом. Данные пользователей никуда не передаются и остаются только на компьютере пользователя.",
            MaximumSize = new Size(SettingsContentWidth, 0),
            AutoSize = true,
            ForeColor = UiTheme.TextMuted,
            Margin = new Padding(0)
        };

        aboutLayout.Controls.Add(aboutTextLabel);
        aboutPanel.Controls.Add(aboutLayout);

        _statusLabel = new Label
        {
            Text = string.Empty,
            AutoSize = true,
            ForeColor = UiTheme.AccentGreen,
            Margin = new Padding(0, 6, 0, 0)
        };

        root.Controls.Add(titleLabel, 0, 0);
        root.Controls.Add(updatesPanel, 0, 1);
        root.Controls.Add(warningsPanel, 0, 2);
        root.Controls.Add(maintenancePanel, 0, 3);
        root.Controls.Add(supportPanel, 0, 4);
        root.Controls.Add(aboutPanel, 0, 5);
        root.Controls.Add(_statusLabel, 0, 6);

        Controls.Add(root);
    }

    private static FlowLayoutPanel CreateVerticalSectionLayout()
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            FlowDirection = FlowDirection.TopDown,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
    }

    private static TableLayoutPanel CreateKeyValueRow(string labelText, out Label valueLabel, string valueText)
    {
        var row = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(0)
        };
        row.ColumnStyles.Add(new ColumnStyle());
        row.ColumnStyles.Add(new ColumnStyle());

        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            ForeColor = UiTheme.TextMuted,
            Margin = new Padding(0, 0, 12, 0)
        };

        valueLabel = new Label
        {
            Text = valueText,
            AutoSize = true,
            ForeColor = UiTheme.TextPrimary,
            Margin = new Padding(0)
        };

        row.Controls.Add(label, 0, 0);
        row.Controls.Add(valueLabel, 1, 0);
        return row;
    }

    private void LoadSettings()
    {
        _isLoadingSettings = true;
        var settings = _appSettingsService.Load();
        _updateCheckBox.Checked = settings.CheckForUpdatesOnStartup;
        _safeModeCheckBox.Checked = settings.SafeModeUserConfigOnly;
        _isLoadingSettings = false;

        if (settings.CheckForUpdatesOnStartup)
            _ = RefreshUpdateAvailabilityAsync(false);
        else
            SetUpdateAvailabilityStatus("Автопроверка отключена", UiTheme.TextDim);
    }

    private void SyncSafeModeFromSettings()
    {
        _isLoadingSettings = true;
        _safeModeCheckBox.Checked = _appSettingsService.Load().SafeModeUserConfigOnly;
        _isLoadingSettings = false;
    }

    private void UpdateCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isLoadingSettings)
            return;

        var settings = _appSettingsService.Load();
        settings.CheckForUpdatesOnStartup = _updateCheckBox.Checked;
        _appSettingsService.Save(settings);

        if (_updateCheckBox.Checked)
            _ = RefreshUpdateAvailabilityAsync(false);
        else
            SetUpdateAvailabilityStatus("Автопроверка отключена", UiTheme.TextDim);

        ShowStatus("Настройки сохранены", UiTheme.AccentGreen);
    }

    private void SafeModeCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isLoadingSettings)
            return;

        if (!_safeModeCheckBox.Checked && !UnsafeTweaksPrompt.ConfirmEnable(this, "отключение безопасного режима в настройках"))
        {
            _isLoadingSettings = true;
            _safeModeCheckBox.Checked = true;
            _isLoadingSettings = false;
            ShowStatus("Безопасный режим оставлен включённым", Color.Orange);
            return;
        }

        var settings = _appSettingsService.Load();
        settings.SafeModeUserConfigOnly = _safeModeCheckBox.Checked;
        settings.UnsafeTweaksRiskAccepted = !_safeModeCheckBox.Checked;
        _appSettingsService.Save(settings);
        ShowStatus(_safeModeCheckBox.Checked ? "Безопасный режим включён" : "Небезопасные твики разрешены", _safeModeCheckBox.Checked ? UiTheme.AccentGreen : Color.Orange);
    }

    private async Task CheckForUpdatesNowAsync()
    {
        await RefreshUpdateAvailabilityAsync(true);
    }

    private void ResetWarningsButton_Click(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "Сбросить все выборы предупреждений? После этого они снова будут показываться как при первом запуске.",
            "Подтверждение",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
            return;

        _resetWarningChoices();
        ShowStatus("Выборы предупреждений сброшены", UiTheme.AccentGreen);
    }

    private void OpenFileBackupsButton_Click()
    {
        var opened = _fileBackupService.OpenBackupFolder();
        ShowStatus(
            opened ? "Папка файловых бэкапов открыта" : "Не удалось открыть папку файловых бэкапов",
            opened ? UiTheme.AccentGreen : Color.Orange);
    }

    private void RestoreFileBackupButton_Click()
    {
        using var dialog = new FileBackupBrowserForm(_fileBackupService);
        dialog.ShowDialog(this);
    }

    private void OpenRegistryBackupsButton_Click()
    {
        var opened = _registryBackupService.OpenBackupFolder();
        ShowStatus(
            opened ? "Папка бэкапа реестра открыта" : "Не удалось открыть папку бэкапа реестра",
            opened ? UiTheme.AccentGreen : Color.Orange);
    }

    private void OpenLogButton_Click()
    {
        try
        {
            Directory.CreateDirectory(_logService.LogDirectory);
            if (!File.Exists(_logService.LogFilePath))
                File.WriteAllText(_logService.LogFilePath, string.Empty);

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{_logService.LogFilePath}\"",
                UseShellExecute = true
            });

            ShowStatus("Журнал открыт", UiTheme.AccentGreen);
        }
        catch
        {
            ShowStatus("Не удалось открыть журнал", Color.Orange);
        }
    }

    private void OpenSupportPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SupportUrl,
                UseShellExecute = true
            });

            ShowStatus("Страница поддержки открыта", UiTheme.AccentGreen);
        }
        catch
        {
            ShowStatus("Не удалось открыть страницу поддержки", Color.Orange);
        }
    }

    private void OpenLatestReleasePage()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = LatestReleaseUrl,
                UseShellExecute = true
            });
            ShowStatus("Страница релиза открыта", UiTheme.AccentGreen);
        }
        catch
        {
            ShowStatus("Не удалось открыть страницу релиза", Color.Orange);
        }
    }

    private void ExportProfileButton_Click()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Экспорт профиля ArbuzTweaker",
            Filter = "Zip archive (*.zip)|*.zip",
            FileName = $"ArbuzTweaker-profile-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
            AddExtension = true,
            DefaultExt = "zip",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            _profileService.ExportProfile(dialog.FileName);
            ShowStatus("Профиль экспортирован", UiTheme.AccentGreen);
        }
        catch
        {
            ShowStatus("Не удалось экспортировать профиль", Color.Orange);
        }
    }

    private void ImportProfileButton_Click()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Импорт профиля ArbuzTweaker",
            Filter = "Zip archive (*.zip)|*.zip",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var result = MessageBox.Show(
            "Импорт профиля перезапишет совпадающие локальные настройки. Текущие конфиги будут сохранены в бэкап перед импортом.\n\nПродолжить?",
            "Импорт профиля",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
            return;

        try
        {
            _profileService.ImportProfile(dialog.FileName);
            ShowStatus("Профиль импортирован. Перезапусти приложение, если вкладки уже были открыты.", UiTheme.AccentGreen);
        }
        catch
        {
            ShowStatus("Не удалось импортировать профиль", Color.Orange);
        }
    }

    private async Task RefreshUpdateAvailabilityAsync(bool promptDownload)
    {
        SetUpdateAvailabilityStatus("Проверка...", UiTheme.TextDim);

        var update = await _updateService.CheckForUpdateDetailsAsync();
        if (!update.HasUpdate || string.IsNullOrWhiteSpace(update.DownloadUrl))
        {
            SetUpdateAvailabilityStatus("Новых обновлений нет", UiTheme.TextMuted);

            if (promptDownload)
            {
                MessageBox.Show(
                    "Новых обновлений нет или релиз пока не опубликован.",
                    "Проверка обновлений",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            return;
        }

        SetUpdateAvailabilityStatus(
            string.IsNullOrWhiteSpace(update.ExpectedSha256)
                ? $"Доступна версия {update.NewVersion}; SHA256 релиза не опубликован"
                : $"Доступна версия {update.NewVersion}; SHA256 будет проверен",
            Color.Orange);

        if (!promptDownload)
            return;

        var result = MessageBox.Show(
            $"Доступна новая версия {update.NewVersion}.\n" +
            (string.IsNullOrWhiteSpace(update.ExpectedSha256)
                ? "Контрольная сумма релиза не опубликована.\n\n"
                : "Контрольная сумма релиза найдена и будет проверена после скачивания.\n\n") +
            "Скачать обновление?",
            "Обновление",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (result != DialogResult.Yes)
            return;

        var downloadedPath = await _updateService.DownloadUpdateAsync(update.DownloadUrl);
        if (!string.IsNullOrWhiteSpace(downloadedPath))
        {
            var isInstaller = string.Equals(update.AssetName, UpdateService.InstallerAssetName, StringComparison.OrdinalIgnoreCase)
                || downloadedPath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)
                || downloadedPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

            if (isInstaller)
            {
                var sha256 = _updateService.GetFileSha256(downloadedPath);
                if (!_updateService.VerifyFileSha256(downloadedPath, update.ExpectedSha256))
                {
                    MessageBox.Show(
                        "Обновление скачано, но контрольная сумма не совпала с релизом GitHub.\n\n" +
                        "Установщик не будет запущен.",
                        "Проверка обновления",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    SetUpdateAvailabilityStatus("SHA256 обновления не совпал", Color.OrangeRed);
                    return;
                }

                var hasSignature = _updateService.HasAuthenticodeSignature(downloadedPath);
                var installNowResult = MessageBox.Show(
                    "Обновление скачано.\n\n" +
                    $"SHA256: {sha256}\n\n" +
                    (string.IsNullOrWhiteSpace(update.ExpectedSha256)
                        ? "Контрольная сумма релиза не опубликована.\n\n"
                        : "SHA256 совпал с релизом GitHub.\n\n") +
                    (hasSignature
                        ? "Цифровая подпись найдена.\n\n"
                        : "Внимание: цифровая подпись не найдена. Запускайте файл только если доверяете этому релизу.\n\n") +
                    "Установить его сейчас?",
                    "Установка обновления",
                    MessageBoxButtons.YesNo,
                    hasSignature ? MessageBoxIcon.Question : MessageBoxIcon.Warning);

                if (installNowResult == DialogResult.Yes && _updateService.LaunchDownloadedUpdate(downloadedPath))
                {
                    FindForm()?.Close();
                    return;
                }
            }

            MessageBox.Show(
                "Обновление скачано.",
                "Успех",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        MessageBox.Show(
            "Не удалось скачать обновление.",
            "Ошибка",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private void SetUpdateAvailabilityStatus(string text, Color color)
    {
        _updateAvailabilityValueLabel.Text = text;
        _updateAvailabilityValueLabel.ForeColor = color;
    }

    private async void ShowStatus(string message, Color color)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = color;
        await Task.Delay(2000);
        _statusLabel.Text = string.Empty;
    }
}
