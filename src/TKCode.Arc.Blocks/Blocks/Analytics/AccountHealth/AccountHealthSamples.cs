namespace TKCode.Arc;

/// <summary>The invented account <see cref="AccountHealth"/> shows while it has no health history of its own.</summary>
internal static class AccountHealthSamples
{
    public const string Name = "Juniper Mills";

    public const string Subtitle = "Growth plan · $48k a year · Owner: Ravi Shah";

    public static double[] History() => [82, 80, 81, 78, 76, 77, 73, 70, 68, 66, 67, 64];

    public static AccountHealthUsageMetric[] Usage() =>
    [
        new("Active seats", [118, 120, 121, 119, 117, 114, 112, 108, 105, 103, 101, 98], "seats") { Limit = 150 },
        new("Weekly logins", [1840, 1902, 1876, 1790, 1720, 1650, 1610, 1540, 1480, 1460, 1410, 1385]),
        new("Reports run", [412, 398, 430, 401, 388, 352, 340, 318, 296, 301, 287, 270]),
    ];

    public static AccountHealthTicket[] Tickets() =>
    [
        new("#4821", "Exports time out for large workspaces", AccountHealthTicketPriority.Urgent, AccountHealthTicketStatus.Open, "3d"),
        new("#4807", "SSO users land on the wrong workspace", AccountHealthTicketPriority.High, AccountHealthTicketStatus.Pending, "6d"),
        new("#4790", "Question about invoice line items", AccountHealthTicketPriority.Normal, AccountHealthTicketStatus.Open, "9d"),
        new("#4752", "Dashboard colors after the update", AccountHealthTicketPriority.Low, AccountHealthTicketStatus.Solved, "2w"),
    ];

    public static AccountHealthRisk[] Risks() =>
    [
        new("Champion left the company", "Our main contact moved on in March; no new owner yet.", AccountHealthRiskLevel.High),
        new("Seats falling for 8 weeks", "98 of 150 seats used, down from 121.", AccountHealthRiskLevel.Medium),
        new("Urgent ticket open for 3 days", "Exports block their month-end reporting.", AccountHealthRiskLevel.Medium),
    ];

    public static AccountHealthAction[] Actions() =>
    [
        new("Meet the new operations lead", "Ask their account team for an intro and agree on goals.", 8),
        new("Fix the export timeouts", "Escalate #4821 to engineering with their workspace size.", 6),
        new("Run a seat review", "Offer to reassign unused seats before the renewal talk.", 4),
        new("Share the quarterly usage report", "Show the reports their finance team uses most.", 2, isDone: true),
    ];

    public static DateTime Renewal(DateTime today) => today.Date.AddDays(46);
}
