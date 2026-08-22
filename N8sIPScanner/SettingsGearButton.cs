using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace N8sIPScanner;

public sealed class SettingsGearButton : Button
{
    private bool _hovering;
    private bool _pressed;

    public SettingsGearButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        TabStop = false;
        Text = "";
        UseVisualStyleBackColor = false;
    }

    protected override void OnMouseEnter(System.EventArgs e)
    {
        _hovering = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(System.EventArgs e)
    {
        _hovering = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        _pressed = true;
        Invalidate();
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var background = Parent?.BackColor ?? UiTheme.Background;
        using var backgroundBrush = new SolidBrush(background);
        g.FillRectangle(backgroundBrush, ClientRectangle);

        if (_pressed || _hovering)
        {
            using var hoverBrush = new SolidBrush(_pressed ? UiTheme.Border : UiTheme.PanelAlt);
            var hoverRect = new Rectangle(1, 1, Width - 2, Height - 2);
            using var path = RoundedRect(hoverRect, 6);
            g.FillPath(hoverBrush, path);
        }

        DrawCenteredGear(g);
    }

    private void DrawCenteredGear(Graphics g)
    {
        var size = Math.Min(Width, Height);
        var center = new PointF(Width / 2f, Height / 2f);

        var outerRadius = size * 0.30f;
        var innerRadius = size * 0.13f;
        var toothLength = size * 0.085f;

        using var pen = new Pen(UiTheme.MutedText, Math.Max(2f, size * 0.065f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        using var thinPen = new Pen(UiTheme.MutedText, Math.Max(1.5f, size * 0.045f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        // Teeth.
        for (var i = 0; i < 8; i++)
        {
            var angle = i * Math.PI / 4.0;
            var x1 = center.X + (float)(Math.Cos(angle) * outerRadius);
            var y1 = center.Y + (float)(Math.Sin(angle) * outerRadius);
            var x2 = center.X + (float)(Math.Cos(angle) * (outerRadius + toothLength));
            var y2 = center.Y + (float)(Math.Sin(angle) * (outerRadius + toothLength));
            g.DrawLine(pen, x1, y1, x2, y2);
        }

        // Outer ring.
        var outer = new RectangleF(
            center.X - outerRadius,
            center.Y - outerRadius,
            outerRadius * 2,
            outerRadius * 2);

        g.DrawEllipse(pen, outer);

        // Inner ring.
        var inner = new RectangleF(
            center.X - innerRadius,
            center.Y - innerRadius,
            innerRadius * 2,
            innerRadius * 2);

        g.DrawEllipse(thinPen, inner);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;

        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }
}
