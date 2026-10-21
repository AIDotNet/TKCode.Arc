using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MentionInputDisabledDemo : UserControl
{
    public MentionInputDisabledDemo()
    {
        InitializeComponent();
        const string text = "Waiting on @Alex Rivera before this thread reopens.";
        var start = text.IndexOf("@Alex Rivera", StringComparison.Ordinal);
        Input.Value = new MentionValue(text,
        [
            new Mention(MentionKind.Person, "u_alex", "Alex Rivera", start, start + "@Alex Rivera".Length)
        ]);
    }
}
