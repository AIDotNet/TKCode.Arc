using TKCode.Arc.DesignTokens;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace TKCode.Arc;

[TemplatePart(BodyPart, typeof(Decorator))]
[TemplatePart(AnnouncerPart, typeof(ProjectsAnnouncer))]
[PseudoClasses(FramedPseudoClass, CompactPseudoClass)]
public partial class GoalTracker
{
    public const string BodyPart = "PART_Body";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while <see cref="IsFramed"/> is true.</summary>
    public const string FramedPseudoClass = ":framed";

    /// <summary>Set while the block is at most <c>CompactWidth</c> (640) wide: key result rows stack their parts.</summary>
    public const string CompactPseudoClass = ":compact";

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<GoalTracker, double>(nameof(CompactWidth), 640);

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    private GoalTrackerGoal[]? _samples;
    private bool _compact;
    private bool _syncing;
    private GoalTrackerKeyResult? _editing;

    private Decorator? _body;
    private ProjectsAnnouncer? _announcer;
    private TextBlock? _title;
    private TextBlock? _period;
    private GoalTrackerBar? _overallBar;
    private TextBlock? _overallText;
    private WrapPanel? _counts;
    private SegmentedControl? _viewSwitch;
    private Select? _teamSelect;
    private StackPanel? _list;
    private GoalTrackerTimeline? _timeline;
    private TextBlock? _empty;

    static GoalTracker()
    {
        FocusableProperty.OverrideDefaultValue<GoalTracker>(false);
    }

    public GoalTracker()
    {
        PseudoClasses.Set(FramedPseudoClass, true);
        Goals.CollectionChanged += (_, _) => Refresh();
    }

    /// <summary>True while the block shows its built-in invented goals (no <see cref="Goals"/> were given).</summary>
    public bool IsShowingSamples => Goals.Count == 0;

    /// <summary>The goals in use (yours, or the samples).</summary>
    public IReadOnlyList<GoalTrackerGoal> EffectiveGoals => IsShowingSamples ? _samples ??= GoalTrackerSamples.Goals(EffectiveStart, EffectiveEnd) : Goals;

    /// <summary>The goals the team filter keeps.</summary>
    public IReadOnlyList<GoalTrackerGoal> VisibleGoals => EffectiveGoals.Where(g => TeamFilter is null || g.Team == TeamFilter).ToList();

    /// <summary>The first day of the period in use.</summary>
    public DateTime EffectiveStart => (PeriodStart ?? QuarterStart(EffectiveToday)).Date;

    /// <summary>The last day of the period in use.</summary>
    public DateTime EffectiveEnd => (PeriodEnd ?? QuarterStart(EffectiveToday).AddMonths(3).AddDays(-1)).Date;

    private DateTime EffectiveToday => (Today ?? DateTime.Today).Date;

    /// <summary>How much of the period has gone by today, from 0 to 1: where every key result is expected to be.</summary>
    public double ExpectedProgress
    {
        get
        {
            var span = (EffectiveEnd - EffectiveStart).TotalDays;
            return span <= 0 ? 1 : Math.Clamp((EffectiveToday - EffectiveStart).TotalDays / span, 0, 1);
        }
    }

    internal StackPanel? List => _list;

    internal GoalTrackerTimeline? TimelineView => _timeline;

    internal ProjectsAnnouncer? Announcer => _announcer;

    internal SegmentedControl? ViewSwitch => _viewSwitch;

    internal Select? TeamSelect => _teamSelect;

    internal GoalTrackerKeyResult? Editing => _editing;

    internal bool IsCompact => _compact;

    private static DateTime QuarterStart(DateTime day) => new(day.Year, (day.Month - 1) / 3 * 3 + 1, 1);

    /// <summary>
    /// Records a check-in on <paramref name="keyResult"/> dated today: sets its current value and confidence, adds the
    /// check-in, refreshes the view and raises <see cref="CheckedIn"/>. Returns the new check-in.
    /// </summary>
    public GoalTrackerCheckIn CheckIn(GoalTrackerKeyResult keyResult, double value, GoalTrackerConfidence confidence, string? note = null)
    {
        var goal = EffectiveGoals.FirstOrDefault(g => g.KeyResults.Contains(keyResult))
                   ?? throw new ArgumentException("The key result is not one of the block's goals.", nameof(keyResult));
        var checkIn = new GoalTrackerCheckIn(EffectiveToday, value, confidence, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), CurrentUser);
        keyResult.CheckIns.Add(checkIn);
        keyResult.Current = value;
        keyResult.Confidence = confidence;
        _editing = null;
        Refresh();
        var labels = Labels;
        _announcer?.Announce(string.Format(CultureInfo.CurrentCulture, labels.CheckedInFormat, keyResult.Title,
            keyResult.Progress.ToString("P0", CultureInfo.CurrentCulture), labels.Confidences[(int)confidence]));
        RaiseEvent(new GoalTrackerCheckedInEventArgs(CheckedInEvent, goal, keyResult, checkIn));
        return checkIn;
    }

    /// <summary>Opens the inline check-in of <paramref name="keyResult"/> (null closes it).</summary>
    public void OpenCheckIn(GoalTrackerKeyResult? keyResult)
    {
        _editing = keyResult;
        BuildList();
    }

    /// <summary>Re-reads the goals: call it after changing a goal or a key result in place.</summary>
    public void Refresh()
    {
        if (_body?.Child is null)
        {
            return;
        }
        UpdateHeader();
        BuildList();
        UpdateTimeline();
        ApplyView();
    }

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_body is not null)
        {
            _body.Child = null;
        }
        _body = e.NameScope.Find<Decorator>(BodyPart);
        _announcer = e.NameScope.Find<ProjectsAnnouncer>(AnnouncerPart);
        if (_body is not null)
        {
            _body.Child = BuildBody();
            Refresh();
        }
    }

    private Control BuildBody()
    {
        var labels = Labels;
        var root = new StackPanel { Spacing = 18 };

        _title = GoalTrackerUi.Text(null, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeLG, FontWeight.Medium, 24, wrap: false);
        ArcTokens.Bind(_title, TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        AutomationProperties.SetHeadingLevel(_title, 2);
        _period = GoalTrackerUi.Text(null, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS, lineHeight: 16.8);
        var titles = new StackPanel { Spacing = 2 };
        titles.Children.Add(_title);
        titles.Children.Add(_period);
        root.Children.Add(titles);

        _overallBar = new GoalTrackerBar { Height = 8 };
        _overallText = GoalTrackerUi.Text(null, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, FontWeight.Medium, wrap: false);
        _counts = new WrapPanel { ItemSpacing = 6, LineSpacing = 6 };
        var overallHead = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        overallHead.Children.Add(_overallText);
        Grid.SetColumn(_counts, 1);
        overallHead.Children.Add(_counts);
        var overall = new StackPanel { Spacing = 8 };
        overall.Children.Add(overallHead);
        overall.Children.Add(_overallBar);
        root.Children.Add(overall);

        _viewSwitch = new SegmentedControl
        {
            ItemsSource = Enum.GetValues<GoalTrackerView>().Select((v, i) => new SegmentedOption(v, i < labels.Views.Count ? labels.Views[i] : v.ToString())).ToArray(),
            SelectedIndex = (int)View,
        };
        AutomationProperties.SetName(_viewSwitch, labels.ViewName);
        _viewSwitch.SelectionChanged += (_, _) =>
        {
            if (!_syncing && _viewSwitch.SelectedIndex >= 0)
            {
                SetCurrentValue(ViewProperty, (GoalTrackerView)_viewSwitch.SelectedIndex);
            }
        };
        _teamSelect = new Select { Size = ControlSize.Small, Width = 180, TextSelector = o => o as string ?? labels.AllTeams };
        AutomationProperties.SetName(_teamSelect, labels.TeamName);
        _teamSelect.SelectionChanged += (_, _) =>
        {
            if (!_syncing)
            {
                SetCurrentValue(TeamFilterProperty, _teamSelect.SelectedIndex <= 0 ? null : _teamSelect.SelectedItem as string);
            }
        };
        var toolbar = new WrapPanel { ItemSpacing = 12, LineSpacing = 10 };
        toolbar.Children.Add(_viewSwitch);
        toolbar.Children.Add(_teamSelect);
        root.Children.Add(toolbar);

        _list = new StackPanel { Spacing = 12 };
        _timeline = new GoalTrackerTimeline { TodayLabel = labels.Today };
        _empty = GoalTrackerUi.Text(labels.Empty, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeSM);
        root.Children.Add(_list);
        root.Children.Add(_timeline);
        root.Children.Add(_empty);
        return root;
    }

    #endregion

    #region Views

    private void UpdateHeader()
    {
        var labels = Labels;
        _title!.Text = Title ?? (IsShowingSamples ? GoalTrackerSamples.Title : null);
        _title.IsVisible = !string.IsNullOrEmpty(_title.Text);
        AutomationProperties.SetName(this, _title.Text);
        _period!.Text = string.Format(CultureInfo.CurrentCulture, labels.PeriodFormat,
            EffectiveStart.ToString("MMM d", CultureInfo.CurrentCulture), EffectiveEnd.ToString("MMM d", CultureInfo.CurrentCulture),
            ExpectedProgress.ToString("P0", CultureInfo.CurrentCulture));

        var visible = VisibleGoals;
        var krs = visible.SelectMany(g => g.KeyResults).ToList();
        var progress = krs.Count == 0 ? 0 : krs.Average(k => k.Progress);
        _overallText!.Text = string.Format(CultureInfo.CurrentCulture, labels.OverallFormat, progress.ToString("P0", CultureInfo.CurrentCulture));
        _overallBar!.Confidence = krs.Count == 0 ? GoalTrackerConfidence.OnTrack : krs.Max(k => k.Confidence);
        _overallBar.Expected = ExpectedProgress;
        _overallBar.Label = _overallText.Text;
        _overallBar.Value = progress;
        _counts!.Children.Clear();
        foreach (var confidence in Enum.GetValues<GoalTrackerConfidence>())
        {
            var count = krs.Count(k => k.Confidence == confidence);
            if (count == 0)
            {
                continue;
            }
            var badge = GoalTrackerUi.Confidence(confidence, labels);
            badge.Content = count.ToString(CultureInfo.CurrentCulture) + " " + badge.Content;
            _counts.Children.Add(badge);
        }

        var teams = EffectiveGoals.Select(g => g.Team).Where(t => !string.IsNullOrEmpty(t)).Distinct().ToList();
        _syncing = true;
        _teamSelect!.ItemsSource = new List<string?> { null }.Concat(teams).ToList();
        _teamSelect.SelectedIndex = TeamFilter is null ? 0 : Math.Max(0, teams.IndexOf(TeamFilter) + 1);
        _teamSelect.IsVisible = teams.Count > 1;
        _syncing = false;
    }

    private void ApplyView()
    {
        if (_list is null || _timeline is null || _empty is null)
        {
            return;
        }
        var any = VisibleGoals.Count > 0;
        _list.IsVisible = any && View == GoalTrackerView.List;
        _timeline.IsVisible = any && View == GoalTrackerView.Timeline;
        _empty.IsVisible = !any;
        if (_viewSwitch is not null && _viewSwitch.SelectedIndex != (int)View)
        {
            _syncing = true;
            _viewSwitch.SelectedIndex = (int)View;
            _syncing = false;
        }
    }

    private void UpdateTimeline()
    {
        if (_timeline is null)
        {
            return;
        }
        _timeline.Start = EffectiveStart;
        _timeline.End = EffectiveEnd;
        _timeline.Today = EffectiveToday;
        _timeline.Rows = VisibleGoals.SelectMany(g => g.KeyResults).ToList();
        AutomationProperties.SetName(_timeline, string.Join("; ", _timeline.Rows.Select(k => k.Title + " " + k.Progress.ToString("P0", CultureInfo.CurrentCulture))));
        _timeline.InvalidateVisual();
    }

    private void BuildList()
    {
        if (_list is null)
        {
            return;
        }
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        _list.Children.Clear();
        foreach (var goal in VisibleGoals)
        {
            _list.Children.Add(BuildGoal(goal));
        }
        if (_editing is not null && focused is not null)
        {
            // The editor just opened from the keyboard or a click: put the focus in its value field.
            Avalonia.Threading.Dispatcher.UIThread.Post(() => _list.GetLogicalDescendants().OfType<NumberField>().FirstOrDefault()?.Focus(NavigationMethod.Tab));
        }
    }

    private Control BuildGoal(GoalTrackerGoal goal)
    {
        var labels = Labels;
        var card = new Border { Padding = new Thickness(16), BorderThickness = new Thickness(1) }.BindCornerRadius(ArcDesignTokenKind.RadiusStep16);
        ArcTokens.Bind(card, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        var stack = new StackPanel { Spacing = 12 };

        var title = GoalTrackerUi.Text(goal.Title, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeBase, FontWeight.Medium, 22);
        AutomationProperties.SetHeadingLevel(title, 3);
        var meta = GoalTrackerUi.Text(string.Join(" · ", new[] { goal.Team, goal.Owner?.Name }.Where(s => !string.IsNullOrEmpty(s))),
            ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS, lineHeight: 16.8);
        var titles = new StackPanel { Spacing = 2 };
        titles.Children.Add(title);
        titles.Children.Add(meta);
        var percent = GoalTrackerUi.Text(goal.Progress.ToString("P0", CultureInfo.CurrentCulture), ArcDesignTokenKind.Foreground,
            ArcDesignTokenKind.TypeBase, FontWeight.Medium, 22, wrap: false);
        percent.FontFeatures = new FontFeatureCollection { FontFeature.Parse("tnum") };
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 10 };
        if (goal.Owner is { } owner)
        {
            head.Children.Add(new ProjectsAvatar { Person = owner, Width = 28, Height = 28, IsDecorative = true, VerticalAlignment = VerticalAlignment.Top });
        }
        Grid.SetColumn(titles, 1);
        head.Children.Add(titles);
        var badge = GoalTrackerUi.Confidence(goal.Confidence, labels);
        badge.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(badge, 2);
        head.Children.Add(badge);
        Grid.SetColumn(percent, 3);
        head.Children.Add(percent);
        stack.Children.Add(head);

        foreach (var kr in goal.KeyResults)
        {
            stack.Children.Add(BuildKeyResult(goal, kr));
        }
        card.Child = stack;
        AutomationProperties.SetName(card, goal.Title);
        return card;
    }

    private Control BuildKeyResult(GoalTrackerGoal goal, GoalTrackerKeyResult kr)
    {
        var labels = Labels;
        var row = new StackPanel { Spacing = 6 };
        var unit = kr.Unit is { Length: > 0 } u ? (u == "%" ? u : " " + u) : string.Empty;
        var value = string.Format(CultureInfo.CurrentCulture, labels.ValueFormat, GoalTrackerUi.Number(kr.Current) + unit, GoalTrackerUi.Number(kr.Target) + unit);

        var title = GoalTrackerUi.Text(kr.Title, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM);
        var valueText = GoalTrackerUi.Text(value, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, lineHeight: 16.8, wrap: false);
        valueText.FontFeatures = new FontFeatureCollection { FontFeature.Parse("tnum") };
        var checkInButton = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, Content = labels.CheckIn };
        AutomationProperties.SetName(checkInButton, labels.CheckIn + ": " + kr.Title);
        checkInButton.Click += (_, _) => OpenCheckIn(ReferenceEquals(_editing, kr) ? null : kr);
        var badge = GoalTrackerUi.Confidence(kr.Confidence, labels);
        badge.VerticalAlignment = VerticalAlignment.Center;

        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 10 };
        if (kr.Owner is { } owner)
        {
            top.Children.Add(new ProjectsAvatar { Person = owner, Width = 22, Height = 22, IsDecorative = true, VerticalAlignment = VerticalAlignment.Center });
        }
        Grid.SetColumn(title, 1);
        title.VerticalAlignment = VerticalAlignment.Center;
        top.Children.Add(title);
        Grid.SetColumn(badge, 2);
        top.Children.Add(badge);
        Grid.SetColumn(checkInButton, 3);
        top.Children.Add(checkInButton);
        row.Children.Add(top);

        var bar = new GoalTrackerBar { Confidence = kr.Confidence, Expected = ExpectedProgress, Label = kr.Title + ", " + value, Value = kr.Progress };
        var barRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10 };
        bar.VerticalAlignment = VerticalAlignment.Center;
        barRow.Children.Add(bar);
        Grid.SetColumn(valueText, 1);
        barRow.Children.Add(valueText);
        row.Children.Add(barRow);
        if (_compact)
        {
            title.TextWrapping = TextWrapping.Wrap;
        }

        var last = kr.CheckIns.OrderBy(c => c.Date).LastOrDefault();
        var lastText = last is null
            ? labels.NoCheckIn
            : string.Format(CultureInfo.CurrentCulture, labels.LastCheckInFormat, last.Date.ToString("MMM d", CultureInfo.CurrentCulture),
                last.Note ?? GoalTrackerUi.Number(last.Value) + unit);
        row.Children.Add(GoalTrackerUi.Text(lastText, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS, lineHeight: 16.8));

        if (ReferenceEquals(_editing, kr))
        {
            row.Children.Add(BuildEditor(kr));
        }
        return row;
    }

    private Control BuildEditor(GoalTrackerKeyResult kr)
    {
        var labels = Labels;
        var editor = new Border { Padding = new Thickness(12), Margin = new Thickness(0, 4, 0, 0) }.BindCornerRadius(ArcDesignTokenKind.RadiusStep14);
        ArcTokens.Bind(editor, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        var number = new NumberField
        {
            Label = labels.NewValue,
            Size = ControlSize.Small,
            Value = (decimal)kr.Current,
            Minimum = decimal.MinValue / 2,
            Maximum = decimal.MaxValue / 2,
            Step = kr.Target - kr.Start is var d && Math.Abs(d) < 10 ? 0.1m : 1m,
            MaximumFractionDigits = 2,
        };
        var confidence = new SegmentedControl
        {
            ItemsSource = Enum.GetValues<GoalTrackerConfidence>().Select((c, i) => new SegmentedOption(c, labels.Confidences[i])).ToArray(),
            SelectedIndex = (int)kr.Confidence,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        AutomationProperties.SetName(confidence, labels.Confidence);
        var note = new Input { Label = labels.Note, Size = ControlSize.Small };
        var save = new Button { Variant = ButtonVariant.Primary, Size = ControlSize.Small, Content = labels.Save };
        save.Click += (_, _) => CheckIn(kr, (double)number.Value, (GoalTrackerConfidence)Math.Max(0, confidence.SelectedIndex), note.Text);
        var cancel = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, Content = labels.Cancel };
        cancel.Click += (_, _) => OpenCheckIn(null);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(cancel);
        actions.Children.Add(save);
        var fields = new Grid { ColumnDefinitions = new ColumnDefinitions(_compact ? "*" : "160,*"), ColumnSpacing = 12, RowSpacing = 10 };
        fields.RowDefinitions = new RowDefinitions(_compact ? "Auto,Auto" : "Auto");
        fields.Children.Add(number);
        Grid.SetColumn(note, _compact ? 0 : 1);
        Grid.SetRow(note, _compact ? 1 : 0);
        fields.Children.Add(note);
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(fields);
        var confidenceLabel = GoalTrackerUi.Text(labels.Confidence, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, FontWeight.Medium, 16.8);
        stack.Children.Add(confidenceLabel);
        stack.Children.Add(confidence);
        stack.Children.Add(actions);
        editor.Child = stack;
        editor.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                OpenCheckIn(null);
                e.Handled = true;
            }
        };
        return editor;
    }

    #endregion

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ViewProperty)
        {
            ApplyView();
        }
        else if (change.Property == TeamFilterProperty || change.Property == TitleProperty || change.Property == PeriodStartProperty
                 || change.Property == PeriodEndProperty || change.Property == TodayProperty)
        {
            Refresh();
        }
        else if (change.Property == LabelsProperty)
        {
            if (_body is not null)
            {
                _body.Child = BuildBody();
                Refresh();
            }
        }
        else if (change.Property == IsFramedProperty)
        {
            PseudoClasses.Set(FramedPseudoClass, IsFramed);
        }
        else if (change.Property == CompactWidthProperty)
        {
            UpdateCompact(Bounds.Width);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateCompact(e.NewSize.Width);
    }

    private void UpdateCompact(double width)
    {
        var compact = width > 0 && width <= CompactWidth;
        if (compact == _compact)
        {
            return;
        }
        _compact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
        BuildList();
    }
}
