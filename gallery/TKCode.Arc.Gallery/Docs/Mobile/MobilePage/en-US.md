---
title: Mobile page
summary: A phone screen: a header, the content and a footer laid out inside the device's safe area.
---

<Demo name="Basic" />

## When to use

`Page` is the root of one screen of a phone or tablet app. It gives the screen its background, keeps its content clear of the status bar, the notch and the home indicator, and puts an optional header (usually a `Navbar`) above the content and an optional footer (usually a `Tabbar`) below it.

- **A screen of a mobile app**: an inbox, a settings list or a detail view, with a navbar on top and tabs at the bottom.
- **A full-bleed screen**: a welcome or onboarding screen with only content, where the background still runs under the status bar.
- **A screen that must react to the device's safe area**: the page follows the platform's safe area padding by itself, or takes one you set.

The live demos on this page run inside a phone frame: a desktop simulation with a device switcher, portrait and landscape, a simulated status bar and safe area, and a keyboard placeholder. It is not a device, and how the controls look and behave on Android and iOS, including with the real soft keyboard, has not been verified.

## When not to use

- **A desktop window or a web-style layout**: use the desktop controls (`TKCode.Arc`) and a normal layout panel. The page's safe area is zero where the platform reports none.
- **A container inside a screen**: use `Card` or a layout panel. A screen has one `Page`.
- **A modal or a sheet over a screen**: use a dialog or a drawer; the page is the screen under them.

## Usage

Set `Header`, the content and `Footer`. The mobile controls have their own XAML namespace, `https://github.com/AIDotNet/TKCode.Arc.Mobile`, usually prefixed `arcm`, so they sit next to the desktop controls of the same name.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
             xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page>
        <arcm:Page.Header>
            <arcm:Navbar Title="Inbox" />
        </arcm:Page.Header>
        <TextBlock Margin="16" Text="Hello" />
    </arcm:Page>
</UserControl>
```

Register the controls with `UseArcMobile()` next to `UseArcCore()` or `UseArcControls()`.

### Safe area

`SafeArea` is the space the page keeps free on each edge. While `UsePlatformSafeArea` is on (the default), the page follows the safe area padding of the top level's insets manager and uses zero where there is none, such as on desktop. Turn `UsePlatformSafeArea` off and set `SafeArea` yourself to override it. The background fills the whole page, so it runs under the status bar while the header and content stay inside the safe area.

The phone frame does this for the demos: it sets `SafeArea` from the simulated device and turns `UsePlatformSafeArea` off. Use the switcher above the phone to see an iPhone-like notch and home indicator, a gesture bar, a small phone and a tablet, and the keyboard toggle to see the page lay out above the keyboard.

### Content only

A page without a header or footer lets the content use the whole safe area.

<Demo name="ContentOnly" />

### Scrolling content

By default the content gets the page's height as it is, so a screen that fills the page can lay itself out. Set `IsScrollable="True"` when the content can be taller than the page: it then scrolls vertically inside the page, between the header and the footer. A `Navbar` with a large title in the `Header` collapses it as the content scrolls.

```xml
<arcm:Page IsScrollable="True">
    <arcm:Page.Header>
        <arcm:Navbar Title="Photos" LargeTitle="True" />
    </arcm:Page.Header>
    <!-- long content -->
</arcm:Page>
```

### Keyboard

When the soft keyboard is up, the page lays itself out above it: the content shrinks by the keyboard's height, the footer goes behind the keyboard (the way iOS hides a tab bar) and the focused input is scrolled into view. `AvoidKeyboard` (default on) controls this; turn it off for a page the keyboard may cover. `KeyboardInset` is the height the keyboard covers, 0 when it is hidden, and it combines with `SafeArea`: the page keeps the larger of the two at the bottom. Put `IsScrollable="True"` on a form so the fields scroll above the keyboard.

<Demo name="Keyboard" />

The height comes from the platform's input pane (`TopLevel.InputPane`) when there is one. A host without it, such as the phone frame on this page, reports it with a `KeyboardInsetSource`, set on any control above the page.

```csharp
var keyboard = new KeyboardInsetSource();
Page.SetKeyboardInsetSource(host, keyboard); // inherited by every page below host
keyboard.Set(291);                            // the keyboard is up, 291 high
keyboard.Set(0);                              // and down again
```

An overlay such as a `Sheet` reads the height from anywhere inside the page with `Page.GetKeyboardInset(visual)`, to lift itself above the keyboard.

### Tokens

`PageToken` holds the geometry: `ContentInset` (the padding around the content slot, 0). The background, foreground, font and size come from the ArcDesign tokens (`Background`, `Foreground`, `BodyFontFamily`, `TypeBase`), so the page follows light and dark and the accent.

## API reference

<!-- api: MobilePage -->

## Accessibility

The page adds no keyboard behavior or role of its own; its header, content and footer keep theirs. Keep the content in reading order and give the navbar a `Title` so the screen has a name.

## Motion

The background follows light and dark on the standard curve. With reduced motion it switches at once.

The page follows the keyboard with a `MotionValue` on the bottom inset, because the inset is a layout input that a ControlTheme transition cannot express. It uses the platform's own keyboard duration when the platform reports one, otherwise a smooth spring on the way up and a snappy one on the way down. With reduced motion, or with motion off, the layout jumps to the keyboard. The focused input is scrolled into view when the keyboard arrives and again when it has settled.

## Related

- `MobileNavbar`: the page's top bar.
- `MobileTabbar`: the page's bottom bar.
- `MobileButton` and `MobileSwitch`: touch controls for the content.
