using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One metric as shown in a <see cref="MetricTicker"/>: the label, the rolling value and the change with an arrow, in one
/// line (scrolling) or with the label above (columns). When the value changes, the cell's background flashes the good or
/// bad tint and fades back. It listens to its metric only while it is on screen.
/// </summary>
internal sealed class MetricTickerCell : Control
{
    internal static readonly StyledProperty<IBrush?> UpBrushProperty =
        AvaloniaProperty.Register<MetricTickerCell, IBrush?>(nameof(UpBrush));

    internal static readonly StyledProperty<IBrush?> DownBrushProperty =
        AvaloniaProperty.Register<MetricTickerCell, IBrush?>(nameof(DownBrush));

    internal static readonly StyledProperty<IBrush?> FlatBrushProperty =
        AvaloniaProperty.Register<MetricTickerCell, IBrush?>(nameof(FlatBrush));

    internal static readonly StyledProperty<IBrush?> UpTintProperty =
        AvaloniaProperty.Register<MetricTickerCell, IBrush?>(nameof(UpTint));

    internal static readonly StyledProperty<IBrush?> DownTintProperty =
        AvaloniaProperty.Register<MetricTickerCell, IBrush?>(nameof(DownTint));

    internal static readonly StyledProperty<CornerRadius> TintRadiusProperty =
        AvaloniaProperty.Register<MetricTickerCell, CornerRadius>(nameof(TintRadius), new CornerRadius(8));

    internal static readonly StyledProperty<Thickness> PaddingProperty =
        AvaloniaProperty.Register<MetricTickerCell, Thickness>(nameof(Padding), new Thickness(8, 4));

    internal static readonly StyledProperty<double> ValueFontSizeProperty =
        AvaloniaProperty.Register<MetricTickerCell, double>(nameof(ValueFontSize), 14);

    internal static readonly StyledProperty<double> StackedValueFontSizeProperty =
        AvaloniaProperty.Register<MetricTickerCell, double>(nameof(StackedValueFontSize), 22);

    private readonly TextBlock _label = new() { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly RollingDigits _value = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly ArcGlyph _arrow = new() { Size = 12, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _change = new() { VerticalAlignment = VerticalAlignment.Center };
    private MetricTickerItem? _item;
    private bool _stacked;
    private bool _watching;
    private bool _attached;
    private bool _shown;
    private int _trend;
    private MotionValue<double>? _flash;

    static MetricTickerCell()
    {
        AffectsRender<MetricTickerCell>(UpTintProperty, DownTintProperty, TintRadiusProperty);
        AffectsMeasure<MetricTickerCell>(PaddingProperty, ValueFontSizeProperty, StackedValueFontSizeProperty);
    }

    public MetricTickerCell()
    {
        ArcTokens.Bind(_label, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(_value, RollingDigits.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_value, RollingDigits.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        _change.FontFeatures = MetricTickerText.Tabular;
        _change.FontSize = 12;
        AutomationProperties.SetAccessibilityView(_value, AccessibilityView.Raw);
        AutomationProperties.SetAccessibilityView(_label, AccessibilityView.Raw);
        AutomationProperties.SetAccessibilityView(_change, AccessibilityView.Raw);
        foreach (var child in new Control[] { _label, _value, _arrow, _change })
        {
            VisualChildren.Add(child);
            LogicalChildren.Add(child);
        }
    }

    public IBrush? UpBrush { get => GetValue(UpBrushProperty); set => SetValue(UpBrushProperty, value); }

    public IBrush? DownBrush { get => GetValue(DownBrushProperty); set => SetValue(DownBrushProperty, value); }

    public IBrush? FlatBrush { get => GetValue(FlatBrushProperty); set => SetValue(FlatBrushProperty, value); }

    public IBrush? UpTint { get => GetValue(UpTintProperty); set => SetValue(UpTintProperty, value); }

    public IBrush? DownTint { get => GetValue(DownTintProperty); set => SetValue(DownTintProperty, value); }

    public CornerRadius TintRadius { get => GetValue(TintRadiusProperty); set => SetValue(TintRadiusProperty, value); }

    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    public double ValueFontSize { get => GetValue(ValueFontSizeProperty); set => SetValue(ValueFontSizeProperty, value); }

    public double StackedValueFontSize { get => GetValue(StackedValueFontSizeProperty); set => SetValue(StackedValueFontSizeProperty, value); }

    /// <summary>The metric shown.</summary>
    public MetricTickerItem? Item
    {
        get => _item;
        set
        {
            if (ReferenceEquals(_item, value))
            {
                return;
            }
            Unwatch();
            _item = value;
            _shown = false;
            if (_attached)
            {
                WatchItem();
            }
            Update(animate: false);
        }
    }

    /// <summary>The label above the value (columns) instead of before it (one line).</summary>
    public bool IsStacked
    {
        get => _stacked;
        set
        {
            if (_stacked != value)
            {
                _stacked = value;
                UpdateFonts();
                InvalidateMeasure();
            }
        }
    }

    /// <summary>The good (1) or bad (-1) direction of the last change, 0 for none (tests).</summary>
    public int Trend => _trend;

    /// <summary>The change text shown, or null (tests).</summary>
    public string? ChangeText => _change.IsVisible ? _change.Text : null;

    /// <summary>The value the digits show (tests).</summary>
    public double ShownValue => _value.Value;

    /// <summary>The background flash, 0 to 1 (tests).</summary>
    public double Flash => _flash?.Value ?? 0;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        WatchItem();
        Update(animate: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        Unwatch();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UpBrushProperty || change.Property == DownBrushProperty || change.Property == FlatBrushProperty)
        {
            UpdateTrendColors();
        }
        else if (change.Property == ValueFontSizeProperty || change.Property == StackedValueFontSizeProperty)
        {
            UpdateFonts();
        }
    }

    private void WatchItem()
    {
        if (_item is not null && !_watching)
        {
            _item.PropertyChanged += OnItemChanged;
            _watching = true;
        }
    }

    private void Unwatch()
    {
        if (_item is not null && _watching)
        {
            _item.PropertyChanged -= OnItemChanged;
        }
        _watching = false;
    }

    private void OnItemChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MetricTickerItem.ValueProperty)
        {
            Update(animate: true);
        }
        else if (e.Property != MetricTickerItem.ChangeProperty && e.Property != MetricTickerItem.PreviousValueProperty)
        {
            Update(animate: false);
        }
    }

    private void Update(bool animate)
    {
        var item = _item;
        if (item is null)
        {
            _label.Text = null;
            _change.IsVisible = false;
            _arrow.IsVisible = false;
            return;
        }
        _label.Text = item.Label;
        _value.Decimals = item.Decimals;
        _value.Prefix = item.Prefix;
        _value.Suffix = item.Suffix;
        _value.IsInstant = !_shown || !animate;
        _value.Value = item.Value;
        _shown = true;
        var changeText = item.FormatChange();
        _change.Text = changeText;
        _change.IsVisible = changeText is not null;
        _trend = item.Trend;
        var rising = !double.IsNaN(item.Change) && item.Change > 0;
        var falling = !double.IsNaN(item.Change) && item.Change < 0;
        _arrow.IsVisible = changeText is not null && (rising || falling);
        _arrow.Data = rising ? ArcIcons.ArrowUp : ArcIcons.ArrowDown;
        UpdateTrendColors();
        AutomationProperties.SetName(this, MetricTicker.Describe(item));
        if (animate && _trend != 0 && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off && _attached)
        {
            _flash ??= CreateFlash();
            _flash.Jump(1);
            _ = _flash.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(1200), ArcEasings.Exit));
        }
        InvalidateMeasure();
    }

    private MotionValue<double> CreateFlash()
    {
        var flash = new MotionValue<double>(this, 0, MotionVectors.Double);
        flash.Changed += _ => InvalidateVisual();
        return flash;
    }

    private void UpdateTrendColors()
    {
        var brush = _trend switch
        {
            > 0 => UpBrush,
            < 0 => DownBrush,
            _ => FlatBrush
        };
        _change.Foreground = brush;
        _arrow.Foreground = brush;
    }

    private void UpdateFonts()
    {
        _value.FontSize = _stacked ? StackedValueFontSize : ValueFontSize;
        _label.FontSize = _stacked ? 12 : 14;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        UpdateFonts();
        var pad = Padding;
        var inner = availableSize.Deflate(pad);
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        _value.Measure(infinite);
        _arrow.Measure(infinite);
        _change.Measure(infinite);
        var changeWidth = (_arrow.IsVisible ? _arrow.DesiredSize.Width + 2 : 0) + (_change.IsVisible ? _change.DesiredSize.Width : 0);
        var valueRow = _value.DesiredSize.Width + (changeWidth > 0 ? 8 + changeWidth : 0);
        if (_stacked)
        {
            _label.Measure(new Size(inner.Width, double.PositiveInfinity));
            var width = Math.Max(_label.DesiredSize.Width, valueRow);
            var height = _label.DesiredSize.Height + 2 + Math.Max(_value.DesiredSize.Height, _change.DesiredSize.Height);
            return new Size(width + pad.Left + pad.Right, height + pad.Top + pad.Bottom);
        }
        _label.Measure(infinite);
        var lineHeight = Math.Max(_label.DesiredSize.Height, Math.Max(_value.DesiredSize.Height, _change.DesiredSize.Height));
        return new Size(_label.DesiredSize.Width + 8 + valueRow + pad.Left + pad.Right, lineHeight + pad.Top + pad.Bottom);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var pad = Padding;
        var x = pad.Left;
        double y;
        double rowHeight;
        if (_stacked)
        {
            var labelWidth = Math.Max(0, finalSize.Width - pad.Left - pad.Right);
            _label.Arrange(new Rect(x, pad.Top, labelWidth, _label.DesiredSize.Height));
            y = pad.Top + _label.DesiredSize.Height + 2;
            rowHeight = Math.Max(_value.DesiredSize.Height, _change.DesiredSize.Height);
        }
        else
        {
            rowHeight = finalSize.Height - pad.Top - pad.Bottom;
            y = pad.Top;
            _label.Arrange(new Rect(x, y + (rowHeight - _label.DesiredSize.Height) / 2, _label.DesiredSize.Width, _label.DesiredSize.Height));
            x += _label.DesiredSize.Width + 8;
        }
        _value.Arrange(new Rect(x, y + (rowHeight - _value.DesiredSize.Height) / 2, _value.DesiredSize.Width, _value.DesiredSize.Height));
        x += _value.DesiredSize.Width + 8;
        if (_arrow.IsVisible)
        {
            _arrow.Arrange(new Rect(x, y + (rowHeight - _arrow.DesiredSize.Height) / 2, _arrow.DesiredSize.Width, _arrow.DesiredSize.Height));
            x += _arrow.DesiredSize.Width + 2;
        }
        _change.Arrange(new Rect(x, y + (rowHeight - _change.DesiredSize.Height) / 2, _change.DesiredSize.Width, _change.DesiredSize.Height));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var flash = _flash?.Value ?? 0;
        if (flash <= 0.004 || _trend == 0)
        {
            return;
        }
        var tint = _trend > 0 ? UpTint : DownTint;
        if (tint is null)
        {
            return;
        }
        var radius = Math.Min(TintRadius.TopLeft, Bounds.Height / 2);
        using (context.PushOpacity(flash))
        {
            context.DrawRectangle(tint, null, new Rect(Bounds.Size), radius, radius);
        }
    }
}

/// <summary>Shared text settings of the metric ticker.</summary>
internal static class MetricTickerText
{
    /// <summary>Tabular figures, so changing numbers keep their width.</summary>
    public static readonly FontFeatureCollection Tabular = new() { FontFeature.Parse("tnum") };
}
