using TKCode.Arc.Primitives;
using TKCode.Arc.DesignTokens;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

[TemplatePart(BodyPart, typeof(Decorator))]
[TemplatePart(AnnouncerPart, typeof(LeadScoringAnnouncer))]
[PseudoClasses(FramedPseudoClass, CompactPseudoClass)]
public partial class LeadScoring
{
    public const string BodyPart = "PART_Body";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while <see cref="IsFramed"/> is true.</summary>
    public const string FramedPseudoClass = ":framed";

    /// <summary>Set while the block is at most <c>CompactWidth</c> (860) wide: the detail panel moves under the table.</summary>
    public const string CompactPseudoClass = ":compact";

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<LeadScoring, double>(nameof(CompactWidth), 860);

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    private LeadScoringLead[]? _sampleLeads;
    private LeadScoringStage[]? _sampleStages;
    private List<LeadScoringLead> _visible = new();
    private readonly List<LeadScoringLead> _selected = new();
    private bool _compact;
    private bool _syncing;

    private Decorator? _body;
    private LeadScoringAnnouncer? _announcer;
    private TextBlock? _title;
    private TextBlock? _count;
    private SegmentedControl? _stageTabs;
    private SegmentedControl? _bandSwitch;
    private SearchField? _search;
    private Table? _table;
    private LeadScoringBulkBar? _bulk;
    private TextBlock? _bulkCount;
    private Select? _bulkStage;
    private Button? _bulkMove;
    private Grid? _columns;
    private Control? _tablePane;
    private Border? _detail;

    static LeadScoring()
    {
        FocusableProperty.OverrideDefaultValue<LeadScoring>(false);
    }

    public LeadScoring()
    {
        PseudoClasses.Set(FramedPseudoClass, true);
        Leads.CollectionChanged += OnDataChanged;
        Stages.CollectionChanged += OnStagesChanged;
    }

    /// <summary>True while the block shows its built-in invented leads (no <see cref="Leads"/> were given).</summary>
    public bool IsShowingSamples => Leads.Count == 0;

    /// <summary>The leads in use (yours, or the samples).</summary>
    public IReadOnlyList<LeadScoringLead> EffectiveLeads => IsShowingSamples ? _sampleLeads ??= LeadScoringSamples.Leads() : Leads;

    /// <summary>The stages in use (yours, or New, Working, Qualified and Nurture).</summary>
    public IReadOnlyList<LeadScoringStage> EffectiveStages => Stages.Count > 0 ? Stages : _sampleStages ??= LeadScoringSamples.Stages();

    /// <summary>The leads the filters keep, highest score first.</summary>
    public IReadOnlyList<LeadScoringLead> VisibleLeads => _visible;

    /// <summary>The rows selected in the table.</summary>
    public IReadOnlyList<LeadScoringLead> SelectedLeads => _selected;

    internal Table? Table => _table;

    internal LeadScoringAnnouncer? Announcer => _announcer;

    internal LeadScoringBulkBar? BulkBar => _bulk;

    internal Select? BulkStage => _bulkStage;

    internal Button? BulkMove => _bulkMove;

    internal Border? Detail => _detail;

    internal SegmentedControl? StageTabs => _stageTabs;

    internal bool IsCompact => _compact;

    /// <summary>The band of a score under the current thresholds.</summary>
    public LeadScoringBand BandOf(int score) => score >= HotThreshold ? LeadScoringBand.Hot : score >= WarmThreshold ? LeadScoringBand.Warm : LeadScoringBand.Cold;

    /// <summary>
    /// Moves <paramref name="leads"/> to the stage with key <paramref name="stageKey"/>, raises <see cref="LeadsMoved"/>
    /// with the leads that were not in it yet and announces the move. Returns how many moved.
    /// </summary>
    public int MoveLeads(IEnumerable<LeadScoringLead> leads, string stageKey)
    {
        if (EffectiveStages.FirstOrDefault(s => s.Key == stageKey) is not { } stage)
        {
            return 0;
        }
        var moved = new List<LeadScoringLead>();
        var from = new List<string>();
        foreach (var lead in leads.ToArray())
        {
            if (lead.StageKey == stageKey)
            {
                continue;
            }
            from.Add(lead.StageKey);
            lead.StageKey = stageKey;
            moved.Add(lead);
        }
        if (moved.Count == 0)
        {
            return 0;
        }
        var labels = Labels;
        _announcer?.Announce(string.Format(CultureInfo.CurrentCulture, labels.MovedFormat,
            moved.Count == 1 ? moved[0].Name : moved.Count + " " + labels.LeadOther, stage.Title));
        RaiseEvent(new LeadScoringLeadsMovedEventArgs(LeadsMovedEvent, moved, from, stage));
        Refresh();
        return moved.Count;
    }

    /// <summary>Re-reads the leads: call it after changing a lead in place (the collections are followed on their own).</summary>
    public void Refresh()
    {
        UpdateStageTabs();
        ApplyFilters();
        UpdateDetail();
    }

    private void OnDataChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ActiveLead is { } active && !EffectiveLeads.Contains(active))
        {
            SetCurrentValue(ActiveLeadProperty, null);
        }
        Refresh();
    }

    private void OnStagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_body is not null)
        {
            _body.Child = BuildBody();
        }
        Refresh();
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
        _announcer = e.NameScope.Find<LeadScoringAnnouncer>(AnnouncerPart);
        if (_body is not null)
        {
            _body.Child = BuildBody();
            Refresh();
        }
    }

    private Control BuildBody()
    {
        var labels = Labels;
        var root = new StackPanel { Spacing = 16 };

        _title = LeadScoringUi.Text(null, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeLG, FontWeight.Medium, 24);
        ArcTokens.Bind(_title, TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        AutomationProperties.SetHeadingLevel(_title, 2);
        _count = LeadScoringUi.Tabular(LeadScoringUi.Text(null, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS, lineHeight: 16.8));
        var titles = new StackPanel { Spacing = 2 };
        titles.Children.Add(_title);
        titles.Children.Add(_count);
        root.Children.Add(titles);

        _stageTabs = new SegmentedControl { HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetName(_stageTabs, labels.StagesName);
        _stageTabs.SelectionChanged += (_, _) =>
        {
            if (_syncing || _stageTabs.SelectedIndex < 0)
            {
                return;
            }
            var index = _stageTabs.SelectedIndex;
            SetCurrentValue(StageFilterProperty, index == 0 ? null : EffectiveStages[index - 1].Key);
        };
        _bandSwitch = new SegmentedControl
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            ItemsSource = Enum.GetValues<LeadScoringBand>().Select((b, i) => new SegmentedOption(b, i < labels.Bands.Count ? labels.Bands[i] : b.ToString())).ToArray(),
            SelectedIndex = (int)Band,
        };
        AutomationProperties.SetName(_bandSwitch, labels.BandName);
        _bandSwitch.SelectionChanged += (_, _) =>
        {
            if (!_syncing && _bandSwitch.SelectedIndex >= 0)
            {
                SetCurrentValue(BandProperty, (LeadScoringBand)_bandSwitch.SelectedIndex);
            }
        };
        _search = new SearchField { Placeholder = labels.Search, Size = ControlSize.Small, Width = 220, Text = SearchText ?? string.Empty };
        AutomationProperties.SetName(_search, labels.Search);
        _search.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty && !_syncing)
            {
                SetCurrentValue(SearchTextProperty, string.IsNullOrEmpty(_search.Text) ? null : _search.Text);
            }
        };
        var filters = new WrapPanel { ItemSpacing = 12, LineSpacing = 10 };
        filters.Children.Add(_stageTabs);
        filters.Children.Add(_bandSwitch);
        filters.Children.Add(_search);
        root.Children.Add(filters);

        _tablePane = BuildTable(labels);
        _detail = new Border { Padding = new Thickness(16) }.BindCornerRadius(ArcDesignTokenKind.RadiusStep16);
        ArcTokens.Bind(_detail, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        _columns = new Grid();
        _columns.Children.Add(_tablePane);
        _columns.Children.Add(_detail);
        root.Children.Add(_columns);
        ApplyColumns();
        return root;
    }

    private Control BuildTable(LeadScoringLabels labels)
    {
        _table = new Table
        {
            IsSelectable = true,
            Caption = labels.TableCaption,
            EmptyMessage = labels.Empty,
            ItemNameOne = labels.LeadOne,
            ItemNameOther = labels.LeadOther,
            RowKeySelector = o => ((LeadScoringLead)o).Key,
            // The table's two-line rows need room for the lead and its score; it never gets narrower than this.
            MinWidth = 360,
        };
        _table.Bind(MaxHeightProperty, _table.GetResourceObservable(LeadScoringTokens.Key(LeadScoringTokenKind.TableHeight)));
        _table.Columns.Add(new DataTableColumn(labels.LeadColumn, o => ((LeadScoringLead)o).Name)
        {
            Key = "lead",
            Width = new GridLength(2, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<LeadScoringLead>((lead, _) => LeadCell(lead)),
        });
        _table.Columns.Add(new DataTableColumn(labels.ScoreColumn, o => ((LeadScoringLead)o).Score)
        {
            Key = "score",
            Width = new GridLength(110),
            IsNumeric = true,
            CellTemplate = new FuncDataTemplate<LeadScoringLead>((lead, _) => ScoreCell(lead)),
        });
        _table.Columns.Add(new DataTableColumn(labels.StageColumn, o => StageTitle(((LeadScoringLead)o).StageKey)) { Key = "stage", Width = new GridLength(1, GridUnitType.Star) });
        _table.Columns.Add(new DataTableColumn(labels.SignalColumn, o => ((LeadScoringLead)o).TopFactor?.Label) { Key = "signal", Width = new GridLength(1.4, GridUnitType.Star) });
        _table.Columns.Add(new DataTableColumn(labels.ActivityColumn, o => ((LeadScoringLead)o).LastActivity) { Key = "activity", Width = new GridLength(1, GridUnitType.Star), IsSortable = false });
        _table.SelectionChanged += OnTableSelectionChanged;
        _table.AddHandler(GotFocusEvent, (_, e) =>
        {
            if ((e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(c => c.DataContext is LeadScoringLead)?.DataContext is LeadScoringLead lead)
            {
                SetCurrentValue(ActiveLeadProperty, lead);
            }
        });

        _bulkCount = LeadScoringUi.Tabular(LeadScoringUi.Text(null, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, FontWeight.Medium));
        _bulkCount.VerticalAlignment = VerticalAlignment.Center;
        _bulkStage = new Select
        {
            Placeholder = labels.MoveTo,
            Size = ControlSize.Small,
            Width = 170,
            ItemsSource = EffectiveStages,
            TextSelector = o => (o as LeadScoringStage)?.Title,
        };
        AutomationProperties.SetName(_bulkStage, labels.MoveTo);
        _bulkMove = new Button { Variant = ButtonVariant.Primary, Size = ControlSize.Small, Content = labels.Move };
        _bulkMove.Click += (_, _) =>
        {
            if (_bulkStage.SelectedItem is LeadScoringStage stage)
            {
                var leads = _selected.ToArray();
                MoveLeads(leads, stage.Key);
                _table.ClearSelection();
            }
        };
        var bulkRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 8 };
        bulkRow.Children.Add(_bulkCount);
        Grid.SetColumn(_bulkStage, 1);
        bulkRow.Children.Add(_bulkStage);
        Grid.SetColumn(_bulkMove, 2);
        bulkRow.Children.Add(_bulkMove);
        _bulk = new LeadScoringBulkBar { Content = bulkRow };
        _bulk.IsShown = false;

        var pane = new StackPanel { Spacing = 10 };
        pane.Children.Add(_bulk);
        pane.Children.Add(_table);
        return pane;
    }

    private Control LeadCell(LeadScoringLead? lead)
    {
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        if (lead is null)
        {
            return stack;
        }
        stack.Children.Add(LeadScoringUi.Text(lead.Name, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, FontWeight.Medium));
        var sub = string.Join(" · ", new[] { lead.JobTitle, lead.Company }.Where(s => !string.IsNullOrEmpty(s)));
        if (sub.Length > 0)
        {
            stack.Children.Add(LeadScoringUi.Text(sub, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS, lineHeight: 16.8));
        }
        return stack;
    }

    private Control ScoreCell(LeadScoringLead? lead)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        if (lead is null)
        {
            return row;
        }
        var number = LeadScoringUi.Tabular(LeadScoringUi.Text(lead.Score.ToString(CultureInfo.CurrentCulture), ArcDesignTokenKind.Foreground,
            ArcDesignTokenKind.TypeSM, FontWeight.Medium));
        number.MinWidth = 24;
        number.TextAlignment = TextAlignment.Right;
        row.Children.Add(new LeadScoringBar { Width = 48, Fraction = lead.Score / 100.0, Band = (int)BandOf(lead.Score) - 1 });
        row.Children.Add(number);
        return row;
    }

    private void ApplyColumns()
    {
        if (_columns is null || _tablePane is null || _detail is null)
        {
            return;
        }
        _columns.ColumnDefinitions.Clear();
        _columns.RowDefinitions.Clear();
        if (_compact)
        {
            _columns.RowDefinitions = new RowDefinitions("Auto,Auto");
            _columns.RowSpacing = 16;
            _columns.ColumnSpacing = 0;
            Grid.SetColumn(_detail, 0);
            Grid.SetRow(_detail, 1);
        }
        else
        {
            _columns.ColumnDefinitions = new ColumnDefinitions("*,320");
            _columns.ColumnSpacing = 20;
            _columns.RowSpacing = 0;
            Grid.SetColumn(_detail, 1);
            Grid.SetRow(_detail, 0);
        }
    }

    #endregion

    #region Filters and the table

    private string StageTitle(string key) => EffectiveStages.FirstOrDefault(s => s.Key == key)?.Title ?? key;

    private void UpdateStageTabs()
    {
        if (_stageTabs is null || _title is null)
        {
            return;
        }
        var labels = Labels;
        _title.Text = Title ?? (IsShowingSamples ? LeadScoringSamples.Title : null);
        _title.IsVisible = !string.IsNullOrEmpty(_title.Text);
        AutomationProperties.SetName(this, _title.Text);
        var leads = EffectiveLeads;
        var options = new List<SegmentedOption> { new(null, labels.AllStages + " " + leads.Count.ToString(CultureInfo.CurrentCulture)) };
        foreach (var stage in EffectiveStages)
        {
            options.Add(new SegmentedOption(stage.Key, stage.Title + " " + leads.Count(l => l.StageKey == stage.Key).ToString(CultureInfo.CurrentCulture)));
        }
        _syncing = true;
        _stageTabs.ItemsSource = options;
        var index = StageFilter is { } key ? EffectiveStages.ToList().FindIndex(s => s.Key == key) + 1 : 0;
        _stageTabs.SelectedIndex = Math.Max(0, index);
        _syncing = false;
    }

    private void ApplyFilters()
    {
        var query = SearchText?.Trim();
        var stage = StageFilter;
        var band = Band;
        _visible = EffectiveLeads
            .Where(l => stage is null || l.StageKey == stage)
            .Where(l => band == LeadScoringBand.All || BandOf(l.Score) == band)
            .Where(l => string.IsNullOrEmpty(query)
                        || l.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                        || (l.Company?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false)
                        || (l.JobTitle?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false))
            .OrderByDescending(l => l.Score)
            .ToList();
        if (_table is not null)
        {
            _table.ItemsSource = _visible;
        }
        _selected.RemoveAll(l => !_visible.Contains(l));
        UpdateBulk();
        if (_count is not null)
        {
            _count.Text = string.Format(CultureInfo.CurrentCulture, Labels.CountFormat, _visible.Count, EffectiveLeads.Count);
        }
    }

    private void OnTableSelectionChanged(object? sender, DataTableSelectionChangedEventArgs e)
    {
        _selected.Clear();
        _selected.AddRange(e.SelectedItems.OfType<LeadScoringLead>());
        if (e.AddedItems.OfType<LeadScoringLead>().LastOrDefault() is { } added)
        {
            SetCurrentValue(ActiveLeadProperty, added);
        }
        UpdateBulk();
    }

    private void UpdateBulk()
    {
        if (_bulk is null || _bulkCount is null)
        {
            return;
        }
        _bulk.IsShown = _selected.Count > 0;
        _bulkCount.Text = string.Format(CultureInfo.CurrentCulture, Labels.SelectedFormat, _selected.Count);
    }

    #endregion

    #region Detail

    private void UpdateDetail()
    {
        if (_detail is null)
        {
            return;
        }
        var labels = Labels;
        var lead = ActiveLead ?? _visible.FirstOrDefault();
        if (lead is null)
        {
            _detail.Child = LeadScoringUi.Text(labels.NoLead, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeSM, wrap: true);
            AutomationProperties.SetName(_detail, labels.NoLead);
            return;
        }
        var stack = new StackPanel { Spacing = 16 };

        // Who, with the score ring.
        var ring = new ArcRing
        {
            Width = 64,
            Height = 64,
            Thickness = 6,
            IsRoundCap = true,
            Value = lead.Score / 100.0,
            ChaseRate = 0.18,
        };
        ArcTokens.Bind(ring, ArcRing.TrackBrushProperty, ArcDesignTokenKind.Border);
        var band = BandOf(lead.Score);
        ArcTokens.Bind(ring, ArcRing.ForegroundProperty, band switch
        {
            LeadScoringBand.Hot => ArcDesignTokenKind.Success,
            LeadScoringBand.Warm => ArcDesignTokenKind.Warning,
            _ => ArcDesignTokenKind.TextMuted,
        });
        var scoreText = LeadScoringUi.Tabular(LeadScoringUi.Text(lead.Score.ToString(CultureInfo.CurrentCulture), ArcDesignTokenKind.Foreground,
            ArcDesignTokenKind.TypeLG, FontWeight.Medium, 22));
        scoreText.HorizontalAlignment = HorizontalAlignment.Center;
        scoreText.VerticalAlignment = VerticalAlignment.Center;
        var ringHost = new Panel { Width = 64, Height = 64 };
        ringHost.Children.Add(ring);
        ringHost.Children.Add(scoreText);
        var who = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var name = LeadScoringUi.Text(lead.Name, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeBase, FontWeight.Medium, 22);
        AutomationProperties.SetHeadingLevel(name, 3);
        who.Children.Add(name);
        var sub = string.Join(" · ", new[] { lead.JobTitle, lead.Company }.Where(s => !string.IsNullOrEmpty(s)));
        who.Children.Add(LeadScoringUi.Text(sub, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS, lineHeight: 16.8, wrap: true));
        who.Children.Add(new Badge
        {
            Content = labels.Bands[(int)band],
            Size = BadgeSize.Small,
            Tone = band switch { LeadScoringBand.Hot => BadgeTone.Success, LeadScoringBand.Warm => BadgeTone.Warning, _ => BadgeTone.Neutral },
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0),
        });
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 14 };
        head.Children.Add(ringHost);
        Grid.SetColumn(who, 1);
        head.Children.Add(who);
        AutomationProperties.SetName(ringHost, labels.ScoreColumn + " " + lead.Score.ToString(CultureInfo.CurrentCulture));
        stack.Children.Add(head);

        // Why: the factors grouped by kind, each with its points on a signed bar.
        var why = new StackPanel { Spacing = 10 };
        why.Children.Add(LeadScoringUi.Heading(labels.WhyHeading));
        var maxAbs = Math.Max(1, lead.Factors.Select(f => Math.Abs(f.Points)).DefaultIfEmpty(1).Max());
        foreach (var group in lead.Factors.GroupBy(f => f.Kind).OrderBy(g => g.Key))
        {
            var kind = (int)group.Key < labels.Kinds.Count ? labels.Kinds[(int)group.Key] : group.Key.ToString();
            var subtotal = group.Sum(f => f.Points);
            var groupHead = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            groupHead.Children.Add(LeadScoringUi.Text(kind, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeXS, FontWeight.Medium, 16.8));
            var total = LeadScoringUi.Tabular(LeadScoringUi.Text(Signed(subtotal), ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, FontWeight.Medium, 16.8));
            Grid.SetColumn(total, 1);
            groupHead.Children.Add(total);
            why.Children.Add(groupHead);
            foreach (var factor in group.OrderByDescending(f => f.Points))
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,64,36"), ColumnSpacing = 8 };
                row.Children.Add(LeadScoringUi.Text(factor.Label, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, lineHeight: 16.8));
                var bar = new LeadScoringBar { IsSigned = true, Fraction = factor.Points / (double)maxAbs };
                Grid.SetColumn(bar, 1);
                row.Children.Add(bar);
                var points = LeadScoringUi.Tabular(LeadScoringUi.Text(Signed(factor.Points),
                    factor.Points >= 0 ? ArcDesignTokenKind.Success : ArcDesignTokenKind.Danger, ArcDesignTokenKind.TypeXS, FontWeight.Medium, 16.8));
                points.TextAlignment = TextAlignment.Right;
                Grid.SetColumn(points, 2);
                row.Children.Add(points);
                AutomationProperties.SetName(row, kind + ": " + factor.Label + ", " + Signed(factor.Points));
                why.Children.Add(row);
            }
        }
        stack.Children.Add(why);

        // Stage: a switch over the stages (choosing one moves the lead), and a button to the next stage.
        var stages = EffectiveStages;
        var stagePanel = new StackPanel { Spacing = 8 };
        stagePanel.Children.Add(LeadScoringUi.Heading(labels.StageHeading));
        var stageSwitch = new SegmentedControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = stages.Select(s => new SegmentedOption(s.Key, s.Title)).ToArray(),
            SelectedIndex = stages.ToList().FindIndex(s => s.Key == lead.StageKey),
        };
        AutomationProperties.SetName(stageSwitch, labels.StageHeading);
        stageSwitch.SelectionChanged += (_, _) =>
        {
            if (stageSwitch.SelectedIndex >= 0 && stages[stageSwitch.SelectedIndex].Key != lead.StageKey)
            {
                MoveLeads([lead], stages[stageSwitch.SelectedIndex].Key);
            }
        };
        stagePanel.Children.Add(stageSwitch);
        var at = stages.ToList().FindIndex(s => s.Key == lead.StageKey);
        if (at >= 0 && at + 1 < stages.Count)
        {
            var next = stages[at + 1];
            var advance = new Button
            {
                Variant = ButtonVariant.Secondary,
                Size = ControlSize.Small,
                Content = string.Format(CultureInfo.CurrentCulture, labels.AdvanceFormat, next.Title),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            advance.Click += (_, _) => MoveLeads([lead], next.Key);
            stagePanel.Children.Add(advance);
        }
        stack.Children.Add(stagePanel);
        _detail.Child = stack;
        AutomationProperties.SetName(_detail, lead.Name);
    }

    private static string Signed(int points) => points > 0 ? "+" + points.ToString(CultureInfo.CurrentCulture) : points < 0 ? "−" + (-points).ToString(CultureInfo.CurrentCulture) : "0";

    #endregion

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StageFilterProperty || change.Property == BandProperty || change.Property == SearchTextProperty
            || change.Property == HotThresholdProperty || change.Property == WarmThresholdProperty)
        {
            if (_bandSwitch is not null && _bandSwitch.SelectedIndex != (int)Band)
            {
                _syncing = true;
                _bandSwitch.SelectedIndex = (int)Band;
                _syncing = false;
            }
            if (_search is not null && _search.Text != (SearchText ?? string.Empty))
            {
                _syncing = true;
                _search.Text = SearchText ?? string.Empty;
                _syncing = false;
            }
            UpdateStageTabs();
            ApplyFilters();
            UpdateDetail();
        }
        else if (change.Property == ActiveLeadProperty)
        {
            UpdateDetail();
        }
        else if (change.Property == TitleProperty)
        {
            UpdateStageTabs();
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
        ApplyColumns();
    }
}
