using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// Finds a meeting time with an assistant: pick who comes and how long it takes, say what matters in plain words
/// ("afternoons, not Friday, keep lunch free"), and the best slots appear on a week view next to everyone's busy times,
/// each with the reasons it was picked. Choose one on the week or in the list and book it.
/// <code>
/// &lt;arc:SmartScheduler People="{Binding Team}" WeekStart="2026-11-16" SlotBooked="OnBooked" /&gt;
/// </code>
/// The limits are read by <see cref="SmartSchedulerPlanner"/> and shown back as chips; slots come from the same planner
/// unless <see cref="SlotFinder"/> is set. With no data set it shows an invented team and week.
/// </summary>
[TemplatePart(WeekPart, typeof(SmartSchedulerWeek))]
[TemplatePart(FindPart, typeof(Button))]
[TemplatePart(BookPart, typeof(Button))]
[PseudoClasses(CompactPseudoClass, ProposedPseudoClass, BookedPseudoClass, NoTimesPseudoClass)]
public partial class SmartScheduler : TemplatedControl
{
    public const string WeekPart = "PART_Week";
    public const string FindPart = "PART_Find";
    public const string BookPart = "PART_Book";

    public const string CompactPseudoClass = ":compact";
    public const string ProposedPseudoClass = ":proposed";
    public const string BookedPseudoClass = ":booked";
    public const string NoTimesPseudoClass = ":no-times";

    #region Properties

    /// <summary>Everyone who could join, with their busy times for the shown week; <see cref="SmartSchedulerPerson.IsInvited"/> sets who starts invited.</summary>
    public static readonly StyledProperty<IReadOnlyList<SmartSchedulerPerson>?> PeopleProperty =
        AvaloniaProperty.Register<SmartScheduler, IReadOnlyList<SmartSchedulerPerson>?>(nameof(People), SmartSchedulerSamples.People());

    /// <summary>The first day of the shown week.</summary>
    public static readonly StyledProperty<DateOnly> WeekStartProperty =
        AvaloniaProperty.Register<SmartScheduler, DateOnly>(nameof(WeekStart), SmartSchedulerSamples.WeekStart);

    /// <summary>How many days the week shows (5).</summary>
    public static readonly StyledProperty<int> DaysProperty =
        AvaloniaProperty.Register<SmartScheduler, int>(nameof(Days), 5);

    /// <summary>The first hour of the working day (9).</summary>
    public static readonly StyledProperty<double> DayStartHourProperty =
        AvaloniaProperty.Register<SmartScheduler, double>(nameof(DayStartHour), 9);

    /// <summary>The last hour of the working day (18).</summary>
    public static readonly StyledProperty<double> DayEndHourProperty =
        AvaloniaProperty.Register<SmartScheduler, double>(nameof(DayEndHour), 18);

    /// <summary>The meeting lengths offered, in minutes (15, 30, 45, 60).</summary>
    public static readonly StyledProperty<IReadOnlyList<int>?> DurationsProperty =
        AvaloniaProperty.Register<SmartScheduler, IReadOnlyList<int>?>(nameof(Durations), SmartSchedulerSamples.Durations());

    /// <summary>The meeting's length in minutes (two-way); a length named in the limits ("45 min") overrides it.</summary>
    public static readonly StyledProperty<int> DurationMinutesProperty =
        AvaloniaProperty.Register<SmartScheduler, int>(nameof(DurationMinutes), 30, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The limits in plain words (two-way).</summary>
    public static readonly StyledProperty<string?> ConstraintsTextProperty =
        AvaloniaProperty.Register<SmartScheduler, string?>(nameof(ConstraintsText), SmartSchedulerSamples.Constraints,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How many slots to propose (3).</summary>
    public static readonly StyledProperty<int> SlotCountProperty =
        AvaloniaProperty.Register<SmartScheduler, int>(nameof(SlotCount), 3);

    /// <summary>Finds the slots; null uses <see cref="SmartSchedulerPlanner.Find"/> after a short simulated wait. An exception shows as no times.</summary>
    public static readonly StyledProperty<Func<SmartSchedulerRequest, CancellationToken, Task<IReadOnlyList<SmartSchedulerSlot>>>?> SlotFinderProperty =
        AvaloniaProperty.Register<SmartScheduler, Func<SmartSchedulerRequest, CancellationToken, Task<IReadOnlyList<SmartSchedulerSlot>>>?>(nameof(SlotFinder));

    /// <summary>Books the chosen slot (send the invites); null waits a moment. An exception keeps the proposals and shows its message.</summary>
    public static readonly StyledProperty<Func<SmartSchedulerBookedEventArgs, CancellationToken, Task>?> BookActionProperty =
        AvaloniaProperty.Register<SmartScheduler, Func<SmartSchedulerBookedEventArgs, CancellationToken, Task>?>(nameof(BookAction));

    /// <summary>Every word the scheduler shows or reads out, with English defaults.</summary>
    public static readonly StyledProperty<SmartSchedulerLabels> LabelsProperty =
        AvaloniaProperty.Register<SmartScheduler, SmartSchedulerLabels>(nameof(Labels), new SmartSchedulerLabels());

    /// <summary>Where the scheduler stands.</summary>
    public static readonly DirectProperty<SmartScheduler, SmartSchedulerState> StateProperty =
        AvaloniaProperty.RegisterDirect<SmartScheduler, SmartSchedulerState>(nameof(State), o => o.State);

    /// <summary>The proposed slots, best first; empty before a search.</summary>
    public static readonly DirectProperty<SmartScheduler, IReadOnlyList<SmartSchedulerSlot>> SlotsProperty =
        AvaloniaProperty.RegisterDirect<SmartScheduler, IReadOnlyList<SmartSchedulerSlot>>(nameof(Slots), o => o.Slots);

    /// <summary>The chosen slot; null when none is.</summary>
    public static readonly DirectProperty<SmartScheduler, SmartSchedulerSlot?> SelectedSlotProperty =
        AvaloniaProperty.RegisterDirect<SmartScheduler, SmartSchedulerSlot?>(nameof(SelectedSlot), o => o.SelectedSlot);

    /// <summary>The limits as read from <see cref="ConstraintsText"/>.</summary>
    public static readonly DirectProperty<SmartScheduler, SmartSchedulerConstraints> ConstraintsProperty =
        AvaloniaProperty.RegisterDirect<SmartScheduler, SmartSchedulerConstraints>(nameof(Constraints), o => o.Constraints);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<SmartScheduler, double>(nameof(CompactWidth), 760);

    /// <inheritdoc cref="PeopleProperty"/>
    public IReadOnlyList<SmartSchedulerPerson>? People { get => GetValue(PeopleProperty); set => SetValue(PeopleProperty, value); }

    /// <inheritdoc cref="WeekStartProperty"/>
    public DateOnly WeekStart { get => GetValue(WeekStartProperty); set => SetValue(WeekStartProperty, value); }

    /// <inheritdoc cref="DaysProperty"/>
    public int Days { get => GetValue(DaysProperty); set => SetValue(DaysProperty, value); }

    /// <inheritdoc cref="DayStartHourProperty"/>
    public double DayStartHour { get => GetValue(DayStartHourProperty); set => SetValue(DayStartHourProperty, value); }

    /// <inheritdoc cref="DayEndHourProperty"/>
    public double DayEndHour { get => GetValue(DayEndHourProperty); set => SetValue(DayEndHourProperty, value); }

    /// <inheritdoc cref="DurationsProperty"/>
    public IReadOnlyList<int>? Durations { get => GetValue(DurationsProperty); set => SetValue(DurationsProperty, value); }

    /// <inheritdoc cref="DurationMinutesProperty"/>
    public int DurationMinutes { get => GetValue(DurationMinutesProperty); set => SetValue(DurationMinutesProperty, value); }

    /// <inheritdoc cref="ConstraintsTextProperty"/>
    public string? ConstraintsText { get => GetValue(ConstraintsTextProperty); set => SetValue(ConstraintsTextProperty, value); }

    /// <inheritdoc cref="SlotCountProperty"/>
    public int SlotCount { get => GetValue(SlotCountProperty); set => SetValue(SlotCountProperty, value); }

    /// <inheritdoc cref="SlotFinderProperty"/>
    public Func<SmartSchedulerRequest, CancellationToken, Task<IReadOnlyList<SmartSchedulerSlot>>>? SlotFinder
    {
        get => GetValue(SlotFinderProperty);
        set => SetValue(SlotFinderProperty, value);
    }

    /// <inheritdoc cref="BookActionProperty"/>
    public Func<SmartSchedulerBookedEventArgs, CancellationToken, Task>? BookAction
    {
        get => GetValue(BookActionProperty);
        set => SetValue(BookActionProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public SmartSchedulerLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="StateProperty"/>
    public SmartSchedulerState State { get => _state; private set => SetAndRaise(StateProperty, ref _state, value); }

    /// <inheritdoc cref="SlotsProperty"/>
    public IReadOnlyList<SmartSchedulerSlot> Slots { get => _slots; private set => SetAndRaise(SlotsProperty, ref _slots, value); }

    /// <inheritdoc cref="SelectedSlotProperty"/>
    public SmartSchedulerSlot? SelectedSlot { get => _selectedSlot; private set => SetAndRaise(SelectedSlotProperty, ref _selectedSlot, value); }

    /// <inheritdoc cref="ConstraintsProperty"/>
    public SmartSchedulerConstraints Constraints { get => _constraints; private set => SetAndRaise(ConstraintsProperty, ref _constraints, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    #endregion

    #region Events

    /// <summary>Raised when the slots are found (also when none were).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SlotsFoundEvent =
        RoutedEvent.Register<SmartScheduler, RoutedEventArgs>(nameof(SlotsFound), RoutingStrategies.Bubble);

    /// <summary>Raised when a slot was booked, with its times and the invited people.</summary>
    public static readonly RoutedEvent<SmartSchedulerBookedEventArgs> SlotBookedEvent =
        RoutedEvent.Register<SmartScheduler, SmartSchedulerBookedEventArgs>(nameof(SlotBooked), RoutingStrategies.Bubble);

    /// <summary>Raised when the slots are found (also when none were).</summary>
    public event EventHandler<RoutedEventArgs>? SlotsFound
    {
        add => AddHandler(SlotsFoundEvent, value);
        remove => RemoveHandler(SlotsFoundEvent, value);
    }

    /// <summary>Raised when a slot was booked, with its times and the invited people.</summary>
    public event EventHandler<SmartSchedulerBookedEventArgs>? SlotBooked
    {
        add => AddHandler(SlotBookedEvent, value);
        remove => RemoveHandler(SlotBookedEvent, value);
    }

    #endregion

    private SmartSchedulerState _state;
    private IReadOnlyList<SmartSchedulerSlot> _slots = [];
    private SmartSchedulerSlot? _selectedSlot;
    private SmartSchedulerConstraints _constraints = new();
    private readonly HashSet<string> _invited = new(StringComparer.Ordinal);
    private CancellationTokenSource? _cts;
    private int _generation;

    static SmartScheduler()
    {
        FocusableProperty.OverrideDefaultValue<SmartScheduler>(false);
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<SmartScheduler>(AutomationLandmarkType.Region);
    }

    public SmartScheduler()
    {
        ResetInvited();
        Constraints = Parse();
    }

    /// <summary>Waits the simulated time; tests replace it.</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = static (time, token) => Task.Delay(time, token);

    /// <summary>The people invited right now.</summary>
    public IReadOnlyList<SmartSchedulerPerson> InvitedPeople => (People ?? []).Where(p => _invited.Contains(p.Id)).ToArray();

    #region Public actions

    /// <summary>Invites or uninvites <paramref name="person"/>; the shown slots are cleared because they may no longer fit.</summary>
    /// <param name="person">One of <see cref="People"/>.</param>
    /// <param name="invited">True to invite.</param>
    public void SetInvited(SmartSchedulerPerson person, bool invited)
    {
        if (invited ? !_invited.Add(person.Id) : !_invited.Remove(person.Id))
        {
            return;
        }
        SyncPeople();
        UpdateWeek();
        ClearSlots();
    }

    /// <summary>Whether <paramref name="person"/> is invited.</summary>
    /// <param name="person">One of <see cref="People"/>.</param>
    public bool IsInvited(SmartSchedulerPerson person) => _invited.Contains(person.Id);

    /// <summary>Looks for slots with the current people, length and limits (what the find button does).</summary>
    public async Task FindAsync()
    {
        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        var generation = ++_generation;
        var constraints = Parse();
        Constraints = constraints;
        var duration = constraints.DurationMinutes ?? DurationMinutes;
        if (constraints.DurationMinutes is { } named && named != DurationMinutes)
        {
            SetCurrentValue(DurationMinutesProperty, named);
        }
        var request = new SmartSchedulerRequest(InvitedPeople, duration, constraints, Days, DayStartHour, DayEndHour);
        SetState(SmartSchedulerState.Finding);
        Announce(Labels.Finding);
        IReadOnlyList<SmartSchedulerSlot> slots;
        try
        {
            if (SlotFinder is { } finder)
            {
                slots = await finder(request, cts.Token);
            }
            else
            {
                await Delay(SmartSchedulerSamples.SimulatedDelay, cts.Token);
                slots = SmartSchedulerPlanner.Find(request, Math.Max(1, SlotCount), Labels.Reasons);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            slots = [];
        }
        if (generation != _generation)
        {
            return;
        }
        Slots = slots;
        SelectedSlot = slots.Count > 0 ? slots[0] : null;
        SetState(slots.Count > 0 ? SmartSchedulerState.Proposed : SmartSchedulerState.NoTimes);
        RenderSlots();
        Announce(slots.Count > 0 ? Labels.Option(1, SlotText(slots[0])) : Labels.NoTimes);
        RaiseEvent(new RoutedEventArgs(SlotsFoundEvent));
    }

    /// <summary>Chooses the slot at <paramref name="index"/> in <see cref="Slots"/>.</summary>
    /// <param name="index">The slot's rank, from 0.</param>
    public void SelectSlot(int index)
    {
        if (index < 0 || index >= Slots.Count || State is SmartSchedulerState.Booking or SmartSchedulerState.Booked)
        {
            return;
        }
        SelectedSlot = Slots[index];
        _week?.Select(index, true);
        SyncOptions();
        UpdateTexts();
        Announce(Labels.Option(index + 1, SlotText(Slots[index])));
    }

    /// <summary>Books the chosen slot (what the book button does).</summary>
    public async Task BookAsync()
    {
        if (SelectedSlot is not { } slot || State != SmartSchedulerState.Proposed)
        {
            return;
        }
        var (start, end) = Times(slot);
        var args = new SmartSchedulerBookedEventArgs(SlotBookedEvent, slot, start, end, InvitedPeople);
        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        SetState(SmartSchedulerState.Booking);
        try
        {
            if (BookAction is { } book)
            {
                await book(args, cts.Token);
            }
            else
            {
                await Delay(SmartSchedulerSamples.SimulatedDelay, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            SetState(SmartSchedulerState.Proposed);
            return;
        }
        catch (Exception ex)
        {
            SetState(SmartSchedulerState.Proposed);
            SetStatus(ex.Message, isError: true);
            Announce(ex.Message);
            return;
        }
        SetState(SmartSchedulerState.Booked);
        var message = Labels.Booked(SlotText(slot), args.People.Count);
        SetStatus(message);
        Announce(message);
        RaiseEvent(args);
    }

    /// <summary>Clears the proposals (after booking, too) so a new search can start.</summary>
    public void StartOver()
    {
        _cts?.Cancel();
        _generation++;
        ClearSlots();
    }

    #endregion

    private void ResetInvited()
    {
        _invited.Clear();
        foreach (var person in People ?? [])
        {
            if (person.IsInvited)
            {
                _invited.Add(person.Id);
            }
        }
    }

    private SmartSchedulerConstraints Parse()
        => SmartSchedulerPlanner.Parse(ConstraintsText, WeekStart, Days, Labels.Reasons);

    private (DateTime Start, DateTime End) Times(SmartSchedulerSlot slot)
    {
        var day = WeekStart.AddDays(slot.Day).ToDateTime(TimeOnly.MinValue);
        return (day.AddHours(slot.Start), day.AddHours(slot.End));
    }

    private string SlotText(SmartSchedulerSlot slot)
    {
        var (start, end) = Times(slot);
        return Labels.Slot(start, end);
    }

    private void ClearSlots()
    {
        Slots = [];
        SelectedSlot = null;
        SetState(SmartSchedulerState.Idle);
        RenderSlots();
    }

    private void SetState(SmartSchedulerState state)
    {
        State = state;
        PseudoClasses.Set(ProposedPseudoClass, state is SmartSchedulerState.Proposed or SmartSchedulerState.Booking);
        PseudoClasses.Set(BookedPseudoClass, state == SmartSchedulerState.Booked);
        PseudoClasses.Set(NoTimesPseudoClass, state == SmartSchedulerState.NoTimes);
        UpdateTexts();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == PeopleProperty)
        {
            ResetInvited();
            BuildPeople();
            UpdateWeek();
            ClearSlots();
        }
        else if (p == WeekStartProperty || p == DaysProperty || p == DayStartHourProperty || p == DayEndHourProperty)
        {
            Constraints = Parse();
            BuildChips();
            UpdateWeek();
            ClearSlots();
        }
        else if (p == ConstraintsTextProperty)
        {
            Constraints = Parse();
            BuildChips();
        }
        else if (p == DurationsProperty)
        {
            BuildDurations();
        }
        else if (p == DurationMinutesProperty)
        {
            SyncDuration();
        }
        else if (p == LabelsProperty)
        {
            Constraints = Parse();
            BuildDurations();
            BuildChips();
            UpdateWeek();
            RenderSlots();
            UpdateTexts();
        }
        else if (p == BoundsProperty || p == CompactWidthProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, Bounds.Width > 0 && Bounds.Width <= CompactWidth);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _cts?.Cancel();
        if (State is SmartSchedulerState.Finding)
        {
            _generation++;
            SetState(Slots.Count > 0 ? SmartSchedulerState.Proposed : SmartSchedulerState.Idle);
        }
        else if (State is SmartSchedulerState.Booking)
        {
            SetState(SmartSchedulerState.Proposed);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SmartSchedulerPeer(this);

    private sealed class SmartSchedulerPeer(SmartScheduler owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore()
            => base.GetNameCore() is { Length: > 0 } name ? name : ((SmartScheduler)Owner).Labels.Title;
    }

    internal static string DayLabel(DateOnly date) => date.ToString("ddd d", CultureInfo.CurrentCulture);
}
