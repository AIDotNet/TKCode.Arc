using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// One collapsed card: label, big number, change with its range ("30 days"), and a sparkline that ends in a dot. It draws no
/// surface of its own (the stage's surfaces sit behind it) and is a button: click, Enter or Space open the chart on its metric.
/// Self-drawn because four of them show a handful of strings and one polyline each, and the numbers never animate here.
/// </summary>
internal sealed class MetricExplorerCard : Control
{
    private const double LabelLine = 19.6;
    private const double DeltaLine = 16.8;

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(64);
    private MetricExplorerGeometry _geometry = new();
    private string _label = string.Empty;
    private string _value = string.Empty;
    private string _change = string.Empty;
    private string _range = string.Empty;
    private AnalyticsDirection _direction;
    private AnalyticsTone _tone;
    private double[] _samples = [];
    private bool _compact;
    private bool _wrapRange;
    private bool _pressed;
    private int _paletteVersion = -1;
    private StreamGeometry? _sparkline;
    private Size _sparkSize;

    static MetricExplorerCard()
    {
        FocusableProperty.OverrideDefaultValue<MetricExplorerCard>(true);
        IsTabStopProperty.OverrideDefaultValue<MetricExplorerCard>(true);
    }

    public MetricExplorerCard()
    {
        Cursor = new Cursor(StandardCursorType.Hand);
        ActualThemeVariantChanged += (_, _) => InvalidatePalette();
        ResourcesChanged += (_, _) => InvalidatePalette();
    }

    /// <summary>Raised with true when the card was opened with the keyboard.</summary>
    public event Action<MetricExplorerCard, bool>? Invoked;

    /// <summary>Raised when the pointer enters or leaves.</summary>
    public event Action<MetricExplorerCard, bool>? HoverChanged;

    /// <summary>The shared geometry.</summary>
    public MetricExplorerGeometry Geometry
    {
        get => _geometry;
        set
        {
            _geometry = value;
            InvalidateMeasure();
        }
    }

    /// <summary>Compact layout (block width ≤ 440): padding 14 / 16, two-line label, smaller number, range text on its own line.</summary>
    public bool IsCompact
    {
        get => _compact;
        set
        {
            if (_compact != value)
            {
                _compact = value;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }
    }

    /// <summary>The sparkline's last y as a fraction of the sparkline area (tests).</summary>
    internal double EndFraction { get; private set; }

    /// <summary>The number of sparkline points (tests).</summary>
    internal int SampleCount => _samples.Length;

    /// <summary>The texts on the card (tests).</summary>
    internal (string Label, string Value, string Change, string Range) Texts => (_label, _value, _change, _range);

    /// <summary>Direction and tone of the change (tests).</summary>
    internal (AnalyticsDirection Direction, AnalyticsTone Tone) Trend => (_direction, _tone);

    /// <summary>Sets what the card shows.</summary>
    public void SetContent(string label, string value, string change, string range, AnalyticsDirection direction, AnalyticsTone tone,
        double[] samples, string automationName)
    {
        _label = label;
        _value = value;
        _change = change;
        _range = range;
        _direction = direction;
        _tone = tone;
        _samples = samples;
        _sparkline = null;
        AutomationProperties.SetName(this, automationName);
        InvalidateMeasure();
        InvalidateVisual();
    }

    private Thickness Padding => _compact ? _geometry.CompactCardPadding : _geometry.CardPadding;

    private double ValueSize => _compact ? 18 : 22;

    private double ValueLine => _compact ? 22.5 : 27.5;

    private void EnsurePalette()
    {
        if (!this.IsAttachedToVisualTree() || (_paletteVersion >= 0 && _palette.IsReady))
        {
            return;
        }
        _palette.Refresh(this);
        _paletteVersion = _palette.Version;
    }

    private void InvalidatePalette()
    {
        _paletteVersion = -1;
        _text.Clear();
        InvalidateVisual();
    }

    private (Typeface Body, Typeface Medium) Faces() => (new Typeface(_palette.BodyFont), new Typeface(_palette.BodyFont, FontStyle.Normal, _palette.Medium));

    private double DeltaWidth()
    {
        var (_, medium) = Faces();
        var text = _text.Get(_change.Length == 0 ? " " : _change, medium, 12, true);
        return 14 + 2 + text.WidthIncludingTrailingWhitespace;
    }

    private double RangeWidth() => _text.Get(_range.Length == 0 ? " " : _range, Faces().Body, 12, true).WidthIncludingTrailingWhitespace;

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsurePalette();
        var padding = Padding;
        var width = double.IsInfinity(availableSize.Width) ? 280 : availableSize.Width;
        var inner = Math.Max(0, width - padding.Left - padding.Right);
        _wrapRange = _compact || (_palette.IsReady && DeltaWidth() + 6 + RangeWidth() > inner);
        var label = _compact ? 2 * LabelLine : LabelLine;
        var delta = DeltaLine + (_wrapRange ? 2 + DeltaLine : 0);
        var height = padding.Top + label + 4 + ValueLine + 4 + delta + 12 + _geometry.SparklineHeight + padding.Bottom;
        return new Size(width, Math.Round(height, 2));
    }

    #region Input

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            HoverChanged?.Invoke(this, true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pressed = false;
        HoverChanged?.Invoke(this, false);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
            Focus(NavigationMethod.Pointer);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pressed && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            _pressed = false;
            Invoked?.Invoke(this, false);
            e.Handled = true;
        }
        _pressed = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Enter or Key.Space && e.KeyModifiers == KeyModifiers.None)
        {
            Invoked?.Invoke(this, true);
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CardPeer(this);

    private sealed class CardPeer(MetricExplorerCard owner) : ControlAutomationPeer(owner), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public void Invoke() => owner.Invoked?.Invoke(owner, true);
    }

    #endregion

    #region Render

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        EnsurePalette();
        if (!_palette.IsReady)
        {
            return;
        }
        var (body, medium) = Faces();
        var padding = Padding;
        var x = padding.Left;
        var inner = Math.Max(0, size.Width - padding.Left - padding.Right);
        var y = padding.Top;

        var label = _compact
            ? _text.GetWrapped(_label, body, 14, inner, 2)
            : _text.GetTrimmed(_label, body, 14, inner);
        label.Draw(context, _palette.Brush(_palette.TextSecondary), new Point(x, y + (LabelLine - Math.Min(label.Height, LabelLine)) / 2));
        y += (_compact ? 2 * LabelLine : LabelLine) + 4;

        var value = _text.GetTrimmed(_value, medium, ValueSize, inner, true);
        value.Draw(context, _palette.Brush(_palette.Foreground), new Point(x, y + (ValueLine - value.Height) / 2));
        y += ValueLine + 4;

        var tone = _palette.ToneColor(_tone);
        DrawArrow(context, new Rect(x, y + (DeltaLine - 14) / 2, 14, 14), _palette.Pen(tone, 1.75 * 14 / 24));
        var change = _text.Get(_change, medium, 12, true);
        change.Draw(context, _palette.Brush(tone), new Point(x + 14 + 2, y + (DeltaLine - change.Height) / 2));
        var range = _text.Get(_range, body, 12, true);
        var rangeX = x + 14 + 2 + change.WidthIncludingTrailingWhitespace + 6;
        var rangeY = y;
        if (_wrapRange)
        {
            rangeX = x;
            rangeY += DeltaLine + 2;
        }
        range.Draw(context, _palette.Brush(_palette.TextMuted), new Point(rangeX, rangeY + (DeltaLine - range.Height) / 2));
        y += DeltaLine + (_wrapRange ? 2 + DeltaLine : 0) + 12;

        DrawSparkline(context, new Rect(x, y, inner, _geometry.SparklineHeight));

        if (AnalyticsFocus.IsVisible(this))
        {
            AnalyticsFocus.Draw(context, _palette, new Rect(size), _geometry.SurfaceRadius);
        }
    }

    // Lucide arrow-up-right (down: turned 90°, flat: arrow-right) in a 24 grid scaled to the box.
    private void DrawArrow(DrawingContext context, Rect box, ImmutablePen pen)
    {
        var s = box.Width / 24;
        Point P(double px, double py) => new(box.X + px * s, box.Y + py * s);
        switch (_direction)
        {
            case AnalyticsDirection.Flat:
                context.DrawLine(pen, P(5, 12), P(19, 12));
                context.DrawLine(pen, P(12, 5), P(19, 12));
                context.DrawLine(pen, P(19, 12), P(12, 19));
                break;
            case AnalyticsDirection.Down:
                context.DrawLine(pen, P(7, 7), P(17, 17));
                context.DrawLine(pen, P(17, 7), P(17, 17));
                context.DrawLine(pen, P(17, 17), P(7, 17));
                break;
            default:
                context.DrawLine(pen, P(7, 17), P(17, 7));
                context.DrawLine(pen, P(7, 7), P(17, 7));
                context.DrawLine(pen, P(17, 7), P(17, 17));
                break;
        }
    }

    private void DrawSparkline(DrawingContext context, Rect area)
    {
        if (_samples.Length < 2 || area.Width <= 0)
        {
            return;
        }
        if (_sparkline is null || _sparkSize != area.Size)
        {
            Build(area.Size);
        }
        using (context.PushTransform(Matrix.CreateTranslation(area.X, area.Y)))
        {
            context.DrawGeometry(null, _palette.Pen(_palette.Accent, 1.5), _sparkline!);
            // The end dot: 6px, centred on the last point.
            context.DrawEllipse(_palette.Brush(_palette.Accent), null, new Point(area.Width, EndFraction * area.Height), 3, 3);
        }
    }

    // viewBox 0 0 100 32 stretched over the area: x spans the width, y keeps 2 units of margin top and bottom.
    private void Build(Size size)
    {
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        foreach (var v in _samples)
        {
            min = Math.Min(min, v);
            max = Math.Max(max, v);
        }
        var span = max - min;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var i = 0; i < _samples.Length; i++)
            {
                var norm = span > 0 ? (_samples[i] - min) / span : 0.5;
                var fraction = (2 + (1 - norm) * 28) / 32;
                var point = new Point(size.Width * i / (_samples.Length - 1), fraction * size.Height);
                if (i == 0)
                {
                    ctx.BeginFigure(point, false);
                }
                else
                {
                    ctx.LineTo(point);
                }
                EndFraction = fraction;
            }
            ctx.EndFigure(false);
        }
        _sparkline = geometry;
        _sparkSize = size;
    }

    #endregion
}
