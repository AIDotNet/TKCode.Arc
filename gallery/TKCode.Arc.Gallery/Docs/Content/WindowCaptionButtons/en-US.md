---
title: Window caption buttons
summary: Minimize, maximize and close for a window whose content reaches the top: put them in your own header, and they follow the window and open Snap Layouts on Windows.
---

<Demo name="Preview" />

## When to use

`WindowCaptionButtons` are the minimize, maximize and close buttons of the Arc title bar as a control of their own.
Use them when the window's content reaches the top edge and the app draws its own header: an `ArcWindow` with
`IsTitleBarVisible="False"` whose sidebar runs to the top, a page header that is also the title bar, or a row of tabs
in the title bar's place.

The buttons drive the window they are in, like the system's:

- they minimize, maximize and close the window, and maximize shows restore while the window is maximized;
- maximize is disabled while `CanResize` or `CanMaximize` is false, and minimize while `CanMinimize` is false;
- on Windows they tell the system what they are, so pointing at maximize opens Windows 11 Snap Layouts.

## When not to use

- **A window that keeps the Arc title bar**: `ArcWindow`'s `TitleBar` already draws the same buttons. Use one set per
  window.
- **macOS**: the system keeps the traffic lights at the top left of every window. Hide the buttons there (see
  Platforms).
- **Browser and mobile apps**: they have no windows. Show your content in the single view.
- **Closing a panel or a dialog**: use the close button of `Dialog` or `Drawer`, or an icon `Button`.

## Usage

Hide the bar of an `ArcWindow` and put the buttons at the right end of your own header. The window still extends its
client area into the decorations, draws no title bar of its own and resizes from its edges.

```xml
<arc:ArcWindow xmlns="https://github.com/avaloniaui"
               xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
               Title="Atlas" IsTitleBarVisible="False">
    <DockPanel>
        <Grid DockPanel.Dock="Top" Height="48" ColumnDefinitions="*,Auto">
            <TextBlock Text="Inbox" Margin="20,0" VerticalAlignment="Center" />
            <arc:WindowCaptionButtons Grid.Column="1" Margin="0,0,8,0" />
        </Grid>
        <TextBlock Text="Content" />
    </DockPanel>
</arc:ArcWindow>
```

A plain Avalonia `Window` with `ExtendClientAreaToDecorationsHint` works too, but on Windows and Linux Avalonia then
draws a title bar with caption buttons of its own over your content. `ArcWindow` turns that bar off.

"Open a window" under the preview opens such a window: a header with a search field and the buttons, and the content
in an overlay host.

### Choosing the buttons

`ShowMinimizeButton`, `ShowMaximizeButton` and `ShowCloseButton` are `True` by default. Set one to `False` to hide that
button; the others close up, and the hidden button takes no room. A window that keeps its size shows minimize and
close, and a tool window only close.

<Demo name="Buttons" />

### Moving the window

The buttons do not move the window: your header does. On Windows, give the header the `TitleBar` element role and the
controls in it the `User` role; the header is then the system caption, so dragging it moves the window, a double click
maximizes it, a right click opens the system menu and dragging it to an edge snaps the window. Give the header a
background (`Transparent` is enough) so its empty part is hit. On macOS and Linux, move the window from a
`PointerPressed` handler instead: there the system would act only on the very element hit, or swallow the double click.

```csharp
if (OperatingSystem.IsWindows())
{
    WindowDecorationProperties.SetElementRole(header, WindowDecorationsElementRole.TitleBar);
    WindowDecorationProperties.SetElementRole(search, WindowDecorationsElementRole.User);
}
header.PointerPressed += (_, e) =>
{
    // On Windows a press on the caption never gets here.
    if (e.Handled || !e.GetCurrentPoint(header).Properties.IsLeftButtonPressed)
    {
        return;
    }
    if (e.ClickCount == 2)
    {
        window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
    else
    {
        window.BeginMoveDrag(e);
    }
    e.Handled = true;
};
```

`WindowDecorationProperties` is in `Avalonia.Controls.Chrome` and `WindowDecorationsElementRole` in `Avalonia.Input`.
`PageHeader` (in `TKCode.Arc.Blocks`) does all of this when `IsTitleBar` is `True`.

### Keep modals below the title bar

`Dialog`, `Drawer`, `BottomSheet` and the other modals open on the window's overlay layer and cover the whole window,
your header included, so the caption buttons and the drag area sit under the scrim while a modal is open. To keep them
in reach, put the app's content in an `ArcOverlayHost` and the header beside it, outside the host. A modal opened from
the content then covers the host only and never the header.

```xml
<arc:ArcWindow xmlns="https://github.com/avaloniaui"
               xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
               Title="Atlas" IsTitleBarVisible="False">
    <DockPanel>
        <!-- Outside the host: never under a scrim. -->
        <Grid DockPanel.Dock="Top" Height="48" ColumnDefinitions="*,Auto">
            <TextBlock Text="Atlas" Margin="20,0" VerticalAlignment="Center" />
            <arc:WindowCaptionButtons Grid.Column="1" Margin="0,0,8,0" />
        </Grid>
        <!-- The app: dialogs, drawers and sheets opened in here cover this area only. -->
        <arc:ArcOverlayHost>
            <TextBlock Text="Content" />
        </arc:ArcOverlayHost>
    </DockPanel>
</arc:ArcWindow>
```

Anything else the app keeps above every modal, such as a splash screen, goes beside the host in the same way. Popups,
menus, tooltips and toasts still open on the window's overlay layer. In the window from the preview, choose "Open a
dialog" to see it.

### Platforms

- **Windows**: the buttons are drawn by the app and act as the system's: a press on them is the system's minimize,
  maximize or close, and pointing at maximize opens Snap Layouts on Windows 11.
- **macOS**: the system keeps the traffic lights. Hide the buttons there, and leave about 80 px at the left of your
  header for the lights.
- **Linux**: the buttons work as on Windows, without Snap Layouts.

```xml
<arc:WindowCaptionButtons xmlns="https://github.com/avaloniaui"
                          xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
                          IsVisible="{OnPlatform True, macOS=False}" />
```

### One set per window

Show one set of caption buttons in a window. With two sets of maximize and close buttons, Windows' hit testing for the
caption buttons and Snap Layouts no longer knows which one is the window's. An `ArcWindow`'s `TitleBar` gives up its
buttons' roles while `CaptionButtons` is `System` or `None`, and a bar hidden with `IsTitleBarVisible="False"` is not
hit at all, so in those windows your set is the only one. A set in the `RightContent` of a bar with
`CaptionButtons="None"` drives the window as well.

### Previews

`IsPreview="True"` draws the buttons without driving the window: they minimize, maximize and close nothing, and the
system does not treat them as caption buttons. The mock-ups on this page use it, so they leave the gallery window
alone.

## API reference

<!-- api: WindowCaptionButtons -->

## Accessibility

- Each button has a tooltip and an accessible name in the app's language: Minimize, Maximize or Restore, and Close.
- As on system title bars, the buttons are not in the Tab order. The keyboard reaches the same commands through the
  system: Alt+Space opens the system menu on Windows, Alt+F4 closes the window and Windows+Up maximizes it.
- A button the window does not allow (a window that cannot be resized or minimized) is disabled and dims its glyph.

## Motion

- The buttons fade their fill and glyph color under the pointer; close turns to the danger color.
- With reduced motion or motion off the colors change at once.
- Minimizing, maximizing and snapping are the system's own animations.

## Responsive behavior

- The buttons are 30 px squares, 4 px apart (`ButtonSize` and `ButtonSpacing` in WindowCaptionButtonsToken): 98 px
  for all three. Keep that room at the end of your header at every width, and let the header's own content give way
  first.
- A hidden button takes no room, so the control is as wide as the buttons it shows.
- The buttons are too small to rely on as touch targets. Phones and browsers have no windows, so show them only in
  desktop apps.

## Related

- `ArcWindow`: the window to use them in, with `IsTitleBarVisible="False"`.
- `PageHeader` (TKCode.Arc.Blocks): `IsTitleBar="True"` makes a page header the window's title bar.
- `Dialog`, `Drawer` and `BottomSheet`: modals that cover the nearest `ArcOverlayHost`, or the whole window.

| Component | Choose it when |
|---|---|
| `ArcWindow` | The window keeps the Arc title bar, which draws the same buttons beside your controls |
| `PageHeader` | A page header with breadcrumbs, a title and actions is also the window's title bar |
| `Dialog` | A task interrupts the page; inside an `ArcOverlayHost` it leaves the title bar uncovered |
| `Button` | A panel or a card needs its own close button |
