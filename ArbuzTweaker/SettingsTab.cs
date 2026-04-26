using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArbuzTweaker;

public partial class SettingsTab : UserControl
{
    private readonly AppSettingsService _appSettingsService;
    private readonly UpdateService _updateService;
    private readonly Action _resetWarningChoices;
    private Label _currentVersionValueLabel = null!;
    private Label _updateAvailabilityValueLabel = null!;
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
        BackColor = UiTheme.Surface;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
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
            ForeColor = UiTheme.TextPrimary,
            Margin = new Padding(0, 4, 0, 12)
        };
        _updateCheckBox.CheckedChanged += UpdateCheckBox_CheckedChanged;

        var checkNowButton = new Button
        {
            Text = "Проверить",
            Size = new Size(170, 35),
            Margin = new Padding(0, 0, 0, 0)
        };
        UiTheme.StyleActionButton(checkNowButton, true);
        checkNowButton.Click += async (s, e) => await CheckForUpdatesNowAsync();

        updatesLayout.Controls.Add(currentVersionRow);
        updatesLayout.Controls.Add(updateAvailabilityRow);
        updatesLayout.Controls.Add(_updateCheckBox);
        updatesLayout.Controls.Add(checkNowButton);
        updatesPanel.Controls.Add(updatesLayout);

        var warningsPanel = UiTheme.CreateSectionPanel();
        var warningsLayout = CreateVerticalSectionLayout();
        warningsLayout.Controls.Add(UiTheme.CreateSectionTitle("Предупреждения"));

        var warningsHint = new Label
        {
            Text = "Сбрасывает сохранённые подтверждения, после чего предупреждения снова будут показываться как при первом использовании.",
            MaximumSize = new Size(880, 0),
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

        warningsLayout.Controls.Add(warningsHint);
        warningsLayout.Controls.Add(resetWarningsButton);
        warningsPanel.Controls.Add(warningsLayout);

        var aboutPanel = UiTheme.CreateSectionPanel();
        var aboutLayout = CreateVerticalSectionLayout();
        aboutLayout.Controls.Add(UiTheme.CreateSectionTitle("О твикере"));

        var aboutTextLabel = new Label
        {
            Text = "ArbuzTweaker сделан при помощи вайбкодинга и создан для того, чтобы люди могли легко и понятно повышать производительность своих устройств.\n\n" +
                   "Используемые методы твика в играх безопасные: программа не является читерским ПО, не даёт преимущества и использует только разрешённые инструменты Valve через редактирование разрешённых файлов и настройку параметров запуска.\n\n" +
                   "Программа не является вирусом. Данные пользователей никуда не передаются и остаются только на компьютере пользователя.",
            MaximumSize = new Size(880, 0),
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
        root.Controls.Add(aboutPanel, 0, 3);
        root.Controls.Add(_statusLabel, 0, 4);

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
        _isLoadingSettings = false;

        if (settings.CheckForUpdatesOnStartup)
            _ = RefreshUpdateAvailabilityAsync(false);
        else
            SetUpdateAvailabilityStatus("Автопроверка отключена", UiTheme.TextDim);
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

    private async Task RefreshUpdateAvailabilityAsync(bool promptDownload)
    {
        SetUpdateAvailabilityStatus("Проверка...", UiTheme.TextDim);

        var (hasUpdate, newVersion, downloadUrl, assetName) = await _updateService.CheckForUpdateAsync();
        if (!hasUpdate || string.IsNullOrWhiteSpace(downloadUrl))
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

        SetUpdateAvailabilityStatus($"Доступна версия {newVersion}", Color.Orange);

        if (!promptDownload)
            return;

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
