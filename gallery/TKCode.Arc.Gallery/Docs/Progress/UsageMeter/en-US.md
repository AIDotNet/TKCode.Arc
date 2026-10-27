---
title: Usage meter
summary: Show what fills an allowance and how close it is to the limit.
---

<Demo name="Storage" />

## When to use

`UsageMeter` shows how a set of categories fills a fixed allowance, and how close the total is to the limit. It is the
component for a usage or billing page: the person reads the free amount, the status and what uses the plan, in one
glance, without a table. It does not show a task moving toward completion, and it does not compare categories that have
no limit.

- **Storage on a billing page**: media, documents, backups and other files measured against the storage included in the
  plan. The legend lists each category, so a person sees that media takes the most room before they delete anything.
- **Egress or API calls in a usage report**: a monthly allowance such as 500 GB of egress, with the status badge
  switching to "Almost full" at 90% so the team can act before the limit.
- **Seats or projects on a plan**: a team with 20 seats across engineering, design and sales. When the total is past the
  limit, the overage reads as a number and a hatched bar, so a plan upgrade prompt has a precise amount to refer to.
- **Usage alerts in an admin console**: a workspace owner opens the usage panel and sees which workspace is over its
  allowance, with the overage named in the badge.
- **Quota per tenant in an internal platform**: each tenant's build minutes or container hours against its quota, shown
  on the tenant detail page beside the controls that change the quota.

## When not to use

- **Work that moves toward a finished state**, such as an import or a migration: use `Progress`. A meter has no
  "finished" state; it has a limit that the total can cross.
- **Categories with no limit, compared with each other**, such as revenue by region: use `BarChart`. A usage meter
  always measures against an allowance.
- **A single headline metric**, such as this month's active users: use `Gauge` for a value on a scale or `MetricCard`
  for a number with a trend.
- **More than a handful of categories**, such as a ledger of fifty cost centers: use `Treemap` or a sorted `Table`. The
  bar is readable with about four accent steps, and a legend of fifty entries does not help a person decide.
- **A share of a whole with no allowance**, such as the mix of traffic sources: use `DonutChart`, which shows proportions
  without a limit line.

## Usage

Give the meter a `Label`, the `Limit` and the categories. Each category is a `UsageSegment` with an `Id`, a `Label` and
a `Value` in the same unit as the limit. Keep the `Id` stable so each category keeps its own motion when the list
changes.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:UsageMeter Label="Workspace storage" Limit="50" Unit="GB">
        <arc:UsageSegment Id="media" Label="Media" Value="14.2" />
        <arc:UsageSegment Id="docs" Label="Documents" Value="9.6" />
        <arc:UsageSegment Id="backups" Label="Backups" Value="6.1" />
        <arc:UsageSegment Id="other" Label="Other" Value="2.4" />
    </arc:UsageMeter>
</UserControl>
```

`UsageMeter` derives from Avalonia's `TemplatedControl`. Its template draws the title, a status badge, the total, a
caption, the bar and a legend, and the meter computes the status from the categories and the limit. It is not focusable as a whole; the legend items inside it take the keyboard, so the meter can sit in a
card without taking a Tab stop of its own.

### Anatomy

1. **Title** (`Label`): the name of the measured thing, 16 px with medium weight. It wraps onto two lines if needed and
   sits in a header row at least 28 px high.
2. **Status badge**: a 28 px high pill with 12 px of horizontal padding and a 6 px gap before its icon. It reads
   "{free} free" while the total is under the warning share, "Almost full" with a warning icon from the warning share up
   to the limit, and "{overage} over" past the limit. Its background is the muted surface color at rest, a warning wash
   (14% warning color into the surface) when almost full, and a danger wash (12% danger color) when over.
3. **Amount**: the rolling total in the display font at 36 px, in the foreground color. It shows the
   total, or the amount of the category that is previewed or pinned.
4. **Caption**: 14 px copy in the secondary text color, beside the amount and aligned to its baseline. It says what the
   amount is: "GB of 50 GB used", "GB in Media · 28% of plan", or "GB free".
5. **Bar**: a 14 px track with full rounded ends, colored with the foreground color at 7% strength over the surface, so
   the empty part is visible in both themes. The used categories are runs of accent steps with a 2 px gap between them.
6. **Legend**: a grid of toggle items, one per category plus one for the remaining amount (`FreeLabel`) or for the
   overage (`OverLabel`). Each column is at least 108 px wide and the number of columns fills the width. Each item has an
   8 px inner padding, a 12 px corner radius, a 12 px swatch, a 14 px label and a rolling value at 16 px with the unit
   beside it.
7. **Focus ring**: a 2 px focus-ring outline drawn 4 px outside a legend item with a 16 px corner radius.

Vertical spacing between the header row, the amount, the bar and the legend is 16 px. The legend is pulled 8 px to the
left so its swatches line up with the title. The bar and the legend are the same width as the meter.

### Almost full and over the limit

The badge shows the free amount. At 90% of the limit (`WarnThreshold` 0.9 by default) it changes to "Almost full" with a
warning icon, and past the limit it shows the overage. `WarnThreshold` changes the share that triggers the warning, and
the same share drives the `Near` status in code.

<Demo name="AlmostFull" />

<Demo name="OverTheLimit" />

```xml
<arc:UsageMeter Label="API calls this month"
                Limit="100"
                Unit="K"
                WarnThreshold="0.8" />
```

Past the limit the bar is struck through with a hatch beside a limit marker, so the overage reads without color. The
legend's last item changes from the free amount to the overage, labelled with `OverLabel`. Keep the wording of the badge
and the legend in step with the plan: "8.1 GB over" tells the person what they will pay for or what they must remove.

### Binding to a view model

In an MVVM application the view model owns the categories, the limit and the rules for what the plan allows. The meter
shows them. With CommunityToolkit.Mvvm, the view model below keeps the categories in an `ObservableCollection<T>`, and
a usage feed updates the `Value` of each category. The meter observes the collection and each category, so the bar,
the total, the badge and the legend follow every change.

```csharp
public partial class StorageUsageViewModel : ObservableObject
{
    public StorageUsageViewModel()
    {
        Categories = new ObservableCollection<UsageSegment>
        {
            new UsageSegment("media", "Media", 14.2),
            new UsageSegment("docs", "Documents", 9.6),
            new UsageSegment("backups", "Backups", 6.1),
            new UsageSegment("other", "Other", 2.4)
        };
    }

    public ObservableCollection<UsageSegment> Categories { get; }

    [ObservableProperty]
    private double _limit = 50;

    [ObservableProperty]
    private string _unit = "GB";

    public void ApplyUsage(IReadOnlyDictionary<string, double> usage)
    {
        foreach (var category in Categories)
        {
            if (category.Id is not null && usage.TryGetValue(category.Id, out var value))
            {
                category.Value = value;
            }
        }
    }
}
```

```xml
<arc:UsageMeter Label="Workspace storage"
                ItemsSource="{Binding Categories}"
                Limit="{Binding Limit}"
                Unit="{Binding Unit}" />
```

- `ItemsSource` takes precedence over the inline `Segments`. Bind the collection of `UsageSegment` objects, not a list
  of numbers, so each category keeps its identity.
- `Limit` and `Unit` are one-way bindings. The meter writes nothing back to the view model.
- The category `Value` is the only value the application writes. Each `Value` change re-lays out the bar and recalculates
  the total and the status. Set `Value` when the number changes, not on every timer tick of a feed that does not change.
- `Status` and `Total` are read-only. Read them in code when the page must react, for example to show an upgrade prompt.
- Compiled bindings need `x:DataType` on the view; the examples here leave it out for brevity.

### Data shape and performance

Each category is a `UsageSegment` with an `Id`, a `Label` and a `Value`. The `Id` is the identity of the category across
updates: keep it when the value changes, so the run re-flows instead of being replaced. Use an `Id` from your data
(a category key or a database identifier), never the label, which may change when a person renames a category.

- Four categories read clearly. The accent steps are four shades (the full accent, then 62%, 36% and 22% into the
  surface), so a fifth category is hard to tell from its neighbours. Group the smallest categories into "Other" before
  you pass them to the meter.
- The legend is a grid, so more categories add rows rather than crowding the bar. Keep the labels short, because each
  legend item shows its full label in a column that is at least 108 px wide.
- The total is rounded to `Decimals` (1 by default, 0 to 6 allowed). Format the amounts in the data layer when a
  category needs a unit that the meter does not show, such as a currency. The meter formats numbers with a period as
  the decimal separator in every culture, so put the localized unit in `Unit` and the localized label in `Label`.
- Each `Value` change refreshes the whole meter. Write only the values that changed, not every category on each tick
  of a feed.

### Units and decimals

`Unit` is shown after each amount. `Decimals` sets the digits after the decimal point. Use `Decimals="0"` for whole
counts such as seats.

```xml
<arc:UsageMeter Label="Seats" Limit="20" Decimals="0">
    <arc:UsageSegment Id="eng" Label="Engineering" Value="12" />
    <arc:UsageSegment Id="design" Label="Design" Value="6" />
    <arc:UsageSegment Id="sales" Label="Sales" Value="5" />
</arc:UsageMeter>
```

Choose one precision per meter. A storage meter in gigabytes reads well with one decimal, so a 0.1 GB upload is visible;
a seat count with a decimal place reads as a mistake. The limit text drops a trailing zero, so a limit of 50.0 reads
"50 GB".

### Labels for the free and over amounts

`FreeLabel` names the remaining allowance in the legend (default "Free"), and `OverLabel` names the overage once the
total is past the limit (default "Over limit"). Use plain nouns that match the plan's language.

```xml
<arc:UsageMeter Label="Projects"
                Limit="10"
                FreeLabel="Remaining"
                OverLabel="Above plan" />
```

### Status in code

Read `Status` for the current state (`Ok`, `Near` or `Over`), and `Total` for the sum of all categories. The status uses
the same rule as the badge: `Near` from the warning share of the limit, and `Over` when the total is above the limit
itself.

```csharp
if (meter.Status == UsageMeterStatus.Over)
{
    ShowUpgradePrompt();
}
meter.ClearPin(); // clears a category pinned from the legend
```

### Example: plan usage on the billing page

A billing page in a SaaS admin console shows three allowances for the current month: the workspace storage, which is
within the plan with 17.7 GB free; the egress for the month, which is almost full at 91.5% and so warns before the
limit; and the storage of a team sandbox, which is over its limit by 2.2 GB. Each title names the thing measured, the
unit is the one the plan's table uses, and the caption states the limit, so the person can judge each state without
opening the plan details. The three states use three different badges and the overage is hatched, so the page reads
without color.

![Three usage meters on a billing page in the Ok, Near and Over states](images/PlanUsage.png)

```xml
<Border Width="640"
        Padding="24"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1">
    <StackPanel Spacing="28">
        <TextBlock Text="Plan usage, October 2026"
                   FontSize="{arc:ArcDesignTokenResource TypeLG}"
                   FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <arc:UsageMeter Label="Workspace storage" Limit="50" Unit="GB">
            <arc:UsageSegment Id="media" Label="Media" Value="14.2" />
            <arc:UsageSegment Id="docs" Label="Documents" Value="9.6" />
            <arc:UsageSegment Id="backups" Label="Backups" Value="6.1" />
            <arc:UsageSegment Id="other" Label="Other" Value="2.4" />
        </arc:UsageMeter>
        <arc:UsageMeter Label="Egress this month" Limit="500" Unit="GB">
            <arc:UsageSegment Id="dashboards" Label="Dashboards" Value="280.4" />
            <arc:UsageSegment Id="exports" Label="Exports" Value="176.9" />
        </arc:UsageMeter>
        <arc:UsageMeter Label="Team sandbox storage" Limit="20" Unit="GB">
            <arc:UsageSegment Id="designs" Label="Designs" Value="12.4" />
            <arc:UsageSegment Id="recordings" Label="Recordings" Value="9.8" />
        </arc:UsageMeter>
        <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
            <arc:Button Content="Manage plan" Variant="Secondary" />
            <arc:Button Content="Upgrade plan" />
        </StackPanel>
    </StackPanel>
</Border>
```

Keep the two actions where the person needs them: the badge says what state the allowance is in, and the footer offers
the next step. The team sandbox meter shows the overage as a number ("2.2 GB over"), which is the figure the upgrade
decision depends on.

### Content guidelines

- **Title**: the thing measured as a short noun phrase in sentence case: "Workspace storage", "Egress this month". Add
  the period only when the limit resets on it.
- **Unit**: the short unit the plan uses, such as "GB". It is shown after every amount, so keep it short.
- **Free and over labels**: keep the defaults "Free" and "Over limit" unless the plan uses other words. Use the same
  nouns across the page.
- **Caption and badge**: state the amount and the limit with digits. "17.7 GB free" is clearer than "Plenty left".

| Instead of | Write |
|---|---|
| Storage usage | Workspace storage |
| You are almost out of space!! | Almost full. 4.3 GB free. |
| Limit exceeded by a lot | 2.2 GB over |
| Free space | Free |
| Overage: 2.2 | Over limit: 2.2 GB |

### Best practices

**Do**

- Put `Value` and `Limit` in the same unit, and show that unit in `Unit`, so every number carries it. A mix of GB and MB
  in one meter makes the bar meaningless.
- Choose `WarnThreshold` so the warning comes early enough to act: a lower share such as 0.8 suits an allowance that
  takes a week to raise.
- Read `Status` in code when a banner must agree with the badge, instead of computing the same rule twice.
- Name the action next to the meter that the overage calls for, such as "Upgrade plan", so a person who sees "over" knows
  what to do.

**Avoid**

- Using the meter for a value with no ceiling. The limit is drawn as the reference, so an unlimited plan has no meaning
  here; use `MetricCard` for the count.
- Passing many small categories. The accent steps are four shades, so beyond four segments the bar stops being
  readable; group the smallest into "Other".
- Relying on the bar's color for the overage. The hatch, the limit marker and the badge text carry the state.
- Showing more precision than the plan uses. A storage meter with two decimals suggests a precision nobody can act on.

### Common questions

**Why does the badge show "Almost full" and not the free amount?** At the warning share the status becomes `Near` and
the badge names it. The free amount is still in the legend and in the text summary read by assistive technology.

**Why does the total not match the sum I expect?** The total is rounded to `Decimals` after the sum. Values with more
decimals than `Decimals` are added first and then rounded, so set `Decimals` to the precision the plan uses and round
the values in your data the same way.

**How do I clear a category that is pinned from the legend?** Press the pinned item again, press `Escape` while the
legend has focus, or call `ClearPin()` from code.

**Does changing the limit update the status?** Yes. Changing `Limit`, `Unit`, `Decimals`, `WarnThreshold` or the label
properties refreshes the meter, so the status, the badge and the legend follow the new allowance at once.

## API reference

<!-- api: UsageMeter -->

<!-- api: UsageSegment -->

Status values of `UsageMeter.Status`:

| Member | Meaning |
|---|---|
| `Ok` | The total is under the warning share (`WarnThreshold`) of the limit |
| `Near` | The total is at or past the warning share, and not yet past the limit |
| `Over` | The total is past the limit |

## Accessibility

- The meter is a group named by `Label`. The bar is an image whose name is a full text summary of every category, the
  total and the status, for example "Workspace storage: 32.3 GB of 50 GB used. Media 14.2 GB, Documents 9.6 GB, Backups
  6.1 GB, Other 2.4 GB. 17.7 GB free."
- The rolling digits are kept out of the control view, and assistive technology reads the text summaries instead, so a
  change is not read digit by digit.
- Status changes are announced politely: "Almost full. 4.3 GB free." when the total crosses the warning share and
  "Over the plan limit by 8.1 GB." when the total passes the limit. A growing overage is announced too; routine changes
  inside one status stay quiet.
- Each legend item is a toggle button, pressed while its category is pinned, named with its category and amount, for
  example "Media, 14.2 GB". The free and overage items are named the same way.
- Hovering with a mouse and focusing a legend item preview that category: the amount and the caption show its share
  of the plan until the pointer or focus moves away.
- Color is never the only signal: the badge carries text and an icon, and the overage is shown with a hatch and a limit
  marker, so the state reads in grayscale.

### Keyboard

The legend is one tab stop with roving focus: the legend item that was used last holds the Tab stop, and the arrow keys
move between items.

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Enter the legend at the last used item, or leave it |
| `Right` / `Down`, `Left` / `Up` | Move to the next or previous legend item; the focus wraps from the last item to the first and back |
| `Home` / `End` | Move to the first or last legend item |
| `Enter` / `Space` | Pin or unpin the focused category; a pinned category keeps the bar, the amount and the caption on it |
| `Escape` | Clear the pinned category |

## Motion

- The first time 40% of the meter is visible, the categories grow in from left to right, one after another.
- After that, each category slides to its new width when a value changes, and the numbers roll toward the new value.
- Past the limit, the overage hatch and the limit marker fade in.
- Hovering or focusing a category dims the others, and a press pins it.
- Reduced motion sets the values and the bar directly and swaps the text with a short fade.

| Change | Full | Reduced | Off |
|---|---|---|---|
| First view of the meter | Categories grow from left to right, 0.58 s, 70 ms apart, once | Shown at full size | Shown at full size |
| A category value changes | Its run slides to the new width on a smooth spring; the runs after it follow in the same frame | Set directly | Set directly |
| A number changes | The digits roll toward the new value, in the direction of the change | Set directly, with the text swapped by a short fade | Immediate |
| A category is previewed or pinned | The other runs dim to 28% and the legend items to 45% over a fast tween | Immediate | Immediate |
| Usage crosses the limit | The overage hatch and the limit marker fade in | Immediate | Immediate |
| Status badge changes | Its width fits the new text on a morph spring; the color washes crossfade | The text swaps with a short fade | Immediate |

## Related

- `Progress` when work moves toward completion and has no allowance.
- `BarChart` for categories without a limit, compared with each other.
- `Gauge` or `MetricCard` for one headline number.

| Component | Choose it when |
|---|---|
| `Progress` | A task moves toward completion and has no allowance to exceed |
| `BarChart` | Categories have no limit and are compared with each other |
| `DonutChart` | Only the share of each category in a whole matters, with no limit |
| `Gauge` | One value is read against a scale, such as a score or a load |
| `MetricCard` | One headline number needs a label, a delta and a trend |
