---
title: MCP servers
summary: Manage the tool servers an assistant can call: each server with a live status, its tools behind switches, the error and log when it fails, and a form to add one by URL or by command.
---

<Demo name="Preview" />

## When to use

MCP servers is the settings card where people decide which tool servers an assistant may call. A tool server (one that
speaks the Model Context Protocol) is reached at an HTTP address or started as a local program, and it lists tools such
as `search_notes` or `post_entry`. The card shows each server as a row: a mark, the name, an HTTP or Local tag, a live
status line ("Connected · 5 tools", "Connecting…", "Couldn't connect", "Off") and a switch. Opening a row shows the
tools, each with its own switch and a "Changes data" tag when it writes something, plus Enable all and Disable all. A
failed server shows its message, a log to open and Retry. The row also lists the address or command, the header or
variable keys and the version, and offers Reconnect and Remove (with a confirmation). Add server opens a form for a URL,
or for a command and its arguments, with optional headers or environment variables; the new server joins the list and
connects at once.

- **An assistant or agent with plug-in tools**: people see what is connected, what failed and what each tool can do.
- **Least privilege**: turn off a tool that writes, or a whole server, without removing it.
- **Fixing a broken connection**: the error, the log and Retry are in one place.

## When not to use

- **A directory of apps to connect with one click**: use `Integrations`.
- **Choosing which model answers**: use `ModelPicker`.
- **Editing secrets**: the card takes headers and variables when a server is added and never shows their values again;
  manage keys with `ApiKeys` or `EnvVariables`.
- **Running the protocol**: the card shows and changes a list of `McpServer` objects. Connecting, listing tools and
  storing the list is your code (`ConnectAsync`, the events).

## Usage

`McpServers` derives from Avalonia's `TemplatedControl`. With no properties it shows four invented sample servers and
simulates connections, so it works as it is in a prototype. Give it your own list in `Servers`; an
`ObservableCollection<McpServer>` lets the card follow your changes and you follow its own.

```xml
<arc:McpServers Servers="{Binding ToolServers}" />
```

```csharp
var servers = new ObservableCollection<McpServer>
{
    new McpServer
    {
        Id = "field-notes", Name = "Field notes", Transport = McpTransport.Http,
        Url = "https://notes.example.dev/mcp", Status = McpServerStatus.Connected,
        Tools = { new McpTool("search_notes", "Finds notes that match a query.") }
    }
};
block.Servers = servers;
```

### Anatomy

- **Header**: the title, a sentence that explains the card, the summary ("3 of 4 connected · 8 tools on") and Add server.
- **Add server form**: name, URL or Command, the address or the command and its arguments, optional headers or
  environment, Cancel and Add and connect.
- **Server row**: the mark (initials), the name and its transport tag, the status dot and words, the address or command
  line, the chevron, and the switch.
- **Open server**: the error box (message, Retry, Show log), the tools with their switches, the details, Reconnect and
  Remove.

### Connecting

Set `ConnectAsync` to connect for real. It gets the server and a token; return `McpConnectResult.Success(tools, version)`
or `McpConnectResult.Failure(message, log)`. Throwing fails with the exception's message. The token is cancelled when the
server is turned off, removed, connected again, or the card leaves the window. Tools the server listed before keep
their switches when it lists them again.

<Demo name="Connect" />

```csharp
block.ConnectAsync = async (server, cancellation) =>
{
    var session = await client.ConnectAsync(server.Url!, cancellation);
    var tools = await session.ListToolsAsync(cancellation);
    return McpConnectResult.Success(tools.Select(t => new McpTool(t.Name, t.Description)), session.ServerVersion);
};
```

Without `ConnectAsync` the card waits `SimulatedConnectDuration` (1.2 s) and uses `McpServersSamples.SimulateConnect`:
an address whose host ends in `.invalid`, or a command whose name starts with `missing`, fails with a log; anything else
succeeds with three invented tools.

### Adding and removing

Add server opens the form (`IsAddFormOpen`, `OpenAddForm()`). It checks the name (needed, not already used), the address
(a full `http://` or `https://` address) or the command (needed), and the variables (one `KEY=value` per line). The
first problem gets the focus; errors update as you type. A valid form adds the server, raises `ServerAdded`, closes and
connects it. `AddServer(draft)` does the same from code.

Remove asks first: "Remove Field notes? Its 5 tools stop being offered." Remove server takes it out of the list and
raises `ServerRemoved`; Keep or Escape returns. Set `CanAddServers` or `CanRemoveServers` to false to hide the actions;
a read-only list hides both.

<Demo name="Empty" />

### Turning servers and tools off

The row's switch turns a server off (status Off, the connection is cancelled) or on (it connects). Each tool's switch
offers or withholds that tool; Enable all and Disable all change every tool at once. `ServerToggled` and `ToolToggled`
report the changes, and `ConnectedCount` and `EnabledToolCount` give the totals the summary shows.

### In a narrow column

Set `ExpandedServer` to open a server from code. In a side panel the card tightens its paddings at its own width.

<Demo name="Narrow" />

### Wording

All words come from `Labels` (`McpServersLabels`): replace the instance to translate or reword. Sentences with values are
functions so the word order can change. `Title` and `Subtitle` on the control win over the labels.

```csharp
block.Labels = new McpServersLabels
{
    Title = "Serveurs d'outils",
    AddServer = "Ajouter un serveur",
    RemovedAnnouncement = name => $"{name} retiré"
};
```

### Content guidelines

- **Name servers after what they hold** ("Field notes", "Ledgerline"), not after the protocol.
- **Describe tools in one sentence** that starts with a verb: "Finds notes whose title or text matches a query."
- **Mark every tool that writes, sends or deletes** with `ChangesData`; people turn those off first.
- **Errors say what happened and what to try**: "The server answered 401: the access key was refused."

## API reference

<!-- api: McpServers -->

<!-- api: McpServer -->

<!-- api: McpTool -->

### Events

| Event | Raised when | Arguments |
|---|---|---|
| `ServerAdded` | A server joined the list | `Server` |
| `ServerRemoved` | A server left the list | `Server` |
| `ServerToggled` | A server's switch changed | `Server`, `IsEnabled` |
| `ToolToggled` | Tools were turned on or off | `Server`, `Tools`, `IsEnabled` |
| `StatusChanged` | A server's status changed | `Server`, `OldStatus`, `NewStatus` |

## Accessibility

- Each server's header is a button named with the server, described with its status and address, and reported as
  expanded or collapsed. The switch beside it is named "Use Field notes".
- Tool switches are named with the tool (and "Changes data" when it writes) and described with the tool's sentence.
- Status never relies on color: the words say it, and the dot is filled when on and hollow when off.
- A polite live region announces connecting, connected with the tool count, failures with the message, adding,
  removing, turning a server off and every tool change.
- The form's fields have visible labels; errors are shown under the field and the first one gets the focus.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Add server, then each server's header and switch, then the controls of the open server |
| `Enter` or `Space` on a header | Opens or closes the server |
| `ArrowDown` / `ArrowUp` on a header | Next or previous server |
| `Home` / `End` on a header | First or last server |
| `Escape` in an open server | Closes it (or cancels the removal question) and returns to its header |
| `Enter` in a one-line form field | Adds the server |
| `Escape` in the form | Cancels it and returns to Add server |

## Motion

- An open server's body grows on a smooth spring; the chevron turns on a spring.
- The status words change with a short label swap (100 ms out, 160 ms in, rising 2 px).
- The connected dot's halo breathes out every 2.4 s; the connecting dot breathes every 0.9 s.
- The form's height springs open and closed.
- Row tints change in 160 ms.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Server opens | Height spring, chevron turns | Height and chevron jump | Immediate |
| Status words | Label swap | 120 ms crossfade | Immediate |
| Live dot | Breathes | Still | Still |
| Form | Height spring | Jumps | Immediate |

## Responsive behavior

- The card changes at its own width. At 520 px or less (`:compact`) the header, list, form and open-server paddings
  tighten; the card is at most 720 px wide and centered in a wider container.
- Names and address lines end with an ellipsis; tool descriptions, errors and logs wrap. The log scrolls above 160 px.
- In the form the command and its arguments share a row in a 2:3 split.
- Let the card stretch rather than giving it a fixed width.

## Related

- `Integrations` for a directory of apps to connect.
- `AgentMemory` for what an assistant remembers.
- `ModelPicker` for the model that calls the tools.

| Component | Choose it when |
|---|---|
| `Integrations` | People connect apps from a catalog |
| `ApiKeys` | People create and revoke keys |
| `Switch` | One setting turns on or off |
| `Accordion` | Sections open and close without status or actions |
