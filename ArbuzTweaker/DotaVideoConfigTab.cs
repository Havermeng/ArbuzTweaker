using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArbuzTweaker;

public partial class DotaVideoConfigTab : UserControl
{
    private static readonly VideoSettingDefinition[] SettingDefinitions =
    {
        new("setting.dota_portrait_animate", "false", "true", false, "Отключает анимацию портрета героя. Без галочки значение возвращается в true."),
        new("setting.cpu_level", "3", "2", false, "При галочке ставит значение 3, без галочки возвращает 2."),
        new("setting.mem_level", "3", "2", false, "При галочке ставит значение 3, без галочки возвращает 2."),
        new("setting.gpu_mem_level", "3", "2", false, "При галочке ставит значение 3, без галочки возвращает 2."),
        new("setting.fullscreen_min_on_focus_loss", "0", "1", false, "Для 2 и более мониторов: при галочке ставит 0, без галочки возвращает 1."),
        new("setting.version.advanced_video", "1", null, true, "Добавляет строку расширенных видео-настроек в video.txt."),
        new("setting.mindxlevel", "100", null, true, "Добавляет строку минимального DirectX level со значением 100."),
        new("setting.maxdxlevel", "100", null, true, "Добавляет строку максимального DirectX level со значением 100."),
        new("setting.dxlevel", "100", null, true, "Добавляет строку текущего DirectX level со значением 100.")
    };

    private readonly Dota2Service _dota2Service;
    private readonly Dictionary<string, CheckBox> _settingCheckBoxes = new(StringComparer.OrdinalIgnoreCase);
    private TextBox _videoTextBox = null!;
    private Panel _settingsPanel = null!;
    private Label _pathLabel = null!;
    private Label _statusLabel = null!;
    private bool _isUpdatingVideoUi;

    public DotaVideoConfigTab(Dota2Service dota2Service)
    {
        _dota2Service = dota2Service;
        InitializeComponent();
        LoadVideoConfigStateAsync();
    }

    private async void LoadVideoConfigStateAsync()
    {
        var videoPath = await _dota2Service.GetPrimaryVideoConfigPathAsync();
        if (!string.IsNullOrWhiteSpace(videoPath))
        {
            _pathLabel.Text = $"video.txt: {videoPath}";
            _pathLabel.ForeColor = Color.Green;
        }
        else
        {
            _pathLabel.Text = "Не удалось определить путь к video.txt.";
            _pathLabel.ForeColor = Color.Orange;
        }

        var content = await _dota2Service.LoadVideoConfigAsync();
        if (content != null)
            SetVideoText(content);
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
            Text = "Dota 2 - Видео конфиг",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6)
        };

        _pathLabel = new Label
        {
            Text = "Поиск video.txt...",
            AutoSize = true,
            ForeColor = Color.Gray,
            Margin = new Padding(0, 0, 0, 12)
        };

        var infoLabel = new Label
        {
            Text = "Эта вкладка читает и меняет файл video.txt. Одни галочки переключают уже существующие значения, а другие добавляют в файл недостающие строки.",
            AutoSize = true,
            ForeColor = Color.Gainsboro,
            MaximumSize = new Size(980, 0),
            Margin = new Padding(0, 0, 0, 12)
        };

        var videoLabel = new Label
        {
            Text = "Содержимое video.txt:",
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        };

        _videoTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 10),
            MinimumSize = new Size(0, 210),
            Margin = new Padding(0, 0, 0, 12)
        };
        _videoTextBox.TextChanged += VideoTextBox_TextChanged;

        var settingsLabel = new Label
        {
            Text = "Быстрые переключатели:",
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

        var openFolderButton = new Button { Text = "Показать video.txt", Size = new Size(210, 35), Margin = new Padding(0, 0, 10, 0) };
        openFolderButton.Click += async (s, e) => await OpenVideoConfigFolderAsync();

        var resetButton = new Button { Text = "Сбросить", Size = new Size(120, 35), Margin = new Padding(0) };
        resetButton.Click += async (s, e) => await ResetAsync();

        buttonsPanel.Controls.Add(applyButton);
        buttonsPanel.Controls.Add(helpButton);
        buttonsPanel.Controls.Add(openFolderButton);
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
        rootLayout.Controls.Add(videoLabel, 0, 3);
        rootLayout.Controls.Add(_videoTextBox, 0, 4);
        rootLayout.Controls.Add(settingsLabel, 0, 5);
        rootLayout.Controls.Add(_settingsPanel, 0, 6);
        rootLayout.Controls.Add(buttonsPanel, 0, 7);
        rootLayout.Controls.Add(_statusLabel, 0, 8);

        Controls.Add(rootLayout);
    }

    private async Task SaveAndApplyAsync()
    {
        var normalizedText = NormalizeVideoText(_videoTextBox.Text);
        if (!string.Equals(_videoTextBox.Text, normalizedText, StringComparison.Ordinal))
            SetVideoText(normalizedText);

        if (!await _dota2Service.SaveVideoConfigAsync(normalizedText))
        {
            ShowStatus("Не удалось сохранить video.txt", Color.Orange);
            return;
        }

        var readOnlyResult = MessageBox.Show(
            "Dota может сбрасывать видео параметры. Сделать video.txt только для чтения после сохранения?",
            "Предупреждение",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (readOnlyResult == DialogResult.Yes)
        {
            if (await _dota2Service.SetVideoConfigReadOnlyAsync(true))
                ShowStatus("Сохранено. video.txt переведен в режим только чтения", Color.Green);
            else
                ShowStatus("Сохранено, но не удалось включить только чтение", Color.Orange);

            return;
        }

        await _dota2Service.SetVideoConfigReadOnlyAsync(false);
        ShowStatus("Сохранено", Color.Green);
    }

    private async Task OpenVideoConfigFolderAsync()
    {
        var videoPath = await _dota2Service.GetPrimaryVideoConfigPathAsync();
        if (string.IsNullOrWhiteSpace(videoPath))
        {
            ShowStatus("Не удалось найти video.txt", Color.Orange);
            return;
        }

        await _dota2Service.LoadVideoConfigAsync();

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{videoPath}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            ShowStatus("Не удалось открыть папку с video.txt", Color.Orange);
        }
    }

    private void PopulateSettingsPanel()
    {
        if (_settingsPanel == null || _videoTextBox == null)
            return;

        var preserveState = _isUpdatingVideoUi;
        _isUpdatingVideoUi = true;

        _settingsPanel.SuspendLayout();
        _settingsPanel.Controls.Clear();
        _settingCheckBoxes.Clear();

        var y = 12;
        var availableWidth = Math.Max(620, _settingsPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 28);
        var checkBoxWidth = Math.Clamp((int)(availableWidth * 0.38), 240, 360);
        var descriptionX = 18 + checkBoxWidth + 18;
        var descriptionWidth = Math.Max(220, availableWidth - checkBoxWidth - 26);

        foreach (var definition in SettingDefinitions)
        {
            var checkBox = new CheckBox
            {
                Text = definition.Key,
                Location = new Point(18, y),
                Size = new Size(checkBoxWidth, 24),
                AutoSize = false,
                ForeColor = Color.White,
                Tag = definition.Key,
                BackColor = Color.Transparent,
                Checked = definition.IsEnabled(GetSettingValue(definition.Key))
            };
            checkBox.CheckedChanged += SettingCheckBox_CheckedChanged;

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
        _isUpdatingVideoUi = preserveState;
    }

    private async Task ResetAsync()
    {
        var lines = new List<string>(GetVideoLines());

        foreach (var definition in SettingDefinitions)
            lines = ApplySettingValue(lines, definition, false);

        SetVideoText(string.Join(Environment.NewLine, lines));
        await SaveAndApplyAsync();
    }

    private void SettingCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingVideoUi)
            return;

        if (sender is CheckBox checkBox && checkBox.Tag is string key)
        {
            var definition = GetDefinition(key);
            if (definition == null)
                return;

            var lines = ApplySettingValue(GetVideoLines(), definition, checkBox.Checked);
            SetVideoText(string.Join(Environment.NewLine, lines));
        }
    }

    private void VideoTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingVideoUi)
            return;

        UpdateSelectionFromText();
    }

    private void SetVideoText(string text)
    {
        _isUpdatingVideoUi = true;
        _videoTextBox.Text = NormalizeVideoText(text);
        UpdateSelectionFromText();
        _isUpdatingVideoUi = false;
    }

    private void UpdateSelectionFromText()
    {
        _isUpdatingVideoUi = true;

        foreach (var definition in SettingDefinitions)
            _settingCheckBoxes[definition.Key].Checked = definition.IsEnabled(GetSettingValue(definition.Key));

        _isUpdatingVideoUi = false;
    }

    private List<string> GetVideoLines()
    {
        return NormalizeVideoLines(_videoTextBox.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None));
    }

    private string NormalizeVideoText(string text)
    {
        return string.Join(Environment.NewLine, NormalizeVideoLines(text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)));
    }

    private string? GetSettingValue(string key)
    {
        var pattern = $"\"{Regex.Escape(key)}\"\\s*\"(?<value>[^\"]*)\"";

        foreach (var line in _videoTextBox.Lines)
        {
            var match = Regex.Match(line, pattern);
            if (match.Success)
                return match.Groups["value"].Value;
        }

        return null;
    }

    private List<string> UpsertSetting(List<string> lines, string key, string value)
    {
        lines = NormalizeVideoLines(lines);
        var pattern = $"\"{Regex.Escape(key)}\"\\s*\"(?<value>[^\"]*)\"";
        var replacement = $"\t\"{key}\"\t\t\"{value}\"";
        var closeBraceIndex = GetClosingBraceIndex(lines);

        for (var i = 0; i < lines.Count; i++)
        {
            if (Regex.IsMatch(lines[i], pattern))
            {
                lines[i] = replacement;
                return lines;
            }
        }

        lines.Insert(closeBraceIndex, replacement);
        return lines;
    }

    private List<string> RemoveSetting(List<string> lines, string key)
    {
        lines = NormalizeVideoLines(lines);
        var pattern = $"\"{Regex.Escape(key)}\"\\s*\"(?<value>[^\"]*)\"";

        for (var i = lines.Count - 1; i >= 0; i--)
        {
            if (Regex.IsMatch(lines[i], pattern))
                lines.RemoveAt(i);
        }

        return lines;
    }

    private List<string> NormalizeVideoLines(IEnumerable<string> lines)
    {
        var normalizedLines = lines
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        var openBraceIndex = normalizedLines.FindIndex(line => line == "{");
        var closeBraceIndex = normalizedLines.FindLastIndex(line => line == "}");
        if (openBraceIndex >= 0 && closeBraceIndex > openBraceIndex)
            return normalizedLines;

        var bodyLines = new List<string>();
        string? headerLine = null;

        foreach (var line in normalizedLines)
        {
            if (line == "{" || line == "}")
                continue;

            if (headerLine == null && IsHeaderLine(line))
            {
                headerLine = line;
                continue;
            }

            bodyLines.Add(line);
        }

        var result = new List<string>();
        if (headerLine != null)
            result.Add(headerLine);

        result.Add("{");
        result.AddRange(bodyLines);
        result.Add("}");
        return result;
    }

    private static bool IsHeaderLine(string line)
    {
        return line.StartsWith('"') &&
               !line.StartsWith("\"setting.", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetClosingBraceIndex(List<string> lines)
    {
        var closeBraceIndex = lines.FindLastIndex(line => line == "}");
        return closeBraceIndex >= 0 ? closeBraceIndex : lines.Count;
    }

    private List<string> ApplySettingValue(List<string> lines, VideoSettingDefinition definition, bool enabled)
    {
        if (enabled)
            return UpsertSetting(lines, definition.Key, definition.CheckedValue);

        if (definition.RemoveWhenUnchecked)
            return RemoveSetting(lines, definition.Key);

        return UpsertSetting(lines, definition.Key, definition.UncheckedValue ?? definition.CheckedValue);
    }

    private VideoSettingDefinition? GetDefinition(string key)
    {
        foreach (var definition in SettingDefinitions)
        {
            if (string.Equals(definition.Key, key, StringComparison.OrdinalIgnoreCase))
                return definition;
        }

        return null;
    }

    private async void ShowStatus(string message, Color color)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = color;
        await Task.Delay(2000);
        _statusLabel.Text = string.Empty;
    }

    private void ShowHelpDialog()
    {
        MessageBox.Show(
            "Эта вкладка работает с файлом video.txt.\n\n" +
            "Большое окно показывает реальное содержимое файла. Изменения в этом окне сохраняются в video.txt при нажатии на 'Применить'.\n\n" +
            "Галочки ниже помогают быстро менять отдельные строки: часть из них переключает значения, а часть добавляет или удаляет строки в файле. После сохранения можно перевести video.txt в режим только чтения, чтобы игра не сбрасывала параметры.",
            "Как это работает?",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private sealed class VideoSettingDefinition
    {
        public VideoSettingDefinition(string key, string checkedValue, string? uncheckedValue, bool removeWhenUnchecked, string description)
        {
            Key = key;
            CheckedValue = checkedValue;
            UncheckedValue = uncheckedValue;
            RemoveWhenUnchecked = removeWhenUnchecked;
            Description = description;
        }

        public string Key { get; }

        public string CheckedValue { get; }

        public string? UncheckedValue { get; }

        public bool RemoveWhenUnchecked { get; }

        public string Description { get; }

        public bool IsEnabled(string? currentValue)
        {
            return string.Equals(currentValue, CheckedValue, StringComparison.OrdinalIgnoreCase);
        }
    }
}
