using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>How an <see cref="AgentMemoryPresence"/> comes in.</summary>
internal enum AgentMemoryEnter
{
    /// <summary>A group or row the filter or the search brings back: height on the settle spring, fade in 160ms after 40ms.</summary>
    Filter,

    /// <summary>A filed or added memory: its slot opens on the smooth spring while it fades in over 160ms.</summary>
    Arrival,

    /// <summary>The captured pill's row: the height opens on the smooth spring, no fade.</summary>
    Fold
}

/// <summary>
/// AnimatePresence for one part of the memory card (a group, a heading, a row, the empty texts, the captured pill's
/// row). Coming in, the height opens from zero on a spring while the part fades in; going out, the part fades out in
/// 100ms while the height closes on the settle spring (Arc's 0.32s), and <see cref="Hidden"/> follows once both are
/// done. While the height moves the content keeps its own height and is clipped (Arc's <c>overflow: hidden</c>).
/// Code-driven because the ControlTheme cannot express it: the height (spring) and the opacity (tween) run on different
/// clocks with their own delays, the part has to stay in the tree until its exit has finished, and an exit turns back
/// from where it is when the part returns. Reduced motion jumps the height and fades in over 120ms, and a part that
/// leaves goes at once (Arc removes leaving groups directly); Off applies every change at once.
/// </summary>
internal class AgentMemoryPresence : Decorator
{
    private static readonly TimeSpan s_exitFade = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan s_enterDelay = TimeSpan.FromMilliseconds(40);

    private MotionValue<double>? _height;
    private MotionValue<double>? _opacity;
    private bool _shown = true;
    private bool _leaving;
    private int _generation;

    /// <summary>False keeps the height still (only the opacity moves): very long lists skip the height motion of their rows.</summary>
    public bool AnimatesHeight { get; set; } = true;

    /// <summary>Shown, or coming in (false while leaving and after it left).</summary>
    public bool IsShown => _shown;

    /// <summary>True between <see cref="Hide"/> and the end of the exit.</summary>
    public bool IsLeaving => _leaving;

    /// <summary>The share of the content's height on screen right now (0 to 1).</summary>
    internal double HeightFraction => _height?.Value ?? (_shown ? 1 : 0);

    /// <summary>True while the height or the opacity is moving.</summary>
    internal bool IsMoving => _height?.IsAnimating == true || _opacity?.IsAnimating == true;

    /// <summary>The exit finished: the content is hidden (out of the tab order) and may be removed.</summary>
    public event EventHandler? Hidden;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ChildProperty && change.NewValue is Control child)
        {
            child.IsVisible = _shown || _leaving;
        }
    }

    private void Ensure()
    {
        if (_height is not null)
        {
            return;
        }
        _height = new MotionValue<double>(this, _shown ? 1 : 0, MotionVectors.Double);
        _height.Changed += _ => InvalidateMeasure();
        _opacity = new MotionValue<double>(this, _shown ? 1 : 0, MotionVectors.Double);
        _opacity.Changed += value => Opacity = Math.Clamp(value, 0, 1);
    }

    // Attached is enough: a part created inside a card on screen animates from its first frame.
    private ArcMotionLevel LevelFor(bool animate) =>
        animate && TopLevel.GetTopLevel(this) is not null ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;

    /// <summary>
    /// Shows the content. From hidden it starts closed and transparent; a part that is leaving turns back from where it is.
    /// </summary>
    public void Show(bool animate, AgentMemoryEnter enter = AgentMemoryEnter.Filter)
    {
        Ensure();
        var fresh = !_shown && !_leaving;
        if (_shown && _height!.Target >= 1 && _opacity!.Target >= 1)
        {
            return;
        }
        _generation++;
        _shown = true;
        _leaving = false;
        IsHitTestVisible = true;
        if (Child is { } child)
        {
            child.IsVisible = true;
        }
        var level = LevelFor(animate);
        if (level == ArcMotionLevel.Off)
        {
            _height!.Jump(1);
            _opacity!.Jump(1);
            return;
        }
        if (fresh)
        {
            _height!.Jump(level == ArcMotionLevel.Full && AnimatesHeight ? 0 : 1);
            _opacity!.Jump(enter == AgentMemoryEnter.Fold ? 1 : 0);
        }
        if (level == ArcMotionLevel.Reduced)
        {
            // No spatial motion: the height lands at once and the content fades in.
            _height!.Jump(1);
            _ = _opacity!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            return;
        }
        var spring = enter == AgentMemoryEnter.Filter ? ArcSpring.Settle : ArcSpring.Smooth;
        if (AnimatesHeight)
        {
            _ = _height!.AnimateTo(1, MotionSpec.Spring(spring));
        }
        else
        {
            _height!.Jump(1);
        }
        if (enter == AgentMemoryEnter.Fold)
        {
            _opacity!.Jump(1);
            return;
        }
        _ = _opacity!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard,
            enter == AgentMemoryEnter.Filter ? s_enterDelay : TimeSpan.Zero));
    }

    /// <summary>
    /// Plays the exit and hides the content when it finishes: a 100ms fade (unless <paramref name="fade"/> is false) while
    /// the height closes on <paramref name="spring"/> (the settle spring by default).
    /// </summary>
    public void Hide(bool animate, bool fade = true, ArcSpring? spring = null)
    {
        if (!_shown)
        {
            return;
        }
        Ensure();
        _shown = false;
        var generation = ++_generation;
        IsHitTestVisible = false;
        var level = LevelFor(animate);
        if (level != ArcMotionLevel.Full || Child is not { IsVisible: true })
        {
            Finish();
            return;
        }
        _leaving = true;
        var opacity = fade
            ? _opacity!.AnimateTo(0, MotionSpec.Tween(s_exitFade, ArcEasings.Standard))
            : Task.FromResult(true);
        var height = AnimatesHeight
            ? _height!.AnimateTo(0, MotionSpec.Spring(spring ?? ArcSpring.Settle))
            : Task.FromResult(true);
        var done = Both(opacity, height);
        if (done.IsCompleted)
        {
            Finish();
            return;
        }
        _ = FinishWhenDone(done, generation);
    }

    /// <summary>Jumps to shown or hidden without motion (the first state, Arc's <c>initial={false}</c>).</summary>
    public void Set(bool shown)
    {
        Ensure();
        _generation++;
        _shown = shown;
        _leaving = false;
        IsHitTestVisible = shown;
        _height!.Jump(shown ? 1 : 0);
        _opacity!.Jump(shown ? 1 : 0);
        if (Child is { } child)
        {
            child.IsVisible = shown;
        }
    }

    private static async Task<bool> Both(Task<bool> first, Task<bool> second) => await first & await second;

    private async Task FinishWhenDone(Task<bool> task, int generation)
    {
        var reached = await task;
        if (reached && generation == _generation && !_shown)
        {
            Finish();
        }
    }

    private void Finish()
    {
        _leaving = false;
        _height?.Jump(0);
        _opacity?.Jump(0);
        if (Child is { } child)
        {
            child.IsVisible = false;
        }
        InvalidateMeasure();
        Hidden?.Invoke(this, EventArgs.Empty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        child.Measure(availableSize.WithHeight(double.PositiveInfinity));
        var desired = child.DesiredSize;
        var fraction = Math.Max(0, HeightFraction);
        return new Size(desired.Width, Math.Min(desired.Height * fraction, availableSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is { } child)
        {
            // The content keeps its own height while the frame opens or closes around it.
            child.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(child.DesiredSize.Height, finalSize.Height)));
        }
        // Clipped only while the height is not whole, so focus rings near the edge stay whole at rest.
        ClipToBounds = HeightFraction < 0.999;
        return finalSize;
    }
}

/// <summary>
/// Reports the whole available width as its desired width, so a layer that <see cref="ArcContentSwap"/> takes out of
/// layout while it fades keeps the width of the row (its buttons stay where they were instead of sliding in).
/// </summary>
internal sealed class AgentMemoryCell : Decorator
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        return double.IsInfinity(availableSize.Width) ? size : size.WithWidth(availableSize.Width);
    }
}

/// <summary>
/// One memory in the list (Arc's <c>li</c>): the presence that brings the row in and out, the row height that follows
/// its content on the smooth spring, and the single cell where the reading row, the editor and the undo bar crossfade
/// (out 100ms, in 160ms). The height grows at once and shrinks only after the old layer has faded, because the
/// crossfade keeps measuring the leaving layer. A filed or added memory lands with an accent tint (7%) that holds for a
/// quarter of its time and then fades.
/// </summary>
internal sealed class AgentMemoryRow : AgentMemoryPresence
{
    /// <summary>Share of the accent in the landing tint (Arc: 7%).</summary>
    internal const double ArrivalAlpha = 0.07;

    /// <summary>Corner radius of the row and of its landing tint.</summary>
    internal const double Radius = 12;

    private readonly Border _arrival;
    private readonly SpringSizeDecorator _size;
    private readonly ArcContentSwap _swap;
    private MotionValue<double>? _tint;

    public AgentMemoryRow(MemoryItem memory)
    {
        Memory = memory;
        _arrival = new Border
        {
            CornerRadius = new CornerRadius(Radius),
            IsHitTestVisible = false,
            Opacity = 0
        };
        ArcTokens.Bind(_arrival, Border.BackgroundProperty, ArcDesignTokenKind.Accent);
        _swap = new ArcContentSwap
        {
            Mode = ContentSwapMode.Crossfade,
            ExitDuration = TimeSpan.FromMilliseconds(100),
            EnterDuration = ArcMotionPresets.Fast,
            MeasuresExiting = true,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        _size = new SpringSizeDecorator
        {
            Axis = SpringSizeAxis.Height,
            Spring = ArcSpring.Smooth,
            Child = _swap
        };
        Child = new Panel { Children = { _arrival, _size } };
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.ListItem);
    }

    /// <summary>The memory this row shows.</summary>
    public MemoryItem Memory { get; }

    /// <summary>The control on screen in the cell (reading row, editor or undo bar), without its cell wrapper.</summary>
    public Control? Content { get; private set; }

    /// <summary>The height follower (tests).</summary>
    internal SpringSizeDecorator SizeHost => _size;

    /// <summary>The crossfade host (tests).</summary>
    internal ArcContentSwap Swap => _swap;

    /// <summary>The landing tint's opacity right now (0 when none).</summary>
    internal double ArrivalOpacity => _arrival.Opacity;

    /// <summary>
    /// Shows <paramref name="content"/> in the cell. The first content and an unanimated change land at once; otherwise
    /// the old layer fades out while the new one fades in.
    /// </summary>
    public void SetContent(Control content, bool animate)
    {
        if (ReferenceEquals(content, Content))
        {
            return;
        }
        Content = content;
        // The content may still sit in a layer that is fading out (a quick Edit and Escape): it moves over.
        if (content.Parent is AgentMemoryCell previous)
        {
            previous.Child = null;
        }
        var cell = new AgentMemoryCell { Child = content };
        if (animate)
        {
            _swap.Content = cell;
            return;
        }
        var mode = _swap.Mode;
        _swap.Mode = ContentSwapMode.None;
        _swap.Content = cell;
        _swap.Mode = mode;
    }

    /// <summary>
    /// Lets go of the content (the row left the list): the cell is emptied so the content can be shown by another row.
    /// </summary>
    public void Release()
    {
        if (Content?.Parent is AgentMemoryCell cell)
        {
            cell.Child = null;
        }
        Content = null;
        var mode = _swap.Mode;
        _swap.Mode = ContentSwapMode.None;
        _swap.Content = null;
        _swap.Mode = mode;
    }

    /// <summary>
    /// The landing tint: opaque for the first quarter of <paramref name="duration"/>, then fading out. Code-driven because
    /// it is a one-shot pulse started by an action (filing or adding), not a state a ControlTheme transition could follow.
    /// Reduced motion fades the tint out over 600ms without the hold; Off shows none.
    /// </summary>
    public void PlayArrival(TimeSpan duration)
    {
        var level = TopLevel.GetTopLevel(this) is null ? ArcMotionLevel.Off : ArcMotion.GetEffectiveLevel(this);
        if (_tint is null)
        {
            _tint = new MotionValue<double>(this, 0, MotionVectors.Double);
            _tint.Changed += value => _arrival.Opacity = ArrivalAlpha * Math.Clamp(value, 0, 1);
        }
        if (level == ArcMotionLevel.Off || duration <= TimeSpan.Zero)
        {
            _tint.Jump(0);
            return;
        }
        _tint.Jump(1);
        if (level == ArcMotionLevel.Reduced)
        {
            _ = _tint.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(600), ArcEasings.Standard));
            return;
        }
        var hold = TimeSpan.FromTicks(duration.Ticks / 4);
        _ = _tint.AnimateTo(0, MotionSpec.Tween(duration - hold, ArcEasings.Standard, hold));
    }
}
