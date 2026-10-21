---
title: Reactions
summary: Acknowledge a post in one tap, with counts that roll.
---

<Demo name="Default" />

## When to use

`Reactions` is a bar of reaction pills with counts, plus an add button that opens a small picker. It lets people
acknowledge a piece of content in one tap, without writing a reply. It sits under the content it refers to, such as a
release note, a status update or a design review comment, and the counts show how many people responded with each
option.

- **Release notes and changelog entries**: a team reads "Release 2026.10 is live" and taps "Ship it" or "Celebrate".
  The counts show how many engineers saw the release, and the viewer's own reaction is highlighted when the saved list includes it.
- **Status updates in a team workspace**: a daily incident summary or a project update gets a "Like" or "Thanks" from
  the people who read it, so the author knows the update landed without a reply thread.
- **Design review comments**: a reviewer marks a comment as "Agreed" or "Question" with one tap. The counts tell the
  author which comments the team already accepts.
- **Announcement feeds in an internal portal**: a welcome post for a new hire gets "Love" and "Celebrate". The
  tooltip names the people who reacted, which turns a count into a sign of who saw it.

## When not to use

- **A response that needs words**, such as a question, a decision or a reply to a comment: use `CommentThread` or
  `ChatThread`, which keep the text and its author together. A reaction cannot carry a sentence.
- **A count the viewer cannot change**, such as the number of open tickets or unread messages: use `Badge` for a fixed
  count, or `AnimatedCounter` when the number should roll in a metric.
- **Choosing one value from a list**, such as a priority, a status or a category: use `Select`, `Combobox` or
  `MorphSelect`. A reaction bar allows several options at once and has no single result.
- **A rating on a numeric scale**, such as one to five stars or a satisfaction score: use `Slider` or a form control
  that stores a number. A reaction carries a choice, not a quantity.
- **Row actions in a touch list**, such as archive or mark as read on an inbox row: use `SwipeActions`, which keeps
  the action next to the row it applies to.

## Usage

The options are the children of `Reactions`. The counts and the viewer's own reactions are its `Value`, which is
set in code or bound from a view model.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Reactions Label="Reactions">
        <arc:ReactionOption Id="like" Label="Like" />
        <arc:ReactionOption Id="love" Label="Love" />
        <arc:ReactionOption Id="celebrate" Label="Celebrate" />
    </arc:Reactions>
</UserControl>
```

```csharp
var reactions = new Reactions
{
    Value = [new Reaction("like", 3)]
};
reactions.Options.Add(new ReactionOption("like", "Like", ArcIcons.ThumbsUp));
reactions.Options.Add(new ReactionOption("love", "Love", ArcIcons.Heart));
reactions.Options.Add(new ReactionOption("celebrate", "Celebrate", ArcIcons.Smile));
```

`Reactions` derives from Avalonia's `TemplatedControl`, so its template, style and layout behave like other Arc
controls. The options are plain data (`ReactionOption` is not a control), and the bar builds one toggle pill for each
reaction that has a count. Use the same `Id` values in `Options` and in `Value`: the id is what your server stores
for a reaction, and the label is only what people read.

A `ReactionOption` takes a 24 by 24 Lucide geometry in `Icon`, drawn with a 1.75 stroke. Icons that are not in
`ArcIcons` can be parsed with `ArcIcons.Parse` or taken from a `StreamGeometry` resource, as the gallery does for a
party popper.

### Anatomy

1. **Bar** (`Label` names it): a group that wraps its pills in rows. Pills are 6 px apart in a row and rows are 8 px
   apart. The bar grows to fit the rows it needs.
2. **Pill**: 32 px high with a pill radius. It has 9 px of padding before the icon and 11 px after the count, with 6 px
   between the icon and the count. The icon is 16 px, and the count uses the small text size in the secondary text
   color.
3. **Count**: the number of people who reacted. When the number changes, only the digits that changed roll.
4. **Viewer's own reaction**: a pill with `Reacted` set has the subtle accent fill and the strong accent text. Its
   border takes a ring of the accent color mixed into the border at 45%, so it stays visible on any surface.
5. **Add button** (`AddLabel`, default "Add reaction"): 38 px wide and 32 px high, showing a smile-plus glyph. It shows
   a close glyph while the picker is open.
6. **Picker** (`PickerLabel`, default "Add a reaction"): a tray that opens above the add button with an 8 px gap. It
   pads its content by 5 px. Each option is a 40 px round toggle with a 20 px glyph, and a caption under the options
   names the highlighted option. The caption adds a check when the viewer has already chosen that option.
7. **Tip**: the pill's name and its tooltip, which appears after `TipDelay` (450 ms by default). It shows the option
   label and, when `ReactedBy` is known, the people who reacted.

### Value and changes

`Value` is two-way. Each `Reaction` holds an id, a count and the viewer's own flag. A reaction with a count of 0 is not
shown, so it leaves the bar. A reaction whose id matches no option is not shown either. When an id appears twice in
`Value`, the first entry wins.

| Action | Result in `Value` | Event |
|---|---|---|
| The viewer taps a pill that is not theirs | Count plus one, `Reacted` set to `true` | `ValueChanged` with `Change.Reacted` set to `true` |
| The viewer taps their own pill | Count minus one, `Reacted` set to `false`; a count of 0 removes the reaction | `ValueChanged` with `Change.Reacted` set to `false` |
| The viewer picks an option that has no pill yet | A new reaction with count 1 joins the end of the list | `ValueChanged` |
| Code sets `Value` | The bar shows the new list; no change is raised | None |

<Demo name="WhoReacted" />

`ValueChanged` is raised after `Value` has changed. Its `Next` property is the new value in bar order, without the
reactions whose count reached zero, and its `Change` property names the option and whether the viewer reacted.

```csharp
reactions.ValueChanged += (_, e) =>
{
    // Save e.Next with the post, and e.Change to show what the viewer did.
};
```

`Toggle(id)` performs the same change as a tap, so a keyboard shortcut or a menu item can add or remove the viewer's
reaction from code. `OpenPicker()` and `ClosePicker()` open and close the picker, and `IsPickerOpen` tells you whether
it is open.

### Binding to a view model

In an MVVM app the view model owns the saved reactions, and the bar updates them when the viewer taps. Each
`Reaction` is an immutable record, so the view model replaces the list instead of editing an item in place. With
CommunityToolkit.Mvvm:

```csharp
public partial class ReleaseNoteViewModel : ObservableObject
{
    private readonly IReleaseNoteService _notes;
    private readonly string _noteId;

    public ReleaseNoteViewModel(IReleaseNoteService notes, string noteId)
    {
        _notes = notes;
        _noteId = noteId;
    }

    [ObservableProperty]
    private IReadOnlyList<Reaction> _reactionValues = [];

    public async Task SaveReactionsAsync(IReadOnlyList<Reaction> next)
    {
        await _notes.SaveReactionsAsync(_noteId, next);
    }
}
```

```xml
<arc:Reactions Label="Reactions to Release 2026.10"
               Value="{Binding ReactionValues}"
               HorizontalAlignment="Left">
    <arc:ReactionOption Id="ship" Label="Ship it" Icon="{x:Static arc:ArcIcons.Zap}" />
    <arc:ReactionOption Id="love" Label="Love" Icon="{x:Static arc:ArcIcons.Heart}" />
    <arc:ReactionOption Id="celebrate" Label="Celebrate" Icon="{x:Static arc:ArcIcons.Smile}" />
</arc:Reactions>
```

```csharp
// In the view's code-behind, where the view model is available:
reactions.ValueChanged += async (_, e) => await viewModel.SaveReactionsAsync(e.Next);
```

- `Value` is two-way by default, so the bar writes each tap back to `ReactionValues` before `ValueChanged` is raised.
  Keep the binding two-way; a one-way binding makes the bar show a change that the view model never saved.
- The bar does not remember the viewer. Your server or view model decides which reactions belong to the signed-in
  user, and sets `Reacted` when it loads the list.
- Compiled bindings need `x:DataType` on the view (the examples here leave it out for brevity).

### Wrapping and width

The bar wraps its pills into rows when they do not fit. Give it a `Width` to see the wrap, or leave the width free so
the bar takes its parent's width. When a change adds a row, the bar's height springs to the new size, and the pills
glide into their new places.

<Demo name="Wrapping" />

Keep the bar on one row in a dense list row or a card footer by giving it enough width for the expected number of
options. In a narrow column, the wrap is the right behavior: it keeps every pill visible without a scroll bar.

### Labels and tips

`Label` names the bar, `AddLabel` names the add button and its tooltip, and `PickerLabel` names the picker. Use a name
that says what the bar refers to, such as "Reactions to this release", so a screen reader user hears the context
before the pills.

`TipDelay` sets how long the pointer must rest on a pill before its tip shows. The default is 450 ms. `TipSelector`
builds the tip for each pill: it receives the option and the reaction and returns the content to show, or `null` to
use the default text.

```xml
<arc:Reactions Label="Reactions to this release"
               AddLabel="Add a reaction"
               TipDelay="0:0:0.6">
    <arc:ReactionOption Id="ship" Label="Ship it" />
    <arc:ReactionOption Id="star" Label="Star" />
</arc:Reactions>
```

The tip is the option label followed by the people who reacted, when your data has them. The default lists up to
three names, with "You" first when the viewer reacted. It then says how many others there are:

| Reaction data | Tip |
|---|---|
| No `ReactedBy` | "Love" |
| `Reacted` and two names | "Love · You, Ada and Lin" |
| Six people, four named | "Love · Ada, Lin, Sam and 3 others" |

### Who reacted

Set `ReactedBy` on a `Reaction` to name the people behind the count. The names are shown only in the tip, not in the
bar, so the pill stays compact. Pass the names in the order you want them to appear, most relevant first. The tip shows up to three names.

Names are optional. A count without names is still correct, and the tip falls back to the option label.

### Live counts

Counts that change on another person's screen arrive through `Value`. The bar rolls only the digits that changed, up
when the count grows and down when it shrinks, and it does not play the tap impulse on the icon, because the viewer
did not tap. A pill that appears in a live update enters the bar, and one that reaches zero leaves it.

<Demo name="LiveCounts" />

Replace the `Value` list with a new list when the server sends an update. The bar matches reactions by `Id`, so a
pill that stays in the list keeps its identity and only its count changes. A new id gets a new pill, and a removed id
gets a pill that leaves the bar.

### Example: release note reactions

A release note in an internal changelog. The reactions sit under the summary so the team can acknowledge the release
without a reply. The bar names the release in its label, the counts show the team's responses, and the viewer's own
"Love" is highlighted. Each option name is a short verb or phrase that reads well in a tooltip. Put a short summary
above the bar so the reactions have something concrete to refer to. The bar is bound to `ReactionValues` from the view
model shown above, so the saved counts appear when the note opens.

```xml
<Border Padding="20"
        CornerRadius="12"
        BorderThickness="1"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        Background="{arc:ArcDesignTokenResource Surface}">
    <StackPanel Spacing="12">
        <TextBlock Text="Release 2026.10 is live"
                   FontSize="16"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="Usage exports now finish up to four times faster, and the billing summary reads the new invoice schema."
                   FontSize="14"
                   TextWrapping="Wrap"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        <arc:Reactions Label="Reactions to Release 2026.10"
                       Value="{Binding ReactionValues}"
                       HorizontalAlignment="Left">
            <arc:ReactionOption Id="ship" Label="Ship it" Icon="{x:Static arc:ArcIcons.Zap}" />
            <arc:ReactionOption Id="love" Label="Love" Icon="{x:Static arc:ArcIcons.Heart}" />
            <arc:ReactionOption Id="celebrate" Label="Celebrate" Icon="{x:Static arc:ArcIcons.Smile}" />
        </arc:Reactions>
    </StackPanel>
</Border>
```

```csharp
// The server returns the saved counts when the note loads; the viewer's own reaction has Reacted set.
viewModel.ReactionValues =
[
    new Reaction("ship", 12),
    new Reaction("love", 4, true) { ReactedBy = ["Ada Okafor", "Jonas Berg", "Lin Park"] },
    new Reaction("celebrate", 2)
];
```

The bar shows the reactions in the order of `Value`, and the viewer's own "Love" has the accent fill. Its tooltip reads
"Love · You, Ada Okafor, Jonas Berg and 1 other": the viewer first, up to three names in all, then the remaining count.

### Content guidelines

- **Option label**: one or two words that name the reaction in the same way people would say it: "Like", "Love",
  "Celebrate", "Ship it". The label is read aloud and shown in the tip, so avoid codes such as "thumbs_up" or
  "+1".
- **Bar label**: name the content the reactions refer to, "Reactions to Release 2026.10", not a generic "Reactions".
- **Add button**: "Add reaction" is the default and matches the picker's purpose. Keep it unless the product uses a
  different term for the same action.
- **Count wording**: the accessible name gives the count as a number ("Like, 4 reactions, including yours"). Do not
  repeat the count in the bar's `Label`.
- **Tip**: the label and the people who reacted, in sentence case, with no punctuation at the end.

| Instead of | Write |
|---|---|
| Reactions | Reactions to Release 2026.10 |
| +1 | Like |
| Click to react | Add reaction |
| thumbs_up | Like |
| Smiley | Celebrate |

### Best practices

**Do**

- Give every option a stable `Id` that your data stores, and a `Label` that can change with the language. Changing an
  id creates a new reaction and orphans the old counts.
- Set `Reacted` from the signed-in user's data when the list loads. Without it the bar cannot show the viewer's own
  reaction, and a second tap adds a count instead of removing one.
- Save the change in `ValueChanged`, or bind `Value` to a view model that saves it. A tap that is not saved reverts
  on reload and the counts disagree between people.
- Keep the option set short, three to six options. The picker is compact by design, and a long list makes the choice
  slower than a tap.
- Name the bar with `Label` so a screen reader user knows what the reactions refer to.

**Avoid**

- Reactions for choices that need a reply. A bar that asks people to "Agree" on a proposal hides the reasons behind
  the vote; use a comment thread or a poll.
- Showing a count of zero as a pill. A zero count leaves the bar by design, so store zero counts as missing.
- Editing a `Reaction` in place in a list that the bar already shows. Records are immutable, so replace the entry with
  `with` and assign a new list, or the bar may not refresh.
- Relying on the tip for meaning. The tip appears after a delay on hover or focus, so the option label must already
  make sense alone.
- Two options with the same label. The tooltips and the screen reader names cannot tell them apart, and the viewer
  cannot choose the right one.

### Common questions

**Why is a reaction not shown in the bar?** The bar shows only reactions whose count is above zero and whose `Id`
matches an option. Check the option id against the reaction id, and set the count to a positive number.

**How do I remove a reaction the viewer made from code?** Call `Toggle(id)` for the reaction the viewer already has.
The viewer's own reaction is removed and its count goes down by one. Calling `Toggle` on a reaction the viewer did not
make adds one for them.

**Why does the count not change when another person reacts?** The control does not poll the server. Update `Value`
when your data changes, for example from a live feed or after a refresh; the counts then roll to the new values.

**Does the order of the options change the order of the pills?** No. The picker shows the options in the order they
were added. The bar shows the reactions in the order of `Value`, and a reaction added by the viewer joins the end of
that list.

**How does the viewer's own reaction survive a reload?** It does not, unless your data stores it. Save `Reacted` or
the reaction's `ReactedBy` names with the post, and load them into `Value` the next time the page opens.

## API reference

<!-- api: Reactions -->

<!-- api: ReactionOption -->

<!-- api: Reaction -->

### Change values

`ValueChanged` passes a `ReactionChange` with the reaction that was changed and the viewer's new state.

| Member | Meaning |
|---|---|
| `Id` | The option id the viewer tapped or picked |
| `Reacted` | `true` when the viewer reacted, `false` when they removed their reaction |

## Accessibility

- The bar is a group named by `Label`. Each pill is a toggle button, and its name gives the label, the count and
  whether the viewer is among them, such as "Like, 4 reactions, including yours".
- The picker is a toolbar with roving focus. The four arrow keys move between options and wrap around, Home and End
  go to the ends, and every option has a name.
- Enter or Space on a pill switches the viewer's reaction. Enter or Space on the add button opens the picker, and
  Escape closes it and returns focus to the add button.
- Every change is announced once through a polite status region, such as "Added Love. 4 reactions." The message is
  not shown on screen.
- When the focused pill leaves the bar, focus moves to a neighbouring pill or to the add button, so keyboard focus is
  never lost.
- Hover highlights an option in the picker only for a mouse. Touch picks an option directly, so a touch user never
  needs to hover first.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Moves between the pills and the add button |
| `Enter` or `Space` on a pill | Switches the viewer's reaction on or off |
| `Enter` or `Space` on the add button | Opens the picker and moves focus to its first option |
| `Right` / `Down`, `Left` / `Up` (in the picker) | Move to the next or previous option; the focus wraps at the ends |
| `Home` / `End` (in the picker) | Move to the first or last option |
| `Enter` or `Space` (in the picker) | Picks the option, toggles the viewer's reaction and returns focus to the add button |
| `Escape` (in the picker) | Closes the picker and returns focus to the add button |

## Motion

- A new pill grows in from a half size with a soft blur. A removed pill shrinks and fades. The other pills glide
  into their new places.
- The icon gets a small kick when the viewer reacts, and fast taps add up instead of restarting it.
- Counts roll only the digits that changed, up when they grow and down when they shrink.
- The picker grows out of the add button, and its options appear one after another.
- Reduced motion uses short fades only and no size springs. Off motion changes everything at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| A pill appears | Grows from half size out of a soft blur, with a snappy scale and a quick fade | Short fade | Immediate |
| A pill leaves at zero | Shrinks to 0.6 with a subtle blur and fades; the other pills glide | Short fade | Immediate |
| The viewer taps a reaction | The icon kicks, and quick taps add velocity | No kick | No kick |
| A count changes | Only the changed digits roll, up when growing and down when shrinking | Instant fade | Immediate |
| Rows change | The bar height springs and the pills glide | No size spring | Immediate |
| The picker opens | Grows from the add button; options appear 40 ms later, 35 ms apart | Short fade | Immediate |

## Related

- `Badge` for a fixed count that the viewer cannot change.
- `Popover` and `DropdownMenu` for other content or choices that open from a button.
- `SwipeActions` for row actions in a touch list.
- `HoldToConfirm` for a single destructive action that must not run on a stray tap.

| Component | Choose it when |
|---|---|
| `CommentThread` | The response needs words, and the comment should keep its author and time |
| `ChatThread` | The conversation is a sequence of messages between people |
| `Badge` | The number is fixed and the viewer cannot change it |
| `Select` / `MorphSelect` | The person picks one value from a list |
| `SwipeActions` | The action belongs to one row in a touch list |
| `Slider` | The person sets a quantity on a scale |
