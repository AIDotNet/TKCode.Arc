using System.Collections.ObjectModel;

namespace TKCode.Arc;

/// <summary>
/// Invented sample data for <see cref="McpServers"/> (the block's default list) and the simulated connection it uses when no
/// <see cref="McpServers.ConnectAsync"/> is set. Every name, address and tool here is made up.
/// </summary>
public static class McpServersSamples
{
    /// <summary>How long the simulated connection takes: 1.2 seconds.</summary>
    public static readonly TimeSpan SimulatedConnectTime = TimeSpan.FromMilliseconds(1200);

    /// <summary>
    /// Four sample servers, a fresh copy on every call: Field notes (HTTP, connected, five tools), Ledgerline (command,
    /// connected, one of four tools off), Orbit calendar (HTTP, failed with a refused token) and Sketchpad (command, off).
    /// </summary>
    public static ObservableCollection<McpServer> Servers() =>
    [
        Server("field-notes", "Field notes", McpTransport.Http, "https://notes.example.dev/mcp", null, null,
            McpServerStatus.Connected, "2.3.1", [new McpServerVariable("Authorization", "Bearer sample-notes-token")]),
        Server("ledgerline", "Ledgerline", McpTransport.Command, null, "ledgerline-mcp", "--books ./books/2026 --read-write",
            McpServerStatus.Connected, "0.9.4", [new McpServerVariable("LEDGER_REGION", "eu")]),
        Server("orbit-calendar", "Orbit calendar", McpTransport.Http, "https://calendar.orbit.example/mcp", null, null,
            McpServerStatus.Error, null, [new McpServerVariable("X-Orbit-Key", "expired-sample-key")]),
        Server("sketchpad", "Sketchpad", McpTransport.Command, null, "npx", "-y @example/sketchpad-mcp",
            McpServerStatus.Disabled, "1.1.0", []),
    ];

    /// <summary>
    /// The tools a sample server lists, by its id; any other server gets three tools named after it. Fresh objects on
    /// every call.
    /// </summary>
    /// <param name="serverId">The server's <see cref="McpServer.Id"/>.</param>
    /// <param name="serverName">The server's name, used for servers that are not samples.</param>
    public static IReadOnlyList<McpTool> ToolsFor(string serverId, string serverName)
    {
        switch (serverId)
        {
            case "field-notes":
                return
                [
                    new McpTool("search_notes", "Finds notes whose title or text matches a query."),
                    new McpTool("read_note", "Returns the full text of one note."),
                    new McpTool("list_notebooks", "Lists the notebooks and how many notes each holds."),
                    new McpTool("create_note", "Writes a new note into a notebook.", changesData: true),
                    new McpTool("tag_note", "Adds or removes tags on a note.", changesData: true),
                ];
            case "ledgerline":
                return
                [
                    new McpTool("query_ledger", "Runs a read-only query over the ledger's entries."),
                    new McpTool("summarize_month", "Totals income and spending by category for one month."),
                    new McpTool("export_csv", "Exports entries in a date range as CSV text."),
                    new McpTool("post_entry", "Posts a new entry to the ledger.", changesData: true),
                ];
            case "orbit-calendar":
                return
                [
                    new McpTool("list_events", "Lists events between two dates."),
                    new McpTool("find_free_time", "Finds open slots shared by several people."),
                    new McpTool("create_event", "Creates an event and sends the invitations.", changesData: true),
                ];
            case "sketchpad":
                return
                [
                    new McpTool("render_diagram", "Turns a text outline into a diagram image."),
                    new McpTool("list_shapes", "Lists the shapes and connectors the canvas supports."),
                ];
        }
        var stem = Stem(serverName);
        return
        [
            new McpTool($"{stem}_search", $"Searches what {serverName} holds."),
            new McpTool($"{stem}_read", $"Reads one item from {serverName}."),
            new McpTool($"{stem}_write", $"Creates or updates an item in {serverName}.", changesData: true),
        ];
    }

    /// <summary>
    /// The simulated outcome of connecting to <paramref name="server"/>, without waiting: an address whose host ends in
    /// <c>.invalid</c>, or a command named <c>missing-…</c>, fails; anything else succeeds with <see cref="ToolsFor"/>.
    /// </summary>
    /// <param name="server">The server to connect.</param>
    public static McpConnectResult SimulateConnect(McpServer server)
    {
        if (server.Transport == McpTransport.Http)
        {
            var host = Uri.TryCreate(server.Url, UriKind.Absolute, out var uri) ? uri.Host : server.Url ?? string.Empty;
            if (host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase))
            {
                return McpConnectResult.Failure(
                    $"No server answered at {host}.",
                    $"→ POST {server.Url}\n  initialize (protocol 2025-06-18)\n✕ name resolution failed for {host}\n  gave up after 3 attempts (1.0 s apart)");
            }
        }
        else
        {
            var command = server.Command ?? string.Empty;
            if (Path.GetFileName(command).StartsWith("missing", StringComparison.OrdinalIgnoreCase))
            {
                return McpConnectResult.Failure(
                    $"The program “{command}” was not found.",
                    $"$ {server.Endpoint}\nspawn {command}: no such file or directory\nprocess exited with code 127 before it said hello");
            }
        }
        return McpConnectResult.Success(ToolsFor(server.Id, server.Name), server.Version ?? "1.0.0");
    }

    /// <summary>
    /// A connect hook that waits <paramref name="delay"/> and then returns <see cref="SimulateConnect"/> (a stand-in for
    /// a real client).
    /// </summary>
    /// <param name="delay">How long the connection takes.</param>
    public static Func<McpServer, CancellationToken, Task<McpConnectResult>> SimulatedConnect(TimeSpan delay) =>
        async (server, cancellation) =>
        {
            await Task.Delay(delay, cancellation);
            return SimulateConnect(server);
        };

    /// <summary>The error the sample Orbit calendar shows until it is retried.</summary>
    internal const string OrbitError = "The server answered 401: the access key was refused.";

    private static McpServer Server(string id, string name, McpTransport transport, string? url, string? command, string? arguments,
        McpServerStatus status, string? version, IReadOnlyList<McpServerVariable> variables)
    {
        var server = new McpServer
        {
            Id = id,
            Name = name,
            Transport = transport,
            Url = url,
            Command = command,
            Arguments = arguments,
            Status = status,
            IsEnabled = status != McpServerStatus.Disabled,
            Version = version,
            Variables = variables,
        };
        if (status == McpServerStatus.Error)
        {
            server.ErrorMessage = OrbitError;
            server.ErrorLog =
                "→ POST https://calendar.orbit.example/mcp\n  initialize (protocol 2025-06-18)\n← 401 Unauthorized\n  www-authenticate: Key realm=\"orbit\", error=\"expired_key\"\n✕ handshake stopped: the key expired on 2026-11-02";
        }
        else
        {
            server.Tools.AddRange(ToolsFor(id, name));
            if (id == "ledgerline")
            {
                server.Tools[^1].IsEnabled = false;
            }
        }
        return server;
    }

    private static string Stem(string name)
    {
        var chars = new List<char>(name.Length);
        foreach (var c in name.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                chars.Add(c);
            }
            else if (chars.Count > 0 && chars[^1] != '_')
            {
                chars.Add('_');
            }
        }
        var stem = new string(chars.ToArray()).Trim('_');
        return stem.Length == 0 ? "server" : stem;
    }
}
