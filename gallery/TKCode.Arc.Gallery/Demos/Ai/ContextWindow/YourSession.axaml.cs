using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ContextWindowYourSessionDemo : UserControl
{
    private readonly List<string> _entries = new();

    public ContextWindowYourSessionDemo()
    {
        InitializeComponent();
        Card.Models.AddRange(ContextWindowDemoData.Models());
        Card.Items.AddRange(ContextWindowDemoData.Items());
        Card.Turns.AddRange(ContextWindowDemoData.Turns());
        Card.Files.AddRange(ContextWindowDemoData.Files());
    }

    private void OnItemsChanged(object? sender, ContextWindowItemsChangedEventArgs e) =>
        Add($"{e.Change}: {e.Items.Count} items, {Card.UsedTokens:N0} tokens");

    private void OnCompacted(object? sender, ContextWindowCompactedEventArgs e) =>
        Add($"Compacted ({e.Reason}): {e.FoldedCount} messages, {e.FoldedTokens:N0} → {e.SummaryTokens:N0} tokens");

    private void OnModelChanged(object? sender, ContextWindowModelChangedEventArgs e) =>
        Add($"Model: {e.OldModel?.Name} → {e.NewModel?.Name}");

    private void Add(string entry)
    {
        _entries.Add(entry);
        if (_entries.Count > 3)
        {
            _entries.RemoveAt(0);
        }
        Log.Text = string.Join("\n", _entries);
    }
}
