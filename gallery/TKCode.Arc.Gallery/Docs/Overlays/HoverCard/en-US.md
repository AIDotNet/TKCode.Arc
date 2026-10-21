---
title: Hover card
summary: Preview a person or link on hover or focus without leaving the page.
---

<Demo name="Mention" />

## When to use

`HoverCard` shows a short, read-only preview of a person, a record or a link when someone points at or focuses a name
in running text. The subject stays in the page, next to the text that names it, so a reader can check who a mention
refers to without losing their place in a thread, a table or a list.

- **A mention in a comment or an incident thread.** An `@maya` in a post-incident review opens her role, team and
  local time, so the on-call engineer knows whom to ask before tagging anyone.
- **An owner or author name in a table.** A deployment list with an owner column can open a small profile on each
  name, instead of sending the reader to a separate user page for a quick check.
- **A link to an internal record.** A link to a pull request, an order or a support ticket can preview its title and
  one line of context on hover, while a click still navigates to the record.
- **Dense rosters of people.** A list of reviewers or assignees where the reader moves from one name to the next.
  Once one card is open, the next one opens almost at once, so scanning a roster stays fast (see Dense list).

## When not to use

- **A one-line label for an icon or a truncated word:** use `Tooltip`. A hover card is for several facts about a
  subject; a label is a phrase, and a tooltip is lighter and does not need a preview layout.
- **Buttons, inputs or any action:** use `Popover`. Focus never enters a hover card, so a button inside it cannot be
  reached with Tab and a field cannot be typed into.
- **The signed-in user's own account menu:** use `UserMenu`, which lists the account actions (profile, sign out) that
  a preview card does not offer.
- **A decision or a warning the reader must see before continuing:** use `Dialog`. A hover card closes when the pointer
  leaves and on any press outside it, so it cannot hold a message the reader has to acknowledge.
- **A full profile, an activity feed or a long bio:** link to the record's page. The card is 296 px wide by default,
  and a long preview covers the content beneath it.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:HoverCard Placement="Bottom">
        <arc:HoverCard.Trigger>
            <arc:Button Variant="Ghost" Content="@maya" />
        </arc:HoverCard.Trigger>
        <arc:HoverCardProfile DisplayName="Maya Chen"
                              Role="Product designer, Payments"
                              Bio="Designs checkout and billing flows."
                              Meta="Lisbon · 14:20 local time">
            <arc:HoverCardProfile.Stats>
                <arc:HoverCardStat Label="Projects" Value="12" />
                <arc:HoverCardStat Label="Reviews" Value="48" />
            </arc:HoverCardProfile.Stats>
        </arc:HoverCardProfile>
    </arc:HoverCard>
</UserControl>
```

`HoverCard` derives from Avalonia's `ContentControl`, so its `Content` is the preview and `ContentTemplate` applies to
it. The `Trigger` is the focusable control that stays in the page. In place, the card shows only the trigger; its
content opens in an anchored `PopoverSurface`, the same surface `Popover` uses, hosted in a popup. The popup is not in
the layout, so the card never pushes the text around it. `HoverCardProfile` derives from `TemplatedControl` and
provides the standard person layout (avatar, name, role, bio, stats and footer) so that every card in an application
looks the same.

### Anatomy

1. **Trigger** (`Trigger`): the focusable control in the page, such as a Ghost `Button` with the handle or a link. It
   is the only part that takes up space in the layout.
2. **Card surface**: a `PopoverSurface` whose width comes from `CardWidth` (296 by default), with 16 px of padding, the
   raised surface color, a 1 px border in the border color and the panel corner radius. It is not focusable, and the
   floating shadow is drawn by the popup.
3. **Avatar**: a large `Avatar` fed by `AvatarSource`. Without a portrait it shows the initials of `DisplayName`.
4. **Name** (`DisplayName`): the base type size in medium weight and the primary text color. It is one line and ends
   with an ellipsis when it is too long.
5. **Role** (`Role`): a small line in the secondary text color, one line with an ellipsis. It is hidden while the
   string is empty.
6. **Bio** (`Bio`): a small paragraph in the secondary text color. It wraps; it is hidden while it is empty.
7. **Stats** (`Stats`): a row under a 1 px rule in the subtle border color, with 12 px above the rule. Each entry shows
   the value in the base size and medium weight, with the label beneath it in the extra-small muted color. Columns are
   24 px apart and rows 8 px apart, and the row wraps. It is hidden while `Stats` is empty.
8. **Meta** (`Meta`): a footer in the extra-small muted color, usually a location and a local time. It is hidden while
   the value is null.

The rows of the profile are separated by 12 px, and the avatar and the text column are separated by 12 px. The card sits
8 px from its trigger (`Offset`).

### Profile card

The profile above is the usual form of the preview: the name and role identify the person, the bio says what they do,
the stats give two numbers that help a reader judge the person, and the meta line gives a location and local time for
people in other time zones. Keep to two or three stats. The card has a fixed width, so each stat takes a column
beside its neighbours and a long list of stats makes the card taller than the reader expects.

### Link preview

`arc:HoverCard.Attach` adds a card to any control, such as a plain text link, without a `Trigger`. The attached card
is anchored to the control it is attached to. While it is open it joins that control's logical tree, so styles and the
`DataContext` of the target reach the card.

<Demo name="LinkPreview" />

Here the card's `Content` is a string. A string is shown as wrapping text in the card's default text template, which
is enough for a title and a one-line summary of a link. Use a `HoverCardProfile` when the subject is a person.

### Timing and width

`OpenDelay` defaults to 500 ms and `CloseDelay` to 140 ms. The open delay stops a card from appearing each time the
pointer crosses a name on the way somewhere else. The close delay is a grace period: it gives the pointer time to
travel the 8 px gap into the card, and entering the card cancels it, so the reader can scroll the bio or select text.
`CardWidth` sets the width of the card, which defaults to 296 (18.5 rem). A card is also limited by the space available
in the window, and it flips to another side when the preferred one does not fit.

```csharp
card.OpenDelay = TimeSpan.FromMilliseconds(300);
card.CardWidth = 320;
```

Widen the card only when the bios are long enough to wrap onto many lines at 296. Shorten `OpenDelay` only on a
trigger where a preview matters more than a quiet page, such as an owner name on a deployment. A page-wide warm state
also applies: while any card is open, and for 300 ms after the last one closed, the next card opens after 80 ms with
a fade only, so a reader who moves across a roster does not wait 500 ms at each name.

### Showing from code

`Show` and `Hide` change `IsOpen` directly. `Show` opens the card at once, without `OpenDelay`, and `Hide` closes it at
once, without `CloseDelay`. `IsOpen` is two-way, so a binding or a code change both open the card. `Opened` and `Closed`
report the state changes. `Closed` also carries a `Reason`, which is `Escape` when the person pressed Escape,
`PressOutside` when they pressed outside the card, and `Programmatic` when the pointer left the trigger or code called
`Hide`. The exit animation can still be playing when `Closed` is raised.

```csharp
card.Show();
card.Closed += (_, e) => log.Text = $"Closed by {e.Reason}";
```

The `Anchor` property is the control the card is positioned against: the attached target when the card is attached,
otherwise the `Trigger`. `Surface` and `Popup` expose the surface and the popup after the first open; they are null
before that.

### Dense list

Once a card is open, moving to the next trigger opens it almost at once with a fade only. A roster of reviewers is the
typical case: each name shows its own card without a half-second wait, and the page does not flash between cards.

<Demo name="DenseList" />

### Binding to a view model

In an MVVM application the view model owns the people, and the hover card renders one profile per item. Only `IsOpen`
is two-way; bind `Trigger`, `Content` and the profile properties one way, from the data.

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

public sealed record MemberProfile(
    string Handle,
    string Name,
    string Role,
    string Bio,
    string Location,
    int Projects,
    int Reviews);

public partial class ReviewersViewModel : ObservableObject
{
    public ObservableCollection<MemberProfile> Reviewers { get; } = new();
}
```

```xml
<ItemsControl ItemsSource="{Binding Reviewers}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <arc:HoverCard>
                <arc:HoverCard.Trigger>
                    <arc:Button Variant="Ghost" Content="{Binding Handle}" />
                </arc:HoverCard.Trigger>
                <arc:HoverCardProfile DisplayName="{Binding Name}"
                                      Role="{Binding Role}"
                                      Bio="{Binding Bio}"
                                      Meta="{Binding Location}" />
            </arc:HoverCard>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

`Stats` is a read-only collection, so XAML cannot bind it. Fill it in code when the profile is created, and format the
numbers with the current culture so that a thousand separator follows the reader's settings:

```csharp
var profile = new HoverCardProfile { DisplayName = member.Name, Role = member.Role };
profile.Stats.Add(new HoverCardStat("Projects", member.Projects.ToString("N0", CultureInfo.CurrentCulture)));
profile.Stats.Add(new HoverCardStat("Reviews", member.Reviews.ToString("N0", CultureInfo.CurrentCulture)));
```

Keep the handle on the trigger and the name in the card: the trigger is what a keyboard user reaches, and it needs
text that names the person.

### Focus and dismissal

A hover card opens from three inputs: the pointer, a Tab focus and a click. The rules differ, and they are chosen so
that a keyboard user and a mouse user both get a predictable card.

- **Tab focus** opens the card at once. Focus that arrives by a pointer click does not open it; the click itself does.
- **Losing focus** starts the close grace. The card closes after `CloseDelay` unless the pointer is over the trigger or
  the card.
- **Escape** closes an open card. The card then stays closed until the pointer or the focus leaves the trigger, so it
  does not reopen while the reader is still on the same name.
- **A press outside** the card closes it with the `PressOutside` reason.
- **Only one card is open** at a time. Opening a card closes the one that was open.
- **Removing the trigger** from the page closes the card at once, without the close animation, so a list that refreshes
  never leaves a card behind.

Focus never enters the card. The card has no focusable content of its own, so the reader cannot Tab into it and
cannot type in it. A card that needs a control must be a `Popover` instead.

### Example: deployment owners in an admin console

A deployment history in a SaaS operations console. Each owner is a ghost button with a handle, so the row reads
quickly and the owner's role and local time appear on hover or focus. The card shows two stats only, because the
reader needs the owner's current load, not a history. The meta line gives the local time, which matters when a
handoff crosses time zones at the end of an on-call shift.

```xml
<StackPanel Width="560" Spacing="12">
    <Grid ColumnDefinitions="*,Auto,Auto" ColumnSpacing="16">
        <TextBlock Text="Payments API 2026.10.14" VerticalAlignment="Center" />
        <TextBlock Grid.Column="1" Text="Succeeded" VerticalAlignment="Center"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        <arc:HoverCard Grid.Column="2" Placement="Left">
            <arc:HoverCard.Trigger>
                <arc:Button Variant="Ghost" Content="@ari" />
            </arc:HoverCard.Trigger>
            <arc:HoverCardProfile DisplayName="Ari Okafor"
                                  Role="Staff engineer, Platform"
                                  Bio="Owns the build pipeline and the release train."
                                  Meta="Lagos · 13:20 local time">
                <arc:HoverCardProfile.Stats>
                    <arc:HoverCardStat Label="Open reviews" Value="3" />
                    <arc:HoverCardStat Label="On call" Value="Today" />
                </arc:HoverCardProfile.Stats>
            </arc:HoverCardProfile>
        </arc:HoverCard>
    </Grid>
    <Grid ColumnDefinitions="*,Auto,Auto" ColumnSpacing="16">
        <TextBlock Text="Billing webhooks 2026.10.19" VerticalAlignment="Center" />
        <TextBlock Grid.Column="1" Text="Running" VerticalAlignment="Center"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        <arc:HoverCard Grid.Column="2" Placement="Left">
            <arc:HoverCard.Trigger>
                <arc:Button Variant="Ghost" Content="@lena" />
            </arc:HoverCard.Trigger>
            <arc:HoverCardProfile DisplayName="Lena Novak"
                                  Role="Research lead"
                                  Bio="Runs the quarterly usability studies."
                                  Meta="Prague · 14:20 local time">
                <arc:HoverCardProfile.Stats>
                    <arc:HoverCardStat Label="Open reviews" Value="1" />
                    <arc:HoverCardStat Label="On call" Value="Tomorrow" />
                </arc:HoverCardProfile.Stats>
            </arc:HoverCardProfile>
        </arc:HoverCard>
    </Grid>
</StackPanel>
```

The `Placement="Left"` keeps each card clear of the status column and the rows below.

### Content guidelines

- **Display name**: the name the organization uses, in full: "Maya Chen". The handle belongs on the trigger, so the
  card does not repeat it.
- **Role**: a function and a team, separated by a comma: "Product designer, Payments". Use sentence case, and leave the
  role out rather than writing a placeholder such as "Employee".
- **Bio**: one sentence about what the person works on, in the present tense. Keep it under a line or two at the card
  width.
- **Meta**: a place and the local time, with a middle dot as the separator: "Lisbon · 14:20 local time".
- **Stat labels**: plural nouns of one or two words ("Projects", "Reviews"). Put the numbers in `Value` as digits, so
  they are easy to compare.

| Instead of | Write |
|---|---|
| Hi! I am a designer who loves things. | Designs checkout and billing flows. |
| Role: Employee | Product designer, Payments |
| Number of projects you have worked on | Projects |
| Time: 14:20 | Lisbon · 14:20 local time |
| Name: @maya | Maya Chen (the handle is the trigger text) |

### Best practices

**Do**

- Use a focusable control as the trigger, with text that names the person, such as a Ghost `Button` with the handle.
  Keyboard users reach the card through the trigger, and the trigger's text is what a screen reader announces first.
- Keep the card read-only. Put actions on the record's page or on the trigger, and use a `Popover` when the preview
  needs a button. Focus never enters a hover card, so a button inside it is unreachable from the keyboard.
- Keep the bio to a sentence and the stats to two or three values. The card has a fixed width, so a short card reads
  at a glance in a long list.
- Keep the default `OpenDelay` on a page with many triggers. A 500 ms wait keeps the cards quiet while the pointer
  crosses names on the way to something else.
- Give a link preview the same text as the link, so the help text announced while the card is open matches what the
  reader already read.

**Avoid**

- Buttons, fields or links inside the card. Focus never enters the card, so these controls cannot be reached from the
  keyboard, and a touch user has no way to press them.
- Setting `OpenDelay` to zero on every trigger in a list. A card then opens on every pointer crossing, and the page
  reads as noise.
- Making the card the only way to reach a person's details. The screen reader hears only the summary in the trigger's
  help text, and the card is not on the page when it is closed.
- Long paragraphs in `Bio` or `Meta`. The card grows with its text and covers the rows beneath it; link to the full
  profile instead.
- A non-focusable `Trigger`, such as a plain `TextBlock`. Keyboard users cannot focus it, so the card never opens for
  them. Use `arc:HoverCard.Attach` on the text, or a focusable control as the trigger.

### Common questions

**Why does a click open the card at once when hovering waits half a second?** A mouse click on the trigger is an
explicit request, so it opens the card without `OpenDelay`. The wait only applies to hover, which can happen by accident
while the pointer crosses the trigger.

**Can the reader move the pointer into the card?** Yes. Leaving the trigger starts the close grace of `CloseDelay`
(140 ms). Entering the card cancels that close, and the card stays open while the pointer is over it. Leaving the card
closes it after the grace.

**How does a touch screen open the card?** A tap on the trigger toggles the card: the first tap opens it at once, and
the next tap on the trigger closes it. Touch pointer enter and leave events do not open or close it.

**What does a screen reader hear while the card is open?** The trigger's help text is set to the summary while the card
is open. For a `HoverCardProfile` that is the name, followed by the role when there is one. For string content it is the
string. Otherwise it is the card's own `AutomationProperties.Name`. The previous help text is restored when the card
closes, so the reader hears the normal trigger description after that.

## API reference

<!-- api: HoverCard -->

<!-- api: HoverCardProfile -->

<!-- api: HoverCardStat -->

## Accessibility

- While the card is open, its summary is written to the trigger's help text. The card itself has no accessible name
  of its own unless one is set, so the trigger's text must name the person.
- Focus never enters the card. Keep its content read-only and put actions on the trigger or in a `Popover`.
- The card surface is exposed to assistive technology as a pane, not as a button or a link.
- Tab focus opens the card at once, Enter skips the open delay, and Escape closes it. It stays closed while the pointer
  rests on the trigger.
- On touch, a tap toggles the card. Only one card is open at a time, so a screen reader user never has two previews
  competing for attention.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` onto the trigger | Opens the card at once, with no open delay |
| `Tab` away from the trigger | Closes the card after `CloseDelay` (140 ms), unless the pointer is over the trigger or the card |
| `Enter` on the trigger | Opens the card at once when it is closed |
| `Escape` | Closes the card; it stays closed until the pointer or the focus leaves the trigger |

## Motion

- The card grows from its trigger edge: it scales from 0.96 to 1 and moves 4px toward the trigger on a smooth spring,
  with a separate fade.
- Profile rows settle in from 4px below one after another. The avatar fades in from a soft blur once its portrait has
  loaded.
- When another card closed less than 300ms ago, the next card opens after 80ms with a fade only.
- Each opening replays the profile rows, so a card that is opened again always settles in from the same starting point.
- Reduced motion removes the scale and the movement and keeps a short fade. Off shows or removes the card at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Card opens | Scales from 0.96 and moves 4px toward the trigger on a smooth spring; the fade runs alongside | Short fade only | Shown at once |
| Card closes | Fades out; reverses if it reopens mid-exit | Short fade | Removed at once |
| Profile rows open | Each row rises 4px and fades in, 35ms after the previous one, on every opening | Rows shown at rest | Rows shown at rest |
| Next card after a recent close | Opens after 80ms with a fade only | Fade only | Shown at once |

## Related

- `Tooltip` for a short text label on hover or focus.
- `Popover` for a card with buttons or inputs, opened on click.
- `Avatar` for the picture shown in a profile.

| Component | Choose it when |
|---|---|
| `Tooltip` | A one-line label is enough for an icon, a shortcut or a truncated word |
| `Popover` | The content has buttons, inputs or a small form, and a click opens it |
| `Avatar` / `AvatarGroup` | You need the portrait or the initials that the card shows, or a group of people |
| `Dialog` | The reader must read or act on something before continuing |
| `UserMenu` | The menu belongs to the signed-in user's own account |
| `DropdownMenu` | A list of commands opens from a trigger |
