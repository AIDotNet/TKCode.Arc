---
title: Response feedback
summary: A feedback control for AI replies: thumbs down opens an inline panel with reason chips and a note, thumbs up asks what was good, and a submit shows a short thank-you before folding back into the row.
---

<Demo name="Preview" />

## When to use

`ResponseFeedback` is a whole AI reply card: the person's message in a bubble, the model's mark and name, the reply and an
action row with copy, an optional regenerate and the thumbs. `FeedbackActions` is the same control on its own, for message
rows you already render. A thumbs down opens an inline panel with reason chips and an optional note; a thumbs up fills and
asks what was good; a submit shows a short thank-you, then the panel folds back into the row.

- **Thumbs up and down on AI replies** in a chat, a copilot or a search answer.
- **Structured reasons** (inaccurate, unsafe) for model evaluation or support triage.
- **A message action row** with copy and regenerate next to the feedback.

## When not to use

- **Star or numeric ratings** of a product or a session: use a rating control.
- **Emoji reactions** to a post in a thread: use `Reactions`.
- **General product feedback** that is not about one reply: use a feedback form.
- **Long surveys** with many questions: use a form or a dialog.

## Usage

With no data `ResponseFeedback` shows the frame alone: the model's mark, an empty reply and the action row, whose
feedback is simulated until you set a `FeedbackHandler`. Give it your reply, the model's name, the question it answers
and the text to copy, and pass `MessageId` so every submission can be stored against the message:

```xml
<arc:ResponseFeedback ModelName="Claude Sonnet 5.5"
                      Prompt="How do I rotate an API key?"
                      CopyText="Create a new key, deploy it, then revoke the old one."
                      MessageId="msg_0192"
                      AllowsRegenerate="True">
    <TextBlock TextWrapping="Wrap" Text="Create a new key, deploy it, then revoke the old one." />
</arc:ResponseFeedback>
```

`Prompt`, `ModelName` and `CopyText` are null by default: the question, the name and copy stay hidden until you set
them. `ModelMark` takes a 24 × 24 stroked glyph drawn at 16px (a neutral sparkle by default).

### Your own message rows

<Demo name="OwnRow" />

`FeedbackActions` is the row on its own. `Leading` holds actions before the thumbs (use 16px icons in small 36px buttons,
such as an icon-only `CopyButton`) and `Trailing` is pinned to the end of the row, for a time or a token count.

### Anatomy

1. **Thumbs**: two 36px round toggle buttons. The active one fills on top of its outline, so nothing moves.
2. **Panel**: after a thumbs down, "What went wrong?" with a close button, the reason chips, the note and a footer with the
   privacy note beside Cancel and Submit. After a thumbs up, the lighter "What was good?" row with Skip and Send.
3. **Thank-you**: a tick that draws once, "Thanks for your feedback" and a second line; it holds for `ThanksDuration`
   (1.6s), then the panel folds away.
4. **Error line**: an icon and a short message next to the actions; Submit becomes Try again. A rating that could not be
   saved shows the same line in the row with its own Try again.

### Saving feedback

<Demo name="Persisting" />

Set `FeedbackHandler`. It is called with a `Rating` submission on every thumb press and a `Details` one when reasons or a
note are submitted. Throw to show the error and Try again; the token is cancelled when the panel closes or the rating
changes, so pass it on to your request:

```csharp
reply.FeedbackHandler = async (feedback, cancellation) =>
{
    using var response = await http.PostAsJsonAsync("/api/feedback", new
    {
        feedback.MessageId,
        stage = feedback.Stage.ToString(),
        rating = feedback.Rating.ToString(),
        feedback.Reasons,
        feedback.Comment,
    }, cancellation);
    response.EnsureSuccessStatusCode();
};
```

Store the rating on the `Rating` stage (`None` means the person cleared it) and attach the reasons and the note on the
`Details` stage. Without a handler the sends are simulated: the thank-you line and the announcement say nothing was sent.
`FeedbackSubmitted` is raised for every submission either way, and `RatingChanged` for every thumb press.

`Rating` is two-way: set it to show a rating loaded from your database, or bind it to own the state. A rating set from
outside checks the thumb without opening the panel or sending anything.

### Reasons and the note

<Demo name="Reasons" />

`NegativeReasons` and `PositiveReasons` are the chips. An empty `PositiveReasons` skips the follow-up row after a thumbs
up. A chip labelled Other moves focus to the note and changes its placeholder. `AllowComment="False"` drops the note,
`CommentPlaceholder` and `CommentMaxLength` (1000) shape it, and `PrivacyNote` (null hides it) is the quiet line beside
Submit.

### While the reply streams

<Demo name="Streaming" />

Set `IsRatingEnabled="False"` while the reply is still streaming, so people rate the finished answer.

### Your own words

`Labels` holds every word the control shows or announces. Replace the object to localize or reword:

```csharp
reply.Labels = new ResponseFeedbackLabels
{
    GoodResponse = "Bonne réponse",
    BadResponse = "Mauvaise réponse",
    NegativeTitle = "Qu'est-ce qui n'allait pas ?",
    Submit = "Envoyer",
};
```

### Narrow containers

The card pads 16px below 480px of width and 24px above, and the chips wrap, so it holds at 360px without scrolling. Below
400px the privacy note and Cancel give way (Close covers Cancel), so the error line and Try again always fit on one row.
Chips grow from 28 to 32px tall after a touch.

### Content guidelines

- Keep reasons to one or two words ("Too long", "Unsafe") so the chips stay on one or two lines at 360px.
- Say what is shared in the privacy note, in a few words.
- The thank-you line thanks the person and says what happens next; it does not ask for more.

### Best practices

- Use `ResponseFeedback` for a single assistant reply with its actions, and `FeedbackActions` when you render your own
  messages.
- Always pass `MessageId` and persist both stages.
- Throw from the handler to show the error; the control handles Try again and aborts stale calls through the token.

### Common questions

**Can people change their mind?** Yes. Pressing the other thumb switches the rating and the panel; pressing the active
thumb clears it. Every press is sent.

**What does Escape do?** It closes the open panel and returns focus to the thumb that opened it. The rating stays.

## API reference

<!-- api: ResponseFeedback -->

<!-- api: FeedbackActions -->

<!-- api: FeedbackSubmission -->

<!-- api: ResponseFeedbackLabels -->

<!-- api: FeedbackSubmittedEventArgs -->

<!-- api: FeedbackRatingChangedEventArgs -->

## Accessibility

- The thumbs are toggle buttons that report pressed, named "Good response" and "Bad response", with the same tooltips.
- Reason chips are toggle buttons inside a group named by the panel's title. Selected chips invert to a solid fill, so the
  state never relies on hue.
- A keyboard press on thumbs down moves focus to the first reason; a pointer press leaves focus where it is. After a
  keyboard submit, focus lands on the thank-you line and returns to the thumb when the panel folds.
- A polite live region announces rating changes, sending, the thank-you state and errors, and says when nothing was sent
  because the send is simulated.
- Errors show an icon and text next to the actions, never color alone.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves through copy, regenerate, thumbs up and thumbs down, then into the open panel |
| Enter, Space | Presses a thumb, toggles a reason chip, or activates Submit, Send, Skip and Cancel. Pressing the active thumb clears the rating |
| Escape | Closes the open panel and returns focus to the thumb that opened it. The rating stays |
| Cmd + Enter, Ctrl + Enter | Submits from the note field |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Panel opens or closes | The measured height springs (0.24s, no bounce) while the panel fades over 160ms | Height cuts at once, the panel fades over 160ms | At once |
| Form to thank-you | One crossfade: out 100ms, in 160ms rising 2px, while the height springs to the new content | 120ms crossfade, height cuts | At once |
| Thumb fills | The filled glyph fades in over 160ms on top of the outline; nothing moves or resizes | Same fade | At once |
| Press | Scales to 0.94 at once and springs back on release | No scale | No scale |
| Success tick | Draws once in 280ms | Shown at once | Shown at once |

The thumbs never change size and the row stretches to its container, so the row's width does not move as a thumb fills or
the panel opens. Every end state and announcement is the same at every motion level.

## Related

- `ReasoningBlock` for the model's thinking above the reply.
- `ToolCall` for the tools an agent called.
- `CopyButton` for the copy action in your own rows.
