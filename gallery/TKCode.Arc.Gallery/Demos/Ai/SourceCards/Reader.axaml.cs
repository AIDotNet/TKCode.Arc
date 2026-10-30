using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Opens sources in an in-app reader, and shows a failed search with Retry.</summary>
public partial class SourceCardsReaderDemo : UserControl
{
    private readonly SourceCardsSource[] _all = SourceCardsDemoData.Sources();

    public SourceCardsReaderDemo()
    {
        InitializeComponent();
        Cards.Sources = _all;
    }

    private void OnSourceInvoked(object? sender, SourceCardsSourceEventArgs e)
    {
        // Handled: the card does not open the browser.
        e.Handled = true;
        Reader.Text = $"[{e.Number}] {e.Item.Title}\n{e.Item.Snippet}";
    }

    private void OnFail(object? sender, RoutedEventArgs e)
    {
        Cards.Sources = _all[..2];
        Cards.ErrorText = SourceCardsDemoData.ErrorText;
        Cards.Status = SourceCardsStatus.Error;
    }

    private void OnRetry(object? sender, RoutedEventArgs e)
    {
        Cards.ErrorText = null;
        Cards.Sources = _all;
        Cards.Status = SourceCardsStatus.Done;
    }
}
