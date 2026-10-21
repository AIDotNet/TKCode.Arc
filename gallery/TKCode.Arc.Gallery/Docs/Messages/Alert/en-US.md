---
title: Alert
summary: A persistent message that helps people recover or continue.
---

<Demo name="Tones" />

## When to use

- Inline notices on a page or in a form, such as an expiring card or a missing domain verification.
- A status that changes over time. Keep one `Alert` and change its tone, title and content.
- A notice the user can close, after which the space below it closes too.

## When not to use

- A short confirmation of a finished action: use `Toast`.
- Application notifications with types, actions and stacking: use `ToastStack`.
- A message that must be answered before the user continues: use `Dialog`.
- A page or list with no content to show: use `EmptyState`.

## Usage

Basic usage sets a `Title` and `Content`. The default `Tone` is `Info`.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Alert Title="Your card expires next month"
               Content="Update your payment method to keep your plan active." />
</UserControl>
```

### Tones

`Tone` sets the icon and its color. `Danger` is announced assertively, the other tones politely.

```xml
<StackPanel Spacing="12">
    <arc:Alert Tone="Success" Title="Domain verified"
               Content="Emails from acme.dev will now be delivered." />
    <arc:Alert Tone="Warning" Title="You're close to your storage limit"
               Content="Free up space or upgrade before uploads pause." />
    <arc:Alert Tone="Danger" Title="Payment failed"
               Content="We couldn't charge the card ending in 4242." />
</StackPanel>
```

### Morph in place

Keep one instance and change its properties when the state changes. The icon swaps, the new text rises in and the
height follows with a spring. Replacing the control instead skips these transitions.

<Demo name="MorphInPlace" />

### Dismiss

Set `IsDismissible` to show the close button. Pressing it collapses the alert, then raises `Dismissed`. Setting
`IsOpen` back to `True` shows the alert again.

<Demo name="Dismiss" />

```csharp
var notice = new Alert { Tone = AlertTone.Warning, IsDismissible = true, Title = "Scheduled maintenance on Sunday" };
notice.Dismissed += (_, _) => SaveDismissedState();

// Show the alert again later.
notice.IsOpen = true;
```

### Custom content

Any non-text `Content` is shown below the title. Text content gets the detail styling; other content is shown as
it is.

<Demo name="CustomContent" />

## API reference

<!-- api: Alert -->

## Accessibility

- `Title` names the alert and `Content` is read as its help text. A changed title is announced again.
- `Danger` alerts are announced assertively; the other tones are announced politely.
- The close button is named `Dismiss: {Title}`, so the user hears which alert it closes.
- The tone icon is not exposed to assistive technology. The tone is conveyed by the title and content.
- The close button is a regular button: Tab reaches it and Enter or Space activates it.

## Motion

- Showing and hiding the alert opens and closes its height on a smooth spring, and the content below closes up.
- A tone change swaps the icon with a short scale and blur. Changed text rises in and unblurs.
- Reduced motion removes the height animation and the movement. Text and icon only fade, and showing or hiding is
  immediate.
- Off motion jumps straight to the end state.

## Related

- `Toast` for a brief confirmation of an action that has finished.
- `ToastStack` for application notifications that are queued, updated and stacked.
- `AnnouncementBar` for a rotating banner across the top of a site.
- `EmptyState` when there is no content to show at all.
