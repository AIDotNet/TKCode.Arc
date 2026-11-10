---
title: Agent builder
summary: Set up an AI agent beside a live test chat: name and description, instructions with a token count, the model, tools with switches, knowledge sources that index in place, a save bar with Discard, and Publish with a status badge.
---

<Demo name="Preview" />

## When to use

`AgentBuilder` is the page where someone creates or edits an AI agent and tries it before anyone else uses it. The left
card holds everything that defines the agent, in the order people decide it: who it is (name, description), what it is
told (instructions), what thinks (the model), what it may do (tools) and what it knows (knowledge sources). The right card
is a chat with that agent. Edits stay a draft until Save; Publish hands the saved version to the people who use the
agent, and a badge says whether what they use is current. The test chat remembers the settings it started with, so a
reply never mixes two versions: when the draft changes, a note offers to restart the chat with the new settings.

- **An agent or assistant editor** in an AI product, an internal tools platform or a support desk.
- **Prompt and tool tuning**: change one instruction or switch one tool, restart the chat, ask the same question again.
- **Knowledge-backed agents**: add a link or upload files and watch each one index before the agent can search it.

## When not to use

- **Only a prompt to edit and run against test cases**: use `PromptStudio`, which compares versions and outputs.
- **Only a model choice**: use `ModelPicker`.
- **A chat for end users**: use `AiChat`. The test chat here is for the person building the agent and keeps no history.
- **Workflows with branches and steps**: a single agent with tools is a different thing from a flow of agents; use a
  graph editor for that.

## Usage

The block derives from Avalonia's `TemplatedControl`. With no data it shows an invented support agent called Harbor
(three models, five tools, three sources, one of which failed to index) and simulates saving, publishing, indexing and
the test replies, so you can try it before wiring anything.

```xml
<arc:AgentBuilder AgentName="{Binding Name}"
                  AgentDescription="{Binding Description}"
                  Instructions="{Binding Instructions}"
                  SelectedModelId="{Binding ModelId}" />
```

Give it your catalog in code (`Models`, `Providers`, `Tools`, `Sources`, `Suggestions`); once any of them, the name or the
instructions are set before it shows, the sample stays out. Set `ShowsSampleData="False"` for a builder that starts empty.

<Demo name="NewAgent" />

### Anatomy

- **Header**: an avatar with the first letter of the name (its color follows the name), the name, a status badge (Draft,
  Published, Unpublished changes), the description and **Publish**.
- **Identity**: the name (required to save) and a one-line description.
- **Instructions**: a multi-line field with a character count and a rough token count (about four characters a token).
  Past `InstructionsMaxLength` (8,000) the field shows an error and the draft cannot be saved.
- **Model**: the model pill of `ModelPicker`; the test chat's model button changes the same setting.
- **Tools**: one row per `AgentBuilderTool` with a switch; a press anywhere on the row flips it. The count says how many are on.
- **Knowledge**: one row per `AgentBuilderSource` with its kind, size and status: a ring while indexing, a check with the
  passage count when ready, the reason and **Retry** when it failed, and **Remove**. Under the list, a link field with
  **Add link** and **Upload files**.
- **Save bar**: "All changes saved" or "Unsaved changes" with **Discard** and **Save**; a failed save shows its message here.
- **Test chat**: an `AiChat` whose replies come from `Responder`, with a note and **Restart** when the draft changed.

### Draft, saved and published

The settings on screen are the draft (`Config`). `SavedConfig` is what was saved; when it is null as the builder first
shows, the settings on screen count as saved. `IsDirty` compares the two, and **Discard** puts the saved settings back.
`PublishedConfig` is what people use: null means Draft, equal to the saved settings means Published, anything else is
Unpublished changes. **Publish** saves first when needed. All three snapshots are `AgentBuilderConfig` records; two are
equal when the name, description, instructions, model, tools that are on and source ids match.

```csharp
builder.SaveHandler = async (config, cancellation) => await api.SaveAgentAsync(agentId, config, cancellation);
builder.PublishHandler = async (config, cancellation) => await api.PublishAgentAsync(agentId, cancellation);
builder.Load(await api.GetAgentAsync(agentId));   // shows it as saved
```

A handler that throws keeps the draft and shows "Not saved: …" with the exception's message. `Ctrl+S` (`Cmd+S`) saves.
`SaveAsync`, `PublishAsync` and `Discard` do the same from code; `ConfigChanged`, `Saved` and `Published` report each step.

<Demo name="Wired" />

### Knowledge sources

**Add link** (or Enter in the field) accepts a full `http` or `https` address and refuses anything else, or a page that is
already a source, with a message under the field. **Upload files** calls `PickFilesHandler`, or the window's file picker
when it is unset (the button hides when neither exists). New and retried sources index through `IndexHandler`:

```csharp
builder.IndexHandler = async (source, progress, cancellation) =>
{
    var job = await api.StartIndexAsync(source.Url, cancellation);
    await foreach (var step in api.WatchAsync(job, cancellation))
    {
        progress.Report(step.Fraction);
    }
    return job.PassageCount;
};
```

Throwing marks the source failed with the message; Remove cancels the work. Without a handler indexing is simulated
(about 2.4 seconds). `AddSources` adds sources your own code received, `RemoveSource` and `RetrySource` act on one, and
`SourceIndexed` reports each that finished or failed.

### The test chat

The chat answers with the settings it had at its first message (`TestConfig`). After an edit, `IsTestStale` turns true
and the note says so; **Restart** (or the chat's New chat button, or `RestartTest()`) empties the chat and the next message
uses the draft. Connect your agent runtime by implementing `IAgentBuilderResponder`:

```csharp
public sealed class RuntimeResponder(AgentRuntime runtime) : IAgentBuilderResponder
{
    public async IAsyncEnumerable<AiChatStreamEvent> RespondAsync(AgentBuilderTestRequest request,
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        await foreach (var delta in runtime.StreamAsync(request.Config, request.Chat.Messages, cancellation))
        {
            yield return new AiChatTextDelta(delta);
        }
    }
}
```

The request carries the settings, the model, the tools that are on and the conversation. Without a responder,
`AgentBuilderSimulatedResponder` answers: it searches the ready sources, runs a tool that is on when the question asks for
it ("Where is order 4417?" runs Order lookup), and sums up the agent's settings, so every switch has a visible effect.

### Narrow containers

The layout follows the builder's own width: below 760 px the test chat moves under the settings, below 520 px the
section paddings tighten. A side panel or a phone gets the same builder.

<Demo name="Narrow" />

### Wording

All words come from `Labels` (`AgentBuilderLabels`), including the test chat's (`Chat`) and the model menu's (`Model`).
Texts with values are functions so the word order can change. `CultureName` formats the counts and sizes.

### Content guidelines

- **Name**: short and specific, as people will see it in a list ("Harbor", "Release notes writer").
- **Description**: one line on what the agent helps with, without a final period.
- **Instructions**: lists of rules read better than long paragraphs; say what the agent must not do.
- **Tool descriptions**: one line on what the agent can do with the tool, not how it works.

## API reference

<!-- api: AgentBuilder -->

<!-- api: AgentBuilderTool -->

<!-- api: AgentBuilderSource -->

### AgentBuilderConfig

| Property | Description |
|---|---|
| `Name`, `Description`, `Instructions` | The text settings |
| `ModelId` | The id of the chosen model, or null |
| `ToolIds` | The ids of the tools that are on, in the order of `Tools` |
| `Sources` | The knowledge sources, in order |

### Events

| Event | Raised when | Arguments |
|---|---|---|
| `ConfigChanged` | The draft changed | `Field`, `Config`, `IsDirty` |
| `Saved` | A save went through | `Config` |
| `Published` | A publish went through | `Config` |
| `SourceIndexed` | A source finished indexing or failed | `Item` |

## Accessibility

- The settings card is a form named "Agent settings" with level-3 headings per section; the agent's name is a level-2 heading.
- Every field has a label or an accessible name; the instructions field has its hint as help text. Errors (an empty
  name, instructions that are too long, a bad link) appear as field messages, in words, not color alone.
- Each tool's switch carries the tool's name and description; it is the row's only tab stop.
- A source row reads its name and status ("returns-and-repairs.pdf, File · 412 KB · Ready · 86 passages"); Remove and
  Retry are named with the source ("Remove help.tidewater.example/fitting").
- A polite live region announces saves, discards, publishing, tools switched, sources added, removed, ready or failed,
  a stale test chat and a restart. The save bar's status is a polite live region too.
- Focus goes where the person would look next: after removing a source, to the next row's Remove (or the link field);
  after a retry, to the row's Remove; after Discard, to the name field; after Restart, to the chat's message box.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Publish, the fields, the model pill, each tool's switch, each source's Retry and Remove, the link field, Add link, Upload, Discard, Save, then the test chat |
| `Space` | Flips the focused tool switch |
| `Enter` in the link field | Adds the link |
| `Ctrl+S` / `Cmd+S` | Saves the draft |
| `Enter`, `Space` and `ArrowDown` on the model pill | Opens the model menu |

## Motion

- The save bar's status crossfades (100 ms out, 160 ms in, rising 2 px) as it changes between saved, unsaved, saving and failed.
- The stale note fades in and its height grows on a spring above the chat; it collapses again after a restart.
- A source's ring follows the indexing progress, catching up a little each frame; the check replaces it when it is ready.
- Rows tint on hover in 160 ms; a tool's glyph square takes the accent tint when it is switched on; the avatar's color
  crossfades when the name changes its color.
- The test chat streams like `AiChat` and the buttons, switches and fields keep their own motion.
- Reduced motion keeps every state: the note fades without growing, the ring jumps to the progress, tints change at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Save bar status | Crossfade with a 2 px rise | 120 ms crossfade | Immediate |
| Stale note | Fades in, height springs | Fades | Immediate |
| Indexing ring | Chases the progress | Jumps | Jumps |
| Row and glyph tints | 160 ms | Immediate | Immediate |

## Responsive behavior

- `AgentBuilder` changes at its own width, not the window's. From 760 px (`StackWidth`) the chat sits beside the settings,
  about 46% of the width and at least 340 px wide, and at least 560 px tall (it grows with the settings card). Below 760
  px (`:stacked`) the chat moves under the settings at 540 px high.
- Below 520 px (`:compact`) the section paddings tighten from 24 to 16.
- Names and descriptions shorten with an ellipsis; tool descriptions, source statuses and the stale note wrap.
- Let the builder stretch (`HorizontalAlignment="Stretch"`) and put it in a scrolling page: the settings card is as tall
  as its content.

## Related

- `ModelPicker` for the model alone.
- `PromptStudio` for editing a prompt against saved test cases.
- `AiChat` for the chat your users see.
- `AgentMemory` for what an assistant remembers about a person.

| Component | Choose it when |
|---|---|
| `PromptStudio` | You compare prompt versions on test cases |
| `ModelPicker` | Only the model is chosen |
| `AiChat` | People talk to the finished agent |
| `SettingsPage` | The settings are not about an agent |
