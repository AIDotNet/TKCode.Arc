using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The header of a <see cref="RibbonTab"/> in the ribbon's tab row (built by the ribbon, one per tab).</summary>
[PseudoClasses(SelectedPseudoClass, ContextualPseudoClass)]
internal sealed class RibbonTabHeader : TemplatedControl
{
    public const string SelectedPseudoClass = ":selected";
    public const string ContextualPseudoClass = ":contextual";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<RibbonTabHeader, string?>(nameof(Text));

    private LazyFocusRing? _focusRing;

    public RibbonTabHeader(Ribbon owner, RibbonTab tab)
    {
        Owner = owner;
        Tab = tab;
        Focusable = true;
        Sync();
    }

    public Ribbon Owner { get; }

    public RibbonTab Tab { get; }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Copies the tab's header, visibility, enabled state and kind.</summary>
    public void Sync()
    {
        Text = Tab.Header;
        IsVisible = Tab.IsVisible;
        IsEnabled = Tab.IsEnabled;
        PseudoClasses.Set(ContextualPseudoClass, Tab.IsContextual);
        AutomationProperties.SetName(this, Tab.Header);
    }

    public void SetSelected(bool selected)
    {
        PseudoClasses.Set(SelectedPseudoClass, selected);
        KeyboardNavigation.SetIsTabStop(this, selected);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, RibbonTokens.Key(RibbonTokenKind.TabFocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep10));
        _focusRing.Attach(AddRing, RemoveRing);
    }

    private void AddRing(Control ring)
    {
        LogicalChildren.Add(ring);
        VisualChildren.Add(ring);
        InvalidateMeasure();
    }

    private void RemoveRing(Control ring)
    {
        VisualChildren.Remove(ring);
        LogicalChildren.Remove(ring);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        _focusRing?.Ring?.Measure(availableSize);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        _focusRing?.Ring?.Arrange(new Rect(size));
        return size;
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
            e.Handled = true;
            if (e.ClickCount == 2)
            {
                Owner.ToggleMinimized();
            }
            else
            {
                Owner.OnHeaderPressed(this);
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RibbonTabHeaderAutomationPeer(this);

    private sealed class RibbonTabHeaderAutomationPeer : ControlAutomationPeer
    {
        public RibbonTabHeaderAutomationPeer(RibbonTabHeader owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
