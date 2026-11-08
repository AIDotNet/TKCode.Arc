using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class EnumSelectorBindingDemo : UserControl
{
    private readonly EnumSelectorDemoState _state = new();

    public EnumSelectorBindingDemo()
    {
        InitializeComponent();
        DataContext = _state;
        // The view model changes; both selectors follow.
        Reset.Click += (_, _) => _state.Permissions = DemoFilePermissions.Read;
    }
}
