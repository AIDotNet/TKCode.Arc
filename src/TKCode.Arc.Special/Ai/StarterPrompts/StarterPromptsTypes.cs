using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A connected app a <see cref="StarterPrompt"/> uses, such as a mail, code or calendar integration. Its mark shows in a
/// tinted square on the card and its name reads beside it and in the card's accessible name.
/// <para>
/// The presets are neutral Lucide glyphs with plain names; pass your own <see cref="Mark"/> (a 24 × 24 stroked geometry)
/// and <see cref="Name"/> for a real integration.
/// </para>
/// </summary>
public sealed class StarterPromptApp
{
    /// <summary>Creates an app with no name and no mark; set both.</summary>
    public StarterPromptApp()
    {
    }

    /// <summary>Creates an app with a name and a mark.</summary>
    public StarterPromptApp(string name, Geometry? mark)
    {
        Name = name;
        Mark = mark;
    }

    /// <summary>The app's name as people know it ("Mail"); shown on the card and read by screen readers.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The app's mark: a 24 × 24 stroked glyph drawn in a tinted square. Null shows the first letter of the name.</summary>
    public Geometry? Mark { get; set; }

    /// <summary>A mail integration (Lucide <c>mail</c>).</summary>
    public static StarterPromptApp Mail { get; } = new("Mail", ArcIcons.Mail);

    /// <summary>A code repository integration (Lucide <c>git-branch</c>).</summary>
    public static StarterPromptApp Repository { get; } = new("Repository", ArcIcons.GitBranch);

    /// <summary>A documents and wiki integration (Lucide <c>file-text</c>).</summary>
    public static StarterPromptApp Docs { get; } = new("Docs", ArcIcons.FileText);

    /// <summary>An issue tracker integration (Lucide <c>circle-dot</c>).</summary>
    public static StarterPromptApp Issues { get; } = new("Issues", ArcIcons.CircleDot);

    /// <summary>A calendar integration (Lucide <c>calendar</c>).</summary>
    public static StarterPromptApp Calendar { get; } = new("Calendar", ArcIcons.Calendar);
}

/// <summary>
/// One starter card of <see cref="StarterPrompts"/>: a short verb-first <see cref="Title"/>, an optional quiet
/// <see cref="Description"/>, and the full <see cref="Prompt"/> that is placed in the composer when the card is picked.
/// </summary>
public sealed class StarterPrompt
{
    /// <summary>A stable id, passed back with the pick and the send.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The <see cref="StarterPromptCategory.Id"/> of the tab the card belongs to.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>The card's title: short and verb first ("Draft the launch email").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>One quiet line under the title, or null.</summary>
    public string? Description { get; set; }

    /// <summary>The full, specific instruction placed in the composer. The composer shows this, not the title.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>The connected app the prompt uses, or null. Set it only when the prompt really uses that integration.</summary>
    public StarterPromptApp? App { get; set; }
}

/// <summary>A tab of <see cref="StarterPrompts"/>: an id the starters refer to, a label and an optional 24 × 24 stroked icon.</summary>
public sealed class StarterPromptCategory
{
    /// <summary>Creates an empty category; set <see cref="Id"/> and <see cref="Label"/>.</summary>
    public StarterPromptCategory()
    {
    }

    /// <summary>Creates a category.</summary>
    public StarterPromptCategory(string id, string label, Geometry? icon = null)
    {
        Id = id;
        Label = label;
        Icon = icon;
    }

    /// <summary>The id <see cref="StarterPrompt.Category"/> refers to. <c>"all"</c> is reserved for the All tab.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The tab's label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The tab's icon (a 24 × 24 stroked glyph), also drawn on cards that have no app; null shows none.</summary>
    public Geometry? Icon { get; set; }
}

/// <summary>Where the composer of a <see cref="StarterPrompts"/> is with its message.</summary>
public enum StarterPromptsSendStatus
{
    /// <summary>Nothing is being sent.</summary>
    Idle,

    /// <summary>The message is on its way; the send button is Stop and Escape stops it.</summary>
    Sending,

    /// <summary>The message was sent: a check and "Sent", and the composer is empty again.</summary>
    Sent,

    /// <summary>The person stopped the send; the text is kept.</summary>
    Stopped,

    /// <summary>The send failed: an alert icon, the message and Retry; the text is kept.</summary>
    Failed
}

/// <summary>Arguments of <see cref="StarterPrompts.PromptSelected"/>: the starter that was picked.</summary>
public sealed class StarterPromptSelectedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public StarterPromptSelectedEventArgs(RoutedEvent routedEvent, StarterPrompt prompt) : base(routedEvent)
    {
        Prompt = prompt;
    }

    /// <summary>The starter whose prompt now fills the composer.</summary>
    public StarterPrompt Prompt { get; }
}

/// <summary>
/// Arguments of <see cref="StarterPrompts.SubmitRequested"/>: the message to send. Start your request and put its task in
/// <see cref="Sending"/>; the composer shows Sending until it completes, the error state with Retry if it throws, and Stop
/// cancels <see cref="CancellationToken"/>. Leave <see cref="Sending"/> null and the send is simulated.
/// </summary>
public sealed class StarterPromptsSubmitEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public StarterPromptsSubmitEventArgs(RoutedEvent routedEvent, string message, StarterPrompt? prompt,
                                         CancellationToken cancellationToken) : base(routedEvent)
    {
        Message = message;
        Prompt = prompt;
        CancellationToken = cancellationToken;
    }

    /// <summary>The composer's text, trimmed.</summary>
    public string Message { get; }

    /// <summary>The starter the message came from, or null when the person wrote it.</summary>
    public StarterPrompt? Prompt { get; }

    /// <summary>Cancelled when the person presses Stop or Escape, or the control leaves the window.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>The send in flight. Throw from it to show the error state (its message is shown); null simulates the send.</summary>
    public Task? Sending { get; set; }
}
