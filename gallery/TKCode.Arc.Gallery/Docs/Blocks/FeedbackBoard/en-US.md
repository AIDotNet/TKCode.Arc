---
title: Feedback board
summary: A public board of feature requests: filter by status, sort by top, newest or trending, vote with a rolling count, open a request to discuss it in a comment thread, and suggest new ones.
---

<Demo name="Preview" />

## When to use

`FeedbackBoard` is a public board of feature requests. People see where each idea stands (under review, planned, in
progress, done), vote for the ones they want, open a request to read and discuss it, and suggest new ones. The list sorts
by top votes, newest or trending, and rows glide to their new places when a vote or a sort changes the order.

- **A product feedback page** inside your app or on your site.
- **A public roadmap** filtered to planned and in-progress items.
- **An internal ideas board** for a team.

## When not to use

- **A quick poll on a few options**: use `FeatureVoting`.
- **Bug reports with fields and attachments**: use a form that files issues.
- **Private one-to-one support**: use `SupportConversation`.

## Usage

```xml
<arc:FeedbackBoard x:Name="Board" Title="Product feedback" SortOrder="Top">
    <arc:FeedbackRequest Id="r1" Title="Dark mode for the mobile app" Votes="214" Status="Planned" />
</arc:FeedbackBoard>
```

```csharp
Board.VoteChanged += (_, e) => api.Vote(e.Request.Id, e.Request.HasVoted);
Board.RequestSubmitted += (_, e) => api.Create(e.Request.Title, e.Request.Description);
```

The board changes the `FeedbackRequest` objects in place: votes update `Votes` and `HasVoted`, comments replace
`Comments` (reported with the exact change by `CommentsChanged`), and new requests are added under review with the
author's vote. Move `Status` from your side when a request is planned or shipped. A board with no requests when it first
loads shows a sample.

### A roadmap view

<Demo name="Roadmap" />

`StatusFilter`, `SortOrder` and `SelectedRequest` are two-way. `CanSubmit="False"` hides the "Suggest a feature" form.
In code, `ToggleVote(request)` and `Submit(title, description)` do what the buttons do.

## API reference

<!-- api: FeedbackBoard -->

<!-- api: FeedbackRequest -->

<!-- api: FeedbackRequestEventArgs -->

<!-- api: FeedbackStatus -->

<!-- api: FeedbackSort -->

## Accessibility

- Each row is a toggle button named by its title, status, votes and comment count; the vote button inside it says the count and whether you voted.
- Status is always written as a word on its pill, never shown by color alone. Votes and new requests are announced politely.
- The form's title field shows an error when empty; `Escape` closes the form and `Ctrl+Enter` posts it.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Move through the header, filters, rows and their vote buttons, and the open request. |
| `Enter` / `Space` | Open a request, vote, or press a chip. |
| `Ctrl+Enter` in the form | Post the request. |
| `Escape` in the form | Close it. |

## Motion

- When a vote or a new sort changes the order, rows glide from where they were to their new places on a smooth spring; a second change mid-glide keeps the speed.
- Vote counts roll digit by digit; the form opens and closes with its height.
- Reduced motion places rows at once and keeps fades; with motion off everything jumps.

## Responsive behavior

- At 760 px and wider the open request sits beside the list (380 px, `DetailWidth`). Below that (`:stacked`) it takes the list's place, with a back button.
- The filters and the sort switch wrap onto separate lines in a narrow column.

## Related

- `FeatureVoting` for a compact vote on a short list.
- `CommentThread` for the discussion on its own.
- `HelpCenter` for answers people can read before they ask.
