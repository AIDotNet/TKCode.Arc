---
title: CTA section
summary: A call to action as a centered closing section, a split beside a setup card that completes itself, or a dismissible banner.
---

<Demo name="Preview" />

## When to use

A call to action asks one thing of the reader: start a trial, book a demo, sign up. `CtaSection` gives that request
three shapes, so the same control can close a landing page, sit beside a product visual in the middle of one, or run
across the top of an app. With nothing set it shows Arc's copy for the shape you choose.

- **The last section of a landing page**: the `Centered` shape, a quiet card with a headline, one sentence, two
  actions and a line of small print with the faces of people who already joined.
- **An in-page prompt beside a product visual**: the `Split` shape puts the copy and short benefit lines on the left
  and a setup card on the right. The card ticks through its steps once it is in view, so the reader sees the outcome the
  button leads to.
- **A slim promotional banner above content**: the `Banner` shape is one line with one action and an optional dismiss
  button. Dismissing closes its gap, so the page below moves up smoothly.

## When not to use

- **The first screen of a page**: use a hero. A call to action closes or interrupts a story; it does not open one.
- **Collecting an email address**: use a newsletter signup block, which validates, sends and confirms in place.
- **A persistent site-wide notice**: use an announcement bar. The banner here is one message with one action, closed by
  the reader, not a rotating or counting notice.
- **A decision with consequences**: use a dialog or an alert dialog. A call to action invites; it does not interrupt.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:CtaSection />
</UserControl>
```

`CtaSection` derives from `TemplatedControl`. `Variant` picks the shape; every text, action, list and picture is a
property, and every one has Arc's English text as its default except the pictures, which are empty until you add
images. The block is a layout, so the action rules, the data and the destinations stay in your app.

### Shapes

| `Variant` | What it shows | Actions |
|---|---|---|
| `Centered` | Headline, text, actions, and a note with up to four faces on a quiet card | Primary, secondary |
| `Split` | Copy, benefit lines and actions beside a stage; the stage holds the setup card or your own `Visual` | Primary, secondary |
| `Banner` | One line of text, an action and an optional dismiss button | Primary only |

Set `Title`, `Description`, `PrimaryAction` and `SecondaryAction` to replace the text of any shape. Leave a value unset
and the shape shows its own Arc copy, so switching `Variant` from `Centered` to `Split` changes the words too.

### Actions

Each action is a `CtaAction` with a `Label`, an optional `Href`, a `ConfirmedLabel`, a `Command` and a
`CommandParameter`. What happens on a press depends on what you gave it.

| The action has | A press does |
|---|---|
| An `Href` | Raises `ActionInvoked`, runs the command if there is one, then opens the address unless a handler marked the event handled. Only absolute http, https and mailto addresses are opened |
| A `Command`, or a handler marks `ActionInvoked` handled | Only runs. Nothing else changes on screen |
| None of these | Confirms in place: a check and `ConfirmedLabel` (or `Label`) replace the text, a second press restores it, and the change is announced politely |

The in-place confirmation is what makes a preview answer every press. In an app, give each action a command, a
destination or a handler.

```xml
<arc:CtaSection Variant="Split">
    <arc:CtaSection.PrimaryAction>
        <arc:CtaAction Label="Create a workspace" Command="{Binding CreateWorkspace}" />
    </arc:CtaSection.PrimaryAction>
    <arc:CtaSection.SecondaryAction>
        <arc:CtaAction Label="Book a demo" Href="https://example.com/demo" />
    </arc:CtaSection.SecondaryAction>
</arc:CtaSection>
```

```csharp
section.ActionInvoked += (_, e) =>
{
    if (e.IsPrimary)
    {
        analytics.Track("cta_primary", e.Action.Label);
    }
};
```

`CtaActionEventArgs` carries the `Action` that was pressed, `IsPrimary`, and `IsConfirmed`: true when the press puts a
plain action into its confirmed state. Mark the event `Handled` to take over a plain action completely.

<Demo name="Links" />

Primary actions end in an arrow that leans 3 px forward under a mouse or a pen. Touch does not trigger the lean.
Actions are large buttons in the centered and split shapes and small buttons in the banner.

### Centered

The `Centered` shape sets its note under the actions. `Note` defaults to "No credit card required"; set it to an empty
string to hide the text. Add images to `Faces` and up to four overlapping faces appear beside the note; the row hides
when it has neither.

```csharp
section.Note = "Join 4,000 teams";
section.Faces.Add(new Bitmap("avatar-1.png"));
section.Faces.Add(new Bitmap("avatar-2.png"));
```

The faces are decoration for screen readers, so describe what the section offers in `Title` and `Description`.

### Split and the setup card

The `Split` shape lists benefit lines in `Points` (null shows Arc's three; an empty list hides the list) and puts a
stage on the right. By default the stage holds a setup card that completes itself:

1. The stage rises into view once, when 35% of it is visible.
2. When half of the card is visible, three timers start. Step *i* finishes 520 + 620 × *i* ms later (about 0.5, 1.1 and
   1.8 seconds in).
3. As each step finishes its mark fills with a check that springs in, the progress line grows, and the next step takes
   the muted fill. The status at the top changes from "Setting up" to "Ready".
4. The last row shows `SetupFaces`, which slide in one after another once that step is current.

Configure the card with `SetupWorkspace` (its first letter is the mark), `SetupSteps` (null shows Arc's three) and
`SetupFaces`. The card is purely a picture of the outcome. It is hidden from screen readers, and the copy beside it
carries the message.

To show your own screenshot or control, set `Visual`. The stage keeps its size, border and entrance; the setup card and its
timers are not used.

<Demo name="CustomVisual" />

```xml
<arc:CtaSection Variant="Split">
    <arc:CtaSection.Visual>
        <Border Width="320" Height="200" CornerRadius="26" Background="{arc:ArcDesignTokenResource SurfaceRaised}" />
    </arc:CtaSection.Visual>
</arc:CtaSection>
```

### Banner

The `Banner` shape is one rounded line. Set `IsDismissible` to show the dismiss button. Pressing it, or calling
`Dismiss()`, closes the banner: it fades out in 160 ms while its height springs to zero, and then `IsOpen` becomes false
and `Dismissed` is raised. `Show()` brings it back at once.

```xml
<arc:CtaSection Variant="Banner"
                IsDismissible="True"
                IsOpen="{Binding ShowPromo, Mode=TwoWay}" />
```

`IsOpen` is two way. Setting it to false from your own code plays the same exit and does not raise `Dismissed`. Remember
the choice yourself (a setting, a cookie equivalent) if the banner should stay closed after the next launch. Set
`DismissLabel` to name the button in another language.

### Responsive layout

The block follows its own width, not the window's. At 860 px or less the split becomes one column with the stage
below the copy. At 560 px or less the paddings tighten, the cards round to 28 px, the actions stack at full width and
the banner puts its text above its actions, with the dismiss button at the right edge. The headline grows with the
block, from 30 px up to 52 px.

<Demo name="Narrow" />

### Content guidelines

- **Headline**: a promise in a few words, such as "Start building in minutes". Centered headlines wrap at about 18
  characters and balance their lines.
- **Action labels**: a verb and at most two other words ("Create a workspace", "Book a demo"). The primary action is
  the one thing you want the reader to do next.
- **Confirmed labels**: say what happened ("Workspace created"), not "Done".
- **Benefit lines**: three or fewer, each one short enough to fit in one line of the left column.

### Best practices

**Do**

- Give the primary action a real destination or command, and keep a single primary action per section.
- Use `Centered` at the end of a page, `Split` in the middle beside a relevant state, and `Banner` at the top of an app
  or documentation page.
- Keep `IsDismissible` on for banners that are not essential, and keep the choice of a reader who dismissed one.

**Avoid**

- Two primary actions. The secondary action is quieter on purpose.
- Long headlines. The title is limited by width and balanced, so a long sentence becomes a tall block.
- Relying on the in-place confirmation in production. It exists to answer presses in previews.

## API reference

<!-- api: CtaSection -->

<!-- api: CtaAction -->

<!-- api: CtaActionEventArgs -->

## Accessibility

- The section is a region named by its headline.
- Every action and the dismiss button have an accessible name. Confirmations are announced politely through a hidden
  live region, and pressing a confirmed action again announces its plain label.
- The faces and the setup card are decoration and are hidden from screen readers.
- The banner closes its gap when dismissed, so the focus order stays clean after it goes.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move through the actions, then the dismiss button in the banner |
| `Enter` / `Space` | Run an action or dismiss the banner |

Keyboard focus shows a 2 px ring around the focused action or dismiss button; pointer focus does not.

## Motion

- Primary actions lean their arrow 3 px forward on hover (160 ms), and a label change crossfades.
- The split's stage rises into view once (opacity and 24 px of travel, 480 ms). In the setup card, the status words
  swap with a short blur, the progress line grows in 480 ms, step marks fill in 240 ms with the check springing in over
  580 ms, and the teammates' faces slide in 60 ms apart.
- Dismissing fades the banner in 160 ms while its height springs closed. Nothing scales.

| Change | Full | Reduced | Off |
|---|---|---|---|
| The stage appears | Rises in once, 480 ms | Shown at once | Shown at once |
| The setup card runs | Steps tick, the line grows, faces slide in | Every step is finished at once | The timeline runs; each change jumps |
| The banner is dismissed | Fades in 160 ms, height springs to zero | Fades in 120 ms, then is removed | Removed at once |
| The arrow leans | 160 ms | Jumps | Jumps |

## Related

- `Button` for a single action, `SegmentedControl` for the layout switch in the preview.
- A newsletter signup when the action collects an email address.

| Component | Choose it when |
|---|---|
| `CtaSection` | You ask for a trial, a demo or a signup at the end of a page, beside a visual, or in a banner |
| A newsletter signup | The action is an email address |
| `Button` | You need one action and no section around it |
