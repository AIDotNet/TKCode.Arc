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

Register the controls with `UseArcMobile()` next to `UseArcCore()` or `UseArc()`.

### Safe area

`SafeArea` is the space the page keeps free on each edge. While `UsePlatformSafeArea` is on (the default), the page follows the safe area padding of the top level's insets manager and uses zero where there is none, such as on desktop. Turn `UsePlatformSafeArea` off and set `SafeArea` yourself to override it. The background fills the whole page, so it runs under the status bar while the header and content stay inside the safe area.

The phone frame does this for the demos: it sets `SafeArea` from the simulated device and turns `UsePlatformSafeArea` off. Use the switcher above the phone to see an iPhone-like notch and home indicator, a gesture bar, a small phone and a tablet, and the keyboard toggle to see the content area shrink.

### Content only

A page without a header or footer lets the content use the whole safe area.

<Demo name="ContentOnly" />

### Tokens

`PageToken` holds the geometry: `ContentInset` (the padding around the content slot, 0). The background, foreground, font and size come from the ArcDesign tokens (`Background`, `Foreground`, `BodyFontFamily`, `TypeBase`), so the page follows light and dark and the accent.

## API reference

<!-- api: MobilePage -->

## Accessibility

The page adds no keyboard behavior or role of its own; its header, content and footer keep theirs. Keep the content in reading order and give the navbar a `Title` so the screen has a name.

## Motion

The background follows light and dark on the standard curve. With reduced motion it switches at once.

## Related

- `MobileNavbar`: the page's top bar.
- `MobileTabbar`: the page's bottom bar.
- `MobileButton` and `MobileSwitch`: touch controls for the content.
