using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CallInsightsOwnCallDemo : UserControl
{
    public CallInsightsOwnCallDemo()
    {
        InitializeComponent();
        var t = (int m, int s) => new TimeSpan(0, m, s);
        Call.Speakers.Add(new CallInsightsSpeaker("jonas", "Jonas Weber", "Customer success", isHost: true));
        Call.Speakers.Add(new CallInsightsSpeaker("clara", "Clara Nunes", "Studio manager"));
        Call.Utterances.AddRange(
        [
            new CallInsightsUtterance("jonas", t(0, 0), t(0, 40), "How has the new print queue been for the studio?", 0.3),
            new CallInsightsUtterance("clara", t(0, 42), t(1, 50), "Much better. Jobs stopped getting lost, and the team trusts it now.", 0.7),
            new CallInsightsUtterance("jonas", t(1, 52), t(2, 30), "Great to hear. Anything that still slows you down?", 0.2),
            new CallInsightsUtterance("clara", t(2, 32), t(3, 40), "Approvals by email are slow, and the invoice format confuses our accountant.", -0.4),
            new CallInsightsUtterance("jonas", t(3, 42), t(4, 50), "Approvals can happen right in the queue. I can switch that on and send a short guide.", 0.4),
            new CallInsightsUtterance("clara", t(4, 52), t(6, 0), "Perfect. Then I'm happy to renew for another year.", 0.8),
        ]);
        Call.Moments.Add(new CallInsightsMoment(t(2, 32), CallInsightsMomentKind.Objection, "Approvals by email are slow"));
        Call.Moments.Add(new CallInsightsMoment(t(4, 52), CallInsightsMomentKind.Commitment, "Ready to renew for a year"));
        Call.Topics.Add(new CallInsightsTopic("Print queue", t(0, 0), t(1, 50)));
        Call.Topics.Add(new CallInsightsTopic("Approvals", t(2, 32), t(4, 50)));
        Call.Topics.Add(new CallInsightsTopic("Renewal", t(4, 52), t(6, 0)));
        Call.NextSteps.Add(new CallInsightsNextStep("Switch on approvals in the queue", "Jonas", "Today"));
        Call.NextSteps.Add(new CallInsightsNextStep("Send the renewal order form", "Jonas", "Fri"));
    }
}
