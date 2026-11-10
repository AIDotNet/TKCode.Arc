---
title: Scroll story
summary: A story told as the page scrolls: steps of text pass a reading line while a pinned stage beside them changes its picture to the current step.
---

<Demo name="Preview" />

## When to use

`ScrollStory` explains a process as the reader scrolls. Steps of text sit in one column, each in a tall slot, and a stage
in the other column stays in view: as the section passes through the window, the stage is held in the middle of what is
visible and stops at the section's top and bottom. The step that crosses the reading line (a little above the middle of
the window) becomes the active one: its text comes up to full strength while the others dim, and the stage cross-fades
to that step's picture. The pictures are built-in drawings of a small app window (an inbox, a board, a document under
review, a rising chart) or images of your own.

- **A "how it works" section** of a landing page with three to six steps.
- **A product tour** that pairs each feature with a picture.
- **A story page** where reading order matters.

## When not to use

- **Content the reader should compare side by side**: use `FeatureBento` or `SpotlightGrid`.
- **A page that does not scroll**: the stage only moves when an ancestor scrolls.
- **A long article**: use `Markdown`; the slots here are tall on purpose.

## Usage

```xml
<arc:ScrollStory Title="From request to release">
    <arc:ScrollStoryStep Kicker="Capture" Title="Every request lands in one inbox" Scene="Capture" Body="Forms, email and chat feed the same list." />
    <arc:ScrollStoryStep Kicker="Launch" Title="Ship it and watch it land" Scene="Launch" Body="Release and follow adoption." />
</arc:ScrollStory>
```

Put the block in a page that scrolls (any ancestor `ScrollViewer`). With no steps it tells an invented story of four;
set `ShowsSampleSteps` to false to leave it empty. A step's `Scene` picks a built-in drawing (`Capture`, `Arrange`,
`Review`, `Launch`) and its `Image` replaces it. `Kicker` defaults to the step number.

`ActiveIndex` reports the step on the stage and `ActiveStepChanged` fires when it changes. `ScrollToStep(index)` scrolls
a step into view.

## API reference

<!-- api: ScrollStory -->

## Accessibility

- The section is named by its title, a level 2 heading.
- Each step is focusable and named by its title, with the body as help text. Focusing a step scrolls it into view and
  makes it active, so the stage follows the keyboard too.
- The stage is named after the active step; the drawings themselves are decorative.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Next or previous step. |
| `Down` / `Up` | Next or previous step. |
| `Home` / `End` | First or last step. |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Stage follows the scroll | Moves with the page, pinned in view (no animation of its own) | Same | Same |
| Picture changes | Cross-fade 240 ms, the new picture rises 10 px | Cross-fade | Immediate |
| Steps dim and brighten | Opacity over 240 ms | 160 ms | Immediate |

Pinning follows the scroll position directly: it is a render transform updated as the viewport moves, never a layout pass.

## Responsive behavior

- Below 760 of the block's own width (`:compact`) the stage is hidden and the steps stack in one column, each with its
  own 200-high picture under the text, fully visible.
- On wide screens the two columns share the width, 56 apart, and each step slot is at least 380 tall so steps pass the
  reading line one at a time.

## Related

- `FeatureBento` and `SpotlightGrid` for features shown together.
- `Stepper` for a process the user moves through in an app.
