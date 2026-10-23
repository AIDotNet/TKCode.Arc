using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Where a value of a <see cref="ConfidenceBars"/> strip stands; it decides the bar's color.</summary>
internal enum ConfidenceBarState
{
    /// <summary>No value yet: a 3px stub in the border color.</summary>
    Pending,

    /// <summary>A value the model is sure of: 55% of the success color over the surface.</summary>
    Ok,

    /// <summary>A value that needs a check: the warning color.</summary>
    Check,

    /// <summary>An accepted value: the success color.</summary>
    Accepted
}

/// <summary>One bar of a <see cref="ConfidenceBars"/> strip.</summary>
/// <param name="Confidence">0 to 1; the bar's height. Null with a state other than <see cref="ConfidenceBarState.Pending"/> is a full bar.</param>
/// <param name="State">The bar's color.</param>
internal sealed record ConfidenceBarItem(double? Confidence, ConfidenceBarState State);

/// <summary>
/// The footer's mini bars of a document extraction (Arc document extraction): one 4px bar per field in an 18px strip, 3px
/// apart and standing on the bottom, radius 2. A pending field is a 3px stub in the border color; a value stands
/// <c>18 − 26 × (1 − confidence)</c> high (99% → 17.7, 64% → 8.6, at least 3), in 55% success over the surface when the model
/// is sure, the warning color when it needs a check and the success color once accepted. A new list with as many items
/// moves each bar: the height on <see cref="ArcSpring.Smooth"/> (about 580ms, rising and falling), the color in 160ms;
/// a list of another length starts over without motion. Staggering (accept) is the owner's: it changes one state after the
/// other. Reduced motion and Off jump. Decorative: it is not in the automation tree (the summary next to it says it).
/// </summary>
[TemplatePart(StripPart, typeof(ConfidenceBarsStrip))]
internal sealed class ConfidenceBars : TemplatedControl
{
    public const string StripPart = "PART_Strip";

    /// <summary>The bars, one per field.</summary>
    public static readonly StyledProperty<IReadOnlyList<ConfidenceBarItem>?> ItemsProperty =
        AvaloniaProperty.Register<ConfidenceBars, IReadOnlyList<ConfidenceBarItem>?>(nameof(Items));

    /// <summary>The success color (accepted bars, and mixed over the surface for sure ones).</summary>
    public static readonly StyledProperty<IBrush?> SuccessBrushProperty =
        AvaloniaProperty.Register<ConfidenceBars, IBrush?>(nameof(SuccessBrush));

    /// <summary>The warning color (bars that need a check).</summary>
    public static readonly StyledProperty<IBrush?> WarningBrushProperty =
        AvaloniaProperty.Register<ConfidenceBars, IBrush?>(nameof(WarningBrush));

    /// <summary>The pending stubs' color (the border color).</summary>
    public static readonly StyledProperty<IBrush?> PendingBrushProperty =
        AvaloniaProperty.Register<ConfidenceBars, IBrush?>(nameof(PendingBrush));

    /// <summary>The surface the sure bars' color is mixed over.</summary>
    public static readonly StyledProperty<IBrush?> SurfaceBrushProperty =
        AvaloniaProperty.Register<ConfidenceBars, IBrush?>(nameof(SurfaceBrush));

    private ConfidenceBarsStrip? _strip;

    static ConfidenceBars()
    {
        FocusableProperty.OverrideDefaultValue<ConfidenceBars>(false);
        IsTabStopProperty.OverrideDefaultValue<ConfidenceBars>(false);
        IsHitTestVisibleProperty.OverrideDefaultValue<ConfidenceBars>(false);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<ConfidenceBars>(AccessibilityView.Raw);
    }

    /// <inheritdoc cref="ItemsProperty"/>
    public IReadOnlyList<ConfidenceBarItem>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <inheritdoc cref="SuccessBrushProperty"/>
    public IBrush? SuccessBrush { get => GetValue(SuccessBrushProperty); set => SetValue(SuccessBrushProperty, value); }

    /// <inheritdoc cref="WarningBrushProperty"/>
    public IBrush? WarningBrush { get => GetValue(WarningBrushProperty); set => SetValue(WarningBrushProperty, value); }

    /// <inheritdoc cref="PendingBrushProperty"/>
    public IBrush? PendingBrush { get => GetValue(PendingBrushProperty); set => SetValue(PendingBrushProperty, value); }

    /// <inheritdoc cref="SurfaceBrushProperty"/>
    public IBrush? SurfaceBrush { get => GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }

    /// <summary>The strip that draws the bars (template part).</summary>
    internal ConfidenceBarsStrip? Strip => _strip;

    /// <summary>The height a bar stands at for <paramref name="item"/>.</summary>
    internal static double HeightOf(ConfidenceBarItem item)
    {
        if (item.State == ConfidenceBarState.Pending)
        {
            return ConfidenceBarsStrip.StubHeight;
        }
        if (item.Confidence is not { } confidence || double.IsNaN(confidence))
        {
            return ConfidenceBarsStrip.StripHeight;
        }
        var height = ConfidenceBarsStrip.StripHeight - 26 * (1 - Math.Clamp(confidence, 0, 1));
        return Math.Clamp(height, ConfidenceBarsStrip.StubHeight, ConfidenceBarsStrip.StripHeight);
    }

    /// <summary>The color of a bar in <paramref name="state"/> with the current brushes.</summary>
    internal Color ColorOf(ConfidenceBarState state) => state switch
    {
        ConfidenceBarState.Ok => Oklch.MixOklab(ColorOfBrush(SurfaceBrush), ColorOfBrush(SuccessBrush), 0.55),
        ConfidenceBarState.Check => ColorOfBrush(WarningBrush),
        ConfidenceBarState.Accepted => ColorOfBrush(SuccessBrush),
        _ => ColorOfBrush(PendingBrush)
    };

    private static Color ColorOfBrush(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _strip = e.NameScope.Find<ConfidenceBarsStrip>(StripPart);
        _strip?.Rebuild(this, Items);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty)
        {
            _strip?.Update(this, Items);
        }
        else if (change.Property == SuccessBrushProperty || change.Property == WarningBrushProperty
                 || change.Property == PendingBrushProperty || change.Property == SurfaceBrushProperty)
        {
            // A theme change recolors at once.
            _strip?.Recolor(this);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new BarsPeer(this);

    /// <summary>Decorative: neither content nor control, and no children.</summary>
    private sealed class BarsPeer(ConfidenceBars owner) : ControlAutomationPeer(owner)
    {
        protected override bool IsContentElementCore() => false;

        protected override bool IsControlElementCore() => false;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>
/// Draws the bars of a <see cref="ConfidenceBars"/>: each bar has a height and a color value of its own and a brush that is
/// recolored in place, so a moving bar only repaints the strip.
/// </summary>
internal sealed class ConfidenceBarsStrip : Control
{
    /// <summary>The strip's height and the tallest bar.</summary>
    public const double StripHeight = 18;

    /// <summary>A pending bar's height and the shortest bar.</summary>
    public const double StubHeight = 3;

    /// <summary>A bar's width.</summary>
    public const double BarWidth = 4;

    /// <summary>The gap between bars.</summary>
    public const double Gap = 3;

    /// <summary>A bar's corner radius.</summary>
    public const double Radius = 2;

    private readonly List<Bar> _bars = new();

    static ConfidenceBarsStrip()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<ConfidenceBarsStrip>(false);
        FocusableProperty.OverrideDefaultValue<ConfidenceBarsStrip>(false);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<ConfidenceBarsStrip>(AccessibilityView.Raw);
    }

    /// <summary>How many bars there are.</summary>
    internal int Count => _bars.Count;

    /// <summary>The height bar <paramref name="index"/> is drawn at now.</summary>
    internal double HeightAt(int index) => _bars[index].Height.Value;

    /// <summary>The color bar <paramref name="index"/> is drawn in now.</summary>
    internal Color ColorAt(int index) => _bars[index].Brush.Color;

    /// <summary>The state bar <paramref name="index"/> is moving to.</summary>
    internal ConfidenceBarState StateAt(int index) => _bars[index].State;

    /// <summary>Starts over with <paramref name="items"/>: new bars at their values, no motion.</summary>
    internal void Rebuild(ConfidenceBars owner, IReadOnlyList<ConfidenceBarItem>? items)
    {
        foreach (var bar in _bars)
        {
            bar.Dispose();
        }
        _bars.Clear();
        if (items is not null)
        {
            foreach (var item in items)
            {
                var bar = new Bar(this, ConfidenceBars.HeightOf(item), owner.ColorOf(item.State)) { State = item.State };
                _bars.Add(bar);
            }
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Moves every bar to its item: heights on the Smooth spring, colors in 160ms (Reduced and Off jump). Another length rebuilds.</summary>
    internal void Update(ConfidenceBars owner, IReadOnlyList<ConfidenceBarItem>? items)
    {
        if (items is null || items.Count != _bars.Count)
        {
            Rebuild(owner, items);
            return;
        }
        var full = ArcMotion.IsFull(this);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var bar = _bars[i];
            var height = ConfidenceBars.HeightOf(item);
            if (height != bar.Height.Target)
            {
                _ = bar.Height.AnimateTo(height, MotionSpec.Spring(ArcSpring.Smooth));
            }
            bar.State = item.State;
            var color = owner.ColorOf(item.State);
            if (color != bar.Color.Target)
            {
                if (full)
                {
                    _ = bar.Color.AnimateTo(color, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
                }
                else
                {
                    bar.Color.Jump(color);
                }
            }
        }
    }

    /// <summary>The theme changed: every bar takes its state's new color at once.</summary>
    internal void Recolor(ConfidenceBars owner)
    {
        foreach (var bar in _bars)
        {
            bar.Color.Jump(owner.ColorOf(bar.State));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = _bars.Count;
        return new Size(count == 0 ? 0 : count * BarWidth + (count - 1) * Gap, StripHeight);
    }

    public override void Render(DrawingContext context)
    {
        // Standing on the bottom of an 18px strip (centered when the strip was given more room).
        var bottom = Bounds.Height > StripHeight ? (Bounds.Height + StripHeight) / 2 : StripHeight;
        for (var i = 0; i < _bars.Count; i++)
        {
            var bar = _bars[i];
            var height = Math.Clamp(bar.Height.Value, 0, StripHeight + 4);
            if (height <= 0.01)
            {
                continue;
            }
            var rect = new Rect(i * (BarWidth + Gap), bottom - height, BarWidth, height);
            var radius = Math.Min(Radius, height / 2);
            context.DrawRectangle(bar.Brush, null, new RoundedRect(rect, radius));
        }
    }

    /// <summary>One bar: its height and color values and the brush it is drawn with.</summary>
    private sealed class Bar : IDisposable
    {
        public Bar(ConfidenceBarsStrip strip, double height, Color color)
        {
            Brush = new SolidColorBrush(color);
            Height = new MotionValue<double>(strip, height, MotionVectors.Double);
            Color = new MotionValue<Color>(strip, color, MotionVectors.Color);
            Height.Changed += _ => strip.InvalidateVisual();
            Color.Changed += value =>
            {
                Brush.Color = value;
                strip.InvalidateVisual();
            };
        }

        public SolidColorBrush Brush { get; }

        public MotionValue<double> Height { get; }

        public MotionValue<Color> Color { get; }

        public ConfidenceBarState State { get; set; }

        public void Dispose()
        {
            Height.Dispose();
            Color.Dispose();
        }
    }
}
