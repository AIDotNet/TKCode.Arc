using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A hairline between groups of rows in a <see cref="MenuSurface"/>. It bleeds 4px into the surface inset like Arc's
/// separators; keyboard navigation and the highlight pass over it. (Rows can also draw one with
/// <see cref="MenuEntry.HasSeparatorBefore"/>.)
/// </summary>
public class MenuEntrySeparator : TemplatedControl
{
    /// <summary>Inherited from the owning <see cref="MenuSurface"/>.</summary>
    public static readonly StyledProperty<MenuSurfaceVariant> VariantProperty =
        MenuSurface.VariantProperty.AddOwner<MenuEntrySeparator>();

    public MenuSurfaceVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    static MenuEntrySeparator()
    {
        FocusableProperty.OverrideDefaultValue<MenuEntrySeparator>(false);
        IsHitTestVisibleProperty.OverrideDefaultValue<MenuEntrySeparator>(false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MenuEntrySeparatorAutomationPeer(this);

    private sealed class MenuEntrySeparatorAutomationPeer : ControlAutomationPeer
    {
        public MenuEntrySeparatorAutomationPeer(Control owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Separator;

        protected override bool IsContentElementCore() => false;
    }
}
