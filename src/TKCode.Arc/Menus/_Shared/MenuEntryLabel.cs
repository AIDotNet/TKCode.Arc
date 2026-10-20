using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>A quiet group label inside a <see cref="MenuSurface"/> ("Sort by", "Workspace"). Not focusable; navigation skips it.</summary>
public class MenuEntryLabel : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MenuEntryLabel, string?>(nameof(Text));

    /// <summary>Inherited from the owning <see cref="MenuSurface"/>.</summary>
    public static readonly StyledProperty<MenuSurfaceVariant> VariantProperty =
        MenuSurface.VariantProperty.AddOwner<MenuEntryLabel>();

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public MenuSurfaceVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    static MenuEntryLabel()
    {
        FocusableProperty.OverrideDefaultValue<MenuEntryLabel>(false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MenuEntryLabelAutomationPeer(this);

    private sealed class MenuEntryLabelAutomationPeer : ControlAutomationPeer
    {
        public MenuEntryLabelAutomationPeer(MenuEntryLabel owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string? GetNameCore() => ((MenuEntryLabel)Owner).Text;
    }
}
