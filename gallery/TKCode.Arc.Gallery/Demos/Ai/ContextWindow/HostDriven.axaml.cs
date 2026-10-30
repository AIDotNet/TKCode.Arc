using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ContextWindowHostDrivenDemo : UserControl
{
    private int _reply;
    private int _file;

    public ContextWindowHostDrivenDemo()
    {
        InitializeComponent();
        Card.Models.Add(ContextWindowDemoData.Models().Last());
        // Twice the documentation session, so a few replies take the 256K window past 80%.
        Card.Items.AddRange(ContextWindowDemoData.Items(scale: 2));
        // Your summarizer knows the real size; this one says 5% of what it folds, at least 2,000 tokens.
        Card.Summarizer = folded => Math.Max(2_000, folded.Sum(item => item.Tokens) / 20);
    }

    private void OnAddReply(object? sender, RoutedEventArgs e)
    {
        var (label, tokens) = ContextWindowDemoData.Replies[_reply++ % ContextWindowDemoData.Replies.Length];
        Card.AddItem(new ContextWindowItem(ContextWindowCategory.Messages, label, tokens));
    }

    private void OnAddFile(object? sender, RoutedEventArgs e)
    {
        Card.AddItem(new ContextWindowItem(ContextWindowCategory.Files, $"exports/batch-{++_file}.json", 6_500));
    }

    private void OnAutoChanged(object? sender, RoutedEventArgs e)
    {
        Card.AutoCompact = AutoSwitch.IsChecked == true;
    }

    private void OnCompacted(object? sender, ContextWindowCompactedEventArgs e)
    {
        Log.Text = $"Folded {e.FoldedCount} messages ({e.FoldedTokens:N0} tokens) into {e.SummaryTokens:N0}; " +
                   $"{e.Before:N0} → {e.After:N0} tokens used.";
    }
}
