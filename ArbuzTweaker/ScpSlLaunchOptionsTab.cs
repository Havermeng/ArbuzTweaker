using System.Text.RegularExpressions;

namespace ArbuzTweaker;

public partial class ScpSlLaunchOptionsTab : UserControl
{
    private const string NoLogOption = "-nolog";
    private const string FDiscordOption = "-fdiscord";
    private const string RuWeakHttpSecurityOption = "-ru --weak-http-security";

    private readonly ScpSlService _scpSlService;
    private readonly AppSettingsService _appSettingsService;
    private ComboBox _steamAccountComboBox = null!;
    private TextBox _launchOptionsTextBox = null!;
    private TextBox _optionsSearchTextBox = null!;
    private Panel _optionsPanel = null!;
    private CheckBox _noLogCheckBox = null!;
    private CheckBox _discordCheckBox = null!;
    private CheckBox _ruWeakHttpSecurityCheckBox = null!;
    private Label _pathLabel = null!;
    private Label _statusLabel = null!;
    private bool _isUpdatingUi;
    private bool _isLoadingSteamAccounts;

    public ScpSlLaunchOptionsTab(ScpSlService scpSlService, AppSettingsService appSettingsService)
    {
        _scpSlService = scpSlService;
        _appSettingsService = appSettingsService;
        InitializeComponent();
        LoadStateAsync();
    }

    private async void LoadStateAsync()
    {
        var (gamePath, _) = await _scpSlService.FindGameAsync();
        if (gamePath != null)
        {
            _pathLabel.Text = $"SCP:SL найдена: {gamePath}";
            _pathLabel.ForeColor = Color.Green;
        }
        else
        {
            _pathLabel.Text = "SCP:SL не найдена. Параметры можно применить, если в Steam уже есть запись игры.";
            _pathLabel.ForeColor = Color.Orange;
        }

        await LoadSteamAccountsAsync();

        var currentLaunchOptions = await _scpSlService.GetCurrentLaunchOptionsAsync();
        if (!string.IsNullOrWhiteSpace(currentLaunchOptions))
            SetLaunchOptionsText(currentLaunchOptions);
    }

    private async Task LoadSteamAccountsAsync()
    {
        _isLoadingSteamAccounts = true;
        _steamAccountComboBox.Items.Clear();

        var settings = _appSettingsService.Load();
        _scpSlService.PreferredSteamAccountId32 = settings.PreferredSteamAccountId32;
        var users = await _scpSlService.GetSteamUsersAsync();

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
        _scpSlService.PreferredSteamAccountId32 = selectedUser.AccountId32;
        _isLoadingSteamAccounts = false;
    }

    private void InitializeComponent()
    {
        AutoScroll = false;

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = false,
            Padding = new Padding(20, 10, 20, 20),
            ColumnCount = 1,
            RowCount = 14
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130F));
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());

        var titleLabel = new Label
        {
            Text = "SCP:SL - параметры запуска",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6)
        };

        _pathLabel = new Label
        {
            Text = "Поиск SCP:SL...",
            AutoSize = true,
            ForeColor = Color.Gray,
            Margin = new Padding(0, 0, 0, 12)
        };

        var infoLabel = new Label
        {
            Text = "Вкладка редактирует только строку LaunchOptions для SCP: Secret Laboratory в пользовательском localconfig.vdf Steam.",
            AutoSize = true,
            ForeColor = Color.Gainsboro,
            MaximumSize = new Size(980, 0),
            Margin = new Padding(0, 0, 0, 12)
        };

        var steamAccountLabel = new Label
        {
            Text = "Steam-аккаунт для localconfig.vdf:",
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

        var launchOptionsLabel = new Label
        {
            Text = "Параметры запуска из файла:",
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        };

        var launchOptionsHintLabel = new Label
        {
            Text = "Каждая строка ниже - отдельная команда запуска. При сохранении твикер объединит строки и запишет их обратно в LaunchOptions.",
            AutoSize = true,
            ForeColor = Color.Gainsboro,
            MaximumSize = new Size(980, 0),
            Margin = new Padding(0, 0, 0, 10)
        };

        _launchOptionsTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            MinimumSize = new Size(0, 0),
            Margin = new Padding(0, 0, 0, 12)
        };
        UiTheme.StyleEditorTextBox(_launchOptionsTextBox);
        _launchOptionsTextBox.TextChanged += LaunchOptionsTextBox_TextChanged;

        var quickOptionsLabel = new Label
        {
            Text = "Готовые параметры:",
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        };

        var quickOptionsHintLabel = new Label
        {
            Text = "Эти пункты добавляют или убирают строки в LaunchOptions. Перед записью localconfig.vdf создаётся файловый бэкап.",
            AutoSize = true,
            ForeColor = Color.Gainsboro,
            MaximumSize = new Size(980, 0),
            Margin = new Padding(0, 0, 0, 10)
        };

        var quickOptionsSearchPanel = CreateOptionsSearchPanel();

        _optionsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Margin = new Padding(0, 0, 0, 12)
        };
        UiTheme.StyleListPanel(_optionsPanel);
        _optionsPanel.Resize += (s, e) => PopulateOptionsPanel();
        PopulateOptionsPanel();

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

        var helpButton = new Button { Text = "Как это работает?", Size = new Size(160, 35), Margin = new Padding(0, 0, 10, 0) };
        helpButton.Click += (s, e) => ShowHelpDialog();

        var openFileButton = new Button { Text = "Показать localconfig.vdf", Size = new Size(230, 35), Margin = new Padding(0, 0, 10, 0) };
        openFileButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, OpenLocalConfigFolderAsync);

        var resetButton = new Button { Text = "Сбросить", Size = new Size(120, 35), Margin = new Padding(0) };
        resetButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, ResetAsync);

        UiTheme.StyleActionButton(applyButton, true);
        UiTheme.StyleActionButton(helpButton);
        UiTheme.StyleActionButton(openFileButton);
        UiTheme.StyleActionButton(resetButton);

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
        rootLayout.Controls.Add(steamAccountLabel, 0, 3);
        rootLayout.Controls.Add(_steamAccountComboBox, 0, 4);
        rootLayout.Controls.Add(launchOptionsLabel, 0, 5);
        rootLayout.Controls.Add(launchOptionsHintLabel, 0, 6);
        rootLayout.Controls.Add(_launchOptionsTextBox, 0, 7);
        rootLayout.Controls.Add(quickOptionsLabel, 0, 8);
        rootLayout.Controls.Add(quickOptionsHintLabel, 0, 9);
        rootLayout.Controls.Add(quickOptionsSearchPanel, 0, 10);
        rootLayout.Controls.Add(_optionsPanel, 0, 11);
        rootLayout.Controls.Add(buttonsPanel, 0, 12);
        rootLayout.Controls.Add(_statusLabel, 0, 13);

        Controls.Add(rootLayout);
    }

    private FlowLayoutPanel CreateOptionsSearchPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 8)
        };

        _optionsSearchTextBox = new TextBox
        {
            Width = 360,
            Margin = new Padding(0, 0, 8, 0)
        };
        UiTheme.StyleSearchTextBox(_optionsSearchTextBox);
        _optionsSearchTextBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.SuppressKeyPress = true;
            PopulateOptionsPanel();
        };

        var searchButton = new Button { Text = "Найти", Size = new Size(100, 31), Margin = new Padding(0, 0, 8, 0) };
        searchButton.Click += (s, e) => PopulateOptionsPanel();
        UiTheme.StyleActionButton(searchButton, true);

        var clearButton = new Button { Text = "Сбросить поиск", Size = new Size(145, 31), Margin = new Padding(0) };
        clearButton.Click += (s, e) =>
        {
            _optionsSearchTextBox.Clear();
            PopulateOptionsPanel();
        };
        UiTheme.StyleActionButton(clearButton);

        panel.Controls.Add(_optionsSearchTextBox);
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
        _scpSlService.PreferredSteamAccountId32 = user.AccountId32;

        var currentLaunchOptions = await _scpSlService.GetCurrentLaunchOptionsAsync();
        SetLaunchOptionsText(currentLaunchOptions ?? string.Empty);
        ShowStatus($"Выбран Steam-аккаунт: {user.DisplayName}", Color.Green);
    }

    private async Task SaveAndApplyAsync()
    {
        var normalizedText = NormalizeLaunchOptionsText(_launchOptionsTextBox.Text);
        if (!string.Equals(_launchOptionsTextBox.Text, normalizedText, StringComparison.Ordinal))
            SetLaunchOptionsText(normalizedText);

        await ApplyLaunchOptionsAsync(GetSelectedOptionsFromText(), false);
    }

    private async Task ResetAsync()
    {
        var result = MessageBox.Show(
            "Сброс очистит все параметры запуска SCP:SL для выбранного Steam-аккаунта. Продолжить?",
            "Подтверждение",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
            return;

        SetLaunchOptionsText(string.Empty);
        await ApplyLaunchOptionsAsync(Array.Empty<string>(), true);
    }

    private async Task OpenLocalConfigFolderAsync()
    {
        var configPath = await _scpSlService.GetPrimaryLocalConfigPathAsync();
        if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
        {
            ShowStatus("Не удалось найти localconfig.vdf", Color.Orange);
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{configPath}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            ShowStatus("Не удалось открыть папку с localconfig.vdf", Color.Orange);
        }
    }

    private async Task ApplyLaunchOptionsAsync(IReadOnlyList<string> enabledOptions, bool isReset)
    {
        var needsUpdate = await _scpSlService.NeedsExactLaunchOptionsUpdateAsync(enabledOptions);

        var steamWasRunning = false;
        var steamClosed = false;

        if (needsUpdate && _scpSlService.IsSteamRunning())
        {
            steamWasRunning = true;

            var closeSteamResult = MessageBox.Show(
                "Steam сейчас запущен. Чтобы параметры запуска SCP:SL сразу отобразились в Steam и не были перезаписаны, лучше закрыть его перед применением.\n\nЗакрыть Steam сейчас?",
                "Steam запущен",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            if (closeSteamResult == DialogResult.Cancel)
                return;

            if (closeSteamResult == DialogResult.Yes)
            {
                steamClosed = await _scpSlService.CloseSteamAsync();
                if (!steamClosed)
                {
                    ShowStatus("Не удалось закрыть Steam", Color.Orange);
                    return;
                }
            }
        }

        if (needsUpdate)
        {
            var applyResult = await _scpSlService.SetExactLaunchOptionsAsync(enabledOptions);
            if (!applyResult.IsSuccess)
            {
                ShowStatus(applyResult.Message, Color.Orange);
                return;
            }
        }

        var baseMessage = isReset ? "Сброшено" : "Сохранено";

        if (steamClosed)
        {
            if (_scpSlService.StartSteam())
                ShowStatus($"{baseMessage}. Steam перезапущен", Color.Green);
            else
                ShowStatus($"{baseMessage}. Не удалось запустить Steam", Color.Orange);

            return;
        }

        if (steamWasRunning)
        {
            ShowStatus($"{baseMessage}. Перезапусти Steam", Color.Orange);
            return;
        }

        ShowStatus(baseMessage, Color.Green);
    }

    private void PopulateOptionsPanel()
    {
        if (_optionsPanel == null || _launchOptionsTextBox == null)
            return;

        var selectedOptions = new HashSet<string>(GetSelectedOptionsFromText(), StringComparer.OrdinalIgnoreCase);
        var preserveState = _isUpdatingUi;
        _isUpdatingUi = true;

        _optionsPanel.SuspendLayout();
        _optionsPanel.Controls.Clear();

        var y = 8;
        var rowIndex = 0;
        var searchQuery = GetOptionsSearchQuery();
        var availableWidth = Math.Max(620, _optionsPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 24);

        y = UiTheme.AddListSectionHeader(_optionsPanel, y, availableWidth, "Параметры запуска");
        AddOptionRow(ref y, ref rowIndex, ref _ruWeakHttpSecurityCheckBox, RuWeakHttpSecurityOption, "Для RU-региона. Иногда помогает с проблемами подключения к серверам игры.", selectedOptions.Contains(RuWeakHttpSecurityOption), searchQuery, RuWeakHttpSecurityCheckBox_CheckedChanged);
        AddOptionRow(ref y, ref rowIndex, ref _noLogCheckBox, NoLogOption, "Отключает часть логирования Unity/игры.", selectedOptions.Contains(NoLogOption), searchQuery, NoLogCheckBox_CheckedChanged);
        AddOptionRow(ref y, ref rowIndex, ref _discordCheckBox, FDiscordOption, "Запускает игру с Discord-авторизацией, если она нужна текущей сборке.", selectedOptions.Contains(FDiscordOption), searchQuery, DiscordCheckBox_CheckedChanged);

        _optionsPanel.AutoScrollMinSize = new Size(0, y + 12);
        _optionsPanel.ResumeLayout();
        _isUpdatingUi = preserveState;
    }

    private void AddOptionRow(
        ref int y,
        ref int rowIndex,
        ref CheckBox field,
        string optionText,
        string description,
        bool isChecked,
        string searchQuery,
        EventHandler handler)
    {
        y = UiTheme.AddCheckListRow(
            _optionsPanel,
            y,
            Math.Max(620, _optionsPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 24),
            rowIndex,
            optionText,
            description,
            isChecked,
            handler,
            out field,
            optionText,
            MatchesSearch(optionText, description, searchQuery));
        rowIndex++;
    }

    private string GetOptionsSearchQuery()
    {
        return _optionsSearchTextBox?.Text.Trim() ?? string.Empty;
    }

    private static bool MatchesSearch(string command, string description, string query)
    {
        return !string.IsNullOrWhiteSpace(query)
            && (command.Contains(query, StringComparison.OrdinalIgnoreCase)
                || description.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private void NoLogCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (!_isUpdatingUi)
            SetOptionLine(NoLogOption, _noLogCheckBox.Checked);
    }

    private void DiscordCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (!_isUpdatingUi)
            SetOptionLine(FDiscordOption, _discordCheckBox.Checked);
    }

    private void RuWeakHttpSecurityCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (!_isUpdatingUi)
            SetOptionLine(RuWeakHttpSecurityOption, _ruWeakHttpSecurityCheckBox.Checked);
    }

    private void LaunchOptionsTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (!_isUpdatingUi)
            UpdateSelectionFromText();
    }

    private void SetOptionLine(string option, bool enabled)
    {
        var options = GetSelectedOptionsFromText().ToList();
        options.RemoveAll(existing => string.Equals(existing, option, StringComparison.OrdinalIgnoreCase));

        if (enabled)
            options.Insert(0, option);

        SetLaunchOptionsText(BuildLaunchOptionsText(options));
    }

    private void SetLaunchOptionsText(string text)
    {
        var normalizedText = NormalizeLaunchOptionsText(text);

        _isUpdatingUi = true;
        _launchOptionsTextBox.Text = normalizedText;
        UpdateSelectionFromText();
        _isUpdatingUi = false;
    }

    private void UpdateSelectionFromText()
    {
        var lines = new HashSet<string>(GetSelectedOptionsFromText(), StringComparer.OrdinalIgnoreCase);
        _isUpdatingUi = true;
        _noLogCheckBox.Checked = lines.Contains(NoLogOption);
        _discordCheckBox.Checked = lines.Contains(FDiscordOption);
        _ruWeakHttpSecurityCheckBox.Checked = lines.Contains(RuWeakHttpSecurityOption);
        _isUpdatingUi = false;
    }

    private List<string> GetSelectedOptionsFromText()
    {
        return ParseLaunchOptionsText(_launchOptionsTextBox.Text);
    }

    private string BuildLaunchOptionsText(IEnumerable<string> options)
    {
        var lines = new List<string>();

        foreach (var option in options)
        {
            var normalizedOption = NormalizeWhitespace(option);
            if (!string.IsNullOrWhiteSpace(normalizedOption) &&
                !lines.Contains(normalizedOption, StringComparer.OrdinalIgnoreCase))
            {
                lines.Add(normalizedOption);
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string NormalizeLaunchOptionsText(string text)
    {
        return BuildLaunchOptionsText(ParseLaunchOptionsText(text));
    }

    private List<string> ParseLaunchOptionsText(string text)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var line = NormalizeWhitespace(rawLine);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            line = ExtractKnownOption(line, RuWeakHttpSecurityOption, result, seen);
            line = ExtractKnownOption(line, NoLogOption, result, seen);
            line = ExtractKnownOption(line, FDiscordOption, result, seen);

            line = NormalizeWhitespace(line);
            if (!string.IsNullOrWhiteSpace(line) && seen.Add(line))
                result.Add(line);
        }

        return result;
    }

    private string ExtractKnownOption(string line, string option, List<string> result, HashSet<string> seen)
    {
        if (!ContainsPhrase(line, option))
            return line;

        if (seen.Add(option))
            result.Add(option);

        return RemovePhrase(line, option);
    }

    private static bool ContainsPhrase(string text, string phrase)
    {
        return Regex.IsMatch(text, $@"(?<!\S){Regex.Escape(phrase)}(?!\S)", RegexOptions.IgnoreCase);
    }

    private static string RemovePhrase(string text, string phrase)
    {
        return Regex.Replace(text, $@"(?<!\S){Regex.Escape(phrase)}(?!\S)", string.Empty, RegexOptions.IgnoreCase);
    }

    private static string NormalizeWhitespace(string text)
    {
        return string.Join(
            " ",
            text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private void ShowHelpDialog()
    {
        MessageBox.Show(
            "Эта вкладка работает с LaunchOptions внутри localconfig.vdf для SCP: Secret Laboratory.\n\n" +
            "Большое окно показывает реальные параметры запуска из файла. Каждая строка в этом окне - отдельная команда запуска.\n\n" +
            "Твикер не меняет файлы игры, не трогает память процесса и не автоматизирует игру. Перед записью localconfig.vdf создаётся бэкап.",
            "Как это работает?",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private async void ShowStatus(string message, Color color)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = color;
        await Task.Delay(2500);
        _statusLabel.Text = string.Empty;
    }
}
