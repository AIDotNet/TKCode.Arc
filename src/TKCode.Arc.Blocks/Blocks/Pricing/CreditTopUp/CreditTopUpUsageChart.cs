using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The usage bars of <see cref="CreditTopUp"/> (Arc Credit top-up, design reference): one rounded 6px bar per day,
/// bottom aligned, height = value ÷ the largest value × the plot height; today's bar in the accent. Pointing at a day's
/// column (the whole column, not just the bar) turns its bar to the foreground and dims the others to 55% over 160ms on
/// Arc's standard curve; <see cref="HoveredIndex"/> drives the readout under the chart. Drawn in one control; no entry
/// animation (the reference draws the final heights on the first frame). The chart is an image for assistive tech.
/// </summary>
internal sealed class CreditTopUpUsageChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<CreditTopUpUsageDay>?> UsageProperty =
        AvaloniaProperty.Register<CreditTopUpUsageChart, IReadOnlyList<CreditTopUpUsageDay>?>(nameof(Usage));

    public static readonly StyledProperty<double> BarWidthProperty =
        AvaloniaProperty.Register<CreditTopUpUsageChart, double>(nameof(BarWidth), 6);

    /// <summary>The width of a day's column when the chart is not stretched (10).</summary>
    public static readonly StyledProperty<double> SlotWidthProperty =
        AvaloniaProperty.Register<CreditTopUpUsageChart, double>(nameof(SlotWidth), 10);

    public static readonly StyledProperty<double> BarRadiusProperty =
        AvaloniaProperty.Register<CreditTopUpUsageChart, double>(nameof(BarRadius), 2);

    public static readonly StyledProperty<double> DimOpacityProperty =
        AvaloniaProperty.Register<CreditTopUpUsageChart, double>(nameof(DimOpacity), 0.55);

    /// <summary>When true the columns share the whole width (the chart under the balance).</summary>
    public static readonly StyledProperty<bool> IsStretchedProperty =
        AvaloniaProperty.Register<CreditTopUpUsageChart, bool>(nameof(IsStretched));

    public static readonly StyledProperty<IBrush?> BarBrushProperty =
        AvaloniaProperty.Register<CreditTopUpUsageChart, IBrush?>(nameof(BarBrush));

    public static readonly StyledProperty<IBrush?> TodayBrushProperty =
        AvaloniaProperty.Register<CreditTopUpUsageChart, IBrush?>(nameof(TodayBrush));

    public static readonly StyledProperty<IBrush?> HoverBrushProperty =
        AvaloniaProperty.Register<CreditTopUpUsageChart, IBrush?>(nameof(HoverBrush));

    public static readonly DirectProperty<CreditTopUpUsageChart, int> HoveredIndexProperty =
        AvaloniaProperty.RegisterDirect<CreditTopUpUsageChart, int>(nameof(HoveredIndex), o => o.HoveredIndex);

    public IReadOnlyList<CreditTopUpUsageDay>? Usage { get => GetValue(UsageProperty); set => SetValue(UsageProperty, value); }
    public double BarWidth { get => GetValue(BarWidthProperty); set => SetValue(BarWidthProperty, value); }
    public double SlotWidth { get => GetValue(SlotWidthProperty); set => SetValue(SlotWidthProperty, value); }
    public double BarRadius { get => GetValue(BarRadiusProperty); set => SetValue(BarRadiusProperty, value); }
    public double DimOpacity { get => GetValue(DimOpacityProperty); set => SetValue(DimOpacityProperty, value); }
    public bool IsStretched { get => GetValue(IsStretchedProperty); set => SetValue(IsStretchedProperty, value); }
    public IBrush? BarBrush { get => GetValue(BarBrushProperty); set => SetValue(BarBrushProperty, value); }
    public IBrush? TodayBrush { get => GetValue(TodayBrushProperty); set => SetValue(TodayBrushProperty, value); }
    public IBrush? HoverBrush { get => GetValue(HoverBrushProperty); set => SetValue(HoverBrushProperty, value); }

    /// <summary>The day under the pointer, or −1.</summary>
    public int HoveredIndex
    {
        get => _hovered;
        private set => SetAndRaise(HoveredIndexProperty, ref _hovered, value);
    }

    private static readonly TimeSpan Fade = ArcMotionPresets.Fast;

    private int _hovered = -1;
    // Per-day highlight (0 rest, 1 pointed at) and the shared dim of the others; built on the first hover.
    private MotionValue<double>[]? _lit;
    private MotionValue<double>? _dim;
    private Color _bar;
    private Color _today;
    private Color _hover;
    private ImmutableSolidColorBrush? _barBrush;
    private ImmutableSolidColorBrush? _todayBrush;
    private ImmutableSolidColorBrush? _hoverBrush;

    static CreditTopUpUsageChart()
    {
        AffectsRender<CreditTopUpUsageChart>(UsageProperty, BarWidthProperty, BarRadiusProperty, IsStretchedProperty, SlotWidthProperty);
        AffectsMeasure<CreditTopUpUsageChart>(UsageProperty, SlotWidthProperty, IsStretchedProperty);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<CreditTopUpUsageChart>(AccessibilityView.Content);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ImagePeer(this);

    private sealed class ImagePeer(Control owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BarBrushProperty || change.Property == TodayBrushProperty || change.Property == HoverBrushProperty)
        {
            _bar = (BarBrush as ISolidColorBrush)?.Color ?? Colors.Gray;
            _today = (TodayBrush as ISolidColorBrush)?.Color ?? Colors.Black;
            _hover = (HoverBrush as ISolidColorBrush)?.Color ?? Colors.Black;
            _barBrush = new ImmutableSolidColorBrush(_bar);
            _todayBrush = new ImmutableSolidColorBrush(_today);
            _hoverBrush = new ImmutableSolidColorBrush(_hover);
            InvalidateVisual();
        }
        else if (change.Property == UsageProperty)
        {
            _lit = null;
            _dim = null;
            HoveredIndex = -1;
        }
    }

    private int Count => Usage?.Count ?? 0;

    private double Slot(double width) => IsStretched && Count > 0 ? width / Count : SlotWidth;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = IsStretched
            ? (double.IsInfinity(availableSize.Width) ? Count * SlotWidth : availableSize.Width)
            : Count * SlotWidth;
        return new Size(width, double.IsInfinity(availableSize.Height) ? 44 : 0);
    }

    /// <summary>The day whose column contains <paramref name="x"/>, or −1.</summary>
    internal int IndexAt(double x)
    {
        var count = Count;
        if (count == 0)
        {
            return -1;
        }
        var slot = Slot(Bounds.Width);
        var left = Bounds.Width - slot * count;
        var index = (int)Math.Floor((x - left) / slot);
        return index >= 0 && index < count ? index : -1;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        // Hover only for a precise pointer (Arc's hover media query).
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        var p = e.GetPosition(this);
        Point(p.Y >= 0 && p.Y <= Bounds.Height ? IndexAt(p.X) : -1);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Point(-1);
    }

    /// <summary>Points at a day (or −1 for none).</summary>
    internal void Point(int index)
    {
        if (index == _hovered)
        {
            return;
        }
        var count = Count;
        if (count == 0)
        {
            return;
        }
        if (_lit is null || _lit.Length != count)
        {
            _lit = new MotionValue<double>[count];
            for (var i = 0; i < count; i++)
            {
                _lit[i] = Create(0);
            }
            _dim = Create(0);
        }
        var spec = MotionSpec.Tween(Fade, ArcEasings.Standard);
        if (_hovered >= 0 && _hovered < count)
        {
            _ = _lit[_hovered].AnimateTo(0, spec);
        }
        if (index >= 0)
        {
            _ = _lit[index].AnimateTo(1, spec);
        }
        _ = _dim!.AnimateTo(index >= 0 ? 1 : 0, spec);
        HoveredIndex = index;
    }

    private MotionValue<double> Create(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    /// <summary>The rectangle of day <paramref name="index"/>'s bar.</summary>
    internal Rect BarRect(int index)
    {
        var usage = Usage;
        if (usage is null || index < 0 || index >= usage.Count)
        {
            return default;
        }
        long max = 0;
        foreach (var day in usage)
        {
            max = Math.Max(max, day.Credits);
        }
        var slot = Slot(Bounds.Width);
        var left = Bounds.Width - slot * usage.Count;
        var height = CreditTopUpMath.BarHeight(usage[index].Credits, max, Bounds.Height);
        var x = left + slot * index + (slot - BarWidth) / 2;
        return new Rect(x, Bounds.Height - height, BarWidth, height);
    }

    public override void Render(DrawingContext context)
    {
        // The whole plot takes the pointer, not only the painted bars.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        var usage = Usage;
        if (usage is null || usage.Count == 0)
        {
            return;
        }
        var dim = _dim?.Value ?? 0;
        var last = usage.Count - 1;
        for (var i = 0; i < usage.Count; i++)
        {
            var rect = BarRect(i);
            if (rect.Height <= 0)
            {
                continue;
            }
            var lit = _lit is not null && i < _lit.Length ? _lit[i].Value : 0;
            var rest = i == last ? _today : _bar;
            var color = lit > 0 ? Mix(rest, _hover, lit) : rest;
            var opacity = 1 - (1 - DimOpacity) * dim * (1 - lit);
            using (context.PushOpacity(opacity))
            {
                context.DrawRectangle(BrushFor(color), null, new RoundedRect(rect, Math.Min(BarRadius, rect.Height / 2)));
            }
        }
    }

    private IBrush BrushFor(Color color)
    {
        if (_barBrush is not null && color == _bar)
        {
            return _barBrush;
        }
        if (_todayBrush is not null && color == _today)
        {
            return _todayBrush;
        }
        if (_hoverBrush is not null && color == _hover)
        {
            return _hoverBrush;
        }
        // In-between colors exist only during the 160ms fade of one or two bars.
        return new ImmutableSolidColorBrush(color);
    }

    private static Color Mix(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)Math.Round(a.A + (b.A - a.A) * t),
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }
}
