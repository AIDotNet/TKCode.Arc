---
title: Window
summary: A desktop window with an Arc title bar: left, center and right areas for your controls, native dragging, snapping and resizing, and caption buttons that follow the theme.
---

<Demo name="Basic" />

## When to use

`ArcWindow` replaces the system title bar with an Arc `TitleBar` drawn in the window's client area, the way WPF's
`WindowChrome` does. The bar takes the theme's colors in light and dark, and you can put your own controls in it.
The window still behaves like a system window:

- dragging the bar moves it, and a double click maximizes or restores it;
- the edges resize it;
- on Windows the bar is the system caption: a right click opens the system menu, dragging to an edge snaps the window,
  and pointing at maximize opens Windows 11 Snap Layouts;
- on macOS the traffic lights stay, and the bar leaves room for them.

Use it for the main window of a desktop app that wants one surface from the top edge down, with a menu, tabs, a search
field or tool buttons in the title bar.

## When not to use

- **Browser and mobile apps**: they have no windows. Show your content in the single view.
- **Dialogs inside a window**: use `Dialog`, `Drawer` or `BottomSheet`, which open in the window's overlay layer.
- **Windows that should look like every other window of the system**: a plain Avalonia `Window` keeps the system title bar.

## Usage

Derive the main window from `ArcWindow` instead of `Window`. Without anything else it shows a bar with the window's
`Title` and, on Windows and Linux, minimize, maximize and close.

```xml
<arc:ArcWindow xmlns="https://github.com/avaloniaui"
               xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
               Title="Atlas" Width="1200" Height="800">
    <TextBlock Text="Content" />
</arc:ArcWindow>
```

```csharp
public partial class MainWindow : ArcWindow
{
    public MainWindow() => InitializeComponent();
}
```

The window extends its client area into the decorations (`ExtendClientAreaToDecorationsHint` is true) and draws no
title bar of its own where Avalonia would (an empty `WindowDecorationsTheme`). It asks the system for a title bar as
tall as the Arc bar, so macOS centers the traffic lights in it. The content starts below the bar. Popups, tooltips and
toasts open over both, as in any Arc window.

### Your controls in the bar

Set the window's `TitleBar` to a `TitleBar` of your own (in XAML, inside `<arc:ArcWindow.TitleBar>`) and fill its three areas:

- `LeftContent` follows the icon and the title (a menu, navigation);
- `CenterContent` is centered on the whole bar while it fits between the sides (a search field, tabs);
- `RightContent` sits before the caption buttons (tool buttons, a theme switcher).

Controls in the areas take their own clicks; the empty part of the bar around them still moves the window.

<Demo name="Areas" />

```xml
<arc:TitleBar>
    <arc:TitleBar.LeftContent>
        <arc:Button Content="File" Variant="Ghost" Size="Small" />
    </arc:TitleBar.LeftContent>
    <arc:TitleBar.CenterContent>
        <arc:Input Placeholder="Search" Size="Small" Width="220" />
    </arc:TitleBar.CenterContent>
    <arc:TitleBar.RightContent>
        <arc:Button Variant="Ghost" Size="Small">
            <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Settings}" Size="15" />
        </arc:Button>
    </arc:TitleBar.RightContent>
</arc:TitleBar>
```

When the window gets narrow, the right area keeps its width, the left area gives way first and the center moves
towards the wider side, then shrinks to the gap between them. Give the center a `Width` or `MaxWidth`.

`Icon` shows before the title, such as an `Image` of the app's logo or an `ArcGlyph`. `Title` follows the window's
`Title` unless you set it on the bar.

### Height

The bar is 40 px tall. Set `Height` on the bar for another height; the window passes it on to the system.

```xml
<arc:TitleBar Height="48" />
```

### Caption buttons

`CaptionButtons` decides who draws minimize, maximize and close:

| Value | Buttons |
|---|---|
| `Auto` (default) | `System` on macOS, `Drawn` elsewhere |
| `Drawn` | The bar draws them at its right end. Close turns red under the pointer. |
| `System` | The system draws them (the macOS traffic lights); the bar leaves room for them at its left |
| `None` | Neither draws them |

<Demo name="CaptionButtons" />

Maximize shows restore while the window is maximized. It is disabled when `CanResize` or `CanMaximize` is false, and
minimize when `CanMinimize` is false.

### Always on top

Set `ShowTopmostButton` to `True` on the `TitleBar` and a pin toggle appears before the caption buttons. It is checked while the window's `Topmost` is true and sets `Topmost` when toggled, so changing `Topmost` from code updates the button too. It is off by default, and it shows with every `CaptionButtons` value.

```xml
<arc:TitleBar ShowTopmostButton="True" />
```

### Without the bar

Set `IsTitleBarVisible` to `False` (or `TitleBar` to null) and the content reaches the top of the window, which still
extends into the decorations and resizes from its edges. Your content then has to move the window, for example with
`BeginMoveDrag`. Set `ExtendClientAreaToDecorationsHint` to `False` as well to get the system's title bar back.

For minimize, maximize and close in a header of your own, use `WindowCaptionButtons`, and put the content in an
`ArcOverlayHost` beside that header so that dialogs and drawers leave it uncovered (see Window caption buttons). A bar
hidden this way is not hit, so its buttons never compete with yours.

### Window border

On Windows 11, DWM draws a 1 px frame on the left, right and bottom edges, but not on the top edge once the client area extends into the title bar. On Windows, while the window is in its normal state (not maximized, full screen or `WindowDecorations="None"`), `ArcWindow` sets the `:top-edge` pseudo-class and draws that top edge itself (color `Border`), so all four edges match. To drop the line, override it with a style:

```xml
<arc:ArcWindow xmlns="https://github.com/avaloniaui"
               xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ArcWindow.Styles>
        <Style Selector="arc|ArcWindow:top-edge /template/ Border#PART_Root">
            <Setter Property="BorderThickness" Value="0" />
        </Style>
    </arc:ArcWindow.Styles>
    <!-- ... -->
</arc:ArcWindow>
```

### Previews

A `TitleBar` drives a window only when it is that `ArcWindow`'s `TitleBar`. One placed anywhere else, like the previews
on this page, draws the same bar and moves, maximizes and closes nothing.

## API reference

<!-- api: ArcWindow -->

<!-- api: TitleBar -->

<!-- api: TitleBarCaptionButtons -->

## Accessibility

- The caption buttons have tooltips and accessible names (Minimize, Maximize or Restore, Close) in the app's language.
- They are not in the Tab order, as on system title bars. The keyboard reaches the same commands through the system
  (Alt+Space opens the system menu on Windows, Alt+F4 closes the window).
- The title dims while another window is active.
- The always-on-top toggle has a tooltip and an accessible name (Keep on top, Stop keeping on top), is in the Tab order and toggles with Space or Enter.

## Motion

- The caption buttons fade their fill and glyph color under the pointer.
- With reduced motion or motion off the colors change at once.
- Maximizing, restoring and snapping are the system's own animations.

## Responsive behavior

- `ArcWindow` is a desktop window, so it has no breakpoint. Its `MinWidth` is 360 px (from ArcWindowToken), the
  narrowest a window can be while the title and the caption buttons still fit side by side.
- When the title bar runs out of width, the right section with the caption buttons keeps its width, the left section
  gives way first, and the center moves toward the wider side before it shrinks. Give the center a `Width` or
  `MaxWidth`; the title is cut with an ellipsis.
- Caption buttons are 30 px (`CaptionButtonSize`), so do not rely on them as touch targets.
- Phones and browsers have no windows: show the content in a single view there, and keep `ArcWindow` for desktop apps.

## Related

- `Dialog`, `Drawer` and `BottomSheet`: content that opens over the window.
- `WindowCaptionButtons`: minimize, maximize and close for a window whose content reaches the top.
- `DropdownMenu`: menus for the bar's left area.
- `Tabs`: tabs for the bar's center area.
