using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// One column of the Project board: the heading with its count, the muted slot behind the cards and the empty text. The
/// cards are not its children (they all live in <see cref="ProjectBoardPanel"/>); screen readers still find them inside
/// the column, a group named "&lt;stage&gt;, &lt;n&gt; tasks". The slot's height is drawn from a spring that runs on the
/// same Gentle curve as the cards, so a column that closes up never leaves a card drawn below its slot (Arc's slot
/// jumps to the new height at once).
/// </summary>
internal sealed class ProjectBoardColumn : TemplatedControl
{
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<ProjectBoardColumn, string>(nameof(Header), string.Empty);

    public static readonly StyledProperty<string> CountTextProperty =
        AvaloniaProperty.Register<ProjectBoardColumn, string>(nameof(CountText), string.Empty);

    public static readonly StyledProperty<string?> EmptyTextProperty =
        AvaloniaProperty.Register<ProjectBoardColumn, string?>(nameof(EmptyText));

    public static readonly StyledProperty<IBrush?> SlotBrushProperty =
        AvaloniaProperty.Register<ProjectBoardColumn, IBrush?>(nameof(SlotBrush));

    private readonly MotionValue<double> _slot;
    private bool _hasSlot;

    static ProjectBoardColumn()
    {
        AffectsRender<ProjectBoardColumn>(SlotBrushProperty);
        FocusableProperty.OverrideDefaultValue<ProjectBoardColumn>(false);
        IsHitTestVisibleProperty.OverrideDefaultValue<ProjectBoardColumn>(false);
    }

    public ProjectBoardColumn(ProjectBoardStage stage)
    {
        Stage = stage;
        _slot = new MotionValue<double>(this, ProjectBoardLayout.SlotMinHeight, MotionVectors.Double);
        _slot.Changed += _ => InvalidateVisual();
    }

    /// <summary>The stage shown.</summary>
    public ProjectBoardStage Stage { get; }

    public string Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    public string CountText { get => GetValue(CountTextProperty); set => SetValue(CountTextProperty, value); }

    /// <summary>The empty text, or null while the column has cards.</summary>
    public string? EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    public IBrush? SlotBrush { get => GetValue(SlotBrushProperty); set => SetValue(SlotBrushProperty, value); }

    /// <summary>The cards shown in the column, top to bottom (set by the panel; screen readers walk them).</summary>
    public IReadOnlyList<ProjectBoardCard> Cards { get; set; } = Array.Empty<ProjectBoardCard>();

    /// <summary>The slot height the layout asks for.</summary>
    public double TargetSlotHeight { get; private set; } = ProjectBoardLayout.SlotMinHeight;

    /// <summary>The slot height drawn right now.</summary>
    public double DrawnSlotHeight => _slot.Value;

    /// <summary>True while the slot's height is moving.</summary>
    public bool IsSlotMoving => _slot.IsAnimating;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<TextBlock>("PART_Count") is { } count)
        {
            count.FontFeatures = ProjectBoardLayout.TabularNumbers;
        }
    }

    /// <summary>Sets the slot height; <paramref name="spec"/> null jumps (first layout, resizes).</summary>
    public Task<bool> SetSlotHeight(double height, MotionSpec? spec)
    {
        var unchanged = _hasSlot && Math.Abs(TargetSlotHeight - height) < 0.001;
        TargetSlotHeight = height;
        if (!_hasSlot || spec is null)
        {
            _hasSlot = true;
            // An arrange that is not a board change (a scroll, a hover) leaves a running glide alone.
            if (!unchanged && (Math.Abs(_slot.Value - height) > 0.001 || _slot.IsAnimating))
            {
                _slot.Jump(height);
            }
            return Task.FromResult(true);
        }
        if (Math.Abs(_slot.Target - height) < 0.001)
        {
            return _slot.IsAnimating ? _slot.AnimateTo(height, spec) : Task.FromResult(true);
        }
        // Velocity starts at zero, like the cards' flights (Arc's layout animations restart from rest).
        return _slot.AnimateTo(height, spec, 0);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (SlotBrush is not { } brush || Bounds.Width <= 0)
        {
            return;
        }
        var height = Math.Max(0, _slot.Value);
        var rect = new Rect(0, ProjectBoardLayout.HeaderHeight, Bounds.Width, height);
        context.DrawRectangle(brush, null, new RoundedRect(rect, ProjectBoardLayout.SlotRadius));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ColumnPeer(this);

    private sealed class ColumnPeer(ProjectBoardColumn owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore()
        {
            var column = (ProjectBoardColumn)Owner;
            var peers = new List<AutomationPeer>(column.Cards.Count);
            foreach (var card in column.Cards)
            {
                peers.Add(CreatePeerForElement(card));
            }
            return peers;
        }
    }
}

/// <summary>
/// The Project board's single panel: it lays every card out in its column's rectangle and holds the column controls
/// behind them. Cards never change parent: a move changes the card's column, the panel arranges once, and
/// <see cref="ProjectsFlip"/> springs each card from where it was drawn (render transforms only). The card that changes
/// column draws above every other card while it flies. Leaving cards (filtered out, removed) keep their last place but
/// take no room while they fade; entering cards fade and grow in place. While a column closes up the panel keeps its
/// height until the springs rest, so the scroll viewport never cuts off a card that is still sliding up.
/// </summary>
internal sealed class ProjectBoardPanel : Panel
{
    private static readonly MotionSpec s_cardSpring = MotionSpec.Spring(ProjectsSprings.Card);

    private readonly ProjectsFlip _flip;
    private readonly List<ProjectBoardColumn> _columns = new();
    private readonly List<IReadOnlyList<ProjectBoardCard>> _assigned = new();
    private readonly HashSet<ProjectBoardCard> _pendingFlights = new();
    private readonly HashSet<ProjectBoardCard> _leaving = new();
    private readonly List<Task<bool>> _slotTasks = new();
    private readonly List<double> _heights = new();
    private ProjectBoardGrid _grid;
    private double _viewportWidth = double.NaN;
    private bool _isNarrow;
    private double _minColumnWidth = 200;
    private double _held;
    private double _lastHeight;
    private int _holdGeneration;

    public ProjectBoardPanel()
    {
        _flip = new ProjectsFlip(this);
        // The cards are ordered for Tab by their place on the board (column by column, top to bottom), not by when
        // they were added: each card is a Local tab group with a TabIndex inside this Local group.
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Local);
    }

    /// <summary>The FLIP tracker of the cards.</summary>
    public ProjectsFlip Flip => _flip;

    /// <summary>The columns of the last measure.</summary>
    public ProjectBoardGrid Grid => _grid;

    /// <summary>The column controls, left to right.</summary>
    public IReadOnlyList<ProjectBoardColumn> Columns => _columns;

    /// <summary>False makes moves, adds and filters change in place at once (the board's <c>AnimatesMoves</c>).</summary>
    public bool AnimatesChanges { get; set; } = true;

    /// <summary>The spring of the layout changes, or null when they jump.</summary>
    public MotionSpec? ChangeSpec => AnimatesChanges && ArcMotion.IsFull(this) ? s_cardSpring : null;

    /// <summary>The cards shown in column <paramref name="index"/>, top to bottom.</summary>
    public IReadOnlyList<ProjectBoardCard> CardsIn(int index) =>
        index >= 0 && index < _assigned.Count ? _assigned[index] : Array.Empty<ProjectBoardCard>();

    /// <summary>The cards fading out.</summary>
    public IReadOnlyCollection<ProjectBoardCard> LeavingCards => _leaving;

    /// <summary>The height the panel keeps while a column closes up (0 when none).</summary>
    public double HeldHeight => _held;

    /// <summary>Sets the width the columns share and the breakpoint; relayouts when they change.</summary>
    public void Configure(double viewportWidth, bool narrow, double minColumnWidth)
    {
        if (Math.Abs(_viewportWidth - viewportWidth) < 0.01 && _isNarrow == narrow && Math.Abs(_minColumnWidth - minColumnWidth) < 0.01)
        {
            return;
        }
        _viewportWidth = viewportWidth;
        _isNarrow = narrow;
        _minColumnWidth = minColumnWidth;
        InvalidateMeasure();
    }

    /// <summary>Replaces the column controls (the stages changed).</summary>
    public void SetColumns(IReadOnlyList<ProjectBoardColumn> columns)
    {
        foreach (var column in _columns)
        {
            if (!columns.Contains(column))
            {
                Children.Remove(column);
            }
        }
        _columns.Clear();
        _columns.AddRange(columns);
        for (var i = 0; i < columns.Count; i++)
        {
            var at = Children.IndexOf(columns[i]);
            if (at < 0)
            {
                Children.Insert(i, columns[i]);
            }
            else if (at != i)
            {
                Children.Move(at, i);
            }
        }
        InvalidateMeasure();
    }

    /// <summary>Sets which cards each column shows, top to bottom (cards must already be children).</summary>
    public void SetCards(IReadOnlyList<IReadOnlyList<ProjectBoardCard>> perColumn)
    {
        _assigned.Clear();
        _assigned.AddRange(perColumn);
        var tab = 0;
        for (var i = 0; i < _columns.Count; i++)
        {
            var cards = i < perColumn.Count ? perColumn[i] : Array.Empty<ProjectBoardCard>();
            _columns[i].Cards = cards;
            foreach (var card in cards)
            {
                KeyboardNavigation.SetTabIndex(card, tab++);
            }
        }
        InvalidateMeasure();
    }

    /// <summary>Adds a card; <paramref name="enter"/> fades it in and grows it from 98% (Gentle).</summary>
    public void AddCard(ProjectBoardCard card, bool enter)
    {
        KeyboardNavigation.SetTabNavigation(card, KeyboardNavigationMode.Local);
        Children.Add(card);
        var motion = Track(card);
        if (enter && ChangeSpec is { } spec)
        {
            motion.Opacity.Jump(0);
            motion.Scale.Jump(0.98);
            _ = motion.Opacity.AnimateTo(1, spec);
            _ = motion.Scale.AnimateTo(1, spec);
        }
    }

    /// <summary>The card will change column in the next layout: it flies above the others.</summary>
    public void MarkFlight(ProjectBoardCard card) => _pendingFlights.Add(card);

    /// <summary>Fades <paramref name="card"/> out (shrinking to 98%) and removes it; without motion it goes at once.</summary>
    public void BeginLeave(ProjectBoardCard card)
    {
        if (!Children.Contains(card))
        {
            return;
        }
        var motion = Track(card);
        card.IsLeaving = true;
        motion.IsLeaving = true;
        _leaving.Add(card);
        if (ChangeSpec is not { } spec || motion.Layout is null)
        {
            RemoveCard(card);
            return;
        }
        _ = motion.Scale.AnimateTo(0.98, spec);
        _ = FinishLeave(card, ++card.LeaveVersion, motion.Opacity.AnimateTo(0, spec));
    }

    private async Task FinishLeave(ProjectBoardCard card, int version, Task<bool> fade)
    {
        await fade;
        if (card.IsLeaving && card.LeaveVersion == version)
        {
            RemoveCard(card);
        }
    }

    /// <summary>Brings a leaving card back (the filter changed again before it was gone).</summary>
    public void Revive(ProjectBoardCard card)
    {
        if (!_leaving.Remove(card))
        {
            return;
        }
        card.IsLeaving = false;
        card.LeaveVersion++;
        var motion = Track(card);
        motion.IsLeaving = false;
        var spec = ChangeSpec ?? MotionSpec.Instant;
        _ = motion.Opacity.AnimateTo(1, spec);
        _ = motion.Scale.AnimateTo(1, spec);
    }

    /// <summary>Raised when a card has left the panel (at the end of its fade, or at once).</summary>
    public event Action<ProjectBoardCard>? CardRemoved;

    /// <summary>Removes a card at once.</summary>
    public void RemoveCard(ProjectBoardCard card)
    {
        _leaving.Remove(card);
        _pendingFlights.Remove(card);
        card.IsFlying = false;
        Children.Remove(card);
        _flip.Forget(card);
        CardRemoved?.Invoke(card);
    }

    private ProjectsItemMotion Track(ProjectBoardCard card)
    {
        if (_flip.TryGet(card, out var existing))
        {
            return existing;
        }
        var motion = _flip.Get(card);
        // A flight ends where its offset comes to rest at zero: the card goes back under the others.
        motion.Offset.Changed += v =>
        {
            if (v == default)
            {
                card.IsFlying = false;
            }
        };
        return motion;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var viewport = double.IsFinite(_viewportWidth) ? _viewportWidth : availableSize.Width;
        _grid = ProjectBoardLayout.Columns(viewport, _columns.Count, _isNarrow, _minColumnWidth);
        var cardWidth = Math.Max(0, _grid.ColumnWidth - 2 * ProjectBoardLayout.SlotPadding);
        var tallest = ProjectBoardLayout.SlotMinHeight;
        for (var i = 0; i < _columns.Count; i++)
        {
            _heights.Clear();
            foreach (var card in CardsIn(i))
            {
                card.Measure(new Size(cardWidth, double.PositiveInfinity));
                _heights.Add(card.DesiredSize.Height);
            }
            var slot = ProjectBoardLayout.SlotHeight(_heights);
            tallest = Math.Max(tallest, slot);
            _columns[i].Measure(new Size(_grid.ColumnWidth, ProjectBoardLayout.HeaderHeight + slot));
        }
        foreach (var card in _leaving)
        {
            var width = _flip.TryGet(card, out var motion) && motion.Layout is { } last ? last.Width : cardWidth;
            card.Measure(new Size(width, double.PositiveInfinity));
        }
        var height = _columns.Count == 0 ? 0 : ProjectBoardLayout.HeaderHeight + tallest + _grid.Padding;
        if (_flip.IsArmed && ChangeSpec is not null && height < _lastHeight - 0.5)
        {
            // A column is closing up: keep the old height until its slot and cards rest (see ArrangeOverride).
            _held = Math.Max(_held, _lastHeight);
            _holdGeneration++;
        }
        var desired = Math.Max(height, _held);
        _lastHeight = desired;
        return new Size(_grid.ContentWidth, desired);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var cardWidth = Math.Max(0, _grid.ColumnWidth - 2 * ProjectBoardLayout.SlotPadding);
        var spec = _flip.IsArmed ? ChangeSpec : null;
        _slotTasks.Clear();
        for (var i = 0; i < _columns.Count; i++)
        {
            var x = _grid.ColumnX(i);
            var y = ProjectBoardLayout.HeaderHeight + ProjectBoardLayout.SlotPadding;
            _heights.Clear();
            foreach (var card in CardsIn(i))
            {
                var h = card.DesiredSize.Height;
                var rect = new Rect(x + ProjectBoardLayout.SlotPadding, y, cardWidth, h);
                card.Arrange(rect);
                var flight = _pendingFlights.Remove(card);
                if (flight)
                {
                    card.IsFlying = true;
                }
                var motion = _flip.Place(card, rect);
                if (flight && !motion.Offset.IsAnimating)
                {
                    card.IsFlying = false;
                }
                _heights.Add(h);
                y += h + ProjectBoardLayout.CardGap;
            }
            var slot = ProjectBoardLayout.SlotHeight(_heights);
            var column = _columns[i];
            column.Arrange(new Rect(x, 0, _grid.ColumnWidth, ProjectBoardLayout.HeaderHeight + slot));
            var task = column.SetSlotHeight(slot, spec);
            if (column.IsSlotMoving)
            {
                _slotTasks.Add(task);
            }
        }
        foreach (var card in _leaving)
        {
            if (_flip.TryGet(card, out var motion) && motion.Layout is { } last)
            {
                card.Arrange(last);
            }
        }
        _pendingFlights.Clear();
        if (_held > 0)
        {
            _ = ReleaseHold(_holdGeneration, _slotTasks.Count == 0 ? Task.CompletedTask : Task.WhenAll(_slotTasks));
        }
        return finalSize;
    }

    private async Task ReleaseHold(int generation, Task settled)
    {
        await settled;
        // Let the frame that brought the springs to rest render before the panel shrinks.
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        if (generation != _holdGeneration || _held <= 0)
        {
            return;
        }
        foreach (var column in _columns)
        {
            if (column.IsSlotMoving)
            {
                return;
            }
        }
        _held = 0;
        InvalidateMeasure();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PanelPeer(this);

    private sealed class PanelPeer(ProjectBoardPanel owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => false;

        protected override bool IsControlElementCore() => false;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore()
        {
            var panel = (ProjectBoardPanel)Owner;
            var peers = new List<AutomationPeer>(panel._columns.Count);
            foreach (var column in panel._columns)
            {
                peers.Add(CreatePeerForElement(column));
            }
            return peers;
        }
    }
}
