using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MentionInputSubmitOnEnterDemo : UserControl
{
    public MentionInputSubmitOnEnterDemo()
    {
        InitializeComponent();
    }

    private void OnSubmitted(object? sender, MentionSubmittedEventArgs e)
    {
        Log.Children.Remove(Empty);
        var mentions = e.Value.Mentions.Count == 1 ? "1 mention" : $"{e.Value.Mentions.Count} mentions";
        Log.Children.Add(new TextBlock { Text = $"Sent: {e.Value.Text.Trim()}  ·  {mentions}", Classes = { "sent" } });
        Input.Clear();
    }
}
