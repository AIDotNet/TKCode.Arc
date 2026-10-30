using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The branch's visible path of an <see cref="AiWorkflow"/>: switching paths crossfades the lists (the old one out in
/// 100ms, the new one in over 160ms, standard curve, no movement) while the height follows the new list on the
/// critically damped 0.22s spring. Growth inside the shown list (an output opening) is followed at once, since those
/// parts animate themselves. Reduced motion: a 120ms crossfade, the height jumps. Off: at once.
/// <para>
/// Code-driven (<see cref="MotionValue{T}"/>) because the height target is the measured height of the new list and the
/// leaving list must stay drawn, out of layout, until it has faded; a ControlTheme cannot express that.
/// </para>
/// </summary>
internal sealed class AiWorkflowPathSwap : Control
{
    private static readonly TimeSpan s_out = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan s_in = TimeSpan.FromMilliseconds(160);

    private readonly RectangleGeometry _clip = new();
    private readonly Dictionary<Control, MotionValue<double>> _fades = [];
    private Control? _current;
    private Control? _leaving;
    private MotionValue<double>? _height;
    private bool _follow;

    public AiWorkflowPathSwap()
    {
        ClipToBounds = false;
    }

    /// <summary>The list shown.</summary>
    public Control? Current => _current;

    /// <summary>The list fading out, if any.</summary>
    internal Control? Leaving => _leaving;

    internal bool IsMoving => _height?.IsAnimating == true;

    /// <summary>Shows <paramref name="content"/>, crossfading from the list shown when <paramref name="animate"/>.</summary>
    public void Show(Control? content, bool animate)
    {
        if (ReferenceEquals(content, _current))
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        animate = animate && IsLoaded && level != ArcMotionLevel.Off && _current is not null;
        if (_leaving is not null)
        {
            Remove(_leaving);
            _leaving = null;
        }
        var old = _current;
        _current = content;
        if (content is not null && content.GetVisualParent() is null)
        {
            LogicalChildren.Add(content);
            VisualChildren.Add(content);
        }
        if (!animate)
        {
            if (old is not null)
            {
                Remove(old);
            }
            if (content is not null)
            {
                content.Opacity = 1;
            }
            _follow = true;
            InvalidateMeasure();
            return;
        }
        var full = level == ArcMotionLevel.Full;
        _leaving = old;
        if (old is not null)
        {
            var leaving = old;
            _ = Fade(old, 0, full ? s_out : ArcMotionPresets.Instant).ContinueWith(_ => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (ReferenceEquals(_leaving, leaving))
                {
                    Remove(leaving);
                    _leaving = null;
                    InvalidateMeasure();
                }
            }), TaskScheduler.Default);
        }
        if (content is not null)
        {
            content.Opacity = 0;
            _ = Fade(content, 1, full ? s_in : ArcMotionPresets.Instant);
        }
        _follow = !full;
        if (full)
        {
            _height ??= CreateHeight();
            _height.Jump(Bounds.Height);
        }
        InvalidateMeasure();
    }

    private Task<bool> Fade(Control control, double to, TimeSpan duration)
    {
        if (!_fades.TryGetValue(control, out var fade))
        {
            fade = new MotionValue<double>(this, control.Opacity, MotionVectors.Double);
            var target = control;
            fade.Changed += v => target.Opacity = Math.Clamp(v, 0, 1);
            _fades[control] = fade;
        }
        fade.Jump(control.Opacity);
        return fade.AnimateTo(to, MotionSpec.Tween(duration, ArcEasings.Standard));
    }

    private void Remove(Control control)
    {
        if (_fades.TryGetValue(control, out var fade))
        {
            fade.Stop();
        }
        control.Opacity = 1;
        VisualChildren.Remove(control);
        LogicalChildren.Remove(control);
    }

    private MotionValue<double> CreateHeight()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += _ => InvalidateMeasure();
        return value;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var available = availableSize.WithHeight(double.PositiveInfinity);
        _leaving?.Measure(available);
        if (_current is not { } content)
        {
            return default;
        }
        content.Measure(available);
        var natural = content.DesiredSize.Height;
        double height;
        if (_height is null || _follow)
        {
            _follow = false;
            _height?.Jump(natural);
            height = natural;
        }
        else if (Math.Abs(_height.Target - natural) > 0.5 && _height.IsAnimating)
        {
            _ = _height.AnimateTo(natural, MotionSpec.Spring(AiWorkflowMetrics.Spring));
            height = _height.Value;
        }
        else if (!_height.IsAnimating && Math.Abs(_height.Value - natural) > 0.5)
        {
            if (_leaving is not null && IsLoaded)
            {
                _ = _height.AnimateTo(natural, MotionSpec.Spring(AiWorkflowMetrics.Spring));
                height = _height.Value;
            }
            else
            {
                _height.Jump(natural);
                height = natural;
            }
        }
        else
        {
            height = _height.Value;
        }
        return new Size(content.DesiredSize.Width, Math.Max(0, height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _current?.Arrange(new Rect(0, 0, finalSize.Width, _current.DesiredSize.Height));
        _leaving?.Arrange(new Rect(0, 0, finalSize.Width, _leaving.DesiredSize.Height));
        var moving = _height?.IsAnimating == true || _leaving is not null;
        if (moving)
        {
            _clip.Rect = new Rect(-8, 0, finalSize.Width + 16, finalSize.Height);
            Clip = _clip;
        }
        else
        {
            Clip = null;
        }
        return finalSize;
    }
}
