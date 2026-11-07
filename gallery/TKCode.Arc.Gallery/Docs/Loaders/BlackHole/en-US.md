---
title: Black hole
summary: A delete transition that spirals the chosen items into a small singularity while the rest of the list bends around it, and spins them back out on undo.
---

<Demo name="Preview" />

## When to use

`BlackHole` wraps a list, a grid or a board and gives a delete a moment of weight. Hand it the rows you are about to
remove and they fall into a hole a few pixels wide, each point along a logarithmic spiral, while everything else in the
wrapper bends around the hole like light around a mass. The hole collapses with one soft shockwave and the real content
is back, with the swallowed rows hidden so your list can close the gap. Undo plays the same path backwards.

- **Deleting or archiving a few selected rows, cards or files**, followed by an Undo.
- **Emptying a trash, clearing notifications or dismissing a batch** where the removal should feel final.
- **A demo or onboarding moment** where one delete should be memorable.

## When not to use

- **Frequent, small removals** such as clearing a chip or a tag. A plain height collapse is quicker to read.
- **Irreversible actions without Undo.** The effect is not a confirmation; ask first, or offer Undo.
- **A long scrolling list wrapped as a whole.** The effect paints the wrapper, so wrap the visible viewport (the
  scroller's content area), not a list thousands of pixels tall.
- **Removing one item every few seconds**, such as a live feed. The 1.3 s transition would queue up behind itself.

## Usage

`BlackHole` is a `ContentControl` in the Special package: register it with `UseArcSpecial()` next to
`UseArcControls()`. Put your list inside it and keep references to the item controls, then call `SwallowAsync` with the
ones to remove. When the task completes, remove them from your data; they stay hidden until you do.

```xml
<arc:BlackHole x:Name="Hole">
    <StackPanel x:Name="Rows" />
</arc:BlackHole>
```

```csharp
await Hole.SwallowAsync(selectedRows);
foreach (var row in selectedRows)
{
    Rows.Children.Remove(row);
}
```

For Undo, put the items back at their places first and call `ReleaseAsync` right away, in the same turn of the UI
thread. They are hidden at once; the black hole waits until they stop moving (your list opening their slots), then
spins them out into place.

```csharp
Rows.Children.Insert(index, row);
await Hole.ReleaseAsync([row]);
```

Calls queue: a release asked for during a swallow starts when the swallow ends. `IsRunning` is true while a transition
runs; disable or ignore your Delete and Undo triggers meanwhile so a double press cannot delete twice.

The preview is a mailbox. Select conversations with a click, Space or Enter, then press Delete in the header, or press
Delete or Backspace on a row. The selected rows fall into a hole opened at their centre; their slots then close on a
smooth spring and a pill under the card says how many messages were removed, with Undo. Undo restores only the last
batch, at the same places, and the restored rows come back unselected. The senders, subjects and avatars are invented;
the avatars are gradients drawn in code.

### The whole API in one place

A grid of file tiles: select some, delete them, restore them. The log lists `TransitionStarted` and
`TransitionCompleted` with their direction and count.

<Demo name="Files" />

### Where the hole opens

By default the hole opens at the centre of the targets' combined bounds, so deleting one row opens it on that row and
deleting two rows far apart opens it between them. `BlackHoleOptions.Origin` places it at a point in the black hole's
coordinates, and `OriginElement` at the centre of a control, for example the button that deleted the items.
`BlackHoleOptions.Duration` changes the length of one call; `Duration` on the control sets the default (1.3 s), and a
release runs `ReleaseSpeed` (0.92) times as long.

<Demo name="Origin" />

```csharp
await Hole.SwallowAsync(rows, new BlackHoleOptions
{
    OriginElement = deleteButton,
    Duration = TimeSpan.FromSeconds(2)
});
```

### Narrow containers

The black hole measures its content at the start of every run, so it works at any width. The hole, the lens and the
shockwave keep their sizes in device-independent pixels; at 320 px the mailbox drops its time column.

<Demo name="Narrow" />

### Reduced motion

With reduced motion the items fade out in 200 ms instead of falling, and fade back in on release. With motion off they
simply disappear and reappear. The tasks complete either way, so your code does not change.

<Demo name="Reduced" />

### Anatomy

1. **Wrapper**: the black hole itself. Your content lays out and takes input as usual.
2. **Stage**: a layer over the content that exists only while a transition runs. It shows a snapshot of the content
   being bent and swallowed, and takes no input.
3. **Lensed content**: everything except the targets, bent by a point mass at the hole. Beyond the Einstein ring
   (four times the core's radius) content is pushed outward; inside it you see a small mirrored image of what lies on
   the far side of the hole.
4. **Falling items**: the targets, cut into a 5 px mesh. Each point falls along its own spiral, so rows stretch into
   curved arms.
5. **Hole**: a black core with a 5.5 px radius, a thin gray photon ring just outside it and a soft dark halo.
6. **Shockwave**: one ring that runs out to 90 px after the collapse, dark on light surfaces and light on dark ones.

### States

| State | What is on screen |
|---|---|
| At rest | The content, nothing else. The stage is hidden and costs nothing |
| Swallowing | The stage over the content: lens, falling items, hole; then the collapse and the shockwave |
| Swallowed | The content again, with the targets hidden (opacity 0) until you remove them |
| Waiting to release | The restored targets hidden while their slots open |
| Releasing | The stage again, playing the swallow backwards |
| Queued | A call waits for the running one; `IsRunning` stays true |

### Content guidelines

- Pass the controls that represent whole items (a row, a card, a tile), not their inner text blocks: whatever the
  control paints falls in, including its selected background.
- Keep the wrapper to the area that changes. Headers, toolbars and the Undo message belong outside it, so they stay
  sharp and clickable while the effect runs.
- Say what happened in words next to the list ("2 messages removed") with Undo. The effect is visual only.

### Best practices

**Do**

- Remove the items from your data only after `SwallowAsync` completes.
- Pair the list with a height animation, so the gap closes after a delete and opens before a release.
- Call `ReleaseAsync` in the same turn as you put the items back, so they never flash at their places first.
- Ignore Delete and Undo while `IsRunning` is true.

**Avoid**

- Removing the items before the swallow finishes: they would vanish from the snapshot's place with nothing left to fall.
- Wrapping a whole page. The snapshot and the per-pixel lens cost grow with the wrapper's area.
- Relying on the effect for feedback. Announce the result in a live region.

### Common questions

**Do the hidden items still take space?** Yes. A swallowed item keeps its size with opacity 0 until you remove it, so
nothing jumps under the effect. Remove it (or animate its height to zero) when the task completes.

**What if the black hole leaves the window during a transition?** The transition jumps to its end and the task
completes, so your code continues.

**Does it need a GPU?** No. The content is snapshotted once with `RenderTargetBitmap` (at the screen's scaling, at most
2x), and each frame is painted on the CPU in parallel bands of rows, then drawn as one bitmap.

**Why are the targets' own animations frozen during the effect?** The effect shows a snapshot taken when it starts. The
real content is back the moment it ends.

## API reference

<!-- api: BlackHole -->

<!-- api: BlackHoleOptions -->

<!-- api: BlackHoleTransitionEventArgs -->

<!-- api: BlackHoleSamples -->

<!-- api: BlackHoleSampleMessage -->

`BlackHoleDirection` is `Swallow` or `Release`.

## Accessibility

- The stage is hidden from assistive technology and exists only while a transition runs; the real controls are back the
  moment it ends.
- The effect says nothing by itself. Announce the result in a polite live region ("2 messages removed"), as the
  preview's Undo pill does, and keep Undo reachable by keyboard.
- Input is not blocked during a transition. Ignore your triggers while `IsRunning` is true, without dimming them.
- In the preview, each row is a checkbox named after its sender, subject and time. Keyboard focus shows a 2 px ring
  outside the row; pointer focus draws none. When a focused row is deleted, focus leaves it with the row.

### Keyboard

The black hole has no keys of its own. Wire Delete or Backspace to your own selection, as the preview does:

| Keys | Action (preview) |
|---|---|
| `Tab` / `Shift+Tab` | Move between the Delete button, the rows and Undo |
| `Space` / `Enter` on a row | Select or deselect it |
| `Delete` / `Backspace` on a row | Delete the selection |
| `Enter` / `Space` on Delete or Undo | Press it |

## Motion

Times are for the default 1.3 s; another `Duration` scales all of them.

- The hole opens in 190 ms with a 10 % overshoot, grows by 40 % while it eats, holds, and collapses in 95 ms with an
  ease-in.
- Each point of an item starts falling 110 ms after the start plus 1.84 ms per pixel of distance from the hole (less if
  the farthest point would land after 90 % of the duration), then falls for 610 ms, its radius shrinking on an
  ease-in sine. Its angle follows θ = θ₀ + 0.88 · ln(r₀ / r), clockwise on screen: about 50° each time the distance
  shrinks by a factor of 2.7.
- The lens strength follows the core: the Einstein radius is four times the core's radius at every moment, so the bend
  opens, grows and collapses with the hole.
- The shockwave starts 30 ms into the collapse and runs out to 90 px in 160 ms (ease-out cubic), 6 px wide, its
  opacity falling from 0.6 with the square of the remaining time.
- A release is the swallow played backwards over 0.92 of the duration, after the restored items stop moving.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Swallow | Spiral, lens, hole, shockwave | Items fade out in 200 ms | Items disappear |
| Release | The same, backwards | Items fade in in 200 ms | Items appear |
| Task completes | When the effect ends | When the fade ends | At once |

## Responsive behavior

- The wrapper is measured at the start of every run, so the effect works at any width, 320 px included.
- The hole, the lens and the shockwave are sized in device-independent pixels; the snapshot follows the screen's
  scaling up to 2x.
- Frames are painted on the CPU: the cost grows with the wrapper's area. The preview's list (478 × 313) is the size the
  effect is tuned for.

## Related

- `SkeletonMorph` for content that grows out of its placeholders.
- `StretchRefresh` for a pull-to-refresh feed.
- `Toast` for the result message and its Undo.
