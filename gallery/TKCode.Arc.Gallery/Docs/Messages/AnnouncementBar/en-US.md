---
title: Announcement bar
summary: A top banner that rotates messages, counts down, and collapses smoothly when dismissed.
---

<Demo name="RotatingMessages" />

## When to use

- Site-wide notices at the top of a page, such as a sale, a release or a maintenance window.
- Two to four short announcements that rotate in one bar.
- A promotion with a live countdown.

## When not to use

- A message about one area or form on the page: use `Alert`.
- Feedback about something the visitor just did: use `Toast` or `ToastStack`.
- A consent choice for cookies or tracking: use `CookieConsent`.
- A one-off message that the user must act on before continuing: use `Dialog`.

## Usage

Add announcements to `Messages`. The bar shows the first one and rotates through the rest every `Interval`, which
is 6 seconds by default.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:AnnouncementBar>
        <arc:AnnouncementBar.Messages>
            <arc:Announcement Id="launch"
                              Message="Arc 2.0 is here, with 40 new components." />
            <arc:Announcement Id="webinar"
                              Message="Live walkthrough on Thursday at 17:00 UTC." />
            <arc:Announcement Id="status"
                              Message="All systems operational." />
        </arc:AnnouncementBar.Messages>
    </arc:AnnouncementBar>
</UserControl>
```

### Actions and countdown

An `Announcement` can have an `Action` button with a `Label` and an `Href`, and a `Countdown` that counts down to
`To`. Use `Command` on the action instead of `Href` when the action should run code.

```xml
<arc:AnnouncementBar>
    <arc:AnnouncementBar.Messages>
        <arc:Announcement Id="sale"
                          Message="Spring sale: 30% off annual plans.">
            <arc:Announcement.Action>
                <arc:AnnouncementAction Label="Upgrade" Href="https://example.com/upgrade" />
            </arc:Announcement.Action>
            <arc:Announcement.Countdown>
                <arc:AnnouncementCountdown To="2026-11-01T00:00:00+00:00" Label="Ends in" />
            </arc:Announcement.Countdown>
        </arc:Announcement>
    </arc:AnnouncementBar.Messages>
</arc:AnnouncementBar>
```

```csharp
bar.ActionInvoked += (_, e) => Analytics.Track("announcement.action", e.Announcement.Id);
bar.CountdownEnded += (_, e) => bar.Messages.Remove(e.Announcement);
```

### Controls and autoplay

Set `IsNavigationVisible` to show the previous and next buttons and the pause ring. `IsAutoPlayEnabled` turns the
rotation off; `Next()` and `Previous()` move through the messages in code.

<Demo name="ControlsAndCountdown" />

```xml
<arc:AnnouncementBar IsNavigationVisible="True"
                     Interval="0:0:8"
                     IsAutoPlayEnabled="True" />
```

### Tone

`Inverted` uses the foreground color as the fill and the background color as the text. Use it for the most
important notices only.

<Demo name="Inverted" />

### Remembered dismissal

Set `DismissalKey` to remember a dismissal across runs. The key is saved in `Storage`. Clear the stored value with
`AnnouncementBar.ClearDismissal` to show the bar again. The dismissal is stored by `Storage`, which defaults to a
file in the application data folder.

<Demo name="RememberedDismissal" />

```xml
<arc:AnnouncementBar DismissalKey="spring-campaign" IsDismissible="True" />
```

```csharp
AnnouncementBar.ClearDismissal("spring-campaign");
bar.IsOpen = true;
```

## API reference

<!-- api: AnnouncementBar -->

<!-- api: Announcement -->

## Accessibility

- The bar is a region named by `Label`, which is `Announcements` by default.
- Each message has a name such as `1 of 3`, so a screen reader can tell where it is in the rotation.
- The rotation does not announce each change. The bar is announced when it is paused, so the user can read it.
- Tab reaches the action, the previous button, the pause ring, the next button and the close button in order.
  Enter and Space activate the focused control.
- The pause ring is a toggle. Its name changes between `Pause announcements` and `Resume announcements`.
- The rotation pauses while the pointer is over the bar, while focus is inside it, and while the window is minimized.

## Motion

- The next message rises from below with a light blur while the bar springs to fit its height.
- The pause ring fills over each interval. It stops where it is when paused and continues when resumed.
- Countdown digits drop in when they change.
- Dismissing collapses the bar and the page below moves up with it.
- Reduced motion stops the rotation and replaces messages and digits with a fade. A dismissal only fades out.
- Off motion shows every change immediately.

## Related

- `Alert` for a message inside a section or a form that stays until it is resolved.
- `ToastStack` for queued results of actions the user just took.
- `CookieConsent` for consent to cookies and tracking.
