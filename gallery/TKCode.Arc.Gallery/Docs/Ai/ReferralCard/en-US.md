---
title: Referral card
summary: A give-and-get referral card where people copy or share their invite link, invite friends by email as chips, and watch invites move from invited to signed up to earned, with the credit total counting up and a milestone reward unlocking.
---

<Demo name="Preview" />

## When to use

`ReferralCard` is a whole referral program in one card. The ask sits on top ("Give $20, get $20"), the invite link waits
in a quiet field with Copy and three share buttons, and an email field turns each typed address into a chip. Below, three
rolling numbers count who was invited, who signed up and what was earned, a segmented bar shows how close the next reward
is, and the people invited move from Invited to Signed up to Earned in place.

- **A referral or invite program** in account settings, billing or an onboarding checklist.
- **Showing what invites have earned** and how close the person is to a bonus reward.

## When not to use

- **Team member invites with roles and permissions**: use a members table.
- **Public leaderboards or affiliate payouts** with many columns: use a table.
- **A one-off share link** with nothing to track: a field with a `CopyButton` is enough.

## Usage

Pass the referrals from your backend in `Referrals`, newest first, and replace the list when sign-ups and payments come
in: the counts, the milestone and the list are all derived from it. Sending invites calls `InviteHandler`; return when the
server has accepted them, or throw with the message the person should read.

```csharp
card.Link = $"https://app.example.com/r/{me.Code}";
card.Reward = 20;
card.Referrals = referrals;            // IReadOnlyList<Referral>, newest first
card.Milestone = new ReferralMilestone
{
    Goal = 5,
    Reward = "3 months free",
    Detail = "Applied to your next invoice",
};
card.InviteHandler = async (emails, token) =>
{
    var response = await http.PostAsJsonAsync("/api/referrals/invite", emails, token);
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException("Could not send the invites. Nothing was sent.");
    }
};
card.MilestoneReached += (_, e) => analytics.Track("referral_milestone_unlocked");
```

With no `Referrals` and no `Simulation` the card is empty: the counts read zero, the list says "No invites yet. Friends
you invite show up here.", and with no `Reward` the heading reads "Invite friends"; `Link` and `Milestone` are empty too.
The package ships no sample program. To show a program moving (a preview, onboarding), give the card a
`ReferralCardSimulation`: the list it starts from and the beats that move friends through the stages. While `Referrals`
is null the card shows the simulation's list and plays its beats once when it is 40% on screen; the preview above plays
the gallery's invented program this way (a friend who signed up subscribes, an invited friend signs up and subscribes,
and the fifth paid referral unlocks the milestone).

```csharp
card.Simulation = new ReferralCardSimulation
{
    Referrals = program,                                   // the list it starts from, newest first
    Beats =
    [
        new(TimeSpan.FromSeconds(1.4), "p4", ReferralStage.Earned),
        new(TimeSpan.FromSeconds(2.2), "p5", ReferralStage.SignedUp, "Ana Souza")
    ]
};
```

Setting `Referrals` hands the card to your data and stops the program; give each card its own simulation. `Simulate`
turns the playback off, or on for a card with your own list (then the first address of each simulated send moves through
the stages).

### Anatomy

1. **The ask**: a gift mark, the heading built from `Reward` and `Currency`, and a line that says what both sides get.
2. **Invite link**: the domain muted and the code in full color, Copy at the end, and three share buttons: a post, a
   network and email.
3. **Invite by email**: chips in a fixed-height field that scrolls sideways, Send, and a hint underneath.
4. **Funnel**: Invited, Signed up and Earned as rolling numbers in tabular digits.
5. **Milestone**: one segment per paid referral needed (up to 12) and a line with the reward it unlocks.
6. **Your invites**: each person with an avatar, the address, the date and the stage as a glyph plus a word, in a list of
   fixed height.

### Your data

<Demo name="YourData" />

When your service reports a sign-up or a payment, replace `Referrals` with the new list; rows keep their place by `Id` and
only what changed moves. After a successful send the addresses join the top of the list as Invited until your next list
arrives.

### Sharing

Each share button raises `ShareRequested` with the channel and a ready URL. Open it and set `Handled`; when no handler
does, the share is simulated and the live region says nothing was posted. The email button builds a `mailto:` link with
`ShareMessage` and the link in its body. The post and network buttons use generic glyphs, not network logos: give them
your network's share intent in `PostShareUrlFormat` and `NetworkShareUrlFormat`, where `{url}` and `{text}` are filled in.

```csharp
card.ShareMessage = "I use our invoicing app. Join with my link and we both get $20.";
card.PostShareUrlFormat = "https://social.example/share?url={url}&text={text}";
card.ShareRequested += (_, e) =>
{
    Launcher.LaunchUriAsync(new Uri(e.Url));
    e.Handled = true;
};

// The same URLs elsewhere:
var mail = ReferralCard.BuildShareUrl(ReferralShareChannel.Email, link, message);
```

### Inviting by email

Typing an address and pressing Enter, a comma, a space or Tab turns it into a chip; pasting a list adds every address in
it. An address that is not valid becomes a chip marked with an alert, and Send refuses until it is fixed or removed. A
duplicate, an address already in the list and anything past `MaxInvites` (10) are refused with a note. While the invites go
out the field turns read only and Send shows a spinner; a failure keeps the chips and shows the reason.

### Narrow cards

<Demo name="Narrow" />

The card is fluid up to 560px. Below a 470px card width the share buttons take their own full row and the milestone
line keeps room for two lines. Long addresses truncate while the date and the stage stay visible.

### Your own words

`Labels` holds every word the card shows or says; counts, amounts and dates are functions. `Title` and `Description`
replace the heading and its line outright.

```csharp
card.Labels = new ReferralCardLabels
{
    Title = reward => $"Offrez {reward}, recevez {reward}",
    Send = "Envoyer",
    StageEarned = reward => $"{reward} gagnés",
};
```

### Content guidelines

- Keep `Reward`, `Currency` and the copy consistent with your real program terms; the heading is built from the reward.
- Name the milestone reward in a few words ("3 months free") and put the fine print in `Detail`.
- The previews' people, amounts and milestone are invented; pass your own program before you ship.

### Common questions

**Does the card send email itself?** No. `InviteHandler` sends; without one the send is simulated and the hint says so.

**Why does the list keep its height?** New invites open at the top inside a list of fixed height, so the card never grows
and nothing below it moves.

## API reference

<!-- api: ReferralCard -->

<!-- api: Referral -->

<!-- api: ReferralMilestone -->

<!-- api: ReferralCardSimulation -->

<!-- api: ReferralCardSimulationBeat -->

<!-- api: ReferralCardLabels -->

<!-- api: ReferralShareEventArgs -->

<!-- api: ReferralInvitesEventArgs -->

<!-- api: ReferralLinkEventArgs -->

<!-- api: ReferralMilestoneEventArgs -->

## Accessibility

- Share buttons are icon buttons with names and tooltips (Share in a post, Share with your network, Share by email); a
  polite live region reports what happened, including "Simulated, nothing was posted."
- The hint under the email field describes the field and is announced politely. An error shows an alert icon plus words
  and turns the field's edge to the danger color, never color alone; a chip that is not valid says so in its name.
- Stages are a glyph plus a word. The milestone bar reads as a progress bar with its value as help text, such as
  "4 of 5 paid referrals".
- Each funnel number is named with its label; the list of people is named "Your invites".
- While sending, the field turns read only instead of disabled, so keyboard focus stays put.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves through Copy, the three share buttons, the email field and Send |
| Enter, comma, space or Tab | In the email field, turns the typed address into a chip |
| Enter | In an empty email field, sends the chips |
| Backspace | In an empty email field, removes the last chip; each chip also has its own remove button |
| Ctrl/Cmd + V | Pasting addresses separated by commas, spaces or new lines adds them all |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Funnel numbers | Roll to the new value in 600ms on the standard curve, in tabular digits | Change at once | Change at once |
| Stage words, the milestone line, share icons and the hint | Old out in 100ms, new in over 160ms rising 2px | Short crossfade, no rise | At once |
| Milestone segments | Fill from the left in 240ms; the fill turns to the success color when unlocked | The same | At once |
| A new invite | Opens at the top on a bounce-free 0.24s height spring inside the fixed-height list while it fades in over 160ms | At once | At once |
| Simulated program | Plays once when 40% on screen | Plays twice as fast | Plays twice as fast |

Every end state is the same at every level; reduced motion only drops the roll, the rise and the height travel.

## Related

- `CopyButton` for copying a single value with confirmation.
- `TagInput` for free-form tags without addresses or sending.
- `Avatar` for the people in the list.
- `AgentStatus` for another compact live status card.
