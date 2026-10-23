---
title: Split button
summary: A primary action with a menu of nearby alternatives.
---

<Demo name="MergeStrategies" />

## When to use

`SplitButton` joins one default action to a chevron that opens the nearby alternatives. It answers two questions in
one control: what usually happens here, and what else can happen. The common path is one click, and the alternatives
are one more click away, so the control fits a toolbar, a page header or a footer where one action dominates.

- **Merge pull request in a code review screen**: Merge with a merge commit is the default. Squash and merge and
  Rebase and merge sit in the menu, with Close pull request as a destructive row after a separator.
- **Export on a revenue report**: Export as CSV is the format most finance teams use, so it is the default. JSON and
  PDF are one click away, and the report keeps its filters either way.
- **Send on an invoice editor**: Send invoice is the default action. Send reminder, Save as draft and Void invoice
  are the alternatives, and the menu separates the destructive action from the rest.
- **Copy with a result in place**: Copy page answers Copied on the same button for a moment, so the confirmation sits
  where the person's eyes already are and no toast is needed.
- **Deploy in a CI console**: Deploy to production is the default for a release branch. Deploy to staging is in the
  menu for the cases where a release needs a rehearsal.

## When not to use

- **Equal options with no default action**, such as Share by email, Slack or link: use `DropdownMenu`, which has a
  trigger and no primary half.
- **A single action with nothing to choose from**, such as Save changes on a settings form: use `Button`.
- **Actions on one row or card object**, such as Rename, Archive and Delete on an order: use `ContextMenu` for the
  pointer and keyboard context menu of the object.
- **Several related actions with no default**, shown side by side: use `ButtonGroup`.
- **A choice that stays selected**, such as a period or a view: use `SegmentedControl` or `Select`. A split button
  runs its action; it does not hold a value.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:SplitButton Label="Merge pull request">
        <arc:MenuEntry Header="Squash and merge" />
        <arc:MenuEntry Header="Rebase and merge" />
        <arc:MenuEntry Header="Close pull request" IsDestructive="True" HasSeparatorBefore="True" />
    </arc:SplitButton>
</UserControl>
```

`SplitButton` derives from Avalonia's `TemplatedControl`. Its two halves are real buttons, so each one takes focus,
shows the focus ring and responds to the keyboard on its own. The menu is the same `MenuSurface` that `DropdownMenu`
uses, so its rows, highlight and keyboard movement match the rest of the library. The rows are `MenuEntry` items,
declared inline in `Items` or supplied through `ItemsSource`.

### Anatomy

1. **Main half**: the default action. It shows the `Label` and, when set, the `Icon` in front
   of it. Its inner padding is 16 px on each side (12 px when `Secondary`), and its minimum height is the medium
   control height.
2. **Icon** (`Icon`): a 16 px Lucide glyph with a 1.75 stroke, followed by the label after an 8 px gap. A new icon
   grows in while the old one shrinks away.
3. **Divider**: a 1 px line between the halves, a faint tint of the text color on the primary variant.
4. **Chevron half**: 38 px wide (32 px when `Secondary`). Its 16 px chevron turns while the
   menu is open. It never scales when pressed.
5. **Menu** (`Menu`): a dropdown anchored to the chevron, aligned to its end and opened 4 px below it. Its rows are
   `MenuEntry` items. A row with `HasSeparatorBefore` starts a new group, and `IsDestructive` colors it as a danger
   action.
6. **Focus ring**: a 2 px ring offset 2 px from each half, shaped to match that half's corners.

The primary variant is a filled pill with the foreground color as its fill. The `Secondary` variant is an outlined
pill: 36 px tall, 12 px text, a main half at least 142 px wide, on the surface color.

### Choosing the variant and the default action

Put the most common action in the main half. Order the menu rows from the most to the least frequent, so the list
reads as a ranking. Put a destructive row last and separate it with `HasSeparatorBefore`, so a misclick on the
chevron does not reach it.

Use `Primary` in a page header, a dialog footer or a card where the action is the goal of the screen. Use `Secondary`
in a dense toolbar or a table header, where a filled button would compete with the content. Do not mix the two
variants in one toolbar: the outlined pill and the filled pill read as different levels of importance.

### Click and menu selection

The main half raises `Click` and runs `Command`. `Click` is raised before the command runs, so a handler can prepare
state the command reads. The chevron half opens the menu, and a row raises `ItemClicked` with the row that was
chosen. The menu closes when a row is chosen.

```xml
<arc:SplitButton Label="Export as CSV"
                 Variant="Secondary"
                 Command="{Binding ExportCommand}">
    <arc:MenuEntry Header="Export as JSON" />
    <arc:MenuEntry Header="Export as PDF" />
</arc:SplitButton>
```

```csharp
merge.Click += (_, _) => log.Text = "Merged with a merge commit.";
merge.ItemClicked += (_, e) => log.Text = $"Menu: {e.Header}";
```

### Rows from data

Use `ItemsSource` for rows that come from a view model, and `ItemTemplate` to draw each row. `MenuEntry` instances
are used as they are. Other items are shown as row headers, so give each row a `MenuEntry` or a template when it needs
its own look. `ItemClicked` returns the data item in `e.Item`, which keeps the handler free of string matching.

```xml
<arc:SplitButton Label="Share" ItemsSource="{Binding ShareTargets}" />
```

### Icon and result in place

`Icon` takes a 24 by 24 Lucide geometry and is set in code. Changing `Label` and `Icon` morphs the main half to the
new text, so a result can be shown on the button itself: "Copy page" becomes "Copied", and the check icon replaces the
copy icon. The shared letters stay in place and only the changed letters move, so the eye does not lose the button.

<Demo name="ResultInPlace" />

Keep a result label to one word, and restore the original label after a short pause. Use the same pattern for a
confirmation such as "Sent" or "Saved", and do not use it for a process that takes a while: then show progress
instead of a label.

### Disabled

Set `IsEnabled` to `False` to disable both halves. A disabled split button also keeps its menu closed, so no
alternative can be chosen while the default action is unavailable. The control fades to about 52% opacity, which
keeps it visible in the layout.

<Demo name="Disabled" />

Disable the control only while the action cannot apply, and say why nearby. A Deploy button disabled because the
branch has failing checks is clearer with a line of text that names the failing check.

### Secondary

`Variant="Secondary"` is a compact outlined pill with 12px text and a 142px main half. In this example, picking a format makes it the default.

<Demo name="Secondary" />

The secondary variant suits toolbars and table headers. In this example, a picked alternative becomes the new label.
Keep the choice in the view model when it must survive a restart, and set `Label` from it when the view loads.

### Binding to a view model

An invoice editor exposes its send actions as commands and holds the menu state in the view model. The main action
is a command, the alternatives come from a collection, and the open state is two-way so that the view model can close
the menu after a send.

```csharp
public partial class InvoiceViewModel : ObservableObject
{
    private readonly IInvoiceService _invoices;

    public InvoiceViewModel(IInvoiceService invoices)
    {
        _invoices = invoices;
        AlternativeSends = new ObservableCollection<SendOption>
        {
            new("Send reminder", SendKind.Reminder),
            new("Save as draft", SendKind.Draft),
            new("Void invoice", SendKind.Void)
        };
    }

    [ObservableProperty]
    private bool _isMenuOpen;

    public string InvoiceId { get; } = "inv_2026_0412";

    public ObservableCollection<SendOption> AlternativeSends { get; }

    [RelayCommand]
    private async Task SendInvoiceAsync()
    {
        var result = await _invoices.SendAsync(InvoiceId, SendKind.Invoice);
        IsMenuOpen = false;
    }

    public async Task RunAsync(SendOption option) =>
        await _invoices.SendAsync(InvoiceId, option.Kind);
}

public enum SendKind { Invoice, Reminder, Draft, Void }

public sealed record SendOption(string Label, SendKind Kind);
```

```xml
<arc:SplitButton Label="Send invoice"
                 Command="{Binding SendInvoiceCommand}"
                 IsOpen="{Binding IsMenuOpen, Mode=TwoWay}"
                 ItemsSource="{Binding AlternativeSends}"
                 MenuButtonLabel="Other ways to send" />
```

```csharp
invoice.ItemClicked += (_, e) => _ = viewModel.RunAsync((SendOption)e.Item!);
```

- `IsOpen` is two-way. The view model can open or close the menu, and the chevron writes the state back when the
  person opens it.
- `Command` runs the default action.
- The alternatives are handled in `ItemClicked`, because a row's data item is the natural argument; the view model
  owns the action and the control owns the menu.

### Example: invoice footer

An invoice editor in a billing console. The footer shows the total on the left and the send actions on the right. Send
invoice is the default, the chevron offers the reminder, the draft and the void, and the void sits below a separator
because it is the only destructive row. The secondary Cancel button is a ghost button, so the split button is the only
filled control in the footer.

![An invoice footer with a Send invoice split button and its alternatives](images/InvoiceFooter.png)

```xml
<StackPanel Width="620" Spacing="16">
    <Border Padding="24"
            Background="{arc:ArcDesignTokenResource Surface}"
            BorderBrush="{arc:ArcDesignTokenResource Border}"
            BorderThickness="1"
            CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
        <StackPanel Spacing="16">
            <Grid ColumnDefinitions="*,Auto">
                <StackPanel Spacing="2">
                    <TextBlock Text="Invoice inv_2026_0412"
                               FontSize="16"
                               FontWeight="SemiBold"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="Northwind Traders, due May 14, 2026"
                               FontSize="13"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                </StackPanel>
                <TextBlock Grid.Column="1"
                           Text="Draft"
                           VerticalAlignment="Center"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            </Grid>
            <Border Height="1" Background="{arc:ArcDesignTokenResource Border}" />
            <Grid ColumnDefinitions="*,Auto,Auto">
                <StackPanel Spacing="2">
                    <TextBlock Text="Total"
                               FontSize="13"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Text="$12,480.00"
                               FontSize="20"
                               FontWeight="SemiBold"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                </StackPanel>
                <arc:Button Grid.Column="1"
                            Content="Cancel"
                            Variant="Ghost"
                            VerticalAlignment="Center"
                            Margin="0,0,12,0" />
                <arc:SplitButton Grid.Column="2"
                                 Label="Send invoice"
                                 Icon="{x:Static arc:ArcIcons.Send}"
                                 VerticalAlignment="Center">
                    <arc:MenuEntry Header="Send reminder" Icon="{x:Static arc:ArcIcons.Bell}" />
                    <arc:MenuEntry Header="Save as draft" Icon="{x:Static arc:ArcIcons.FileText}" />
                    <arc:MenuEntry Header="Void invoice"
                                   Icon="{x:Static arc:ArcIcons.CircleX}"
                                   IsDestructive="True"
                                   HasSeparatorBefore="True" />
                </arc:SplitButton>
            </Grid>
        </StackPanel>
    </Border>
</StackPanel>
```

### Content guidelines

- **Main label**: a verb and an object, in sentence case: "Merge pull request", "Export as CSV", "Send invoice". It
  names the action the default performs, so the person knows what a click does before reading the menu.
- **Menu rows**: the same grammar as the main label, and the same nouns. A destructive row names its consequence:
  "Void invoice", not "Delete".
- **Chevron name**: the default `{Label} more actions` is correct for most controls. Set `MenuButtonLabel` when the label
  is a generic verb such as "Send" or "Export", so the name says what the menu offers.
- **Result labels**: one word that confirms the action, such as "Copied" or "Sent", in the same tense as the default.

| Instead of | Write |
|---|---|
| Click here to send | Send invoice |
| Export | Export as CSV |
| Delete invoice (in the menu) | Void invoice |
| Copied successfully! | Copied |
| More options (chevron name) | Other ways to send |

### Best practices

**Do**

- Put the action that most people take in the main half. A split button whose default is rarely used teaches people to
  open the menu every time.
- Put a destructive row last, below a separator, so a misclick on the chevron does not reach it.
- Bind `Command` for the default action and keep the menu rows to actions that can run in the same context.
- Keep the label short enough that the main half stays under the width of the toolbar it sits in.

**Avoid**

- Using a split button when every option is equal. Without a default, the primary half implies a choice the person
  has not made; use `DropdownMenu`.
- Disabling the control to hide an action that needs a precondition. Disabled controls teach nothing; say why
  nearby.
- Using several different split buttons in one toolbar. The menus compete, and the person has to read every
  chevron.
- Changing the label for a long process. A result label confirms a finished action; a running process needs
  progress.
- Putting the `Secondary` variant next to `Primary` controls in one row. The two weights read as different levels of
  importance.

### Common questions

**How do I change the accessible name of the chevron?** Set `MenuButtonLabel`. When it is `null` (the default), the
name is `{Label} more actions`.

**What does Enter do on the chevron?** It opens the menu with the first row highlighted. The main action runs only on
the main half.

**Can the code open or close the menu?** Yes. Call `Open()` to open it with focus on the surface, or `Open(true)` to
highlight the first row. `Close()` closes it without returning focus. `IsOpen` is two-way, so binding it is the same
as calling those methods.

**Does Click run before Command?** Yes. `Click` is raised first, then the command runs. Use `Click` for state the
command depends on, and keep the command for the work itself.

**Why does a row not look destructive?** Set `IsDestructive="True"` on the `MenuEntry`. The row takes the danger color,
and `HasSeparatorBefore="True"` adds the separator above it.

## API reference

<!-- api: SplitButton -->

<!-- api: MenuEntry -->

`SplitButtonVariant` has two values: `Primary` (the filled default) and `Secondary` (the outlined pill). The event
argument of `ItemClicked` is `MenuEntryClickedEventArgs`, which exposes `Entry` (the activated row), `Item` (the data
item behind the row, or the entry itself for inline rows) and `Header` (the row's header).

## Accessibility

- The main half and the chevron are two buttons. The main half is named by `Label`. The chevron is named
  `{Label} more actions`, unless `MenuButtonLabel` sets another name, and reports whether the menu is expanded.
- The menu uses the Menu and MenuItem roles. Arrow keys move through the rows and loop at the ends.
- A change of `Label` is announced politely, so a result such as "Copied" reaches a screen reader without moving focus.
- Set `MenuButtonLabel` when the label alone does not say what the chevron offers. "Send invoice more actions" is
  accurate but long; "Other ways to send" says what the menu contains.
- The focus ring follows each half, so keyboard users see which half will act.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Moves between the main half and the chevron, then to the next control. |
| `Enter` / `Space` on the main half | Raises `Click` and runs `Command`. |
| `Enter` / `Space` on the chevron | Opens the menu with its first row highlighted. |
| `ArrowDown` on the chevron | Opens the menu with its first row highlighted. |
| `ArrowDown` / `ArrowUp` in the menu | Moves to the next or previous row. Disabled rows are skipped, and the movement loops at both ends. |
| `Escape` | Closes the menu and returns focus to the chevron. |

## Motion

- The label changes letter by letter: shared letters glide, and changed letters rise out of a soft blur. The icon
  crossfades, and the main half springs to the new width.
- The main half presses in slightly. The chevron never scales, and it turns while the menu is open.
- The menu opens 4px below the chevron and its rows fade in one after another.
- Reduced motion removes the press, the width spring and the menu transform, and keeps a short fade. Off shows the
  menu and the label change at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Label changes | Shared letters glide, changed letters rise out of a soft blur, removed letters lift and fade | Crossfades in 120 ms | Replaces at once |
| Icon changes | The new icon grows in from a smaller size out of a subtle blur; the old one shrinks away | A short fade | Immediate |
| Main half pressed | The whole pill presses in slightly | No press | Immediate |
| Menu opens | Opens 4 px below the chevron, and its rows fade in one after another | A short fade without the transform | Immediate |

## Related

- `DropdownMenu` for a menu of equal options with no default action.
- `Button` for a single action without alternatives.
- `ButtonGroup` for several related actions shown side by side.
- `MenuEntry` is the row type of both the split button and the menus.

| Component | Choose it when |
|---|---|
| `DropdownMenu` | All options are equal and none is the default |
| `Button` | There is one action and nothing to choose |
| `ButtonGroup` | Several related actions sit side by side without a default |
| `FloatingButtonGroup` | Icon actions float over a canvas or a selection |
| `SegmentedControl` | A value stays selected, such as a period or a view |

