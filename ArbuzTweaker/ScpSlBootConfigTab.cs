using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArbuzTweaker;

// Редактор boot.config SCP:SL — текстового конфига движка Unity в папке игры.
// Формат простой: по строке на настройку вида key=value. Тумблеры добавляют
// или убирают строки; снятие галочки удаляет строку, возвращая поведение к
// стандартному (движок сам подставит своё значение для отсутствующего ключа).
public partial class ScpSlBootConfigTab : UserControl
{
    // Число потоков движка = числу физических ядер процессора ТОГО ПК, на котором запущен твикер
    // (гайд советует ставить по ядрам, а не по логическим процессорам). Считаем в рантайме.
    private static readonly int WorkerCount = DetectWorkerCount();
    private static readonly string WorkerCountValue = WorkerCount.ToString();

    private static int DetectWorkerCount()
    {
        var cores = GetPhysicalCoreCount();
        // Если ядра определить не удалось — берём логические процессоры (тоже с этого ПК).
        return Math.Max(1, cores > 0 ? cores : Environment.ProcessorCount);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemLogicalProcessorInformation
    {
        public UIntPtr ProcessorMask;
        public int Relationship; // 0 = RelationProcessorCore
        public ulong Reserved1;
        public ulong Reserved2;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetLogicalProcessorInformation(IntPtr buffer, ref uint returnLength);

    private static int GetPhysicalCoreCount()
    {
        try
        {
            uint length = 0;
            GetLogicalProcessorInformation(IntPtr.Zero, ref length);
            if (length == 0)
                return 0;

            var buffer = Marshal.AllocHGlobal((int)length);
            try
            {
                if (!GetLogicalProcessorInformation(buffer, ref length))
                    return 0;

                var size = Marshal.SizeOf<SystemLogicalProcessorInformation>();
                var count = (int)(length / size);
                var cores = 0;
                for (var i = 0; i < count; i++)
                {
                    var info = Marshal.PtrToStructure<SystemLogicalProcessorInformation>(buffer + i * size);
                    if (info.Relationship == 0) // RelationProcessorCore — одна запись на физическое ядро
                        cores++;
                }

                return cores;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return 0;
        }
    }

    private static readonly BootSettingDefinition[] SettingDefinitions =
    {
        new("gfx-enable-gfx-jobs", "1", UiTheme.Impact.Fps,
            "Многопоточная отправка команд рендера",
            "Разрешает движку раскидывать команды отрисовки по нескольким потокам (Graphics Jobs). На многоядерных процессорах может поднять FPS и убрать рывки. Строка gfx-enable-gfx-jobs=1."),
        new("gfx-enable-native-gfx-jobs", "1", UiTheme.Impact.Fps,
            "Нативные Graphics Jobs",
            "Дополняет предыдущий пункт — включает нативную реализацию многопоточного рендера. Ставьте вместе с ним. Строка gfx-enable-native-gfx-jobs=1."),
        new("job-worker-count", WorkerCountValue, UiTheme.Impact.Fps,
            $"Рабочих потоков движка = {WorkerCountValue}",
            $"Задаёт число рабочих потоков движка по числу физических ядер процессора этого ПК ({WorkerCountValue}). Значение берётся с компьютера, на котором запущен твикер. При желании можно вписать другое число в тексте выше. Строка job-worker-count."),
        new("gc-max-time-slice", "3", UiTheme.Impact.AntiStutter,
            "Ограничить паузы сборщика мусора",
            "Ограничивает время, которое сборщик мусора Unity может занять за кадр — меньше микрофризов от уборки памяти. Строка gc-max-time-slice=3."),
        new("hdr-display-enabled", "0", UiTheme.Impact.AntiStutter,
            "Отключить HDR-вывод",
            "Выключает вывод HDR на монитор. Большинству мониторов он не нужен, а его отключение убирает лишний этап обработки кадра. Строка hdr-display-enabled=0."),
        new("vr-enabled", "0", UiTheme.Impact.AntiStutter,
            "Отключить поддержку VR",
            "SCP:SL — не VR-игра, поэтому инициализация VR при старте лишняя. Строка vr-enabled=0."),
        new("no-stereo-rendering", "1", UiTheme.Impact.AntiStutter,
            "Отключить стерео-рендеринг",
            "Отключает стерео-рендер (нужен только для VR и 3D-очков) — движок не готовит два изображения. Строка no-stereo-rendering=1."),
        new("wait-for-native-debugger", "0", UiTheme.Impact.AntiStutter,
            "Не ждать отладчик при запуске (нативный)",
            "Запрещает движку ждать подключения отладчика при старте. Должно быть 0; иногда случайно стоит 1 и тормозит запуск. Строка wait-for-native-debugger=0."),
        new("wait-for-managed-debugger", "0", UiTheme.Impact.AntiStutter,
            "Не ждать отладчик при запуске (управляемый)",
            "То же самое для управляемого (C#) отладчика — игра не ждёт его при старте. Строка wait-for-managed-debugger=0."),
        new("gfx-disable-mt-rendering", "1", UiTheme.Impact.Fps,
            "[альтернатива] Однопоточный рендер",
            "Противоположность Graphics Jobs: заставляет рисовать в один поток. На части процессоров это убирает рывки, на других — снижает FPS. Включайте ВМЕСТО «Graphics Jobs» и сравнивайте. Строка gfx-disable-mt-rendering=1."),
        new("force-feature-level-9-3", "1", UiTheme.Impact.Fps,
            "[для очень слабых ПК] Упростить рендер до DX9.3",
            "Форсирует уровень DirectX 9.3 — сильно упрощает графику и заметно ухудшает картинку. Имеет смысл только на совсем слабых видеокартах. Строка force-feature-level-9-3=1."),
    };

    private readonly ScpSlService _scpSlService;
    private readonly Dictionary<string, CheckBox> _settingCheckBoxes = new(StringComparer.OrdinalIgnoreCase);
    private TextBox _bootTextBox = null!;
    private TextBox _settingsSearchTextBox = null!;
    private Panel _settingsPanel = null!;
    private Label _pathLabel = null!;
    private Label _statusLabel = null!;
    private bool _isUpdatingUi;
    private int _statusToken;
    private int _lastSettingsPanelWidth = -1;

    public ScpSlBootConfigTab(ScpSlService scpSlService)
    {
        _scpSlService = scpSlService;
        InitializeComponent();
        LoadBootConfigStateAsync();
    }

    private async void LoadBootConfigStateAsync()
    {
        await RefreshBootPathStateAsync();

        var content = await _scpSlService.LoadBootConfigAsync();
        if (content != null)
            SetBootText(content);
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
            RowCount = 10
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle());                          // 0 заголовок
        rootLayout.RowStyles.Add(new RowStyle());                          // 1 путь
        rootLayout.RowStyles.Add(new RowStyle());                          // 2 инфо
        rootLayout.RowStyles.Add(new RowStyle());                          // 3 подпись текста
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150F));   // 4 текст boot.config
        rootLayout.RowStyles.Add(new RowStyle());                          // 5 подпись переключателей
        rootLayout.RowStyles.Add(new RowStyle());                          // 6 поиск
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 250F));   // 7 список переключателей
        rootLayout.RowStyles.Add(new RowStyle());                          // 8 кнопки
        rootLayout.RowStyles.Add(new RowStyle());                          // 9 статус

        var titleLabel = new Label
        {
            Text = "SCP:SL — boot.config (движок Unity)",
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
            Text = "boot.config — служебный файл движка Unity в папке установки игры. Галочки ниже добавляют или убирают в нём строки-настройки; снятие галочки удаляет строку и возвращает поведение по умолчанию. Перед записью создаётся резервная копия файла. Правки сохраняются между запусками, но сбрасываются при обновлении игры или проверке целостности файлов в Steam — тогда примените заново.",
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = Color.Gainsboro,
            MaximumSize = new Size(980, 0),
            Margin = new Padding(0, 0, 0, 12)
        };

        var bootLabel = new Label
        {
            Text = "Содержимое boot.config:",
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        };

        _bootTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            MinimumSize = new Size(0, 0),
            Margin = new Padding(0, 0, 0, 12)
        };
        UiTheme.StyleEditorTextBox(_bootTextBox);
        _bootTextBox.TextChanged += BootTextBox_TextChanged;

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

        var openFolderButton = new Button { Text = "Показать boot.config", Size = new Size(210, 35), Margin = new Padding(0, 0, 10, 0) };
        openFolderButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, OpenBootConfigFolderAsync);

        var resetButton = new Button { Text = "Снять все галочки", Size = new Size(170, 35), Margin = new Padding(0) };
        resetButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, ResetAsync);

        UiTheme.StyleActionButton(applyButton, true);
        UiTheme.StyleActionButton(helpButton);
        UiTheme.StyleActionButton(openFolderButton);
        UiTheme.StyleActionButton(resetButton);

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
        rootLayout.Controls.Add(bootLabel, 0, 3);
        rootLayout.Controls.Add(_bootTextBox, 0, 4);
        rootLayout.Controls.Add(settingsLabel, 0, 5);
        rootLayout.Controls.Add(settingsSearchPanel, 0, 6);
        rootLayout.Controls.Add(_settingsPanel, 0, 7);
        rootLayout.Controls.Add(buttonsPanel, 0, 8);
        rootLayout.Controls.Add(_statusLabel, 0, 9);

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

    private async Task RefreshBootPathStateAsync()
    {
        var path = await _scpSlService.GetBootConfigPathAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            _pathLabel.Text = "Не удалось найти установку SCP:SL. Запустите игру хотя бы раз через Steam.";
            _pathLabel.ForeColor = Color.Orange;
            return;
        }

        if (System.IO.File.Exists(path))
        {
            _pathLabel.Text = $"boot.config: {path}";
            _pathLabel.ForeColor = Color.Green;
        }
        else
        {
            _pathLabel.Text = $"boot.config пока нет, будет создан при применении: {path}";
            _pathLabel.ForeColor = Color.Orange;
        }
    }

    private async Task SaveAndApplyAsync()
    {
        var normalizedText = NormalizeBootText(_bootTextBox.Text);
        if (!string.Equals(_bootTextBox.Text, normalizedText, StringComparison.Ordinal))
            SetBootText(normalizedText);

        if (!await _scpSlService.SaveBootConfigAsync(normalizedText))
        {
            ShowStatus("Не удалось сохранить boot.config (проверьте, что игра установлена и файл не занят).", Color.Orange);
            return;
        }

        await RefreshBootPathStateAsync();
        ShowStatus("boot.config сохранён. Изменения вступят в силу при следующем запуске игры.", Color.Green);
    }

    private async Task OpenBootConfigFolderAsync()
    {
        var path = await _scpSlService.GetBootConfigPathAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            ShowStatus("Не удалось найти установку SCP:SL.", Color.Orange);
            return;
        }

        try
        {
            var argument = System.IO.File.Exists(path)
                ? $"/select,\"{path}\""
                : $"\"{System.IO.Path.GetDirectoryName(path)}\"";

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = argument,
                UseShellExecute = true
            });
        }
        catch
        {
            ShowStatus("Не удалось открыть папку с boot.config.", Color.Orange);
        }
    }

    private void PopulateSettingsPanel()
    {
        if (_settingsPanel == null || _bootTextBox == null)
            return;

        var preserveState = _isUpdatingUi;
        _isUpdatingUi = true;
        _lastSettingsPanelWidth = _settingsPanel.ClientSize.Width;

        _settingsPanel.SuspendLayout();
        UiTheme.ClearAndDisposeControls(_settingsPanel);
        _settingCheckBoxes.Clear();

        var y = 8;
        var rowIndex = 0;
        var searchQuery = GetSettingsSearchQuery();
        var availableWidth = Math.Max(620, _settingsPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 24);

        y = UiTheme.AddListSectionHeader(_settingsPanel, y, availableWidth, "Настройки boot.config");

        foreach (var definition in SettingDefinitions)
        {
            y = UiTheme.AddCheckListRow(
                _settingsPanel,
                y,
                availableWidth,
                rowIndex,
                definition.Title,
                definition.Description,
                definition.IsEnabled(GetSettingValue(definition.Key)),
                SettingCheckBox_CheckedChanged,
                out var checkBox,
                definition.Key,
                MatchesSearch(definition.Title, definition.Description, definition.Key, searchQuery),
                definition.Impact);

            _settingCheckBoxes[definition.Key] = checkBox;
            rowIndex++;
        }

        _settingsPanel.AutoScrollMinSize = new Size(0, y + 12);
        _settingsPanel.ResumeLayout();
        _isUpdatingUi = preserveState;
    }

    private string GetSettingsSearchQuery()
    {
        return _settingsSearchTextBox?.Text.Trim() ?? string.Empty;
    }

    private static bool MatchesSearch(string title, string description, string key, string query)
    {
        return !string.IsNullOrWhiteSpace(query)
            && (title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || description.Contains(query, StringComparison.OrdinalIgnoreCase)
                || key.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private async Task ResetAsync()
    {
        var lines = new List<string>(GetBootLines());

        foreach (var definition in SettingDefinitions)
            lines = RemoveSetting(lines, definition.Key);

        SetBootText(string.Join(Environment.NewLine, lines));
        await SaveAndApplyAsync();
    }

    private void SettingCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingUi)
            return;

        if (sender is CheckBox checkBox && checkBox.Tag is string key)
        {
            var definition = GetDefinition(key);
            if (definition == null)
                return;

            var lines = checkBox.Checked
                ? UpsertSetting(GetBootLines(), definition.Key, definition.CheckedValue)
                : RemoveSetting(GetBootLines(), definition.Key);

            SetBootText(string.Join(Environment.NewLine, lines));
        }
    }

    private void BootTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingUi)
            return;

        UpdateSelectionFromText();
    }

    private void SetBootText(string text)
    {
        _isUpdatingUi = true;
        _bootTextBox.Text = NormalizeBootText(text);
        UpdateSelectionFromText();
        _isUpdatingUi = false;
    }

    private void UpdateSelectionFromText()
    {
        _isUpdatingUi = true;

        foreach (var definition in SettingDefinitions)
        {
            if (_settingCheckBoxes.TryGetValue(definition.Key, out var checkBox))
                checkBox.Checked = definition.IsEnabled(GetSettingValue(definition.Key));
        }

        _isUpdatingUi = false;
    }

    private List<string> GetBootLines()
    {
        return _bootTextBox.Text
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Select(line => line.TrimEnd())
            .ToList();
    }

    private string NormalizeBootText(string text)
    {
        var lines = text
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Select(line => line.TrimEnd())
            .ToList();

        // Убираем пустые строки в конце, чтобы файл не разрастался.
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
            lines.RemoveAt(lines.Count - 1);

        return string.Join(Environment.NewLine, lines);
    }

    private string? GetSettingValue(string key)
    {
        var pattern = $"^\\s*{Regex.Escape(key)}\\s*=\\s*(?<value>.*?)\\s*$";

        foreach (var line in _bootTextBox.Lines)
        {
            var match = Regex.Match(line, pattern);
            if (match.Success)
                return match.Groups["value"].Value;
        }

        return null;
    }

    private List<string> UpsertSetting(List<string> lines, string key, string value)
    {
        var pattern = $"^\\s*{Regex.Escape(key)}\\s*=";
        var replacement = $"{key}={value}";

        for (var i = 0; i < lines.Count; i++)
        {
            if (Regex.IsMatch(lines[i], pattern))
            {
                lines[i] = replacement;
                return lines;
            }
        }

        lines.Add(replacement);
        return lines;
    }

    private List<string> RemoveSetting(List<string> lines, string key)
    {
        var pattern = $"^\\s*{Regex.Escape(key)}\\s*=";

        for (var i = lines.Count - 1; i >= 0; i--)
        {
            if (Regex.IsMatch(lines[i], pattern))
                lines.RemoveAt(i);
        }

        return lines;
    }

    private BootSettingDefinition? GetDefinition(string key)
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
        await Task.Delay(5000);
        if (token == _statusToken)
            _statusLabel.Text = string.Empty;
    }

    private void ShowHelpDialog()
    {
        MessageBox.Show(
            "boot.config — служебный текстовый файл движка Unity в папке установки SCP:SL " +
            "(…\\SCP Secret Laboratory\\SCPSL_Data\\boot.config).\n\n" +
            "Большое окно показывает реальное содержимое файла. Галочки ниже добавляют или " +
            "убирают отдельные строки-настройки; снятие галочки удаляет строку и возвращает " +
            "поведение по умолчанию. Изменения сохраняются в файл кнопкой «Применить», и перед " +
            "записью создаётся резервная копия.\n\n" +
            "Настройки вступают в силу при следующем запуске игры. Обновление игры или проверка " +
            "целостности файлов в Steam сбрасывают boot.config к заводскому — тогда просто примените заново.",
            "Как это работает?",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private sealed class BootSettingDefinition
    {
        public BootSettingDefinition(string key, string checkedValue, UiTheme.Impact impact, string title, string description)
        {
            Key = key;
            CheckedValue = checkedValue;
            Impact = impact;
            Title = title;
            Description = description;
        }

        public string Key { get; }

        public string CheckedValue { get; }

        public UiTheme.Impact Impact { get; }

        public string Title { get; }

        public string Description { get; }

        public bool IsEnabled(string? currentValue)
        {
            return string.Equals(currentValue, CheckedValue, StringComparison.OrdinalIgnoreCase);
        }
    }
}
