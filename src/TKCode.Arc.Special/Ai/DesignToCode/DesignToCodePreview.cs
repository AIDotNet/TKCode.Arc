using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The output pane's two layers, Code and Preview: both stay mounted, so switching costs nothing and keeps each scroll
/// position. Switching crossfades them (the old layer out in 100ms, the new one in over 160ms); the hidden layer is then
/// made invisible, which also takes it out of the tab order and away from assistive tech (inert). Reduced motion swaps
/// with a short crossfade, Off at once.
/// </summary>
/// <remarks>
/// Code-driven (<see cref="MotionValue{T}"/>) because the two layers need different durations in and out and the
/// outgoing one must turn invisible once its fade ends, which a ControlTheme transition cannot sequence.
/// </remarks>
internal sealed class DesignToCodeLayers : Panel
{
    private static readonly TimeSpan s_out = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan s_in = ArcMotionPresets.Fast;
    private MotionValue<double>? _codeFade;
    private MotionValue<double>? _previewFade;
    private bool _showsPreview;
    private bool _initialized;

    /// <summary>Whether the preview layer (the second child) is the one shown.</summary>
    public bool ShowsPreview => _showsPreview;

    private Control? CodeLayer => Children.Count > 0 ? Children[0] : null;

    private Control? PreviewLayer => Children.Count > 1 ? Children[1] : null;

    /// <summary>Shows the code (false) or the preview (true).</summary>
    public void Show(bool preview, bool animate)
    {
        if (_initialized && preview == _showsPreview)
        {
            return;
        }
        _initialized = true;
        _showsPreview = preview;
        var incoming = preview ? PreviewLayer : CodeLayer;
        var outgoing = preview ? CodeLayer : PreviewLayer;
        if (incoming is null || outgoing is null)
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!animate || level == ArcMotionLevel.Off || !this.IsAttachedToVisualTree())
        {
            Fade(preview ? Preview() : Code(), incoming, 1, TimeSpan.Zero, instant: true);
            Fade(preview ? Code() : Preview(), outgoing, 0, TimeSpan.Zero, instant: true);
            return;
        }
        var reduced = level == ArcMotionLevel.Reduced;
        Fade(preview ? Preview() : Code(), incoming, 1, reduced ? ArcMotionPresets.Instant : s_in, instant: false);
        Fade(preview ? Code() : Preview(), outgoing, 0, reduced ? ArcMotionPresets.Instant : s_out, instant: false);
    }

    private MotionValue<double> Code() => _codeFade ??= Make(CodeLayer!);

    private MotionValue<double> Preview() => _previewFade ??= Make(PreviewLayer!);

    private MotionValue<double> Make(Control layer)
    {
        var value = new MotionValue<double>(this, layer.Opacity, MotionVectors.Double);
        value.Changed += v => layer.Opacity = v;
        return value;
    }

    private static void Fade(MotionValue<double> value, Control layer, double target, TimeSpan duration, bool instant)
    {
        var showing = target > 0;
        layer.IsHitTestVisible = showing;
        if (showing)
        {
            layer.IsVisible = true;
        }
        if (instant)
        {
            value.Jump(target);
            layer.Opacity = target;
            layer.IsVisible = showing;
            return;
        }
        _ = Run();

        async Task Run()
        {
            var done = await value.AnimateTo(target, MotionSpec.Tween(duration, ArcEasings.Standard));
            if (done && !showing)
            {
                layer.IsVisible = false;
            }
        }
    }
}

/// <summary>
/// The card's body: the design pane and the output pane side by side (the design pane at a fixed width, the output the
/// rest), or stacked, each pane at the same fixed height, so the card never changes size while code streams.
/// </summary>
internal sealed class DesignToCodeSplit : Panel
{
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<DesignToCodeSplit, bool>(nameof(IsStacked));

    public static readonly StyledProperty<double> DesignWidthProperty =
        AvaloniaProperty.Register<DesignToCodeSplit, double>(nameof(DesignWidth), 344);

    public static readonly StyledProperty<double> PaneHeightProperty =
        AvaloniaProperty.Register<DesignToCodeSplit, double>(nameof(PaneHeight), 352);

    static DesignToCodeSplit()
    {
        AffectsMeasure<DesignToCodeSplit>(IsStackedProperty, DesignWidthProperty, PaneHeightProperty);
    }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    public double DesignWidth { get => GetValue(DesignWidthProperty); set => SetValue(DesignWidthProperty, value); }

    public double PaneHeight { get => GetValue(PaneHeightProperty); set => SetValue(PaneHeightProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : DesignWidth * 2;
        var height = PaneHeight;
        var (design, output) = Sizes(width);
        if (Children.Count > 0)
        {
            Children[0].Measure(design);
        }
        if (Children.Count > 1)
        {
            Children[1].Measure(output);
        }
        return new Size(width, IsStacked ? 2 * height : height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (design, output) = Sizes(finalSize.Width);
        if (Children.Count > 0)
        {
            Children[0].Arrange(new Rect(design));
        }
        if (Children.Count > 1)
        {
            var origin = IsStacked ? new Point(0, design.Height) : new Point(design.Width, 0);
            Children[1].Arrange(new Rect(origin, output));
        }
        return finalSize;
    }

    private (Size Design, Size Output) Sizes(double width)
    {
        var height = PaneHeight;
        if (IsStacked)
        {
            return (new Size(width, height), new Size(width, height));
        }
        var design = Math.Min(DesignWidth, width / 2);
        return (new Size(design, height), new Size(Math.Max(0, width - design), height));
    }
}
