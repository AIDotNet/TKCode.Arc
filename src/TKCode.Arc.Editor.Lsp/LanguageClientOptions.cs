namespace TKCode.Arc.Editor.Lsp;

/// <summary>How a <see cref="LanguageClient"/> introduces itself to a language server and talks to the host.</summary>
public sealed class LanguageClientOptions
{
    /// <summary>
    /// The name of this connection: the owner of the diagnostics it shows (one owner per client, so two servers on one
    /// document keep their markers apart). Defaults to "lsp".
    /// </summary>
    public string Name { get; init; } = "lsp";

    /// <summary>The client name sent to the server in <c>initialize</c>. Defaults to "TKCode.Arc.Editor".</summary>
    public string ClientName { get; init; } = "TKCode.Arc.Editor";

    /// <summary>The client version sent to the server in <c>initialize</c>, if any.</summary>
    public string? ClientVersion { get; init; }

    /// <summary>The workspace root (a folder URI, usually <c>file:///...</c>); null when the documents have no workspace.</summary>
    public Uri? RootUri { get; init; }

    /// <summary>The server's initialization options as JSON text (sent as <c>initializationOptions</c>); null for none.</summary>
    public string? InitializationOptions { get; init; }

    /// <summary>The locale sent to the server (an IETF tag such as "en-US"); null uses the current UI culture.</summary>
    public string? Locale { get; init; }

    /// <summary>How long <see cref="LanguageClient.StopAsync"/> waits for the server's <c>shutdown</c> answer. Defaults to 5 seconds.</summary>
    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Answers the server's <c>window/showMessageRequest</c>: gets the message and the titles of its actions and returns
    /// the title the user chose, or null when the user dismissed it. Without a handler the request is answered with null.
    /// </summary>
    public Func<LanguageClientMessageEventArgs, IReadOnlyList<string>, CancellationToken, Task<string?>>? ShowMessageRequest { get; init; }
}

/// <summary>The kind of a message from a language server (the protocol's MessageType).</summary>
public enum LanguageClientMessageType
{
    /// <summary>An error.</summary>
    Error = 1,

    /// <summary>A warning.</summary>
    Warning = 2,

    /// <summary>Information.</summary>
    Info = 3,

    /// <summary>A log line.</summary>
    Log = 4,

    /// <summary>A debug line.</summary>
    Debug = 5,
}

/// <summary>A message a language server sent (<c>window/showMessage</c>, <c>window/logMessage</c>) or one the client logs.</summary>
public sealed class LanguageClientMessageEventArgs : EventArgs
{
    /// <summary>Creates the arguments of a message.</summary>
    public LanguageClientMessageEventArgs(LanguageClientMessageType type, string message)
    {
        Type = type;
        Message = message;
    }

    /// <summary>The kind of the message.</summary>
    public LanguageClientMessageType Type { get; }

    /// <summary>The text of the message.</summary>
    public string Message { get; }
}
