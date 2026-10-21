---
title: Empty states
summary: Four empty states in one illustration whose shapes morph between scenes as you switch tabs.
---

<Demo name="Preview" />

## When to use

- A page or panel that can be empty for more than one reason: no search results, no connection, nothing left to
  read, or a missing page.
- An empty state with one clear next step that takes a moment, such as a retry, a refresh or a search.
- A settings or help page that shows the empty states of a product side by side.

## When not to use

- One empty list with an icon and a button: use `EmptyState`.
- The content is still loading: use `Skeleton`.
- An error on a page that already shows content: use `Alert`.

## Usage

With no items the block shows Arc's four scenes and copy. Handle `ActionInvoked` and set `Phase` to show what the
action did. The block itself never does any work.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:EmptyStates Label="Workspace" />
</UserControl>
```

### Scenes and copy

Each `EmptyStatesItem` is one tab. `Scene` picks the drawing: `NoResults`, `Offline`, `CaughtUp` or `NotFound`. Set
`Header`, `Title`, `Description` and `ActionLabel` for the Idle phase, and the `Done` versions for the result. Text
you leave out uses Arc's copy for the scene. With one item the tab row is hidden.

<Demo name="CustomItems" />

```xml
<arc:EmptyStates>
    <arc:EmptyStatesItem Scene="Offline"
                         Title="Sync is paused"
                         ActionLabel="Resume sync" />
    <arc:EmptyStatesItem Scene="NotFound" />
</arc:EmptyStates>
```

### Actions and phases

Pressing the button raises `ActionInvoked` with the item and the phase it was pressed in, then runs `ActionCommand`
with the same arguments. Set `Phase` to `Loading` while the work runs: the button shows a spinner, the description
changes to `LoadingDescription`, and the title and button label stay. Set `Done` when it worked, or `Idle` when it
failed. Presses are ignored while loading.

<Demo name="AsyncRetry" />

```csharp
states.ActionInvoked += async (_, e) =>
{
    if (e.Phase == EmptyStatesPhase.Done)
    {
        states.Phase = EmptyStatesPhase.Idle;
        return;
    }
    states.Phase = EmptyStatesPhase.Loading;
    var ok = await sync.RetryAsync();
    states.Phase = ok ? EmptyStatesPhase.Done : EmptyStatesPhase.Idle;
};
```

To change the copy after a failed attempt, set the item's `Description` while the phase is still `Loading`. The new
text then appears together with the return to `Idle`.

### Selection

`SelectedIndex` and `SelectedItem` are two-way and stay in step. Choosing another tab puts `Phase` back to `Idle`. Set
`Phase` together with the selection to open on a result.

```xml
<arc:EmptyStates SelectedIndex="1" Phase="Done" />
```

### Narrow layouts

The block follows its own width. At 420 or less the tabs get smaller text and padding. From 560 the title is larger.
Labels that do not fit keep their width, and the row slides to keep the selected tab in view.

<Demo name="Narrow" />

## API reference

<!-- api: EmptyStates -->

<!-- api: EmptyStatesItem -->

## Accessibility

- The block is a region named by `Label`. The tab row is a tab list named by `TabListLabel`, and each tab reports
  whether it is selected.
- Only the selected tab is a Tab stop. ArrowLeft and ArrowRight move between tabs and wrap around. Home and End go to
  the first and last tab. Moving selects the tab.
- The drawing is an image named after what it shows, such as "A cloud joined to a device by a broken line". Set
  `IllustrationLabel`, `LoadingIllustrationLabel` and `DoneIllustrationLabel` to name it in another language.
- After each phase change the new title and description, or the loading text, are announced politely.

## Motion

- Switching tabs moves every shape of the drawing to the next scene on a spring. Outlines morph point by point and
  lines draw in a little later. Colors cross over in 480 ms.
- A phase change nudges the same shapes with shorter delays.
- The old copy lifts away with a blur while the new title and description rise in. The copy height springs to fit,
  so the button does not jump. The tab highlight glides to the selected tab.
- Under a mouse the layers of the drawing tilt slightly toward the pointer.
- Reduced motion crossfades the old drawing into the new one and fades the copy. Off switches at once.

## Related

- `EmptyState` for a single empty list with an icon, a message and an action.
- `Skeleton` while content is loading.
- `Alert` for an error on a page that already shows content.
