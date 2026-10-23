namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The ReleaseReadiness demos' data (all invented): the Version 5.3 rollout of a mobile app with four checks, the first
/// one done, and two owners; and a service handover with six checks, two of them optional.
/// </summary>
public static class ReleaseReadinessDemoData
{
    public const string Title = "Version 5.3";

    public const string Subtitle = "Lumen mobile · goes live Tuesday";

    public const string Channel = "#launches";

    /// <summary>The four rollout checks; the design sign-off is done already (and comes back done after a reset).</summary>
    public static ReleaseCheck[] RolloutChecks() =>
    [
        new("Design signed off", "Final screens approved by product", isDone: true) { Key = "design", IsInitiallyDone = true },
        new("Smoke tests green", "Build 5.3.0-beta.2 passed on preview") { Key = "smoke" },
        new("Release notes written", "Customer summary drafted and reviewed") { Key = "notes" },
        new("Support briefed", "Help desk knows what is changing") { Key = "support" },
    ];

    /// <summary>The rollout's two owners, drawn as generated avatars (initials on a tint, never photos).</summary>
    public static ProjectPerson[] RolloutOwners() =>
    [
        new("Rina Kato", "rina") { Hue = 205 },
        new("Samir Aziz", "samir") { Hue = 28 },
    ];

    /// <summary>The handover's six checks: four required, two optional.</summary>
    public static ReleaseCheck[] HandoffChecks() =>
    [
        new("Runbook updated", "Steps for restarts and failovers are current", isDone: true) { Key = "runbook" },
        new("Dashboards shared", "Latency and error panels are in the team folder") { Key = "dashboards" },
        new("Alerts rerouted", "Pages go to the platform rotation from Friday") { Key = "alerts" },
        new("Open incidents closed", "Nothing older than a week is still open") { Key = "incidents" },
        new("Access granted", "New owners can deploy and read the logs") { Key = "access", IsRequired = false },
        new("Intro call booked", "Thirty minutes with the outgoing team") { Key = "intro", IsRequired = false },
    ];

    /// <summary>The handover's owners.</summary>
    public static ProjectPerson[] HandoffOwners() =>
    [
        new("Noor Haddad", "noor") { Hue = 150 },
        new("Tomás Ferreira", "tomas") { Hue = 265 },
        new("Ingrid Solberg", "ingrid") { Hue = 340 },
    ];
}
