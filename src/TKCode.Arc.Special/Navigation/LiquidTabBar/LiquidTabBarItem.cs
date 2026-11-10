using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Mixins;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaSelectingItemsControl = Avalonia.Controls.Primitives.SelectingItemsControl;

namespace TKCode.Arc;

/// <summary>
/// One tab of a <see cref="LiquidTabBar"/>: an optional glyph and its label (<see cref="ContentControl.Content"/>).
/// A disabled tab is skipped by the arrow keys and cannot be selected.
/// </summary>
[TemplatePart(RootPart, typeof(Panel))]
[PseudoClasses(SelectedPseudoClass, PressedPseudoClass)]
public class LiquidTabBarItem : ContentControl, ISelectable
{
    internal const string RootPart = "PART_Root";
    internal const string SelectedPseudoClass = ":selected";
    internal const string PressedPseudoClass = ":pressed";

    /// <summary>Whether this tab is the selected one.</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaSelectingItemsControl.IsSelectedProperty.AddOwner<LiquidTabBarItem>();

    /// <summary>A 24 × 24 stroked glyph before the label; null shows the label alone.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<LiquidTabBarItem, Geometry?>(nameof(Glyph));

    private LazyFocusRing? _focusRing;

    static LiquidTabBarItem()
    {
        SelectableMixin.Attach<LiquidTabBarItem>(IsSelectedProperty);
        PressedMixin.Attach<LiquidTabBarItem>();
        FocusableProperty.OverrideDefaultValue<LiquidTabBarItem>(true);
    }

    /// <inheritdoc cref="IsSelectedProperty"/>
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    /// <inheritdoc cref="GlyphProperty"/>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    private LiquidTabBar? Owner => ItemsControl.ItemsControlFromItemContainer(this) as LiquidTabBar;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty && change.NewValue is string text)
        {
            AutomationProperties.SetName(this, text);
        }
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, LiquidTabBarTokens.Key(LiquidTabBarTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusPill));
        _focusRing.Attach(e.NameScope.Find<Panel>(RootPart));
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // A mouse or pen selects on press; touch waits for the release, so a swipe across the bar can still scroll.
        if (e.Handled || e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            Owner?.OnItemPressed(this);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Handled || e.Pointer.Type != PointerType.Touch)
        {
            return;
        }
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            Owner?.OnItemPressed(this);
        }
    }

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new LiquidTabBarItemAutomationPeer(this);

    private sealed class LiquidTabBarItemAutomationPeer : ListItemAutomationPeer
    {
        public LiquidTabBarItemAutomationPeer(LiquidTabBarItem owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;
    }
}
