using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArbuzTweaker;

public partial class DotaTab : UserControl
{
    private const int MaxFpsLimitValue = 999;

    private static readonly ConfigCommandGroupDefinition[] CommandGroups =
    {
        new(
            "FPS и производительность",
            new[]
            {
                new ConfigCommandDefinition("dota_cheap_water 1", "Упрощает отрисовку воды в реке."),
                new ConfigCommandDefinition("fps_max", 240, "Ограничивает FPS в игре."),
                new ConfigCommandDefinition("fps_max_menu", 120, "Ограничивает FPS в меню."),
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
            "Картинка и резкость",
            new[]
            {
                new ConfigCommandDefinition("mat_viewportscale 0.999999", "Рендерит почти в полном масштабе: картинка становится четче и резче, но может заметно снизить FPS."),
                new ConfigCommandDefinition("r_dota_fsr_upsample 2", "Выставляет FSR-апскейл в режим 2 для более резкой картинки."),
                new ConfigCommandDefinition("r_dota_fsr_rcas_sharpness 0", "Ставит резкость RCAS в 0 для связки с FSR-настройкой.")
            }),
        new(
            "Интерфейс",
            new[]
            {
                new ConfigCommandDefinition("dota_hud_enable_dispel_effect 1", "Показывает надпись DISPEL при развеивании эффекта."),
                new ConfigCommandDefinition("dota_health_hurt_threshold 1", "Полоска здоровья уменьшается сразу, без заметной задержки после получения урона."),
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
                new ConfigCommandDefinition("cl_clock_recvmargin_enable 0", "Отключает recvmargin для снижения задержки ввода; эффект зависит от сети и сервера."),
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
                new ConfigCommandDefinition("r_depth_of_field 0", "Отключает depth of field.")
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

    private readonly ConfigService _configService;
    private readonly Dota2Service _dota2Service;
    private readonly AppSettingsService _appSettingsService;
    private readonly FileBackupService _fileBackupService;
    private readonly string _configFileName = "dota2_config.json";
    private readonly Dictionary<string, CheckBox> _commandCheckBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NumericUpDown> _numericCommandInputs = new(StringComparer.OrdinalIgnoreCase);
    private TextBox _autoexecTextBox = null!;
    private TextBox _commandSearchTextBox = null!;
    private Panel _commandPanel = null!;
    private Button _saveButton = null!;
    private Button _resetButton = null!;
    private Button _helpButton = null!;
    private Button _openAutoexecButton = null!;
    private Button _restoreBackupButton = null!;
    private Label _statusLabel = null!;
    private Label _pathLabel = null!;
    private bool _pathFound;
    private bool _isUpdatingAutoexecUi;
    private string _lastSavedAutoexecText = string.Empty;
    private int _statusToken;
    private int _lastCommandPanelWidth = -1;

    public DotaTab(
        ConfigService configService,
        Dota2Service dota2Service,
        AppSettingsService appSettingsService,
        FileBackupService fileBackupService)
    {
        _configService = configService;
        _dota2Service = dota2Service;
        _appSettingsService = appSettingsService;
        _fileBackupService = fileBackupService;
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
        UiTheme.StyleTabControl(tabControl);

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

        var launchOptionsControl = new DotaLaunchOptionsTab(_configService, _dota2Service, _appSettingsService)
        {
            Dock = DockStyle.Fill
        };
        launchOptionsPage.Controls.Add(launchOptionsControl);

        var videoConfigControl = new DotaVideoConfigTab(_dota2Service, _appSettingsService)
        {
            Dock = DockStyle.Fill
        };
        videoConfigPage.Controls.Add(videoConfigControl);

        var configLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = false,
            Padding = new Padding(20, 10, 20, 20),
            ColumnCount = 1,
            RowCount = 11
        };
        configLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        configLayout.RowStyles.Add(new RowStyle());
        configLayout.RowStyles.Add(new RowStyle());
        configLayout.RowStyles.Add(new RowStyle());
        configLayout.RowStyles.Add(new RowStyle());
        configLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220F));
        configLayout.RowStyles.Add(new RowStyle());
        configLayout.RowStyles.Add(new RowStyle());
        configLayout.RowStyles.Add(new RowStyle());
        configLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
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
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = Color.Gainsboro,
            MaximumSize = new Size(980, 0),
            Margin = new Padding(0, 0, 0, 10)
        };

        _autoexecTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = false,
            AcceptsTab = true,
            MinimumSize = new Size(0, 220),
            Margin = new Padding(0, 0, 0, 12)
        };
        UiTheme.StyleEditorTextBox(_autoexecTextBox);
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
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = Color.Gainsboro,
            MaximumSize = new Size(980, 0),
            Margin = new Padding(0, 0, 0, 10)
        };

        var commandSearchPanel = CreateCommandSearchPanel();

        _commandPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Margin = new Padding(0, 0, 0, 12)
        };
        UiTheme.StyleListPanel(_commandPanel);
        _commandPanel.Resize += (s, e) =>
        {
            if (_commandPanel.ClientSize.Width != _lastCommandPanelWidth)
                PopulateCommandPanel();
        };
        PopulateCommandPanel();

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 10)
        };

        _saveButton = new Button { Text = "Применить", Size = new Size(120, 35), Margin = new Padding(0, 0, 10, 0) };
        _saveButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, SaveConfigAsync);

        _helpButton = new Button { Text = "Как это работает?", AutoSize = true, MinimumSize = new Size(0, 35), Padding = new Padding(10, 0, 10, 0), Margin = new Padding(0, 0, 10, 0) };
        _helpButton.Click += (s, e) => ShowHelpDialog();

        _openAutoexecButton = new Button { Text = $"Показать {Dota2Service.AutoexecFileName}", Size = new Size(220, 35), Margin = new Padding(0, 0, 10, 0) };
        _openAutoexecButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, OpenAutoexecFolderAsync);

        _restoreBackupButton = new Button { Text = "Восстановить бэкап", Size = new Size(180, 35), Margin = new Padding(0, 0, 10, 0) };
        _restoreBackupButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, OpenDotaBackupsAsync);

        _resetButton = new Button { Text = "Сбросить к базовому", Size = new Size(205, 35), Margin = new Padding(0) };
        _resetButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, ResetConfigAsync);

        UiTheme.StyleActionButton(_saveButton, true);
        UiTheme.StyleActionButton(_helpButton);
        UiTheme.StyleActionButton(_openAutoexecButton);
        UiTheme.StyleActionButton(_restoreBackupButton);
        UiTheme.StyleActionButton(_resetButton);

        buttonsPanel.Controls.Add(_saveButton);
        buttonsPanel.Controls.Add(_helpButton);
        buttonsPanel.Controls.Add(_openAutoexecButton);
        buttonsPanel.Controls.Add(_restoreBackupButton);
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
        configLayout.Controls.Add(commandSearchPanel, 0, 7);
        configLayout.Controls.Add(_commandPanel, 0, 8);
        configLayout.Controls.Add(buttonsPanel, 0, 9);
        configLayout.Controls.Add(_statusLabel, 0, 10);

        configPage.Controls.Add(configLayout);

        tabControl.TabPages.Add(launchOptionsPage);
        tabControl.TabPages.Add(configPage);
        tabControl.TabPages.Add(videoConfigPage);
        Controls.Add(tabControl);
    }

    private FlowLayoutPanel CreateCommandSearchPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 8)
        };

        _commandSearchTextBox = new TextBox
        {
            Width = 360,
            Margin = new Padding(0, 0, 8, 0)
        };
        UiTheme.StyleSearchTextBox(_commandSearchTextBox);
        _commandSearchTextBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.SuppressKeyPress = true;
            PopulateCommandPanel();
        };

        var searchButton = new Button { Text = "Найти", Size = new Size(100, 31), Margin = new Padding(0, 0, 8, 0) };
        searchButton.Click += (s, e) => PopulateCommandPanel();
        UiTheme.StyleActionButton(searchButton, true);

        var clearButton = new Button { Text = "Сбросить поиск", Size = new Size(145, 31), Margin = new Padding(0) };
        clearButton.Click += (s, e) =>
        {
            _commandSearchTextBox.Clear();
            PopulateCommandPanel();
        };
        UiTheme.StyleActionButton(clearButton);

        panel.Controls.Add(_commandSearchTextBox);
        panel.Controls.Add(searchButton);
        panel.Controls.Add(clearButton);
        return panel;
    }

    private void PopulateCommandPanel()
    {
        if (_commandPanel == null || _autoexecTextBox == null)
            return;

        var preserveState = _isUpdatingAutoexecUi;
        _isUpdatingAutoexecUi = true;
        _lastCommandPanelWidth = _commandPanel.ClientSize.Width;

        _commandPanel.SuspendLayout();
        UiTheme.ClearAndDisposeControls(_commandPanel);
        _commandCheckBoxes.Clear();
        _numericCommandInputs.Clear();

        var y = 8;
        var autoexecLines = GetAutoexecLines();
        var availableWidth = Math.Max(620, _commandPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 24);
        var searchQuery = GetCommandSearchQuery();
        var rowIndex = 0;

        foreach (var group in CommandGroups)
        {
            var commands = group.Commands
                .Where(command => MatchesSearch(command.Command, command.Description, searchQuery))
                .ToArray();
            if (commands.Length == 0)
                continue;

            y = UiTheme.AddListSectionHeader(_commandPanel, y, availableWidth, group.Title);

            foreach (var command in commands)
            {
                y = UiTheme.AddCheckListRow(
                    _commandPanel,
                    y,
                    availableWidth,
                    rowIndex,
                    command.DisplayText,
                    command.Description,
                    TryGetCommandLine(autoexecLines, command, out _),
                    CommandCheckBox_CheckedChanged,
                    out var checkBox,
                    command);

                _commandCheckBoxes[command.Command] = checkBox;
                if (command.IsNumeric)
                    AddNumericCommandInput(command, checkBox, autoexecLines);

                rowIndex++;
            }

            y += 10;
        }

        if (rowIndex == 0)
            UiTheme.AddEmptyListMessage(_commandPanel, y, availableWidth, "Ничего не найдено. Попробуй другую команду или слово из описания.");

        _commandPanel.ResumeLayout();
        _commandPanel.AutoScrollMinSize = new Size(0, y + 12);
        _isUpdatingAutoexecUi = preserveState;
    }

    private string GetCommandSearchQuery()
    {
        return _commandSearchTextBox?.Text.Trim() ?? string.Empty;
    }

    private static bool MatchesSearch(string command, string description, string query)
    {
        return string.IsNullOrWhiteSpace(query)
            || command.Contains(query, StringComparison.OrdinalIgnoreCase)
            || description.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void AddNumericCommandInput(ConfigCommandDefinition definition, CheckBox checkBox, IReadOnlyList<string> autoexecLines)
    {
        if (checkBox.Parent is not Panel rowPanel)
            return;

        var value = TryGetNumericCommandValue(autoexecLines, definition, out var currentValue)
            ? currentValue
            : definition.DefaultNumericValue;

        checkBox.Width = Math.Min(155, checkBox.Width);

        var input = new NumericUpDown
        {
            Minimum = 0,
            Maximum = MaxFpsLimitValue,
            Value = Math.Clamp(value, 0, MaxFpsLimitValue),
            Size = new Size(58, 25),
            Location = new Point(checkBox.Right + 8, 7),
            TextAlign = HorizontalAlignment.Center,
            Font = new Font("Segoe UI", 9.5F),
            BackColor = UiTheme.SurfaceAlt,
            ForeColor = UiTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Tag = definition
        };

        input.ValueChanged += NumericCommandInput_ValueChanged;
        rowPanel.Controls.Add(input);
        _numericCommandInputs[definition.Command] = input;
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

    private async Task OpenDotaBackupsAsync()
    {
        using var backupForm = new FileBackupBrowserForm(_fileBackupService, "Dota 2");
        backupForm.ShowDialog(this);

        var currentAutoexec = await _dota2Service.LoadAutoexecAsync();
        SetAutoexecText(currentAutoexec ?? string.Empty);
        _lastSavedAutoexecText = NormalizeAutoexecText(_autoexecTextBox.Text);
        ShowStatus("Бэкапы закрыты. Конфиг перечитан", Color.Gray);
    }

    private async Task ApplyChangesAsync(
        string autoexecContent,
        bool includeAutoexec,
        string successMessage,
        string actionLabel)
    {
        // Здесь меняется только наличие +exec autoexec.cfg; остальные параметры запуска
        // принадлежат вкладке «Параметры запуска» и не трогаются.
        var needsLaunchOptionsUpdate = await _dota2Service.HasLocalConfigAsync()
            && await _dota2Service.NeedsLaunchOptionsUpdateAsync(
                Array.Empty<string>(),
                Array.Empty<string>(),
                includeAutoexec);

        await _dota2Service.SaveAutoexecAsync(autoexecContent);

        if (!needsLaunchOptionsUpdate)
        {
            ShowStatus(successMessage, Color.Green);
            return;
        }

        bool steamWasRunning = false;
        bool steamClosed = false;

        if (_dota2Service.IsSteamRunning())
        {
            steamWasRunning = true;

            var closeSteamResult = MessageBox.Show(
                $"Файл {Dota2Service.AutoexecFileName} сохранён, но в параметры запуска Dota 2 нужно внести {Dota2Service.AutoexecLaunchCommand}.\n\n" +
                "Steam сейчас запущен: если менять параметры запуска при работающем Steam, он перезапишет их при выходе.\n\n" +
                "Закрыть Steam и применить параметры запуска сейчас?",
                "Steam запущен",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            if (closeSteamResult == DialogResult.Cancel)
            {
                ShowStatus($"{actionLabel}. Параметры запуска не изменены", Color.Orange);
                return;
            }

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

        var applyResult = await _dota2Service.SetLaunchOptionsAsync(
            Array.Empty<string>(),
            Array.Empty<string>(),
            includeAutoexec);

        if (!applyResult.IsSuccess)
        {
            ShowStatus(applyResult.Message, Color.Orange);
            return;
        }

        if (steamClosed)
        {
            if (await _dota2Service.StartSteamAsync())
                ShowStatus($"{actionLabel}. Steam перезапущен", Color.Green);
            else
                ShowStatus($"{actionLabel}. Не удалось запустить Steam", Color.Orange);

            return;
        }

        if (steamWasRunning)
        {
            ShowStatus($"{actionLabel}. Перезапусти Steam, чтобы параметры запуска сохранились", Color.Orange);
            return;
        }

        ShowStatus(successMessage, Color.Green);
    }

    private void CommandCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingAutoexecUi)
            return;

        if (sender is CheckBox checkBox && checkBox.Tag is ConfigCommandDefinition definition)
            SetCommandLine(definition, checkBox.Checked);
    }

    private void NumericCommandInput_ValueChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingAutoexecUi)
            return;

        if (sender is not NumericUpDown input || input.Tag is not ConfigCommandDefinition definition)
            return;

        if (_commandCheckBoxes.TryGetValue(definition.Command, out var checkBox) && checkBox.Checked)
            SetCommandLine(definition, true);
    }

    private void AutoexecTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingAutoexecUi)
            return;

        UpdateCommandSelectionFromAutoexec();
    }

    private void SetCommandLine(ConfigCommandDefinition definition, bool enabled)
    {
        var selectedCommands = GetSelectedConfigCommands();
        selectedCommands.RemoveAll(line => IsDefinitionCommand(definition, line));

        if (enabled)
            selectedCommands.Add(GetCommandForUi(definition));

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
        var autoexecLines = GetAutoexecLines();
        var previousState = _isUpdatingAutoexecUi;
        _isUpdatingAutoexecUi = true;

        foreach (var definition in AllCommandDefinitions)
        {
            if (_commandCheckBoxes.TryGetValue(definition.Command, out var checkBox))
                checkBox.Checked = TryGetCommandLine(autoexecLines, definition, out _);

            if (definition.IsNumeric
                && _numericCommandInputs.TryGetValue(definition.Command, out var input)
                && TryGetNumericCommandValue(autoexecLines, definition, out var value))
            {
                input.Value = Math.Clamp(value, 0, MaxFpsLimitValue);
            }
        }

        _isUpdatingAutoexecUi = previousState;
    }

    private List<string> GetSelectedConfigCommands()
    {
        var result = new List<string>();
        var autoexecLines = GetAutoexecLines();

        foreach (var definition in AllCommandDefinitions)
        {
            if (TryGetCommandLine(autoexecLines, definition, out var commandLine))
                result.Add(commandLine);
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
            if (definition.IsNumeric && TryNormalizeNumericCommand(definition, normalizedWhitespaceLine, out var numericCommand))
                return numericCommand;

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

    private string GetCommandForUi(ConfigCommandDefinition definition)
    {
        if (!definition.IsNumeric)
            return definition.Command;

        var value = definition.DefaultNumericValue;
        if (_numericCommandInputs.TryGetValue(definition.Command, out var input))
            value = (int)input.Value;

        return definition.BuildNumericCommand(value);
    }

    private static bool TryGetCommandLine(IEnumerable<string> lines, ConfigCommandDefinition definition, out string commandLine)
    {
        foreach (var line in lines)
        {
            if (IsDefinitionCommand(definition, line))
            {
                commandLine = definition.IsNumeric && TryNormalizeNumericCommand(definition, line, out var numericCommand)
                    ? numericCommand
                    : definition.Command;
                return true;
            }
        }

        commandLine = string.Empty;
        return false;
    }

    private static bool TryGetNumericCommandValue(IEnumerable<string> lines, ConfigCommandDefinition definition, out int value)
    {
        foreach (var line in lines)
        {
            if (TryParseNumericCommand(definition, line, out value))
                return true;
        }

        value = definition.DefaultNumericValue;
        return false;
    }

    private static bool IsDefinitionCommand(ConfigCommandDefinition definition, string line)
    {
        return definition.IsNumeric
            ? TryParseNumericCommand(definition, line, out _)
            : string.Equals(definition.Command, line, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryNormalizeNumericCommand(ConfigCommandDefinition definition, string line, out string commandLine)
    {
        if (TryParseNumericCommand(definition, line, out var value))
        {
            commandLine = definition.BuildNumericCommand(value);
            return true;
        }

        commandLine = string.Empty;
        return false;
    }

    private static bool TryParseNumericCommand(ConfigCommandDefinition definition, string line, out int value)
    {
        value = definition.DefaultNumericValue;

        if (!definition.IsNumeric)
            return false;

        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !string.Equals(parts[0], definition.CommandName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (parts[1].Length > 3 || !int.TryParse(parts[1], out value))
            return false;

        return value is >= 0 and <= MaxFpsLimitValue;
    }

    private bool IsPresetCommand(string line)
    {
        foreach (var definition in AllCommandDefinitions)
        {
            if (IsDefinitionCommand(definition, line))
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
            if (TryGetCommandLine(selectedSet, definition, out var commandLine))
            {
                result.Add(commandLine);
                continue;
            }

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

    private async void ShowStatus(string message, Color color)
    {
        var token = ++_statusToken;
        _statusLabel.Text = message;
        _statusLabel.ForeColor = color;
        await Task.Delay(4000);
        if (token == _statusToken)
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
            CommandName = command.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? command;
            DisplayText = command;
        }

        public ConfigCommandDefinition(string commandName, int defaultNumericValue, string description)
        {
            CommandName = commandName;
            DefaultNumericValue = Math.Clamp(defaultNumericValue, 0, MaxFpsLimitValue);
            Command = BuildNumericCommand(DefaultNumericValue);
            DisplayText = commandName;
            Description = description;
            IsNumeric = true;
        }

        public string Command { get; }

        public string CommandName { get; }

        public string DisplayText { get; }

        public string Description { get; }

        public bool IsNumeric { get; }

        public int DefaultNumericValue { get; }

        public string BuildNumericCommand(int value)
        {
            return $"{CommandName} {Math.Clamp(value, 0, MaxFpsLimitValue)}";
        }
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
