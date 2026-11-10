// Ported from Arc's free faq-section and contact-section blocks (MIT, Copyright (c) 2026 Elia Kuratli): the stage face of
// faq-section.tsx and the MorphPanel and Face of contact-section.tsx.

using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One surface whose faces slide vertically in the direction of travel out of a soft blur (Arc's AnimatePresence with
/// <c>mode="popLayout"</c>): the leaving face floats out of flow while the new one takes the space. With
/// <see cref="SpringsHeight"/> the surface also springs from the old height to the new one (Arc's MorphPanel) and, between
/// switches, its height is automatic.
/// </summary>
/// <remarks>
/// Driven by <see cref="MotionValue{T}"/> because a ControlTheme cannot express it: the travel depends on the direction of
/// each switch, a leaving face must outlive its removal from the layout, and the height target is measured.
/// </remarks>
internal sealed class ContentFaceSwap : Control
{
    private static readonly SpringSpec s_smooth = SpringSpec.FromVisual(0.4, 0);

    private readonly List<Control> _leaving = new();
    private readonly Dictionary<Control, FaceMotion> _motions = new();
    private Control? _current;
    private MotionValue<double>? _height;
    private double _heightNow;
    private double _fromHeight;
    private bool _heightPending;
    private bool _heightActive;

    public ContentFaceSwap()
    {
        ClipToBounds = false;
    }

    /// <summary>How far the new face travels in (px, in the direction of the switch).</summary>
    public double EnterTravel { get; init; } = 18;

    /// <summary>How far the leaving face travels out (px, against the direction of the switch).</summary>
    public double ExitTravel { get; init; } = 12;

    /// <summary>The blur of a face at either end of its travel (px).</summary>
    public double BlurRadius { get; init; } = 4;

    public TimeSpan EnterDuration { get; init; } = ArcMotionPresets.Standard;

    public TimeSpan EnterDelay { get; init; } = TimeSpan.FromMilliseconds(40);

    public TimeSpan ExitDuration { get; init; } = ArcMotionPresets.Fast;

    /// <summary>The fade of a face under reduced motion; zero swaps at once.</summary>
    public TimeSpan ReducedEnterDuration { get; init; } = ArcMotionPresets.Fast;

    public TimeSpan ReducedExitDuration { get; init; } = ArcMotionPresets.Instant;

    /// <summary>The surface springs from the old content's height to the new one's.</summary>
    public bool SpringsHeight { get; init; }

    /// <summary>The face in flow.</summary>
    public Control? Current => _current;

    /// <summary>How many faces are still leaving (tests read it).</summary>
    public int LeavingCount => _leaving.Count;

    /// <summary>Shows <paramref name="face"/>: at once the first time, then by the swap motion, travelling by <paramref name="direction"/>.</summary>
    public void Swap(Control face, int direction = 1)
    {
        if (ReferenceEquals(face, _current))
        {
            return;
        }
        var previous = _current;
        if (_leaving.Remove(face))
        {
            // The face was still on its way out: it comes back from where it is.
            _motions[face].Stop();
        }
        else
        {
            VisualChildren.Add(face);
            LogicalChildren.Add(face);
        }
        _current = face;
        face.IsHitTestVisible = true;
        if (previous is null)
        {
            InvalidateMeasure();
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        previous.IsHitTestVisible = false;
        if (level == ArcMotionLevel.Off || (level == ArcMotionLevel.Reduced && ReducedEnterDuration == TimeSpan.Zero))
        {
            Remove(previous);
            MotionOf(face).Rest();
            InvalidateMeasure();
            return;
        }
        var full = level == ArcMotionLevel.Full;
        _leaving.Add(previous);
        MotionOf(face).Enter(this, direction, full);
        MotionOf(previous).Leave(this, direction, full, () => OnLeft(previous));
        if (SpringsHeight && full)
        {
            _fromHeight = Bounds.Height;
            _heightPending = true;
        }
        InvalidateMeasure();
    }

    private FaceMotion MotionOf(Control face)
    {
        if (!_motions.TryGetValue(face, out var motion))
        {
            motion = new FaceMotion(face);
            _motions[face] = motion;
        }
        return motion;
    }

    private void OnLeft(Control face)
    {
        if (_leaving.Remove(face))
        {
            Remove(face);
        }
    }

    private void Remove(Control face)
    {
        _motions.Remove(face);
        face.Opacity = 1;
        face.Effect = null;
        face.RenderTransform = null;
        LogicalChildren.Remove(face);
        VisualChildren.Remove(face);
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var constraint = new Size(width, double.PositiveInfinity);
        foreach (var face in _leaving)
        {
            face.Measure(constraint);
        }
        if (_current is null)
        {
            return default;
        }
        _current.Measure(constraint);
        var size = _current.DesiredSize;
        if (_heightPending)
        {
            _heightPending = false;
            if (Math.Abs(_fromHeight - size.Height) >= 1)
            {
                _heightActive = true;
                _heightNow = _fromHeight;
                var target = size.Height;
                // A spring cannot start inside a measure pass: its first value invalidates the layout.
                Dispatcher.UIThread.Post(() => StartHeight(target), DispatcherPriority.Loaded);
            }
        }
        return _heightActive ? new Size(size.Width, _heightNow) : size;
    }

    private async void StartHeight(double target)
    {
        if (_height is null)
        {
            _height = new MotionValue<double>(this, _heightNow, MotionVectors.Double);
            _height.Changed += value =>
            {
                _heightNow = value;
                InvalidateMeasure();
            };
        }
        _height.Jump(_heightNow);
        var finished = await _height.AnimateTo(target, MotionSpec.Spring(s_smooth));
        if (finished)
        {
            _heightActive = false;
            InvalidateMeasure();
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var face in _leaving)
        {
            face.Arrange(new Rect(0, 0, finalSize.Width, face.DesiredSize.Height));
        }
        if (_current is not null)
        {
            _current.Arrange(new Rect(0, 0, finalSize.Width, _heightActive ? _current.DesiredSize.Height : Math.Max(finalSize.Height, _current.DesiredSize.Height)));
        }
        return finalSize;
    }

    /// <summary>The opacity, travel and blur of one face.</summary>
    private sealed class FaceMotion
    {
        private readonly Control _face;
        private readonly TranslateTransform _translate = new();
        private readonly MotionValue<double> _opacity;
        private readonly MotionValue<double> _y;
        private readonly MotionValue<double> _blur;

        public FaceMotion(Control face)
        {
            _face = face;
            _opacity = new MotionValue<double>(face, 1, MotionVectors.Double);
            _opacity.Changed += value => _face.Opacity = Math.Clamp(value, 0, 1);
            _y = new MotionValue<double>(face, 0, MotionVectors.Double);
            _y.Changed += value => _translate.Y = value;
            _blur = new MotionValue<double>(face, 0, MotionVectors.Double);
            _blur.Changed += value => _face.Effect = ArcBlur.Get(value);
        }

        public void Stop()
        {
            _opacity.Jump(_opacity.Value);
            _y.Jump(_y.Value);
            _blur.Jump(_blur.Value);
        }

        public void Rest()
        {
            _opacity.Jump(1);
            _y.Jump(0);
            _blur.Jump(0);
            _face.Opacity = 1;
            _face.Effect = null;
            _translate.Y = 0;
        }

        public void Enter(ContentFaceSwap owner, int direction, bool full)
        {
            _face.RenderTransform = _translate;
            Easing enter = ArcEasings.Enter;
            if (!full)
            {
                _opacity.Jump(0);
                _face.Opacity = 0;
                _ = _opacity.AnimateTo(1, MotionSpec.Tween(owner.ReducedEnterDuration, enter));
                return;
            }
            _opacity.Jump(0);
            _face.Opacity = 0;
            _y.Jump(direction * owner.EnterTravel);
            _translate.Y = direction * owner.EnterTravel;
            _blur.Jump(owner.BlurRadius);
            _face.Effect = ArcBlur.Get(owner.BlurRadius);
            _ = _y.AnimateTo(0, MotionSpec.Spring(s_smooth));
            _ = _opacity.AnimateTo(1, MotionSpec.Tween(owner.EnterDuration, enter, owner.EnterDelay));
            _ = _blur.AnimateTo(0, MotionSpec.Tween(owner.EnterDuration, enter, owner.EnterDelay));
        }

        public async void Leave(ContentFaceSwap owner, int direction, bool full, Action done)
        {
            _face.RenderTransform = _translate;
            var duration = full ? owner.ExitDuration : owner.ReducedExitDuration;
            var spec = MotionSpec.Tween(duration, ArcEasings.Standard);
            if (full)
            {
                _ = _y.AnimateTo(direction * -owner.ExitTravel, spec);
                _ = _blur.AnimateTo(owner.BlurRadius, spec);
            }
            var finished = await _opacity.AnimateTo(0, spec);
            if (finished)
            {
                done();
            }
        }
    }
}
