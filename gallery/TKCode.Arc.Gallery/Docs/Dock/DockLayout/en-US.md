---
title: Dock layout
summary: "An IDE-style layout of tabbed panes in nested, resizable splits around a document area: drag panes to dock them, float them in their own windows, auto-hide tools to the edges, bind documents to a collection, switch panes from the keyboard, and save layouts to JSON."
---

<Demo name="Preview" />

## When to use

- IDEs, editors, dashboards, trading terminals and design tools: several long-lived work areas side by side that people arrange for themselves.
- Documents that open and close in a central area while tools stay around it.
- Layouts people expect to find again the next time: save them to JSON and load them back.

## When not to use

- Two or three fixed panes nobody rearranges: use `ResizablePanels`.
- Switching between views at one level: use `Tabs`.
- An application frame with a sidebar and a main area: use the App shell blocks.
- Content that comes and goes over the page: use `Popover`, `Drawer` or `Dialog`.

## Usage

Add the package and register it in `UseArc`:

```csharp
AppBuilder.Configure<App>()
    .UseArc(arc => arc.UseArcControls().UseArcDock());
```

Write the layout tree as the content of a `DockLayout`. A `DockSplit` lays its children side by side (`Horizontal`) or one above the other (`Vertical`); each child's `Size` is its share. A `DockGroup` is one card of tabs, one tab per `DockPane`. The `DockDocumentArea` holds the documents: panes without a `PreferredSide`. Every pane needs an `Id` that is unique in the layout; saved layouts refer to panes by it.

```xml
<arc:DockLayout>
    <arc:DockSplit Orientation="Horizontal">
        <arc:DockGroup Size="22">
            <arc:DockPane Id="explorer" Title="Explorer" Icon="{x:Static arc:ArcIcons.Folder}" PreferredSide="Left">
                <TextBlock Text="Explorer" />
            </arc:DockPane>
        </arc:DockGroup>
        <arc:DockSplit Orientation="Vertical" Size="78">
            <arc:DockDocumentArea Size="70">
                <arc:DockGroup>
                    <arc:DockPane Id="doc:readme" Title="README.md">
                        <TextBlock Text="Readme" />
                    </arc:DockPane>
                </arc:DockGroup>
            </arc:DockDocumentArea>
            <arc:DockGroup Size="30">
                <arc:DockPane Id="terminal" Title="Terminal" PreferredSide="Bottom">
                    <TextBlock Text="Terminal" />
                </arc:DockPane>
            </arc:DockGroup>
        </arc:DockSplit>
    </arc:DockSplit>
</arc:DockLayout>
```

The pane is its own content host. When it moves to another group it is the same instance under a new parent, so its content keeps its scroll position, input and selection. Only a group's active pane is in the visual tree.

### Tabs and splits

Splits nest as deep as you need, and the document area can hold a split of document groups for side-by-side editors. Drag the gap between two cards to resize them; the dividers behave like `ResizablePanels` (Arrow keys, Home, End and Enter on a focused divider). The model keeps itself tidy: a group whose last pane leaves is removed, a split left with one child is replaced by that child, and a split inside a split of the same orientation merges into it. The document area stays even when it is empty and then shows `EmptyDocumentContent`.

<Demo name="TabsAndSplits" />

Document tabs have a close button that shows on hover, on the selected tab and on the keyboard-focused tab; `IsModified` shows a dot instead until the pointer is over the tab. Tool tabs have no close button; close them from the tab menu or the keyboard. When the tabs do not fit, the strip scrolls with the wheel and a chevron button lists every tab.

### Moving panes

Right-click a tab, or press Shift+F10, for its menu: Close, Close others and Close all (documents), Move to › New group left, right, above or below, and Dock to › Left, Right, Top or Bottom (tools; docks along the whole layout). In code, `Move` takes a `DockTarget`:

```csharp
Layout.Move(terminal, DockTarget.Split(editorGroup, DockPosition.Bottom)); // a new group below, half of the group
Layout.Move(outline, DockTarget.Edge(DockPosition.Right));                // along the right edge, a quarter of the layout
Layout.Move(search, DockTarget.Into(explorerGroup, 0));                   // first tab of a group
Layout.Move(problems, DockTarget.DocumentArea(DockPosition.Left));        // beside the whole document area
```

Documents stay inside the document area, and tools enter it only when `CanDockAsDocument` is true; a move those rules refuse returns false and changes nothing. `Open` adds a pane: without a target a document joins the most recently active document group, and a tool joins a group of tools with the same `PreferredSide` or gets a new group along that edge, `PreferredSize` pixels wide or high. `Close` raises `PaneClosing`, which can cancel, then `PaneClosed`; the group selects the tab to the right (or left) and an emptied group goes away.

### Drag to dock

Drag a tab along its strip to reorder it; the other tabs make room. Drag it more than 16px above or below the strip, or out of its group, and it becomes a dock drag: a ghost of the tab follows the pointer, a compass shows over the group under the pointer, and a preview glides to where the pane will land.

- The compass's middle button adds the pane to that group as a tab; its four arrows split the group, the new group taking half of it.
- Over the document area, a second ring of four buttons docks beside the whole document area.
- The buttons at the middle of the layout's four edges dock along the whole layout, taking a quarter of it.
- Over a tab strip, a line shows where the tab goes among the others.
- Away from the buttons, the part of the group under the pointer decides: within a quarter of an edge splits that side, the middle joins as a tab.
- Drag the empty part of a strip to move the whole group. Escape cancels and puts the pane back.

The rules of `Move` apply: documents stay in the document area, tools enter it only with `CanDockAsDocument`, a group cannot drop onto itself, and nothing drags while the layout `IsLocked`. A button that does not accept the pane is dimmed. Set `ShowDockIndicators` to false for a quieter drag without the compass: the pointer's place in a group, or near the layout's edge, still picks the side and only the preview shows.

<Demo name="DragToDock" />

### Floating

Drop a tab outside the layout, choose Float in its menu, or call `Float`, and the pane moves to a floating window near where it was. The window belongs to the layout's window: it has no taskbar button, stays above its owner and follows its theme and motion settings. It can hold several panes and groups, like the layout. To dock it back, drag its tab or title bar onto a compass button or a tab strip (the window follows the pointer while the layout shows its compass), double-click its title bar, or choose Dock in the tab menu. `Dock` returns a pane to where it was before: its old group, or beside the old neighbour with the old size when the group has gone. Closing the window closes its panes that can close and docks the others back.

<Demo name="Floating" />

`FloatingMode` chooses how panes float. `Auto` uses real windows in a desktop window of a classic desktop application, and floating cards inside the layout anywhere else, such as the browser. `Window` and `Overlay` force one of them, and `Disabled` keeps every pane docked. A floating card has the popup surface and shadow, moves by its title bar, resizes from its bottom-right corner, stays inside the layout and has Dock and Close buttons. The floats are part of the model (`Workspace.Floats`, each a `DockFloat` with its tree and `Bounds`): adding one in code opens its window, and saved layouts bring them back, pulled onto a screen when the monitor they were on is gone.

```csharp
Layout.Float(properties);                       // a window (or card) near the pane
Layout.Dock(properties);                        // back where it came from
Layout.FloatingMode = DockFloatingMode.Overlay; // cards inside the layout, also on the desktop
```

On Linux under Wayland an application cannot place its windows: a pane dropped outside opens where the system puts it, the title bar moves the window with the system's move, and floating windows dock back from the menu or by a double click on the title bar.

### Auto hide

A tool can leave its group for a strip on the edge of the layout nearest that group: right-click its tab and choose Auto hide, press Shift+Escape inside it, or call `AutoHide`. The strip shows a button with the pane's icon and title, turned on the left and right edges. Clicking the button, or pressing Enter on it, slides the pane out over the content as a card as wide or high as the pane was docked (at most 70% of the layout), and focus moves into it. A click outside, Escape, Shift+Escape or focus moving elsewhere slides it back. Drag the card's inner edge to resize it. The pin in its header, the menu's Pin or `Pin` puts the pane back into its group, or into a new group along that edge when the group has gone.

<Demo name="AutoHide" />

Auto-hidden panes are saved with the layout. In XAML, put them in the workspace's edge lists (`LeftHidden`, `RightHidden`, `TopHidden`, `BottomHidden`); a pane that was never docked slides out at its `PreferredSize`:

```xml
<arc:DockLayout>
    <arc:DockLayout.Workspace>
        <arc:DockWorkspace>
            <arc:DockSplit Orientation="Horizontal">
                <arc:DockDocumentArea Size="75">
                    <arc:DockGroup>
                        <arc:DockPane Id="doc:readme" Title="README.md">
                            <TextBlock Text="Readme" />
                        </arc:DockPane>
                    </arc:DockGroup>
                </arc:DockDocumentArea>
                <arc:DockGroup Size="25">
                    <arc:DockPane Id="properties" Title="Properties" PreferredSide="Right">
                        <TextBlock Text="Properties" />
                    </arc:DockPane>
                </arc:DockGroup>
            </arc:DockSplit>
            <arc:DockWorkspace.RightHidden>
                <arc:DockPane Id="outline" Title="Outline" PreferredSide="Right" PreferredSize="220">
                    <TextBlock Text="Outline" />
                </arc:DockPane>
            </arc:DockWorkspace.RightHidden>
        </arc:DockWorkspace>
    </arc:DockLayout.Workspace>
</arc:DockLayout>
```

While a pane is on a strip its `State` is `AutoHidden` and `IsPinned` is false. Documents never auto-hide, and `CanAutoHide` false keeps a tool docked. Floating panes do not auto-hide (dock them first), but an auto-hidden pane can float; `Dock` on an auto-hidden pane pins it. The slid-out card does not start drags: pin the pane, or use the menu's Float and Dock to.

### Maximize and the pane switcher

Double-click the empty part of a group's tab strip, or choose Maximize in its menu, and the group fills the whole layout until you do it again (the menu then says Restore) or activate a pane of another group; the layout itself does not change. Ctrl+Tab opens the pane switcher: every pane, most recently used first, with the previous one highlighted. Press Tab again with Ctrl held to move on (Shift+Tab goes back) and release Ctrl to switch to it. `ToggleMaximize`, `ToggleMaximizeCommand`, `ShowPaneSwitcherCommand` and `AutoHideActiveCommand` do the same from code or an application menu.

### Save and restore

`SaveLayout` writes the layout as JSON: the tree with each node's share, the active panes, and the panes on auto-hide strips and in floating windows. Panes are stored by `Id` only; `LoadLayout` finds them among the panes the layout has had and asks `PaneResolver` for the others. Unknown ids are skipped, and panes the saved layout does not mention go back by their `PreferredSide`. `LoadLayout` returns false and keeps the layout when the text is not a layout or comes from a newer version. Save on `LayoutChanged` to keep the layout automatically.

<Demo name="SaveAndRestore" />

```csharp
Layout.LayoutChanged += (_, _) => settings.DockLayout = Layout.SaveLayout();
if (settings.DockLayout is { } saved)
{
    Layout.LoadLayout(saved);
}
```

### Documents from a collection

Bind `DocumentsSource` (and `ToolsSource`) to a collection and every item becomes a pane whose `DataContext` is the item. `DocumentTemplate` (`ToolTemplate`) builds the pane: a template that makes a `DockPane` is used as it is, anything else is put into a new pane. A pane without an `Id` or `Title` takes them from the item: from `IDockItem.DockId` and `DockTitle` when the item implements `IDockItem`, otherwise from `ToString()`. Adding an item opens its pane (documents join the most recent document group, tools go by their `PreferredSide`, the left when they have none) and removing it closes the pane without `PaneClosing`. When the user closes a tab, `PaneClosing` comes first; if no handler cancels and the collection is a writable list, the item is removed from it. Saved layouts restore these panes by id.

<Demo name="Mvvm" />

```xml
<arc:DockLayout DocumentsSource="{Binding OpenFiles}">
    <arc:DockLayout.DocumentTemplate>
        <DataTemplate>
            <arc:DockPane Id="{Binding Path}" Title="{Binding Name}" IsModified="{Binding IsDirty}">
                <TextBlock Text="{Binding Text}" />
            </arc:DockPane>
        </DataTemplate>
    </arc:DockLayout.DocumentTemplate>
    <arc:DockDocumentArea />
</arc:DockLayout>
```

The active document is `ActivePane.DataContext`; `ActivePaneChanged` tells you when it changes.

### Locking

`IsLocked` keeps every pane where it is: tabs cannot close, move, float, auto-hide or be dragged, and the menu's commands are off. The dividers still resize, auto-hidden panes still slide out, and a group can still be maximized.

<Demo name="Locked" />

## API reference

<!-- api: DockLayout -->

<!-- api: DockPane -->

<!-- api: DockSplit -->

<!-- api: DockGroup -->

<!-- api: DockDocumentArea -->

## Accessibility

- The layout reads as a group named "Dock layout" unless you name it with `AutomationProperties.Name`. Each group reads as a tab list and each tab as a tab item named after its pane; a modified document adds "modified". A document tab's close button is named "Close README.md".
- Changes are announced politely: "Explorer docked to the right of Terminal", "Search docked to the bottom edge", "README.md, tab 2 of 2", "Terminal moved to a new window", "README.md closed".
- The compass buttons are named "Dock left", "Dock right", "Dock top", "Dock bottom" and "Dock as tab".
- A strip button reads as a button named after its pane, with the help text "Auto hidden, press Enter to show"; the slid-out card is named after the pane, and the pane switcher reads as a list. Auto hiding is announced too: "Explorer auto hidden".
- Everything a pointer does with panes has a menu command (Move to, Dock to, Float, Dock), and the menu opens from the keyboard.

### Keyboard

| Key | Action |
| --- | --- |
| Left / Right, Home / End | On a tab: move to the previous, next, first or last tab of the group and show its pane. |
| Ctrl / ⌘ + Shift + Left / Right | On a tab: move the tab one place left or right. |
| Delete | On a tab: close its pane (when it can close). |
| Ctrl + F4, ⌘ + W | On a tab or inside a pane: close the active pane. |
| Shift + F10, Menu key | On a tab: open its menu with the first row highlighted. |
| Arrow keys, Home, End, Enter | On a divider: resize, as in `ResizablePanels`. |
| Ctrl + Tab, Ctrl + Shift + Tab | Anywhere in the layout: open the pane switcher on the previous (or least recent) pane; Tab moves on while Ctrl is held, and releasing Ctrl switches. |
| Up / Down, Enter, Escape | In the pane switcher: move the highlight, switch, or close without switching. |
| Shift + Escape | Inside a tool: auto-hide it. Inside a slid-out pane: slide it back. |
| Enter, Space | On a strip button: slide its pane out, or back. |
| Escape | Inside a slid-out pane: slide it back; focus returns to its strip button. |
| Escape | While dragging a pane: cancel and put it back. |

When a pane closes while it has focus, focus moves to the tab of the pane that takes its place.

## Motion

- The selected tab's background glides between tabs on the same spring as `Tabs`; it takes the accent while the group holds the active pane.
- Tab text and the action buttons ease their colors over the standard duration.
- Dividers follow the pointer and settle as in `ResizablePanels`.
- While a tab is dragged along its strip, the other tabs make room on a snappy spring, and the dropped tab springs into its slot.
- The drop preview glides between targets on a snappy spring; the compass fades in over 120ms while growing from 92%.
- A floating card enters like a popup, fading in from 97%. The ghost and a floating window follow the pointer directly.
- An auto-hidden pane slides out on the smooth spring and back in 180ms.
- Panes moved by a command change place at once; nothing loops while the layout is at rest.
- Reduced motion: the selected background, the tabs and the preview jump instead of gliding; the compass and the floating card only fade, and an auto-hidden pane fades in and out in 120ms instead of sliding.

## Responsive behavior

- `DockLayout` does not change at a width: it has no breakpoint and no compact layout. Its splits share the width they
  are given. Each pane resists below its `MinSize` (120 px by default), and when the layout is narrower than all the
  minimums together they scale down and the panes get narrow.
- It measures its own width, not the window's, so a layout in a phone preview or a narrow side panel is squeezed the
  same way as on a phone.
- A floating window is at least 320 px wide and 240 px high, and an auto-hidden pane slides out over the content at most
  70% of the layout's size.
- When the tabs do not fit, the strip scrolls with the wheel and a chevron lists every tab. A document tab's close
  button shows on hover and on the selected tab only, and the tab menu opens with a right click or Shift+F10, so do not
  rely on closing an unselected tab by touch.
- On a phone, show one view at a time with `Tabs`, which the docs recommend for switching between views at one level,
  instead of a split layout.

## Related

- `ResizablePanels`: a fixed row or column of resizable panes.
- `Tabs`: switch between views at one level.
- `ArcWindow`: a desktop window with an Arc title bar.
