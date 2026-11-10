using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A compact booking bar: where, when and who in one pill, with a search button at the end. Each segment opens its own
/// picker under the pill (a list of places, a month for the stay, steppers for the guests), and the pill reshapes around
/// the open segment: it widens, a raised chip glides under it, and the search button grows to show its word.
/// </summary>
/// <remarks>
/// Picking a place moves on to the dates, and picking the check-out moves on to the guests. Escape or a press outside
/// closes the picker. <see cref="SearchRequested"/> carries everything the pill holds; every value is also a two-way
/// property.
/// </remarks>
[TemplatePart(ShellPart, typeof(Border))]
[TemplatePart(RowPart, typeof(BookingPillRow))]
[TemplatePart(PlacePart, typeof(BookingPillSegmentButton))]
[TemplatePart(DatesPart, typeof(BookingPillSegmentButton))]
[TemplatePart(GuestsPart, typeof(BookingPillSegmentButton))]
[TemplatePart(SearchPart, typeof(BookingPillSearchButton))]
[TemplatePart(PopupPart, typeof(ArcPopup))]
[TemplatePart(SwapPart, typeof(ArcContentSwap))]
[TemplatePart(LivePart, typeof(ExplainerAnnouncer))]
[PseudoClasses(OpenPseudoClass)]
public class BookingPill : TemplatedControl
{
    internal const string ShellPart = "PART_Shell";
    internal const string RowPart = "PART_Row";
    internal const string PlacePart = "PART_Place";
    internal const string DatesPart = "PART_Dates";
    internal const string GuestsPart = "PART_Guests";
    internal const string SearchPart = "PART_Search";
    internal const string PopupPart = "PART_Popup";
    internal const string SwapPart = "PART_Swap";
    internal const string LivePart = "PART_Live";
    internal const string OpenPseudoClass = ":open";

    /// <summary>The segment whose picker is open; <see cref="BookingPillPart.None"/> when all are closed.</summary>
    public static readonly StyledProperty<BookingPillPart> OpenPartProperty =
        AvaloniaProperty.Register<BookingPill, BookingPillPart>(nameof(OpenPart), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The picked place, or null for anywhere.</summary>
    public static readonly StyledProperty<string?> PlaceProperty =
        AvaloniaProperty.Register<BookingPill, string?>(nameof(Place), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The first night of the stay, or null.</summary>
    public static readonly StyledProperty<DateOnly?> CheckInProperty =
        AvaloniaProperty.Register<BookingPill, DateOnly?>(nameof(CheckIn), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The day of leaving, after <see cref="CheckIn"/>, or null.</summary>
    public static readonly StyledProperty<DateOnly?> CheckOutProperty =
        AvaloniaProperty.Register<BookingPill, DateOnly?>(nameof(CheckOut), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Adults (13 and over by the default wording).</summary>
    public static readonly StyledProperty<int> AdultsProperty =
        AvaloniaProperty.Register<BookingPill, int>(nameof(Adults), defaultBindingMode: BindingMode.TwoWay,
            coerce: static (_, v) => Math.Max(0, v));

    /// <summary>Children (2 to 12 by the default wording).</summary>
    public static readonly StyledProperty<int> ChildrenProperty =
        AvaloniaProperty.Register<BookingPill, int>(nameof(Children), defaultBindingMode: BindingMode.TwoWay,
            coerce: static (_, v) => Math.Max(0, v));

    /// <summary>Infants (under 2 by the default wording). They do not count toward <see cref="MaxGuests"/>.</summary>
    public static readonly StyledProperty<int> InfantsProperty =
        AvaloniaProperty.Register<BookingPill, int>(nameof(Infants), defaultBindingMode: BindingMode.TwoWay,
            coerce: static (_, v) => Math.Max(0, v));

    /// <summary>The most adults and children together. Default 16.</summary>
    public static readonly StyledProperty<int> MaxGuestsProperty =
        AvaloniaProperty.Register<BookingPill, int>(nameof(MaxGuests), 16);

    /// <summary>The most infants. Default 5.</summary>
    public static readonly StyledProperty<int> MaxInfantsProperty =
        AvaloniaProperty.Register<BookingPill, int>(nameof(MaxInfants), 5);

    /// <summary>The earliest day that can be picked; days before it are faded. Null allows any day. Default today.</summary>
    public static readonly StyledProperty<DateOnly?> MinDateProperty =
        AvaloniaProperty.Register<BookingPill, DateOnly?>(nameof(MinDate));

    /// <summary>Culture of the dates and month names. Default en-US.</summary>
    public static readonly StyledProperty<string> CultureNameProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(CultureName), "en-US");

    /// <summary>Label of the place segment.</summary>
    public static readonly StyledProperty<string> PlaceLabelProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(PlaceLabel), "Where");

    /// <summary>Label of the dates segment.</summary>
    public static readonly StyledProperty<string> DatesLabelProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(DatesLabel), "When");

    /// <summary>Label of the guests segment.</summary>
    public static readonly StyledProperty<string> GuestsLabelProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(GuestsLabel), "Who");

    /// <summary>Value of the place segment while no place is picked.</summary>
    public static readonly StyledProperty<string> AnyPlaceTextProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(AnyPlaceText), "Anywhere");

    /// <summary>Value of the dates segment while no dates are picked.</summary>
    public static readonly StyledProperty<string> AnyDatesTextProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(AnyDatesText), "Add dates");

    /// <summary>Value of the guests segment while there are no guests.</summary>
    public static readonly StyledProperty<string> AnyGuestsTextProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(AnyGuestsText), "Add guests");

    /// <summary>Placeholder of the search field in the place picker.</summary>
    public static readonly StyledProperty<string> PlaceSearchPlaceholderProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(PlaceSearchPlaceholder), "Search places");

    /// <summary>Shown when no place matches the search.</summary>
    public static readonly StyledProperty<string> NoPlacesTextProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(NoPlacesText), "No places match");

    /// <summary>Word on the search button while a picker is open, and its accessible name.</summary>
    public static readonly StyledProperty<string> SearchTextProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(SearchText), "Search");

    /// <summary>Format of the guest count for one guest; {0} is the number.</summary>
    public static readonly StyledProperty<string> GuestFormatProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(GuestFormat), "{0} guest");

    /// <summary>Format of the guest count for more than one guest; {0} is the number.</summary>
    public static readonly StyledProperty<string> GuestsFormatProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(GuestsFormat), "{0} guests");

    /// <summary>Format of the infant count for one infant; {0} is the number.</summary>
    public static readonly StyledProperty<string> InfantFormatProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(InfantFormat), "{0} infant");

    /// <summary>Format of the infant count for more than one infant; {0} is the number.</summary>
    public static readonly StyledProperty<string> InfantsFormatProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(InfantsFormat), "{0} infants");

    /// <summary>Format of the stay length for one night; {0} is the number.</summary>
    public static readonly StyledProperty<string> NightFormatProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(NightFormat), "{0} night");

    /// <summary>Format of the stay length for more than one night; {0} is the number.</summary>
    public static readonly StyledProperty<string> NightsFormatProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(NightsFormat), "{0} nights");

    /// <summary>Line under the month while the check-in is being chosen.</summary>
    public static readonly StyledProperty<string> PickCheckInTextProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(PickCheckInText), "Pick the first night");

    /// <summary>Line under the month while the check-out is being chosen.</summary>
    public static readonly StyledProperty<string> PickCheckOutTextProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(PickCheckOutText), "Pick the day you leave");

    /// <summary>Read after the check-in day.</summary>
    public static readonly StyledProperty<string> CheckInTextProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(CheckInText), "check-in");

    /// <summary>Read after the check-out day.</summary>
    public static readonly StyledProperty<string> CheckOutTextProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(CheckOutText), "check-out");

    /// <summary>Text of the button that clears the dates.</summary>
    public static readonly StyledProperty<string> ClearDatesTextProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(ClearDatesText), "Clear");

    /// <summary>Accessible name of the previous month button.</summary>
    public static readonly StyledProperty<string> PreviousMonthTextProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(PreviousMonthText), "Previous month");

    /// <summary>Accessible name of the next month button.</summary>
    public static readonly StyledProperty<string> NextMonthTextProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(NextMonthText), "Next month");

    /// <summary>Title of the adults row.</summary>
    public static readonly StyledProperty<string> AdultsTextProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(AdultsText), "Adults");

    /// <summary>Title of the children row.</summary>
    public static readonly StyledProperty<string> ChildrenTextProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(ChildrenText), "Children");

    /// <summary>Title of the infants row.</summary>
    public static readonly StyledProperty<string> InfantsTextProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(InfantsText), "Infants");

    /// <summary>Detail under the adults row.</summary>
    public static readonly StyledProperty<string> AdultsDetailProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(AdultsDetail), "Ages 13 or above");

    /// <summary>Detail under the children row.</summary>
    public static readonly StyledProperty<string> ChildrenDetailProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(ChildrenDetail), "Ages 2 to 12");

    /// <summary>Detail under the infants row.</summary>
    public static readonly StyledProperty<string> InfantsDetailProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(InfantsDetail), "Under 2");

    /// <summary>First word of the − buttons' accessible names ("Decrease Adults").</summary>
    public static readonly StyledProperty<string> DecreaseTextProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(DecreaseText), "Decrease");

    /// <summary>First word of the + buttons' accessible names ("Increase Adults").</summary>
    public static readonly StyledProperty<string> IncreaseTextProperty =
        AvaloniaProperty.Register<BookingPill, string>(nameof(IncreaseText), "Increase");

    /// <summary>Raised by the search button with everything the pill holds.</summary>
    public static readonly RoutedEvent<BookingPillSearchEventArgs> SearchRequestedEvent =
        RoutedEvent.Register<BookingPill, BookingPillSearchEventArgs>(nameof(SearchRequested), RoutingStrategies.Bubble);

    internal static readonly StyledProperty<double> DayCellSizeProperty =
        AvaloniaProperty.Register<BookingPill, double>(nameof(DayCellSize), 40);

    internal static readonly StyledProperty<double> OpenGrowProperty =
        AvaloniaProperty.Register<BookingPill, double>(nameof(OpenGrow), 0.3);

    internal static readonly StyledProperty<double> DayFontSizeProperty =
        AvaloniaProperty.Register<BookingPill, double>(nameof(DayFontSize), 14);

    internal static readonly StyledProperty<double> WeekdayFontSizeProperty =
        AvaloniaProperty.Register<BookingPill, double>(nameof(WeekdayFontSize), 12);

    internal static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<BookingPill, IBrush?>(nameof(MutedBrush));

    internal static readonly StyledProperty<IBrush?> RangeBrushProperty =
        AvaloniaProperty.Register<BookingPill, IBrush?>(nameof(RangeBrush));

    internal static readonly StyledProperty<IBrush?> PreviewBrushProperty =
        AvaloniaProperty.Register<BookingPill, IBrush?>(nameof(PreviewBrush));

    internal static readonly StyledProperty<IBrush?> EndFillBrushProperty =
        AvaloniaProperty.Register<BookingPill, IBrush?>(nameof(EndFillBrush));

    internal static readonly StyledProperty<IBrush?> EndTextBrushProperty =
        AvaloniaProperty.Register<BookingPill, IBrush?>(nameof(EndTextBrush));

    internal static readonly StyledProperty<IBrush?> HoverBrushProperty =
        AvaloniaProperty.Register<BookingPill, IBrush?>(nameof(HoverBrush));

    internal static readonly StyledProperty<IBrush?> FocusBrushProperty =
        AvaloniaProperty.Register<BookingPill, IBrush?>(nameof(FocusBrush));

    private readonly BookingPillPickers _pickers;
    private readonly BookingPillSegmentButton?[] _segments = new BookingPillSegmentButton?[3];
    private Border? _shell;
    private BookingPillRow? _row;
    private BookingPillSearchButton? _search;
    private ArcPopup? _popup;
    private ArcContentSwap? _swap;
    private ExplainerAnnouncer? _live;
    private TopLevel? _watched;
    private CultureInfo _culture = CultureInfo.GetCultureInfo("en-US");
    private bool _keyboardOpen;
    private bool _pointerInput;

    public BookingPill()
    {
        _pickers = new BookingPillPickers(this);
        Places.CollectionChanged += (_, _) => _pickers.FilterPlaces();
        SetCurrentValue(MinDateProperty, DateOnly.FromDateTime(DateTime.Today));
        AddHandler(PointerPressedEvent, (_, _) => _pointerInput = true, RoutingStrategies.Tunnel, true);
        AddHandler(KeyDownEvent, (_, _) => _pointerInput = false, RoutingStrategies.Tunnel, true);
    }

    /// <summary>The places offered in the place picker, in order.</summary>
    [Content]
    public AvaloniaList<BookingPlace> Places { get; } = [];

    /// <inheritdoc cref="OpenPartProperty"/>
    public BookingPillPart OpenPart
    {
        get => GetValue(OpenPartProperty);
        set => SetValue(OpenPartProperty, value);
    }

    /// <inheritdoc cref="PlaceProperty"/>
    public string? Place
    {
        get => GetValue(PlaceProperty);
        set => SetValue(PlaceProperty, value);
    }

    /// <inheritdoc cref="CheckInProperty"/>
    public DateOnly? CheckIn
    {
        get => GetValue(CheckInProperty);
        set => SetValue(CheckInProperty, value);
    }

    /// <inheritdoc cref="CheckOutProperty"/>
    public DateOnly? CheckOut
    {
        get => GetValue(CheckOutProperty);
        set => SetValue(CheckOutProperty, value);
    }

    /// <inheritdoc cref="AdultsProperty"/>
    public int Adults
    {
        get => GetValue(AdultsProperty);
        set => SetValue(AdultsProperty, value);
    }

    /// <inheritdoc cref="ChildrenProperty"/>
    public int Children
    {
        get => GetValue(ChildrenProperty);
        set => SetValue(ChildrenProperty, value);
    }

    /// <inheritdoc cref="InfantsProperty"/>
    public int Infants
    {
        get => GetValue(InfantsProperty);
        set => SetValue(InfantsProperty, value);
    }

    /// <inheritdoc cref="MaxGuestsProperty"/>
    public int MaxGuests
    {
        get => GetValue(MaxGuestsProperty);
        set => SetValue(MaxGuestsProperty, value);
    }

    /// <inheritdoc cref="MaxInfantsProperty"/>
    public int MaxInfants
    {
        get => GetValue(MaxInfantsProperty);
        set => SetValue(MaxInfantsProperty, value);
    }

    /// <inheritdoc cref="MinDateProperty"/>
    public DateOnly? MinDate
    {
        get => GetValue(MinDateProperty);
        set => SetValue(MinDateProperty, value);
    }

    /// <inheritdoc cref="CultureNameProperty"/>
    public string CultureName
    {
        get => GetValue(CultureNameProperty);
        set => SetValue(CultureNameProperty, value);
    }

    /// <inheritdoc cref="PlaceLabelProperty"/>
    public string PlaceLabel
    {
        get => GetValue(PlaceLabelProperty);
        set => SetValue(PlaceLabelProperty, value);
    }

    /// <inheritdoc cref="DatesLabelProperty"/>
    public string DatesLabel
    {
        get => GetValue(DatesLabelProperty);
        set => SetValue(DatesLabelProperty, value);
    }

    /// <inheritdoc cref="GuestsLabelProperty"/>
    public string GuestsLabel
    {
        get => GetValue(GuestsLabelProperty);
        set => SetValue(GuestsLabelProperty, value);
    }

    /// <inheritdoc cref="AnyPlaceTextProperty"/>
    public string AnyPlaceText
    {
        get => GetValue(AnyPlaceTextProperty);
        set => SetValue(AnyPlaceTextProperty, value);
    }

    /// <inheritdoc cref="AnyDatesTextProperty"/>
    public string AnyDatesText
    {
        get => GetValue(AnyDatesTextProperty);
        set => SetValue(AnyDatesTextProperty, value);
    }

    /// <inheritdoc cref="AnyGuestsTextProperty"/>
    public string AnyGuestsText
    {
        get => GetValue(AnyGuestsTextProperty);
        set => SetValue(AnyGuestsTextProperty, value);
    }

    /// <inheritdoc cref="PlaceSearchPlaceholderProperty"/>
    public string PlaceSearchPlaceholder
    {
        get => GetValue(PlaceSearchPlaceholderProperty);
        set => SetValue(PlaceSearchPlaceholderProperty, value);
    }

    /// <inheritdoc cref="NoPlacesTextProperty"/>
    public string NoPlacesText
    {
        get => GetValue(NoPlacesTextProperty);
        set => SetValue(NoPlacesTextProperty, value);
    }

    /// <inheritdoc cref="SearchTextProperty"/>
    public string SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    /// <inheritdoc cref="GuestFormatProperty"/>
    public string GuestFormat
    {
        get => GetValue(GuestFormatProperty);
        set => SetValue(GuestFormatProperty, value);
    }

    /// <inheritdoc cref="GuestsFormatProperty"/>
    public string GuestsFormat
    {
        get => GetValue(GuestsFormatProperty);
        set => SetValue(GuestsFormatProperty, value);
    }

    /// <inheritdoc cref="InfantFormatProperty"/>
    public string InfantFormat
    {
        get => GetValue(InfantFormatProperty);
        set => SetValue(InfantFormatProperty, value);
    }

    /// <inheritdoc cref="InfantsFormatProperty"/>
    public string InfantsFormat
    {
        get => GetValue(InfantsFormatProperty);
        set => SetValue(InfantsFormatProperty, value);
    }

    /// <inheritdoc cref="NightFormatProperty"/>
    public string NightFormat
    {
        get => GetValue(NightFormatProperty);
        set => SetValue(NightFormatProperty, value);
    }

    /// <inheritdoc cref="NightsFormatProperty"/>
    public string NightsFormat
    {
        get => GetValue(NightsFormatProperty);
        set => SetValue(NightsFormatProperty, value);
    }

    /// <inheritdoc cref="PickCheckInTextProperty"/>
    public string PickCheckInText
    {
        get => GetValue(PickCheckInTextProperty);
        set => SetValue(PickCheckInTextProperty, value);
    }

    /// <inheritdoc cref="PickCheckOutTextProperty"/>
    public string PickCheckOutText
    {
        get => GetValue(PickCheckOutTextProperty);
        set => SetValue(PickCheckOutTextProperty, value);
    }

    /// <inheritdoc cref="CheckInTextProperty"/>
    public string CheckInText
    {
        get => GetValue(CheckInTextProperty);
        set => SetValue(CheckInTextProperty, value);
    }

    /// <inheritdoc cref="CheckOutTextProperty"/>
    public string CheckOutText
    {
        get => GetValue(CheckOutTextProperty);
        set => SetValue(CheckOutTextProperty, value);
    }

    /// <inheritdoc cref="ClearDatesTextProperty"/>
    public string ClearDatesText
    {
        get => GetValue(ClearDatesTextProperty);
        set => SetValue(ClearDatesTextProperty, value);
    }

    /// <inheritdoc cref="PreviousMonthTextProperty"/>
    public string PreviousMonthText
    {
        get => GetValue(PreviousMonthTextProperty);
        set => SetValue(PreviousMonthTextProperty, value);
    }

    /// <inheritdoc cref="NextMonthTextProperty"/>
    public string NextMonthText
    {
        get => GetValue(NextMonthTextProperty);
        set => SetValue(NextMonthTextProperty, value);
    }

    /// <inheritdoc cref="AdultsTextProperty"/>
    public string AdultsText
    {
        get => GetValue(AdultsTextProperty);
        set => SetValue(AdultsTextProperty, value);
    }

    /// <inheritdoc cref="ChildrenTextProperty"/>
    public string ChildrenText
    {
        get => GetValue(ChildrenTextProperty);
        set => SetValue(ChildrenTextProperty, value);
    }

    /// <inheritdoc cref="InfantsTextProperty"/>
    public string InfantsText
    {
        get => GetValue(InfantsTextProperty);
        set => SetValue(InfantsTextProperty, value);
    }

    /// <inheritdoc cref="AdultsDetailProperty"/>
    public string AdultsDetail
    {
        get => GetValue(AdultsDetailProperty);
        set => SetValue(AdultsDetailProperty, value);
    }

    /// <inheritdoc cref="ChildrenDetailProperty"/>
    public string ChildrenDetail
    {
        get => GetValue(ChildrenDetailProperty);
        set => SetValue(ChildrenDetailProperty, value);
    }

    /// <inheritdoc cref="InfantsDetailProperty"/>
    public string InfantsDetail
    {
        get => GetValue(InfantsDetailProperty);
        set => SetValue(InfantsDetailProperty, value);
    }

    /// <inheritdoc cref="DecreaseTextProperty"/>
    public string DecreaseText
    {
        get => GetValue(DecreaseTextProperty);
        set => SetValue(DecreaseTextProperty, value);
    }

    /// <inheritdoc cref="IncreaseTextProperty"/>
    public string IncreaseText
    {
        get => GetValue(IncreaseTextProperty);
        set => SetValue(IncreaseTextProperty, value);
    }

    /// <summary>Raised by the search button with everything the pill holds.</summary>
    public event EventHandler<BookingPillSearchEventArgs>? SearchRequested
    {
        add => AddHandler(SearchRequestedEvent, value);
        remove => RemoveHandler(SearchRequestedEvent, value);
    }

    internal double DayCellSize => GetValue(DayCellSizeProperty);

    internal double OpenGrow => GetValue(OpenGrowProperty);

    internal double DayFontSize => GetValue(DayFontSizeProperty);

    internal double WeekdayFontSize => GetValue(WeekdayFontSizeProperty);

    internal IBrush? MutedBrush => GetValue(MutedBrushProperty);

    internal IBrush? RangeBrush => GetValue(RangeBrushProperty);

    internal IBrush? PreviewBrush => GetValue(PreviewBrushProperty);

    internal IBrush? EndFillBrush => GetValue(EndFillBrushProperty);

    internal IBrush? EndTextBrush => GetValue(EndTextBrushProperty);

    internal IBrush? HoverBrush => GetValue(HoverBrushProperty);

    internal IBrush? FocusBrush => GetValue(FocusBrushProperty);

    internal CultureInfo Culture => _culture;

    internal BookingPillPickers Pickers => _pickers;

    internal BookingPillRow? Row => _row;

    internal ArcPopup? Popup => _popup;

    internal BookingPillSearchButton? SearchButton => _search;

    internal BookingPillSegmentButton? SegmentFor(BookingPillPart part) => part == BookingPillPart.None ? null : _segments[(int)part - 1];

    internal string? Announcement => _live?.Announcement;

    /// <summary>Opens a segment's picker (or closes it with <see cref="BookingPillPart.None"/>).</summary>
    public void Open(BookingPillPart part) => SetCurrentValue(OpenPartProperty, part);

    /// <summary>Closes the open picker.</summary>
    public void Close() => SetCurrentValue(OpenPartProperty, BookingPillPart.None);

    /// <summary>Closes the picker and raises <see cref="SearchRequested"/>.</summary>
    public void Search()
    {
        Close();
        RaiseEvent(new BookingPillSearchEventArgs(SearchRequestedEvent, Place, CheckIn, CheckOut, Adults, Children, Infants));
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        foreach (var segment in _segments)
        {
            if (segment is not null)
            {
                segment.Click -= OnSegmentClick;
            }
        }
        if (_search is not null)
        {
            _search.Click -= OnSearchClick;
        }
        if (_popup is not null)
        {
            _popup.Closed -= OnPopupClosed;
        }
        _shell = e.NameScope.Find<Border>(ShellPart);
        _row = e.NameScope.Find<BookingPillRow>(RowPart);
        _segments[0] = e.NameScope.Find<BookingPillSegmentButton>(PlacePart);
        _segments[1] = e.NameScope.Find<BookingPillSegmentButton>(DatesPart);
        _segments[2] = e.NameScope.Find<BookingPillSegmentButton>(GuestsPart);
        _search = e.NameScope.Find<BookingPillSearchButton>(SearchPart);
        _popup = e.NameScope.Find<ArcPopup>(PopupPart);
        _swap = e.NameScope.Find<ArcContentSwap>(SwapPart);
        _live = e.NameScope.Find<ExplainerAnnouncer>(LivePart);
        foreach (var segment in _segments)
        {
            if (segment is not null)
            {
                segment.Click += OnSegmentClick;
            }
        }
        if (_search is not null)
        {
            _search.Click += OnSearchClick;
        }
        if (_popup is not null)
        {
            _popup.Closed += OnPopupClosed;
            _popup.PlacementTarget = _shell;
        }
        UpdateTexts();
        ApplyOpen(animate: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OpenPartProperty)
        {
            ApplyOpen(animate: true);
        }
        else if (change.Property == CultureNameProperty)
        {
            try
            {
                _culture = CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(CultureName) ? "en-US" : CultureName);
            }
            catch (CultureNotFoundException)
            {
                _culture = CultureInfo.InvariantCulture;
            }
            _pickers.Month?.Refresh();
            UpdateTexts();
        }
        else if (change.Property == PlaceProperty || change.Property == CheckInProperty || change.Property == CheckOutProperty ||
                 change.Property == AdultsProperty || change.Property == ChildrenProperty || change.Property == InfantsProperty ||
                 change.Property == MaxGuestsProperty || change.Property == MaxInfantsProperty || change.Property == MinDateProperty)
        {
            UpdateTexts();
        }
        else if (change.Property == FontFamilyProperty || change.Property == ForegroundProperty ||
                 change.Property == DayFontSizeProperty || change.Property == WeekdayFontSizeProperty ||
                 change.Property == MutedBrushProperty || change.Property == EndTextBrushProperty ||
                 change.Property == FocusBrushProperty || change.Property == DayCellSizeProperty)
        {
            _pickers.Month?.Refresh();
        }
        else if (change.Property == OpenGrowProperty && _row is not null)
        {
            _row.OpenGrow = change.GetNewValue<double>();
        }
        else if (change.Property.OwnerType == typeof(BookingPill) && change.Property.PropertyType == typeof(string))
        {
            UpdateTexts();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Watch(null);
    }

    // --- open and close -------------------------------------------------------------------------------------------

    private void OnSegmentClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not BookingPillSegmentButton segment)
        {
            return;
        }
        _keyboardOpen = !_pointerInput;
        // A click on the open segment closes it again.
        Open(OpenPart == segment.Part ? BookingPillPart.None : segment.Part);
    }

    private void OnSearchClick(object? sender, RoutedEventArgs e) => Search();

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (OpenPart != BookingPillPart.None && _popup is { IsOpen: false })
        {
            Close();
        }
    }

    private void ApplyOpen(bool animate)
    {
        var part = OpenPart;
        var open = part != BookingPillPart.None;
        PseudoClasses.Set(OpenPseudoClass, open);
        for (var i = 0; i < 3; i++)
        {
            if (_segments[i] is { } segment)
            {
                segment.IsOpen = (int)part == i + 1;
            }
        }
        if (_search is not null)
        {
            _search.IsExpanded = open;
        }
        _row?.Open(open ? (int)part - 1 : -1, animate && IsLoaded);
        if (_popup is null || _swap is null)
        {
            return;
        }
        if (!open)
        {
            Watch(null);
            if (_popup.IsOpen && _shell is not null && _popup.Child is { } child && child.IsKeyboardFocusWithin)
            {
                // Hand focus back before the picker leaves, or it falls back to the window.
                SegmentFor(_lastPart)?.Focus(_pointerInput ? NavigationMethod.Pointer : NavigationMethod.Tab);
            }
            _popup.IsOpen = false;
            return;
        }
        var picker = _pickers.For(part);
        _swap.Mode = _popup.IsOpen && animate ? ContentSwapMode.Crossfade : ContentSwapMode.None;
        _swap.Content = picker;
        _popup.Placement = part switch
        {
            BookingPillPart.Place => PlacementMode.BottomEdgeAlignedLeft,
            BookingPillPart.Dates => PlacementMode.Bottom,
            _ => PlacementMode.BottomEdgeAlignedRight
        };
        if (_popup.IsOpen)
        {
            _popup.Reposition();
        }
        else
        {
            _popup.IsOpen = true;
        }
        _lastPart = part;
        _live?.Announce(SegmentFor(part)?.Label);
        Watch(TopLevel.GetTopLevel(this));
        if (_keyboardOpen)
        {
            _keyboardOpen = false;
            Dispatcher.UIThread.Post(() =>
            {
                if (OpenPart == part)
                {
                    _pickers.FirstFocus(part)?.Focus(NavigationMethod.Tab);
                }
            }, DispatcherPriority.Loaded);
        }
    }

    private BookingPillPart _lastPart;

    /// <summary>Watches presses in the window while a picker is open: one outside the pill and the picker closes it.</summary>
    private void Watch(TopLevel? top)
    {
        if (ReferenceEquals(top, _watched))
        {
            return;
        }
        _watched?.RemoveHandler(PointerPressedEvent, OnWindowPointerPressed);
        _watched = top;
        top?.AddHandler(PointerPressedEvent, OnWindowPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Visual source || this.IsVisualAncestorOf(source) || ReferenceEquals(source, this))
        {
            return;
        }
        if (_popup?.Child is { } child && (ReferenceEquals(child, source) || child.IsVisualAncestorOf(source)))
        {
            return;
        }
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Key != Key.Escape || OpenPart == BookingPillPart.None)
        {
            return;
        }
        var part = OpenPart;
        SegmentFor(part)?.Focus(NavigationMethod.Tab);
        Close();
        e.Handled = true;
    }

    // --- values ---------------------------------------------------------------------------------------------------

    internal void PickPlace(string name)
    {
        SetCurrentValue(PlaceProperty, name);
        _keyboardOpen = !_pointerInput;
        Open(BookingPillPart.Dates);
    }

    internal bool CanPick(DateOnly day) => MinDate is not { } min || day >= min;

    /// <summary>
    /// The first pick is the check-in; the next pick after it is the check-out, and the guests open. A pick on or before
    /// the check-in, or a pick once both are set, starts a new stay.
    /// </summary>
    internal void PickDay(DateOnly day)
    {
        if (!CanPick(day))
        {
            return;
        }
        if (CheckIn is { } from && CheckOut is null && day > from)
        {
            SetCurrentValue(CheckOutProperty, day);
            _live?.Announce($"{day.ToString("D", _culture)}, {CheckOutText}. {StayLine()}");
            _keyboardOpen = !_pointerInput;
            Open(BookingPillPart.Guests);
            return;
        }
        SetCurrentValue(CheckOutProperty, null);
        SetCurrentValue(CheckInProperty, day);
        _live?.Announce($"{day.ToString("D", _culture)}, {CheckInText}. {PickCheckOutText}");
    }

    internal void ClearDates()
    {
        SetCurrentValue(CheckInProperty, null);
        SetCurrentValue(CheckOutProperty, null);
    }

    internal int GuestCount(int kind) => kind switch
    {
        0 => Adults,
        1 => Children,
        _ => Infants
    };

    internal string GuestTitle(int kind) => kind switch
    {
        0 => AdultsText,
        1 => ChildrenText,
        _ => InfantsText
    };

    internal string GuestDetail(int kind) => kind switch
    {
        0 => AdultsDetail,
        1 => ChildrenDetail,
        _ => InfantsDetail
    };

    /// <summary>
    /// Whether a stepper may move: counts stay at zero or more, adults and children within <see cref="MaxGuests"/>,
    /// infants within <see cref="MaxInfants"/>, and the last adult stays while children or infants come along.
    /// </summary>
    internal bool CanChangeGuests(int kind, int delta)
    {
        var value = GuestCount(kind) + delta;
        if (value < 0)
        {
            return false;
        }
        if (delta > 0)
        {
            return kind == 2 ? value <= MaxInfants : Adults + Children + delta <= MaxGuests;
        }
        return kind != 0 || value > 0 || (Children == 0 && Infants == 0);
    }

    internal void ChangeGuests(int kind, int delta)
    {
        if (!CanChangeGuests(kind, delta))
        {
            return;
        }
        var property = kind switch
        {
            0 => AdultsProperty,
            1 => ChildrenProperty,
            _ => InfantsProperty
        };
        SetCurrentValue(property, GuestCount(kind) + delta);
        // Children and infants travel with an adult.
        if (kind != 0 && delta > 0 && Adults == 0)
        {
            SetCurrentValue(AdultsProperty, 1);
        }
        _live?.Announce($"{GuestTitle(kind)} {GuestCount(kind).ToString(_culture)}");
    }

    private static string Plural(string one, string many, int count, CultureInfo culture)
        => string.Format(culture, count == 1 ? one : many, count);

    /// <summary>The place segment's value: the place or "Anywhere".</summary>
    internal string PlaceSummary() => string.IsNullOrWhiteSpace(Place) ? AnyPlaceText : Place;

    /// <summary>The dates segment's value: "Mar 14 – 18", "Mar 30 – Apr 2", "Mar 14 –" or "Add dates".</summary>
    internal string DatesSummary()
    {
        if (CheckIn is not { } from)
        {
            return AnyDatesText;
        }
        var start = from.ToString("MMM d", _culture);
        if (CheckOut is not { } to)
        {
            return start + " –";
        }
        var end = to.Month == from.Month && to.Year == from.Year ? to.ToString("%d", _culture) : to.ToString("MMM d", _culture);
        return $"{start} – {end}";
    }

    /// <summary>The guests segment's value: "3 guests, 1 infant" or "Add guests".</summary>
    internal string GuestsSummary()
    {
        var guests = Adults + Children;
        if (guests == 0 && Infants == 0)
        {
            return AnyGuestsText;
        }
        var text = Plural(GuestFormat, GuestsFormat, guests, _culture);
        return Infants > 0 ? $"{text}, {Plural(InfantFormat, InfantsFormat, Infants, _culture)}" : text;
    }

    /// <summary>The line under the month: what to pick next, or the stay ("4 nights").</summary>
    internal string StayLine()
    {
        if (CheckIn is null)
        {
            return PickCheckInText;
        }
        if (CheckOut is not { } to)
        {
            return PickCheckOutText;
        }
        var nights = to.DayNumber - CheckIn.Value.DayNumber;
        return Plural(NightFormat, NightsFormat, nights, _culture);
    }

    private void UpdateTexts()
    {
        if (_segments[0] is { } place)
        {
            place.Label = PlaceLabel;
            place.Value = PlaceSummary();
            place.IsEmpty = string.IsNullOrWhiteSpace(Place);
        }
        if (_segments[1] is { } dates)
        {
            dates.Label = DatesLabel;
            dates.Value = DatesSummary();
            dates.IsEmpty = CheckIn is null;
        }
        if (_segments[2] is { } guests)
        {
            guests.Label = GuestsLabel;
            guests.Value = GuestsSummary();
            guests.IsEmpty = Adults + Children + Infants == 0;
        }
        if (_search is not null)
        {
            _search.Text = SearchText;
        }
        _pickers.UpdateTexts();
    }
}
