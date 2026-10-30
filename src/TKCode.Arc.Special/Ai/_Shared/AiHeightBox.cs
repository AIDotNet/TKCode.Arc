using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The animated clip box of the Special › AI components (Arc's measured-height containers): a disclosure that opens and
/// closes its content (a tool call's panels, a reasoning body, a feedback form, an incident), or, kept open with
/// <see cref="FollowsContent"/>, a box whose height follows content that grows and shrinks (a thread, a draft, a summary).
/// Opening springs the height from 0 to the content on <see cref="HeightSpring"/> (<see cref="ArcSpring.Smooth"/> by
/// default: critically damped, measured ω 13.1) while the content fades in (<see cref="FadeInDuration"/> on
/// <see cref="FadeInEasing"/> after <see cref="FadeInDelay"/>); closing springs to 0 while it fades out over
/// <see cref="FadeOutDuration"/>, then the content leaves the visual tree (Arc unmounts it), or stays in it hidden with
/// <see cref="KeepsContent"/>. A fade of zero leaves the content opaque. At rest an open box is auto height: content that
/// grows or shrinks (a fold opening inside, a row's reason line) is followed at once, since those parts animate themselves;
/// with <see cref="FollowsContent"/> it springs there instead, and a change of the available width (a resize that rewraps
/// the text) still cuts. While <see cref="IsHeld"/> the open height never drops below what it was when the hold began.
/// The box clips only while it moves, <see cref="CrossClipOverflow"/> wider on each side. Reduced motion and Off: everything
/// changes at once; with <see cref="FadesWhenReduced"/>, reduced motion cuts the height but keeps the fades.
/// <para>
/// Code-driven (<see cref="MotionValue{T}"/>) because the target is the measured content height, a toggle mid-way must keep
/// the spring's velocity and the unmount waits for the spring to settle; a ControlTheme transition can express none of
/// these.
/// </para>
/// </summary>
internal sealed class AiHeightBox : Control
{
    /// <summary>Whether the content is shown.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AiHeightBox, bool>(nameof(IsExpanded));

    /// <summary>What opens and closes.</summary>
    public static readonly StyledProperty<Control?> ContentProperty =
        AvaloniaProperty.Register<AiHeightBox, Control?>(nameof(Content));

    /// <summary>
    /// Whether the open height may not drop below what it was when the hold began (a draft that clears and streams again);
    /// longer content still grows it, and releasing springs to the content.
    /// </summary>
    public static readonly StyledProperty<bool> IsHeldProperty =
        AvaloniaProperty.Register<AiHeightBox, bool>(nameof(IsHeld));

    /// <summary>How far the content may overflow left and right while the box clips (focus rings, highlights).</summary>
    public static readonly StyledProperty<double> CrossClipOverflowProperty =
        AvaloniaProperty.Register<AiHeightBox, double>(nameof(CrossClipOverflow));

    /// <summary>
    /// The height spring most of the AI components use (Arc's <c>visualDuration: .24, bounce: 0</c>): panels, details, threads,
    /// blocks and rows.
    /// </summary>
    internal static readonly SpringSpec PanelSpring = SpringSpec.FromVisual(0.24, 0);

    private static readonly Easing s_easeOut = new SplineEasing(0, 0, 0.58, 1);

    private readonly RectangleGeometry _clip = new();
    private MotionValue<double>? _height;
    private MotionValue<double>? _fade;
    private Task _settling = Task.CompletedTask;
    private Control? _mounted;
    private double _natural;
    private double _floor;
    private double? _lastWidth;
    private bool _opening;
    private bool _instant;
    private bool _jumpOnMeasure;
    private bool _keepsContent;
    private bool _hidden;
    private bool _inputBlocked;
    private int _closeRun;
    private double _laidOut = double.NaN;

    static AiHeightBox()
    {
        ClipToBoundsProperty.OverrideDefaultValue<AiHeightBox>(false);
        AffectsMeasure<AiHeightBox>(IsHeldProperty);
        AffectsArrange<AiHeightBox>(CrossClipOverflowProperty);
    }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <inheritdoc cref="ContentProperty"/>
    [Content]
    public Control? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <inheritdoc cref="IsHeldProperty"/>
    public bool IsHeld { get => GetValue(IsHeldProperty); set => SetValue(IsHeldProperty, value); }

    /// <inheritdoc cref="CrossClipOverflowProperty"/>
    public double CrossClipOverflow { get => GetValue(CrossClipOverflowProperty); set => SetValue(CrossClipOverflowProperty, value); }

    /// <summary>The spring of the height (<see cref="ArcSpring.Smooth"/> by default; the Reasoning block uses the 0.26s settle).</summary>
    public SpringSpec HeightSpring { get; set; } = ArcMotionPresets.Smooth;

    /// <summary>The content's fade in (160ms by default; zero shows it at once).</summary>
    public TimeSpan FadeInDuration { get; set; } = ArcMotionPresets.Fast;

    /// <summary>The fade in's curve (Arc's standard curve by default; CSS ease-out for appearing rows and groups).</summary>
    public Easing FadeInEasing { get; set; } = ArcEasings.Standard;

    /// <summary>A wait before the fade in (60ms for a row appended by a live run).</summary>
    public TimeSpan FadeInDelay { get; set; }

    /// <summary>The content's fade out (160ms on the standard curve by default; zero keeps it opaque while it closes).</summary>
    public TimeSpan FadeOutDuration { get; set; } = ArcMotionPresets.Fast;

    /// <summary>Whether closing animates; false shuts the box at once (a group emptied for a new run).</summary>
    public bool AnimatesClose { get; set; } = true;

    /// <summary>
    /// Whether the content stays in the visual tree when closed, hidden (out of layout, input and the tab order), instead of
    /// leaving it; for content whose state must survive a close (a form, a row that owns streams).
    /// </summary>
    public bool KeepsContent
    {
        get => _keepsContent;
        set
        {
            if (_keepsContent == value)
            {
                return;
            }
            _keepsContent = value;
            if (value && _mounted is null && Content is not null)
            {
                Mount();
                if (!IsExpanded)
                {
                    Hide();
                }
            }
        }
    }

    /// <summary>
    /// Whether a change of the content's height while open springs (keeping any velocity) instead of being followed at once;
    /// a change of the available width (a resize that rewraps the text) still cuts.
    /// </summary>
    public bool FollowsContent { get; set; }

    /// <summary>Whether reduced motion keeps the fades (the height still cuts); otherwise reduced motion changes everything at once.</summary>
    public bool FadesWhenReduced { get; set; }

    /// <summary>Whether the content stops taking pointer input as soon as a close starts (what fades out leaves input at once).</summary>
    public bool DisablesInputWhileClosing { get; set; }

    /// <summary>CSS <c>ease-out</c>, the curve of the appear fades.</summary>
    internal static Easing EaseOut => s_easeOut;

    /// <summary>Whether the content is in the tree (hidden or not).</summary>
    internal bool IsMounted => _mounted is not null;

    /// <summary>The height drawn now.</summary>
    internal double DisplayedHeight => _mounted is null ? 0 : _height?.Value ?? 0;

    /// <summary>The height the box is heading to.</summary>
    internal double TargetHeight => _height?.Target ?? 0;

    /// <summary>The content's opacity drawn now.</summary>
    internal double DisplayedOpacity => _mounted is null ? 0 : _fade?.Value ?? 1;

    /// <summary>True while the height or the fade runs.</summary>
    internal bool IsMoving => _height?.IsAnimating == true || _fade?.IsAnimating == true;

    /// <summary>The content's natural height (last measure).</summary>
    internal double NaturalHeight => _natural;

    /// <summary>Raised when the content left the tree after closing (not with <see cref="KeepsContent"/>).</summary>
    internal event EventHandler? ContentUnmounted;

    /// <summary>
    /// Raised, posted, when a close has finished: the height reached 0 and the content left or hid (also after a close at
    /// once). Not raised when the box opened again meanwhile.
    /// </summary>
    internal event EventHandler? Closed;

    /// <summary>Completes when the height is at rest (at once when it already is).</summary>
    internal Task WhenSettled() => _height?.IsAnimating == true ? _settling : Task.CompletedTask;

    /// <summary>Opens without motion (the first state, or a box that appears as part of a larger one).</summary>
    internal void OpenInstantly()
    {
        _instant = true;
        SetCurrentValue(IsExpandedProperty, true);
        _instant = false;
        _height?.Jump(Goal);
        _fade?.Jump(1);
        ApplyOpacity(1);
        _jumpOnMeasure = true;
        InvalidateMeasure();
    }

    /// <summary>Closes without motion; a close under way finishes at once.</summary>
    internal void CloseInstantly()
    {
        _instant = true;
        if (IsExpanded)
        {
            SetCurrentValue(IsExpandedProperty, false);
        }
        else
        {
            Close();
        }
        _instant = false;
    }

    /// <summary>Opens once the box is loaded, so the opening animates (a row added to a list that is already on screen).</summary>
    internal void OpenWhenLoaded()
    {
        if (IsLoaded)
        {
            SetCurrentValue(IsExpandedProperty, true);
            return;
        }
        void OnLoaded(object? sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            SetCurrentValue(IsExpandedProperty, true);
        }
        Loaded += OnLoaded;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            if (change.GetNewValue<bool>())
            {
                Open();
            }
            else
            {
                Close();
            }
        }
        else if (change.Property == IsHeldProperty)
        {
            _floor = change.GetNewValue<bool>() ? DisplayedHeight : 0;
        }
        else if (change.Property == ContentProperty)
        {
            var wasMounted = _mounted is not null;
            Unmount(raise: false);
            if (change.OldValue is Control oldContent)
            {
                LogicalChildren.Remove(oldContent);
            }
            if (change.NewValue is Control newContent)
            {
                LogicalChildren.Add(newContent);
            }
            if (wasMounted || IsExpanded || KeepsContent)
            {
                Mount();
                _height?.Jump(IsExpanded ? _natural : 0);
                if (IsExpanded)
                {
                    _jumpOnMeasure = true;
                    _fade?.Jump(1);
                    ApplyOpacity(1);
                }
                else if (KeepsContent)
                {
                    Hide();
                }
            }
            InvalidateMeasure();
        }
    }

    private ArcMotionLevel Level => IsLoaded && !_instant ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;

    private bool Animates => Level == ArcMotionLevel.Full;

    /// <summary>True when reduced motion cuts the height but keeps the fades.</summary>
    private bool OnlyFades => FadesWhenReduced && Level == ArcMotionLevel.Reduced;

    /// <summary>Whether the content is in the tree and not hidden.</summary>
    private bool IsShown => _mounted is not null && !_hidden;

    /// <summary>The open height: the content's, or the held height while that is taller.</summary>
    private double Goal => IsHeld ? Math.Max(_natural, _floor) : _natural;

    private void Open()
    {
        _closeRun++;
        if (_inputBlocked)
        {
            _inputBlocked = false;
            IsHitTestVisible = true;
        }
        var first = !IsShown;
        Mount();
        var onlyFades = OnlyFades;
        if (!Animates && !onlyFades)
        {
            _opening = false;
            Fade.Jump(1);
            ApplyOpacity(1);
            _jumpOnMeasure = true;
            InvalidateMeasure();
            return;
        }
        if (first || (_height?.Value ?? 0) <= 0.01)
        {
            HeightValue.Jump(0);
            Fade.Jump(0);
            ApplyOpacity(0);
        }
        // Reduced motion with fades: the height cuts to the content at once and the content only fades.
        _opening = !onlyFades;
        _jumpOnMeasure = onlyFades;
        if (FadeInDuration <= TimeSpan.Zero)
        {
            Fade.Jump(1);
            ApplyOpacity(1);
        }
        else
        {
            _ = Fade.AnimateTo(1, MotionSpec.Tween(FadeInDuration, FadeInEasing, FadeInDelay));
        }
        InvalidateMeasure();
    }

    private void Close()
    {
        _opening = false;
        _jumpOnMeasure = false;
        if (!IsShown)
        {
            HeightValue.Jump(0);
            InvalidateMeasure();
            return;
        }
        var run = ++_closeRun;
        if (DisablesInputWhileClosing)
        {
            _inputBlocked = true;
            IsHitTestVisible = false;
        }
        var onlyFades = OnlyFades;
        if ((!Animates && !onlyFades) || !AnimatesClose)
        {
            HeightValue.Jump(0);
            Fade.Jump(0);
            ApplyOpacity(0);
            FinishClose(true, run);
            InvalidateMeasure();
            return;
        }
        var fade = FadeOutDuration <= TimeSpan.Zero
            ? Task.FromResult(true)
            : Fade.AnimateTo(0, MotionSpec.Tween(FadeOutDuration, ArcEasings.Standard));
        // Reduced motion with fades: the content fades out in place, then the height cuts to 0.
        _ = AfterCloseAsync(onlyFades ? fade : SpringHeight(0), run);
    }

    private async Task AfterCloseAsync(Task<bool> motion, int run)
    {
        var reached = await motion;
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => FinishClose(reached, run));
            return;
        }
        FinishClose(reached, run);
    }

    private void FinishClose(bool reached, int run)
    {
        if (reached && run == _closeRun && !IsExpanded)
        {
            HeightValue.Jump(0);
            if (!KeepsContent)
            {
                Unmount(raise: true);
            }
            else
            {
                Hide();
                InvalidateMeasure();
            }
            RaiseClosed(run);
        }
    }

    private void RaiseClosed(int run)
    {
        if (Closed is null)
        {
            return;
        }
        // Posted: the owner may remove the box, which must not happen inside its own property change or a layout pass.
        Dispatcher.UIThread.Post(() =>
        {
            if (run == _closeRun && !IsExpanded)
            {
                Closed?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    private Task<bool> SpringHeight(double target)
    {
        var motion = HeightValue.AnimateTo(target, MotionSpec.Spring(HeightSpring));
        _settling = motion;
        return motion;
    }

    private MotionValue<double> HeightValue
    {
        get
        {
            if (_height is null)
            {
                _height = new MotionValue<double>(this, IsExpanded ? _natural : 0, MotionVectors.Double);
                _height.Changed += OnHeightChanged;
            }
            return _height;
        }
    }

    private MotionValue<double> Fade
    {
        get
        {
            if (_fade is null)
            {
                _fade = new MotionValue<double>(this, IsExpanded ? 1 : 0, MotionVectors.Double);
                _fade.Changed += ApplyOpacity;
            }
            return _fade;
        }
    }

    private void ApplyOpacity(double value)
    {
        if (_mounted is null)
        {
            return;
        }
        // A zero fade keeps the content opaque.
        var fades = IsExpanded ? FadeInDuration > TimeSpan.Zero : FadeOutDuration > TimeSpan.Zero;
        _mounted.Opacity = fades ? Math.Clamp(value, 0, 1) : 1;
    }

    private void OnHeightChanged(double value)
    {
        // Layout snaps to device pixels: sub-pixel frames of the spring's tail would measure the parents for nothing.
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var snapped = Math.Round(value * scale);
        if (snapped != _laidOut || !HeightValue.IsAnimating || Math.Abs(value - HeightValue.Target) < 0.01)
        {
            _laidOut = snapped;
            InvalidateMeasure();
        }
        UpdateClip();
    }

    private void Mount()
    {
        if (Content is not { } content)
        {
            return;
        }
        if (_mounted is null)
        {
            _mounted = content;
            VisualChildren.Add(content);
        }
        else if (!_hidden)
        {
            return;
        }
        if (_hidden)
        {
            _hidden = false;
            content.IsVisible = true;
        }
        ApplyOpacity(_fade?.Value ?? (IsExpanded ? 1 : 0));
    }

    private void Hide()
    {
        if (_mounted is not { } content || _hidden)
        {
            return;
        }
        _hidden = true;
        content.IsVisible = false;
        content.Opacity = 1;
        Clip = null;
    }

    private void Unmount(bool raise)
    {
        if (_mounted is not { } content)
        {
            return;
        }
        _mounted = null;
        VisualChildren.Remove(content);
        content.Opacity = 1;
        if (_hidden)
        {
            _hidden = false;
            content.IsVisible = true;
        }
        Clip = null;
        InvalidateMeasure();
        if (raise)
        {
            ContentUnmounted?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_mounted is not { } content)
        {
            return default;
        }
        content.Measure(availableSize.WithHeight(double.PositiveInfinity));
        _natural = content.DesiredSize.Height;
        var passive = _lastWidth is { } last && Math.Abs(last - availableSize.Width) >= 0.5;
        _lastWidth = availableSize.Width;
        var height = HeightValue;
        if (IsExpanded)
        {
            var goal = Goal;
            if (_jumpOnMeasure)
            {
                _jumpOnMeasure = false;
                height.Jump(goal);
            }
            else if (_opening)
            {
                _opening = false;
                if (Animates)
                {
                    _ = SpringHeight(goal);
                }
                else
                {
                    height.Jump(goal);
                }
            }
            else if (Math.Abs(height.Target - goal) > 0.5 || (!height.IsAnimating && Math.Abs(height.Value - goal) > 0.5))
            {
                // Still opening: the spring keeps its velocity toward the new content height. At rest the content is followed
                // at once, or on the spring with FollowsContent (a resize that rewraps the text still cuts).
                var spring = FollowsContent ? Animates && !passive : height.IsAnimating;
                if (spring)
                {
                    _ = SpringHeight(goal);
                }
                else
                {
                    height.Jump(goal);
                }
            }
        }
        UpdateClip();
        return new Size(content.DesiredSize.Width, Math.Max(0, height.Value));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_mounted is { } content)
        {
            content.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(_natural, 0)));
        }
        UpdateClip();
        return finalSize;
    }

    private void UpdateClip()
    {
        if (_mounted is null || _height is null)
        {
            Clip = null;
            return;
        }
        var shown = Math.Max(0, _height.Value);
        var moving = _height.IsAnimating && Math.Abs(_height.Value - _height.Target) > 0.01;
        if (!moving && shown >= _natural - 0.5)
        {
            Clip = null;
            return;
        }
        var cross = CrossClipOverflow;
        // One geometry, updated in place: a moving clip allocates nothing per frame.
        _clip.Rect = new Rect(-cross, 0, Math.Max(0, Bounds.Width) + 2 * cross, shown);
        Clip = _clip;
        InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Away from a window the motion would only jump later: settle now.
        var closing = !IsExpanded && IsShown;
        var run = ++_closeRun;
        _opening = false;
        if (IsExpanded)
        {
            _fade?.Jump(1);
            ApplyOpacity(1);
            _height?.Jump(Goal);
        }
        else
        {
            _fade?.Jump(0);
            _height?.Jump(0);
            if (!KeepsContent)
            {
                Unmount(raise: false);
            }
            else
            {
                Hide();
            }
            if (closing)
            {
                RaiseClosed(run);
            }
        }
    }
}
