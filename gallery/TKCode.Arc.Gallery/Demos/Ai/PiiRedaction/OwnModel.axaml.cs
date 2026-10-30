using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The card over an app's own model: names from the app, a stand-in model through Sender and an audit log.</summary>
public partial class PiiRedactionOwnModelDemo : UserControl
{
    public PiiRedactionOwnModelDemo()
    {
        InitializeComponent();
        Card.Message = "Hello, this is Mara Quill from the Lisbon office. Please update the booking for Theo Brandt " +
                       "and send the invoice to mara.quill@example.com. My direct line is +1 555 010 0188.";
        // The signed-in person and their contacts; introductions are found on top of these.
        Card.Names = ["Mara Quill", "Theo Brandt"];
        Card.Sender = async (prompt, token) =>
        {
            // Your backend sends the safe prompt to the model; this stand-in answers after a short wait.
            await Task.Delay(700, token);
            return "Done, [NAME_1]. The booking for [NAME_2] is updated and the invoice is on its way to [EMAIL_1]. " +
                   "If anything changes I will call [PHONE_1].";
        };
    }

    private void OnRedacted(object? sender, PiiRedactedEventArgs e)
    {
        // Log the types server side, never the values next to the prompt.
        var types = e.Entities.GroupBy(x => x.Type).Select(g => $"{g.Count()} {g.Key.ToString().ToLowerInvariant()}");
        Log.Text = "pii.redacted: " + string.Join(", ", types);
    }

    private void OnReplyRestored(object? sender, PiiReplyRestoredEventArgs e)
    {
        Log.Text = $"Reply restored: {e.Raw.Length} characters from the model, {e.Restored.Length} shown to the person.";
    }
}
