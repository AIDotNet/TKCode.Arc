using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The step viewport of <see cref="LoginSplit"/>: one step on show, the leaving one popped out of layout at its place. Going
/// forward the new step rises from 14px below on Arc's smooth spring while its fade and 4px blur resolve in 240 ms on the
/// enter curve after 60 ms; the old step drifts 8px up on the same spring and fades out in 180 ms, blurring to 2px. Going
/// back mirrors both. The height follows the new step on the smooth spring only while the steps change; otherwise it is
/// the step's own, so a field message opening inside a step moves the card at once. Reduced motion removes the old step
/// at once and fades the new one in 120 ms; Off swaps at once.
/// </summary>
/// <remarks>
/// Code-driven: the leaving step must stay drawn at its measured size outside layout, the target height is measured, and a
/// change of that height during the glide retargets the running spring with its velocity.
/// </remarks>
internal sealed class LoginSplitViewport : Control
{
    private readonly List<Layer> _leaving = new();
    private Layer? _current;
    private MotionValue<double>? _height;
    private bool _gliding;
    private bool _pendingGlide;
    private double _glideFrom;
    private double _shownHeight;
    private bool _measuring;

    public LoginSplitViewport()
    {
        ClipToBounds = true;
    }

    private sealed class Layer
    {
        public required Control View { get; init; }
        public required TranslateTransform Move { get; init; }
        public MotionValue<double>? Offset { get; set; }
        public MotionValue<double>? Fade { get; set; }
        public Size Size { get; set; }
    }

    /// <summary>The step on show.</summary>
    public Control? Current => _current?.View;

    /// <summary>Steps still leaving (tests read it).</summary>
    internal int LeavingCount => _leaving.Count;

    /// <summary>True while the height follows the spring rather than the step (tests read it).</summary>
    internal bool IsGliding => _gliding;

    /// <summary>The height shown now (tests read it).</summary>
    internal double ShownHeight => _shownHeight;

    /// <summary>
    /// Shows <paramref name="view"/>: 1 enters going forward, −1 going back, 0 swaps without motion (the first step).
    /// </summary>
    public void Show(Control view, int direction)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        var animate = direction != 0 && _current is not null && IsLoaded && level != ArcMotionLevel.Off;
        var full = animate && level == ArcMotionLevel.Full;
        var from = _gliding && _height is not null ? _height.Value : _shownHeight;

        if (_current is { } outgoing)
        {
            outgoing.Size = outgoing.View.Bounds.Size;
            outgoing.View.IsHitTestVisible = false;
            if (full)
            {
                _leaving.Add(outgoing);
                Leave(outgoing, direction);
            }
            else
            {
                Remove(outgoing);
            }
        }

        var move = new TranslateTransform();
        view.RenderTransform = move;
        var layer = new Layer { View = view, Move = move };
        _current = layer;
        LogicalChildren.Add(view);
        VisualChildren.Add(view);
        if (animate)
        {
            Enter(layer, direction, full);
        }

        if (full)
        {
            _pendingGlide = true;
            _glideFrom = from;
        }
        else
        {
            StopGlide();
        }
        InvalidateMeasure();
    }

    private void Enter(Layer layer, int direction, bool full)
    {
        var view = layer.View;
        view.Opacity = 0;
        var fade = new MotionValue<double>(this, 0, MotionVectors.Double);
        layer.Fade = fade;
        if (!full)
        {
            fade.Changed += p => view.Opacity = p;
            _ = fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            return;
        }
        var start = direction * LoginSplitMotion.StepEnterOffset;
        layer.Move.Y = start;
        view.Effect = ArcBlur.Get(LoginSplitMotion.StepEnterBlur);
        fade.Changed += p =>
        {
            var t = Math.Clamp(p, 0, 1);
            view.Opacity = t;
            view.Effect = ArcBlur.Get(LoginSplitMotion.StepEnterBlur * (1 - t));
        };
        _ = fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, LoginSplitMotion.StepEnterDelay));
        var offset = new MotionValue<double>(this, start, MotionVectors.Double);
        layer.Offset = offset;
        offset.Changed += y => layer.Move.Y = y;
        _ = offset.AnimateTo(0, MotionSpec.Spring(LoginSplitMotion.Smooth));
    }

    private async void Leave(Layer layer, int direction)
    {
        layer.Offset?.Stop();
        layer.Fade?.Stop();
        var view = layer.View;
        var startOpacity = view.Opacity;
        var fade = new MotionValue<double>(this, 0, MotionVectors.Double);
        layer.Fade = fade;
        fade.Changed += p =>
        {
            var t = Math.Clamp(p, 0, 1);
            view.Opacity = startOpacity * (1 - t);
            view.Effect = ArcBlur.Get(LoginSplitMotion.StepExitBlur * t);
        };
        _ = fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Standard));
        var offset = new MotionValue<double>(this, layer.Move.Y, MotionVectors.Double);
        layer.Offset = offset;
        offset.Changed += y => layer.Move.Y = y;
        await offset.AnimateTo(-direction * LoginSplitMotion.StepExitOffset, MotionSpec.Spring(LoginSplitMotion.Smooth));
        if (_leaving.Contains(layer))
        {
            Remove(layer);
        }
    }

    private void Remove(Layer layer)
    {
        layer.Offset?.Dispose();
        layer.Fade?.Dispose();
        layer.Offset = null;
        layer.Fade = null;
        _leaving.Remove(layer);
        LogicalChildren.Remove(layer.View);
        VisualChildren.Remove(layer.View);
    }

    /// <summary>Drops every leaving step and finishes the entering motion at once.</summary>
    public void Settle()
    {
        foreach (var layer in _leaving.ToArray())
        {
            Remove(layer);
        }
        if (_current is { } current)
        {
            current.Offset?.Stop();
            current.Fade?.Stop();
            current.Move.Y = 0;
            current.View.Opacity = 1;
            current.View.Effect = null;
        }
        StopGlide();
        InvalidateMeasure();
    }

    private void StopGlide()
    {
        _pendingGlide = false;
        if (_gliding)
        {
            _gliding = false;
            _height?.Stop();
        }
    }

    private MotionValue<double> HeightValue()
    {
        if (_height is null)
        {
            _height = new MotionValue<double>(this, 0, MotionVectors.Double);
            _height.Changed += _ =>
            {
                if (!_measuring)
                {
                    InvalidateMeasure();
                }
            };
        }
        return _height;
    }

    private async void Glide(double to)
    {
        var height = HeightValue();
        _gliding = true;
        var reached = await height.AnimateTo(to, MotionSpec.Spring(LoginSplitMotion.Smooth));
        if (reached && height.Target.Equals(to))
        {
            // Back to auto: the step's own height from now on.
            _gliding = false;
            if (!_measuring)
            {
                InvalidateMeasure();
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _measuring = true;
        try
        {
            foreach (var layer in _leaving)
            {
                layer.View.Measure(layer.Size);
            }
            if (_current is null)
            {
                _shownHeight = 0;
                return default;
            }
            _current.View.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            var content = _current.View.DesiredSize;
            var to = content.Height;
            if (_pendingGlide)
            {
                _pendingGlide = false;
                var from = _glideFrom;
                if (from > 0 && to > 0 && Math.Abs(from - to) > 0.01)
                {
                    var height = HeightValue();
                    if (!_gliding)
                    {
                        height.Jump(from);
                    }
                    Glide(to);
                }
            }
            else if (_gliding && _height is not null && Math.Abs(_height.Target - to) > 0.01)
            {
                Glide(to);
            }
            var shown = _gliding && _height is not null ? _height.Value : to;
            _shownHeight = shown;
            return new Size(content.Width, shown);
        }
        finally
        {
            _measuring = false;
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var layer in _leaving)
        {
            layer.View.Arrange(new Rect(0, 0, layer.Size.Width, layer.Size.Height));
        }
        if (_current is { } current)
        {
            current.View.Arrange(new Rect(0, 0, finalSize.Width, current.View.DesiredSize.Height));
        }
        return finalSize;
    }
}
