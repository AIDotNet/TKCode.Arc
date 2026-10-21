using Avalonia.Controls;
using Avalonia.Input;

namespace TKCode.Arc.Gallery.Demos;

public partial class ShortcutRecorderReservedDemo : UserControl
{
    public ShortcutRecorderReservedDemo()
    {
        InitializeComponent();
        var platform = ShortcutFormatter.CurrentPlatform;
        string Caps(KeyGesture gesture) => ShortcutFormatter.Format(gesture, platform).Text;

        var toggleSidebar = ShortcutFormatter.Parse("mod+b", platform);
        var duplicateLine = ShortcutFormatter.Parse("mod+d", platform);
        var closeTab = ShortcutFormatter.Parse("mod+w", platform);
        var copy = ShortcutFormatter.Parse("mod+c", platform);

        Sidebar.Value = toggleSidebar;
        Sidebar.ResetValue = toggleSidebar;
        Sidebar.Description = $"Try {Caps(closeTab)} or {Caps(copy)}.";
        Duplicate.Value = duplicateLine;
        Duplicate.ResetValue = duplicateLine;
    }
}
