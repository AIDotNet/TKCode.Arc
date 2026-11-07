using Avalonia.Collections;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class LinkUnfurlResolverDemo : UserControl
{
    private readonly AvaloniaList<string> _log = new();

    public LinkUnfurlResolverDemo()
    {
        InitializeComponent();
        Log.ItemsSource = _log;
        // A stand-in for your unfurl endpoint: a short wait, then a preview built here (or none).
        Composer.Resolver = async (url, cancellationToken) =>
        {
            await Task.Delay(700, cancellationToken);
            if (url.Contains("missing", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            var preview = LinkUnfurl.CreatePlainPreview(url);
            preview.Description = $"Fetched by the demo's resolver at {DateTime.Now:HH:mm:ss}";
            return preview;
        };
        Composer.SendHandler = async message =>
        {
            await Task.Delay(900);
            if (message.Text.Contains("fail", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The server turned this message down. Remove \"fail\" and send again.");
            }
        };
        Composer.LinkAdded += (_, e) => Add($"Added {e.Link.Address}");
        Composer.LinkResolved += (_, e) => Add($"{e.Link.Status}: {e.Link.Preview?.Title ?? e.Link.Address}");
        Composer.PreviewToggled += (_, e) => Add(e.Link.IsExpanded ? "Card opened" : "Card folded");
        Composer.LinkRemoved += (_, e) => Add($"Removed {e.Link.Address}");
        Composer.SendRequested += (_, e) => Add($"Sending {e.Message.Text.Length} characters, {e.Message.Previews.Count} preview(s)");
    }

    private void Add(string line)
    {
        _log.Insert(0, line);
        if (_log.Count > 8)
        {
            _log.RemoveAt(_log.Count - 1);
        }
    }
}
