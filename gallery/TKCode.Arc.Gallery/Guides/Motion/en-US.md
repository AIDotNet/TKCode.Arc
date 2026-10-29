---
title: Motion
---

# Motion

Motion in TKCode.Arc follows Arc's motion system: springs for anything that moves or changes size, short eased fades
for color and opacity, and rolling digits and word-by-word rewrites for text that changes. Motion is there to explain
what changed (where a panel came from, which value moved, which word is new), not to decorate. Every animation in the
library respects a motion level, so people who are sensitive to motion, and machines that need to stay light, get a
calmer interface without losing any information.

## Motion levels

There are three levels, `ArcMotionLevel.Full`, `Reduced` and `Off`:

| Level | What moves | When to use it |
|---|---|---|
| `Full` (default) | Springs, travel, scale, morphs, blur, staggered entrances, rolling digits | The normal experience. |
| `Reduced` | Only short fades and color changes; positions and sizes jump to their end, decorative loops stop | People who asked for less motion, remote desktop sessions, presentations. |
| `Off` | Nothing; every change applies at once | Tests and screenshots, kiosk and low-power devices. |

No information is carried by motion alone: under `Reduced` and `Off`, the end state is the same as under `Full`.

### Setting the level

`ArcMotion.Level` is an inherited attached property. Set it on a window to affect everything in it, or on any
container to affect only that part of the interface:

```csharp
ArcMotion.SetLevel(mainWindow, ArcMotionLevel.Reduced);
```

```xml
<StackPanel arc:ArcMotion.Level="Off" Spacing="12">
    <arc:Progress Label="Import" Value="64" />
    <arc:Switch Content="Sync in background" IsChecked="True" />
</StackPanel>
```

The theme can set a level for the whole application too, through `ArcTheme.Apply(t => t.MotionLevel(...))` (see
**Theming**). The effective level of a control is the lower of the two: the inherited `ArcMotion.Level` and the theme's
motion mode. A window set to `Full` in a theme set to `Reduced` runs reduced motion.

The library does not read the operating system's reduce-motion preference on its own. Offer a setting in your
application (the gallery's Appearance panel has Full, Reduced and Off), store it with the user's other preferences,
and apply it to the main window at start-up.

## The motion system

### Springs

Arc's springs are defined by how long they look and how much they bounce, not by physics constants. The presets are
`ArcSpring` values and are also available as `ArcMotionPresets`:

| Spring | Shape | Used for |
|---|---|---|
| `Smooth` | 0.4 s, no bounce | Motion that reports state: switch thumbs, panels, progress, values. |
| `Snappy` | 0.26 s, bounce 0.12 | Presses, small indicators, icon swaps, pop-ins, menus. |
| `Morph` | 0.42 s, bounce 0.16 | Shared highlights, widths that follow content, shape morphs. |
| `Responsive` | stiffness 520, damping 38 | Direct manipulation that must keep up with the pointer. |
| `Gentle` | stiffness 340, damping 34 | Larger, calmer movements. |

A spring started while another is running continues from the current position and velocity, so interrupting an
animation (pressing a button twice, dragging back) never jumps.

### Durations and easing

| Token | Value | Used for |
|---|---|---|
| `DurationInstant` | 120 ms | Hover and press colors. |
| `DurationFast` | 160 ms | Small fades, the reduced-motion replacement for most movements. |
| `DurationExit` | 180 ms | Leaving elements; exits are quicker than entrances. |
| `DurationStandard` | 240 ms | Most fades and color transitions. |
| `DurationConsidered` | 480 ms | Larger, deliberate changes. |
| `DurationSpring` | 580 ms | The CSS-style spring curve. |

The easing curves are `EaseEnter`, `EaseExit`, `EaseStandard` and `EaseInOut` (`ArcEasings` in code). Staggered
entrances use `StaggerChar` (16 ms), `StaggerWord` (40 ms), `StaggerLine` (80 ms) and `StaggerItem` (35 ms) between
elements.

### Performance

Motion runs on a shared frame scheduler: running springs and loops advance together in one batch per frame, and
nothing is scheduled while nothing moves. Continuous effects (skeleton pulses, shimmers, live dots) pause
when their control is scrolled out of view, hidden or detached. Animated blurs are quantized to 0.25 px steps and
reused, so a running blur does not allocate per frame.

## Motion in your own controls

The rules the library follows apply to controls you write as well.

### Transitions in a control theme

Declare transitions in the control's `ControlTheme`, under the motion pseudo-classes, and set
`arc:ArcMotion.Tracks="True"` so the control receives them. A tracked control gets `:motion-full` or
`:motion-reduced` after it is loaded, so the first frame never animates; with level `Off` it gets neither, and nothing
animates:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <ControlTheme x:Key="StatusPanelTheme" TargetType="Border">
        <Setter Property="arc:ArcMotion.Tracks" Value="True" />
        <Style Selector="^:motion-full">
            <Setter Property="Transitions">
                <Transitions>
                    <arc:SpringDoubleTransition Property="Opacity" Spring="Smooth" />
                    <arc:SpringThicknessTransition Property="Padding" Spring="Snappy" />
                </Transitions>
            </Setter>
        </Style>
        <Style Selector="^:motion-reduced">
            <Setter Property="Transitions">
                <Transitions>
                    <DoubleTransition Property="Opacity" Duration="0:0:0.16" />
                </Transitions>
            </Setter>
        </Style>
    </ControlTheme>
</ResourceDictionary>
```

The spring transitions (`SpringDoubleTransition`, `SpringThicknessTransition`, `SpringCornerRadiusTransition`,
`SpringSizeTransition`, `SpringPointTransition`, `SpringVectorTransition` and
`SpringTransformOperationsTransition`) take a `Spring` preset, or a custom `VisualDuration` and `Bounce`. Under reduced
motion, replace movement with a short opacity fade and let sizes and positions change at once.

### Motion that a theme cannot express

When a value must follow the pointer, keep its velocity across interruptions, or drive custom drawing, use
`MotionValue<T>`. It honors the effective motion level of its owner: under `Off`, and for springs under `Reduced`, it
jumps to the target.

```csharp
public sealed class SpendBar : Control
{
    private readonly MotionValue<double> _fill;
    private double _shown;

    public SpendBar()
    {
        _fill = new MotionValue<double>(this, 0, MotionVectors.Double);
        _fill.Changed += value =>
        {
            _shown = value;
            InvalidateVisual();
        };
    }

    public void ShowShare(double share) => _ = _fill.AnimateTo(share, MotionSpec.Spring(ArcSpring.Smooth));

    public override void Render(DrawingContext context)
    {
        var accent = ArcTokens.GetColor(this, ArcDesignTokenKind.Accent);
        context.FillRectangle(new SolidColorBrush(accent), new Rect(0, 0, Bounds.Width * _shown, Bounds.Height));
    }
}
```

`MotionVectors` covers `double`, `Point`, `Vector`, `Size`, `Rect`, `Thickness`, `CornerRadius` and `Color`; each
component runs its own spring. `MotionSpec.Tween(duration, easing)` runs a timed curve instead of a spring, and
`MotionSpec.Instant` jumps. For loops (a pulse, a sweep) use `LiveLoop`, which pauses off screen and below its minimum
motion level; for a value that runs to 1 over a fixed time (a hold-to-confirm fill, a countdown) use `MotionTimeline`.

Use code-driven motion only when a control theme cannot express it, and say why in a comment next to it. Theme
transitions are cheaper, easier to review and automatically follow the motion level.

## Guidelines

**Do**

- Animate the change, not the element: move what moved, fade what appeared, roll the digit that changed.
- Keep entrances under half a second and exits quicker than entrances.
- Give every animated change an end state that reads correctly without the animation, then check it with `Off`.
- Use `Snappy` for feedback on a press and `Smooth` for anything that reports a value.

**Avoid**

- Motion as the only signal of a state change; pair it with text, an icon or a color.
- Animating many large surfaces at once; stagger them or animate the container.
- Bounce on anything that represents money, progress or an error: it reads as playful.
- Infinite loops in the main content; reserve them for loading states and stop them when the content arrives.

## Testing

- Run tests and capture screenshots with `ArcMotionLevel.Off`, so the results show final states and do not depend on
  timing. The library's own tests and the gallery's screenshots do this.
- Review each screen once with `Reduced`: nothing should slide, scale or bounce, and nothing should be missing.
- Watch for layout jumps under `Off`: a container that only looked right because of a size animation needs a fixed
  layout.
