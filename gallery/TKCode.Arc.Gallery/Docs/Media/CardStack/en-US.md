---
title: Card stack
summary: Review a deck one card at a time, with a throw and an undo.
---

<Demo name="Shortlist" />

## When to use

`CardStack` turns a queue of items into a deck that a person works through one card at a time. Each card asks for one
deliberate decision, pass or keep, and the next card rises into place. It fits a triage task where every item deserves
a moment of attention and a table would hide the details that matter, such as a product photo or a candidate summary.
Place it in the main area of a focused task, and show the running result, such as a count of kept items, beside it.

- **Merchandising approval**: a catalog team clears new product listings before they publish. Each card shows the
  photo, price and supplier, and the reviewer approves or rejects it. Undo covers a listing approved by mistake.
- **Candidate screening in a recruiting tool**: a recruiter keeps or passes on applicant profiles. One card per
  candidate keeps the reviewer on one summary at a time instead of a row of ten half-read records.
- **Onboarding choices in a SaaS app**: a new workspace admin keeps or passes on each suggested integration, such as
  Slack, Jira or Google Drive. The deck ends with an "All cards reviewed" state instead of a long settings form.
- **Flagged payment review in a fintech console**: an analyst confirms or clears each flagged transaction. Each
  decision is announced as it happens, and Undo returns the last one to the top of the deck before the batch is
  submitted.

## When not to use

- **Comparing items side by side or sorting them**: use `SortableDataTable`. Columns and sorting let people compare
  many records at once; a deck shows one record at a time.
- **Browsing without deciding**: use `Carousel` for a row of slides, banners or photos that people look at but do not
  need to keep or pass.
- **Quick actions on a row of a list**: use `SwipeActions` for archive, snooze or mark-as-read on each row. A row is a
  place to act, not a queue that moves forward.
- **Reordering a list the person owns**: use `ReorderableList`, which keeps every item visible while it moves.
- **A single yes or no in a form**: use `Checkbox` or `Switch`, which sit inline with the other fields and need no
  queue, stamps or undo.

## Usage

```xml
<arc:CardStack Label="Destinations"
               LeftLabel="Pass"
               RightLabel="Keep"
               LeftOutcome="skipped"
               RightOutcome="shortlisted">
    <arc:CardStack.ItemTemplate>
        <DataTemplate>
            <Border Width="300" Height="360" Padding="20" CornerRadius="12" Background="#F5F1EA">
                <TextBlock Text="{Binding}" FontSize="18" />
            </Border>
        </DataTemplate>
    </arc:CardStack.ItemTemplate>
</arc:CardStack>
```

`CardStack` derives from Avalonia's `ItemsControl`, so `ItemsSource`, `Items`, `ItemTemplate` and the item container
logic work as they do in a list. Each item becomes one card, a `ContentControl` container that holds the item's
template. The stack lays out and realizes only the top four cards, plus any card still in flight, so a queue of 500
items costs about the same as a queue of five. `Label` names the stack for assistive technology.

The example above uses a fixed card size for brevity. In the theme the card is 304 px tall and the stage around it is
328 px tall, so a face built to those dimensions fills the stack without a gap.

### Anatomy

1. **Stage** (`Label` names it): the focusable group that holds the cards. It reserves the top card and the 24 px strip
   below it that the cards behind peek into, so nothing moves as cards leave. Its width is the smaller of the available
   width and 320 px.
2. **Top card**: the card that takes input, 304 px tall and 320 px wide, with the face from `ItemTemplate`. It is the
   only card that responds to a drag, a click or a key press.
3. **Cards behind**: up to three cards, each 5% smaller than the one in front and offset 12 px down, with a black shade
   at 3.5% opacity (22% in dark mode). They rise into place as the top card leaves.
4. **Stamp**: a 32 px pill in the top corner of the card, 16 px from its edges. It shows `LeftLabel` or `RightLabel`
   while the top card is dragged, and it is transparent at rest.
5. **Focus ring**: an outline drawn around the top card, or around the empty state, when the stage has keyboard focus.
   The stage itself draws no outline.
6. **Button row**: the `LeftLabel` button, the Undo button and the `RightLabel` button, 8 px apart and 20 px below the
   stage. The Undo button carries a rotate icon and the text "Undo"; below 22.5rem (360 px) it keeps only the icon.
7. **Empty state**: shown once every card is decided. It is a panel as tall as a card, with 24 px padding, a 12 px gap,
   the title "All cards reviewed" and a "Start over" button. `EmptyContent` replaces the title and the button.

Below 26rem (416 px) the stack switches to its compact layout: the cards are 280 px tall, the stage is 304 px and the
buttons use the compact size. The switch follows the stack's own width, not the window width, so a stack placed in a
narrow column changes layout even in a wide window.

### Card face and size

The card height is fixed by the theme, so the face must fit 304 px by 320 px. Keep it to a title, one or two lines of
detail and one image or icon. A long description belongs in a detail view that the person opens from the card. Use the
theme text sizes (`TypeSM` for body copy, `TypeBase` with `WeightMedium` for a title) so the face matches the rest of
the app.

### Binding to a view model

The view model owns the queue and records each decision. The stack has no command for deciding: drag, keyboard and
the buttons all raise the same `Decided` event, so one handler covers every input method. The view model holds the
collection, and the code-behind forwards each decision to it.

```csharp
public interface IShortlistService
{
    IEnumerable<Destination> LoadCandidates();

    Task SaveDecisionAsync(int id, bool keep);
}

public partial class ShortlistViewModel : ObservableObject
{
    private readonly IShortlistService _shortlist;

    public ShortlistViewModel(IShortlistService shortlist)
    {
        _shortlist = shortlist;
        Destinations = new ObservableCollection<Destination>(shortlist.LoadCandidates());
    }

    public ObservableCollection<Destination> Destinations { get; }

    [ObservableProperty]
    private string _lastDecision = "Drag a card, or focus the stack and press the arrow keys.";

    public async Task RecordAsync(Destination item, CardDecision decision)
    {
        await _shortlist.SaveDecisionAsync(item.Id, decision == CardDecision.Right);
        LastDecision = decision == CardDecision.Right
            ? $"{item.Name} kept."
            : $"{item.Name} passed.";
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:CardStack x:Name="Shortlist"
                   Label="Destinations"
                   ItemsSource="{Binding Destinations}">
        <arc:CardStack.ItemTemplate>
            <DataTemplate>
                <StackPanel Spacing="6" Margin="20">
                    <TextBlock Text="{Binding Name}" FontSize="18" FontWeight="Medium" />
                    <TextBlock Text="{Binding Details}" FontSize="14" />
                </StackPanel>
            </DataTemplate>
        </arc:CardStack.ItemTemplate>
    </arc:CardStack>
    <TextBlock Text="{Binding LastDecision}" />
</StackPanel>
```

```csharp
// Code-behind: forward the decisions to the view model.
Shortlist.LabelSelector = item => ((Destination)item!).Name;
Shortlist.Decided += async (_, e) =>
    await ViewModel.RecordAsync((Destination)e.Item!, e.Decision);
```

- `ItemsSource` is one-way: the stack reads the collection and never writes to it. `CurrentIndex` and `CanUndo` are
  read-only, so bind them one-way to show progress, for example "3 of 12".
- Use `Decided` and `DecisionUndone` for the decision itself. The event arguments carry the item and the decision, so
  the view model does not need to read the control's state.
- `ResetCommand` is an `ICommand` that restarts the deck. It can execute only while `CanUndo` is true, so a "Review
  again" button outside the stack stays disabled until a decision exists.
- Changing the collection, by adding, removing or replacing items, starts the stack over. Changing a property of an
  item does not, so update a record in place when its details change.

### Deciding and undoing in code

`Decide` throws the top card in the given direction with a default flick. `Undo` brings the last card back, and
`CanUndo` reports whether there is one to bring back. `Reset` returns every card to the stack.

```csharp
stack.Decided += (_, e) => log.Text = $"{((Destination)e.Item!).Name}: {(e.Decision == CardDecision.Right ? "shortlisted" : "skipped")}";
stack.DecisionUndone += (_, e) => log.Text = $"{((Destination)e.Item!).Name}: restored";
stack.StackReset += (_, _) => log.Text = "All cards are back in the stack.";

stack.Decide(CardDecision.Right);
stack.Undo();
stack.Reset();
```

Use these calls to drive the deck from a toolbar, a shortcut in the host app or a test without a pointer. A decision made
in code raises the same events as a drag or a key press, so the view model logic does not change.

### Naming cards

Set `LabelSelector` to return the name of each card. The name is the card's accessible label and appears in the
announcements. When the items are not strings, the selector must return a string.

```csharp
stack.LabelSelector = item => ((Destination)item!).Name;
```

Without a selector, the stack uses the item itself when it is a string, the automation name of a control item, or the
item's `ToString()`. Set the selector for any model class so the names do not depend on `ToString()`.

### Custom button and stamp text

`LeftLabel` and `RightLabel` name the buttons and the stamps that appear while a card is dragged. The defaults are
"Pass" and "Keep". `LeftOutcome` and `RightOutcome` are the past-tense words used in announcements, such as
"skipped". Without an outcome, the announcement names the action, for example "Lisbon: Keep. 4 left."

<Demo name="CustomActions" />

The labels are plain text, so write them as the verbs the person performs: "Not now" and "Interview" for a candidate
deck, "Reject" and "Approve" for a listing review. Keep each label short enough to sit beside the other button.

### Empty state

`EmptyContent` replaces the default message that shows once every card is decided. Bind a button to `ResetCommand` to
offer a new pass. `EmptyContentTemplate` builds the same content from a data template and is used only when
`EmptyContent` is not set.

```xml
<arc:CardStack Label="Candidates" ItemsSource="{Binding Candidates}">
    <arc:CardStack.EmptyContent>
        <Button Content="Review again" Command="{Binding ResetCommand, RelativeSource={RelativeSource AncestorType=arc:CardStack}}" />
    </arc:CardStack.EmptyContent>
</arc:CardStack>
```

### Photos

Card faces can show `AsyncImage` photos over a drawn scene, so the card still looks complete while the photo loads or
when the device is offline. Keep the photo inside the card's fixed height, and let the drawn scene stand in until the
image arrives.

<Demo name="Photos" />

### Data shape and performance

- **Item model**: any object works. Keep each item to what the face needs and load details on demand in a separate
  view. A large model costs no more than a small one, as long as the face template is light.
- **Item count**: the stack realizes four cards and any card still moving, so the number of items does not change the
  cost of a frame. The cost that matters is the face template, which runs for each of those four cards.
- **Updates**: adding, removing or replacing items restarts the deck from the first card. Plan updates so a person does
  not lose a queue they are working through: update a single record in place, and replace the collection only when the
  batch itself changes.
- **Undo history**: each decision is kept so `Undo` can bring it back, and undo steps run in reverse order, the most
  recent decision first.

### Example: product listing approval

A catalog team at a retailer clears new listings before they go live. Each card is one listing with a photo area, the
name, the price and the supplier. The reviewer approves or rejects the listing, and Undo recovers the last one. The
labels are the verbs of the review and the outcomes are the words in the announcement ("approved", "rejected"), so
the spoken status matches the buttons. The status line below the deck gives the reviewer the last action in plain
words. The card face uses fixed copy here so the screenshot is self-contained; in the app, bind the same elements to the
listing's data.

![A product listing card in a review deck with Reject, Undo and Approve buttons](images/ProductReview.png)

```xml
<StackPanel Width="560" Spacing="16">
    <TextBlock Text="New listings to review"
               FontSize="16"
               FontWeight="Medium"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:CardStack Label="Listings awaiting review"
                   LeftLabel="Reject"
                   RightLabel="Approve"
                   LeftOutcome="rejected"
                   RightOutcome="approved"
                   HorizontalAlignment="Center">
        <arc:CardStack.ItemTemplate>
            <DataTemplate>
                <StackPanel Spacing="12" Margin="20">
                    <Border Height="120"
                            CornerRadius="10"
                            Background="{arc:ArcDesignTokenResource SurfaceMuted}">
                        <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Package}"
                                      Size="48"
                                      HorizontalAlignment="Center"
                                      VerticalAlignment="Center"
                                      Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    </Border>
                    <TextBlock Text="Trail Runner 2"
                               FontSize="18"
                               FontWeight="Medium"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="Northwind · Footwear · $129.00"
                               FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                    <TextBlock Text="Submitted by Mia Chen on 2026-10-18"
                               FontSize="13"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                </StackPanel>
            </DataTemplate>
        </arc:CardStack.ItemTemplate>
        <x:String>Trail Runner 2</x:String>
        <x:String>Harbor Rain Shell</x:String>
        <x:String>Ridge Field Jacket</x:String>
    </arc:CardStack>
    <TextBlock Text="Drag a listing, or focus the deck and press the arrow keys."
               FontSize="13"
               HorizontalAlignment="Center"
               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
</StackPanel>
```

### Content guidelines

- **Stack label**: a noun phrase that names the queue, such as "Listings awaiting review". It is the only name a
  screen reader gives the stack, so "Review" alone is not enough.
- **Card name**: the item's name alone, because the stack adds the position: "Lisbon, 1 of 5".
- **Button labels**: a short verb that names the decision: "Pass" and "Keep", "Reject" and "Approve". Avoid answers
  such as "Yes" and "No", which do not say what happens to the item.
- **Outcomes**: past tense, so the announcement reads as a result: "Lisbon approved. 3 left."
- **Empty state**: say that the review is complete and offer a way to start again: "All cards reviewed" with
  "Start over".

| Instead of | Write |
|---|---|
| Yes / No | Approve / Reject |
| Left / Right | Pass / Keep |
| Done! | All cards reviewed. |
| Approved | Approved listing, or the item name with the outcome |
| Reset | Start over |

### Best practices

**Do**

- Give the stack a `Label` that names the queue. A screen reader announces it when the deck gets focus, so it is the
  first thing the person hears.
- Use past-tense `LeftOutcome` and `RightOutcome` words, so each decision is announced as a result.
- Record every decision from `Decided` in the view model. Drag, keyboard and buttons then persist the same way.
- Offer "Start over" or "Review again" in the empty state when the queue can be reused, so the end state leads
  somewhere.

**Avoid**

- Using the deck for records that need side-by-side comparison. A reader who must compare columns loses the context of
  the other items.
- Putting horizontal drags or other gestures inside a card. They compete with the card's own drag.
- Long text on the face. The card has a fixed height, and a face that does not fit is clipped.
- Relying on the stamp alone. Stamps appear only during a drag, so keyboard and screen reader users depend on the
  buttons and the announcements.
- Replacing the whole collection to refresh one record. That restarts the deck; update the record instead.

### Common questions

**Why does Undo stay disabled?** Undo is available only after a decision, and `CanUndo` is `False` again once the last
decision is undone. The button stays focusable while disabled, so keyboard users keep their place.

**How do I show how many cards are left?** Bind the count to the stack's `CurrentIndex` one-way and subtract it from
the item count, or read the "4 left" part of the announcement. The stack does not expose a remaining count property.

**Can I restart the deck from code after it is empty?** Yes. Call `Reset()`, or execute `ResetCommand`. Both bring
every card back, and the cards return from the deepest one first.

**Does editing one item restart the deck?** No. Changing a property of an item leaves the deck where it is. Adding,
removing or replacing items in the collection restarts it, so update records in place.

**Why does the card not tilt when I use the keyboard?** The tilt and the stamp follow the drag. A key press decides the
card with a default flick, so there is no drag to tilt it.

## API reference

<!-- api: CardStack -->

`CardDecision` and the event data are public types the page uses:

| Type | Member | Meaning |
|---|---|---|
| `CardDecision` | `Left` | The card was passed (`LeftLabel`, default "Pass"). |
| `CardDecision` | `Right` | The card was kept (`RightLabel`, default "Keep"). |
| `CardStackDecisionEventArgs` | `Item` | The decided or restored item. |
| `CardStackDecisionEventArgs` | `Index` | The item's index in the items. |
| `CardStackDecisionEventArgs` | `Decision` | The decision, `Left` or `Right`. |

## Accessibility

- The stage is a focusable group named by `Label` (default "Cards"), with the role "card stack". Its help text names
  the arrow keys and Backspace, and says the top card can be passed or kept.
- Each card is named by `LabelSelector` and its position in the stack, such as "Lisbon, 1 of 5".
- Decisions, undos and resets are announced politely, so a screen reader hears them without losing focus. Examples
  are "Lisbon skipped. 1 left.", "Kyoto restored." and "All cards are back in the stack."
- The buttons mirror every gesture, so a swipe is never the only way to decide. A disabled Undo button keeps focus and
  is announced as unavailable, so a keyboard user does not lose their place when no decision exists.
- The focus ring is drawn on the top card, or on the empty state, not on the stage.

### Keyboard

The stage takes focus with `Tab`. Arrow keys decide only while the stage itself has focus; when a button has focus,
the arrows act on the focused control as usual.

| Keys | Action |
|---|---|
| `Left` / `Right` | Pass or keep the top card when the stage has focus |
| `Backspace` | Undo the last decision |
| `Ctrl+Z` / `Cmd+Z` | Undo the last decision |
| `Tab` / `Shift+Tab` | Move between the stage and the buttons; Undo stays in the order while disabled |
| `Enter` or `Space` | Press the focused button: pass, undo, keep or start over |

Arrow keys with `Ctrl` or `Cmd` are not handled by the stack, so a shortcut such as `Cmd+Left` in the host app still
reaches the app.

## Motion

- The top card follows the drag, tilts toward the direction of travel and shows its stamp. A fast flick or a long
  drag throws the card off the stack with its release speed.
- The cards behind rise into place as the top card leaves.
- Undo flies the last card back from the point where it left. Reset brings the cards back one after another, from
  the deepest one.
- A short drag springs back to the stack.
- Reduced motion fades a card out and in on the same spot, without movement. Off jumps to the end state at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Top card released past the threshold | Thrown with its release speed, then fades out | Fades out in place | Removed at once |
| Cards behind after a decision | Rise into place as the top card leaves | Fade in place | Already in place |
| Undo | Flies back from where the card left | Fades in place | Shown at once |
| Start over | Cards deal back one after another, deepest first | Fade in place | Shown at once |
| Empty state appears | Settles in with a short move as the blur clears | Fades in | Shown at once |

## Related

- `Carousel` for browsing a row of items without deciding on each.
- `SwipeActions` for quick actions on a row in a list.
- `SortableDataTable` for reviewing many records side by side.

| Component | Choose it when |
|---|---|
| `SortableDataTable` | The reader compares many records by column or sorts them |
| `Carousel` | People browse slides, banners or photos without a decision |
| `SwipeActions` | Each row of a list needs a quick action such as archive or snooze |
| `ReorderableList` | The person changes the order of a list they own |
| `Checkbox` / `Switch` | A single yes or no sits inline in a form |
| `Stepper` | The task is a fixed sequence of steps rather than a queue of items |
