---
title: Bottom sheet
summary: A sheet that rests at a peek or full height and follows your finger.
---

<Demo name="Detents" />

## When to use

A `BottomSheet` is a modal panel that rises from the bottom edge of the window and rests at one or more heights,
called detents. It suits the secondary task of a mobile-first screen: the person sees a useful peek of the content
first and expands the sheet only when they need more. It sits above the page with a scrim, so it is a focused step
inside a flow, not a second navigation layer.

- **Delivery and pickup options at checkout**: a shopper on a phone chooses a delivery slot or a pickup point for
  order 4417 from a peek that shows the cheapest option, then expands for the full list of carriers and times.
- **Filters on a product or order list**: a store app shows the active filter count in the peek and lets the person
  adjust filters at full height without leaving the list they are scrolling.
- **Share and export options**: a dashboard on a tablet offers "Copy link", "Send by email" and "Export as PDF" in a
  short sheet that the person can dismiss with a downward flick.
- **Map and media views**: a delivery-tracking map shows the courier's ETA in the peek, and the person drags the sheet
  up to read the stop list while the map stays visible behind it.
- **Trip or booking details**: an itinerary opens at a peek with the first day visible, so the person can see that
  the booking exists and expand it to read the rest.

## When not to use

- **Decisions that must interrupt the whole task**, such as deleting a project or cancelling a subscription: use
  `Dialog`, which is centred and keeps Cancel and the destructive action in one visible panel.
- **Side panels on wide layouts**, where the list should stay visible next to the details: use `Drawer` on the
  right or left edge.
- **Small content anchored to one control**, such as a column picker or a colour swatch grid: use `Popover`, which
  points at its trigger and does not dim the page.
- **A one-line hint for an icon button**: use `Tooltip`. A sheet is far too heavy for a label.
- **A list of commands**: use `DropdownMenu` or `ContextMenu`, which keep the commands next to the control that
  triggers them.

## Usage

A `BottomSheet` derives from `ModalOverlay`, which itself is a `ContentControl`. Its `Content` is the scrolling body,
`Title` and `Description` form the header, and `Actions` takes an optional footer. The control renders nothing where
it is declared; while `IsOpen` is true, its panel is shown on the window's overlay layer with a scrim, a focus trap,
Escape and outside-press dismissal and an exit that finishes before the panel is removed. The sheet adds the detents,
the drag and the scroll rules described below.

### Basic sheet

```xml
<arc:BottomSheet Title="Trip to Lisbon"
                 Description="3 nights, 12 to 15 June"
                 IsOpen="{Binding ShowTrip}">
    <StackPanel Spacing="12">
        <TextBlock Text="Fri · Arrive 14:05 at LIS, taxi to Alfama" />
        <TextBlock Text="Sat · Tram 28 and the castle" />
    </StackPanel>
</arc:BottomSheet>
```

### Anatomy

1. **Grabber**: a button with a 56 × 20 px hit area around a 36 × 5 px bar. The bar
   uses the border-strong color and turns to the muted text color on hover and while the sheet is dragged. Its
   accessible name is "Expand sheet" at a smaller detent and "Collapse sheet" at the tallest one.
2. **Header** (`Title`, `Description`): 20 px side padding, 8 px above and 16 px below (16 px sides when narrow). The
   title uses the large type size at medium weight in the foreground color. The description uses the small type size
   in the secondary text color, 2 px under the title, and is hidden when empty.
3. **Close button** (`ShowCloseButton`, `CloseButtonName`): a 32 px fully rounded button on the right of the header,
   with a 1 px border and the muted surface fill. It is shown by default.
4. **Header rule**: a 1 px line in the border color under the header. It appears only once the content has scrolled
   under the header (`:scrolled`).
5. **Body** (`Content`): a scrolling area with 20 px side padding, 4 px above and 24 px below (16 px sides when
   narrow). Its scroll bar appears only at the tallest detent. Horizontal scrolling is disabled.
6. **Actions** (`Actions`): an optional footer inside the scrolling body, aligned to the right and 20 px below the
   content. It is hidden when `Actions` is null.
7. **Surface**: the surface-raised background with a 1 px border on its top and side edges and no bottom edge, the
   surface radius on its top corners and the floating shadow. It is at most 576 px wide (36 rem), centred, and extends
   160 px below the window edge so an upward stretch never shows a gap.
8. **Scrim**: a dimming layer behind the sheet with the sheet's own scrim color (light and dark variants). The scrim is
   not blurred; its opacity follows the sheet position.

### Choosing detents

`Detents` lists the resting heights as fractions of the window height. The default is 0.45 and 0.92, which gives a
peek and a nearly full view. Values are sorted, duplicates are removed, values above 1 are clamped to 1, and zero or
negative values are ignored. A single value creates a fixed-height sheet. Use a peek height that shows the first
useful row completely, because that peek is what the person decides from. Keep the tallest detent below the full
window height when the page behind the sheet still has context to show, and use 0.92 or more only for full-screen
reading such as an itinerary or a media viewer.

`BottomSheetDetents.Parse` reads a comma or space separated string, so the list can come from configuration or a
resource. `SnapPoints` is an alias of `Detents` for code that uses that name. `EffectiveDetents` returns the sorted
values in use, which is useful for logging and tests.

<Demo name="ThreeDetents" />

```csharp
share.Detents = BottomSheetDetents.Parse("0.3, 0.6, 0.95");
share.InitialDetentIndex = 1;
```

Three detents suit a share or export sheet: a small peek for quick actions, a middle stop for a short list and a full
stop for a preview. Changing `Detents` while the sheet is open keeps the same index, clamped to the new list, so a
shorter list can move the sheet to a lower stop; the sheet stays open.

### Opening and moving between detents

`InitialDetentIndex` chooses the detent the sheet opens at. The default is 0, the smallest. The index refers to the
sorted list, and it is clamped to the list. `DetentIndex` reports the detent the sheet rests on; it is read-only, so
change it through `SnapTo`, `Expand` and `Collapse`.

`OpenCommand` opens the sheet from a button without setting `IsOpen`. `Expand` moves the sheet to its tallest detent,
`Collapse` to its smallest, and `SnapTo` to any index. `SnapTo` does nothing while the sheet is closed. `DetentChanged`
is raised each time the sheet comes to rest on another detent; it is not raised during a drag.

```csharp
openTrip.Command = trip.OpenCommand;

trip.DetentChanged += (_, e) => detent.Text = $"Resting at {e.Fraction:P0}";
trip.Expand();
```

### Long content

The content is not virtualized: every item in the body is created when the sheet opens. Keep long lists paged, or
put a control that virtualizes its own items, such as a `ListBox`, inside the sheet. Below the tallest detent the
content does not scroll. A wheel scroll down, a touch drag on the content, a Tab into content below the fold or a
scroll key opens the sheet the rest of the way first. Collapsing the sheet returns the content to its top, so the
peek always shows the start of the content.

```xml
<arc:BottomSheet Title="Itinerary" IsOpen="{Binding ShowItinerary}">
    <ItemsControl ItemsSource="{Binding Items}" />
</arc:BottomSheet>
```

### Closing

`Closed` reports why the sheet closed through `OverlayClosedEventArgs.Reason`. A fling or a drag down past the
smallest detent reports `Gesture`, Escape reports `Escape`, a press on the scrim reports `PressOutside`, the close
button reports `CloseButton`, and a button that sets a result reports `Action`. `Closing` is raised before a user
dismissal and can be cancelled with `e.Cancel`.

```csharp
trip.Closed += (_, e) => detent.Text = $"Closed by {e.Reason}";
```

### Drag, flick and rubber band

The whole header is the drag handle, so a press anywhere above the content can start a drag. A touch drag on the
content moves the sheet until it is fully open; after that the content scrolls, and a pull down from the top of the
content moves the sheet again. The sheet follows the pointer one to one. Above the tallest detent it stretches with a
rubber band of at most 120 px. On release, the sheet projects 0.2 seconds of its velocity and settles on the nearest
detent, or closes when the projection lands below the smallest one. A release faster than 320 px/s always moves the
sheet at least one detent, so a quick flick is never ignored. A click on the grabber toggles between the smallest and
the tallest detent.

Drag is disabled under reduced motion and when motion is off. Use the grabber, the keyboard or `SnapTo` in those
cases, and keep the close button available for every user.

### Focus and dismissal

Focus starts on the sheet itself, so a screen reader announces its title first. While the sheet is the topmost modal,
Tab and Shift+Tab stay inside it, and focus returns to the control that opened it when the sheet closes. Escape closes
the sheet when `CloseOnEscape` is true (the default), and a press on the scrim closes it when `CloseOnOutsidePress` is
true. Set `IsModal` to false when the page must stay interactive: the scrim and the focus trap are then removed. When
sheets are stacked, only the topmost one answers Escape.

### Binding to a view model

`IsOpen` is two-way by default, so closing the sheet from a gesture or Escape writes `false` back to the view model.
`DetentIndex` is read-only, so the view model receives detent changes through the `DetentChanged` event and stores
them in its own property. Compiled bindings need `x:DataType` on the view; the examples leave it out for brevity.

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public interface IOrderService
{
    Task SetDeliverySlotAsync(string slotId);
}

public partial class DeliveryViewModel : ObservableObject
{
    private readonly IOrderService _orders;

    public DeliveryViewModel(IOrderService orders) => _orders = orders;

    [ObservableProperty]
    private bool _showOptions;

    [ObservableProperty]
    private int _restingDetent;

    [ObservableProperty]
    private string _selectedSlotId = string.Empty;

    [RelayCommand]
    private async Task ConfirmSlotAsync()
    {
        await _orders.SetDeliverySlotAsync(SelectedSlotId);
        ShowOptions = false;
    }
}
```

```xml
<arc:BottomSheet Title="Delivery options"
                 Description="Order 4417 ships from Northwind Berlin."
                 Detents="0.5, 0.9"
                 IsOpen="{Binding ShowOptions}">
    <StackPanel Spacing="12">
        <RadioButton GroupName="slot" Content="Tomorrow, 08:00 to 12:00 (free)" />
        <RadioButton GroupName="slot" Content="Tomorrow, 17:00 to 20:00 (4.90 EUR)" />
    </StackPanel>
    <arc:BottomSheet.Actions>
        <arc:Button Content="Confirm delivery slot" Command="{Binding ConfirmSlotCommand}" />
    </arc:BottomSheet.Actions>
</arc:BottomSheet>
```

```csharp
delivery.Closed += (_, e) => log.Text = $"Options closed by {e.Reason}";
delivery.DetentChanged += (_, e) => viewModel.RestingDetent = e.Index;
```

### Example: delivery options at mobile checkout

A checkout screen on a phone shows the order total and the chosen slot in the page. Tapping "Change" opens the sheet at
a 50% peek, which lists the two cheapest slots without hiding the order total above it. The second option is a
stop at 90% for the full carrier list. The header names the order, so the person sees which order the choice changes,
and the single action at the bottom states the outcome. The sheet closes after the slot is saved; the view model sets
`ShowOptions` to `false` rather than the button setting a result, because the save may fail and the sheet must then
stay open with its message.

```xml
<arc:BottomSheet Title="Delivery options"
                 Description="Order 4417 ships from Northwind Berlin."
                 Detents="0.5, 0.9"
                 IsOpen="{Binding ShowOptions}">
    <StackPanel Spacing="12">
        <RadioButton GroupName="slot"
                     Content="Tomorrow, 08:00 to 12:00 (free)"
                     IsChecked="True" />
        <RadioButton GroupName="slot" Content="Tomorrow, 17:00 to 20:00 (4.90 EUR)" />
        <RadioButton GroupName="slot" Content="Thursday, 09:00 to 13:00 (free)" />
        <TextBlock Text="Carrier: Northwind Courier. Signature required above 500 EUR."
                   TextWrapping="Wrap"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    </StackPanel>
    <arc:BottomSheet.Actions>
        <arc:Button Content="Save delivery slot" Command="{Binding ConfirmSlotCommand}" />
    </arc:BottomSheet.Actions>
</arc:BottomSheet>
```

### Content guidelines

Write the header so a person knows what the sheet is for before they expand it. The title is a short noun phrase in
sentence case. The description names the scope, with numbers as digits and dates in a short form. The actions are
verb and object, and the peek should not depend on a hint that is only visible after expanding.

| Instead of | Write |
|---|---|
| Options | Delivery options |
| Click here to see more | Show all 12 stops |
| Swipe up for details | Expand the sheet, or use the grabber to expand it |
| OK | Save delivery slot |
| Stuff you can do | Share itinerary |
| 3 nights from 12th June | 3 nights, 12 to 15 June |

Do not repeat the title in the first line of the body; the body should add the information the header does not contain.

### Best practices

**Do**

- Put the decision or the most useful row in the peek, so the sheet earns the expansion. The person decides from the
  peek, and content they must scroll to find is often skipped.
- Keep the close button visible. Gestures are not discoverable on every input device, and a visible control is the
  dismissal path for keyboard and assistive technology users.
- Give every sheet a `Title`. It is the panel's accessible name; without it the sheet is announced as an unnamed
  window.
- Use `DetentChanged` to store the resting position when the sheet's layout depends on it, such as a map that pans when
  the sheet grows.

**Avoid**

- Stacking sheets on top of each other. Each one adds a scrim and a focus trap, and only the topmost answers Escape,
  so the person has to close several layers to return to the page.
- Long unpaged lists in the body. The content is not virtualized, so every row is created when the sheet opens.
- Relying on a drag as the only way to close. Drag is disabled under reduced motion, and a button or the keyboard
  must always remain.
- Horizontal scrolling inside the body. Horizontal scrolling is disabled, so wide content is clipped rather than
  scrolled.
- Using a sheet for a destructive confirmation. A `Dialog` keeps the consequence and the buttons in one centred panel
  that people read before they act.

### Common questions

**Why does the sheet open at its smallest detent?** `InitialDetentIndex` defaults to 0, which is the smallest detent
in the sorted list. Set it to the index of the detent you want, or call `Expand` after opening.

**How do I open the sheet from a button without code-behind?** Bind the button's command to the sheet's `OpenCommand`
through its name, for example `Command="{Binding #TripSheet.OpenCommand}"`, or bind `IsOpen` in the view model.

**Why does my content not scroll at the peek?** Below the tallest detent the content is fixed. A wheel scroll down, a
touch drag on the content, a Tab into content below the fold or a scroll key opens the sheet first. This keeps the peek
from scrolling away from the header.

**How do I stop a tap outside the sheet from closing it?** Set `CloseOnOutsidePress` to `False`. To allow the press only
at some times, keep it `True` and cancel the `Closing` event while `e.Reason` is `PressOutside` and work is pending.

**How do I rename the close button for another language?** Set `CloseButtonName`. The default is "Close", and the
name is read by assistive technology.

**Can I read the detent in XAML?** `DetentIndex` is read-only. Subscribe to `DetentChanged` in code or in the view
model and bind the value you store.

## API reference

<!-- api: BottomSheet -->

<!-- api: BottomSheetDetents -->

## Accessibility

- The sheet takes its name from `Title` and its help text from `Description`. Focus starts on the sheet itself.
- The grabber is a button that exposes its expanded state. Its name is "Expand sheet" or "Collapse sheet".
- On the grabber, ArrowUp and ArrowDown move one detent, Home and End go to the highest and lowest detents, and Enter or
  Space toggles between the lowest and highest.
- Changes of detent are announced politely: "Sheet expanded", "Sheet collapsed" or the new height as a percentage.
  Escape closes the sheet. Its close button is named "Close" by default; set `CloseButtonName` to change it.
- The panel reports itself to assistive technology as a window, so it is announced as a separate surface.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move through the controls in the sheet; focus stays inside while the sheet is topmost. Tab into content below the fold expands the sheet |
| `ArrowUp` / `ArrowDown` (grabber) | Move one detent up or down |
| `Home` / `End` (grabber) | Go to the tallest detent or to the smallest detent |
| `Enter` or `Space` (grabber) | Toggle between the smallest and the tallest detent |
| `ArrowDown`, `PageDown` or `Space` (sheet or its content, focused) | Expand to the tallest detent first; once expanded, scroll the content down |
| `ArrowUp`, `PageUp` or `Shift+Space` (sheet or its content, focused) | Scroll the content up |
| `Escape` | Close the sheet, unless `CloseOnEscape` is false or a `Closing` handler cancels it |

Scroll steps are 48 px for the arrow keys and 85% of the visible height for the page keys.

## Motion

- One motion value drives the sheet. A drag follows the pointer, and past the tallest detent the sheet stretches with
  a rubber band of up to 120 px.
- On release the sheet settles on the nearest detent, or closes when it is dragged down. A flick faster than 320 px/s
  always moves it at least one detent.
- The dim behind the sheet depends on its position. At the smallest detent it keeps 78% of the full dim, and it fades
  as the sheet moves down.
- Reduced motion makes detents jump and fades the sheet in and out. Off shows or removes the sheet at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Opening | Slides up from below the edge on the smooth spring to its detent | Shown at its detent with a short fade | Immediate |
| Closing | Springs down with a 0.3 s visual duration and keeps the release velocity | Short fade out | Immediate |
| Moving between detents | Smooth spring to the new detent | Jumps to the new detent | Immediate |
| Dragging | Follows the pointer one to one; rubber band past the tallest detent | Drag disabled | Drag disabled |
| Scrim | Follows the sheet position | Follows the sheet position | Immediate |

## Related

- `Dialog` for a decision that must be answered before anything else.
- `Drawer` for a side panel on wide layouts.
- `Popover` for small content anchored to a control.

| Component | Choose it when |
|---|---|
| `Dialog` | The person must confirm, cancel or enter a short value before the task continues |
| `Drawer` | Filters, settings or record details should sit beside the page on a wide layout |
| `Popover` | The content is small, anchored to one control and does not need a scrim |
| `Tooltip` | A single line explains an icon button |
| `Alert` | A message stays in the page until it is resolved |
| `Toast` | A short result, such as "Delivery slot saved", should disappear on its own |
