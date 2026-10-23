---
title: Command palette
summary: A complete keyboard driven action surface with search, grouped results, and shortcuts.
---

<Demo name="Preview" />

## When to use

The command palette is the keyboard entry point to the actions and pages of an application. It belongs in the app
shell: people open it from anywhere in the window with Cmd+K or Ctrl+K, type a few letters, and run the command without
moving through the sidebar or the menus. `CommandPaletteDialog` shows it as a popup over the page and closes it again.
It replaces a long menu tree with one search field and a list of commands grouped by intent.

- **Navigation across a large admin console**: a CRM with forty pages, where a sales manager types "pipe" to reach the
  pipeline report. Once a sidebar holds more items than people can scan, typing is faster than browsing.
- **Actions that live in several menus**: "Export invoices as CSV" is useful from the billing page, the reports page
  and the invoice list toolbar. One command with its own group keeps it in one place, so the menus can stay short.
- **Power-user work in analytics or developer tools**: "Open saved report", "Run query" or "Switch environment". The
  `Shortcut` cap teaches the key next to the command, so frequent users learn it and stop reaching for the mouse.
- **Preferences and toggles**: "Switch to dark mode" or "Change time zone". `Keywords` let people find the command by
  the outcome they want ("appearance", "invoice"), not by the menu name the team chose.
- **Operator tools in a support or fulfilment console**: "Resend receipt", "Open account" or "Refund payment" for a
  support agent who works from the keyboard during a call, where each second of menu browsing is noticed.

## When not to use

- **Searching records or documents with snippets and filters**, such as support tickets by status and date: use
  `SearchResults`, which shows a full results page with filters and matching text.
- **A search field in a header that shows a few matches** for the current page: use `ExpandingSearch`, which stays
  compact and does not list commands.
- **Choosing a value for a form**, such as a plan, a country or a cost centre: use `Combobox`. The palette runs
  commands; it does not set a field that is submitted with the form.
- **Actions on one object**, such as Download, Void and Archive on a single invoice row: use `DropdownMenu`, anchored to
  the row so the object stays in view while the person chooses.
- **Picking one value from a short fixed list**: use `Select`. A palette with three entries is more work than a
  select.
- **Asking for a decision or a short form**, such as confirming that a workspace will be deleted: use `Dialog`. The
  palette closes as soon as a command is chosen, so it cannot hold a question.

## Usage

`CommandPaletteDialog` is the palette as a popup. Declare it once, anywhere in a view. It renders nothing where it sits
and shows a `CommandPalette` above the page when it opens: people press Cmd+K (Ctrl+K elsewhere) from anywhere in the
window, or click a button that points at it. Add `CommandPaletteItem` children or set `ItemsSource`.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <Panel>
        <arc:Button Content="Search commands"
                    arc:CommandPaletteDialog.Opens="{Binding #Commands}" />

        <arc:CommandPaletteDialog x:Name="Commands"
                                  PlaceholderText="Search project commands">
            <arc:CommandPaletteItem Id="rename" Label="Rename board" Description="Give it a new title" Group="Board" Shortcut="R" />
            <arc:CommandPaletteItem Id="invite" Label="Invite a teammate" Group="Board" />
            <arc:CommandPaletteItem Id="export" Label="Export tasks" Group="Workspace" />
        </arc:CommandPaletteDialog>
    </Panel>
</UserControl>
```

```csharp
Commands.ItemSelected += (_, e) => Run(e.Item.Id);
```

The preview above shows the popup the way an app sees it, kept inside its stage (`IsModal="False"`): it is open when
the page loads, Esc closes it, and the button or Cmd+K opens it again. In a window the dialog covers the whole window
with a light scrim, as the demo under "The open gesture" shows.

The palette the dialog shows is a `CommandPalette`, a `TemplatedControl`. Its template holds a search row, a results
list grouped by heading and an empty state. The search field is the palette's own text box, derived from Avalonia's
`TextBox`, so the caret, selection, IME composition and clipboard shortcuts behave as they do in `Input`. The palette
adds the filtering, the highlight and the keyboard model on top. Its look comes from the palette's theme, so change the
appearance through the theme rather than in code. `CommandPalette` also works on its own, inline in a page; see "The
palette on its own".

Children written in XAML go to `Items`, which is an inline list on the dialog and on the palette. When `ItemsSource` is
set, it replaces `Items`. Use `Items` for a fixed menu and `ItemsSource` for a list the application builds or changes at
run time.

### Anatomy

1. **Search row**: the row holds the search icon, the field and the trailing controls. It is at least 54 px high with
   14 px side padding (11 px when the window is 480 px wide or less). The 18 px search glyph sits in a 22 px box, 10 px
   before the field.
2. **Search field** (`PlaceholderText`, `Label`): 14 px text on a 20 px line. The placeholder reads "Search commands"
   until the person types.
3. **Clear button** (`ClearButtonLabel`): a 24 px square with a 15 px cross. It appears once the query has text.
4. **Shortcut cap** (`ShortcutHint`): the key cap at the end of the search row, 24 px high with 7 px corners. In the
   dialog it shows the open gesture. It is hidden when the window is 480 px wide or less, and when `ShortcutHint` is an
   empty string.
5. **Close button** ("Esc", `ShowCloseButton`, `CloseButtonLabel`): 24 px high, at least 27 px wide, with 6 px side
   padding. It has its own focus ring, 3 px outside the corners. The dialog shows it by default; an inline palette shows
   it once `ShowCloseButton` is true.
6. **Results list** (`ResultsLabel`): up to 342 px high, then it scrolls. It has 8 px side padding and 6 px top and
   bottom padding (6 px all round when the window is 480 px wide or less).
7. **Group heading** (`Group`, or `DefaultGroup` for commands without a group): 12 px muted text with 6 px above and 3 px
   below. Groups are 6 px apart.
8. **Command row** (`Label`, `Description`, `Icon`, `Shortcut`): at least 34 px high with 8 px side padding and an 8 px
   corner radius. The 16 px icon is followed by a 10 px gap. The label is 14 px medium weight, and the description is
   12 px muted text on the same line, 8 px after the label. The shortcut cap is 18 px high with 11 px text and 5 px corners.
9. **Highlight**: one rounded rectangle with the row radius that moves between rows. It marks the command that Enter runs.
10. **Empty state** (`EmptyTitle`, `EmptyDescription`): a 32 px icon box with a 20 px glyph, 4 px between the title and
    the description, and 26 px above and 28 px below.
11. **Surface**: a floating panel with the surface color, the subtle border, the panel radius and the floating shadow.
    The dialog makes it `PanelWidth` wide, 560 px by default; an inline palette takes the width from its host. The
    examples here use 400, 480 and 560 px.
12. **Scrim** (dialog only, `ScrimBrush`, `BackdropBlur`): a near-black layer at 32% opacity over the page in both
    appearances, with an optional backdrop blur. It is absent when `IsModal` is false.

### Commands

Each `CommandPaletteItem` has a `Label` and optional `Description`, `Group`, `Icon`, `Shortcut`, `Keywords` and
`IsEnabled`. Commands are listed under their group, and the groups appear in the order their first command appears. A
command without a group goes under `DefaultGroup`, which is "Actions" by default. Set `DefaultGroup` to the heading
your app uses for its general commands, such as "Pages" in a navigation palette.

`Icon` takes a 24 by 24 Lucide geometry such as an `ArcIcons` glyph, or any control. The palette draws it at 16 px. The
default is the Lucide Command glyph.

The demo uses an inline palette, so that every command stays on screen. The dialog takes the same commands.

<Demo name="InlineItems" />

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:generic="using:System.Collections.Generic"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:CommandPalette>
        <arc:CommandPaletteItem Id="theme" Label="Switch theme" Group="Settings" Shortcut="T">
            <arc:CommandPaletteItem.Keywords>
                <generic:List x:TypeArguments="x:String">
                    <x:String>dark mode</x:String>
                    <x:String>appearance</x:String>
                </generic:List>
            </arc:CommandPaletteItem.Keywords>
        </arc:CommandPaletteItem>
    </arc:CommandPalette>
</UserControl>
```

`IsEnabled="False"` shows a command that cannot run right now, dimmed. A disabled command is skipped by the highlight
and cannot be chosen by a click or by Enter. Pair it with a `Description` that says why, as the demo does with "Needs
the Analytics plan": the person sees the command and the reason at the same time, instead of wondering why it is missing.

The search looks at the label, the description, the group and the `Keywords`, joined by spaces and compared without
regard to case. The query is trimmed, and a match is any substring of that joined text, so a query can run across two
fields. Give every command a stable `Id`. Rows keep their place by `Id` when the list is filtered or refreshed, so the
highlight stays on its command.

A narrow window gets a tighter palette. When the window is 480 pixels wide or less, the palette hides the shortcut caps
and tightens its padding, and at 340 pixels or less the rows also tighten their sides. The rule follows the window, not
the palette's own width: the 400 pixel palette above keeps its caps in a normal window, and so does a popup 440 pixels
wide.

### Running a command

Enter runs the highlighted command. After typing, with nothing highlighted, Enter runs the top match. A click runs the
command under the pointer. Both clear the query and then, in this order:

1. The dialog starts to close, unless `CloseOnSelect` is off, and the focus returns to the page.
2. `ItemSelected` is raised. It bubbles, so a page can listen.
3. `SelectCommand` runs with the chosen item.
4. The item's own `Command` runs with its `CommandParameter`.

The next time the dialog opens, the field is empty. An inline palette does not close, so it keeps the focus in the
field and runs steps 2 to 4.

```csharp
dialog.ItemSelected += (_, e) => Navigate(e.Item.Id);
```

Use `ItemSelected` in code-behind when the navigation is simple, and `SelectCommand` in an MVVM app so the view model
decides what each command does. Use the item's `Command` when one command belongs to one item, such as a command that
opens a specific dialog. Use `Tag` to carry an application object with an item, so the handler does not parse `Id`.
Because the dialog closes before the handlers run, a command that opens a `Dialog` finds the focus already back on the
page, and that dialog returns it there when it closes.

### Binding to a view model

Bind `ItemsSource` to a collection of `CommandPaletteItem` objects, and bind `SelectCommand` to a command that receives
the chosen item. Bind `IsOpen` to open and close the dialog from the view model; it is two-way by default, so the view
model also sees a close that a key or a click started. The palette follows changes to the collection and to the items.
Rows that stay move to their new place, new rows fade in, and the frame changes height. Change the items inside the
`ObservableCollection` rather than replacing the whole list, so the rows that stay keep their place.

The demo uses an inline palette, which also has a two-way `Query`; `ActiveItem` is read-only and reports the highlighted
command. The dialog has no `Query`: it starts every opening with an empty field.

<Demo name="DataBinding" />

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TKCode.Arc;

public partial class CommandCenterViewModel : ObservableObject
{
    private readonly IWorkspaceNavigator _navigator;

    public CommandCenterViewModel(IWorkspaceNavigator navigator)
    {
        _navigator = navigator;
        Commands = new ObservableCollection<CommandPaletteItem>
        {
            new() { Id = "billing", Label = "Go to billing", Group = "Navigation", Icon = ArcIcons.CreditCard, Shortcut = "G B" },
            new() { Id = "reports", Label = "Open reports", Group = "Navigation", Icon = ArcIcons.ChartBar },
            new() { Id = "export", Label = "Export invoices as CSV", Description = "Current month", Group = "Actions", Icon = ArcIcons.FileText },
            new() { Id = "invite", Label = "Invite teammate", Group = "Actions", Icon = ArcIcons.Users, Keywords = ["add user", "seat"] }
        };
    }

    public ObservableCollection<CommandPaletteItem> Commands { get; }

    [ObservableProperty]
    private bool _isPaletteOpen;

    [RelayCommand]
    private void OpenPalette() => IsPaletteOpen = true;

    [RelayCommand]
    private async Task RunCommandAsync(CommandPaletteItem? item)
    {
        if (item is null)
        {
            return;
        }
        await _navigator.RunAsync(item.Id);
    }
}
```

```xml
<arc:CommandPaletteDialog ItemsSource="{Binding Commands}"
                          SelectCommand="{Binding RunCommandCommand}"
                          IsOpen="{Binding IsPaletteOpen}"
                          PlaceholderText="Search pages and actions" />
```

```xml
<arc:CommandPalette ItemsSource="{Binding Commands}"
                    Query="{Binding Query, Mode=TwoWay}"
                    SelectCommand="{Binding RunCommandCommand}"
                    PlaceholderText="Search pages and actions" />
```

- `IsOpen` is two-way, so the view model reads every close. Set it to true to open the dialog, for example from a toolbar
  command like `OpenPaletteCommand`.
- `Query` on an inline palette is two-way, so the view model can read the text (for example, to log a search) or reset
  it. Set it to an empty string to clear the search; `Clear()` is a palette method for code-behind that also resets the
  highlight.
- `ItemsSource` and `SelectCommand` are one-way. The palette never writes to the collection or the command.
- Compiled bindings need `x:DataType` on the view. The examples here leave it out for brevity.

### Opening and closing

The dialog opens with `IsOpen`, `Open()`, `Toggle()`, the open gesture or a button that points at it. It closes with
`IsOpen`, `Close()`, or one of the actions below. `IsOpen` binds two ways, so a view model sees every close, including
the ones people start with a key or a click.

| What the person does | `DismissReason` | Turn it off with |
|---|---|---|
| Presses Esc in an empty field | `Escape` | `CloseOnEscape` |
| Presses on the scrim; with `IsModal` false, anywhere outside the palette | `PressOutside` | `CloseOnOutsidePress` |
| Clicks the "Esc" key cap | `CloseButton` | `ShowCloseButton` hides the cap |
| Chooses a command | `Action` | `CloseOnSelect` |
| Presses the open gesture while the dialog is open | `Programmatic` | `IsShortcutEnabled` |

Esc with a query in the field only clears the query, and a second Esc closes the dialog. The key cap is a click on a
visible button, so it always closes, even with `CloseOnEscape` off. `Close()`, `CloseCommand` and setting `IsOpen` to
false are closes from code and report `Programmatic`.

To keep the dialog open for one close request, cancel `Closing`. It runs before every close in the table and before
`Close()`, with the reason in `e.Reason`; setting `IsOpen` to false closes without asking. `Closed` follows when the
dialog closes, while the exit animation may still be playing, and `LastCloseReason` keeps the reason afterwards. A
command that is chosen still runs when `Closing` is cancelled; only the closing is skipped.

```csharp
dialog.Closing += (_, e) => e.Cancel = hasUnsavedInput && e.Reason == DismissReason.PressOutside;
dialog.Closed += (_, e) => lastCloseReason = e.Reason;
```

### Opening from a button or from code

Any button can open the dialog with the attached property `CommandPaletteDialog.Opens`, as in the usage example. In
code, call `Open()`, `Toggle()` or `Close()`. To open it from a menu item or a toolbar command, bind to `OpenCommand`,
`ToggleCommand` or `CloseCommand`, which are ready-made `ICommand` properties of the dialog.

```xml
<MenuItem Header="Command palette" Command="{Binding #Commands.OpenCommand}" />
```

Show the button where people look for search, in addition to the key, so that people who do not know Cmd+K still find
the palette. Closing returns the focus to the element that had it, so a button that opened the dialog gets it back.

### The open gesture

`OpenGesture` is Cmd+K or Ctrl+K until you set one, on every platform. The dialog handles it at the window, before the
window and its controls do, from wherever the focus is, including a text box, and it toggles: the same key closes an
open dialog. The default matches the K key with Cmd or Ctrl alone, so Ctrl+Shift+K is not the open gesture.

The gesture is ignored while the dialog is turned off (`IsShortcutEnabled`), while it is not in a window, disabled or on
a hidden page or tab, and while another modal is on top, such as a `Dialog` or another command palette dialog that is
open. The key cap in the search row shows the gesture: `ShortcutHint` replaces its text, and an empty string hides it.
With `IsShortcutEnabled` false and no `ShortcutHint`, the cap is hidden too, because the key does nothing.

The demo answers to Cmd+Shift+P (Ctrl+Shift+P elsewhere), because the preview at the top of this page already answers to
Cmd+K. It covers the whole window and blurs the page behind its scrim (`BackdropBlur`).

<Demo name="OnDemand" />

```csharp
dialog.OpenGesture = new KeyGesture(Key.P, KeyModifiers.Control | KeyModifiers.Shift);
```

```xml
<arc:CommandPaletteDialog BackdropBlur="4" PanelWidth="640" />
```

A key has one answer. When two dialogs, or a dialog and an inline palette, match the same key, only one of them
answers, and which one is not defined; give each dialog on a page its own gesture. The same holds for your own
Cmd/Ctrl+K handler, such as a sidebar search: the dialog answers the key at the window before your handler sees it, so
give the dialog another `OpenGesture`, or set `IsShortcutEnabled` to false and call `Open()` from your handler.

The key cap and each command's `Shortcut` text are labels. The palette does not bind a command's `Shortcut`: bind the
real key in your app, in the same window-level handler that runs the command, so the cap never shows a shortcut that
does nothing.

### Placement

The palette is `PanelWidth` wide (560 by default) and sits `PanelTopRatio` of the window height from the top (0.13, and
at least 16 pixels). In a narrow window it keeps 16 pixels from each side, and it never reaches below the window: the
list scrolls instead. Filtering changes the palette's height but not where its top edge is.

`IsModal` decides how the dialog treats the page:

| | `IsModal` true (default) | `IsModal` false |
|---|---|---|
| Scrim | A scrim covers the page (`ScrimBrush`, `BackdropBlur`) | None |
| The page | Behind the scrim, out of reach | Stays interactive |
| Focus | Stays inside the palette | Is free |
| Outside press | A press on the scrim closes (`CloseOnOutsidePress`) | A press anywhere outside the palette closes |

`ScrimBrush` defaults to a light dim from the theme. `BackdropBlur` is a radius in pixels and defaults to 0: no blur.
Under the reduced transparency setting the blur is off and the scrim stays.

### Focus

- Opening moves the focus to the field and remembers where it was. Closing returns it there, whether the dialog closed
  with a key, a click or a command, as long as that element is still on screen and nothing else took the focus.
- When the dialog closes with Esc, or the element showed a focus ring when the dialog opened, the returned focus shows
  its ring again.
- While the dialog is modal, the focus stays inside it: Tab goes from the field to the clear button (when there is a
  query) and the "Esc" key cap, then back to the field.
- Choosing a command closes the dialog before `ItemSelected` and the commands run, so a command that opens another
  dialog finds the focus already back on the page.
- With `IsModal` off, the page stays interactive and the focus is free. A press anywhere outside the palette closes it.
- Opening while a text box has the focus works, because the dialog handles the gesture before the text box does. The
  text box gets the focus back on close.

### The palette on its own

`CommandPalette` is the control inside the dialog. Use it inline, in a page or a side panel, when the list should always
be there. It has the same commands, text and keys; it leaves opening and closing to you. Set `ShowCloseButton` to show
the "Esc" key cap, handle `CloseRequested`, and set `AutoFocus` so the field takes the focus when the palette appears.

```xml
<arc:CommandPalette x:Name="Palette" MaxWidth="560" ShowCloseButton="True" AutoFocus="True">
    <arc:CommandPaletteItem Id="dashboard" Label="Go to dashboard" Group="Navigation" Shortcut="G D" />
    <arc:CommandPaletteItem Id="theme" Label="Switch theme" Group="Settings" />
</arc:CommandPalette>
```

```csharp
Palette.CloseRequested += (_, e) => Hide(e.Reason);
```

`CloseRequested` is raised by Escape in an empty field and by the "Esc" key cap, and its `Reason` is
`CommandPaletteCloseReason.Escape` or `CommandPaletteCloseReason.CloseButton`. Escape with a query clears it and is
handled. In an empty field Escape is left unhandled, so an overlay around the palette can also close on it, as the
dialog does. Whatever hides the palette is yours: close it there, and after `ItemSelected`.

Cmd+K on macOS, or Ctrl+K elsewhere, moves the focus to the field of an inline palette, from anywhere in the window; it
does not toggle. The palette stands back while another modal that does not contain it is on top, and while another
palette has the focus. When a page shows more than one palette, set `IsShortcutEnabled` to false on all but one.
`ShortcutHint` changes the text of the key cap, and an empty string hides it; the cap stays visible when
`IsShortcutEnabled` is false, so hide it too.

```xml
<arc:CommandPalette IsShortcutEnabled="False" ShortcutHint="" />
<arc:CommandPalette ShortcutHint="Ctrl K" />
```

Call `FocusInput()` to move the focus into the field later, for example when the host is asked to open a second time,
and `Clear()` before you show the palette again, so that the previous query and highlight do not carry over.

### Long lists

The list is virtual: only the rows near the visible part exist, and they are reused as you scroll or filter. A thousand
commands open, filter and scroll as fast as ten. Rows share one height: the palette measures the rows it has drawn and
uses that height for the rest, so keep each row to one line, as the label and description already are.

The dialog builds its palette the first time it opens and keeps it for the next time. While the dialog is closed it does
no work for the list and only listens for the open gesture. `Palette` is null until the first opening.

### Text

All text has an English default. `PlaceholderText` is the hint in the empty field and `Label` names the field, and the
dialog as a whole. `EmptyTitle` and `EmptyDescription` fill the empty state. `ClearButtonLabel`, `CloseButtonLabel` and
`ResultsLabel` name the clear button, the close button and the list for assistive technology. Set them when the app is
not in English, because assistive technology reads these strings.

| Property | Default |
|---|---|
| `PlaceholderText` | Search commands |
| `Label` | Command palette |
| `EmptyTitle` | No matching actions |
| `EmptyDescription` | Try a different word or clear the search. |
| `DefaultGroup` | Actions |
| `ClearButtonLabel` | Clear search |
| `CloseButtonLabel` | Close command palette |
| `ResultsLabel` | Command results |
| `ShortcutHint` | "⌘ K" on macOS and iOS, "Ctrl K" elsewhere; on the dialog, the open gesture |

```xml
<arc:CommandPaletteDialog PlaceholderText="Jump to a page"
                          Label="Quick switcher"
                          EmptyTitle="No pages found"
                          EmptyDescription="Check the spelling or try another word." />
```

### Example: billing operations console

A billing team works in one console with pages for invoices, subscriptions and refunds. The console declares one
`CommandPaletteDialog`, and it opens on the key the team already uses. The commands are the things an operator does
many times a day: move between the three pages, export a month of invoices, look up an account. The groups are short
nouns, the labels start with a verb, and the refund command is disabled for operators without the refund permission,
with a description that says so. The export command's label already contains "invoices", so an operator who types it
finds the command at once. The `IsOpen` in the sample opens the dialog as soon as the view appears, which is how the
picture was taken; in the console it opens with the key.

![The billing console behind a dimmed scrim, with the command palette open and the refund command disabled](images/BillingConsole.png)

```xml
<Panel Width="720" Height="480">
    <StackPanel Margin="32,24" Spacing="4">
        <TextBlock Text="Billing"
                   FontSize="{arc:ArcDesignTokenResource TypeXL}"
                   FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="Invoices, subscriptions and refunds for Northwind Cloud"
                   FontSize="{arc:ArcDesignTokenResource TypeSM}"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </StackPanel>
    <TextBlock Margin="32,0,32,24"
               VerticalAlignment="Bottom"
               Text="12 open invoices · 3 past due · $4,280.00 outstanding"
               FontSize="{arc:ArcDesignTokenResource TypeSM}"
               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    <arc:CommandPaletteDialog IsOpen="True"
                              PanelWidth="480"
                              PanelTopRatio="0.2"
                              Label="Billing commands"
                              PlaceholderText="Search billing actions"
                              ResultsLabel="Billing command results"
                              EmptyTitle="No billing actions found"
                              EmptyDescription="Try refund, invoice or plan.">
        <arc:CommandPaletteItem Id="go-invoices"
                                Label="Go to invoices"
                                Group="Navigation"
                                Icon="{x:Static arc:ArcIcons.FileText}"
                                Shortcut="G I" />
        <arc:CommandPaletteItem Id="go-subscriptions"
                                Label="Go to subscriptions"
                                Group="Navigation"
                                Icon="{x:Static arc:ArcIcons.CreditCard}"
                                Shortcut="G S" />
        <arc:CommandPaletteItem Id="export-invoices"
                                Label="Export invoices as CSV"
                                Description="Current month"
                                Group="Actions"
                                Icon="{x:Static arc:ArcIcons.Download}" />
        <arc:CommandPaletteItem Id="refund"
                                Label="Refund payment"
                                Description="Needs the refund permission"
                                Group="Actions"
                                Icon="{x:Static arc:ArcIcons.Undo2}"
                                IsEnabled="False" />
        <arc:CommandPaletteItem Id="lookup-account"
                                Label="Open account"
                                Description="By account ID, such as acct_2026_0412"
                                Group="Actions"
                                Icon="{x:Static arc:ArcIcons.Search}" />
    </arc:CommandPaletteDialog>
</Panel>
```

### Content guidelines

- **Command label**: a verb and an object in sentence case: "Export invoices as CSV", "Go to billing", "Refund payment".
  Keep it to two to four words, because the row also shows a description and a shortcut cap.
- **Navigation label**: start with "Go to" or "Open" so the person knows the command moves them.
- **Group**: a short noun for the kind of work: "Navigation", "Actions", "Settings", "Account". Keep the set of groups
  small, so the headings do not outnumber the commands.
- **Description**: a short note on the same line: the scope, the consequence or the reason a command is disabled. Write
  numbers as digits.
- **Keywords**: synonyms people type that the label does not contain: "invoice" for billing, "log out" for sign out.
  Do not repeat the label or the group in the keywords.
- **Placeholder**: name the scope of the search: "Search billing actions" tells people what the palette covers better
  than "Search".

| Instead of | Write |
|---|---|
| Click here to see billing | Go to billing |
| Export | Export invoices as CSV |
| Settings > Notifications > Email | Email notification settings |
| Dark mode toggle | Switch to dark mode |
| Invoice list page | Open invoices |

### Best practices

**Do**

- Give every command a stable `Id`. Rows keep their place and the highlight stays on the same command when the data
  refreshes, which keeps the list from jumping under the person's hand.
- Bind the real key for every `Shortcut` you show, in the window-level handler, so the cap teaches a key that works.
- Use `Keywords` for the words people search for, and keep the label as the name of the command. The label is what the
  person reads to confirm the choice.
- Disable a command with `IsEnabled="False"` and a `Description` that says why. A hidden command teaches nothing; a
  disabled one with a reason tells people what they need.
- Keep one palette dialog per window. A second dialog needs its own `OpenGesture`, and an inline palette next to it needs
  `IsShortcutEnabled="False"`, or the key has two claimants and only one of them answers.
- Offer a button that opens the dialog next to the key. People who do not know Cmd+K then still find the palette.
- Handle a chosen command in `ItemSelected` or `SelectCommand` and expect the dialog to be closing already. A command
  that opens a `Dialog` then needs no focus handling of its own.

**Avoid**

- Copying every menu item into the list. The palette is for commands people search for; a list of the whole sidebar
  makes the search slower than the sidebar it replaces.
- Destructive commands that match a short query. Enter runs the top match after typing, so "del" could delete a
  workspace before the person looks. Put destructive actions behind a confirmation in the command, or keep them out of
  the palette.
- Shortcut caps for keys the app does not handle. People copy the cap and find nothing, which is worse than no cap.
- Replacing the whole list on every change. The palette follows changes to the collection and to the items, so update
  the existing `ObservableCollection` and let the rows that stay keep their place.
- Turning off `CloseOnEscape` or `CloseOnOutsidePress` to protect unsaved input. People expect both to close a popup;
  cancel `Closing` for the moment when it must stay open.

### Common questions

**Why does Enter run a command I did not highlight?** After typing, with nothing highlighted, Enter runs the top match.
Typing clears the highlight. To run another command, press the arrow keys first, or click the row.

**Why does a disabled command not run?** A command with `IsEnabled="False"` is skipped by the highlight and cannot be
chosen by a click or by Enter. Enter after typing runs the first enabled match instead.

**Why does Cmd+K or Ctrl+K do nothing?** Look for `IsShortcutEnabled` set to false, a different `OpenGesture` on the
dialog, a hidden page or tab around the dialog, another modal on top of the window (a `Dialog` or another open command
palette dialog), and another dialog or palette on the page that answers the same key first. While one of them is true,
the dialog ignores the key.

**Why did my own Cmd+K handler stop working?** A dialog on the page answers the key at the window, before the controls
below it. Give the dialog another `OpenGesture`, or turn its shortcut off and call `Open()` from your handler.

**Can I change the key that opens the palette?** Yes. Set `OpenGesture` on the dialog. The inline palette's focus key is
fixed at Cmd/Ctrl+K: to open or focus it with another key, handle that key in your window and call `FocusInput()` on the
palette, or call `Open()` on a dialog.

**How do I close the dialog from code?** Call `Close()`, run `CloseCommand`, or set `IsOpen` to false. `Close()` and
`CloseCommand` raise `Closing`, which can cancel the close; setting `IsOpen` does not.

**How do I update the list while it is open?** Change the item's `IsEnabled` or add and remove items in the
`ObservableCollection`; the palette follows both.

## API reference

<!-- api: CommandPaletteDialog -->

<!-- api: CommandPalette -->

<!-- api: CommandPaletteItem -->

### Event arguments

| Type | Property | Description |
|---|---|---|
| `CommandPaletteItemSelectedEventArgs` | `Item` | The command that was chosen. |
| `CommandPaletteCloseRequestedEventArgs` | `Reason` | A `CommandPaletteCloseReason`: `Escape` for the Escape key in an empty field, `CloseButton` for the "Esc" key cap. |
| `OverlayClosingEventArgs` | `Reason` | A `DismissReason`: why the dialog is closing. |
| `OverlayClosingEventArgs` | `Cancel` | Set it to true to keep the dialog open. |
| `OverlayClosedEventArgs` | `Reason` | A `DismissReason`: why the dialog closed. |

## Accessibility

- The dialog is exposed as a window named by `Label`. Its search field has the same name, and the field's help text is
  the label of the highlighted command. An inline palette names the field by `Label` and has no window of its own.
- The result list is named by `ResultsLabel`. Each row is a list item named by its label and description, with its
  position in the set, and the highlighted row reports itself as selected. Group headings are groups named by the
  group.
- ArrowDown and ArrowUp move the highlight and stop at the first and last command. Home and End jump to them. Disabled
  commands are skipped.
- Enter runs the highlighted command, or the top match after typing. Escape clears the query, and in an empty field it
  closes the dialog. Nothing needs the mouse.
- Cmd/Ctrl+K opens the dialog from anywhere in the window, and the focus moves to the field. Closing returns the focus
  to where it was.
- The clear button and the "Esc" key cap can be reached with Tab and show a focus ring. The key cap is a button named
  by `CloseButtonLabel`, and the clear button is named by `ClearButtonLabel`.
- A modal dialog keeps the page behind it out of the keyboard's reach. With `IsModal` off, the page stays reachable.
- The empty state is plain text, so screen readers read it.
- Under the reduced transparency setting the backdrop blur is off; the scrim stays.
- Set `Label`, `ResultsLabel`, `ClearButtonLabel` and `CloseButtonLabel` in the language of the app. They are the names
  assistive technology announces; the visible hints do not replace them.

### Keyboard

Except for the open gesture, the keys below work while the search field has the focus. Home and End move the highlight,
not the caret, so use Left and Right inside the field to move the caret.

| Keys | Action |
|---|---|
| `Cmd+K` on macOS, `Ctrl+K` elsewhere | Open the dialog from anywhere in the window; press it again to close. On an inline palette, move the focus to the field. Works when `IsShortcutEnabled` is true; a dialog's `OpenGesture` replaces the key. |
| `Down` / `Up` | Move the highlight to the next or previous enabled command. From no highlight, Down goes to the first command and Up to the last. The highlight stops at the ends. |
| `Home` / `End` | Move the highlight to the first or last enabled command. |
| `Enter` | Run the highlighted command, or the top match after typing with nothing highlighted. The dialog then closes. |
| `Escape` | Clear the query when there is one. In an empty field, close the dialog (an inline palette raises `CloseRequested`). |
| `Tab` / `Shift+Tab` | Move between the search field, the clear button (when there is a query) and the "Esc" key cap. In a modal dialog the focus cycles inside the palette. |

## Motion

- The scrim fades in. The palette fades in with it and rises 6 pixels from a slightly smaller scale on the smooth spring.
  Closing is a short fade of both. Opening again during the exit turns around from where it is.
- One highlight moves between rows: a short slide for the arrow keys and a quick spring for the pointer. It fades in
  where it first appears and fades out when nothing is highlighted.
- While you type, rows that stay glide to their new place, rows that leave fade out, and new rows fade in. The frame
  height follows the list on a smooth spring. When rows would jump more than six places, they move at once.
- The results fade out before the empty state rises in, so the frame changes height once.
- The clear button pops in from a smaller scale out of a soft blur.
- Reduced motion keeps only the fades. Off shows every change at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| The dialog opens | The scrim fades in; the palette fades in, rising 6 px from a scale of 0.98 on the smooth spring | Fades only | Immediate |
| The dialog closes | The scrim and the palette fade out quickly | Fades only | Immediate |
| An inline palette appears | Fades in, rising 6 px from a scale of 0.98 on the smooth spring | Fades only | Immediate |
| The highlight moves with the arrow keys | Short slide | No slide; the highlight changes at once | Immediate |
| The highlight moves with the pointer | Quick spring | No spring; the highlight changes at once | Immediate |
| Rows change while typing | Remaining rows glide, leaving rows fade out, new rows fade in | Fade only | Immediate |
| The results frame changes height | Smooth spring | Immediate | Immediate |
| Results and empty state swap | Results fade out, then the empty state rises in | Fade only | Immediate |
| The clear button appears | Pops in from a smaller scale out of a soft blur | Fade only | Immediate |

## Related

- `SearchResults` for a full search page with filters and snippets.
- `ExpandingSearch` for a compact search field in a header.
- `DropdownMenu` for the actions of one object.
- `Combobox` for choosing a value in a form.
- `Dialog` for a focused surface that asks for a decision.

| Component | Choose it when |
|---|---|
| `SearchResults` | People search records or documents and need snippets, filters and a results page |
| `ExpandingSearch` | A compact search in a header filters the current page or a short list |
| `SearchField` | A search box sits inside a page and its results are shown beside it |
| `DropdownMenu` | A few actions apply to one object, such as one invoice row |
| `Combobox` | The person picks a value that a form saves |
| `Select` | The person picks one value from a short fixed list in a form |
| `Dialog` | The person must answer a question or fill in a short form before returning to the page |
