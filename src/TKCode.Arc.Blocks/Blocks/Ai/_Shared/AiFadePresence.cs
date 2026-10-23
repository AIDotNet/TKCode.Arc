using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Shows and hides one visual the quiet way of Arc's AI chat: in, opacity 0 → 1 in 160ms on the standard curve while it
/// rises <see cref="Rise"/> px into place; out, opacity → 0 in 100ms ease-out without moving, then the visual is hidden
/// (<c>IsVisible</c> false) so it takes no input and leaves the tab order. While it fades out it is not hit-testable. A
/// reversal continues from the opacity on screen. Reduced motion fades 120ms linear both ways without rising; Off switches
/// at once. The state given to the constructor (and every change before the visual is in a window) applies without motion.
/// <para>
/// Code-driven (<see cref="MotionValue{T}"/>) rather than Core <c>PresenceHost</c>, whose fade is fixed at 240ms in and
/// 160ms out (Arc measured 160 / 100), and rather than a ControlTheme transition: one property cannot take two durations
/// (in and out), and the visual may leave the tab order only after its fade.
/// </para>
/// </summary>
internal sealed class AiFadePresence : IDisposable
{
    /// <summary>Arc's exit: 100ms.</summary>
    internal static readonly TimeSpan ExitDuration = TimeSpan.FromMilliseconds(100);

    /// <summary>CSS <c>ease-out</c>.</summary>
    private static readonly Easing s_easeOut = new SplineEasing(0, 0, 0.58, 1);

    private static readonly Easing s_linear = new LinearEasing();

    private readonly InputElement _visual;
    private readonly MotionValue<double> _progress;
    private readonly TranslateTransform _translate = new();
    private bool _shown;
    private bool _entering;
    private int _generation;

    /// <param name="visual">The visual to show and hide (its opacity, visibility and, with a rise, render transform are taken over).</param>
    /// <param name="shown">The starting state, applied at once.</param>
    /// <param name="rise">How far (px) the visual rises while it enters; 0 fades only.</param>
    public AiFadePresence(InputElement visual, bool shown, double rise = 0)
    {
        _visual = visual;
        Rise = rise;
        _shown = shown;
        _progress = new MotionValue<double>(visual, shown ? 1 : 0, MotionVectors.Double);
        _progress.Changed += Apply;
        if (rise > 0)
        {
            visual.RenderTransform = _translate;
        }
        Apply(_progress.Value);
        visual.IsVisible = shown;
        visual.IsHitTestVisible = shown;
    }

    /// <summary>How far (px) the visual rises while it enters.</summary>
    public double Rise { get; }

    /// <summary>The state asked for (the visual may still be fading out).</summary>
    public bool IsShown => _shown;

    /// <summary>The opacity on screen (0 hidden, 1 shown).</summary>
    public double Progress => _progress.Value;

    /// <summary>True while a fade runs.</summary>
    public bool IsAnimating => _progress.IsAnimating;

    /// <summary>Raised when a fade out finished and the visual was hidden.</summary>
    public event EventHandler? Exited;

    /// <summary>Shows or hides the visual (a call that asks for the current state does nothing).</summary>
    public void SetShown(bool shown)
    {
        if (shown == _shown)
        {
            return;
        }
        _shown = shown;
        var generation = ++_generation;
        var level = TopLevelOf(_visual) ? ArcMotion.GetEffectiveLevel(_visual) : ArcMotionLevel.Off;
        if (shown)
        {
            _entering = level == ArcMotionLevel.Full;
            _visual.IsVisible = true;
            _visual.IsHitTestVisible = true;
            switch (level)
            {
                case ArcMotionLevel.Full:
                    _ = _progress.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
                    break;
                case ArcMotionLevel.Reduced:
                    _ = _progress.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
                    break;
                default:
                    _progress.Jump(1);
                    break;
            }
            return;
        }
        _entering = false;
        _visual.IsHitTestVisible = false;
        if (level == ArcMotionLevel.Off)
        {
            _progress.Jump(0);
            Finish(generation);
            return;
        }
        var spec = level == ArcMotionLevel.Full
            ? MotionSpec.Tween(ExitDuration, s_easeOut)
            : MotionSpec.Tween(ArcMotionPresets.Instant, s_linear);
        var task = _progress.AnimateTo(0, spec);
        if (task.IsCompleted)
        {
            Finish(generation);
        }
        else
        {
            _ = FinishWhenDone(task, generation);
        }
    }

    private async Task FinishWhenDone(Task<bool> task, int generation)
    {
        if (await task)
        {
            Finish(generation);
        }
    }

    private void Finish(int generation)
    {
        if (generation != _generation || _shown)
        {
            return;
        }
        _visual.IsVisible = false;
        Exited?.Invoke(this, EventArgs.Empty);
    }

    private void Apply(double value)
    {
        var p = Math.Clamp(value, 0, 1);
        _visual.Opacity = p;
        if (Rise > 0)
        {
            // Only the way in moves; the way out fades in place.
            _translate.Y = _entering ? Rise * (1 - p) : 0;
        }
    }

    private static bool TopLevelOf(Visual visual) => Avalonia.Controls.TopLevel.GetTopLevel(visual) is not null;

    public void Dispose()
    {
        _progress.Changed -= Apply;
        _progress.Dispose();
    }
}
