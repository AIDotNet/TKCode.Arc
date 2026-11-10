---
title: Hero section
summary: Three full screen SaaS heroes: a live dashboard rising from the bottom edge over a drifting mesh, a workflow graph that routes sample events node by node, and editorial type over a mesh gradient.
---

<Demo name="Preview" />

## When to use

A hero is the first screen of a landing page: it has a moment to say what the product does. `HeroSection` gives you three
complete designs for that moment, and a plain layout for your own words.

- **The product screen is the pitch**: `Centered` sets a headline over a soft moving gradient, with a dashboard that
  rises from the bottom edge. The range switch inside it really works.
- **Showing the product working is the pitch**: `Split` puts the copy beside a workflow that runs sample events on its
  own, for automation, developer tools and infrastructure.
- **One confident line**: `Minimal` is large type over a full mesh, for a launch or a download page.
- **None of those fit**: set `Title` and the hero shows your own announcement, headline, actions, install command, facts
  and visual in the chosen layout.

## When not to use

- **Titles inside an app**: use `PageHeader`. A hero is for the outside of a product, not for its screens.
- **The goal is an email address**: use `NewsletterSignup`, which validates and confirms in place.
- **The end of a page**: use `CtaSection`. A hero opens a story, and a call to action closes it.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:HeroSection Variant="Split" />
</UserControl>
```

`HeroSection` derives from `TemplatedControl`. `Variant` picks the design. Each design fills the height it is given, or
one viewport when it sits in a scroller, so it fills one screen in a page and fits a frame in a preview. The sample copy
and data belong to fictional products, so replace them or set `Title` before shipping.

### Designs

| `Variant` | What it shows | Good for |
|---|---|---|
| `Centered` | Headline over a drifting mesh, and a dashboard that settles from a tilt and fades out at the bottom edge | Products with a strong screen |
| `Split` | Copy beside a workflow graph on a dotted canvas | Automation, developer tools |
| `Minimal` | Large type over a mesh with grain, customers along the bottom edge | Launches, downloads |

<Demo name="Editorial" />

### Actions

`PrimaryAction` and `SecondaryAction` take a `HeroAction`, which adds `IsExternal` to `CtaAction`. Leave one unset and the
design shows its own. A press first raises `ActionInvoked` and runs the `Command`. Then:

| The action has | A press also does |
|---|---|
| An `Href` | Opens the address, unless a handler marked the event handled. Only absolute http, https and mailto addresses open |
| A `ConfirmedLabel` and no `Href` | Shows a check and the label for 2.4 seconds |
| Neither | Nothing more |

Primary actions end in an arrow that leans 2 px under a mouse or a pen. `IsExternal` shows an outward arrow instead.

```csharp
hero.PrimaryAction = new HeroAction { Label = "Start free trial", Command = viewModel.StartTrial };
hero.SecondaryAction = new HeroAction { Label = "Pricing", Href = "https://example.com/pricing", IsExternal = true };
hero.ActionInvoked += (_, e) => analytics.Track("hero", e.Action.Label);
```

### The workflow

`Split` draws its graph inside the block, so it needs no other package. Every event runs the same steps: a trigger, a
lookup and a branch, then three actions. Data draws along each wire as a step finishes, and each step reports its time.
A small payment takes the "No" branch, so two actions are skipped and their wires turn dashed. **Send test event** runs
the next sample at once. The run pauses off screen and resumes where it stopped.

<Demo name="Workflow" />

### Your own content

Set `Title` and the hero shows your content at its natural height. `Variant` becomes the layout: `Centered` centers the
copy, `Minimal` sets it left, and `Split` also shows `Media` beside it. The other properties are optional: `Description`,
`Announcement`, `InstallCommand` (with a copy button) and `Meta` (small facts). The actions show only if you set them.

<Demo name="OwnContent" />

```xml
<arc:HeroSection Variant="Split" Title="Ship your docs today" InstallCommand="npm create quickdocs">
    <arc:HeroSection.Media>
        <Border Height="320" Background="{arc:ArcDesignTokenResource SurfaceMuted}" />
    </arc:HeroSection.Media>
</arc:HeroSection>
```

### Best practices

**Do**

- Say what the product does in the headline, in a few words. Titles balance their lines and wrap at 12 to 16 characters.
- Give the primary action a real command or address.
- Replace the sample product, numbers and customers with your own.

**Avoid**

- Long descriptions. Two sentences are enough.
- Two heroes on one page. The designs each fill a screen and each runs a moving background.

## API reference

<!-- api: HeroSection -->

<!-- api: HeroAction -->

## Accessibility

- The title is a level one heading, and the section is named by it when you set `Title`.
- Every action, the switch inside the dashboard and the copy button have accessible names.
- The mesh, the dots, the glow and the customer marks are decoration. The dashboard and the graph are named as sample
  content, and each node's state has a name: Waiting, Running, Done or Skipped.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move through the actions, then the controls inside the design |
| `Enter` / `Space` | Run an action or send a test event |

## Motion

- The entrance plays once: items rise 12 px out of a 2 px blur in 640 ms, 80 ms apart. Set `IsEntranceAnimated` to false
  to skip it, or call `ReplayEntrance` to see it again.
- The dashboard rises 56 px in 1.1 s while it settles from a 22 degree tilt and 94% scale in 1.5 s.
- The meshes drift on a 20 second loop at thirty frames a second, while they are on screen.
- A workflow node's state pops on the snappy spring, its fill changes in 240 ms and each wire draws in the time of its step.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Entrance | Rises, blurs and tilts | Fades only | Shown at once |
| Mesh | Drifts | One still frame | One still frame |
| Workflow | Runs event by event | Each event shows its finished run | Each event shows its finished run |

## Responsive behavior

- `HeroSection` follows its own width, not the window's. Headlines scale with the width.
- `Centered`: below 720 px of room the dashboard switches to its 600 px design and crops evenly at both edges rather than
  shrink past 60%. At 560 px or less the side gutters go away and the actions fill the row.
- `Split`: at 900 px or less the graph moves under the copy. Below 520 px it uses its compact layout and hides the
  run summary.
- `Minimal`: at 800 px or less the description and the actions stack, and customers wrap to four columns, then three.
- Touch: an arrow leans only for a mouse or a pen, so a touch never depends on hover.

<Demo name="Narrow" />

## Related

| Component | Choose it when |
|---|---|
| `HeroSection` | The first screen of a landing page |
| `CtaSection` | You ask for a trial or a demo at the end of a page |
| `NewsletterSignup` | The action is an email address |
| `StatsBand` | You back the claim with numbers just under the hero |
