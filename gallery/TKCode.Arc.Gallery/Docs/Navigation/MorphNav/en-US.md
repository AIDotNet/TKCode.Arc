---
title: Morph nav
summary: A compact navigation pill that grows into a panel of sections and links on one continuous surface, and folds back into the pill when a link is chosen.
---

<Demo name="Preview" />

## When to use

`MorphNav` is a site or app menu that stays out of the way. At rest it is one small pill: your mark, your name, the
page you are on and a menu glyph. Pressing it does not open a separate menu; the pill itself grows, on a spring, into
a panel of titled sections with a link per row, and when a link is picked the panel shrinks back into the pill, which
now names the new page. Because the same surface changes size, corners and shadow, the eye never loses its place.

- **Marketing sites and docs** with a handful of sections, where a full header bar would crowd the hero.
- **Focused apps** (an editor, a reader, a player) that want navigation one press away but not on screen all the time.
- **Phone layouts**, where the panel takes the width of the screen and stacks its sections.

## When not to use

- **Deep hierarchies**: a tree with many levels needs a sidebar or a breadcrumb, not one panel.
- **Navigation used on every step** of a task: a visible tab bar or sidebar saves a press each time.
- **Actions** such as Delete or Export: the panel is for going somewhere; use a menu or a toolbar for doing things.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:MorphNav Title="Lantern">
        <arc:MorphNavSection Header="Notebook">
            <arc:MorphNavLink Text="Today" Description="What you saw this morning" />
            <arc:MorphNavLink Text="Archive" />
        </arc:MorphNavSection>
        <arc:MorphNavSection Header="Help">
            <arc:MorphNavLink Text="Contact" />
        </arc:MorphNavSection>
    </arc:MorphNav>
</UserControl>
```

`MorphNav` is in the Special package: register it with `UseArcSpecial()` next to `UseArcControls()`. Its content is a
list of `MorphNavSection` elements, each with a `Header` and its `MorphNavLink` elements. A link has `Text`, an
optional `Description` line and an optional `Glyph` drawn in a small tile; `Tag` carries your own value, such as a
route.

The nav takes only the pill's room in the layout. The panel grows over the page: to the right when the nav is aligned
left, to the left when it is aligned right and both ways when it is centered, and it keeps 16 px from the sides of the
window. It sits above its siblings (`ZIndex` 10), so put it in the same panel as the page it floats over.

### Knowing what was chosen

`LinkInvoked` is raised when a link is chosen, with the link in `e.Link`. The link becomes `CurrentLink`, which is
two-way: bind it to your router to mark the page you are on, and set it when the page changes by other means. Its
text shows in the pill, and its row is marked in the panel. `Open()`, `Close()`, `Toggle()` and the two-way `IsOpen`
control the panel from code.

`Footer` is shown under the sections, for a sign-in or a call to action. `PanelWidth` sets the panel's width (600 px by
default), never more than the window less its gutters.

<Demo name="Footer" />

### Best practices

**Do**

- Keep it to two to four sections of a few links each, so the panel reads at a glance.
- Give links a short `Description` when their names alone are vague.
- Keep `CurrentLink` in step with the page, so the pill always says where you are.

**Avoid**

- Long link texts: rows trim with an ellipsis rather than wrap.
- Putting the nav inside a parent that clips: the panel is drawn outside the pill's box.

## API reference

<!-- api: MorphNav -->

## Accessibility

- The pill is a button named by `MenuLabel` and the current page ("Open menu, Archive"), and reports whether the panel
  is expanded.
- Links are announced as links named by their text, with the description as help text. Each can be invoked by
  assistive technology.
- Focus moves into the panel when it opens from the keyboard (to the current page, else the first link) and back to the
  pill when it closes. Pointer use shows no focus ring; keyboard use shows the shared ring.

### Keyboard interactions

| Key | Action |
|---|---|
| Enter or Space on the pill | Opens the panel. |
| Down / Up | Moves to the next or previous link. |
| Right / Left | Moves to the link at the same height in the section beside it, when sections sit side by side. |
| Home / End | Moves to the first or last link. |
| Enter or Space on a link | Chooses the link and closes the panel. |
| Escape | Closes the panel and returns focus to the pill. |

A press outside the nav also closes the panel.

## Motion

- Opening and closing are one surface changing size on the morph spring, with its corners going from the pill's to the
  panel's and its shadow rising from resting to floating.
- The pill fades out as the panel fades in; on the way back the panel keeps its room until it has faded, then the
  surface shrinks to the pill.
- The surface clips its content only while it moves. Nothing outside the nav is laid out again while it moves.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Size | Morph spring | Jumps | Jumps |
| Corners and shadow | Spring and 240ms | Jump | Jump |
| Pill and panel | Crossfade | Crossfade | Swap |

## Responsive behavior

- The panel is `PanelWidth` wide, never wider than the window less 16 px on each side. Inside the phone preview it
  follows the preview's width.
- Sections sit side by side while each column can be at least 176 px wide, and stack on narrower screens.
- The pill's width follows its content; keep the title short on phones.

## Related

- `Tabs` and `LiquidTabBar` keep navigation always on screen.
- `ActionMorph` uses the same growing surface for an action rather than for navigation.

| Component | Choose it when |
|---|---|
| `MorphNav` | Navigation should stay one small pill until it is needed |
| `Tabs` | A few views of one page should always be visible |
| `ActionMorph` | A single action needs a little room for its input |
