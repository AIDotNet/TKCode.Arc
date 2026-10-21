---
title: Comment thread
summary: Threaded comments with replies, reactions, mentions, inline edit, and resolve.
---

<Demo name="DesignReview" />

## When to use

`CommentThread` is the discussion attached to one object: a design frame, a document clause, a ticket or a pull request.
Each comment carries its replies, people react with emoji, authors edit and delete their own comments in place, and the
thread can be resolved once the question is settled. The thread stays with the object, so the reason for a decision is
found next to the decision itself.

- **Design review of a checkout screen**: a product designer and an engineer discuss the spacing of the order summary. The
  replies nest under the comment they answer, and the thread is resolved when the spacing is agreed.
- **Feedback on a legal clause**: a reviewer comments on one clause of a contract and mentions the owner with "@" so the
  person is notified. The resolved thread stays on the clause as the record of the decision.
- **Discussion on a support ticket**: an engineer and a support lead discuss the cause of an incident on ticket
  INC-2026-0931. Resolving the thread marks the question closed without deleting the discussion.
- **Questions on a metric**: a finance lead asks why the annual recurring revenue figure changed, and the analyst answers in
  a thread attached to that figure.

## When not to use

- **A live conversation between people who are online together**: use `ChatThread`, which has a message list, typing
  and read receipts.
- **A history of events across many objects**, such as deployments or status changes: use `Timeline`.
- **Only an input with mention suggestions and no discussion**: use `MentionInput`.
- **A single note that nobody replies to**: use `Textarea`. A thread with one comment and no replies is a note with extra
  controls.

## Usage

Set `CurrentUser` to the person who reads the thread. They can edit and delete their own comments, and only theirs.
`Comments` is the tree of comments, oldest first, and each comment's replies sit under its `Replies` list.

```csharp
var ana = new CommentAuthor("ana", "Ana Ruiz");
var mina = new CommentAuthor("mina", "Mina Park");

var thread = new CommentThread
{
    Title = "Hero headline",
    CurrentUser = ana,
    Comments =
    [
        new ThreadComment("c1", mina, "The new headline reads well. @Theo Walsh can we tighten the subhead?", "2h")
        {
            Replies =
            [
                new ThreadComment("c2", new CommentAuthor("theo", "Theo Walsh"), "Yes, trimming it to one line.", "1h")
            ]
        }
    ]
};
```

```xml
<arc:CommentThread Title="Pricing table spacing"
                   Placeholder="Reply to the thread"
                   CurrentUser="{Binding CurrentUser}"
                   Comments="{Binding Comments}"
                   IsResolved="{Binding IsResolved}" />
```

`CommentThread` derives from `TemplatedControl`, and it sizes to its content, so place it in a container that gives it the
width you want. The thread shows every change at once: it builds a new immutable comment tree and displays it, then reports
the change. It does not wait for the application to save anything.

### Anatomy

1. **Header**: the `Title` (a string, or a control in the header only), the subtitle "3 comments" and the Resolve button.
   The header has 18 px of left padding and 12 px of top padding.
2. **Comment**: a 16 px rounded surface with 10 px of padding and 10 px between comments. It holds the portrait (28 px, or
   initials when there is no `Avatar`), the author's name, the time and "edited" for an edited comment.
3. **Body**: 14 px text at a 1.4 line height. A known person written as "@Full Name" is set in medium weight.
4. **Actions**: Reply for every comment; Edit and Delete only on the current user's comments. The actions are 28 px high.
   They are visible while the pointer is over the comment, while a key focus is inside it, while a prompt is open and
   always on touch screens.
5. **Reactions**: one chip per emoji, with its count. The add-reaction button opens a picker of 28 px emoji buttons with 16 px emoji.
6. **Replies toggle**: "Show 2 replies" or "Hide replies". Replies sit under a rail line, indented 24 px (12 px narrow).
7. **Deleted placeholder**: a deleted comment with replies stays as "This comment was deleted", so the thread keeps its
   structure.
8. **Composer**: the current user's portrait (hidden below 420 px), a field that grows from 38 px to 160 px with a 14 px
   radius, and a Send button 38 px high (32 px while editing).
9. **Replying line**: "Replying to Theo Walsh" above the composer, with a 22 px close button.
10. **Mention suggestions**: a list under the field, rows 32 px high, at most five people.
11. **Resolved chip**: when the thread is resolved, the header folds into a 24 px icon, the title, the count of people who
    took part, and a Reopen button 32 px high. The chip has a 22 px radius.

Lines and spacing are set by the component theme. Use the theme resources to change the look of the whole thread, not
per-instance properties.

### Binding to a view model

The view model owns the comments and the resolved state. It loads them, applies each change the thread reports, and
returns the saved tree. `Comments` and `IsResolved` are two-way, so the thread writes the new values back when they change.
This example uses CommunityToolkit.Mvvm.

```csharp
public interface IReviewStore
{
    Task<ThreadComment[]> LoadCommentsAsync(string reviewId);

    Task<bool> IsResolvedAsync(string reviewId);

    Task ApplyChangeAsync(string reviewId, CommentThreadChange change);
}

public partial class DesignReviewViewModel : ObservableObject
{
    private readonly IReviewStore _store;

    public DesignReviewViewModel(IReviewStore store)
    {
        _store = store;
        CurrentUser = new CommentAuthor("ana", "Ana Ruiz", "avares://Acme.Review/Assets/ana.png");
    }

    public CommentAuthor CurrentUser { get; }

    [ObservableProperty]
    private IReadOnlyList<ThreadComment> _comments = [];

    [ObservableProperty]
    private bool _isResolved;

    public async Task LoadAsync(string reviewId)
    {
        Comments = await _store.LoadCommentsAsync(reviewId);
        IsResolved = await _store.IsResolvedAsync(reviewId);
    }

    public Task RecordAsync(string reviewId, CommentThreadChange change) =>
        _store.ApplyChangeAsync(reviewId, change);
}
```

```xml
<arc:CommentThread Title="Pricing table spacing"
                   CurrentUser="{Binding CurrentUser}"
                   Comments="{Binding Comments, Mode=TwoWay}"
                   IsResolved="{Binding IsResolved, Mode=TwoWay}"
                   People="{Binding People}" />
```

- `CommentsChanged` is the event that carries each change. The code-behind forwards it to `RecordAsync`, so the store saves
  the change itself and the view model does not compare trees.
- `People` lists the people who can be mentioned. Leave it null to offer everyone in the thread and the current user.
- To refuse a change, set `Comments` back to the previous tree in the handler. The thread then shows the old tree.

### Mentions and people

Type `@` in the composer to suggest people. `People` sets the list. Without it, the people in the thread and the current user are offered. Suggestions match the start of any word in a name, so "w" finds "Theo Walsh".

<Demo name="EmptyThread" />

### Reactions and depth

`Reactions` sets the emoji the picker offers on each comment. The default is six emoji: thumbs up, red heart, party popper,
eyes, rocket and check mark. `MaxDepth` limits nesting (default 2): a reply deeper than that attaches to the deepest allowed parent.

### Resolve and reopen

`Resolve()` folds the thread into the resolved chip, and it works only when the thread has comments. `Reopen()` opens a
resolved thread again. `IsResolved` is two-way, and `ResolvedChanged` reports each change with the new state.

<Demo name="Resolved" />

```csharp
thread.ResolvedChanged += (_, e) => Status.Text = e.IsResolved ? "Resolved" : "Reopened";

thread.Resolve();
thread.Reopen();
```

### Changes and persistence

`CommentsChanged` reports each change as a `CommentThreadChange`. Save that change on the server rather than comparing the
whole tree. The server must also check that people edit and delete only their own comments, since it receives only what the client reports.

| Kind | Fields that are set | Meaning |
|---|---|---|
| `Reply` | `Comment`, `ParentId` (null for a top-level comment) | A new comment or reply was written |
| `Edit` | `Id`, `Body` | A comment's body was changed |
| `Delete` | `Id` | A comment was deleted |
| `React` | `Id`, `Emoji`, `Added` | A reaction was added (`Added` true) or removed (false) |

```csharp
thread.CommentsChanged += (_, e) =>
{
    if (e.Change.Kind == CommentThreadChangeKind.Edit)
    {
        // Save e.Change.Body for e.Change.Id.
    }
};
```

### Narrow

Under 420 px the composer drops its avatar and replies indent 12 px instead of 24 px.

<Demo name="Narrow" />

### Example: design review on a pricing page

A review of the pricing table in a SaaS marketing page. The first comment has a reaction from the current user and a mention of Theo Walsh, highlighted in the body. The reply answers the question in one line, and the last comment is edited, so the
"edited" mark shows. The header carries the title of the element under review and the subtitle with the count. The thread
stays unresolved, so the Resolve button is still in the header.

![A pricing table design review with a mention, a reply, a reaction and an edited comment](images/DesignReview.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"
             xmlns:generic="using:System.Collections.Generic"
             Width="600">
    <arc:CommentThread Title="Pricing table spacing"
                       Placeholder="Reply to the thread">
        <arc:CommentThread.CurrentUser>
            <arc:CommentAuthor Id="ana" Name="Ana Ruiz" />
        </arc:CommentThread.CurrentUser>
        <arc:CommentThread.Comments>
            <generic:List x:TypeArguments="arc:ThreadComment">
                <arc:ThreadComment Id="c1"
                                   Body="The annual toggle no longer lines up with the plan names on mobile. @Theo Walsh can we fix it before the release?"
                                   CreatedAt="2h">
                    <arc:ThreadComment.Author>
                        <arc:CommentAuthor Id="mina" Name="Mina Park" />
                    </arc:ThreadComment.Author>
                    <arc:ThreadComment.Reactions>
                        <generic:List x:TypeArguments="arc:CommentReaction">
                            <arc:CommentReaction Emoji="👍">
                                <arc:CommentReaction.UserIds>
                                    <generic:List x:TypeArguments="x:String">
                                        <x:String>ana</x:String>
                                        <x:String>theo</x:String>
                                    </generic:List>
                                </arc:CommentReaction.UserIds>
                            </arc:CommentReaction>
                        </generic:List>
                    </arc:ThreadComment.Reactions>
                    <arc:ThreadComment.Replies>
                        <generic:List x:TypeArguments="arc:ThreadComment">
                            <arc:ThreadComment Id="c2"
                                               Body="Yes, I will move the toggle up with the plan names in the next build."
                                               CreatedAt="1h">
                                <arc:ThreadComment.Author>
                                    <arc:CommentAuthor Id="theo" Name="Theo Walsh" />
                                </arc:ThreadComment.Author>
                            </arc:ThreadComment>
                        </generic:List>
                    </arc:ThreadComment.Replies>
                </arc:ThreadComment>
                <arc:ThreadComment Id="c3"
                                   Body="Checked at 375 px. The gap between the price and the unit is 8 px now."
                                   CreatedAt="45m"
                                   IsEdited="True">
                    <arc:ThreadComment.Author>
                        <arc:CommentAuthor Id="ana" Name="Ana Ruiz" />
                    </arc:ThreadComment.Author>
                </arc:ThreadComment>
            </generic:List>
        </arc:CommentThread.Comments>
    </arc:CommentThread>
</UserControl>
```

The example keeps its comments inline, as a static picture must. In the application they come from the view model, and the
thread reports each change through `CommentsChanged`.

### Content guidelines

- **Title**: names the object under review ("Pricing table spacing", "Clause 4.2 termination"), not the discussion.
- **Placeholder**: invites a reply to the thread ("Reply to the thread"). The default "Reply, or @mention someone" fits a
  team that uses mentions every day.
- **Comments**: write the point and the question in full sentences. Mention a person with "@" only when that person must act.
- **Empty state**: the default line "No comments yet. Start the conversation below." is fine for a review tool. Keep it
  short when the product is not a review tool.
- **Other languages**: set `Labels` for the actions, the counts and the announcements. Counts and names are functions, so the
  plural rules belong to them.

| Instead of | Write |
|---|---|
| Comment thread 1 | Pricing table spacing |
| Reply here... | Reply to the thread |
| 1 comments | 1 comment |
| Delete? | Delete this comment? |
| Thread closed | Resolved |

### Best practices

**Do**

- Set `CurrentUser` to the signed-in person before you show the thread. Edit and Delete follow that identity, so a wrong
  user shows the wrong actions.
- Persist each `CommentThreadChange` on the server, and check on the server that people change only their own comments.
- Give every comment a stable `Id`. The thread reuses the view of each comment by its id when the tree changes.
- Resolve the thread when the question is answered, so the resolved chip shows the outcome and folds the discussion away.
- Set `People` to the reviewers of the object, so the suggestions show the right people first.

**Avoid**

- Comparing two trees to find out what changed. The change in `CommentsChanged` already says what happened.
- Writing a draft into `Comments` while the person types. Only sent comments belong in the tree.
- Using the thread for more than one object. Each thread belongs to one object, and a second object needs a second thread.
- Removing a comment that has replies from your store. Mark it deleted instead, so the thread keeps a placeholder and the replies stay in place.
- Leaving `Reactions` at the default for a product that uses its own emoji, such as a team that reviews with check marks only.

### Common questions

**Why is there no Edit or Delete on someone else's comment?** Edit and Delete appear only on comments whose author has the same `Id` as `CurrentUser`. The server must check it again.

**Why does Resolve do nothing on a new thread?** `Resolve()` folds the thread only when it has comments, so an empty thread stays open.

**How do I start a reply from code?** Call `ReplyTo(id)` with the id of the comment. It focuses the composer, and the
replying line shows its author. `CancelReply()` cancels the reply in progress, and `ReplyingToId` returns the id being
answered, or null.

**Can I hold a change until the server accepts it?** The thread shows a change at once. To refuse one, set `Comments` back to the previous tree in the handler, as described above.

## API reference

<!-- api: CommentThread -->

<!-- api: ThreadComment -->

## Accessibility

- The thread is a named region ("Comment thread", `Label` on `Labels.Thread`). Each comment is a named group with the
  author and the time, such as "Mina Park, 2h". A deleted comment reads "Deleted comment".
- The composer is a combobox while mention suggestions are open, and its expanded state is exposed to assistive technology.
- Reaction chips are toggle buttons, and their names include the count and whether you reacted, such as "thumbs up, 2, including you".
- The button that folds replies exposes its expanded state. Icon-only actions such as Edit, Delete and Reply have names.
- The edit field is named "Edit comment", and its Save button is named "Save".

### Keyboard

| Keys | Action |
|---|---|
| `Enter` (in the composer) | Adds a new line |
| `Ctrl` + `Enter` or `Cmd` + `Enter` | Sends a new comment, or saves an edit |
| `@` | Opens the mention suggestions |
| `Up` / `Down` (suggestions) | Moves the active suggestion; wraps at both ends |
| `Enter` or `Tab` (suggestions) | Inserts the active person as "@Full Name " |
| `Escape` | Closes the suggestions first; then cancels an edit, a reply, a delete confirmation or the reaction picker |

The actions of a comment take focus with `Tab` like any other button, so keyboard users reach Reply, Edit and Delete without
a pointer.

## Motion

- Opening replies, adding a comment and folding a resolved thread change the height of the thread on a spring.
- Reaction counts roll to their new value, and chips move when others are added or removed.
- The edit form, the delete confirmation and the reaction picker slide in a short distance.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Replies open or close | The thread height springs to the new size | The content changes in place | Immediate |
| A comment is added | The thread height springs, and the comment arrives | The content changes in place | Immediate |
| Thread resolved or reopened | The thread folds into the chip, or opens, on a spring | The content changes in place | Immediate |
| Reaction count changes | The count rolls to the new value | The count changes in place | Immediate |
| Edit form, delete prompt or picker opens | Slides in 8 px | The content changes in place | Immediate |

Reduced motion replaces the movement with an in-place change and turns transitions off. Off jumps to the final state.

## Related

- `ChatThread` for a live conversation between people who are online together.
- `Timeline` for a feed of activity across many objects.
- `MentionInput` for a single input with mention suggestions and no thread.

| Component | Choose it when |
|---|---|
| `ChatThread` | People talk in turns, with typing and read receipts |
| `Timeline` | The history is a list of events across many objects |
| `MentionInput` | You need one input with mentions, not a discussion |
| `Textarea` | A single note is written, and nobody replies to it |
| `Avatar` | You need the portrait or initials of a person outside the thread |
