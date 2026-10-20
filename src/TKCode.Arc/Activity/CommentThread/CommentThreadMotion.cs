using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Motion constants of <see cref="CommentThread"/> (Arc: <c>HEIGHT = physical(.42, 0)</c>).</summary>
internal static class CommentThreadMotion
{
    /// <summary>The height spring of every growing part (visual duration 0.42s, no bounce).</summary>
    public static readonly SpringSpec Height = SpringSpec.FromVisual(0.42, 0);

    /// <summary>How long a morph stays armed waiting for the size change it announced.</summary>
    public static readonly TimeSpan MorphWindow = TimeSpan.FromMilliseconds(700);
}

/// <summary>
/// A part that grows from nothing and shrinks away (Arc's <c>grow</c>: <c>height 0 → auto</c> on the HEIGHT spring with
/// a standard fade, inside <c>overflow: clip</c> with a 4px clip margin). At rest it follows its content exactly; the
/// first state is shown without motion. Reduced fades only (instant duration) and changes height at once; Off applies
/// the end state. Layout follows the height only while it moves (an intentional size spring kept in this decorator).
/// </summary>
internal class CommentThreadGrow : Decorator
{
    private enum GrowState
    {
        Shown,
        Entering,
        Leaving,
        Hidden
    }

    private GrowState _state = GrowState.Shown;
    private MotionValue<double>? _height;
    private MotionValue<double>? _fade;
    private double _target = double.NaN;
    private int _generation;
    private readonly RectangleGeometry _clip = new();

    /// <summary>Raised when an exit finished (the part is hidden and takes no space).</summary>
    public event EventHandler? Exited;

    /// <summary>Whether the part is present (entering or shown).</summary>
    public bool IsPresent => _state is GrowState.Shown or GrowState.Entering;

    /// <summary>True while the height moves.</summary>
    public bool IsMoving => _state is GrowState.Entering or GrowState.Leaving;

    /// <summary>Duration of the fade in (Arc: standard; the mention list uses fast).</summary>
    public TimeSpan EnterFade { get; set; } = ArcMotionPresets.Standard;

    /// <summary>When set, the exit is a tween of this length for height and opacity (the mention list: instant).</summary>
    public TimeSpan? ExitTween { get; set; }

    /// <summary>Horizontal overflow kept visible while clipping (Arc <c>overflow-clip-margin: 4px</c>).</summary>
    public double ClipMargin { get; set; } = 4;

    /// <summary>Shows or hides the part at once (first state, recycled views).</summary>
    public void SetPresent(bool present)
    {
        _generation++;
        _state = present ? GrowState.Shown : GrowState.Hidden;
        _height?.Stop();
        _fade?.Jump(present ? 1 : 0);
        Opacity = present ? 1 : 0;
        if (Child is { } child)
        {
            child.IsVisible = present;
        }
        Clip = null;
        InvalidateMeasure();
    }

    /// <summary>Grows the part in (from where it is when it was leaving).</summary>
    public void Enter(bool animate)
    {
        if (_state is GrowState.Shown or GrowState.Entering)
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!animate || VisualRoot is null || level == ArcMotionLevel.Off)
        {
            SetPresent(true);
            return;
        }
        ++_generation;
        var from = _state == GrowState.Leaving ? Math.Max(0, _height?.Value ?? 0) : 0;
        if (Child is { } child)
        {
            child.IsVisible = true;
        }
        _fade ??= CreateFade(_state == GrowState.Leaving ? Opacity : 0);
        if (_state == GrowState.Hidden)
        {
            _fade.Jump(0);
        }
        if (level == ArcMotionLevel.Reduced)
        {
            _state = GrowState.Shown;
            _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Enter));
            Clip = null;
            InvalidateMeasure();
            return;
        }
        _state = GrowState.Entering;
        _height ??= CreateHeight();
        _height.Jump(from);
        _target = double.NaN; // the next measure starts the spring toward the content height
        _ = _fade.AnimateTo(1, MotionSpec.Tween(EnterFade, ArcEasings.Enter));
        InvalidateMeasure();
    }

    /// <summary>Shrinks the part away, then raises <see cref="Exited"/>.</summary>
    public void Leave(bool animate)
    {
        if (_state is GrowState.Hidden or GrowState.Leaving)
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        var generation = ++_generation;
        if (!animate || VisualRoot is null || level == ArcMotionLevel.Off)
        {
            SetPresent(false);
            Exited?.Invoke(this, EventArgs.Empty);
            return;
        }
        _fade ??= CreateFade(Opacity);
        if (level == ArcMotionLevel.Reduced)
        {
            _state = GrowState.Leaving;
            _ = FadeOutAsync(generation);
            return;
        }
        _height ??= CreateHeight();
        var current = Bounds.Height;
        if (_state != GrowState.Entering)
        {
            _height.Jump(current);
        }
        _state = GrowState.Leaving;
        _target = 0;
        Task<bool> done;
        if (ExitTween is { } tween)
        {
            done = _height.AnimateTo(0, MotionSpec.Tween(tween, ArcEasings.Standard));
            _ = _fade.AnimateTo(0, MotionSpec.Tween(tween, ArcEasings.Standard));
        }
        else
        {
            done = _height.AnimateTo(0, MotionSpec.Spring(CommentThreadMotion.Height));
            _ = _fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
        }
        _ = FinishLeaveAsync(done, generation);
        InvalidateMeasure();
    }

    private async Task FadeOutAsync(int generation)
    {
        var done = await _fade!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        if (done && generation == _generation)
        {
            SetPresent(false);
            Exited?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task FinishLeaveAsync(Task<bool> done, int generation)
    {
        if (await done && generation == _generation && _state == GrowState.Leaving)
        {
            SetPresent(false);
            Exited?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Hands the height back to the content once the enter spring reached it.</summary>
    private async Task SettleAsync(Task<bool> spring, int generation)
    {
        if (await spring && generation == _generation && _state == GrowState.Entering)
        {
            _state = GrowState.Shown;
            Clip = null;
            InvalidateMeasure();
        }
    }

    private MotionValue<double> CreateHeight()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += _ => InvalidateMeasure();
        return value;
    }

    private MotionValue<double> CreateFade(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += v => Opacity = Math.Clamp(v, 0, 1);
        return value;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child || _state == GrowState.Hidden)
        {
            Child?.Measure(availableSize);
            return default;
        }
        child.Measure(availableSize.WithHeight(double.PositiveInfinity));
        var desired = child.DesiredSize;
        if (_state == GrowState.Shown)
        {
            return new Size(desired.Width, Math.Min(desired.Height, availableSize.Height));
        }
        if (_state == GrowState.Entering && !desired.Height.Equals(_target))
        {
            _target = desired.Height;
            var height = _height!;
            _ = SettleAsync(height.AnimateTo(_target, MotionSpec.Spring(CommentThreadMotion.Height), height.Velocity), _generation);
        }
        return new Size(desired.Width, Math.Max(0, _height?.Value ?? 0));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is not { } child)
        {
            return finalSize;
        }
        child.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(child.DesiredSize.Height, finalSize.Height)));
        if (IsMoving)
        {
            _clip.Rect = new Rect(-ClipMargin, 0, finalSize.Width + 2 * ClipMargin, finalSize.Height);
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

/// <summary>
/// Arc's <c>AutoHeight</c>: follows its content's height exactly, except right after <see cref="ArmMorph"/> (the view
/// changed between thread and resolved chip, or between reading, editing, confirming and picking): then the height
/// springs (HEIGHT), and further changes while it moves re-target the spring. Reduced and Off always jump. Clips only
/// while it moves.
/// </summary>
internal sealed class CommentThreadAutoHeight : Decorator
{
    private MotionValue<double>? _height;
    private double _target = double.NaN;
    private DateTime _morphUntil;
    private readonly RectangleGeometry _clip = new();

    /// <summary>Overflow kept visible left and right while clipping.</summary>
    public double ClipMargin { get; set; }

    /// <summary>True while the height springs.</summary>
    public bool IsMoving => _height?.IsAnimating == true;

    /// <summary>The next content height change springs instead of jumping.</summary>
    public void ArmMorph()
    {
        _morphUntil = DateTime.UtcNow + CommentThreadMotion.MorphWindow;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        child.Measure(availableSize.WithHeight(double.PositiveInfinity));
        var desired = child.DesiredSize;
        if (_height is null)
        {
            _height = new MotionValue<double>(this, desired.Height, MotionVectors.Double);
            _height.Changed += _ => InvalidateMeasure();
            _target = desired.Height;
        }
        else if (Math.Abs(desired.Height - _target) > 0.01)
        {
            _target = desired.Height;
            var full = IsLoaded && ArcMotion.IsFull(this);
            if (full && DateTime.UtcNow <= _morphUntil)
            {
                _morphUntil = default;
                _ = _height.AnimateTo(_target, MotionSpec.Spring(CommentThreadMotion.Height), _height.Velocity);
            }
            else if (full && _height.IsAnimating)
            {
                _ = _height.AnimateTo(_target, MotionSpec.Spring(CommentThreadMotion.Height), _height.Velocity);
            }
            else
            {
                _height.Jump(_target);
            }
        }
        return new Size(desired.Width, Math.Max(0, _height.IsAnimating ? _height.Value : _target));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is not { } child)
        {
            return finalSize;
        }
        child.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(child.DesiredSize.Height, finalSize.Height)));
        if (IsMoving)
        {
            _clip.Rect = new Rect(-ClipMargin, 0, finalSize.Width + 2 * ClipMargin, finalSize.Height);
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

/// <summary>
/// Faces that replace each other in place (AnimatePresence <c>mode="popLayout"</c>): the active face takes part in
/// layout and fades in; the face it replaces leaves the flow at once and fades out where it was, then hides. Faces are
/// kept (state survives) and a face can turn around mid-exit. Reduced and Off swap at once.
/// </summary>
internal sealed class CommentThreadFaces : Panel
{
    private sealed class FaceState
    {
        public MotionValue<double>? Fade;
        public bool Leaving;
        public int Generation;
    }

    private readonly Dictionary<Control, FaceState> _states = new();

    /// <summary>The face in the flow.</summary>
    public Control? Active { get; private set; }

    /// <summary>Fade in (Arc: standard for the thread and chip, fast for view and edit).</summary>
    public TimeSpan EnterDuration { get; set; } = ArcMotionPresets.Standard;

    /// <summary>Fade out (Arc: standard for the thread and chip, instant for view and edit).</summary>
    public TimeSpan ExitDuration { get; set; } = ArcMotionPresets.Standard;

    /// <summary>Easing of the fade out.</summary>
    public Easing ExitEasing { get; set; } = ArcEasings.Enter;

    /// <summary>Blur a face enters from and leaves to, in px (Arc: subtle 2px for the thread and chip).</summary>
    public double Blur { get; set; }

    /// <summary>Makes <paramref name="face"/> the active face (adding it when needed).</summary>
    public void Show(Control face, bool animate)
    {
        if (!Children.Contains(face))
        {
            Children.Add(face);
        }
        if (ReferenceEquals(Active, face) && !State(face).Leaving)
        {
            face.IsVisible = true;
            return;
        }
        var full = animate && IsLoaded && ArcMotion.IsFull(this);
        var previous = Active;
        Active = face;
        if (previous is not null && !ReferenceEquals(previous, face))
        {
            Hide(previous, full);
        }
        var state = State(face);
        state.Leaving = false;
        state.Generation++;
        face.IsVisible = true;
        face.IsHitTestVisible = true;
        state.Fade ??= CreateFade(face, 0);
        if (!full)
        {
            state.Fade.Jump(1);
            ApplyFade(face, 1);
        }
        else
        {
            if (!state.Fade.IsAnimating && state.Fade.Value >= 1)
            {
                state.Fade.Jump(0);
            }
            _ = state.Fade.AnimateTo(1, MotionSpec.Tween(EnterDuration, ArcEasings.Enter));
        }
        InvalidateMeasure();
    }

    private void Hide(Control face, bool animate)
    {
        var state = State(face);
        state.Leaving = true;
        var generation = ++state.Generation;
        face.IsHitTestVisible = false;
        state.Fade ??= CreateFade(face, 1);
        if (!animate)
        {
            state.Fade.Jump(0);
            ApplyFade(face, 0);
            face.IsVisible = false;
            state.Leaving = false;
            return;
        }
        _ = HideAsync(face, state, generation);
    }

    private async Task HideAsync(Control face, FaceState state, int generation)
    {
        var done = await state.Fade!.AnimateTo(0, MotionSpec.Tween(ExitDuration, ExitEasing));
        if (done && generation == state.Generation && state.Leaving)
        {
            face.IsVisible = false;
            state.Leaving = false;
            InvalidateArrange();
        }
    }

    private FaceState State(Control face)
    {
        if (!_states.TryGetValue(face, out var state))
        {
            state = new FaceState();
            _states[face] = state;
        }
        return state;
    }

    private MotionValue<double> CreateFade(Control face, double initial)
    {
        var value = new MotionValue<double>(face, initial, MotionVectors.Double);
        value.Changed += v => ApplyFade(face, v);
        return value;
    }

    private void ApplyFade(Control face, double fade)
    {
        fade = Math.Clamp(fade, 0, 1);
        face.Opacity = fade;
        face.Effect = Blur > 0 && fade < 1 ? ArcBlur.Get(Blur * (1 - fade)) : null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = default(Size);
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            child.Measure(availableSize);
            if (ReferenceEquals(child, Active))
            {
                size = child.DesiredSize;
            }
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            // A leaving face keeps its own height at the top, out of the flow (popLayout).
            var height = ReferenceEquals(child, Active) ? finalSize.Height : child.DesiredSize.Height;
            child.Arrange(new Rect(0, 0, finalSize.Width, height));
        }
        return finalSize;
    }
}

/// <summary>
/// Faces shown one after another (AnimatePresence <c>mode="wait"</c>): the old face fades out (instant) in place, then
/// the new one fades in (fast) sliding <see cref="Show"/>'s offset in x (P25). Reduced and Off swap at once.
/// </summary>
internal sealed class CommentThreadSequence : Panel
{
    private Control? _displayed;
    private Control? _wanted;
    private double _wantedOffset;
    private bool _exiting;
    private int _generation;
    private MotionValue<double>? _fade;
    private MotionValue<double>? _x;
    private readonly TranslateTransform _translate = new();

    /// <summary>Raised when a face starts entering, with whether it animates (the picker staggers its emoji).</summary>
    public event Action<Control, bool>? Entering;

    /// <summary>The face shown (or leaving before the wanted one).</summary>
    public Control? Displayed => _displayed;

    /// <summary>The face that is (or will be) shown.</summary>
    public Control? Wanted => _wanted;

    /// <summary>Shows <paramref name="face"/>, entering from <paramref name="offsetX"/> px.</summary>
    public void Show(Control face, double offsetX, bool animate)
    {
        if (!Children.Contains(face))
        {
            face.IsVisible = false;
            Children.Add(face);
        }
        _wanted = face;
        _wantedOffset = offsetX;
        var full = animate && IsLoaded && ArcMotion.IsFull(this);
        if (_displayed is null || !full)
        {
            _generation++;
            _exiting = false;
            var changed = !ReferenceEquals(_displayed, face);
            foreach (var child in Children)
            {
                child.IsVisible = ReferenceEquals(child, face);
            }
            _displayed = face;
            _fade?.Jump(1);
            _x?.Jump(0);
            Apply();
            if (changed)
            {
                Entering?.Invoke(face, false);
            }
            InvalidateMeasure();
            return;
        }
        if (ReferenceEquals(_displayed, face))
        {
            if (_exiting)
            {
                // Turn around: the face that was leaving comes back.
                _generation++;
                _exiting = false;
                _ = _fade!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
            }
            return;
        }
        if (_exiting)
        {
            return;
        }
        _ = SwapAsync(++_generation);
    }

    private async Task SwapAsync(int generation)
    {
        _exiting = true;
        _fade ??= CreateValue(1, apply: true);
        _x ??= CreateValue(0, apply: true);
        var done = await _fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        if (!done || generation != _generation || _wanted is not { } next)
        {
            return;
        }
        _exiting = false;
        if (_displayed is not null)
        {
            _displayed.IsVisible = false;
        }
        _displayed = next;
        next.IsVisible = true;
        _x.Jump(_wantedOffset);
        _fade.Jump(0);
        Apply();
        Entering?.Invoke(next, true);
        var spec = MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter);
        _ = _x.AnimateTo(0, spec);
        _ = _fade.AnimateTo(1, spec);
        InvalidateMeasure();
    }

    private MotionValue<double> CreateValue(double initial, bool apply)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        if (apply)
        {
            value.Changed += _ => Apply();
        }
        return value;
    }

    private void Apply()
    {
        if (_displayed is not { } face)
        {
            return;
        }
        face.Opacity = Math.Clamp(_fade?.Value ?? 1, 0, 1);
        _translate.X = _x?.Value ?? 0;
        if (!ReferenceEquals(face.RenderTransform, _translate))
        {
            foreach (var child in Children)
            {
                if (!ReferenceEquals(child, face) && ReferenceEquals(child.RenderTransform, _translate))
                {
                    child.RenderTransform = null;
                    child.Opacity = 1;
                }
            }
            face.RenderTransform = _translate;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            if (child.IsVisible)
            {
                child.Measure(availableSize);
            }
        }
        return _displayed is { IsVisible: true } face ? face.DesiredSize : default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            if (child.IsVisible)
            {
                child.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            }
        }
        return finalSize;
    }
}
