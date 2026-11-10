using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class McpServersConnectDemo : UserControl
{
    private int _attempts;

    public McpServersConnectDemo()
    {
        InitializeComponent();
        Block.ConnectAsync = async (server, cancellation) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(900), cancellation);
            _attempts++;
            if (_attempts % 2 == 1)
            {
                return McpConnectResult.Failure(
                    "The server closed the connection during the handshake.",
                    $"→ initialize {server.Endpoint}\n← connection reset by peer\n✕ attempt {_attempts} failed");
            }
            return McpConnectResult.Success(McpServersSamples.ToolsFor(server.Id, server.Name), "3.0.0");
        };
        Block.StatusChanged += (_, e) => Write($"{e.Server.Name}: {e.OldStatus} → {e.NewStatus}");
        Block.ToolToggled += (_, e) => Write($"{e.Server.Name}: {e.Tools.Count} tool(s) {(e.IsEnabled ? "on" : "off")}");
        Block.ServerToggled += (_, e) => Write($"{e.Server.Name} turned {(e.IsEnabled ? "on" : "off")}");
        Write("Press Reconnect or Retry on a server.");
    }

    private void Write(string line) => Log.Text = line;
}
