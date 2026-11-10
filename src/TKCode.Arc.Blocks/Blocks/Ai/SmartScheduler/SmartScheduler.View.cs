using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

// The template parts and how they follow the state.
public partial class SmartScheduler
{
    private SmartSchedulerWeek? _week;
    private Panel? _people;
    private SegmentedControl? _duration;
    private Textarea? _constraintsBox;
    private Panel? _chips;
    private Panel? _options;
    private Button? _find;
    private Button? _book;
    private Button? _startOver;
    private TextBlock? _status;
    private AiAnnouncer? _announcer;
    private readonly Dictionary<string, TextBlock> _texts = new();
    private readonly List<SmartSchedulerOption> _optionRows = new();
    private bool _syncing;
    private bool _statusIsError;

    internal SmartSchedulerWeek? Week => _week;

    internal IReadOnlyList<SmartSchedulerOption> OptionRows => _optionRows;

    internal Panel? ChipsPanel => _chips;

    internal Panel? PeoplePanel => _people;

    internal TextBlock? StatusText => _status;

    internal string? Announcement => _announcer?.Announcement;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_week is not null)
        {
            _week.SelectionRequested -= SelectSlot;
        }
        if (_find is not null)
        {
            _find.Click -= OnFindClick;
        }
        if (_book is not null)
        {
            _book.Click -= OnBookClick;
        }
        if (_startOver is not null)
        {
            _startOver.Click -= OnStartOverClick;
        }
        if (_duration is not null)
        {
            _duration.SelectionChanged -= OnDurationChanged;
        }
        if (_constraintsBox is not null)
        {
            _constraintsBox.TextChanged -= OnConstraintsChanged;
        }
        _week = e.NameScope.Find<SmartSchedulerWeek>(WeekPart);
        _people = e.NameScope.Find<Panel>("PART_People");
        _duration = e.NameScope.Find<SegmentedControl>("PART_Duration");
        _constraintsBox = e.NameScope.Find<Textarea>("PART_Constraints");
        _chips = e.NameScope.Find<Panel>("PART_Chips");
        _options = e.NameScope.Find<Panel>("PART_Options");
        _find = e.NameScope.Find<Button>(FindPart);
        _book = e.NameScope.Find<Button>(BookPart);
        _startOver = e.NameScope.Find<Button>("PART_StartOver");
        _status = e.NameScope.Find<TextBlock>("PART_Status");
        _announcer = e.NameScope.Find<AiAnnouncer>("PART_Announcer");
        _texts.Clear();
        foreach (var name in new[] { "Title", "PeopleHeading", "DurationHeading", "ConstraintsHeading", "ProposalsHeading" })
        {
            if (e.NameScope.Find<TextBlock>("PART_" + name) is { } text)
            {
                _texts[name] = text;
            }
        }
        if (_week is not null)
        {
            _week.SelectionRequested += SelectSlot;
        }
        if (_find is not null)
        {
            _find.Click += OnFindClick;
        }
        if (_book is not null)
        {
            _book.Click += OnBookClick;
        }
        if (_startOver is not null)
        {
            _startOver.Click += OnStartOverClick;
        }
        if (_duration is not null)
        {
            _duration.SelectionChanged += OnDurationChanged;
        }
        if (_constraintsBox is not null)
        {
            _constraintsBox.Text = ConstraintsText;
            _constraintsBox.TextChanged += OnConstraintsChanged;
        }
        BuildPeople();
        BuildDurations();
        BuildChips();
        UpdateWeek();
        RenderSlots();
        UpdateTexts();
    }

    private void OnFindClick(object? sender, RoutedEventArgs e) => _ = FindAsync();

    private void OnBookClick(object? sender, RoutedEventArgs e) => _ = BookAsync();

    private void OnStartOverClick(object? sender, RoutedEventArgs e)
    {
        StartOver();
        _find?.Focus(NavigationMethod.Tab);
    }

    private void OnDurationChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && _duration?.SelectedValue is int minutes && minutes != DurationMinutes)
        {
            SetCurrentValue(DurationMinutesProperty, minutes);
            ClearSlots();
        }
    }

    private void OnConstraintsChanged(object? sender, TextChangedEventArgs e)
    {
        if (_constraintsBox is not null && _constraintsBox.Text != ConstraintsText)
        {
            SetCurrentValue(ConstraintsTextProperty, _constraintsBox.Text);
        }
    }

    private void BuildPeople()
    {
        if (_people is null)
        {
            return;
        }
        _people.Children.Clear();
        foreach (var person in People ?? [])
        {
            var box = new Checkbox
            {
                Content = person.Name,
                Description = person.Detail,
                IsChecked = _invited.Contains(person.Id),
                Tag = person,
            };
            box.IsCheckedChanged += (_, _) =>
            {
                if (!_syncing)
                {
                    SetInvited(person, box.IsChecked == true);
                }
            };
            _people.Children.Add(box);
        }
    }

    private void SyncPeople()
    {
        if (_people is null)
        {
            return;
        }
        _syncing = true;
        try
        {
            foreach (var box in _people.Children.OfType<Checkbox>())
            {
                if (box.Tag is SmartSchedulerPerson person)
                {
                    box.IsChecked = _invited.Contains(person.Id);
                }
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void BuildDurations()
    {
        if (_duration is null)
        {
            return;
        }
        _syncing = true;
        try
        {
            _duration.Items.Clear();
            foreach (var minutes in Durations ?? [])
            {
                _duration.Items.Add(new SegmentedOption(minutes, Labels.Minutes(minutes)));
            }
            _duration.SelectedValue = DurationMinutes;
            AutomationProperties.SetName(_duration, Labels.Duration);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SyncDuration()
    {
        if (_duration is null)
        {
            return;
        }
        _syncing = true;
        try
        {
            _duration.SelectedValue = DurationMinutes;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void BuildChips()
    {
        if (_chips is null)
        {
            return;
        }
        _chips.Children.Clear();
        foreach (var text in Constraints.Understood)
        {
            _chips.Children.Add(new SmartSchedulerChip { Text = text });
        }
        _chips.IsVisible = _chips.Children.Count > 0;
    }

    private void UpdateWeek()
    {
        if (_week is null)
        {
            return;
        }
        var days = Math.Max(1, Days);
        var labels = new string[days];
        for (var i = 0; i < days; i++)
        {
            labels[i] = DayLabel(WeekStart.AddDays(i));
        }
        _week.SetWeek(days, DayStartHour, DayEndHour, labels, Labels.Hour, InvitedPeople);
        AutomationProperties.SetName(_week, Labels.Week);
    }

    private void RenderSlots()
    {
        var selected = SelectedSlot is { } s ? IndexOf(s) : -1;
        _week?.SetSlots(Slots, selected);
        if (_options is null)
        {
            return;
        }
        foreach (var row in _optionRows)
        {
            row.Click -= OnOptionClick;
        }
        _options.Children.Clear();
        _optionRows.Clear();
        for (var i = 0; i < Slots.Count; i++)
        {
            var slot = Slots[i];
            var text = SlotText(slot);
            var row = new SmartSchedulerOption
            {
                Rank = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                Title = text,
                Reasons = string.Join(" · ", slot.Reasons),
                Index = i,
            };
            AutomationProperties.SetName(row, Labels.Option(i + 1, text));
            AutomationProperties.SetHelpText(row, row.Reasons);
            row.Click += OnOptionClick;
            _optionRows.Add(row);
            _options.Children.Add(row);
        }
        SyncOptions();
    }

    private int IndexOf(SmartSchedulerSlot slot)
    {
        for (var i = 0; i < Slots.Count; i++)
        {
            if (ReferenceEquals(Slots[i], slot))
            {
                return i;
            }
        }
        return -1;
    }

    private void OnOptionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is SmartSchedulerOption option)
        {
            SelectSlot(option.Index);
        }
    }

    private void SyncOptions()
    {
        var selected = SelectedSlot is { } s ? IndexOf(s) : -1;
        foreach (var row in _optionRows)
        {
            row.IsSelected = row.Index == selected;
        }
        if (_week is not null && selected >= 0)
        {
            AutomationProperties.SetHelpText(_week, Labels.Option(selected + 1, SlotText(Slots[selected])));
        }
    }

    private void UpdateTexts()
    {
        var labels = Labels;
        SetText("Title", labels.Title);
        SetText("PeopleHeading", labels.People);
        SetText("DurationHeading", labels.Duration);
        SetText("ConstraintsHeading", labels.Constraints);
        SetText("ProposalsHeading", labels.Proposals);
        if (_constraintsBox is not null)
        {
            _constraintsBox.Placeholder = labels.ConstraintsPlaceholder;
            AutomationProperties.SetName(_constraintsBox, labels.Constraints);
        }
        if (_find is not null)
        {
            _find.Content = labels.Find;
            _find.IsLoading = State == SmartSchedulerState.Finding;
            _find.IsEnabled = State is not (SmartSchedulerState.Booking or SmartSchedulerState.Booked);
        }
        if (_book is not null)
        {
            _book.IsLoading = State == SmartSchedulerState.Booking;
            _book.Content = SelectedSlot is { } slot ? labels.Book(labels.SlotShort(Times(slot).Start)) : labels.Book("");
        }
        if (_startOver is not null)
        {
            _startOver.Content = labels.StartOver;
        }
        if (State != SmartSchedulerState.Booked && !_statusIsError)
        {
            SetStatus(State switch
            {
                SmartSchedulerState.Finding => labels.Finding,
                SmartSchedulerState.NoTimes => labels.NoTimes,
                SmartSchedulerState.Idle => labels.Idle,
                _ => null,
            });
        }
        _statusIsError = false;
    }

    private void SetStatus(string? text, bool isError = false)
    {
        _statusIsError = isError;
        if (_status is null)
        {
            return;
        }
        _status.Text = text;
        _status.IsVisible = !string.IsNullOrEmpty(text);
        _status.Classes.Set("error", isError);
    }

    private void SetText(string name, string text)
    {
        if (_texts.TryGetValue(name, out var block))
        {
            block.Text = text;
        }
    }

    private void Announce(string text) => _announcer?.Announce(text);
}
