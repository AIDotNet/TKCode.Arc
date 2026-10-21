using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class MentionInputCommentBoxDemo : UserControl
{
    public MentionInputCommentBoxDemo()
    {
        InitializeComponent();
    }

    private void OnMentionAdded(object? sender, MentionAddedEventArgs e)
    {
        Added.Text = $"Added {e.Mention.Text} ({e.Mention.Id})";
    }

    private void OnMentionClick(object? sender, RoutedEventArgs e)
    {
        Input.OpenSuggestions(MentionKind.Person);
    }

    private void OnChannelClick(object? sender, RoutedEventArgs e)
    {
        Input.OpenSuggestions(MentionKind.Channel);
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        Input.Clear();
    }
}
