using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The glyph of an AI answer action (Arc answer: copy → check, outline → filled rating). A change swaps the glyph the way
/// Arc's answer does: the new one fades in over 240ms (enter curve) while it grows from 0.85 to full size on
/// <see cref="ArcSpring.Snappy"/>; the old one fades out in 160ms (standard curve) while it shrinks to 0.85. Reduced motion
/// crossfades both in 120ms (linear) without scaling; Off swaps at once, as does any change before the glyph is shown.
/// Glyphs are Lucide shapes stroked at 1.75 in the inherited text color, filled too when <see cref="IsFilled"/>.
/// <para>
/// Code-driven (<see cref="MotionValue{T}"/>) rather than a ControlTheme transition or <c>ArcContentSwap</c>: each layer runs
/// opacity on a tween and scale on a spring with different durations, and the old layer must leave only after its fade.
/// <c>ArcContentSwap</c>'s icon mode grows from 0.6 out of a blur, which is not this motion.
/// </para>
/// </summary>
internal sealed class AiAnswerGlyphSwap : Control
{
    /// <summary>The scale a glyph enters from and leaves to.</summary>
    internal const double SwapScale = 0.85;

    /// <summary>How long the old glyph takes to fade out (160ms).</summary>
    internal static readonly TimeSpan ExitDuration = ArcMotionPresets.Fast;

    /// <summary>How long the new glyph takes to fade in (240ms).</summary>
    internal static readonly TimeSpan EnterDuration = ArcMotionPresets.Standard;

    private static readonly Easing s_linear = new LinearEasing();

    /// <summary>The Lucide glyph (24 × 24 box).</summary>
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<AiAnswerGlyphSwap, Geometry?>(nameof(Data));

    /// <summary>Fills the glyph as well as stroking it (a chosen rating).</summary>
    public static readonly StyledProperty<bool> IsFilledProperty =
        AvaloniaProperty.Register<AiAnswerGlyphSwap, bool>(nameof(IsFilled));

    /// <summary>The glyph's size (16).</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<AiAnswerGlyphSwap, double>(nameof(Size), 16);

    /// <summary>The color (inherited text color).</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<AiAnswerGlyphSwap>();

    private readonly List<Layer> _exiting = new();
    private Layer? _current;

    static AiAnswerGlyphSwap()
    {
        AffectsMeasure<AiAnswerGlyphSwap>(SizeProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<AiAnswerGlyphSwap>(false);
        FocusableProperty.OverrideDefaultValue<AiAnswerGlyphSwap>(false);
        HorizontalAlignmentProperty.OverrideDefaultValue<AiAnswerGlyphSwap>(Avalonia.Layout.HorizontalAlignment.Center);
        VerticalAlignmentProperty.OverrideDefaultValue<AiAnswerGlyphSwap>(Avalonia.Layout.VerticalAlignment.Center);
    }

    /// <inheritdoc cref="DataProperty"/>
    public Geometry? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }

    /// <inheritdoc cref="IsFilledProperty"/>
    public bool IsFilled { get => GetValue(IsFilledProperty); set => SetValue(IsFilledProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>The glyph on screen now (the newest layer).</summary>
    internal AiActionGlyph? ShownGlyph => _current?.Glyph;

    /// <summary>Glyphs still fading out.</summary>
    internal int ExitingCount => _exiting.Count;

    /// <summary>The scale of the glyph on screen now (1 at rest).</summary>
    internal double ShownScale => _current?.Scale.ScaleX ?? 1;

    private sealed class Layer
    {
        public required AiActionGlyph Glyph { get; init; }
        public required ScaleTransform Scale { get; init; }
        public MotionValue<double>? Fade { get; set; }
        public MotionValue<double>? Grow { get; set; }

        public void Stop()
        {
            Fade?.Dispose();
            Grow?.Dispose();
            Fade = null;
            Grow = null;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataProperty || change.Property == IsFilledProperty)
        {
            Swap();
        }
        else if (change.Property == SizeProperty)
        {
            foreach (var layer in AllLayers())
            {
                layer.Glyph.Size = Size;
            }
        }
    }

    private IEnumerable<Layer> AllLayers()
    {
        foreach (var layer in _exiting)
        {
            yield return layer;
        }
        if (_current is not null)
        {
            yield return _current;
        }
    }

    private Layer CreateLayer()
    {
        var scale = new ScaleTransform(1, 1);
        var glyph = new AiActionGlyph
        {
            Data = Data,
            IsFilled = IsFilled,
            Size = Size,
            RenderTransform = scale,
            RenderTransformOrigin = RelativePoint.Center
        };
        LogicalChildren.Add(glyph);
        VisualChildren.Add(glyph);
        return new Layer { Glyph = glyph, Scale = scale };
    }

    private void RemoveLayer(Layer layer)
    {
        layer.Stop();
        LogicalChildren.Remove(layer.Glyph);
        VisualChildren.Remove(layer.Glyph);
        _exiting.Remove(layer);
    }

    private void Swap()
    {
        if (_current is null)
        {
            _current = CreateLayer();
            InvalidateMeasure();
            return;
        }
        if (ReferenceEquals(_current.Glyph.Data, Data) && _current.Glyph.IsFilled == IsFilled)
        {
            return;
        }
        var level = TopLevel.GetTopLevel(this) is not null ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        if (!IsLoaded || level == ArcMotionLevel.Off || !IsEffectivelyVisible)
        {
            // In place: no motion, and any glyphs still leaving go at once.
            foreach (var layer in _exiting.ToArray())
            {
                RemoveLayer(layer);
            }
            _current.Stop();
            _current.Glyph.Data = Data;
            _current.Glyph.IsFilled = IsFilled;
            _current.Glyph.Opacity = 1;
            _current.Scale.ScaleX = 1;
            _current.Scale.ScaleY = 1;
            return;
        }
        var outgoing = _current;
        _exiting.Add(outgoing);
        _current = CreateLayer();
        var full = level == ArcMotionLevel.Full;
        StartExit(outgoing, full);
        StartEnter(_current, full);
        InvalidateMeasure();
    }

    private void StartEnter(Layer layer, bool full)
    {
        layer.Glyph.Opacity = 0;
        var fade = new MotionValue<double>(this, 0, MotionVectors.Double);
        layer.Fade = fade;
        fade.Changed += v => layer.Glyph.Opacity = Math.Clamp(v, 0, 1);
        _ = fade.AnimateTo(1, full
            ? MotionSpec.Tween(EnterDuration, ArcEasings.Enter)
            : MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
        if (!full)
        {
            return;
        }
        layer.Scale.ScaleX = SwapScale;
        layer.Scale.ScaleY = SwapScale;
        var grow = new MotionValue<double>(this, SwapScale, MotionVectors.Double);
        layer.Grow = grow;
        grow.Changed += v =>
        {
            layer.Scale.ScaleX = v;
            layer.Scale.ScaleY = v;
        };
        _ = grow.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
    }

    private void StartExit(Layer layer, bool full)
    {
        layer.Stop();
        var startOpacity = layer.Glyph.Opacity;
        var startScale = layer.Scale.ScaleX;
        var progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        layer.Fade = progress;
        progress.Changed += p =>
        {
            layer.Glyph.Opacity = Math.Clamp(startOpacity * (1 - p), 0, 1);
            if (full)
            {
                var s = startScale + (SwapScale - startScale) * p;
                layer.Scale.ScaleX = s;
                layer.Scale.ScaleY = s;
            }
        };
        var task = progress.AnimateTo(1, full
            ? MotionSpec.Tween(ExitDuration, ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
        _ = RemoveWhenDone(layer, task);
    }

    private async Task RemoveWhenDone(Layer layer, Task<bool> task)
    {
        // Awaited on the UI thread: the layer leaves in the same dispatcher turn as its last frame.
        await task;
        if (_exiting.Contains(layer))
        {
            RemoveLayer(layer);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var layer in AllLayers())
        {
            layer.Glyph.Measure(availableSize);
        }
        return new Size(Size, Size);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var layer in AllLayers())
        {
            layer.Glyph.Arrange(new Rect(finalSize));
        }
        return finalSize;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // A swap does not outlive the glyph's stay in a window: the new glyph stands at rest.
        foreach (var layer in _exiting.ToArray())
        {
            RemoveLayer(layer);
        }
        if (_current is not null)
        {
            _current.Stop();
            _current.Glyph.Opacity = 1;
            _current.Scale.ScaleX = 1;
            _current.Scale.ScaleY = 1;
        }
    }
}
