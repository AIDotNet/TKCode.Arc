using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>A place row in the place picker of a <see cref="BookingPill"/>: the name over its detail.</summary>
internal sealed class BookingPillOption : AvaloniaButton
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<BookingPillOption, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<BookingPillOption, string?>(nameof(Detail));

    public static readonly StyledProperty<bool> IsCurrentProperty =
        AvaloniaProperty.Register<BookingPillOption, bool>(nameof(IsCurrent));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    /// <summary>The place already picked.</summary>
    public bool IsCurrent
    {
        get => GetValue(IsCurrentProperty);
        set => SetValue(IsCurrentProperty, value);
    }

    public BookingPlace? Place { get; set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCurrentProperty)
        {
            PseudoClasses.Set(":current", change.GetNewValue<bool>());
        }
        else if (change.Property == TitleProperty || change.Property == DetailProperty)
        {
            AutomationProperties.SetName(this, string.IsNullOrEmpty(Detail) ? Title : $"{Title}, {Detail}");
        }
    }
}

/// <summary>
/// The three pickers of a <see cref="BookingPill"/>, built in code the first time each is opened: a filterable list of
/// places, a month of days for the stay, and steppers for the guests with rolling counts.
/// </summary>
internal sealed class BookingPillPickers
{
    private readonly BookingPill _owner;
    private readonly List<BookingPillOption> _options = [];
    private StackPanel? _place;
    private Input? _search;
    private StackPanel? _list;
    private TextBlock? _noMatch;
    private StackPanel? _dates;
    private BookingPillMonth? _month;
    private TextBlock? _monthTitle;
    private TextBlock? _stay;
    private Avalonia.Controls.Control? _clear;
    private StackPanel? _guests;
    private readonly RollingDigits?[] _counts = new RollingDigits?[3];
    private readonly Button?[] _minus = new Button?[3];
    private readonly Button?[] _plus = new Button?[3];
    private readonly TextBlock?[] _guestTitles = new TextBlock?[3];
    private readonly TextBlock?[] _guestDetails = new TextBlock?[3];

    public BookingPillPickers(BookingPill owner)
    {
        _owner = owner;
    }

    public BookingPillMonth? Month => _month;

    public Input? SearchBox => _search;

    public IReadOnlyList<BookingPillOption> Options => _options;

    public IReadOnlyList<RollingDigits?> Counts => _counts;

    public IReadOnlyList<Button?> PlusButtons => _plus;

    public IReadOnlyList<Button?> MinusButtons => _minus;

    public string? StayText => _stay?.Text;

    public Control For(BookingPillPart part) => part switch
    {
        BookingPillPart.Place => PlacePicker(),
        BookingPillPart.Dates => DatesPicker(),
        _ => GuestsPicker()
    };

    /// <summary>The first control to focus when a picker opens from the keyboard.</summary>
    public Control? FirstFocus(BookingPillPart part) => part switch
    {
        BookingPillPart.Place => _search,
        BookingPillPart.Dates => _month,
        BookingPillPart.Guests => _plus[0],
        _ => null
    };

    private static TextBlock Text(ArcDesignTokenKind size, ArcDesignTokenKind brush, FontWeight weight = FontWeight.Normal)
    {
        var text = new TextBlock { FontWeight = weight, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(text, TextBlock.FontSizeProperty, size);
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, brush);
        return text;
    }

    private static Button IconButton(Geometry icon, ButtonVariant variant)
    {
        var glyph = new ArcGlyph { Data = icon, Size = 16, VerticalAlignment = VerticalAlignment.Center };
        return new Button { Variant = variant, Size = ControlSize.Small, Content = glyph, MinWidth = 36 };
    }

    // --- place ----------------------------------------------------------------------------------------------------

    private Control PlacePicker()
    {
        if (_place is not null)
        {
            return _place;
        }
        _search = new Input { IsLabelVisible = false, Placeholder = _owner.PlaceSearchPlaceholder };
        AutomationProperties.SetName(_search, _owner.PlaceLabel);
        _search.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                FilterPlaces();
            }
        };
        _search.KeyDown += OnSearchKeyDown;
        _list = new StackPanel { Spacing = 2 };
        _noMatch = Text(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextMuted);
        _noMatch.Margin = new Thickness(12, 8);
        _place = new StackPanel { Spacing = 8, Children = { _search, _list, _noMatch } };
        FilterPlaces();
        return _place;
    }

    /// <summary>Shows the places whose name or detail contains the typed text, reusing the rows already made.</summary>
    public void FilterPlaces()
    {
        if (_list is null || _noMatch is null)
        {
            return;
        }
        var query = _search?.Text?.Trim() ?? string.Empty;
        var shown = 0;
        foreach (var place in _owner.Places)
        {
            if (query.Length > 0 &&
                !place.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) &&
                !(place.Detail?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false))
            {
                continue;
            }
            if (shown == _options.Count)
            {
                var option = new BookingPillOption();
                option.Click += OnOptionClick;
                option.KeyDown += OnOptionKeyDown;
                _options.Add(option);
                _list.Children.Add(option);
            }
            var row = _options[shown];
            row.Place = place;
            row.Title = place.Name;
            row.Detail = place.Detail;
            row.IsCurrent = place.Name == _owner.Place;
            row.IsVisible = true;
            shown++;
        }
        for (var i = shown; i < _options.Count; i++)
        {
            _options[i].IsVisible = false;
            _options[i].Place = null;
        }
        _noMatch.Text = _owner.NoPlacesText;
        _noMatch.IsVisible = shown == 0;
    }

    private void OnOptionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is BookingPillOption { Place: { } place })
        {
            _owner.PickPlace(place.Name);
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && FirstVisible(0, 1) is { } first)
        {
            first.Focus(NavigationMethod.Directional);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && FirstVisible(0, 1) is { Place: { } place })
        {
            _owner.PickPlace(place.Name);
            e.Handled = true;
        }
    }

    private void OnOptionKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not BookingPillOption option || (e.Key != Key.Down && e.Key != Key.Up))
        {
            return;
        }
        var index = _options.IndexOf(option);
        var step = e.Key == Key.Down ? 1 : -1;
        if (FirstVisible(index + step, step) is { } next)
        {
            next.Focus(NavigationMethod.Directional);
        }
        else if (step < 0)
        {
            _search?.Focus(NavigationMethod.Directional);
        }
        e.Handled = true;
    }

    private BookingPillOption? FirstVisible(int from, int step)
    {
        for (var i = from; i >= 0 && i < _options.Count; i += step)
        {
            if (_options[i].IsVisible)
            {
                return _options[i];
            }
        }
        return null;
    }

    // --- dates ----------------------------------------------------------------------------------------------------

    private Control DatesPicker()
    {
        if (_dates is not null)
        {
            return _dates;
        }
        var previous = IconButton(ArcIcons.ChevronLeft, ButtonVariant.Ghost);
        var next = IconButton(ArcIcons.ChevronRight, ButtonVariant.Ghost);
        AutomationProperties.SetName(previous, _owner.PreviousMonthText);
        AutomationProperties.SetName(next, _owner.NextMonthText);
        _monthTitle = Text(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, FontWeight.SemiBold);
        _monthTitle.HorizontalAlignment = HorizontalAlignment.Center;
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        Grid.SetColumn(_monthTitle, 1);
        Grid.SetColumn(next, 2);
        header.Children.Add(previous);
        header.Children.Add(_monthTitle);
        header.Children.Add(next);
        _month = new BookingPillMonth(_owner) { HorizontalAlignment = HorizontalAlignment.Stretch };
        _month.MonthChanged += UpdateDates;
        previous.Click += (_, _) => _month.ShowMonth(_month.Month.AddMonths(-1));
        next.Click += (_, _) => _month.ShowMonth(_month.Month.AddMonths(1));
        _stay = Text(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextSecondary);
        var clear = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, Content = _owner.ClearDatesText };
        clear.Click += (_, _) => _owner.ClearDates();
        _clear = clear;
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(clear, 1);
        footer.Children.Add(_stay);
        footer.Children.Add(clear);
        _dates = new StackPanel { Spacing = 8, Children = { header, _month, footer } };
        var first = _owner.CheckIn ?? _owner.MinDate ?? DateOnly.FromDateTime(DateTime.Today);
        _month.MoveCursor(first);
        _month.ShowMonth(first);
        UpdateDates();
        return _dates;
    }

    /// <summary>The month title, the stay line and the clear button follow the picked dates.</summary>
    public void UpdateDates()
    {
        if (_month is null || _monthTitle is null || _stay is null)
        {
            return;
        }
        _monthTitle.Text = _month.Month.ToString("MMMM yyyy", _owner.Culture);
        _stay.Text = _owner.StayLine();
        if (_clear is not null)
        {
            _clear.IsEnabled = _owner.CheckIn is not null;
        }
        _month.InvalidateVisual();
    }

    // --- guests ---------------------------------------------------------------------------------------------------

    private Control GuestsPicker()
    {
        if (_guests is not null)
        {
            return _guests;
        }
        _guests = new StackPanel();
        for (var i = 0; i < 3; i++)
        {
            if (i > 0)
            {
                var line = new Border { Height = 1, Margin = new Thickness(0, 4) };
                ArcTokens.Bind(line, Border.BackgroundProperty, ArcDesignTokenKind.BorderSubtle);
                _guests.Children.Add(line);
            }
            _guests.Children.Add(GuestRow(i));
        }
        UpdateGuests();
        return _guests;
    }

    private Control GuestRow(int kind)
    {
        var title = Text(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, FontWeight.SemiBold);
        var detail = Text(ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted);
        _guestTitles[kind] = title;
        _guestDetails[kind] = detail;
        var minus = IconButton(ArcIcons.Minus, ButtonVariant.Secondary);
        var plus = IconButton(ArcIcons.Plus, ButtonVariant.Secondary);
        minus.Click += (_, _) => _owner.ChangeGuests(kind, -1);
        plus.Click += (_, _) => _owner.ChangeGuests(kind, 1);
        _minus[kind] = minus;
        _plus[kind] = plus;
        var count = new RollingDigits { MinWidth = 28, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        ArcTokens.Bind(count, RollingDigits.FontSizeProperty, ArcDesignTokenKind.TypeBase);
        ArcTokens.Bind(count, RollingDigits.ForegroundProperty, ArcDesignTokenKind.Foreground);
        count.FontWeight = FontWeight.SemiBold;
        _counts[kind] = count;
        var countBox = new Border { Width = 36, Child = count };
        var stepper = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { minus, countBox, plus } };
        var words = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { title, detail } };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(4, 8) };
        Grid.SetColumn(stepper, 1);
        row.Children.Add(words);
        row.Children.Add(stepper);
        return row;
    }

    /// <summary>Counts, labels and the limits of the − and + buttons follow the guests.</summary>
    public void UpdateGuests()
    {
        if (_guests is null)
        {
            return;
        }
        for (var kind = 0; kind < 3; kind++)
        {
            var value = _owner.GuestCount(kind);
            if (_counts[kind] is { } count)
            {
                count.Value = value;
            }
            _guestTitles[kind]!.Text = _owner.GuestTitle(kind);
            _guestDetails[kind]!.Text = _owner.GuestDetail(kind);
            _minus[kind]!.IsEnabled = _owner.CanChangeGuests(kind, -1);
            _plus[kind]!.IsEnabled = _owner.CanChangeGuests(kind, 1);
            AutomationProperties.SetName(_minus[kind]!, $"{_owner.DecreaseText} {_owner.GuestTitle(kind)}");
            AutomationProperties.SetName(_plus[kind]!, $"{_owner.IncreaseText} {_owner.GuestTitle(kind)}");
            AutomationProperties.SetHelpText(_minus[kind]!, value.ToString(_owner.Culture));
            AutomationProperties.SetHelpText(_plus[kind]!, value.ToString(_owner.Culture));
        }
    }

    /// <summary>Texts that come from the pill's properties.</summary>
    public void UpdateTexts()
    {
        if (_search is not null)
        {
            _search.Placeholder = _owner.PlaceSearchPlaceholder;
            AutomationProperties.SetName(_search, _owner.PlaceLabel);
        }
        if (_clear is Button clear)
        {
            clear.Content = _owner.ClearDatesText;
        }
        FilterPlaces();
        UpdateDates();
        UpdateGuests();
    }
}
