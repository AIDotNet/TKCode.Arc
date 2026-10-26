---
title: Mobile pull to refresh
summary: Pull scrollable content down at its top to refresh it, with rubber-band resistance, a spinner and a spring back.
---

<Demo name="Basic" />

## When to use

`PullToRefresh` wraps a scrolling list or page so the person can ask for fresh data with the gesture they already know.

- **Feeds and inboxes**: messages, timelines and order lists that change on the server.
- **Any list with a reload action**: when a button would add clutter.

The live demo ends the refresh with a timer, as a request would, and runs inside a phone frame, a desktop simulation driven by the mouse. Touch behavior on a real device, and how it combines with the platform's own overscroll, has not been verified.

## When not to use

- **Content that is not scrollable and never reloads**: there is nothing to pull.
- **Updates that should arrive on their own**: refresh in the background and show the result.
- **A horizontal list**: the pull is vertical only.

## Usage

Put the scrolling content inside it. Pulling down while the content is at its top drags the content along with resistance, and an arrow that turns over as the pull reaches `PullThreshold` (64 px by default). Releasing past the threshold raises `RefreshRequested`; releasing early springs the content back.

```xml
<arcm:PullToRefresh Command="{Binding ReloadCommand}">
    <ScrollViewer>
        <!-- the list -->
    </ScrollViewer>
</arcm:PullToRefresh>
```

The refresh keeps going, with a spinner, until `IsRefreshing` is false again. Either bind `IsRefreshing` (it is two-way), or take a deferral from the event data and dispose it when the work is done.

```csharp
refresher.RefreshRequested += async (_, e) =>
{
    using var deferral = e.GetDeferral();
    await LoadAsync();
};
```

`IsRefreshing` can also be set from code to show the spinner without a pull. That does not raise `RefreshRequested`.

### Pass-through

A gesture that starts while the inner `ScrollViewer` is scrolled, goes up, or goes mostly sideways is left to the content, so scrolling a long list never fights the pull. Pulling is not available while a refresh is running.

### Tokens

`PullToRefreshToken` holds the geometry: `IndicatorExtent` (56, also where the content rests while refreshing), `ArrowSize` (20), `SpinnerSize` (22) and `SpinnerThickness` (2). Colors come from the ArcDesign tokens.

## API reference

<!-- api: MobilePullToRefresh -->

## Accessibility

The refresh is a gesture, so give the person another way to reload (a menu item or a button) where it matters. `RefreshRequested` and `Command` run when the pull is released, not while it is held.

## Motion

The content follows the finger with a rubber-band curve: it keeps up at first and resists more the further it goes. On release, the finger's speed is handed to a spring, so the content settles smoothly on the indicator or back at the top. A new pull catches the content in flight. The arrow turns over as the pull reaches the threshold, and a spinner replaces it while refreshing. Under reduced motion the content does not stretch with the finger; the refresh still works, and the content jumps.

## Related

- `MobileListGroup`: the usual content.
- `MobilePage`: holds the refreshed screen.
- `MobileNavbar`: the bar above it.
