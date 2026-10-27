using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A project's environment variables and secrets (Arc Env variables, design reference): every row is a value and the
/// environments it applies to, shown as a matrix of switches. Values stay masked until revealed and sensitive values
/// are write-only. People search keys, add one variable or paste a whole .env file, edit or remove a value inline,
/// compare two environments by fingerprint and copy a missing value across. Every change marks its row and opens a
/// banner that redeploys the affected environments through <see cref="RedeployAction"/>; a failed deployment keeps the
/// changes and offers to retry. Below <see cref="CompactWidth"/> the rows stack with labelled environment pills.
/// <code>
/// &lt;arc:EnvVariables ProjectName="lumen-console"&gt;
///     &lt;arc:EnvVariables.Environments&gt;
///         &lt;arc:EnvEnvironment Key="development" Name="Development" /&gt;
///         &lt;arc:EnvEnvironment Key="production" Name="Production" /&gt;
///     &lt;/arc:EnvVariables.Environments&gt;
/// &lt;/arc:EnvVariables&gt;
/// </code>
/// </summary>
[TemplatePart(ShellPart, typeof(Avalonia.Controls.Border))]
[PseudoClasses(CompactPseudoClass, NarrowPseudoClass, FramedPseudoClass, ReadOnlyPseudoClass,
    RedeployPendingPseudoClass, RedeployRunningPseudoClass, RedeployDonePseudoClass, RedeployFailedPseudoClass)]
public partial class EnvVariables : TemplatedControl
{
    internal const string ShellPart = "PART_Shell";
    internal const string CompactPseudoClass = ":compact";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string FramedPseudoClass = ":framed";
    internal const string ReadOnlyPseudoClass = ":readonly";
    internal const string RedeployPendingPseudoClass = ":redeploy-pending";
    internal const string RedeployRunningPseudoClass = ":redeploy-running";
    internal const string RedeployDonePseudoClass = ":redeploy-done";
    internal const string RedeployFailedPseudoClass = ":redeploy-failed";

    #region Properties

    /// <summary>The heading; null shows <see cref="EnvVariablesLabels.Title"/>.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<EnvVariables, string?>(nameof(Title));

    /// <summary>The project's name, set in monospace before the key count; null shows the count alone.</summary>
    public static readonly StyledProperty<string?> ProjectNameProperty =
        AvaloniaProperty.Register<EnvVariables, string?>(nameof(ProjectName));

    /// <summary>The rows, when you give them as a list to follow (observed when it raises collection changes); it wins over <see cref="Variables"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> VariablesSourceProperty =
        AvaloniaProperty.Register<EnvVariables, IEnumerable?>(nameof(VariablesSource));

    /// <summary>The text the .env form starts with when it is opened by the import button (empty starts blank).</summary>
    public static readonly StyledProperty<string> SampleDotenvProperty =
        AvaloniaProperty.Register<EnvVariables, string>(nameof(SampleDotenv), string.Empty);

    /// <summary>The environment keys chosen when the add panel opens; null chooses all but the last (all of a single one).</summary>
    public static readonly StyledProperty<IList<string>?> DefaultAddEnvironmentsProperty =
        AvaloniaProperty.Register<EnvVariables, IList<string>?>(nameof(DefaultAddEnvironments));

    /// <summary>Which table shows (two-way).</summary>
    public static readonly StyledProperty<EnvVariablesView> ViewModeProperty =
        AvaloniaProperty.Register<EnvVariables, EnvVariablesView>(nameof(ViewMode), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The search text (two-way): a case-insensitive part of a key name; values are never searched.</summary>
    public static readonly StyledProperty<string> SearchTextProperty =
        AvaloniaProperty.Register<EnvVariables, string>(nameof(SearchText), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The compare view's left environment key (two-way); null uses the second environment (or the first).</summary>
    public static readonly StyledProperty<string?> CompareLeftProperty =
        AvaloniaProperty.Register<EnvVariables, string?>(nameof(CompareLeft), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The compare view's right environment key (two-way); null uses the last environment.</summary>
    public static readonly StyledProperty<string?> CompareRightProperty =
        AvaloniaProperty.Register<EnvVariables, string?>(nameof(CompareRight), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the compare view lists keys with the same value too (two-way).</summary>
    public static readonly StyledProperty<bool> ShowMatchingProperty =
        AvaloniaProperty.Register<EnvVariables, bool>(nameof(ShowMatching), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Deploys the changes: awaited when Redeploy is pressed (or <see cref="RedeployAsync"/> is called) with what the banner
    /// counted and a token that <see cref="CancelRedeploy"/> (or the block leaving the window) cancels. An exception fails
    /// the deployment: the banner turns red with the message and offers to retry, and the changes stay. Null simulates a
    /// deployment of about 1.7 seconds.
    /// </summary>
    public static readonly StyledProperty<Func<EnvRedeployRequest, CancellationToken, Task>?> RedeployActionProperty =
        AvaloniaProperty.Register<EnvVariables, Func<EnvRedeployRequest, CancellationToken, Task>?>(nameof(RedeployAction));

    /// <summary>Run when a deployment starts, with the <see cref="EnvRedeployRequest"/> as the parameter, before <see cref="RedeployAction"/>; when it cannot execute the deployment does not start.</summary>
    public static readonly StyledProperty<ICommand?> RedeployCommandProperty =
        AvaloniaProperty.Register<EnvVariables, ICommand?>(nameof(RedeployCommand));

    /// <summary>Hides adding, importing, editing and the environment switches' input: the block only shows and compares.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<EnvVariables, bool>(nameof(IsReadOnly));

    /// <summary>Draws the block as a bordered, rounded card (the default); false sits it flat on the page.</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<EnvVariables, bool>(nameof(IsFramed), true);

    /// <summary>Every text the block shows or announces.</summary>
    public static readonly StyledProperty<EnvVariablesLabels> LabelsProperty =
        AvaloniaProperty.Register<EnvVariables, EnvVariablesLabels>(nameof(Labels), new EnvVariablesLabels());

    /// <summary>Below this width of the block the rows stack (760, from the theme).</summary>
    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<EnvVariables, double>(nameof(CompactWidth), 760);

    /// <summary>Below this width the side insets tighten and buttons fill the width (560, from the theme).</summary>
    public static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<EnvVariables, double>(nameof(NarrowWidth), 560);

    /// <summary>The number of changes waiting for a deployment.</summary>
    public static readonly DirectProperty<EnvVariables, int> PendingChangeCountProperty =
        AvaloniaProperty.RegisterDirect<EnvVariables, int>(nameof(PendingChangeCount), o => o.PendingChangeCount);

    /// <summary>The keys of the environments that keep old values until the next deployment, in column order.</summary>
    public static readonly DirectProperty<EnvVariables, IReadOnlyList<string>> PendingEnvironmentsProperty =
        AvaloniaProperty.RegisterDirect<EnvVariables, IReadOnlyList<string>>(nameof(PendingEnvironments), o => o.PendingEnvironments);

    /// <summary>Where the redeploy banner stands.</summary>
    public static readonly DirectProperty<EnvVariables, EnvRedeployState> RedeployStateProperty =
        AvaloniaProperty.RegisterDirect<EnvVariables, EnvRedeployState>(nameof(RedeployState), o => o.RedeployState);

    #endregion

    #region Events

    /// <summary>Raised after every change of the rows (add, import, save, removal, switch, copy across) with all rows.</summary>
    public static readonly RoutedEvent<EnvVariablesChangedEventArgs> VariablesChangedEvent =
        RoutedEvent.Register<EnvVariables, EnvVariablesChangedEventArgs>(nameof(VariablesChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a variable was added from the add panel.</summary>
    public static readonly RoutedEvent<EnvVariableEventArgs> VariableAddedEvent =
        RoutedEvent.Register<EnvVariables, EnvVariableEventArgs>(nameof(VariableAdded), RoutingStrategies.Bubble);

    /// <summary>Raised when a value was saved in the inline editor.</summary>
    public static readonly RoutedEvent<EnvVariableEventArgs> VariableSavedEvent =
        RoutedEvent.Register<EnvVariables, EnvVariableEventArgs>(nameof(VariableSaved), RoutingStrategies.Bubble);

    /// <summary>Raised when a variable was removed.</summary>
    public static readonly RoutedEvent<EnvVariableEventArgs> VariableRemovedEvent =
        RoutedEvent.Register<EnvVariables, EnvVariableEventArgs>(nameof(VariableRemoved), RoutingStrategies.Bubble);

    /// <summary>Raised when variables were imported from a .env file.</summary>
    public static readonly RoutedEvent<EnvVariablesImportedEventArgs> VariablesImportedEvent =
        RoutedEvent.Register<EnvVariables, EnvVariablesImportedEventArgs>(nameof(VariablesImported), RoutingStrategies.Bubble);

    /// <summary>Raised when a row was turned on or off for an environment.</summary>
    public static readonly RoutedEvent<EnvScopeEventArgs> ScopeToggledEvent =
        RoutedEvent.Register<EnvVariables, EnvScopeEventArgs>(nameof(ScopeToggled), RoutingStrategies.Bubble);

    /// <summary>Raised when a switch was refused because another row of the key covers the environment; set Handled to show your own message instead of the note.</summary>
    public static readonly RoutedEvent<EnvScopeEventArgs> ScopeConflictEvent =
        RoutedEvent.Register<EnvVariables, EnvScopeEventArgs>(nameof(ScopeConflict), RoutingStrategies.Bubble);

    /// <summary>Raised just before a value shows (fill <see cref="EnvVariable.Value"/> here to load it lazily) and after it hid.</summary>
    public static readonly RoutedEvent<EnvValueRevealedEventArgs> ValueRevealedEvent =
        RoutedEvent.Register<EnvVariables, EnvValueRevealedEventArgs>(nameof(ValueRevealed), RoutingStrategies.Bubble);

    /// <summary>Raised when a value was copied to another environment from the compare view.</summary>
    public static readonly RoutedEvent<EnvValueCopiedEventArgs> ValueCopiedEvent =
        RoutedEvent.Register<EnvVariables, EnvValueCopiedEventArgs>(nameof(ValueCopied), RoutingStrategies.Bubble);

    /// <summary>Raised when a deployment is about to start; set Cancel to stop it.</summary>
    public static readonly RoutedEvent<EnvRedeployEventArgs> RedeployRequestedEvent =
        RoutedEvent.Register<EnvVariables, EnvRedeployEventArgs>(nameof(RedeployRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when a deployment finished.</summary>
    public static readonly RoutedEvent<EnvRedeployEventArgs> RedeployedEvent =
        RoutedEvent.Register<EnvVariables, EnvRedeployEventArgs>(nameof(Redeployed), RoutingStrategies.Bubble);

    /// <summary>Raised when a deployment failed (the changes are kept).</summary>
    public static readonly RoutedEvent<EnvRedeployEventArgs> RedeployFailedEvent =
        RoutedEvent.Register<EnvVariables, EnvRedeployEventArgs>(nameof(RedeployFailed), RoutingStrategies.Bubble);

    /// <inheritdoc cref="VariablesChangedEvent"/>
    public event EventHandler<EnvVariablesChangedEventArgs>? VariablesChanged
    {
        add => AddHandler(VariablesChangedEvent, value);
        remove => RemoveHandler(VariablesChangedEvent, value);
    }

    /// <inheritdoc cref="VariableAddedEvent"/>
    public event EventHandler<EnvVariableEventArgs>? VariableAdded
    {
        add => AddHandler(VariableAddedEvent, value);
        remove => RemoveHandler(VariableAddedEvent, value);
    }

    /// <inheritdoc cref="VariableSavedEvent"/>
    public event EventHandler<EnvVariableEventArgs>? VariableSaved
    {
        add => AddHandler(VariableSavedEvent, value);
        remove => RemoveHandler(VariableSavedEvent, value);
    }

    /// <inheritdoc cref="VariableRemovedEvent"/>
    public event EventHandler<EnvVariableEventArgs>? VariableRemoved
    {
        add => AddHandler(VariableRemovedEvent, value);
        remove => RemoveHandler(VariableRemovedEvent, value);
    }

    /// <inheritdoc cref="VariablesImportedEvent"/>
    public event EventHandler<EnvVariablesImportedEventArgs>? VariablesImported
    {
        add => AddHandler(VariablesImportedEvent, value);
        remove => RemoveHandler(VariablesImportedEvent, value);
    }

    /// <inheritdoc cref="ScopeToggledEvent"/>
    public event EventHandler<EnvScopeEventArgs>? ScopeToggled
    {
        add => AddHandler(ScopeToggledEvent, value);
        remove => RemoveHandler(ScopeToggledEvent, value);
    }

    /// <inheritdoc cref="ScopeConflictEvent"/>
    public event EventHandler<EnvScopeEventArgs>? ScopeConflict
    {
        add => AddHandler(ScopeConflictEvent, value);
        remove => RemoveHandler(ScopeConflictEvent, value);
    }

    /// <inheritdoc cref="ValueRevealedEvent"/>
    public event EventHandler<EnvValueRevealedEventArgs>? ValueRevealed
    {
        add => AddHandler(ValueRevealedEvent, value);
        remove => RemoveHandler(ValueRevealedEvent, value);
    }

    /// <inheritdoc cref="ValueCopiedEvent"/>
    public event EventHandler<EnvValueCopiedEventArgs>? ValueCopied
    {
        add => AddHandler(ValueCopiedEvent, value);
        remove => RemoveHandler(ValueCopiedEvent, value);
    }

    /// <inheritdoc cref="RedeployRequestedEvent"/>
    public event EventHandler<EnvRedeployEventArgs>? RedeployRequested
    {
        add => AddHandler(RedeployRequestedEvent, value);
        remove => RemoveHandler(RedeployRequestedEvent, value);
    }

    /// <inheritdoc cref="RedeployedEvent"/>
    public event EventHandler<EnvRedeployEventArgs>? Redeployed
    {
        add => AddHandler(RedeployedEvent, value);
        remove => RemoveHandler(RedeployedEvent, value);
    }

    /// <inheritdoc cref="RedeployFailedEvent"/>
    public event EventHandler<EnvRedeployEventArgs>? RedeployFailed
    {
        add => AddHandler(RedeployFailedEvent, value);
        remove => RemoveHandler(RedeployFailedEvent, value);
    }

    #endregion

    private int _pendingChangeCount;
    private IReadOnlyList<string> _pendingEnvironments = Array.Empty<string>();
    private EnvRedeployState _redeployState;

    static EnvVariables()
    {
        AffectsMeasure<EnvVariables>(CompactWidthProperty, NarrowWidthProperty);
    }

    /// <summary>Creates the block with no rows and the default environments (Development, Preview, Production).</summary>
    public EnvVariables()
    {
        Environments = new AvaloniaList<EnvEnvironment>();
        Environments.CollectionChanged += (_, _) => OnEnvironmentsChanged();
        SetCurrentValue(VariablesProperty, new AvaloniaList<EnvVariable>());
        InitializeLogic();
    }

    /// <summary>The rows, in any order (the block shows them by key); <see cref="VariablesSource"/> wins when set.</summary>
    public static readonly StyledProperty<AvaloniaList<EnvVariable>> VariablesProperty =
        AvaloniaProperty.Register<EnvVariables, AvaloniaList<EnvVariable>>(nameof(Variables));

    /// <inheritdoc cref="VariablesProperty"/>
    [Content]
    public AvaloniaList<EnvVariable> Variables { get => GetValue(VariablesProperty); set => SetValue(VariablesProperty, value); }

    /// <summary>
    /// The environments, in column order. Empty uses three defaults: development, preview and production. Each row's
    /// <see cref="EnvVariable.Environments"/> holds these keys.
    /// </summary>
    public AvaloniaList<EnvEnvironment> Environments { get; }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="ProjectNameProperty"/>
    public string? ProjectName { get => GetValue(ProjectNameProperty); set => SetValue(ProjectNameProperty, value); }

    /// <inheritdoc cref="VariablesSourceProperty"/>
    public IEnumerable? VariablesSource { get => GetValue(VariablesSourceProperty); set => SetValue(VariablesSourceProperty, value); }

    /// <inheritdoc cref="SampleDotenvProperty"/>
    public string SampleDotenv { get => GetValue(SampleDotenvProperty); set => SetValue(SampleDotenvProperty, value); }

    /// <inheritdoc cref="DefaultAddEnvironmentsProperty"/>
    public IList<string>? DefaultAddEnvironments { get => GetValue(DefaultAddEnvironmentsProperty); set => SetValue(DefaultAddEnvironmentsProperty, value); }

    /// <inheritdoc cref="ViewModeProperty"/>
    public EnvVariablesView ViewMode { get => GetValue(ViewModeProperty); set => SetValue(ViewModeProperty, value); }

    /// <inheritdoc cref="SearchTextProperty"/>
    public string SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }

    /// <inheritdoc cref="CompareLeftProperty"/>
    public string? CompareLeft { get => GetValue(CompareLeftProperty); set => SetValue(CompareLeftProperty, value); }

    /// <inheritdoc cref="CompareRightProperty"/>
    public string? CompareRight { get => GetValue(CompareRightProperty); set => SetValue(CompareRightProperty, value); }

    /// <inheritdoc cref="ShowMatchingProperty"/>
    public bool ShowMatching { get => GetValue(ShowMatchingProperty); set => SetValue(ShowMatchingProperty, value); }

    /// <inheritdoc cref="RedeployActionProperty"/>
    public Func<EnvRedeployRequest, CancellationToken, Task>? RedeployAction { get => GetValue(RedeployActionProperty); set => SetValue(RedeployActionProperty, value); }

    /// <inheritdoc cref="RedeployCommandProperty"/>
    public ICommand? RedeployCommand { get => GetValue(RedeployCommandProperty); set => SetValue(RedeployCommandProperty, value); }

    /// <inheritdoc cref="IsReadOnlyProperty"/>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public EnvVariablesLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="CompactWidthProperty"/>
    public double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <inheritdoc cref="NarrowWidthProperty"/>
    public double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    /// <inheritdoc cref="PendingChangeCountProperty"/>
    public int PendingChangeCount
    {
        get => _pendingChangeCount;
        private set => SetAndRaise(PendingChangeCountProperty, ref _pendingChangeCount, value);
    }

    /// <inheritdoc cref="PendingEnvironmentsProperty"/>
    public IReadOnlyList<string> PendingEnvironments
    {
        get => _pendingEnvironments;
        private set => SetAndRaise(PendingEnvironmentsProperty, ref _pendingEnvironments, value);
    }

    /// <inheritdoc cref="RedeployStateProperty"/>
    public EnvRedeployState RedeployState
    {
        get => _redeployState;
        private set
        {
            if (SetAndRaise(RedeployStateProperty, ref _redeployState, value))
            {
                PseudoClasses.Set(RedeployPendingPseudoClass, value == EnvRedeployState.Pending);
                PseudoClasses.Set(RedeployRunningPseudoClass, value == EnvRedeployState.Running);
                PseudoClasses.Set(RedeployDonePseudoClass, value == EnvRedeployState.Done);
                PseudoClasses.Set(RedeployFailedPseudoClass, value == EnvRedeployState.Failed);
            }
        }
    }
}
