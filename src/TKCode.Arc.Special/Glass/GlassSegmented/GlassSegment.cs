using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One option of a <see cref="GlassSegmented"/>: a label with an optional icon. Write it inline
/// (<c>&lt;arc:GlassSegment Content="Day" /&gt;</c>) or let the control create it for each item of
/// <c>ItemsSource</c>. Its label brightens while it is selected or while the dragged lens is over it.
/// </summary>
[TemplatePart(RootPart, typeof(Panel))]
[PseudoClasses(SelectedPseudoClass, UnderLensPseudoClass)]
public class GlassSegment : ContentControl, IGlassLensItem
{
    public const string RootPart = "PART_Root";
    public const string SelectedPseudoClass = ":selected";
    public const string UnderLensPseudoClass = ":under-lens";

    /// <summary>Whether this segment is the selected one (set by the owning <see cref="GlassSegmented"/>).</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        SelectingItemsControl.IsSelectedProperty.AddOwner<GlassSegment>();

    /// <summary>An optional 24×24 stroke icon before the label (for example <c>ArcIcons.Sun</c>).</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<GlassSegment, Geometry?>(nameof(Icon));

    private LazyFocusRing? _focusRing;

    static GlassSegment()
    {
        FocusableProperty.OverrideDefaultValue<GlassSegment>(true);
    }

    /// <inheritdoc cref="IsSelectedProperty"/>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>True while the lens is over this segment (tests read it).</summary>
    internal bool IsUnderLens => PseudoClasses.Contains(UnderLensPseudoClass);

    void IGlassLensItem.SetUnderLens(bool value) => PseudoClasses.Set(UnderLensPseudoClass, value);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(SelectedPseudoClass, IsSelected);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, GlassSegmentTokens.Key(GlassSegmentTokenKind.FocusRingMargin), ArcDesignTokenKind.RadiusPill);
        _focusRing.Attach(e.NameScope.Find<Panel>(RootPart), below: true);
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

    /// <summary>Whether the keyboard focus ring shows (tests read it).</summary>
    internal bool IsFocusRingShown => _focusRing?.Ring?.IsVisible == true;

    protected override AutomationPeer OnCreateAutomationPeer() => new GlassSegmentAutomationPeer(this);

    private sealed class GlassSegmentAutomationPeer : ContentControlAutomationPeer, ISelectionItemProvider
    {
        public GlassSegmentAutomationPeer(GlassSegment owner) : base(owner)
        {
        }

        private GlassSegment Segment => (GlassSegment)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

        protected override string? GetNameCore() =>
            AutomationProperties.GetName(Segment) is { Length: > 0 } name ? name : Segment.Content as string ?? base.GetNameCore();

        public bool IsSelected => Segment.IsSelected;

        public ISelectionProvider? SelectionContainer => null;

        public void Select()
        {
            if (ItemsControl.ItemsControlFromItemContainer(Segment) is SelectingItemsControl owner &&
                owner.IndexFromContainer(Segment) is var index and >= 0 && Segment.IsEffectivelyEnabled)
            {
                owner.SelectedIndex = index;
            }
        }

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }
    }
}
