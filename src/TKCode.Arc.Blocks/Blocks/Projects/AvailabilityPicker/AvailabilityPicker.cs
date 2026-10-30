using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// Find a meeting time that suits several people: a strip of days, the proposed times of the selected day with one mark per
/// attendee (green when free, grey when busy), and a footer that sums up the choice with a button to book it. Choosing
/// another day glides the day highlight there, slides the times in from that side and selects the first time that suits
/// everyone; a time with busy attendees can still be booked, and the footer says who has to move a meeting. Booking turns
/// the button into a quiet "Booked"; any new choice undoes it. The days, times and attendees are data you provide.
/// </summary>
[TemplatePart(TitlePart, typeof(Avalonia.Controls.TextBlock))]
[TemplatePart(SubtitlePart, typeof(Avalonia.Controls.TextBlock))]
[TemplatePart(AttendeesPart, typeof(ProjectsAvatarStack))]
[TemplatePart(DayStripPart, typeof(AvailabilityDayStrip))]
[TemplatePart(DayTitlePart, typeof(AvailabilityTextSwap))]
[TemplatePart(FreeLegendPart, typeof(Avalonia.Controls.TextBlock))]
[TemplatePart(BusyLegendPart, typeof(Avalonia.Controls.TextBlock))]
[TemplatePart(SlotsPart, typeof(AvailabilitySlotSwap))]
[TemplatePart(SelectionLabelPart, typeof(AvailabilityTextSwap))]
[TemplatePart(SelectionValuePart, typeof(AvailabilityTextSwap))]
[TemplatePart(ConfirmPart, typeof(Button))]
[TemplatePart(AnnouncerPart, typeof(ProjectsAnnouncer))]
[PseudoClasses(FramedPseudoClass, CompactPseudoClass, NarrowPseudoClass, ConfirmedPseudoClass)]
public partial class AvailabilityPicker : TemplatedControl
{
    public const string TitlePart = "PART_Title";
    public const string SubtitlePart = "PART_Subtitle";
    public const string AttendeesPart = "PART_Attendees";
    public const string DayStripPart = "PART_DayStrip";
    public const string DayTitlePart = "PART_DayTitle";
    public const string FreeLegendPart = "PART_FreeLegend";
    public const string BusyLegendPart = "PART_BusyLegend";
    public const string SlotsPart = "PART_Slots";
    public const string SelectionLabelPart = "PART_SelectionLabel";
    public const string SelectionValuePart = "PART_SelectionValue";
    public const string ConfirmPart = "PART_Confirm";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while <see cref="IsFramed"/> is true.</summary>
    public const string FramedPseudoClass = ":framed";

    /// <summary>Set below 520 wide: one column of times, smaller day cards, the footer stacked over a full-width button.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set at 360 wide and below: the legend and the cards' count line are hidden.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while <see cref="IsConfirmed"/> is true.</summary>
    public const string ConfirmedPseudoClass = ":confirmed";

    #region Data

    /// <summary>The days, in the order of the strip. Use it in XAML; <see cref="DaysSource"/> takes precedence when set.</summary>
    public static readonly StyledProperty<AvaloniaList<AvailabilityDay>> DaysProperty =
        AvaloniaProperty.Register<AvailabilityPicker, AvaloniaList<AvailabilityDay>>(nameof(Days));

    /// <summary>A collection of <see cref="AvailabilityDay"/>s (observed when it raises collection changes); replaces <see cref="Days"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> DaysSourceProperty =
        AvaloniaProperty.Register<AvailabilityPicker, IEnumerable?>(nameof(DaysSource));

    /// <summary>The attendees, in the order of the avatars and of each time's marks.</summary>
    public static readonly StyledProperty<AvaloniaList<ProjectPerson>> AttendeesProperty =
        AvaloniaProperty.Register<AvailabilityPicker, AvaloniaList<ProjectPerson>>(nameof(Attendees));

    /// <summary>A collection of <see cref="ProjectPerson"/>s (observed when it raises collection changes); replaces <see cref="Attendees"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> AttendeesSourceProperty =
        AvaloniaProperty.Register<AvailabilityPicker, IEnumerable?>(nameof(AttendeesSource));

    #endregion

    #region State

    /// <summary>The selected day; null (or a day that is not in <see cref="Days"/>) selects the first day.</summary>
    public static readonly StyledProperty<AvailabilityDay?> SelectedDayProperty =
        AvaloniaProperty.Register<AvailabilityPicker, AvailabilityDay?>(nameof(SelectedDay), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The selected time. A time of another day selects that day as well. When the day changes and this time is not on it,
    /// the picker selects one by <see cref="AutoSelectSlot"/>.
    /// </summary>
    public static readonly StyledProperty<AvailabilitySlot?> SelectedSlotProperty =
        AvaloniaProperty.Register<AvailabilityPicker, AvailabilitySlot?>(nameof(SelectedSlot), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Which time is selected by itself after the day changes, and at first when <see cref="SelectedSlot"/> is not set.</summary>
    public static readonly StyledProperty<AvailabilitySlotAutoSelect> AutoSelectSlotProperty =
        AvaloniaProperty.Register<AvailabilityPicker, AvailabilitySlotAutoSelect>(nameof(AutoSelectSlot));

    /// <summary>True once the selection is booked; choosing another day or time sets it back to false.</summary>
    public static readonly StyledProperty<bool> IsConfirmedProperty =
        AvaloniaProperty.Register<AvailabilityPicker, bool>(nameof(IsConfirmed), defaultBindingMode: BindingMode.TwoWay);

    #endregion

    #region Header and look

    /// <summary>The meeting's name, the picker's heading (and its screen reader name).</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string?>(nameof(Title));

    /// <summary>The line under the title; null builds it from <see cref="Duration"/> and <see cref="TimeZoneName"/>.</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string?>(nameof(Subtitle));

    /// <summary>The meeting's length: shown under the title, and the end of every time without its own <see cref="AvailabilitySlot.End"/>.</summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<AvailabilityPicker, TimeSpan>(nameof(Duration), TimeSpan.FromMinutes(30));

    /// <summary>The time zone the times are in, as shown after the duration ("Lisbon time"); null shows the duration only.</summary>
    public static readonly StyledProperty<string?> TimeZoneNameProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string?>(nameof(TimeZoneName));

    /// <summary>Draws the picker as a bordered, rounded surface with divided sections (true); false lays it flat on the page.</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<AvailabilityPicker, bool>(nameof(IsFramed), true);

    /// <summary>Shows the Free / Busy legend beside the day heading (always hidden at 360 wide and below).</summary>
    public static readonly StyledProperty<bool> IsLegendVisibleProperty =
        AvaloniaProperty.Register<AvailabilityPicker, bool>(nameof(IsLegendVisible), true);

    /// <summary>Runs when a selection is booked (parameter: the <see cref="AvailabilitySelectionEventArgs"/> of <see cref="Confirmed"/>).</summary>
    public static readonly StyledProperty<ICommand?> ConfirmCommandProperty =
        AvaloniaProperty.Register<AvailabilityPicker, ICommand?>(nameof(ConfirmCommand));

    #endregion

    #region Texts

    /// <summary>The button's label before booking.</summary>
    public static readonly StyledProperty<string> ConfirmTextProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(ConfirmText), "Book this time");

    /// <summary>The button's label once booked (shown after a check).</summary>
    public static readonly StyledProperty<string> ConfirmedTextProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(ConfirmedText), "Booked");

    /// <summary>The line of a time that suits every attendee, and the footer's line for it.</summary>
    public static readonly StyledProperty<string> EveryoneFreeTextProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(EveryoneFreeText), "Everyone's free");

    /// <summary>The line of a time with one busy attendee; {0} is their short name.</summary>
    public static readonly StyledProperty<string> BusyFormatProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(BusyFormat), "{0} is busy");

    /// <summary>The line of a time with several busy attendees; {0} is their short names ("Noor and Ingrid").</summary>
    public static readonly StyledProperty<string> BusyManyFormatProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(BusyManyFormat), "{0} are busy");

    /// <summary>The footer's line for a time with busy attendees; {0} is their short names.</summary>
    public static readonly StyledProperty<string> ConflictFormatProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(ConflictFormat), "{0} will need to move a meeting");

    /// <summary>The footer's line once booked.</summary>
    public static readonly StyledProperty<string> ConfirmedLabelTextProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(ConfirmedLabelText), "Invites on the way");

    /// <summary>The footer's line while no time is selected.</summary>
    public static readonly StyledProperty<string> NoSlotTextProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(NoSlotText), "Pick a time");

    /// <summary>Shown in place of the times on a day without any.</summary>
    public static readonly StyledProperty<string> NoSlotsTextProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(NoSlotsText), "No times on this day");

    /// <summary>A day card's count line; {0} is <see cref="AvailabilityDay.OpenCount"/>.</summary>
    public static readonly StyledProperty<string> OpenCountFormatProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(OpenCountFormat), "{0} open");

    /// <summary>The legend's word for a free attendee.</summary>
    public static readonly StyledProperty<string> FreeLegendTextProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(FreeLegendText), "Free");

    /// <summary>The legend's word for a busy attendee.</summary>
    public static readonly StyledProperty<string> BusyLegendTextProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(BusyLegendText), "Busy");

    /// <summary>The word joining the last two names in lists ("Noor and Ingrid").</summary>
    public static readonly StyledProperty<string> AndWordProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(AndWord), "and");

    /// <summary>The duration under the title; {0} is the number of minutes.</summary>
    public static readonly StyledProperty<string> DurationFormatProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(DurationFormat), "{0} min");

    /// <summary>The line under the title when a time zone is set; {0} is the duration, {1} <see cref="TimeZoneName"/>.</summary>
    public static readonly StyledProperty<string> SubtitleFormatProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(SubtitleFormat), "{0} · {1}");

    /// <summary>A day card's weekday; {0} is the date (a DateTime) in the current culture.</summary>
    public static readonly StyledProperty<string> DayNameFormatProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(DayNameFormat), "{0:ddd}");

    /// <summary>A day card's number; {0} is the date.</summary>
    public static readonly StyledProperty<string> DayNumberFormatProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(DayNumberFormat), "{0:%d}");

    /// <summary>The long date of the heading and the footer; {0} is the date.</summary>
    public static readonly StyledProperty<string> DayLabelFormatProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(DayLabelFormat), "{0:dddd, d MMMM}");

    /// <summary>A time of day; {0} is the date and time (a DateTime).</summary>
    public static readonly StyledProperty<string> TimeFormatProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(TimeFormat), "{0:HH:mm}");

    /// <summary>A time range as shown; {0} is the start, {1} the end (both formatted).</summary>
    public static readonly StyledProperty<string> RangeFormatProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(RangeFormat), "{0} – {1}");

    /// <summary>The footer's summary; {0} is the long date, {1} the range.</summary>
    public static readonly StyledProperty<string> SelectionFormatProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(SelectionFormat), "{0}, {1}");

    /// <summary>The avatars' screen reader name; {0} is the attendees' names ("A, B and C").</summary>
    public static readonly StyledProperty<string> AttendeesAutomationFormatProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(AttendeesAutomationFormat), "Invited: {0}");

    /// <summary>The day strip's screen reader name; {0} is the first day's date.</summary>
    public static readonly StyledProperty<string> WeekAutomationFormatProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(WeekAutomationFormat), "Days from {0:d MMMM}");

    /// <summary>A day card's screen reader name; {0} is the date, {1} <see cref="AvailabilityDay.OpenCount"/>.</summary>
    public static readonly StyledProperty<string> DayAutomationFormatProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(DayAutomationFormat), "{0:dddd, d MMMM}, open for everyone: {1}");

    /// <summary>The time list's screen reader name; {0} is the date.</summary>
    public static readonly StyledProperty<string> TimesAutomationFormatProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(TimesAutomationFormat), "Times for {0:dddd, d MMMM}");

    /// <summary>A time's screen reader name; {0} is the start, {1} the end, {2} its line ("Everyone's free").</summary>
    public static readonly StyledProperty<string> SlotAutomationFormatProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(SlotAutomationFormat), "{0} to {1}, {2}");

    /// <summary>Announced politely when a time is booked; {0} is the long date, {1} the start, {2} the end.</summary>
    public static readonly StyledProperty<string> ConfirmedAnnouncementFormatProperty =
        AvaloniaProperty.Register<AvailabilityPicker, string>(nameof(ConfirmedAnnouncementFormat), "Booked {0}, {1} to {2}.");

    #endregion

    #region Token-driven internals

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<AvailabilityPicker, double>(nameof(CompactWidth), 520);

    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<AvailabilityPicker, double>(nameof(NarrowWidth), 360);

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    #endregion

    #region Events

    /// <summary>Raised after the selected day changed (the picker may have selected a time with it).</summary>
    public static readonly RoutedEvent<AvailabilityDayChangedEventArgs> SelectedDayChangedEvent =
        RoutedEvent.Register<AvailabilityPicker, AvailabilityDayChangedEventArgs>(nameof(SelectedDayChanged), RoutingStrategies.Bubble);

    /// <summary>Raised after the selected time changed.</summary>
    public static readonly RoutedEvent<AvailabilitySlotChangedEventArgs> SelectedSlotChangedEvent =
        RoutedEvent.Register<AvailabilityPicker, AvailabilitySlotChangedEventArgs>(nameof(SelectedSlotChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the book button is pressed or <see cref="Confirm"/> is called; set Cancel to keep it unbooked.</summary>
    public static readonly RoutedEvent<AvailabilityConfirmRequestedEventArgs> ConfirmRequestedEvent =
        RoutedEvent.Register<AvailabilityPicker, AvailabilityConfirmRequestedEventArgs>(nameof(ConfirmRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="IsConfirmed"/> turns true, with the booked selection.</summary>
    public static readonly RoutedEvent<AvailabilitySelectionEventArgs> ConfirmedEvent =
        RoutedEvent.Register<AvailabilityPicker, AvailabilitySelectionEventArgs>(nameof(Confirmed), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="IsConfirmed"/> turns false (a new choice or <see cref="ResetConfirmation"/>), with the selection that was booked.</summary>
    public static readonly RoutedEvent<AvailabilitySelectionEventArgs> ConfirmationResetEvent =
        RoutedEvent.Register<AvailabilityPicker, AvailabilitySelectionEventArgs>(nameof(ConfirmationReset), RoutingStrategies.Bubble);

    #endregion

    static AvailabilityPicker()
    {
        BlockFocusModality.Watch();
        FocusableProperty.OverrideDefaultValue<AvailabilityPicker>(false);
    }

    public AvailabilityPicker()
    {
        SetCurrentValue(DaysProperty, new AvaloniaList<AvailabilityDay>());
        SetCurrentValue(AttendeesProperty, new AvaloniaList<ProjectPerson>());
        PseudoClasses.Set(FramedPseudoClass, true);
    }

    #region CLR properties

    /// <inheritdoc cref="DaysProperty"/>
    [Content]
    public AvaloniaList<AvailabilityDay> Days { get => GetValue(DaysProperty); set => SetValue(DaysProperty, value); }

    /// <inheritdoc cref="DaysSourceProperty"/>
    public IEnumerable? DaysSource { get => GetValue(DaysSourceProperty); set => SetValue(DaysSourceProperty, value); }

    /// <inheritdoc cref="AttendeesProperty"/>
    public AvaloniaList<ProjectPerson> Attendees { get => GetValue(AttendeesProperty); set => SetValue(AttendeesProperty, value); }

    /// <inheritdoc cref="AttendeesSourceProperty"/>
    public IEnumerable? AttendeesSource { get => GetValue(AttendeesSourceProperty); set => SetValue(AttendeesSourceProperty, value); }

    /// <inheritdoc cref="SelectedDayProperty"/>
    public AvailabilityDay? SelectedDay { get => GetValue(SelectedDayProperty); set => SetValue(SelectedDayProperty, value); }

    /// <inheritdoc cref="SelectedSlotProperty"/>
    public AvailabilitySlot? SelectedSlot { get => GetValue(SelectedSlotProperty); set => SetValue(SelectedSlotProperty, value); }

    /// <inheritdoc cref="AutoSelectSlotProperty"/>
    public AvailabilitySlotAutoSelect AutoSelectSlot { get => GetValue(AutoSelectSlotProperty); set => SetValue(AutoSelectSlotProperty, value); }

    /// <inheritdoc cref="IsConfirmedProperty"/>
    public bool IsConfirmed { get => GetValue(IsConfirmedProperty); set => SetValue(IsConfirmedProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    /// <inheritdoc cref="TimeZoneNameProperty"/>
    public string? TimeZoneName { get => GetValue(TimeZoneNameProperty); set => SetValue(TimeZoneNameProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="IsLegendVisibleProperty"/>
    public bool IsLegendVisible { get => GetValue(IsLegendVisibleProperty); set => SetValue(IsLegendVisibleProperty, value); }

    /// <inheritdoc cref="ConfirmCommandProperty"/>
    public ICommand? ConfirmCommand { get => GetValue(ConfirmCommandProperty); set => SetValue(ConfirmCommandProperty, value); }

    /// <inheritdoc cref="ConfirmTextProperty"/>
    public string ConfirmText { get => GetValue(ConfirmTextProperty); set => SetValue(ConfirmTextProperty, value); }

    /// <inheritdoc cref="ConfirmedTextProperty"/>
    public string ConfirmedText { get => GetValue(ConfirmedTextProperty); set => SetValue(ConfirmedTextProperty, value); }

    /// <inheritdoc cref="EveryoneFreeTextProperty"/>
    public string EveryoneFreeText { get => GetValue(EveryoneFreeTextProperty); set => SetValue(EveryoneFreeTextProperty, value); }

    /// <inheritdoc cref="BusyFormatProperty"/>
    public string BusyFormat { get => GetValue(BusyFormatProperty); set => SetValue(BusyFormatProperty, value); }

    /// <inheritdoc cref="BusyManyFormatProperty"/>
    public string BusyManyFormat { get => GetValue(BusyManyFormatProperty); set => SetValue(BusyManyFormatProperty, value); }

    /// <inheritdoc cref="ConflictFormatProperty"/>
    public string ConflictFormat { get => GetValue(ConflictFormatProperty); set => SetValue(ConflictFormatProperty, value); }

    /// <inheritdoc cref="ConfirmedLabelTextProperty"/>
    public string ConfirmedLabelText { get => GetValue(ConfirmedLabelTextProperty); set => SetValue(ConfirmedLabelTextProperty, value); }

    /// <inheritdoc cref="NoSlotTextProperty"/>
    public string NoSlotText { get => GetValue(NoSlotTextProperty); set => SetValue(NoSlotTextProperty, value); }

    /// <inheritdoc cref="NoSlotsTextProperty"/>
    public string NoSlotsText { get => GetValue(NoSlotsTextProperty); set => SetValue(NoSlotsTextProperty, value); }

    /// <inheritdoc cref="OpenCountFormatProperty"/>
    public string OpenCountFormat { get => GetValue(OpenCountFormatProperty); set => SetValue(OpenCountFormatProperty, value); }

    /// <inheritdoc cref="FreeLegendTextProperty"/>
    public string FreeLegendText { get => GetValue(FreeLegendTextProperty); set => SetValue(FreeLegendTextProperty, value); }

    /// <inheritdoc cref="BusyLegendTextProperty"/>
    public string BusyLegendText { get => GetValue(BusyLegendTextProperty); set => SetValue(BusyLegendTextProperty, value); }

    /// <inheritdoc cref="AndWordProperty"/>
    public string AndWord { get => GetValue(AndWordProperty); set => SetValue(AndWordProperty, value); }

    /// <inheritdoc cref="DurationFormatProperty"/>
    public string DurationFormat { get => GetValue(DurationFormatProperty); set => SetValue(DurationFormatProperty, value); }

    /// <inheritdoc cref="SubtitleFormatProperty"/>
    public string SubtitleFormat { get => GetValue(SubtitleFormatProperty); set => SetValue(SubtitleFormatProperty, value); }

    /// <inheritdoc cref="DayNameFormatProperty"/>
    public string DayNameFormat { get => GetValue(DayNameFormatProperty); set => SetValue(DayNameFormatProperty, value); }

    /// <inheritdoc cref="DayNumberFormatProperty"/>
    public string DayNumberFormat { get => GetValue(DayNumberFormatProperty); set => SetValue(DayNumberFormatProperty, value); }

    /// <inheritdoc cref="DayLabelFormatProperty"/>
    public string DayLabelFormat { get => GetValue(DayLabelFormatProperty); set => SetValue(DayLabelFormatProperty, value); }

    /// <inheritdoc cref="TimeFormatProperty"/>
    public string TimeFormat { get => GetValue(TimeFormatProperty); set => SetValue(TimeFormatProperty, value); }

    /// <inheritdoc cref="RangeFormatProperty"/>
    public string RangeFormat { get => GetValue(RangeFormatProperty); set => SetValue(RangeFormatProperty, value); }

    /// <inheritdoc cref="SelectionFormatProperty"/>
    public string SelectionFormat { get => GetValue(SelectionFormatProperty); set => SetValue(SelectionFormatProperty, value); }

    /// <inheritdoc cref="AttendeesAutomationFormatProperty"/>
    public string AttendeesAutomationFormat { get => GetValue(AttendeesAutomationFormatProperty); set => SetValue(AttendeesAutomationFormatProperty, value); }

    /// <inheritdoc cref="WeekAutomationFormatProperty"/>
    public string WeekAutomationFormat { get => GetValue(WeekAutomationFormatProperty); set => SetValue(WeekAutomationFormatProperty, value); }

    /// <inheritdoc cref="DayAutomationFormatProperty"/>
    public string DayAutomationFormat { get => GetValue(DayAutomationFormatProperty); set => SetValue(DayAutomationFormatProperty, value); }

    /// <inheritdoc cref="TimesAutomationFormatProperty"/>
    public string TimesAutomationFormat { get => GetValue(TimesAutomationFormatProperty); set => SetValue(TimesAutomationFormatProperty, value); }

    /// <inheritdoc cref="SlotAutomationFormatProperty"/>
    public string SlotAutomationFormat { get => GetValue(SlotAutomationFormatProperty); set => SetValue(SlotAutomationFormatProperty, value); }

    /// <inheritdoc cref="ConfirmedAnnouncementFormatProperty"/>
    public string ConfirmedAnnouncementFormat { get => GetValue(ConfirmedAnnouncementFormatProperty); set => SetValue(ConfirmedAnnouncementFormatProperty, value); }

    /// <inheritdoc cref="SelectedDayChangedEvent"/>
    public event EventHandler<AvailabilityDayChangedEventArgs>? SelectedDayChanged
    {
        add => AddHandler(SelectedDayChangedEvent, value);
        remove => RemoveHandler(SelectedDayChangedEvent, value);
    }

    /// <inheritdoc cref="SelectedSlotChangedEvent"/>
    public event EventHandler<AvailabilitySlotChangedEventArgs>? SelectedSlotChanged
    {
        add => AddHandler(SelectedSlotChangedEvent, value);
        remove => RemoveHandler(SelectedSlotChangedEvent, value);
    }

    /// <inheritdoc cref="ConfirmRequestedEvent"/>
    public event EventHandler<AvailabilityConfirmRequestedEventArgs>? ConfirmRequested
    {
        add => AddHandler(ConfirmRequestedEvent, value);
        remove => RemoveHandler(ConfirmRequestedEvent, value);
    }

    /// <inheritdoc cref="ConfirmedEvent"/>
    public event EventHandler<AvailabilitySelectionEventArgs>? Confirmed
    {
        add => AddHandler(ConfirmedEvent, value);
        remove => RemoveHandler(ConfirmedEvent, value);
    }

    /// <inheritdoc cref="ConfirmationResetEvent"/>
    public event EventHandler<AvailabilitySelectionEventArgs>? ConfirmationReset
    {
        add => AddHandler(ConfirmationResetEvent, value);
        remove => RemoveHandler(ConfirmationResetEvent, value);
    }

    #endregion

    #region Public methods

    /// <summary>
    /// Selects <paramref name="day"/> as if it had been chosen; <paramref name="animate"/> false moves the highlight and swaps
    /// the times at once. Returns false when the day is not in <see cref="Days"/>.
    /// </summary>
    public bool SelectDay(AvailabilityDay day, bool animate = true)
    {
        FlushData();
        if (!_days.Contains(day))
        {
            return false;
        }
        var suppress = _suppressMotion;
        _suppressMotion = !animate;
        try
        {
            SetCurrentValue(SelectedDayProperty, day);
        }
        finally
        {
            _suppressMotion = suppress;
        }
        return true;
    }

    /// <summary>Selects <paramref name="slot"/> (and its day). Returns false when it is on no day or cannot be booked.</summary>
    public bool SelectSlot(AvailabilitySlot slot)
    {
        FlushData();
        if (DayOf(slot) is null || !slot.IsEnabled)
        {
            return false;
        }
        SetCurrentValue(SelectedSlotProperty, slot);
        return true;
    }

    /// <summary>
    /// Books the selected time, as the button does: raises <see cref="ConfirmRequested"/> (which may cancel), then sets
    /// <see cref="IsConfirmed"/>, announces it and runs <see cref="ConfirmCommand"/>. Returns false when nothing was booked.
    /// </summary>
    public bool Confirm() => RequestConfirm(user: false);

    /// <summary>Undoes a booking (raises <see cref="ConfirmationReset"/>); the selection stays.</summary>
    public void ResetConfirmation()
    {
        if (IsConfirmed)
        {
            SetCurrentValue(IsConfirmedProperty, false);
        }
    }

    #endregion
}
