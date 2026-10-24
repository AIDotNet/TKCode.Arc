using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Swaps content in place with layers stacked in one cell (a CSS grid whose items share row 1 and column 1, sized by the
/// largest): the old layer lifts to <see cref="ExitOffset"/>, blurs to <see cref="Blur"/> and fades out on the standard
/// curve in <see cref="ExitSeconds"/>, while the new one rises from <see cref="EnterOffset"/> below out of the same blur
/// on the enter curve in <see cref="EnterSeconds"/> after <see cref="EnterDelay"/>. Content whose key comes back while it
/// is still leaving is called back from where it is. Reduced motion fades only; Off swaps at once.
/// </summary>
/// <remarks>
/// Code-driven because a ControlTheme transition cannot keep the outgoing content on screen while the incoming one
/// takes its place, nor turn a leaving layer around from where it is. Each layer's progress is a <see cref="MotionValue{T}"/>.
/// </remarks>
internal sealed class OnboardingSwap : Control
{
    private readonly List<Layer> _exiting = new();
    private Layer? _current;

    /// <summary>Seconds the incoming layer waits under full motion.</summary>
    public double EnterDelay { get; set; }

    /// <summary>Seconds of the incoming layer's rise (enter curve).</summary>
    public double EnterSeconds { get; set; } = .48;

    /// <summary>Seconds of the outgoing layer's lift (standard curve).</summary>
    public double ExitSeconds { get; set; } = .18;

    /// <summary>Seconds of the incoming fade under reduced motion (no delay, no travel, no blur).</summary>
    public double ReducedEnterSeconds { get; set; } = .48;

    /// <summary>Seconds of the outgoing fade under reduced motion.</summary>
    public double ReducedExitSeconds { get; set; } = .18;

    /// <summary>Where the incoming layer starts, in px below its place.</summary>
    public double EnterOffset { get; set; } = 6;

    /// <summary>Where the outgoing layer ends, in px (negative: up).</summary>
    public double ExitOffset { get; set; } = -4;

    /// <summary>The blur radius both layers pass through.</summary>
    public double Blur { get; set; } = 4;

    /// <summary>The content shown (or arriving) now.</summary>
    internal Control? CurrentContent => _current?.Content;

    /// <summary>The key of the content shown now.</summary>
    internal object? CurrentKey => _current?.Key;

    /// <summary>Layers still leaving.</summary>
    internal int ExitingCount => _exiting.Count;

    /// <summary>Runs <paramref name="action"/> on every layer's content (current and leaving).</summary>
    internal void ForEachContent(Action<Control> action)
    {
        foreach (var layer in _exiting)
        {
            action(layer.Content);
        }
        if (_current is not null)
        {
            action(_current.Content);
        }
    }

    /// <summary>Shows the content of <paramref name="key"/>, made by <paramref name="create"/> unless it is still leaving.</summary>
    public void Show(object key, Func<Control> create)
    {
        if (_current is not null && Equals(_current.Key, key))
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (_current is null || !IsLoaded || !IsEffectivelyVisible || level == ArcMotionLevel.Off)
        {
            foreach (var layer in _exiting.ToArray())
            {
                Remove(layer);
            }
            if (_current is not null)
            {
                Remove(_current);
            }
            _current = Add(key, create());
            Apply(_current, 1, 0, 0);
            InvalidateMeasure();
            return;
        }
        var full = level == ArcMotionLevel.Full;
        Layer? recalled = null;
        foreach (var layer in _exiting)
        {
            if (Equals(layer.Key, key))
            {
                recalled = layer;
                break;
            }
        }
        var outgoing = _current;
        _exiting.Add(outgoing);
        _ = Exit(outgoing, full);
        if (recalled is not null)
        {
            _exiting.Remove(recalled);
            _current = recalled;
            AutomationProperties.SetAccessibilityView(recalled.Content, AccessibilityView.Content);
        }
        else
        {
            _current = Add(key, create());
            Apply(_current, 0, full ? EnterOffset : 0, full ? Blur : 0);
        }
        Enter(_current, full);
        InvalidateMeasure();
    }

    private sealed class Layer(object key, Control content, TranslateTransform translate)
    {
        public object Key { get; } = key;

        public Control Content { get; } = content;

        public TranslateTransform Translate { get; } = translate;

        public MotionValue<double>? Progress { get; set; }

        public double Opacity { get; set; } = 1;

        public double Y { get; set; }

        public double BlurRadius { get; set; }
    }

    private Layer Add(object key, Control content)
    {
        var translate = new TranslateTransform();
        content.RenderTransform = translate;
        LogicalChildren.Add(content);
        VisualChildren.Add(content);
        return new Layer(key, content, translate);
    }

    private void Remove(Layer layer)
    {
        layer.Progress?.Stop();
        layer.Progress = null;
        _exiting.Remove(layer);
        LogicalChildren.Remove(layer.Content);
        VisualChildren.Remove(layer.Content);
    }

    private static void Apply(Layer layer, double opacity, double y, double blur)
    {
        layer.Opacity = opacity;
        layer.Y = y;
        layer.BlurRadius = blur;
        layer.Content.Opacity = Math.Clamp(opacity, 0, 1);
        layer.Translate.Y = y;
        layer.Content.Effect = ArcBlur.Get(Math.Max(0, blur));
    }

    private void Enter(Layer layer, bool full)
    {
        layer.Progress?.Stop();
        double opacity = layer.Opacity, y = layer.Y, blur = layer.BlurRadius;
        var progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        layer.Progress = progress;
        progress.Changed += p => Apply(layer, opacity + (1 - opacity) * p, y * (1 - p), blur * (1 - p));
        var spec = full
            ? MotionSpec.Tween(TimeSpan.FromSeconds(EnterSeconds), ArcEasings.Enter, TimeSpan.FromSeconds(Math.Max(0, EnterDelay)))
            : MotionSpec.Tween(TimeSpan.FromSeconds(ReducedEnterSeconds), ArcEasings.Enter);
        _ = progress.AnimateTo(1, spec);
    }

    private async Task Exit(Layer layer, bool full)
    {
        layer.Progress?.Stop();
        // The leaving copy is not read again.
        AutomationProperties.SetAccessibilityView(layer.Content, AccessibilityView.Raw);
        double opacity = layer.Opacity, y = layer.Y, blur = layer.BlurRadius;
        var endY = full ? ExitOffset : y;
        var endBlur = full ? Blur : 0;
        var progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        layer.Progress = progress;
        progress.Changed += p => Apply(layer, opacity * (1 - p), y + (endY - y) * p, blur + (endBlur - blur) * p);
        var seconds = full ? ExitSeconds : ReducedExitSeconds;
        var done = await progress.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromSeconds(seconds), ArcEasings.Standard));
        if (done && _exiting.Contains(layer) && ReferenceEquals(layer.Progress, progress))
        {
            Remove(layer);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = default(Size);
        foreach (var layer in _exiting)
        {
            layer.Content.Measure(availableSize);
            size = new Size(Math.Max(size.Width, layer.Content.DesiredSize.Width), Math.Max(size.Height, layer.Content.DesiredSize.Height));
        }
        if (_current is not null)
        {
            _current.Content.Measure(availableSize);
            size = new Size(Math.Max(size.Width, _current.Content.DesiredSize.Width), Math.Max(size.Height, _current.Content.DesiredSize.Height));
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var layer in _exiting)
        {
            layer.Content.Arrange(new Rect(finalSize));
        }
        _current?.Content.Arrange(new Rect(finalSize));
        return finalSize;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var layer in _exiting.ToArray())
        {
            Remove(layer);
        }
        if (_current is not null)
        {
            _current.Progress?.Stop();
            _current.Progress = null;
            Apply(_current, 1, 0, 0);
            AutomationProperties.SetAccessibilityView(_current.Content, AccessibilityView.Content);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);

    /// <summary>The swap itself is invisible to screen readers; its content is read in place.</summary>
    private sealed class NoneAutomationPeer(OnboardingSwap owner) : ControlAutomationPeer(owner)
    {
        protected override bool IsContentElementCore() => false;

        protected override bool IsControlElementCore() => false;
    }
}
