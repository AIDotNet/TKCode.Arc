using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Your own email, quick replies, model and writer.</summary>
public partial class ReplyDraftOwnDataDemo : UserControl
{
    public ReplyDraftOwnDataDemo()
    {
        InitializeComponent();
        Draft.Message = new ReplyDraftMessage
        {
            FromName = "Jonas Weber",
            FromEmail = "jonas@fabrikam.example",
            Subject = "Invoice 4471 charged twice",
            Body = "Hello,\n\nOur card was charged twice for invoice 4471 this morning. Can you refund the duplicate?\n\nJonas",
            ReceivedAt = DateTimeOffset.Now.AddMinutes(-12),
        };
        Draft.Suggestions =
        [
            new ReplyDraftSuggestion("refund", "Refund it"),
            new ReplyDraftSuggestion("details", "Ask for details"),
        ];
        Draft.Model = ReplyDraftDemoData.Model;
        Draft.Generator = ReplyDraftStreams.FromText(Write);
        Draft.DraftCompleted += (_, e) => Log.Text = $"Draft ready: {e.Mode}, {e.Tone}, {e.Length}, {e.Suggestion?.Label}";
    }

    // Your service would call a model here; the demo picks a canned text.
    private static async Task<string> Write(ReplyDraftRequest request)
    {
        await Task.Delay(500, request.CancellationToken);
        var refund = request.Suggestion?.Id != "details";
        var opening = request.Tone == ReplyDraftTone.Formal ? "Dear Jonas," : "Hi Jonas,";
        var body = refund
            ? "Sorry about that. I've refunded the duplicate charge for invoice 4471; it should reach your card in 3 to 5 business days."
            : "Sorry about that. Could you send the last four digits of the card and the time of both charges so I can find them?";
        if (request.Length == ReplyDraftLength.Long)
        {
            body += " I've also added a note to your account so this does not happen again at the next renewal.";
        }
        if (request.Tone == ReplyDraftTone.Concise)
        {
            body = refund
                ? "Refunded the duplicate for invoice 4471. Allow 3 to 5 business days."
                : "Please send the card's last four digits and both charge times.";
        }
        return opening + "\n\n" + body + "\n\nBest,\nAlex";
    }
}
