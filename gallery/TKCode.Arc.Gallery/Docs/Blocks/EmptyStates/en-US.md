---
title: Empty states
summary: Four empty states in one illustration whose shapes morph between scenes as you switch tabs.
---

<Demo name="Preview" />

## When to use

An empty state tells people why a view has no content and what they can do next. `EmptyStates` covers the four
situations an application meets most often (a search with no matches, a lost connection, a cleared queue and a missing
page) in one block, with a drawing and copy for each. Place it where the empty view is a whole page or panel and the
next step may take a moment, because the block shows the work in progress and the result in the same place.

- **A filtered invoice list with no matches**: a billing console where the status and month filters hide every
  invoice. The `NoResults` scene names the cause in the title and offers one action, such as showing the paid invoices.
- **A connection that drops in an offline-capable editor**: a field app keeps edits on the device. The `Offline` scene
  says the edits are safe and offers a retry, which shows the connection attempt and then the result.
- **A ticket or approval queue that is clear**: a support inbox or a review queue with nothing left to read. The
  `CaughtUp` scene confirms the state, and its action checks again for new items.
- **A link to a page that moved**: a help centre or a CRM with saved links to projects that were archived. The
  `NotFound` scene explains the move and offers a search for the new location.
- **A design review or onboarding guide**: a page that shows the four states side by side so the team can check their
  copy and drawings in one place.

## When not to use

- **One empty list with an icon and one button**: use `EmptyState`. It is a single state with no tabs and no phases, so
  it is lighter on a dense page.
- **Content that is still loading**: use `Skeleton`. It shows the shape of the content that will arrive; an empty state
  would tell people there is nothing when something is on its way.
- **An error on a page that already shows content**: use `Alert`, which sits beside the content and leaves it readable.
- **A confirmation after a save or a send**: use `Toast`, which says what happened and goes away on its own.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:EmptyStates Label="Workspace" />
</UserControl>
```

`EmptyStates` derives from `TemplatedControl`. Its tabs are `EmptyStatesItem` objects, its drawing is a control that
animates between scenes, and its action is a button. The block does no work of its own. The app
handles `ActionInvoked` (or runs `ActionCommand`), does the work, and moves `Phase` to `Loading` and then `Done` or
back to `Idle`. The drawing and the copy follow `Phase` and the selected tab.

### Anatomy

1. **Tab row** (`Items`): one tab per `EmptyStatesItem`, showing its `Header`. The tabs sit in a 30 px track with a 3 px
   inset and a 12 px corner radius. The tabs share the row equally up to 460 px, and a tab never gets narrower than its
   label. With one item the row is hidden. The selected tab has the surface color, a 1 px border and a resting shadow.
2. **Drawing** (`Scene`): a 320 by 216 logical canvas scaled to at most 360 px wide. Ten shapes and the 1.5 px strokes
   do not scale with the drawing. The colors come from the theme tokens: the surface and foreground colors for the main
   shape, the muted text color for the outlines and the strong border color for the supporting shapes.
3. **Title** (`Title`, `DoneTitle`): the large text size, medium weight. At 560 px wide or more the block uses the
   extra-large size.
4. **Description** (`Description`, `DoneDescription`, `LoadingDescription`): secondary text in the small size, up to
   about 350 px wide, so long sentences wrap into more lines.
5. **Action button** (`ActionLabel`, `DoneActionLabel`): a small primary button. While `Phase` is `Loading` it shows a
   spinner and ignores presses.
6. **Card**: a 1 px border, a 20 px corner radius and the surface color. The padding grows with the block's width, from
   16 to 40 px on the sides and 16 to 48 px at the top and bottom. The gaps between the parts are 20 to 32 px.

### Scenes and copy

Each `EmptyStatesItem` is one tab. `Scene` picks the drawing, and the copy properties set the text for each phase. Text
you leave out uses Arc's copy for that scene, so a tab can set only its header and action label.

| Scene | Drawing | Default header | Default title |
|---|---|---|---|
| `NoResults` | A magnifier over empty rows | No results | No results for “Q3 roadmap” |
| `Offline` | A cloud joined to a device by a broken line | Offline | You are offline |
| `CaughtUp` | A stacked inbox with a check mark | Caught up | All caught up |
| `NotFound` | A folded map whose route ends in a cross | Not found | Page not found |

Set `Header` on every tab when there are several. The header is the only label in the tab row, and people use it to
decide which state they are looking at. Choose the scene by the cause, not by the look you like: `NoResults` after a
filter, `Offline` when the connection is lost, `NotFound` for a broken link, and `CaughtUp` when nothing is left to do.

<Demo name="CustomItems" />

```xml
<arc:EmptyStates>
    <arc:EmptyStatesItem Scene="Offline"
                         Title="Sync is paused"
                         ActionLabel="Resume sync" />
    <arc:EmptyStatesItem Scene="NotFound" />
</arc:EmptyStates>
```

`Items` is an `AvaloniaList<EmptyStatesItem>`, not an `ItemsSource`. Add the tabs in XAML or with `Items.Add` and
`Items.Remove` in code. The block keeps the selection on an item of the list: when the selected item is removed, the
selection moves to a valid index, and a selection made before the items arrive is kept.

### Actions and phases

Pressing the action button raises `ActionInvoked` with the selected item and the phase the button was pressed in, then
runs `ActionCommand` with the same event arguments. Presses are ignored while the phase is `Loading`, so the app never
receives a second press for the same work.

| Phase | Title shown | Description shown | Button |
|---|---|---|---|
| `Idle` | `Title` (or the scene's copy) | `Description` | `ActionLabel` |
| `Loading` | The title of the phase it started from | `LoadingDescription` if set, otherwise the current description | Spinner, presses ignored |
| `Done` | `DoneTitle` | `DoneDescription` | `DoneActionLabel` |

Set `Phase` to `Loading` while the work runs. A `Loading` that started from `Done` keeps the done title, so the copy does
not jump back to the empty state in the middle of an action. Set `Done` when the work succeeded, or `Idle` when it
failed. Use `Done` to show a result the person can act on (for example, "Hide paid invoices") and `Idle` to offer the
action again.

<Demo name="AsyncRetry" />

```csharp
states.ActionInvoked += async (_, e) =>
{
    if (e.Phase == EmptyStatesPhase.Done)
    {
        states.Phase = EmptyStatesPhase.Idle;
        return;
    }
    states.Phase = EmptyStatesPhase.Loading;
    var ok = await sync.RetryAsync();
    states.Phase = ok ? EmptyStatesPhase.Done : EmptyStatesPhase.Idle;
};
```

To change the copy after a failed attempt, set the item's `Description` while the phase is still `Loading`. The new
text then appears together with the return to `Idle`. The demo does this: its first attempt fails, and the description
changes to a line that tells the person what to check.

Every scene has its own default `LoadingDescription`, such as "Reaching sync.northwind.example…" for `Offline`. The
`NoResults` scene has none, so its description stays on screen while the search runs. Set `LoadingDescription` to
an empty string to keep the description on any scene.

### Binding to a view model

`EmptyStates` has no items binding, so its tabs are fixed in XAML or code. Its state is bindable: `SelectedIndex`,
`SelectedItem` and `Phase` are two-way, and `ActionCommand` is one-way. The view model owns the phase and the work, and
the block shows it.

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TKCode.Arc;

public partial class InvoiceEmptyStateViewModel : ObservableObject
{
    private readonly IInvoiceService _invoices;

    public InvoiceEmptyStateViewModel(IInvoiceService invoices)
    {
        _invoices = invoices;
    }

    [ObservableProperty]
    private EmptyStatesPhase _phase = EmptyStatesPhase.Idle;

    [ObservableProperty]
    private int _selectedIndex;

    [RelayCommand]
    private async Task RunActionAsync(EmptyStatesActionEventArgs? args)
    {
        if (args is null)
        {
            return;
        }
        if (args.Phase == EmptyStatesPhase.Done)
        {
            Phase = EmptyStatesPhase.Idle;
            return;
        }
        Phase = EmptyStatesPhase.Loading;
        var result = await _invoices.RefreshAsync();
        Phase = result.IsSuccess ? EmptyStatesPhase.Done : EmptyStatesPhase.Idle;
    }
}
```

```xml
<arc:EmptyStates SelectedIndex="{Binding SelectedIndex, Mode=TwoWay}"
                 Phase="{Binding Phase, Mode=TwoWay}"
                 ActionCommand="{Binding RunActionCommand}"
                 Label="Invoices" />
```

- Two-way `Phase` means the block writes it back. Choosing another tab sets `Phase` to `Idle`, so the view model sees
  that change through its own `Phase` property and can drop any result it was holding.
- Compiled bindings need `x:DataType` on the view. The examples here leave it out for brevity.

### Narrow layouts

The block follows its own width, not the window's width. At 420 px or less the tabs get smaller text and tighter
padding. From 560 px the title uses the larger size. A label that does not fit keeps its width, and the tab row slides
to keep the selected tab in view.

<Demo name="Narrow" />

### Using motion with restraint

The morph between scenes is the block's signature, and it works best where the state change is the news: a filter was
cleared, a connection came back, a result arrived. Let the drawing carry that change and keep the copy short.

- Use the scene change for a state the person caused (a tab, a filter, a retry). A drawing that morphs when a
  background refresh runs every few seconds draws the eye away from the list it belongs to.
- Keep the `Loading` phase short. The drawing moves on each phase change, so a spinner that runs for minutes gives
  the person a moving picture and no information; show a progress indicator in the surrounding page for long work.
- Under reduced motion the drawing crossfades instead of morphing, so the change stays visible without travel. The
  mouse tilt is off in reduced motion and is disabled entirely when motion is off.

### Example: invoices with three empty states

A billing console's invoice page has three tabs for the states the team meets most often. The first tab is selected: a
month filter hides every invoice, so the title names the month and the action clears the paid-invoice filter. The
second tab shows the sync state, and the third shows reminders that are all scheduled. Each tab sets its own header,
so the tab row reads as three short labels. The copy names real objects (the month, the customer) because people
look for the cause in the title first.

![Three empty states on an invoice page with the no results tab selected](images/InvoicesEmptyStates.png)

```xml
<arc:EmptyStates Width="640"
                 Label="Invoices"
                 TabListLabel="Invoice empty states">
    <arc:EmptyStatesItem Scene="NoResults"
                         Header="Search"
                         Title="No invoices match “March 2026”"
                         Description="The status filter hides 14 paid invoices."
                         ActionLabel="Show paid invoices"
                         DoneTitle="26 invoices from March 2026"
                         DoneDescription="Paid and unpaid invoices are shown."
                         DoneActionLabel="Hide paid invoices" />
    <arc:EmptyStatesItem Scene="Offline"
                         Header="Sync"
                         Title="Invoices may be out of date"
                         Description="Edits are saved here and sync when you reconnect."
                         ActionLabel="Try again" />
    <arc:EmptyStatesItem Scene="CaughtUp"
                         Header="Reminders"
                         Title="No reminders due"
                         Description="Every overdue invoice has a reminder this week."
                         ActionLabel="Check again" />
</arc:EmptyStates>
```

### Content guidelines

- **Title**: say what is true and name the object or the filter: "No invoices match “March 2026”", not "No data".
  Sentence case, no exclamation mark.
- **Description**: one or two sentences about the cause and the consequence: "Edits are saved here and sync when you
  reconnect." Keep it within the block's description width of about 350 px.
- **Action label**: a verb and an object, the same as a button elsewhere in the app: "Show paid invoices", "Try again".
  Use "Try again" only when the same action can succeed next time.
- **Done copy**: say what changed, with the new count or the new state: "26 invoices from March 2026". A result that
  only says "Success" tells the person nothing about what to check.
- **Loading copy**: say what is happening now, in present tense, with an ellipsis: "Connecting to api.example.com…".

| Instead of | Write |
|---|---|
| No data | No invoices match “March 2026” |
| Error | You are offline |
| Nothing here | All caught up |
| Click here | Show paid invoices |
| Success | 26 invoices from March 2026 |

### Best practices

**Do**

- Match the scene to the cause. People read the drawing before the title; a `NotFound` drawing on a filter that hid the
  results sends them to the wrong fix.
- Set `Header` on every tab. The header is the only label in the tab row, so a tab named "Tab 2" tells the person
  nothing.
- Resolve every `Loading` to `Done` or `Idle`. Presses are ignored while `Loading`, so a phase that never resolves
  leaves the button stuck for the person.
- Write the `Done` copy as the outcome, so the person can read what changed without opening the list.
- Use `Done` for a result that has its own next step ("Hide paid invoices") and `Idle` when the action can run again.

**Avoid**

- A single empty list. Use `EmptyState`: one state, one button, no phases, and no tab row to explain.
- A destructive action as the empty state's button. `ActionInvoked` fires on the press without a confirmation, so a
  "Delete all drafts" button here runs at once. Put destructive actions behind a dialog.
- Treating the tabs as data. The block shows scenes and copy only. It does not filter, fetch or count anything, so the
  app must set `Phase` from its own results.
- Long descriptions. The copy is limited to about 350 px, so a paragraph wraps into a tall block that moves the page
  below it. Keep each description to one or two sentences.
- Changing the drawing for decoration. A scene should match the state the person is in; a "CaughtUp" drawing on an
  error suggests the error is resolved.

### Common questions

**Why does the button ignore my press?** While `Phase` is `Loading`, the block ignores presses of the action button and
does not raise `ActionInvoked` or run `ActionCommand`. Set `Phase` to `Done` or `Idle` when the work finishes.

**Why does the description stay the same while loading?** `LoadingDescription` replaces the description only when it has
text. The `Offline`, `CaughtUp` and `NotFound` scenes have a default loading line; `NoResults` has none, so the
description stays. Set `LoadingDescription` on the item to choose the line.

**Does switching tabs keep the action state?** No. Choosing another tab puts `Phase` back to `Idle`, and the new tab shows
its own `Idle` copy. To open a tab on a result, set `SelectedIndex` and `Phase` together, as in the narrow example:
`<arc:EmptyStates SelectedIndex="1" Phase="Done" />`.

**How do I add or remove a tab at run time?** Change `Items` (`Items.Add` or `Items.Remove`). The tabs follow the list,
and the selection is kept on the same item or moved to a valid index when the selected item goes away.

## API reference

<!-- api: EmptyStates -->

<!-- api: EmptyStatesItem -->

## Accessibility

- The block is a region named by `Label`. The tab row is a tab list named by `TabListLabel`, and each tab reports
  whether it is selected.
- Only the selected tab is a Tab stop. ArrowLeft and ArrowRight move between tabs and wrap around. Home and End go to
  the first and last tab. Moving selects the tab.
- Each tab is named by its header and reports its position among the tabs, so a screen reader reads "Sync, 2 of 3".
- The drawing is an image named after what it shows, such as "A cloud joined to a device by a broken line". Set
  `IllustrationLabel`, `LoadingIllustrationLabel` and `DoneIllustrationLabel` to name it in another language.
- After each phase change the new title and description, or the loading text, are announced politely. Switching tabs
  clears the last announcement, so an old result is not read again.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Enter or leave the tab row (the selected tab), and move to the action button after it |
| `Left` / `Right` | Select and focus the previous or next tab; wraps at the ends |
| `Home` / `End` | Select and focus the first or last tab |
| `Enter` / `Space` | Select the focused tab, as for any button |

## Motion

- Switching tabs moves every shape of the drawing to the next scene on a spring. Outlines morph point by point and
  lines draw in a little later. Colors cross over in 480 ms.
- A phase change nudges the same shapes with shorter delays.
- The old copy lifts away with a blur while the new title and description rise in. The copy height springs to fit,
  so the button does not jump. The tab highlight glides to the selected tab.
- Under a mouse the layers of the drawing tilt slightly toward the pointer.
- Reduced motion crossfades the old drawing into the new one and fades the copy. Off switches at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| The scene changes (another tab) | Shapes spring to the new positions, sizes and colors; outlines morph; lines draw a beat later | The old drawing crossfades into the new one | Immediate |
| The phase changes | Shapes move with shorter delays | The drawing crossfades | Immediate |
| The copy changes | The old copy lifts up and blurs out; the new copy rises from below through a blur; the height springs to fit | The copy fades | Immediate |
| The pointer moves over the drawing | The layers tilt slightly toward the pointer | No tilt | No tilt |

## Related

- `EmptyState` for a single empty list with an icon, a message and an action.
- `Skeleton` while content is loading.
- `Alert` for an error on a page that already shows content.

| Component | Choose it when |
|---|---|
| `EmptyState` | One list or panel is empty, and one message and one action explain it |
| `Skeleton` | The content is loading and its shape is known |
| `Alert` | An error or warning concerns content that is still on the page |
| `Toast` | A short confirmation follows a save or a send and needs no action |
| `Tabs` | The person switches between sections of the page that all have content |
| `SearchResults` | A search returns results with snippets and filters, and the empty result is one of its states |
