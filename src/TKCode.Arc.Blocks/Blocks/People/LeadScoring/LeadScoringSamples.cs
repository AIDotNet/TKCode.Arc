namespace TKCode.Arc;

/// <summary>The invented pipeline <see cref="LeadScoring"/> shows while it has no leads of its own.</summary>
internal static class LeadScoringSamples
{
    public const string Title = "Inbound leads";

    public static LeadScoringStage[] Stages() =>
    [
        new("new", "New"),
        new("working", "Working"),
        new("qualified", "Qualified"),
        new("nurture", "Nurture"),
    ];

    private static LeadScoringFactor Fit(string label, int points) => new(label, points, LeadScoringFactorKind.Fit);

    private static LeadScoringFactor Eng(string label, int points) => new(label, points, LeadScoringFactorKind.Engagement);

    private static LeadScoringFactor Int(string label, int points) => new(label, points, LeadScoringFactorKind.Intent);

    private static LeadScoringLead Lead(string key, string name, string company, string job, string stage, string activity,
        params LeadScoringFactor[] factors)
        => new(key, name, company, stage) { JobTitle = job, LastActivity = activity, Factors = factors.ToList() };

    public static LeadScoringLead[] Leads() =>
    [
        Lead("l1", "Ines Moreau", "Larkfield Health", "VP Operations", "working", "12m ago",
            Fit("200–1,000 employees", 18), Fit("Decision maker", 15), Eng("Opened 6 emails", 10), Int("Requested a demo", 30), Int("Pricing page, 3 visits", 15)),
        Lead("l2", "Daniel Osei", "Copperline Studio", "Founder", "new", "1h ago",
            Fit("Under 50 employees", 4), Fit("Decision maker", 15), Eng("Webinar attended", 12), Int("Started a trial", 25), Eng("No reply in 2 weeks", -6)),
        Lead("l3", "Mei Tanaka", "Northgate Robotics", "Head of IT", "qualified", "Yesterday",
            Fit("1,000+ employees", 22), Fit("Technical buyer", 10), Eng("Replied to sales", 14), Int("Security docs downloaded", 20), Int("Pricing page, 5 visits", 20)),
        Lead("l4", "Lucas Ferreira", "Bluewren Foods", "Ops analyst", "new", "3h ago",
            Fit("200–1,000 employees", 18), Fit("Not a decision maker", -5), Eng("Opened 2 emails", 4), Int("Read 3 guides", 8)),
        Lead("l5", "Sara Lindqvist", "Oakridge Legal", "Office manager", "nurture", "2w ago",
            Fit("50–200 employees", 10), Eng("Unsubscribed from news", -10), Eng("Opened 1 email", 2), Int("Read 1 guide", 4)),
        Lead("l6", "Omar Haddad", "Tidewater Logistics", "COO", "working", "35m ago",
            Fit("1,000+ employees", 22), Fit("Decision maker", 15), Eng("Booked a call", 16), Int("Pricing page, 4 visits", 18), Int("Compared plans", 12)),
        Lead("l7", "Grace Whitfield", "Fernhill Schools", "IT coordinator", "new", "Today",
            Fit("Public sector", 6), Fit("Technical buyer", 10), Eng("Webinar attended", 12), Int("Asked about discounts", 10)),
        Lead("l8", "Arjun Mehta", "Sparrow Payments", "Engineering lead", "qualified", "2d ago",
            Fit("200–1,000 employees", 18), Fit("Technical buyer", 10), Eng("API docs, 9 visits", 18), Int("Started a trial", 25), Int("Invited 4 teammates", 12)),
        Lead("l9", "Hannah Becker", "Cloverleaf Retail", "Marketing lead", "nurture", "1w ago",
            Fit("50–200 employees", 10), Eng("Opened 3 emails", 6), Int("Read 2 guides", 6), Fit("Competitor customer", -8)),
        Lead("l10", "Tomás Rivera", "Brightpath Clinics", "Practice manager", "working", "5h ago",
            Fit("50–200 employees", 10), Fit("Decision maker", 15), Eng("Replied to sales", 14), Int("Requested a quote", 22)),
        Lead("l11", "Yuki Sato", "Harbor & Pine", "Designer", "new", "4d ago",
            Fit("Under 50 employees", 4), Eng("Opened 1 email", 2), Int("Read 1 guide", 4)),
        Lead("l12", "Nadia Petrov", "Granite Analytics", "Data director", "working", "20m ago",
            Fit("200–1,000 employees", 18), Fit("Decision maker", 15), Eng("Webinar attended", 12), Int("Pricing page, 2 visits", 10), Int("Requested a demo", 30)),
    ];
}
