using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>Which token fills an <see cref="AnalyticsShareBar"/>.</summary>
internal enum AnalyticsBarColor
{
    /// <summary>Accent.</summary>
    Accent,

    /// <summary>Success (growth, positive values).</summary>
    Success,

    /// <summary>Danger (losses, negative values).</summary>
    Danger,

    /// <summary>TextMuted (neutral, no data).</summary>
    Muted,

    /// <summary><see cref="AnalyticsShareBar.CustomColor"/>.</summary>
    Custom
}

/// <summary>Which token draws the track behind an <see cref="AnalyticsShareBar"/>.</summary>
internal enum AnalyticsBarTrack
{
    /// <summary>No track (Metrics dashboard background bars).</summary>
    None,

    /// <summary>SurfaceMuted (KPI drilldown 6px, Journey flow 3px, Choropleth 3px).</summary>
    SurfaceMuted,

    /// <summary>BorderSubtle (MRR waterfall 2px, doubles as the row divider).</summary>
    BorderSubtle
}

/// <summary>How an <see cref="AnalyticsShareBar"/> moves to a new share.</summary>
internal enum AnalyticsBarMotion
{
    /// <summary><see cref="AnalyticsShareBar.Spring"/> (default Smooth: KPI drilldown, MRR waterfall, Journey flow).</summary>
    Spring,

    /// <summary>480ms (.22, 1, .36, 1) (Metrics dashboard's clip-path transition).</summary>
    Tween,

    /// <summary>Arc's 580ms CSS spring curve (Choropleth rank bars).</summary>
    CssSpring
}

/// <summary>
/// A horizontal share bar: a track and a fill drawn from the left at <see cref="Value"/> (0–1) of the width. The fill is
/// drawn at its width (Arc scales it, which squashes the rounded ends; we keep the ends round). Used for ranked list
/// backgrounds (Metrics dashboard: full row, radius 6, Accent 8% in Surface, no track), thin share bars (KPI drilldown 6px
/// pill on SurfaceMuted; MRR waterfall 2px r1 on BorderSubtle; Journey flow / Choropleth 3px pill on SurfaceMuted) and rank
/// bars (<see cref="MinimumFraction"/> 0.015 on Choropleth). The first value shows at once; reduced motion and Off jump.
/// </summary>
internal sealed class AnalyticsShareBar : Control
{
    /// <summary>Share of the width (0–1).</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<AnalyticsShareBar, double>(nameof(Value));

    /// <summary>Smallest drawn share for a non-zero value (Choropleth 0.015).</summary>
    public static readonly StyledProperty<double> MinimumFractionProperty =
        AvaloniaProperty.Register<AnalyticsShareBar, double>(nameof(MinimumFraction));

    /// <summary>Fill token.</summary>
    public static readonly StyledProperty<AnalyticsBarColor> FillColorProperty =
        AvaloniaProperty.Register<AnalyticsShareBar, AnalyticsBarColor>(nameof(FillColor));

    /// <summary>Fill colour for <see cref="AnalyticsBarColor.Custom"/> (a series colour).</summary>
    public static readonly StyledProperty<Color> CustomColorProperty =
        AvaloniaProperty.Register<AnalyticsShareBar, Color>(nameof(CustomColor));

    /// <summary>Fill weight mixed into Surface in OKLab (1 = pure; Metrics dashboard 0.08).</summary>
    public static readonly StyledProperty<double> FillWeightProperty =
        AvaloniaProperty.Register<AnalyticsShareBar, double>(nameof(FillWeight), 1);

    /// <summary>Track token.</summary>
    public static readonly StyledProperty<AnalyticsBarTrack> TrackProperty =
        AvaloniaProperty.Register<AnalyticsShareBar, AnalyticsBarTrack>(nameof(Track), AnalyticsBarTrack.SurfaceMuted);

    /// <summary>Corner radius; NaN (default) = pill (half the height).</summary>
    public static readonly StyledProperty<double> RadiusProperty =
        AvaloniaProperty.Register<AnalyticsShareBar, double>(nameof(Radius), double.NaN);

    /// <summary>Motion kind.</summary>
    public static readonly StyledProperty<AnalyticsBarMotion> MotionProperty =
        AvaloniaProperty.Register<AnalyticsShareBar, AnalyticsBarMotion>(nameof(Motion));

    private readonly AnalyticsPalette _palette = new();
    private readonly LiveLoop _loop;
    private AnalyticsSpringChannel _spring = new(0, 0.0005, 0.005);
    private AnalyticsTween _tween = new(0);
    private bool _hasValue;
    private bool _usesTween;

    static AnalyticsShareBar()
    {
        AffectsRender<AnalyticsShareBar>(FillColorProperty, CustomColorProperty, FillWeightProperty, TrackProperty, RadiusProperty, MinimumFractionProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<AnalyticsShareBar>(false);
    }

    /// <summary>Creates the bar.</summary>
    public AnalyticsShareBar()
    {
        _loop = new LiveLoop(this, Advance);
        ActualThemeVariantChanged += (_, _) => Invalidate();
        ResourcesChanged += (_, _) => Invalidate();
    }

    /// <inheritdoc cref="ValueProperty"/>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="MinimumFractionProperty"/>
    public double MinimumFraction { get => GetValue(MinimumFractionProperty); set => SetValue(MinimumFractionProperty, value); }

    /// <inheritdoc cref="FillColorProperty"/>
    public AnalyticsBarColor FillColor { get => GetValue(FillColorProperty); set => SetValue(FillColorProperty, value); }

    /// <inheritdoc cref="CustomColorProperty"/>
    public Color CustomColor { get => GetValue(CustomColorProperty); set => SetValue(CustomColorProperty, value); }

    /// <inheritdoc cref="FillWeightProperty"/>
    public double FillWeight { get => GetValue(FillWeightProperty); set => SetValue(FillWeightProperty, value); }

    /// <inheritdoc cref="TrackProperty"/>
    public AnalyticsBarTrack Track { get => GetValue(TrackProperty); set => SetValue(TrackProperty, value); }

    /// <inheritdoc cref="RadiusProperty"/>
    public double Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <inheritdoc cref="MotionProperty"/>
    public AnalyticsBarMotion Motion { get => GetValue(MotionProperty); set => SetValue(MotionProperty, value); }

    /// <summary>Spring for <see cref="AnalyticsBarMotion.Spring"/> (Smooth).</summary>
    public SpringSpec Spring { get; set; } = AnalyticsSprings.Smooth;

    /// <summary>The share drawn now.</summary>
    public double DisplayValue => _usesTween ? _tween.Value : _spring.Value;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            MoveTo(Math.Clamp(double.IsFinite(Value) ? Value : 0, 0, 1));
        }
    }

    private void MoveTo(double target)
    {
        var animate = _hasValue && IsLoaded && ArcMotion.IsFull(this);
        _hasValue = true;
        var current = DisplayValue;
        if (!animate)
        {
            _spring.Jump(target);
            _tween.Jump(target);
            _loop.Stop();
            InvalidateVisual();
            return;
        }
        _usesTween = Motion != AnalyticsBarMotion.Spring;
        if (_usesTween)
        {
            _tween.Jump(current);
            var css = Motion == AnalyticsBarMotion.CssSpring;
            _tween.Start(target, css ? ArcMotionPresets.CssSpring : ArcMotionPresets.Considered, css ? AnalyticsEasings.CssSpring : AnalyticsEasings.Standard);
        }
        else
        {
            if (Math.Abs(_spring.Value - current) > 1e-9)
            {
                _spring.Jump(current);
            }
            _spring.SpringTo(target, Spring);
        }
        _loop.Start();
    }

    private bool Advance(double dt)
    {
        var moving = _usesTween ? _tween.Advance(dt) : _spring.Step(dt);
        InvalidateVisual();
        return moving;
    }

    private void Invalidate()
    {
        if (_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        var radius = double.IsNaN(Radius) ? bounds.Height / 2 : Math.Min(Radius, bounds.Height / 2);
        if (Track != AnalyticsBarTrack.None)
        {
            var track = Track == AnalyticsBarTrack.BorderSubtle ? _palette.BorderSubtle : _palette.SurfaceMuted;
            context.DrawRectangle(_palette.Brush(track), null, new RoundedRect(bounds, radius));
        }
        var share = DisplayValue;
        if (share > 0 && MinimumFraction > 0)
        {
            share = Math.Max(share, MinimumFraction);
        }
        var width = Math.Clamp(share, 0, 1) * bounds.Width;
        if (width <= 0.01)
        {
            return;
        }
        var color = FillColor switch
        {
            AnalyticsBarColor.Success => _palette.Success,
            AnalyticsBarColor.Danger => _palette.Danger,
            AnalyticsBarColor.Muted => _palette.TextMuted,
            AnalyticsBarColor.Custom => CustomColor,
            _ => _palette.Accent
        };
        if (FillWeight < 1)
        {
            color = AnalyticsColors.Mix(color, FillWeight, _palette.Surface);
        }
        var fill = new Rect(0, 0, width, bounds.Height);
        context.DrawRectangle(_palette.Brush(color), null, new RoundedRect(fill, Math.Min(radius, width / 2)));
    }
}

/// <summary>
/// Metrics dashboard's ranked list row: a 30px row whose background is a share bar (radius 6, Accent 8% in Surface,
/// width = value / first row's value), the name (14 Foreground, one line with an ellipsis) on the left and the value
/// (14 TextSecondary tabular, grouped, counting 480ms) on the right, 8px from both ends. Rows are not interactive. Stack
/// rows 2px apart. Screen readers read "name, value".
/// </summary>
internal sealed class AnalyticsRankedRow : Control
{
    /// <summary>Row name.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<AnalyticsRankedRow, string?>(nameof(Label));

    /// <summary>Row value.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<AnalyticsRankedRow, double>(nameof(Value));

    /// <summary>Bar share (value / largest value).</summary>
    public static readonly StyledProperty<double> ShareProperty =
        AvaloniaProperty.Register<AnalyticsRankedRow, double>(nameof(Share));

    /// <summary>Value format (grouped integer).</summary>
    public static readonly StyledProperty<AnalyticsNumberFormat> FormatProperty =
        AvaloniaProperty.Register<AnalyticsRankedRow, AnalyticsNumberFormat>(nameof(Format), AnalyticsNumberFormat.Integer);

    private readonly AnalyticsShareBar _bar = new()
    {
        Radius = 6, FillWeight = 0.08, Track = AnalyticsBarTrack.None, Motion = AnalyticsBarMotion.Tween
    };

    private readonly TextBlock _name = new()
    {
        TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 12, 0)
    };

    private readonly AnalyticsCountUp _value = new()
    {
        VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 8, 0)
    };

    private readonly Grid _grid = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto") };

    /// <summary>Creates the row.</summary>
    public AnalyticsRankedRow()
    {
        Grid.SetColumnSpan(_bar, 2);
        Grid.SetColumn(_value, 1);
        _grid.Children.Add(_bar);
        _grid.Children.Add(_name);
        _grid.Children.Add(_value);
        VisualChildren.Add(_grid);
        LogicalChildren.Add(_grid);
        ArcTokens.Bind(_name, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_name, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_value, AnalyticsCountUp.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(_value, AnalyticsCountUp.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        Height = 30;
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="ValueProperty"/>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="ShareProperty"/>
    public double Share { get => GetValue(ShareProperty); set => SetValue(ShareProperty, value); }

    /// <inheritdoc cref="FormatProperty"/>
    public AnalyticsNumberFormat Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <summary>The background bar.</summary>
    public AnalyticsShareBar Bar => _bar;

    /// <summary>The value text.</summary>
    public AnalyticsCountUp ValueText => _value;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            _name.Text = Label;
            ToolTip.SetTip(_name, Label);
        }
        else if (change.Property == ValueProperty)
        {
            _value.Value = Value;
        }
        else if (change.Property == ShareProperty)
        {
            _bar.Value = Share;
        }
        else if (change.Property == FormatProperty)
        {
            _value.Format = Format;
        }
        if (change.Property == LabelProperty || change.Property == ValueProperty || change.Property == FormatProperty)
        {
            AutomationProperties.SetName(this, $"{Label}, {Format.Format(Value)}");
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _grid.Measure(availableSize);
        return _grid.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _grid.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>
/// A content veil: a Surface-coloured layer (70% by default) that fades over content behind a detail panel (KPI
/// drilldown: 240ms ease-out in and out; reduced motion 120ms). It does not take clicks away from the panel above it but
/// does block the content under it; it has no meaning for screen readers.
/// </summary>
internal sealed class AnalyticsVeil : Control
{
    /// <summary>Shown or hidden.</summary>
    public static readonly StyledProperty<bool> IsShownProperty =
        AvaloniaProperty.Register<AnalyticsVeil, bool>(nameof(IsShown));

    /// <summary>Opacity of the Surface fill when shown (0.7).</summary>
    public static readonly StyledProperty<double> StrengthProperty =
        AvaloniaProperty.Register<AnalyticsVeil, double>(nameof(Strength), 0.7);

    /// <summary>Corner radius of the veil (KPI drilldown: 0 0 16 16).</summary>
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<AnalyticsVeil, CornerRadius>(nameof(CornerRadius));

    private readonly AnalyticsPalette _palette = new();
    private readonly LiveLoop _loop;
    private AnalyticsTween _fade = new(0);

    static AnalyticsVeil()
    {
        AffectsRender<AnalyticsVeil>(StrengthProperty, CornerRadiusProperty);
    }

    /// <summary>Creates the veil.</summary>
    public AnalyticsVeil()
    {
        _loop = new LiveLoop(this, dt =>
        {
            var running = _fade.Advance(dt);
            Sync();
            return running;
        }) { MinimumLevel = ArcMotionLevel.Reduced };
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        ActualThemeVariantChanged += (_, _) =>
        {
            _palette.Refresh(this);
            InvalidateVisual();
        };
        Sync();
    }

    /// <inheritdoc cref="IsShownProperty"/>
    public bool IsShown { get => GetValue(IsShownProperty); set => SetValue(IsShownProperty, value); }

    /// <inheritdoc cref="StrengthProperty"/>
    public double Strength { get => GetValue(StrengthProperty); set => SetValue(StrengthProperty, value); }

    /// <inheritdoc cref="CornerRadiusProperty"/>
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>The fade now (0–1).</summary>
    public double Progress => _fade.Value;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != IsShownProperty)
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        var target = IsShown ? 1 : 0;
        if (!IsLoaded || level == ArcMotionLevel.Off)
        {
            _fade.Jump(target);
            _loop.Stop();
        }
        else
        {
            _fade.Start(target, level == ArcMotionLevel.Full ? ArcMotionPresets.Standard : ArcMotionPresets.Instant, AnalyticsEasings.EaseOut);
            _loop.Start();
        }
        Sync();
    }

    private void Sync()
    {
        IsHitTestVisible = _fade.Value > 0.001 || IsShown;
        IsVisible = IsHitTestVisible;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        var alpha = Strength * Math.Clamp(_fade.Value, 0, 1);
        if (alpha <= 0)
        {
            return;
        }
        var brush = _palette.Brush(_palette.Surface, alpha);
        var r = CornerRadius;
        var rect = new Rect(Bounds.Size);
        context.DrawRectangle(brush, null, new RoundedRect(rect, r.TopLeft, r.TopRight, r.BottomRight, r.BottomLeft));
    }
}
