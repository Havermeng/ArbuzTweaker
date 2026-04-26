using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArbuzTweaker;

public partial class ScpSlLaunchOptionsTab : UserControl
{
    private const string NoLogOption = "-nolog";
    private const string RuOption = "-ru";
    private const string WindowModeExclusiveOption = "-window-mode exclusive";
    private const string ScreenFullscreenOption = "-screen-fullscreen";
    private const string ScreenQualityLowOption = "-screen-quality Low";
    private const string FDiscordOption = "-fdiscord";

    private readonly ScpSlService _scpSlService;
    private TextBox _launchOptionsTextBox = null!;
    private Panel _optionsPanel = null!;
    private CheckBox _nologCheckBox = null!;
    private CheckBox _ruCheckBox = null!;
    private CheckBox _windowModeCheckBox = null!;
    private CheckBox _fullscreenCheckBox = null!;
    private CheckBox _screenQualityCheckBox = null!;
    private CheckBox _fdiscordCheckBox = null!;
    private Label _pathLabel = null!;
    private Label _statusLabel = null!;
    private bool _pathFound;
    private bool _isUpdatingUi;

    public ScpSlLaunchOptionsTab(ScpSlService scpSlService)
    {
        _scpSlService = scpSlService;
        InitializeComponent();
        LoadStateAsync();
    }

    private async void LoadStateAsync()
    {
        var (gamePath, _) = await _scpSlService.FindGameAsync();
        if (gamePath != null)
        {
            _pathFound = true;
            _pathLabel.Text = $"SCP:SL найдена: {gamePath}";
            _pathLabel.ForeColor = Color.Green;
        }
        else
        {
            _pathLabel.Text = "SCP:SL не найдена. Можно подготовить параметры локально.";
            _pathLabel.ForeColor = Color.Orange;
        }

        var currentLaunchOptions = await _scpSlService.GetCurrentLaunchOptionsAsync();
        if (!string.IsNullOrWhiteSpace(currentLaunchOptions))
            LoadLaunchOptions(currentLaunchOptions);
    }

    private void InitializeComponent()
    {
        AutoScroll = false;

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 11
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());

        var titleLabel = new Label
        {
            Text = "SCP:SL - Параметры запуска",
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
            Text = "Эта вкладка читает и меняет строку LaunchOptions для SCP: Secret Laboratory в localconfig.vdf.",
            AutoSize = true,
            ForeColor = Color.Gainsboro,
            MaximumSize = new Size(980, 0),
            Margin = new Padding(0, 0, 0, 12)
        };

        var launchOptionsLabel = new Label
        {
            Text = "Параметры запуска из файла:",
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        };

        var launchOptionsHintLabel = new Label
        {
            Text = "Каждая строка в окне ниже - отдельная команда запуска для SCP:SL.",
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
            Font = new Font("Consolas", 10),
            MinimumSize = new Size(0, 190),
            Margin = new Padding(0, 0, 0, 12)
        };
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

        _optionsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(35, 35, 35),
            Margin = new Padding(0, 0, 0, 12)
        };
        _optionsPanel.Resize += (s, e) => PopulateOptionsPanel();
        PopulateOptionsPanel();

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

        var openFileButton = new Button { Text = "Показать localconfig.vdf", Size = new Size(230, 35), Margin = new Padding(0, 0, 10, 0) };
        openFileButton.Click += async (s, e) => await OpenLocalConfigFolderAsync();

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
        rootLayout.Controls.Add(launchOptionsLabel, 0, 3);
        rootLayout.Controls.Add(launchOptionsHintLabel, 0, 4);
        rootLayout.Controls.Add(_launchOptionsTextBox, 0, 5);
        rootLayout.Controls.Add(quickOptionsLabel, 0, 6);
        rootLayout.Controls.Add(quickOptionsHintLabel, 0, 7);
        rootLayout.Controls.Add(_optionsPanel, 0, 8);
        rootLayout.Controls.Add(buttonsPanel, 0, 9);
        rootLayout.Controls.Add(_statusLabel, 0, 10);

        Controls.Add(rootLayout);
    }

    private async Task SaveAndApplyAsync()
    {
        var normalizedText = NormalizeLaunchOptionsText(_launchOptionsTextBox.Text);
        if (!string.Equals(_launchOptionsTextBox.Text, normalizedText, StringComparison.Ordinal))
            SetLaunchOptionsText(normalizedText);

        var selectedOptions = GetSelectedOptionsFromText();

        if (!_pathFound)
        {
            ShowStatus("SCP:SL не найдена", Color.Orange);
            return;
        }

        await ApplyLaunchOptionsAsync(selectedOptions);
    }

    private async Task ResetAsync()
    {
        SetLaunchOptionsText(string.Empty);

        if (!_pathFound)
        {
            ShowStatus("SCP:SL не найдена", Color.Orange);
            return;
        }

        await ApplyLaunchOptionsAsync(Array.Empty<string>());
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

    private async Task ApplyLaunchOptionsAsync(IReadOnlyList<string> enabledOptions)
    {
        var managedOptions = new[]
        {
            NoLogOption,
            RuOption,
            WindowModeExclusiveOption,
            ScreenFullscreenOption,
            ScreenQualityLowOption,
            FDiscordOption
        };

        var needsUpdate = await _scpSlService.NeedsLaunchOptionsUpdateAsync(enabledOptions, managedOptions);

        bool steamWasRunning = false;
        bool steamClosed = false;

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
            var applyResult = await _scpSlService.SetLaunchOptionsAsync(enabledOptions, managedOptions);
            if (!applyResult.IsSuccess)
            {
                ShowStatus(applyResult.Message, Color.Orange);
                return;
            }
        }

        if (steamClosed)
        {
            if (_scpSlService.StartSteam())
                ShowStatus("Сохранено. Steam перезапущен", Color.Green);
            else
                ShowStatus("Сохранено. Не удалось запустить Steam", Color.Orange);

            return;
        }

        if (steamWasRunning)
        {
            ShowStatus("Сохранено. Перезапусти Steam", Color.Orange);
            return;
        }

        ShowStatus("Параметры запуска сохранены", Color.Green);
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

        var y = 12;
        var availableWidth = Math.Max(620, _optionsPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 28);
        var checkBoxWidth = Math.Clamp((int)(availableWidth * 0.32), 150, 260);
        var descriptionX = 18 + checkBoxWidth + 18;
        var descriptionWidth = Math.Max(260, availableWidth - checkBoxWidth - 26);

        AddOptionRow(ref y, ref _nologCheckBox, NoLogOption, "Отключает запись логов игры.", selectedOptions.Contains(NoLogOption), checkBoxWidth, descriptionX, descriptionWidth, NoLogCheckBox_CheckedChanged);
        AddOptionRow(ref y, ref _ruCheckBox, RuOption, "Иногда помогает при ошибках подключения к центральным серверам.", selectedOptions.Contains(RuOption), checkBoxWidth, descriptionX, descriptionWidth, RuCheckBox_CheckedChanged);
        AddOptionRow(ref y, ref _windowModeCheckBox, WindowModeExclusiveOption, "Включает эксклюзивный полноэкранный режим окна.", selectedOptions.Contains(WindowModeExclusiveOption), checkBoxWidth, descriptionX, descriptionWidth, WindowModeCheckBox_CheckedChanged);
        AddOptionRow(ref y, ref _fullscreenCheckBox, ScreenFullscreenOption, "Запускает игру в полноэкранном режиме.", selectedOptions.Contains(ScreenFullscreenOption), checkBoxWidth, descriptionX, descriptionWidth, FullscreenCheckBox_CheckedChanged);
        AddOptionRow(ref y, ref _screenQualityCheckBox, ScreenQualityLowOption, "Ставит низкое качество изображения через launch options.", selectedOptions.Contains(ScreenQualityLowOption), checkBoxWidth, descriptionX, descriptionWidth, ScreenQualityCheckBox_CheckedChanged);
        AddOptionRow(ref y, ref _fdiscordCheckBox, FDiscordOption, "Включает авторизацию в игре через Discord.", selectedOptions.Contains(FDiscordOption), checkBoxWidth, descriptionX, descriptionWidth, FDiscordCheckBox_CheckedChanged);

        _optionsPanel.ResumeLayout();
        _isUpdatingUi = preserveState;
    }

    private void AddOptionRow(ref int y, ref CheckBox field, string optionText, string description, bool isChecked, int checkBoxWidth, int descriptionX, int descriptionWidth, EventHandler handler)
    {
        field = new CheckBox
        {
            Text = optionText,
            Location = new Point(18, y),
            Size = new Size(checkBoxWidth, 24),
            AutoSize = false,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Checked = isChecked
        };
        field.CheckedChanged += handler;

        var descriptionFont = new Font("Segoe UI", 10);
        var descriptionSize = TextRenderer.MeasureText(
            description,
            descriptionFont,
            new Size(descriptionWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix | TextFormatFlags.Left);

        var descriptionLabel = new Label
        {
            Text = description,
            Location = new Point(descriptionX, y + 2),
            Size = new Size(descriptionWidth, Math.Max(28, descriptionSize.Height + 8)),
            AutoSize = false,
            UseMnemonic = false,
            TextAlign = ContentAlignment.TopLeft,
            Font = descriptionFont,
            ForeColor = Color.Gainsboro,
            BackColor = Color.Transparent
        };

        _optionsPanel.Controls.Add(field);
        _optionsPanel.Controls.Add(descriptionLabel);
        y += Math.Max(field.Height, descriptionLabel.Height) + 12;
    }

    private void NoLogCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingUi)
            return;

        SetOptionLine(NoLogOption, _nologCheckBox.Checked);
    }

    private void RuCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingUi)
            return;

        SetOptionLine(RuOption, _ruCheckBox.Checked);
    }

    private void WindowModeCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingUi)
            return;

        SetOptionLine(WindowModeExclusiveOption, _windowModeCheckBox.Checked);
    }

    private void FullscreenCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingUi)
            return;

        SetOptionLine(ScreenFullscreenOption, _fullscreenCheckBox.Checked);
    }

    private void ScreenQualityCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingUi)
            return;

        SetOptionLine(ScreenQualityLowOption, _screenQualityCheckBox.Checked);
    }

    private void FDiscordCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingUi)
            return;

        SetOptionLine(FDiscordOption, _fdiscordCheckBox.Checked);
    }

    private void LaunchOptionsTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingUi)
            return;

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

    private void LoadLaunchOptions(string launchOptions)
    {
        SetLaunchOptionsText(launchOptions);
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
        PopulateOptionsPanel();
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

            line = ExtractKnownOption(line, WindowModeExclusiveOption, result, seen);
            line = ExtractKnownOption(line, ScreenQualityLowOption, result, seen);
            line = ExtractKnownOption(line, NoLogOption, result, seen);
            line = ExtractKnownOption(line, ScreenFullscreenOption, result, seen);
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
        return string.Join(" ", text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private void ShowHelpDialog()
    {
        MessageBox.Show(
            "Эта вкладка работает с LaunchOptions внутри localconfig.vdf для SCP: Secret Laboratory.\n\n" +
            "Большое окно показывает реальные параметры запуска из файла. Каждая строка в этом окне - отдельная команда запуска.\n\n" +
            "Готовые пункты ниже просто добавляют или убирают стандартные строки в этом окне. После нажатия на 'Применить' твикер записывает содержимое окна обратно в localconfig.vdf.",
            "Как это работает?",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private async void ShowStatus(string message, Color color)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = color;
        await Task.Delay(2000);
        _statusLabel.Text = string.Empty;
    }
}
