using Avalonia.Media;
using TKCode.Arc;

namespace ArcStarterApp.Models;

/// <summary>A kind of step the palette offers: how a new one looks and which outputs it has.</summary>
public sealed record StepKind(string Key, string Title, string Description, Geometry Icon, IReadOnlyList<string> Outputs, bool IsTrigger = false);

/// <summary>
/// The kinds of step. A step's Kind is saved with it and chooses its icon and outputs when the workflow is loaded. Add a
/// kind here to offer it in the palette (and give it a card of its own with a WorkflowNodeTemplate, as the canvas allows).
/// </summary>
public static class StepKinds
{
    public static IReadOnlyList<StepKind> All { get; } =
    [
        new("trigger", "When a form is sent", "Trigger", ArcIcons.Inbox, [], IsTrigger: true),
        new("lookup", "Find the record", "Data", ArcIcons.Search, []),
        new("condition", "Check a condition", "Condition", ArcIcons.GitBranch, ["Yes", "No"]),
        new("approval", "Ask for approval", "People", ArcIcons.ShieldCheck, []),
        new("action", "Update the record", "Action", ArcIcons.Database, []),
        new("email", "Send an email", "Message", ArcIcons.Mail, []),
        new("wait", "Wait", "Delay", ArcIcons.Clock, []),
    ];

    public static StepKind Find(string? key) => All.FirstOrDefault(k => k.Key == key) ?? All[^1];
}
