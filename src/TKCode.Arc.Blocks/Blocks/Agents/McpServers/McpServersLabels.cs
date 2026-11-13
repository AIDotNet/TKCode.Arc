namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="McpServers"/> shows or reads out, with English defaults. Replace the whole object to translate or
/// reword; sentences with values are functions so the word order can change.
/// </summary>
public sealed class McpServersLabels
{
    /// <summary>The heading ("Tool servers").</summary>
    public string Title { get; init; } = "Tool servers";

    /// <summary>The line under the heading.</summary>
    public string Subtitle { get; init; } = "Servers give the assistant its tools. Turn off a server or a single tool to keep it from being used.";

    /// <summary>The summary at the end of the header; the arguments are the connected servers, all servers and the enabled tools of connected servers.</summary>
    public Func<int, int, int, string> Summary { get; init; } =
        static (connected, servers, tools) => $"{connected} of {servers} connected · {tools} {(tools == 1 ? "tool" : "tools")} on";

    /// <summary>The button that opens the form ("Add server").</summary>
    public string AddServer { get; init; } = "Add server";

    /// <summary>The form's heading.</summary>
    public string FormTitle { get; init; } = "Add a tool server";

    /// <summary>The label of the name field.</summary>
    public string NameLabel { get; init; } = "Name";

    /// <summary>The placeholder of the name field.</summary>
    public string NamePlaceholder { get; init; } = "Field notes";

    /// <summary>The accessible name of the transport switch.</summary>
    public string TransportLabel { get; init; } = "Connect with";

    /// <summary>The HTTP choice of the transport switch.</summary>
    public string HttpOption { get; init; } = "URL";

    /// <summary>The command choice of the transport switch.</summary>
    public string CommandOption { get; init; } = "Command";

    /// <summary>The label of the address field.</summary>
    public string UrlLabel { get; init; } = "Server URL";

    /// <summary>The placeholder of the address field.</summary>
    public string UrlPlaceholder { get; init; } = "https://tools.example.dev/mcp";

    /// <summary>The label of the command field.</summary>
    public string CommandLabel { get; init; } = "Command";

    /// <summary>The placeholder of the command field.</summary>
    public string CommandPlaceholder { get; init; } = "npx";

    /// <summary>The label of the arguments field.</summary>
    public string ArgumentsLabel { get; init; } = "Arguments";

    /// <summary>The placeholder of the arguments field.</summary>
    public string ArgumentsPlaceholder { get; init; } = "-y @example/notes-server --read-only";

    /// <summary>The label of the variables field for an HTTP server.</summary>
    public string HeadersLabel { get; init; } = "Headers (optional)";

    /// <summary>The label of the variables field for a command.</summary>
    public string EnvironmentLabel { get; init; } = "Environment (optional)";

    /// <summary>The help under the variables field.</summary>
    public string VariablesHint { get; init; } = "One KEY=value per line. Values are never shown again.";

    /// <summary>The placeholder of the variables field.</summary>
    public string VariablesPlaceholder { get; init; } = "API_TOKEN=…";

    /// <summary>The form's cancel button.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The form's submit button.</summary>
    public string Submit { get; init; } = "Add and connect";

    /// <summary>The error of an empty name.</summary>
    public string NameRequired { get; init; } = "Give the server a name.";

    /// <summary>The error of a name already in the list; the argument is the name.</summary>
    public Func<string, string> NameTaken { get; init; } = static name => $"A server named “{name}” is already in the list.";

    /// <summary>The error of an empty address.</summary>
    public string UrlRequired { get; init; } = "Enter the server's address.";

    /// <summary>The error of an address that is not a full http or https address.</summary>
    public string UrlInvalid { get; init; } = "Use a full address that starts with http:// or https://.";

    /// <summary>The error of an empty command.</summary>
    public string CommandRequired { get; init; } = "Enter the program to start.";

    /// <summary>The error of a variables line that is not KEY=value; the argument is the line number.</summary>
    public Func<int, string> VariableInvalid { get; init; } = static line => $"Line {line} is not KEY=value.";

    /// <summary>The word of a connected server.</summary>
    public string Connected { get; init; } = "Connected";

    /// <summary>The word of a server being connected.</summary>
    public string Connecting { get; init; } = "Connecting…";

    /// <summary>The word of a server whose connection failed.</summary>
    public string Failed { get; init; } = "Couldn't connect";

    /// <summary>The word of a server turned off.</summary>
    public string Off { get; init; } = "Off";

    /// <summary>The tool count after "Connected"; the arguments are the enabled tools and all tools.</summary>
    public Func<int, int, string> ToolCount { get; init; } =
        static (enabled, total) => enabled == total ? $"{total} {(total == 1 ? "tool" : "tools")}" : $"{enabled} of {total} tools";

    /// <summary>The tag of an HTTP server.</summary>
    public string HttpTag { get; init; } = "HTTP";

    /// <summary>The tag of a command server.</summary>
    public string CommandTag { get; init; } = "Local";

    /// <summary>The accessible name of a server's switch; the argument is the server name.</summary>
    public Func<string, string> ServerSwitchName { get; init; } = static name => $"Use {name}";

    /// <summary>The heading of a server's tools.</summary>
    public string ToolsHeading { get; init; } = "Tools";

    /// <summary>The count next to the tools heading; the arguments are the enabled tools and all tools.</summary>
    public Func<int, int, string> ToolsOn { get; init; } = static (enabled, total) => $"{enabled} of {total} on";

    /// <summary>The button that turns every tool on.</summary>
    public string EnableAll { get; init; } = "Enable all";

    /// <summary>The button that turns every tool off.</summary>
    public string DisableAll { get; init; } = "Disable all";

    /// <summary>The line of a connected server that lists no tools.</summary>
    public string NoTools { get; init; } = "This server lists no tools.";

    /// <summary>The line while the tools are being fetched.</summary>
    public string FetchingTools { get; init; } = "Fetching the tool list…";

    /// <summary>The line above the tools of a server that is off.</summary>
    public string DisabledNote { get; init; } = "This server is off. Turn it on to offer its tools.";

    /// <summary>The tag of a tool that writes, sends or deletes something.</summary>
    public string ChangesDataTag { get; init; } = "Changes data";

    /// <summary>The heading of the error box.</summary>
    public string ErrorTitle { get; init; } = "Couldn't connect";

    /// <summary>The button that shows the error log.</summary>
    public string ShowLog { get; init; } = "Show log";

    /// <summary>The button that hides the error log.</summary>
    public string HideLog { get; init; } = "Hide log";

    /// <summary>The button that connects again after an error.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The label of the address in the details.</summary>
    public string AddressLabel { get; init; } = "Address";

    /// <summary>The label of the command line in the details.</summary>
    public string CommandLineLabel { get; init; } = "Command";

    /// <summary>The label of the headers in the details.</summary>
    public string HeadersDetail { get; init; } = "Headers";

    /// <summary>The label of the environment variables in the details.</summary>
    public string EnvironmentDetail { get; init; } = "Environment";

    /// <summary>The label of the version in the details.</summary>
    public string VersionLabel { get; init; } = "Version";

    /// <summary>The button that connects a connected server again.</summary>
    public string Reconnect { get; init; } = "Reconnect";

    /// <summary>The button that asks to remove a server.</summary>
    public string Remove { get; init; } = "Remove";

    /// <summary>The question before removing; the arguments are the server name and its tool count.</summary>
    public Func<string, int, string> RemoveQuestion { get; init; } = static (name, tools) => tools == 0
        ? $"Remove {name}?"
        : $"Remove {name}? Its {tools} {(tools == 1 ? "tool stops" : "tools stop")} being offered.";

    /// <summary>The button that confirms the removal.</summary>
    public string RemoveConfirm { get; init; } = "Remove server";

    /// <summary>The button that keeps the server.</summary>
    public string Keep { get; init; } = "Keep";

    /// <summary>The heading of the empty list.</summary>
    public string EmptyTitle { get; init; } = "No tool servers yet";

    /// <summary>The sentence of the empty list.</summary>
    public string EmptyBody { get; init; } = "Add a server by its URL or by a command to give the assistant new tools.";

    /// <summary>Announced when a connection starts; the argument is the server name.</summary>
    public Func<string, string> ConnectingAnnouncement { get; init; } = static name => $"Connecting to {name}";

    /// <summary>Announced when a server connected; the arguments are the server name and its tool count.</summary>
    public Func<string, int, string> ConnectedAnnouncement { get; init; } =
        static (name, tools) => $"{name} connected, {tools} {(tools == 1 ? "tool" : "tools")}";

    /// <summary>Announced when a connection failed; the arguments are the server name and the error.</summary>
    public Func<string, string, string> FailedAnnouncement { get; init; } = static (name, error) => $"{name} couldn't connect. {error}";

    /// <summary>Announced when a server was added; the argument is the server name.</summary>
    public Func<string, string> AddedAnnouncement { get; init; } = static name => $"{name} added";

    /// <summary>Announced when a server was removed; the argument is the server name.</summary>
    public Func<string, string> RemovedAnnouncement { get; init; } = static name => $"{name} removed";

    /// <summary>Announced when a server was turned off; the argument is the server name.</summary>
    public Func<string, string> ServerOffAnnouncement { get; init; } = static name => $"{name} turned off";

    /// <summary>Announced when a tool was turned on or off; the arguments are the tool name and whether it is on.</summary>
    public Func<string, bool, string> ToolAnnouncement { get; init; } = static (tool, on) => on ? $"{tool} on" : $"{tool} off";

    /// <summary>Announced after Enable all or Disable all; the arguments are the server name, the tool count and whether they are on.</summary>
    public Func<string, int, bool, string> AllToolsAnnouncement { get; init; } =
        static (name, count, on) => on ? $"All {count} tools of {name} on" : $"All {count} tools of {name} off";
}
