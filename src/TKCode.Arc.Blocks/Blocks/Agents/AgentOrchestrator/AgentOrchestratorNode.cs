using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One agent in the graph of an <see cref="AgentOrchestrator"/>: a monogram, the name (with a Lead tag on the lead), the
/// role, a status icon (a spinner while thinking or working, an hourglass while waiting, a check when done, a cross when
/// failed), what it is doing and the status word. The status is a pseudo-class (<c>:idle</c>, <c>:thinking</c>,
/// <c>:working</c>, <c>:waiting</c>, <c>:done</c>, <c>:failed</c>), so it never relies on color alone. Focusable: a
/// click, Enter or Space selects it.
/// </summary>
[TemplatePart(RingHostPart, typeof(Panel))]
[PseudoClasses(IdlePseudoClass, ThinkingPseudoClass, WorkingPseudoClass, WaitingPseudoClass, DonePseudoClass, FailedPseudoClass,
    SelectedPseudoClass, LeadPseudoClass, TreePseudoClass, EmptyPseudoClass)]
internal sealed class AgentOrchestratorNode : TemplatedControl
{
    public const string RingHostPart = "PART_RingHost";

    public const string IdlePseudoClass = ":idle";
    public const string ThinkingPseudoClass = ":thinking";
    public const string WorkingPseudoClass = ":working";
    public const string WaitingPseudoClass = ":waiting";
    public const string DonePseudoClass = ":done";
    public const string FailedPseudoClass = ":failed";
    public const string SelectedPseudoClass = ":selected";
    public const string LeadPseudoClass = ":lead";
    public const string TreePseudoClass = ":tree";

    /// <summary>Set while the agent has neither a task nor an activity (the line reads "No task yet").</summary>
    public const string EmptyPseudoClass = ":empty";

    public static readonly DirectProperty<AgentOrchestratorNode, string> AgentNameProperty =
        AvaloniaProperty.RegisterDirect<AgentOrchestratorNode, string>(nameof(AgentName), o => o.AgentName);

    public static readonly DirectProperty<AgentOrchestratorNode, string?> RoleProperty =
        AvaloniaProperty.RegisterDirect<AgentOrchestratorNode, string?>(nameof(Role), o => o.Role);

    public static readonly DirectProperty<AgentOrchestratorNode, string> InitialProperty =
        AvaloniaProperty.RegisterDirect<AgentOrchestratorNode, string>(nameof(Initial), o => o.Initial);

    public static readonly DirectProperty<AgentOrchestratorNode, string> DoingProperty =
        AvaloniaProperty.RegisterDirect<AgentOrchestratorNode, string>(nameof(Doing), o => o.Doing);

    public static readonly DirectProperty<AgentOrchestratorNode, string> StatusTextProperty =
        AvaloniaProperty.RegisterDirect<AgentOrchestratorNode, string>(nameof(StatusText), o => o.StatusText);

    public static readonly DirectProperty<AgentOrchestratorNode, string> LeadTextProperty =
        AvaloniaProperty.RegisterDirect<AgentOrchestratorNode, string>(nameof(LeadText), o => o.LeadText);

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<AgentOrchestratorNode, bool>(nameof(IsSelected));

    public static readonly StyledProperty<bool> IsTreeProperty =
        AvaloniaProperty.Register<AgentOrchestratorNode, bool>(nameof(IsTree));

    private OrchestratorAgent? _agent;
    private AgentOrchestratorLabels _labels = AgentOrchestratorLabels.Default;
    private bool _isLead;
    private string _name = string.Empty;
    private string? _role;
    private string _initial = string.Empty;
    private string _doing = string.Empty;
    private string _statusText = string.Empty;
    private string _leadText = string.Empty;
    private LazyFocusRing? _focusRing;

    static AgentOrchestratorNode()
    {
        FocusableProperty.OverrideDefaultValue<AgentOrchestratorNode>(true);
        ClipToBoundsProperty.OverrideDefaultValue<AgentOrchestratorNode>(false);
    }

    public string AgentName { get => _name; private set => SetAndRaise(AgentNameProperty, ref _name, value); }
    public string? Role { get => _role; private set => SetAndRaise(RoleProperty, ref _role, value); }
    public string Initial { get => _initial; private set => SetAndRaise(InitialProperty, ref _initial, value); }
    public string Doing { get => _doing; private set => SetAndRaise(DoingProperty, ref _doing, value); }
    public string StatusText { get => _statusText; private set => SetAndRaise(StatusTextProperty, ref _statusText, value); }
    public string LeadText { get => _leadText; private set => SetAndRaise(LeadTextProperty, ref _leadText, value); }
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public bool IsTree { get => GetValue(IsTreeProperty); set => SetValue(IsTreeProperty, value); }

    /// <summary>The node's place in the graph (the lead is 0).</summary>
    public int Index { get; init; }

    /// <summary>Raised when the node is clicked or chosen with Enter or Space.</summary>
    public event Action<AgentOrchestratorNode>? Invoked;

    /// <summary>The agent shown; the node follows its changes.</summary>
    public OrchestratorAgent? Agent
    {
        get => _agent;
        set
        {
            if (ReferenceEquals(_agent, value))
            {
                return;
            }
            if (_agent is not null)
            {
                _agent.PropertyChanged -= OnAgentChanged;
            }
            _agent = value;
            if (_agent is not null)
            {
                _agent.PropertyChanged += OnAgentChanged;
            }
            Refresh();
        }
    }

    public AgentOrchestratorLabels Labels
    {
        get => _labels;
        set
        {
            _labels = value;
            Refresh();
        }
    }

    public bool IsLead
    {
        get => _isLead;
        init => _isLead = value;
    }

    private void OnAgentChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        var agent = _agent;
        var status = agent?.Status ?? OrchestratorAgentStatus.Idle;
        AgentName = agent?.Name ?? string.Empty;
        Role = string.IsNullOrWhiteSpace(agent?.Role) ? null : agent!.Role;
        Initial = AgentName.Length > 0 ? char.ToUpperInvariant(AgentName[0]).ToString() : "?";
        var doing = status switch
        {
            OrchestratorAgentStatus.Done or OrchestratorAgentStatus.Failed => agent?.Result ?? agent?.Activity ?? agent?.Task,
            _ => agent?.Activity ?? agent?.Task,
        };
        var empty = string.IsNullOrWhiteSpace(doing);
        Doing = empty ? _labels.NoTask : doing!;
        StatusText = _labels.StatusName(status);
        LeadText = _labels.Lead;
        PseudoClasses.Set(IdlePseudoClass, status == OrchestratorAgentStatus.Idle);
        PseudoClasses.Set(ThinkingPseudoClass, status == OrchestratorAgentStatus.Thinking);
        PseudoClasses.Set(WorkingPseudoClass, status == OrchestratorAgentStatus.Working);
        PseudoClasses.Set(WaitingPseudoClass, status == OrchestratorAgentStatus.Waiting);
        PseudoClasses.Set(DonePseudoClass, status == OrchestratorAgentStatus.Done);
        PseudoClasses.Set(FailedPseudoClass, status == OrchestratorAgentStatus.Failed);
        PseudoClasses.Set(LeadPseudoClass, _isLead);
        PseudoClasses.Set(EmptyPseudoClass, empty);
        AutomationProperties.SetName(this, _labels.NodeName(AgentName, Role, StatusText, empty ? null : doing));
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Refresh();
        _focusRing?.Attach(null, null);
        if (e.NameScope.Find<Panel>(RingHostPart) is { } host)
        {
            _focusRing = new LazyFocusRing(this, ArcDesignTokens.Key(ArcDesignTokenKind.FocusEdgeThickness),
                ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep18));
            // The ring sits 3px outside the 14px frame; the margin has no token, so the local value wins.
            _focusRing.Attach(ring =>
            {
                ring.Margin = new Thickness(-3);
                host.Children.Add(ring);
            }, ring => host.Children.Remove(ring));
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(SelectedPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == IsTreeProperty)
        {
            PseudoClasses.Set(TreePseudoClass, change.GetNewValue<bool>());
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            Focus(NavigationMethod.Pointer);
            Invoked?.Invoke(this);
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key is Key.Enter or Key.Space)
        {
            Invoked?.Invoke(this);
            e.Handled = true;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NodePeer(this);

    private sealed class NodePeer(AgentOrchestratorNode owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
