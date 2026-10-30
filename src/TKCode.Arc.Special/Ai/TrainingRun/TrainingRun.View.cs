using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using ArcButton = TKCode.Arc.Button;

namespace TKCode.Arc;

public sealed partial class TrainingRun
{
    internal const string ChartPart = "PART_Chart";
    internal const string EventsPart = "PART_Events";
    internal const string AnnouncerPart = "PART_Announcer";

    private TrainingRunChart? _chart;
    private TrainingRunEventList? _eventList;
    private ExplainerAnnouncer? _announcer;
    private ArcGlyph? _modelGlyph;
    private Border? _monogram;
    private TextBlock? _monogramText;
    private TextBlock? _title;
    private TextBlock? _subtitle;
    private ArcSpinner? _spinner;
    private ArcGlyph? _statusGlyph;
    private ArcContentSwap? _statusLabel;
    private TextBlock? _trainLabel;
    private TextBlock? _trainValue;
    private TextBlock? _validationLabel;
    private TextBlock? _validationValue;
    private TextBlock? _legendTail;
    private ToolCallText? _activity;
    private UniformGrid? _statsGrid;
    private UniformGrid? _metricsGrid;
    private ArcContentSwap? _footerInfo;
    private readonly TrainingRunStat[] _stats = new TrainingRunStat[4];
    private readonly TrainingRunStat[] _metrics = new TrainingRunStat[4];
    private readonly TrainingRunReadout _stepReadout = new();
    private readonly TrainingRunReadout _rateReadout = new();
    private readonly TrainingRunReadout _elapsedReadout = new();
    private readonly TrainingRunReadout _etaReadout = new();
    private int _shownLossVersion = -1;
    private int _shownEventsVersion = -1;
    private bool? _shownSimulated;
    private string? _shownStatus;
    private string? _footerKey;
    private bool? _showsMetrics;

    /// <summary>The chart part (tests).</summary>
    internal TrainingRunChart? ChartPartControl => _chart;

    /// <summary>The event log part (tests).</summary>
    internal TrainingRunEventList? EventListPart => _eventList;

    /// <summary>The live region (tests).</summary>
    internal ExplainerAnnouncer? AnnouncerPartControl => _announcer;

    /// <summary>The live stats: step, learning rate, elapsed, ETA (tests).</summary>
    internal IReadOnlyList<TrainingRunStat> StatCells => _stats;

    /// <summary>The results shown on completion (tests).</summary>
    internal IReadOnlyList<TrainingRunStat> MetricCells => _metrics;

    /// <summary>True while the results replace the live stats.</summary>
    internal bool ShowsMetrics => _showsMetrics == true;

    /// <summary>The legend's train value, validation label and value, and the text at its end (tests).</summary>
    internal string LegendTrain => _trainValue?.Text ?? string.Empty;

    internal string LegendValidationLabel => _validationLabel?.Text ?? string.Empty;

    internal string LegendValidation => _validationValue?.Text ?? string.Empty;

    internal string LegendTail => _legendTail?.Text ?? string.Empty;

    /// <summary>The line over the empty chart, or null when it is hidden.</summary>
    internal string? ActivityText => _activity is { IsVisible: true } ? _activity.Text : null;

    /// <summary>What the footer shows: "dataset:…", "error:…", "deploying", "deployed:…", "deploy-failed:…" or "".</summary>
    internal string FooterKey => _footerKey ?? string.Empty;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        UnhookParts();
        var scope = e.NameScope;
        _chart = scope.Find<TrainingRunChart>(ChartPart);
        _eventList = scope.Find<TrainingRunEventList>(EventsPart);
        _announcer = scope.Find<ExplainerAnnouncer>(AnnouncerPart);
        _modelGlyph = scope.Find<ArcGlyph>("PART_ModelGlyph");
        _monogram = scope.Find<Border>("PART_Monogram");
        _monogramText = scope.Find<TextBlock>("PART_MonogramText");
        _title = scope.Find<TextBlock>("PART_Title");
        _subtitle = scope.Find<TextBlock>("PART_Subtitle");
        _spinner = scope.Find<ArcSpinner>("PART_Spinner");
        _statusGlyph = scope.Find<ArcGlyph>("PART_StatusGlyph");
        _statusLabel = scope.Find<ArcContentSwap>("PART_StatusLabel");
        _trainLabel = scope.Find<TextBlock>("PART_TrainLabel");
        _trainValue = scope.Find<TextBlock>("PART_TrainValue");
        _validationLabel = scope.Find<TextBlock>("PART_ValidationLabel");
        _validationValue = scope.Find<TextBlock>("PART_ValidationValue");
        _legendTail = scope.Find<TextBlock>("PART_LegendTail");
        _activity = scope.Find<ToolCallText>("PART_Activity");
        _statsGrid = scope.Find<UniformGrid>("PART_Stats");
        _metricsGrid = scope.Find<UniformGrid>("PART_Metrics");
        _footerInfo = scope.Find<ArcContentSwap>("PART_FooterInfo");
        _cancel = scope.Find<ArcButton>("PART_Cancel");
        _pause = scope.Find<ArcButton>("PART_Pause");
        _resume = scope.Find<ArcButton>("PART_Resume");
        _retry = scope.Find<ArcButton>("PART_Retry");
        _restart = scope.Find<ArcButton>("PART_Restart");
        _deploy = scope.Find<ArcButton>("PART_Deploy");
        HookParts();
        BuildStats();
        _shownLossVersion = -1;
        _shownEventsVersion = -1;
        _shownSimulated = null;
        _shownStatus = null;
        _footerKey = null;
        _showsMetrics = null;
        Refresh();
    }

    private void HookParts()
    {
        if (_chart is not null)
        {
            _chart.InspectedChanged += OnInspectedChanged;
        }
        HookButtons();
    }

    private void UnhookParts()
    {
        if (_chart is not null)
        {
            _chart.InspectedChanged -= OnInspectedChanged;
        }
        UnhookButtons();
        _statsGrid?.Children.Clear();
        _metricsGrid?.Children.Clear();
    }

    private void BuildStats()
    {
        if (_statsGrid is not null)
        {
            _statsGrid.Children.Clear();
            TrainingRunReadout[] readouts = [_stepReadout, _rateReadout, _elapsedReadout, _etaReadout];
            for (var i = 0; i < _stats.Length; i++)
            {
                _stats[i] = new TrainingRunStat { Value = readouts[i] };
                _statsGrid.Children.Add(_stats[i]);
            }
            AutomationProperties.SetControlTypeOverride(_statsGrid, Avalonia.Automation.Peers.AutomationControlType.List);
        }
        if (_metricsGrid is not null)
        {
            _metricsGrid.Children.Clear();
            for (var i = 0; i < _metrics.Length; i++)
            {
                _metrics[i] = new TrainingRunStat();
                _metricsGrid.Children.Add(_metrics[i]);
            }
            AutomationProperties.SetControlTypeOverride(_metricsGrid, Avalonia.Automation.Peers.AutomationControlType.List);
        }
    }

    private void OnInspectedChanged()
    {
        UpdateLegend();
    }

    /// <summary>Brings every part in line with the state; a part that already shows the value is left alone.</summary>
    internal void Refresh()
    {
        if (_chart is null)
        {
            return;
        }
        var labels = Labels;
        var status = EffectiveStatus;
        PseudoClasses.Set(QueuedPseudoClass, status == TrainingRunStatus.Queued);
        PseudoClasses.Set(RunningPseudoClass, status == TrainingRunStatus.Running);
        PseudoClasses.Set(PausedPseudoClass, status == TrainingRunStatus.Paused);
        PseudoClasses.Set(CompletedPseudoClass, status == TrainingRunStatus.Completed);
        PseudoClasses.Set(FailedPseudoClass, status == TrainingRunStatus.Failed);
        PseudoClasses.Set(CancelledPseudoClass, status == TrainingRunStatus.Cancelled);
        if (status != TrainingRunStatus.Completed)
        {
            // Leaving Completed aborts a deploy in flight and forgets its result.
            ResetDeploy();
        }

        RefreshHead(labels, status);

        // The chart: samples only when they changed, the run's shape every time (cheap).
        var loss = EffectiveLoss;
        if (_shownSimulated != IsSimulated || _shownLossVersion != LossVersion)
        {
            _shownLossVersion = LossVersion;
            _chart.SetSamples(loss);
        }
        if (!ReferenceEquals(_chart.EpochLabel, labels.Epoch))
        {
            _chart.EpochLabel = labels.Epoch;
        }
        _chart.SetRun(EffectiveTotalSteps, EffectiveEpochs, EffectiveStep, status == TrainingRunStatus.Running);
        if (_activity is not null)
        {
            var waiting = loss.Count == 0 && status is TrainingRunStatus.Queued or TrainingRunStatus.Running;
            _activity.IsVisible = waiting;
            if (waiting)
            {
                var text = EffectiveActivity ?? (status == TrainingRunStatus.Queued ? labels.WaitingInQueue : labels.StartingUp);
                if (_activity.Text != text)
                {
                    _activity.Text = text;
                }
            }
            _activity.IsShimmering = waiting;
        }
        UpdateLegend();

        RefreshStats(labels, status);

        if (_eventList is not null && (_shownSimulated != IsSimulated || _shownEventsVersion != EventsVersion))
        {
            _shownEventsVersion = EventsVersion;
            _eventList.SetEvents(EffectiveEvents, labels);
        }
        _shownSimulated = IsSimulated;

        RefreshFooter(labels, status);
        RefreshButtons(labels, status);
    }

    private void RefreshHead(TrainingRunLabels labels, TrainingRunStatus status)
    {
        if (_title is not null && _title.Text != DisplayName)
        {
            _title.Text = DisplayName;
        }
        var model = DisplayModel;
        if (_subtitle is not null)
        {
            var details = DisplayDetails;
            var parts = string.IsNullOrEmpty(model.Name) ? details : [model.Name, .. details];
            var subtitle = string.Join(" · ", parts);
            if (_subtitle.Text != subtitle)
            {
                _subtitle.Text = subtitle;
            }
        }
        if (_modelGlyph is not null && _monogram is not null)
        {
            _modelGlyph.IsVisible = model.Mark is not null;
            _monogram.IsVisible = model.Mark is null;
            _modelGlyph.Data = model.Mark;
            if (model.MarkBrush is { } brush)
            {
                _modelGlyph.Foreground = brush;
            }
            else
            {
                _modelGlyph.ClearValue(ArcGlyph.ForegroundProperty);
            }
            if (_monogramText is not null)
            {
                var name = model.Name.Trim();
                _monogramText.Text = name.Length > 0 ? name[..1].ToUpper(CultureInfo.InvariantCulture) : "?";
            }
            AutomationProperties.SetName(_modelGlyph, model.Name);
        }

        var statusText = StatusText;
        if (_statusLabel is not null && !Equals(_statusLabel.Content, statusText))
        {
            _statusLabel.Content = statusText;
        }
        var spinning = status == TrainingRunStatus.Running;
        if (_spinner is not null)
        {
            _spinner.IsVisible = spinning;
        }
        if (_statusGlyph is not null)
        {
            _statusGlyph.IsVisible = !spinning;
            _statusGlyph.Data = status switch
            {
                TrainingRunStatus.Queued => ArcIcons.Clock,
                TrainingRunStatus.Paused => ArcIcons.Pause,
                TrainingRunStatus.Completed => ArcIcons.CircleCheck,
                TrainingRunStatus.Failed => ArcIcons.CircleAlert,
                TrainingRunStatus.Cancelled => ArcIcons.Ban,
                _ => ArcIcons.Circle,
            };
        }
        // The live region says each status change once (never each step); Starting is part of Training.
        var spoken = status == TrainingRunStatus.Running ? labels.Training : statusText;
        if (_shownStatus is not null && _shownStatus != spoken)
        {
            _announcer?.Announce(spoken);
        }
        _shownStatus = spoken;
    }

    /// <summary>The legend reads the latest sample, or the inspected one; the chart's name carries the same.</summary>
    private void UpdateLegend()
    {
        if (_chart is null)
        {
            return;
        }
        var labels = Labels;
        var points = _chart.Points;
        var inspected = _chart.InspectedIndex;
        var index = inspected ?? points.Count - 1;
        double? train = null;
        double? validation = null;
        if (index >= 0)
        {
            train = _chart.SmoothedAt(index) ?? _chart.TrainAt(index);
            validation = _chart.ValidationAt(index);
        }
        var trainText = train is { } t ? TrainingRunFormat.Loss(t) : labels.Missing;
        var validationText = validation is { } v ? TrainingRunFormat.Loss(v) : labels.Missing;
        Set(_trainLabel, labels.Train);
        Set(_trainValue, trainText);
        Set(_validationLabel, PseudoClasses.Contains(NarrowPseudoClass) ? labels.ValidationShort : labels.Validation);
        Set(_validationValue, validationText);
        string tail;
        if (inspected is { } i && i < points.Count)
        {
            tail = labels.StepAt(points[i].Step);
        }
        else if (EffectiveStatus == TrainingRunStatus.Queued || IsStartingUp)
        {
            tail = string.Empty;
        }
        else
        {
            var epochs = EffectiveEpochs;
            tail = labels.EpochOf(TrainingRunFormat.EpochOf(Math.Min(EffectiveStep, EffectiveTotalSteps - 1), EffectiveTotalSteps, epochs), epochs);
        }
        Set(_legendTail, tail);
        PseudoClasses.Set(InspectingPseudoClass, inspected is not null);

        var name = labels.Chart + ", " + (index < 0
            ? labels.NoSamples
            : labels.StepAt(points[index].Step) + ", " + labels.TrainLoss(trainText)
              + (validation is null ? string.Empty : ", " + labels.ValidationLoss(validationText)));
        if (AutomationProperties.GetName(_chart) != name)
        {
            AutomationProperties.SetName(_chart, name);
        }
    }

    private static void Set(TextBlock? block, string text)
    {
        if (block is not null && block.Text != text)
        {
            block.Text = text;
        }
    }

    private void RefreshStats(TrainingRunLabels labels, TrainingRunStatus status)
    {
        if (_statsGrid is null || _metricsGrid is null)
        {
            return;
        }
        var narrow = PseudoClasses.Contains(NarrowPseudoClass);
        _statsGrid.Columns = narrow ? 2 : 4;
        _metricsGrid.Columns = narrow ? 2 : 4;
        AutomationProperties.SetName(_statsGrid, labels.Stats);
        AutomationProperties.SetName(_metricsGrid, labels.Stats);

        var step = EffectiveStep;
        var total = EffectiveTotalSteps;
        if (status == TrainingRunStatus.Queued)
        {
            _stepReadout.ShowMissing(labels.Missing);
        }
        else
        {
            _stepReadout.ShowNumber(step, 0, labels.StepTotal(total), TrainingRunFormat.Count(step) + labels.StepTotal(total));
        }
        if (EffectiveLearningRate is { } rate && status != TrainingRunStatus.Queued)
        {
            var (mantissa, exponent) = TrainingRunFormat.Scientific(rate);
            _rateReadout.ShowNumber(mantissa, 2, TrainingRunFormat.Exponent(exponent), TrainingRunFormat.LearningRate(rate));
        }
        else
        {
            _rateReadout.ShowMissing(labels.Missing);
        }
        if (EffectiveElapsed is { } elapsed && status != TrainingRunStatus.Queued)
        {
            _elapsedReadout.ShowTime(elapsed, labels, null);
        }
        else
        {
            _elapsedReadout.ShowMissing(labels.Missing);
        }
        var showsEta = status is TrainingRunStatus.Queued or TrainingRunStatus.Running or TrainingRunStatus.Paused;
        if (EffectiveEta is { } eta && showsEta)
        {
            _etaReadout.ShowTime(eta, labels, status == TrainingRunStatus.Queued ? labels.About : null);
        }
        else
        {
            _etaReadout.ShowMissing(labels.Missing);
        }
        string[] statLabels = [labels.Step, labels.LearningRate, labels.Elapsed, labels.Eta];
        for (var i = 0; i < _stats.Length; i++)
        {
            _stats[i].Label = statLabels[i];
            _stats[i].UpdateName();
        }

        var completed = status == TrainingRunStatus.Completed;
        if (completed)
        {
            var metrics = Metrics ?? DefaultMetrics(labels);
            for (var i = 0; i < _metrics.Length; i++)
            {
                var cell = _metrics[i];
                if (i < metrics.Count)
                {
                    cell.IsVisible = true;
                    cell.Label = metrics[i].Label;
                    cell.Value = metrics[i].Value;
                    cell.Hint = metrics[i].Hint;
                }
                else
                {
                    cell.IsVisible = false;
                }
            }
        }
        if (_showsMetrics != completed)
        {
            var first = _showsMetrics is null;
            _showsMetrics = completed;
            _statsGrid.IsVisible = !completed;
            _metricsGrid.IsVisible = completed;
            if (!first)
            {
                FadeIn(completed ? _metricsGrid : _statsGrid);
            }
        }
    }

    /// <summary>The results on completion when <see cref="Metrics"/> is not set: final train and validation loss, steps, duration.</summary>
    internal IReadOnlyList<TrainingRunMetric> DefaultMetrics(TrainingRunLabels labels)
    {
        var loss = EffectiveLoss;
        double? firstTrain = null;
        double? lastTrain = null;
        double? lastValidation = null;
        double? bestValidation = null;
        var bestStep = 0;
        foreach (var point in loss)
        {
            if (point.Train is { } train)
            {
                firstTrain ??= train;
                lastTrain = train;
            }
            if (point.Validation is { } validation)
            {
                lastValidation = validation;
                if (bestValidation is null || validation < bestValidation)
                {
                    bestValidation = validation;
                    bestStep = point.Step;
                }
            }
        }
        var list = new List<TrainingRunMetric>(4)
        {
            new(labels.FinalTrainLoss, lastTrain is { } lt ? TrainingRunFormat.Loss(lt) : labels.Missing,
                firstTrain is { } ft ? labels.From(TrainingRunFormat.Loss(ft)) : null),
            new(labels.FinalValidationLoss, lastValidation is { } lv ? TrainingRunFormat.Loss(lv) : labels.Missing,
                bestValidation is not null ? labels.BestAt(bestStep) : null),
            new(labels.Steps, TrainingRunFormat.Count(EffectiveStep), labels.EpochCount(EffectiveEpochs)),
            new(labels.Duration, EffectiveElapsed is { } elapsed ? TrainingRunFormat.Time(elapsed, labels) : labels.Missing),
        };
        return list;
    }

    /// <summary>The incoming stats or results fade in (160ms, the theme's transition); reduced motion and Off: at once.</summary>
    private void FadeIn(Control target)
    {
        if (!ArcMotion.IsFull(this))
        {
            target.Opacity = 1;
            return;
        }
        target.Opacity = 0;
        Dispatcher.UIThread.Post(() => target.Opacity = 1, DispatcherPriority.Loaded);
    }

    private void RefreshFooter(TrainingRunLabels labels, TrainingRunStatus status)
    {
        if (_footerInfo is null)
        {
            return;
        }
        string key;
        if (status == TrainingRunStatus.Completed && _deployState == DeployState.Deployed)
        {
            key = "deployed:" + _endpoint;
        }
        else if (status == TrainingRunStatus.Completed && _deployState == DeployState.Failed)
        {
            key = "deploy-failed:" + (_deployError ?? labels.DeployFailed);
        }
        else if (status == TrainingRunStatus.Failed && !string.IsNullOrEmpty(EffectiveError))
        {
            key = "error:" + EffectiveError;
        }
        else if (DisplayDataset is { } dataset)
        {
            key = "dataset:" + dataset.Name + "|" + dataset.Examples + "|" + (dataset.Mark is null ? "" : "m") + "|" + labels.GetHashCode();
        }
        else
        {
            key = string.Empty;
        }
        if (key == _footerKey)
        {
            return;
        }
        _footerKey = key;
        _copyEndpoint = null;
        _footerInfo.Content = key switch
        {
            _ when key.StartsWith("deployed:", StringComparison.Ordinal) => DeployedView(labels),
            _ when key.StartsWith("deploy-failed:", StringComparison.Ordinal) => ProblemView(_deployError ?? labels.DeployFailed),
            _ when key.StartsWith("error:", StringComparison.Ordinal) => ProblemView(EffectiveError!),
            _ when key.Length > 0 => DatasetView(DisplayDataset!, labels),
            _ => null,
        };
    }

    private static TextBlock Muted(string text)
    {
        var block = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        return block;
    }

    private Control DatasetView(TrainingRunDataset dataset, TrainingRunLabels labels)
    {
        var glyph = FooterGlyph(dataset.Mark ?? ArcIcons.Database, ArcDesignTokenKind.TextSecondary);
        glyph.VerticalAlignment = VerticalAlignment.Center;
        var name = new TextBlock { Text = dataset.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        ArcTokens.Bind(name, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 6 };
        grid.Children.Add(glyph);
        Grid.SetColumn(name, 1);
        name.HorizontalAlignment = HorizontalAlignment.Left;
        grid.Children.Add(name);
        if (dataset.Examples is { } examples)
        {
            var count = Muted("· " + labels.Examples(examples));
            count.FontFeatures = TabularNumbers;
            Grid.SetColumn(count, 2);
            grid.Children.Add(count);
            // The name gives way first; the count stays whole.
            grid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Auto);
            grid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            count.HorizontalAlignment = HorizontalAlignment.Left;
        }
        AutomationProperties.SetName(grid, dataset.Examples is { } n ? dataset.Name + ", " + labels.Examples(n) : dataset.Name);
        return grid;
    }

    private Control ProblemView(string message)
    {
        var glyph = FooterGlyph(ArcIcons.CircleAlert, ArcDesignTokenKind.Danger);
        glyph.VerticalAlignment = VerticalAlignment.Top;
        glyph.Margin = new Thickness(0, 2, 0, 0);
        var text = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap };
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, ArcDesignTokenKind.Danger);
        Grid.SetColumn(text, 1);
        return new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 6, Children = { glyph, text } };
    }

    private Control DeployedView(TrainingRunLabels labels)
    {
        var glyph = FooterGlyph(ArcIcons.CircleCheck, ArcDesignTokenKind.Success);
        glyph.VerticalAlignment = VerticalAlignment.Center;
        var word = new TextBlock { Text = labels.DeployedTo, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(word, 1);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), ColumnSpacing = 6, Children = { glyph, word } };
        if (!string.IsNullOrEmpty(_endpoint))
        {
            var endpoint = Muted(_endpoint);
            ArcTokens.Bind(endpoint, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
            endpoint.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(endpoint, 2);
            var copy = new CopyButton { Value = _endpoint, IsIconOnly = true, Variant = CopyButtonVariant.Plain, VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(copy, labels.CopyEndpoint);
            Grid.SetColumn(copy, 3);
            grid.Children.Add(endpoint);
            grid.Children.Add(copy);
            _copyEndpoint = copy;
        }
        return grid;
    }

    private static readonly FontFeatureCollection TabularNumbers = new() { FontFeature.Parse("tnum") };

    /// <summary>A footer glyph at the token's dataset mark size (14px).</summary>
    private static ArcGlyph FooterGlyph(Geometry data, ArcDesignTokenKind color)
    {
        var glyph = new ArcGlyph { Data = data };
        glyph.Bind(ArcGlyph.SizeProperty, glyph.GetResourceObservable(TrainingRunTokens.Key(TrainingRunTokenKind.DatasetMarkSize)));
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, color);
        return glyph;
    }
}
