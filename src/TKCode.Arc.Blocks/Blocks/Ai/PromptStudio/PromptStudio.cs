using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A card to edit a prompt template, try it on saved test cases, and compare and ship versions: the version and the model
/// on top, a system / user template editor whose <c>{{name}}</c> variables are colored chips, a field for each variable, a
/// row of test cases, a Run button and an output panel for the reply. A version menu compares the open version with another
/// as a word diff or promotes it to production, and a model menu picks the model and the effort.
/// <code>
/// &lt;arc:PromptStudio Title="Support reply" RunHandler="{Binding Run}" /&gt;
/// </code>
/// With no <see cref="RunHandler"/> a built-in simulator answers and the card says so.
/// </summary>
public partial class PromptStudio : TemplatedControl
{
    #region Properties

    /// <summary>The name of the prompt, in the header.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PromptStudio, string>(nameof(Title), string.Empty);

    /// <summary>The version the editor has open (two-way); null opens the first (newest).</summary>
    public static readonly StyledProperty<PromptVersion?> OpenVersionProperty =
        AvaloniaProperty.Register<PromptStudio, PromptVersion?>(nameof(OpenVersion), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The version in production (two-way); null follows <see cref="OpenVersion"/>. Promoting updates it at once and raises <c>Promoted</c>.</summary>
    public static readonly StyledProperty<PromptVersion?> ProductionVersionProperty =
        AvaloniaProperty.Register<PromptStudio, PromptVersion?>(nameof(ProductionVersion), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The template being edited, the draft (two-way); starts as the open version's.</summary>
    public static readonly StyledProperty<PromptTemplate> DraftProperty =
        AvaloniaProperty.Register<PromptStudio, PromptTemplate>(nameof(Draft), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The part of the template that is shown (two-way).</summary>
    public static readonly StyledProperty<PromptPart> ActivePartProperty =
        AvaloniaProperty.Register<PromptStudio, PromptPart>(nameof(ActivePart), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The selected test case (two-way); choosing one resets the values to its own.</summary>
    public static readonly StyledProperty<PromptTestCase?> SelectedTestCaseProperty =
        AvaloniaProperty.Register<PromptStudio, PromptTestCase?>(nameof(SelectedTestCase), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The selected model (two-way); choosing one sets <see cref="Effort"/> to its default.</summary>
    public static readonly StyledProperty<AiModel?> SelectedModelProperty =
        AvaloniaProperty.Register<PromptStudio, AiModel?>(nameof(SelectedModel), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The effort (two-way).</summary>
    public static readonly StyledProperty<ModelEffort> EffortProperty =
        AvaloniaProperty.Register<PromptStudio, ModelEffort>(nameof(Effort), ModelEffort.Medium, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Runs the prompt; null uses the built-in simulator.</summary>
    public static readonly StyledProperty<PromptRunHandler?> RunHandlerProperty =
        AvaloniaProperty.Register<PromptStudio, PromptRunHandler?>(nameof(RunHandler));

    /// <summary>Estimates the tokens of a text; null uses about a quarter of the characters.</summary>
    public static readonly StyledProperty<Func<string, int>?> TokenEstimatorProperty =
        AvaloniaProperty.Register<PromptStudio, Func<string, int>?>(nameof(TokenEstimator));

    /// <summary>The moment relative times ("last week") are measured from; null uses now.</summary>
    public static readonly StyledProperty<DateTimeOffset?> NowProperty =
        AvaloniaProperty.Register<PromptStudio, DateTimeOffset?>(nameof(Now));

    /// <summary>Whether the editor shows the word diff against <see cref="CompareVersion"/> (two-way); Escape leaves it.</summary>
    public static readonly StyledProperty<bool> IsCompareModeProperty =
        AvaloniaProperty.Register<PromptStudio, bool>(nameof(IsCompareMode), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The version the open one is compared with (two-way).</summary>
    public static readonly StyledProperty<PromptVersion?> CompareVersionProperty =
        AvaloniaProperty.Register<PromptStudio, PromptVersion?>(nameof(CompareVersion), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Where the run is (read-only).</summary>
    public static readonly DirectProperty<PromptStudio, PromptRunState> RunStateProperty =
        AvaloniaProperty.RegisterDirect<PromptStudio, PromptRunState>(nameof(RunState), o => o.RunState);

    /// <summary>What the last finished run produced (read-only).</summary>
    public static readonly DirectProperty<PromptStudio, PromptRunResult?> LastResultProperty =
        AvaloniaProperty.RegisterDirect<PromptStudio, PromptRunResult?>(nameof(LastResult), o => o.LastResult);

    /// <summary>Run the prompt (read-only command for toolbars).</summary>
    public static readonly DirectProperty<PromptStudio, ICommand> RunCommandProperty =
        AvaloniaProperty.RegisterDirect<PromptStudio, ICommand>(nameof(RunCommand), o => o.RunCommand);

    /// <summary>Stop the run (read-only command for toolbars).</summary>
    public static readonly DirectProperty<PromptStudio, ICommand> StopCommandProperty =
        AvaloniaProperty.RegisterDirect<PromptStudio, ICommand>(nameof(StopCommand), o => o.StopCommand);

    /// <summary>Every word the block shows or announces.</summary>
    public static readonly StyledProperty<PromptStudioLabels> LabelsProperty =
        AvaloniaProperty.Register<PromptStudio, PromptStudioLabels>(nameof(Labels), new PromptStudioLabels());

    #endregion

    #region Events

    /// <summary>Raised after every edit of the template; save it here.</summary>
    public static readonly RoutedEvent<PromptTemplateChangedEventArgs> TemplateChangedEvent =
        RoutedEvent.Register<PromptStudio, PromptTemplateChangedEventArgs>(nameof(TemplateChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a run starts.</summary>
    public static readonly RoutedEvent<PromptRunStartedEventArgs> RunStartedEvent =
        RoutedEvent.Register<PromptStudio, PromptRunStartedEventArgs>(nameof(RunStarted), RoutingStrategies.Bubble);

    /// <summary>Raised when a run ends: done, stopped or failed.</summary>
    public static readonly RoutedEvent<PromptRunCompletedEventArgs> RunCompletedEvent =
        RoutedEvent.Register<PromptStudio, PromptRunCompletedEventArgs>(nameof(RunCompleted), RoutingStrategies.Bubble);

    /// <summary>Raised when the handler throws; change <c>Message</c> to word the error.</summary>
    public static readonly RoutedEvent<PromptRunFailedEventArgs> RunFailedEvent =
        RoutedEvent.Register<PromptStudio, PromptRunFailedEventArgs>(nameof(RunFailed), RoutingStrategies.Bubble);

    /// <summary>Raised when a version is promoted to production or a promotion is undone; set <c>Cancel</c> to roll it back.</summary>
    public static readonly RoutedEvent<PromptPromotedEventArgs> PromotedEvent =
        RoutedEvent.Register<PromptStudio, PromptPromotedEventArgs>(nameof(Promoted), RoutingStrategies.Bubble);

    /// <summary>Raised after a variable is added from the editor.</summary>
    public static readonly RoutedEvent<PromptVariableAddedEventArgs> VariableAddedEvent =
        RoutedEvent.Register<PromptStudio, PromptVariableAddedEventArgs>(nameof(VariableAdded), RoutingStrategies.Bubble);

    /// <summary>Raised after every edit of the template.</summary>
    public event EventHandler<PromptTemplateChangedEventArgs>? TemplateChanged { add => AddHandler(TemplateChangedEvent, value); remove => RemoveHandler(TemplateChangedEvent, value); }

    /// <summary>Raised when a run starts.</summary>
    public event EventHandler<PromptRunStartedEventArgs>? RunStarted { add => AddHandler(RunStartedEvent, value); remove => RemoveHandler(RunStartedEvent, value); }

    /// <summary>Raised when a run ends.</summary>
    public event EventHandler<PromptRunCompletedEventArgs>? RunCompleted { add => AddHandler(RunCompletedEvent, value); remove => RemoveHandler(RunCompletedEvent, value); }

    /// <summary>Raised when the handler throws.</summary>
    public event EventHandler<PromptRunFailedEventArgs>? RunFailed { add => AddHandler(RunFailedEvent, value); remove => RemoveHandler(RunFailedEvent, value); }

    /// <summary>Raised when a version is promoted or a promotion is undone.</summary>
    public event EventHandler<PromptPromotedEventArgs>? Promoted { add => AddHandler(PromotedEvent, value); remove => RemoveHandler(PromotedEvent, value); }

    /// <summary>Raised after a variable is added from the editor.</summary>
    public event EventHandler<PromptVariableAddedEventArgs>? VariableAdded { add => AddHandler(VariableAddedEvent, value); remove => RemoveHandler(VariableAddedEvent, value); }

    #endregion

    private PromptRunState _runState;
    private PromptRunResult? _lastResult;
    private readonly PromptStudioCommand _runCommand = new();
    private readonly PromptStudioCommand _stopCommand = new();

    /// <summary>The saved versions, newest first.</summary>
    public AvaloniaList<PromptVersion> Versions { get; } = new();

    /// <summary>The variables in the order of their colors and fields; names that appear in the text but not here are added after.</summary>
    public AvaloniaList<string> Variables { get; } = new();

    /// <summary>The saved test cases.</summary>
    public AvaloniaList<PromptTestCase> TestCases { get; } = new();

    /// <summary>The models the person can pick, with their prices.</summary>
    public AvaloniaList<AiModel> Models { get; } = new();

    /// <summary>The providers of the models, for the marks of models that have none of their own.</summary>
    public AvaloniaList<AiModelProvider> Providers { get; } = new();

    /// <summary>The current value of each variable (the test case's values, as the person edited them).</summary>
    public AvaloniaDictionary<string, string> Values { get; } = new();

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="OpenVersionProperty"/>
    public PromptVersion? OpenVersion { get => GetValue(OpenVersionProperty); set => SetValue(OpenVersionProperty, value); }

    /// <inheritdoc cref="ProductionVersionProperty"/>
    public PromptVersion? ProductionVersion { get => GetValue(ProductionVersionProperty); set => SetValue(ProductionVersionProperty, value); }

    /// <inheritdoc cref="DraftProperty"/>
    public PromptTemplate Draft { get => GetValue(DraftProperty); set => SetValue(DraftProperty, value); }

    /// <inheritdoc cref="ActivePartProperty"/>
    public PromptPart ActivePart { get => GetValue(ActivePartProperty); set => SetValue(ActivePartProperty, value); }

    /// <inheritdoc cref="SelectedTestCaseProperty"/>
    public PromptTestCase? SelectedTestCase { get => GetValue(SelectedTestCaseProperty); set => SetValue(SelectedTestCaseProperty, value); }

    /// <inheritdoc cref="SelectedModelProperty"/>
    public AiModel? SelectedModel { get => GetValue(SelectedModelProperty); set => SetValue(SelectedModelProperty, value); }

    /// <inheritdoc cref="EffortProperty"/>
    public ModelEffort Effort { get => GetValue(EffortProperty); set => SetValue(EffortProperty, value); }

    /// <inheritdoc cref="RunHandlerProperty"/>
    public PromptRunHandler? RunHandler { get => GetValue(RunHandlerProperty); set => SetValue(RunHandlerProperty, value); }

    /// <inheritdoc cref="TokenEstimatorProperty"/>
    public Func<string, int>? TokenEstimator { get => GetValue(TokenEstimatorProperty); set => SetValue(TokenEstimatorProperty, value); }

    /// <inheritdoc cref="NowProperty"/>
    public DateTimeOffset? Now { get => GetValue(NowProperty); set => SetValue(NowProperty, value); }

    /// <inheritdoc cref="IsCompareModeProperty"/>
    public bool IsCompareMode { get => GetValue(IsCompareModeProperty); set => SetValue(IsCompareModeProperty, value); }

    /// <inheritdoc cref="CompareVersionProperty"/>
    public PromptVersion? CompareVersion { get => GetValue(CompareVersionProperty); set => SetValue(CompareVersionProperty, value); }

    /// <inheritdoc cref="RunStateProperty"/>
    public PromptRunState RunState { get => _runState; private set => SetAndRaise(RunStateProperty, ref _runState, value); }

    /// <inheritdoc cref="LastResultProperty"/>
    public PromptRunResult? LastResult { get => _lastResult; private set => SetAndRaise(LastResultProperty, ref _lastResult, value); }

    /// <inheritdoc cref="RunCommandProperty"/>
    public ICommand RunCommand => _runCommand;

    /// <inheritdoc cref="StopCommandProperty"/>
    public ICommand StopCommand => _stopCommand;

    /// <inheritdoc cref="LabelsProperty"/>
    public PromptStudioLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
}

/// <summary>A command whose action and availability the studio sets (the run and stop commands).</summary>
internal sealed class PromptStudioCommand : ICommand
{
    private bool _canExecute = true;

    /// <summary>What the command does.</summary>
    public Action? Execute { get; set; }

    /// <summary>Whether the command can run now.</summary>
    public bool CanExecuteNow
    {
        get => _canExecute;
        set
        {
            if (_canExecute != value)
            {
                _canExecute = value;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => _canExecute;

    /// <inheritdoc/>
    void ICommand.Execute(object? parameter) => Execute?.Invoke();
}
