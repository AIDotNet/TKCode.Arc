---
title: Reactions
summary: Acknowledge a post in one tap, with counts that roll.
---

## When to use

- Acknowledging a post, comment or release note with one tap, like a chat reaction.
- Showing how many people reacted with each option, and whether the current user is one of them.
- A short, fixed set of options, such as Like, Love and Celebrate.

## When not to use

- A reply that needs words: give the post a reply field instead.
- A count that cannot be changed by the viewer: use `Badge`.
- Choosing one value from a list: use a selector or a menu.

## Usage

The options are the children of `Reactions`. The counts and the viewer's own reactions are its `Value`, which is
set in code.

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

A `ReactionOption` takes a 24 by 24 Lucide geometry in `Icon`.

### Value and changes

`Value` is two-way. A pill with a count of 0 leaves the bar. `Reacted` marks the viewer's own reaction, and
`ReactedBy` lists the names shown in the tip.

```csharp
var people = new Reactions
{
    Value =
    [
        new Reaction("love", 4, true) { ReactedBy = ["Ada Okafor", "Jonas Berg", "Lin Park"] },
        new Reaction("celebrate", 2) { ReactedBy = ["Maya Chen", "Sam Ortiz"] }
    ]
};
```

`ValueChanged` runs after a change. `Next` is the new value and `Change` names the option and whether the viewer
reacted.

```csharp
reactions.ValueChanged += (_, e) =>
{
    // Save e.Next with the post.
};
```

### Labels

`Label` names the bar. `AddLabel` and `PickerLabel` name the add button and the picker. `TipDelay` sets how long
the pointer must rest on a pill before its tip appears.

```xml
<arc:Reactions Label="Reactions to this release"
               AddLabel="Add a reaction"
               TipDelay="0:0:0.6">
    <arc:ReactionOption Id="ship" Label="Ship it" />
    <arc:ReactionOption Id="star" Label="Star" />
</arc:Reactions>
```

### Wrapping and width

The bar wraps when the pills do not fit. Give it a `Width` to see the wrap.

```xml
<arc:Reactions Width="220" HorizontalAlignment="Left" />
```

## API reference

<!-- api: Reactions -->

<!-- api: ReactionOption -->

<!-- api: Reaction -->

## Accessibility

- The bar is a group named by `Label`. Each pill is a toggle button, and its name gives the label, the count and
  whether the viewer is among them.
- The picker is a toolbar with roving focus. The four arrow keys move between options and wrap around, Home and End
  go to the ends, and every option has a name.
- Enter or Space on a pill switches the viewer's reaction. Enter or Space on the add button opens the picker, and
  Escape closes it and returns focus to the add button.
- Every change is announced once through a polite status region, such as "Added Love. 4 reactions."
- When the focused pill leaves the bar, focus moves to a neighbouring pill or to the add button.

## Motion

- A new pill grows in from a half size with a soft blur. A removed pill shrinks and fades. The other pills glide
  into their new places.
- The icon gets a small kick when the viewer reacts, and fast taps add up instead of restarting it.
- Counts roll only the digits that changed, up when they grow and down when they shrink.
- The picker grows out of the add button, and its options appear one after another.
- Reduced motion uses short fades only and no size springs. Off motion changes everything at once.

## Related

- `Badge` for a fixed count that the viewer cannot change.
- `Popover` and `DropdownMenu` for other content or choices that open from a button.
- `SwipeActions` for row actions in a touch list.
