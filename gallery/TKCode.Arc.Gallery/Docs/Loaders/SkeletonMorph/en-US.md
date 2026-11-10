---
title: Skeleton morph
summary: Loading placeholders that stretch into the real content block by block, while the card eases to its new height.
---

<Demo name="Preview" />

## When to use

`SkeletonMorph` replaces the hard swap from gray boxes to content. Every visible piece of the loaded layout sits in a
`MorphBlock`. While the region loads, each block is a skeleton of the same size as its content: a round avatar, a pill
for a button, one bar per line of text. When the data arrives, the blocks resolve one after another in reading order.
Each skeleton stretches in place to the box its content really takes and fades, while the content sharpens out of a soft
blur underneath. The region eases to its new height at the same time, so a card's border never jumps.

- **Profile cards, feeds and lists that load in under a few seconds**: the layout is visible from the first frame and
  settles without a flash.
- **Dashboards whose layout is known before the data**: tiles keep their places and only their insides change.
- **Places where a hard swap would feel jarring**: a card that grows or shrinks by a line eases instead of jumping.

## When not to use

- **Hundreds of rows**: each block measures and animates when it resolves. Use `Skeleton` rows in a virtualized list.
- **Loading that takes more than a few seconds**: show progress with `Progress` so people know how far it is.
- **A layout that is unknown in advance**: a skeleton that does not match what arrives is worse than none. Use
  `Skeleton` or an `EmptyState`.
- **A single placeholder without the morph**: use `Skeleton`.

## Usage

Put the loaded layout in a `SkeletonMorph` and wrap each visible piece in a `MorphBlock`. Bind `IsLoading` to your
request. Give text blocks the text's own line height and number of lines, and set the skeleton width near the typical
width of the text. Add the `card` style class to make the region itself the card: its border then follows the eased
height.

```xml
<arc:SkeletonMorph Classes="card" IsLoading="{Binding IsLoading}" LoadingLabel="Loading profile">
    <StackPanel Spacing="16">
        <StackPanel Orientation="Horizontal" Spacing="12">
            <arc:MorphBlock IsCircle="True" SkeletonWidth="40">
                <arc:Avatar DisplayName="{Binding Name}" />
            </arc:MorphBlock>
            <arc:MorphBlock SkeletonWidth="120" Lines="1" LineHeight="20">
                <TextBlock Text="{Binding Name}" FontSize="16" LineHeight="20" />
            </arc:MorphBlock>
        </StackPanel>
        <arc:MorphBlock Lines="3" LineHeight="20">
            <TextBlock Text="{Binding Bio}" FontSize="14" LineHeight="20" TextWrapping="Wrap" />
        </arc:MorphBlock>
    </StackPanel>
</arc:SkeletonMorph>
```

Both types live in the Special package: register it with `UseArcSpecial()` next to `UseArcControls()`.
`SkeletonMorph` and `MorphBlock` derive from Avalonia's `ContentControl`.

The preview above rebuilds a profile card and a list of transfers. Both load once when the page opens (1.2 s), and
Reload loads them again for 1.5 s. Press Reload while the blocks are still resolving: every block returns to its
skeleton at once, while each card eases back to its loading height. The Follow toggle and the request timer belong to
the demo; the people, amounts and dates are invented.

### Driving it

`IsLoading` is two way, so a switch or a view model can drive it. `Resolved` is raised when the last block has resolved
and its skeleton has faded; `IsResolving` is true in between.

<Demo name="Controlled" />

```csharp
private async Task ShowAsync(Func<Task<object>> load)
{
    profile.IsLoading = true;
    DataContext = await load();
    profile.IsLoading = false; // the blocks resolve, then Resolved is raised
}
```

A block shows nothing of its content while it loads: the content is not in the visual tree, so it cannot take focus and
screen readers cannot reach half loaded text. Bound content can be empty until the data arrives.

### Skeleton shapes

A block is a filled shape unless `Lines` is set.

- **Shapes**: `SkeletonWidth` and `SkeletonHeight` in pixels with `SkeletonRadius` corners (6 by default). `IsCircle`
  makes a round avatar placeholder; with a width and no height it is a circle of that width.
- **Text**: `Lines` bars, each centred in a line box `LineHeight` tall (20 by default). A bar is 12 px tall, or 6 px less
  than the line when the line is shorter than 18 px. The last bar of several is 62 % of the width.
- **Width**: leave `SkeletonWidth` unset to fill the available width; `SkeletonWidthRatio` draws a filling skeleton at a
  fraction of it (`0.6` for 60 %). A filling block also takes the full width once loaded.
- **Height**: without `SkeletonHeight`, a text block is `Lines` × `LineHeight` tall and any other block 12 px.

<Demo name="Shapes" />

Blocks are left and top aligned by default, like the text they stand for. Use `HorizontalAlignment="Right"` for a
right-aligned figure such as an amount: the skeleton then stretches from the block's new left edge.

### Stagger

Blocks resolve in reading order: the order of the visual tree, depth first. `Stagger` sets the time between two blocks
(40 ms by default); `0` resolves every block in the same frame. Blocks of a nested `SkeletonMorph` belong to that region
and follow its `IsLoading`.

<Demo name="Stagger" />

### Reduced motion

With reduced motion the skeletons keep their opacity pulse (so they still read as loading), but nothing stretches or
blurs, and every block crossfades to its content in 120 ms at the same time. The height of the region changes at once.
With motion off the skeletons do not pulse.

<Demo name="ReducedMotion" />

### Anatomy

1. **Region**: a `ContentControl` that holds the loaded layout. With the `card` class it has a surface background, a
   1 px border, 26 px corners and 20 px padding.
2. **Shell**: the part around the content whose height follows the content on a spring. It clips only while it moves.
3. **Status**: a hidden live region that says `LoadingLabel` while the region loads.
4. **Block**: one `MorphBlock` per visible piece: its content, and the skeleton drawn over it.
5. **Skeleton**: a filled shape or one rounded bar per text line, in the muted surface color. It may overflow its block
   while it stretches.

### States

| State | Region | Blocks |
|---|---|---|
| Loading | `:loading`; the status says the label | Skeletons at their own size, breathing together; no content |
| Resolving | `:resolving`; `IsResolving` is true | One after another: content mounts, the skeleton stretches and fades |
| Loaded | No pseudo-class | Content only; `IsResolved` is true |
| Back to loading | `:loading` again | Every block is a skeleton in the same frame; the height still eases |

### Content guidelines

- Size skeletons to the real content. Give text an explicit line height and pass the same value to `LineHeight`; set
  `SkeletonWidth` near the typical width, not the longest.
- Wrap every visible piece. A piece outside a block changes without animation and appears before its neighbours.
- Write `LoadingLabel` as what is loading, in a few words: "Loading profile", "Loading transfers".
- Keep the loaded layout the same shape as the skeleton. Different line counts are fine; a different layout is not.

### Best practices

- Put the card styles on the region (the `card` class, or your own `Background`, `BorderBrush`, `CornerRadius` and
  `Padding`) so the border follows the eased height.
- Keep the region's height free: it is top aligned by default, because a stretched region would hide the ease.
- Reload by setting `IsLoading` back to true. It is immediate, so fast reloads never leave a block halfway.
- Do not disable the button that triggers a reload while the region loads: ignore its presses instead, so keyboard focus
  stays where it is.

### Common questions

**Why does the card change height after loading?** The content has a different size from its skeleton, for example a bio
that needs two lines instead of three. The region eases to the new height on the same spring as the blocks.

**Can I use percentages for widths?** Leave `SkeletonWidth` unset and set `SkeletonWidthRatio`.

**What happens to bindings inside a block while it loads?** The content object exists but is not shown; its template and
bindings are applied when the block resolves, so they read the data that has arrived.

## API reference

<!-- api: SkeletonMorph -->

<!-- api: MorphBlock -->

## Accessibility

- While loading, the region's status reads `LoadingLabel` through a polite live region and its item status carries the
  same text. Both are cleared when loading ends.
- Skeletons are drawn, not controls: screen readers do not see them.
- Content is not in the visual tree until its block resolves, so nothing half loaded can be focused or read.
- Blocks keep reading order, so the focus order is the same before and after loading. The region and the blocks are not
  focusable themselves.

### Keyboard

The region adds no keys. Controls inside blocks keep their own keyboard behaviour once they are shown; while loading
they cannot be reached with `Tab`.

## Motion

- Skeletons breathe together while loading: opacity from 1 to 0.55 over 1.4 s on an ease-in-out curve, and back.
- Blocks resolve in reading order, 40 ms apart. The skeleton stretches from its own box to the content's measured box on
  a critically damped spring (the smooth spring, about 0.5 s, no overshoot) and fades over 380 ms after a 50 ms pause.
  The content fades in over 330 ms while it sharpens out of a 4 px blur.
- The region eases to its new height on the same spring and clips while it moves.
- Going back to loading is immediate for the blocks; the height still eases.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Pulse | 1 ↔ 0.55, 1.4 s | 1 ↔ 0.55, 1.4 s | None |
| Resolve order | 40 ms apart | All at once | All at once |
| Skeleton to content | Stretch on a spring and fade | 120 ms crossfade | Instant |
| Content | Fade in from a 4 px blur | 120 ms fade | Instant |
| Region height | Spring | Instant | Instant |
| Back to loading | Instant (height springs) | Instant | Instant |

## Responsive behavior

- Blocks size to their content once loaded, so the layout reflows at any width.
- A filling skeleton follows the available width; `SkeletonWidthRatio` keeps it at a fraction of it.
- When the width changes, the region's height follows at once; only changes of content ease.

## Related

- `Skeleton` for a simple placeholder without the morph.
- `MorphLoader` for a small loader that folds into a check or a cross.
- `EmptyState` for a useful next step when there is nothing to show.

| Component | Choose it when |
|---|---|
| `SkeletonMorph` | The layout is known and should settle into the content |
| `Skeleton` | You need a placeholder, or long virtualized lists |
| `MorphLoader` | A compact indicator is enough |
| `Progress` | Loading takes long enough to show how far it is |
