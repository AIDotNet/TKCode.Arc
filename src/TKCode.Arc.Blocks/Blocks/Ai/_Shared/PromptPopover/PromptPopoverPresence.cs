using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// Shows a prompt studio popover surface in its popup (M1's <see cref="ModelMenuPopup"/>: overlay hosting, floating shadow,
/// left or right edges aligned with the pill and kept inside the window) with Arc's popover motion, played on the popup's
/// motion actor so the shadow moves with the surface. Enter: opacity 0 → 1 in 160ms on the enter curve while the transform
/// goes from 5px toward the pill at scale 0.97 to rest on Arc's 580ms CSS spring curve, about the corner next to the pill
/// (top left for the version menu, top right for the model menu; the bottom corner when the popover flipped above). Exit:
/// 130ms standard to 2.5px and scale 0.98 while fading out. A close during the enter (or an open during the exit) continues
/// from the values on screen. Reduced motion: a 120ms linear fade, no transform; Off: instant.
/// <para>
/// Code-driven (MotionValue) rather than the popup's own motions: opacity and transform run on two curves and durations, a
/// reversal must continue from the values on screen (the popup's motions restart from fixed start values), the origin is the
/// pill's corner, and the popup may only close after the exit has played. Same model as M1's <c>ModelMenuPresence</c>,
/// with the studio's distances and for any surface.
/// </para>
/// </summary>
internal sealed class PromptPopoverPresence : IDisposable
{
    internal const double EnterTravel = 5;
    internal const double EnterScale = 0.97;
    internal const double ExitTravel = 2.5;
    internal const double ExitScale = 0.98;
    internal static readonly TimeSpan ExitDuration = TimeSpan.FromMilliseconds(130);
    private static readonly Easing s_linear = new LinearEasing();

    private readonly MotionValue<double> _opacity;
    private readonly MotionValue<Point> _pose;
    private readonly MatrixTransform _transform = new();
    private readonly MatrixTransform _fallbackTransform = new();
    private ArcPopupFrame? _actor;
    private bool _closing;
    private int _generation;

    public PromptPopoverPresence(Control surface, ModelMenuPopup popup)
    {
        Surface = surface;
        Popup = popup;
        Popup.Child = surface;
        Popup.Opened += OnPopupOpened;
        _opacity = new MotionValue<double>(surface, 0, MotionVectors.Double);
        // X: travel toward the pill in px, Y: scale.
        _pose = new MotionValue<Point>(surface, new Point(0, 1), MotionVectors.Point);
        _opacity.Changed += _ => Apply();
        _pose.Changed += _ => Apply();
        surface.SizeChanged += (_, _) => Apply();
    }

    public ModelMenuPopup Popup { get; }

    public Control Surface { get; }

    /// <summary>Open and not leaving.</summary>
    public bool IsShown => Popup.IsOpen && !_closing;

    /// <summary>Playing its exit.</summary>
    public bool IsClosing => Popup.IsOpen && _closing;

    /// <summary>Current opacity of the surface (tests).</summary>
    internal double CurrentOpacity => _opacity.Value;

    /// <summary>Current travel (px toward the pill) and scale of the surface (tests).</summary>
    internal Point CurrentPose => _pose.Value;

    /// <summary>The current transform of the surface (tests).</summary>
    internal Matrix CurrentMatrix => _actor is not null ? _transform.Matrix : _fallbackTransform.Matrix;

    /// <summary>The popup opened (the surface is in the window).</summary>
    public event EventHandler? Shown;

    /// <summary>The exit finished and the popup closed.</summary>
    public event EventHandler? Hidden;

    /// <summary>Opens the popup (already placed by the owner) and plays the enter; reverses a running exit.</summary>
    public void Show()
    {
        if (Popup.PlacementTarget is not { } target || TopLevel.GetTopLevel(target) is null)
        {
            return;
        }
        var reopening = Popup.IsOpen && _closing;
        if (Popup.IsOpen && !_closing)
        {
            return;
        }
        _generation++;
        _closing = false;
        var level = ArcMotion.GetEffectiveLevel(target);
        if (!reopening)
        {
            _opacity.Jump(level == ArcMotionLevel.Off ? 1 : 0);
            _pose.Jump(level == ArcMotionLevel.Full ? new Point(EnterTravel, EnterScale) : new Point(0, 1));
            Popup.IsOpen = true;
            if (!Popup.IsOpen)
            {
                return;
            }
        }
        Apply();
        switch (level)
        {
            case ArcMotionLevel.Full:
                _ = _opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
                _ = _pose.AnimateTo(new Point(0, 1), MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
                break;
            case ArcMotionLevel.Reduced:
                _pose.Jump(new Point(0, 1));
                _ = _opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
                break;
            default:
                _opacity.Jump(1);
                _pose.Jump(new Point(0, 1));
                break;
        }
    }

    /// <summary>Plays the exit and closes the popup afterwards (at once when <paramref name="immediate"/>).</summary>
    public void Hide(bool immediate = false)
    {
        if (!Popup.IsOpen)
        {
            return;
        }
        var target = Popup.PlacementTarget;
        var level = target is not null && TopLevel.GetTopLevel(target) is not null ? ArcMotion.GetEffectiveLevel(target) : ArcMotionLevel.Off;
        if (immediate || level == ArcMotionLevel.Off || TopLevel.GetTopLevel(Surface) is null)
        {
            Finish();
            return;
        }
        if (_closing)
        {
            return;
        }
        _closing = true;
        var generation = ++_generation;
        Task<bool> task;
        if (level == ArcMotionLevel.Full)
        {
            var exit = MotionSpec.Tween(ExitDuration, ArcEasings.Standard);
            _ = _pose.AnimateTo(new Point(ExitTravel, ExitScale), exit);
            task = _opacity.AnimateTo(0, exit);
        }
        else
        {
            task = _opacity.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
        }
        if (task.IsCompleted)
        {
            Finish();
        }
        else
        {
            _ = FinishWhenDone(task, generation);
        }
    }

    private async Task FinishWhenDone(Task<bool> task, int generation)
    {
        var done = await task;
        if (done && generation == _generation && _closing)
        {
            Finish();
        }
    }

    private void Finish()
    {
        _generation++;
        _closing = false;
        if (Popup.IsOpen)
        {
            Popup.IsOpen = false;
        }
        _opacity.Jump(0);
        _pose.Jump(new Point(0, 1));
        Hidden?.Invoke(this, EventArgs.Empty);
    }

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        _actor = Surface.FindAncestorOfType<ArcPopupFrame>();
        if (_actor is not null)
        {
            _actor.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
            _actor.RenderTransform = _transform;
        }
        Apply();
        Shown?.Invoke(this, EventArgs.Empty);
    }

    private void Apply()
    {
        var opacity = Math.Clamp(_opacity.Value, 0, 1);
        var pose = _pose.Value;
        var matrix = Matrix.Identity;
        if (pose != new Point(0, 1))
        {
            var (origin, toward) = OriginAndDirection();
            // The surface starts toward the pill: below it, it starts higher.
            var shift = toward * pose.X;
            matrix = Matrix.CreateTranslation(-origin.X, -origin.Y) * Matrix.CreateScale(pose.Y, pose.Y) *
                     Matrix.CreateTranslation(origin.X + shift.X, origin.Y + shift.Y);
        }
        if (_actor is { } actor && TopLevel.GetTopLevel(actor) is not null)
        {
            actor.Opacity = opacity;
            _transform.Matrix = matrix;
            if (Surface.RenderTransform is not null || Surface.Opacity < 1)
            {
                Surface.Opacity = 1;
                Surface.RenderTransform = null;
            }
        }
        else
        {
            // Before the popup frame exists the surface carries the motion itself.
            Surface.Opacity = opacity;
            Surface.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
            _fallbackTransform.Matrix = matrix;
            Surface.RenderTransform = _fallbackTransform;
        }
    }

    /// <summary>Transform origin in actor coordinates (the corner next to the pill) and the unit vector toward the pill.</summary>
    private (Point Origin, Vector Toward) OriginAndDirection()
    {
        var size = Surface.Bounds.Size;
        var topLeft = _actor is { } actor && Surface.TranslatePoint(default, actor) is { } p ? p : default;
        var above = Popup.IsAbove;
        var x = Popup.HorizontalAlign switch
        {
            < 0 => topLeft.X,
            > 0 => topLeft.X + size.Width,
            _ => topLeft.X + size.Width / 2
        };
        return (new Point(x, topLeft.Y + (above ? size.Height : 0)), new Vector(0, above ? 1 : -1));
    }

    public void Dispose()
    {
        Hide(immediate: true);
        _opacity.Dispose();
        _pose.Dispose();
    }
}
