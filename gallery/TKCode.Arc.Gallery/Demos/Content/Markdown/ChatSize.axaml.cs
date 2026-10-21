using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MarkdownChatSizeDemo : UserControl
{
    public MarkdownChatSizeDemo()
    {
        InitializeComponent();
        Message.Text = """
            Deployed **web-42** to `staging`. Logs: [build #1042](https://uiarc.dev).

            ```sh
            kubectl rollout status deploy/web
            ```
            """;
    }
}
