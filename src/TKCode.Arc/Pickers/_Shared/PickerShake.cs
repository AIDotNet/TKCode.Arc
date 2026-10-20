using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Limit feedback (P13) for pickers: Arc's invalid-input shake, x 0 → −5 → 4 → −2 → 0 over 0.32s on the standard
/// curve, played on a render transform (no relayout). Full motion only.
/// </summary>
internal sealed class PickerShake
{
    private static readonly double[] s_frames = [0, -5, 4, -2, 0];
    private readonly Control _target;
    private readonly TranslateTransform _transform = new();
    private MotionValue<double>? _progress;

    public PickerShake(Control target)
    {
        _target = target;
        target.RenderTransform = _transform;
    }

    public double Offset => _transform.X;

    public void Play()
    {
        if (!_target.IsLoaded || !ArcMotion.IsFull(_target))
        {
            return;
        }
        if (_progress is null)
        {
            _progress = new MotionValue<double>(_target, 0, MotionVectors.Double);
            _progress.Changed += p => _transform.X = PickerDrawing.Keyframes(s_frames, p);
        }
        _progress.Jump(0);
        _ = _progress.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(320), ArcEasings.Standard));
    }
}
