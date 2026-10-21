using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MentionInputSerializedDemo : UserControl
{
    public MentionInputSerializedDemo()
    {
        InitializeComponent();
        const string text = "Ship notes are up, @Priya Natarajan. Posting in #engineering next.";
        var priya = text.IndexOf("@Priya Natarajan", StringComparison.Ordinal);
        var engineering = text.IndexOf("#engineering", StringComparison.Ordinal);
        Input.Value = new MentionValue(text,
        [
            new Mention(MentionKind.Person, "u_priya", "Priya Natarajan", priya, priya + "@Priya Natarajan".Length),
            new Mention(MentionKind.Channel, "c_engineering", "engineering", engineering, engineering + "#engineering".Length)
        ]);
        Output.Text = MentionSerializer.Serialize(Input.Value);
        Input.ValueChanged += (_, e) => Output.Text = MentionSerializer.Serialize(e.NewValue);
    }
}
