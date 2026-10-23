---
title: Expandable card
summary: Give a dense card more room when requested.
---

<Demo name="Default" />

## When to use

`ExpandableCard` is a summary card whose header opens a details panel below it. The card keeps the title and a
one-line summary visible, so a list of cards stays scannable, and the details appear in place only for the people
who want them. The card can also grow sideways, which gives wide details, such as a log line or a status timeline,
room without leaving the list.

- **Deployment history in a developer console**: each release is a card with the build, the time and the outcome in
  the header. A failed deploy opens to show the failing health check; a successful one stays closed.
- **Order and shipment status in a customer account**: the header reads "Order #4821" with "Shipped, arrives
  Thursday" underneath. The details list the items, the carrier and the tracking number, and the summary rolls
  forward as the status changes.
- **Plan and subscription summaries in billing**: a plan card shows the price and the billing cycle, and opens to
  list the included seats, the limits and the invoice history.
- **Incident and alert queues**: a row of alerts where the header names the service and the time, and the details
  hold the error message and the last few log lines.

## When not to use

- **Several mutually exclusive sections of text**, such as the questions of an FAQ: use `Accordion`, which keeps
  one answer open at a time and lays the rows out as a single list.
- **A card with nothing to reveal**: use `Card`. A header that does nothing is a control with no purpose.
- **Browsing several cards in the same place**, such as a deck of product or project cards: use `CardStack`, which
  moves between cards rather than opening each one in place.
- **Editing a record**: use `Drawer` or `Dialog`. A form inside a card competes with the card's header and the
  layout around it, and it has no room for validation messages.
- **A long paragraph** that needs only a "Read more" toggle: use `ExpandableText`, which clamps the text to a set
  number of lines.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ExpandableCard Header="Pro plan"
                        Description="$24 per month · billed yearly"
                        CollapsedWidth="420">
        <StackPanel Spacing="8">
            <TextBlock Text="Unlimited projects and collaborators." />
            <TextBlock Text="Version history for a full year." />
            <TextBlock Text="Priority support with a four-hour response time." />
        </StackPanel>
    </arc:ExpandableCard>
</UserControl>
```

`ExpandableCard` derives from Avalonia's `HeaderedContentControl`, so `Header` and `Content` work as they do on any
headered control. The card adds the summary line (`Description`), the trigger that covers the whole header, the
chevron, the details panel and the morph between the closed and open sizes. `Header` is the card title; `Description`
is the summary line under it; `Content` holds the details.

Without a width, the card fills its container.

### Anatomy

1. **Card surface**: the surface color with a 1 px border in the border color, the surface corner radius and the
   resting shadow. The surface clips its contents, so the header's hover fill follows the card's corners.
2. **Header trigger**: the whole header is one button, at least 64 px tall, with 20 px of padding on each side and
   16 px above and below. Hover and press change its background to the muted surface color.
3. **Title** (`Header`): the small type size at medium weight in the foreground color. A string header also becomes
   the card's accessible name.
4. **Summary line** (`Description`): the extra-small type size in the muted text color, 4 px below the title. When
   `Description` is null or empty, the line is hidden and the header shrinks to the title.
5. **Chevron**: an 18 px down chevron 20 px after the copy, in the muted text color while closed. It takes the
   foreground color while the card is open or the pointer is over the header, and it turns 180 degrees when the card
   opens.
6. **Details panel** (`Content`): the muted surface color with a 1 px divider above it. Its padding is 20 px on the
   sides, 16 px on top and 20 px below. The text uses the secondary text color at the small type size with a 1.4 line
   height. A closed panel is exactly zero pixels high, because the divider is an inset line rather than a border on
   the panel itself.
7. **Focus ring**: a 2 px ring in the focus color around the header, shown for keyboard focus only.

### Widths: CollapsedWidth and ExpandedWidth

`CollapsedWidth` caps the closed card's width in pixels, and the card is centered in its container. Leave it unset
and the closed card fills the container. `ExpandedWidth` caps the width while the card is open, so the card can grow
sideways. When `ExpandedWidth` is not set, the open card uses `CollapsedWidth`. When `CollapsedWidth` is not set and
`ExpandedWidth` is, the closed card fills the container and the open card is capped at `ExpandedWidth`.

<Demo name="GrowingSideways" />

The width and the height change together on the same spring, so a card that grows sideways should hold details that
read well at the wider size. The details are laid out at their open width the whole time, so their text never
reflows while the card moves.

### Open by default

`IsExpanded` is two-way. Set it to `True` to start open, or bind it to a view model property. A card that starts
open is the right choice when the reason for the card is the details, such as a failed deploy that needs attention
before anything else in the list.

<Demo name="OpenByDefault" />

### Updating the summary

Change `Description` in code or through a binding. Only the words that changed roll in; the rest of the line stays
still, so a status change reads as a change and not as a redraw.

```csharp
order.Description = "Out for delivery · arrives today";
```

Keep the summary in one shape, such as "status · time" or "product · amount". A summary that changes its shape from
one update to the next makes every word roll, which is harder to read than a change in one part of the line.

### Events

`ExpandedChanged` is a routed event that bubbles. It is raised after the card opens or closes, so a handler sees the
new `IsExpanded` value.

```csharp
card.ExpandedChanged += (_, _) =>
{
    logger.Info($"Plan details open: {card.IsExpanded}");
};
```

### Binding to a view model

A deployment card usually belongs to a view model that owns the summary and whether the card is open. With
CommunityToolkit.Mvvm, the view model notifies the card through its properties, and the card reports the reader's
clicks back through the two-way `IsExpanded` binding.

```csharp
public partial class DeploymentViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isDetailsOpen;

    [ObservableProperty]
    private string _summary = "api-gateway · 2 minutes ago";

    [RelayCommand]
    private void Retry() => Summary = "api-gateway · retrying now";
}
```

```xml
<arc:ExpandableCard Header="Deploy failed"
                    Description="{Binding Summary}"
                    IsExpanded="{Binding IsDetailsOpen, Mode=TwoWay}">
    <StackPanel Spacing="12">
        <TextBlock Text="Health check on /ready timed out after 30s." TextWrapping="Wrap" />
        <arc:Button Content="Retry deploy"
                    Variant="Secondary"
                    Size="Small"
                    Command="{Binding RetryCommand}"
                    HorizontalAlignment="Left" />
    </StackPanel>
</arc:ExpandableCard>
```

Bind `Description` one way, because the card only displays it. Bind `IsExpanded` two-way so that a close made with the
keyboard, with Escape, or by the reader's click updates the view model. Do not write `IsDetailsOpen` from the
view model in response to `Description` changes, because the card's open state is the reader's choice.

### Layout in lists and stacks

Inside a vertical list or a `StackPanel`, the card takes the width the panel gives it, so a column of cards lines up
without any width on the cards themselves. Inside a horizontal stack, the available width is unbounded, so the caps
decide the width: set `CollapsedWidth` and `ExpandedWidth` on those cards, or the card sizes itself to its content.
A container resize applies at once without an animation, so a window resize never starts a motion.

### Example: deployment history

A deployment history in a developer console. The list shows the latest three releases of three services. The first
card is open because its deploy failed and the health check output is the thing to read first. Each header names the
service and the time, so the list reads in the order the team works through it. The width is 560, which fits the
details text on one or two lines.

![A deployment history with an open failed deploy and two closed successful deploys](images/DeployHistory.png)

```xml
<StackPanel Width="560" Spacing="12">
    <arc:ExpandableCard Header="Deploy failed"
                        Description="api-gateway · 2 minutes ago"
                        IsExpanded="True">
        <StackPanel Spacing="12">
            <TextBlock Text="Health check on /ready timed out after 30s. Build 4821 is not serving traffic; build 4818 still is."
                       FontSize="14"
                       TextWrapping="Wrap"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            <arc:Button Content="Retry deploy"
                        Variant="Secondary"
                        Size="Small"
                        HorizontalAlignment="Left" />
        </StackPanel>
    </arc:ExpandableCard>
    <arc:ExpandableCard Header="Deploy succeeded"
                        Description="web-app · 14 minutes ago">
        <TextBlock Text="Build 4820 deployed to 6 regions in 4 minutes."
                   FontSize="14"
                   TextWrapping="Wrap"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    </arc:ExpandableCard>
    <arc:ExpandableCard Header="Deploy succeeded"
                        Description="billing-worker · 1 hour ago">
        <TextBlock Text="Build 4812 deployed to 2 regions in 3 minutes."
                   FontSize="14"
                   TextWrapping="Wrap"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    </arc:ExpandableCard>
</StackPanel>
```

### Content guidelines

- **Header**: the noun or the outcome in sentence case, such as "Pro plan", "Order #4821" or "Deploy failed". Keep it
  to one line; the summary carries the rest.
- **Summary**: one line of facts in a fixed shape, separated by a middle dot: "api-gateway · 2 minutes ago", "$24
  per month · billed yearly". Put the time or the status first when the list is sorted by it.
- **Details**: the detail the reader needs to act on, in complete sentences. A details panel should hold the
  reason for the status and the next step, not a repeat of the header.
- **Action**: a verb and an object on the button inside the details, such as "Retry deploy" or "Download invoice".

| Instead of | Write |
|---|---|
| Click to see more | Plan details |
| Status: failed!!! | Deploy failed |
| More | Retry deploy |
| Details | Health check on /ready timed out after 30s |
| Order info | Order #4821 · shipped, arrives Thursday |

### Best practices

**Do**

- Keep the header and the summary short enough that a list of cards scans at a glance. The header is what the reader
  reads when nothing is open.
- Open a card by default only when its details need attention before anything else in the list, such as a failure.
- Keep the details to one or two short paragraphs and a single action. A long details panel pushes the rest of the
  list down the page, which is what the card was meant to avoid.
- Set `Description` for every card in a list, or for none. A mix of cards with and without a summary line makes the
  headers jump in height.

**Avoid**

- Putting a form inside the details. Validation messages and tab order inside a collapsed panel are hard to reach;
  a `Drawer` or `Dialog` is the right container for a form.
- Changing the summary shape on every update. Words roll only when they change, so a new shape rolls the whole
  line and reads as noise.
- Setting `CollapsedWidth` on one card in a list and leaving it unset on the others. The cards then differ in width
  and the column looks ragged.
- Using the card as the only route to a detail that everyone needs. A reader who never opens it misses it, so keep
  required information on the page.
- Relying on the animation to explain a change. The card is correct at rest, and reduced motion shows the same end
  state at once.

### Common questions

**How do I hide the summary line?** Set `Description` to `null` or an empty string. The summary row collapses, and
the header becomes only the title.

**Why does Escape not close the card?** Escape closes the card only when it is open and no control inside it has
already handled the key. A text box or a dialog inside the details that uses Escape keeps the key for itself.

**Can I open the card in code before the page is shown?** Yes. Setting `IsExpanded` before the card is loaded shows
the end state at once, so the first frame is correct. After the card is loaded, changes to `IsExpanded` animate.

**Why does the card not animate when the window is resized?** Container resizes apply at once. The morph is for the
changes the reader makes (opening, closing and a summary update), not for layout changes, so a window resize never
starts a motion.

**How do I know the state from outside the card?** Read `IsExpanded`, or handle `ExpandedChanged`. The handler runs
after the change, so `IsExpanded` already holds the new value.

## API reference

<!-- api: ExpandableCard -->

## Accessibility

- The header is a button that exposes the expand and collapse pattern, so assistive technology announces whether
  the card is open or closed, and the state changes when the card does.
- The card's accessible name is its `Header` when the header is a string. The `Description` is exposed as the help
  text of the header button, so it is read after the name.
- The summary line is also exposed as a text element with its full content, so screen readers read the whole line
  and not only the words that rolled in.
- The details are inert while the card is closed: their controls cannot take focus or receive pointer input.
- Escape closes an open card from anywhere inside it and returns focus to the header, so focus never stays in a
  panel that has closed.

### Keyboard

The header is a button, so the standard button keys apply. The card adds the Escape close.

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Moves to the header; moves into the details only while the card is open |
| `Enter` / `Space` | Opens or closes the card from the header |
| `Escape` | Closes an open card when focus is inside it and no control has handled the key, then moves focus to the header |

## Motion

- Opening is one motion: the width and height grow together on a smooth spring that does not overshoot, and the
  chevron turns with them. The card changes its real size instead of scaling, so text and corners never stretch.
- The details fade in once the card has made room for them. On close, the details fade out first and the card
  shrinks after them.
- A second click during the motion reverses from the current size and speed. A close that follows another change
  within about 450 ms skips the hold before the card shrinks.
- In the summary line, changed words rise in from below with a soft blur. The words they replace lift away and fade.
  Words that did not change stay still.
- Reduced motion applies the end state at once, and the summary text is replaced without animation.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Card opens | Width and height grow together on a smooth spring; chevron turns | Open at once | Immediate |
| Details appear | Fade in once the card has made room, after a 120 ms delay | Shown at once | Immediate |
| Card closes | Details fade out in 100 ms; the card shrinks after a 60 ms hold | Closed at once | Immediate |
| Summary changes | Changed words rise in and the old words lift away and fade | Replaced at once | Immediate |
| Header hover and press | Background crossfades to the muted surface | Immediate | Immediate |

## Related

- `Accordion` for a list of mutually exclusive sections.
- `Card` for a card without expandable details.
- `ExpandableText` for a long paragraph that expands in place.

| Component | Choose it when |
|---|---|
| `Accordion` | Several sections of text open one at a time as a single list |
| `Card` | The card has no details to reveal, so its content stays visible |
| `CardStack` | The reader moves between cards rather than opening each one |
| `ExpandableText` | One long paragraph should show a few lines with a "Read more" toggle |
| `MetricCard` | A single number with its trend is the whole point of the card |
