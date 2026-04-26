using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArbuzTweaker;

public partial class ScpSlBootConfigTab : UserControl
{
    private readonly ScpSlService _scpSlService;
    private readonly string _jobWorkerCountValue;
    private TextBox _bootConfigTextBox = null!;
    private Panel _settingsPanel = null!;
    private Label _pathLabel = null!;
    private Label _statusLabel = null!;
    private bool _isUpdatingUi;
    private Dictionary<string, CheckBox> _settingCheckBoxes = new(StringComparer.OrdinalIgnoreCase);

    private BootConfigSettingDefinition[] SettingDefinitions => new[]
    {
        new BootConfigSettingDefinition("gfx-enable-native-gfx-jobs", "1", "Включает native gfx jobs."),
        new BootConfigSettingDefinition("gfx-enable-gfx-jobs", "1", "Включает gfx jobs."),
        new BootConfigSettingDefinition("gfx-disable-mt-rendering", "1", "Отключает многопоточный рендеринг Unity, если это нужно для стабильности."),
        new BootConfigSettingDefinition("job-worker-count", _jobWorkerCountValue, "Автоматически подставляет количество физических ядер процессора."),
        new BootConfigSettingDefinition("hdr-display-enabled", "0", "Отключает HDR в boot.config."),
        new BootConfigSettingDefinition("gc-max-time-slice", "1", "Ограничивает время GC в одном слайсе."),
        new BootConfigSettingDefinition("no-stereo-rendering", "1", "Отключает stereo rendering."),
        new BootConfigSettingDefinition("force-feature-level-9-3", "1", "Форсирует feature level 9_3."),
        new BootConfigSettingDefinition("wait-for-managed-debugger", "0", "Отключает ожидание managed debugger."),
        new BootConfigSettingDefinition("wait-for-native-debugger", "0", "Отключает ожидание native debugger."),
        new BootConfigSettingDefinition("scripting-runtime-version", "latest", "Использует latest scripting runtime."),
        new BootConfigSettingDefinition("vr-enabled", "0", "Отключает VR.")
    };

    public ScpSlBootConfigTab(ScpSlService scpSlService)
    {
        _scpSlService = scpSlService;
        _jobWorkerCountValue = GetPhysicalCoreCount().ToString();
        InitializeComponent();
        LoadStateAsync();
    }

    private async void LoadStateAsync()
    {
        var bootConfigPath = await _scpSlService.GetBootConfigPathAsync();
        if (!string.IsNullOrWhiteSpace(bootConfigPath))
        {
            _pathLabel.Text = $"boot.config: {bootConfigPath}";
            _pathLabel.ForeColor = Color.Green;
        }
        else
        {
            _pathLabel.Text = "Не удалось определить путь к boot.config.";
            _pathLabel.ForeColor = Color.Orange;
        }

        var content = await _scpSlService.LoadBootConfigAsync();
        if (content != null)
            SetBootConfigText(content);
    }

    private void InitializeComponent()
    {
        AutoScroll = false;

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 10
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());

        var titleLabel = new Label
        {
            Text = "SCP:SL - boot.config",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6)
        };

        _pathLabel = new Label
        {
            Text = "Поиск boot.config...",
            AutoSize = true,
            ForeColor = Color.Gray,
            Margin = new Padding(0, 0, 0, 12)
        };

        var infoLabel = new Label
        {
            Text = "Эта вкладка читает и меняет boot.config для SCP: Secret Laboratory. Галочки ниже добавляют или убирают рекомендуемые строки в этот файл.",
            AutoSize = true,
            ForeColor = Color.Gainsboro,
            MaximumSize = new Size(980, 0),
            Margin = new Padding(0, 0, 0, 12)
        };

        var bootConfigLabel = new Label
        {
            Text = "Содержимое boot.config:",
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        };

        _bootConfigTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 10),
            MinimumSize = new Size(0, 170),
            Margin = new Padding(0, 0, 0, 12)
        };
        _bootConfigTextBox.TextChanged += BootConfigTextBox_TextChanged;

        var settingsLabel = new Label
        {
            Text = "Быстрые параметры boot.config:",
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8)
        };

        _settingsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(35, 35, 35),
            Margin = new Padding(0, 0, 0, 12)
        };
        _settingsPanel.Resize += (s, e) => PopulateSettingsPanel();
        PopulateSettingsPanel();

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 10)
        };

        var applyButton = new Button { Text = "Применить", Size = new Size(120, 35), Margin = new Padding(0, 0, 10, 0) };
        applyButton.Click += async (s, e) => await SaveAndApplyAsync();

        var helpButton = new Button { Text = "Как это работает?", Size = new Size(160, 35), Margin = new Padding(0, 0, 10, 0) };
        helpButton.Click += (s, e) => ShowHelpDialog();

        var openFileButton = new Button { Text = "Показать boot.config", Size = new Size(210, 35), Margin = new Padding(0, 0, 10, 0) };
        openFileButton.Click += async (s, e) => await OpenBootConfigFolderAsync();

        var resetButton = new Button { Text = "Сбросить", Size = new Size(120, 35), Margin = new Padding(0) };
        resetButton.Click += async (s, e) => await ResetAsync();

        buttonsPanel.Controls.Add(applyButton);
        buttonsPanel.Controls.Add(helpButton);
        buttonsPanel.Controls.Add(openFileButton);
        buttonsPanel.Controls.Add(resetButton);

        _statusLabel = new Label
        {
            Text = string.Empty,
            AutoSize = true,
            ForeColor = Color.Green,
            Margin = new Padding(0)
        };

        rootLayout.Controls.Add(titleLabel, 0, 0);
        rootLayout.Controls.Add(_pathLabel, 0, 1);
        rootLayout.Controls.Add(infoLabel, 0, 2);
        rootLayout.Controls.Add(bootConfigLabel, 0, 3);
        rootLayout.Controls.Add(_bootConfigTextBox, 0, 4);
        rootLayout.Controls.Add(settingsLabel, 0, 5);
        rootLayout.Controls.Add(_settingsPanel, 0, 6);
        rootLayout.Controls.Add(buttonsPanel, 0, 7);
        rootLayout.Controls.Add(_statusLabel, 0, 8);

        Controls.Add(rootLayout);
    }

    private async Task SaveAndApplyAsync()
    {
        var normalizedText = NormalizeBootConfigText(_bootConfigTextBox.Text);
        if (!string.Equals(_bootConfigTextBox.Text, normalizedText, StringComparison.Ordinal))
            SetBootConfigText(normalizedText);

        if (await _scpSlService.SaveBootConfigAsync(normalizedText))
        {
            ShowStatus("boot.config сохранён", Color.Green);
            return;
        }

        ShowStatus("Не удалось сохранить boot.config", Color.Orange);
    }

    private async Task OpenBootConfigFolderAsync()
    {
        var path = await _scpSlService.GetBootConfigPathAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            ShowStatus("Не удалось найти boot.config", Color.Orange);
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            ShowStatus("Не удалось открыть папку с boot.config", Color.Orange);
        }
    }

    private async Task ResetAsync()
    {
        var lines = GetBootConfigLines().Where(line => !IsManagedSetting(line)).ToList();
        SetBootConfigText(string.Join(Environment.NewLine, lines));
        await SaveAndApplyAsync();
    }

    private void PopulateSettingsPanel()
    {
        if (_settingsPanel == null || _bootConfigTextBox == null)
            return;

        var preserveState = _isUpdatingUi;
        _isUpdatingUi = true;

        _settingsPanel.SuspendLayout();
        _settingsPanel.Controls.Clear();
        _settingCheckBoxes.Clear();

        var y = 12;
        var availableWidth = Math.Max(620, _settingsPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 28);
        var checkBoxWidth = Math.Clamp((int)(availableWidth * 0.45), 260, 380);
        var descriptionX = 18 + checkBoxWidth + 18;
        var descriptionWidth = Math.Max(220, availableWidth - checkBoxWidth - 26);
        var lines = GetBootConfigLines();

        foreach (var definition in SettingDefinitions)
        {
            var checkBox = new CheckBox
            {
                Text = $"{definition.Key}={definition.Value}",
                Location = new Point(18, y),
                Size = new Size(checkBoxWidth, 24),
                AutoSize = false,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Checked = lines.Contains(definition.ToLine(), StringComparer.OrdinalIgnoreCase)
            };
            checkBox.CheckedChanged += (_, _) =>
            {
                if (_isUpdatingUi)
                    return;

                SetBootConfigLine(definition, checkBox.Checked);
            };

            var descriptionFont = new Font("Segoe UI", 10);
            var descriptionSize = TextRenderer.MeasureText(
                definition.Description,
                descriptionFont,
                new Size(descriptionWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix | TextFormatFlags.Left);

            var descriptionLabel = new Label
            {
                Text = definition.Description,
                Location = new Point(descriptionX, y + 2),
                Size = new Size(descriptionWidth, Math.Max(28, descriptionSize.Height + 8)),
                AutoSize = false,
                UseMnemonic = false,
                TextAlign = ContentAlignment.TopLeft,
                Font = descriptionFont,
                ForeColor = Color.Gainsboro,
                BackColor = Color.Transparent
            };

            _settingCheckBoxes[definition.Key] = checkBox;
            _settingsPanel.Controls.Add(checkBox);
            _settingsPanel.Controls.Add(descriptionLabel);
            y += Math.Max(checkBox.Height, descriptionLabel.Height) + 12;
        }

        _settingsPanel.ResumeLayout();
        _isUpdatingUi = preserveState;
    }

    private void BootConfigTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingUi)
            return;

        PopulateSettingsPanel();
    }

    private void SetBootConfigText(string text)
    {
        _isUpdatingUi = true;
        _bootConfigTextBox.Text = NormalizeBootConfigText(text);
        PopulateSettingsPanel();
        _isUpdatingUi = false;
    }

    private void SetBootConfigLine(BootConfigSettingDefinition definition, bool enabled)
    {
        var lines = GetBootConfigLines();
        lines.RemoveAll(line => line.StartsWith(definition.Key + "=", StringComparison.OrdinalIgnoreCase));

        if (enabled)
            lines.Add(definition.ToLine());

        SetBootConfigText(string.Join(Environment.NewLine, lines));
    }

    private List<string> GetBootConfigLines()
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in _bootConfigTextBox.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || !seen.Add(line))
                continue;

            result.Add(line);
        }

        return result;
    }

    private string NormalizeBootConfigText(string text)
    {
        return string.Join(Environment.NewLine, GetNormalizedBootConfigLines(text));
    }

    private List<string> GetNormalizedBootConfigLines(string text)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || !seen.Add(line))
                continue;

            result.Add(line);
        }

        return result;
    }

    private bool IsManagedSetting(string line)
    {
        foreach (var definition in SettingDefinitions)
        {
            if (line.StartsWith(definition.Key + "=", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void ShowHelpDialog()
    {
        MessageBox.Show(
            "Эта вкладка работает с файлом boot.config у SCP: Secret Laboratory.\n\n" +
            "Большое окно показывает реальное содержимое boot.config.\n\n" +
            "Галочки ниже добавляют или убирают рекомендуемые строки прямо в этот файл. Для job-worker-count твикер автоматически подставляет количество физических ядер процессора.",
            "Как это работает?",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static int GetPhysicalCoreCount()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("select NumberOfCores from Win32_Processor");
            var totalCores = 0;

            foreach (ManagementObject processor in searcher.Get())
            {
                if (processor["NumberOfCores"] is uint cores)
                    totalCores += (int)cores;
                else if (processor["NumberOfCores"] is int intCores)
                    totalCores += intCores;
            }

            if (totalCores > 0)
                return totalCores;
        }
        catch
        {
        }

        return Math.Max(1, Environment.ProcessorCount);
    }

    private async void ShowStatus(string message, Color color)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = color;
        await Task.Delay(2000);
        _statusLabel.Text = string.Empty;
    }

    private sealed class BootConfigSettingDefinition
    {
        public BootConfigSettingDefinition(string key, string value, string description)
        {
            Key = key;
            Value = value;
            Description = description;
        }

        public string Key { get; }

        public string Value { get; }

        public string Description { get; }

        public string ToLine() => $"{Key}={Value}";
    }
}
