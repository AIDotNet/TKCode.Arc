using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One legend entry of a <see cref="SplitBar"/>: a color dot, the label, the value (rolling) and the share. Pointing at
/// it highlights its part in the bar. The entry for the empty rest has a hollow dot and takes no pointer highlight.
/// </summary>
internal sealed class SplitBarLegendItem : Control
{
    private readonly SplitBar _owner;
    private readonly SplitBarSlot? _slot;
    private readonly SplitBarLegendDot _dot = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _label = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly RollingDigits _value = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _percent = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly ArcShrinkRow _row = new() { Spacing = 6 };
    private bool _shown;

    public SplitBarLegendItem(SplitBar owner, SplitBarSlot? slot)
    {
        _owner = owner;
        _slot = slot;
        _dot.IsHollow = slot is null;
        ArcShrinkRow.SetIsShrinkable(_label, true);
        ArcTokens.Bind(_label, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(_value, RollingDigits.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_value, RollingDigits.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_percent, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        _percent.FontFeatures = SplitBarText.Tabular;
        AutomationProperties.SetAccessibilityView(_value, AccessibilityView.Raw);
        _row.Children.Add(_dot);
        _row.Children.Add(_label);
        _row.Children.Add(_value);
        _row.Children.Add(_percent);
        VisualChildren.Add(_row);
        LogicalChildren.Add(_row);
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    /// <summary>The part this entry stands for; null for the empty rest.</summary>
    public SplitBarSlot? Slot => _slot;

    /// <summary>The label shown (tests).</summary>
    public string? LabelText => _label.Text;

    /// <summary>The share shown (tests).</summary>
    public string? PercentText => _percent.IsVisible ? _percent.Text : null;

    /// <summary>The value the digits roll to (tests).</summary>
    public double ShownValue => _value.Value;

    public void Update(int colorIndex)
    {
        if (_slot is null)
        {
            return;
        }
        var segment = _slot.Segment;
        _dot.Index = colorIndex;
        _dot.Brush = segment.Brush;
        _label.Text = segment.Label;
        SetValue(Math.Max(0, segment.Value), _owner.ShareOf(segment));
    }

    public void UpdateRemainder(string label, double value, double share)
    {
        _label.Text = label;
        SetValue(value, share);
    }

    public void SetDim(double dim) => Opacity = 1 - Math.Clamp(dim, 0, 1) * 0.45;

    private void SetValue(double value, double share)
    {
        _value.Decimals = _owner.ValueDecimals;
        _value.Prefix = _owner.ValuePrefix;
        _value.Suffix = _owner.ValueSuffix;
        // The first value shows at once; later ones roll.
        _value.IsInstant = !_shown || !ArcMotion.IsFull(_owner);
        _value.Value = value;
        _shown = true;
        _percent.IsVisible = _owner.ShowsPercent;
        _percent.Text = (share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _row.Measure(availableSize);
        return _row.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _row.Arrange(new Rect(finalSize));
        return finalSize;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (_slot is null || e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        var index = IndexOf();
        if (index >= 0)
        {
            _owner.SetCurrentValue(SplitBar.HighlightedIndexProperty, index);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_slot is not null && _owner.HighlightedIndex == IndexOf())
        {
            _owner.SetCurrentValue(SplitBar.HighlightedIndexProperty, -1);
        }
    }

    private int IndexOf()
    {
        var index = 0;
        foreach (var slot in _owner.Slots)
        {
            if (ReferenceEquals(slot, _slot))
            {
                return index;
            }
            if (!slot.IsLeaving)
            {
                index++;
            }
        }
        return -1;
    }
}

/// <summary>A legend dot: the part's color (or its series color), or a hollow ring for the empty rest.</summary>
internal sealed class SplitBarLegendDot : Control
{
    internal static readonly StyledProperty<IBrush?> BrushProperty =
        AvaloniaProperty.Register<SplitBarLegendDot, IBrush?>(nameof(Brush));

    internal static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<SplitBarLegendDot, IBrush?>(nameof(RingBrush));

    internal static readonly StyledProperty<IBrush?> Series1Property = SplitBarTrack.Series1Property.AddOwner<SplitBarLegendDot>();

    internal static readonly StyledProperty<IBrush?> Series2Property = SplitBarTrack.Series2Property.AddOwner<SplitBarLegendDot>();

    internal static readonly StyledProperty<IBrush?> Series3Property = SplitBarTrack.Series3Property.AddOwner<SplitBarLegendDot>();

    internal static readonly StyledProperty<IBrush?> Series4Property = SplitBarTrack.Series4Property.AddOwner<SplitBarLegendDot>();

    internal static readonly StyledProperty<double> DotSizeProperty =
        AvaloniaProperty.Register<SplitBarLegendDot, double>(nameof(DotSize), 8);

    private int _index;
    private bool _isHollow;
    private IPen? _ringPen;
    private IBrush? _ringPenBrush;

    static SplitBarLegendDot()
    {
        AffectsRender<SplitBarLegendDot>(BrushProperty, RingBrushProperty, Series1Property, Series2Property, Series3Property, Series4Property);
        AffectsMeasure<SplitBarLegendDot>(DotSizeProperty);
    }

    public IBrush? Brush { get => GetValue(BrushProperty); set => SetValue(BrushProperty, value); }

    public IBrush? RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }

    public IBrush? Series1 { get => GetValue(Series1Property); set => SetValue(Series1Property, value); }

    public IBrush? Series2 { get => GetValue(Series2Property); set => SetValue(Series2Property, value); }

    public IBrush? Series3 { get => GetValue(Series3Property); set => SetValue(Series3Property, value); }

    public IBrush? Series4 { get => GetValue(Series4Property); set => SetValue(Series4Property, value); }

    public double DotSize { get => GetValue(DotSizeProperty); set => SetValue(DotSizeProperty, value); }

    public int Index
    {
        get => _index;
        set
        {
            if (_index != value)
            {
                _index = value;
                InvalidateVisual();
            }
        }
    }

    public bool IsHollow
    {
        get => _isHollow;
        set
        {
            _isHollow = value;
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(DotSize, DotSize);

    public override void Render(DrawingContext context)
    {
        var size = DotSize;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        if (_isHollow)
        {
            if (RingBrush is { } ring)
            {
                if (!ReferenceEquals(ring, _ringPenBrush))
                {
                    _ringPenBrush = ring;
                    _ringPen = new ImmutablePen(ring.ToImmutable(), 1.5);
                }
                context.DrawEllipse(null, _ringPen, center, size / 2 - 0.75, size / 2 - 0.75);
            }
            return;
        }
        var brush = Brush ?? (_index % 4) switch
        {
            0 => Series1,
            1 => Series2,
            2 => Series3,
            _ => Series4
        };
        if (brush is null)
        {
            return;
        }
        using (context.PushOpacity(Brush is null ? SplitBarTrack.SeriesOpacity(_index) : 1))
        {
            context.DrawEllipse(brush, null, center, size / 2, size / 2);
        }
    }
}

/// <summary>Shared text settings of the split bar.</summary>
internal static class SplitBarText
{
    /// <summary>Tabular figures, so rolling and changing numbers do not shift their neighbours.</summary>
    public static readonly FontFeatureCollection Tabular = new() { FontFeature.Parse("tnum") };
}
