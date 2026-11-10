---
title: Feature voting
summary: A compact card for voting on a short list of proposed features: picking one rolls its count and glides the share bars, and sending the vote turns the card into a thank-you with the results.
---

<Demo name="Preview" />

## When to use

`FeatureVoting` is a small card that asks one question, such as "What should we build next?", and lists three to six
proposed features. A visitor picks one (or up to `MaxVotes`), sees their vote added to the count right away, and sends it
with the Vote button. The card then thanks them, keeps the results on screen and offers "Change my vote". It suits the
end of a changelog, an in-app announcement, a settings page or a sidebar, where a full feedback board would be too much.

- **A quick poll** on what to build next, shown to signed-in users.
- **A prioritising round** where each person may pick two or three items (`MaxVotes`).
- **A blind vote** that hides the counts until the person has voted (`ShowsResultsBeforeVoting="False"`).

## When not to use

- **Open-ended ideas or long lists**: use `FeedbackBoard`, which has statuses, comments and a form for new requests.
- **Ratings or satisfaction scores**: a slider or a rating control asks a different question.
- **Anonymous public polls with fraud checks**: the card only collects the choice; counting is up to your server.

## Usage

```xml
<arc:FeatureVoting Title="What should we build next?" x:Name="Poll" MaxVotes="1">
    <arc:FeatureVotingOption Id="offline" Title="Offline mode" Description="Work without a connection." Votes="412" />
    <arc:FeatureVotingOption Id="calendar" Title="Calendar view" Votes="296" />
</arc:FeatureVoting>
```

```csharp
Poll.Voted += (_, e) =>
{
    var ids = e.Options.Select(o => o.Id);   // send the choice to your server
};
```

With no options the card shows a sample list. `Votes` counts other people's votes; the card adds the viewer's own vote on
top while an option is picked, and `TotalVotes` and `SelectedOptions` follow. Change `Votes` in place to show live counts.

### Several picks and hidden results

<Demo name="PickTwo" />

With `MaxVotes` above 1 the hint under the description counts the picks, and a pick beyond the limit is refused with a
spoken hint. `ShowsResultsBeforeVoting="False"` hides the counts and bars until the vote is sent.

### Sending from code

`Toggle(option)` picks or unpicks an option, `Submit()` sends the vote (it does nothing without a pick), and `ChangeVote()`
reopens the choice. `IsSubmitted` is two-way, so a page can restore the thank-you for someone who already voted.

## API reference

<!-- api: FeatureVoting -->

<!-- api: FeatureVotingOption -->

<!-- api: FeatureVotingVotedEventArgs -->

## Accessibility

- The options are a group named by the title. Each option is a toggle button whose name reads the title, the count, the share and "your vote" when picked.
- Picks, refused picks and the thank-you are announced politely.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Move to the options, then to the button. |
| `↑` / `↓`, `Home` / `End` | Move between options. |
| `Enter` / `Space` | Pick or unpick the focused option. |
| `Ctrl+Enter` | Send the vote. |

## Motion

- A pick rolls its count digit by digit and every share bar glides to its new length on a smooth spring; a new count from the server mid-glide keeps the speed.
- The check mark pops into the picked option; the header and the button swap with a short lift, and the thank-you check draws itself.
- Reduced motion places bars and digits at once and keeps short fades; with motion off everything jumps.

## Responsive behavior

- The card is at most 420 px wide (`CardMaxWidth`) and stretches below that. Titles and descriptions wrap; the count stays on the right.
- It has no breakpoints of its own; in a narrow column keep descriptions to one line.

## Related

- `FeedbackBoard` for a full board of requests with statuses and comments.
- `Reactions` for a lightweight reaction row under content.
