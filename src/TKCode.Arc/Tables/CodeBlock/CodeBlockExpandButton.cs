using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The "Show all N lines" / "Show fewer lines" toggle under a collapsible <see cref="CodeBlock"/>. Both labels
/// reserve the cell so the chevron never moves; the visible label swaps with a blur rise and the chevron turns
/// on the snappy spring (theme). Exposes the expand / collapse pattern like Arc's <c>aria-expanded</c>.
/// </summary>
internal sealed class CodeBlockExpandButton : AvaloniaButton
{
    public const string ExpandedPseudoClass = ":expanded";

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<CodeBlockExpandButton, bool>(nameof(IsExpanded));

    public static readonly StyledProperty<string> CollapsedLabelProperty =
        AvaloniaProperty.Register<CodeBlockExpandButton, string>(nameof(CollapsedLabel), string.Empty);

    public static readonly StyledProperty<string> ExpandedLabelProperty =
        AvaloniaProperty.Register<CodeBlockExpandButton, string>(nameof(ExpandedLabel), string.Empty);

    public static readonly DirectProperty<CodeBlockExpandButton, string> CurrentLabelProperty =
        AvaloniaProperty.RegisterDirect<CodeBlockExpandButton, string>(nameof(CurrentLabel), o => o.CurrentLabel);

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public string CollapsedLabel
    {
        get => GetValue(CollapsedLabelProperty);
        set => SetValue(CollapsedLabelProperty, value);
    }

    public string ExpandedLabel
    {
        get => GetValue(ExpandedLabelProperty);
        set => SetValue(ExpandedLabelProperty, value);
    }

    private string _currentLabel = string.Empty;

    public string CurrentLabel
    {
        get => _currentLabel;
        private set => SetAndRaise(CurrentLabelProperty, ref _currentLabel, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty || change.Property == CollapsedLabelProperty || change.Property == ExpandedLabelProperty)
        {
            CurrentLabel = IsExpanded ? ExpandedLabel : CollapsedLabel;
            PseudoClasses.Set(ExpandedPseudoClass, IsExpanded);
            Avalonia.Automation.AutomationProperties.SetName(this, CurrentLabel);
        }
    }

    // The focus ring is built on the first keyboard focus rather than declared hidden in every block's template.
    private readonly TablesBFocusRing _focusRing = new(new Thickness(4), CodeBlockTokens.Key(CodeBlockTokenKind.ExpandFocusRingRadius));

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing.Attach(e.NameScope.Find<Panel>("PART_Layers"));
        _focusRing.Update(Classes.Contains(":focus-visible"));
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing.Update(Classes.Contains(":focus-visible"));
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing.Update(Classes.Contains(":focus-visible"));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ExpandPeer(this);

    private sealed class ExpandPeer : ButtonAutomationPeer, IExpandCollapseProvider
    {
        public ExpandPeer(CodeBlockExpandButton owner) : base(owner)
        {
        }

        private CodeBlockExpandButton Button => (CodeBlockExpandButton)Owner;

        public ExpandCollapseState ExpandCollapseState => Button.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (!Button.IsExpanded)
            {
                Button.RaiseClick();
            }
        }

        public void Collapse()
        {
            if (Button.IsExpanded)
            {
                Button.RaiseClick();
            }
        }
    }

    private void RaiseClick() => OnClick();
}
