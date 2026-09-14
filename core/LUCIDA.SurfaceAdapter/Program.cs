using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Lucida.SurfaceAdapter;

internal static class Native
{
    public const int WmHotkey = 0x0312;
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModNoRepeat = 0x4000;
    public const int SwHide = 0;
    public const int SwShow = 5;
    public const uint MouseLeftDown = 0x0002;
    public const uint MouseLeftUp = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, char[] text, int count);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

internal sealed record Mapping(
    string Id,
    string Label,
    RectangleF SourceFraction,
    RectangleF DestinationFraction,
    Color Outline,
    string Status
);

internal sealed class SurfaceForm : Form
{
    private const int PeekHotkey = 1;
    private const int ArmHotkey = 2;
    private const int TargetHotkey = 3;
    private const int QuitHotkey = 4;

    private readonly System.Windows.Forms.Timer refreshTimer;
    private readonly List<Mapping> mappings;
    private Bitmap? snapshot;
    private IntPtr targetWindow;
    private Native.Rect targetRect;
    private bool composed = true;
    private bool inputArmed;
    private Point hoverPoint;
    private string status = "Sin objetivo. Enfoca grandMA3 y presiona Ctrl+Alt+T.";

    public SurfaceForm()
    {
        Text = "LUCIDA Surface Adapter";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1280, 720);
        TopMost = true;
        ShowInTaskbar = false;
        KeyPreview = true;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        mappings = BuildMappings();
        refreshTimer = new System.Windows.Forms.Timer { Interval = 250 };
        refreshTimer.Tick += (_, _) => RefreshSnapshot();
        refreshTimer.Start();

        MouseMove += (_, e) => { hoverPoint = e.Location; Invalidate(); };
        MouseClick += HandleMouseClick;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE: el overlay no roba el foco al host.
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RegisterHotkeys();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Hide();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Native.UnregisterHotKey(Handle, PeekHotkey);
        Native.UnregisterHotKey(Handle, ArmHotkey);
        Native.UnregisterHotKey(Handle, TargetHotkey);
        Native.UnregisterHotKey(Handle, QuitHotkey);
        snapshot?.Dispose();
        refreshTimer.Dispose();
        base.OnFormClosed(e);
    }

    private void RegisterHotkeys()
    {
        var modifiers = Native.ModControl | Native.ModAlt | Native.ModNoRepeat;
        Native.RegisterHotKey(Handle, PeekHotkey, modifiers, (uint)Keys.L);
        Native.RegisterHotKey(Handle, ArmHotkey, modifiers, (uint)Keys.A);
        Native.RegisterHotKey(Handle, TargetHotkey, modifiers, (uint)Keys.T);
        Native.RegisterHotKey(Handle, QuitHotkey, modifiers, (uint)Keys.Q);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WmHotkey)
        {
            switch (m.WParam.ToInt32())
            {
                case PeekHotkey:
                    composed = !composed;
                    status = composed ? "Superficie compuesta: mouse transformado." : "PEEK: interfaz real y mouse nativo.";
                    Invalidate();
                    return;
                case ArmHotkey:
                    inputArmed = !inputArmed;
                    status = inputArmed ? "INPUT ARMADO: los clicks se enviaran al objetivo." : "INPUT DESARMADO: modo inspeccion.";
                    Invalidate();
                    return;
                case TargetHotkey:
                    CaptureForegroundTarget();
                    return;
                case QuitHotkey:
                    Close();
                    return;
            }
        }
        base.WndProc(ref m);
    }

    private void CaptureForegroundTarget()
    {
        var foreground = Native.GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == Handle)
        {
            status = "No se encontro una ventana objetivo.";
            Invalidate();
            return;
        }

        targetWindow = foreground;
        if (!Native.GetWindowRect(targetWindow, out targetRect) || targetRect.Width < 200 || targetRect.Height < 150)
        {
            status = "La ventana objetivo no tiene un rectangulo utilizable.";
            Invalidate();
            return;
        }

        RefreshSnapshot();
        status = $"Objetivo capturado: {WindowTitle(targetWindow)}. Input desarmado.";
        Show();
        Invalidate();
    }

    private void RefreshSnapshot()
    {
        if (targetWindow == IntPtr.Zero || !Native.GetWindowRect(targetWindow, out targetRect))
            return;

        var width = Math.Min(Math.Max(targetRect.Width, 1), 5000);
        var height = Math.Min(Math.Max(targetRect.Height, 1), 4000);
        if (width < 200 || height < 150)
            return;

        var wasVisible = Visible;
        if (wasVisible)
        {
            Hide();
            Thread.Sleep(18);
        }

        try
        {
            using var source = new Bitmap(width, height);
            using (var graphics = Graphics.FromImage(source))
            {
                graphics.CopyFromScreen(targetRect.Left, targetRect.Top, 0, 0, source.Size, CopyPixelOperation.SourceCopy);
            }
            snapshot?.Dispose();
            snapshot = new Bitmap(source);
        }
        finally
        {
            if (wasVisible)
                Show();
        }
        Invalidate();
    }

    private static string WindowTitle(IntPtr hWnd)
    {
        var chars = new char[256];
        var length = Native.GetWindowText(hWnd, chars, chars.Length);
        return length > 0 ? new string(chars, 0, length) : "(sin titulo)";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(Color.FromArgb(8, 12, 18));
        if (snapshot is null)
        {
            DrawEmpty(e.Graphics);
            return;
        }

        if (composed)
            DrawComposed(e.Graphics);
        else
            DrawPeek(e.Graphics);
        DrawStatus(e.Graphics);
    }

    private void DrawEmpty(Graphics graphics)
    {
        using var title = new Font("Segoe UI", 18, FontStyle.Bold);
        using var body = new Font("Segoe UI", 12);
        using var brush = new SolidBrush(Color.White);
        graphics.DrawString("LUCIDA / XANAX Surface Adapter", title, brush, 32, 32);
        graphics.DrawString("Ctrl+Alt+T  capturar ventana objetivo\nCtrl+Alt+L  peek real / superficie compuesta\nCtrl+Alt+A  armar o desarmar input\nCtrl+Alt+Q  salir", body, brush, 34, 78);
    }

    private void DrawComposed(Graphics graphics)
    {
        foreach (var mapping in mappings)
        {
            var source = Fraction(snapshot!.Size, mapping.SourceFraction);
            var destination = Fraction(ClientSize, mapping.DestinationFraction);
            graphics.DrawImage(snapshot, destination, source, GraphicsUnit.Pixel);
            using var outline = new Pen(mapping.Outline, IsHovered(destination) ? 4 : 2);
            graphics.DrawRectangle(outline, Rectangle.Round(destination));
            DrawLabel(graphics, mapping.Label, destination, mapping.Outline);
        }
    }

    private void DrawPeek(Graphics graphics)
    {
        var destination = new Rectangle(18, 54, ClientSize.Width - 36, ClientSize.Height - 100);
        graphics.DrawImage(snapshot!, destination);
        foreach (var mapping in mappings)
        {
            var source = Fraction(snapshot!.Size, mapping.SourceFraction);
            var scaled = new Rectangle(
                destination.Left + source.Left * destination.Width / snapshot.Width,
                destination.Top + source.Top * destination.Height / snapshot.Height,
                source.Width * destination.Width / snapshot.Width,
                source.Height * destination.Height / snapshot.Height);
            using var outline = new Pen(mapping.Outline, IsHovered(scaled) ? 4 : 2);
            graphics.DrawRectangle(outline, scaled);
            DrawLabel(graphics, mapping.Id, scaled, mapping.Outline);
        }
    }

    private void DrawStatus(Graphics graphics)
    {
        using var bar = new SolidBrush(Color.FromArgb(235, 12, 18, 28));
        graphics.FillRectangle(bar, 0, ClientSize.Height - 42, ClientSize.Width, 42);
        using var font = new Font("Segoe UI", 10, FontStyle.Bold);
        using var brush = new SolidBrush(inputArmed ? Color.FromArgb(255, 255, 110, 110) : Color.FromArgb(255, 190, 215, 230));
        var mode = composed ? "COMPOSED" : "PEEK REAL";
        graphics.DrawString($"{mode}  |  {(inputArmed ? "INPUT ARMED" : "INPUT OFF")}  |  {status}", font, brush, 16, ClientSize.Height - 29);
    }

    private static void DrawLabel(Graphics graphics, string text, RectangleF rectangle, Color color)
    {
        var labelRectangle = new RectangleF(rectangle.Left + 8, rectangle.Top + 8, Math.Min(rectangle.Width - 16, 250), 27);
        using var brush = new SolidBrush(Color.FromArgb(225, color));
        using var textBrush = new SolidBrush(Color.FromArgb(255, 5, 13, 20));
        using var font = new Font("Segoe UI", 9, FontStyle.Bold);
        graphics.FillRectangle(brush, labelRectangle);
        graphics.DrawString(text, font, textBrush, labelRectangle.Left + 6, labelRectangle.Top + 5);
    }

    private bool IsHovered(RectangleF rectangle) => rectangle.Contains(hoverPoint);

    private void HandleMouseClick(object? sender, MouseEventArgs e)
    {
        if (snapshot is null || targetWindow == IntPtr.Zero)
        {
            status = "No hay ventana objetivo capturada.";
            Invalidate();
            return;
        }

        if (!composed)
        {
            if (!inputArmed)
            {
                status = "PEEK: click nativo simulado bloqueado; Ctrl+Alt+A para armar.";
                Invalidate();
                return;
            }
            var native = new Point(
                targetRect.Left + e.X * targetRect.Width / Math.Max(ClientSize.Width, 1),
                targetRect.Top + e.Y * targetRect.Height / Math.Max(ClientSize.Height, 1));
            SendClick(native);
            return;
        }

        var mapping = mappings.FirstOrDefault(item => Fraction(ClientSize, item.DestinationFraction).Contains(e.Location));
        if (mapping is null)
        {
            status = "Zona sin mapping: click bloqueado.";
            Invalidate();
            return;
        }

        var destination = Fraction(ClientSize, mapping.DestinationFraction);
        var source = Fraction(snapshot.Size, mapping.SourceFraction);
        var sourceX = source.Left + (e.X - destination.Left) * source.Width / Math.Max(destination.Width, 1);
        var sourceY = source.Top + (e.Y - destination.Top) * source.Height / Math.Max(destination.Height, 1);
        var nativePoint = new Point(targetRect.Left + sourceX, targetRect.Top + sourceY);

        if (!inputArmed)
        {
            status = $"{mapping.Id}: target ({nativePoint.X},{nativePoint.Y}) calculado; input desarmado.";
            Invalidate();
            return;
        }

        SendClick(nativePoint);
    }

    private void SendClick(Point point)
    {
        Hide();
        Native.SetForegroundWindow(targetWindow);
        Native.SetCursorPos(point.X, point.Y);
        Native.mouse_event(Native.MouseLeftDown, 0, 0, 0, UIntPtr.Zero);
        Native.mouse_event(Native.MouseLeftUp, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(30);
        Show();
        RefreshSnapshot();
        status = $"Click enviado a ({point.X},{point.Y}); resultado pendiente de observacion.";
    }

    private static Rectangle Fraction(Size size, RectangleF fraction)
    {
        return new Rectangle(
            (int)(fraction.X * size.Width),
            (int)(fraction.Y * size.Height),
            Math.Max(2, (int)(fraction.Width * size.Width)),
            Math.Max(2, (int)(fraction.Height * size.Height)));
    }

    private static List<Mapping> BuildMappings() => new()
    {
        new("fixture_selection", "FIXTURES / GROUPS", new RectangleF(0.00f, 0.00f, 0.30f, 0.30f), new RectangleF(0.03f, 0.09f, 0.19f, 0.32f), Color.Cyan, "partial"),
        new("workspace", "WORKSPACE", new RectangleF(0.20f, 0.05f, 0.30f, 0.28f), new RectangleF(0.03f, 0.46f, 0.19f, 0.32f), Color.Lime, "partial"),
        new("fixture_sheet", "FIXTURE SHEET", new RectangleF(0.35f, 0.20f, 0.40f, 0.42f), new RectangleF(0.25f, 0.09f, 0.50f, 0.49f), Color.Cyan, "partial"),
        new("attribute_control", "ATTRIBUTE CONTROL", new RectangleF(0.70f, 0.72f, 0.28f, 0.22f), new RectangleF(0.25f, 0.61f, 0.50f, 0.12f), Color.Lime, "partial"),
        new("reusable_value", "PALETTES / PRESETS", new RectangleF(0.66f, 0.00f, 0.34f, 0.25f), new RectangleF(0.78f, 0.09f, 0.19f, 0.32f), Color.Gold, "partial"),
        new("playback_control", "PLAYBACKS", new RectangleF(0.75f, 0.18f, 0.25f, 0.34f), new RectangleF(0.78f, 0.46f, 0.19f, 0.32f), Color.Violet, "partial"),
        new("cue_sequence", "CUE / SEQUENCE", new RectangleF(0.20f, 0.65f, 0.60f, 0.35f), new RectangleF(0.25f, 0.76f, 0.72f, 0.16f), Color.HotPink, "partial"),
    };
}

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new SurfaceForm());
    }
}
