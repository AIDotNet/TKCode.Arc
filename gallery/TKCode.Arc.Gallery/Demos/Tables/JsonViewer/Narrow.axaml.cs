using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class JsonViewerNarrowDemo : UserControl
{
    public JsonViewerNarrowDemo()
    {
        InitializeComponent();
        Viewer.Json = """{ "user": { "id": 7, "handle": "grace", "roles": ["admin", "editor"], "active": true } }""";
    }
}
