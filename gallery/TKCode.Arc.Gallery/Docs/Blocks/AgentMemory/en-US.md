---
title: Agent memory
summary: A card that shows what an assistant remembers: filter by group, search, edit a sentence in place, forget with a few seconds to undo, file a just-noted memory and add one by hand.
---

<Demo name="Preview" />

## When to use

Agent memory is the page where people see, correct and delete what an assistant has kept about them. It is one card:
a capacity meter, two switches, filter pills, a search, the memories grouped by kind, and a field to add one.

- **A settings page for an assistant that learns**: "Reference saved memories" and "Reference chat history" are the two
  switches people look for, and the list under them is the proof of what is saved.
- **A trust surface after a chat**: the assistant noted something ("Noted: you run builds with the dotnet CLI"). The
  captured pill shows it and files it in one press.
- **A memory that must be reviewable**: every sentence can be edited in place or forgotten, and a forget keeps a few
  seconds of undo before it is real.
- **A bounded memory**: the meter shows how much of the capacity is used and warns before it is full.

## When not to use

- **A log of events or messages**: a memory is one short sentence without a final period. Use a `Timeline` or a table for history.
- **Long notes or documents**: each memory is limited to `MaxLength` characters (200 by default). Use an editor for long text.
- **Settings that are not memories**: put toggles and choices in a settings page with `Switch` and `Select`.
- **Deleting with no way back**: the undo bar is the design. If your data cannot be restored for a few seconds, say so
  in the explanation text and shorten `UndoDuration`.
- **Storing the memories**: the card changes a list and tells you. Saving, syncing and what the assistant does with the
  memories is your code.

## Usage

`AgentMemory` derives from Avalonia's `TemplatedControl`. A memory is a `MemoryItem`, an `AvaloniaObject` with an id, a
sentence, the key of its group, where it came from and a date. Groups are `MemoryGroup`s; with none, the four default
groups (About you, Preferences, Projects, People) are used.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AgentMemory AssistantName="Orbit" Capacity="2000">
        <arc:AgentMemory.Memories>
            <arc:MemoryItem Id="1" GroupKey="preferences" SourceKind="Chat"
                            Text="Prefers tables to long bullet lists" />
            <arc:MemoryItem Id="2" GroupKey="people" PersonName="Mina Okafor"
                            Text="Mina Okafor reviews every database migration" />
        </arc:AgentMemory.Memories>
    </arc:AgentMemory>
</UserControl>
```

### Anatomy

- **Header**: the title, a subtitle that names the assistant, and the capacity meter ("62% full" over a thin bar).
- **Switches**: "Reference saved memories" and "Reference chat history", each with a line that explains its state.
- **Toolbar**: the filter pills and, at the end, the search button.
- **Captured pill**: the assistant's latest note. Press it to file the memory into its group.
- **List**: the memories, grouped under headings with counts, scrolling inside the card above `ListMaxHeight`.
- **Add field**: type a sentence and press Enter; the card sorts it into a group.

### Filtering and searching

The pills filter by group; the All view shows every group that has memories under its heading, and a single group shows
its memories without a heading (or its own sentence when it is empty). The search button opens a box over the pills.
It matches anywhere in a sentence, ignores case and highlights every hit. Results are grouped with the number of hits
per group, and the search covers all groups whatever the filter is. Escape or the close button clears the search and
brings back the filter you had.

`SelectedGroupKey`, `SearchText` and `IsSearchOpen` are two-way, so you can set them from code.

<Demo name="Bindings" />

Give the card a list to follow with `MemoriesSource` (an `ObservableCollection<MemoryItem>` works): the card makes its
changes on that list, and what you change in it appears in the card.

### Editing and forgetting

The Edit button on a row (visible on hover and focus) opens a small editor: Enter saves, Shift+Enter adds a line, Escape
cancels. An empty or too long sentence is refused with a message; past `MaxLength` a counter ("212 of 200 characters") appears.
Forget turns the row into a bar, "Forgot “…”", with an **Undo** button that drains for `UndoDuration`. The count and the
meter drop at once; the change is committed and `MemoryChanged` raised only when the time runs out. Several bars can
count down together.

### Saving the change

Set `CommitAsync` to save a change before it takes effect. The editor shows a saving state while the task runs. If
it throws, its message appears in place and the change is rolled back; if it is cancelled, the change is rolled back
silently. A forget is committed once its undo window has ended.

<Demo name="Commit" />

```csharp
memory.CommitAsync = async (change, cancellation) =>
{
    await api.SaveAsync(change.Kind, change.Memory.Id, change.Memory.Text, cancellation);
};
```

`MemoryChange` has the `Kind` (`Capture`, `Add`, `Edit` or `Forget`), the `Memory` and, for an edit, the
`PreviousText`. `MemoryChanged` reports the change and the whole list after it.

### Captured memories

Put what the assistant has just noted in `Captured`: a `CapturedMemory` with a short `Note` and the `Memory` to file. The
first one not yet filed shows as a pill. Pressing it switches the filter to the memory's group if needed, adds the memory,
tints its row for a moment and changes the pill to "Saved to Preferences" before it folds away.
`FileCaptured()` does the same from code.

### Adding by hand

The field at the bottom adds a memory. The card sorts it into a group: with no `ClassifyAsync` it makes a keyword guess
(two capitalized names mean People, "prefer" or "want" mean Preferences, "project" or "moving" mean Projects, anything
else the first group). Set `ClassifyAsync` to ask your own code or a model. When the capacity would be exceeded, adding is
refused with "Memory is full" and the text stays. Set `IsAddEnabled` to false for a read-only card.

### Capacity

`Capacity` counts characters across all memories. The meter warns from `WarningRatio` (90% by default) with a color and
an icon, not color alone. A capacity of zero hides the meter and the limit.

<Demo name="Capacity" />

### Paused

Turning "Reference saved memories" off dims the list and replaces the captured pill with a sentence saying memories are
paused. The list stays editable; adding and forgetting still work. `IsSavedMemoriesEnabled` and `IsChatHistoryEnabled`
are two-way, and `SettingToggled` reports a change.

<Demo name="Paused" />

### Wording

All words come from `Labels` (`AgentMemoryLabels`): replace the instance to translate or reword. Formatted texts are
functions so the word order can change.

```csharp
memory.Labels = new AgentMemoryLabels
{
    Title = "Souvenirs",
    SavedTo = group => $"Rangé dans {group}",
    NoMatches = query => $"Aucun souvenir ne correspond à « {query} »"
};
```

`Title` and `Subtitle` on the control win over the labels when set.

### Content guidelines

- **A memory is one sentence without a final period**, in the third person or as a plain fact: "Prefers tables to long bullet lists".
- **Be specific and neutral**: "Is training for a spring half marathon", not "Loves running!".
- **People**: put the name in the sentence and in `PersonName`; the row shows a small avatar with the first letter (or `PersonImage`).
- **Source**: `Chat` shows "From a chat", `Manual` "Added by you", with a short date. Give `SourceUri` to make the source a link and
  handle `SourceRequested` or `SourceCommand` to open the conversation.

### Best practices

- Save with `CommitAsync` and show real errors; never swallow a failure, the card rolls the change back for you.
- Keep `UndoDuration` at five seconds or more; shorter windows are hard to use with a screen reader.
- Offer the same list to the assistant that the card shows: what people cannot see, they cannot correct.
- Keep four to six groups; more than that scrolls the pills.

### Common questions

**Why did my forget not call `CommitAsync` at once?** It is committed when the undo window ends; until then it can be undone.

**Can I use my own groups?** Yes. Add `MemoryGroup`s with unique keys and set each memory's `GroupKey`. A memory whose key matches no
group is shown in the first group.

**Is the card also a good fit for a narrow sidebar?** Below 440 px wide it tightens its padding, wraps the subtitle and lets the filters scroll.

## API reference

<!-- api: AgentMemory -->

<!-- api: MemoryItem -->

<!-- api: MemoryGroup -->

<!-- api: CapturedMemory -->

### MemoryChange

| Property | Description |
|---|---|
| `Kind` | `Capture`, `Add`, `Edit` or `Forget` |
| `Memory` | The memory that changed (for a forget, as it was) |
| `PreviousText` | The text before an edit; null for other kinds |

### Events

| Event | Raised when | Arguments |
|---|---|---|
| `MemoryChanged` | A change was committed | `Change`, `Memories` (the whole list after it) |
| `SettingToggled` | One of the two switches changed | `Setting` (`SavedMemories` or `ChatHistory`), `IsOn` |
| `SourceRequested` | A source with a link was pressed | `Memory` |

## Accessibility

- The meter is announced as "Memory used, 62% full" and reads the real value, not the animating one.
- The switches have a name and an explanation that updates with their state. The filter pills are a group with the name
  "Filter memories", report which one is pressed, and are one tab stop; Left and Right move and select.
- Rows are list items inside groups named by their heading. The Edit and Forget buttons are named with the sentence ("Edit
  memory: …") and have tooltips; they appear on hover and keyboard focus, and take no extra space.
- A polite live region announces search results, saved edits, a forget with its undo time, the end of the window, a
  restore, filing and adding, a full memory, and each switch.
- Focus goes where the person would look next: into the editor and back to Edit after saving or canceling; to Undo after Forget
  and back to Forget after Undo; to the search box and back to the search button; to the new row's Edit button after filing a
  captured memory with the keyboard.
- Status never relies on color alone: the warning meter adds an icon, errors add an icon and a sentence, saved states a check.
- Reduced motion keeps every state, announcement and the undo time.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Switches, the selected filter pill, the search button, the captured pill, each row's Edit and Forget, the add field |
| `ArrowLeft` / `ArrowRight` on the pills | Moves to the previous or next pill and selects it; wraps around |
| `Home` / `End` on the pills | First or last pill |
| `Enter` or `Space` | Presses the focused button, switch or pill |
| `Enter` in the editor | Saves; `Shift+Enter` adds a line |
| `Escape` in the editor | Cancels |
| `Escape` in the search | Clears it, closes it and returns to the search button |
| `Enter` in the add field | Sorts and adds the memory |

## Motion

- Filtering fades leaving groups and rows out in 100 ms, fades entering ones in over 160 ms after a short delay, and
  their heights follow on a critically damped spring. The selected pill's background glides between pills.
- The search box grows over the pills in 240 ms while the pills fade out; its field and close button fade in.
- A row, its editor and its undo bar crossfade (100 ms out, 160 ms in) while the row's height springs between them.
- The undo ring drains in a straight line over the undo window; the seconds count down once a second.
- A filed or added row opens its slot on a smooth spring, then its row tints for 1.6 seconds and fades.
- The meter's fill and number follow one spring; the warning color changes at once.
- Explanations, the captured pill's text and the add field's status crossfade, rising 2 px as they come in.
- Reduced motion drops springs, movement and the drain: heights jump, crossfades become short fades, the ring stays full and
  the seconds still count. Off applies every change at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Filter or search | Fade out 100 ms, in 160 ms; heights spring | Short fades; heights jump | Immediate |
| Row to editor or undo bar | Crossfade; height springs | 120 ms crossfade | Immediate |
| Selected pill | Glides on a spring | Jumps | Immediate |
| Search opens | Width over 240 ms | Immediate | Immediate |
| Undo ring | Drains over the window | Stays full | Stays full |
| Filed or added row | Slot springs open; 1.6 s tint | No slot motion; tint stays | Immediate |

## Related

- `ModelPicker` for choosing the model that uses the memories.
- `Switch` for a setting that is not about memories.
- `Timeline` for a history of events.

| Component | Choose it when |
|---|---|
| `Switch` | One setting turns on or off |
| `Timeline` | The list is a history in time order |
| `Textarea` | People write a longer text |
| `Toast` | A short message confirms an action and goes away |
