using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where the suggestion of a <see cref="GhostText"/> is.</summary>
public enum GhostTextStatus
{
    /// <summary>No suggestion and no request: the chip shows the model's name.</summary>
    Idle,

    /// <summary>A request is waiting for its first words: the chip shows a shimmering "Thinking".</summary>
    Loading,

    /// <summary>A suggestion sits at the caret (it may still be streaming): Tab accepts it.</summary>
    Ready,

    /// <summary>The suggest function threw: the chip shows "No suggestion" with Retry.</summary>
    Error
}

/// <summary>How a <see cref="GhostText"/> is drawn.</summary>
public enum GhostTextVariant
{
    /// <summary>A bordered field with its own background, hover border and keyboard focus ring.</summary>
    Field,

    /// <summary>No border, background or ring: the text sits flush inside a card you already have.</summary>
    Plain
}

/// <summary>How much of a suggestion was accepted.</summary>
public enum GhostTextAcceptMode
{
    /// <summary>The whole suggestion (Tab, a tap on the chip, or <see cref="GhostText.AcceptAll"/>).</summary>
    All,

    /// <summary>The next word (Cmd, Ctrl, Option or Alt + Right, or <see cref="GhostText.AcceptWord"/>).</summary>
    Word
}

/// <summary>
/// Asks for a continuation of <paramref name="text"/> (the text before the caret, which is always the end of the field).
/// Return a <see cref="GhostTextResponse"/> (a string, a task of one, or a stream of chunks), or null to skip this text
/// without showing Thinking. Cancel your work when <paramref name="cancellationToken"/> fires: the person kept typing, moved
/// the caret or left the field. A thrown exception, or a faulted task or stream, shows "No suggestion" with Retry.
/// </summary>
/// <param name="text">The whole text of the field.</param>
/// <param name="cancellationToken">Fires when the answer is no longer wanted; late chunks are ignored either way.</param>
public delegate GhostTextResponse? GhostTextSuggest(string text, CancellationToken cancellationToken);

/// <summary>
/// The answer of a <see cref="GhostTextSuggest"/> function: a whole suggestion now, a task that completes with one, or a
/// stream of chunks that appear one after another (each fades in on its own). A string or a <c>Task&lt;string?&gt;</c>
/// converts implicitly; wrap an async iterator with <see cref="FromStream"/>. A null or empty result shows nothing.
/// </summary>
public sealed class GhostTextResponse
{
    private GhostTextResponse(string? text, Task<string?>? task, IAsyncEnumerable<string>? stream)
    {
        Text = text;
        Task = task;
        Stream = stream;
    }

    internal string? Text { get; }

    internal Task<string?>? Task { get; }

    internal IAsyncEnumerable<string>? Stream { get; }

    /// <summary>A whole suggestion, shown at once.</summary>
    public static GhostTextResponse FromText(string text) => new(text, null, null);

    /// <summary>A whole suggestion that arrives later; Thinking shows until the task completes.</summary>
    public static GhostTextResponse FromTask(Task<string?> task) => new(null, task ?? throw new ArgumentNullException(nameof(task)), null);

    /// <summary>A suggestion streamed chunk by chunk (an async iterator over your model's stream).</summary>
    public static GhostTextResponse FromStream(IAsyncEnumerable<string> chunks) =>
        new(null, null, chunks ?? throw new ArgumentNullException(nameof(chunks)));

    /// <summary>A whole suggestion; null stays null (no suggestion, no Thinking).</summary>
    public static implicit operator GhostTextResponse?(string? text) => text is null ? null : FromText(text);

    /// <summary>A suggestion that arrives later; null stays null.</summary>
    public static implicit operator GhostTextResponse?(Task<string?>? task) => task is null ? null : FromTask(task);
}

/// <summary>Arguments of <see cref="GhostText.Accepted"/>.</summary>
public sealed class GhostTextAcceptedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public GhostTextAcceptedEventArgs(RoutedEvent routedEvent, string acceptedText, GhostTextAcceptMode mode)
        : base(routedEvent)
    {
        AcceptedText = acceptedText;
        Mode = mode;
    }

    /// <summary>The text that was accepted: the whole suggestion or the next word.</summary>
    public string AcceptedText { get; }

    /// <summary>Whether the whole suggestion or one word was accepted.</summary>
    public GhostTextAcceptMode Mode { get; }
}

/// <summary>Arguments of <see cref="GhostText.Dismissed"/>.</summary>
public sealed class GhostTextDismissedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public GhostTextDismissedEventArgs(RoutedEvent routedEvent, string suggestion)
        : base(routedEvent)
    {
        Suggestion = suggestion;
    }

    /// <summary>The suggestion the person dismissed (what was still showing).</summary>
    public string Suggestion { get; }
}

/// <summary>Arguments of <see cref="GhostText.StatusChanged"/>.</summary>
public sealed class GhostTextStatusChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public GhostTextStatusChangedEventArgs(RoutedEvent routedEvent, GhostTextStatus oldStatus, GhostTextStatus newStatus)
        : base(routedEvent)
    {
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }

    /// <summary>The status before the change.</summary>
    public GhostTextStatus OldStatus { get; }

    /// <summary>The status now.</summary>
    public GhostTextStatus NewStatus { get; }
}
