using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
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
            var shortcutPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft",
                "Windows",
                "Start Menu",
                "Programs",
                "Steam",
                shortcutName + ".url");

            if (!File.Exists(shortcutPath))
                return null;

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
        using var darkBrush = new SolidBrush(Color.FromArgb(28, 28, 28));
        using var outlinePen = new Pen(Color.FromArgb(220, 228, 235), 1.2F);
        using var symbolPen = new Pen(Color.FromArgb(220, 228, 235), 1.3F);
        using var font = new Font("Segoe UI", 4.6F, FontStyle.Bold, GraphicsUnit.Point);
        using var textBrush = new SolidBrush(Color.FromArgb(235, 240, 245));

        g.FillRectangle(darkBrush, 1, 1, 16, 16);
        g.DrawRectangle(outlinePen, 1, 1, 16, 16);
        g.DrawString("SCP", font, textBrush, new PointF(2.1F, 1.1F));

        g.DrawEllipse(symbolPen, 4.5F, 6.0F, 9.0F, 9.0F);
        g.DrawLine(symbolPen, 9.0F, 6.0F, 9.0F, 9.4F);
        g.DrawLine(symbolPen, 4.9F, 10.5F, 13.1F, 10.5F);
        g.DrawArc(symbolPen, 5.5F, 6.8F, 7.0F, 7.0F, 218, 86);
        g.DrawArc(symbolPen, 5.5F, 6.8F, 7.0F, 7.0F, 338, 86);
        g.DrawArc(symbolPen, 5.5F, 6.8F, 7.0F, 7.0F, 98, 86);
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
        using var shadowBrush = new SolidBrush(Color.FromArgb(90, 8, 12, 24));
        using var iconBrush = new SolidBrush(Color.FromArgb(210, 236, 255));
        using var font = new Font("Segoe MDL2 Assets", 15.5F, FontStyle.Regular, GraphicsUnit.Pixel);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        var glyph = char.ConvertFromUtf32(0xE713);
        g.DrawString(glyph, font, shadowBrush, new RectangleF(1.0F, 1.0F, 18.0F, 18.0F), format);
        g.DrawString(glyph, font, iconBrush, new RectangleF(0.0F, 0.0F, 18.0F, 18.0F), format);
    }

    private static void DrawDotIcon(Graphics g)
    {
        using var brush = new SolidBrush(AccentBlue);
        g.FillEllipse(brush, 5, 5, 8, 8);
    }
}
