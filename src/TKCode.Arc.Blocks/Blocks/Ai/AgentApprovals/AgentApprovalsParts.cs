using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>A waiting request in the list of an <see cref="AgentApprovals"/> queue: glyph, title, agent, time and risk.</summary>
[PseudoClasses(SelectedPseudoClass, HighPseudoClass, MediumPseudoClass)]
internal sealed class AgentApprovalsRow : AiAgentButton
{
    public const string SelectedPseudoClass = ":selected";
    public const string HighPseudoClass = ":high";
    public const string MediumPseudoClass = ":medium";

    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<AgentApprovalsRow, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AgentApprovalsRow, string?>(nameof(Title));

    public static readonly StyledProperty<string?> MetaProperty =
        AvaloniaProperty.Register<AgentApprovalsRow, string?>(nameof(Meta));

    public static readonly StyledProperty<string?> RiskTextProperty =
        AvaloniaProperty.Register<AgentApprovalsRow, string?>(nameof(RiskText));

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }

    public string? RiskText { get => GetValue(RiskTextProperty); set => SetValue(RiskTextProperty, value); }

    public AgentApprovalRequest? Request { get; set; }

    public AgentApprovals? Owner { get; set; }

    public bool IsSelected
    {
        get => PseudoClasses.Contains(SelectedPseudoClass);
        set => PseudoClasses.Set(SelectedPseudoClass, value);
    }

    public void Refresh()
    {
        if (Request is not { } request)
        {
            return;
        }
        var labels = Owner?.Labels ?? new AgentApprovalsLabels();
        Glyph = AgentApprovals.GlyphOf(request.Kind);
        Title = request.Title;
        var time = request.Requested is { } requested ? labels.Time(requested, Owner?.Clock() ?? DateTimeOffset.Now) : null;
        Meta = time is null ? request.Agent : $"{request.Agent} · {time}";
        RiskText = labels.Risk(request.Risk);
        PseudoClasses.Set(HighPseudoClass, request.Risk == AgentApprovalRisk.High);
        PseudoClasses.Set(MediumPseudoClass, request.Risk == AgentApprovalRisk.Medium);
        AutomationProperties.SetName(this, labels.RowName(request.Title, request.Agent, RiskText));
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (Request is { } request)
        {
            Owner?.Select(request);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    /// <summary>Reads as a list item that is selected or not.</summary>
    private sealed class RowPeer(AgentApprovalsRow owner) : ButtonAutomationPeer(owner), Avalonia.Automation.Provider.ISelectionItemProvider
    {
        private AgentApprovalsRow Row => (AgentApprovalsRow)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        public bool IsSelected => Row.IsSelected;

        public Avalonia.Automation.Provider.ISelectionProvider? SelectionContainer => null;

        public void Select() => Invoke();

        public void AddToSelection() => Invoke();

        public void RemoveFromSelection()
        {
        }
    }
}
