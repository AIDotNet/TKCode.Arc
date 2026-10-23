---
title: Badge
summary: A small label for status, category, or metadata.
---

<Demo name="Tones" />

## When to use

`Badge` is a small pill that names a state, a category or a piece of metadata. It sits beside a title or inside a
table cell, where it can be read in a glance and compared with the badges in the rows around it. It is inline text
with a tone: it does not take focus, open anything or announce itself, and its label must say the state on its own.

- **Order and invoice status in a billing table**: a `Small` badge reads "Paid", "Pending" or "Failed" in each row, so
  an operator can scan a page of invoices without opening one.
- **Deployment and environment labels** on a release page: "Live" in the success tone beside the production version,
  "Beta" in the accent tone beside a preview build, and "Draft" for work not yet published.
- **Metadata on a card or a detail header**: a plan name, a region or a "New" marker next to a customer name, where
  the label is a fact about the item and not an action.
- **A counter that changes while the page is open**: an unread count or a number of open tickets beside a menu item.
  The badge stays mounted, so the new number rises in place and the pill widens to fit.
- **A row marker with a leading icon** in an activity or incident timeline: the icon names the kind of event, and the
  tone says whether it is good, pending or failed.

## When not to use

- **A full sentence of feedback**, such as "Your card was declined": use `Alert` for a message that stays on the page,
  or `Toast` for one that goes away on its own.
- **A value the user switches on and off**, such as a filter or a tag they choose: use `ChipGroup`, which handles
  selection and keyboard focus.
- **A large number with context around it**, such as revenue this quarter: use `MetricCard`, which gives the number
  room and a caption.
- **A person's presence**, such as online or offline: use the status dot on `Avatar`. A badge names a state; it does
  not sit on a person's photo.
- **A count or state that the user must act on from the badge**: use a `Button` or a `Tabs` item, because a badge
  takes no focus and has no click behavior.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Badge Content="Draft" />
</UserControl>
```

`Badge` derives from Avalonia's `ContentControl`, so `Content` can be any object. Strings and numbers get the in-place
animation described below; other content is shown as it is, without the swap. The pill's fill, border and text come
from the tone and the theme, so set `Tone` and `Size` rather than the brushes.

### Anatomy

1. **Pill** (the control itself): a 1 px border with the pill radius. The minimum height is 26 px at `Medium` and
   22 px at `Small`; the pill grows with its label.
2. **Tone ring** (`Info` only): an accent ring laid over the border at 24% opacity.
3. **Hover wash** (`Info` only): a 10% accent wash over the surface, with the ring at 32%, while the pointer is over
   the badge.
4. **Icon** (`Icon`, leading): an optional glyph in the tone color. It is hidden from assistive technology.
5. **Label** (`Content`): the text in medium weight. Keep it to one or two words, because the pill widens with it and
   a long label pushes its neighbours in a table.

Padding is 10 px on each side at `Medium` and 8 px at `Small`. The gap between the icon and the label is 5 px at
`Medium` and 4 px at `Small`.

### Tones

`Tone` sets the color treatment. `Neutral` is the default. The label must name the state by itself, because the
tone is not the only signal, and printed or grayscale reports lose color.

| Tone | Text | Fill | Border | Use it for |
|---|---|---|---|---|
| `Neutral` | Secondary text color | Muted surface | Border color | Draft, archived, metadata |
| `Success` | Success color | Success tint, 10% into the surface | Success tint, 25% into the border | Live, paid, passing, healthy |
| `Info` | Strong accent | Subtle accent | Border with a 24% accent ring | Beta, syncing, accent emphasis |
| `Warning` | Warning color | Warning tint, 11% into the surface | Warning tint, 27% into the border | Pending, degraded, expiring |
| `Danger` | Danger color | Danger tint, 10% into the surface | Danger tint, 26% into the border | Failed, overdue, blocked |

The success, warning and danger fills are fixed mixes of the state color into the neutral surface, so they keep their
meaning when the accent changes. `Info` follows the accent, which is why it is the tone for accent emphasis and not
for a state that must always look the same.

### Sizes

`Size` sets the height: `Medium` (the default) is 26 px and `Small` is 22 px. Use `Small` in dense tables and
`Medium` beside headings and in cards.

<Demo name="Small" />

| Size | Minimum height | Side padding | Label | Icon gap |
|---|---|---|---|---|
| `Medium` | 26 px | 10 px | Arc XS | 5 px |
| `Small` | 22 px | 8 px | 11 px | 4 px |

Use one size for each table column or group, so the rows keep the same height. A table cell of a fixed width fits a
`Small` badge with a one-word label and an icon.

### Icon

`Icon` takes a leading icon. It takes the tone color and is hidden from assistive technology. The example uses an
`ArcGlyph` from `ArcIcons`, the Arc icon set, at 12 px.

<Demo name="Icons" />

```csharp
badge.Tone = BadgeTone.Success;
badge.Content = "Passing";
badge.Icon = new ArcGlyph { Data = ArcIcons.Check, Size = 12 };
```

Add the icon when the glyph adds information, such as a warning triangle for a failed check. A decorative icon on
every badge makes a table noisy, and the same tone already carries the color.

### Changing in place

Keep the badge mounted and change `Content`. A new string or number rises in from a soft blur, the old one lifts away,
and the pill width springs to fit. Re-creating the badge loses this motion and builds a new control for every change.

<Demo name="ChangingInPlace" />

A new icon pops in from a slightly smaller size. A change caused by a font or a theme change, rather than by new
content, takes effect at once, so the layout does not play a spring when nothing about the state changed.

### Binding to a view model

`Badge` only displays its values. Bind `Content`, `Tone`, `Size` and `Icon` from the view model; the badge never
writes a value back and raises no events or commands for the view model to handle.

```csharp
public partial class DeploymentViewModel : ObservableObject
{
    [ObservableProperty]
    private string _statusText = "Queued";

    [ObservableProperty]
    private BadgeTone _statusTone = BadgeTone.Info;

    [ObservableProperty]
    private int _commentCount;
}
```

```xml
<StackPanel Orientation="Horizontal" Spacing="8">
    <arc:Badge Tone="{Binding StatusTone}" Content="{Binding StatusText}" />
    <arc:Badge Tone="Neutral" Content="{Binding CommentCount}" />
</StackPanel>
```

Set these properties on the UI thread, because Avalonia properties must change there. When the state changes, update
the text and the tone together, so the label and its color move in the same frame.

### Data shape and performance

A badge is cheap for a few labels and costs more in a long table. Each badge measures its own width to spring to fit
its label, so a table with thousands of badges on screen does more layout work than a table of plain text. In long
tables:

- Use `Small` and a one-word label, so the width changes little when the value changes.
- Keep the rows virtualized, as the Avalonia `ListBox` and `DataGrid` do by default, so only visible rows create
  badges.
- Format numbers in the code that owns them, with `ToString("N0", culture)` or a formatted string, so the
  separators match the locale: "1,204" in English and "1.204" in German.

### Example: invoice status table

A billing operations page lists the invoices sent this week. Each row names the customer and shows the status as a
`Small` badge. Paid invoices use the success tone and need no icon. A pending invoice gets a clock icon, and a failed
one gets a warning triangle, because a failed payment needs a person to act and the icon adds a second signal. The draft
row uses the neutral tone so it reads as "not sent yet" and not as a problem.

![Invoice status table with paid, pending, failed and draft badges in a Small size](images/BadgeStatusTable.png)

```xml
<Border Width="640"
        Padding="20"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1"
        CornerRadius="12">
    <StackPanel Spacing="14">
        <TextBlock Text="Invoices sent this week"
                   FontSize="16"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <Grid ColumnDefinitions="176,*,Auto">
            <TextBlock Text="Invoice" FontSize="12"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            <TextBlock Grid.Column="1" Text="Customer" FontSize="12"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            <TextBlock Grid.Column="2" Text="Status" FontSize="12"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </Grid>
        <Border Height="1" Background="{arc:ArcDesignTokenResource Border}" />
        <Grid ColumnDefinitions="176,*,Auto">
            <TextBlock Text="inv_2026_0412" FontSize="12" VerticalAlignment="Center"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            <TextBlock Grid.Column="1" Text="Northwind Traders" VerticalAlignment="Center"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <arc:Badge Grid.Column="2" Size="Small" Tone="Success" Content="Paid" />
        </Grid>
        <Grid ColumnDefinitions="176,*,Auto">
            <TextBlock Text="inv_2026_0413" FontSize="12" VerticalAlignment="Center"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            <TextBlock Grid.Column="1" Text="Acme Analytics" VerticalAlignment="Center"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <arc:Badge Grid.Column="2" Size="Small" Tone="Warning" Content="Pending">
                <arc:Badge.Icon>
                    <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Clock}" Size="12" />
                </arc:Badge.Icon>
            </arc:Badge>
        </Grid>
        <Grid ColumnDefinitions="176,*,Auto">
            <TextBlock Text="inv_2026_0414" FontSize="12" VerticalAlignment="Center"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            <TextBlock Grid.Column="1" Text="Contoso Labs" VerticalAlignment="Center"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <arc:Badge Grid.Column="2" Size="Small" Tone="Danger" Content="Failed">
                <arc:Badge.Icon>
                    <arc:ArcGlyph Data="{x:Static arc:ArcIcons.TriangleAlert}" Size="12" />
                </arc:Badge.Icon>
            </arc:Badge>
        </Grid>
        <Grid ColumnDefinitions="176,*,Auto">
            <TextBlock Text="inv_2026_0415" FontSize="12" VerticalAlignment="Center"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            <TextBlock Grid.Column="1" Text="Fabrikam Studio" VerticalAlignment="Center"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <arc:Badge Grid.Column="2" Size="Small" Content="Draft" />
        </Grid>
    </StackPanel>
</Border>
```

### Content guidelines

- **Label**: one or two words that name the state or category, in sentence case and without punctuation: "Paid",
  "Pending", "Live". The badge is read as its own name, so it must make sense without the color.
- **Numbers**: a count is a number in digits, formatted for the locale ("1,204"), and it stays short enough that the
  pill does not grow past its neighbours.
- **Tone**: pick the tone from the meaning of the state, not from the look you want. Danger means a person must act;
  Warning means a state needs attention soon; Success means the state is good.

| Instead of | Write |
|---|---|
| `Content="LIVE NOW!!"` | `Content="Live"` |
| `Content="Status: failed"` | `Content="Failed"`, the row gives the context |
| `Content="Your card was declined"` | Use `Alert`; a badge holds a word or a short phrase |
| `Content="1204"` | `Content="1,204"`, formatted for the locale |
| `Tone="Danger"` on a neutral count | `Tone="Neutral"`, since red means action is needed |

### Best practices

**Do**

- Name the state in the label. The tone is a color, so color-blind readers and grayscale prints lose it when the label
  is vague.
- Keep the badge mounted and change `Content` and `Tone` when the state changes. The label rises in place and the pill
  springs to its new width, which tells the reader that something changed.
- Use one size in each column or group. Equal row heights keep a table scannable.
- Reserve `Danger` for failures and actions the person must take, and `Info` for accent emphasis. Mixed meanings
  across screens make people stop trusting the colors.

**Avoid**

- Using a badge for a sentence. A sentence needs room to wrap and an action to dismiss it, which `Alert` and `Toast`
  provide.
- Re-creating the badge for each state. A new control loses the in-place motion and builds layout for every change.
- Adding a long label or a large icon in a narrow table cell. The pill widens with the label and pushes the cells
  around it.
- Expecting a click. A badge does not take focus, so a status that the person must open needs a `Button` or a row
  action beside it.
- Announcing every change from the badge. A badge does not announce its own changes, so put it in a live region of
  the surrounding content when a change must be read aloud.

### Common questions

**Why does my count not animate?** The badge was probably re-created, or its `Content` is not a string or a number.
Only strings and numeric types get the in-place swap. Keep the same badge on screen and set `Content` to a string or a
number.

**Can a badge sit on an avatar as a count or a dot?** No. The badge is a standalone pill, not an overlay on another
element. For presence on a person, use the status dot on `Avatar`.

**Why does the Info tone change with the accent but Success does not?** `Info` is derived from the accent color, so it
follows the product's accent. Success, warning and danger are fixed tints of the state colors into the neutral
surface, so they keep their meaning when the accent changes.

**How do I change the tone from code?** Set `Tone` on the instance, for example `badge.Tone = BadgeTone.Warning;`. With
full motion the colors crossfade; with reduced motion the change is instant.

**Can I make the badge clickable?** Not directly. The badge is not focusable and has no click event. Put it in a
`Button` or a row that handles the action, and keep the badge as its visual label.

## API reference

<!-- api: Badge -->

## Accessibility

- The badge is inline text: its label is read with the text around it.
- The icon is hidden from assistive technology. The label must say the state, not the color.
- A string or number label is the badge's accessible name. A number is named with the current culture.
- While a label changes, the old text is hidden from assistive technology and only the current text is read.
- A badge does not announce its changes. If a change must be announced, put the badge in a live region of the
  surrounding content.
- The badge is not focusable and has no keyboard behavior: `Tab` skips it, and the row or control that holds the badge
  carries any keyboard work.

## Motion

- A new label rises from a soft blur, and the old one lifts away with a subtle blur. The pill width follows on a
  morph spring.
- A new icon pops in from a slightly smaller size. A size change caused by a font change happens at once.
- Reduced motion fades the label quickly and jumps the width. Off changes everything at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| The label changes | The old text lifts away with a subtle blur and the new text rises from a soft blur | Quick fade | Immediate |
| The pill width after a label change | Springs to fit the new label | Jumps to the new width | Immediate |
| The tone changes | Background, border and text crossfade on the standard curve | Instant | Immediate |
| The pointer enters or leaves (fine pointers) | Background and border crossfade; `Info` fades its accent wash | Instant | Immediate |
| A font or theme change re-sizes the badge | Happens at once | Happens at once | Immediate |

## Related

- `Alert` or `Toast` for a sentence of feedback.
- `ChipGroup` for values the user can select.
- `MetricCard` for a large value with its context.

| Component | Choose it when |
|---|---|
| `Alert` | A message needs a sentence and stays on the page |
| `Toast` | A short confirmation or error should appear and go away on its own |
| `ChipGroup` | The user selects or clears values |
| `MetricCard` | A large number needs its label, trend and context |
| `Avatar` | The status belongs to a person, shown as a dot on the photo |
| `SortableDataTable` | The badges sit in columns that the person sorts and compares |
