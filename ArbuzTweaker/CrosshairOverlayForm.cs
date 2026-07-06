using System.Drawing.Drawing2D;

namespace ArbuzTweaker;

internal enum CrosshairShape
{
    Classic,
    Cross,
    Dot,
    Circle,
    CircleCross,
    Corners,
    TShape
}

internal sealed record CrosshairSettings(
    CrosshairShape Shape,
    int Size,
    int Gap,
    int Thickness,
    int OpacityPercent,
    Color Color,
    Color OutlineColor,
    bool ShowCenterDot,
    bool ShowOutline);

internal sealed record CrosshairCenterCheckResult(bool IsOk, string Message);

internal sealed class CrosshairOverlayForm : Form
{
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExLayered = 0x00080000;
    private const int WsExNoActivate = 0x08000000;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    private static readonly Color TransparentColor = Color.FromArgb(255, 0, 255);
    private static readonly IntPtr HwndTopMost = new(-1);
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
        Opacity = Math.Clamp(settings.OpacityPercent, 0, 100) / 100D;
        KeepOnTop();
        Invalidate();
    }

    public void KeepOnTop()
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        var virtualScreen = SystemInformation.VirtualScreen;
        if (!Bounds.Equals(virtualScreen))
            Bounds = virtualScreen;

        TopMost = true;
        SetWindowPos(Handle, HwndTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
    }

    public CrosshairCenterCheckResult CheckCenter()
    {
        var targetScreen = Screen.PrimaryScreen ?? Screen.AllScreens.FirstOrDefault();
        if (targetScreen == null)
            return new CrosshairCenterCheckResult(false, "Не удалось определить основной экран.");

        var virtualScreen = SystemInformation.VirtualScreen;
        var expectedX = targetScreen.Bounds.Left + targetScreen.Bounds.Width / 2;
        var expectedY = targetScreen.Bounds.Top + targetScreen.Bounds.Height / 2;
        var drawX = Bounds.Left + (targetScreen.Bounds.Left + targetScreen.Bounds.Width / 2 - Bounds.Left);
        var drawY = Bounds.Top + (targetScreen.Bounds.Top + targetScreen.Bounds.Height / 2 - Bounds.Top);
        var boundsOk = Bounds.Equals(virtualScreen);
        var centerOk = Math.Abs(drawX - expectedX) <= 1 && Math.Abs(drawY - expectedY) <= 1;
        var visibleOk = Visible;

        if (boundsOk && centerOk && visibleOk)
            return new CrosshairCenterCheckResult(true, $"Центр проверен: {expectedX}x{expectedY}, основной экран.");

        return new CrosshairCenterCheckResult(
            false,
            $"Проверка центра не пройдена. Оверлей: {Bounds}, виртуальный экран: {virtualScreen}, расчётный центр: {drawX}x{drawY}.");
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
        var dotSize = Math.Max(2, thickness + 1);

        // TransparencyKey removes only exact transparent pixels. Anti-aliasing blends
        // edges with that key color and creates a fake colored outline around shapes.
        e.Graphics.SmoothingMode = SmoothingMode.None;
        if (_settings.ShowOutline)
        {
            var outlineSize = _settings.Shape == CrosshairShape.Dot
                ? size + Math.Max(2, thickness + 2)
                : size;
            using var outlinePen = new Pen(_settings.OutlineColor, thickness + 3)
            {
                StartCap = LineCap.Square,
                EndCap = LineCap.Square
            };
            using var outlineBrush = new SolidBrush(_settings.OutlineColor);
            DrawShape(e.Graphics, outlinePen, outlineBrush, centerX, centerY, outlineSize, gap, Math.Max(2, dotSize + 2), forceCenterDot: false);
        }

        using var pen = new Pen(_settings.Color, thickness)
        {
            StartCap = LineCap.Square,
            EndCap = LineCap.Square
        };
        using var dotBrush = new SolidBrush(_settings.Color);
        DrawShape(e.Graphics, pen, dotBrush, centerX, centerY, size, gap, dotSize, forceCenterDot: _settings.ShowCenterDot);
    }

    private void DrawShape(
        Graphics graphics,
        Pen pen,
        Brush dotBrush,
        int centerX,
        int centerY,
        int size,
        int gap,
        int dotSize,
        bool forceCenterDot)
    {
        switch (_settings.Shape)
        {
            case CrosshairShape.Cross:
                DrawCross(graphics, pen, centerX, centerY, size);
                break;
            case CrosshairShape.Dot:
                DrawCenterDot(graphics, dotBrush, centerX, centerY, Math.Max(2, size));
                break;
            case CrosshairShape.Circle:
                DrawCircle(graphics, pen, centerX, centerY, size);
                if (forceCenterDot)
                    DrawCenterDot(graphics, dotBrush, centerX, centerY, dotSize);
                break;
            case CrosshairShape.CircleCross:
                DrawCircle(graphics, pen, centerX, centerY, size + gap);
                DrawClassic(graphics, pen, dotBrush, centerX, centerY, size, gap, dotSize, forceCenterDot);
                break;
            case CrosshairShape.Corners:
                DrawCorners(graphics, pen, centerX, centerY, size, gap);
                if (forceCenterDot)
                    DrawCenterDot(graphics, dotBrush, centerX, centerY, dotSize);
                break;
            case CrosshairShape.TShape:
                DrawTShape(graphics, pen, dotBrush, centerX, centerY, size, gap, dotSize, forceCenterDot);
                break;
            default:
                DrawClassic(graphics, pen, dotBrush, centerX, centerY, size, gap, dotSize, forceCenterDot);
                break;
        }
    }

    private static void DrawClassic(
        Graphics graphics,
        Pen pen,
        Brush dotBrush,
        int centerX,
        int centerY,
        int size,
        int gap,
        int dotSize,
        bool drawDot)
    {
        graphics.DrawLine(pen, centerX - gap - size, centerY, centerX - gap, centerY);
        graphics.DrawLine(pen, centerX + gap, centerY, centerX + gap + size, centerY);
        graphics.DrawLine(pen, centerX, centerY - gap - size, centerX, centerY - gap);
        graphics.DrawLine(pen, centerX, centerY + gap, centerX, centerY + gap + size);

        if (drawDot)
            DrawCenterDot(graphics, dotBrush, centerX, centerY, dotSize);
    }

    private static void DrawCross(Graphics graphics, Pen pen, int centerX, int centerY, int radius)
    {
        graphics.DrawLine(pen, centerX - radius, centerY, centerX + radius, centerY);
        graphics.DrawLine(pen, centerX, centerY - radius, centerX, centerY + radius);
    }

    private static void DrawCircle(Graphics graphics, Pen pen, int centerX, int centerY, int radius)
    {
        radius = Math.Max(4, radius);
        graphics.DrawEllipse(pen, centerX - radius, centerY - radius, radius * 2, radius * 2);
    }

    private static void DrawCorners(Graphics graphics, Pen pen, int centerX, int centerY, int size, int gap)
    {
        var outer = Math.Max(size + gap, 8);
        var inner = Math.Max(gap, 2);

        graphics.DrawLine(pen, centerX - outer, centerY - outer, centerX - inner, centerY - outer);
        graphics.DrawLine(pen, centerX - outer, centerY - outer, centerX - outer, centerY - inner);

        graphics.DrawLine(pen, centerX + inner, centerY - outer, centerX + outer, centerY - outer);
        graphics.DrawLine(pen, centerX + outer, centerY - outer, centerX + outer, centerY - inner);

        graphics.DrawLine(pen, centerX - outer, centerY + outer, centerX - inner, centerY + outer);
        graphics.DrawLine(pen, centerX - outer, centerY + inner, centerX - outer, centerY + outer);

        graphics.DrawLine(pen, centerX + inner, centerY + outer, centerX + outer, centerY + outer);
        graphics.DrawLine(pen, centerX + outer, centerY + inner, centerX + outer, centerY + outer);
    }

    private static void DrawTShape(
        Graphics graphics,
        Pen pen,
        Brush dotBrush,
        int centerX,
        int centerY,
        int size,
        int gap,
        int dotSize,
        bool drawDot)
    {
        graphics.DrawLine(pen, centerX - gap - size, centerY, centerX - gap, centerY);
        graphics.DrawLine(pen, centerX + gap, centerY, centerX + gap + size, centerY);
        graphics.DrawLine(pen, centerX, centerY + gap, centerX, centerY + gap + size);
        if (drawDot)
            DrawCenterDot(graphics, dotBrush, centerX, centerY, dotSize);
    }

    private static void DrawCenterDot(Graphics graphics, Brush brush, int centerX, int centerY, int dotSize)
    {
        dotSize = Math.Max(2, dotSize);
        graphics.FillEllipse(brush, centerX - dotSize / 2F, centerY - dotSize / 2F, dotSize, dotSize);
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

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);
}
