---
title: Mobile toast
summary: A transient message that slides up near the bottom, leaves by itself and can be swiped away.
---

<Demo name="Basic" />

## When to use

`Toast` tells the person that something happened without asking for anything: a message was archived, a link was copied, a reminder was set.

- **Confirmation of a finished action** that needs no answer.
- **A light error or status** that the person can ignore.

The live demo runs inside a phone frame, a desktop simulation; touch feedback and the swipe on a real device have not been verified here.

## When not to use

- **A decision**: use `Dialog`.
- **A message that must not be missed** or that the person has to act on: a toast leaves by itself after a few seconds.
- **Long text**: a toast is one or two lines.

## Usage

Put one `Toast` in the page's `Overlay` slot and call `Show` whenever there is something to say.

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Overlay>
        <arcm:Toast Name="Toasts" Policy="Stack" MaxVisible="3" />
    </arcm:Page.Overlay>
</arcm:Page>
```

```csharp
Toasts.Show("Message archived");
Toasts.Show("Reminder set", TimeSpan.FromSeconds(5), ArcIcons.Bell);
Toasts.Show("Offline", TimeSpan.Zero); // stays until swiped away or DismissAll()
```

A toast stays for `DefaultDuration` (3 s) unless `Show` is given a duration. A finger on a toast holds its countdown and releasing it carries on from where it stopped; a swipe to either side past 72 px, or a quick flick, sends it away.

`Policy` decides what happens when a toast arrives while others show. `Stack` keeps the newest `MaxVisible` toasts, newest at the bottom, and sends the oldest away. `Replace` shows one at a time: the new toast replaces the one showing.

The toasts sit `BottomOffset` (16) above the page's footer and bottom safe area: the toast finds its `Page` and adds the footer's height and `SafeArea.Bottom` itself, so it clears a `Tabbar`. The rest of the overlay does not catch touches, so the page stays usable while a toast shows.

### Several overlays on one page

`Page.Overlay` holds one element. To use a toast with a sheet or a dialog, put them in a `Grid` in that slot with the toast last, so it shows above the others.

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Overlay>
        <Grid>
            <arcm:Sheet Title="Share" />
            <arcm:Dialog Title="Delete?" />
            <arcm:Toast Name="Toasts" />
        </Grid>
    </arcm:Page.Overlay>
</arcm:Page>
```

### Tokens

`ToastToken` holds `BodyMinHeight` (44), `BodyMaxWidth` (360), `BodyPadding`, `StackGap` (8), `SideInset` (16) and `GlyphSize` (18). The toast is an inverted pill: the Foreground color as its fill and the Background color as its text.

## API reference

<!-- api: MobileToast -->

## Accessibility

The message is announced politely, without taking the focus. Because a toast leaves by itself, never put the only copy of important information in one, and keep the text short. The swipe is a shortcut: a toast also leaves on its own. The toast is at least 44 high so a finger can hold it.

## Motion

A toast rises 16 px and fades in on a snappy spring, and leaves faster, in 0.16 s with no overshoot, so the next toast never waits for the last one. Presence and the swipe offset are code-driven motion values, because a swipe writes the offset on every move and a release hands the finger's velocity to a spring that brings an unfinished swipe back. The countdown is a pausable timeline, so it ends on time even while no frames are drawn. With reduced motion a toast only fades, in 0.12 s, and a swipe decides on release instead of following the finger; with motion off it appears and disappears at once.

## Related

- `MobilePage`: the `Overlay` slot holds the toast, and its footer and safe area decide where the toast sits.
- `MobileDialog`: for a decision.
- `MobileSheet`: for choices and forms.
- `MobileTabbar`: the toast rises above it.
