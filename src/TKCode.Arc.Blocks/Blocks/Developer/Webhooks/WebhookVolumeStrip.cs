using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The traffic chart of <see cref="Webhooks"/> (Arc Webhooks, design reference): one bar per bucket (48 half hours) in
/// equal columns 5px apart over a 56px area with a 1px baseline, failed events as a Danger cap above a 1px gap, and the
/// axis under it (labels at bars 0, 12, 24 and 36, "Now" at the right). Bar heights spring (Smooth) to count / max,
/// staggered 4.2ms per bar; caps follow the bar at once. Hovering a column brightens its bar (20% → 55%) and dims the
/// others to .45 in 160ms; <see cref="HoverChanged"/> lets the block swap the chart's title for the bucket's reading.
/// Self-drawn in one control with cached brushes and text layouts: no per-frame allocation. Code-driven because each
/// bar has its own delayed spring and the hover state is per column of a drawing, not a styled element.
/// </summary>
internal sealed class WebhookVolumeStrip : Control
{
    private const double AxisGap = 6;
    private const double AxisHeight = 16;
    private const double CapGap = 1;
    private const double BarRadius = 2;
    private const double RestAlpha = 0.20;
    private const double HoverOverlayAlpha = 0.4375; // 20% under 43.75% composes to Arc's 55%.
    private const double DimmedOpacity = 0.45;

    /// <summary>Height of the bars area.</summary>
    public static readonly StyledProperty<double> ChartHeightProperty =
        AvaloniaProperty.Register<WebhookVolumeStrip, double>(nameof(ChartHeight), 56);

    /// <summary>Gap between columns.</summary>
    public static readonly StyledProperty<double> BarGapProperty =
        AvaloniaProperty.Register<WebhookVolumeStrip, double>(nameof(BarGap), 5);

    private readonly List<MotionValue<double>> _heights = new();
    private readonly List<MotionValue<double>> _emphasis = new();
    private MotionValue<double>? _dim;
    private int[] _totals = [];
    private int[] _failed = [];
    private int _max;
    private int _hovered = -1;
    private string[] _axisTexts = [];
    private int[] _axisIndices = [];
    private string? _nowText;
    private TextLayout?[] _axisLayouts = [];
    private TextLayout? _nowLayout;
    private bool _paletteDirty = true;
    private IBrush _ok = Brushes.Gray;
    private IBrush _okHover = Brushes.Gray;
    private IBrush _fail = Brushes.Red;
    private IBrush _axis = Brushes.Gray;
    private IBrush _baseline = Brushes.LightGray;

    static WebhookVolumeStrip()
    {
        AffectsMeasure<WebhookVolumeStrip>(ChartHeightProperty);
        AffectsRender<WebhookVolumeStrip>(BarGapProperty);
    }

    public WebhookVolumeStrip()
    {
        ClipToBounds = false;
        Cursor = new Cursor(StandardCursorType.Arrow);
    }

    public double ChartHeight { get => GetValue(ChartHeightProperty); set => SetValue(ChartHeightProperty, value); }

    public double BarGap { get => GetValue(BarGapProperty); set => SetValue(BarGapProperty, value); }

    /// <summary>The hovered bucket, or -1.</summary>
    public int HoveredIndex => _hovered;

    /// <summary>Raised when the hovered bucket changes (-1 when the pointer leaves).</summary>
    public event EventHandler<int>? HoverChanged;

    /// <summary>The number of bars.</summary>
    public int BarCount => _totals.Length;

    /// <summary>The drawn height of a bar right now (tests).</summary>
    public double BarHeight(int index) => index >= 0 && index < _heights.Count ? _heights[index].Value : 0;

    /// <summary>The height a bar is heading to.</summary>
    public double TargetHeight(int index) => index >= 0 && index < _totals.Length && _max > 0 ? _totals[index] * ChartHeight / _max : 0;

    /// <summary>The drawn height of a bar's failed cap (without its gap).</summary>
    public double CapHeight(int index)
    {
        if (index < 0 || index >= _totals.Length || _totals[index] <= 0)
        {
            return 0;
        }
        var bar = BarHeight(index);
        return Math.Max(0, bar * Math.Min(_failed[index], _totals[index]) / _totals[index] - CapGap);
    }

    /// <summary>Whether a bar is still springing.</summary>
    public bool IsMoving => _heights.Any(h => h.IsAnimating);

    /// <summary>
    /// Shows <paramref name="buckets"/>. With <paramref name="animate"/> the bars spring from where they are (a new
    /// endpoint) with the per-bar stagger; otherwise they jump. Failed caps always change at once (Arc).
    /// </summary>
    public void SetData(IReadOnlyList<WebhookVolumeBucket> buckets, bool animate)
    {
        var count = buckets.Count;
        if (_totals.Length != count)
        {
            _totals = new int[count];
            _failed = new int[count];
            foreach (var value in _heights.Concat(_emphasis))
            {
                value.Stop();
            }
            _heights.Clear();
            _emphasis.Clear();
            for (var i = 0; i < count; i++)
            {
                var height = new MotionValue<double>(this, 0, MotionVectors.Double);
                height.Changed += _ => InvalidateVisual();
                _heights.Add(height);
                var emphasis = new MotionValue<double>(this, 0, MotionVectors.Double);
                emphasis.Changed += _ => InvalidateVisual();
                _emphasis.Add(emphasis);
            }
            _hovered = -1;
        }
        _max = 0;
        for (var i = 0; i < count; i++)
        {
            _totals[i] = Math.Max(0, buckets[i].Total);
            _failed[i] = Math.Max(0, buckets[i].Failed);
            _max = Math.Max(_max, _totals[i]);
        }
        var springs = animate && VisualRoot is not null;
        for (var i = 0; i < count; i++)
        {
            var target = TargetHeight(i);
            if (springs)
            {
                _ = _heights[i].AnimateTo(target, MotionSpec.Spring(DeveloperSprings.Smooth,
                    TimeSpan.FromMilliseconds(DeveloperSprings.BarStagger.TotalMilliseconds * i)));
            }
            else
            {
                _heights[i].Jump(target);
            }
        }
        InvalidateVisual();
    }

    /// <summary>The axis: labels under the given bar indices, and the text at the right edge.</summary>
    public void SetAxis(IReadOnlyList<(int Index, string Text)> labels, string now)
    {
        _axisIndices = labels.Select(l => l.Index).ToArray();
        _axisTexts = labels.Select(l => l.Text).ToArray();
        _nowText = now;
        DisposeLayouts();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, ChartHeight + AxisGap + AxisHeight);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeChanged;
        ResourcesChanged += OnResourcesChanged;
        _paletteDirty = true;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeChanged;
        ResourcesChanged -= OnResourcesChanged;
        DisposeLayouts();
    }

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e) => OnThemeChanged(sender, EventArgs.Empty);

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        _paletteDirty = true;
        DisposeLayouts();
        InvalidateVisual();
    }

    private void DisposeLayouts()
    {
        foreach (var layout in _axisLayouts)
        {
            layout?.Dispose();
        }
        _axisLayouts = [];
        _nowLayout?.Dispose();
        _nowLayout = null;
    }

    private void EnsurePalette()
    {
        if (!_paletteDirty)
        {
            return;
        }
        try
        {
            var foreground = ArcTokens.GetColor(this, ArcDesignTokenKind.Foreground);
            _ok = new ImmutableSolidColorBrush(Oklch.WithAlpha(foreground, RestAlpha));
            _okHover = new ImmutableSolidColorBrush(Oklch.WithAlpha(foreground, HoverOverlayAlpha));
            _fail = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.Danger));
            _axis = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.TextMuted));
            _baseline = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.Border));
            _paletteDirty = false;
        }
        catch (Exception)
        {
            // Detached or no theme scope yet: keep the fallbacks and try again on the next render.
        }
    }

    private void EnsureLayouts()
    {
        if (_axisLayouts.Length == _axisTexts.Length && (_nowLayout is not null || _nowText is null))
        {
            return;
        }
        double size = 12;
        FontFamily family = FontFamily.Default;
        try
        {
            size = ArcTokens.Get<double>(this, ArcDesignTokenKind.TypeXS);
            family = ArcTokens.Get<FontFamily>(this, ArcDesignTokenKind.BodyFontFamily);
        }
        catch (Exception)
        {
            // Fallbacks above.
        }
        var face = new Typeface(family);
        _axisLayouts = _axisTexts.Select(t => (TextLayout?)new TextLayout(t, face, size, _axis)).ToArray();
        _nowLayout = _nowText is null ? null : new TextLayout(_nowText, face, size, _axis);
    }

    /// <summary>The gap between columns: <see cref="BarGap"/>, but at most 30% of a column's pitch on narrow strips.</summary>
    private double Gap(int count, double width) => count == 0 ? BarGap : Math.Min(BarGap, width / count * 0.3);

    private double ColumnWidth(int count, double width) =>
        count == 0 ? 0 : Math.Max(0, (width - Gap(count, width) * (count - 1)) / count);

    public override void Render(DrawingContext context)
    {
        EnsurePalette();
        EnsureLayouts();
        var width = Bounds.Width;
        var chart = ChartHeight;
        // Hit testing covers the whole strip (a drawing is only hit where it painted).
        context.FillRectangle(Brushes.Transparent, new Rect(0, 0, width, Bounds.Height));
        context.FillRectangle(_baseline, new Rect(0, chart - 1, width, 1));
        var count = _totals.Length;
        var column = ColumnWidth(count, width);
        var gap = Gap(count, width);
        var dim = _dim?.Value ?? 0;
        for (var i = 0; i < count; i++)
        {
            var height = Math.Max(0, _heights[i].Value);
            if (height < 0.25)
            {
                continue;
            }
            var x = i * (column + gap);
            var top = chart - 1 - height;
            var bar = new Rect(x, top, column, height);
            var emphasis = _emphasis[i].Value;
            var opacity = 1 - (1 - DimmedOpacity) * dim * (1 - emphasis);
            var radius = new Vector(Math.Min(BarRadius, column / 2), Math.Min(BarRadius, height));
            using (context.PushOpacity(opacity))
            using (context.PushClip(new RoundedRect(bar, radius, radius, default, default)))
            {
                var cap = _totals[i] > 0 ? height * Math.Min(_failed[i], _totals[i]) / _totals[i] : 0;
                var okRect = new Rect(x, top + cap, column, height - cap);
                context.FillRectangle(_ok, okRect);
                if (emphasis > 0.001)
                {
                    using (context.PushOpacity(emphasis))
                    {
                        context.FillRectangle(_okHover, okRect);
                    }
                }
                if (cap - CapGap > 0.25)
                {
                    context.FillRectangle(_fail, new Rect(x, top, column, cap - CapGap));
                }
            }
        }
        var axisTop = chart + AxisGap;
        for (var i = 0; i < _axisLayouts.Length; i++)
        {
            if (_axisLayouts[i] is not { } layout || _axisIndices[i] >= count)
            {
                continue;
            }
            layout.Draw(context, new Point(_axisIndices[i] * (column + gap), axisTop));
        }
        if (_nowLayout is { } now)
        {
            now.Draw(context, new Point(width - now.WidthIncludingTrailingWhitespace, axisTop));
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        var point = e.GetPosition(this);
        if (point.Y < 0 || point.Y > ChartHeight)
        {
            Hover(-1);
            return;
        }
        Hover(IndexAt(point.X));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Hover(-1);
    }

    /// <summary>The column under <paramref name="x"/> (a gap belongs to the column at its left).</summary>
    public int IndexAt(double x)
    {
        var count = _totals.Length;
        if (count == 0)
        {
            return -1;
        }
        var column = ColumnWidth(count, Bounds.Width);
        var index = (int)Math.Floor(x / (column + Gap(count, Bounds.Width)));
        return Math.Clamp(index, 0, count - 1);
    }

    /// <summary>Hovers <paramref name="index"/> (-1 for none), as the pointer does.</summary>
    public void Hover(int index)
    {
        if (index == _hovered || index >= _totals.Length)
        {
            return;
        }
        var previous = _hovered;
        _hovered = index;
        _dim ??= CreateDim();
        var spec = ArcMotion.IsFull(this) ? DeveloperSprings.FadeOut() : MotionSpec.Instant;
        if (previous >= 0 && previous < _emphasis.Count)
        {
            _ = _emphasis[previous].AnimateTo(0, spec);
        }
        if (index >= 0)
        {
            _ = _emphasis[index].AnimateTo(1, spec);
        }
        _ = _dim.AnimateTo(index >= 0 ? 1 : 0, spec);
        HoverChanged?.Invoke(this, index);
    }

    private MotionValue<double> CreateDim()
    {
        var dim = new MotionValue<double>(this, 0, MotionVectors.Double);
        dim.Changed += _ => InvalidateVisual();
        return dim;
    }

    /// <summary>The drawn opacity of a bar right now (tests).</summary>
    public double BarOpacity(int index) =>
        index < 0 || index >= _emphasis.Count ? 1 : 1 - (1 - DimmedOpacity) * (_dim?.Value ?? 0) * (1 - _emphasis[index].Value);
}
