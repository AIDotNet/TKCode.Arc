---
title: Orbit logos
summary: An integrations orbit: your product mark in the centre, logos on two slowly turning rings, and a card that morphs out of the logo you pick.
---

<Demo name="Preview" />

## When to use

`OrbitLogos` shows everything your product plugs into as logos on two rings around your own mark. The rings turn
slowly and hold still whenever the pointer or keyboard focus is inside. Picking a logo glides it to a fixed spot, draws
a line to it from the centre and opens a card with what the integration does, its status and a Connect action.

- **An integrations page or a hero**: a lively picture of the ecosystem that still lets people look closely.
- **An integrations screen in your app**: browse by category, read about a tool and connect it in one place.
- **A launch page**: everything the product works with, in one glance.

## When not to use

- **Logos that are only social proof**: if nobody will open them, a quiet row of marks says it with less motion.
- **A big catalogue**: two rings get crowded past a couple of dozen marks, and people want to search a big list; use
  a grid or a table with a search field.
- **When the order or a search matters**: the rings turn, so nothing stays in a fixed place.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:OrbitLogos>
        <arc:OrbitLogo Name="Vercel"
                       Category="Dev"
                       Icon="m12 1.608 12 20.784H0Z"
                       Description="Comments preview links on builds."
                       IsConnected="True" />
        <arc:OrbitLogo Name="Slack"
                       Category="Comms"
                       Icon="avares://MyApp/Assets/slack-color.png"
                       Description="Posts deploy alerts to your channels."
                       Href="https://example.com/docs/slack" />
    </arc:OrbitLogos>
</UserControl>
```

`OrbitLogos` is a templated control in the Special package. Register the package with `UseArcSpecial()` next to
`UseArcControls()`. The logos are its content: each `OrbitLogo` has a `Name`, an optional `Id` (selections use the `Id`,
or the name when there is none), an `Icon`, a `Category`, a one-line `Description`, a starting `IsConnected` and an
`Href` for the Docs link. The orbit fills the width up to 460px; from 760px wide it sits beside the detail panel, below
that the card sits under the orbit.

### Marks

`Icon` takes SVG path data on a 24 by 24 grid (the Simple Icons format), an image address (a `.png`, `.jpg` or `.webp`
file, or an http(s), `avares`, file or data URL), an `ImageSource` or any control. Path data follows the text color, so a
black-and-white brand needs no color; set `Color` on the logo to paint it in the brand color. Images keep their own
colors. A control as `Icon` shows on the ring only; the card then falls back to the first letter of the name.

Brand marks are sample content. Show only integrations that exist and follow each brand's trademark rules.

### Categories

Give every logo a `Category` and the chips appear above the orbit; `AllLabel` renames the first chip. A chip turns the
matching logos toward the detail side and dims the rest, and the arrow keys then step only through that category. A
category that does not contain the open logo closes its card.

### Opening, connecting and the centre

`DefaultSelected` opens a logo on first display; `SelectedId` is the open logo's id or name, two-way, and `null` closes the
card. `SelectionChanged` reports `Logo` and `Index` when a person opens, steps or closes, with no logo and index -1 when
the card closes.

```csharp
orbit.ConnectHandler = async logo => await api.ConnectAsync(logo.Id ?? logo.Name);
```

The Connect button calls `ConnectHandler` (or a `ConnectRequested` handler that sets `Completion`). The card shows
Connecting while the task runs, marks the logo connected when it completes and shows Try again if it faults. Without a
handler the card simulates a short connection.

<Demo name="Connect" />

`Center` is the content of the centre disc, such as your product mark; it defaults to a simple mark in the accent color.
`Speed` multiplies the rotation (at 1 the inner ring turns once every 64 seconds and the outer ring, the other way,
every 88 seconds; 0 holds the orbit still) and `IsPaused` holds it for a page-level motion control. `Label` is the
accessible name of the logo list and the title of the idle panel.

<Demo name="Center" />

### Best practices

**Do**

- Put your product mark in the centre and give every logo a category and a one-line description.
- Wire `ConnectHandler` to your real flow and return its task.
- Keep to about a dozen logos so the rings stay readable.

**Avoid**

- Logos without a name: the name is the accessible name and the tag people see.
- Using the orbit as the only way to reach an integration; keep the settings screen it belongs to.

### Common questions

**Why does the orbit stop when I move over it?** It eases to rest while the pointer or focus is inside, a category is
chosen or a card is open, so nothing moves while you aim.

**Do name tags show on touch screens?** No. Tags appear on hover and keyboard focus only.

## API reference

<!-- api: OrbitLogos -->

<!-- api: OrbitLogo -->

## Accessibility

- The logos are a labelled list of toggle buttons; each name includes the category and whether it is connected.
- Only one logo is in the tab order, so the orbit is a single stop between the chips and the card.
- The card is a labelled group with real buttons and a Docs link.
- Connected status is shown as text and an icon in the card, not by the dot on the chip alone.
- Opening, stepping, filtering and connecting are announced politely.
- Pointer focus draws no ring; keyboard focus shows the shared focus ring, the lifted chip and its name tag.

### Keyboard

| Key | Action |
|---|---|
| Tab | Moves into the category chips, then to one logo in the orbit, then into the card. The orbit holds still while focus is inside. |
| Arrow keys | Step to the previous or next logo in the current category. With a card open, the card and the ring follow. |
| Home, End | Jump to the first or last logo in the current category. |
| Enter, Space | Opens the focused logo, or closes it if it is already open. |
| Escape | Closes the card and returns focus to its logo. |

## Motion

- One loop moves the chips with render transforms and sleeps when everything is at rest, the orbit is out of view or the
  window is hidden.
- The turn eases to rest on a critically damped spring whenever the pointer or focus enters, a category is picked or a
  card is open.
- Opening a logo springs its ring the short way, with a touch of overshoot, until the logo is at the fixed spot: on the
  right of the orbit when it sits beside the detail panel, at the bottom otherwise. When an outer logo parks, the inner
  ring turns a gap to that side so the connector stays clear.
- An accent connector grows from the centre to the chosen logo.
- The card grows out of the chip: it starts as a circle at the chip's box and springs to its own box, and then its
  content fades in. Previous and next slide the content in the direction of travel.
- The category highlight is one pill that glides between the chips on the shape spring.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Rotation | Turns slowly, eases to rest | None | None |
| Rings and connector | Spring into place | Placed at once | Placed at once |
| Card | Morphs out of the chip | Appears | Appears |
| Previous and next | Slide | Swap | Swap |

## Related

- `HoverCard` previews a person or link on hover without leaving the page.
- `Carousel` browses a row of slides instead of a ring of logos.
