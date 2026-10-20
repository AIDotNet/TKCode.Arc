using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>A card stack decision: thrown left (Arc "Pass") or right ("Keep").</summary>
public enum CardDecision
{
    Left,
    Right
}

/// <summary>Event data of <see cref="CardStack.Decided"/> and <see cref="CardStack.DecisionUndone"/>.</summary>
public sealed class CardStackDecisionEventArgs : RoutedEventArgs
{
    internal CardStackDecisionEventArgs(RoutedEvent routedEvent, object? item, int index, CardDecision decision)
        : base(routedEvent)
    {
        Item = item;
        Index = index;
        Decision = decision;
    }

    /// <summary>The decided (or restored) item.</summary>
    public object? Item { get; }

    /// <summary>Its index in the items.</summary>
    public int Index { get; }

    public CardDecision Decision { get; }
}

/// <summary>
/// A deck of cards reviewed one at a time (Arc card stack): drag or flick the top card left or right, or use the
/// buttons and arrow keys. Use it for quick, one-by-one triage where each item deserves a moment of attention:
/// shortlisting destinations, candidates or design options. The top card tilts with the drag and shows a stamp; a
/// flick or a long enough drag throws it out with the release velocity, the cards behind rise into place, Undo flies
/// the last card back from where it left, and Start over deals the deck back. Only the top four cards (and cards still
/// in flight) are realized, however long the stack.
/// <para>
/// Keyboard on the focused stage: ← / → decide, Backspace or Ctrl / Cmd + Z undo. Decisions, undo and reset are
/// announced politely. Items come from <c>Items</c> / <c>ItemsSource</c> with <c>ItemTemplate</c>; changing the items
/// starts the stack over.
/// </para>
/// </summary>
[TemplatePart(StagePart, typeof(Panel))]
[TemplatePart(EmptyPart, typeof(Border))]
[TemplatePart(EmptyPresenterPart, typeof(ContentPresenter))]
[TemplatePart(StartOverButtonPart, typeof(AvaloniaButton))]
[TemplatePart(LeftButtonPart, typeof(AvaloniaButton))]
[TemplatePart(UndoButtonPart, typeof(AvaloniaButton))]
[TemplatePart(RightButtonPart, typeof(AvaloniaButton))]
[TemplatePart(AnnouncerPart, typeof(Control))]
[PseudoClasses(CompactPseudoClass, NarrowPseudoClass, ReviewedPseudoClass, CustomEmptyPseudoClass, StageFocusPseudoClass)]
public class CardStack : ItemsControl
{
    public const string StagePart = "PART_Stage";
    public const string EmptyPart = "PART_Empty";
    public const string EmptyPresenterPart = "PART_EmptyPresenter";
    public const string StartOverButtonPart = "PART_StartOverButton";
    public const string LeftButtonPart = "PART_LeftButton";
    public const string UndoButtonPart = "PART_UndoButton";
    public const string RightButtonPart = "PART_RightButton";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Narrower than 26rem: shorter cards, tighter buttons.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Narrower than 22.5rem: Undo keeps only its icon so all three buttons stay on one row.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Every card has been decided and none is still in flight (the empty state shows).</summary>
    public const string ReviewedPseudoClass = ":reviewed";

    /// <summary><see cref="EmptyContent"/> or <see cref="EmptyContentTemplate"/> replaces the default empty state.</summary>
    public const string CustomEmptyPseudoClass = ":custom-empty";

    /// <summary>The stage has keyboard focus.</summary>
    public const string StageFocusPseudoClass = ":stage-focus";

    private static readonly SpringSpec s_throwSpring = SpringSpec.FromVisual(0.5, 0);
    private static readonly TimeSpan s_reducedFade = TimeSpan.FromMilliseconds(150);
    private static readonly Avalonia.Animation.Easings.Easing s_linear = new Avalonia.Animation.Easings.LinearEasing();

    #region Properties

    /// <summary>Name of the left action, on its button and on the stamp while dragging left (default "Pass").</summary>
    public static readonly StyledProperty<string> LeftLabelProperty =
        AvaloniaProperty.Register<CardStack, string>(nameof(LeftLabel), "Pass");

    /// <summary>Name of the right action (default "Keep").</summary>
    public static readonly StyledProperty<string> RightLabelProperty =
        AvaloniaProperty.Register<CardStack, string>(nameof(RightLabel), "Keep");

    /// <summary>Past tense of the left action for announcements, such as "skipped". Without it: "Lisbon: Pass."</summary>
    public static readonly StyledProperty<string?> LeftOutcomeProperty =
        AvaloniaProperty.Register<CardStack, string?>(nameof(LeftOutcome));

    /// <summary>Past tense of the right action for announcements, such as "shortlisted".</summary>
    public static readonly StyledProperty<string?> RightOutcomeProperty =
        AvaloniaProperty.Register<CardStack, string?>(nameof(RightOutcome));

    /// <summary>Accessible name of the stack (default "Cards").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<CardStack, string>(nameof(Label), "Cards");

    /// <summary>
    /// A short name for a card, used in its accessible label and in announcements ("Lisbon"). Defaults to the item's
    /// text (a string, a control's automation name, or <c>ToString()</c>).
    /// </summary>
    public static readonly StyledProperty<Func<object?, string>?> LabelSelectorProperty =
        AvaloniaProperty.Register<CardStack, Func<object?, string>?>(nameof(LabelSelector));

    /// <summary>Shown once every card has been decided, instead of "All cards reviewed" and Start over.</summary>
    public static readonly StyledProperty<object?> EmptyContentProperty =
        AvaloniaProperty.Register<CardStack, object?>(nameof(EmptyContent));

    public static readonly StyledProperty<IDataTemplate?> EmptyContentTemplateProperty =
        AvaloniaProperty.Register<CardStack, IDataTemplate?>(nameof(EmptyContentTemplate));

    /// <summary>Index of the top card (equals the item count once every card is decided).</summary>
    public static readonly DirectProperty<CardStack, int> CurrentIndexProperty =
        AvaloniaProperty.RegisterDirect<CardStack, int>(nameof(CurrentIndex), o => o.CurrentIndex);

    /// <summary>True when there is a decision to undo.</summary>
    public static readonly DirectProperty<CardStack, bool> CanUndoProperty =
        AvaloniaProperty.RegisterDirect<CardStack, bool>(nameof(CanUndo), o => o.CanUndo);

    /// <summary>Brings every card back (for custom empty content: <c>Command="{Binding $parent[arc:CardStack].ResetCommand}"</c>).</summary>
    public static readonly DirectProperty<CardStack, ICommand> ResetCommandProperty =
        AvaloniaProperty.RegisterDirect<CardStack, ICommand>(nameof(ResetCommand), o => o.ResetCommand);

    public static readonly RoutedEvent<CardStackDecisionEventArgs> DecidedEvent =
        RoutedEvent.Register<CardStack, CardStackDecisionEventArgs>(nameof(Decided), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<CardStackDecisionEventArgs> DecisionUndoneEvent =
        RoutedEvent.Register<CardStack, CardStackDecisionEventArgs>(nameof(DecisionUndone), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RoutedEventArgs> StackResetEvent =
        RoutedEvent.Register<CardStack, RoutedEventArgs>(nameof(StackReset), RoutingStrategies.Bubble);

    public string LeftLabel { get => GetValue(LeftLabelProperty); set => SetValue(LeftLabelProperty, value); }

    public string RightLabel { get => GetValue(RightLabelProperty); set => SetValue(RightLabelProperty, value); }

    public string? LeftOutcome { get => GetValue(LeftOutcomeProperty); set => SetValue(LeftOutcomeProperty, value); }

    public string? RightOutcome { get => GetValue(RightOutcomeProperty); set => SetValue(RightOutcomeProperty, value); }

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public Func<object?, string>? LabelSelector { get => GetValue(LabelSelectorProperty); set => SetValue(LabelSelectorProperty, value); }

    public object? EmptyContent { get => GetValue(EmptyContentProperty); set => SetValue(EmptyContentProperty, value); }

    public IDataTemplate? EmptyContentTemplate
    {
        get => GetValue(EmptyContentTemplateProperty);
        set => SetValue(EmptyContentTemplateProperty, value);
    }

    private int _currentIndex;

    public int CurrentIndex
    {
        get => _currentIndex;
        private set => SetAndRaise(CurrentIndexProperty, ref _currentIndex, value);
    }

    private bool _canUndo;

    public bool CanUndo
    {
        get => _canUndo;
        private set => SetAndRaise(CanUndoProperty, ref _canUndo, value);
    }

    public ICommand ResetCommand { get; }

    /// <summary>A card was decided (by drag, key, button or <see cref="Decide"/>).</summary>
    public event EventHandler<CardStackDecisionEventArgs>? Decided
    {
        add => AddHandler(DecidedEvent, value);
        remove => RemoveHandler(DecidedEvent, value);
    }

    /// <summary>The last decision was undone; the card is back on top.</summary>
    public event EventHandler<CardStackDecisionEventArgs>? DecisionUndone
    {
        add => AddHandler(DecisionUndoneEvent, value);
        remove => RemoveHandler(DecisionUndoneEvent, value);
    }

    /// <summary>All cards were brought back.</summary>
    public event EventHandler<RoutedEventArgs>? StackReset
    {
        add => AddHandler(StackResetEvent, value);
        remove => RemoveHandler(StackResetEvent, value);
    }

    #endregion

    private readonly List<(int Index, CardDecision Decision)> _history = new();
    private readonly List<int> _flying = new();
    private readonly Dictionary<int, (Point Exit, double Tilt)> _exits = new();
    private readonly Dictionary<int, (Point Exit, double Tilt, double Delay)> _returning = new();
    private readonly Dictionary<int, Vector> _flings = new();
    private readonly MotionValue<double> _emptyProgress;
    private readonly TranslateTransform _emptyOffset = new();
    private readonly BlurEffect _emptyBlur = new() { Radius = 0 };
    private int _position;
    private double _lift;
    private bool _emptyShown;
    private bool _emptyEntering;

    private CardStackStage? _stage;
    private Border? _empty;
    private ContentPresenter? _emptyPresenter;
    private AvaloniaButton? _startOver;
    private MediaButton? _leftButton;
    private MediaButton? _undoButton;
    private MediaButton? _rightButton;
    private MediaLiveRegion? _announcer;

    public CardStack()
    {
        ResetCommand = new CardStackResetCommand(this);
        _emptyProgress = new MotionValue<double>(this, 0, MotionVectors.Double);
        _emptyProgress.Changed += OnEmptyMotion;
    }

    #region Public API

    /// <summary>Decides the top card (a button or key: thrown with a default flick).</summary>
    public void Decide(CardDecision decision)
    {
        var count = ItemCount;
        if (_position >= count)
        {
            return;
        }
        var index = _position;
        var item = ItemAt(index);
        // Only a card that is on screen can be thrown; one decided before it was ever realized just leaves.
        var thrown = Panel?.CardAt(index);
        SetLift(0);
        _position++;
        _history.Add((index, decision));
        if (thrown is not null && !_flying.Contains(index))
        {
            _flying.Add(index);
        }
        _returning.Clear();
        var left = count - _position;
        var outcome = decision == CardDecision.Right ? RightOutcome : LeftOutcome;
        var action = decision == CardDecision.Right ? RightLabel : LeftLabel;
        var name = LabelFor(index);
        Announce($"{name}{(outcome is not null ? " " + outcome : ": " + action)}. {(left > 0 ? $"{left} left." : "All cards reviewed.")}");
        RefreshCards();
        if (thrown is not null)
        {
            Throw(thrown, index, decision);
        }
        RaiseEvent(new CardStackDecisionEventArgs(DecidedEvent, item, index, decision));
    }

    /// <summary>Undoes the last decision: the card flies back from where it left.</summary>
    public void Undo()
    {
        if (_history.Count == 0)
        {
            return;
        }
        var (index, decision) = _history[^1];
        var item = ItemAt(index);
        _position = index;
        _history.RemoveAt(_history.Count - 1);
        _flying.Remove(index);
        _returning.Clear();
        if (_exits.TryGetValue(index, out var exit))
        {
            _returning[index] = (exit.Exit, exit.Tilt, 0);
        }
        Announce($"{LabelFor(index)} restored.");
        RefreshCards();
        if (Panel?.CardAt(index) is { } card)
        {
            _returning.Remove(index);
            ReturnCard(card, 0);
        }
        RaiseEvent(new CardStackDecisionEventArgs(DecisionUndoneEvent, item, index, decision));
    }

    /// <summary>Brings every card back. The deck deals itself back: the card that will sit deepest returns first.</summary>
    public void Reset()
    {
        if (_history.Count == 0)
        {
            return;
        }
        var count = Math.Min(CardStackMath.Visible, ItemCount);
        var previous = _position;
        _returning.Clear();
        for (var i = 0; i < count; i++)
        {
            if (i < previous && _exits.TryGetValue(i, out var exit))
            {
                _returning[i] = (exit.Exit, exit.Tilt, (count - 1 - i) * 0.06);
            }
        }
        _position = 0;
        _history.Clear();
        _flying.Clear();
        _flings.Clear();
        Announce("All cards are back in the stack.");
        RefreshCards();
        if (Panel is { } panel)
        {
            foreach (var pair in panel.Cards)
            {
                if (pair.Value is CardStackCard { WasDecided: true } card)
                {
                    var delay = _returning.Remove(pair.Key, out var back) ? back.Delay : 0;
                    ReturnCard(card, delay);
                }
            }
        }
        RaiseEvent(new RoutedEventArgs(StackResetEvent));
    }

    #endregion

    #region Model

    internal CardStackPanel? Panel => ItemsPanelRoot as CardStackPanel;

    internal string? LastAnnouncement => _announcer?.LastAnnouncement;

    internal IReadOnlyList<int> FlyingIndices => _flying;

    internal bool IsCardShown(int index) => (index >= _position && index < _position + CardStackMath.Visible) || _flying.Contains(index);

    internal IEnumerable<int> ShownIndices()
    {
        var end = Math.Min(ItemCount, _position + CardStackMath.Visible);
        for (var i = _position; i < end; i++)
        {
            yield return i;
        }
        foreach (var index in _flying)
        {
            if (index < _position || index >= end)
            {
                yield return index;
            }
        }
    }

    private object? ItemAt(int index) => index >= 0 && index < ItemCount ? Items[index] : null;

    internal string LabelFor(int index)
    {
        var item = ItemAt(index);
        if (LabelSelector is { } selector)
        {
            return selector(item);
        }
        return item switch
        {
            null => string.Empty,
            string text => text,
            Control control => AutomationProperties.GetName(control) ?? string.Empty,
            _ => item.ToString() ?? string.Empty
        };
    }

    private CardDecision? DecisionOf(int index)
    {
        for (var i = _history.Count - 1; i >= 0; i--)
        {
            if (_history[i].Index == index)
            {
                return _history[i].Decision;
            }
        }
        return null;
    }

    private void Announce(string message) => _announcer?.Announce(message);

    internal void OnItemsReset()
    {
        _position = 0;
        _history.Clear();
        _flying.Clear();
        _exits.Clear();
        _returning.Clear();
        _flings.Clear();
        _lift = 0;
        UpdateDerived(animate: false);
    }

    private void UpdateDerived(bool animate = true)
    {
        var count = ItemCount;
        CurrentIndex = _position;
        CanUndo = _history.Count > 0;
        ((CardStackResetCommand)ResetCommand).RaiseCanExecuteChanged();
        if (_leftButton is not null)
        {
            _leftButton.IsSoftDisabled = _position >= count;
        }
        if (_rightButton is not null)
        {
            _rightButton.IsSoftDisabled = _position >= count;
        }
        if (_undoButton is not null)
        {
            _undoButton.IsSoftDisabled = _history.Count == 0;
        }
        UpdateEmpty(animate);
    }

    private void RefreshCards()
    {
        if (Panel is { } panel)
        {
            foreach (var pair in panel.Cards)
            {
                if (pair.Value is CardStackCard card)
                {
                    ApplyCardState(card, pair.Key, fresh: false);
                }
            }
            panel.Refresh();
        }
        UpdateDerived();
    }

    private void ApplyCardState(CardStackCard card, int index, bool fresh)
    {
        var decided = DecisionOf(index) is not null;
        var slot = decided ? 0 : index - _position;
        card.Slot = slot;
        card.ZIndex = decided ? 20 + Math.Max(0, _flying.IndexOf(index)) : CardStackMath.Visible - slot;
        var interactive = !decided && slot == 0;
        card.SetInteractive(interactive, interactive ? $"{LabelFor(index)}, {index + 1} of {ItemCount}" : null);
        card.SetFlying(decided && _flying.Contains(index));
        card.SetStageFocus(interactive && _stage?.IsFocusVisible == true);
        card.KeepLabel = RightLabel;
        card.PassLabel = LeftLabel;
        // Depth springs toward its slot; while the top card is dragged away, the cards behind already start to rise.
        var target = slot - (slot > 0 ? _lift * CardStackMath.Lift : 0);
        if (fresh)
        {
            card.Depth.Jump(target);
        }
        else if (card.Depth.Target != target)
        {
            AnimateDepth(card, target);
        }
    }

    private void AnimateDepth(CardStackCard card, double target)
    {
        if (ArcMotion.IsFull(this))
        {
            _ = card.Depth.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
        }
        else
        {
            card.Depth.Jump(target);
        }
    }

    private void SetLift(double lift)
    {
        if (Math.Abs(lift - _lift) < 1e-6)
        {
            return;
        }
        _lift = lift;
        if (Panel is not { } panel)
        {
            return;
        }
        foreach (var pair in panel.Cards)
        {
            if (pair.Value is CardStackCard { Slot: > 0 } card && DecisionOf(pair.Key) is null)
            {
                AnimateDepth(card, card.Slot - lift * CardStackMath.Lift);
            }
        }
    }

    #endregion

    #region Cards

    internal void OnCardRealized(Control container, int index)
    {
        if (container is not CardStackCard card)
        {
            return;
        }
        card.Owner = this;
        card.Index = index;
        var decided = DecisionOf(index) is not null;
        card.Reset(decided ? 0 : index - _position);
        ApplyCardState(card, index, fresh: true);
        if (decided)
        {
            card.WasDecided = true;
        }
        // A card brought back by undo or start over starts where it left the screen; with reduced motion it simply
        // fades in at home.
        if (_returning.Remove(index, out var back))
        {
            if (ArcMotion.IsFull(this))
            {
                card.Tilt = back.Tilt;
                card.X.Jump(back.Exit.X);
                card.Y.Jump(back.Exit.Y);
            }
            card.Fade.Jump(0);
            card.IsSettled = false;
            ReturnCard(card, back.Delay);
        }
    }

    internal void OnCardRecycling(Control container)
    {
        if (container is CardStackCard card)
        {
            card.CancelDrag();
            card.X.Stop();
            card.Y.Stop();
            card.Fade.Stop();
            card.Depth.Stop();
            card.Index = -1;
        }
    }

    internal void OnTopCardMoved(CardStackCard card)
    {
        if (card.Index == _position)
        {
            SetLift(CardStackMath.LiftFor(card.X.Value, card.Y.Value));
        }
    }

    internal void OnCardReleased(CardStackCard card, Point offset, Vector velocity)
    {
        var index = card.Index;
        if (index != _position || index >= ItemCount)
        {
            return;
        }
        var direction = CardStackMath.ReleaseDirection(offset, velocity, card.Bounds.Width);
        if (direction != 0)
        {
            _flings[index] = velocity;
            Decide(direction > 0 ? CardDecision.Right : CardDecision.Left);
            return;
        }
        // Below the threshold the card springs home, keeping the release velocity so it settles instead of stopping dead.
        if (!ArcMotion.IsFull(this))
        {
            card.X.Jump(0);
            card.Y.Jump(0);
            return;
        }
        _ = card.X.AnimateTo(0, MotionSpec.Spring(ArcSpring.Morph), velocity.X);
        _ = card.Y.AnimateTo(0, MotionSpec.Spring(ArcSpring.Morph), velocity.Y);
    }

    private void Throw(CardStackCard card, int index, CardDecision decision)
    {
        card.CancelDrag();
        card.IsHoming = false;
        card.WasDecided = true;
        var direction = decision == CardDecision.Right ? 1 : -1;
        var width = card.Bounds.Width > 0 ? card.Bounds.Width : 320;
        var current = new Point(card.X.Value, card.Y.Value);
        // Buttons and keys throw with a default flick; a drag keeps the finger's velocity.
        var velocity = _flings.Remove(index, out var fling) ? fling : new Vector(direction * CardStackMath.DefaultFling.X, CardStackMath.DefaultFling.Y);
        var exit = CardStackMath.ExitFor(current, velocity, direction, width);
        _exits[index] = (exit, card.Tilt);
        if (!ArcMotion.IsFull(this))
        {
            FadeOut(card, index, MotionSpec.Tween(s_reducedFade, s_linear));
            return;
        }
        _ = card.X.AnimateTo(exit.X, MotionSpec.Spring(s_throwSpring), velocity.X);
        _ = card.Y.AnimateTo(exit.Y, MotionSpec.Spring(s_throwSpring), velocity.Y);
        // The fade finishes before the spring does.
        FadeOut(card, index, MotionSpec.Tween(TimeSpan.FromMilliseconds(220), ArcEasings.Standard, TimeSpan.FromMilliseconds(120)));
    }

    private async void FadeOut(CardStackCard card, int index, MotionSpec spec)
    {
        var completed = await card.Fade.AnimateTo(0, spec);
        if (completed && card.Index == index)
        {
            OnCardGone(index);
        }
    }

    private void OnCardGone(int index)
    {
        if (_flying.Remove(index))
        {
            RefreshCards();
        }
    }

    /// <summary>Undo, or a first mount from where the card left: come back from there, catching any flight in progress.</summary>
    private void ReturnCard(CardStackCard card, double delaySeconds)
    {
        if (!card.WasDecided && card.IsSettled)
        {
            return;
        }
        card.WasDecided = false;
        card.IsSettled = true;
        var delay = TimeSpan.FromSeconds(delaySeconds);
        if (!ArcMotion.IsFull(this))
        {
            card.X.Jump(0);
            card.Y.Jump(0);
            _ = card.Fade.AnimateTo(1, MotionSpec.Tween(s_reducedFade, s_linear, delay));
            return;
        }
        card.IsHoming = true;
        _ = card.Y.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth, delay));
        _ = card.Fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter, delay));
        HomeX(card, delay);
    }

    private static async void HomeX(CardStackCard card, TimeSpan delay)
    {
        if (await card.X.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth, delay)))
        {
            card.IsHoming = false;
        }
    }

    #endregion

    #region Empty state

    private void UpdateEmpty(bool animate)
    {
        var empty = _position >= ItemCount && _flying.Count == 0;
        PseudoClasses.Set(ReviewedPseudoClass, empty);
        if (empty == _emptyShown)
        {
            return;
        }
        _emptyShown = empty;
        if (_empty is null)
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        var instant = !animate || !IsLoaded || level == ArcMotionLevel.Off;
        if (empty)
        {
            _empty.IsVisible = true;
            _emptyEntering = level == ArcMotionLevel.Full;
            if (instant)
            {
                _emptyProgress.Jump(1);
                return;
            }
            _emptyProgress.Jump(0);
            _ = _emptyProgress.AnimateTo(1, _emptyEntering
                ? MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter)
                : MotionSpec.Tween(s_reducedFade, s_linear));
        }
        else
        {
            _emptyEntering = false;
            if (instant)
            {
                _emptyProgress.Jump(0);
                _empty.IsVisible = false;
                return;
            }
            HideEmpty();
        }
    }

    private async void HideEmpty()
    {
        if (await _emptyProgress.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)) && !_emptyShown &&
            _empty is not null)
        {
            _empty.IsVisible = false;
        }
    }

    private void OnEmptyMotion(double progress)
    {
        if (_empty is null)
        {
            return;
        }
        _empty.Opacity = progress;
        // Enters from 8px below out of a soft blur; leaves as a plain fade.
        var rest = _emptyEntering ? 1 - progress : 0;
        _emptyOffset.Y = 8 * rest;
        var radius = 4 * rest;
        if (radius < 0.05)
        {
            _empty.Effect = null;
        }
        else
        {
            _emptyBlur.Radius = radius;
            _empty.Effect = _emptyBlur;
        }
    }

    #endregion

    #region Focus, keys, layout

    internal void OnStageFocusChanged()
    {
        var focused = _stage?.IsFocusVisible == true;
        PseudoClasses.Set(StageFocusPseudoClass, focused);
        if (Panel?.CardAt(_position) is { } top)
        {
            top.SetStageFocus(focused && top.IsInteractive);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || _stage is null || !ReferenceEquals(e.Source, _stage))
        {
            base.OnKeyDown(e);
            return;
        }
        var command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        if (e.Key is Key.Left or Key.Right && !command)
        {
            e.Handled = true;
            Decide(e.Key == Key.Right ? CardDecision.Right : CardDecision.Left);
        }
        else if (e.Key == Key.Back || (command && e.Key == Key.Z))
        {
            e.Handled = true;
            Undo();
        }
        else
        {
            base.OnKeyDown(e);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateCompact(e.NewSize.Width);
    }

    private void UpdateCompact(double width)
    {
        // Arc media queries (26rem, 22.5rem), read from the stack's own width.
        var compact = width > 0 && width < 416;
        var narrow = width > 0 && width < 360;
        PseudoClasses.Set(CompactPseudoClass, compact);
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        foreach (var button in new[] { _leftButton, _undoButton, _rightButton })
        {
            button?.Classes.Set("compact", compact);
        }
        if (_undoButton is not null)
        {
            _undoButton.IsLabelHidden = narrow;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemCountProperty)
        {
            UpdateDerived();
        }
        else if (change.Property == LeftLabelProperty || change.Property == RightLabelProperty ||
                 change.Property == LabelProperty || change.Property == LabelSelectorProperty)
        {
            UpdateStageDescription();
            RefreshCards();
        }
        else if (change.Property == EmptyContentProperty || change.Property == EmptyContentTemplateProperty)
        {
            PseudoClasses.Set(CustomEmptyPseudoClass, EmptyContent is not null || EmptyContentTemplate is not null);
        }
    }

    private void UpdateStageDescription()
    {
        if (_stage is null)
        {
            return;
        }
        AutomationProperties.SetName(_stage, Label);
        AutomationProperties.SetHelpText(_stage,
            $"Drag the top card, or use the left and right arrow keys to {LeftLabel.ToLowerInvariant()} or {RightLabel.ToLowerInvariant()} it. Backspace undoes the last decision.");
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_stage is not null)
        {
            _stage.Owner = null;
        }
        if (_startOver is not null)
        {
            _startOver.Click -= OnStartOverClick;
        }
        if (_leftButton is not null)
        {
            _leftButton.Click -= OnLeftClick;
        }
        if (_undoButton is not null)
        {
            _undoButton.Click -= OnUndoClick;
        }
        if (_rightButton is not null)
        {
            _rightButton.Click -= OnRightClick;
        }
        if (_empty is not null)
        {
            _empty.RenderTransform = null;
            _empty.Effect = null;
        }
        _stage = e.NameScope.Find<CardStackStage>(StagePart);
        _empty = e.NameScope.Find<Border>(EmptyPart);
        _emptyPresenter = e.NameScope.Find<ContentPresenter>(EmptyPresenterPart);
        _startOver = e.NameScope.Find<AvaloniaButton>(StartOverButtonPart);
        _leftButton = e.NameScope.Find<MediaButton>(LeftButtonPart);
        _undoButton = e.NameScope.Find<MediaButton>(UndoButtonPart);
        _rightButton = e.NameScope.Find<MediaButton>(RightButtonPart);
        _announcer = e.NameScope.Find<MediaLiveRegion>(AnnouncerPart);
        if (_stage is not null)
        {
            _stage.Owner = this;
        }
        if (_startOver is not null)
        {
            _startOver.Click += OnStartOverClick;
        }
        if (_leftButton is not null)
        {
            _leftButton.Click += OnLeftClick;
        }
        if (_undoButton is not null)
        {
            _undoButton.Click += OnUndoClick;
        }
        if (_rightButton is not null)
        {
            _rightButton.Click += OnRightClick;
        }
        if (_empty is not null)
        {
            _empty.RenderTransform = _emptyOffset;
            _empty.IsVisible = false;
            _emptyShown = false;
        }
        PseudoClasses.Set(CustomEmptyPseudoClass, EmptyContent is not null || EmptyContentTemplate is not null);
        UpdateStageDescription();
        UpdateCompact(Bounds.Width);
        UpdateDerived(animate: false);
    }

    private void OnStartOverClick(object? sender, RoutedEventArgs e) => Reset();

    private void OnLeftClick(object? sender, RoutedEventArgs e) => Decide(CardDecision.Left);

    private void OnUndoClick(object? sender, RoutedEventArgs e) => Undo();

    private void OnRightClick(object? sender, RoutedEventArgs e) => Decide(CardDecision.Right);

    #endregion

    #region Containers

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new CardStackCard();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<CardStackCard>(item, out recycleKey);

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new CardStackAutomationPeer(this);

    private sealed class CardStackAutomationPeer : ControlAutomationPeer
    {
        public CardStackAutomationPeer(CardStack owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => false;

        protected override bool IsControlElementCore() => false;
    }

    private sealed class CardStackResetCommand : ICommand
    {
        private readonly CardStack _owner;

        public CardStackResetCommand(CardStack owner) => _owner = owner;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _owner.CanUndo;

        public void Execute(object? parameter) => _owner.Reset();

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
