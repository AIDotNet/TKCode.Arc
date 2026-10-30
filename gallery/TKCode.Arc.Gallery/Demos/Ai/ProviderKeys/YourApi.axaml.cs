using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Provider keys wired to an app: a test delegate standing in for a server call, and an event log.</summary>
public partial class ProviderKeysYourApiDemo : UserControl
{
    private readonly List<string> _entries = new();

    public ProviderKeysYourApiDemo()
    {
        InitializeComponent();
        // What your server returns: only the last four characters, never the full key (reveal and copy are off).
        Card.Keys = ProviderKeysDemoData.StoredKeys;
        Card.TestKey = TestOnServerAsync;
    }

    /// <summary>Stands in for a request to your server, which would call the provider (for example its models list).</summary>
    private static async Task<ProviderKeyTestResult> TestOnServerAsync(ProviderKeyTestRequest request)
    {
        await Task.Delay(900, request.CancellationToken);
        if (request.Key is { } key && key.Contains("revoked", StringComparison.OrdinalIgnoreCase))
        {
            return ProviderKeyTestResult.Fail("401 Unauthorized");
        }
        return ProviderKeyTestResult.Ok("sample server, nothing was sent");
    }

    private void OnSaved(object? sender, ProviderKeySavedEventArgs e) => Add($"Saved ({e.Mode}): {e.Provider}, ends in {e.Key[^4..]}");

    private void OnRemoved(object? sender, ProviderKeyEventArgs e) => Add($"Removed for good: {e.Provider}");

    private void OnCopied(object? sender, ProviderKeyEventArgs e) => Add($"Copied: {e.Provider}");

    private void OnKeysChanged(object? sender, ProviderKeysChangedEventArgs e) => Add($"{e.Change}: {e.Provider} ({e.Keys.Count} keys)");

    private void Add(string entry)
    {
        _entries.Add(entry);
        if (_entries.Count > 4)
        {
            _entries.RemoveAt(0);
        }
        Log.Text = string.Join("\n", _entries);
    }
}
