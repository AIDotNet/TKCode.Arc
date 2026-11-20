using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// An animated illustration of how a model learns, told in five steps that loop seamlessly (about 21 s at speed 1). A
/// batch leaves a stack of training data and drops into the input layer of a small network drawn on 2.5D plates; signals
/// ride every connection forward to a prediction; the error rides them back as a short dash and each connection it
/// passes grows a little stronger; over the epochs batches stream through, the useful connections thicken and take the
/// accent while the rest stay light, the loss curve falls beside the network and the loss, epoch and output readouts
/// count in step; at the end the held-out data runs through once, the score counts up and the evaluation passes its
/// target. A step bar with play and pause replays the story or jumps to any step (while paused, a step shows its still
/// frame); a caption explains the current step; a mouse resting on a stack or a layer shows a detail and slows the
/// story down. Under reduced motion it shows the finished run as a still frame. The network and the numbers are
/// illustrative.
/// <para>
/// The <see cref="Explainer"/> clock drives it: <see cref="Explainer.AutoPlay"/> starts it on first sight,
/// <see cref="Explainer.Pause"/> holds it, <see cref="Explainer.StepForward"/> plays to the next step,
/// <see cref="Explainer.Restart"/> starts over and <see cref="Explainer.Speed"/> scales it (0 holds it still). A playing
/// run always loops without a pause, so <see cref="Explainer.IsLooping"/> has no effect; <see cref="Explainer.Finished"/>
/// is raised at the end of every run. <see cref="Explainer.AreControlsVisible"/> shows the step bar. The header-only
/// members (<see cref="Explainer.Model"/>) have no effect.
/// </para>
/// </summary>
[TemplatePart(StagePart, typeof(ModelTrainingStage))]
[TemplatePart(StepsPart, typeof(Grid))]
[TemplatePart(CaptionPart, typeof(TextBlock))]
[PseudoClasses(CompactPseudoClass, UntrainedPseudoClass, EvalPendingPseudoClass, EvalScoringPseudoClass, EvalPassedPseudoClass,
    EvalFailedPseudoClass, ReducedPseudoClass)]
public class ModelTraining : Explainer
{
    /// <summary>The drawn diagram: data stacks, network and outputs.</summary>
    public const string StagePart = "PART_Stage";

    internal const string StepsPart = "PART_Steps";
    internal const string CaptionPart = "PART_Caption";
    internal const string CompactPseudoClass = ":compact";
    internal const string UntrainedPseudoClass = ":untrained";
    internal const string EvalPendingPseudoClass = ":eval-pending";
    internal const string EvalScoringPseudoClass = ":eval-scoring";
    internal const string EvalPassedPseudoClass = ":eval-passed";
    internal const string EvalFailedPseudoClass = ":eval-failed";
    internal const string ReducedPseudoClass = ":reduced";

    /// <summary>The number of steps in the step bar.</summary>
    internal const int StepTotal = 5;

    /// <summary>The pace a resting mouse slows the story to.</summary>
    internal const double HoverRate = 0.6;

    private static readonly IReadOnlyList<string> s_sampleClasses = ["Cat", "Dog"];
    private static readonly IReadOnlyList<int> s_sampleHidden = [4, 4];

    /// <summary>
    /// How many training steps (batches) the story shows: the first one slowly over the batch, forward and backward steps,
    /// the rest streaming through the epochs step (2 to 20, default 6).
    /// </summary>
    public static readonly StyledProperty<int> StepCountProperty =
        AvaloniaProperty.Register<ModelTraining, int>(nameof(StepCount), 6, coerce: static (_, v) => Math.Clamp(v, 2, 20));

    /// <summary>The samples per batch, named in the batch caption and the training data's detail (default 32).</summary>
    public static readonly StyledProperty<int> BatchSizeProperty =
        AvaloniaProperty.Register<ModelTraining, int>(nameof(BatchSize), 32, coerce: static (_, v) => Math.Max(1, v));

    /// <summary>
    /// How far each update moves the weights toward their trained values (approximate; default 0.05): a larger rate makes
    /// the connections strengthen sooner, a smaller one later. The finished model looks the same.
    /// </summary>
    public static readonly StyledProperty<double> LearningRateProperty =
        AvaloniaProperty.Register<ModelTraining, double>(nameof(LearningRate), 0.05,
            coerce: static (_, v) => double.IsFinite(v) ? Math.Clamp(v, 0.0001, 1) : 0.05);

    /// <summary>
    /// The loss curve as values spread evenly over the training, first to last (at least two); null (the default) draws a
    /// curve from <see cref="LossFrom"/> to <see cref="LossTo"/>. When set, its first and last values replace them.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> LossValuesProperty =
        AvaloniaProperty.Register<ModelTraining, IReadOnlyList<double>?>(nameof(LossValues));

    /// <summary>Every word the illustration shows or announces.</summary>
    public static readonly StyledProperty<ModelTrainingLabels> LabelsProperty =
        AvaloniaProperty.Register<ModelTraining, ModelTrainingLabels>(nameof(Labels), new ModelTrainingLabels());

    /// <summary>The two classes the model learns to tell apart, shown beside the output layer (default Cat and Dog).</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> ClassNamesProperty =
        AvaloniaProperty.Register<ModelTraining, IReadOnlyList<string>?>(nameof(ClassNames));

    /// <summary>The units of the input layer, one per feature; each data sheet shows one line per feature (2 to 5, default 3).</summary>
    public static readonly StyledProperty<int> InputCountProperty =
        AvaloniaProperty.Register<ModelTraining, int>(nameof(InputCount), 3, coerce: static (_, v) => Math.Clamp(v, 2, 5));

    /// <summary>The units of the two hidden layers, each 2 to 6 (default 4 and 4).</summary>
    public static readonly StyledProperty<IReadOnlyList<int>?> HiddenUnitsProperty =
        AvaloniaProperty.Register<ModelTraining, IReadOnlyList<int>?>(nameof(HiddenUnits));

    /// <summary>The samples in the training data, shown under its stack (default 12,800).</summary>
    public static readonly StyledProperty<int> SamplesProperty =
        AvaloniaProperty.Register<ModelTraining, int>(nameof(Samples), 12800, coerce: static (_, v) => Math.Max(0, v));

    /// <summary>The samples in the held-out data (default 1,600).</summary>
    public static readonly StyledProperty<int> HeldOutSamplesProperty =
        AvaloniaProperty.Register<ModelTraining, int>(nameof(HeldOutSamples), 1600, coerce: static (_, v) => Math.Max(0, v));

    /// <summary>The epochs the story trains for; the epoch readout and the chart's axis count to it (default 12).</summary>
    public static readonly StyledProperty<int> EpochsProperty =
        AvaloniaProperty.Register<ModelTraining, int>(nameof(Epochs), 12, coerce: static (_, v) => Math.Clamp(v, 1, 9999));

    /// <summary>The loss after the first batch, where the curve starts (default 0.69).</summary>
    public static readonly StyledProperty<double> LossFromProperty =
        AvaloniaProperty.Register<ModelTraining, double>(nameof(LossFrom), 0.69);

    /// <summary>The loss after the last epoch, where the curve ends; the validation point sits just above it (default 0.08).</summary>
    public static readonly StyledProperty<double> LossToProperty =
        AvaloniaProperty.Register<ModelTraining, double>(nameof(LossTo), 0.08);

    /// <summary>The accuracy on the held-out data in percent; the evaluation counts up to it (default 94.2).</summary>
    public static readonly StyledProperty<double> AccuracyProperty =
        AvaloniaProperty.Register<ModelTraining, double>(nameof(Accuracy), 94.2);

    /// <summary>The accuracy the evaluation must reach in percent, shown under its name (default 90).</summary>
    public static readonly StyledProperty<double> TargetProperty =
        AvaloniaProperty.Register<ModelTraining, double>(nameof(Target), 90);

    /// <summary>
    /// The step (0 to 4) shown before the story plays and where playing starts; −1 (the default) shows the finished,
    /// evaluated model and plays from the start. Under reduced motion it picks the still frame shown.
    /// </summary>
    public static readonly StyledProperty<int> DefaultStepProperty =
        AvaloniaProperty.Register<ModelTraining, int>(nameof(DefaultStep), -1, coerce: static (_, v) => Math.Clamp(v, -1, StepTotal - 1));

    /// <summary>Shows the sentence under the step bar that explains the current step (default true).</summary>
    public static readonly StyledProperty<bool> IsCaptionVisibleProperty =
        AvaloniaProperty.Register<ModelTraining, bool>(nameof(IsCaptionVisible), true);

    /// <summary>
    /// The colour of the signals, the error dash, the learned connections, the loss curve and the current step; null (the
    /// default) uses the accent. The passed evaluation keeps the success colour.
    /// </summary>
    public static readonly StyledProperty<IBrush?> AccentProperty =
        AvaloniaProperty.Register<ModelTraining, IBrush?>(nameof(Accent));

    /// <summary>The diagram's accessible description; null (the default) builds one from the classes and the numbers.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ModelTraining, string?>(nameof(Description));

    /// <summary>Where the run is.</summary>
    public static readonly DirectProperty<ModelTraining, ModelTrainingPhase> PhaseProperty =
        AvaloniaProperty.RegisterDirect<ModelTraining, ModelTrainingPhase>(nameof(Phase), o => o.Phase);

    /// <summary>The training step (batch) the run is on, from 1; 0 before the first.</summary>
    public static readonly DirectProperty<ModelTraining, int> IterationProperty =
        AvaloniaProperty.RegisterDirect<ModelTraining, int>(nameof(Iteration), o => o.Iteration);

    /// <summary>The loss the readout shows; NaN before the first batch has been measured.</summary>
    public static readonly DirectProperty<ModelTraining, double> CurrentLossProperty =
        AvaloniaProperty.RegisterDirect<ModelTraining, double>(nameof(CurrentLoss), o => o.CurrentLoss);

    /// <summary>The step the step bar marks as current (0 batch, 1 forward pass, 2 backward pass, 3 epochs, 4 evaluate).</summary>
    public static readonly DirectProperty<ModelTraining, int> CurrentStepProperty =
        AvaloniaProperty.RegisterDirect<ModelTraining, int>(nameof(CurrentStep), o => o.CurrentStep);

    /// <summary>Raised when a training step's weight update finishes, with the step and the loss after it.</summary>
    public static readonly RoutedEvent<ModelTrainingIterationEventArgs> IterationCompletedEvent =
        RoutedEvent.Register<ModelTraining, ModelTrainingIterationEventArgs>(nameof(IterationCompleted), RoutingStrategies.Bubble);

    internal static readonly StyledProperty<IBrush?> CaptionStageInkProperty =
        AvaloniaProperty.Register<ModelTraining, IBrush?>(nameof(CaptionStageInk));

    private readonly ModelTrainingTimeline _timeline = new();
    private readonly ModelTrainingStepButton[] _steps = new ModelTrainingStepButton[StepTotal];
    private readonly string?[] _epochTexts = new string?[64];
    private ModelTrainingStage? _stage;
    private ModelTrainingChart? _chart;
    private ModelTrainingEvalMark? _evalMark;
    private ModelTrainingBodyPanel? _body;
    private Grid? _stepBar;
    private TextBlock? _caption;
    private TextBlock? _lossLabel;
    private TextBlock? _lossValue;
    private TextBlock? _lossMeta;
    private TextBlock? _epochLabel;
    private TextBlock? _epochValue;
    private TextBlock? _epochMeta;
    private TextBlock? _evalName;
    private TextBlock? _evalTarget;
    private TextBlock? _evalScore;
    private TextBlock? _tagPending;
    private TextBlock? _tagScoring;
    private TextBlock? _tagPassed;
    private Control? _playButton;
    private ModelTrainingPhase _phase;
    private int _iteration;
    private double _currentLoss = double.NaN;
    private int _currentStep = StepTotal - 1;
    private int _stillStep = -1;
    private int _captionStep = -1;
    private bool _captionCompact;
    private int _timelineVersion;
    private int _shownEpoch = -1;
    private double _shownLoss = double.NaN;
    private double _shownScore = double.NaN;
    private int _shownEval = -1;
    private bool _compact;
    private bool _seeking;
    private bool _playingBeforeEnd;
    private bool _loopPending;
    private bool _hovered;
    private bool _rebuildPending;
    private bool _defaultApplied;
    private bool _moved;
    private double _rate = 1;
    private double _rateVelocity;
    private double _lastWall;

    static ModelTraining()
    {
        TitleProperty.OverrideDefaultValue<ModelTraining>("Model training");
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<ModelTraining>(AutomationControlType.Group);
    }

    /// <summary>Creates the illustration with its sample task (two classes of photos, six training steps, twelve epochs).</summary>
    public ModelTraining()
    {
        AutomationProperties.SetName(this, Title);
        for (var i = 0; i < StepTotal; i++)
        {
            var button = new ModelTrainingStepButton(i);
            var index = i;
            button.Click += (_, _) => GoToStep(index);
            _steps[i] = button;
        }
        BuildTimeline();
    }

    /// <inheritdoc cref="StepCountProperty"/>
    public int StepCount { get => GetValue(StepCountProperty); set => SetValue(StepCountProperty, value); }

    /// <inheritdoc cref="BatchSizeProperty"/>
    public int BatchSize { get => GetValue(BatchSizeProperty); set => SetValue(BatchSizeProperty, value); }

    /// <inheritdoc cref="LearningRateProperty"/>
    public double LearningRate { get => GetValue(LearningRateProperty); set => SetValue(LearningRateProperty, value); }

    /// <inheritdoc cref="LossValuesProperty"/>
    public IReadOnlyList<double>? LossValues { get => GetValue(LossValuesProperty); set => SetValue(LossValuesProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ModelTrainingLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="ClassNamesProperty"/>
    public IReadOnlyList<string>? ClassNames { get => GetValue(ClassNamesProperty); set => SetValue(ClassNamesProperty, value); }

    /// <inheritdoc cref="InputCountProperty"/>
    public int InputCount { get => GetValue(InputCountProperty); set => SetValue(InputCountProperty, value); }

    /// <inheritdoc cref="HiddenUnitsProperty"/>
    public IReadOnlyList<int>? HiddenUnits { get => GetValue(HiddenUnitsProperty); set => SetValue(HiddenUnitsProperty, value); }

    /// <inheritdoc cref="SamplesProperty"/>
    public int Samples { get => GetValue(SamplesProperty); set => SetValue(SamplesProperty, value); }

    /// <inheritdoc cref="HeldOutSamplesProperty"/>
    public int HeldOutSamples { get => GetValue(HeldOutSamplesProperty); set => SetValue(HeldOutSamplesProperty, value); }

    /// <inheritdoc cref="EpochsProperty"/>
    public int Epochs { get => GetValue(EpochsProperty); set => SetValue(EpochsProperty, value); }

    /// <inheritdoc cref="LossFromProperty"/>
    public double LossFrom { get => GetValue(LossFromProperty); set => SetValue(LossFromProperty, value); }

    /// <inheritdoc cref="LossToProperty"/>
    public double LossTo { get => GetValue(LossToProperty); set => SetValue(LossToProperty, value); }

    /// <inheritdoc cref="AccuracyProperty"/>
    public double Accuracy { get => GetValue(AccuracyProperty); set => SetValue(AccuracyProperty, value); }

    /// <inheritdoc cref="TargetProperty"/>
    public double Target { get => GetValue(TargetProperty); set => SetValue(TargetProperty, value); }

    /// <inheritdoc cref="DefaultStepProperty"/>
    public int DefaultStep { get => GetValue(DefaultStepProperty); set => SetValue(DefaultStepProperty, value); }

    /// <inheritdoc cref="IsCaptionVisibleProperty"/>
    public bool IsCaptionVisible { get => GetValue(IsCaptionVisibleProperty); set => SetValue(IsCaptionVisibleProperty, value); }

    /// <inheritdoc cref="AccentProperty"/>
    public IBrush? Accent { get => GetValue(AccentProperty); set => SetValue(AccentProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="PhaseProperty"/>
    public ModelTrainingPhase Phase { get => _phase; private set => SetAndRaise(PhaseProperty, ref _phase, value); }

    /// <inheritdoc cref="IterationProperty"/>
    public int Iteration { get => _iteration; private set => SetAndRaise(IterationProperty, ref _iteration, value); }

    /// <inheritdoc cref="CurrentLossProperty"/>
    public double CurrentLoss { get => _currentLoss; private set => SetAndRaise(CurrentLossProperty, ref _currentLoss, value); }

    /// <inheritdoc cref="CurrentStepProperty"/>
    public int CurrentStep { get => _currentStep; private set => SetAndRaise(CurrentStepProperty, ref _currentStep, value); }

    /// <inheritdoc cref="IterationCompletedEvent"/>
    public event EventHandler<ModelTrainingIterationEventArgs>? IterationCompleted
    {
        add => AddHandler(IterationCompletedEvent, value);
        remove => RemoveHandler(IterationCompletedEvent, value);
    }

    /// <summary>The colour of the step name that starts the caption on compact cards.</summary>
    internal IBrush? CaptionStageInk { get => GetValue(CaptionStageInkProperty); set => SetValue(CaptionStageInkProperty, value); }

    internal ModelTrainingTimeline Timeline => _timeline;

    internal int TimelineVersion => _timelineVersion;

    internal ModelTrainingStepButton[] StepButtons => _steps;

    internal double StoryTime => Now;

    internal bool IsCompact => _compact;

    internal double PaceRate => _rate;

    /// <summary>The run time the diagram shows: the still of the default step before the story has started.</summary>
    internal double RenderTime
    {
        get
        {
            if (IsAtStart)
            {
                return _timeline.StillTime(DefaultStep >= 0 ? DefaultStep : StepTotal - 1);
            }
            return Math.Min(Now, _timeline.Length);
        }
    }

    /// <summary>True before the story has first played, stepped or jumped (the illustration then shows its still).</summary>
    private bool IsAtStart => !_moved && Now <= 0 && !IsPlaying && !IsStepping;

    /// <summary>The layer sizes: inputs, the two hidden layers, two outputs.</summary>
    internal int[] LayerSizes
    {
        get
        {
            var hidden = HiddenUnits ?? s_sampleHidden;
            var first = hidden.Count > 0 ? Math.Clamp(hidden[0], 2, 6) : 4;
            var second = hidden.Count > 1 ? Math.Clamp(hidden[1], 2, 6) : first;
            return [InputCount, first, second, 2];
        }
    }

    internal int WeightSeed => 1;

    /// <summary>The caption of the current step (tests).</summary>
    internal string CaptionText => CaptionFor(CurrentStep);

    private protected override double ClockRate => ArcMotion.IsFull(this) ? _rate : 1;

    private protected override double LoopDelay => 0;

    private protected override double StepSpeed => 2 * Math.Max(1, Speed);

    /// <summary>The name of class <paramref name="index"/> (0 or 1).</summary>
    internal string ClassAt(int index)
    {
        var classes = ClassNames;
        if (classes is not null && index < classes.Count && !string.IsNullOrEmpty(classes[index]))
        {
            return classes[index];
        }
        return s_sampleClasses[Math.Clamp(index, 0, 1)];
    }

    /// <summary>The epoch count as text (cached).</summary>
    internal string EpochText(int epoch)
    {
        if (epoch >= 0 && epoch < _epochTexts.Length)
        {
            return _epochTexts[epoch] ??= epoch.ToString(CultureInfo.InvariantCulture);
        }
        return epoch.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Jumps to step <paramref name="step"/> (0 batch … 4 evaluate): while the story plays it goes on from that step's start;
    /// otherwise it shows that step's still frame.
    /// </summary>
    public void GoToStep(int step)
    {
        step = Math.Clamp(step, 0, StepTotal - 1);
        _defaultApplied = true;
        if (IsPlaying && ArcMotion.IsFull(this))
        {
            _stillStep = -1;
            SeekQuietly(_timeline.StageStart(step));
        }
        else
        {
            if (IsPlaying)
            {
                Pause();
            }
            _stillStep = step;
            SeekQuietly(_timeline.StillTime(step));
        }
        UpdateSteps();
        Announce(CaptionFor(step));
    }

    /// <summary>The detail a mouse resting on <paramref name="region"/> shows.</summary>
    internal string? DetailOf(ModelTrainingRegion region)
    {
        var labels = Labels;
        var sizes = LayerSizes;
        return region switch
        {
            ModelTrainingRegion.TrainingSet => labels.TrainingDetail(Samples, labels.Unit, BatchSize),
            ModelTrainingRegion.HeldOutSet => labels.HeldOutDetail(HeldOutSamples, labels.Unit),
            ModelTrainingRegion.Layer0 => labels.InputDetail(sizes[0]),
            ModelTrainingRegion.Layer1 => labels.HiddenDetail(sizes[1], sizes[0] * sizes[1]),
            ModelTrainingRegion.Layer2 => labels.HiddenDetail(sizes[2], sizes[1] * sizes[2]),
            ModelTrainingRegion.Layer3 => labels.OutputDetail(ClassAt(0), ClassAt(1)),
            _ => null
        };
    }

    internal void OnHoverChanged(bool hovered)
    {
        if (_hovered == hovered)
        {
            return;
        }
        _hovered = hovered;
        if (IsPlaying)
        {
            RequestFrames(1.2);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _stage = scope.Find<ModelTrainingStage>(StagePart);
        _chart = scope.Find<ModelTrainingChart>("PART_Chart");
        _evalMark = scope.Find<ModelTrainingEvalMark>("PART_EvalMark");
        _body = scope.Find<ModelTrainingBodyPanel>("PART_Body");
        _caption = scope.Find<TextBlock>(CaptionPart);
        _lossLabel = scope.Find<TextBlock>("PART_LossLabel");
        _lossValue = scope.Find<TextBlock>("PART_LossValue");
        _lossMeta = scope.Find<TextBlock>("PART_LossMeta");
        _epochLabel = scope.Find<TextBlock>("PART_EpochLabel");
        _epochValue = scope.Find<TextBlock>("PART_EpochValue");
        _epochMeta = scope.Find<TextBlock>("PART_EpochMeta");
        _evalName = scope.Find<TextBlock>("PART_EvalName");
        _evalTarget = scope.Find<TextBlock>("PART_EvalTarget");
        _evalScore = scope.Find<TextBlock>("PART_EvalScore");
        _tagPending = scope.Find<TextBlock>("PART_TagPending");
        _tagScoring = scope.Find<TextBlock>("PART_TagScoring");
        _tagPassed = scope.Find<TextBlock>("PART_TagPassed");
        _playButton = scope.Find<Control>(PlayButtonPart);
        if (_stepBar is not null)
        {
            _stepBar.Children.Clear();
        }
        _stepBar = scope.Find<Grid>(StepsPart);
        if (_stepBar is not null)
        {
            _stepBar.ColumnDefinitions.Clear();
            for (var i = 0; i < StepTotal; i++)
            {
                _stepBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                var button = _steps[i];
                (button.Parent as Panel)?.Children.Remove(button);
                Grid.SetColumn(button, i);
                _stepBar.Children.Add(button);
            }
        }
        _stage?.Attach(this);
        _chart?.Attach(this);
        _evalMark?.Attach(this);
        UpdateLabels();
        UpdateMotionChrome();
        UpdateSizing(Bounds.Width);
        Refresh(force: true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == StepCountProperty || property == LossValuesProperty || property == LearningRateProperty ||
            property == EpochsProperty || property == LossFromProperty || property == LossToProperty ||
            property == AccuracyProperty || property == TargetProperty)
        {
            BuildTimeline();
            ScheduleRebuild();
        }
        else if (property == InputCountProperty || property == HiddenUnitsProperty)
        {
            _stage?.NetworkChanged();
            UpdateDescription();
        }
        else if (property == LabelsProperty || property == ClassNamesProperty || property == BatchSizeProperty ||
                 property == SamplesProperty || property == HeldOutSamplesProperty || property == DescriptionProperty)
        {
            UpdateLabels();
            Refresh(force: true);
        }
        else if (property == TitleProperty)
        {
            AutomationProperties.SetName(this, Title);
            UpdateDescription();
        }
        else if (property == AccentProperty)
        {
            _stage?.InvalidateVisual();
            _chart?.InvalidateVisual();
            _evalMark?.InvalidateVisual();
        }
        else if (property == CaptionStageInkProperty)
        {
            UpdateCaption(CurrentStep, force: true);
        }
        else if (property == DefaultStepProperty)
        {
            Refresh(force: true);
        }
        else if (property == IsCaptionVisibleProperty)
        {
            if (_caption is not null)
            {
                _caption.IsVisible = IsCaptionVisible;
            }
        }
        else if (property == ArcMotion.LevelProperty)
        {
            UpdateMotionChrome();
        }
        else if (property == BoundsProperty)
        {
            UpdateSizing(change.GetNewValue<Rect>().Width);
        }
    }

    private protected override void BuildStory(ExplainerScript script)
    {
        var timeline = _timeline;
        var n = timeline.Steps;
        script.At(0, () => EnterPhase(ModelTrainingPhase.Batch, 1));
        script.At(ModelTrainingTimeline.ForwardStart, () => EnterPhase(ModelTrainingPhase.Forward, 1), isStop: true);
        script.At(timeline.FirstMeasure, () =>
        {
            EnterPhase(ModelTrainingPhase.Loss, 1);
            CurrentLoss = timeline.LossAt(0);
        });
        script.At(ModelTrainingTimeline.BackwardStart, () => EnterPhase(ModelTrainingPhase.Backward, 1), isStop: true);
        script.At(timeline.UpdateEnd(1) - 0.8, () => EnterPhase(ModelTrainingPhase.Update, 1));
        script.At(ModelTrainingTimeline.EpochsStart, () => EnterPhase(ModelTrainingPhase.Epochs, 2), isStop: true);
        for (var k = 1; k <= n; k++)
        {
            var step = k;
            if (k >= 2)
            {
                script.At(ModelTrainingTimeline.EpochsStart + (k - 2) * ModelTrainingTimeline.CyclePeriod, () => Iteration = step);
            }
            script.At(timeline.UpdateEnd(k), () => CompleteStep(step));
        }
        script.At(timeline.EvaluateStart, () => EnterPhase(ModelTrainingPhase.Evaluate, n), isStop: true);
        script.At(timeline.Length, () => EnterPhase(ModelTrainingPhase.Done, n), isStop: true);
        script.Length = timeline.Length;
    }

    private void EnterPhase(ModelTrainingPhase phase, int iteration)
    {
        Phase = phase;
        Iteration = iteration;
        if (!_seeking && phase is ModelTrainingPhase.Batch or ModelTrainingPhase.Forward or ModelTrainingPhase.Backward or ModelTrainingPhase.Epochs or ModelTrainingPhase.Evaluate)
        {
            AnnounceBeat(CaptionFor(_timeline.StageAt(Now)));
        }
    }

    private void CompleteStep(int step)
    {
        var loss = _timeline.LossAt(step / (double)_timeline.Steps);
        CurrentLoss = loss;
        if (!_seeking)
        {
            RaiseEvent(new ModelTrainingIterationEventArgs(IterationCompletedEvent, this, step, loss));
        }
    }

    private protected override void ResetScene()
    {
        Phase = ModelTrainingPhase.Ready;
        Iteration = 0;
        CurrentLoss = double.NaN;
        Refresh(force: true);
    }

    private protected override void OnTimeChanged()
    {
        var wall = WallTime;
        var dt = Math.Clamp(wall - _lastWall, 0, 0.1);
        _lastWall = wall;
        StepRate(dt);
        if (IsPlaying)
        {
            _stillStep = -1;
        }
        _playingBeforeEnd = IsPlaying;
        if (Now > 0)
        {
            _moved = true;
        }
        Refresh(force: false);
    }

    private protected override void OnPlaybackChanged()
    {
        if (IsPlaying || IsStepping)
        {
            _moved = true;
        }
        if (IsPlaying)
        {
            _stillStep = -1;
            if (!_defaultApplied && Now <= 0 && DefaultStep > 0)
            {
                // Playing starts from the default step.
                _defaultApplied = true;
                Dispatcher.UIThread.Post(() =>
                {
                    if (IsPlaying && Now < _timeline.StageStart(DefaultStep))
                    {
                        SeekQuietly(_timeline.StageStart(DefaultStep));
                    }
                }, DispatcherPriority.Background);
            }
        }
        Refresh(force: true);
    }

    private protected override void OnFinished()
    {
        Refresh(force: true);
        if (!_playingBeforeEnd || IsLooping || _loopPending || !ArcMotion.IsFull(this) || Speed <= 0)
        {
            return;
        }
        // A playing run loops: the relax at the start of the next run turns the finished model back into an untrained one.
        _loopPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _loopPending = false;
            if (IsFinished && IsAttached && !IsPlaying && _stillStep < 0)
            {
                Restart();
            }
        }, DispatcherPriority.Send);
    }

    private void SeekQuietly(double time)
    {
        _moved = true;
        _seeking = true;
        try
        {
            Seek(time);
        }
        finally
        {
            _seeking = false;
        }
        _playingBeforeEnd = IsPlaying;
        Refresh(force: true);
    }

    private void BuildTimeline()
    {
        _timeline.Build(StepCount, Epochs, LossFrom, LossTo, LossValues, Accuracy, Target, LearningRate);
        _timelineVersion++;
        _shownEpoch = -1;
        _shownLoss = double.NaN;
        _shownScore = double.NaN;
        _shownEval = -1;
        UpdateDescription();
    }

    private void ScheduleRebuild()
    {
        if (_rebuildPending)
        {
            return;
        }
        _rebuildPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _rebuildPending = false;
            Rebuild();
            Refresh(force: true);
        }, DispatcherPriority.Background);
    }

    /// <summary>Moves the hover pace toward its target on a critically damped spring.</summary>
    private void StepRate(double dt)
    {
        var target = _hovered ? HoverRate : 1;
        if (dt <= 0 || (Math.Abs(_rate - target) < 0.0005 && Math.Abs(_rateVelocity) < 0.0005))
        {
            _rate = dt <= 0 ? _rate : target;
            _rateVelocity = dt <= 0 ? _rateVelocity : 0;
            return;
        }
        const double omega = 9;
        var steps = Math.Max(1, (int)Math.Ceiling(dt / 0.008));
        var h = dt / steps;
        for (var i = 0; i < steps; i++)
        {
            var a = omega * omega * (target - _rate) - 2 * omega * _rateVelocity;
            _rateVelocity += a * h;
            _rate += _rateVelocity * h;
        }
    }

    // ---- Presentation ---------------------------------------------------------------------------------------------

    private void Refresh(bool force)
    {
        var t = RenderTime;
        var timeline = _timeline;
        var step = timeline.StageAt(t);
        CurrentStep = step;
        UpdateSteps();
        UpdateReadouts(t, force);
        UpdateCaption(step, force);
        _stage?.InvalidateVisual();
        _chart?.InvalidateVisual();
        _evalMark?.InvalidateVisual();
    }

    private void UpdateSteps()
    {
        var t = RenderTime;
        var timeline = _timeline;
        var step = timeline.StageAt(t);
        var still = IsAtStart || _stillStep >= 0 || IsFinished;
        for (var i = 0; i < StepTotal; i++)
        {
            var button = _steps[i];
            double progress;
            int state;
            if (i < step)
            {
                state = 2;
                progress = 1;
            }
            else if (i == step)
            {
                state = 1;
                progress = still ? 1 : timeline.StageProgress(i, t);
            }
            else
            {
                state = 0;
                progress = 0;
            }
            // The relax at the start of a run empties the previous run's bars.
            if (!still && t < 0.7)
            {
                var previous = 1 - ModelTrainingEase.InOutCubic(t / 0.7);
                progress = Math.Max(progress, previous);
                if (i > 0)
                {
                    state = previous > 0.01 ? 2 : 0;
                }
            }
            button.SetState(state);
            button.Progress = progress;
        }
    }

    private void UpdateReadouts(double t, bool force)
    {
        var timeline = _timeline;
        var labels = Labels;
        var measured = timeline.IsMeasured(t);
        PseudoClasses.Set(UntrainedPseudoClass, !measured);
        var epoch = timeline.EpochAt(t);
        if (force || epoch != _shownEpoch)
        {
            _shownEpoch = epoch;
            SetText(_epochValue, EpochText(epoch));
        }
        var loss = measured ? timeline.LossAt(timeline.HeadAt(t)) : timeline.LossFrom;
        var rounded = Math.Round(loss, 2);
        if (force || rounded != _shownLoss)
        {
            _shownLoss = rounded;
            SetText(_lossValue, rounded.ToString("0.00", CultureInfo.InvariantCulture));
            SetText(_lossMeta, measured ? labels.StartedAt(timeline.LossFrom) : labels.NoBatch);
        }
        var evalState = timeline.EvalStateAt(t);
        var score = Math.Round(timeline.ScoreAt(t), 1);
        if (force || evalState != _shownEval || score != _shownScore)
        {
            _shownEval = evalState;
            _shownScore = score;
            SetText(_evalScore, evalState == 0 ? string.Empty : score.ToString("0.0", CultureInfo.InvariantCulture) + "%");
            var passed = timeline.PassesTarget;
            PseudoClasses.Set(EvalPendingPseudoClass, evalState == 0);
            PseudoClasses.Set(EvalScoringPseudoClass, evalState == 1);
            PseudoClasses.Set(EvalPassedPseudoClass, evalState == 2 && passed);
            PseudoClasses.Set(EvalFailedPseudoClass, evalState == 2 && !passed);
            SetText(_tagPassed, passed ? labels.Passed : labels.BelowTarget);
        }
    }

    private void UpdateCaption(int step, bool force)
    {
        if (_caption is null || (!force && step == _captionStep && _compact == _captionCompact))
        {
            return;
        }
        _captionStep = step;
        _captionCompact = _compact;
        var text = CaptionFor(step);
        var inlines = _caption.Inlines ??= new InlineCollection();
        inlines.Clear();
        if (_compact)
        {
            inlines.Add(new Run(Labels.StepAt(step) + ". ") { FontWeight = FontWeight.Medium, Foreground = CaptionStageInk });
        }
        inlines.Add(new Run(text));
    }

    private string CaptionFor(int step)
    {
        var labels = Labels;
        var timeline = _timeline;
        return step switch
        {
            0 => labels.BatchCaption(BatchSize),
            1 => labels.ForwardCaption,
            2 => labels.BackwardCaption,
            3 => labels.EpochsCaption(timeline.Epochs, timeline.LossFrom, timeline.LossTo),
            _ => labels.EvaluateCaption(timeline.Accuracy, timeline.Target, timeline.PassesTarget)
        };
    }

    private void UpdateLabels()
    {
        var labels = Labels;
        SetText(_lossLabel, labels.LossCurve);
        SetText(_epochLabel, labels.Epoch);
        SetText(_epochMeta, labels.EpochOf(_timeline.Epochs));
        SetText(_evalName, labels.Evaluation);
        SetText(_evalTarget, labels.TargetReadout(_timeline.Target));
        SetText(_tagPending, labels.Pending);
        SetText(_tagScoring, labels.Scoring);
        SetText(_tagPassed, _timeline.PassesTarget ? labels.Passed : labels.BelowTarget);
        for (var i = 0; i < StepTotal; i++)
        {
            var name = labels.StepAt(i);
            _steps[i].Text = name;
            AutomationProperties.SetName(_steps[i], labels.StepName(i + 1, StepTotal, name));
        }
        if (_stepBar is not null)
        {
            AutomationProperties.SetName(_stepBar, labels.StepsName);
        }
        _captionStep = -1;
        UpdateDescription();
    }

    private void UpdateDescription()
    {
        if (_stage is null)
        {
            return;
        }
        AutomationProperties.SetName(_stage, Description ?? BuildDescription());
    }

    /// <summary>The diagram's default accessible description, from the classes and the numbers.</summary>
    internal string BuildDescription()
    {
        var timeline = _timeline;
        var sizes = LayerSizes;
        var unit = Labels.Unit;
        var verdict = timeline.PassesTarget ? "so it passes" : "so it falls short";
        return string.Create(CultureInfo.InvariantCulture,
            $"{Title}: a network with {sizes[0]} inputs, hidden layers of {sizes[1]} and {sizes[2]} units and two outputs, " +
            $"{ClassAt(0)} and {ClassAt(1)}, learns from batches of {BatchSize} {unit}. Forward passes give predictions, the " +
            $"error flows back and strengthens the useful connections, and over {timeline.Epochs} epochs the loss drops from " +
            $"{timeline.LossFrom:0.00} to {timeline.LossTo:0.00}. On held-out data it scores {timeline.Accuracy:0.0}% against " +
            $"a target of {timeline.Target:0.#}%, {verdict}.");
    }

    private void UpdateMotionChrome()
    {
        var full = ArcMotion.IsFull(this);
        PseudoClasses.Set(ReducedPseudoClass, !full);
        if (_playButton is not null)
        {
            _playButton.IsVisible = full;
        }
    }

    private void UpdateSizing(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var compact = width < 600;
        if (compact != _compact)
        {
            _compact = compact;
            PseudoClasses.Set(CompactPseudoClass, compact);
            foreach (var button in _steps)
            {
                button.SetCompact(compact);
            }
            _captionStep = -1;
            UpdateCaption(CurrentStep, force: true);
        }
        if (_body is not null)
        {
            _body.RowGap = compact ? 20 : 24;
        }
    }

    private static void SetText(TextBlock? block, string? text)
    {
        if (block is not null && block.Text != text)
        {
            block.Text = text;
        }
    }
}
