---
title: Cube mascot
summary: An isometric cube mascot whose shaded faces and painted-on face show what the assistant is doing: it turns in eased quarter turns while thinking, prints lines onto the floor while writing, settles with a sparkle when done and shakes once on error.
---

<Demo name="Preview" />

## When to use

`CubeMascot` is an assistant character drawn from real 3D math: a cube with a rounded silhouette, three faces shaded by
how they face the light, and a face painted on the front side. Each status changes both its motion and its face.

- **Show that an assistant is generating** with a recognisable, product-like character: in a reply header, in a
  composer, on an image canvas or in an empty state.
- **Brands with a geometric, technical feel**, where a soft blob would feel off.

## When not to use

- **A softer, rounder character**, or one that needs a listening state: use a pebble or ghost style mascot instead.
- **Steps, tools and elapsed time of a run**: use `AgentStatus`.
- **A known percentage**: use a progress bar; the cube shows activity, not progress.

## Usage

Map your assistant's real phases to `Status`: waiting for the first token is `Thinking`, streaming is `Writing`,
finished is `Done`, failed is `Error`, otherwise `Idle`. Keep thinking short in real use and switch to writing on the
first token.

```xml
<arc:CubeMascot Size="24" Status="{Binding Phase}" AssistantName="Atlas" />
```

```csharp
mascot.Status = CubeMascotStatus.Thinking; // request sent
mascot.Status = CubeMascotStatus.Writing;  // first token
mascot.Status = CubeMascotStatus.Done;     // last token
```

Leave `Status` unset and the cube plays through idle, thinking, writing, done and error on a loop. That is meant for
showcases; `EffectiveStatus` reports the state it shows and `StatusChanged` is raised at each step.

### Anatomy

1. **Body**: a cube turned so that its face side looks slightly left, seen from 32 degrees above. Its silhouette is
   the convex hull of the eight projected corners with rounded corners.
2. **Faces**: the top, the face side and the shaded side, each shaded by its normal against a light from the upper
   left, so a face that turns away darkens continuously.
3. **Painted face**: eyes, brows and a mouth drawn through the face side's projected plane, so they foreshorten with it.
4. **Ground** (56px and up): a contact shadow and, while writing, lines printed onto the floor in front of the face.
5. **Sparkle**: a four-point star that grows and turns in when done.

### States

| Status | Motion | Face |
|---|---|---|
| `Idle` | Slow tilt, sway, float and glance on sines that never repeat; blinks every 2.6 to 4.9s | Open eyes, a gentle smile |
| `Thinking` | Holds 0.5s, then turns a quarter turn over 0.8s, rising and leaning into the turn | Eyes up; the face fades in on the side that arrives |
| `Writing` | Tips the face down; a line every 360ms slides out along the ground; a nod per four lines | Eyes down, sweeping slowly |
| `Done` | Dips once and settles; the sparkle grows, turns in, then twinkles | Eyes close into a happy squint |
| `Error` | Shakes 3.5 times in 560ms and comes to rest | Worried brows and a frown |

### In a reply header

<Demo name="ReplyHeader" />

Use 24 to 32px inline in a message header or composer, 40 to 64px in a panel header and 96 to 160px in an empty state
or on a generating canvas. Set `AssistantName` to the assistant's name; it is used in the accessible label.

### Sizes

<Demo name="Sizes" />

`Size` sets the width and height of the square (96 by default). Below 56px a compact drawing takes over: the cube fills
more of the frame, the eyes are bolder and there is no mouth, shadow or printed lines, so it reads at 24px. The square
never changes size between states, so it never moves the layout around it.

### Colors

<Demo name="Colors" />

The body is the accent by default and the face is the accent foreground, so the cube follows the theme and the accent
in light and dark. `BodyBrush` and `FaceBrush` change them; the top and the shaded side are mixed from the body.
`TopBrush`, `FrontBrush` and `SideBrush` set the three shading anchors directly, and `LineBrush`, `ShadowBrush` and
`SparkleBrush` color the printed lines, the contact shadow and the sparkle. `ShowsGround="False"` drops the shadow and
the lines.

### On a canvas, and pausing

<Demo name="EmptyState" />

`IsPaused` freezes the cube on the current frame. The cube also pauses by itself while it is off screen or hidden, and
resumes on the same frame.

### Best practices

- Turn `Announce` on for the one mascot that carries status, not for decorative copies.
- Show a text status next to the cube where the state matters; the cube takes no input, so put controls such as Stop
  beside it.
- Leave `Status` unset only for showcases.

## API reference

<!-- api: CubeMascot -->

<!-- api: CubeMascotStatus -->

<!-- api: CubeMascotLabels -->

<!-- api: CubeMascotStatusChangedEventArgs -->

## Accessibility

- The cube presents as an image whose name is built from the assistant's name and the status, for example "Atlas,
  writing", and updates with every state. `AccessibleName` replaces the whole label; the words are in
  `CubeMascotLabels`.
- With `Announce` on, status changes are read in a polite live region. Use it on one mascot per view.
- Status is never motion alone: each state changes the face (eyes up, eyes down, happy squint, worried brows and frown)
  and the label.
- Under reduced motion every state is a still pose that switches in place.

### Keyboard

| Keys | Action |
|---|---|
| None | The mascot is a status image and takes no focus. Pair it with a real control when people act on what it shows. |

## Motion

| Change | Full | Reduced and off |
|---|---|---|
| Idle | Tilt, sway, float and glance as a sum of slow sines with incommensurate periods; a 170ms blink every 2.6 to 4.9s, sometimes twice | Still pose |
| Thinking | Holds 0.5s, turns 90 degrees over 0.8s on a cubic ease in and out, rising slightly and leaning into the turn; the leaving face fades as it turns edge on, the arriving face fades in and opens its eyes | Still, eyes up |
| Writing | A line every 360ms slides out from under the face along the ground for 1.44s and only fades in and out; its length changes while it is invisible; the eyes sweep and the cube nods once per four lines | Still, three printed lines |
| Done | One dip that settles without overshoot; the eyes pass through a closed line into the squint; the sparkle grows over 550ms, turns in and then twinkles slowly | Still squint and sparkle |
| Error | 3.5 shakes in 560ms under a squared envelope that reaches rest with zero velocity | Still brows and frown |
| Status change | Every pose value blends from the frame on screen over 420ms (240ms into error) on an ease in and out; a change caught mid turn keeps turning forward | Switches in place |

The cube is real 3D: eight corners are rotated (yaw, roll, pitch, then a fixed 32 degree camera) and projected each
frame. One frame loop per cube moves its paths in place and recolors its brushes, so a frame allocates nothing. The
loop runs only while the cube is on screen and visible, and its clock only advances then, so it resumes on the same
frame. A settled error does not loop at all, and under reduced motion nothing loops.

## Related

- `AgentStatus` for the steps, tools and elapsed time of a run.
- `ToolCall` for the tool calls an agent makes.
- `ContextWindow` for how much of a model's context is in use.
