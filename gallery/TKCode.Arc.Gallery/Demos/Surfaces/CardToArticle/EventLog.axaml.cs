using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class CardToArticleEventLogDemo : UserControl
{
    private readonly List<string> _lines = new();

    public CardToArticleEventLogDemo()
    {
        InitializeComponent();
        Stories.Items = CardToArticleSamples.AllStories();
    }

    private void OnOpenMeteors(object? sender, RoutedEventArgs e) => Stories.Open("city-meteors");

    private void OnClose(object? sender, RoutedEventArgs e) => Stories.Close();

    private void OnOpenItemChanged(object? sender, CardToArticleOpenChangedEventArgs e)
    {
        var line = e.NewId is { } id ? $"Opened {id} ({e.Reason})" : $"Closed {e.OldId} ({e.Reason})";
        _lines.Insert(0, line);
        if (_lines.Count > 4)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }
}
