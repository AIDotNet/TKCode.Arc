---
title: Card stack
summary: Review a deck one card at a time, with a throw and an undo.
---

<Demo name="Shortlist" />

## When to use

- Sorting a queue of items one at a time, such as a shortlist, a review queue or a set of preferences.
- Each card needs a clear yes or no, and the reader may take a decision back.
- The deck can be reviewed again from the start once it is empty.

## When not to use

- Comparing items side by side or sorting them: use `SortableDataTable`.
- Browsing without deciding: use `Carousel`.
- Quick actions on a row of a list: use `SwipeActions`.

## Usage

`ItemsSource` supplies the cards, and `ItemTemplate` draws each card face. `Label` names the stack.

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

### Naming cards

Set `LabelSelector` to return the name of each card. The name is used for the card's accessible label and in
announcements. When the items are not strings, the selector must return a string.

```csharp
stack.LabelSelector = item => ((Destination)item!).Name;
```

### Custom button and stamp text

`LeftLabel` and `RightLabel` name the buttons and the stamps that appear while a card is dragged. `LeftOutcome` and
`RightOutcome` are the past-tense words used in announcements, such as "skipped".

<Demo name="CustomActions" />

### Deciding and undoing in code

`Decide` throws the top card in the given direction. `Undo` brings the last card back, and `CanUndo` reports whether
there is one to bring back. `Reset` returns every card to the stack.

```csharp
stack.Decided += (_, e) => log.Text = $"{((Destination)e.Item!).Name}: {(e.Decision == CardDecision.Right ? "shortlisted" : "skipped")}";
stack.DecisionUndone += (_, e) => log.Text = $"{((Destination)e.Item!).Name}: restored";
stack.StackReset += (_, _) => log.Text = "All cards are back in the stack.";

stack.Decide(CardDecision.Right);
stack.Undo();
stack.Reset();
```

### Empty state

`EmptyContent` replaces the default message that shows once every card is decided. Bind a button to
`ResetCommand` to offer a new pass.

```xml
<arc:CardStack Label="Candidates" ItemsSource="{Binding Candidates}">
    <arc:CardStack.EmptyContent>
        <Button Content="Review again" Command="{Binding ResetCommand, RelativeSource={RelativeSource AncestorType=arc:CardStack}}" />
    </arc:CardStack.EmptyContent>
</arc:CardStack>
```

### Photos

Card faces can show `AsyncImage` photos over a drawn scene, so they still look complete offline.

<Demo name="Photos" />

## API reference

<!-- api: CardStack -->

## Accessibility

- The stage is focusable and announced as a "card stack". Each card has a name such as "Lisbon, 1 of 3".
- When the stage has focus, Left and Right arrows decide the top card. Backspace, Ctrl + Z and Cmd + Z undo.
- The buttons mirror every gesture, so a swipe is never the only way to decide. A disabled undo button keeps focus.
- Decisions, undos and resets are announced politely.
- The focus ring is drawn on the top card, or on the empty state.

## Motion

- The top card follows the drag, tilts toward the direction of travel and shows its stamp. A fast flick or a long
  drag throws the card off the stack with its release speed.
- The cards behind rise into place as the top card leaves.
- Undo flies the last card back from the point where it left. Reset brings the cards back one after another, from
  the deepest one.
- A short drag springs back to the stack.
- Reduced motion fades a card out and in on the same spot, without movement. Off jumps to the end state at once.

## Related

- `Carousel` for browsing a row of items without deciding on each.
- `SwipeActions` for quick actions on a row in a list.
- `SortableDataTable` for reviewing many records side by side.
