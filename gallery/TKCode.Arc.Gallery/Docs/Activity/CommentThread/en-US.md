---
title: Comment thread
summary: Threaded comments with replies, reactions, mentions, inline edit, and resolve.
---

## When to use

- A review discussion on a document, a design, a ticket or a pull request.
- Feedback that stays attached to one object, with replies nested under the comment they answer.
- A thread that someone can resolve once the question is settled.

## When not to use

- A live conversation between people who are online together: use `ChatThread`.
- A history of events across many objects: use `Timeline`.
- Only an input with mentions and no thread: use `MentionInput`.

## Usage

Set `CurrentUser` to the person who reads the thread. They can edit and delete their own comments. `Comments` is the
tree of comments, and replies nest under `Replies`.

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

### Mentions and people

Type `@` in the composer to suggest people. `People` lists who can be mentioned. Without it, the people in the thread
are offered.

```csharp
var thread = new CommentThread
{
    Title = "Footer links",
    CurrentUser = ana,
    People = [ana, mina, new CommentAuthor("kai", "Kai Moreno")]
};
```

`Reactions` sets the emoji offered on each comment. `MaxDepth` limits nesting: a deeper reply attaches to the deepest
allowed parent.

### Resolve and reopen

Resolving folds the thread into one chip that keeps the people who took part. `IsResolved` is two-way.

```csharp
thread.ResolvedChanged += (_, e) => Status.Text = e.IsResolved ? "Resolved" : "Reopened";

thread.Resolve();
thread.Reopen();
```

### Changes and persistence

`CommentsChanged` reports each change as a `CommentThreadChange` with a `Kind` of `Reply`, `Edit`, `Delete` or `React`.
Save that change on the server rather than comparing the whole tree. The server must also check that people edit and
delete only their own comments. To reject a change, set `Comments` back to the previous value in the handler.

```csharp
thread.CommentsChanged += (_, e) =>
{
    if (e.Change.Kind == CommentThreadChangeKind.Edit)
    {
        // Save e.Change.Body for e.Change.Id.
    }
};
```

## API reference

<!-- api: CommentThread -->

<!-- api: ThreadComment -->

## Accessibility

- The composer is a combobox: its mention suggestions are a list with the current item. Each comment is a named group with the
  author and time. A deleted comment reads "Deleted comment".
- Reaction chips are toggle buttons. The button that folds replies exposes its expanded state.
- Icon-only actions such as Edit, Delete and Reply have specific names.
- Ctrl or Cmd + Enter sends. Up and Down move through suggestions, Enter or Tab inserts the mention, and Escape closes
  the suggestions first, then cancels an edit or a reply.

## Motion

- Opening replies, adding a comment and folding a resolved thread change the height of the thread on a spring.
- Reaction counts roll to their new value, and chips move when others are added or removed.
- The edit form, the delete confirmation and the reaction picker slide in a short distance.
- Reduced motion replaces the movement with an in-place change and turns transitions off. Off jumps to the final state.

## Related

- `ChatThread` for a live conversation between people who are online together.
- `Timeline` for a feed of activity across many objects.
- `MentionInput` for a single input with mention suggestions and no thread.
