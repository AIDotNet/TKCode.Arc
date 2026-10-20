---
title: Theme switcher
summary: Four smooth ways to move between light and dark appearance.
---

## When to use

- A single light and dark switch in a header or toolbar.
- A switch whose page transition should match the layout: a circle from the button, an eclipse, a centre seam or a rise.
- A switch that only reports the request and lets the app apply the theme.

## When not to use

- A theme choice that includes system or sits in an account menu: use `UserMenu`.
- A plain action button that does not change the theme: use `Button`.

## Usage

`ThemeSwitcher` shows the appearance that the next press will switch to. Without an `Appearance`, it follows the
actual theme variant of the window.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ThemeSwitcher Variant="Reveal" />
</UserControl>
```

### Variants

`Variant` picks the page transition. `Reveal` spreads the new appearance in a circle from the button. `Eclipse`
crosses the page like an eclipse, which suits showcase pages. `Split` opens from a centre seam and does not depend on
the button position. `Rise` rises from the bottom and is the quietest.

```xml
<arc:ThemeSwitcher Variant="Eclipse" />
<arc:ThemeSwitcher Variant="Split" />
<arc:ThemeSwitcher Variant="Rise" IsIconOnly="True" />
```

### Icon only and text

`IsIconOnly` shows a 38 by 38 square button. `Text` sets the visible text beside the icon, and `Label` sets the
accessible name. Without a `Label`, the name says which appearance the next press will switch to.

```xml
<arc:ThemeSwitcher Text="Appearance" Label="Switch appearance" />
```

### Handling the request

`ThemeChangeRequested` is raised before the theme changes. Its arguments give the next appearance, the variant, the
trigger button and the origin of the reveal. Set `Handled` to apply the theme yourself. The switcher then does not apply
it.

```csharp
switcher.ThemeChangeRequested += (_, e) =>
{
    e.Handled = true;
    switcher.Appearance = e.Next;
    ApplyAppearance(e.Next);
};
```

### Controlled appearance

`Appearance` is the appearance shown: the icon, the pressed state and the name. Set it from code to keep the switcher in
step with the app.

```xml
<arc:ThemeSwitcher Appearance="Light" Variant="Rise" />
```

```csharp
switcher.Toggle();
```

## API reference

<!-- api: ThemeSwitcher -->

## Accessibility

- The switcher is a toggle button. It reports the dark appearance as On, and its name names the next appearance.
- The sun and moon icons are hidden from assistive technology.
- Enter and Space switch the theme.
- Keyboard focus shows the standard focus ring, as on any Arc button.

## Motion

- The sun and the moon turn and scale into each other on a shared path, and the old icon leaves quickly.
- In dark mode the icon nudges in the direction of its variant: `Reveal` enlarges it, `Eclipse` and `Rise` move it slightly.
- The page transition reveals the new appearance by its variant. The old window is kept as a snapshot and the new
  appearance is cut in over it.
- The first appearance set does not animate.
- Reduced motion removes the icon nudge and the page transition, and the icon fades in at once. Off switches at once.

## Related

- `Button` for a plain action with no theme change.
- `UserMenu` for a theme choice that includes system and lives in an account menu.
- `SegmentedControl` for a choice among a few named options that stays selected.
- `IconMorph` for a standalone icon that morphs between two states.
