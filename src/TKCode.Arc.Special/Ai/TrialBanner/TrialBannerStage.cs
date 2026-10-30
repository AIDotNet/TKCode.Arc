using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The surface of a <see cref="TrialBanner"/> and its faces (the banner row, the card, the pill). One face is shown at a
/// time; when the face changes (dismiss or snooze folds the banner into the pill, the pill opens it again) the surface's
/// rectangle springs from the old face's box to the new one's on one bounce-free spring, the old face fades out in 100ms
/// and the new one in over 160ms, and both are clipped to the moving rounded rectangle, so one surface morphs instead of
/// two boxes trading places. Height follows the rectangle (content below moves with it); width only moves the surface,
/// the control itself always fills its container.
/// <para>
/// Code-driven because the target box is measured at run time (the pill's text, the banner's wrapped copy) and a retarget
/// mid-morph must keep its velocity (<see cref="MotionValue{T}"/>). Faces are laid out once at their own size, never per
/// frame. Passive size changes (a window resize, copy that changes) jump; only a face change springs. Reduced motion and
/// Off: the new face at once.
/// </para>
/// </summary>
internal sealed class TrialBannerStage : Panel
{
    /// <summary>The surface's rectangle spring: 0.33s, no bounce (Arc's settle).</summary>
    private static readonly MotionSpec s_morph = MotionSpec.Spring(ArcSpring.Settle);

    private static readonly TimeSpan s_fadeOut = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan s_fadeIn = TimeSpan.FromMilliseconds(160);

    private readonly RectangleGeometry _clip = new();
    private readonly Dictionary<Control, MotionValue<double>> _fades = new();
    private MotionValue<Rect>? _rect;
    private Control? _active;
    private Control? _leaving;
    private Rect _leavingBox;
    private bool _morphPending;
    private bool _hasLaidOut;

    /// <summary>The rounded rectangle drawn behind the faces (styled by the theme; its radius follows the face).</summary>
    public Border? Surface { get; set; }

    /// <summary>The face on screen, or null when the banner renders nothing.</summary>
    public Control? Active => _active;

    /// <summary>True while the surface is moving between two faces.</summary>
    public bool IsMorphing => _rect?.IsAnimating == true;

    /// <summary>The surface's current rectangle.</summary>
    public Rect SurfaceRect => _rect?.Value ?? default;

    /// <summary>Shows <paramref name="face"/> (null: nothing), morphing from the current face when motion is full.</summary>
    public void Show(Control? face, bool animate)
    {
        if (ReferenceEquals(face, _active))
        {
            return;
        }
        var previous = _active;
        _active = face;
        foreach (var child in Children)
        {
            if (!ReferenceEquals(child, Surface) && !ReferenceEquals(child, face) && !ReferenceEquals(child, previous))
            {
                child.IsVisible = false;
            }
        }
        if (face is not null)
        {
            face.IsVisible = true;
        }
        var morphs = animate && _hasLaidOut && previous is not null && ArcMotion.IsFull(this) && this.IsAttachedToVisualTree();
        if (_leaving is not null && !ReferenceEquals(_leaving, face))
        {
            EndFade(_leaving, visible: false);
        }
        _leaving = null;
        if (!morphs)
        {
            if (previous is not null && !ReferenceEquals(previous, face))
            {
                EndFade(previous, visible: false);
            }
            if (face is not null)
            {
                EndFade(face, visible: true);
            }
            _morphPending = false;
            _rect?.Stop();
            InvalidateMeasure();
            return;
        }
        _leaving = previous;
        _leavingBox = previous!.Bounds;
        Fade(previous, 0, s_fadeOut);
        if (face is not null)
        {
            if (!_fades.ContainsKey(face) || !(_fades[face].IsAnimating))
            {
                face.Opacity = 0;
                Fader(face).Jump(0);
            }
            Fade(face, 1, s_fadeIn);
        }
        _morphPending = true;
        InvalidateMeasure();
    }

    private MotionValue<double> Fader(Control face)
    {
        if (!_fades.TryGetValue(face, out var fade))
        {
            fade = new MotionValue<double>(this, face.Opacity, MotionVectors.Double);
            fade.Changed += value => face.Opacity = value;
            _fades[face] = fade;
        }
        return fade;
    }

    private void Fade(Control face, double to, TimeSpan duration)
    {
        var fade = Fader(face);
        _ = fade.AnimateTo(to, MotionSpec.Tween(duration, ArcEasings.Standard));
    }

    private void EndFade(Control face, bool visible)
    {
        if (_fades.TryGetValue(face, out var fade))
        {
            fade.Jump(1);
        }
        face.Opacity = 1;
        face.IsVisible = visible;
    }

    private void EnsureRect(Rect initial)
    {
        if (_rect is not null)
        {
            return;
        }
        _rect = new MotionValue<Rect>(this, initial, MotionVectors.Rect);
        _rect.Changed += _ =>
        {
            InvalidateMeasure();
            InvalidateArrange();
        };
    }

    /// <summary>The box a face takes: stretched faces fill the width (up to their MaxWidth), the others their own size.</summary>
    private static Size FaceSize(Control face, double width)
    {
        var desired = face.DesiredSize;
        if (face.HorizontalAlignment == HorizontalAlignment.Stretch && !double.IsInfinity(width))
        {
            var max = face.MaxWidth + face.Margin.Left + face.Margin.Right;
            return new Size(Math.Min(width, max), desired.Height);
        }
        return desired;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            if (!ReferenceEquals(child, Surface) && child.IsVisible)
            {
                child.Measure(availableSize);
            }
        }
        var target = _active is null ? default : FaceSize(_active, availableSize.Width);
        var targetRect = new Rect(0, 0, target.Width, target.Height);
        EnsureRect(targetRect);
        if (_morphPending)
        {
            _morphPending = false;
            if (_rect!.Value != targetRect)
            {
                _ = Settle(targetRect);
            }
        }
        else if (_rect!.IsAnimating)
        {
            if (_rect.Target != targetRect)
            {
                _ = Settle(targetRect);
            }
        }
        else if (_rect.Value != targetRect)
        {
            _rect.Jump(targetRect);
        }
        var current = _rect.Value;
        Surface?.Measure(current.Size);
        var width = double.IsInfinity(availableSize.Width) ? current.Width : availableSize.Width;
        return new Size(width, current.Height);
    }

    private async Task Settle(Rect target)
    {
        var arrived = await _rect!.AnimateTo(target, s_morph);
        if (arrived && _leaving is { } leaving)
        {
            _leaving = null;
            EndFade(leaving, visible: false);
            InvalidateMeasure();
        }
        InvalidateArrange();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _hasLaidOut = true;
        var current = _rect?.Value ?? new Rect(finalSize);
        if (Surface is { } surface)
        {
            surface.Arrange(current);
        }
        if (_active is { } active)
        {
            var size = FaceSize(active, finalSize.Width);
            active.Arrange(new Rect(0, 0, size.Width, size.Height));
        }
        if (_leaving is { IsVisible: true } leaving)
        {
            leaving.Arrange(new Rect(0, 0, _leavingBox.Width, _leavingBox.Height));
        }
        if (IsMorphing || _leaving is not null)
        {
            var radius = Surface?.CornerRadius.TopLeft ?? 0;
            radius = Math.Min(radius, Math.Min(current.Width, current.Height) / 2);
            _clip.Rect = current;
            _clip.RadiusX = radius;
            _clip.RadiusY = radius;
            Clip = _clip;
            InvalidateVisual();
        }
        else if (Clip is not null)
        {
            Clip = null;
        }
        return finalSize;
    }
}
