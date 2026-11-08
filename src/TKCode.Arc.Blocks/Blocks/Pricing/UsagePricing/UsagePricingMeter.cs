using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The plan meter at the top of the <see cref="UsagePricing"/> card (notes 3.1, 6.2): one equal segment per plan, a 4px
/// pill bar over its name. Bars up to the recommended plan are filled (cumulative); a fill grows from the left on a soft
/// Smooth spring (visual duration 0.45s) and the current plan's name turns strong over 240ms. Drawn in one Render pass:
/// names are cached text layouts in both colors, crossfaded by opacity, so a frame allocates nothing. Hidden from
/// assistive technology (the plan name says the same).
/// </summary>
internal sealed class UsagePricingMeter : Control
{
    /// <summary>Arc's fill spring was fitted at ω 11.8, ζ .94: the Smooth family, a little softer.</summary>
    internal static readonly SpringSpec FillSpring = SpringSpec.FromVisual(0.45, 0);

    public static readonly StyledProperty<int> CurrentIndexProperty =
        AvaloniaProperty.Register<UsagePricingMeter, int>(nameof(CurrentIndex), -1);

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<UsagePricingMeter, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<UsagePricingMeter, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<IBrush?> NameBrushProperty =
        AvaloniaProperty.Register<UsagePricingMeter, IBrush?>(nameof(NameBrush));

    public static readonly StyledProperty<IBrush?> CurrentNameBrushProperty =
        AvaloniaProperty.Register<UsagePricingMeter, IBrush?>(nameof(CurrentNameBrush));

    public static readonly StyledProperty<double> BarHeightProperty =
        AvaloniaProperty.Register<UsagePricingMeter, double>(nameof(BarHeight), 4);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<UsagePricingMeter, double>(nameof(Gap), 4);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<UsagePricingMeter>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<UsagePricingMeter>();

    public int CurrentIndex { get => GetValue(CurrentIndexProperty); set => SetValue(CurrentIndexProperty, value); }
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
    public IBrush? NameBrush { get => GetValue(NameBrushProperty); set => SetValue(NameBrushProperty, value); }
    public IBrush? CurrentNameBrush { get => GetValue(CurrentNameBrushProperty); set => SetValue(CurrentNameBrushProperty, value); }
    public double BarHeight { get => GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>Space between a bar and its name.</summary>
    internal const double NameGap = 8;

    /// <summary>Line height of a name (12px × 1.4).</summary>
    internal const double NameLineHeight = 16.8;

    private sealed class Segment
    {
        public string Name = string.Empty;
        public MotionValue<double>? Fill;
        public MotionValue<double>? Strong;
        public double FillValue;
        public double StrongValue;
        public TextLayout? Muted;
        public TextLayout? Current;
    }

    private readonly List<Segment> _segments = new();

    static UsagePricingMeter()
    {
        AffectsMeasure<UsagePricingMeter>(BarHeightProperty, FontSizeProperty, FontFamilyProperty);
        AffectsRender<UsagePricingMeter>(TrackBrushProperty, FillBrushProperty, GapProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<UsagePricingMeter>(false);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<UsagePricingMeter>(AccessibilityView.Raw);
    }

    /// <summary>Fill (0–1) of each segment right now (tests).</summary>
    internal IReadOnlyList<double> Fills => _segments.Select(s => s.FillValue).ToList();

    /// <summary>How strong (0–1) each name is drawn right now (tests).</summary>
    internal IReadOnlyList<double> Strengths => _segments.Select(s => s.StrongValue).ToList();

    /// <summary>The segment names (tests).</summary>
    internal IReadOnlyList<string> Names => _segments.Select(s => s.Name).ToList();

    /// <summary>Sets the plan names (one segment each); fills and colors jump to the current index.</summary>
    public void SetNames(IReadOnlyList<string> names)
    {
        var same = names.Count == _segments.Count;
        for (var i = 0; same && i < names.Count; i++)
        {
            same = names[i] == _segments[i].Name;
        }
        if (same)
        {
            return;
        }
        foreach (var segment in _segments)
        {
            segment.Fill?.Stop();
            segment.Strong?.Stop();
            DisposeText(segment);
        }
        _segments.Clear();
        foreach (var name in names)
        {
            _segments.Add(new Segment { Name = name });
        }
        Sync(animate: false);
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CurrentIndexProperty)
        {
            Sync(animate: IsLoaded);
        }
        else if (change.Property == NameBrushProperty || change.Property == CurrentNameBrushProperty ||
                 change.Property == FontFamilyProperty || change.Property == FontSizeProperty)
        {
            foreach (var segment in _segments)
            {
                DisposeText(segment);
            }
            InvalidateVisual();
        }
    }

    private void Sync(bool animate)
    {
        var current = CurrentIndex;
        var full = animate && ArcMotion.IsFull(this);
        var colors = animate && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
        for (var i = 0; i < _segments.Count; i++)
        {
            var segment = _segments[i];
            var fill = i <= current ? 1.0 : 0.0;
            var strong = i == current ? 1.0 : 0.0;
            if (full)
            {
                segment.Fill ??= Create(segment, isFill: true);
                _ = segment.Fill.AnimateTo(fill, MotionSpec.Spring(FillSpring));
            }
            else
            {
                segment.Fill?.Jump(fill);
                segment.FillValue = fill;
            }
            if (colors)
            {
                segment.Strong ??= Create(segment, isFill: false);
                _ = segment.Strong.AnimateTo(strong, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
            }
            else
            {
                segment.Strong?.Jump(strong);
                segment.StrongValue = strong;
            }
        }
        InvalidateVisual();
    }

    private MotionValue<double> Create(Segment segment, bool isFill)
    {
        var value = new MotionValue<double>(this, isFill ? segment.FillValue : segment.StrongValue, MotionVectors.Double);
        value.Changed += v =>
        {
            if (isFill)
            {
                segment.FillValue = v;
            }
            else
            {
                segment.StrongValue = v;
            }
            InvalidateVisual();
        };
        return value;
    }

    private static void DisposeText(Segment segment)
    {
        segment.Muted?.Dispose();
        segment.Current?.Dispose();
        segment.Muted = null;
        segment.Current = null;
    }

    protected override Size MeasureOverride(Size availableSize)
        => new(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, BarHeight + NameGap + NameLineHeight);

    public override void Render(DrawingContext context)
    {
        var count = _segments.Count;
        if (count == 0)
        {
            return;
        }
        var width = Bounds.Width;
        var gap = Gap;
        var segmentWidth = Math.Max(0, (width - gap * (count - 1)) / count);
        var radius = BarHeight / 2;
        var typeface = new Typeface(FontFamily);
        for (var i = 0; i < count; i++)
        {
            var segment = _segments[i];
            var x = i * (segmentWidth + gap);
            var bar = new Rect(x, 0, segmentWidth, BarHeight);
            if (TrackBrush is { } track)
            {
                context.DrawRectangle(track, null, bar, radius, radius);
            }
            var fill = Math.Clamp(segment.FillValue, 0, 1);
            if (fill > 0.001 && FillBrush is { } fillBrush)
            {
                // Arc scales a fill that covers the bar (scaleX from the left) inside the clipped pill.
                using (context.PushClip(new RoundedRect(bar, radius)))
                {
                    context.DrawRectangle(fillBrush, null, new Rect(x, 0, segmentWidth * fill, BarHeight));
                }
            }
            segment.Muted ??= new TextLayout(segment.Name, typeface, FontSize, NameBrush ?? Brushes.Gray, maxWidth: Math.Max(1, segmentWidth),
                textTrimming: TextTrimming.CharacterEllipsis);
            segment.Current ??= new TextLayout(segment.Name, typeface, FontSize, CurrentNameBrush ?? Brushes.Black, maxWidth: Math.Max(1, segmentWidth),
                textTrimming: TextTrimming.CharacterEllipsis);
            var origin = new Point(x, BarHeight + NameGap + (NameLineHeight - segment.Muted.Height) / 2);
            var strong = Math.Clamp(segment.StrongValue, 0, 1);
            if (strong < 0.999)
            {
                using (context.PushOpacity(1 - strong))
                {
                    segment.Muted.Draw(context, origin);
                }
            }
            if (strong > 0.001)
            {
                using (context.PushOpacity(strong))
                {
                    segment.Current.Draw(context, origin);
                }
            }
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) > 0.5)
        {
            foreach (var segment in _segments)
            {
                DisposeText(segment);
            }
        }
    }
}
