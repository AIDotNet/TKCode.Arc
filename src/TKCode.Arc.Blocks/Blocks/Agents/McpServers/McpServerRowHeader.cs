using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The button that opens and closes one server of <see cref="McpServers"/>: the mark, the name with its transport tag, the
/// status line and the chevron. It reports expanded or collapsed to assistive technology; the keyboard ring is built the
/// first time it shows (<see cref="LazyFocusRing"/>).
/// </summary>
[TemplatePart(RingHostPart, typeof(Panel))]
[PseudoClasses(ExpandedPseudoClass)]
internal sealed class McpServerRowHeader : AvaloniaButton
{
    public const string RingHostPart = "PART_RingHost";
    public const string ExpandedPseudoClass = ":expanded";

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<McpServerRowHeader, bool>(nameof(IsExpanded));

    private LazyFocusRing? _ring;

    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    protected override Type StyleKeyOverride => typeof(McpServerRowHeader);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _ring?.Attach(null, null);
        if (e.NameScope.Find<Panel>(RingHostPart) is { } host)
        {
            _ring = new LazyFocusRing(this, ArcDesignTokens.Key(ArcDesignTokenKind.FocusEdgeThickness), ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep18));
            _ring.Attach(ring =>
            {
                ring.Margin = new Thickness(-2);
                host.Children.Add(ring);
            }, ring => host.Children.Remove(ring));
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _ring?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _ring?.Update();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            PseudoClasses.Set(ExpandedPseudoClass, IsExpanded);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    private sealed class Peer : ButtonAutomationPeer, IExpandCollapseProvider
    {
        public Peer(McpServerRowHeader owner) : base(owner)
        {
        }

        private McpServerRowHeader Header => (McpServerRowHeader)Owner;

        public ExpandCollapseState ExpandCollapseState => Header.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (!Header.IsExpanded)
            {
                Invoke();
            }
        }

        public void Collapse()
        {
            if (Header.IsExpanded)
            {
                Invoke();
            }
        }
    }
}
