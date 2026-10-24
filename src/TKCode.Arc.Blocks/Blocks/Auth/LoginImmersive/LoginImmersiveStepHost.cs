using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Shows one of its children at a time and changes between them the way the Immersive login's card does: the outgoing
/// view leaves layout at once and lifts away (<see cref="ExitOffset"/> on the standard curve in
/// <see cref="ExitDuration"/>, fading in <see cref="ExitOpacityDuration"/> and blurring to <see cref="ExitBlur"/>) while
/// the incoming one rises from <see cref="EnterOffset"/> on the smooth spring, fading in after a short delay out of a
/// soft blur. With <see cref="AnimatesHeight"/> the host's height follows on the smooth spring only while views change
/// (and keeps following if the new view grows meanwhile); otherwise it is the current view's height, so rows that
/// open inside a view are not delayed. Reduced motion removes the old view at once and fades the new one in; Off swaps.
/// </summary>
/// <remarks>
/// Code-driven because a ControlTheme cannot keep a leaving view on screen outside layout while the next one takes
/// its place, nor animate the container's height only during a change. Each view keeps its own motion values, so a
/// view called back while it is still leaving turns around from where it is.
/// </remarks>
internal sealed class LoginImmersiveStepHost : Panel
{
    private sealed class Layer
    {
        public required Control View { get; init; }
        public required TranslateTransform Translate { get; init; }
        public required MotionValue<double> Y { get; init; }
        public required MotionValue<double> Opacity { get; init; }
        public required MotionValue<double> Blur { get; init; }
        public bool IsExiting { get; set; }
        public int Generation { get; set; }
    }

    private readonly Dictionary<Control, Layer> _layers = new();
    private Layer? _current;
    private MotionValue<double>? _height;
    private bool _gliding;
    private double _glideFrom = double.NaN;

    public LoginImmersiveStepHost()
    {
        ClipToBounds = true;
    }

    /// <summary>Where the incoming view starts, in px below its place (12).</summary>
    public double EnterOffset { get; set; } = 12;

    /// <summary>The blur the incoming view starts from (4).</summary>
    public double EnterBlur { get; set; } = 4;

    /// <summary>The incoming fade and unblur (240 ms, enter curve).</summary>
    public TimeSpan EnterDuration { get; set; } = ArcMotionPresets.Standard;

    /// <summary>Delay of the incoming fade (60 ms).</summary>
    public TimeSpan EnterOpacityDelay { get; set; } = TimeSpan.FromMilliseconds(60);

    /// <summary>Delay of the incoming unblur (40 ms).</summary>
    public TimeSpan EnterBlurDelay { get; set; } = TimeSpan.FromMilliseconds(40);

    /// <summary>Where the outgoing view goes, in px (−8: up).</summary>
    public double ExitOffset { get; set; } = -8;

    /// <summary>The blur the outgoing view ends at (4).</summary>
    public double ExitBlur { get; set; } = 4;

    /// <summary>The outgoing lift and blur (180 ms, standard curve).</summary>
    public TimeSpan ExitDuration { get; set; } = ArcMotionPresets.Exit;

    /// <summary>The outgoing fade (120 ms, standard curve).</summary>
    public TimeSpan ExitOpacityDuration { get; set; } = ArcMotionPresets.Instant;

    /// <summary>The incoming fade under reduced motion (120 ms).</summary>
    public TimeSpan ReducedEnterDuration { get; set; } = ArcMotionPresets.Instant;

    /// <summary>Whether the host's height springs from the old view's height to the new one's during a change.</summary>
    public bool AnimatesHeight { get; set; } = true;

    /// <summary>The spring of the incoming rise and of the height (Arc's smooth spring).</summary>
    public SpringSpec Spring { get; set; } = SpringSpec.FromVisual(0.4, 0);

    /// <summary>The view shown (or arriving) now.</summary>
    public Control? Current => _current?.View;

    /// <summary>Whether the height is springing (tests read it).</summary>
    internal bool IsGliding => _gliding;

    /// <summary>Views still leaving (tests read it).</summary>
    internal int ExitingCount => _layers.Values.Count(l => l.IsExiting);

    /// <summary>Raised when a view finished leaving and is hidden.</summary>
    public event EventHandler<Control>? ViewHidden;

    private Layer LayerOf(Control view)
    {
        if (_layers.TryGetValue(view, out var layer))
        {
            return layer;
        }
        var translate = new TranslateTransform();
        view.RenderTransform = translate;
        layer = new Layer
        {
            View = view,
            Translate = translate,
            Y = new MotionValue<double>(this, 0, MotionVectors.Double),
            Opacity = new MotionValue<double>(this, 1, MotionVectors.Double),
            Blur = new MotionValue<double>(this, 0, MotionVectors.Double)
        };
        layer.Y.Changed += y => translate.Y = y;
        layer.Opacity.Changed += o => view.Opacity = o;
        layer.Blur.Changed += b => view.Effect = ArcBlur.Get(b);
        _layers[view] = layer;
        return layer;
    }

    protected override void ChildrenChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        base.ChildrenChanged(sender, e);
        foreach (var child in Children)
        {
            if (_current?.View != child && !(_layers.TryGetValue(child, out var layer) && layer.IsExiting))
            {
                child.IsVisible = false;
            }
        }
        if (_current is null && Children.Count > 0)
        {
            Show(Children[0]);
        }
    }

    /// <summary>Makes <paramref name="view"/> (one of the children) the current view, animating per the motion level.</summary>
    public void Show(Control view)
    {
        if (ReferenceEquals(_current?.View, view))
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        var animate = _current is not null && IsLoaded && IsEffectivelyVisible && level != ArcMotionLevel.Off;
        var full = level == ArcMotionLevel.Full;
        var outgoing = _current;
        var incoming = LayerOf(view);
        incoming.Generation++;
        _current = incoming;
        incoming.IsExiting = false;
        view.IsVisible = true;
        view.ZIndex = 0;

        if (!animate)
        {
            if (outgoing is not null)
            {
                Hide(outgoing);
            }
            Settle(incoming);
            StopGlide();
            InvalidateMeasure();
            return;
        }

        if (outgoing is not null)
        {
            if (full)
            {
                StartExit(outgoing);
            }
            else
            {
                Hide(outgoing);
            }
        }
        StartEnter(incoming, full);
        if (AnimatesHeight && full && Bounds.Height > 0)
        {
            // The glide starts at the next measure, once the new view's height is known.
            _glideFrom = _gliding && _height is not null ? _height.Value : Bounds.Height;
        }
        else
        {
            StopGlide();
        }
        InvalidateMeasure();
    }

    private void Settle(Layer layer)
    {
        layer.Y.Jump(0);
        layer.Opacity.Jump(1);
        layer.Blur.Jump(0);
        layer.View.Effect = null;
    }

    private void Hide(Layer layer)
    {
        layer.Y.Stop();
        layer.Opacity.Stop();
        layer.Blur.Stop();
        layer.IsExiting = false;
        layer.View.IsVisible = false;
        Settle(layer);
        ViewHidden?.Invoke(this, layer.View);
    }

    private void StartEnter(Layer layer, bool full)
    {
        if (full)
        {
            if (layer.Opacity.Value >= 0.999 && Math.Abs(layer.Y.Value) < 0.001)
            {
                // A fresh entrance (not a view called back while leaving): start below, clear and blurred.
                layer.Y.Jump(EnterOffset);
                layer.Opacity.Jump(0);
                layer.Blur.Jump(EnterBlur);
            }
            _ = layer.Y.AnimateTo(0, MotionSpec.Spring(Spring));
            _ = layer.Opacity.AnimateTo(1, MotionSpec.Tween(EnterDuration, ArcEasings.Enter, EnterOpacityDelay));
            _ = layer.Blur.AnimateTo(0, MotionSpec.Tween(EnterDuration, ArcEasings.Enter, EnterBlurDelay));
        }
        else
        {
            layer.Y.Jump(0);
            layer.Blur.Jump(0);
            layer.Opacity.Jump(0);
            _ = layer.Opacity.AnimateTo(1, MotionSpec.Tween(ReducedEnterDuration, ArcEasings.Standard));
        }
    }

    private async void StartExit(Layer layer)
    {
        layer.IsExiting = true;
        layer.View.ZIndex = 1;
        var generation = ++layer.Generation;
        _ = layer.Opacity.AnimateTo(0, MotionSpec.Tween(ExitOpacityDuration, ArcEasings.Standard));
        _ = layer.Blur.AnimateTo(ExitBlur, MotionSpec.Tween(ExitDuration, ArcEasings.Standard));
        var done = await layer.Y.AnimateTo(ExitOffset, MotionSpec.Tween(ExitDuration, ArcEasings.Standard));
        if (layer.IsExiting && layer.Generation == generation)
        {
            Hide(layer);
            InvalidateMeasure();
        }
        _ = done;
    }

    private void StopGlide()
    {
        _glideFrom = double.NaN;
        if (_gliding)
        {
            _gliding = false;
            _height?.Stop();
        }
    }

    private async void Glide(double from, double to)
    {
        _height ??= CreateHeight();
        if (!_gliding)
        {
            _height.Jump(from);
        }
        _gliding = true;
        var arrived = await _height.AnimateTo(to, MotionSpec.Spring(Spring));
        if (arrived && _gliding && Math.Abs(_height.Target - to) < 0.01)
        {
            _gliding = false;
            InvalidateMeasure();
        }
    }

    private MotionValue<double> CreateHeight()
    {
        var height = new MotionValue<double>(this, 0, MotionVectors.Double);
        height.Changed += _ => InvalidateMeasure();
        return height;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var currentHeight = 0.0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            if (ReferenceEquals(child, _current?.View))
            {
                width = Math.Max(width, child.DesiredSize.Width);
                currentHeight = child.DesiredSize.Height;
            }
        }
        if (!double.IsNaN(_glideFrom))
        {
            var from = _glideFrom;
            _glideFrom = double.NaN;
            if (Math.Abs(from - currentHeight) > 0.5)
            {
                Glide(from, currentHeight);
            }
        }
        else if (_gliding && _height is not null && Math.Abs(_height.Target - currentHeight) > 0.5)
        {
            // The new view grew or shrank while the height was still moving: keep following it.
            Glide(_height.Value, currentHeight);
        }
        var height = _gliding && _height is not null ? Math.Max(0, _height.Value) : currentHeight;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            // Every view keeps its own height and sits at the top; the leaving one is out of layout.
            child.Arrange(new Rect(0, 0, finalSize.Width, child.DesiredSize.Height));
        }
        return finalSize;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var layer in _layers.Values.ToArray())
        {
            if (layer.IsExiting)
            {
                Hide(layer);
            }
        }
        if (_current is not null)
        {
            Settle(_current);
        }
        StopGlide();
    }
}
