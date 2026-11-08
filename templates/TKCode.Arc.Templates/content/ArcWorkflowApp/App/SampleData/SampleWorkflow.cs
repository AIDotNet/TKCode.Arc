using ArcStarterApp.Models;

namespace ArcStarterApp.SampleData;

/// <summary>The workflow a new app starts with: a refund request that needs approval above a limit.</summary>
public static class SampleWorkflow
{
    public static WorkflowDocument Create() => new()
    {
        Name = "Refund requests",
        Steps =
        [
            new() { Id = "request", X = 300, Y = 0, Kind = "trigger", Title = "Refund requested", Description = "Support form" },
            new() { Id = "order", X = 300, Y = 120, Kind = "lookup", Title = "Find the order", Description = "Payments" },
            new() { Id = "check", X = 300, Y = 240, Kind = "condition", Title = "Amount over $500?", Description = "Condition" },
            new() { Id = "approve", X = 100, Y = 380, Kind = "approval", Title = "Manager approval", Description = "Finance team", IsLocked = true },
            new() { Id = "refund", X = 300, Y = 520, Kind = "action", Title = "Issue the refund", Description = "Payments" },
            new() { Id = "notify", X = 300, Y = 640, Kind = "email", Title = "Email the customer", Description = "Refund template" },
        ],
        Connections =
        [
            new() { From = "request", To = "order" },
            new() { From = "order", To = "check" },
            new() { From = "check", Output = 0, To = "approve" },
            new() { From = "check", Output = 1, To = "refund" },
            new() { From = "approve", To = "refund" },
            new() { From = "refund", To = "notify" },
        ],
    };
}
