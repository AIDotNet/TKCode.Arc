using Avalonia;
using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>How <see cref="McpServers"/> reaches a tool server.</summary>
public enum McpTransport
{
    /// <summary>A remote server at an HTTP or HTTPS address (<see cref="McpServer.Url"/>).</summary>
    Http,

    /// <summary>A local program started from a command line (<see cref="McpServer.Command"/> and <see cref="McpServer.Arguments"/>).</summary>
    Command
}

/// <summary>Where a tool server stands.</summary>
public enum McpServerStatus
{
    /// <summary>Turned off: its tools are not offered to the assistant.</summary>
    Disabled,

    /// <summary>A connection is being made and the tool list fetched.</summary>
    Connecting,

    /// <summary>Connected; its enabled tools are offered to the assistant.</summary>
    Connected,

    /// <summary>The last connection failed; <see cref="McpServer.ErrorMessage"/> says why.</summary>
    Error
}

/// <summary>
/// One tool a server offers: a name the assistant calls, a sentence on what it does, and whether it is offered. Data only:
/// an <see cref="AvaloniaObject"/> so a change made in code (or by the block) reaches the screen and your bindings.
/// </summary>
public class McpTool : AvaloniaObject
{
    /// <summary>The name the assistant calls the tool by, such as <c>search_notes</c>.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<McpTool, string>(nameof(Name), string.Empty);

    /// <summary>One sentence on what the tool does.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<McpTool, string?>(nameof(Description));

    /// <summary>Whether the tool is offered to the assistant (two-way; default true).</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<McpTool, bool>(nameof(IsEnabled), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the tool writes, sends or deletes something; its row shows a "Changes data" tag.</summary>
    public static readonly StyledProperty<bool> ChangesDataProperty =
        AvaloniaProperty.Register<McpTool, bool>(nameof(ChangesData));

    /// <summary>Creates an empty tool.</summary>
    public McpTool()
    {
    }

    /// <summary>Creates a tool with a name, a description and whether it changes data.</summary>
    /// <param name="name">The name the assistant calls.</param>
    /// <param name="description">One sentence on what it does.</param>
    /// <param name="changesData">Whether it writes, sends or deletes something.</param>
    public McpTool(string name, string? description, bool changesData = false)
    {
        Name = name;
        Description = description;
        ChangesData = changesData;
    }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public bool IsEnabled { get => GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

    /// <inheritdoc cref="ChangesDataProperty"/>
    public bool ChangesData { get => GetValue(ChangesDataProperty); set => SetValue(ChangesDataProperty, value); }
}

/// <summary>A header (HTTP) or an environment variable (command) sent to a server. The value is never shown, only the key.</summary>
/// <param name="Key">The header or variable name.</param>
/// <param name="Value">Its value.</param>
public sealed record McpServerVariable(string Key, string Value);

/// <summary>
/// A tool server <see cref="McpServers"/> lists: its name, how it is reached, whether it is on, where it stands and the
/// tools it offers. Data only: an <see cref="AvaloniaObject"/> so changes from your code or from the block reach both the
/// screen and your bindings.
/// </summary>
public class McpServer : AvaloniaObject
{
    /// <summary>A stable id reported in events; the block makes one from the name for servers it adds.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<McpServer, string>(nameof(Id), string.Empty);

    /// <summary>The display name, such as "Field notes".</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<McpServer, string>(nameof(Name), string.Empty);

    /// <summary>How the server is reached: <see cref="McpTransport.Http"/> (default) or <see cref="McpTransport.Command"/>.</summary>
    public static readonly StyledProperty<McpTransport> TransportProperty =
        AvaloniaProperty.Register<McpServer, McpTransport>(nameof(Transport));

    /// <summary>The address of an HTTP server, such as <c>https://notes.example.dev/mcp</c>.</summary>
    public static readonly StyledProperty<string?> UrlProperty =
        AvaloniaProperty.Register<McpServer, string?>(nameof(Url));

    /// <summary>The program of a command server, such as <c>npx</c> or <c>/usr/local/bin/ledger-tools</c>.</summary>
    public static readonly StyledProperty<string?> CommandProperty =
        AvaloniaProperty.Register<McpServer, string?>(nameof(Command));

    /// <summary>The arguments after <see cref="Command"/>, as one line.</summary>
    public static readonly StyledProperty<string?> ArgumentsProperty =
        AvaloniaProperty.Register<McpServer, string?>(nameof(Arguments));

    /// <summary>Headers (HTTP) or environment variables (command) sent to the server; only their keys are shown.</summary>
    public static readonly StyledProperty<IReadOnlyList<McpServerVariable>?> VariablesProperty =
        AvaloniaProperty.Register<McpServer, IReadOnlyList<McpServerVariable>?>(nameof(Variables));

    /// <summary>Whether the server is turned on (two-way; the row's switch edits it). Default true.</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<McpServer, bool>(nameof(IsEnabled), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Where the server stands (two-way; the block sets it while connecting).</summary>
    public static readonly StyledProperty<McpServerStatus> StatusProperty =
        AvaloniaProperty.Register<McpServer, McpServerStatus>(nameof(Status), McpServerStatus.Connected, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>One sentence on why the last connection failed; shown while <see cref="Status"/> is <see cref="McpServerStatus.Error"/>.</summary>
    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<McpServer, string?>(nameof(ErrorMessage));

    /// <summary>The server's last lines of output or the transport's trace, shown under "Show log" with the error.</summary>
    public static readonly StyledProperty<string?> ErrorLogProperty =
        AvaloniaProperty.Register<McpServer, string?>(nameof(ErrorLog));

    /// <summary>The version the server reported, such as "2.3.1"; null shows none.</summary>
    public static readonly StyledProperty<string?> VersionProperty =
        AvaloniaProperty.Register<McpServer, string?>(nameof(Version));

    /// <summary>The tools the server offers, in its order.</summary>
    [Content]
    public AvaloniaList<McpTool> Tools { get; } = new();

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="TransportProperty"/>
    public McpTransport Transport { get => GetValue(TransportProperty); set => SetValue(TransportProperty, value); }

    /// <inheritdoc cref="UrlProperty"/>
    public string? Url { get => GetValue(UrlProperty); set => SetValue(UrlProperty, value); }

    /// <inheritdoc cref="CommandProperty"/>
    public string? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    /// <inheritdoc cref="ArgumentsProperty"/>
    public string? Arguments { get => GetValue(ArgumentsProperty); set => SetValue(ArgumentsProperty, value); }

    /// <inheritdoc cref="VariablesProperty"/>
    public IReadOnlyList<McpServerVariable>? Variables { get => GetValue(VariablesProperty); set => SetValue(VariablesProperty, value); }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public bool IsEnabled { get => GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public McpServerStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="ErrorMessageProperty"/>
    public string? ErrorMessage { get => GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }

    /// <inheritdoc cref="ErrorLogProperty"/>
    public string? ErrorLog { get => GetValue(ErrorLogProperty); set => SetValue(ErrorLogProperty, value); }

    /// <inheritdoc cref="VersionProperty"/>
    public string? Version { get => GetValue(VersionProperty); set => SetValue(VersionProperty, value); }

    /// <summary>The address (HTTP) or the command line (command) as one line.</summary>
    public string Endpoint => Transport == McpTransport.Http
        ? Url ?? string.Empty
        : string.IsNullOrWhiteSpace(Arguments) ? Command ?? string.Empty : $"{Command} {Arguments}";

    /// <summary>How many of <see cref="Tools"/> are enabled.</summary>
    public int EnabledToolCount
    {
        get
        {
            var count = 0;
            foreach (var tool in Tools)
            {
                if (tool.IsEnabled)
                {
                    count++;
                }
            }
            return count;
        }
    }
}

/// <summary>What the Add server form produced: everything needed to create and connect a server.</summary>
/// <param name="Name">The display name (trimmed).</param>
/// <param name="Transport">HTTP or command.</param>
/// <param name="Url">The address of an HTTP server; null for a command.</param>
/// <param name="Command">The program of a command server; null for HTTP.</param>
/// <param name="Arguments">The arguments of a command server; null when none.</param>
/// <param name="Variables">Headers (HTTP) or environment variables (command), one per <c>KEY=value</c> line.</param>
public sealed record McpServerDraft(
    string Name,
    McpTransport Transport,
    string? Url,
    string? Command,
    string? Arguments,
    IReadOnlyList<McpServerVariable> Variables);

/// <summary>The outcome of connecting to a server: its tools and version, or why it failed.</summary>
public sealed class McpConnectResult
{
    private McpConnectResult(bool succeeded, IReadOnlyList<McpTool> tools, string? version, string? errorMessage, string? errorLog)
    {
        Succeeded = succeeded;
        Tools = tools;
        Version = version;
        ErrorMessage = errorMessage;
        ErrorLog = errorLog;
    }

    /// <summary>Whether the connection worked.</summary>
    public bool Succeeded { get; }

    /// <summary>The tools the server listed (empty after a failure).</summary>
    public IReadOnlyList<McpTool> Tools { get; }

    /// <summary>The version the server reported, or null.</summary>
    public string? Version { get; }

    /// <summary>One sentence on the failure, or null.</summary>
    public string? ErrorMessage { get; }

    /// <summary>The output or trace behind the failure, or null.</summary>
    public string? ErrorLog { get; }

    /// <summary>A connection that worked and listed <paramref name="tools"/>.</summary>
    /// <param name="tools">The tools the server offers.</param>
    /// <param name="version">The version it reported.</param>
    public static McpConnectResult Success(IEnumerable<McpTool> tools, string? version = null) =>
        new(true, tools.ToArray(), version, null, null);

    /// <summary>A connection that failed with <paramref name="message"/> and, optionally, a <paramref name="log"/>.</summary>
    /// <param name="message">One sentence on why.</param>
    /// <param name="log">The output or trace behind it.</param>
    public static McpConnectResult Failure(string message, string? log = null) =>
        new(false, Array.Empty<McpTool>(), null, message, log);
}
