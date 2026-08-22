using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace N8sIPScanner;

public sealed class AnimatedCobaltProgressBar : Control
{
    private readonly System.Windows.Forms.Timer _animationTimer;
    private double _pulsePhase;

    private int _maximum = 100;
    private int _value;

    public AnimatedCobaltProgressBar()
    {
        DoubleBuffered = true;
        Height = 24;
        BackColor = Color.Gainsboro;

        _animationTimer = new System.Windows.Forms.Timer
        {
            Interval = 45
        };

        _animationTimer.Tick += (_, _) =>
        {
            _pulsePhase += 0.12;
            if (_pulsePhase > Math.PI * 2)
            {
                _pulsePhase = 0;
            }

            Invalidate();
        };
    }

    public int Maximum
    {
        get => _maximum;
        set
        {
            _maximum = Math.Max(1, value);
            if (_value > _maximum)
            {
                _value = _maximum;
            }

            Invalidate();
        }
    }

    public int Value
    {
        get => _value;
        set
        {
            _value = Math.Max(0, Math.Min(Maximum, value));
            Invalidate();
        }
    }

    public bool IsAnimating
    {
        get => _animationTimer.Enabled;
        set
        {
            if (value)
            {
                _animationTimer.Start();
            }
            else
            {
                _animationTimer.Stop();
                _pulsePhase = 0;
            }

            Invalidate();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var outer = ClientRectangle;
        outer.Width -= 1;
        outer.Height -= 1;

        using var backgroundBrush = new SolidBrush(UiTheme.Field);
        using var borderPen = new Pen(UiTheme.Border);

        e.Graphics.FillRectangle(backgroundBrush, outer);
        e.Graphics.DrawRectangle(borderPen, outer);

        if (Maximum <= 0 || Value <= 0)
        {
            return;
        }

        var fill = new Rectangle(2, 2, Width - 5, Height - 5);
        var percent = Math.Max(0, Math.Min(1.0, (double)Value / Maximum));
        fill.Width = (int)Math.Round(fill.Width * percent);

        if (fill.Width <= 0)
        {
            return;
        }

        var cobalt = GetPulseCobalt();
        using var fillBrush = new SolidBrush(cobalt);
        e.Graphics.FillRectangle(fillBrush, fill);

        // Subtle top highlight so the bar has some depth without moving stripes.
        var highlight = new Rectangle(fill.Left, fill.Top, fill.Width, Math.Max(1, fill.Height / 2));
        using var highlightBrush = new SolidBrush(Color.FromArgb(IsAnimating ? 42 : 28, Color.White));
        e.Graphics.FillRectangle(highlightBrush, highlight);
    }

    private Color GetPulseCobalt()
    {
        // Base cobalt: #0047AB.
        const int baseR = 0;
        const int baseG = 71;
        const int baseB = 171;

        if (!IsAnimating)
        {
            return Color.FromArgb(baseR, baseG, baseB);
        }

        // Gently brighten and darken the fill while scanning.
        // Range is intentionally small so it feels alive, not flashy.
        var wave = (Math.Sin(_pulsePhase) + 1.0) / 2.0; // 0 to 1
        var lift = (int)Math.Round(18 + (wave * 28));   // 18 to 46

        var r = Clamp(baseR + lift);
        var g = Clamp(baseG + lift);
        var b = Clamp(baseB + lift);

        return Color.FromArgb(r, g, b);
    }

    private static int Clamp(int value)
    {
        return Math.Max(0, Math.Min(255, value));
    }
}
