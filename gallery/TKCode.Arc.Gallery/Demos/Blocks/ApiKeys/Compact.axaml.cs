using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ApiKeysCompactDemo : UserControl
{
    public ApiKeysCompactDemo()
    {
        InitializeComponent();
        Block.Keys.AddRange(ApiKeysDemoData.Keys());
        Block.Scopes.AddRange(ApiKeysDemoData.Scopes());
        Block.Expand(Block.Keys[0]);
    }
}
