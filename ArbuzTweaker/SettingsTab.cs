using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArbuzTweaker;

public partial class SettingsTab : UserControl
{
    private readonly AppSettingsService _appSettingsService;
    private readonly UpdateService _updateService;
    private readonly Action _resetWarningChoices;
    private CheckBox _updateCheckBox = null!;
    private Label _statusLabel = null!;
    private bool _isLoadingSettings;

    public SettingsTab(
        AppSettingsService appSettingsService,
        UpdateService updateService,
        Action resetWarningChoices)
    {
        _appSettingsService = appSettingsService;
        _updateService = updateService;
        _resetWarningChoices = resetWarningChoices;

        InitializeComponent();
        LoadSettings();
    }

    private void InitializeComponent()
    {
        AutoScroll = true;

        var titleLabel = new Label
        {
            Text = "Настройки",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            Location = new Point(20, 20),
            AutoSize = true
        };

        var updatesLabel = new Label
        {
            Text = "Обновления",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 70),
            AutoSize = true
        };

        _updateCheckBox = new CheckBox
        {
            Text = "Проверять обновления при запуске",
            Location = new Point(20, 100),
            AutoSize = true,
            ForeColor = Color.White
        };
        _updateCheckBox.CheckedChanged += UpdateCheckBox_CheckedChanged;

        var checkNowButton = new Button
        {
            Text = "Проверить обновления",
            Location = new Point(20, 140),
            Size = new Size(170, 35)
        };
        checkNowButton.Click += async (s, e) => await CheckForUpdatesNowAsync();

        var warningsLabel = new Label
        {
            Text = "Предупреждения",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 210),
            AutoSize = true
        };

        var resetWarningsButton = new Button
        {
            Text = "Сбросить выборы предупреждений",
            Location = new Point(20, 240),
            Size = new Size(250, 35)
        };
        resetWarningsButton.Click += ResetWarningsButton_Click;

        var aboutLabel = new Label
        {
            Text = "О твикере",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 310),
            AutoSize = true
        };

        var aboutTextLabel = new Label
        {
            Text = "ArbuzTweaker сделан при помощи вайбкодинга и создан для того, чтобы люди могли легко и понятно повышать производительность своих устройств.\n\n" +
                   "Используемые методы твика в играх безопасные: программа не является читерским ПО, не дает преимущества и использует только разрешенные инструменты Valve через редактирование разрешенных файлов и настройку параметров запуска.\n\n" +
                   "Программа не является вирусом. Данные пользователей никуда не передаются и остаются только на компьютере пользователя.",
            Location = new Point(20, 340),
            MaximumSize = new Size(820, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        _statusLabel = new Label
        {
            Text = string.Empty,
            Location = new Point(20, 510),
            AutoSize = true,
            ForeColor = Color.Green
        };

        Controls.Add(titleLabel);
        Controls.Add(updatesLabel);
        Controls.Add(_updateCheckBox);
        Controls.Add(checkNowButton);
        Controls.Add(warningsLabel);
        Controls.Add(resetWarningsButton);
        Controls.Add(aboutLabel);
        Controls.Add(aboutTextLabel);
        Controls.Add(_statusLabel);
    }

    private void LoadSettings()
    {
        _isLoadingSettings = true;
        var settings = _appSettingsService.Load();
        _updateCheckBox.Checked = settings.CheckForUpdatesOnStartup;
        _isLoadingSettings = false;
    }

    private void UpdateCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isLoadingSettings)
            return;

        var settings = _appSettingsService.Load();
        settings.CheckForUpdatesOnStartup = _updateCheckBox.Checked;
        _appSettingsService.Save(settings);
        ShowStatus("Настройки сохранены", Color.Green);
    }

    private async Task CheckForUpdatesNowAsync()
    {
        var (hasUpdate, newVersion, downloadUrl, assetName) = await _updateService.CheckForUpdateAsync();
        if (!hasUpdate || string.IsNullOrWhiteSpace(downloadUrl))
        {
            MessageBox.Show(
                "Новых обновлений нет или релиз пока не опубликован.",
                "Проверка обновлений",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Доступна новая версия {newVersion}.\nСкачать обновление?",
            "Обновление",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (result != DialogResult.Yes)
            return;

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
        ShowStatus("Выборы предупреждений сброшены", Color.Green);
    }

    private async void ShowStatus(string message, Color color)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = color;
        await Task.Delay(2000);
        _statusLabel.Text = string.Empty;
    }
}
