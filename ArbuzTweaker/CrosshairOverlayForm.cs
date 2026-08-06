using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

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
    private const int WsExNoActivate = 0x08000000;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    private static readonly IntPtr HwndTopMost = new(-1);
    private CrosshairSettings _settings;
    private Rectangle _targetScreenBounds;
    private bool _layoutDirty = true;

    public CrosshairOverlayForm(CrosshairSettings settings)
    {
        _settings = settings;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Color.Black;
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
            createParams.ExStyle |= WsExTransparent | WsExToolWindow | WsExNoActivate;
            return createParams;
        }
    }

    public void UpdateSettings(CrosshairSettings settings)
    {
        _settings = settings;
        Opacity = Math.Clamp(settings.OpacityPercent, 0, 100) / 100D;
        _layoutDirty = true;
        KeepOnTop();
        Invalidate();
    }

    public void KeepOnTop()
    {
        if (IsDisposed)
            return;

        var targetScreen = ResolveTargetScreenBounds();
        var extent = CalculateExtent(_settings);
        var width = extent * 2;
        var height = extent * 2;
        var x = targetScreen.Left + targetScreen.Width / 2 - extent;
        var y = targetScreen.Top + targetScreen.Height / 2 - extent;
        var desiredBounds = new Rectangle(x, y, width, height);

        if (!Bounds.Equals(desiredBounds))
        {
            Bounds = desiredBounds;
            _layoutDirty = true;
        }

        if (_layoutDirty)
            RebuildWindowRegion();

        if (IsHandleCreated)
        {
            // SWP_SHOWWINDOW только для видимого оверлея: иначе любое обновление
            // настроек показывало скрытое окно в обход WinForms.
            SetWindowPos(
                Handle,
                HwndTopMost,
                desiredBounds.X,
                desiredBounds.Y,
                desiredBounds.Width,
                desiredBounds.Height,
                SwpNoActivate | (Visible ? SwpShowWindow : 0));
        }

        _targetScreenBounds = targetScreen;
    }

    public CrosshairCenterCheckResult CheckCenter()
    {
        KeepOnTop();

        if (Screen.AllScreens.Length == 0)
            return new CrosshairCenterCheckResult(false, "Не удалось определить экран.");

        var expectedX = _targetScreenBounds.Left + _targetScreenBounds.Width / 2;
        var expectedY = _targetScreenBounds.Top + _targetScreenBounds.Height / 2;
        var drawX = Bounds.Left + ClientSize.Width / 2;
        var drawY = Bounds.Top + ClientSize.Height / 2;
        var centerOk = Math.Abs(drawX - expectedX) <= 1 && Math.Abs(drawY - expectedY) <= 1;
        var visibleOk = Visible;

        if (centerOk && visibleOk)
            return new CrosshairCenterCheckResult(true, $"Центр проверен: {expectedX}x{expectedY}, экран {FormatRectangle(_targetScreenBounds)}.");

        return new CrosshairCenterCheckResult(
            false,
            $"Проверка центра не пройдена. Оверлей: {FormatRectangle(Bounds)}, экран: {FormatRectangle(_targetScreenBounds)}, расчётный центр: {drawX}x{drawY}.");
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // The window region already clips the form to the crosshair shape.
        // Filling the clipped region avoids old pixels after a settings change.
        e.Graphics.Clear(_settings.ShowOutline ? _settings.OutlineColor : _settings.Color);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var centerX = ClientSize.Width / 2;
        var centerY = ClientSize.Height / 2;
        var size = Math.Max(4, _settings.Size);
        var gap = Math.Max(0, _settings.Gap);
        var thickness = Math.Max(1, _settings.Thickness);
        var dotSize = Math.Max(2, thickness + 1);

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

    private Rectangle ResolveTargetScreenBounds()
    {
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow != IntPtr.Zero && (!IsHandleCreated || foregroundWindow != Handle))
        {
            GetWindowThreadProcessId(foregroundWindow, out var processId);
            if (processId != (uint)Environment.ProcessId)
                return Screen.FromHandle(foregroundWindow).Bounds;

            // Окна самого твикера (настройки, диалоги) не считаются целевыми:
            // иначе на мультимониторе прицел прыгал за окном настроек.
            if (!_targetScreenBounds.IsEmpty)
                return _targetScreenBounds;
        }

        return Screen.FromPoint(Cursor.Position).Bounds;
    }

    private void RebuildWindowRegion()
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
            return;

        var centerX = ClientSize.Width / 2;
        var centerY = ClientSize.Height / 2;
        using var path = BuildRegionPath(centerX, centerY, _settings);
        var previousRegion = Region;
        Region = new Region(path);
        previousRegion?.Dispose();
        _layoutDirty = false;
    }

    private static int CalculateExtent(CrosshairSettings settings)
    {
        var size = Math.Max(4, settings.Size);
        var gap = Math.Max(0, settings.Gap);
        var thickness = Math.Max(1, settings.Thickness);
        var outlineExtra = settings.ShowOutline ? thickness + 6 : thickness + 3;

        var radius = settings.Shape switch
        {
            CrosshairShape.Dot => size + outlineExtra,
            CrosshairShape.Circle => size + outlineExtra,
            CrosshairShape.CircleCross => size + gap + outlineExtra,
            CrosshairShape.Corners => size + gap + outlineExtra,
            CrosshairShape.TShape => size + gap + outlineExtra,
            CrosshairShape.Cross => size + outlineExtra,
            _ => size + gap + outlineExtra
        };

        return Math.Max(24, radius + 4);
    }

    private static GraphicsPath BuildRegionPath(int centerX, int centerY, CrosshairSettings settings)
    {
        var path = new GraphicsPath();
        var size = Math.Max(4, settings.Size);
        var gap = Math.Max(0, settings.Gap);
        var thickness = Math.Max(1, settings.Thickness);
        var dotSize = Math.Max(2, thickness + 1);
        // Регион и есть видимая фигура (он целиком заливается в OnPaintBackground),
        // поэтому его толщина должна совпадать с настройкой; +4 при обводке — кольцо
        // цвета обводки по 2 пикселя с каждой стороны основной линии.
        var regionThickness = Math.Max(1, thickness + (settings.ShowOutline ? 4 : 0));

        AddShapePath(path, settings.Shape, centerX, centerY, size, gap, dotSize, settings.ShowCenterDot);

        if (settings.ShowOutline)
        {
            var outlineSize = settings.Shape == CrosshairShape.Dot
                ? size + Math.Max(2, thickness + 2)
                : size;
            AddShapePath(path, settings.Shape, centerX, centerY, outlineSize, gap, Math.Max(2, dotSize + 2), forceCenterDot: false);
        }

        using var widenPen = new Pen(Color.Black, regionThickness)
        {
            StartCap = LineCap.Square,
            EndCap = LineCap.Square,
            LineJoin = LineJoin.Miter
        };

        try
        {
            path.Widen(widenPen);
        }
        catch
        {
            path.Reset();
            path.AddEllipse(centerX - 6, centerY - 6, 12, 12);
        }

        AddFilledDotRegions(path, centerX, centerY, settings, size, thickness, dotSize);
        return path;
    }

    private static void AddFilledDotRegions(
        GraphicsPath path,
        int centerX,
        int centerY,
        CrosshairSettings settings,
        int size,
        int thickness,
        int dotSize)
    {
        if (settings.Shape == CrosshairShape.Dot)
        {
            var visibleDotSize = settings.ShowOutline
                ? size + Math.Max(2, thickness + 2)
                : size;
            AddDotPath(path, centerX, centerY, Math.Max(2, visibleDotSize));
            return;
        }

        if (settings.ShowCenterDot && settings.Shape is CrosshairShape.Classic or CrosshairShape.Circle or CrosshairShape.CircleCross or CrosshairShape.Corners or CrosshairShape.TShape)
            AddDotPath(path, centerX, centerY, Math.Max(2, dotSize));
    }

    private static void AddShapePath(
        GraphicsPath path,
        CrosshairShape shape,
        int centerX,
        int centerY,
        int size,
        int gap,
        int dotSize,
        bool forceCenterDot)
    {
        switch (shape)
        {
            case CrosshairShape.Cross:
                AddCrossPath(path, centerX, centerY, size);
                break;
            case CrosshairShape.Dot:
                AddDotPath(path, centerX, centerY, Math.Max(2, size));
                break;
            case CrosshairShape.Circle:
                AddCirclePath(path, centerX, centerY, size);
                if (forceCenterDot)
                    AddDotPath(path, centerX, centerY, dotSize);
                break;
            case CrosshairShape.CircleCross:
                AddCirclePath(path, centerX, centerY, size + gap);
                AddClassicPath(path, centerX, centerY, size, gap);
                if (forceCenterDot)
                    AddDotPath(path, centerX, centerY, dotSize);
                break;
            case CrosshairShape.Corners:
                AddCornersPath(path, centerX, centerY, size, gap);
                if (forceCenterDot)
                    AddDotPath(path, centerX, centerY, dotSize);
                break;
            case CrosshairShape.TShape:
                AddTShapePath(path, centerX, centerY, size, gap);
                if (forceCenterDot)
                    AddDotPath(path, centerX, centerY, dotSize);
                break;
            default:
                AddClassicPath(path, centerX, centerY, size, gap);
                if (forceCenterDot)
                    AddDotPath(path, centerX, centerY, dotSize);
                break;
        }
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
        DrawShapeCore(graphics, pen, dotBrush, centerX, centerY, size, gap, dotSize, forceCenterDot);
    }

    private void DrawShapeCore(
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

    private static void AddClassicPath(GraphicsPath path, int centerX, int centerY, int size, int gap)
    {
        path.StartFigure();
        path.AddLine(centerX - gap - size, centerY, centerX - gap, centerY);
        path.StartFigure();
        path.AddLine(centerX + gap, centerY, centerX + gap + size, centerY);
        path.StartFigure();
        path.AddLine(centerX, centerY - gap - size, centerX, centerY - gap);
        path.StartFigure();
        path.AddLine(centerX, centerY + gap, centerX, centerY + gap + size);
    }

    private static void AddCrossPath(GraphicsPath path, int centerX, int centerY, int radius)
    {
        path.StartFigure();
        path.AddLine(centerX - radius, centerY, centerX + radius, centerY);
        path.StartFigure();
        path.AddLine(centerX, centerY - radius, centerX, centerY + radius);
    }

    private static void AddCirclePath(GraphicsPath path, int centerX, int centerY, int radius)
    {
        radius = Math.Max(4, radius);
        path.StartFigure();
        path.AddEllipse(centerX - radius, centerY - radius, radius * 2, radius * 2);
    }

    private static void AddCornersPath(GraphicsPath path, int centerX, int centerY, int size, int gap)
    {
        var outer = Math.Max(size + gap, 8);
        var inner = Math.Max(gap, 2);

        path.StartFigure();
        path.AddLine(centerX - outer, centerY - outer, centerX - inner, centerY - outer);
        path.StartFigure();
        path.AddLine(centerX - outer, centerY - outer, centerX - outer, centerY - inner);

        path.StartFigure();
        path.AddLine(centerX + inner, centerY - outer, centerX + outer, centerY - outer);
        path.StartFigure();
        path.AddLine(centerX + outer, centerY - outer, centerX + outer, centerY - inner);

        path.StartFigure();
        path.AddLine(centerX - outer, centerY + outer, centerX - inner, centerY + outer);
        path.StartFigure();
        path.AddLine(centerX - outer, centerY + inner, centerX - outer, centerY + outer);

        path.StartFigure();
        path.AddLine(centerX + inner, centerY + outer, centerX + outer, centerY + outer);
        path.StartFigure();
        path.AddLine(centerX + outer, centerY + inner, centerX + outer, centerY + outer);
    }

    private static void AddTShapePath(GraphicsPath path, int centerX, int centerY, int size, int gap)
    {
        path.StartFigure();
        path.AddLine(centerX - gap - size, centerY, centerX - gap, centerY);
        path.StartFigure();
        path.AddLine(centerX + gap, centerY, centerX + gap + size, centerY);
        path.StartFigure();
        path.AddLine(centerX, centerY + gap, centerX, centerY + gap + size);
    }

    private static void AddDotPath(GraphicsPath path, int centerX, int centerY, int dotSize)
    {
        dotSize = Math.Max(2, dotSize);
        path.StartFigure();
        path.AddEllipse(centerX - dotSize / 2F, centerY - dotSize / 2F, dotSize, dotSize);
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

    private static string FormatRectangle(Rectangle rectangle)
    {
        return $"{rectangle.X},{rectangle.Y} {rectangle.Width}x{rectangle.Height}";
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);
}
