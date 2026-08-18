using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArbuzTweaker;

/// <summary>
/// Вкладка с твиками из гайда PC-Tuning. Каждый пункт показывает честную оценку влияния:
/// часть твиков про FPS и задержки, часть — только про фон и приватность.
/// </summary>
public sealed class PcTuningTab : UserControl
{
    private readonly PcTuningService _service;
    private readonly Func<Task>? _restoreBackupAsync;
    private readonly Dictionary<PcTuningTweak, CheckBox> _checkBoxes = new();

    // Единый источник правды для галочек: список пересобирается при поиске и ресайзе,
    // и без этого словаря состояние отфильтрованных пунктов терялось.
    private readonly Dictionary<string, bool> _states = new(StringComparer.Ordinal);

    private Panel _listPanel = null!;
    private TextBox _searchTextBox = null!;
    private Label _statusLabel = null!;
    private int _statusToken;
    private int _lastListWidth = -1;

    private static readonly Font TweakFont = new("Segoe UI", 10F, FontStyle.Regular);
    private static readonly Font DescriptionFont = new("Segoe UI", 9F);
    private static readonly Font BadgeFont = new("Segoe UI Semibold", 8.5F);
    private static readonly Font HeaderFont = new("Segoe UI Semibold", 10.5F);

    public PcTuningTab(PcTuningService service, Func<Task>? restoreBackupAsync = null)
    {
        _service = service;
        _restoreBackupAsync = restoreBackupAsync;
        InitializeComponent();
        LoadState();
    }

    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Surface;
        AutoScroll = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = UiTheme.Surface,
            Padding = new Padding(20, 16, 20, 12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var infoLabel = new Label
        {
            Text = "Твики из гайда PC-Tuning. У каждого пункта честно указано, влияет ли он на производительность: "
                 + "часть снижает задержки и фризы, часть нужна только для чистого фона и приватности. "
                 + "Все изменения сохраняются в бэкап и откатываются кнопкой ниже.",
            AutoSize = true,
            MaximumSize = new Size(820, 0),
            ForeColor = UiTheme.TextMuted,
            Margin = new Padding(0, 0, 0, 10)
        };

        var searchPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 8)
        };

        _searchTextBox = new TextBox
        {
            Width = 320,
            Margin = new Padding(0, 0, 8, 0),
            PlaceholderText = "Поиск по названию или описанию"
        };
        UiTheme.StyleSearchTextBox(_searchTextBox);
        _searchTextBox.TextChanged += (s, e) => PopulateList();

        var selectSafeButton = CreateAutoSizedButton("Отметить безопасные", new Padding(0, 0, 8, 0));
        selectSafeButton.Click += (s, e) => SelectSafe();

        var clearButton = CreateAutoSizedButton("Снять все галочки", new Padding(0));
        clearButton.Click += (s, e) => SetAllChecked(false);

        searchPanel.Controls.Add(_searchTextBox);
        searchPanel.Controls.Add(selectSafeButton);
        searchPanel.Controls.Add(clearButton);

        _listPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        UiTheme.StyleListPanel(_listPanel);
        _listPanel.Resize += (s, e) =>
        {
            // Пересобираем только при реальной смене ширины: иначе список моргает на каждый Resize.
            if (_listPanel.ClientSize.Width != _lastListWidth)
                PopulateList();
        };

        var buttonsPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 8)
        };

        var applyButton = CreateAutoSizedButton("Применить", new Padding(0, 0, 10, 0), primary: true, tall: true);
        applyButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, ApplyAsync);

        var refreshButton = CreateAutoSizedButton("Обновить состояние", new Padding(0, 0, 10, 0), tall: true);
        refreshButton.Click += (s, e) =>
        {
            LoadState();
            ShowStatus("Состояние обновлено", UiTheme.AccentGreen);
        };

        buttonsPanel.Controls.Add(applyButton);
        buttonsPanel.Controls.Add(refreshButton);

        if (_restoreBackupAsync != null)
        {
            var restoreButton = CreateAutoSizedButton("Откатить бэкап реестра", new Padding(0), tall: true);
            restoreButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, RestoreAsync);
            buttonsPanel.Controls.Add(restoreButton);
        }

        _statusLabel = new Label
        {
            Text = string.Empty,
            AutoSize = true,
            ForeColor = UiTheme.AccentGreen,
            Margin = new Padding(0)
        };

        layout.Controls.Add(infoLabel, 0, 0);
        layout.Controls.Add(searchPanel, 0, 1);
        layout.Controls.Add(_listPanel, 0, 2);
        layout.Controls.Add(buttonsPanel, 0, 3);
        layout.Controls.Add(_statusLabel, 0, 4);

        Controls.Add(layout);
        PopulateList();
    }

    private void PopulateList()
    {
        if (_listPanel == null)
            return;

        _checkBoxes.Clear();
        _listPanel.SuspendLayout();
        UiTheme.ClearAndDisposeControls(_listPanel);
        _lastListWidth = _listPanel.ClientSize.Width;

        var query = _searchTextBox?.Text.Trim() ?? string.Empty;
        var rowWidth = Math.Max(560, _listPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 16);
        var y = 6;
        var supportedTweaks = _service.GetSupportedTweaks();

        foreach (var category in new[] { PcTuningCategory.Latency, PcTuningCategory.Stability, PcTuningCategory.Privacy })
        {
            var tweaks = supportedTweaks
                .Where(tweak => tweak.Category == category)
                .Where(tweak => Matches(tweak, query))
                .ToList();

            if (tweaks.Count == 0)
                continue;

            y = AddCategoryHeader(y, rowWidth, category);

            foreach (var tweak in tweaks)
                y = AddTweakRow(y, rowWidth, tweak, _states.TryGetValue(tweak.Id, out var isChecked) && isChecked);

            y += 8;
        }

        if (_checkBoxes.Count == 0)
            UiTheme.AddEmptyListMessage(_listPanel, y, rowWidth, "Ничего не найдено по запросу.");

        _listPanel.AutoScrollMinSize = new Size(0, y + 12);
        _listPanel.ResumeLayout();
    }

    private int AddCategoryHeader(int y, int width, PcTuningCategory category)
    {
        var (title, subtitle) = category switch
        {
            PcTuningCategory.Latency => ("Задержки и производительность", "Влияют на плавность и отзывчивость в играх"),
            PcTuningCategory.Stability => ("Фризы и фоновая активность", "Убирают внезапные подтормаживания, а не поднимают средний FPS"),
            _ => ("Приватность и чистый фон", "На производительность не влияют")
        };

        var header = new Label
        {
            Text = title,
            Location = new Point(8, y),
            Size = new Size(width - 16, 24),
            AutoSize = false,
            Font = HeaderFont,
            ForeColor = UiTheme.AccentGreen,
            BackColor = Color.Transparent
        };

        var hint = new Label
        {
            Text = subtitle,
            Location = new Point(8, y + 22),
            Size = new Size(width - 16, 20),
            AutoSize = false,
            Font = DescriptionFont,
            ForeColor = UiTheme.TextDim,
            BackColor = Color.Transparent
        };

        _listPanel.Controls.Add(header);
        _listPanel.Controls.Add(hint);
        return y + 48;
    }

    private int AddTweakRow(int y, int width, PcTuningTweak tweak, bool restoredChecked)
    {
        var innerWidth = width - 24;
        var badgeText = GetBadgeText(tweak.Impact);
        var badgeSize = TextRenderer.MeasureText(badgeText, BadgeFont) + new Size(14, 6);

        var descriptionText = tweak.Description;
        if (tweak.RequiresReboot)
            descriptionText += " Нужна перезагрузка.";

        var descriptionSize = TextRenderer.MeasureText(
            descriptionText,
            DescriptionFont,
            new Size(innerWidth - 8, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

        var warningHeight = 0;
        Size warningSize = Size.Empty;
        if (!string.IsNullOrWhiteSpace(tweak.Warning))
        {
            warningSize = TextRenderer.MeasureText(
                tweak.Warning,
                DescriptionFont,
                new Size(innerWidth - 8, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            warningHeight = warningSize.Height + 6;
        }

        var rowHeight = 34 + descriptionSize.Height + warningHeight + 12;

        var rowPanel = new Panel
        {
            Location = new Point(8, y),
            Size = new Size(width - 16, rowHeight),
            BackColor = UiTheme.SurfaceAlt,
            BorderStyle = BorderStyle.None
        };

        var checkBox = new CheckBox
        {
            Text = tweak.Name,
            Location = new Point(10, 8),
            Size = new Size(innerWidth - badgeSize.Width - 12, 22),
            AutoSize = false,
            UseMnemonic = false,
            Font = TweakFont,
            ForeColor = UiTheme.TextPrimary,
            BackColor = Color.Transparent,
            Checked = restoredChecked
        };
        checkBox.CheckedChanged += (s, e) => _states[tweak.Id] = checkBox.Checked;

        var badge = new Label
        {
            Text = badgeText,
            Location = new Point(rowPanel.Width - badgeSize.Width - 12, 9),
            Size = badgeSize,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = BadgeFont,
            ForeColor = GetBadgeForeColor(tweak.Impact),
            BackColor = GetBadgeBackColor(tweak.Impact)
        };

        var descriptionLabel = new Label
        {
            Text = descriptionText,
            Location = new Point(30, 32),
            Size = new Size(innerWidth - 8, descriptionSize.Height),
            AutoSize = false,
            UseMnemonic = false,
            Font = DescriptionFont,
            ForeColor = UiTheme.TextMuted,
            BackColor = Color.Transparent
        };

        rowPanel.Controls.Add(checkBox);
        rowPanel.Controls.Add(badge);
        rowPanel.Controls.Add(descriptionLabel);

        if (warningHeight > 0)
        {
            var warningLabel = new Label
            {
                Text = "! " + tweak.Warning,
                Location = new Point(30, 32 + descriptionSize.Height + 4),
                Size = new Size(innerWidth - 8, warningSize.Height),
                AutoSize = false,
                UseMnemonic = false,
                Font = DescriptionFont,
                ForeColor = Color.Orange,
                BackColor = Color.Transparent
            };
            rowPanel.Controls.Add(warningLabel);
        }

        _checkBoxes[tweak] = checkBox;
        _listPanel.Controls.Add(rowPanel);
        return y + rowHeight + 6;
    }

    // Ширину кнопок считает WinForms: при масштабе экрана 125% фиксированные размеры
    // обрезали русские подписи прямо посередине слова.
    private static Button CreateAutoSizedButton(string text, Padding margin, bool primary = false, bool tall = false)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 0, 14, 0),
            MinimumSize = new Size(0, tall ? 35 : 31),
            Margin = margin
        };

        UiTheme.StyleActionButton(button, primary);
        return button;
    }

    private static bool Matches(PcTuningTweak tweak, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return tweak.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || tweak.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetBadgeText(PcTuningImpact impact)
    {
        return impact switch
        {
            PcTuningImpact.Measured => "влияет",
            PcTuningImpact.Situational => "ситуативно",
            _ => "не про FPS"
        };
    }

    private static Color GetBadgeForeColor(PcTuningImpact impact)
    {
        return impact switch
        {
            PcTuningImpact.Measured => Color.White,
            PcTuningImpact.Situational => Color.White,
            _ => UiTheme.TextDim
        };
    }

    private static Color GetBadgeBackColor(PcTuningImpact impact)
    {
        return impact switch
        {
            PcTuningImpact.Measured => Color.FromArgb(0, 130, 70),
            PcTuningImpact.Situational => Color.FromArgb(60, 90, 120),
            _ => Color.FromArgb(48, 48, 48)
        };
    }

    // Состояние читается в фоне: часть твиков опрашивает WMI через PowerShell,
    // а синхронный опрос подвешивал вкладку на несколько секунд при открытии.
    private async void LoadState()
    {
        var tweaks = _service.GetSupportedTweaks();
        if (tweaks.Count == 0)
            return;

        var states = await Task.Run(() => tweaks.ToDictionary(tweak => tweak.Id, tweak => _service.IsEnabled(tweak)));

        if (IsDisposed || Disposing)
            return;

        foreach (var (id, isEnabled) in states)
            _states[id] = isEnabled;

        foreach (var (tweak, checkBox) in _checkBoxes)
        {
            if (states.TryGetValue(tweak.Id, out var isEnabled))
                checkBox.Checked = isEnabled;
        }
    }

    private void SetAllChecked(bool value)
    {
        foreach (var tweak in _service.GetSupportedTweaks())
            _states[tweak.Id] = value;

        foreach (var checkBox in _checkBoxes.Values)
            checkBox.Checked = value;
    }

    private void SelectSafe()
    {
        // Безопасными считаем твики без отдельного предупреждения.
        foreach (var tweak in _service.GetSupportedTweaks())
            _states[tweak.Id] = string.IsNullOrWhiteSpace(tweak.Warning);

        foreach (var (tweak, checkBox) in _checkBoxes)
            checkBox.Checked = string.IsNullOrWhiteSpace(tweak.Warning);

        ShowStatus("Отмечены твики без предупреждений", UiTheme.AccentGreen);
    }

    private async Task ApplyAsync()
    {
        // Берём все поддерживаемые твики, а не только видимые: при активном поиске
        // отмеченные ранее пункты не должны молча выпадать из применения.
        var desired = _service.GetSupportedTweaks()
            .Select(tweak => (Tweak: tweak, Enable: _states.TryGetValue(tweak.Id, out var value) && value))
            .ToList();

        var planned = await Task.Run(() => desired
            .Where(item => item.Enable != _service.IsEnabled(item.Tweak))
            .ToList());

        if (planned.Count == 0)
        {
            ShowStatus("Нет изменений для применения", UiTheme.TextDim);
            return;
        }

        if (planned.Any(change => change.Tweak.RequiresAdmin) && !IsRunningAsAdministrator())
        {
            ShowStatus("Для этих твиков нужен запуск от имени администратора", Color.Orange);
            return;
        }

        if (!ConfirmPreview(planned))
            return;

        var applied = 0;
        var failed = 0;
        var needsReboot = false;

        foreach (var change in planned)
        {
            if (await _service.ApplyAsync(change.Tweak, change.Enable))
            {
                applied++;
                needsReboot |= change.Tweak.RequiresReboot;
            }
            else
            {
                failed++;
            }
        }

        LoadState();

        var message = failed == 0 ? $"Применено твиков: {applied}" : $"Применено: {applied}, с ошибкой: {failed}";
        if (needsReboot)
            message += ". Часть изменений вступит в силу после перезагрузки";

        ShowStatus(message, failed == 0 ? UiTheme.AccentGreen : Color.Orange);
    }

    private bool ConfirmPreview(IReadOnlyList<(PcTuningTweak Tweak, bool Enable)> planned)
    {
        var preview = new StringBuilder();
        preview.AppendLine("Будут применены изменения:");
        preview.AppendLine();

        foreach (var change in planned.Take(14))
            preview.AppendLine((change.Enable ? "Включить: " : "Вернуть как было: ") + change.Tweak.Name);

        if (planned.Count > 14)
        {
            preview.AppendLine();
            preview.AppendLine("И ещё пунктов: " + (planned.Count - 14));
        }

        var warnings = planned
            .Where(change => change.Enable && !string.IsNullOrWhiteSpace(change.Tweak.Warning))
            .Select(change => "— " + change.Tweak.Name + ": " + change.Tweak.Warning)
            .ToList();

        if (warnings.Count > 0)
        {
            preview.AppendLine();
            preview.AppendLine("Обратите внимание:");
            foreach (var warning in warnings)
                preview.AppendLine(warning);
        }

        preview.AppendLine();
        preview.AppendLine("Прежние значения сохранятся в бэкап реестра. Продолжить?");

        return MessageBox.Show(
            preview.ToString(),
            "Перед применением",
            MessageBoxButtons.YesNo,
            warnings.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information) == DialogResult.Yes;
    }

    private async Task RestoreAsync()
    {
        if (_restoreBackupAsync == null)
            return;

        await _restoreBackupAsync();
        LoadState();
    }

    private static bool IsRunningAsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
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
}
