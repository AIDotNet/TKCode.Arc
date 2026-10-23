---
title: Announcement bar
summary: A top banner that rotates messages, counts down, and collapses smoothly when dismissed.
---

<Demo name="RotatingMessages" />

## When to use

`AnnouncementBar` is a slim banner at the top of a page that speaks to every visitor, not to one section. It holds two
to four short messages, rotates through them, and can carry one call to action and a live countdown per message. When
it is dismissed, the page below moves up. It is the right place for news that applies to the whole site or app and
that the person can read at a glance.

- **Seasonal sale on a storefront or a SaaS pricing site**: "Spring sale: 30% off annual plans" with an `Upgrade`
  action and a countdown to the end of the offer, so the deadline is visible on every page.
- **Release news in an admin console**: "Arc 2.0 is here, with 40 new components" rotating with "Live walkthrough on
  Thursday at 17:00 UTC", each with its own link, so one bar carries the news of a release.
- **Scheduled maintenance on a status or operations dashboard**: an inverted bar with a "Starts in" countdown that
  stays visible until the window opens. It is the most important notice on the page, so it gets the strongest tone.
- **A campaign a returning customer should see once**: a bar with a `DismissalKey` that stays closed on the next visit
  and returns when the key changes for a new campaign.

## When not to use

- **A message about one section or form**, such as an expiring card on a billing page: use `Alert`, which lives in the
  page flow next to the content it concerns.
- **Feedback about something the visitor just did**, such as "Changes saved": use `Toast` or `ToastStack`.
- **A consent choice for cookies or tracking**: use `CookieConsent`, which asks a question and records the answer.
- **A decision the person must make before continuing**: use `Dialog`.
- **A large countdown on a page that is about the countdown**, such as a launch page or a checkout timer: use
  `Countdown`, which shows the time as the main content rather than as a line of a banner.

## Usage

Add announcements to `Messages`. The bar shows the first one and rotates through the rest every `Interval`, which
is 6 seconds by default.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
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

`AnnouncementBar` derives from Avalonia's `TemplatedControl`. Its messages are `Announcement` objects, which the bar
draws itself. It takes the width its container gives it, so put it in the first row of the window or page layout,
where it spans the full width. The `Id` of each `Announcement` identifies it in events and in your analytics, so keep
ids stable across releases.

### Anatomy

1. **Action** (`Announcement.Action`): the label of the call to action with an arrow after it. It inherits the bar's
   foreground color in medium weight, and the arrow moves 2 px toward the destination on hover.
2. **Message** (`Message`): the message text in the bar's font size (`TypeSM`) and foreground color, on a line height
   of 19.6 px. A message change rises into place inside a window that is at least 32 px tall.
3. **Countdown** (`Announcement.Countdown`): the remaining time in medium digits, with the optional `Label` before it
   and a 6 px gap. The colons and the day mark use the muted color, and the countdown is drawn as one control.
4. **Mirror column**: an empty column on the left that is as wide as the controls on the right (32 px per control and
   a 2 px gap between them). It keeps the message centered on the page. It is removed on narrow bars.
5. **Controls** (`IsNavigationVisible`, `IsDismissible`): previous (chevron up), the pause ring and next (chevron
   down) when navigation is visible and there is more than one message, and the dismiss button (X) when
   `IsDismissible` is `True`. Each is a 32 px round ghost button in the muted color. Hover fills it at 10% and a press
   at 12%.
6. **Bar**: a muted surface with a 1 px bottom border. The padding is 16 px on the left, 8 px on the right and 6 px
   above and below. On bars 560 px wide or narrower the left padding is 14 px, the column gap is 8 px instead of 12 px
   and the mirror column is removed, so the message starts at the left edge.

### Tones

`Tone` sets the look of the bar. `Neutral` (the default) is a muted surface with a bottom border and suits almost
every page. `Inverted` fills the bar with the foreground color and sets the text in the background color. Use it for
the one notice that must not be missed, and never on more than one bar on a page.

<Demo name="Inverted" />

In the inverted tone the secondary text is the background color at 66% and the hover fill is the background color at
10%, so the controls stay readable on the filled bar.

### Controls and autoplay

Set `IsNavigationVisible` to show the previous and next buttons and the pause ring. `IsAutoPlayEnabled` turns the
rotation off. `Next()` and `Previous()` move through the messages in code, and both wrap around at the ends.

<Demo name="ControlsAndCountdown" />

```xml
<arc:AnnouncementBar IsNavigationVisible="True"
                     Interval="0:0:8"
                     IsAutoPlayEnabled="True" />
```

Rotation runs only when all three conditions hold: there is more than one message, `IsAutoPlayEnabled` is `True`, and
the motion level of the bar is full. Under reduced or off motion the bar stays on its first message, so give
people a way to read the others: `IsNavigationVisible` with the previous and next buttons, or your own buttons that call
`Next()` and `Previous()`. The pause ring fills over each `Interval`. Pressing it sets `IsPaused` and stops the clock
where it is. Pressing it again resumes and finishes the remaining part of the interval.

`SelectedIndex` is two-way and reports the visible message. Use `SelectedIndexChanged` to log which message was
shown, for example to measure which announcement people reach.

### Actions and countdown

An `Announcement` can have an `Action` button with a `Label` and an `Href`, and a `Countdown` that counts down to
`To`. Use `Command` on the action instead of `Href` when the action should run code. When the action has both, the
command runs first and then the link opens. `ActionInvoked` is raised after either, with the message and its index.

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
public static class Analytics
{
    public static void Track(string name, string id) { }
}

bar.ActionInvoked += (_, e) => Analytics.Track("announcement.action", e.Announcement.Id);
bar.CountdownEnded += (_, e) => bar.Messages.Remove(e.Announcement);
```

The countdown shows days while a whole day or more remains, then hours, minutes and seconds, and its digits change on
the whole second. It stops at zero and shows zeros. `CountdownEnded` is raised once, when the countdown reaches zero
or when its message is shown after the end time has passed. Remove the message in that handler, as above, so the
bar does not keep an expired offer.

### Binding to a view model

The bar is a list of data with state, so bind it to a collection the view model owns. Use `ItemsSource` for the
messages; it overrides `Messages` when it is set. Bind `IsOpen` and `SelectedIndex` two-way only when the view model
needs to know that the person closed the bar or moved through the messages.

```csharp
public partial class StorefrontViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<Announcement> _announcements = new()
    {
        new Announcement { Id = "sale", Message = "Spring sale: 30% off annual plans." },
        new Announcement { Id = "webinar", Message = "Live walkthrough on Thursday at 17:00 UTC." }
    };

    [ObservableProperty]
    private bool _isBannerOpen = true;

    [ObservableProperty]
    private int _bannerIndex;
}
```

```xml
<arc:AnnouncementBar ItemsSource="{Binding Announcements}"
                     IsOpen="{Binding IsBannerOpen, Mode=TwoWay}"
                     SelectedIndex="{Binding BannerIndex, Mode=TwoWay}"
                     IsNavigationVisible="True" />
```

When the collection changes, the bar keeps the selected index (wrapped to the new count) and restarts the rotation
timer. Replacing the whole collection is therefore safe, and adding or removing a message does not need a separate
refresh.

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

Choose a key that names the campaign, not the page, and change it when a new campaign starts, so every visitor sees the
new message once. Dismissing writes the key to storage at once, and `Closed` is raised at once too, before the bar has
finished collapsing.

### Example: spring campaign on a SaaS site

A marketing page for Northwind Analytics shows the spring offer above the pricing table. The bar carries the offer and
its deadline as a countdown, a link to the upgrade page, and the previous and next controls, so a visitor can read the
other notice without waiting. Autoplay is off because the offer is the message to read, not a slideshow. The page
content below stands in for the pricing table, so the bar's collapse moves it up as it would on the live site.

![A spring sale bar with a countdown, an upgrade link and navigation above a pricing table](images/SaleBanner.png)

```xml
<Border Width="720"
        CornerRadius="12"
        BorderThickness="1"
        ClipToBounds="True"
        Background="{arc:ArcDesignTokenResource Background}"
        BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}">
    <StackPanel>
        <arc:AnnouncementBar IsNavigationVisible="True"
                             IsAutoPlayEnabled="False">
            <arc:AnnouncementBar.Messages>
                <arc:Announcement Id="sale" Message="Spring sale: 30% off annual plans.">
                    <arc:Announcement.Countdown>
                        <arc:AnnouncementCountdown To="2026-11-01T00:00:00+00:00" Label="Ends in" />
                    </arc:Announcement.Countdown>
                    <arc:Announcement.Action>
                        <arc:AnnouncementAction Label="Upgrade" />
                    </arc:Announcement.Action>
                </arc:Announcement>
                <arc:Announcement Id="annual"
                                  Message="Annual plans include the Pro reports and 10 seats." />
                <arc:Announcement Id="status" Message="All systems operational." />
            </arc:AnnouncementBar.Messages>
        </arc:AnnouncementBar>
        <StackPanel Margin="24,20" Spacing="6">
            <TextBlock Text="Pricing" FontSize="22" FontWeight="Medium"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Pro is $24 per seat per month, billed annually."
                       FontSize="14"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        </StackPanel>
    </StackPanel>
</Border>
```

### Content guidelines

- **Message**: one sentence that states the news and, if needed, the date or the number: "Spring sale: 30% off annual
  plans." Keep it under about 70 characters so it fits one line on a narrow window.
- **Action label**: a verb and an object in sentence case: "See what's new", "Save a seat". Avoid "Click here".
- **Countdown label**: a short phrase before the time: "Ends in", "Starts in". The label is spoken before the time.
- **Tone of voice**: direct and factual. Give the date or the deadline instead of urgency words, and avoid exclamation
  marks.

| Instead of | Write |
|---|---|
| Huge sale!!! | Spring sale: 30% off annual plans. |
| Click here | Save a seat |
| New! Read more | Arc 2.0 is here, with 40 new components. |
| Maintenance soon | Scheduled maintenance tonight at 22:00 UTC. |

### Best practices

**Do**

- Keep two to four messages in one bar. Each message should be readable in one interval, which is about 6 seconds.
- Put the most important message first, because the bar shows the first one when it loads.
- Give each message a stable `Id`. Events report it, so analytics can follow one message across rotations and releases.
- Remove a message from `Messages` when its countdown ends, so the bar does not keep an expired offer.

**Avoid**

- Long messages. A message that wraps to several lines pushes the page down and the bar stops looking like a banner.
- More than one inverted bar on a page. Two strong bars compete, and neither reads as the important one.
- Using the bar for one section of the page. The bar speaks to every visitor, so a notice about one form belongs in
  `Alert`.
- Relying on rotation alone. Under reduced motion the bar stops on the current message, so offer the navigation
  controls or a link to the full list.

### Common questions

**Why is `IsOpen` still `True` after a visitor dismissed the bar?** A remembered dismissal hides the bar without
changing `IsOpen`, so the bar can tell the difference between a dismissal from this session and a remembered one.
Setting `IsOpen` to `True` does not bring it back. Call `AnnouncementBar.ClearDismissal` with the key, then set
`DismissalKey` again (for example to `null` and back) so the bar reads the storage again.

**Why does the bar not rotate in my app?** Rotation needs more than one message, `IsAutoPlayEnabled` set to `True`, and
full motion. Under reduced or off motion the bar stops on the current message by design. Add the navigation controls or
call `Next()` from a timer of your own if you need to keep rotating.

**Where is the dismissal stored?** In `Storage` when you set one on the bar. Otherwise it goes to
`MessageStorage.Default`, which writes a small file in the local application data folder. Set
`MessageStorage.Default` once at startup to keep it in your settings service, or to a memory store in tests.

**How do I read the message the person is on?** Use `Current`, which is the visible `Announcement`, or `Count` for the
number of messages. `SelectedIndex` gives the position, starting at zero.

## API reference

<!-- api: AnnouncementBar -->

<!-- api: Announcement -->

Companion types used by an `Announcement`:

| Type | Properties |
|---|---|
| `AnnouncementAction` | `Label` (the text), `Href` (`Uri`, opened with the system launcher), `Command` (`ICommand`, run on invoke), `CommandParameter` |
| `AnnouncementCountdown` | `To` (`DateTimeOffset`, when the countdown reaches zero), `Label` (text before the time, optional) |

## Accessibility

- The bar is a region named by `Label`, which is `Announcements` by default.
- Each message has a name such as `1 of 3`, so a screen reader can tell where it is in the rotation.
- The rotation does not announce each change. The bar is announced when it is paused, so the user can read it.
- Tab reaches the action, the previous button, the pause ring, the next button and the close button in order.
  Enter and Space activate the focused control.
- The pause ring is a toggle. Its name changes between `Pause announcements` and `Resume announcements`.
- The rotation pauses while the pointer is over the bar, while focus is inside it, and while the window is minimized.
  Only a mouse pointer pauses it. A touch screen does not, so provide the pause ring for touch users.
- The countdown is not announced as it ticks. Its accessible name spells out the remaining days, hours and minutes.
- The previous, next and close buttons are named `Previous announcement`, `Next announcement` and `Dismiss`.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Moves through the action, previous, pause ring, next and dismiss controls, in that order. Hidden controls are skipped |
| `Enter` / `Space` | Runs the focused action, presses the focused button, or toggles the pause ring |

The bar itself does not take focus, so `Tab` goes straight to its controls. Focus inside the bar pauses the rotation,
so a keyboard user can read a message without it changing under them.

## Motion

- The next message rises from below with a light blur while the bar springs to fit its height.
- The pause ring fills over each interval. It stops where it is when paused and continues when resumed.
- Countdown digits drop in when they change.
- Dismissing collapses the bar and the page below moves up with it.
- Reduced motion stops the rotation and replaces messages and digits with a fade. A dismissal only fades out.
- Off motion shows every change immediately.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Automatic rotation | Advances every `Interval`; the next message rises from below | Stops; the bar stays on the current message | Stops |
| Manual change (`Next`, `Previous`) | The message rises or drops with a blur and the height springs to fit | The message fades | Immediate |
| Pause ring | Visible while rotating; fills over the interval and stops when paused | Hidden | Hidden |
| Countdown digit change | The new digit drops in and the old one falls away | The digit fades | Immediate |
| Dismissal | The height collapses on a smooth spring and the page below moves up | The bar fades out without collapsing | Immediate |

## Related

- `Alert` for a message inside a section or a form that stays until it is resolved.
- `ToastStack` for queued results of actions the user just took.
- `CookieConsent` for consent to cookies and tracking.

| Component | Choose it when |
|---|---|
| `Alert` | The notice belongs to one section or form and stays until it is resolved |
| `Toast` | A finished action needs a short confirmation that closes by itself |
| `ToastStack` | Results, errors or undo actions from the app must stack at the edge of the window |
| `CookieConsent` | The notice asks visitors for a choice about cookies or tracking |
| `Countdown` | The countdown is the main content of the page, not a line of a banner |
| `Dialog` | The person must answer before the page continues |
