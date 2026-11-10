---
title: AI feature launch
summary: An in-product announcement of a new AI feature: a short looping demo you can pause, what it does, a try button and a dismiss that remembers why.
---

<Demo name="Preview" />

## When to use

`AiFeatureLaunch` tells people inside the product that a new AI feature is ready and invites them to try it. A short
looping demo on top shows the feature at work, a "New" badge and the feature's name lead the text, one or two sentences
say what it does, a few highlights say what to expect, and a try button starts it right away. People who are not ready
press "Maybe later" or the close button; `Dismissed` reports which, so the app can decide whether to show it again.

- **Launching a feature.** Show it once on the screen where the feature lives: a summary button in a thread, a writing
  helper in an editor.
- **Turning a quiet feature on.** When a feature exists but few people find it, show it near the place it helps.
- **Release notes in context.** One card per feature instead of a long list nobody reads.

## When not to use

- **Several features at once.** Show the most useful one; a carousel of launches is easy to ignore.
- **Warnings and outages.** Use `Alert` or `AnnouncementBar`; this card is an invitation, not a notice.
- **First-run setup.** Use a step-by-step flow such as `AI setup wizard` or `Multi-step form`.

## Usage

The block derives from Avalonia's `TemplatedControl`. Without any data it shows an invented sample
(`AiFeatureLaunchSamples`).

```xml
<arc:AiFeatureLaunch Title="Thread summaries"
                     Description="Catch up on any long thread in seconds."
                     TryCommand="{Binding OpenSummaries}" />
```

```csharp
launch.Highlights = ["Decisions, owners and open questions", "Links back to every message"];
launch.Dismissed += (_, e) => settings.MarkSeen("thread-summaries", e.Reason);
```

### Anatomy

- **Demo**: the built-in loop (a page of message lines read by a soft band and turned into a three-point summary), or your
  own content in `Demo` (an image, a video, any control). `DemoDescription` is what screen readers hear in its place.
  `IsDemoVisible` hides the area.
- **Pause and close**: the pause button on the demo's corner flips `IsDemoPlaying`; the close button dismisses with the
  reason `Closed`.
- **Text**: the "New" badge, `Title`, `Description` and `Highlights`.
- **Buttons**: "Maybe later" (reason `Later`, hidden with `IsLaterVisible="False"`) and the try button (`TryRequested`,
  `TryCommand`; with `DismissOnTry` it also closes with the reason `Tried`).

### Remembering the answer

`IsOpen` (two-way) says whether the card shows. `Dismiss(reason)` closes it from code and `Show()` brings it back.
Setting `IsOpen` directly closes it without raising `Dismissed`.

<Demo name="Remembered" />

### Words

`Labels` (`AiFeatureLaunchLabels`) holds the badge, the button texts and the screen reader names of the pause, play and
close buttons and of the highlights list.

## API reference

<!-- api: AiFeatureLaunch -->

<!-- api: AiFeatureLaunchDismissReason -->

<!-- api: AiFeatureLaunchLabels -->

## Accessibility

- The card is a named region; the title is a level 2 heading. The highlights form a named list of items.
- The demo is one image named by `DemoDescription`; its drawing is hidden from screen readers.
- The pause button is named "Pause the demo" or "Play the demo" by state; the close button is named "Dismiss".
- **Escape** anywhere in the card dismisses it (reason `Closed`). **Tab** reaches the pause, close, later and try buttons in
  reading order.

## Motion

- The demo loops every 6.4 seconds: the lines write in, a soft band reads them from top to bottom, the summary card rises
  and its points grow, then the scene fades and starts again. It runs only under full motion, while visible and in view,
  and allocates nothing per frame.
- Paused, the demo holds its frame. Under reduced motion or Off it shows the finished scene and never moves.
- Closing scales the card down slightly while it fades and its height folds away; showing it again reverses that. Reduced
  motion keeps only the fade; Off closes it at once.

## Responsive behavior

- The card is at most 420 wide and fills narrower containers. At 360 or less (`:compact`) the text padding tightens and the
  two buttons stack at full width with the try button on top.
- The demo keeps its height (184) and draws its scene to the width it gets; the title wraps under the badge when needed.
- Nothing depends on hover.

## Related

- `AnnouncementBar` for a one-line notice across the page.
- `Card` and `Badge`, the pieces this card is made of.
- `AI setup wizard`, for turning the feature on for a whole workspace.
