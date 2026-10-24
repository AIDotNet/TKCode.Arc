---
title: Onboarding illustration
summary: A compact first-run card whose line drawing morphs from an empty box to files, a team and a badge as people step through it.
---

<Demo name="Preview" />

## When to use

`OnboardingIllustration` is the first thing a person sees after creating an account or a workspace: a short card that
explains three or four ideas, one per step, each with a simple picture. The pictures are one line drawing whose shapes
travel from scene to scene, so the steps read as one story (an empty space, content arriving, people joining, done)
rather than four separate slides. Put it in a dialog or a first-run page and send Get started to the flow that sets
the workspace up.

- **A document or workspace product on first launch**: a notes app, a wiki or a file space that starts empty. The
  steps say where content lives, how to bring files in and how to invite people, and Get started opens the new space.
- **A new project, board or channel**: a project tool that walks a person through the first board, or a team chat app
  that creates a channel and invites members. Three steps are often enough.
- **A feature that changes how people work**: shared review, comments on a line, or a new import. A short tour of three
  or four steps before the person uses it for the first time.
- **A tour people come back to**: the completed state offers Replay intro, so the same card can live behind a Help menu
  entry such as "Show the tour again".

## When not to use

- **Tasks people complete over days**: use a checklist that tracks each task's progress and stays visible until the
  last task is done. This card does not remember which steps a person has read.
- **A form that collects information in several steps**: use `Stepper` or a multi-step form. This card shows
  information only; it has no fields and no validation.
- **A feature section on a marketing page**: use a feature block with a larger picture and a call to action. The card
  is sized for a dialog (at most 440 px wide).
- **Long explanations**: a step holds a title and a sentence or two. Material that needs headings, lists or code
  belongs in the documentation, with a link from the last step.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:OnboardingIllustration Label="Northdesk onboarding"
                                GetStartedCommand="{Binding CreateWorkspaceCommand}" />
</UserControl>
```

`OnboardingIllustration` derives from `TemplatedControl`. With no steps it shows four sample steps, one for each
scene, so it looks complete before you write any text. The block does no work of its own: it moves between steps,
raises events and leaves the rest to the app. Handle `GetStarted` (or bind `GetStartedCommand`) to create the
workspace, close the dialog or open the next page.

### Anatomy

1. **Card**: at most 440 px wide (narrower when its container is), a 1 px border, a 20 px corner radius, the raised
   surface color and the raised shadow. `IsFramed="False"` removes the border, the radius and the shadow.
2. **Stage**: the muted surface with a 1 px line below it. Padding 40 px above, 20 px at the sides and 16 px below.
3. **Drawing**: a 320 by 200 canvas scaled to fit, at most 352 by 220 px. Seven rounded shapes, a dotted trail, three
   short lines and a check mark. The 1.5 px strokes scale with the drawing.
4. **Skip**: a 28 px pill 10 px from the stage's top right corner, on every step but the last.
5. **Copy**: the step's title (the display face, 18 px, medium) over its sentence (14 px, secondary text). The copy
   area is at least 72 px high, so a one-line sentence and a two-line sentence leave the buttons in the same place.
6. **Step dots**: one 6 px dot per step; the current step's dot is 20 px wide. Done steps use the muted text color,
   the current step the accent and later steps the strong border color. Each dot is a button with a 32 px hit area.
7. **Back**: an outlined 36 px button with an arrow. On the first step and in the completed state it looks disabled
   but keeps its place in the Tab order.
8. **Main button**: 136 px wide and filled with the foreground color. It says Next, then Get started on the last step,
   then Replay intro once the onboarding is complete. Its width never changes with the label.

### Scenes

Each step names a scene. The scenes share the same shapes, and each shape plays a role in every scene, so moving from
one step to the next looks like one picture changing rather than a slide changing.

| Scene | What it shows | Use it for |
|---|---|---|
| `Folder` | An empty open box with a plus sign | A new, empty space; the first step |
| `Files` | Three pages dropping into the box while a note flies in along a dotted trail | Bringing content in: imports, uploads, connected sources |
| `Team` | Three people under a shared board; a dotted comment points at a highlighted line | Inviting people, sharing, reviewing together |
| `Badge` | A round badge with two ribbons, three rays and a check mark that draws in | Done: the last step |

A scene may appear on several steps; two steps with the same scene change only their text. Steps do not need to follow
the order above, but the story reads best when they do, because each shape moves to its next role.

### Your own steps

<Demo name="CustomSteps" />

```xml
<arc:OnboardingIllustration Label="Fieldnote onboarding"
                            CompletedTitle="Study created"
                            CompletedDescription="Open the tour again from Help whenever you like.">
    <arc:OnboardingStep Scene="Folder"
                        Title="Name your research space"
                        Description="A space holds one study. Start with the April interviews and add more studies later."
                        SceneDescription="An empty open box with a plus sign beside it" />
    <arc:OnboardingStep Scene="Files"
                        Title="Import your recordings"
                        Description="Drop audio, transcripts and notes. Fieldnote tags each file by speaker and date."
                        SceneDescription="Recordings dropping into the open box while a note flies in" />
</arc:OnboardingIllustration>
```

`Steps` is an `AvaloniaList<OnboardingStep>` and the control's content, so steps can be written inside the element.
A view model can supply them through `StepsSource` instead; it wins over `Steps`, and changes to an observable
collection show at once. Editing a step's `Title`, `Description` or `Scene` while it is shown updates the card in place.

`SceneDescription` is the drawing's accessible name for that step. Describe what the picture shows, in a short
phrase: "Three people gathered around one shared board".

The button words, the dots' accessible names and the announced status texts come from `Labels`, an
`OnboardingIllustrationLabels` object with English defaults. Texts with numbers are functions, so word order can change
in other languages:

```csharp
card.Labels = new OnboardingIllustrationLabels
{
    Skip = "Skip tour",
    GetStarted = "Open study",
    ReplayIntro = "Show tour",
    StepStatus = (step, count) => $"Tour step {step} of {count}",
    DotName = (step, title) => $"Tour step {step}: {title}"
};
```

### Steps, completion and events

| Action | Result | Event |
|---|---|---|
| Next (main button, steps before the last) | The next step | `StepChanged` (`Next`) |
| Back | The previous step; nothing on the first step | `StepChanged` (`Back`) |
| A step dot | That step, even several steps away; leaves the completed state | `StepChanged` (`Dot`) |
| Left / Right arrow | The previous or next step; nothing at either end or once complete | `StepChanged` (`Keyboard`) |
| Skip | The last step (not the completed state) | `StepChanged` (`Skip`), then `Skipped` |
| Get started (last step) | The completed state, if `IsCompletionVisible` | `GetStarted` |
| Replay intro | The first step | `StepChanged` (`Replay`), then `Replayed` |

`CurrentIndex` (0-based) and `IsCompleted` are two-way, so a view model can follow the card and move it. Setting
`IsCompleted` to true moves to the last step; setting `CurrentIndex` while complete leaves the completed state. The
methods `Next`, `Back`, `GoTo`, `Skip`, `Complete` and `Replay` do the same from code and return false when there was
nothing to do.

<Demo name="Controlled" />

`StepChanged` carries the old index, the new index and the reason, including `Programmatic` for changes made from code.
`GetStartedCommand` and `SkipCommand` run after their events, with the event arguments as the parameter.

<Demo name="EventsLog" />

```csharp
card.GetStarted += async (_, _) =>
{
    await workspaces.CreateAsync(name);
    dialog.Close();
};
```

When the app closes the dialog itself, set `IsCompletionVisible="False"`: Get started then raises `GetStarted` and the
card stays on the last step instead of showing its completed state. Leave it on when the card stays on screen (a Help
page or a side panel), so the person sees that the tour is done and can replay it.

### Narrow layouts

The card follows its own width, not the window's. Narrower than 410 px it switches to a compact layout: 42 px above
the drawing and 12 px around it, 16 px around the copy, a smaller title, a copy area of at least 88 px (room for three
lines), Back as a 36 px arrow button (still named "Back" for screen readers) and a 128 px main button. The drawing
shrinks with the card and keeps its proportions.

<Demo name="Narrow" />

On a phone, where the card fills the screen from edge to edge, set `IsFramed="False"`.

### Using motion with restraint

The morph is the card's signature: the box becomes the papers' destination, the papers become people and the people
become a badge. It works because each step is short and the person controls the pace.

- Keep each step to a title and a sentence or two. The drawing carries the change; long copy pulls attention away from
  it and makes the person wait for the text instead.
- Let people drive the steps. Do not advance the card on a timer: the morph takes about a second, and a step that
  moves on its own while someone reads is lost.
- Reduced motion replaces the morph with a 240 ms crossfade of the whole picture, and the copy fades without moving.

<Demo name="ReducedMotion" />

### Content guidelines

- **Title**: what the person does or gets in this step, in four to seven words, sentence case: "Bring the files you
  already use". No ending punctuation.
- **Sentence**: one or two sentences with a concrete example from their space: "Drag in slides, sheets, and PDFs."
  Mention real names (a project, a teammate) when the app knows them.
- **Last step**: confirm what is ready, with counts if they are known: "Spring launch notes now has 5 files and 2
  teammates."
- **Completed state**: say what happened and how to see the tour again, in one short sentence each.
- **Scene description**: describe the picture, not the step: "Pages dropping into the open box", not "Import step".

| Instead of | Write |
|---|---|
| Welcome to our amazing platform! | Set up your first space |
| Step 2 | Bring the files you already use |
| You can do lots of things with teams. | Invite Ana and Ruben to the same draft. |
| Success! | You are all set |
| Finish | Get started |

### Best practices

**Do**

- Keep the tour to three or four steps; with more, put the rest in the product where people meet it.
- Use the scenes in story order (`Folder`, `Files`, `Team`, `Badge`) so every shape moves to its next role.
- Send Get started somewhere useful: the new workspace, the import dialog, the first project.
- Keep Skip on. People who know the product already should reach the end in one press.
- Offer the tour again from a Help menu, and show the completed state there so Replay intro is one press away.

**Avoid**

- Collecting input in the card. It has no fields; use a form for settings and names.
- Advancing on a timer or on scroll. The person decides when to move on.
- Hiding the dots. They show how long the tour is and let people jump back to a step they want to read again.
- Using the badge scene before the last step: it says "done", which confuses people in the middle of a tour.

### Common questions

**How do I show the card only once?** The card does not remember anything. Store a flag when `GetStarted` (or
`Skipped`) is raised and do not open the dialog next time.

**Why does Back stay focusable on the first step?** Back is marked unavailable but stays in the Tab order (Arc's
`aria-disabled`), so the focus does not jump when a step makes it unavailable. Screen readers announce it as disabled,
and pressing it does nothing.

**What happens to focus after Skip?** Skip disappears on the last step. If it had focus, focus moves to the main
button (Get started), so keyboard users can carry on with Enter.

**Can a step reuse a scene?** Yes. Two steps with the same scene change only their text when you move between them.

**Can the card have more than four steps?** Yes, and as few as one (then the only button is Get started). The dots
grow with the steps; past five or six the tour usually needs shortening rather than more dots.

## API reference

<!-- api: OnboardingIllustration -->

<!-- api: OnboardingStep -->

<!-- api: OnboardingIllustrationLabels -->

<!-- api: OnboardingStepChangedEventArgs -->

## Accessibility

- The card is a region named by `Label` ("Northdesk onboarding"). Put it in a dialog with its own name when it opens
  on first run.
- The drawing is an image named by the current step's `SceneDescription`; the name changes with the step.
- Every change of step reads the new title, sentence and position ("Step 2 of 4") through a polite live region. The
  completed state reads its title and sentence followed by "Onboarding complete".
- The dots are a group named "Onboarding steps". Each dot is a button named "Step 2: Bring the files you already use",
  and the current one reports "Current step".
- Back is reported as disabled on the first step and in the completed state, but stays focusable. In the compact layout
  it shows only its arrow and keeps the name "Back".
- Focus stays where it was after every action: on the main button after Next, Get started and Replay intro, on Back
  after Back, on the dot after a dot. When Skip disappears while it has focus, focus moves to the main button.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move through Skip, each step dot, Back and the main button |
| `Left` / `Right` | Previous or next step, from anywhere in the card; focus does not move. Nothing at either end, and nothing once complete (`Right` never presses Get started) |
| `Enter` / `Space` | Press the focused button or dot |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| The step changes | Every shape springs to the next scene (centers in about half a second, sizes, corners and turns a little slower), one after another 30 ms apart; going back, the last shape moves first. The trail curves in at 100 ms and its dots appear at 160 ms; the lines follow at 120, 160 and 200 ms; colors cross over in 480 ms | The old picture fades out as the new one fades in, 240 ms | Immediate |
| The badge appears | The check mark draws in from 320 ms over 480 ms | Shown complete | Shown complete |
| A second change within about 0.7 s | Every shape turns toward the new scene at once, keeping its speed | Crossfade | Immediate |
| The copy changes | The old text lifts 4 px and blurs out in 180 ms; the new text rises 6 px out of a blur from 80 ms, over 480 ms | The texts fade (180 ms out, 480 ms in) | Immediate |
| The main button's label changes | The old label lifts out in 180 ms; the new one rises in over 240 ms | The labels fade | Immediate |
| A step dot | The current dot widens to 20 px and the old one narrows to 6 px on a spring; the color crosses over in 240 ms | Width at once; the color still crosses over | Immediate |
| Skip appears or goes | Fades in 160 ms | Fades in 160 ms | Immediate |
| A mouse moves over the stage | The shapes drift toward the pointer by up to 6 px across and 4 px down, more for shapes in front | No drift | No drift |

The card has no entrance animation and no motion at rest: the drawing only moves after an action or under the mouse.

## Related

- `EmptyStates` for an empty view whose drawing morphs between four situations.
- `Stepper` for a multi-step task that collects input and validates each step.
- `Dialog` to host the card on first run.

| Component | Choose it when |
|---|---|
| `OnboardingIllustration` | A short first-run tour of three or four ideas, each with a picture |
| `EmptyStates` | A view is empty and the drawing explains why |
| `Stepper` | People fill in a form in several steps |
| `Dialog` | The card opens on top of the app on first run |
