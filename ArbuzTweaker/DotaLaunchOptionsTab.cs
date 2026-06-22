using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArbuzTweaker;

public partial class DotaLaunchOptionsTab : UserControl
{
    private const string HighOption = "-high";
    private const string NoHltvOption = "-nohltv";
    private const string NovidOption = "-novid";
    private const string MapDotaOption = "-map dota";
    private const string PrewarmOption = "-prewarm";
    private const string ThreadsOptionPrefix = "-threads";

    private readonly ConfigService _configService;
    private readonly Dota2Service _dota2Service;
    private readonly AppSettingsService _appSettingsService;
    private readonly string _configFileName = "dota2_launch_options.json";
    private readonly string _threadsOption;
    private ComboBox _steamAccountComboBox = null!;
    private TextBox _launchOptionsTextBox = null!;
    private TextBox _optionsSearchTextBox = null!;
    private Panel _optionsPanel = null!;
    private CheckBox _highCheckBox = null!;
    private CheckBox _noHltvCheckBox = null!;
    private CheckBox _threadsCheckBox = null!;
    private CheckBox _novidCheckBox = null!;
    private CheckBox _mapDotaCheckBox = null!;
    private CheckBox _prewarmCheckBox = null!;
    private Label _pathLabel = null!;
    private Label _statusLabel = null!;
    private bool _pathFound;
    private bool _includeAutoexecLaunchOption;
    private bool _isUpdatingLaunchOptionsUi;
    private bool _isLoadingSteamAccounts;

    public DotaLaunchOptionsTab(ConfigService configService, Dota2Service dota2Service, AppSettingsService appSettingsService)
    {
        _configService = configService;
        _dota2Service = dota2Service;
        _appSettingsService = appSettingsService;
        _threadsOption = $"{ThreadsOptionPrefix} {GetSuggestedThreadCount()}";
        InitializeComponent();
        LoadStateAsync();
    }

    private async void LoadStateAsync()
    {
        var (dotaPath, _) = await _dota2Service.FindDota2Async();
        if (dotaPath != null)
        {
            _pathFound = true;
            _pathLabel.Text = $"Dota 2 найдена: {dotaPath}";
            _pathLabel.ForeColor = Color.Green;
        }
        else
        {
            _pathLabel.Text = "Dota 2 не найдена. Можно подготовить параметры локально.";
            _pathLabel.ForeColor = Color.Orange;
        }

        await LoadSteamAccountsAsync();

        var content = await _configService.LoadConfigAsync(_configFileName);
        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                var config = System.Text.Json.JsonSerializer.Deserialize<DotaLaunchOptionsConfigData>(content);
                if (config != null)
                {
                    _includeAutoexecLaunchOption = config.IncludeAutoexecLaunchOption;
                    SetLaunchOptionsText(BuildLaunchOptionsText(config.EnabledOptions ?? Array.Empty<string>(), _includeAutoexecLaunchOption));
                }
            }
            catch { }
        }

        var currentLaunchOptions = await _dota2Service.GetCurrentLaunchOptionsAsync();
        if (!string.IsNullOrWhiteSpace(currentLaunchOptions))
            LoadLaunchOptions(currentLaunchOptions);
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
            Text = "Dota 2 - Параметры запуска",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6)
        };

        _pathLabel = new Label
        {
            Text = "Поиск Dota 2...",
            AutoSize = true,
            ForeColor = Color.Gray,
            Margin = new Padding(0, 0, 0, 12)
        };

        var infoLabel = new Label
        {
            Text = "Эта вкладка читает и меняет строку LaunchOptions в localconfig.vdf. Здесь настраиваются именно параметры запуска Steam, а не autoexec.cfg.",
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
            Text = "Здесь отображаются и редактируются команды из LaunchOptions. Каждая команда должна быть с новой строки. +exec autoexec.cfg тоже хранится здесь.",
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
            Text = "Эти пункты добавляют или убирают строки в LaunchOptions. Список ниже автоматически подстраивается под ширину окна.",
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
        _optionsPanel.Resize += (s, e) => PopulateLaunchOptionsPanel();
        PopulateLaunchOptionsPanel();

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
            PopulateLaunchOptionsPanel();
        };

        var searchButton = new Button { Text = "Найти", Size = new Size(100, 31), Margin = new Padding(0, 0, 8, 0) };
        searchButton.Click += (s, e) => PopulateLaunchOptionsPanel();
        UiTheme.StyleActionButton(searchButton, true);

        var clearButton = new Button { Text = "Сбросить поиск", Size = new Size(145, 31), Margin = new Padding(0) };
        clearButton.Click += (s, e) =>
        {
            _optionsSearchTextBox.Clear();
            PopulateLaunchOptionsPanel();
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
        _dota2Service.PreferredSteamAccountId32 = user.AccountId32;

        var currentLaunchOptions = await _dota2Service.GetCurrentLaunchOptionsAsync();
        SetLaunchOptionsText(currentLaunchOptions ?? string.Empty);
        ShowStatus($"Выбран Steam-аккаунт: {user.DisplayName}", Color.Green);
    }

    private async Task SaveAndApplyAsync()
    {
        var normalizedText = NormalizeLaunchOptionsText(_launchOptionsTextBox.Text);
        if (!string.Equals(_launchOptionsTextBox.Text, normalizedText, StringComparison.Ordinal))
            SetLaunchOptionsText(normalizedText);

        var selectedOptions = GetSelectedOptionsFromText();
        await SaveLocalConfigAsync(selectedOptions);

        if (!_pathFound)
        {
            ShowStatus("Сохранено локально. Dota 2 не найдена", Color.Orange);
            return;
        }

        await ApplyLaunchOptionsAsync(selectedOptions, "Сохранено!", "Сохранено");
    }

    private async Task ResetAsync()
    {
        SetLaunchOptionsText(string.Empty);
        await SaveLocalConfigAsync(Array.Empty<string>());

        if (!_pathFound)
        {
            ShowStatus("Сброшено локально. Dota 2 не найдена", Color.Orange);
            return;
        }

        await ApplyLaunchOptionsAsync(Array.Empty<string>(), "Параметры запуска сброшены.", "Сброшено");
    }

    private async Task SaveLocalConfigAsync(IReadOnlyList<string> enabledOptions)
    {
        var config = new DotaLaunchOptionsConfigData
        {
            EnabledOptions = enabledOptions.ToArray(),
            IncludeAutoexecLaunchOption = _includeAutoexecLaunchOption,
            LastModified = DateTime.Now
        };

        var json = System.Text.Json.JsonSerializer.Serialize(
            config,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        await _configService.SaveConfigAsync(_configFileName, json);
    }

    private async Task OpenLocalConfigFolderAsync()
    {
        var configPath = await _dota2Service.GetPrimaryLocalConfigPathAsync();
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

    private async Task ApplyLaunchOptionsAsync(
        IReadOnlyList<string> enabledOptions,
        string successMessage,
        string actionLabel)
    {
        var needsLaunchOptionsUpdate = await _dota2Service.NeedsExactLaunchOptionsUpdateAsync(
            enabledOptions,
            _includeAutoexecLaunchOption);

        bool steamWasRunning = false;
        bool steamClosed = false;

        if (needsLaunchOptionsUpdate && _dota2Service.IsSteamRunning())
        {
            steamWasRunning = true;

            var closeSteamResult = MessageBox.Show(
                "Steam сейчас запущен. Чтобы параметры запуска сразу отобразились в Steam и не были перезаписаны, лучше закрыть его перед применением.\n\nЗакрыть Steam сейчас?",
                "Steam запущен",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            if (closeSteamResult == DialogResult.Cancel)
                return;

            if (closeSteamResult == DialogResult.Yes)
            {
                steamClosed = await _dota2Service.CloseSteamAsync();
                if (!steamClosed)
                {
                    ShowStatus("Не удалось закрыть Steam", Color.Orange);
                    return;
                }
            }
        }

        if (needsLaunchOptionsUpdate)
        {
            var applyResult = await _dota2Service.SetExactLaunchOptionsAsync(
                enabledOptions,
                _includeAutoexecLaunchOption);

            if (!applyResult.IsSuccess)
            {
                ShowStatus(applyResult.Message, Color.Orange);
                return;
            }
        }

        if (steamClosed)
        {
            if (_dota2Service.StartSteam())
                ShowStatus($"{actionLabel}. Steam перезапущен", Color.Green);
            else
                ShowStatus($"{actionLabel}. Не удалось запустить Steam", Color.Orange);

            return;
        }

        if (steamWasRunning)
        {
            ShowStatus($"{actionLabel}. Перезапусти Steam", Color.Orange);
            return;
        }

        ShowStatus(successMessage, Color.Green);
    }

    private void PopulateLaunchOptionsPanel()
    {
        if (_optionsPanel == null || _launchOptionsTextBox == null)
            return;

        var selectedOptions = new HashSet<string>(GetSelectedOptionsFromText(), StringComparer.OrdinalIgnoreCase);
        var preserveState = _isUpdatingLaunchOptionsUi;
        _isUpdatingLaunchOptionsUi = true;

        _optionsPanel.SuspendLayout();
        _optionsPanel.Controls.Clear();

        var y = 8;
        var rowIndex = 0;
        var searchQuery = GetOptionsSearchQuery();
        var availableWidth = Math.Max(620, _optionsPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 24);

        y = UiTheme.AddListSectionHeader(_optionsPanel, y, availableWidth, "Параметры запуска");
        AddLaunchOptionRow(ref y, ref rowIndex, ref _highCheckBox, HighOption, "Выставляет высокий приоритет процесса Dota 2.", selectedOptions.Contains(HighOption), searchQuery, HighCheckBox_CheckedChanged);
        AddLaunchOptionRow(ref y, ref rowIndex, ref _noHltvCheckBox, NoHltvOption, "Отключает компоненты HLTV/GOTV, если они не используются.", selectedOptions.Contains(NoHltvOption), searchQuery, NoHltvCheckBox_CheckedChanged);
        AddLaunchOptionRow(ref y, ref rowIndex, ref _threadsCheckBox, _threadsOption, "Автоматически подставляет количество физических ядер процессора в параметр -threads.", selectedOptions.Any(IsThreadsOption), searchQuery, ThreadsCheckBox_CheckedChanged);
        AddLaunchOptionRow(ref y, ref rowIndex, ref _novidCheckBox, NovidOption, "Отключает вступительный ролик при запуске игры.", selectedOptions.Contains(NovidOption), searchQuery, NovidCheckBox_CheckedChanged);
        AddLaunchOptionRow(ref y, ref rowIndex, ref _mapDotaCheckBox, MapDotaOption, "Запускает загрузку карты dota при старте клиента.", selectedOptions.Contains(MapDotaOption), searchQuery, MapDotaCheckBox_CheckedChanged);
        AddLaunchOptionRow(ref y, ref rowIndex, ref _prewarmCheckBox, PrewarmOption, "Предзагружает игровые ресурсы; может уменьшить проблемы при загрузке в матч.", selectedOptions.Contains(PrewarmOption), searchQuery, PrewarmCheckBox_CheckedChanged);

        _optionsPanel.AutoScrollMinSize = new Size(0, y + 12);
        _optionsPanel.ResumeLayout();
        _isUpdatingLaunchOptionsUi = preserveState;
    }

    private void AddLaunchOptionRow(
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

    private void HighCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingLaunchOptionsUi)
            return;

        SetOptionLine(HighOption, _highCheckBox.Checked);
    }

    private void NoHltvCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingLaunchOptionsUi)
            return;

        SetOptionLine(NoHltvOption, _noHltvCheckBox.Checked);
    }

    private void ThreadsCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingLaunchOptionsUi)
            return;

        SetThreadsOption(_threadsCheckBox.Checked);
    }

    private void NovidCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingLaunchOptionsUi)
            return;

        SetOptionLine(NovidOption, _novidCheckBox.Checked);
    }

    private void MapDotaCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingLaunchOptionsUi)
            return;

        SetOptionLine(MapDotaOption, _mapDotaCheckBox.Checked);
    }

    private void PrewarmCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingLaunchOptionsUi)
            return;

        SetOptionLine(PrewarmOption, _prewarmCheckBox.Checked);
    }

    private void LaunchOptionsTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingLaunchOptionsUi)
            return;

        UpdateSelectionFromText();
    }

    private void SetOptionLine(string option, bool enabled)
    {
        var options = GetSelectedOptionsFromText().ToList();
        options.RemoveAll(existing => string.Equals(existing, option, StringComparison.OrdinalIgnoreCase));

        if (enabled)
            options.Insert(0, option);

        SetLaunchOptionsText(BuildLaunchOptionsText(options, _includeAutoexecLaunchOption));
    }

    private void SetThreadsOption(bool enabled)
    {
        var options = GetSelectedOptionsFromText().ToList();
        options.RemoveAll(IsThreadsOption);

        if (enabled)
            options.Insert(0, _threadsOption);

        SetLaunchOptionsText(BuildLaunchOptionsText(options, _includeAutoexecLaunchOption));
    }

    private void LoadLaunchOptions(string launchOptions)
    {
        _includeAutoexecLaunchOption = ContainsPhrase(launchOptions, Dota2Service.AutoexecLaunchCommand)
            || ContainsPhrase(launchOptions, Dota2Service.LegacyAutoexecLaunchCommand);
        var remainingOptions = RemovePhrase(launchOptions, Dota2Service.AutoexecLaunchCommand);
        remainingOptions = RemovePhrase(remainingOptions, Dota2Service.LegacyAutoexecLaunchCommand);
        SetLaunchOptionsText(remainingOptions);
    }

    private void SetLaunchOptionsText(string text)
    {
        var normalizedText = NormalizeLaunchOptionsText(text);

        _isUpdatingLaunchOptionsUi = true;
        _launchOptionsTextBox.Text = normalizedText;
        UpdateSelectionFromText();
        _isUpdatingLaunchOptionsUi = false;
    }

    private void UpdateSelectionFromText()
    {
        var lines = new HashSet<string>(GetSelectedOptionsFromText(), StringComparer.OrdinalIgnoreCase);
        _isUpdatingLaunchOptionsUi = true;
        _highCheckBox.Checked = lines.Contains(HighOption);
        _noHltvCheckBox.Checked = lines.Contains(NoHltvOption);
        _threadsCheckBox.Checked = lines.Any(IsThreadsOption);
        _novidCheckBox.Checked = lines.Contains(NovidOption);
        _mapDotaCheckBox.Checked = lines.Contains(MapDotaOption);
        _prewarmCheckBox.Checked = lines.Contains(PrewarmOption);
        _isUpdatingLaunchOptionsUi = false;
    }

    private List<string> GetSelectedOptionsFromText()
    {
        return ParseLaunchOptionsText(_launchOptionsTextBox.Text);
    }

    private string BuildLaunchOptionsText(IEnumerable<string> options, bool includeAutoexec)
    {
        var lines = new List<string>();

        if (includeAutoexec)
            lines.Add(Dota2Service.AutoexecLaunchCommand);

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
        return BuildLaunchOptionsText(ParseLaunchOptionsText(text), _includeAutoexecLaunchOption);
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

            if (ContainsPhrase(line, Dota2Service.AutoexecLaunchCommand))
                line = RemovePhrase(line, Dota2Service.AutoexecLaunchCommand);

            if (ContainsPhrase(line, Dota2Service.LegacyAutoexecLaunchCommand))
                line = RemovePhrase(line, Dota2Service.LegacyAutoexecLaunchCommand);

            line = NormalizeWhitespace(line);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            line = ExtractKnownOption(line, MapDotaOption, result, seen);
            line = ExtractKnownOption(line, PrewarmOption, result, seen);
            line = ExtractKnownOption(line, HighOption, result, seen);
            line = ExtractKnownOption(line, NoHltvOption, result, seen);
            line = ExtractKnownOption(line, NovidOption, result, seen);
            line = ExtractThreadsOption(line, result, seen);

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

    private string ExtractThreadsOption(string line, List<string> result, HashSet<string> seen)
    {
        var match = Regex.Match(line, $@"(?<!\S){Regex.Escape(ThreadsOptionPrefix)}\s+\d+(?!\S)", RegexOptions.IgnoreCase);
        if (!match.Success)
            return line;

        if (seen.Add(_threadsOption))
            result.Add(_threadsOption);

        return Regex.Replace(line, $@"(?<!\S){Regex.Escape(ThreadsOptionPrefix)}\s+\d+(?!\S)", string.Empty, RegexOptions.IgnoreCase);
    }

    private static bool ContainsPhrase(string text, string phrase)
    {
        return Regex.IsMatch(
            text,
            $@"(?<!\S){Regex.Escape(phrase)}(?!\S)",
            RegexOptions.IgnoreCase);
    }

    private static string RemovePhrase(string text, string phrase)
    {
        return Regex.Replace(
            text,
            $@"(?<!\S){Regex.Escape(phrase)}(?!\S)",
            string.Empty,
            RegexOptions.IgnoreCase);
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
            "Эта вкладка работает с LaunchOptions внутри localconfig.vdf.\n\n" +
            "Большое окно показывает реальные параметры запуска из файла. Каждая строка в этом окне - отдельная команда запуска.\n\n" +
            "Галочки -high, -nohltv, -threads, -novid, -map dota и -prewarm просто добавляют или убирают соответствующие строки в этом окне. Для -threads твикер автоматически подставляет количество физических ядер процессора. После нажатия на 'Применить' твикер записывает содержимое окна обратно в localconfig.vdf.",
            "Как это работает?",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static bool IsThreadsOption(string option)
    {
        return Regex.IsMatch(option, $@"^{Regex.Escape(ThreadsOptionPrefix)}\s+\d+$", RegexOptions.IgnoreCase);
    }

    private static int GetSuggestedThreadCount()
    {
        return Math.Max(1, Environment.ProcessorCount);
    }

    private async void ShowStatus(string message, Color color)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = color;
        await Task.Delay(2000);
        _statusLabel.Text = string.Empty;
    }
}

public class DotaLaunchOptionsConfigData
{
    public string[] EnabledOptions { get; set; } = Array.Empty<string>();

    public bool IncludeAutoexecLaunchOption { get; set; }

    public DateTime LastModified { get; set; }
}
