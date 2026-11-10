using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A guided setup that turns on AI features for a workspace in five steps: choose the provider, the model and enter a
/// key; choose which data the assistant may read; set the privacy rules (training, masking, history); run a test; and
/// review the result before turning it on. A stepper on the side shows the way and lets people go back to any finished
/// step; each step checks its own fields before Continue moves on, and the test must pass before the summary.
/// <code>
/// &lt;arc:AiSetupWizard Providers="{Binding Providers}" DataSources="{Binding Sources}"
///                     TestRunner="{Binding TestAsync}" Completed="OnAiTurnedOn" /&gt;
/// </code>
/// With no data set the wizard offers its invented sample providers and sources and a simulated test.
/// </summary>
[TemplatePart(StepperPart, typeof(Stepper))]
[TemplatePart(PagePart, typeof(ArcContentSwap))]
[TemplatePart(BackPart, typeof(Button))]
[TemplatePart(NextPart, typeof(Button))]
[PseudoClasses(CompactPseudoClass, FirstPseudoClass, CompletedPseudoClass)]
public partial class AiSetupWizard : TemplatedControl
{
    public const string StepperPart = "PART_Stepper";
    public const string PagePart = "PART_Page";
    public const string BackPart = "PART_Back";
    public const string NextPart = "PART_Next";

    public const string CompactPseudoClass = ":compact";
    public const string FirstPseudoClass = ":first";
    public const string CompletedPseudoClass = ":completed";

    #region Properties

    /// <summary>The providers to choose from.</summary>
    public static readonly StyledProperty<IReadOnlyList<AiSetupProvider>?> ProvidersProperty =
        AvaloniaProperty.Register<AiSetupWizard, IReadOnlyList<AiSetupProvider>?>(nameof(Providers), AiSetupWizardSamples.Providers());

    /// <summary>The kinds of data the assistant may be allowed to read.</summary>
    public static readonly StyledProperty<IReadOnlyList<AiSetupDataSource>?> DataSourcesProperty =
        AvaloniaProperty.Register<AiSetupWizard, IReadOnlyList<AiSetupDataSource>?>(nameof(DataSources), AiSetupWizardSamples.DataSources());

    /// <summary>The history choices in days, 0 meaning none (default Off, 30 and 90).</summary>
    public static readonly StyledProperty<IReadOnlyList<int>?> HistoryChoicesProperty =
        AvaloniaProperty.Register<AiSetupWizard, IReadOnlyList<int>?>(nameof(HistoryChoices), AiSetupWizardSamples.HistoryChoices());

    /// <summary>
    /// Runs the connection test with the choices so far: report each passed check (1 to 4) through the progress, and
    /// return the result. An exception fails the test with its message. Null runs the simulated test.
    /// </summary>
    public static readonly StyledProperty<Func<AiSetupConfiguration, IProgress<int>, CancellationToken, Task<AiSetupTestResult>>?> TestRunnerProperty =
        AvaloniaProperty.Register<AiSetupWizard, Func<AiSetupConfiguration, IProgress<int>, CancellationToken, Task<AiSetupTestResult>>?>(nameof(TestRunner));

    /// <summary>The step shown (two-way). Setting it moves only to a step that is already open (see <see cref="GoTo"/>).</summary>
    public static readonly StyledProperty<AiSetupStep> CurrentStepProperty =
        AvaloniaProperty.Register<AiSetupWizard, AiSetupStep>(nameof(CurrentStep), defaultBindingMode: BindingMode.TwoWay,
            coerce: CoerceStep);

    /// <summary>Every word the wizard shows or reads out, with English defaults.</summary>
    public static readonly StyledProperty<AiSetupWizardLabels> LabelsProperty =
        AvaloniaProperty.Register<AiSetupWizard, AiSetupWizardLabels>(nameof(Labels), new AiSetupWizardLabels());

    /// <summary>What has been chosen so far.</summary>
    public static readonly DirectProperty<AiSetupWizard, AiSetupConfiguration> ConfigurationProperty =
        AvaloniaProperty.RegisterDirect<AiSetupWizard, AiSetupConfiguration>(nameof(Configuration), o => o.Configuration);

    /// <summary>The result of the last test; null before one finished (and again after a change that needs a new one).</summary>
    public static readonly DirectProperty<AiSetupWizard, AiSetupTestResult?> TestResultProperty =
        AvaloniaProperty.RegisterDirect<AiSetupWizard, AiSetupTestResult?>(nameof(TestResult), o => o.TestResult);

    /// <summary>True while the test runs.</summary>
    public static readonly DirectProperty<AiSetupWizard, bool> IsTestingProperty =
        AvaloniaProperty.RegisterDirect<AiSetupWizard, bool>(nameof(IsTesting), o => o.IsTesting);

    /// <summary>True once the last step's button was pressed.</summary>
    public static readonly DirectProperty<AiSetupWizard, bool> IsCompletedProperty =
        AvaloniaProperty.RegisterDirect<AiSetupWizard, bool>(nameof(IsCompleted), o => o.IsCompleted);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<AiSetupWizard, double>(nameof(CompactWidth), 640);

    /// <inheritdoc cref="ProvidersProperty"/>
    public IReadOnlyList<AiSetupProvider>? Providers { get => GetValue(ProvidersProperty); set => SetValue(ProvidersProperty, value); }

    /// <inheritdoc cref="DataSourcesProperty"/>
    public IReadOnlyList<AiSetupDataSource>? DataSources { get => GetValue(DataSourcesProperty); set => SetValue(DataSourcesProperty, value); }

    /// <inheritdoc cref="HistoryChoicesProperty"/>
    public IReadOnlyList<int>? HistoryChoices { get => GetValue(HistoryChoicesProperty); set => SetValue(HistoryChoicesProperty, value); }

    /// <inheritdoc cref="TestRunnerProperty"/>
    public Func<AiSetupConfiguration, IProgress<int>, CancellationToken, Task<AiSetupTestResult>>? TestRunner
    {
        get => GetValue(TestRunnerProperty);
        set => SetValue(TestRunnerProperty, value);
    }

    /// <inheritdoc cref="CurrentStepProperty"/>
    public AiSetupStep CurrentStep { get => GetValue(CurrentStepProperty); set => SetValue(CurrentStepProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AiSetupWizardLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="ConfigurationProperty"/>
    public AiSetupConfiguration Configuration { get => _configuration; private set => SetAndRaise(ConfigurationProperty, ref _configuration, value); }

    /// <inheritdoc cref="TestResultProperty"/>
    public AiSetupTestResult? TestResult { get => _testResult; private set => SetAndRaise(TestResultProperty, ref _testResult, value); }

    /// <inheritdoc cref="IsTestingProperty"/>
    public bool IsTesting { get => _isTesting; private set => SetAndRaise(IsTestingProperty, ref _isTesting, value); }

    /// <inheritdoc cref="IsCompletedProperty"/>
    public bool IsCompleted { get => _isCompleted; private set => SetAndRaise(IsCompletedProperty, ref _isCompleted, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    #endregion

    #region Events

    /// <summary>Raised when the step shown changes.</summary>
    public static readonly RoutedEvent<AiSetupStepChangedEventArgs> StepChangedEvent =
        RoutedEvent.Register<AiSetupWizard, AiSetupStepChangedEventArgs>(nameof(StepChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the last step's button is pressed, with what was set up.</summary>
    public static readonly RoutedEvent<AiSetupCompletedEventArgs> CompletedEvent =
        RoutedEvent.Register<AiSetupWizard, AiSetupCompletedEventArgs>(nameof(Completed), RoutingStrategies.Bubble);

    /// <summary>Raised when the step shown changes.</summary>
    public event EventHandler<AiSetupStepChangedEventArgs>? StepChanged
    {
        add => AddHandler(StepChangedEvent, value);
        remove => RemoveHandler(StepChangedEvent, value);
    }

    /// <summary>Raised when the last step's button is pressed, with what was set up.</summary>
    public event EventHandler<AiSetupCompletedEventArgs>? Completed
    {
        add => AddHandler(CompletedEvent, value);
        remove => RemoveHandler(CompletedEvent, value);
    }

    #endregion

    private AiSetupConfiguration _configuration = new();
    private AiSetupTestResult? _testResult;
    private bool _isTesting;
    private bool _isCompleted;
    private Stepper? _stepper;
    private ArcContentSwap? _page;
    private Button? _back;
    private Button? _next;
    private TextBlock? _title;
    private AiAnnouncer? _announcer;
    private AiSetupStep _furthest;
    private CancellationTokenSource? _testCts;

    static AiSetupWizard()
    {
        FocusableProperty.OverrideDefaultValue<AiSetupWizard>(false);
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<AiSetupWizard>(AutomationLandmarkType.Form);
    }

    /// <summary>Waits a simulated check; tests replace it.</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = static (time, token) => Task.Delay(time, token);

    /// <summary>The furthest step reached; steps up to it can be revisited.</summary>
    public AiSetupStep FurthestStep => _furthest;

    internal Control? CurrentPage => _page?.Content as Control;

    internal Stepper? StepperControl => _stepper;

    internal string? Announcement => _announcer?.Announcement;

    private static AiSetupStep CoerceStep(AvaloniaObject sender, AiSetupStep value)
    {
        var wizard = (AiSetupWizard)sender;
        return value < AiSetupStep.Provider ? AiSetupStep.Provider : value > wizard._furthest ? wizard._furthest : value;
    }

    #region Public actions

    /// <summary>Checks the current step and moves to the next one; on the last step, finishes. Returns whether it moved.</summary>
    public bool Next()
    {
        if (IsCompleted || IsTesting)
        {
            return false;
        }
        if (CurrentStep == AiSetupStep.Done)
        {
            Finish();
            return true;
        }
        if (!Validate(CurrentStep) || (CurrentStep == AiSetupStep.Test && TestResult is not { IsSuccess: true }))
        {
            return false;
        }
        var next = CurrentStep + 1;
        if (next > _furthest)
        {
            _furthest = next;
        }
        SetCurrentValue(CurrentStepProperty, next);
        return true;
    }

    /// <summary>Goes back one step. Returns whether it moved.</summary>
    public bool Back()
    {
        if (CurrentStep == AiSetupStep.Provider || IsCompleted)
        {
            return false;
        }
        SetCurrentValue(CurrentStepProperty, CurrentStep - 1);
        return true;
    }

    /// <summary>Shows <paramref name="step"/> when it was reached before. Returns whether it moved.</summary>
    /// <param name="step">The step to show.</param>
    public bool GoTo(AiSetupStep step)
    {
        if (step > _furthest || IsCompleted)
        {
            return false;
        }
        SetCurrentValue(CurrentStepProperty, step);
        return true;
    }

    /// <summary>Puts the wizard back at the first step with the default choices.</summary>
    public void Reset()
    {
        _testCts?.Cancel();
        IsTesting = false;
        IsCompleted = false;
        TestResult = null;
        _furthest = AiSetupStep.Provider;
        Configuration = DefaultConfiguration();
        SetCurrentValue(CurrentStepProperty, AiSetupStep.Provider);
        BuildPages();
        ShowPage(false);
        UpdateChrome();
    }

    #endregion

    private AiSetupConfiguration DefaultConfiguration()
    {
        var choices = HistoryChoices;
        return new AiSetupConfiguration
        {
            DataSourceIds = (DataSources ?? []).Where(s => s.IsEnabledByDefault).Select(s => s.Id).ToArray(),
            HistoryDays = choices is { Count: > 0 } ? (choices.Contains(30) ? 30 : choices[0]) : 0,
        };
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_back is not null)
        {
            _back.Click -= OnBackClick;
        }
        if (_next is not null)
        {
            _next.Click -= OnNextClick;
        }
        if (_stepper is not null)
        {
            _stepper.StepSelected -= OnStepSelected;
        }
        _stepper = e.NameScope.Find<Stepper>(StepperPart);
        _page = e.NameScope.Find<ArcContentSwap>(PagePart);
        _back = e.NameScope.Find<Button>(BackPart);
        _next = e.NameScope.Find<Button>(NextPart);
        _title = e.NameScope.Find<TextBlock>("PART_Title");
        _announcer = e.NameScope.Find<AiAnnouncer>("PART_Announcer");
        if (_back is not null)
        {
            _back.Click += OnBackClick;
        }
        if (_next is not null)
        {
            _next.Click += OnNextClick;
        }
        if (_stepper is not null)
        {
            _stepper.StepSelected += OnStepSelected;
        }
        if (_configuration.ProviderId is null && _configuration.DataSourceIds.Count == 0)
        {
            Configuration = DefaultConfiguration();
        }
        BuildSteps();
        BuildPages();
        ShowPage(false);
        UpdateChrome();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CurrentStepProperty)
        {
            var oldStep = change.GetOldValue<AiSetupStep>();
            var newStep = change.GetNewValue<AiSetupStep>();
            SyncKey();
            ShowPage(true, newStep > oldStep ? 1 : -1);
            UpdateChrome();
            _announcer?.Announce(Labels.StepAnnouncement((int)newStep + 1, 5, StepName(newStep)));
            RaiseEvent(new AiSetupStepChangedEventArgs(StepChangedEvent, oldStep, newStep));
        }
        else if (change.Property == ProvidersProperty || change.Property == DataSourcesProperty || change.Property == HistoryChoicesProperty)
        {
            if (_page is not null)
            {
                Reset();
            }
        }
        else if (change.Property == LabelsProperty)
        {
            BuildSteps();
            BuildPages();
            ShowPage(false);
            UpdateChrome();
        }
        else if (change.Property == BoundsProperty || change.Property == CompactWidthProperty)
        {
            var compact = Bounds.Width > 0 && Bounds.Width <= CompactWidth;
            PseudoClasses.Set(CompactPseudoClass, compact);
            if (_stepper is not null)
            {
                _stepper.Orientation = compact ? Orientation.Horizontal : Orientation.Vertical;
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _testCts?.Cancel();
    }

    private string StepName(AiSetupStep step)
        => Labels.Steps is { } steps && (int)step < steps.Count ? steps[(int)step] : step.ToString();

    private void BuildSteps()
    {
        if (_stepper is null)
        {
            return;
        }
        _stepper.Steps.Clear();
        var labels = Labels;
        for (var i = 0; i < 5; i++)
        {
            var step = (AiSetupStep)i;
            var description = labels.StepDescriptions is { } d && i < d.Count ? d[i] : null;
            _stepper.Steps.Add(new StepperStep(StepName(step), description) { Id = step.ToString() });
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => Back();

    private void OnNextClick(object? sender, RoutedEventArgs e) => Next();

    private void OnStepSelected(object? sender, StepperStepSelectedEventArgs e) => GoTo((AiSetupStep)e.Index);

    private void UpdateChrome()
    {
        var labels = Labels;
        if (_title is not null)
        {
            _title.Text = IsCompleted ? labels.CompletedTitle : labels.Title;
        }
        if (_stepper is not null)
        {
            _stepper.CurrentIndex = IsCompleted ? 5 : (int)CurrentStep;
        }
        if (_back is not null)
        {
            _back.Content = labels.Back;
            _back.IsEnabled = CurrentStep > AiSetupStep.Provider && !IsTesting && !IsCompleted;
        }
        if (_next is not null)
        {
            _next.Content = CurrentStep == AiSetupStep.Done ? labels.Finish : labels.Continue;
            _next.IsEnabled = !IsTesting && !IsCompleted && (CurrentStep != AiSetupStep.Test || TestResult is { IsSuccess: true });
        }
        PseudoClasses.Set(FirstPseudoClass, CurrentStep == AiSetupStep.Provider);
        PseudoClasses.Set(CompletedPseudoClass, IsCompleted);
    }

    private void Finish()
    {
        IsCompleted = true;
        UpdateChrome();
        _pages[(int)AiSetupStep.Done] = BuildDonePage();
        ShowPage(false);
        _announcer?.Announce(Labels.CompletedTitle);
        RaiseEvent(new AiSetupCompletedEventArgs(CompletedEvent, Configuration));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AiSetupWizardPeer(this);

    private sealed class AiSetupWizardPeer(AiSetupWizard owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore()
            => base.GetNameCore() is { Length: > 0 } name ? name : ((AiSetupWizard)Owner).Labels.Title;
    }
}
