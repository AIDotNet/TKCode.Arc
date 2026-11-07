using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The header's "All cards" pill (Arc Wallet stack, design reference): 32 high, a 1px border, the surface fill, a 16px
/// chevron-left (stroke 1.75) 6px before 14 / 500 text; it puts the open card back.
/// </summary>
internal sealed class WalletStackHeaderButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(WalletStackHeaderButton);
}
