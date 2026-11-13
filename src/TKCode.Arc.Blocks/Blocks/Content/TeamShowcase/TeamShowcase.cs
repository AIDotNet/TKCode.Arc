using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A page section that introduces a team: a heading, a department filter and a grid of people, each with a portrait
/// (generated from the name when there is no picture), the name and the role. Pointing at a person, focusing them with
/// the keyboard or tapping them shows a short bio over the portrait. Choosing a department fades the others out and
/// slides the remaining people into their new places.
/// <code>
/// &lt;arc:TeamShowcase Title="The people behind Northwind"&gt;
///     &lt;arc:TeamShowcaseMember Name="Mara Okonjo" Role="Head of Design" Department="Design" Bio="..." /&gt;
/// &lt;/arc:TeamShowcase&gt;
/// </code>
/// </summary>
[TemplatePart(FilterPart, typeof(SegmentedControl))]
[TemplatePart(GridPart, typeof(TeamShowcaseGrid))]
[TemplatePart(AnnouncerPart, typeof(SiteAnnouncer))]
[PseudoClasses(CompactPseudoClass, NarrowPseudoClass, EmptyPseudoClass, NoFilterPseudoClass)]
public class TeamShowcase : TemplatedControl
{
    public const string FilterPart = "PART_Filter";
    public const string GridPart = "PART_Grid";
    public const string AnnouncerPart = "PART_Announcer";
    public const string CompactPseudoClass = ":compact";
    public const string NarrowPseudoClass = ":narrow";
    public const string EmptyPseudoClass = ":empty";
    public const string NoFilterPseudoClass = ":no-filter";

    /// <summary>The small line above the title; null leaves it out.</summary>
    public static readonly StyledProperty<string?> EyebrowProperty =
        AvaloniaProperty.Register<TeamShowcase, string?>(nameof(Eyebrow), "Our team");

    /// <summary>The section's heading.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<TeamShowcase, string?>(nameof(Title), "The people who make it");

    /// <summary>The paragraph under the heading; null leaves it out.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<TeamShowcase, string?>(nameof(Description),
            "A small team across nine cities. Hover or focus anyone to read what they work on.");

    /// <summary>The department whose people are shown (two-way); null shows everyone.</summary>
    public static readonly StyledProperty<string?> SelectedDepartmentProperty =
        AvaloniaProperty.Register<TeamShowcase, string?>(nameof(SelectedDepartment), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the department filter shows (it also hides when the people have fewer than two departments).</summary>
    public static readonly StyledProperty<bool> IsFilterVisibleProperty =
        AvaloniaProperty.Register<TeamShowcase, bool>(nameof(IsFilterVisible), true);

    /// <summary>The filter's first option, which shows everyone.</summary>
    public static readonly StyledProperty<string> AllLabelProperty =
        AvaloniaProperty.Register<TeamShowcase, string>(nameof(AllLabel), "Everyone");

    /// <summary>The screen reader name of the filter.</summary>
    public static readonly StyledProperty<string> FilterLabelProperty =
        AvaloniaProperty.Register<TeamShowcase, string>(nameof(FilterLabel), "Filter by department");

    /// <summary>What is announced after a department was chosen; {0} is the number of people, {1} the department.</summary>
    public static readonly StyledProperty<string> DepartmentResultFormatProperty =
        AvaloniaProperty.Register<TeamShowcase, string>(nameof(DepartmentResultFormat), "{0} people in {1}");

    /// <summary>What is announced after everyone was chosen; {0} is the number of people.</summary>
    public static readonly StyledProperty<string> AllResultFormatProperty =
        AvaloniaProperty.Register<TeamShowcase, string>(nameof(AllResultFormat), "{0} people");

    /// <summary>The line shown when the chosen department has nobody in it.</summary>
    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<TeamShowcase, string>(nameof(EmptyText), "Nobody here yet.");

    /// <summary>The widest a column of the grid gets before another column is added.</summary>
    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<TeamShowcase, double>(nameof(MinColumnWidth), 200);

    /// <summary>The most columns the grid uses.</summary>
    public static readonly StyledProperty<int> MaxColumnsProperty =
        AvaloniaProperty.Register<TeamShowcase, int>(nameof(MaxColumns), 4);

    /// <summary>Below this width of the block (720) the heading and the gaps get smaller.</summary>
    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<TeamShowcase, double>(nameof(CompactWidth), 720);

    /// <summary>Below this width of the block (480) the filter scrolls and the paddings tighten further.</summary>
    public static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<TeamShowcase, double>(nameof(NarrowWidth), 480);

    /// <summary>Raised when a person's card is pressed (click, tap, Enter or Space); the bio is pinned open at the same time.</summary>
    public static readonly RoutedEvent<TeamShowcaseMemberEventArgs> MemberInvokedEvent =
        RoutedEvent.Register<TeamShowcase, TeamShowcaseMemberEventArgs>(nameof(MemberInvoked), RoutingStrategies.Bubble);

    /// <summary>Whether the invented sample team shows while <see cref="Members"/> is empty.</summary>
    public static readonly StyledProperty<bool> ShowsSampleTeamProperty =
        AvaloniaProperty.Register<TeamShowcase, bool>(nameof(ShowsSampleTeam), true);

    private static readonly TeamShowcaseMember[] s_samples = TeamShowcaseSamples.Create().ToArray();
    private readonly AvaloniaList<TeamShowcaseMember> _members = new();
    private readonly List<TeamShowcaseCard> _cards = new();
    private SegmentedControl? _filter;
    private TeamShowcaseGrid? _grid;
    private SiteAnnouncer? _announcer;
    private bool _syncingFilter;

    public TeamShowcase()
    {
        _members.CollectionChanged += OnMembersChanged;
    }

    /// <summary>The people, in display order. While it is empty the block shows an invented sample team (see <see cref="ShowsSampleTeam"/>).</summary>
    [Content]
    public AvaloniaList<TeamShowcaseMember> Members => _members;

    /// <inheritdoc cref="ShowsSampleTeamProperty"/>
    public bool ShowsSampleTeam { get => GetValue(ShowsSampleTeamProperty); set => SetValue(ShowsSampleTeamProperty, value); }

    /// <summary>The people the block shows: <see cref="Members"/>, or the sample team while that is empty.</summary>
    public IReadOnlyList<TeamShowcaseMember> EffectiveMembers
        => _members.Count > 0 || !ShowsSampleTeam ? _members : s_samples;

    /// <inheritdoc cref="EyebrowProperty"/>
    public string? Eyebrow { get => GetValue(EyebrowProperty); set => SetValue(EyebrowProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="SelectedDepartmentProperty"/>
    public string? SelectedDepartment { get => GetValue(SelectedDepartmentProperty); set => SetValue(SelectedDepartmentProperty, value); }

    /// <inheritdoc cref="IsFilterVisibleProperty"/>
    public bool IsFilterVisible { get => GetValue(IsFilterVisibleProperty); set => SetValue(IsFilterVisibleProperty, value); }

    /// <inheritdoc cref="AllLabelProperty"/>
    public string AllLabel { get => GetValue(AllLabelProperty); set => SetValue(AllLabelProperty, value); }

    /// <inheritdoc cref="FilterLabelProperty"/>
    public string FilterLabel { get => GetValue(FilterLabelProperty); set => SetValue(FilterLabelProperty, value); }

    /// <inheritdoc cref="DepartmentResultFormatProperty"/>
    public string DepartmentResultFormat { get => GetValue(DepartmentResultFormatProperty); set => SetValue(DepartmentResultFormatProperty, value); }

    /// <inheritdoc cref="AllResultFormatProperty"/>
    public string AllResultFormat { get => GetValue(AllResultFormatProperty); set => SetValue(AllResultFormatProperty, value); }

    /// <inheritdoc cref="EmptyTextProperty"/>
    public string EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    /// <inheritdoc cref="MinColumnWidthProperty"/>
    public double MinColumnWidth { get => GetValue(MinColumnWidthProperty); set => SetValue(MinColumnWidthProperty, value); }

    /// <inheritdoc cref="MaxColumnsProperty"/>
    public int MaxColumns { get => GetValue(MaxColumnsProperty); set => SetValue(MaxColumnsProperty, value); }

    /// <inheritdoc cref="CompactWidthProperty"/>
    public double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <inheritdoc cref="NarrowWidthProperty"/>
    public double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    /// <inheritdoc cref="MemberInvokedEvent"/>
    public event EventHandler<TeamShowcaseMemberEventArgs>? MemberInvoked
    {
        add => AddHandler(MemberInvokedEvent, value);
        remove => RemoveHandler(MemberInvokedEvent, value);
    }

    /// <summary>The departments of the people, in the order they first appear.</summary>
    public IReadOnlyList<string> Departments =>
        EffectiveMembers.Select(m => m.Department).Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => d!).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>The people the filter currently shows, in order.</summary>
    public IReadOnlyList<TeamShowcaseMember> VisibleMembers => EffectiveMembers.Where(Matches).ToList();

    internal IReadOnlyList<TeamShowcaseCard> Cards => _cards;

    internal SegmentedControl? Filter => _filter;

    internal TeamShowcaseGrid? Grid => _grid;

    internal SiteAnnouncer? Announcer => _announcer;

    /// <summary>Moves keyboard focus to the first person shown.</summary>
    public void FocusFirstMember()
        => _cards.FirstOrDefault(c => c.IsVisible && !c.IsLeaving)?.Focus(NavigationMethod.Tab);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_filter is not null)
        {
            _filter.SelectionChanged -= OnFilterChanged;
        }
        _grid?.Children.Clear();
        _filter = e.NameScope.Find<SegmentedControl>(FilterPart);
        _grid = e.NameScope.Find<TeamShowcaseGrid>(GridPart);
        _announcer = e.NameScope.Find<SiteAnnouncer>(AnnouncerPart);
        if (_filter is not null)
        {
            _filter.SelectionChanged += OnFilterChanged;
        }
        RebuildCards();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedDepartmentProperty)
        {
            ApplyFilter(animate: true, announce: false);
            SyncFilterSelection();
        }
        else if (change.Property == ShowsSampleTeamProperty)
        {
            RebuildCards();
        }
        else if (change.Property == AllLabelProperty || change.Property == IsFilterVisibleProperty || change.Property == FilterLabelProperty)
        {
            RebuildFilter();
        }
        else if (change.Property == BoundsProperty || change.Property == CompactWidthProperty || change.Property == NarrowWidthProperty)
        {
            var width = Bounds.Width;
            PseudoClasses.Set(CompactPseudoClass, width > 0 && width < CompactWidth);
            PseudoClasses.Set(NarrowPseudoClass, width > 0 && width < NarrowWidth);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || _grid is null || e.Source is not TeamShowcaseCard current)
        {
            return;
        }
        var placed = _grid.Placed().ToList();
        var index = placed.IndexOf(current);
        if (index < 0)
        {
            return;
        }
        var columns = Math.Max(1, _grid.Columns);
        var target = e.Key switch
        {
            Key.Right => index + 1,
            Key.Left => index - 1,
            Key.Down => index + columns,
            Key.Up => index - columns,
            Key.Home => 0,
            Key.End => placed.Count - 1,
            _ => -2
        };
        if (target == -2)
        {
            return;
        }
        e.Handled = true;
        if (target >= 0 && target < placed.Count && target != index)
        {
            placed[target].Focus(NavigationMethod.Directional);
            placed[target].BringIntoView();
        }
    }

    private void OnMembersChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildCards();

    private bool Matches(TeamShowcaseMember member)
        => SelectedDepartment is not { } department || string.Equals(member.Department, department, StringComparison.Ordinal);

    private void RebuildCards()
    {
        foreach (var card in _cards)
        {
            card.Invoked -= OnCardInvoked;
        }
        _cards.Clear();
        if (_grid is null)
        {
            return;
        }
        _grid.Children.Clear();
        foreach (var member in EffectiveMembers)
        {
            var card = new TeamShowcaseCard(member) { IsVisible = Matches(member) };
            card.Invoked += OnCardInvoked;
            _cards.Add(card);
            _grid.Children.Add(card);
        }
        RebuildFilter();
        UpdateEmpty();
    }

    private void RebuildFilter()
    {
        if (_filter is null)
        {
            return;
        }
        var departments = Departments;
        var shown = IsFilterVisible && departments.Count >= 2;
        PseudoClasses.Set(NoFilterPseudoClass, !shown);
        AutomationProperties.SetName(_filter, FilterLabel);
        _syncingFilter = true;
        try
        {
            var options = new List<SegmentedOption> { new() { Value = string.Empty, Label = AllLabel } };
            options.AddRange(departments.Select(d => new SegmentedOption { Value = d, Label = d }));
            _filter.ItemsSource = options;
            _filter.SelectedIndex = SelectedDepartment is { } selected ? Math.Max(0, departments.ToList().IndexOf(selected) + 1) : 0;
        }
        finally
        {
            _syncingFilter = false;
        }
    }

    private void SyncFilterSelection()
    {
        if (_filter?.ItemsSource is not List<SegmentedOption> options)
        {
            return;
        }
        var index = SelectedDepartment is { } selected ? options.FindIndex(o => Equals(o.Value, selected)) : 0;
        if (index >= 0 && _filter.SelectedIndex != index)
        {
            _syncingFilter = true;
            _filter.SelectedIndex = index;
            _syncingFilter = false;
        }
    }

    private void OnFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingFilter || _filter is null)
        {
            return;
        }
        var value = _filter.SelectedValue as string;
        var department = string.IsNullOrEmpty(value) ? null : value;
        if (string.Equals(department, SelectedDepartment, StringComparison.Ordinal))
        {
            return;
        }
        SetCurrentValue(SelectedDepartmentProperty, department);
        Announce();
    }

    private void ApplyFilter(bool animate, bool announce)
    {
        if (_grid is null)
        {
            return;
        }
        var entering = 0;
        var moved = false;
        foreach (var card in _cards)
        {
            var show = Matches(card.Member);
            var shown = card.IsVisible && !card.IsLeaving;
            if (show && !shown)
            {
                card.Enter(TimeSpan.FromMilliseconds(Math.Min(entering, 6) * ArcMotionPresets.StaggerItem.TotalMilliseconds + 60));
                entering++;
                moved = true;
            }
            else if (!show && shown)
            {
                if (card.IsKeyboardFocusWithin)
                {
                    // Focus must not stay on a card that is going away: hand it to the filter.
                    _filter?.Focus(NavigationMethod.Tab);
                }
                card.Leave();
                moved = true;
            }
        }
        if (moved && animate)
        {
            _grid.AnimateNextArrange();
        }
        _grid.InvalidateMeasure();
        UpdateEmpty();
        if (announce)
        {
            Announce();
        }
    }

    private void Announce()
    {
        var count = EffectiveMembers.Count(Matches);
        var text = SelectedDepartment is { } department
            ? string.Format(CultureInfo.CurrentCulture, DepartmentResultFormat, count, department)
            : string.Format(CultureInfo.CurrentCulture, AllResultFormat, count);
        _announcer?.Announce(text);
    }

    private void UpdateEmpty() => PseudoClasses.Set(EmptyPseudoClass, !EffectiveMembers.Any(Matches));

    private void OnCardInvoked(object? sender, EventArgs e)
    {
        if (sender is TeamShowcaseCard card)
        {
            foreach (var other in _cards)
            {
                if (!ReferenceEquals(other, card) && other.IsPinned)
                {
                    other.SetPinned(false);
                }
            }
            RaiseEvent(new TeamShowcaseMemberEventArgs(MemberInvokedEvent, card.Member));
        }
    }
}
