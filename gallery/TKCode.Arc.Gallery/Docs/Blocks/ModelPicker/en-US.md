---
title: Model picker
summary: Choose a model and its reasoning effort from one compact pill: featured models with a note each, an effort setting, every other model by provider, and a summary of what the choice costs.
---

<Demo name="Preview" />

## When to use

The model picker lets people decide which model answers and how hard it thinks. It is one pill that shows the current
choice, a menu behind it, and an optional summary of what the model can hold and costs. It belongs wherever the choice
matters but should not take space until someone looks for it.

- **A settings page for an AI feature**: the default model for a support assistant or a drafting tool. The summary
  under the pill shows the context size and the price per million tokens, so the choice is informed.
- **A model catalog with a few favorites**: three or four featured models with a note each ("Quick and capable for daily
  chats"), and the long tail grouped by provider under "More models".
- **An effort control tied to the model**: some models reason longer when asked. The Effort row appears only for models
  that offer levels, and a level the next model lacks moves to the nearest one.
- **A header or toolbar chip**: set `IsSummaryVisible` to false and the picker is only the pill, for the top of a chat
  or beside a prompt field.
- **A product that bills by usage**: the summary prints input and output prices, and a small note under it says which
  currency and unit they are in.

## When not to use

- **A short fixed choice with no explanation**, such as Small, Medium and Large: use `SegmentedControl` or `Select`.
- **A comparison of several models side by side**: the picker shows one model at a time. Build a table with
  `SortableDataTable` when people need to compare prices and limits.
- **A choice among hundreds of models that people search by name**: the menu has no search field. Use `Combobox` and
  filter your own list.
- **A model setting that is not about the model**, such as temperature or stop words: put those in a form with
  `NumberField` and `Switch`. The picker chooses the model and its effort only.
- **Running the model**: the picker does not call anything. It reports the choice; your code decides what to do with it.

## Usage

`ModelPicker` derives from Avalonia's `TemplatedControl`. Its template holds a `ModelPill`, a `ModelSummary` and a
hidden `ModelMenu`. The picker owns the state: you give it models and read the choice back. A model is an `AiModel`, an
`AvaloniaObject` with the name, a one-line `Blurb` for the featured list, a `Description` for the summary, the context
size and prices, and the effort levels it offers. Providers group the "More models" list and supply the mark of any
model that has none.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ModelPicker SelectedModelId="aurora-prime-3" SummaryNote="Prices in USD per 1M tokens">
        <arc:ModelPicker.Providers>
            <arc:AiModelProvider Id="aurora" Name="Aurora Labs">
                <arc:AiModelProvider.Mark>
                    <StreamGeometry>M2 19a10 10 0 0 1 20 0h-3.2a6.8 6.8 0 0 0-13.6 0Z</StreamGeometry>
                </arc:AiModelProvider.Mark>
            </arc:AiModelProvider>
        </arc:ModelPicker.Providers>
        <arc:AiModel Id="aurora-prime-3" Name="Aurora Prime 3" ProviderId="aurora"
                     Blurb="Deep work: refactors, research, long tasks"
                     Description="A careful planner for multi-step coding and research."
                     ContextTokens="800000" InputPrice="6" OutputPrice="24" />
        <arc:AiModel Id="aurora-pocket-24" Name="Aurora Pocket 2.4" ProviderId="aurora" Tag="Fast"
                     ContextTokens="400000" InputPrice="0.4" OutputPrice="1.6" />
    </arc:ModelPicker>
</UserControl>
```

Effort levels are a list, so set them in code (XAML has no array literal):

```csharp
prime.EffortLevels = [ModelEffort.Low, ModelEffort.Medium, ModelEffort.High, ModelEffort.ExtraHigh, ModelEffort.Max];
prime.DefaultEffort = ModelEffort.Medium;
```

The first four models are featured. `FeaturedIds` picks others, in the order you give. The rest appear under "More models",
grouped in the order of `Providers`. With no selection the picker selects the first featured model once and writes it
back to `SelectedModel`.

### Anatomy

1. **Pill**: 40 px high with a full radius and a 1 px border. It shows the model's mark (16 px, no tile), the name in
   medium weight, the effort in the muted text color when the model has one, and a chevron that turns while the menu is
   open. Hover and open use the muted surface and the strong border. A keyboard focus ring appears only for keyboard use.
2. **Menu**: 304 px wide (the window width less 24 px when narrower), on the raised surface with the panel radius and the
   floating shadow. It opens 8 px below the pill.
3. **Featured rows**: 54 px high. Each has the mark (18 px), the name, the blurb in the secondary color, and a check on
   the selected model.
4. **Effort row**: 40 px high, with the current level on the right. It is hidden when the selected model offers no levels.
5. **More models row**: shows the name of the selected model on the right when that model is not featured. It is hidden
   when every model is featured.
6. **Submenus**: the effort list is 264 px wide with a name, a description and a check on each level, and "Default"
   after the model's default. The other models list is 272 px wide, at most 420 px high, scrolls, and groups its compact
   rows (38 px) under the provider names, with the model's `Tag` at the end of the row.
7. **Summary**: a description of the model, a table of Context, Input, Output and Effort, an optional note, and a
   confirmation line.

### Choosing a model, effort and more

Press the pill, or press `Enter`, `Space` or `ArrowDown` while it has focus, to open the menu. One highlight follows the
pointer and jumps with the arrow keys. Choose a model to select it, close the menu and return focus to the pill. Choose
Effort or More models to open a submenu to the right; when there is no room on the right it opens on the left, and when
there is room on neither side it opens as a page. Choosing the model that is already selected closes the menu and
changes nothing.

A model that does not offer the current effort moves it to the nearest level it does offer, and to the lower of two
equal choices. The picker says so in the confirmation: "Aurora Prime 3 → Cobalt Atlas 2 · Effort adjusted to High". A
model with no levels has no effort (`Effort` is null) and the Effort row disappears.

### Pages in a narrow window

When the window is at most `PagedMenuWidth` wide (640 by default) the Effort and More models rows open pages inside
the menu instead of flyouts. A back row heads each page, the content slides in from the side of travel, and the menu's
height follows the page. The demo sets a very wide `PagedMenuWidth`, so you can see the pages in a wide window.

<Demo name="Pages" />

### The summary and the confirmation

The summary shows the selected model's `Description` and four facts: Context (`ContextTokens`, such as "800K tokens"),
Input and Output (`InputPrice` and `OutputPrice` per million tokens, with `CurrencySymbol`) and Effort. A model without
levels shows "Fixed". Facts the model leaves empty show a dash. At 400 px or narrower the facts fold into two rows of two,
and a three-line description makes the summary taller without moving the pill.

After a choice in the menu, a confirmation appears under the facts: "Aurora Prime 3 → Cobalt Atlas 2" for a model, or
"Effort: Medium → High" for an effort level. It stays for `NoticeDuration` (2.6 seconds) and fades out. Its space is
always reserved, so nothing below it moves. `SummaryNote` is the small print under the facts; use it to state the unit
and whether the prices are real.

### The pill alone

Set `IsSummaryVisible` to false and the picker is only the pill. The menu, the keyboard behavior, the events and the
announcements are the same, so the pill can sit in a header or a toolbar.

<Demo name="PillOnly" />

### Reading and setting the choice

`SelectedModel`, `SelectedModelId` and `Effort` are two-way. `ModelChanged` and `EffortChanged` run after a choice in the
menu, a snap to a level the model offers, or a change made in code. `IsSnap` on the effort event is true when a new
model moved the effort. `SelectCommand` and `EffortCommand` run with the new value for view models that prefer commands.

<Demo name="Bindings" />

```csharp
public sealed class PickerSettings
{
    public string? ModelId { get; set; }

    public ModelEffort? Effort { get; set; }
}

picker.ModelChanged += (_, e) => settings.ModelId = e.NewModel?.Id;
picker.EffortChanged += (_, e) => settings.Effort = e.NewEffort;

// An id the catalog does not have yet waits for it.
picker.SelectedModelId = "cobalt-atlas-2";
```

```xml
<arc:ModelPicker SelectedModel="{Binding Model, Mode=TwoWay}"
                 Effort="{Binding Effort, Mode=TwoWay}"
                 ModelsSource="{Binding Catalog}"
                 IsOpen="{Binding IsPickerOpen, Mode=TwoWay}" />
```

- `ModelsSource` follows a collection you can change; it replaces the inline `Models`.
- `IsOpen` is two-way. Setting it before the picker is on screen opens the menu once it is shown. `Open()` and `Close()`
  do the same from code, and `Open(ModelMenuOpenFocus.Selected)` starts the keyboard position on the selected model.
- Setting `Effort` to a level the selected model does not offer corrects it at once, and the event reports the level that
  is really in use.

### Wording

`Labels` holds every word the picker shows or says, in a `ModelPickerLabels` object with English defaults. It has a part
for the pill, the menu and the summary, plus the confirmation and the announcements. Assign a new instance to change
them. The level names appear in all four places, so give each part the same function.

<Demo name="CustomWording" />

```csharp
picker.Labels = new ModelPickerLabels
{
    EffortName = effort => effort == ModelEffort.Max ? "Maximal" : ModelEffortNames.Name(effort),
    ModelNotice = (previous, next) => $"{previous} → {next}",
    Menu = new ModelMenuLabels { MoreModels = "Weitere Modelle", Effort = "Aufwand" }
};
```

### Marks

A model's `Mark` is either a `Geometry` drawn in a 24 × 24 box and filled with `MarkColor` (or the text color, which
flips with the theme), or an `IImage` such as a `DrawingImage` that keeps its own colors. With no `Mark` the model uses
its provider's. Marks are decorative; the name is always next to them. Use your own shapes: the picker never adds a tile
or a border around a mark.

### Content guidelines

- **Blurb**: one line that says what the model is good at, such as "Reads charts, screenshots and PDFs". It is cut with
  an ellipsis, so put the useful words first.
- **Description**: one or two sentences for the summary. Say what it is for and what the person trades off.
- **Tag**: one or two words, such as "Fast" or "Open weights". It is plain text, not a badge.
- **Prices**: print real values only when they are real. Set `IsSamplePrice` on a placeholder and say so in `SummaryNote`.
- **Names**: use the name people see in your product, and keep the featured list to three to five models.

| Instead of | Write |
|---|---|
| The best model for everything | Deep work: refactors, research, long tasks |
| Cheap | Quick and capable for daily chats |
| Model with 1000000 tokens | 1M tokens of context (the summary formats it) |

### Best practices

**Do**

- Give every model a stable `Id` and store the id, not the display name.
- Keep `EffortLevels` and `DefaultEffort` together on the model; the default must be one of the levels.
- Put the picker where the choice applies, such as the header of the chat or the field it affects.
- Handle `ModelChanged` and `EffortChanged` to save the choice; the picker keeps it in memory only.
- Pass `Providers` so the "More models" list is grouped and models without marks get their provider's.

**Avoid**

- Real vendor logos inside a design that is not theirs.
- More than five featured models. The menu stops being a short list.
- Hiding an effort the model cannot change: leave `EffortLevels` empty and the row disappears by itself.
- Calling `Open()` on every update. It moves keyboard focus into the menu.

### Common questions

**Why is there no search?** The menu is a short list with a long tail grouped by provider. For a long catalog use
`Combobox` and your own filter.

**Why does a level change when I pick another model?** The new model does not offer it. The picker moves the effort to the
nearest level that model offers, and says so in the confirmation.

**Can the menu open on hover?** No. The pill opens on a press, as a menu button does, so a pointer passing over it does
not open anything.

**Why does the summary say "Fixed"?** The selected model offers no effort levels. Change the wording with
`Labels.Summary.NotAdjustable`.

## API reference

<!-- api: ModelPicker -->

<!-- api: AiModel -->

<!-- api: AiModelProvider -->

### ModelEffort

| Value | Name | Description |
|---|---|---|
| `Low` | Low | Short replies, lowest latency |
| `Medium` | Medium | Everyday balance |
| `High` | High | Works through harder problems |
| `ExtraHigh` | Extra high | Long, careful reasoning |
| `Max` | Max | Exhaustive, slowest |

`ModelEffortNames.Name(effort)` and `ModelEffortNames.Description(effort)` return these words, and `ModelEffortSnap.Snap`
picks the level a model offers.

### ModelPickerLabels

| Property | Default | Where it shows |
|---|---|---|
| `Pill` | `ModelPillLabels` | Accessible name of the pill |
| `Menu` | `ModelMenuLabels` | Rows, pages and accessible names of the menu |
| `Summary` | `ModelSummaryLabels` | Fact labels and values of the summary |
| `EffortName` (function of level) | "Medium" | Confirmation and announcements |
| `ModelNotice` (function of old and new name) | "A → B" | Confirmation after a model change |
| `EffortNotice` (function of old and new level) | "Effort: Medium → High" | Confirmation after an effort change |
| `ModelAdjustedNotice` | "A → B · Effort adjusted to High" | Confirmation when the effort moved too |
| `ModelAnnouncement` | "Now using Aurora Prime 3, medium effort" | Polite announcement after a model change |
| `EffortAnnouncement` | "Aurora Prime 3, high effort" | Polite announcement after an effort change |

### ModelMenuLabels, ModelPillLabels and ModelSummaryLabels

| Property | Default | Where it shows |
|---|---|---|
| `ModelMenuLabels.Menu` | "Models" | Accessible name of the menu |
| `ModelMenuLabels.Featured` | "Featured models" | Accessible name of the featured group |
| `ModelMenuLabels.Effort` / `MoreModels` | "Effort" / "More models" | The two rows under the featured list |
| `ModelMenuLabels.EffortMenu` / `MoreModelsMenu` | "Effort" / "More models" | Submenu names and page titles |
| `ModelMenuLabels.Default` | "Default" | After the model's default level |
| `ModelMenuLabels.BackTo` | "Back to featured models, Effort" | Accessible name of the back row |
| `ModelMenuLabels.Empty` | "No models available" | The only line of an empty menu |
| `ModelPillLabels.AutomationName` | "Model: Aurora Prime 3, medium effort. Change model" | Accessible name of the pill |
| `ModelSummaryLabels.Region` | "Selected model" | Accessible name of the summary |
| `ModelSummaryLabels.Context` / `Input` / `Output` / `Effort` | "Context" / "Input" / "Output" / "Effort" | Fact labels |
| `ModelSummaryLabels.NotAdjustable` | "Fixed" | Effort fact of a model without levels |
| `ModelSummaryLabels.ContextValue` | "800K tokens" | Context fact |

## Accessibility

- The pill is a button named by the model and the effort, such as "Model: Aurora Prime 3, medium effort. Change model",
  and reports whether the menu is open.
- The menu is a menu. Featured models and effort levels are radio items with a checked state; the Effort and More models
  rows report that they open a submenu; the provider groups are named by the provider.
- After a choice, a polite live region says "Now using Aurora Swift 3, medium effort" (or the effort sentence for an
  effort change). The visible confirmation is not what screen readers hear.
- Focus returns to the pill when the menu closes. After a keyboard close the pill shows its focus ring; after a pointer
  close it does not.
- The summary is a labeled region, and each fact reads as one phrase, such as "Context: 800K tokens".
- Marks are decorative.
- Reduced motion keeps every state and every announcement.

### Keyboard

| Keys | Action |
|---|---|
| `Enter`, `Space` or `ArrowDown` on the pill | Opens the menu with the selected model highlighted |
| `ArrowUp` / `ArrowDown` in the menu | Moves between the rows; wraps around at the ends |
| `Home` / `End` | First or last row |
| A letter | Jumps to the next row whose name starts with it; repeat to cycle |
| `Enter` or `Space` on a model or level | Chooses it, closes the menu and returns focus to the pill |
| `Enter`, `Space` or `ArrowRight` on Effort or More models | Opens the submenu with the selected entry highlighted |
| `ArrowLeft` | Closes the submenu (or goes back a page) and returns to its row |
| `Escape` | Closes the whole menu and returns focus to the pill |
| `Tab` | Does nothing inside the menu |

## Motion

- The menu grows out of the pill: it starts 6 px toward it at scale 0.97, fades in over 160 ms and settles on Arc's
  spring curve over 580 ms. It leaves in 130 ms at half the distance and scale 0.985. A close during an open, or an
  open during a close, continues from where the surface is.
- One highlight glides between the rows on a snappy spring for the pointer, jumps for the keyboard, fades in where it
  appears in 80 ms and fades out in 80 ms.
- Submenus enter and leave the same way from the side of their row. Moving to another row closes the open submenu at
  once; a diagonal move toward it does not.
- In a narrow window the new page slides in 12 px from the side of travel while it fades in over 160 ms, and the menu's
  height follows the page on the smooth spring.
- After a choice the pill's mark and text crossfade in 100 and 160 ms, and its width follows the new text on a quick
  spring. The summary crossfades its description and only the facts that changed. The confirmation fades in, stays for
  2.6 seconds and fades out.
- Reduced motion drops movement and springs and keeps 120 ms fades. Off applies every change at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Menu opens or closes | Grows from the pill; 160 ms fade, 580 ms spring | 120 ms fade | Immediate |
| Highlight | Glides, fades in and out in 80 ms | Jumps, 120 ms fade | Immediate |
| Page in a narrow window | Slides 12 px and fades in; height springs | 120 ms fade | Immediate |
| Pill changes model | Mark and text crossfade; width springs | 120 ms crossfade | Immediate |
| Summary changes | Description and changed facts crossfade | 120 ms crossfade | Immediate |
| Confirmation | Fades in, stays 2.6 s, fades out | 120 ms fades | Immediate |

## Related

- `Select` and `Combobox` for choosing one item from a list that is not about models.
- `DropdownMenu` for a menu of actions.
- `SegmentedControl` for a short choice that stays visible.

| Component | Choose it when |
|---|---|
| `Select` / `Combobox` | The list is long, searchable or not about models |
| `DropdownMenu` | The menu lists actions, not a choice that stays selected |
| `SegmentedControl` | A few options must be visible at once |
| `Popover` | A small form or panel opens from a button |
