using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A number of a <see cref="RevenueGlobe"/>'s panels that counts toward its value: each change eases out over about a
/// quarter second (every frame closes 1 − e^(−dt/0.125) of the gap, so +16 shows as 2, 2, 2, 1, …). Plain text with
/// tabular figures, not rolling columns. Screen readers read the final value at once. Reduced motion jumps.
/// </summary>
/// <remarks>
/// Code-driven motion: the value is retargeted many times a second during a replay and must keep easing from where it
/// is; a ControlTheme transition cannot animate formatted text.
/// </remarks>
internal sealed class RevenueGlobeTicker : TextBlock
{
    /// <summary>Time constant of the easing (s).</summary>
    public const double TimeConstant = 0.125;

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<RevenueGlobeTicker, double>(nameof(Value));

    public static readonly StyledProperty<Func<double, string>?> FormatterProperty =
        AvaloniaProperty.Register<RevenueGlobeTicker, Func<double, string>?>(nameof(Formatter));

    private readonly LiveLoop _loop;
    private double _shown;
    private bool _primed;

    public RevenueGlobeTicker()
    {
        _loop = new LiveLoop(this, Step);
        _loop.RunningChanged += running =>
        {
            if (!running && _shown != Value)
            {
                // Out of view or motion turned down: land on the value.
                Show(Value);
            }
        };
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    /// <summary>The value the text counts toward.</summary>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Turns the shown value into text (default: whole number).</summary>
    public Func<double, string>? Formatter { get => GetValue(FormatterProperty); set => SetValue(FormatterProperty, value); }

    /// <summary>The value on screen now (tests).</summary>
    internal double ShownValue => _shown;

    /// <summary>The final text, as screen readers read it.</summary>
    internal string FinalText => Format(Value);

    private string Format(double value) => Formatter?.Invoke(Math.Round(value)) ?? Math.Round(value).ToString("N0");

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            var animate = _primed && IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
            _primed = true;
            if (animate)
            {
                _loop.Start();
                if (!_loop.IsRunning)
                {
                    Show(Value);
                }
            }
            else
            {
                _loop.Stop();
                Show(Value);
            }
        }
        else if (change.Property == FormatterProperty)
        {
            Show(_shown);
        }
    }

    private bool Step(double dt)
    {
        var target = Value;
        var gap = target - _shown;
        if (Math.Abs(gap) < 0.5)
        {
            Show(target);
            return false;
        }
        Show(_shown + gap * (1 - Math.Exp(-Math.Min(dt, 0.1) / TimeConstant)));
        return true;
    }

    private void Show(double value)
    {
        _shown = value;
        var text = Format(value);
        if (text != Text)
        {
            Text = text;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RevenueGlobeTickerAutomationPeer(this);
}

/// <summary>Reads a ticker's final value rather than the number it is counting through.</summary>
internal sealed class RevenueGlobeTickerAutomationPeer : TextBlockAutomationPeer
{
    public RevenueGlobeTickerAutomationPeer(RevenueGlobeTicker owner)
        : base(owner)
    {
    }

    protected override string? GetNameCore() => ((RevenueGlobeTicker)Owner).FinalText;
}

/// <summary>
/// A region of a <see cref="RevenueGlobe"/>'s list (toggle row): name, share of forecast, revenue and a progress bar.
/// Pressing it asks the block to fly there (or back); <see cref="ToggleButton.IsChecked"/> is set by the block only.
/// </summary>
internal sealed class RevenueGlobeRegionRow : ToggleButton
{
    public static readonly StyledProperty<string?> RegionNameProperty =
        AvaloniaProperty.Register<RevenueGlobeRegionRow, string?>(nameof(RegionName));

    public static readonly StyledProperty<string?> ShareTextProperty =
        AvaloniaProperty.Register<RevenueGlobeRegionRow, string?>(nameof(ShareText));

    public static readonly StyledProperty<double> AmountProperty =
        AvaloniaProperty.Register<RevenueGlobeRegionRow, double>(nameof(Amount));

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<RevenueGlobeRegionRow, double>(nameof(Progress));

    public static readonly StyledProperty<Func<double, string>?> AmountFormatterProperty =
        AvaloniaProperty.Register<RevenueGlobeRegionRow, Func<double, string>?>(nameof(AmountFormatter));

    public string? RegionName { get => GetValue(RegionNameProperty); set => SetValue(RegionNameProperty, value); }

    public string? ShareText { get => GetValue(ShareTextProperty); set => SetValue(ShareTextProperty, value); }

    public double Amount { get => GetValue(AmountProperty); set => SetValue(AmountProperty, value); }

    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    public Func<double, string>? AmountFormatter { get => GetValue(AmountFormatterProperty); set => SetValue(AmountFormatterProperty, value); }

    /// <summary>Index of the region in the block.</summary>
    public int Index { get; set; }

    protected override Type StyleKeyOverride => typeof(RevenueGlobeRegionRow);

    // The block owns the pressed state (selection); a click only reports.
    protected override void Toggle()
    {
    }
}

/// <summary>A city of a <see cref="RevenueGlobe"/>'s top-cities list (toggle row): name, country code and revenue.</summary>
internal sealed class RevenueGlobeCityRow : ToggleButton
{
    public static readonly StyledProperty<string?> CityNameProperty =
        AvaloniaProperty.Register<RevenueGlobeCityRow, string?>(nameof(CityName));

    public static readonly StyledProperty<string?> CountryCodeProperty =
        AvaloniaProperty.Register<RevenueGlobeCityRow, string?>(nameof(CountryCode));

    public static readonly StyledProperty<double> AmountProperty =
        AvaloniaProperty.Register<RevenueGlobeCityRow, double>(nameof(Amount));

    public static readonly StyledProperty<Func<double, string>?> AmountFormatterProperty =
        AvaloniaProperty.Register<RevenueGlobeCityRow, Func<double, string>?>(nameof(AmountFormatter));

    public string? CityName { get => GetValue(CityNameProperty); set => SetValue(CityNameProperty, value); }

    public string? CountryCode { get => GetValue(CountryCodeProperty); set => SetValue(CountryCodeProperty, value); }

    public double Amount { get => GetValue(AmountProperty); set => SetValue(AmountProperty, value); }

    public Func<double, string>? AmountFormatter { get => GetValue(AmountFormatterProperty); set => SetValue(AmountFormatterProperty, value); }

    /// <summary>Index of the city in the block's cities (−1 while the row is empty).</summary>
    public int Index { get; set; } = -1;

    protected override Type StyleKeyOverride => typeof(RevenueGlobeCityRow);

    protected override void Toggle()
    {
    }
}

/// <summary>A region's progress against its forecast: a 4px pill track with the filled share drawn over it.</summary>
internal sealed class RevenueGlobeProgress : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<RevenueGlobeProgress, double>(nameof(Value));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<RevenueGlobeProgress, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<RevenueGlobeProgress, IBrush?>(nameof(FillBrush));

    static RevenueGlobeProgress()
    {
        AffectsRender<RevenueGlobeProgress>(ValueProperty, TrackBrushProperty, FillBrushProperty);
    }

    /// <summary>Filled share, 0 … 1 (written every frame, no transition).</summary>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(0, 4);

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var radius = bounds.Height / 2;
        context.DrawRectangle(TrackBrush, null, bounds, radius, radius);
        var share = Math.Clamp(Value, 0, 1);
        if (share > 0 && FillBrush is not null)
        {
            using (context.PushClip(new Rect(0, 0, bounds.Width * share, bounds.Height)))
            {
                context.DrawRectangle(FillBrush, null, bounds, radius, radius);
            }
        }
    }
}

/// <summary>The hover card content of a <see cref="RevenueGlobe"/> city: name, region and local time, then three figures.</summary>
internal sealed class RevenueGlobeTip : TemplatedControl
{
    public static readonly StyledProperty<string?> CityNameProperty =
        AvaloniaProperty.Register<RevenueGlobeTip, string?>(nameof(CityName));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<RevenueGlobeTip, string?>(nameof(Subtitle));

    public static readonly StyledProperty<string?> TodayLabelProperty =
        AvaloniaProperty.Register<RevenueGlobeTip, string?>(nameof(TodayLabel));

    public static readonly StyledProperty<string?> TodayTextProperty =
        AvaloniaProperty.Register<RevenueGlobeTip, string?>(nameof(TodayText));

    public static readonly StyledProperty<string?> PaymentsLabelProperty =
        AvaloniaProperty.Register<RevenueGlobeTip, string?>(nameof(PaymentsLabel));

    public static readonly StyledProperty<string?> PaymentsTextProperty =
        AvaloniaProperty.Register<RevenueGlobeTip, string?>(nameof(PaymentsText));

    public static readonly StyledProperty<string?> ShareLabelProperty =
        AvaloniaProperty.Register<RevenueGlobeTip, string?>(nameof(ShareLabel));

    public static readonly StyledProperty<string?> ShareTextProperty =
        AvaloniaProperty.Register<RevenueGlobeTip, string?>(nameof(ShareText));

    public string? CityName { get => GetValue(CityNameProperty); set => SetValue(CityNameProperty, value); }

    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    public string? TodayLabel { get => GetValue(TodayLabelProperty); set => SetValue(TodayLabelProperty, value); }

    public string? TodayText { get => GetValue(TodayTextProperty); set => SetValue(TodayTextProperty, value); }

    public string? PaymentsLabel { get => GetValue(PaymentsLabelProperty); set => SetValue(PaymentsLabelProperty, value); }

    public string? PaymentsText { get => GetValue(PaymentsTextProperty); set => SetValue(PaymentsTextProperty, value); }

    public string? ShareLabel { get => GetValue(ShareLabelProperty); set => SetValue(ShareLabelProperty, value); }

    public string? ShareText { get => GetValue(ShareTextProperty); set => SetValue(ShareTextProperty, value); }

    protected override Type StyleKeyOverride => typeof(RevenueGlobeTip);
}

/// <summary>How a <see cref="RevenueGlobe"/> lays out for its width.</summary>
internal enum RevenueGlobeLayout
{
    /// <summary>≥ 880: globe and timeline beside a 320px panel.</summary>
    Wide,

    /// <summary>600 – 880: the panel under the globe, in two columns.</summary>
    Stacked,

    /// <summary>&lt; 600: the panel under the globe in one column.</summary>
    Narrow
}

/// <summary>
/// Places the globe, the timeline and the panel of a <see cref="RevenueGlobe"/> for its <see cref="Layout"/>: beside
/// each other when wide (globe height min(580, 0.52·W)), stacked otherwise (0.66·W; narrow min(440, 0.96·W)).
/// Children: globe area, timeline area, panel.
/// </summary>
internal sealed class RevenueGlobeStagePanel : Panel
{
    public static readonly StyledProperty<RevenueGlobeLayout> LayoutProperty =
        AvaloniaProperty.Register<RevenueGlobeStagePanel, RevenueGlobeLayout>(nameof(Layout));

    public static readonly StyledProperty<double> PanelWidthProperty =
        AvaloniaProperty.Register<RevenueGlobeStagePanel, double>(nameof(PanelWidth), 320);

    static RevenueGlobeStagePanel()
    {
        AffectsMeasure<RevenueGlobeStagePanel>(LayoutProperty, PanelWidthProperty);
    }

    public RevenueGlobeLayout Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }

    public double PanelWidth { get => GetValue(PanelWidthProperty); set => SetValue(PanelWidthProperty, value); }

    /// <summary>Height of the globe area for a container <paramref name="width"/>.</summary>
    public static double GlobeHeight(RevenueGlobeLayout layout, double width) => layout switch
    {
        RevenueGlobeLayout.Wide => Math.Min(580, 0.52 * width),
        RevenueGlobeLayout.Stacked => 0.66 * width,
        _ => Math.Min(440, 0.96 * width)
    };

    /// <summary>The layout for a container <paramref name="width"/>.</summary>
    public static RevenueGlobeLayout LayoutFor(double width) =>
        width >= 880 ? RevenueGlobeLayout.Wide : width >= 600 ? RevenueGlobeLayout.Stacked : RevenueGlobeLayout.Narrow;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 880;
        var children = Children;
        if (children.Count < 3)
        {
            return default;
        }
        var globeHeight = GlobeHeight(Layout, width);
        if (Layout == RevenueGlobeLayout.Wide)
        {
            var vizWidth = Math.Max(0, width - PanelWidth);
            children[0].Measure(new Size(vizWidth, globeHeight));
            children[1].Measure(new Size(vizWidth, double.PositiveInfinity));
            children[2].Measure(new Size(PanelWidth, double.PositiveInfinity));
            var left = globeHeight + children[1].DesiredSize.Height;
            return new Size(width, Math.Max(left, children[2].DesiredSize.Height));
        }
        children[0].Measure(new Size(width, globeHeight));
        children[1].Measure(new Size(width, double.PositiveInfinity));
        children[2].Measure(new Size(width, double.PositiveInfinity));
        return new Size(width, globeHeight + children[1].DesiredSize.Height + children[2].DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = Children;
        if (children.Count < 3)
        {
            return finalSize;
        }
        var width = finalSize.Width;
        var globeHeight = GlobeHeight(Layout, width);
        if (Layout == RevenueGlobeLayout.Wide)
        {
            var vizWidth = Math.Max(0, width - PanelWidth);
            children[0].Arrange(new Rect(0, 0, vizWidth, globeHeight));
            children[1].Arrange(new Rect(0, globeHeight, vizWidth, children[1].DesiredSize.Height));
            children[2].Arrange(new Rect(vizWidth, 0, PanelWidth, finalSize.Height));
            return finalSize;
        }
        var timeline = children[1].DesiredSize.Height;
        children[0].Arrange(new Rect(0, 0, width, globeHeight));
        children[1].Arrange(new Rect(0, globeHeight, width, timeline));
        children[2].Arrange(new Rect(0, globeHeight + timeline, width, Math.Max(children[2].DesiredSize.Height, finalSize.Height - globeHeight - timeline)));
        return finalSize;
    }
}

/// <summary>
/// The figures, regions and top cities of a <see cref="RevenueGlobe"/>'s panel: one column, or (with
/// <see cref="TwoColumns"/>) the figures across the top and the two lists side by side.
/// </summary>
internal sealed class RevenueGlobePanelsLayout : Panel
{
    public static readonly StyledProperty<bool> TwoColumnsProperty =
        AvaloniaProperty.Register<RevenueGlobePanelsLayout, bool>(nameof(TwoColumns));

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<RevenueGlobePanelsLayout, double>(nameof(Gap), 20);

    static RevenueGlobePanelsLayout()
    {
        AffectsMeasure<RevenueGlobePanelsLayout>(TwoColumnsProperty, GapProperty);
    }

    public bool TwoColumns { get => GetValue(TwoColumnsProperty); set => SetValue(TwoColumnsProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 640;
        var children = Children;
        if (children.Count < 3)
        {
            return default;
        }
        var gap = Gap;
        if (TwoColumns)
        {
            var column = Math.Max(0, (width - gap) / 2);
            children[0].Measure(new Size(width, double.PositiveInfinity));
            children[1].Measure(new Size(column, double.PositiveInfinity));
            children[2].Measure(new Size(column, double.PositiveInfinity));
            return new Size(width, children[0].DesiredSize.Height + gap +
                                   Math.Max(children[1].DesiredSize.Height, children[2].DesiredSize.Height));
        }
        var height = 0.0;
        for (var i = 0; i < 3; i++)
        {
            children[i].Measure(new Size(width, double.PositiveInfinity));
            height += children[i].DesiredSize.Height + (i > 0 ? gap : 0);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = Children;
        if (children.Count < 3)
        {
            return finalSize;
        }
        var width = finalSize.Width;
        var gap = Gap;
        if (TwoColumns)
        {
            var column = Math.Max(0, (width - gap) / 2);
            var top = children[0].DesiredSize.Height;
            children[0].Arrange(new Rect(0, 0, width, top));
            children[1].Arrange(new Rect(0, top + gap, column, children[1].DesiredSize.Height));
            children[2].Arrange(new Rect(column + gap, top + gap, column, children[2].DesiredSize.Height));
            return finalSize;
        }
        var y = 0.0;
        for (var i = 0; i < 3; i++)
        {
            var h = children[i].DesiredSize.Height;
            children[i].Arrange(new Rect(0, y, width, h));
            y += h + gap;
        }
        return finalSize;
    }
}

/// <summary>
/// A <see cref="RevenueGlobe"/>'s header: the heading (title and description) and the date on one row when they fit
/// with a 24px gap, otherwise the date under the heading (4px apart) and the heading wrapping to the full width.
/// Children: heading, date.
/// </summary>
internal sealed class RevenueGlobeHeaderPanel : Panel
{
    private const double ColumnGap = 24;
    private const double RowGap = 4;
    private bool _wrapped;

    /// <summary>The date sits under the heading.</summary>
    public bool IsWrapped => _wrapped;

    protected override Size MeasureOverride(Size availableSize)
    {
        var children = Children;
        if (children.Count < 2)
        {
            return default;
        }
        var width = availableSize.Width;
        children[1].Measure(Size.Infinity);
        children[0].Measure(Size.Infinity);
        var heading = children[0].DesiredSize;
        var date = children[1].DesiredSize;
        _wrapped = double.IsFinite(width) && heading.Width + ColumnGap + date.Width > width;
        if (!_wrapped)
        {
            return new Size(heading.Width + ColumnGap + date.Width, Math.Max(heading.Height, date.Height));
        }
        children[0].Measure(new Size(width, double.PositiveInfinity));
        children[1].Measure(new Size(width, double.PositiveInfinity));
        heading = children[0].DesiredSize;
        date = children[1].DesiredSize;
        var gap = date.Height > 0 ? RowGap : 0;
        return new Size(Math.Max(heading.Width, date.Width), heading.Height + gap + date.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = Children;
        if (children.Count < 2)
        {
            return finalSize;
        }
        var heading = children[0].DesiredSize;
        var date = children[1].DesiredSize;
        if (!_wrapped)
        {
            children[0].Arrange(new Rect(0, (finalSize.Height - heading.Height) / 2, Math.Max(0, finalSize.Width - date.Width - ColumnGap), heading.Height));
            children[1].Arrange(new Rect(finalSize.Width - date.Width, (finalSize.Height - date.Height) / 2, date.Width, date.Height));
            return finalSize;
        }
        children[0].Arrange(new Rect(0, 0, finalSize.Width, heading.Height));
        var gap = date.Height > 0 ? RowGap : 0;
        children[1].Arrange(new Rect(0, heading.Height + gap, date.Width, date.Height));
        return finalSize;
    }
}

/// <summary>
/// The control row over the timeline: Replay and Pause on the left, the clock and the Live pill on the right, 8px apart;
/// with <see cref="ClockOnOwnRow"/> (below 400) the clock takes a row of its own above the buttons.
/// Children: replay, pause, clock, live.
/// </summary>
internal sealed class RevenueGlobeControlsPanel : Panel
{
    public static readonly StyledProperty<bool> ClockOnOwnRowProperty =
        AvaloniaProperty.Register<RevenueGlobeControlsPanel, bool>(nameof(ClockOnOwnRow));

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<RevenueGlobeControlsPanel, double>(nameof(Gap), 8);

    static RevenueGlobeControlsPanel()
    {
        AffectsMeasure<RevenueGlobeControlsPanel>(ClockOnOwnRowProperty, GapProperty);
    }

    public bool ClockOnOwnRow { get => GetValue(ClockOnOwnRowProperty); set => SetValue(ClockOnOwnRowProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var children = Children;
        if (children.Count < 4)
        {
            return default;
        }
        var row = 0.0;
        foreach (var child in children)
        {
            child.Measure(Size.Infinity);
        }
        row = Math.Max(row, Math.Max(children[0].DesiredSize.Height, Math.Max(children[1].DesiredSize.Height, children[3].DesiredSize.Height)));
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        if (ClockOnOwnRow)
        {
            return new Size(width, children[2].DesiredSize.Height + Gap + row);
        }
        return new Size(width, Math.Max(row, children[2].DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = Children;
        if (children.Count < 4)
        {
            return finalSize;
        }
        var gap = Gap;
        var top = 0.0;
        var clock = children[2].DesiredSize;
        var rowHeight = finalSize.Height;
        if (ClockOnOwnRow)
        {
            children[2].Arrange(new Rect(0, 0, clock.Width, clock.Height));
            top = clock.Height + gap;
            rowHeight = finalSize.Height - top;
        }
        var x = 0.0;
        for (var i = 0; i < 2; i++)
        {
            var size = children[i].DesiredSize;
            if (!children[i].IsVisible)
            {
                children[i].Arrange(default);
                continue;
            }
            children[i].Arrange(new Rect(x, top + (rowHeight - size.Height) / 2, size.Width, size.Height));
            x += size.Width + gap;
        }
        var live = children[3].DesiredSize;
        if (ClockOnOwnRow)
        {
            children[3].Arrange(new Rect(x, top + (rowHeight - live.Height) / 2, live.Width, live.Height));
        }
        else
        {
            var liveX = finalSize.Width - live.Width;
            children[3].Arrange(new Rect(liveX, top + (rowHeight - live.Height) / 2, live.Width, live.Height));
            children[2].Arrange(new Rect(liveX - gap - clock.Width, top + (rowHeight - clock.Height) / 2, clock.Width, clock.Height));
        }
        return finalSize;
    }
}

/// <summary>Spreads its children over the width with equal space between them (the hour marks under the timeline).</summary>
internal sealed class RevenueGlobeSpreadPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var height = 0.0;
        var width = 0.0;
        foreach (var child in Children)
        {
            child.Measure(Size.Infinity);
            height = Math.Max(height, child.DesiredSize.Height);
            width += child.DesiredSize.Width;
        }
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = Children;
        if (children.Count == 0)
        {
            return finalSize;
        }
        var total = 0.0;
        foreach (var child in children)
        {
            total += child.DesiredSize.Width;
        }
        var space = children.Count > 1 ? Math.Max(0, (finalSize.Width - total) / (children.Count - 1)) : 0;
        var x = 0.0;
        foreach (var child in children)
        {
            child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
            x += child.DesiredSize.Width + space;
        }
        return finalSize;
    }
}
