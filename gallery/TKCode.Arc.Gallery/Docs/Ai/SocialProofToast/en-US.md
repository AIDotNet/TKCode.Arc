---
title: Social proof toast
summary: A stack of social proof notifications for landing and pricing pages, where recent trials, upgrades and verified purchases from people and companies arrive every few seconds; it pauses and fans out on hover, and visitors can dismiss any toast.
---

<Demo name="Preview" />

## When to use

`SocialProofToast` shows what other people are doing right now: a trial started, a team upgraded, a verified purchase.
A new toast rises in at the front every few seconds while the older ones step back behind it. Hovering the stack, or
moving keyboard focus into it, pauses the cycle and fans the toasts out so each one can be read or dismissed.

- **Landing and pricing pages** that show recent trials, purchases or upgrades.
- **Launch or waitlist pages** with a live feed of people joining and teams upgrading.
- **Near a checkout button**, to highlight verified purchases from known companies.

## When not to use

- **Feedback after the visitor's own action** (saved, copied, failed): use `Toast`.
- **Inside a signed in product** where the activity is not marketing: use an activity feed list.
- **When you have no real events to show.** Fabricated social proof erodes trust; a simulation is for previews only.

## Usage

Add the stack and give it your feed in `Events`, oldest first. It shows one toast every `Interval` (4 seconds by
default) after an `InitialDelay` (0.8 seconds), keeps up to `MaxVisible` toasts (3) and starts over at the end unless
`Loop` is false. With no events and no simulation (below) the stack stays empty until you push one.

```csharp
// One row of your billing backend's recent activity.
record Signup(string Id, string FirstName, string Action, string City, DateTimeOffset CreatedAt, bool IsPaid);

stack.Events.AddRange(recent.Select(r => new SocialProofEvent(r.Id, r.FirstName, r.Action)
{
    Meta = r.City,
    Time = r.CreatedAt,
    VerifiedLabel = r.IsPaid ? "Verified purchase" : null,
}));
```

### Your events

<Demo name="YourEvents" />

Each `SocialProofEvent` has a stable `Id`, a `Name` (a first name or a company, in medium weight) and an `Action` read
after it. `Meta` adds short context such as a city or a seat count. `Time` renders as relative time (Just now, 4 min
ago, 2 h ago); `TimeText` is shown as is. `IsVerified` or `VerifiedLabel` adds a check badge with a label.

People are drawn as an `Avatar` with their initials, or their `Portrait`. Set `Actor` to `Company` for a rounded square
with the first letter of the name, or with your own mark in `Logo` (a 24 × 24 stroked glyph drawn in the text color, so
it follows the theme).

Call `Push` to show an event the moment it happens, for example from a server-sent event. It goes to the front at once
and the countdown to the next toast starts over.

For a preview, leave `Events` empty and give the stack a `SocialProofToastSimulation`: its events, oldest first, each a
`SocialProofToastSimulationEvent` with the event and how long `Ago` it happened. Every pass measures the times back from
that moment, so the feed never drifts into "40 min ago". Your own `Events` set it aside, and setting `Simulation` to null
empties the stack. Every name, company and place in the previews here is invented.

```csharp
stack.Simulation = new SocialProofToastSimulation
{
    Events =
    [
        new SocialProofToastSimulationEvent { Event = new SocialProofEvent("s1", "Sofia", "started a Pro trial") { Meta = "Madrid" }, Ago = TimeSpan.FromMinutes(14) },
        new SocialProofToastSimulationEvent { Event = new SocialProofEvent("s2", "Mateo", "bought the Team plan") { Meta = "Lisbon" }, Ago = TimeSpan.FromMinutes(11) },
    ],
};
```

### Anatomy

1. **Stack**: toasts share one 64px slot. The front toast sits at the edge; each one behind it steps back 10px and
   shrinks 5%, with its text faded out.
2. **Mark**: a 36px round avatar for a person, a rounded square for a company.
3. **Sentence**: the name and the action on one line, truncated with an ellipsis.
4. **Second line**: the meta and the time, then the verified badge (an icon and a label).
5. **Dismiss**: a 24px button that appears on hover or keyboard focus, and always on touch screens.

### Corner position

<Demo name="Corner" />

`Position` pins the stack to a window corner: `BottomLeft`, `BottomRight`, `TopLeft` or `TopRight`. It is 360px wide
and 24px from the edges; on a window narrower than 480px it spans the width with 16px gutters. Top corners stack
downward. `Inline` (the default) keeps it in the layout, filling its container's width; use it for docs and previews,
and a corner on a real page.

### Selecting and limiting

<Demo name="Selectable" />

`IsSelectable` makes each toast a button that raises `Selected`, for example to open pricing or a customer story.
`Limit` stops after that many toasts in a session so the page stays calm. `Shown` fires each time a toast appears (for
impression tracking) and `Dismissed` when a visitor closes one.

### Pausing

The cycle pauses on its own while the stack is hovered or holds keyboard focus, while it is scrolled out of view and
while it is hidden, and the countdown keeps its remaining time. Set `IsPaused` while a modal or cookie banner
is open. `ExpandOnHover="False"` keeps the stack closed on hover (it still pauses). `Prefill` shows that many toasts
at once without an entrance, which is useful for previews and screenshots. `Restart` clears the stack and starts the
feed again.

### Your own words

`Labels` holds every word the stack shows or says. Replace the object to localize or reword:

```csharp
stack.Labels = new SocialProofToastLabels
{
    Region = "Actividad reciente",
    JustNow = "Ahora mismo",
    MinutesAgo = minutes => $"hace {minutes} min",
    Dismiss = sentence => "Cerrar: " + sentence,
};
```

### Content guidelines

- Keep the action short and concrete: "started a Pro trial", "upgraded to Business".
- Use first names for people and the company name for teams. Do not show surnames or email addresses.
- Put one fact in `Meta`: a city or a seat count, not both.

### Best practices

- Feed it real, recent events from your billing or analytics backend.
- Keep it calm: an interval of 4 to 8 seconds and a limit of about 5 to 8 toasts per session.
- Pause it while modals or cookie banners are open.

### Common questions

**Do new toasts interrupt screen readers?** No. New toasts are not announced; only dismissals are confirmed, politely.

**How many toasts are on screen?** At most `MaxVisible`, plus one fading out from the back.

## API reference

<!-- api: SocialProofToast -->

<!-- api: SocialProofEvent -->

<!-- api: SocialProofToastLabels -->

<!-- api: SocialProofToastSimulation -->

<!-- api: SocialProofToastSimulationEvent -->

<!-- api: SocialProofToastEventArgs -->

## Accessibility

- The stack is a labelled region ("Recent activity"). New toasts are not announced so screen reader users are not
  interrupted every few seconds; a dismissal is confirmed in a polite status.
- Each dismiss button names the toast it closes, such as "Dismiss: Sofia started a Pro trial".
- Toasts fading out past the limit are hidden from assistive technology and take no input. Hover and keyboard focus
  both pause the cycle, so nothing moves while someone reads or acts.
- The verified state is an icon plus a text label, never color alone. Avatars are not read separately because the name
  is in the sentence.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves to the dismiss button of each visible toast (and to the toast itself when `IsSelectable` is set). Focus inside the stack pauses it and fans it out |
| Enter, Space | Dismisses the toast from its dismiss button, or selects the focused toast when `IsSelectable` is set |
| Escape | Dismisses the focused toast, or the front one. Focus moves to the next toast |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| A new toast arrives | Rises 16px and fades in at the front; the others step back 10px and shrink 5% each on a bounce-free spring (visual duration 0.24s) | Fades in; the others move at once | At once |
| Covered toasts | Their content fades out over 160ms so text never overlaps | Same fade | At once |
| Hover or focus | The stack fans out on the same spring | At once | At once |
| Dismiss | Fades and shrinks slightly over 160ms; the rest close the gap on the spring | Fades; the rest move at once | At once |
| Past the limit | The back toast fades out over 160ms | Same fade | At once |

Toasts are always 64px high, so nothing resizes between states, and the inline stack reserves the height of the fanned
stack so hovering never moves the page. The cycle keeps running with reduced motion and with motion off: it is
content, not decoration.

## Related

- `Toast` for a brief confirmation of the visitor's own action.
- `Avatar` for the people in the feed.
