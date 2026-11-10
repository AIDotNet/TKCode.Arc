using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Interactivity;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class ResearchRun
{
    /// <summary>The status line ("Reading sources · 1m 05s") (template use).</summary>
    internal static readonly DirectProperty<ResearchRun, string?> StatusTextProperty =
        AvaloniaProperty.RegisterDirect<ResearchRun, string?>(nameof(StatusText), o => o.StatusText);

    /// <summary>The sources count ("4 of 7 read") (template use).</summary>
    internal static readonly DirectProperty<ResearchRun, string?> SourcesCountTextProperty =
        AvaloniaProperty.RegisterDirect<ResearchRun, string?>(nameof(SourcesCountText), o => o.SourcesCountText);

    /// <summary>The folded process line (template use).</summary>
    internal static readonly DirectProperty<ResearchRun, string?> SummaryTextProperty =
        AvaloniaProperty.RegisterDirect<ResearchRun, string?>(nameof(SummaryText), o => o.SummaryText);

    private string? _statusText;
    private string? _sourcesCountText;
    private string? _summaryText;

    internal string? StatusText { get => _statusText; private set => SetAndRaise(StatusTextProperty, ref _statusText, value); }

    internal string? SourcesCountText { get => _sourcesCountText; private set => SetAndRaise(SourcesCountTextProperty, ref _sourcesCountText, value); }

    internal string? SummaryText { get => _summaryText; private set => SetAndRaise(SummaryTextProperty, ref _summaryText, value); }

    private readonly Dictionary<ResearchRunSource, ResearchRunSourceRow> _rows = new();
    private Grid? _stages;
    private StackPanel? _plan;
    private StackPanel? _sources;
    private StackPanel? _notes;
    private StackPanel? _report;
    private AiCollapsible? _process;
    private Button? _action;
    private Button? _toggle;
    private AiAnnouncer? _announcer;
    private TextBlock? _planHeading;
    private TextBlock? _sourcesHeading;
    private TextBlock? _notesHeading;
    private TextBlock? _reportHeading;
    private CopyButton? _copy;
    private Control? _summaryLine;
    private Control? _notesSection;
    private Grid? _processGrid;
    private Control? _sourcesColumn;

    internal AiAnnouncer? Announcer => _announcer;

    internal Button? ActionButton => _action;

    internal Button? ToggleButton => _toggle;

    internal StackPanel? ReportHost => _report;

    internal StackPanel? NotesHost => _notes;

    internal StackPanel? PlanHost => _plan;

    internal ResearchRunSourceRow? RowOf(ResearchRunSource source) => _rows.GetValueOrDefault(source);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_action is not null)
        {
            _action.Click -= OnActionClick;
        }
        if (_toggle is not null)
        {
            _toggle.Click -= OnToggleClick;
        }
        _sources?.Children.Clear();
        _rows.Clear();
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _stages = scope.Find<Grid>(StagesPart);
        _plan = scope.Find<StackPanel>(PlanPart);
        _sources = scope.Find<StackPanel>(SourcesPart);
        _notes = scope.Find<StackPanel>(NotesPart);
        _report = scope.Find<StackPanel>(ReportPart);
        _process = scope.Find<AiCollapsible>(ProcessPart);
        _action = scope.Find<Button>(ActionPart);
        _toggle = scope.Find<Button>(ToggleProcessPart);
        _announcer = scope.Find<AiAnnouncer>(AnnouncerPart);
        _planHeading = scope.Find<TextBlock>("PART_PlanHeading");
        _sourcesHeading = scope.Find<TextBlock>("PART_SourcesHeading");
        _notesHeading = scope.Find<TextBlock>("PART_NotesHeading");
        _reportHeading = scope.Find<TextBlock>("PART_ReportHeading");
        _copy = scope.Find<CopyButton>("PART_Copy");
        _summaryLine = scope.Find<Control>("PART_SummaryLine");
        _notesSection = scope.Find<Control>("PART_NotesSection");
        _processGrid = scope.Find<Grid>("PART_ProcessGrid");
        _sourcesColumn = scope.Find<Control>("PART_SourcesColumn");
        LayoutProcess();
        if (_action is not null)
        {
            _action.Click += OnActionClick;
        }
        if (_toggle is not null)
        {
            _toggle.Click += OnToggleClick;
        }
        RebuildAll();
    }

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        if (IsWorking(Phase))
        {
            Stop();
        }
        else if (Phase == ResearchRunPhase.Idle && IsSimulated)
        {
            StartSimulation();
        }
        else
        {
            Restart();
        }
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e) => SetCurrentValue(IsProcessExpandedProperty, !IsProcessExpanded);

    /// <summary>Plan and sources side by side, or stacked while compact (a Grid's columns are not a styled property).</summary>
    private void LayoutProcess()
    {
        if (_processGrid is null || _sourcesColumn is null)
        {
            return;
        }
        _processGrid.ColumnDefinitions.Clear();
        if (IsCompact)
        {
            _processGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            _processGrid.RowSpacing = 20;
            Grid.SetColumn(_sourcesColumn, 0);
            Grid.SetRow(_sourcesColumn, 1);
        }
        else
        {
            _processGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            _processGrid.ColumnDefinitions.Add(new ColumnDefinition(24, GridUnitType.Pixel));
            _processGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            _processGrid.RowSpacing = 0;
            Grid.SetColumn(_sourcesColumn, 2);
            Grid.SetRow(_sourcesColumn, 0);
        }
    }

    private void RebuildAll()
    {
        RebuildStages();
        RebuildPlan();
        RebuildSources();
        RebuildNotes();
        RebuildReport();
        UpdateProcess();
        UpdateTexts();
    }

    private void UpdateTexts()
    {
        var labels = Labels;
        var sources = ShownSources;
        var read = 0;
        foreach (var source in sources)
        {
            if (source.Status == ResearchRunSourceStatus.Read)
            {
                read++;
            }
        }
        var elapsed = labels.Elapsed(Elapsed);
        StatusText = Phase == ResearchRunPhase.Idle ? labels.Phase(Phase) : $"{labels.Phase(Phase)} · {elapsed}";
        SourcesCountText = sources.Count == 0 ? null : labels.SourcesCount(read, sources.Count);
        SummaryText = labels.Summary(read, ShownNotes.Count, elapsed);
        SetText(_planHeading, labels.PlanHeading);
        SetText(_sourcesHeading, labels.SourcesHeading);
        SetText(_notesHeading, labels.NotesHeading);
        SetText(_reportHeading, labels.ReportHeading);
        if (_action is not null)
        {
            var working = IsWorking(Phase);
            _action.Content = working ? labels.Stop : labels.Restart;
            _action.Variant = working ? ButtonVariant.Secondary : ButtonVariant.Primary;
            _action.IsVisible = working || IsSimulated || Phase != ResearchRunPhase.Idle;
        }
        UpdateProcess();
        UpdateStages();
    }

    private static void SetText(TextBlock? block, string text)
    {
        if (block is not null)
        {
            block.Text = text;
        }
    }

    private void UpdateProcess()
    {
        var done = Phase == ResearchRunPhase.Done;
        if (_process is not null)
        {
            _process.IsExpanded = !done || IsProcessExpanded;
        }
        if (_summaryLine is not null)
        {
            _summaryLine.IsVisible = done;
        }
        if (_toggle is not null)
        {
            _toggle.Content = IsProcessExpanded ? Labels.HideProcess : Labels.ShowProcess;
        }
    }

    #region Stages

    private void RebuildStages()
    {
        if (_stages is null)
        {
            return;
        }
        _stages.Children.Clear();
        _stages.ColumnDefinitions.Clear();
        var names = Labels.Stages;
        for (var i = 0; i < 4; i++)
        {
            _stages.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            var stage = new ResearchRunStage { Text = i < names.Count ? names[i] : string.Empty, IsFirst = i == 0 };
            Grid.SetColumn(stage, i);
            _stages.Children.Add(stage);
        }
        UpdateStages();
    }

    /// <summary>The stage the run is in (0 plan, 1 read, 2 write, 3 report); -1 before it starts.</summary>
    internal int CurrentStage => Phase switch
    {
        ResearchRunPhase.Planning => 0,
        ResearchRunPhase.Reading => 1,
        ResearchRunPhase.Writing => 2,
        ResearchRunPhase.Done => 3,
        ResearchRunPhase.Stopped => StoppedStage(),
        _ => -1
    };

    private int StoppedStage() => !string.IsNullOrEmpty(ShownReport) ? 2 : ShownSources.Count > 0 ? 1 : 0;

    private void UpdateStages()
    {
        if (_stages is null)
        {
            return;
        }
        var current = CurrentStage;
        var done = Phase == ResearchRunPhase.Done;
        for (var i = 0; i < _stages.Children.Count; i++)
        {
            if (_stages.Children[i] is ResearchRunStage stage)
            {
                stage.State = i < current || (done && i == current) ? ResearchRunStageState.Done
                    : i == current ? (Phase == ResearchRunPhase.Stopped ? ResearchRunStageState.Stopped : ResearchRunStageState.Current)
                    : ResearchRunStageState.Upcoming;
            }
        }
    }

    #endregion

    #region Plan

    private void RebuildPlan()
    {
        if (_plan is null)
        {
            return;
        }
        _plan.Children.Clear();
        var plan = ShownPlan;
        var done = ShownPlanDone;
        for (var i = 0; i < plan.Count; i++)
        {
            var state = i < done ? ResearchRunStageState.Done
                : i == done && IsWorking(Phase) && Phase != ResearchRunPhase.Planning ? ResearchRunStageState.Current
                : ResearchRunStageState.Upcoming;
            _plan.Children.Add(PlanRow(plan[i], state));
        }
        UpdateStages();
    }

    private static Control PlanRow(string text, ResearchRunStageState state)
    {
        var row = new DockPanel();
        Control mark;
        if (state == ResearchRunStageState.Current)
        {
            var spinner = new ArcSpinner { Width = 13, Height = 13 };
            ArcTokens.Bind(spinner, ArcSpinner.ForegroundProperty, ArcDesignTokenKind.Accent);
            mark = spinner;
        }
        else
        {
            var glyph = new ArcGlyph { Data = state == ResearchRunStageState.Done ? ArcIcons.CircleCheck : ArcIcons.Circle, Size = 14 };
            ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, state == ResearchRunStageState.Done ? ArcDesignTokenKind.Success : ArcDesignTokenKind.TextMuted);
            mark = glyph;
        }
        mark.VerticalAlignment = VerticalAlignment.Top;
        mark.Margin = new Thickness(0, 3, 8, 0);
        DockPanel.SetDock(mark, Dock.Left);
        var label = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, LineHeight = 20 };
        ArcTokens.Bind(label, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(label, TextBlock.ForegroundProperty, state == ResearchRunStageState.Upcoming ? ArcDesignTokenKind.TextSecondary : ArcDesignTokenKind.Foreground);
        row.Children.Add(mark);
        row.Children.Add(label);
        AutomationProperties.SetName(row, text);
        return row;
    }

    #endregion

    #region Sources

    private void RebuildSources()
    {
        if (_sources is null)
        {
            return;
        }
        var sources = ShownSources;
        var keep = new HashSet<ResearchRunSource>(sources);
        foreach (var source in _rows.Keys.ToArray())
        {
            if (!keep.Contains(source))
            {
                _rows.Remove(source);
            }
        }
        _sources.Children.Clear();
        foreach (var source in sources)
        {
            if (!_rows.TryGetValue(source, out var row))
            {
                row = new ResearchRunSourceRow { Owner = this, Source = source };
                _rows[source] = row;
            }
            row.Refresh();
            _sources.Children.Add(row);
        }
    }

    #endregion

    #region Notes and report

    private void RebuildNotes()
    {
        if (_notes is null)
        {
            return;
        }
        _notes.Children.Clear();
        if (_notesSection is not null)
        {
            _notesSection.IsVisible = ShownNotes.Count > 0;
        }
        foreach (var note in ShownNotes)
        {
            var row = new DockPanel();
            var dot = new Ellipse { Width = 5, Height = 5, Margin = new Thickness(2, 8, 10, 0), VerticalAlignment = VerticalAlignment.Top };
            ArcTokens.Bind(dot, Shape.FillProperty, ArcDesignTokenKind.TextMuted);
            DockPanel.SetDock(dot, Dock.Left);
            var text = Paragraph(note.Text, ArcDesignTokenKind.TextSecondary);
            foreach (var index in note.Sources)
            {
                AddCitation(text, index);
            }
            row.Children.Add(dot);
            row.Children.Add(text);
            _notes.Children.Add(row);
        }
    }

    private void RebuildReport()
    {
        if (_report is null)
        {
            return;
        }
        _report.Children.Clear();
        var report = ShownReport;
        if (_copy is not null)
        {
            _copy.Value = report ?? string.Empty;
            _copy.IsVisible = Phase == ResearchRunPhase.Done && !string.IsNullOrEmpty(report);
        }
        if (string.IsNullOrWhiteSpace(report))
        {
            var placeholder = Paragraph(Phase == ResearchRunPhase.Stopped ? Labels.ReportStopped : Labels.ReportPending, ArcDesignTokenKind.TextMuted);
            _report.Children.Add(placeholder);
            return;
        }
        foreach (var paragraph in report.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            _report.Children.Add(CitedParagraph(paragraph.Trim()));
        }
    }

    /// <summary>A paragraph whose "[n]" marks become citation buttons.</summary>
    private TextBlock CitedParagraph(string text)
    {
        var block = Paragraph(null, ArcDesignTokenKind.Foreground);
        block.Inlines ??= new InlineCollection();
        var start = 0;
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '[')
            {
                var close = text.IndexOf(']', i + 1);
                if (close > i + 1 && int.TryParse(text.AsSpan(i + 1, close - i - 1), out var index) && FindSource(index) is not null)
                {
                    if (i > start)
                    {
                        block.Inlines.Add(new Run(text[start..i].TrimEnd()));
                    }
                    AddCitation(block, index);
                    i = close + 1;
                    start = i;
                    continue;
                }
            }
            i++;
        }
        if (start < text.Length)
        {
            block.Inlines.Add(new Run(text[start..]));
        }
        return block;
    }

    private static TextBlock Paragraph(string? text, ArcDesignTokenKind color)
    {
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap, LineHeight = 22 };
        if (text is not null)
        {
            block.Inlines = new InlineCollection { new Run(text) };
        }
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        return block;
    }

    private void AddCitation(TextBlock block, int index)
    {
        if (FindSource(index) is not { } source)
        {
            return;
        }
        block.Inlines ??= new InlineCollection();
        var chip = new ResearchRunCitation { Content = index.ToString(System.Globalization.CultureInfo.InvariantCulture), Source = source, Owner = this };
        AutomationProperties.SetName(chip, Labels.CitationName(index, source.Title));
        Tooltip.SetTip(chip, string.IsNullOrEmpty(source.Domain) ? source.Title : $"{source.Title} · {source.Domain}");
        block.Inlines.Add(new InlineUIContainer(chip) { BaselineAlignment = BaselineAlignment.Center });
    }

    internal ResearchRunSource? FindSource(int index)
    {
        foreach (var source in ShownSources)
        {
            if (source.Index == index)
            {
                return source;
            }
        }
        return null;
    }

    #endregion
}
