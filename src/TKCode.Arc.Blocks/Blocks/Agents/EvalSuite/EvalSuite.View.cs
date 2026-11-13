using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

[TemplatePart(HostPart, typeof(Panel))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(SubtitlePart, typeof(TextBlock))]
[TemplatePart(RunPart, typeof(Button))]
[TemplatePart(PickersPart, typeof(Grid))]
[TemplatePart(BaselinePickerPart, typeof(Select))]
[TemplatePart(CandidatePickerPart, typeof(Select))]
[TemplatePart(ProgressPart, typeof(Progress))]
[TemplatePart(TilesPart, typeof(Grid))]
[TemplatePart(FiltersPart, typeof(PillFilterBar))]
[TemplatePart(HeaderPart, typeof(EvalSuiteCaseHeader))]
[TemplatePart(ListPart, typeof(EvalSuiteCaseList))]
[TemplatePart(DetailHostPart, typeof(SpringSizeDecorator))]
[TemplatePart(DiffPart, typeof(AiDiffText))]
[PseudoClasses(CompactPseudoClass, RunningPseudoClass, OpenPseudoClass, DiffPseudoClass)]
public partial class EvalSuite
{
    public const string HostPart = "PART_Host";
    public const string TitlePart = "PART_Title";
    public const string SubtitlePart = "PART_Subtitle";
    public const string RunPart = "PART_Run";
    public const string PickersPart = "PART_Pickers";
    public const string BaselinePickerPart = "PART_BaselinePicker";
    public const string CandidatePickerPart = "PART_CandidatePicker";
    public const string ProgressPart = "PART_Progress";
    public const string TilesPart = "PART_Tiles";
    public const string FiltersPart = "PART_Filters";
    public const string HeaderPart = "PART_Header";
    public const string ListPart = "PART_List";
    public const string DetailHostPart = "PART_DetailHost";
    public const string DiffPart = "PART_Diff";

    /// <summary>Set while the suite is at most <c>620</c> wide: tighter paddings, stacked tiles and outputs, no check column.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while a run is in progress.</summary>
    public const string RunningPseudoClass = ":running";

    /// <summary>Set while a case is open below the list.</summary>
    public const string OpenPseudoClass = ":open";

    /// <summary>Set while the open case shows the word diff.</summary>
    public const string DiffPseudoClass = ":diff";

    private const string FilterRegressions = "regressions";
    private const string FilterFixed = "fixed";
    private const string FilterFailing = "failing";

    private readonly AiAnnouncer _announcer = new();
    private readonly Dictionary<string, EvalSuiteRow> _rowsById = new(StringComparer.Ordinal);
    private readonly List<EvalSuiteRow> _allRows = new();
    private readonly Dictionary<string, EvalCaseStatus> _live = new(StringComparer.Ordinal);

    private Panel? _host;
    private TextBlock? _title;
    private TextBlock? _subtitle;
    private Button? _run;
    private Grid? _pickers;
    private Select? _baselinePicker;
    private Select? _candidatePicker;
    private Progress? _progress;
    private Grid? _tiles;
    private Border? _baselineTile;
    private Border? _candidateTile;
    private Border? _deltaTile;
    private TextBlock? _baselineCaption;
    private TextBlock? _candidateCaption;
    private TextBlock? _deltaCaption;
    private RollingDigits? _baselineRate;
    private RollingDigits? _candidateRate;
    private RollingDigits? _delta;
    private ArcGlyph? _deltaGlyph;
    private TextBlock? _baselineCount;
    private TextBlock? _candidateCount;
    private TextBlock? _deltaNote;
    private PillFilterBar? _filters;
    private EvalSuiteCaseHeader? _header;
    private EvalSuiteCaseList? _list;
    private SpringSizeDecorator? _detailHost;
    private TextBlock? _detailTitle;
    private EvalSuiteChip? _detailStatus;
    private SegmentedControl? _detailMode;
    private Button? _close;
    private TextBlock? _inputCaption;
    private SelectableTextBlock? _detailInput;
    private TextBlock? _detailCheck;
    private Grid? _outputs;
    private Border? _candidateBox;
    private TextBlock? _baselineOutputTitle;
    private TextBlock? _candidateOutputTitle;
    private EvalSuiteChip? _baselineVerdict;
    private EvalSuiteChip? _candidateVerdict;
    private SelectableTextBlock? _baselineOutput;
    private SelectableTextBlock? _candidateOutput;
    private TextBlock? _baselineReason;
    private TextBlock? _candidateReason;
    private AiDiffText? _diff;
    private TextBlock? _diffSummary;

    private bool _syncing;
    private bool _compact;
    private string? _detailShownId;
    private TimeSpan _lastElapsed;

    #region Internal access (tests)

    internal EvalSuiteCaseList? List => _list;
    internal EvalSuiteCaseHeader? Header => _header;
    internal PillFilterBar? FilterBar => _filters;
    internal Button? RunButton => _run;
    internal Select? BaselinePicker => _baselinePicker;
    internal Select? CandidatePicker => _candidatePicker;
    internal Progress? ProgressBar => _progress;
    internal TextBlock? SubtitleText => _subtitle;
    internal RollingDigits? BaselineRateDigits => _baselineRate;
    internal RollingDigits? CandidateRateDigits => _candidateRate;
    internal RollingDigits? DeltaDigits => _delta;
    internal TextBlock? DeltaNoteText => _deltaNote;
    internal SpringSizeDecorator? DetailHost => _detailHost;
    internal TextBlock? DetailTitleText => _detailTitle;
    internal SelectableTextBlock? BaselineOutputText => _baselineOutput;
    internal SelectableTextBlock? CandidateOutputText => _candidateOutput;
    internal TextBlock? BaselineReasonText => _baselineReason;
    internal TextBlock? CandidateReasonText => _candidateReason;
    internal AiDiffText? DiffText => _diff;
    internal SegmentedControl? DetailModeSwitch => _detailMode;
    internal Button? CloseButton => _close;
    internal Grid? OutputsGrid => _outputs;
    internal Grid? TilesGrid => _tiles;
    internal string? LastAnnouncement => _announcer.Announcement;

    /// <summary>Every case's row in suite order (tests).</summary>
    internal IReadOnlyList<EvalSuiteRow> AllRows => _allRows;

    /// <summary>The status of a case as shown.</summary>
    internal EvalCaseStatus StatusOf(string caseId) => _rowsById.TryGetValue(caseId, out var row) ? row.Status : EvalCaseStatus.NotRun;

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _host?.Children.Remove(_announcer);
        var scope = e.NameScope;
        _host = scope.Find<Panel>(HostPart);
        _title = scope.Find<TextBlock>(TitlePart);
        _subtitle = scope.Find<TextBlock>(SubtitlePart);
        _run = scope.Find<Button>(RunPart);
        _pickers = scope.Find<Grid>(PickersPart);
        _baselinePicker = scope.Find<Select>(BaselinePickerPart);
        _candidatePicker = scope.Find<Select>(CandidatePickerPart);
        _progress = scope.Find<Progress>(ProgressPart);
        _tiles = scope.Find<Grid>(TilesPart);
        _baselineTile = scope.Find<Border>("PART_BaselineTile");
        _candidateTile = scope.Find<Border>("PART_CandidateTile");
        _deltaTile = scope.Find<Border>("PART_DeltaTile");
        _baselineCaption = scope.Find<TextBlock>("PART_BaselineCaption");
        _candidateCaption = scope.Find<TextBlock>("PART_CandidateCaption");
        _deltaCaption = scope.Find<TextBlock>("PART_DeltaCaption");
        _baselineRate = scope.Find<RollingDigits>("PART_BaselineRate");
        _candidateRate = scope.Find<RollingDigits>("PART_CandidateRate");
        _delta = scope.Find<RollingDigits>("PART_Delta");
        _deltaGlyph = scope.Find<ArcGlyph>("PART_DeltaGlyph");
        _baselineCount = scope.Find<TextBlock>("PART_BaselineCount");
        _candidateCount = scope.Find<TextBlock>("PART_CandidateCount");
        _deltaNote = scope.Find<TextBlock>("PART_DeltaNote");
        _filters = scope.Find<PillFilterBar>(FiltersPart);
        _header = scope.Find<EvalSuiteCaseHeader>(HeaderPart);
        _list = scope.Find<EvalSuiteCaseList>(ListPart);
        _detailHost = scope.Find<SpringSizeDecorator>(DetailHostPart);
        _detailTitle = scope.Find<TextBlock>("PART_DetailTitle");
        _detailStatus = scope.Find<EvalSuiteChip>("PART_DetailStatus");
        _detailMode = scope.Find<SegmentedControl>("PART_DetailMode");
        _close = scope.Find<Button>("PART_Close");
        _inputCaption = scope.Find<TextBlock>("PART_InputCaption");
        _detailInput = scope.Find<SelectableTextBlock>("PART_DetailInput");
        _detailCheck = scope.Find<TextBlock>("PART_DetailCheck");
        _outputs = scope.Find<Grid>("PART_Outputs");
        _candidateBox = scope.Find<Border>("PART_CandidateBox");
        _baselineOutputTitle = scope.Find<TextBlock>("PART_BaselineOutputTitle");
        _candidateOutputTitle = scope.Find<TextBlock>("PART_CandidateOutputTitle");
        _baselineVerdict = scope.Find<EvalSuiteChip>("PART_BaselineVerdict");
        _candidateVerdict = scope.Find<EvalSuiteChip>("PART_CandidateVerdict");
        _baselineOutput = scope.Find<SelectableTextBlock>("PART_BaselineOutput");
        _candidateOutput = scope.Find<SelectableTextBlock>("PART_CandidateOutput");
        _baselineReason = scope.Find<TextBlock>("PART_BaselineReason");
        _candidateReason = scope.Find<TextBlock>("PART_CandidateReason");
        _diff = scope.Find<AiDiffText>(DiffPart);
        _diffSummary = scope.Find<TextBlock>("PART_DiffSummary");

        _host?.Children.Add(_announcer);
        if (_header is not null)
        {
            _header.List = _list;
        }
        Hook();
        _compact = false;
        _detailShownId = null;
        ApplyCompact(Bounds.Width > 0 && Bounds.Width <= CompactWidth);
        SyncAll();
    }

    private void Hook()
    {
        if (_run is not null)
        {
            _run.Click += OnRunClick;
        }
        if (_baselinePicker is not null)
        {
            _baselinePicker.SelectionChanged += OnPickerChanged;
        }
        if (_candidatePicker is not null)
        {
            _candidatePicker.SelectionChanged += OnPickerChanged;
        }
        _filters?.AddHandler(PillFilterBar.SelectionChangedEvent, OnFilterChosen);
        if (_list is not null)
        {
            _list.Invoked += OnRowInvoked;
            _list.CursorMoved += OnCursorMoved;
        }
        if (_detailMode is not null)
        {
            _detailMode.SelectionChanged += OnDetailModeChanged;
        }
        if (_close is not null)
        {
            _close.Click += OnCloseClick;
        }
    }

    private void Unhook()
    {
        if (_run is not null)
        {
            _run.Click -= OnRunClick;
        }
        if (_baselinePicker is not null)
        {
            _baselinePicker.SelectionChanged -= OnPickerChanged;
        }
        if (_candidatePicker is not null)
        {
            _candidatePicker.SelectionChanged -= OnPickerChanged;
        }
        _filters?.RemoveHandler(PillFilterBar.SelectionChangedEvent, OnFilterChosen);
        if (_list is not null)
        {
            _list.Invoked -= OnRowInvoked;
            _list.CursorMoved -= OnCursorMoved;
        }
        if (_detailMode is not null)
        {
            _detailMode.SelectionChanged -= OnDetailModeChanged;
        }
        if (_close is not null)
        {
            _close.Click -= OnCloseClick;
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyCompact(e.NewSize.Width <= CompactWidth);
    }

    /// <summary>Stacks the tiles, the pickers and the outputs in a narrow suite; the theme tightens the paddings.</summary>
    private void ApplyCompact(bool compact)
    {
        if (compact == _compact && _tiles is not null && _tiles.ColumnDefinitions.Count > 0)
        {
            return;
        }
        _compact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
        if (_list is not null)
        {
            _list.IsCompact = compact;
        }
        _header?.InvalidateVisual();
        if (_tiles is { } tiles)
        {
            Arrange3(tiles, compact);
        }
        if (_outputs is { } outputs)
        {
            outputs.ColumnDefinitions = compact ? new ColumnDefinitions("*") : new ColumnDefinitions("*,*");
            outputs.RowDefinitions = compact ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto");
            if (_candidateBox is { } box)
            {
                Grid.SetColumn(box, compact ? 0 : 1);
                Grid.SetRow(box, compact ? 1 : 0);
            }
        }
        if (_pickers is { } pickers)
        {
            // Two choices with an arrow between them; when compact the arrow stays and the choices share the width.
            pickers.ColumnDefinitions = compact ? new ColumnDefinitions("*,Auto,*") : new ColumnDefinitions("220,Auto,220");
        }
    }

    private static void Arrange3(Grid grid, bool stacked)
    {
        grid.ColumnDefinitions = stacked ? new ColumnDefinitions("*") : new ColumnDefinitions("*,*,*");
        grid.RowDefinitions = stacked ? new RowDefinitions("Auto,Auto,Auto") : new RowDefinitions("Auto");
        for (var i = 0; i < grid.Children.Count; i++)
        {
            Grid.SetColumn(grid.Children[i], stacked ? 0 : i);
            Grid.SetRow(grid.Children[i], stacked ? i : 0);
        }
    }

    #endregion

    #region Property changes

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == CasesProperty || p == ResultsProperty || p == BaselineVersionIdProperty || p == CandidateVersionIdProperty)
        {
            if (_inRun && !_writingResults && p != ResultsProperty)
            {
                // Changing the cases or the versions in the middle of a run ends it: its results would no longer fit.
                Stop();
            }
            Rebuild();
            SyncAll();
        }
        else if (p == VersionsProperty || p == LabelsProperty || p == CultureNameProperty || p == TitleProperty)
        {
            Rebuild();
            SyncAll();
        }
        else if (p == FilterProperty)
        {
            Rebuild();
            SyncFilters();
            SyncList();
        }
        else if (p == SelectedCaseIdProperty)
        {
            SyncDetail();
        }
        else if (p == DetailModeProperty)
        {
            SyncDetailMode();
        }
        else if (p == CompactWidthProperty)
        {
            ApplyCompact(Bounds.Width > 0 && Bounds.Width <= CompactWidth);
        }
    }

    #endregion

    #region Model

    private EvalVersion? VersionById(string? id)
    {
        if (id is null || Versions is not { } versions)
        {
            return null;
        }
        foreach (var version in versions)
        {
            if (version.Id == id)
            {
                return version;
            }
        }
        return null;
    }

    /// <summary>Recomputes every row's results and status, the shown rows and the summary.</summary>
    private void Rebuild()
    {
        var cases = Cases ?? Array.Empty<EvalCase>();
        var baseId = BaselineVersionId;
        var candId = CandidateVersionId;
        var lookup = new Dictionary<(string, string), EvalResult>();
        if (Results is { } results)
        {
            foreach (var result in results)
            {
                // The last result for a case and version wins.
                lookup[(result.CaseId, result.VersionId)] = result;
            }
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        _allRows.Clear();
        for (var i = 0; i < cases.Count; i++)
        {
            var evalCase = cases[i];
            if (evalCase is null || !seen.Add(evalCase.Id))
            {
                continue;
            }
            if (!_rowsById.TryGetValue(evalCase.Id, out var row) || !ReferenceEquals(row.Case, evalCase) || row.Number != _allRows.Count + 1)
            {
                row?.ClearLayouts();
                row = new EvalSuiteRow(evalCase, _allRows.Count + 1);
                _rowsById[evalCase.Id] = row;
            }
            var baseline = baseId is null ? null : lookup.GetValueOrDefault((evalCase.Id, baseId));
            var candidate = candId is null ? null : lookup.GetValueOrDefault((evalCase.Id, candId));
            var status = _live.TryGetValue(evalCase.Id, out var live) ? live : EvalSuiteGrader.Compare(baseline, candidate);
            if (!ReferenceEquals(row.Baseline, baseline) || !ReferenceEquals(row.Candidate, candidate) || row.Status != status)
            {
                row.Baseline = baseline;
                row.Candidate = candidate;
                row.Status = status;
                row.ClearLayouts();
            }
            _allRows.Add(row);
        }
        // Forget rows of cases that left the suite.
        if (_rowsById.Count != _allRows.Count)
        {
            foreach (var id in _rowsById.Keys.Where(id => !seen.Contains(id)).ToArray())
            {
                _rowsById[id].ClearLayouts();
                _rowsById.Remove(id);
            }
        }
        Summary = EvalSuiteGrader.Summarize(_allRows.Count, _allRows.Select(r => (r.Baseline, r.Candidate)));
    }

    private int CountOf(EvalCaseFilter filter) => filter == EvalCaseFilter.All
        ? _allRows.Count
        : _allRows.Count(r => EvalSuiteGrader.Matches(filter, r.Status));

    /// <summary>The rows the filter shows.</summary>
    internal IReadOnlyList<EvalSuiteRow> ShownRows() =>
        Filter == EvalCaseFilter.All ? _allRows.ToArray() : _allRows.Where(r => EvalSuiteGrader.Matches(Filter, r.Status)).ToArray();

    #endregion

    #region Sync to the template

    private void SyncAll()
    {
        if (_list is null)
        {
            return;
        }
        var labels = Labels;
        var culture = Culture;
        AutomationProperties.SetName(this, Title ?? labels.SuiteName);
        if (_title is not null)
        {
            _title.Text = Title ?? labels.SuiteName;
        }
        if (_inputCaption is not null)
        {
            _inputCaption.Text = labels.InputTitle;
        }
        if (_close is not null)
        {
            AutomationProperties.SetName(_close, labels.CloseCase);
            Tooltip.SetTip(_close, labels.CloseCase);
        }
        if (_filters is not null)
        {
            _filters.AccessibleName = labels.FilterName;
        }
        if (_detailMode is not null)
        {
            AutomationProperties.SetName(_detailMode, labels.DetailModeName);
            _syncing = true;
            try
            {
                _detailMode.ItemsSource = new[]
                {
                    new SegmentedOption(EvalDetailMode.SideBySide, labels.SideBySide),
                    new SegmentedOption(EvalDetailMode.Diff, labels.Diff),
                };
            }
            finally
            {
                _syncing = false;
            }
        }
        if (_progress is not null)
        {
            AutomationProperties.SetName(_progress, labels.ProgressName);
        }
        _list.Labels = labels;
        _list.Culture = culture;
        SyncPickers();
        SyncRunState();
        SyncSummary();
        SyncFilters();
        SyncList();
        _detailShownId = null;
        SyncDetailMode();
        SyncDetail();
    }

    private void SyncPickers()
    {
        var labels = Labels;
        var versions = Versions ?? Array.Empty<EvalVersion>();
        _syncing = true;
        try
        {
            foreach (var (picker, label, id) in new[] { (_baselinePicker, labels.Baseline, BaselineVersionId), (_candidatePicker, labels.Candidate, CandidateVersionId) })
            {
                if (picker is null)
                {
                    continue;
                }
                picker.Label = label;
                AutomationProperties.SetName(picker, label);
                picker.TextSelector = item => item is EvalVersion v
                    ? string.IsNullOrEmpty(v.Note) ? v.Name : string.Format(Culture, labels.VersionWithNoteFormat, v.Name, v.Note)
                    : item?.ToString();
                if (!ReferenceEquals(picker.ItemsSource, versions))
                {
                    picker.ItemsSource = versions;
                }
                picker.SelectedItem = VersionById(id);
            }
        }
        finally
        {
            _syncing = false;
        }
        var baseline = VersionById(BaselineVersionId);
        var candidate = VersionById(CandidateVersionId);
        _header?.SetTitles(labels.ColumnStatus, labels.ColumnCase, labels.ColumnCheck,
            baseline?.Name ?? labels.Baseline, candidate?.Name ?? labels.Candidate);
    }

    private void SyncRunState()
    {
        var labels = Labels;
        var culture = Culture;
        var running = Phase == EvalRunPhase.Running;
        PseudoClasses.Set(RunningPseudoClass, running);
        if (_run is not null)
        {
            _run.Content = running ? labels.Stop : labels.Run;
            _run.IsEnabled = running || (VersionById(BaselineVersionId) is not null && VersionById(CandidateVersionId) is not null &&
                                         _allRows.Count > 0);
        }
        if (_progress is not null)
        {
            _progress.IsVisible = running;
            _progress.Maximum = Math.Max(1, RunTotal);
            _progress.Value = FinishedCount;
        }
        if (_baselinePicker is not null)
        {
            _baselinePicker.IsEnabled = !running;
        }
        if (_candidatePicker is not null)
        {
            _candidatePicker.IsEnabled = !running;
        }
        if (_subtitle is not null)
        {
            _subtitle.Text = string.Format(culture, labels.SubtitleFormat,
                string.Format(culture, labels.CaseCountFormat, _allRows.Count), StatusLine());
        }
    }

    /// <summary>The second half of the subtitle: where the run stands.</summary>
    internal string StatusLine()
    {
        var labels = Labels;
        var culture = Culture;
        return Phase switch
        {
            EvalRunPhase.Running => string.Format(culture, labels.StatusRunningFormat, FinishedCount, RunTotal),
            EvalRunPhase.Completed => string.Format(culture, labels.StatusDoneFormat, RunTotal, Seconds(_lastElapsed)),
            EvalRunPhase.Stopped => string.Format(culture, labels.StatusStoppedFormat, FinishedCount, RunTotal),
            _ => Summary.Compared > 0 ? string.Format(culture, labels.StatusLoadedFormat, Summary.Compared) : labels.StatusIdle
        };
    }

    private string Seconds(TimeSpan time) =>
        string.Format(Culture, Labels.SecondsFormat, time.TotalSeconds.ToString("0.0", Culture));

    /// <summary>The rounded rates as shown; the change is their difference, so the three figures always agree.</summary>
    internal (int Baseline, int Candidate, int Delta) ShownRates()
    {
        var s = Summary;
        var b = (int)Math.Round(s.BaselineRate * 100, MidpointRounding.AwayFromZero);
        var c = (int)Math.Round(s.CandidateRate * 100, MidpointRounding.AwayFromZero);
        return (b, c, c - b);
    }

    private void SyncSummary()
    {
        var labels = Labels;
        var culture = Culture;
        var s = Summary;
        var baseline = VersionById(BaselineVersionId)?.Name ?? labels.Baseline;
        var candidate = VersionById(CandidateVersionId)?.Name ?? labels.Candidate;
        var (b, c, d) = ShownRates();
        if (_baselineCaption is not null)
        {
            _baselineCaption.Text = string.Format(culture, labels.RateCaptionFormat, baseline);
        }
        if (_candidateCaption is not null)
        {
            _candidateCaption.Text = string.Format(culture, labels.RateCaptionFormat, candidate);
        }
        if (_deltaCaption is not null)
        {
            _deltaCaption.Text = labels.DeltaCaption;
        }
        if (_baselineRate is not null)
        {
            _baselineRate.CultureName = CultureName;
            _baselineRate.Value = b;
        }
        if (_candidateRate is not null)
        {
            _candidateRate.CultureName = CultureName;
            _candidateRate.Value = c;
        }
        if (_baselineCount is not null)
        {
            _baselineCount.Text = string.Format(culture, labels.PassedOfFormat, s.BaselinePassed, s.Compared);
        }
        if (_candidateCount is not null)
        {
            _candidateCount.Text = string.Format(culture, labels.PassedOfFormat, s.CandidatePassed, s.Compared);
        }
        if (_delta is not null)
        {
            _delta.CultureName = CultureName;
            _delta.Prefix = d > 0 ? "+" : d < 0 ? "−" : null;
            _delta.Suffix = labels.DeltaUnit;
            _delta.Value = Math.Abs(d);
        }
        if (_deltaGlyph is not null)
        {
            _deltaGlyph.Data = d > 0 ? ArcIcons.TrendingUp : d < 0 ? ArcIcons.TrendingDown : ArcIcons.Equal;
        }
        PseudoClasses.Set(":up", d > 0);
        PseudoClasses.Set(":down", d < 0);
        if (_deltaNote is not null)
        {
            _deltaNote.Text = string.Format(culture, labels.DeltaNoteFormat, s.Regressions, s.Fixes);
        }
        var signed = d > 0 ? "+" + d.ToString(culture) : d.ToString(culture);
        if (_baselineTile is not null)
        {
            AutomationProperties.SetName(_baselineTile, string.Format(culture, labels.RateNameFormat, baseline, b, s.BaselinePassed, s.Compared));
        }
        if (_candidateTile is not null)
        {
            AutomationProperties.SetName(_candidateTile, string.Format(culture, labels.RateNameFormat, candidate, c, s.CandidatePassed, s.Compared));
        }
        if (_deltaTile is not null)
        {
            AutomationProperties.SetName(_deltaTile, string.Format(culture, labels.DeltaNameFormat, signed) + ". " + _deltaNote?.Text);
        }
    }

    private void SyncFilters()
    {
        if (_filters is null)
        {
            return;
        }
        var labels = Labels;
        var culture = Culture;
        var options = new List<PillFilterOption>
        {
            new(null, string.Format(culture, labels.FilterAllFormat, CountOf(EvalCaseFilter.All))),
            new(FilterRegressions, string.Format(culture, labels.FilterRegressionsFormat, CountOf(EvalCaseFilter.Regressions))),
            new(FilterFixed, string.Format(culture, labels.FilterFixedFormat, CountOf(EvalCaseFilter.Fixed))),
            new(FilterFailing, string.Format(culture, labels.FilterFailingFormat, CountOf(EvalCaseFilter.Failing))),
        };
        var current = _filters.Options;
        var same = current is not null && current.Count == options.Count;
        for (var i = 0; same && i < options.Count; i++)
        {
            same = current![i] == options[i];
        }
        _syncing = true;
        try
        {
            if (!same)
            {
                _filters.Options = options;
            }
            _filters.SelectedKey = KeyOf(Filter);
        }
        finally
        {
            _syncing = false;
        }
    }

    private static string? KeyOf(EvalCaseFilter filter) => filter switch
    {
        EvalCaseFilter.Regressions => FilterRegressions,
        EvalCaseFilter.Fixed => FilterFixed,
        EvalCaseFilter.Failing => FilterFailing,
        _ => null
    };

    private static EvalCaseFilter FilterOf(string? key) => key switch
    {
        FilterRegressions => EvalCaseFilter.Regressions,
        FilterFixed => EvalCaseFilter.Fixed,
        FilterFailing => EvalCaseFilter.Failing,
        _ => EvalCaseFilter.All
    };

    private string FilterWord(EvalCaseFilter filter) => filter switch
    {
        EvalCaseFilter.Regressions => Labels.Regression.ToLower(Culture),
        EvalCaseFilter.Fixed => Labels.Fixed.ToLower(Culture),
        EvalCaseFilter.Failing => Labels.Failing.ToLower(Culture),
        _ => string.Empty
    };

    private void SyncList()
    {
        if (_list is null)
        {
            return;
        }
        var shown = ShownRows();
        var empty = _allRows.Count == 0 ? Labels.EmptySuite : string.Format(Culture, Labels.EmptyFilterFormat, FilterWord(Filter));
        _list.SetRows(shown, empty);
        _list.SetSelected(SelectedCaseId);
    }

    /// <summary>Repaints after results arrived during a run: rows, counts, tiles and subtitle.</summary>
    private void SyncProgress()
    {
        SyncRunState();
        SyncSummary();
        SyncFilters();
        SyncList();
        if (SelectedCaseId is not null)
        {
            _detailShownId = null;
            SyncDetail();
        }
    }

    #endregion

    #region The open case

    private void SyncDetail()
    {
        var id = SelectedCaseId;
        var row = id is null ? null : _rowsById.GetValueOrDefault(id);
        var open = row is not null;
        PseudoClasses.Set(OpenPseudoClass, open);
        _list?.SetSelected(open ? id : null);
        if (_detailHost is not null)
        {
            _detailHost.IsExpanded = open;
        }
        if (row is null || ReferenceEquals(_detailShownId, id) && _detailShownId is not null)
        {
            return;
        }
        _detailShownId = id;
        var labels = Labels;
        var culture = Culture;
        var baseline = VersionById(BaselineVersionId);
        var candidate = VersionById(CandidateVersionId);
        var c = row.Case;
        if (_detailTitle is not null)
        {
            _detailTitle.Text = c.Title;
        }
        _detailStatus?.ShowStatus(row.Status, labels);
        if (_detailInput is not null)
        {
            _detailInput.Text = c.Input;
        }
        if (_detailCheck is not null)
        {
            var check = c.Check == EvalCheckKind.Rubric
                ? string.Format(culture, labels.RubricLineFormat, labels.CheckName(c.Check), c.Expected, c.Threshold.ToString("0.00", culture))
                : string.Format(culture, labels.CheckLineFormat, labels.CheckName(c.Check), c.Expected);
            _detailCheck.Text = string.IsNullOrEmpty(c.Tag) ? check : c.Tag + " · " + check;
        }
        FillOutput(_baselineOutputTitle, _baselineVerdict, _baselineOutput, _baselineReason, labels.Baseline, baseline, row.Baseline);
        FillOutput(_candidateOutputTitle, _candidateVerdict, _candidateOutput, _candidateReason, labels.Candidate, candidate, row.Candidate);
        if (_diff is not null)
        {
            _diff.Before = row.Baseline?.Output ?? string.Empty;
            _diff.After = row.Candidate?.Output ?? string.Empty;
            var summary = string.Format(culture, labels.DiffSummaryFormat, _diff.InsertedWords, _diff.DeletedWords);
            _diff.SummaryName = summary;
            if (_diffSummary is not null)
            {
                _diffSummary.Text = row.Baseline is null || row.Candidate is null ? labels.NoOutput : summary;
            }
        }
    }

    private void FillOutput(TextBlock? title, EvalSuiteChip? verdict, SelectableTextBlock? output, TextBlock? reason, string role,
        EvalVersion? version, EvalResult? result)
    {
        var labels = Labels;
        var culture = Culture;
        if (title is not null)
        {
            title.Text = string.Format(culture, labels.OutputTitleFormat, role, version?.Name ?? string.Empty);
        }
        verdict?.ShowVerdict(result, labels, culture);
        if (output is not null)
        {
            output.Text = result?.Output is { Length: > 0 } text ? text : labels.NoOutput;
            output.Classes.Set("empty", result is null);
        }
        if (reason is not null)
        {
            reason.Text = result is null
                ? null
                : result.Duration > TimeSpan.Zero
                    ? string.Format(culture, labels.ReasonLineFormat, result.Detail ?? labels.Verdict(result, culture), Seconds(result.Duration))
                    : result.Detail ?? labels.Verdict(result, culture);
            reason.IsVisible = result is not null;
        }
    }

    private void SyncDetailMode()
    {
        var diff = DetailMode == EvalDetailMode.Diff;
        PseudoClasses.Set(DiffPseudoClass, diff);
        if (_detailMode is not null)
        {
            _syncing = true;
            try
            {
                _detailMode.SelectedIndex = diff ? 1 : 0;
            }
            finally
            {
                _syncing = false;
            }
        }
    }

    /// <summary>Opens a case below the list and moves the list's cursor to it.</summary>
    /// <param name="caseId">The <see cref="EvalCase.Id"/>.</param>
    public void OpenCase(string caseId)
    {
        SetCurrentValue(SelectedCaseIdProperty, caseId);
        _list?.MoveTo(caseId);
    }

    /// <summary>Closes the open case.</summary>
    public void CloseCase() => SetCurrentValue(SelectedCaseIdProperty, null);

    #endregion

    #region Input

    private void OnRunClick(object? sender, RoutedEventArgs e)
    {
        if (Phase == EvalRunPhase.Running)
        {
            Stop();
        }
        else
        {
            _ = RunAsync();
        }
    }

    private void OnPickerChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || sender is not Select picker)
        {
            return;
        }
        var id = (picker.SelectedItem as EvalVersion)?.Id;
        if (id is null)
        {
            return;
        }
        SetCurrentValue(ReferenceEquals(picker, _baselinePicker) ? BaselineVersionIdProperty : CandidateVersionIdProperty, id);
    }

    private void OnFilterChosen(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (_syncing || _filters is null)
        {
            return;
        }
        SetCurrentValue(FilterProperty, FilterOf(_filters.SelectedKey));
    }

    private void OnRowInvoked(EvalSuiteRow row)
    {
        if (SelectedCaseId == row.Case.Id)
        {
            CloseCase();
        }
        else
        {
            SetCurrentValue(SelectedCaseIdProperty, row.Case.Id);
        }
    }

    private void OnCursorMoved(EvalSuiteRow row)
    {
        // With a case open the arrows page through the cases.
        if (SelectedCaseId is not null)
        {
            SetCurrentValue(SelectedCaseIdProperty, row.Case.Id);
        }
    }

    private void OnDetailModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _detailMode is null)
        {
            return;
        }
        SetCurrentValue(DetailModeProperty, _detailMode.SelectedIndex == 1 ? EvalDetailMode.Diff : EvalDetailMode.SideBySide);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        var id = SelectedCaseId;
        CloseCase();
        if (_list is not null && id is not null)
        {
            _list.MoveTo(id);
            _list.Focus(NavigationMethod.Tab);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && SelectedCaseId is { } id)
        {
            var wasInList = _list?.IsKeyboardFocusWithin == true;
            CloseCase();
            if (_list is not null && !wasInList)
            {
                _list.MoveTo(id);
                _list.Focus(NavigationMethod.Tab);
            }
            e.Handled = true;
        }
    }

    #endregion
}
