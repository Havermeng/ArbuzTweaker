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

    public static void StyleSidebarButton(Button button, bool active)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = AccentBlueHover;
        button.FlatAppearance.MouseDownBackColor = AccentBlue;
        button.BackColor = active ? AccentBlue : Surface;
        button.ForeColor = TextPrimary;
        button.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Regular);
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
        button.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Regular);
        button.Cursor = Cursors.Hand;
    }

    public static void StyleEditorTextBox(TextBox textBox)
    {
        textBox.BackColor = Color.FromArgb(24, 24, 24);
        textBox.ForeColor = TextPrimary;
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.Font = new Font("Consolas", 10);
        textBox.HideSelection = false;
    }

    public static void StyleSearchTextBox(TextBox textBox)
    {
        textBox.BackColor = Color.FromArgb(24, 24, 24);
        textBox.ForeColor = TextPrimary;
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.Font = new Font("Segoe UI", 10);
    }

    public static void StyleListPanel(Panel panel)
    {
        panel.BorderStyle = BorderStyle.None;
        panel.BackColor = Surface;
    }

    public static int AddListSectionHeader(Panel panel, int y, int width, string text)
    {
        var label = new Label
        {
            Text = text,
            Location = new Point(8, y),
            Size = new Size(width, 28),
            AutoSize = false,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Regular),
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
        bool highlighted = false)
    {
        var checkBoxWidth = Math.Clamp((int)(availableWidth * 0.40), 240, 360);
        var descriptionX = checkBoxWidth + 24;
        var descriptionWidth = Math.Max(220, availableWidth - checkBoxWidth - 36);
        var descriptionFont = new Font("Segoe UI", 9.5F);
        var descriptionSize = TextRenderer.MeasureText(
            description,
            descriptionFont,
            new Size(descriptionWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix | TextFormatFlags.Left);
        var rowHeight = Math.Max(42, descriptionSize.Height + 18);

        var rowPanel = new Panel
        {
            Location = new Point(8, y),
            Size = new Size(availableWidth, rowHeight),
            BackColor = highlighted
                ? Color.FromArgb(45, 70, 90)
                : rowIndex % 2 == 0
                    ? Color.FromArgb(38, 38, 38)
                    : Color.FromArgb(32, 32, 32),
            BorderStyle = BorderStyle.FixedSingle
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
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Regular),
            ForeColor = TextPrimary,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };
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
                if (TryLoadSteamShortcutIcon("Dota 2") is Image dotaIcon)
                    return dotaIcon;
                DrawDotaIcon(graphics);
                break;
            case "SCP:SL":
                if (TryLoadSteamShortcutIcon("SCP Secret Laboratory") is Image scpIcon)
                    return scpIcon;
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

    private static void DrawDotIcon(Graphics g)
    {
        using var brush = new SolidBrush(AccentBlue);
        g.FillEllipse(brush, 5, 5, 8, 8);
    }
}
