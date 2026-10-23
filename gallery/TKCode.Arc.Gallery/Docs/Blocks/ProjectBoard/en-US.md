---
title: Project board
summary: A compact sprint board: arrow buttons step each card to the next or previous stage and it flies to its new column, under a sprint line, the team and an All work / My work switch.
---

<Demo name="Preview" />

## When to use

`ProjectBoard` is a small board for one sprint or one project: a handful of stages as columns and a few dozen tasks as
cards. Each card has two arrow buttons that send its task one stage left or right; the card flies in a straight line to
the end of its new column while the column it left closes up. The header counts the finished tasks, a thin sprint line
fills as tasks reach the last stage, the team sits beside an All work / My work switch, and Add task drops a new card into
the first stage.

- **A team or project page.** The board answers "where is everything" at a glance and lets people move their own work
  without leaving the page.
- **A dashboard tile.** Framed, it sits among other cards; flat, it fills a page of its own.
- **Touch and keyboard first.** Moving a card is a button press, not a drag: it works the same with a mouse, a finger,
  Tab and Enter, or a screen reader, and every move is announced.

## When not to use

- **Large boards.** Every card is a live element and there is no virtualization; keep it to a few dozen tasks. For
  hundreds of tasks, drag and drop between columns, swimlanes or work-in-progress limits, use a full kanban board.
- **Free reordering.** A move always lands at the end of the next or previous column; cards cannot be dragged into a
  position or skip a stage from the card. Call `MoveTask` with an index from your own controls when you need that.
- **Editing.** Cards show a task, they do not edit it. Open your own form from `AddRequested` or from a selection
  elsewhere on the page.
- **Reporting.** For burn-down charts or comparisons across sprints use a chart; the sprint line only shows one ratio.

## Usage

### Stages, tasks and people

`Stages` are the columns, left to right; two to six read well. Each `ProjectBoardStage` has a `Key` that tasks refer to
and a `Header`. The stage marked `IsDone` counts as finished (without one, the last stage does): its tasks fill the sprint
line, their titles turn quiet and their due labels stop warning.

Each `ProjectBoardTask` is a card: `Area` and `DueText` at the top (`IsDueSoon` shows the due label in the warning color),
`Title` in the middle, then the `Owner`'s avatar, the `CommentCount` (hidden at zero) and the two arrows. `StageKey`
names the stage; a key that matches no stage shows the card in the first column. Each column shows its stage's tasks in
the order of the collection.

People are `ProjectPerson` objects: a name and a key, with an optional picture. Without a picture the board draws the
initials on a soft color picked from the key, so the same person keeps the same color. `Team` is the row of avatars in the
header and `CurrentUser` is "me": the person My work filters for and the owner of new drafts.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ProjectBoard Title="Launch prep">
        <arc:ProjectBoard.Stages>
            <arc:ProjectBoardStage Key="planned" Header="Planned" />
            <arc:ProjectBoardStage Key="progress" Header="In progress" />
            <arc:ProjectBoardStage Key="review" Header="Review" />
            <arc:ProjectBoardStage Key="done" Header="Done" IsDone="True" />
        </arc:ProjectBoard.Stages>
        <arc:ProjectBoardTask Title="Split the settings API" StageKey="progress" Area="Platform" DueText="Wed" CommentCount="6" />
        <arc:ProjectBoardTask Title="Contrast audit for receipts" StageKey="review" Area="Checkout" DueText="Today" IsDueSoon="True" />
    </arc:ProjectBoard>
</UserControl>
```

```csharp
var rhea = new ProjectPerson("Rhea Calloway", "rhea");
board.Team.AddRange([rhea, new ProjectPerson("Tomas Vierra", "tomas")]);
board.CurrentUser = rhea;
board.TasksSource = sprint.Tasks; // an ObservableCollection<ProjectBoardTask>
```

`Tasks` is for tasks written in XAML or added in code; `TasksSource` takes your own collection and wins when set. A
collection that raises change notifications is observed, and a writable one is reordered by moves and receives new tasks,
so it always matches what the board shows. Changing a task's `StageKey` from outside moves its card too.

### Moving tasks

The right arrow sends a task to the next stage and the left arrow to the previous one; the arrow that would leave the board
is disabled. A moved task goes to the end of its new column, in both directions. `TaskMoving` comes first and can keep the
task where it is (`Cancel`); `TaskMoved` follows as the card starts to fly, with the stages and the positions before and
after, so it is the place to save the change. `MoveCommand` runs after a move made with the arrows, with a
`ProjectBoardMoveRequest`.

`MoveNext`, `MovePrevious` and `MoveTask` do the same from code; `MoveTask` can go to any stage and, with an index, to a
position in it. `FocusTask` puts the keyboard on a card and `ScrollToStage` brings a column into view.

<Demo name="Persisted" />

```csharp
board.TaskMoving += (_, e) =>
{
    if (e.ToStage.Key == "review" && e.Task.CommentCount == 0)
    {
        e.Cancel = true; // the app's rule: review needs a comment first
    }
};
board.TaskMoved += (_, e) => SaveStage(e.Task.Key, e.ToStage.Key, e.ToIndex);

void SaveStage(string taskKey, string stageKey, int position)
{
    // Send the change to your store; the board has already moved the card.
}
```

### All work and My work

With a `CurrentUser`, the header shows the switch. My work keeps the cards the current user owns (`Filter` is
`ProjectBoardFilter.Mine`); the others fade out where they are while the rest close up, and they fade back in when the
switch returns to All work. Columns count what they show and say "Nothing assigned to you" when they are empty; the header
and the sprint line always count every task. `Filter` is two-way and raises `FilterChanged`. Without a `CurrentUser` the
switch is hidden.

### Adding tasks

Add task raises `AddRequested`. Put a task in the event's `Task` to add it, or set `Handled` and add nothing, for
example to open your own form first and call `AddTask(task, stageKey)` when it is filled in. Without a handler the board
adds a draft: the next title of `DraftTitles`, with `DraftArea`, `NoDateText` as its due label and the current user as its
owner. New tasks go to the end of `AddStageKey` (the first stage by default), fade in, raise `TaskAdded`, run `AddCommand`
and are announced; focus stays on the Add button for the next one. `IsAddVisible` hides the button.

### Your own stages

Stages are data, so a board can follow any workflow. A stage's `EmptyText` replaces the board's empty texts for that
column.

<Demo name="CustomStages" />

### Framed or flat

`IsFramed` draws the board as a card with a 1px border and a 24px radius, for a board that sits among other content. The
default is flat: the board fills its space with the surface color, like a page of its own. `Title` and the progress
sentence are optional (`IsProgressVisible` hides the sentence and the line).

<Demo name="Framed" />

### Narrow containers

The board reads its own width, not the window's. Columns share the width down to 200 each (`MinColumnWidth`); below that
they scroll sideways. At 700 and below (`NarrowWidth`) each column is the width less 76, so the next one peeks in, the
team row is hidden, and the filter, Add and arrow buttons grow to 40px touch targets without making the cards taller. At
400 and below (`CompactWidth`) the actions take a row of their own and Add shows only its icon, keeping its name as a
tooltip. After scrolling stops, an offset within a third of a column of a column's start settles on it; a moved card's
column scrolls into view so the card can be followed.

<Demo name="Narrow" />

### Texts

Every text has an English default and a property: the progress sentence (`ProgressFormat`, "{0} of {1} shipped this
sprint"), the switch (`AllWorkText`, `MyWorkText`, `FilterLabel`), `TeamLabel`, `AddText`, the empty texts (`EmptyText`,
`EmptyMineText`), the drafts (`DraftTitles`, `NewTaskTitle`, `DraftArea`, `NoDateText`), and what screen readers hear
(`ColumnLabelFormat`, `ColumnLabelSingularFormat`, `MoveToFormat`, `MovePreviousText`, `MoveNextText`, `MovedFormat`,
`AddedFormat`, `DueLabelFormat`, `OwnerLabelFormat`, `CommentsFormat`, `CommentsSingularFormat`). In XAML a format that
starts with a brace needs `{}` in front.

```xml
<arc:ProjectBoard Title="Lancement"
                  ProgressFormat="{}{0} sur {1} livrées ce sprint"
                  AllWorkText="Tout"
                  MyWorkText="Mes tâches"
                  AddText="Ajouter"
                  MoveToFormat="Déplacer {0} vers {1}"
                  MovedFormat="{}{0} est maintenant dans {1}." />
```

## API reference

<!-- api: ProjectBoard -->

<!-- api: ProjectBoardStage -->

<!-- api: ProjectBoardTask -->

<!-- api: ProjectPerson -->

<!-- api: ProjectBoardMoveRequest -->

### Events

| Event | Raised when | Arguments |
|---|---|---|
| `TaskMoving` | A task is about to change stage | `Task`, `FromStage`, `ToStage`, `Cancel` |
| `TaskMoved` | A task changed stage, as its card starts to fly | `Task`, `FromStage`, `ToStage`, `FromIndex`, `ToIndex` |
| `AddRequested` | Add task was pressed | `Stage`, `Task` (set it to add your own), `Handled` |
| `TaskAdded` | The board added a task | `Task`, `Stage` |
| `FilterChanged` | `Filter` changed | `OldFilter`, `NewFilter` |

## Accessibility

- Keyboard: `Tab` and `Shift+Tab` go through All work, My work, Add task, then every enabled arrow, column by column
  and top to bottom; `Enter` or `Space` presses the focused button (chooses a filter, adds a task or moves the card).
- The board is a group named "Project board". Each column is a group named after its stage and the number of cards it
  shows ("Review, 1 task"); its cards are list items named after the task, its area, due label, owner and comments
  ("Split the settings API, Platform, due Wed, assigned to Rhea Calloway, 6 comments").
- The arrows are named after where they send the task ("Move Split the settings API to Review"); a disabled arrow says
  "the previous stage" or "the next stage". The switch is a group named "Show" with two toggle buttons that report which one
  is pressed. The team avatars are named "Team", the Add button "Add task" even when only its icon shows.
- Moves and adds are announced politely ("Moved Split the settings API to Review.", "Added Draft the pricing FAQ to
  Planned."); filtering is not, the column names already change.
- After a move, focus stays on the same arrow of the same card in its new column, or moves to the other arrow when that one
  turned off at the edge, so pressing Enter again keeps going; on narrow boards the column scrolls into view.
- Hover styles follow a mouse or a pen only; a finger never leaves a button looking hovered. Status never relies on color
  alone: the due label keeps its text, finished cards stay in the finished column.

## Motion

- **Moves**: the card flies from where it is drawn to the end of its new column in a straight line, on a Gentle spring
  (stiffness 340, damping 34, about 600 ms), above every other card. The cards under it in the old column slide up on the
  same spring, and both slots follow them: a closing slot never leaves a card hanging below it. A second press mid-flight
  starts again from where the card is drawn.
- **Filtering**: the highlight glides to the chosen button on a critically damped spring (stiffness 400, damping 40). Cards
  that leave fade out and shrink to 98% where they are while the others close up; cards that come back fade in and grow
  from 98%, all on the Gentle spring.
- **Adding**: the new card fades in and grows from 98%; the sprint line springs to its new ratio (also when tasks reach or
  leave the finished stage).
- **Hover and press**: colors change over 160 ms; the arrows press to 92%, Add to 97%, and Add dims to 88% under a mouse.
- Reduced motion and `AnimatesMoves="False"` apply moves, adds and filters in place at once and keep the color changes.
  Off applies everything at once. Nothing runs while the board is still.

## Related

- Release readiness, Kanban board, Week calendar and Availability picker, the other Projects blocks, share its people,
  avatars and progress line.
- `SegmentedControl` for a view switch outside the block.
- `AvatarGroup` for a stack of avatars elsewhere on the page.
- `LayoutMorph` for one set of cards in several layouts.
