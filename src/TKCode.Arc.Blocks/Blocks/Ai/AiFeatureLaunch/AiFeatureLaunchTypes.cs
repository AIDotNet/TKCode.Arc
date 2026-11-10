using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>How an <see cref="AiFeatureLaunch"/> was closed.</summary>
public enum AiFeatureLaunchDismissReason
{
    /// <summary>The close button, Escape, or <see cref="AiFeatureLaunch.Dismiss"/> from code.</summary>
    Closed,

    /// <summary>The "Maybe later" button.</summary>
    Later,

    /// <summary>The try button, with <see cref="AiFeatureLaunch.DismissOnTry"/> on.</summary>
    Tried
}

/// <summary>Arguments of <see cref="AiFeatureLaunch.Dismissed"/>.</summary>
public sealed class AiFeatureLaunchDismissedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiFeatureLaunchDismissedEventArgs(RoutedEvent routedEvent, AiFeatureLaunchDismissReason reason) : base(routedEvent)
        => Reason = reason;

    /// <summary>How the announcement was closed; store it to decide whether to show it again.</summary>
    public AiFeatureLaunchDismissReason Reason { get; }
}

/// <summary>
/// Every word an <see cref="AiFeatureLaunch"/> shows or reads out, with English defaults. Assign a new instance to
/// <see cref="AiFeatureLaunch.Labels"/> to change them.
/// </summary>
public sealed record AiFeatureLaunchLabels
{
    /// <summary>The badge before the title ("New").</summary>
    public string Badge { get; init; } = "New";

    /// <summary>The main button ("Try it now").</summary>
    public string Try { get; init; } = "Try it now";

    /// <summary>The quiet button that closes it for now ("Maybe later").</summary>
    public string Later { get; init; } = "Maybe later";

    /// <summary>Accessible name of the close button ("Dismiss").</summary>
    public string Close { get; init; } = "Dismiss";

    /// <summary>Accessible name of the demo's button while it plays ("Pause the demo").</summary>
    public string Pause { get; init; } = "Pause the demo";

    /// <summary>Accessible name of the demo's button while it is paused ("Play the demo").</summary>
    public string Play { get; init; } = "Play the demo";

    /// <summary>Accessible name of the list of what the feature does ("What it does").</summary>
    public string Highlights { get; init; } = "What it does";
}

/// <summary>Invented sample data for <see cref="AiFeatureLaunch"/>: its defaults.</summary>
public static class AiFeatureLaunchSamples
{
    /// <summary>The sample feature's name.</summary>
    public const string Title = "Thread summaries";

    /// <summary>The sample description.</summary>
    public const string Description =
        "Catch up on any long thread in seconds. Summaries pull out what was decided, who owns what and what is still open.";

    /// <summary>What the built-in demo shows, for screen readers.</summary>
    public const string DemoDescription =
        "Animation: a long thread is read from top to bottom and turned into a three-point summary.";

    /// <summary>Three things the sample feature does.</summary>
    public static IReadOnlyList<string> Highlights() =>
    [
        "Decisions, owners and open questions in three lines",
        "Every point links back to the message it came from",
        "Works in threads, documents and meeting notes",
    ];
}
