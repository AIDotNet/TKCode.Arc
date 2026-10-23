---
title: Card
summary: A contained group of related content and actions.
---

<Demo name="QuickLook" />

## When to use

`Card` is the tile for a browsable collection: a grid of projects, listings, posts or workspaces where each item has
a visual, a title, an owner and a status that changes over time. It groups those parts and the few actions that
belong to the item into one contained surface, so a grid reads as a set of equal units. A card can also open a quick
look, which grows the tile into a larger view of the same item without leaving the page.

- **Project and workspace grids** in an admin console: a card per project with its cover image, the owner's avatar
  and a status such as "Updated 2 hours ago". The footer holds one action, such as "Open project".
- **Catalog and listing tiles** in a storefront or an internal product catalog: a product photo, a name, a short line
  of copy and a trailing button. The media is the visual anchor of each tile.
- **Quick look at a record** in a CRM or a case-management grid: a card for a customer or a ticket opens to show its
  open questions and milestones. The grid keeps its place, and the person returns to the same tile when the view
  closes.
- **Live state on a tile** in a billing or deployment dashboard: a card whose `Status` changes as a job runs ("Syncing
  12 of 40 invoices", then "Synced just now"). New words rise in one after another and are announced politely.
- **Feeds of posts and release notes** in a developer portal or an editorial tool: a card per post with a byline
  (`Meta`) and a footer action, without the weight of a full table.

## When not to use

- **A single number with context** such as revenue, a conversion rate or open incidents: use `MetricCard`, which
  holds the value, its change and a sparkline.
- **Content that expands in place** without a dialog, such as a settings group that opens under its header: use
  `ExpandableCard`.
- **A modal with no tile to grow from**, such as a confirmation or a form opened from a toolbar button: use `Dialog`.
- **Triage of one item at a time** where people accept or reject each card: use `CardStack`, which handles drag,
  keys and undo for a deck of cards.
- **Many records compared by columns** such as invoices with amounts, dates and statuses: use `Table`. Cards hide the
  columns that make rows comparable.
- **A grid or list with nothing in it yet**: show `EmptyState` in the grid area. An empty card is not a message.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Card Title="Harbour redesign"
              Description="New booking flow and room pages."
              Width="280" />
</UserControl>
```

`Card` derives from Avalonia's `ContentControl`. `Title` and `Description` give it the heading and the supporting line,
and `Content` takes anything else, placed between the description and the footer. Each optional part is a typed
property, so a card with an avatar, a status and an action needs no extra layout: the card arranges the footer, hides
the parts that are empty and keeps the same spacing across a grid.

### Anatomy

1. **Media** (`Media`): an image or visual at the top, with a minimum height of 112 px. It is hidden while the
   property is empty.
2. **Title** (`Title`): 18 px, medium weight, 1.3 line height, at heading level 3. With `Details` set, the title
   becomes a button that opens the quick look.
3. **Description** (`Description`): 14 px body text, 8 px under the title. Its line length is capped at about 34
   digit widths, so longer copy wraps to more lines instead of stretching the card.
4. **Content** (`Content`): any element between the description and the footer, such as a list of tags or a progress
   bar. It is hidden while `Content` is empty.
5. **Footer**: 20 px below the content. It appears when `Avatar`, `Meta`, `Status` or `Action` has a value.
6. **Avatar** (`Avatar`): a leading slot 32 px wide for the owner's `Avatar` control, with 12 px gaps to the byline and
   the action.
7. **Byline** (`Meta` and `Status`): `Meta` is the owner's line in 12 px text. `Status` sits under it on a single line
   that clips at the edge and ends in an ellipsis when it is too long.
8. **Action** (`Action`): a trailing control, usually a small button. It keeps its own focus and click target and does
   not open the quick look.
9. **Surface**: a 1 px border on the surface color, 26 px corners and 20 px of content padding. The shadow is off at
   rest and appears on hover.

### Parts and combinations

Arc has no `Variant` property for the card. Its variants are combinations of the optional parts, chosen per item.

| Combination | Parts to set | Use it for |
|---|---|---|
| Plain | `Title`, `Description` | A reference tile, a link list, a summary without a visual |
| Media and byline | `Media`, `Avatar`, `Meta` | Grids where every item has a picture and an owner |
| Live | `Status` on top of the above | Items whose state changes while the page is open |
| Interactive | `Details` on top of the above | Items that open to show more, such as a record or a project brief |

A plain card with only a title and a description is under 80 px high at a width of 240 px, so a grid of plain cards
stays dense. Do not mix plain and media cards in one row unless their heights match: the grid cell stretches the card,
and empty space below short copy looks like a layout error.

### Media, owner and footer

Give every card in a grid the same media height, so the titles line up across the row. Pass a `Small` avatar in the
`Avatar` slot, which is sized for the 32 px slot. `Meta` is the owner's name; use one line of text. The action is one
control. If a card needs a second action, place it in `Content` or in `Details`, so the footer keeps one trailing
target.

```xml
<arc:Card Title="Harbour redesign"
          Description="New booking flow and room pages."
          Meta="Maya Chen"
          Status="Updated 2 hours ago"
          Width="280">
    <arc:Card.Media>
        <Border Height="132" Background="#5B8DEF" />
    </arc:Card.Media>
    <arc:Card.Avatar>
        <arc:Avatar DisplayName="Maya Chen" Size="Small" />
    </arc:Card.Avatar>
    <arc:Card.Action>
        <arc:Button Content="Share" Variant="Ghost" Size="Small" />
    </arc:Card.Action>
</arc:Card>
```

### Status

`Status` is a short line such as "Updated 2 hours ago". When the text changes, the old line lifts away and the new
words rise in one after another, about 40 ms apart. The line is drawn with cached text and creates no controls per
word, so a card that updates often stays light.

<Demo name="StatusUpdates" />

```xml
<arc:Card Title="Weekly digest"
          Description="Highlights from every team, every Friday."
          Status="{Binding LastUpdate}"
          Width="300" />
```

Write the status as a fact with a time: "Syncing 12 of 40 invoices", "Published to 4 teams", "Updated 2 hours ago".
Change the string only when the state changes; re-setting the same text on a timer replays the animation and conveys
nothing new. Setting `Status` to `null` or an empty string hides the line. The roll plays only when an existing status
is replaced, so the first value appears at once.

### Quick look

Set `Details` and the title becomes a button, and the whole card becomes a trigger: a press on the card that is not on
a nested control opens the quick look. The surface, media, title, description, byline and action fly into a larger
view on the same page, and the details rise in after them.

<Demo name="Controlled" />

```xml
<arc:Card Title="Quarterly plan"
          Description="Goals, owners and dates for the next three months."
          IsDetailsOpen="{Binding IsPlanOpen, Mode=TwoWay}"
          Width="280">
    <arc:Card.Details>
        <TextBlock Text="Opened from code: IsDetailsOpen is two-way." TextWrapping="Wrap" />
    </arc:Card.Details>
</arc:Card>
```

`Details` holds the content of the larger view and can be any control. The quick look is 480 px wide at most, and the
window keeps a 16 px gutter around it. Keep the details to what a person needs to decide whether to open the record:
a summary, the next step and one or two links. A full editing form belongs in `Dialog` or on a page.

The quick look repeats the card's copy and footer (its title is 22 px, the media sits on top with a floating close
button), so the same text is not written twice. Put only the extra material in `Details`.

Use `OpenDetails()` and `CloseDetails()` to open and close the quick look from code, and `DetailsOpenChanged` to learn
why it changed. `IsDetailsOpen` is two-way, so a binding can open or close it, and the event reports every change.

```csharp
card.DetailsOpenChanged += (_, e) => log.Text = e.IsOpen ? "Open" : $"Closed ({e.Reason})";
card.OpenDetails();
card.CloseDetails();
```

The reason is `Programmatic` for a change made by code, `CloseButton` for the close button, `Escape` for the Escape
key and `PressOutside` for a press on the scrim behind the quick look.

### Without a quick look

Without `Details` the title is a plain heading and the card only lifts on hover. The title takes no focus and a press
on the card does nothing, so the only stops are the controls inside the card. Use this form for tiles that show
information or hold a footer action.

<Demo name="WithoutQuickLook" />

### Binding to a view model

A project grid binds one card per item. The view model owns each item's state and the card shows it. With
CommunityToolkit.Mvvm, an `ObservableObject` holds the properties and an `ObservableCollection<T>` feeds the grid.

```csharp
public partial class ProjectCardViewModel : ObservableObject
{
    public ProjectCardViewModel(string title, string owner)
    {
        _title = title;
        _owner = owner;
    }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _owner;

    [ObservableProperty]
    private string? _status;

    [ObservableProperty]
    private bool _isBriefOpen;

    [ObservableProperty]
    private string? _brief;

    [RelayCommand]
    private async Task ShareAsync()
    {
        await _sharing.ShareAsync(Title);
    }
}

public partial class ProjectsViewModel : ObservableObject
{
    public ObservableCollection<ProjectCardViewModel> Projects { get; } = new();
}
```

```xml
<ItemsControl ItemsSource="{Binding Projects}">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
            <WrapPanel ItemSpacing="16" LineSpacing="16" />
        </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <arc:Card Title="{Binding Title}"
                      Description="{Binding Brief}"
                      Meta="{Binding Owner}"
                      Status="{Binding Status}"
                      IsDetailsOpen="{Binding IsBriefOpen, Mode=TwoWay}"
                      Width="280"
                      VerticalAlignment="Top">
                <arc:Card.Action>
                    <arc:Button Content="Share" Variant="Ghost" Size="Small"
                                Command="{Binding ShareCommand}" />
                </arc:Card.Action>
                <arc:Card.Details>
                    <TextBlock Text="{Binding Brief}" TextWrapping="Wrap" />
                </arc:Card.Details>
            </arc:Card>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

- `Title`, `Description`, `Meta` and `Status` are one-way: the view model owns them and the card does not write them.
- `IsDetailsOpen` is two-way: a press or the close button writes `False` back, and code can set it to open the quick
  look. Bind it with `Mode=TwoWay` only when the view model must know that the person closed the view.
- Use `DetailsOpenChanged` for a side effect that needs the reason, such as saving a draft after `Escape`. Use the
  binding for the state itself.
- The `Action` is a normal button with its own `Command`. Its press does not open the quick look.
- Compiled bindings need `x:DataType` on the template and the view. The examples leave it out of the view for brevity.

### Data shape and performance

A quick look is created the first time it opens, so a grid whose cards nobody opens pays nothing extra for
`Details`. Keep the `Details` content light: it is built on the first open. For long collections, host the cards in a virtualizing panel, so off-screen cards are not created.

Set the width on the card or on its grid column. The card fills the cell it is given (its minimum width is 0), so a
fixed width of 260 to 300 px gives a regular grid, and a `UniformGrid` keeps columns equal as the window resizes.

### Focus and dismissal

A card with `Details` is one tab stop: the title button. Enter or Space on it opens the quick look, and focus moves to
the close button inside. While the quick look is open, Tab and Shift+Tab stay inside it, so the page behind is not
reachable by keyboard. Escape, a press on the scrim and the close button all close it, and closing returns focus to
the title. The close button is named "Close quick look" by default; set `CloseButtonName` when the copy needs to say
what closes, for example "Close project brief".

A press on the `Action` button runs its command and does not open the quick look. If the card leaves the window while
the quick look is open, the quick look is removed and `IsDetailsOpen` becomes `False`.

### Example: project grid in a workspace

A project list on a workspace home page. Each card shows a cover, the owner and the last update, and the footer holds
one "Open" action. The titles are nouns, the descriptions are one sentence, and the status is a fact with a
time. Both cards share one width and align at the top, so the row reads as one set.

![Two project cards in a workspace grid, one with a live status line](images/ProjectGrid.png)

```xml
<StackPanel Width="640" Orientation="Horizontal" Spacing="20">
    <arc:Card Title="Harbour redesign"
              Description="New booking flow and room pages for the 2026 release."
              Meta="Maya Chen"
              Status="Updated 2 hours ago"
              Width="300"
              VerticalAlignment="Top">
        <arc:Card.Media>
            <Border Height="132">
                <Border.Background>
                    <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,100%">
                        <GradientStop Color="#5B8DEF" Offset="0" />
                        <GradientStop Color="#9CC2FF" Offset="1" />
                    </LinearGradientBrush>
                </Border.Background>
            </Border>
        </arc:Card.Media>
        <arc:Card.Avatar>
            <arc:Avatar DisplayName="Maya Chen" Size="Small" />
        </arc:Card.Avatar>
        <arc:Card.Action>
            <arc:Button Content="Open" Variant="Secondary" Size="Small" />
        </arc:Card.Action>
    </arc:Card>
    <arc:Card Title="Atlas API"
              Description="Endpoint inventory and examples for the public docs."
              Meta="Jonas Berg"
              Status="Syncing 12 of 40 pages"
              Width="300"
              VerticalAlignment="Top">
        <arc:Card.Media>
            <Border Height="132">
                <Border.Background>
                    <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,100%">
                        <GradientStop Color="#2FA88B" Offset="0" />
                        <GradientStop Color="#8EE0C6" Offset="1" />
                    </LinearGradientBrush>
                </Border.Background>
            </Border>
        </arc:Card.Media>
        <arc:Card.Avatar>
            <arc:Avatar DisplayName="Jonas Berg" Size="Small" />
        </arc:Card.Avatar>
        <arc:Card.Action>
            <arc:Button Content="Open" Variant="Secondary" Size="Small" />
        </arc:Card.Action>
    </arc:Card>
</StackPanel>
```

### Content guidelines

- **Title**: a short noun phrase in sentence case that names the item: "Harbour redesign", "Q3 invoices". The title is
  the card's accessible name, so it must make sense out of context.
- **Description**: one sentence about what the item is or holds. It wraps at about 34 digit widths, so keep it to two
  lines at most. Do not repeat the title.
- **Status**: a fact with a time, in sentence case: "Updated 2 hours ago", "Syncing 12 of 40 invoices". Use digits for
  counts and avoid vague words such as "Recently".
- **Action**: a verb, with the object when the card does not name it: "Open", "Share report".

| Instead of | Write |
|---|---|
| Click here to see more | Open project |
| Lots of stuff happening | Syncing 12 of 40 invoices |
| Recently updated | Updated 2 hours ago |
| This card shows a project | Harbour redesign |

### Best practices

**Do**

- Set `Details` only when the item has more to show than the card. A quick look that repeats the page adds nothing.
- Keep every card in a grid at one width and one media height, so the titles and footers line up.
- Use `Status` for state that changes while the page is open, and keep the wording stable between changes.
- Set `CloseButtonName` when the quick look holds a draft or a decision, so the close control names its outcome.

**Avoid**

- Long forms inside `Details`. The quick look is a bounded view meant for reading and one or two actions.
- More than one action in the footer. The footer has one trailing slot, and a second action competes with the press
  that opens the card.
- Re-setting `Status` to the same text on a timer. Each change replays the roll and draws attention without news.
- Relying on the hover lift to show that a card is clickable. Touch and pen never lift the card, so the title and the
  `Details` affordance must carry the meaning on their own.

### Common questions

**Why does pressing the card not open the quick look?** Only a card with `Details` opens on a press, and only when the
press is not on a nested control. Check that `Details` has a value, then check that the press is not on the `Action`.

**How do I open the quick look from a toolbar button?** Bind `IsDetailsOpen` to a view model property, or call
`OpenDetails()` from code. `CloseDetails()` closes it with the reason `Programmatic`.

**Why is the card not in the tab order?** The card itself is not focusable. With `Details`, the title button is the
tab stop; without it, the title is a heading and only the footer action takes focus.

**Does the first status animate?** No. The roll plays only when an existing status is replaced by new text. Setting the
first value, or clearing the status, changes the line at once.

**Why does the card not lift on touch?** The lift answers only a mouse pointer. Touch and pen get the same layout and
the same press behavior without the hover state.

## API reference

<!-- api: Card -->

## Accessibility

- The card is a group named by `Title`. Without `Details`, the title is a heading at level 3.
- With `Details`, the title is a button; Enter or Space opens the quick look.
- The quick look is a dialog named by its title, with the description as its help text. Its close button is named by
  `CloseButtonName`, "Close quick look" by default.
- Changes to `Status` are read out through a polite live region.
- Nested actions keep their own focus and names. Give an icon-only action a name with `AutomationProperties.Name`, and
  set alternative text on the images in `Media`, or mark them as decorative.
- The hover lift and the media zoom are visual only; the card has the same content and actions without them.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move between the title (with `Details`), the content's controls and the footer action |
| `Enter` or `Space` on the title | Open the quick look, with focus moving into it |
| `Tab` / `Shift+Tab` with the quick look open | Move among its controls only; focus stays inside the quick look |
| `Escape` | Close the quick look and return focus to the title |
| `Enter` or `Space` on the close button | Close the quick look with the reason `CloseButton` |

A card without `Details` takes no focus of its own; only its nested controls are stops.

## Motion

- On hover the card lifts 2px and its border and shadow deepen. The media slowly zooms to 1.04 (mouse only).
- With `Details`, the surface, media, title and byline fly into the quick look on one spring while the page dims behind
  a blur. Closing flies them back into the card.
- A new `Status` lifts the old line away while the new words rise in.
- Reduced motion removes the lift, zoom and morph; the quick look only fades. Off applies changes at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Hover on a card | Lifts 2px, border and shadow deepen | Border and shadow change at once, no lift | Immediate |
| Media on hover | Zooms to 1.04 over 960 ms, back in 480 ms | No zoom | Immediate |
| Quick look opens or closes | Surface, media and title fly on a spring (0.3 s open, 0.26 s close) | Fades in and out | Immediate |
| Status changes | Old line lifts away, new words rise one after another | Swapped at once | Immediate |
| Scrim behind the quick look | Fades in and out over the page | Fades in and out | Immediate |

## Related

- `MetricCard` for one number with its context and change.
- `ExpandableCard` for content that expands in place, without a dialog.
- `Dialog` for a modal with no card to grow from.
- `CardStack` for triaging a deck of cards one at a time.
- `EmptyState` for a grid or list that has no cards yet.

| Component | Choose it when |
|---|---|
| `MetricCard` | The content is one number with its change and a trend |
| `ExpandableCard` | The content opens in place inside the page, without a dialog |
| `Dialog` | The view is modal and there is no tile to grow from |
| `CardStack` | People decide on one card at a time, with drag, keys and undo |
| `Table` | The items are rows that need comparable columns |
| `EmptyState` | The grid or list has no items yet |
