using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class LeadScoringOwnLeadsDemo : UserControl
{
    public LeadScoringOwnLeadsDemo()
    {
        InitializeComponent();
        Board.Stages.AddRange([new LeadScoringStage("met", "Met at booth"), new LeadScoringStage("call", "Call booked"), new LeadScoringStage("won", "Won")]);
        Board.Leads.AddRange(
        [
            new LeadScoringLead("a", "Paula Ito", "Fernway Labs", "met")
            {
                JobTitle = "CTO",
                LastActivity = "Yesterday",
                Factors = [new("Decision maker", 25, LeadScoringFactorKind.Fit), new("Asked for pricing", 30, LeadScoringFactorKind.Intent), new("Scanned badge twice", 10, LeadScoringFactorKind.Engagement)],
            },
            new LeadScoringLead("b", "Marcus Hale", "Quarry Supply", "call")
            {
                JobTitle = "Buyer",
                LastActivity = "2d ago",
                Factors = [new("Mid-size company", 15, LeadScoringFactorKind.Fit), new("Watched the demo", 20, LeadScoringFactorKind.Engagement), new("Budget next year", -10, LeadScoringFactorKind.Intent)],
            },
            new LeadScoringLead("c", "Ada Brennan", "Glow Dental", "met")
            {
                JobTitle = "Owner",
                LastActivity = "Today",
                Factors = [new("Small practice", 8, LeadScoringFactorKind.Fit), new("Took a brochure", 5, LeadScoringFactorKind.Engagement)],
            },
        ]);
        Board.LeadsMoved += (_, e) => Status.Text = $"{string.Join(", ", e.Leads.Select(l => l.Name))} moved to {e.Stage.Title}";
    }
}
