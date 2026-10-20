using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The floating readout of a chart (P17): a raised card that follows its anchor on its own snappy spring, flips
/// to the other side before it would leave the plot, and fades and scales in (0.96 → 1) when a readout starts.
/// Its content is rebuilt only when the active point changes; drawing a frame allocates nothing.
/// </summary>
internal sealed class ChartTooltip
{
    private enum LineKind
    {
        Title,
        Row,
        Value,
        Note,
        Event
    }

    private struct Line
    {
        public LineKind Kind;
        public string Text;
        public string? Value;
        public Color Swatch;
        public bool Dashed;
        public double Emphasis;
    }

    private readonly Visual _host;
    private readonly List<Line> _lines = new();
    // Position runs on a two-component spring array: retargeting every frame (the anchor glides) keeps velocity
    // and allocates nothing, unlike re-issuing MotionValue animations.
    private readonly ChartSpringArray _position;
    private readonly double[] _target = new double[2];
    private readonly MotionValue<double> _shown;
    private double _minWidth = 140;
    private double _maxWidth = 220;
    private Size _size;
    private int _measuredVersion = -1;
    private bool _dirty = true;
    private bool _visible;

    /// <summary>Snappy spring written as stiffness and damping, so retargeting keeps velocity (Arc's <c>follow</c>).</summary>
    public static readonly SpringSpec Follow = SpringSpec.FromVisual(0.26, 0.12);

    public ChartTooltip(Visual host, Action invalidate)
    {
        _host = host;
        _position = new ChartSpringArray(host);
        _position.Jump(_target);
        _position.Changed += invalidate;
        _shown = new MotionValue<double>(host, 0, MotionVectors.Double);
        _shown.Changed += _ => invalidate();
    }

    public bool IsVisible => _visible;

    public double Opacity => _shown.Value;

    public Point Position => new(_position[0], _position[1]);

    public void Begin(double minWidth, double maxWidth)
    {
        _lines.Clear();
        _minWidth = minWidth;
        _maxWidth = maxWidth;
        _dirty = true;
    }

    public void AddTitle(string text) => _lines.Add(new Line { Kind = LineKind.Title, Text = text });

    /// <summary>A series row: swatch, name and value. <paramref name="emphasis"/>: 1 active, -1 dimmed, 0 normal.</summary>
    public void AddRow(Color swatch, bool dashed, string name, string value, double emphasis = 0)
        => _lines.Add(new Line { Kind = LineKind.Row, Text = name, Value = value, Swatch = swatch, Dashed = dashed, Emphasis = emphasis });

    public void AddValue(string text) => _lines.Add(new Line { Kind = LineKind.Value, Text = text });

    public void AddNote(string text) => _lines.Add(new Line { Kind = LineKind.Note, Text = text });

    public void AddEvent(Color dot, string text) => _lines.Add(new Line { Kind = LineKind.Event, Text = text, Swatch = dot });

    /// <summary>The card size for the current content (padding 10 × 12, 4px between lines).</summary>
    public Size Measure(ChartPalette palette, ChartTextCache texts, double available)
    {
        if (!_dirty && _measuredVersion == palette.Version)
        {
            return _size;
        }
        _dirty = false;
        _measuredVersion = palette.Version;
        var maxWidth = Math.Max(60, Math.Min(_maxWidth, available));
        double width = 0, height = 0;
        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            double w, h;
            switch (line.Kind)
            {
                case LineKind.Row:
                {
                    var name = texts.Get(palette, line.Text, palette.SizeSM, palette.TextSecondary);
                    var value = texts.Get(palette, line.Value ?? string.Empty, palette.SizeSM, palette.Foreground, medium: true);
                    w = 10 + 8 + name.WidthIncludingTrailingWhitespace + 16 + value.WidthIncludingTrailingWhitespace;
                    h = palette.SizeSM * 1.4;
                    break;
                }
                case LineKind.Title:
                    w = texts.Get(palette, line.Text, palette.SizeXS, palette.TextSecondary).WidthIncludingTrailingWhitespace;
                    h = palette.SizeXS * 1.4 + 2;
                    break;
                case LineKind.Value:
                {
                    var natural = texts.Get(palette, line.Text, palette.SizeBase, palette.Foreground, medium: true).WidthIncludingTrailingWhitespace;
                    w = Math.Min(natural, maxWidth - 24);
                    var wrapped = texts.Get(palette, line.Text, palette.SizeBase, palette.Foreground, medium: true, maxWidth: maxWidth - 24, wrap: true);
                    h = natural > maxWidth - 24 ? Math.Max(palette.SizeBase * 1.4, wrapped.Height) : palette.SizeBase * 1.4;
                    break;
                }
                case LineKind.Event:
                    w = 13 + texts.Get(palette, line.Text, palette.SizeXS, palette.Foreground).WidthIncludingTrailingWhitespace;
                    h = palette.SizeXS * 1.4 + 4;
                    break;
                default:
                {
                    var natural = texts.Get(palette, line.Text, palette.SizeXS, palette.TextMuted).WidthIncludingTrailingWhitespace;
                    w = Math.Min(natural, maxWidth - 24);
                    var wrapped = texts.Get(palette, line.Text, palette.SizeXS, palette.TextMuted, maxWidth: maxWidth - 24, wrap: true);
                    h = natural > maxWidth - 24 ? Math.Max(palette.SizeXS * 1.4, wrapped.Height) : palette.SizeXS * 1.4;
                    break;
                }
            }
            width = Math.Max(width, w);
            height += h + (i > 0 ? LineGap : 0);
        }
        _size = new Size(Math.Clamp(width + 24, _minWidth, maxWidth), height + 20);
        return _size;
    }

    /// <summary>Gap between lines (Arc: 4px for multi-series readouts, 2px for BrushChart).</summary>
    public double LineGap { get; set; } = 4;

    /// <summary>Shows or hides the card (fast fade with a 0.96 → 1 scale).</summary>
    public void SetVisible(bool visible)
    {
        if (_visible == visible)
        {
            return;
        }
        _visible = visible;
        var level = ArcMotion.GetEffectiveLevel(_host);
        _ = _shown.AnimateTo(visible ? 1 : 0, level == ArcMotionLevel.Full
            ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)
            : MotionSpec.Instant);
    }

    /// <summary>Springs the card's top-left corner to <paramref name="target"/>; <paramref name="jump"/> places it at once.</summary>
    public void MoveTo(Point target, bool jump)
    {
        if (Math.Abs(_target[0] - target.X) < 0.01 && Math.Abs(_target[1] - target.Y) < 0.01 && !jump)
        {
            return;
        }
        _target[0] = target.X;
        _target[1] = target.Y;
        if (jump || !ArcMotion.IsFull(_host))
        {
            _position.Jump(_target);
            return;
        }
        _position.AnimateTo(_target, Follow);
    }

    /// <summary>
    /// Places the card beside an anchor: to the right of <paramref name="anchorX"/> with a 14px gap, flipped to the
    /// left before it would leave <paramref name="bounds"/>, centered on <paramref name="anchorY"/> and clamped inside.
    /// </summary>
    public static Point Beside(Size card, double anchorX, double anchorY, Rect bounds, double gap = 14)
    {
        var x = anchorX + gap;
        if (x + card.Width > bounds.Right)
        {
            x = anchorX - gap - card.Width;
        }
        x = Math.Clamp(x, bounds.X, Math.Max(bounds.X, bounds.Right - card.Width));
        var y = Math.Clamp(anchorY - card.Height / 2, bounds.Y, Math.Max(bounds.Y, bounds.Bottom - card.Height));
        return new Point(x, y);
    }

    public void Render(DrawingContext context, ChartPalette palette, ChartTextCache texts)
    {
        var opacity = _shown.Value;
        if (opacity <= 0.001 || _lines.Count == 0)
        {
            return;
        }
        var size = _size;
        var origin = new Point(Math.Round(_position[0]), Math.Round(_position[1]));
        var scale = 0.96 + 0.04 * opacity;
        var center = new Point(origin.X + size.Width / 2, origin.Y + size.Height / 2);
        using var transform = context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale)
                                                    * Matrix.CreateTranslation(center.X, center.Y));
        using var fade = context.PushOpacity(opacity);
        var card = new RoundedRect(new Rect(origin, size), 14);
        context.DrawRectangle(palette.Brush(palette.SurfaceRaised), palette.Pen(palette.Border, 1), card, palette.FloatingShadow);
        var x = origin.X + 12;
        var y = origin.Y + 10;
        // One pixel of slack so a line measured at the card width never wraps or trims on rounding.
        var inner = size.Width - 24 + 1;
        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            if (i > 0)
            {
                y += LineGap;
            }
            switch (line.Kind)
            {
                case LineKind.Title:
                {
                    var text = texts.Get(palette, line.Text, palette.SizeXS, palette.TextSecondary, maxWidth: inner);
                    var h = palette.SizeXS * 1.4;
                    context.DrawText(text, new Point(x, y + (h - text.Height) / 2));
                    y += h + 2;
                    break;
                }
                case LineKind.Row:
                {
                    var h = palette.SizeSM * 1.4;
                    using var dim = context.PushOpacity(line.Emphasis < 0 ? 0.5 : 1);
                    var swatchBrush = palette.Brush(line.Swatch);
                    var my = y + h / 2;
                    if (line.Dashed)
                    {
                        context.DrawRectangle(swatchBrush, null, new RoundedRect(new Rect(x, my - 1.25, 3, 2.5), 1));
                        context.DrawRectangle(swatchBrush, null, new RoundedRect(new Rect(x + 5, my - 1.25, 3, 2.5), 1));
                    }
                    else
                    {
                        context.DrawRectangle(swatchBrush, null, new RoundedRect(new Rect(x, my - 1.25, 10, 2.5), 1.25));
                    }
                    var value = texts.Get(palette, line.Value ?? string.Empty, palette.SizeSM, palette.Foreground, medium: true);
                    var valueX = origin.X + size.Width - 12 - value.WidthIncludingTrailingWhitespace;
                    var nameColor = line.Emphasis > 0 ? palette.Foreground : palette.TextSecondary;
                    var nameWidth = Math.Max(1, valueX - 16 - (x + 18) + 1);
                    var name = texts.Get(palette, line.Text, palette.SizeSM, nameColor, maxWidth: nameWidth);
                    context.DrawText(name, new Point(x + 18, y + (h - name.Height) / 2));
                    context.DrawText(value, new Point(valueX, y + (h - value.Height) / 2));
                    y += h;
                    break;
                }
                case LineKind.Value:
                {
                    var text = texts.Get(palette, line.Text, palette.SizeBase, palette.Foreground, medium: true, maxWidth: inner, wrap: true);
                    var h = Math.Max(palette.SizeBase * 1.4, text.Height);
                    context.DrawText(text, new Point(x, y + (h - text.Height) / 2));
                    y += h;
                    break;
                }
                case LineKind.Event:
                {
                    y += 4;
                    var h = palette.SizeXS * 1.4;
                    context.DrawEllipse(palette.Brush(line.Swatch), null, new Point(x + 3.5, y + h / 2), 3.5, 3.5);
                    var text = texts.Get(palette, line.Text, palette.SizeXS, palette.Foreground, maxWidth: inner - 13);
                    context.DrawText(text, new Point(x + 13, y + (h - text.Height) / 2));
                    y += h;
                    break;
                }
                default:
                {
                    var text = texts.Get(palette, line.Text, palette.SizeXS, palette.TextMuted, maxWidth: inner, wrap: true);
                    var h = Math.Max(palette.SizeXS * 1.4, text.Height);
                    context.DrawText(text, new Point(x, y + (h - text.Height) / 2));
                    y += h;
                    break;
                }
            }
        }
    }
}
