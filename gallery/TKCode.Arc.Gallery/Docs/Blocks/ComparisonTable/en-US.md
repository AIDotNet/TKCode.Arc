---
title: Comparison table
summary: An us versus them table with a pinned header, a highlighted column and a stacked phone view.
---

<Demo name="Preview" />

## When to use

Use `ComparisonTable` on a pricing or alternatives page to show how your product stacks up against named alternatives, with three to five columns and grouped rows.

## When not to use

- **Comparing your own plans**: use the `PlanComparison` block.
- **Large data sets that need sorting**: use a data table.
- **Claims you cannot verify**: keep competitor names generic or factual.

## Usage

```csharp
table.Columns =
[
    new ComparisonColumn { Id = "us", Name = "Relay", Caption = "$10 per seat", IsHighlighted = true },
    new ComparisonColumn { Id = "them", Name = "Legacy suite", Caption = "$24 per seat" }
];
table.Sections =
[
    new ComparisonSection
    {
        Id = "core", Title = "Core",
        Rows =
        [
            new ComparisonRow { Id = "offline", Feature = "Offline mode", Values = { ["us"] = true, ["them"] = false } },
            new ComparisonRow { Id = "sso", Feature = "SSO", Values = { ["us"] = true, ["them"] = ComparisonValue.Partial("Top plan") } },
            new ComparisonRow { Id = "seats", Feature = "Seats", Values = { ["us"] = "Unlimited", ["them"] = "Up to 50" } }
        ]
    }
];
table.Cta = new ComparisonCta { Label = "Start free trial", Href = "https://example.com/signup" };
```

<Demo name="CustomData" />

A value is a `bool` (check or dash), a string (printed text), or `ComparisonValue.Partial(note)`. A column without a value shows a dash. Mark your product with `IsHighlighted`; otherwise the first column is yours.

### Only differences

The switch sets `IsDifferencesOnly`, which hides rows where every visible column has the same value; notes do not count. A section with no rows left closes its title too.

```xml
<arc:ComparisonTable MaxTableHeight="560" StickyTop="64" IsDifferencesOnly="True" />
```

### The pinned header

The header stays pinned while the table passes under it, in any scrolling ancestor. `StickyTop` offsets it under a fixed site header. `MaxTableHeight` caps the table and scrolls it inside the block.

### Narrow containers

Below `StackBelow` the table shows your column beside one competitor, chosen with a picker (`CompareWith`).

<Demo name="Narrow" />

### The call to action

`Cta` adds a button at the foot of your column. With `Href` and no `Command` it opens a link, unless a handler of `CtaInvoked` marks the event handled. Otherwise it runs the command and morphs to `DoneLabel`.

## API reference

<!-- api: ComparisonTable -->

<!-- api: ComparisonCtaEventArgs -->

## Accessibility

- Each cell has a text alternative with its column name: "Big suite: Included", "Partial, Top plan", "Not included".
- Meaning never rests on color alone: checks, half circles and dashes differ in shape, and a legend names them.
- The switch is named by `DifferencesLabel`, and a live region says when rows are filtered.
- `Tab` reaches the switch, the picker and the call to action; `Space` flips the switch.

## Motion

- Checks in your column draw themselves once in 480 ms when their row is fully in view, staggered 35 ms per row after a 60 ms delay, up to the twelfth row.
- "Only differences" collapses rows and section titles on the smooth spring.
- Picking a competitor rolls its header 8 px and pops the marks (opacity and scale .8 on the snappy spring).
- The call to action morphs its width and swaps its label with a 2 px blur.
- Reduced motion shows marks at once, fades the swaps and collapses without a spring. Off is instant.

## Responsive behavior

- The block follows its own width. At 820 px the feature column narrows; at 640 px the spacing tightens; below `StackBelow` (640 px) it stacks into two columns with a picker.
- Column widths are fractions, so it never scrolls sideways. Row hover applies to a mouse or a pen only.

## Related

- The `PlanComparison` block for your own pricing tiers.
- The `PricingCalculator` block for prices that follow a seat count.
