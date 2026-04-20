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
    private const string NovidOption = "-novid";

    private readonly ConfigService _configService;
    private readonly Dota2Service _dota2Service;
    private readonly string _configFileName = "dota2_launch_options.json";
    private TextBox _launchOptionsTextBox = null!;
    private CheckBox _novidCheckBox = null!;
    private Label _pathLabel = null!;
    private Label _statusLabel = null!;
    private bool _pathFound;
    private bool _includeAutoexecLaunchOption;
    private bool _isUpdatingLaunchOptionsUi;

    public DotaLaunchOptionsTab(ConfigService configService, Dota2Service dota2Service)
    {
        _configService = configService;
        _dota2Service = dota2Service;
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

    private void InitializeComponent()
    {
        var titleLabel = new Label
        {
            Text = "Dota 2 - Параметры запуска",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            Location = new Point(20, 20),
            AutoSize = true
        };

        _pathLabel = new Label
        {
            Text = "Поиск Dota 2...",
            Location = new Point(20, 50),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        var infoLabel = new Label
        {
            Text = "Эта вкладка читает и меняет строку LaunchOptions в localconfig.vdf. Здесь настраиваются именно параметры запуска Steam, а не autoexec.cfg.txt.",
            Location = new Point(20, 90),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        var launchOptionsLabel = new Label
        {
            Text = "Параметры запуска из файла:",
            Location = new Point(20, 125),
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };

        var launchOptionsHintLabel = new Label
        {
            Text = "Здесь отображаются и редактируются команды из LaunchOptions. Каждая команда должна быть с новой строки. +exec autoexec.cfg.txt тоже хранится здесь.",
            Location = new Point(20, 148),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        _launchOptionsTextBox = new TextBox
        {
            Location = new Point(20, 175),
            Size = new Size(740, 120),
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 10)
        };
        _launchOptionsTextBox.TextChanged += LaunchOptionsTextBox_TextChanged;

        _novidCheckBox = new CheckBox
        {
            Text = NovidOption,
            Location = new Point(20, 330),
            AutoSize = true,
            ForeColor = Color.White,
            Tag = NovidOption
        };
        _novidCheckBox.CheckedChanged += NovidCheckBox_CheckedChanged;

        var descriptionLabel = new Label
        {
            Text = "Отключает вступительный ролик при запуске игры.",
            Location = new Point(120, 332),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        var applyButton = new Button
        {
            Text = "Применить",
            Location = new Point(20, 390),
            Size = new Size(120, 35)
        };
        applyButton.Click += async (s, e) => await SaveAndApplyAsync();

        var helpButton = new Button
        {
            Text = "Как это работает?",
            Location = new Point(150, 390),
            Size = new Size(160, 35)
        };
        helpButton.Click += (s, e) => ShowHelpDialog();

        var openFileButton = new Button
        {
            Text = "Показать localconfig.vdf в папке",
            Location = new Point(320, 390),
            Size = new Size(230, 35)
        };
        openFileButton.Click += async (s, e) => await OpenLocalConfigFolderAsync();

        var resetButton = new Button
        {
            Text = "Сбросить",
            Location = new Point(560, 390),
            Size = new Size(120, 35)
        };
        resetButton.Click += async (s, e) => await ResetAsync();

        _statusLabel = new Label
        {
            Text = string.Empty,
            Location = new Point(20, 440),
            AutoSize = true,
            ForeColor = Color.Green
        };

        Controls.Add(titleLabel);
        Controls.Add(_pathLabel);
        Controls.Add(infoLabel);
        Controls.Add(launchOptionsLabel);
        Controls.Add(launchOptionsHintLabel);
        Controls.Add(_launchOptionsTextBox);
        Controls.Add(_novidCheckBox);
        Controls.Add(descriptionLabel);
        Controls.Add(applyButton);
        Controls.Add(helpButton);
        Controls.Add(openFileButton);
        Controls.Add(resetButton);
        Controls.Add(_statusLabel);
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

    private void NovidCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingLaunchOptionsUi)
            return;

        SetOptionLine(NovidOption, _novidCheckBox.Checked);
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

    private void LoadLaunchOptions(string launchOptions)
    {
        _includeAutoexecLaunchOption = ContainsPhrase(launchOptions, Dota2Service.AutoexecLaunchCommand);
        var remainingOptions = RemovePhrase(launchOptions, Dota2Service.AutoexecLaunchCommand);
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
        _novidCheckBox.Checked = lines.Contains(NovidOption);
        _isUpdatingLaunchOptionsUi = false;
    }

    private List<string> GetSelectedOptionsFromText()
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in _launchOptionsTextBox.Lines)
        {
            var line = NormalizeWhitespace(rawLine);
            if (string.IsNullOrWhiteSpace(line) || !seen.Add(line))
                continue;

            if (ContainsPhrase(line, Dota2Service.AutoexecLaunchCommand))
                continue;

            result.Add(line);
        }

        return result;
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
        var options = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var line = NormalizeWhitespace(rawLine);
            if (string.IsNullOrWhiteSpace(line) || !seen.Add(line))
                continue;

            if (ContainsPhrase(line, Dota2Service.AutoexecLaunchCommand))
                continue;

            options.Add(line);
        }

        return BuildLaunchOptionsText(options, _includeAutoexecLaunchOption);
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
            "Галочка -novid просто добавляет или убирает строку -novid в этом окне. После нажатия на 'Применить' твикер записывает содержимое окна обратно в localconfig.vdf.",
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

public class DotaLaunchOptionsConfigData
{
    public string[] EnabledOptions { get; set; } = Array.Empty<string>();

    public bool IncludeAutoexecLaunchOption { get; set; }

    public DateTime LastModified { get; set; }
}
