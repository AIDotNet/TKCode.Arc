using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Your own lanes, tickets and an async classifier; the routing events written under the card.</summary>
public partial class TicketRouterYourClassifierDemo : UserControl
{
    private static readonly string[] s_extra =
    [
        "Refund the duplicate charge please",
        "App crashes when I open settings",
        "Idea: let us pin favorite reports",
        "Where do I change my email address?",
    ];

    private readonly ObservableCollection<TicketMessage> _tickets =
    [
        new("q-1", TicketChannel.Chat, "Rosa Diaz", "Fieldstone", "Invoice shows the wrong company name", "Our new legal name is not on the invoice."),
        new("q-2", TicketChannel.Email, "Ken Ito", "Bluebird Labs", "Password reset email never arrives", "Tried three times, nothing in spam either."),
        new("q-3", TicketChannel.HelpDesk, "Ava Moss", "Glenbrook", "Hello, quick thing", "Not urgent, whenever someone has a minute."),
    ];

    private int _added;

    public TicketRouterYourClassifierDemo()
    {
        InitializeComponent();
        Router.Lanes =
        [
            new TicketLane("billing", "Billing", "Noor Hassan", "Eli Brandt"),
            new TicketLane("engineering", "Engineering", "Rina Kato"),
            new TicketLane("success", "Success", "Tomas Vidal", "June Park"),
            new TicketLane("review", "Triage") { IsReview = true },
        ];
        Router.Loop = false;
        Router.Classifier = ClassifyAsync;
        Router.Tickets = _tickets;
        Router.TicketRouted += (_, e) => Log.Text = e.Ticket.Assignee is { } who
            ? $"TicketRouted: {e.Ticket.Message.Subject} → {e.Ticket.LaneId} ({who})"
            : $"TicketRouted: {e.Ticket.Message.Subject} → {e.Ticket.LaneId}, suggested {e.Ticket.SuggestedLaneId}";
        Router.ReviewConfirmed += (_, e) => Log.Text = $"ReviewConfirmed: {e.Ticket.Message.Subject} → {e.LaneId} ({e.Ticket.Assignee})";
        Add.Click += (_, _) =>
        {
            var subject = s_extra[_added % s_extra.Length];
            _added++;
            _tickets.Add(new TicketMessage($"x-{_added}", TicketChannel.Messages, "Sam Rivera", "Northwind", subject, "Sent from the demo."));
        };
    }

    /// <summary>A stand-in for your model call: waits, then reads a few words. Cancelled when the router pauses.</summary>
    private static async Task<TicketClassification> ClassifyAsync(TicketMessage message, CancellationToken token)
    {
        await Task.Delay(600, token);
        var text = (message.Subject + " " + message.Preview).ToLowerInvariant();
        if (text.Contains("refund"))
        {
            return new TicketClassification(TicketIntent.Refund, TicketSentiment.Neutral, 0.93);
        }
        if (text.Contains("invoice") || text.Contains("charge"))
        {
            return new TicketClassification(TicketIntent.Billing, TicketSentiment.Neutral, 0.9);
        }
        if (text.Contains("crash") || text.Contains("never arrives") || text.Contains("error"))
        {
            return new TicketClassification(TicketIntent.Bug, TicketSentiment.Negative, 0.88);
        }
        if (text.Contains("idea"))
        {
            return new TicketClassification(TicketIntent.Feature, TicketSentiment.Positive, 0.85);
        }
        return new TicketClassification(TicketIntent.Question, TicketSentiment.Neutral, 0.42);
    }
}
