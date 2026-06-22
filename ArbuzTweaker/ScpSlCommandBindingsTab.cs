using System.Diagnostics;

namespace ArbuzTweaker;

public partial class ScpSlCommandBindingsTab : UserControl
{
    private readonly ScpSlService _scpSlService;
    private TextBox _bindingsTextBox = null!;
    private TextBox _bindingsSearchTextBox = null!;
    private Label _pathLabel = null!;
    private Label _statusLabel = null!;
    private int _lastSearchStart;

    public ScpSlCommandBindingsTab(ScpSlService scpSlService)
    {
        _scpSlService = scpSlService;
        InitializeComponent();
        LoadStateAsync();
    }

    private async void LoadStateAsync()
    {
        var path = _scpSlService.GetCommandBindingsPath();
        _pathLabel.Text = File.Exists(path)
            ? $"cmdbinding.txt: {path}"
            : $"cmdbinding.txt будет создан: {path}";
        _pathLabel.ForeColor = File.Exists(path) ? Color.Green : Color.Orange;

        var content = await _scpSlService.LoadCommandBindingsAsync();
        if (content != null)
        {
            _bindingsTextBox.Text = NormalizeLineEndings(content);
            return;
        }

        ShowStatus("Не удалось прочитать cmdbinding.txt", Color.Orange);
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
            RowCount = 9
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle());
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
        rootLayout.RowStyles.Add(new RowStyle());

        var titleLabel = new Label
        {
            Text = "SCP:SL - бинды команд",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6)
        };

        _pathLabel = new Label
        {
            Text = "Поиск cmdbinding.txt...",
            AutoSize = true,
            ForeColor = Color.Gray,
            Margin = new Padding(0, 0, 0, 12)
        };

        var infoLabel = new Label
        {
            Text = "Эта вкладка редактирует пользовательский файл биндов SCP:SL. Она не отправляет команды в игру и не автоматизирует игровой процесс.",
            AutoSize = true,
            ForeColor = Color.Gainsboro,
            MaximumSize = new Size(980, 0),
            Margin = new Padding(0, 0, 0, 12)
        };

        var bindingsLabel = new Label
        {
            Text = "Содержимое cmdbinding.txt:",
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        };

        var searchPanel = CreateBindingsSearchPanel();

        _bindingsTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            AcceptsReturn = true,
            AcceptsTab = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            MinimumSize = new Size(0, 80),
            Margin = new Padding(0, 0, 0, 12)
        };
        UiTheme.StyleEditorTextBox(_bindingsTextBox);
        _bindingsTextBox.TextChanged += (s, e) => _lastSearchStart = 0;

        var examplesLabel = new Label
        {
            Text = "Пример: 104:emotion happy",
            AutoSize = true,
            ForeColor = Color.Gainsboro,
            Margin = new Padding(0, 0, 0, 8)
        };

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 10)
        };

        var saveButton = new Button { Text = "Сохранить", Size = new Size(115, 35), Margin = new Padding(0, 0, 10, 0) };
        saveButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, SaveAsync);

        var openFolderButton = new Button { Text = "Показать файл", Size = new Size(140, 35), Margin = new Padding(0, 0, 10, 0) };
        openFolderButton.Click += (s, e) => OpenCommandBindingsFolder();

        var guideButton = new Button { Text = "Инструкция", Size = new Size(125, 35), Margin = new Padding(0, 0, 10, 0) };
        guideButton.Click += (s, e) => ShowGuideDialog();

        var keyCodesButton = new Button { Text = "Клавиши", Size = new Size(125, 35), Margin = new Padding(0, 0, 10, 0) };
        keyCodesButton.Click += (s, e) => ShowKeyCodesDialog();

        var clearButton = new Button { Text = "Очистить", Size = new Size(110, 35), Margin = new Padding(0) };
        clearButton.Click += (s, e) => _bindingsTextBox.Clear();

        UiTheme.StyleActionButton(saveButton, true);
        UiTheme.StyleActionButton(openFolderButton);
        UiTheme.StyleActionButton(guideButton);
        UiTheme.StyleActionButton(keyCodesButton);
        UiTheme.StyleActionButton(clearButton);

        buttonsPanel.Controls.Add(saveButton);
        buttonsPanel.Controls.Add(openFolderButton);
        buttonsPanel.Controls.Add(guideButton);
        buttonsPanel.Controls.Add(keyCodesButton);
        buttonsPanel.Controls.Add(clearButton);

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
        rootLayout.Controls.Add(bindingsLabel, 0, 3);
        rootLayout.Controls.Add(searchPanel, 0, 4);
        rootLayout.Controls.Add(_bindingsTextBox, 0, 5);
        rootLayout.Controls.Add(examplesLabel, 0, 6);
        rootLayout.Controls.Add(buttonsPanel, 0, 7);
        rootLayout.Controls.Add(_statusLabel, 0, 8);

        Controls.Add(rootLayout);
    }

    private FlowLayoutPanel CreateBindingsSearchPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 8)
        };

        _bindingsSearchTextBox = new TextBox
        {
            Width = 360,
            Margin = new Padding(0, 0, 8, 0)
        };
        UiTheme.StyleSearchTextBox(_bindingsSearchTextBox);
        _bindingsSearchTextBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.SuppressKeyPress = true;
            FindNextBinding();
        };

        var searchButton = new Button { Text = "Найти", Size = new Size(100, 31), Margin = new Padding(0, 0, 8, 0) };
        searchButton.Click += (s, e) => FindNextBinding();
        UiTheme.StyleActionButton(searchButton, true);

        var clearButton = new Button { Text = "Сбросить поиск", Size = new Size(145, 31), Margin = new Padding(0) };
        clearButton.Click += (s, e) =>
        {
            _bindingsSearchTextBox.Clear();
            _lastSearchStart = 0;
            _bindingsTextBox.SelectionLength = 0;
        };
        UiTheme.StyleActionButton(clearButton);

        panel.Controls.Add(_bindingsSearchTextBox);
        panel.Controls.Add(searchButton);
        panel.Controls.Add(clearButton);
        return panel;
    }

    private void FindNextBinding()
    {
        var query = _bindingsSearchTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            ShowStatus("Введите текст для поиска", Color.Orange);
            return;
        }

        var text = _bindingsTextBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            ShowStatus("Файл биндов пуст", Color.Orange);
            return;
        }

        var start = Math.Min(Math.Max(_lastSearchStart, 0), text.Length);
        var index = text.IndexOf(query, start, StringComparison.OrdinalIgnoreCase);
        if (index < 0 && start > 0)
            index = text.IndexOf(query, 0, StringComparison.OrdinalIgnoreCase);

        if (index < 0)
        {
            ShowStatus("Ничего не найдено", Color.Orange);
            _lastSearchStart = 0;
            return;
        }

        _bindingsTextBox.Focus();
        _bindingsTextBox.Select(index, query.Length);
        _bindingsTextBox.ScrollToCaret();
        _lastSearchStart = index + query.Length;
        ShowStatus("Совпадение найдено", Color.Green);
    }

    private async Task SaveAsync()
    {
        var content = NormalizeLineEndings(_bindingsTextBox.Text);
        if (await _scpSlService.SaveCommandBindingsAsync(content))
        {
            _bindingsTextBox.Text = content;
            var path = _scpSlService.GetCommandBindingsPath();
            _pathLabel.Text = $"cmdbinding.txt: {path}";
            _pathLabel.ForeColor = Color.Green;
            ShowStatus("cmdbinding.txt сохранён", Color.Green);
            return;
        }

        ShowStatus("Не удалось сохранить cmdbinding.txt", Color.Orange);
    }

    private void OpenCommandBindingsFolder()
    {
        try
        {
            var path = _scpSlService.GetCommandBindingsPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
                return;
            }

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = directory,
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            ShowStatus("Не удалось открыть папку cmdbinding.txt", Color.Orange);
        }
    }

    private static void ShowGuideDialog()
    {
        MessageBox.Show(
            "Как добавлять бинды в файл:\n" +
            "1. Каждый бинд пишется с новой строки.\n" +
            "2. Формат строки: КОД_КЛАВИШИ:КОМАНДА\n" +
            "3. Код клавиши можно посмотреть через кнопку \"Список клавиш\".\n\n" +
            "Пример:\n" +
            "104:emotion happy\n\n" +
            "Удаление бинда:\n" +
            "Удалите ненужную строку из файла ниже и нажмите \"Сохранить\".",
            "SCP:SL - инструкция по биндам",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static void ShowKeyCodesDialog()
    {
        using var dialog = new Form
        {
            Text = "SCP:SL - коды клавиш",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(560, 720),
            MinimumSize = new Size(420, 520),
            BackColor = UiTheme.Surface,
            ForeColor = UiTheme.TextPrimary,
            Font = new Font("Segoe UI", 10)
        };

        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 10),
            Text = NormalizeLineEndings(KeyCodesText),
            BackColor = Color.FromArgb(24, 24, 24),
            ForeColor = UiTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };

        var closeButton = new Button
        {
            Text = "Закрыть",
            Dock = DockStyle.Bottom,
            Height = 38
        };
        closeButton.Click += (s, e) => dialog.Close();

        dialog.Controls.Add(textBox);
        dialog.Controls.Add(closeButton);
        dialog.ShowDialog();
    }

    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
    }

    private async void ShowStatus(string message, Color color)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = color;
        await Task.Delay(2500);
        _statusLabel.Text = string.Empty;
    }

    private const string KeyCodesText = """
Формат строки в cmdbinding.txt: КОД_КЛАВИШИ:КОМАНДА
Пример: 104:emotion happy

Основные клавиши:
None = 0
Backspace = 8
Tab = 9
Return = 13
Pause = 19
Escape = 27
Space = 32
Delete = 127

Цифры:
Alpha0 = 48
Alpha1 = 49
Alpha2 = 50
Alpha3 = 51
Alpha4 = 52
Alpha5 = 53
Alpha6 = 54
Alpha7 = 55
Alpha8 = 56
Alpha9 = 57

Буквы:
A = 97
B = 98
C = 99
D = 100
E = 101
F = 102
G = 103
H = 104
I = 105
J = 106
K = 107
L = 108
M = 109
N = 110
O = 111
P = 112
Q = 113
R = 114
S = 115
T = 116
U = 117
V = 118
W = 119
X = 120
Y = 121
Z = 122

Numpad:
Keypad0 = 256
Keypad1 = 257
Keypad2 = 258
Keypad3 = 259
Keypad4 = 260
Keypad5 = 261
Keypad6 = 262
Keypad7 = 263
Keypad8 = 264
Keypad9 = 265
KeypadPeriod = 266
KeypadDivide = 267
KeypadMultiply = 268
KeypadMinus = 269
KeypadPlus = 270
KeypadEnter = 271
KeypadEquals = 272

Стрелки и навигация:
UpArrow = 273
DownArrow = 274
RightArrow = 275
LeftArrow = 276
Insert = 277
Home = 278
End = 279
PageUp = 280
PageDown = 281

F-клавиши:
F1 = 282
F2 = 283
F3 = 284
F4 = 285
F5 = 286
F6 = 287
F7 = 288
F8 = 289
F9 = 290
F10 = 291
F11 = 292
F12 = 293
F13 = 294
F14 = 295
F15 = 296

Модификаторы:
Numlock = 300
CapsLock = 301
ScrollLock = 302
RightShift = 303
LeftShift = 304
RightControl = 305
LeftControl = 306
RightAlt = 307
LeftAlt = 308
LeftWindows = 311
RightWindows = 312
AltGr = 313
Print = 316
Break = 318
Menu = 319

Мышь:
Mouse0 = 323
Mouse1 = 324
Mouse2 = 325
Mouse3 = 326
Mouse4 = 327
Mouse5 = 328
Mouse6 = 329

Символы:
Exclaim = 33
DoubleQuote = 34
Hash = 35
Dollar = 36
Ampersand = 38
Quote = 39
LeftParen = 40
RightParen = 41
Asterisk = 42
Plus = 43
Comma = 44
Minus = 45
Period = 46
Slash = 47
Colon = 58
Semicolon = 59
Less = 60
Equals = 61
Greater = 62
Question = 63
At = 64
LeftBracket = 91
Backslash = 92
RightBracket = 93
Caret = 94
Underscore = 95
BackQuote = 96

Геймпад/джойстик:
JoystickButton0 = 330
JoystickButton1 = 331
JoystickButton2 = 332
JoystickButton3 = 333
JoystickButton4 = 334
JoystickButton5 = 335
JoystickButton6 = 336
JoystickButton7 = 337
JoystickButton8 = 338
JoystickButton9 = 339
JoystickButton10 = 340
JoystickButton11 = 341
JoystickButton12 = 342
JoystickButton13 = 343
JoystickButton14 = 344
JoystickButton15 = 345
JoystickButton16 = 346
JoystickButton17 = 347
JoystickButton18 = 348
JoystickButton19 = 349
""";
}
