using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// An animated explainer of a model's training loop. Each step draws a batch from the data, runs it forward through a
/// small network (a wave of activations, left to right), compares the prediction with the right answer to get the loss
/// (bars show the probability the model gave each class, and the loss is −ln of the right one), runs the loss backward
/// (a wave of gradients, right to left) and nudges every weight (the connections change thickness and color). The loss
/// lands on a curve that falls step by step. The first step stops at every phase with a caption; later steps repeat the
/// loop faster. Play, pause, step, restart and a 0.5× / 1× / 2× speed control drive it; under reduced motion it opens on
/// its end and steps apply in place. The network and the numbers are illustrative.
/// </summary>
[TemplatePart(StagePart, typeof(ModelTrainingStage))]
[TemplatePart(ExplainerChapterKit.StatusPart, typeof(ExplainerStatus))]
[TemplatePart(ExplainerChapterKit.CaptionPart, typeof(ArcContentSwap))]
[TemplatePart(ExplainerChapterKit.SpeedPart, typeof(SegmentedControl))]
public class ModelTraining : Explainer
{
    /// <summary>The drawn data, network, prediction and loss curve.</summary>
    public const string StagePart = "PART_Stage";

    /// <summary>How many training steps the story runs (2 to 20, default 6).</summary>
    public static readonly StyledProperty<int> StepCountProperty =
        AvaloniaProperty.Register<ModelTraining, int>(nameof(StepCount), 6, coerce: static (_, v) => Math.Clamp(v, 2, 20));

    /// <summary>The examples per batch, named in the batch caption (default 32).</summary>
    public static readonly StyledProperty<int> BatchSizeProperty =
        AvaloniaProperty.Register<ModelTraining, int>(nameof(BatchSize), 32, coerce: static (_, v) => Math.Max(1, v));

    /// <summary>The learning rate, named in the readout and the update caption; a larger rate moves the weights further each step (default 0.05).</summary>
    public static readonly StyledProperty<double> LearningRateProperty =
        AvaloniaProperty.Register<ModelTraining, double>(nameof(LearningRate), 0.05,
            coerce: static (_, v) => double.IsFinite(v) ? Math.Clamp(v, 0.0001, 1) : 0.05);

    /// <summary>
    /// The loss of each step, in order; null (the default) uses a sample curve that falls from 2.30 and levels off. Extra
    /// values are ignored, missing ones continue the sample curve. Values are clamped to 0.01 – 5.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> LossValuesProperty =
        AvaloniaProperty.Register<ModelTraining, IReadOnlyList<double>?>(nameof(LossValues));

    /// <summary>Every word the card shows or announces.</summary>
    public static readonly StyledProperty<ModelTrainingLabels> LabelsProperty =
        AvaloniaProperty.Register<ModelTraining, ModelTrainingLabels>(nameof(Labels), new ModelTrainingLabels());

    /// <summary>The phase of the current step.</summary>
    public static readonly DirectProperty<ModelTraining, ModelTrainingPhase> PhaseProperty =
        AvaloniaProperty.RegisterDirect<ModelTraining, ModelTrainingPhase>(nameof(Phase), o => o.Phase);

    /// <summary>The current step, from 1; 0 before the first.</summary>
    public static readonly DirectProperty<ModelTraining, int> IterationProperty =
        AvaloniaProperty.RegisterDirect<ModelTraining, int>(nameof(Iteration), o => o.Iteration);

    /// <summary>The last loss measured; NaN before the first.</summary>
    public static readonly DirectProperty<ModelTraining, double> CurrentLossProperty =
        AvaloniaProperty.RegisterDirect<ModelTraining, double>(nameof(CurrentLoss), o => o.CurrentLoss);

    /// <summary>Raised when a step finishes its update, with the step and its loss.</summary>
    public static readonly RoutedEvent<ModelTrainingIterationEventArgs> IterationCompletedEvent =
        RoutedEvent.Register<ModelTraining, ModelTrainingIterationEventArgs>(nameof(IterationCompleted), RoutingStrategies.Bubble);

    internal static readonly StyledProperty<Thickness> BodyPaddingProperty =
        AvaloniaProperty.Register<ModelTraining, Thickness>(nameof(BodyPadding), new Thickness(24, 24, 24, 0));

    internal static readonly StyledProperty<Thickness> FooterPaddingProperty =
        AvaloniaProperty.Register<ModelTraining, Thickness>(nameof(FooterPadding), new Thickness(24, 16, 24, 24));

    /// <summary>The network's layer sizes: inputs, hidden units, classes.</summary>
    internal static readonly int[] LayerSizes = [4, 5, 3];

    /// <summary>The class that is the right answer for every batch (the middle one).</summary>
    internal const int TargetClass = 1;

    /// <summary>Phase lengths in seconds at speed 1 for the first step: batch, forward, loss, backward, update.</summary>
    internal static readonly double[] PhaseSeconds = [0.7, 1.3, 0.9, 1.3, 0.9];

    /// <summary>How much faster the steps after the first run.</summary>
    internal const double RepeatPace = 0.45;

    private readonly ModelTrainingScene _scene = new();
    private readonly ExplainerChapterKit _kit;
    private ModelTrainingStage? _stage;
    private double[] _losses = [];
    private ModelTrainingPhase _phase;
    private int _iteration;
    private double _currentLoss = double.NaN;
    private string? _caption;
    private string? _sentence;
    private bool _rebuildPending;
    private bool _pauseCheckPending;

    static ModelTraining()
    {
        TitleProperty.OverrideDefaultValue<ModelTraining>("Model training");
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<ModelTraining>(AutomationControlType.Group);
    }

    /// <summary>Creates the explainer with a six-step sample run.</summary>
    public ModelTraining()
    {
        _kit = new ExplainerChapterKit(this);
        AutomationProperties.SetName(this, Title);
    }

    /// <summary>How many training steps the story runs (2 to 20, default 6).</summary>
    public int StepCount { get => GetValue(StepCountProperty); set => SetValue(StepCountProperty, value); }

    /// <summary>The examples per batch, named in the batch caption (default 32).</summary>
    public int BatchSize { get => GetValue(BatchSizeProperty); set => SetValue(BatchSizeProperty, value); }

    /// <summary>The learning rate, named in the readout and the update caption; a larger rate moves the weights further each step (default 0.05).</summary>
    public double LearningRate { get => GetValue(LearningRateProperty); set => SetValue(LearningRateProperty, value); }

    /// <summary>
    /// The loss of each step, in order; null (the default) uses a sample curve that falls from 2.30 and levels off. Extra
    /// values are ignored, missing ones continue the sample curve. Values are clamped to 0.01 – 5.
    /// </summary>
    public IReadOnlyList<double>? LossValues { get => GetValue(LossValuesProperty); set => SetValue(LossValuesProperty, value); }

    /// <summary>Every word the card shows or announces.</summary>
    public ModelTrainingLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>The phase of the current step.</summary>
    public ModelTrainingPhase Phase { get => _phase; private set => SetAndRaise(PhaseProperty, ref _phase, value); }

    /// <summary>The current step, from 1; 0 before the first.</summary>
    public int Iteration { get => _iteration; private set => SetAndRaise(IterationProperty, ref _iteration, value); }

    /// <summary>The last loss measured; NaN before the first.</summary>
    public double CurrentLoss { get => _currentLoss; private set => SetAndRaise(CurrentLossProperty, ref _currentLoss, value); }

    /// <summary>Raised when a step finishes its update, with the step and its loss.</summary>
    public event EventHandler<ModelTrainingIterationEventArgs>? IterationCompleted
    {
        add => AddHandler(IterationCompletedEvent, value);
        remove => RemoveHandler(IterationCompletedEvent, value);
    }

    internal Thickness BodyPadding { get => GetValue(BodyPaddingProperty); set => SetValue(BodyPaddingProperty, value); }

    internal Thickness FooterPadding { get => GetValue(FooterPaddingProperty); set => SetValue(FooterPaddingProperty, value); }

    internal ModelTrainingScene Scene => _scene;

    internal double StoryTime => Now;

    internal bool Animates => IsTravelEnabled;

    /// <summary>The losses the story plays (one per step).</summary>
    internal IReadOnlyList<double> Losses => _losses;

    internal string? CaptionText => _kit.CaptionText;

    internal string? StatusText => _kit.StatusText;

    private protected override double StepSpeed => 2 * Math.Max(1, Speed);

    /// <summary>The sample loss curve: an exponential fall from 2.30 to about 0.4 with a little stable noise.</summary>
    internal static double SampleLoss(int step)
    {
        var smooth = 0.38 + (2.3 - 0.38) * Math.Exp(-0.55 * step);
        var noise = Math.Sin(step * 2.7 + 0.4) * 0.07 * Math.Exp(-0.15 * step);
        return Math.Round(smooth + (step == 0 ? 0 : noise), 3);
    }

    /// <summary>The probability the model gives the right class for a loss (cross-entropy: p = e^−loss).</summary>
    internal static double TargetProbability(double loss) => Math.Exp(-loss);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _kit.Apply(e.NameScope);
        _stage = e.NameScope.Find<ModelTrainingStage>(StagePart);
        _stage?.Attach(this);
        _kit.SetSpeedLabel(Labels.Speed);
        UpdateStatus(force: true);
        _kit.SetCaption(CurrentCaption(), animate: false);
        UpdateSizing(Bounds.Width);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StepCountProperty || change.Property == LossValuesProperty ||
            change.Property == LearningRateProperty || change.Property == BatchSizeProperty)
        {
            ScheduleRebuild();
        }
        else if (change.Property == LabelsProperty)
        {
            _kit.SetSpeedLabel(Labels.Speed);
            UpdateStatus(force: true);
            _kit.SetCaption(CurrentCaption(), animate: false);
            _stage?.OnLabelsChanged();
        }
        else if (change.Property == TitleProperty)
        {
            AutomationProperties.SetName(this, Title);
        }
        else if (change.Property == SpeedProperty)
        {
            _kit.UpdateSpeed();
        }
        else if (change.Property == IsSteppingProperty || change.Property == IsFinishedProperty)
        {
            UpdateStatus();
        }
        else if (change.Property == BoundsProperty)
        {
            UpdateSizing(change.GetNewValue<Rect>().Width);
        }
    }

    private protected override void BuildStory(ExplainerScript script)
    {
        var count = StepCount;
        _losses = new double[count];
        var given = LossValues;
        for (var i = 0; i < count; i++)
        {
            var value = given is not null && i < given.Count && double.IsFinite(given[i]) ? given[i] : SampleLoss(i);
            _losses[i] = Math.Clamp(value, 0.01, 5);
        }
        _scene.Prepare(count);
        _stage?.SetRun(count, _losses);
        var t = 0.3;
        for (var step = 1; step <= count; step++)
        {
            var first = step == 1;
            var pace = first ? 1 : RepeatPace;
            var gap = first ? 0.35 : 0.12;
            for (var p = 0; p < 5; p++)
            {
                var phase = (ModelTrainingPhase)(p + 1);
                var duration = PhaseSeconds[p] * pace;
                var at = t;
                var n = step;
                script.At(at, () => EnterPhase(n, phase, at, duration));
                if (phase == ModelTrainingPhase.Loss)
                {
                    script.At(at + duration * 0.5, () => MeasureLoss(n));
                }
                if (phase == ModelTrainingPhase.Update)
                {
                    script.At(at + duration * 0.6, () => PlotLoss(n));
                    script.At(at + duration, () => CompleteStep(n));
                }
                t += duration;
                if (first || phase == ModelTrainingPhase.Update)
                {
                    script.At(t + gap * 0.5, AnnounceSentence, isStop: true);
                }
                t += gap;
            }
        }
        script.At(t + 0.2, Finish, isStop: true);
        script.Length = t + 0.2;
    }

    private void EnterPhase(int step, ModelTrainingPhase phase, double at, double duration)
    {
        Iteration = step;
        Phase = phase;
        _scene.Iteration = step;
        _scene.Phase = phase;
        _scene.PhaseStart = at;
        _scene.PhaseDuration = duration;
        if (phase == ModelTrainingPhase.Update)
        {
            _scene.StartUpdate(step, at, duration);
        }
        var labels = Labels;
        if (step == 1)
        {
            _caption = phase switch
            {
                ModelTrainingPhase.Batch => labels.BatchCaption(BatchSize),
                ModelTrainingPhase.Forward => labels.ForwardCaption,
                ModelTrainingPhase.Loss => labels.LossCaption(TargetProbability(_losses[0]), _losses[0]),
                ModelTrainingPhase.Backward => labels.BackwardCaption,
                _ => labels.UpdateCaption(LearningRate)
            };
            _sentence = _caption;
        }
        else if (phase == ModelTrainingPhase.Batch)
        {
            _caption = labels.RepeatCaption(step, StepCount);
        }
        _kit.SetCaption(_caption ?? CurrentCaption(), Animates);
        UpdateStatus();
        _stage?.Changed();
        Beat(duration);
    }

    private void MeasureLoss(int step)
    {
        var loss = _losses[step - 1];
        CurrentLoss = loss;
        _scene.Loss = loss;
        _scene.LossAt = Now;
        if (step > 1)
        {
            _sentence = string.Create(CultureInfo.InvariantCulture, $"{Labels.StepReadout(step, StepCount)}: loss {loss:0.00}");
        }
        _stage?.Changed();
    }

    private void PlotLoss(int step)
    {
        _scene.Plotted = step;
        _scene.PlottedAt = Now;
        _stage?.Changed();
    }

    private void CompleteStep(int step)
    {
        RaiseEvent(new ModelTrainingIterationEventArgs(IterationCompletedEvent, this, step, _losses[step - 1]));
    }

    private void Finish()
    {
        Phase = ModelTrainingPhase.Done;
        _scene.Phase = ModelTrainingPhase.Done;
        _scene.Finished = true;
        _caption = Labels.DoneCaption(_losses[0], _losses[^1]);
        _sentence = _caption;
        _kit.SetCaption(_caption, Animates);
        UpdateStatus();
        _stage?.Changed();
    }

    private void AnnounceSentence() => AnnounceBeat(_sentence);

    private void Beat(double seconds)
    {
        if (Animates)
        {
            RequestFrames(seconds + 0.6);
        }
    }

    private string CurrentCaption() => _caption ?? Labels.ReadyCaption(StepCount);

    private protected override void ResetScene()
    {
        var hadContent = _scene.HasStarted;
        _scene.Reset();
        _caption = null;
        _sentence = null;
        Phase = ModelTrainingPhase.Ready;
        Iteration = 0;
        CurrentLoss = double.NaN;
        _kit.SetCaption(CurrentCaption(), Animates && hadContent);
        UpdateStatus();
        _stage?.Changed();
        if (hadContent)
        {
            RequestFrames(0.6);
        }
    }

    private protected override void OnTimeChanged()
    {
        _stage?.Tick();
        UpdateStatus();
    }

    private protected override void OnFinished()
    {
        UpdateStatus();
        Announce(_caption);
    }

    private protected override void OnPlaybackChanged()
    {
        UpdateStatus();
        if (!IsPlaying && !_pauseCheckPending)
        {
            _pauseCheckPending = true;
            Dispatcher.UIThread.Post(AnnouncePause, DispatcherPriority.Background);
        }
    }

    private void AnnouncePause()
    {
        _pauseCheckPending = false;
        if (!IsPlaying && !IsStepping && !IsFinished && _scene.HasStarted && IsReaderDriven)
        {
            Announce(_sentence ?? _caption);
        }
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
        }, DispatcherPriority.Background);
    }

    private void UpdateStatus(bool force = false)
    {
        var phase = _kit.PhaseOf(_scene.HasStarted);
        var labels = Labels;
        var text = phase switch
        {
            ExplainerChapterPhase.Playing => labels.Training,
            ExplainerChapterPhase.Paused => labels.Paused,
            ExplainerChapterPhase.Done => labels.Trained,
            _ => labels.Ready
        };
        _kit.SetStatus(phase, text, force);
    }

    private void UpdateSizing(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var padding = ExplainerChapterKit.CardPadding(width);
        var body = new Thickness(padding, padding, padding, AreControlsVisible ? 0 : padding);
        if (body != BodyPadding)
        {
            SetCurrentValue(BodyPaddingProperty, body);
        }
        var footer = new Thickness(padding, 16, padding, padding);
        if (footer != FooterPadding)
        {
            SetCurrentValue(FooterPaddingProperty, footer);
        }
        _kit.SetSpeedLabelVisible(width >= 420);
    }
}

/// <summary>What the training story has shown so far; the stage draws it at the current story time.</summary>
internal sealed class ModelTrainingScene
{
    private double[] _start = [];
    private double[] _goal = [];

    /// <summary>The weights before the current update and after it (35 connections: 4×5 + 5×3).</summary>
    public double[] Before { get; private set; } = [];

    public double[] After { get; private set; } = [];

    public double UpdateStart { get; private set; }

    public double UpdateDuration { get; private set; } = 1;

    public int Iteration { get; set; }

    public ModelTrainingPhase Phase { get; set; }

    public double PhaseStart { get; set; }

    public double PhaseDuration { get; set; } = 1;

    public double Loss { get; set; } = double.NaN;

    public double LossAt { get; set; }

    /// <summary>The steps whose loss is on the curve.</summary>
    public int Plotted { get; set; }

    public double PlottedAt { get; set; }

    public bool Finished { get; set; }

    public bool HasStarted => Iteration > 0 || Finished;

    /// <summary>The number of connections.</summary>
    public static int WeightCount => ModelTraining.LayerSizes[0] * ModelTraining.LayerSizes[1] + ModelTraining.LayerSizes[1] * ModelTraining.LayerSizes[2];

    /// <summary>Sets up the weights for a run: random-looking start values that drift toward fixed trained values.</summary>
    public void Prepare(int steps)
    {
        var n = WeightCount;
        _start = new double[n];
        _goal = new double[n];
        for (var i = 0; i < n; i++)
        {
            _start[i] = Noise(i * 7 + 3) * 0.55;
            _goal[i] = Noise(i * 13 + 101);
        }
        Reset();
    }

    /// <summary>The weights after <paramref name="updates"/> updates.</summary>
    public double[] WeightsAfter(int updates)
    {
        var w = new double[_start.Length];
        var f = 1 - Math.Pow(0.62, updates);
        for (var i = 0; i < w.Length; i++)
        {
            w[i] = _start[i] + (_goal[i] - _start[i]) * f;
        }
        return w;
    }

    public void StartUpdate(int step, double at, double duration)
    {
        Before = WeightsAfter(step - 1);
        After = WeightsAfter(step);
        UpdateStart = at;
        UpdateDuration = duration;
    }

    public void Reset()
    {
        Before = WeightsAfter(0);
        After = Before;
        UpdateStart = 0;
        UpdateDuration = 1;
        Iteration = 0;
        Phase = ModelTrainingPhase.Ready;
        PhaseStart = 0;
        PhaseDuration = 1;
        Loss = double.NaN;
        Plotted = 0;
        Finished = false;
    }

    /// <summary>A stable value in [−1, 1] for an index.</summary>
    private static double Noise(int seed)
    {
        var x = Math.Sin(seed * 12.9898 + 78.233) * 43758.5453;
        return (x - Math.Floor(x)) * 2 - 1;
    }
}
