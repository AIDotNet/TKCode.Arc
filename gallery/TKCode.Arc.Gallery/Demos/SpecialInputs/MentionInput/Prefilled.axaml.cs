using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MentionInputPrefilledDemo : UserControl
{
    public MentionInputPrefilledDemo()
    {
        InitializeComponent();
        const string text = "Thanks @Maya Chen, the new flow reads well. Can @Jordan Lee review it in #design before Friday?";
        Input.Value = new MentionValue(text,
        [
            Find(text, MentionKind.Person, "u_maya", "Maya Chen"),
            Find(text, MentionKind.Person, "u_jordan", "Jordan Lee"),
            Find(text, MentionKind.Channel, "c_design", "design")
        ]);
        Count.Text = Summary(Input.Value);
        Input.ValueChanged += (_, e) => Count.Text = Summary(e.NewValue);
    }

    private static Mention Find(string text, MentionKind kind, string id, string label)
    {
        var token = MentionSerializer.MentionText(kind, label);
        var start = text.IndexOf(token, StringComparison.Ordinal);
        return new Mention(kind, id, label, start, start + token.Length);
    }

    private static string Summary(MentionValue value) => value.Mentions.Count switch
    {
        0 => "No mentions",
        1 => "1 mention: " + value.Mentions[0].Text,
        _ => $"{value.Mentions.Count} mentions: " + string.Join(", ", value.Mentions.Select(static m => m.Text))
    };
}
