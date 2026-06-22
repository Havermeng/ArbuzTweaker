using System.Drawing.Drawing2D;

namespace ArbuzTweaker;

internal sealed record CrosshairSettings(int Size, int Gap, int Thickness, Color Color);

internal sealed class CrosshairOverlayForm : Form
{
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExLayered = 0x00080000;
    private const int WsExNoActivate = 0x08000000;

    private static readonly Color TransparentColor = Color.FromArgb(255, 0, 255);
    private CrosshairSettings _settings;

    public CrosshairOverlayForm(CrosshairSettings settings)
    {
        _settings = settings;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = TransparentColor;
        TransparencyKey = TransparentColor;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        UpdateSettings(settings);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var createParams = base.CreateParams;
            createParams.ExStyle |= WsExTransparent | WsExToolWindow | WsExLayered | WsExNoActivate;
            return createParams;
        }
    }

    public void UpdateSettings(CrosshairSettings settings)
    {
        _settings = settings;
        Bounds = SystemInformation.VirtualScreen;
        TopMost = true;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(TransparentColor);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var targetScreen = Screen.PrimaryScreen ?? Screen.AllScreens.FirstOrDefault();
        if (targetScreen == null)
            return;

        var centerX = targetScreen.Bounds.Left + targetScreen.Bounds.Width / 2 - Bounds.Left;
        var centerY = targetScreen.Bounds.Top + targetScreen.Bounds.Height / 2 - Bounds.Top;
        var size = Math.Max(4, _settings.Size);
        var gap = Math.Max(0, _settings.Gap);
        var thickness = Math.Max(1, _settings.Thickness);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(_settings.Color, thickness)
        {
            StartCap = LineCap.Square,
            EndCap = LineCap.Square
        };
        using var dotBrush = new SolidBrush(_settings.Color);

        e.Graphics.DrawLine(pen, centerX - gap - size, centerY, centerX - gap, centerY);
        e.Graphics.DrawLine(pen, centerX + gap, centerY, centerX + gap + size, centerY);
        e.Graphics.DrawLine(pen, centerX, centerY - gap - size, centerX, centerY - gap);
        e.Graphics.DrawLine(pen, centerX, centerY + gap, centerX, centerY + gap + size);

        var dotSize = Math.Max(2, thickness + 1);
        e.Graphics.FillEllipse(dotBrush, centerX - dotSize / 2F, centerY - dotSize / 2F, dotSize, dotSize);
    }

    protected override void WndProc(ref Message m)
    {
        const int wmNchittest = 0x0084;
        const int htTransparent = -1;

        if (m.Msg == wmNchittest)
        {
            m.Result = new IntPtr(htTransparent);
            return;
        }

        base.WndProc(ref m);
    }
}
