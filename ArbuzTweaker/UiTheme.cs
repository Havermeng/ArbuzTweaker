using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArbuzTweaker;

internal static class UiTheme
{
    public static readonly Color WindowBackground = Color.FromArgb(20, 20, 20);
    public static readonly Color Surface = Color.FromArgb(34, 34, 34);
    public static readonly Color SurfaceAlt = Color.FromArgb(28, 28, 28);
    public static readonly Color Border = Color.FromArgb(88, 88, 88);
    public static readonly Color AccentBlue = Color.FromArgb(28, 132, 226);
    public static readonly Color AccentBlueHover = Color.FromArgb(40, 144, 238);
    public static readonly Color AccentGreen = Color.FromArgb(0, 210, 110);
    public static readonly Color TextPrimary = Color.White;
    public static readonly Color TextMuted = Color.Gainsboro;
    public static readonly Color TextDim = Color.Gray;

    private static readonly Dictionary<string, Image> SidebarIcons = new();

    // Шрифты общие и не диспозятся: Control.Font не владеет шрифтом, а создание
    // нового Font на каждый вызов Style* утекало GDI-хендлами (лимит 10000 на процесс).
    private static readonly Font SidebarButtonFont = new("Segoe UI Semibold", 10.5F, FontStyle.Regular);
    private static readonly Font ActionButtonFont = new("Segoe UI Semibold", 10F, FontStyle.Regular);
    private static readonly Font EditorFont = new("Consolas", 10);
    private static readonly Font SearchFont = new("Segoe UI", 10);
    private static readonly Font SectionHeaderFont = new("Segoe UI Semibold", 9.5F, FontStyle.Regular);
    private static readonly Font SectionTitleFont = new("Segoe UI Semibold", 10.5F, FontStyle.Regular);
    private static readonly Font ListDescriptionFont = new("Segoe UI", 9.5F);

    public static void StyleSidebarButton(Button button, bool active)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = AccentBlueHover;
        button.FlatAppearance.MouseDownBackColor = AccentBlue;
        button.BackColor = active ? AccentBlue : Surface;
        button.ForeColor = TextPrimary;
        button.Font = SidebarButtonFont;
        button.TextAlign = ContentAlignment.MiddleLeft;
        button.Padding = new Padding(12, 0, 0, 0);
        button.Cursor = Cursors.Hand;
        button.ImageAlign = ContentAlignment.MiddleLeft;
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        button.Image = GetSidebarIcon(button.Tag as string ?? button.Text);
    }

    public static void StyleActionButton(Button button, bool primary = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = primary ? AccentBlue : Border;
        button.FlatAppearance.MouseOverBackColor = primary ? AccentBlueHover : Color.FromArgb(48, 48, 48);
        button.FlatAppearance.MouseDownBackColor = primary ? AccentBlue : Color.FromArgb(56, 56, 56);
        button.BackColor = primary ? AccentBlue : SurfaceAlt;
        button.ForeColor = TextPrimary;
        button.Font = ActionButtonFont;
        button.Cursor = Cursors.Hand;
    }

    public static void StyleEditorTextBox(TextBox textBox)
    {
        textBox.BackColor = Color.FromArgb(24, 24, 24);
        textBox.ForeColor = TextPrimary;
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.Font = EditorFont;
        textBox.HideSelection = false;
    }

    public static void StyleSearchTextBox(TextBox textBox)
    {
        textBox.BackColor = Color.FromArgb(24, 24, 24);
        textBox.ForeColor = TextPrimary;
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.Font = SearchFont;
    }

    // Подписи с AutoSize и фиксированным MaximumSize обрезались, когда контентная
    // область уже зашитой ширины: перенос строк подгоняется под реальную ширину контейнера.
    public static void EnableDynamicLabelWrap(Control container, params Control[] controls)
    {
        void UpdateWidths()
        {
            var available = container.ClientSize.Width;
            foreach (var control in controls)
            {
                var width = Math.Max(240, available - control.Margin.Horizontal - container.Padding.Horizontal - 8);
                control.MaximumSize = new Size(width, 0);
            }
        }

        container.SizeChanged += (s, e) => UpdateWidths();
        UpdateWidths();
    }

    // То же самое, но для всего дерева контролов: каждой подписи с AutoSize и заданным
    // MaximumSize ширина переноса пересчитывается от реальной ширины её родителя.
    public static void EnableDynamicLabelWrapForDescendants(Control root)
    {
        void UpdateWidths()
        {
            void Walk(Control parent)
            {
                foreach (Control child in parent.Controls)
                {
                    if (child is Label or CheckBox && child.AutoSize && child.MaximumSize.Width > 0 && parent.ClientSize.Width > 0)
                    {
                        var width = Math.Max(240, parent.ClientSize.Width - child.Left - child.Margin.Right - 10);
                        child.MaximumSize = new Size(width, 0);
                    }

                    Walk(child);
                }
            }

            Walk(root);
        }

        root.SizeChanged += (s, e) => UpdateWidths();
        UpdateWidths();
    }

    public static void StyleListPanel(Panel panel)
    {
        panel.BorderStyle = BorderStyle.None;
        panel.BackColor = Surface;
    }

    /// <summary>Включает двойную буферизацию у панели — убирает мерцание при ресайзе/сворачивании.</summary>
    public static void EnableDoubleBuffering(Control control)
    {
        typeof(Control)
            .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(control, true, null);
    }

    public static void ClearAndDisposeControls(Control container)
    {
        var oldControls = new Control[container.Controls.Count];
        container.Controls.CopyTo(oldControls, 0);
        container.Controls.Clear();

        foreach (var control in oldControls)
            control.Dispose();
    }

    public static void StyleTabControl(TabControl tabControl)
    {
        tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabControl.SizeMode = TabSizeMode.Fixed;
        tabControl.ItemSize = new Size(170, 34);
        tabControl.DrawItem += TabControl_DrawItem;

        // Заголовки растягиваются на всю ширину: пустой хвост полосы вкладок ComCtl
        // рисует системным светлым цветом, и на тёмной теме он выглядел белой полосой.
        // Флаг и гистерезис обязательны: смена ItemSize сама меняет ClientSize и снова
        // вызывает SizeChanged — без защиты это бесконечная рекурсия (stack overflow).
        var stretchingTabHeaders = false;
        void StretchTabHeaders()
        {
            if (stretchingTabHeaders || tabControl.TabCount == 0 || tabControl.ClientSize.Width <= 0)
                return;

            stretchingTabHeaders = true;
            try
            {
                var width = Math.Max(120, (tabControl.ClientSize.Width - 6) / tabControl.TabCount);
                if (Math.Abs(tabControl.ItemSize.Width - width) > 2)
                    tabControl.ItemSize = new Size(width, 34);
            }
            finally
            {
                stretchingTabHeaders = false;
            }
        }

        tabControl.SizeChanged += (s, e) => StretchTabHeaders();
        tabControl.ControlAdded += (s, e) => StretchTabHeaders();
        tabControl.HandleCreated += (s, e) => StretchTabHeaders();
    }

    private static void TabControl_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tabControl || e.Index < 0 || e.Index >= tabControl.TabPages.Count)
            return;

        var isSelected = e.Index == tabControl.SelectedIndex;
        var tabRect = tabControl.GetTabRect(e.Index);

        using var backBrush = new SolidBrush(isSelected ? Surface : SurfaceAlt);
        e.Graphics.FillRectangle(backBrush, tabRect);

        if (isSelected)
        {
            using var accentBrush = new SolidBrush(AccentBlue);
            e.Graphics.FillRectangle(accentBrush, tabRect.X, tabRect.Bottom - 3, tabRect.Width, 3);
        }

        TextRenderer.DrawText(
            e.Graphics,
            tabControl.TabPages[e.Index].Text,
            tabControl.Font,
            tabRect,
            isSelected ? TextPrimary : TextMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    public static int AddListSectionHeader(Panel panel, int y, int width, string text)
    {
        var label = new Label
        {
            Text = text,
            Location = new Point(8, y),
            Size = new Size(width, 28),
            AutoSize = false,
            Font = SectionHeaderFont,
            ForeColor = AccentGreen,
            BackColor = SurfaceAlt,
            Padding = new Padding(8, 5, 0, 0)
        };

        panel.Controls.Add(label);
        return y + 34;
    }

    public static int AddCheckListRow(
        Panel panel,
        int y,
        int availableWidth,
        int rowIndex,
        string command,
        string description,
        bool isChecked,
        EventHandler checkedChanged,
        out CheckBox checkBox,
        object? tag = null,
        bool highlighted = false,
        Impact? impact = null)
    {
        // Маркер влияния в правом верхнем углу; под него резервируем место, чтобы описание не налезало.
        var badge = impact.HasValue ? CreateImpactBadge(impact.Value) : null;
        var badgeReserve = badge != null ? badge.Width + 14 : 0;

        var checkBoxWidth = Math.Clamp((int)(availableWidth * 0.40), 240, 360);
        var descriptionX = checkBoxWidth + 24;
        var descriptionWidth = Math.Max(180, availableWidth - checkBoxWidth - 36 - badgeReserve);
        var descriptionFont = ListDescriptionFont;
        var descriptionSize = TextRenderer.MeasureText(
            description,
            descriptionFont,
            new Size(descriptionWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix | TextFormatFlags.Left);
        var rowHeight = Math.Max(42, descriptionSize.Height + 18);

        // Единый вид карточки со вкладками «Система» и «Оптимизация»: тёмная подложка без рамки,
        // выделение поиска — синеватым. Чередование строк убрано намеренно.
        var rowPanel = new Panel
        {
            Location = new Point(8, y),
            Size = new Size(availableWidth, rowHeight),
            BackColor = highlighted ? Color.FromArgb(45, 70, 90) : SurfaceAlt,
            BorderStyle = BorderStyle.None
        };

        checkBox = new CheckBox
        {
            Text = command,
            Location = new Point(10, 9),
            Size = new Size(checkBoxWidth, rowHeight - 16),
            AutoSize = false,
            ForeColor = TextPrimary,
            Tag = tag ?? command,
            BackColor = Color.Transparent,
            UseMnemonic = false,
            Checked = isChecked
        };
        checkBox.CheckedChanged += checkedChanged;

        var descriptionLabel = new Label
        {
            Text = description,
            Location = new Point(descriptionX, 8),
            Size = new Size(descriptionWidth, rowHeight - 14),
            AutoSize = false,
            UseMnemonic = false,
            TextAlign = ContentAlignment.TopLeft,
            Font = descriptionFont,
            ForeColor = TextMuted,
            BackColor = Color.Transparent
        };

        rowPanel.Controls.Add(checkBox);
        rowPanel.Controls.Add(descriptionLabel);

        if (badge != null)
        {
            badge.Location = new Point(availableWidth - badge.Width - 12, 9);
            rowPanel.Controls.Add(badge);
        }

        panel.Controls.Add(rowPanel);

        return y + rowHeight + 6;
    }

    public static void AddEmptyListMessage(Panel panel, int y, int width, string text)
    {
        panel.Controls.Add(new Label
        {
            Text = text,
            Location = new Point(8, y),
            Size = new Size(width, 42),
            AutoSize = false,
            ForeColor = TextDim,
            BackColor = SurfaceAlt,
            Padding = new Padding(10, 10, 0, 0)
        });
    }

    public static Panel CreateSectionPanel()
    {
        return new Panel
        {
            BackColor = SurfaceAlt,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(14),
            Margin = new Padding(0, 0, 0, 14),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top
        };
    }

    public static Label CreateSectionTitle(string text)
    {
        return new Label
        {
            Text = text,
            Font = SectionTitleFont,
            ForeColor = TextPrimary,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };
    }

    private static readonly Font SectionHeaderLabelFont = new("Segoe UI Semibold", 11F, FontStyle.Regular);
    private static readonly Font CardTitleFont = new("Segoe UI Semibold", 10F, FontStyle.Regular);

    /// <summary>Заголовок секции (акцентно-зелёный) для потоковых раскладок — единый вид на всех вкладках.</summary>
    public static Label CreateSectionHeaderLabel(string text)
    {
        return new Label
        {
            Text = text,
            Font = SectionHeaderLabelFont,
            ForeColor = AccentGreen,
            AutoSize = true,
            UseMnemonic = false,
            Margin = new Padding(2, 12, 0, 6)
        };
    }

    /// <summary>Карточка твика (тёмная подложка) — единый контейнер для пунктов на вкладках Windows.</summary>
    public static Panel CreateCard()
    {
        return new Panel
        {
            BackColor = SurfaceAlt,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(0),
            BorderStyle = BorderStyle.None
        };
    }

    /// <summary>Заголовок внутри карточки (для пунктов без чекбокса — действия вроде «восстановить сеть»).</summary>
    public static Label CreateCardTitle(string text)
    {
        return new Label
        {
            Text = text,
            Font = CardTitleFont,
            ForeColor = TextPrimary,
            AutoSize = true,
            UseMnemonic = false
        };
    }

    /// <summary>Честная оценка влияния твика — единый смысл маркеров на всех вкладках.</summary>
    public enum Impact
    {
        /// <summary>Реально поднимает FPS / снижает задержку.</summary>
        Fps,

        /// <summary>Помогает против фризов, лагов и утечек — если проблема есть.</summary>
        AntiStutter,

        /// <summary>На FPS не влияет — фон, приватность, удобство.</summary>
        Background
    }

    private static readonly Font BadgeFont = new("Segoe UI Semibold", 8.5F);

    public static string ImpactText(Impact impact) => impact switch
    {
        Impact.Fps => "+FPS",
        Impact.AntiStutter => "против фризов",
        _ => "чистит фон"
    };

    /// <summary>Маркер-плашка с честной оценкой влияния твика. Одинаковый вид на всех вкладках Windows.</summary>
    public static Label CreateImpactBadge(Impact impact)
    {
        var (fore, back) = impact switch
        {
            Impact.Fps => (Color.White, Color.FromArgb(0, 130, 70)),
            Impact.AntiStutter => (Color.White, Color.FromArgb(60, 90, 120)),
            _ => (TextDim, Color.FromArgb(48, 48, 48))
        };

        var badge = new Label
        {
            Text = ImpactText(impact),
            AutoSize = true,
            Font = BadgeFont,
            ForeColor = fore,
            BackColor = back,
            Padding = new Padding(8, 3, 8, 3),
            TextAlign = ContentAlignment.MiddleCenter,
            UseMnemonic = false,
            Margin = new Padding(0)
        };

        // Фиксируем размер сразу, чтобы badge.Width был надёжен ещё до добавления в контейнер
        // (иначе позиционирование по правому краю обрезало более широкие плашки).
        badge.Size = badge.PreferredSize;
        badge.AutoSize = false;
        return badge;
    }

    public static async Task RunButtonOperationAsync(object? sender, Func<Task> operation)
    {
        if (sender is not Control control)
        {
            await operation();
            return;
        }

        if (!control.Enabled)
            return;

        var form = control.FindForm();
        var previousCursor = control.Cursor;
        control.Enabled = false;
        control.Cursor = Cursors.WaitCursor;
        if (form != null)
            form.UseWaitCursor = true;

        try
        {
            await operation();
        }
        finally
        {
            if (form != null)
                form.UseWaitCursor = false;

            control.Cursor = previousCursor;
            control.Enabled = true;
        }
    }

    private static Image GetSidebarIcon(string key)
    {
        if (SidebarIcons.TryGetValue(key, out var cached))
            return cached;

        var icon = CreateSidebarIcon(key);
        SidebarIcons[key] = icon;
        return icon;
    }

    private static Image CreateSidebarIcon(string key)
    {
        if (key == "Dota 2" && TryLoadSteamShortcutIcon("Dota 2") is Image dotaIcon)
            return dotaIcon;

        if (key == "SCP:SL" && TryLoadSteamShortcutIcon("SCP Secret Laboratory") is Image scpIcon)
            return scpIcon;

        var bitmap = new Bitmap(18, 18);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        switch (key)
        {
            case "Windows":
                DrawWindowsIcon(graphics);
                break;
            case "Dota 2":
                DrawDotaIcon(graphics);
                break;
            case "SCP:SL":
                DrawScpIcon(graphics);
                break;
            case "Прицел":
                DrawCrosshairIcon(graphics);
                break;
            case "Функции":
                DrawFunctionsIcon(graphics);
                break;
            case "Стороннее ПО":
                DrawSettingsIcon(graphics);
                break;
            case "Настройки":
                DrawGearIcon(graphics);
                break;
            case "Гайд":
                DrawGuideIcon(graphics);
                break;
            default:
                DrawDotIcon(graphics);
                break;
        }

        return bitmap;
    }

    private static Image? TryLoadSteamShortcutIcon(string shortcutName)
    {
        try
        {
            var shortcutPaths = new[]
            {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Microsoft",
                    "Windows",
                    "Start Menu",
                    "Programs",
                    "Steam",
                    shortcutName + ".url"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    shortcutName + ".url"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                    shortcutName + ".url")
            };

            foreach (var shortcutPath in shortcutPaths)
            {
                if (!File.Exists(shortcutPath))
                    continue;

                foreach (var line in File.ReadAllLines(shortcutPath))
                {
                    if (!line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var iconPath = line.Substring("IconFile=".Length).Trim();
                    if (!File.Exists(iconPath))
                        return null;

                    using var icon = new Icon(iconPath, new Size(18, 18));
                    return icon.ToBitmap();
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private static void DrawWindowsIcon(Graphics g)
    {
        using var brush = new SolidBrush(Color.FromArgb(90, 180, 255));
        g.FillRectangle(brush, 2, 2, 6, 6);
        g.FillRectangle(brush, 10, 2, 6, 6);
        g.FillRectangle(brush, 2, 10, 6, 6);
        g.FillRectangle(brush, 10, 10, 6, 6);
    }

    private static void DrawDotaIcon(Graphics g)
    {
        var state = g.Save();
        g.TranslateTransform(9, 9);
        g.RotateTransform(-10);
        g.TranslateTransform(-9, -9);

        using var redBrush = new SolidBrush(Color.FromArgb(170, 34, 34));
        using var blackBrush = new SolidBrush(Color.FromArgb(26, 18, 18));

        g.FillRectangle(redBrush, 2, 2, 14, 14);

        using var cut1 = new GraphicsPath();
        cut1.AddPolygon(new[]
        {
            new Point(4, 11),
            new Point(10, 4),
            new Point(12, 5),
            new Point(6, 12)
        });
        g.FillPath(blackBrush, cut1);

        g.FillPolygon(blackBrush, new[]
        {
            new Point(2, 2),
            new Point(6, 2),
            new Point(2, 6)
        });

        g.FillPolygon(blackBrush, new[]
        {
            new Point(16, 12),
            new Point(16, 16),
            new Point(12, 16)
        });

        g.Restore(state);
    }

    private static void DrawScpIcon(Graphics g)
    {
        using var backgroundBrush = new SolidBrush(Color.FromArgb(28, 28, 32));
        using var borderPen = new Pen(Color.FromArgb(230, 230, 230), 1.2F);
        using var linePen = new Pen(Color.FromArgb(230, 230, 230), 1.5F);
        using var accentPen = new Pen(Color.FromArgb(50, 155, 255), 1.2F);

        g.FillRectangle(backgroundBrush, 2.5F, 2.5F, 13.0F, 13.0F);
        g.DrawRectangle(borderPen, 2.5F, 2.5F, 13.0F, 13.0F);
        g.DrawEllipse(borderPen, 5.0F, 5.0F, 8.0F, 8.0F);
        g.DrawLine(linePen, 4.0F, 9.0F, 7.2F, 9.0F);
        g.DrawLine(linePen, 10.8F, 9.0F, 14.0F, 9.0F);
        g.DrawLine(linePen, 9.0F, 4.0F, 9.0F, 7.2F);
        g.DrawLine(linePen, 9.0F, 10.8F, 9.0F, 14.0F);
        g.DrawArc(accentPen, 3.5F, 3.5F, 11.0F, 11.0F, 210, 55);
        g.DrawArc(accentPen, 3.5F, 3.5F, 11.0F, 11.0F, 25, 55);
    }

    private static void DrawToolsIcon(Graphics g)
    {
        using var bodyBrush = new SolidBrush(Color.FromArgb(255, 190, 70));
        using var lidBrush = new SolidBrush(Color.FromArgb(255, 214, 102));
        using var darkBrush = new SolidBrush(Color.FromArgb(138, 88, 28));
        using var borderPen = new Pen(Color.FromArgb(115, 70, 18), 1F);

        g.FillRectangle(bodyBrush, 2.5F, 6.5F, 13.0F, 8.0F);
        g.FillRectangle(lidBrush, 4.5F, 4.5F, 9.0F, 3.0F);
        g.FillRectangle(darkBrush, 7.2F, 3.2F, 3.6F, 1.5F);
        g.FillRectangle(darkBrush, 7.1F, 9.2F, 3.8F, 1.7F);
        g.DrawRectangle(borderPen, 2.5F, 6.5F, 13.0F, 8.0F);
        g.DrawRectangle(borderPen, 4.5F, 4.5F, 9.0F, 3.0F);
    }

    private static void DrawCrosshairIcon(Graphics g)
    {
        using var outerPen = new Pen(Color.FromArgb(220, 220, 220), 1.3F);
        using var accentPen = new Pen(Color.FromArgb(76, 176, 255), 1.6F);
        using var dotBrush = new SolidBrush(Color.FromArgb(76, 176, 255));

        g.DrawEllipse(outerPen, 3.0F, 3.0F, 12.0F, 12.0F);
        g.DrawLine(accentPen, 1.5F, 9.0F, 6.2F, 9.0F);
        g.DrawLine(accentPen, 11.8F, 9.0F, 16.5F, 9.0F);
        g.DrawLine(accentPen, 9.0F, 1.5F, 9.0F, 6.2F);
        g.DrawLine(accentPen, 9.0F, 11.8F, 9.0F, 16.5F);
        g.FillEllipse(dotBrush, 7.6F, 7.6F, 2.8F, 2.8F);
    }

    private static void DrawFunctionsIcon(Graphics g)
    {
        using var screenBrush = new SolidBrush(Color.FromArgb(45, 50, 56));
        using var borderPen = new Pen(Color.FromArgb(210, 216, 222), 1.2F);
        using var accentPen = new Pen(Color.FromArgb(76, 176, 255), 1.5F);

        g.FillRectangle(screenBrush, 2.5F, 3.5F, 13.0F, 9.0F);
        g.DrawRectangle(borderPen, 2.5F, 3.5F, 13.0F, 9.0F);
        g.DrawLine(borderPen, 7.0F, 13.5F, 11.0F, 13.5F);
        g.DrawLine(borderPen, 9.0F, 12.5F, 9.0F, 15.0F);
        g.DrawArc(accentPen, 6.0F, 5.6F, 6.0F, 5.6F, 35, 290);
        g.DrawLine(accentPen, 9.0F, 5.2F, 9.0F, 8.2F);
    }

    private static void DrawSettingsIcon(Graphics g)
    {
        using var panelBrush = new SolidBrush(Color.FromArgb(49, 174, 255));
        using var panelLightBrush = new SolidBrush(Color.FromArgb(92, 206, 255));
        using var sliderBrush = new SolidBrush(Color.White);
        using var borderPen = new Pen(Color.FromArgb(16, 112, 190), 1F);

        g.FillRectangle(panelBrush, 2.5F, 2.5F, 13.0F, 13.0F);
        g.DrawRectangle(borderPen, 2.5F, 2.5F, 13.0F, 13.0F);
        g.FillRectangle(panelLightBrush, 4.0F, 4.0F, 10.0F, 1.6F);
        g.FillRectangle(sliderBrush, 5.0F, 7.0F, 8.0F, 1.4F);
        g.FillRectangle(sliderBrush, 5.0F, 10.0F, 8.0F, 1.4F);
        g.FillRectangle(sliderBrush, 5.0F, 13.0F, 8.0F, 1.4F);
        g.FillEllipse(panelLightBrush, 6.0F, 6.2F, 2.3F, 2.3F);
        g.FillEllipse(panelLightBrush, 10.2F, 9.2F, 2.3F, 2.3F);
        g.FillEllipse(panelLightBrush, 7.8F, 12.2F, 2.3F, 2.3F);
    }

    private static void DrawGearIcon(Graphics g)
    {
        using var shadowBrush = new SolidBrush(Color.FromArgb(80, 8, 12, 24));
        using var gearBrush = new SolidBrush(Color.FromArgb(196, 204, 212));
        using var innerRingBrush = new SolidBrush(Color.FromArgb(34, 76, 112));
        using var centerBrush = new SolidBrush(Color.FromArgb(76, 176, 255));

        FillGearShape(g, shadowBrush, 9.7F, 9.8F);
        FillGearShape(g, gearBrush, 9.0F, 9.0F);
        g.FillEllipse(innerRingBrush, 5.3F, 5.3F, 7.4F, 7.4F);
        g.FillEllipse(centerBrush, 6.6F, 6.6F, 4.8F, 4.8F);
    }

    private static void FillGearShape(Graphics g, Brush brush, float centerX, float centerY)
    {
        for (var i = 0; i < 8; i++)
        {
            var state = g.Save();
            g.TranslateTransform(centerX, centerY);
            g.RotateTransform(i * 45F);
            g.FillRectangle(brush, -1.6F, -8.0F, 3.2F, 4.6F);
            g.Restore(state);
        }

        g.FillEllipse(brush, centerX - 6.3F, centerY - 6.3F, 12.6F, 12.6F);
    }

    private static void DrawGuideIcon(Graphics g)
    {
        // Открытая книга/руководство.
        using var pageBrush = new SolidBrush(Color.FromArgb(230, 230, 230));
        using var linePen = new Pen(Color.FromArgb(120, 130, 140), 1F);
        using var spinePen = new Pen(Color.FromArgb(76, 176, 255), 1.4F);

        g.FillPolygon(pageBrush, new[] { new PointF(2.5F, 4F), new PointF(8.5F, 5.5F), new PointF(8.5F, 15F), new PointF(2.5F, 13.5F) });
        g.FillPolygon(pageBrush, new[] { new PointF(15.5F, 4F), new PointF(9.5F, 5.5F), new PointF(9.5F, 15F), new PointF(15.5F, 13.5F) });
        g.DrawLine(spinePen, 9F, 5.4F, 9F, 15F);
        g.DrawLine(linePen, 4F, 7F, 7.5F, 7.8F);
        g.DrawLine(linePen, 4F, 9F, 7.5F, 9.8F);
        g.DrawLine(linePen, 10.5F, 7.8F, 14F, 7F);
        g.DrawLine(linePen, 10.5F, 9.8F, 14F, 9F);
    }

    private static void DrawDotIcon(Graphics g)
    {
        using var brush = new SolidBrush(AccentBlue);
        g.FillEllipse(brush, 5, 5, 8, 8);
    }
}
