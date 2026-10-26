---
title: Mobile button
summary: A touch-first button with a press state instead of a hover state and a 44 px minimum touch target.
---

<Demo name="Basic" />

## When to use

`Button` of `TKCode.Arc.Mobile` is the button for touch screens. It is filled, at least 44 by 44, and shows a press state: while a finger is on it the button scales down to 0.97 and dims to 0.85. There is no hover state, because a finger does not hover.

- **The main action of a screen**: "Continue" or "Save", full width at the bottom of the content.
- **An action in a navbar slot**: "Edit" or "Done".
- **A form's submit button**.

The live demo runs inside a phone frame, a desktop simulation. With a mouse, press and hold the button to see the press state. How it feels under a finger on Android and iOS has not been verified.

## When not to use

- **A desktop interface**: use `Button` from `TKCode.Arc`, which has variants, sizes and hover states.
- **A choice that stays selected**: use `MobileSwitch` for a setting, or a list for several options.

## Usage

The mobile `Button` is in the `https://github.com/AIDotNet/TKCode.Arc.Mobile` XAML namespace (prefix `arcm`) and in the .NET namespace `TKCode.Arc.Mobile`, so it does not clash with the desktop `Button`; this page's API table calls it `MobileButton` for the same reason. It derives from Avalonia's `Button`, so `Content`, `Command` and `Click` work as usual.

```xml
<StackPanel xmlns="https://github.com/avaloniaui"
            xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
            Spacing="12">
    <arcm:Button Content="Continue" HorizontalAlignment="Stretch" />
    <arcm:Button Content="Not available" IsEnabled="False" />
</StackPanel>
```

A disabled button is dimmed to 0.5.

### Variants

`Variant` sets how prominent the button is: `Filled` (default, a solid pill for the main action), `Tinted` (a soft accent pill for a secondary action) and `Plain` (accent text with no background, for bar actions). Use one `Filled` button per screen.

```xml
<arcm:Button Variant="Tinted" Content="More" />
<arcm:Button Variant="Plain" Content="Done" />
```

### Tokens

`ButtonToken` holds the geometry: `TouchTarget` (44), `ContentPadding` (16 by 10), `PressedScale` (0.97), `PressedOpacity` (0.85) and `DisabledOpacity` (0.5). Colors, radii and type come from the ArcDesign tokens, so the button follows light and dark, the accent and the Pixel style.

## API reference

<!-- api: MobileButton -->

## Accessibility

It is a normal Avalonia button: it can be focused and invoked from the keyboard and exposes its content as its name. The 44 px minimum keeps the target easy to hit.

## Motion

The press scale and dim are declared in the control theme and switch at once with reduced motion.

## Related

- `MobileSwitch`: a toggle for settings.
- `MobileNavbar`: buttons go in its slots.
- `MobilePage`: the screen a button sits on.
