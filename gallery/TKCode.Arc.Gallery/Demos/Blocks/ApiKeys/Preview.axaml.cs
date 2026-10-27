using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ApiKeysPreviewDemo : UserControl
{
    public ApiKeysPreviewDemo()
    {
        InitializeComponent();
        Block.Keys.AddRange(ApiKeysDemoData.Keys());
        Block.Scopes.AddRange(ApiKeysDemoData.Scopes());
    }
}
