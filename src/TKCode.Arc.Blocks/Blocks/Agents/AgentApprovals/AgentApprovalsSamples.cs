namespace TKCode.Arc;

/// <summary>
/// Invented sample data for <see cref="AgentApprovals"/>: four requests from made-up agents of a small company. With no
/// <see cref="AgentApprovals.Requests"/> the queue shows these and decides them locally.
/// </summary>
public static class AgentApprovalsSamples
{
    /// <summary>The sample requests, timed relative to <paramref name="now"/>.</summary>
    public static IReadOnlyList<AgentApprovalRequest> Requests(DateTimeOffset now) =>
    [
        new AgentApprovalRequest
        {
            Id = "ap-1", Agent = "Ledger", Kind = AgentApprovalKind.Payment, Risk = AgentApprovalRisk.High,
            Title = "Pay the March invoice from Lindenleaf Print Co.",
            Context = "The invoice matches purchase order 2231 and the delivery note signed on Mar 4. It is due in two days and a late fee of 2% applies after that.",
            Details =
            [
                new AgentApprovalDetail("Amount", "$4,280.00"), new AgentApprovalDetail("To", "Lindenleaf Print Co. · account ending 0417"),
                new AgentApprovalDetail("From", "Operating account"), new AgentApprovalDetail("Reference", "INV-2026-0311")
            ],
            RiskNote = "A payment cannot be pulled back once sent.",
            Requested = now.AddMinutes(-6)
        },
        new AgentApprovalRequest
        {
            Id = "ap-2", Agent = "Patchwork", Kind = AgentApprovalKind.Deploy, Risk = AgentApprovalRisk.Medium,
            Title = "Deploy build 4.18.2 to production",
            Context = "All 412 tests pass and the staging check ran clean for an hour. The build fixes the upload limit bug customers reported this morning.",
            Details =
            [
                new AgentApprovalDetail("Service", "uploads-api"), new AgentApprovalDetail("Change", "3 commits · 61 lines"),
                new AgentApprovalDetail("Rollout", "10%, then everyone after 15 minutes")
            ],
            RiskNote = "Visible to every customer; can be rolled back in about a minute.",
            Requested = now.AddMinutes(-14)
        },
        new AgentApprovalRequest
        {
            Id = "ap-3", Agent = "Quill", Kind = AgentApprovalKind.Email, Risk = AgentApprovalRisk.Medium,
            Title = "Send the renewal quote to Fernhill Studio",
            Context = "Their plan renews on Apr 1. The quote keeps last year's price and adds the two seats they asked for in the support chat.",
            Details =
            [
                new AgentApprovalDetail("To", "ops@fernhill.example"), new AgentApprovalDetail("Subject", "Your renewal for April"),
                new AgentApprovalDetail("Attachment", "renewal-quote.pdf")
            ],
            RiskNote = "An email cannot be recalled once sent.",
            Requested = now.AddMinutes(-31)
        },
        new AgentApprovalRequest
        {
            Id = "ap-4", Agent = "Scout", Kind = AgentApprovalKind.DataChange, Risk = AgentApprovalRisk.Low,
            Title = "Archive 38 duplicate leads in the CRM",
            Context = "Each one has the same email address as an older lead. Notes and activity are merged into the older record first.",
            Details = [new AgentApprovalDetail("Records", "38 leads"), new AgentApprovalDetail("Undo", "Restorable for 30 days")],
            RiskNote = "Archived leads can be restored.",
            Requested = now.AddHours(-2)
        }
    ];
}
