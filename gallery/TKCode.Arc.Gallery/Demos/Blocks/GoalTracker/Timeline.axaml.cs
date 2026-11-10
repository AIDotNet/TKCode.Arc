using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class GoalTrackerTimelineDemo : UserControl
{
    public GoalTrackerTimelineDemo()
    {
        InitializeComponent();
        var start = new DateTime(DateTime.Today.Year, 1, 1);
        Goals.PeriodStart = start;
        Goals.PeriodEnd = start.AddMonths(6).AddDays(-1);
        Goals.Today = start.AddDays(100);
        var mia = new ProjectPerson("Mia Laurent");
        var reply = new GoalTrackerKeyResult("First reply under 2 hours", 35, 90, 62, "%") { Owner = mia, Confidence = GoalTrackerConfidence.OnTrack };
        reply.CheckIns.Add(new GoalTrackerCheckIn(start.AddDays(30), 44, GoalTrackerConfidence.OnTrack, "Night shift added"));
        reply.CheckIns.Add(new GoalTrackerCheckIn(start.AddDays(70), 62, GoalTrackerConfidence.OnTrack, "Macros for top questions"));
        var csat = new GoalTrackerKeyResult("Satisfaction score", 4.1, 4.6, 4.2) { Owner = mia, Confidence = GoalTrackerConfidence.AtRisk };
        csat.CheckIns.Add(new GoalTrackerCheckIn(start.AddDays(60), 4.2, GoalTrackerConfidence.AtRisk, "Billing questions drag it down"));
        Goals.Goals.Add(new GoalTrackerGoal("Answer every customer faster", "Support", mia) { KeyResults = [reply, csat] });
        Goals.CheckedIn += (_, e) => Status.Text = $"{e.KeyResult.Title}: {e.CheckIn.Value} ({e.CheckIn.Confidence})";
    }
}
