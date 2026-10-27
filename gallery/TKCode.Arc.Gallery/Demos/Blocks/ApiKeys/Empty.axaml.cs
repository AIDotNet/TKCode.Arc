using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ApiKeysEmptyDemo : UserControl
{
    public ApiKeysEmptyDemo()
    {
        InitializeComponent();
        Block.Scopes.AddRange(ApiKeysDemoData.Scopes());

        // Handled: the button restores the samples instead of opening the form (its default).
        Block.EmptyActionInvoked += (_, e) =>
        {
            e.Handled = true;
            Block.Keys.AddRange(ApiKeysDemoData.Keys());
        };
    }
}
