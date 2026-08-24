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
        new("setting.cpu_level", "3", "2", false, "Внутренний уровень детализации Dota на стороне процессора (галочка — 3, без неё — 2). Продвинутая настройка video.txt."),
        new("setting.mem_level", "3", "2", false, "Уровень использования оперативной памяти под ресурсы игры (галочка — 3, без неё — 2). Продвинутая настройка."),
        new("setting.gpu_mem_level", "3", "2", false, "Уровень использования видеопамяти под текстуры (галочка — 3, без неё — 2). Продвинутая настройка."),
        new("setting.fullscreen_min_on_focus_loss", "0", "1", false, "Для двух и более мониторов: галочка не даёт Dota сворачиваться, когда вы кликаете на другой монитор."),
        new("setting.version.advanced_video", "1", null, true, "Включает раздел расширенных видео-настроек в video.txt — нужен, чтобы работали параметры DirectX ниже."),
        new("setting.mindxlevel", "100", null, true, "Нижняя граница уровня DirectX (100 = DirectX 10). Форсирует графический путь рендеринга."),
        new("setting.maxdxlevel", "100", null, true, "Верхняя граница уровня DirectX (100 = DirectX 10). Форсирует графический путь рендеринга."),
        new("setting.dxlevel", "100", null, true, "Текущий уровень DirectX (100 = DirectX 10). Форсирует графический путь рендеринга; влияет на совместимость и производительность на старых видеокартах.")
    };

    private readonly Dota2Service _dota2Service;
    private readonly AppSettingsService _appSettingsService;
    private readonly Dictionary<string, CheckBox> _settingCheckBoxes = new(StringComparer.OrdinalIgnoreCase);
    private ComboBox _steamAccountComboBox = null!;
    private TextBox _videoTextBox = null!;
    private TextBox _settingsSearchTextBox = null!;
    private Panel _settingsPanel = null!;
    private Label _pathLabel = null!;
    private Label _statusLabel = null!;
    private Button _unlockReadOnlyButton = null!;
    private bool _isUpdatingVideoUi;
    private bool _isLoadingSteamAccounts;
    private int _statusToken;
    private int _lastSettingsPanelWidth = -1;

    public DotaVideoConfigTab(Dota2Service dota2Service, AppSettingsService appSettingsService)
    {
        _dota2Service = dota2Service;
        _appSettingsService = appSettingsService;
        InitializeComponent();
        LoadVideoConfigStateAsync();
    }

    private async void LoadVideoConfigStateAsync()
    {
        await LoadSteamAccountsAsync();
        await RefreshVideoPathStateAsync();

        var content = await _dota2Service.LoadVideoConfigAsync();
        if (content != null)
            SetVideoText(content);
    }

    private async Task LoadSteamAccountsAsync()
    {
        _isLoadingSteamAccounts = true;
        _steamAccountComboBox.Items.Clear();

        var settings = _appSettingsService.Load();
        _dota2Service.PreferredSteamAccountId32 = settings.PreferredSteamAccountId32;
        var users = await _dota2Service.GetSteamUsersAsync();

        foreach (var user in users)
            _steamAccountComboBox.Items.Add(user);

        if (users.Count == 0)
        {
            _steamAccountComboBox.Enabled = false;
            _isLoadingSteamAccounts = false;
            return;
        }

        _steamAccountComboBox.Enabled = true;
        var selectedUser = users.FirstOrDefault(user => string.Equals(user.AccountId32, settings.PreferredSteamAccountId32, StringComparison.OrdinalIgnoreCase))
            ?? users.First();
        _steamAccountComboBox.SelectedItem = selectedUser;
        _dota2Service.PreferredSteamAccountId32 = selectedUser.AccountId32;
        _isLoadingSteamAccounts = false;
    }

    private void InitializeComponent()
    {
        AutoScroll = true;

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoScroll = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(20, 10, 20, 20),
            ColumnCount = 1,
            RowCount = 12
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 140F));
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 210F));
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
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = Color.Gainsboro,
            MaximumSize = new Size(980, 0),
            Margin = new Padding(0, 0, 0, 12)
        };

        var steamAccountLabel = new Label
        {
            Text = "Steam-аккаунт для video.txt:",
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        };

        _steamAccountComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 520,
            Enabled = false,
            Margin = new Padding(0, 0, 0, 12)
        };
        _steamAccountComboBox.SelectedIndexChanged += async (s, e) => await SteamAccountComboBox_SelectedIndexChangedAsync();

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
            MinimumSize = new Size(0, 0),
            Margin = new Padding(0, 0, 0, 12)
        };
        UiTheme.StyleEditorTextBox(_videoTextBox);
        _videoTextBox.TextChanged += VideoTextBox_TextChanged;

        var settingsLabel = new Label
        {
            Text = "Быстрые переключатели:",
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8)
        };

        var settingsSearchPanel = CreateSettingsSearchPanel();

        _settingsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            MinimumSize = new Size(0, 0),
            Margin = new Padding(0, 0, 0, 12)
        };
        UiTheme.StyleListPanel(_settingsPanel);
        _settingsPanel.Resize += (s, e) =>
        {
            if (_settingsPanel.ClientSize.Width != _lastSettingsPanelWidth)
                PopulateSettingsPanel();
        };
        PopulateSettingsPanel();

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 10)
        };

        var applyButton = new Button { Text = "Применить", Size = new Size(120, 35), Margin = new Padding(0, 0, 10, 0) };
        applyButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, SaveAndApplyAsync);

        var helpButton = new Button { Text = "Как это работает?", AutoSize = true, MinimumSize = new Size(0, 35), Padding = new Padding(10, 0, 10, 0), Margin = new Padding(0, 0, 10, 0) };
        helpButton.Click += (s, e) => ShowHelpDialog();

        var openFolderButton = new Button { Text = "Показать video.txt", Size = new Size(210, 35), Margin = new Padding(0, 0, 10, 0) };
        openFolderButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, OpenVideoConfigFolderAsync);

        _unlockReadOnlyButton = new Button { Text = "Разблокировать video.txt", Size = new Size(210, 35), Margin = new Padding(0, 0, 10, 0) };
        _unlockReadOnlyButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, UnlockVideoConfigAsync);

        var resetButton = new Button { Text = "Сбросить", Size = new Size(120, 35), Margin = new Padding(0) };
        resetButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, ResetAsync);

        UiTheme.StyleActionButton(applyButton, true);
        UiTheme.StyleActionButton(helpButton);
        UiTheme.StyleActionButton(openFolderButton);
        UiTheme.StyleActionButton(_unlockReadOnlyButton);
        UiTheme.StyleActionButton(resetButton);

        buttonsPanel.Controls.Add(applyButton);
        buttonsPanel.Controls.Add(helpButton);
        buttonsPanel.Controls.Add(openFolderButton);
        buttonsPanel.Controls.Add(_unlockReadOnlyButton);
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
        rootLayout.Controls.Add(steamAccountLabel, 0, 3);
        rootLayout.Controls.Add(_steamAccountComboBox, 0, 4);
        rootLayout.Controls.Add(videoLabel, 0, 5);
        rootLayout.Controls.Add(_videoTextBox, 0, 6);
        rootLayout.Controls.Add(settingsLabel, 0, 7);
        rootLayout.Controls.Add(settingsSearchPanel, 0, 8);
        rootLayout.Controls.Add(_settingsPanel, 0, 9);
        rootLayout.Controls.Add(buttonsPanel, 0, 10);
        rootLayout.Controls.Add(_statusLabel, 0, 11);

        Controls.Add(rootLayout);
    }

    private FlowLayoutPanel CreateSettingsSearchPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 8)
        };

        _settingsSearchTextBox = new TextBox
        {
            Width = 360,
            Margin = new Padding(0, 0, 8, 0)
        };
        UiTheme.StyleSearchTextBox(_settingsSearchTextBox);
        _settingsSearchTextBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.SuppressKeyPress = true;
            PopulateSettingsPanel();
        };

        var searchButton = new Button { Text = "Найти", Size = new Size(100, 31), Margin = new Padding(0, 0, 8, 0) };
        searchButton.Click += (s, e) => PopulateSettingsPanel();
        UiTheme.StyleActionButton(searchButton, true);

        var clearButton = new Button { Text = "Сбросить поиск", Size = new Size(145, 31), Margin = new Padding(0) };
        clearButton.Click += (s, e) =>
        {
            _settingsSearchTextBox.Clear();
            PopulateSettingsPanel();
        };
        UiTheme.StyleActionButton(clearButton);

        panel.Controls.Add(_settingsSearchTextBox);
        panel.Controls.Add(searchButton);
        panel.Controls.Add(clearButton);
        return panel;
    }

    private async Task SteamAccountComboBox_SelectedIndexChangedAsync()
    {
        if (_isLoadingSteamAccounts || _steamAccountComboBox.SelectedItem is not SteamUserInfo user)
            return;

        var settings = _appSettingsService.Load();
        settings.PreferredSteamAccountId32 = user.AccountId32;
        _appSettingsService.Save(settings);
        _dota2Service.PreferredSteamAccountId32 = user.AccountId32;

        await RefreshVideoPathStateAsync();

        var content = await _dota2Service.LoadVideoConfigAsync();
        SetVideoText(content ?? string.Empty);
        ShowStatus($"Выбран Steam-аккаунт: {user.DisplayName}", Color.Green);
    }

    private async Task RefreshVideoPathStateAsync()
    {
        var videoPath = await _dota2Service.GetPrimaryVideoConfigPathAsync();
        if (string.IsNullOrWhiteSpace(videoPath))
        {
            _pathLabel.Text = "Не удалось определить путь к video.txt.";
            _pathLabel.ForeColor = Color.Orange;

            if (_unlockReadOnlyButton != null)
                _unlockReadOnlyButton.Enabled = false;

            return;
        }

        var isReadOnly = await _dota2Service.IsVideoConfigReadOnlyAsync();
        if (isReadOnly == true)
        {
            _pathLabel.Text = $"video.txt: {videoPath} (только чтение: настройки из меню Dota не сохранятся)";
            _pathLabel.ForeColor = Color.Orange;
        }
        else
        {
            _pathLabel.Text = $"video.txt: {videoPath}";
            _pathLabel.ForeColor = Color.Green;
        }

        if (_unlockReadOnlyButton != null)
            _unlockReadOnlyButton.Enabled = isReadOnly == true;
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
            "Если включить 'только чтение', Dota не сможет менять video.txt: настройки из твикера не будут сбрасываться, но изменения графики из меню игры тоже не сохранятся.\n\nВключить только чтение?",
            "Зафиксировать video.txt?",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (readOnlyResult == DialogResult.Yes)
        {
            if (await _dota2Service.SetVideoConfigReadOnlyAsync(true))
            {
                await RefreshVideoPathStateAsync();
                ShowStatus("Сохранено. video.txt переведен в режим только чтения", Color.Green);
            }
            else
                ShowStatus("Сохранено, но не удалось включить только чтение", Color.Orange);

            return;
        }

        await _dota2Service.SetVideoConfigReadOnlyAsync(false);
        await RefreshVideoPathStateAsync();
        ShowStatus("Сохранено", Color.Green);
    }

    private async Task UnlockVideoConfigAsync()
    {
        if (await _dota2Service.SetVideoConfigReadOnlyAsync(false))
        {
            await RefreshVideoPathStateAsync();
            ShowStatus("video.txt разблокирован. Dota сможет сохранять настройки графики.", Color.Green);
            return;
        }

        ShowStatus("Не удалось снять режим только чтения с video.txt", Color.Orange);
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
        _lastSettingsPanelWidth = _settingsPanel.ClientSize.Width;

        _settingsPanel.SuspendLayout();
        UiTheme.ClearAndDisposeControls(_settingsPanel);
        _settingCheckBoxes.Clear();

        var y = 8;
        var rowIndex = 0;
        var searchQuery = GetSettingsSearchQuery();
        var availableWidth = Math.Max(620, _settingsPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 24);

        y = UiTheme.AddListSectionHeader(_settingsPanel, y, availableWidth, "Параметры video.txt");

        foreach (var definition in SettingDefinitions)
        {
            y = UiTheme.AddCheckListRow(
                _settingsPanel,
                y,
                availableWidth,
                rowIndex,
                definition.Key,
                definition.Description,
                definition.IsEnabled(GetSettingValue(definition.Key)),
                SettingCheckBox_CheckedChanged,
                out var checkBox,
                definition.Key,
                MatchesSearch(definition.Key, definition.Description, searchQuery));

            _settingCheckBoxes[definition.Key] = checkBox;
            rowIndex++;
        }

        _settingsPanel.AutoScrollMinSize = new Size(0, y + 12);
        _settingsPanel.ResumeLayout();
        _isUpdatingVideoUi = preserveState;
    }

    private string GetSettingsSearchQuery()
    {
        return _settingsSearchTextBox?.Text.Trim() ?? string.Empty;
    }

    private static bool MatchesSearch(string command, string description, string query)
    {
        return !string.IsNullOrWhiteSpace(query)
            && (command.Contains(query, StringComparison.OrdinalIgnoreCase)
                || description.Contains(query, StringComparison.OrdinalIgnoreCase));
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
        {
            if (_settingCheckBoxes.TryGetValue(definition.Key, out var checkBox))
                checkBox.Checked = definition.IsEnabled(GetSettingValue(definition.Key));
        }

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
        var token = ++_statusToken;
        _statusLabel.Text = message;
        _statusLabel.ForeColor = color;
        await Task.Delay(4000);
        if (token == _statusToken)
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
