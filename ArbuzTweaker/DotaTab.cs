using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArbuzTweaker;

public partial class DotaTab : UserControl
{
    private static readonly ConfigCommandGroupDefinition[] CommandGroups =
    {
        new(
            "FPS и производительность",
            new[]
            {
                new ConfigCommandDefinition("dota_cheap_water 1", "Упрощает отрисовку воды в реке."),
                new ConfigCommandDefinition("fps_max 240", "Ограничивает FPS в игре."),
                new ConfigCommandDefinition("fps_max_menu 120", "Ограничивает FPS в меню."),
                new ConfigCommandDefinition("cl_globallight_shadow_mode 0", "Отключает или упрощает глобальные тени."),
                new ConfigCommandDefinition("mat_queue_mode 2", "Включает многопоточную обработку рендера, если параметр поддерживается."),
                new ConfigCommandDefinition("mat_picmip 2", "Снижает качество текстур."),
                new ConfigCommandDefinition("mat_vsync 0", "Отключает вертикальную синхронизацию."),
                new ConfigCommandDefinition("mat_triplebuffered 0", "Отключает тройную буферизацию."),
                new ConfigCommandDefinition("r_deferrer 0", "Отключает часть deferred-рендера; эффект зависит от текущей версии клиента."),
                new ConfigCommandDefinition("r_deferred_additive_pass 0", "Отключает часть additive-эффектов deferred-рендера."),
                new ConfigCommandDefinition("r_deferred_height_fog 0", "Отключает height fog в deferred-рендере."),
                new ConfigCommandDefinition("r_deferred_specular 0", "Отключает specular-эффекты deferred-рендера."),
                new ConfigCommandDefinition("r_deferred_specular_bloom 0", "Отключает bloom для specular-эффектов deferred-рендера."),
                new ConfigCommandDefinition("r_renderoverlayfragment 0", "Отключает часть overlay-эффектов рендера."),
                new ConfigCommandDefinition("r_screenspace_aa 0", "Отключает экранное сглаживание."),
                new ConfigCommandDefinition("r_shadowrendertotexture 0", "Упрощает или отключает render-to-texture для теней."),
                new ConfigCommandDefinition("r_WaterDrawReflection 0", "Отключает отражения на воде."),
                new ConfigCommandDefinition("gpu_level 0", "Понижает пресет нагрузки на GPU."),
                new ConfigCommandDefinition("cpu_level 0", "Понижает пресет нагрузки на CPU."),
                new ConfigCommandDefinition("cl_interp_ratio 1", "Снижает коэффициент интерполяции." )
            }),
        new(
            "Интерфейс",
            new[]
            {
                new ConfigCommandDefinition("dota_hud_enable_dispel_effect 1", "Показывает надпись DISPEL при развеивании эффекта."),
                new ConfigCommandDefinition("net_graph 1", "Включает net_graph с сетевой информацией."),
                new ConfigCommandDefinition("net_graphheight 64", "Задает высоту net_graph."),
                new ConfigCommandDefinition("net_graphinsetbottom 425", "Задает отступ net_graph снизу."),
                new ConfigCommandDefinition("net_graphinsetright -150", "Задает отступ net_graph справа."),
                new ConfigCommandDefinition("net_graphproportionalfont 0", "Отключает пропорциональный шрифт в net_graph."),
                new ConfigCommandDefinition("net_graphtext 1", "Включает текстовую часть net_graph.")
            }),
        new(
            "Сеть и клиент",
            new[]
            {
                new ConfigCommandDefinition("cl_interp 0.01", "Задает минимальное значение интерполяции клиента."),
                new ConfigCommandDefinition("cl_lagcompensation 1", "Включает серверную компенсацию задержки."),
                new ConfigCommandDefinition("cl_pred_optimize 2", "Включает более агрессивную оптимизацию клиентского предсказания сети."),
                new ConfigCommandDefinition("cl_smooth 1", "Включает сглаживание обзора после ошибок предсказания клиента."),
                new ConfigCommandDefinition("cl_smoothtime 0.01", "Задает длительность сглаживания после ошибок предсказания клиента."),
                new ConfigCommandDefinition("cl_spectator_cmdrate_factor 0.5", "Меняет частоту сетевых обновлений в режиме наблюдателя.")
            }),
        new(
            "Отключение мусора",
            new[]
            {
                new ConfigCommandDefinition("dota_ambient_creatures 0", "Отключает фоновых существ на карте."),
                new ConfigCommandDefinition("dota_ambient_cloth 0", "Отключает анимацию ткани и похожих элементов."),
                new ConfigCommandDefinition("dota_embers 0", "Отключает частицы ember-эффекта в меню."),
                new ConfigCommandDefinition("+map_enable_portrait_worlds 0", "Отключает 3D-постеры и portrait worlds в главном меню. Это не управляет параметром setting.dota_portrait_animate из video.txt."),
                new ConfigCommandDefinition("dota_portrait_animate 0", "Отключает анимацию портрета героя через autoexec. Может пересекаться с setting.dota_portrait_animate в video.txt."),
                new ConfigCommandDefinition("r_dota_fxaa 1", "Включает FXAA-сглаживание."),
                new ConfigCommandDefinition("r_ssao 0", "Отключает SSAO."),
                new ConfigCommandDefinition("r_dota_allow_wind_on_trees 0", "Отключает анимацию ветра на деревьях."),
                new ConfigCommandDefinition("r_dota_allow_parallax_mapping 0", "Отключает parallax mapping у материалов."),
                new ConfigCommandDefinition("r_depth_of_field 0", "Отключает depth of field."),
                new ConfigCommandDefinition("r_dota_fsr_upsample 1", "Включает FSR-апскейлинг, если он поддерживается текущей сборкой.")
            }),
        new(
            "Остальное",
            new[]
            {
                new ConfigCommandDefinition("engine_no_focus_sleep 0", "Не дает игре замедляться при потере фокуса; может помочь после сворачивания окна."),
                new ConfigCommandDefinition("joystick 0", "Отключает поддержку джойстика."),
                new ConfigCommandDefinition("snd_disable_mixer_duck 1", "Отключает автоматическое приглушение звука."),
                new ConfigCommandDefinition("developer 1", "Включает расширенный вывод отладочных сообщений."),
                new ConfigCommandDefinition("con_enable 1", "Разрешает консоль.")
            })
    };

    private static readonly ConfigCommandDefinition[] AllCommandDefinitions = CommandGroups
        .SelectMany(group => group.Commands)
        .ToArray();

    private static readonly HashSet<string> NetworkSensitiveCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "cl_interp 0.01",
        "cl_lagcompensation 1",
        "cl_pred_optimize 2",
        "cl_smooth 1",
        "cl_smoothtime 0.01",
        "cl_spectator_cmdrate_factor 0.5"
    };

    private static readonly string[] LegacyManagedLaunchOptions = { "-console", "-novid" };

    private readonly ConfigService _configService;
    private readonly Dota2Service _dota2Service;
    private readonly string _configFileName = "dota2_config.json";
    private readonly Dictionary<string, CheckBox> _commandCheckBoxes = new(StringComparer.OrdinalIgnoreCase);
    private TextBox _autoexecTextBox = null!;
    private Panel _commandPanel = null!;
    private Button _saveButton = null!;
    private Button _resetButton = null!;
    private Button _helpButton = null!;
    private Button _openAutoexecButton = null!;
    private Label _statusLabel = null!;
    private Label _pathLabel = null!;
    private bool _pathFound;
    private bool _isUpdatingAutoexecUi;
    private string _lastSavedAutoexecText = string.Empty;

    public DotaTab(ConfigService configService, Dota2Service dota2Service)
    {
        _configService = configService;
        _dota2Service = dota2Service;
        InitializeComponent();
        LoadPathsAsync();
    }

    private async void LoadPathsAsync()
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
            _pathLabel.Text = "Dota 2 не найдена. Можно подготовить конфиг заранее.";
            _pathLabel.ForeColor = Color.Orange;
        }

        await LoadConfigAsync();
    }

    private void InitializeComponent()
    {
        var tabControl = new TabControl
        {
            Dock = DockStyle.Fill
        };

        var configPage = new TabPage
        {
            Text = "Конфиг",
            BackColor = Color.FromArgb(35, 35, 35),
            ForeColor = Color.White,
            AutoScroll = false
        };

        var launchOptionsPage = new TabPage
        {
            Text = "Параметры запуска",
            BackColor = Color.FromArgb(35, 35, 35),
            ForeColor = Color.White
        };

        var videoConfigPage = new TabPage
        {
            Text = "Видео конфиг",
            BackColor = Color.FromArgb(35, 35, 35),
            ForeColor = Color.White
        };

        var launchOptionsControl = new DotaLaunchOptionsTab(_configService, _dota2Service)
        {
            Dock = DockStyle.Fill
        };
        launchOptionsPage.Controls.Add(launchOptionsControl);

        var videoConfigControl = new DotaVideoConfigTab(_dota2Service)
        {
            Dock = DockStyle.Fill
        };
        videoConfigPage.Controls.Add(videoConfigControl);

        var configLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 10
        };
        configLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        configLayout.RowStyles.Add(new RowStyle());
        configLayout.RowStyles.Add(new RowStyle());
        configLayout.RowStyles.Add(new RowStyle());
        configLayout.RowStyles.Add(new RowStyle());
        configLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
        configLayout.RowStyles.Add(new RowStyle());
        configLayout.RowStyles.Add(new RowStyle());
        configLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
        configLayout.RowStyles.Add(new RowStyle());
        configLayout.RowStyles.Add(new RowStyle());

        var titleLabel = new Label
        {
            Text = "Dota 2 - Твики и конфиг",
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

        var configLabel = new Label
        {
            Text = $"Конфиг {Dota2Service.AutoexecFileName}:",
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        };

        var configHintLabel = new Label
        {
            Text = $"Здесь отображается и редактируется содержимое файла {Dota2Service.AutoexecFileName}. Отмеченные команды ниже тоже добавляются сюда.",
            AutoSize = true,
            ForeColor = Color.Gainsboro,
            MaximumSize = new Size(980, 0),
            Margin = new Padding(0, 0, 0, 10)
        };

        _autoexecTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 10),
            MinimumSize = new Size(0, 220),
            Margin = new Padding(0, 0, 0, 12)
        };
        _autoexecTextBox.TextChanged += AutoexecTextBox_TextChanged;

        var commandLabel = new Label
        {
            Text = "Готовые команды:",
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        };

        var commandHintLabel = new Label
        {
            Text = $"Эти галочки добавляют или убирают строки в {Dota2Service.AutoexecFileName}.",
            AutoSize = true,
            ForeColor = Color.Gainsboro,
            MaximumSize = new Size(980, 0),
            Margin = new Padding(0, 0, 0, 10)
        };

        _commandPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(35, 35, 35),
            Margin = new Padding(0, 0, 0, 12)
        };
        _commandPanel.Resize += (s, e) => PopulateCommandPanel();
        PopulateCommandPanel();

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 10)
        };

        _saveButton = new Button { Text = "Применить", Size = new Size(120, 35), Margin = new Padding(0, 0, 10, 0) };
        _saveButton.Click += async (s, e) => await SaveConfigAsync();

        _helpButton = new Button { Text = "Как это работает?", Size = new Size(160, 35), Margin = new Padding(0, 0, 10, 0) };
        _helpButton.Click += (s, e) => ShowHelpDialog();

        _openAutoexecButton = new Button { Text = $"Показать {Dota2Service.AutoexecFileName} в папке", Size = new Size(220, 35), Margin = new Padding(0, 0, 10, 0) };
        _openAutoexecButton.Click += async (s, e) => await OpenAutoexecFolderAsync();

        _resetButton = new Button { Text = "Сбросить", Size = new Size(120, 35), Margin = new Padding(0) };
        _resetButton.Click += async (s, e) => await ResetConfigAsync();

        buttonsPanel.Controls.Add(_saveButton);
        buttonsPanel.Controls.Add(_helpButton);
        buttonsPanel.Controls.Add(_openAutoexecButton);
        buttonsPanel.Controls.Add(_resetButton);

        _statusLabel = new Label
        {
            Text = string.Empty,
            AutoSize = true,
            ForeColor = Color.Green,
            Margin = new Padding(0)
        };

        configLayout.Controls.Add(titleLabel, 0, 0);
        configLayout.Controls.Add(_pathLabel, 0, 1);
        configLayout.Controls.Add(configLabel, 0, 2);
        configLayout.Controls.Add(configHintLabel, 0, 3);
        configLayout.Controls.Add(_autoexecTextBox, 0, 4);
        configLayout.Controls.Add(commandLabel, 0, 5);
        configLayout.Controls.Add(commandHintLabel, 0, 6);
        configLayout.Controls.Add(_commandPanel, 0, 7);
        configLayout.Controls.Add(buttonsPanel, 0, 8);
        configLayout.Controls.Add(_statusLabel, 0, 9);

        configPage.Controls.Add(configLayout);

        tabControl.TabPages.Add(launchOptionsPage);
        tabControl.TabPages.Add(configPage);
        tabControl.TabPages.Add(videoConfigPage);
        Controls.Add(tabControl);
    }

    private void PopulateCommandPanel()
    {
        if (_commandPanel == null || _autoexecTextBox == null)
            return;

        var preserveState = _isUpdatingAutoexecUi;
        _isUpdatingAutoexecUi = true;

        _commandPanel.SuspendLayout();
        _commandPanel.Controls.Clear();
        _commandCheckBoxes.Clear();

        var y = 10;
        var selectedCommands = new HashSet<string>(GetAutoexecLines(), StringComparer.OrdinalIgnoreCase);
        var availableWidth = Math.Max(620, _commandPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 28);
        var checkBoxWidth = Math.Clamp((int)(availableWidth * 0.40), 240, 360);
        var descriptionX = 18 + checkBoxWidth + 18;
        var descriptionWidth = Math.Max(220, availableWidth - checkBoxWidth - 26);

        foreach (var group in CommandGroups)
        {
            var groupLabel = new Label
            {
                Text = group.Title,
                Location = new Point(10, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.White
            };
            _commandPanel.Controls.Add(groupLabel);
            y += 24;

            foreach (var command in group.Commands)
            {
                var checkBox = new CheckBox
                {
                    Text = command.Command,
                    Location = new Point(18, y),
                    Size = new Size(checkBoxWidth, 24),
                    AutoSize = false,
                    ForeColor = Color.White,
                    Tag = command.Command,
                    BackColor = Color.Transparent,
                    Checked = selectedCommands.Contains(command.Command)
                };
                checkBox.CheckedChanged += CommandCheckBox_CheckedChanged;

                var descriptionFont = new Font("Segoe UI", 10);
                var descriptionSize = TextRenderer.MeasureText(
                    command.Description,
                    descriptionFont,
                    new Size(descriptionWidth, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix | TextFormatFlags.Left);

                var descriptionLabel = new Label
                {
                    Text = command.Description,
                    Location = new Point(descriptionX, y + 2),
                    Size = new Size(descriptionWidth, Math.Max(28, descriptionSize.Height + 8)),
                    AutoSize = false,
                    UseMnemonic = false,
                    TextAlign = ContentAlignment.TopLeft,
                    Font = descriptionFont,
                    ForeColor = Color.Gainsboro,
                    BackColor = Color.Transparent
                };

                _commandCheckBoxes[command.Command] = checkBox;
                _commandPanel.Controls.Add(checkBox);
                _commandPanel.Controls.Add(descriptionLabel);

                y += Math.Max(checkBox.Height, descriptionLabel.Height) + 12;
            }

            y += 6;
        }

        _commandPanel.ResumeLayout();
        _isUpdatingAutoexecUi = preserveState;
    }

    private async Task LoadConfigAsync()
    {
        var storedText = string.Empty;
        var content = await _configService.LoadConfigAsync(_configFileName);
        if (!string.IsNullOrEmpty(content))
        {
            try
            {
                var config = System.Text.Json.JsonSerializer.Deserialize<DotaConfigData>(content);
                if (config != null)
                    storedText = BuildStoredAutoexecText(config);
            }
            catch { }
        }

        if (!string.IsNullOrWhiteSpace(storedText))
            SetAutoexecText(storedText);

        var currentAutoexec = await _dota2Service.LoadAutoexecAsync();
        if (currentAutoexec != null && (!string.IsNullOrWhiteSpace(currentAutoexec) || string.IsNullOrWhiteSpace(storedText)))
            SetAutoexecText(currentAutoexec);

        _lastSavedAutoexecText = NormalizeAutoexecText(_autoexecTextBox.Text);
    }

    private async Task SaveConfigAsync()
    {
        var normalizedAutoexecText = NormalizeAutoexecText(_autoexecTextBox.Text);
        if (!string.Equals(_autoexecTextBox.Text, normalizedAutoexecText, StringComparison.Ordinal))
            SetAutoexecText(normalizedAutoexecText);

        if (HasNetworkSensitiveChanges(_lastSavedAutoexecText, normalizedAutoexecText) && !ConfirmNetworkSensitiveChange())
            return;

        var selectedCommands = GetSelectedConfigCommands();
        await SaveStoredConfigAsync(selectedCommands, normalizedAutoexecText);
        _lastSavedAutoexecText = normalizedAutoexecText;

        if (!_pathFound)
        {
            ShowStatus("Сохранено локально. Dota 2 не найдена", Color.Orange);
            return;
        }

        await ApplyChangesAsync(normalizedAutoexecText, true, "Сохранено!", "Сохранено");
    }

    private async Task ResetConfigAsync()
    {
        var warningResult = MessageBox.Show(
            $"Сброс удалит выбранные команды и очистит содержимое {Dota2Service.AutoexecFileName}.",
            "Предупреждение",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (warningResult != DialogResult.Yes)
            return;

        var confirmResult = MessageBox.Show(
            "Подтвердить полный сброс настроек Dota 2?",
            "Подтверждение",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmResult != DialogResult.Yes)
            return;

        if (HasNetworkSensitiveChanges(_lastSavedAutoexecText, string.Empty) && !ConfirmNetworkSensitiveChange())
            return;

        SetAutoexecText(string.Empty);
        await SaveStoredConfigAsync(Array.Empty<string>(), string.Empty);
        _lastSavedAutoexecText = string.Empty;

        if (!_pathFound)
        {
            ShowStatus("Сброшено локально. Dota 2 не найдена", Color.Orange);
            return;
        }

        await ApplyChangesAsync(string.Empty, false, "Настройки Dota 2 сброшены.", "Сброшено");
    }

    private async Task SaveStoredConfigAsync(IReadOnlyList<string> selectedCommands, string autoexecContent)
    {
        var config = new DotaConfigData
        {
            LaunchOptions = string.Empty,
            EnabledLaunchOptions = Array.Empty<string>(),
            EnabledConfigCommands = selectedCommands.ToArray(),
            Autoexec = autoexecContent,
            LastModified = DateTime.Now
        };

        var json = System.Text.Json.JsonSerializer.Serialize(
            config,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        await _configService.SaveConfigAsync(_configFileName, json);
    }

    private async Task OpenAutoexecFolderAsync()
    {
        if (!_pathFound || string.IsNullOrWhiteSpace(_dota2Service.DotaPath))
        {
            ShowStatus("Не удалось найти папку Dota 2", Color.Orange);
            return;
        }

        await _dota2Service.LoadAutoexecAsync();

        var autoexecPath = Path.Combine(_dota2Service.DotaPath, "game", "dota", "cfg", Dota2Service.AutoexecFileName);
        if (!File.Exists(autoexecPath))
        {
            ShowStatus($"Не удалось создать {Dota2Service.AutoexecFileName}", Color.Orange);
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{autoexecPath}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            ShowStatus($"Не удалось открыть папку с {Dota2Service.AutoexecFileName}", Color.Orange);
        }
    }

    private async Task ApplyChangesAsync(
        string autoexecContent,
        bool includeAutoexec,
        string successMessage,
        string actionLabel)
    {
        var needsLaunchOptionsUpdate = await _dota2Service.NeedsLaunchOptionsUpdateAsync(
            Array.Empty<string>(),
            LegacyManagedLaunchOptions,
            includeAutoexec);

        await _dota2Service.SaveAutoexecAsync(autoexecContent);

        Dota2Service.LaunchOptionsApplyResult? applyResult = null;
        if (needsLaunchOptionsUpdate)
        {
            applyResult = await _dota2Service.SetLaunchOptionsAsync(
                Array.Empty<string>(),
                LegacyManagedLaunchOptions,
                includeAutoexec);

            if (!applyResult.IsSuccess)
            {
                ShowStatus(applyResult.Message, Color.Orange);
                return;
            }
        }

        if (!needsLaunchOptionsUpdate)
        {
            ShowStatus(successMessage, Color.Green);
            return;
        }

        ShowStatus(successMessage, Color.Green);
    }

    private void CommandCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingAutoexecUi)
            return;

        if (sender is CheckBox checkBox && checkBox.Tag is string command)
            SetCommandLine(command, checkBox.Checked);
    }

    private void AutoexecTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingAutoexecUi)
            return;

        UpdateCommandSelectionFromAutoexec();
    }

    private void SetCommandLine(string command, bool enabled)
    {
        var selectedCommands = new HashSet<string>(GetSelectedConfigCommands(), StringComparer.OrdinalIgnoreCase);
        if (enabled)
            selectedCommands.Add(command);
        else
            selectedCommands.Remove(command);

        var customLines = GetCustomAutoexecLines();
        SetAutoexecText(BuildAutoexecText(selectedCommands, customLines));
    }

    private void SetAutoexecText(string text)
    {
        var normalizedText = NormalizeAutoexecText(text);

        _isUpdatingAutoexecUi = true;
        _autoexecTextBox.Text = normalizedText;
        UpdateCommandSelectionFromAutoexec();
        _isUpdatingAutoexecUi = false;
    }

    private void UpdateCommandSelectionFromAutoexec()
    {
        var autoexecLines = new HashSet<string>(GetAutoexecLines(), StringComparer.OrdinalIgnoreCase);
        var previousState = _isUpdatingAutoexecUi;
        _isUpdatingAutoexecUi = true;

        foreach (var definition in AllCommandDefinitions)
        {
            _commandCheckBoxes[definition.Command].Checked = autoexecLines.Contains(definition.Command);
        }

        _isUpdatingAutoexecUi = previousState;
    }

    private List<string> GetSelectedConfigCommands()
    {
        var result = new List<string>();

        foreach (var definition in AllCommandDefinitions)
        {
            if (_commandCheckBoxes[definition.Command].Checked)
                result.Add(definition.Command);
        }

        return result;
    }

    private List<string> GetCustomAutoexecLines()
    {
        var result = new List<string>();

        foreach (var line in GetAutoexecLines())
        {
            if (!IsPresetCommand(line))
                result.Add(line);
        }

        return result;
    }

    private List<string> GetAutoexecLines()
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in _autoexecTextBox.Lines)
        {
            var line = NormalizeConfigLine(rawLine);
            if (string.IsNullOrWhiteSpace(line) || !seen.Add(line))
                continue;

            result.Add(line);
        }

        return result;
    }

    private string NormalizeAutoexecText(string text)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var line = NormalizeConfigLine(rawLine);
            if (string.IsNullOrWhiteSpace(line) || !seen.Add(line))
                continue;

            result.Add(line);
        }

        return string.Join(Environment.NewLine, result);
    }

    private string NormalizeConfigLine(string line)
    {
        var normalizedWhitespaceLine = NormalizeWhitespace(line);
        if (string.IsNullOrWhiteSpace(normalizedWhitespaceLine))
            return string.Empty;

        foreach (var definition in AllCommandDefinitions)
        {
            if (string.Equals(normalizedWhitespaceLine, definition.Command, StringComparison.OrdinalIgnoreCase))
                return definition.Command;
        }

        return normalizedWhitespaceLine;
    }

    private static string NormalizeWhitespace(string line)
    {
        return string.Join(
            " ",
            line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private bool IsPresetCommand(string line)
    {
        foreach (var definition in AllCommandDefinitions)
        {
            if (string.Equals(definition.Command, line, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private string BuildStoredAutoexecText(DotaConfigData config)
    {
        var selectedCommands = config.EnabledConfigCommands ?? Array.Empty<string>();
        return BuildAutoexecText(selectedCommands, SplitLines(config.Autoexec));
    }

    private string BuildAutoexecText(IEnumerable<string> selectedCommands, IEnumerable<string> customLines)
    {
        var result = new List<string>();
        var selectedSet = new HashSet<string>(selectedCommands.Select(NormalizeConfigLine), StringComparer.OrdinalIgnoreCase);

        foreach (var definition in AllCommandDefinitions)
        {
            if (selectedSet.Contains(definition.Command))
                result.Add(definition.Command);
        }

        foreach (var line in customLines)
        {
            var normalizedLine = NormalizeConfigLine(line);
            if (string.IsNullOrWhiteSpace(normalizedLine))
                continue;

            if (!result.Contains(normalizedLine, StringComparer.OrdinalIgnoreCase))
                result.Add(normalizedLine);
        }

        return string.Join(Environment.NewLine, result);
    }

    private static IEnumerable<string> SplitLines(string text)
    {
        return text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
    }

    private void ShowHelpDialog()
    {
        MessageBox.Show(
            $"Большое окно сверху - это содержимое файла {Dota2Service.AutoexecFileName}. Всё, что ты пишешь там, сохраняется именно в этот файл.\n\n" +
            "Галочки ниже добавляют или убирают типовые строки прямо в этом файле.\n\n" +
            $"При нажатии на 'Применить' твикер сохранит {Dota2Service.AutoexecFileName} и автоматически оставит в параметрах запуска Dota 2 только команду {Dota2Service.AutoexecLaunchCommand} для запуска этого файла.",
            "Как это работает?",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private bool HasNetworkSensitiveChanges(string oldText, string newText)
    {
        var oldCommands = ExtractNetworkSensitiveCommands(oldText);
        var newCommands = ExtractNetworkSensitiveCommands(newText);

        return !oldCommands.SetEquals(newCommands);
    }

    private HashSet<string> ExtractNetworkSensitiveCommands(string text)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var normalizedLine = NormalizeConfigLine(rawLine);
            if (NetworkSensitiveCommands.Contains(normalizedLine))
                result.Add(normalizedLine);
        }

        return result;
    }

    private static bool ConfirmNetworkSensitiveChange()
    {
        return MessageBox.Show(
            "Изменение этого параметра может негативно сказаться на интернет соединении! ПРОДОЛЖИТЬ?",
            "Предупреждение",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning) == DialogResult.Yes;
    }

    private async void ShowStatus(string message, Color color)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = color;
        await Task.Delay(2000);
        _statusLabel.Text = string.Empty;
    }

    private sealed class ConfigCommandGroupDefinition
    {
        public ConfigCommandGroupDefinition(string title, ConfigCommandDefinition[] commands)
        {
            Title = title;
            Commands = commands;
        }

        public string Title { get; }

        public ConfigCommandDefinition[] Commands { get; }
    }

    private sealed class ConfigCommandDefinition
    {
        public ConfigCommandDefinition(string command, string description)
        {
            Command = command;
            Description = description;
        }

        public string Command { get; }

        public string Description { get; }
    }
}

public class DotaConfigData
{
    public string LaunchOptions { get; set; } = string.Empty;

    public string[] EnabledLaunchOptions { get; set; } = Array.Empty<string>();

    public string[] EnabledConfigCommands { get; set; } = Array.Empty<string>();

    public string Autoexec { get; set; } = string.Empty;

    public DateTime LastModified { get; set; }
}
