---
title: Billing toggle
summary: A monthly and yearly switch with a savings badge and prices that roll to the new amount.
---

<Demo name="Pricing" />

## When to use

`BillingToggle` is the segmented switch that picks a billing period, usually at the top of a pricing page or in the
plan section of a billing settings screen. It pairs with `BillingPrice`, which shows the amount for the chosen period
and rolls to the new number when the period changes, so the person sees the cost of each choice without leaving the
page.

- **Billing period on a pricing page**: a SaaS product that sells monthly and yearly plans. The savings badge ("Save
  20%") on the yearly option explains the discount where the decision is made.
- **Plan changes in billing settings**: an account page where an admin moves a workspace from monthly to yearly
  billing. The toggle shows the new amount and the "billed yearly" period text before the person confirms.
- **Lifetime or one-time options next to subscriptions**: a developer tool that sells monthly, yearly and lifetime
  licenses. A three-option toggle with a "Best value" badge keeps the choice in one row.
- **Checkout summaries with a period switch**: a checkout step that shows the total for a monthly or annual term, with
  the badge on the selected option stating the saving in money ("You save $48").

## When not to use

- **Switching views that do not change a price**, such as a list and a board of the same orders: use
  `SegmentedControl`. It has no badges and no price semantics.
- **Options that need a description or a vertical layout**, such as a list of plans with a sentence about each: use
  `RadioGroup` or `RadioCards`. `BillingToggle` is a single horizontal row of short labels.
- **A single on or off setting**, such as "Renew automatically": use `Switch`, which reads as a setting that takes
  effect at once.
- **Plans that each carry a price, features and a reason when unavailable**: use `RadioCards`. Put the price in the
  card and keep the toggle for the period only.
- **Choosing from a long list of billing currencies or regions**: use `Select` or `Combobox`. The toggle is for two
  to three options that fit in one row.

## Usage

The toggle with no options shows Monthly and Yearly, and Yearly carries the badge "Save 20%". Set `Value` to the
selected option's value.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:BillingToggle Value="yearly" />
</UserControl>
```

`BillingToggle` derives from Avalonia's `SelectingItemsControl`. It therefore has the usual items model (`ItemsSource`,
`Items`, `SelectedIndex`, `SelectedItem`, `SelectedValue`) and the selection events of that base class. `Value` is the
property to use in an application: it holds the selected option's value as a string, is two-way, and stays in sync
with `SelectedValue`. The toggle adds the savings badges, the gliding thumb and the period semantics on top of the
selecting control, so a billing screen gets a complete control without a custom template.

### Anatomy

1. **Track** (`BillingToggle` itself): a pill on the muted surface color with a 1 px subtle border. It is inset by 3 px
   on every side (4 px in `Large`) and holds the options in a horizontal row.
2. **Thumb**: a raised pill on the raised surface color with a soft shadow, under the selected option. It is one
   shape for the whole track, and it moves between options instead of appearing in each one.
3. **Option** (one `BillingToggleOption` per period): 34 px tall in `Medium` and 42 px in `Large`. It has 14 px side
   padding (18 px in `Large`) and the label in medium weight.
4. **Label** (`Label`, or `Value` when no label is given): the period name, such as "Monthly" or "Yearly". The text uses
   the small type size in `Medium` and the base size in `Large`.
5. **Badge** (`Badge`): a savings note 24 px tall in `Medium` and 32 px in `Large`, 8 px of side inset, in a pill with a
   quiet background (the foreground at 6%) and secondary text. It sits 8 px after the label.
6. **Active badge** (`ActiveBadge`): the text the badge shows while its option is selected, such as "You save $48".
   When it is not set, the badge keeps `Badge`. The badge takes the accent color while selected (see Size and accent).
7. **Focus ring**: a 2 px ring around the selected option. It appears for keyboard focus only.

A `BillingPrice` is a separate control with its own parts: the currency sign, the amount in the display font, the
old price (`WasAmount`) struck through to the left of the period text, and the period text (`Period`). The currency sign
is set at about 0.55 of the amount's size and raised to sit level with the top of the figures.

### Custom options

Write the options inline or bind `ItemsSource` to a list of `BillingToggleOption` objects. Options added inline must
exist before the toggle joins the logical tree, because the toggle installs its default Monthly and Yearly options only
when it has no options at that moment. `Badge` is shown on the option. `ActiveBadge` replaces it while the option is
selected, so a note can change in place without moving the content around it. Every badge text an option can show
reserves the same width, so "Save 20%" and "You save $48" do not move the neighboring option.

Use a badge for information that helps choose the option: a saving, a "Best value" marker or a plan limit. Leave it out
when every option has the same cost, because a badge that says nothing extra adds noise to the row.

<Demo name="ThreePeriods" />

### Prices

`BillingPrice` shows an amount. `Currency` defaults to `$` and `Decimals` to 0; a negative `Decimals` counts as 0.
`Amount` is a `decimal`, and the value is formatted with the en-US culture, so use a `decimal` and not a formatted
string. `WasAmount` draws the old price struck through when it is higher than `Amount`; a lower or null value hides it.
`Period` is the text after the amount, and it swaps in place when it changes.

```xml
<arc:BillingPrice Amount="24" WasAmount="30" Period="per month" />
```

When the period changes, update the price in the same handler that receives the toggle's value. The amount rolls to
the new number, the old price fades out with its own last value, and the period text swaps. The example below sets the
three values together, so the card never shows a new amount with an old period.

```csharp
var toggle = new BillingToggle { Value = "yearly" };
var price = new BillingPrice { Amount = 24, WasAmount = 30, Period = "per month, billed yearly" };

toggle.ValueChanged += (_, e) =>
{
    var yearly = e.NewValue == "yearly";
    price.Amount = yearly ? 24 : 30;
    price.WasAmount = yearly ? 30 : null;
    price.Period = yearly ? "per month, billed yearly" : "per month";
};
```

The `ValueChanged` event carries `OldValue` and `NewValue`. It bubbles, so a handler on a parent panel also receives
it. Use the event for side effects such as tracking or reloading a quote; use the bound view model for the data.

### Size and accent

`Size` sets the option height: `Medium` is 34 and `Large` is 42. Use one size for the whole billing area. `Medium`
fits a settings form and a pricing row; `Large` suits the hero of a pricing page, where the toggle is the first control a
visitor touches. The track inset and the text size grow with the size, so the two sizes are not simply scaled copies.

`BadgeAccent` colors the selected badge. Its default is the Arc success color. The badge background is the accent at
13% opacity in the light theme and 18% in the dark theme, and the badge text is the accent mixed with a little
foreground in the light theme. The accent is a brush the app may set, so it is derived in code rather than read from a
token. Keep the accent to the success or information family: a red or amber badge reads as a warning, not as a saving.

```xml
<arc:BillingToggle Size="Large" Value="yearly" />
```

### Binding to a view model

`Value` is two-way and carries the selected option's value. Bind it to a string property in the view model. The prices
are one-way inputs of `BillingPrice`, so the view model owns their numbers.

```csharp
public partial class PricingViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _billingPeriod = "yearly";

    [ObservableProperty]
    private decimal _proAmount = 24;

    [ObservableProperty]
    private decimal? _proWasAmount = 30;

    [ObservableProperty]
    private string _proPeriod = "per month, billed yearly";

    partial void OnBillingPeriodChanged(string? value)
    {
        var yearly = value == "yearly";
        ProAmount = yearly ? 24 : 30;
        ProWasAmount = yearly ? 30 : null;
        ProPeriod = yearly ? "per month, billed yearly" : "per month";
    }
}
```

```xml
<StackPanel Spacing="24">
    <arc:BillingToggle Value="{Binding BillingPeriod}" HorizontalAlignment="Center" />
    <arc:BillingPrice Amount="{Binding ProAmount}"
                      WasAmount="{Binding ProWasAmount}"
                      Period="{Binding ProPeriod}" />
</StackPanel>
```

- `Value` is two-way. A change made by the person writes the new value back to `BillingPeriod`, and a change made in
  code selects the matching option.
- Bind `BillingPeriod` as a `string`. Options carry string values such as "monthly" and "yearly"; map it to an enum in the
  view model.
- Set the three price properties from one method, as `OnBillingPeriodChanged` does, so the view model never exposes
  a half-updated price.
- Compiled bindings need `x:DataType` on the view (the examples here leave it out for brevity).

### Example: plan section of a pricing page

A pricing page for a project management product, Northwind Cloud. The toggle sits above three plans. Each plan shows
its monthly price under yearly billing, the old monthly price struck through and the period beside it. The yearly
option carries the saving, and the Pro plan has a call to action in a card the same height as its neighbors.

![A pricing section with a yearly billing toggle and three plan prices with struck-through monthly prices](images/PricingPlans.png)

```xml
<StackPanel Width="680" Spacing="24">
    <TextBlock Text="Plans for Northwind Cloud"
               FontSize="22"
               FontWeight="SemiBold"
               HorizontalAlignment="Center"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:BillingToggle Size="Large" Value="yearly" HorizontalAlignment="Center">
        <arc:BillingToggleOption Value="monthly" Label="Monthly" />
        <arc:BillingToggleOption Value="yearly"
                                 Label="Yearly"
                                 Badge="Save 20%"
                                 ActiveBadge="You save $48" />
    </arc:BillingToggle>
    <Grid ColumnDefinitions="*,*,*">
        <Border Grid.Column="0"
                Margin="0,0,16,0"
                Padding="20"
                CornerRadius="12"
                BorderThickness="1"
                BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}"
                Background="{arc:ArcDesignTokenResource SurfaceRaised}">
            <StackPanel Spacing="12">
                <TextBlock Text="Starter" FontWeight="SemiBold"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                <arc:BillingPrice Amount="10" WasAmount="12" Period="per month" />
                <TextBlock Text="Up to 5 members and shared boards."
                           TextWrapping="Wrap" FontSize="13"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                <arc:Button Content="Choose Starter" Variant="Secondary" HorizontalAlignment="Stretch" />
            </StackPanel>
        </Border>
        <Border Grid.Column="1"
                Margin="0,0,16,0"
                Padding="20"
                CornerRadius="12"
                BorderThickness="1"
                BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}"
                Background="{arc:ArcDesignTokenResource SurfaceRaised}">
            <StackPanel Spacing="12">
                <TextBlock Text="Pro" FontWeight="SemiBold"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                <arc:BillingPrice Amount="24" WasAmount="30" Period="per month" />
                <TextBlock Text="Unlimited boards, audit logs and SSO."
                           TextWrapping="Wrap" FontSize="13"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                <arc:Button Content="Choose Pro" HorizontalAlignment="Stretch" />
            </StackPanel>
        </Border>
        <Border Grid.Column="2"
                Padding="20"
                CornerRadius="12"
                BorderThickness="1"
                BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}"
                Background="{arc:ArcDesignTokenResource SurfaceRaised}">
            <StackPanel Spacing="12">
                <TextBlock Text="Team" FontWeight="SemiBold"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                <arc:BillingPrice Amount="48" WasAmount="60" Period="per month" />
                <TextBlock Text="Everything in Pro, plus admin controls."
                           TextWrapping="Wrap" FontSize="13"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                <arc:Button Content="Choose Team" Variant="Secondary" HorizontalAlignment="Stretch" />
            </StackPanel>
        </Border>
    </Grid>
</StackPanel>
```

### Content guidelines

- **Option label**: a single word for the period: "Monthly", "Yearly", "Lifetime". Use sentence case, and avoid
  "Pay yearly" or "Annual billing" because the toggle is a choice of period, not an action.
- **Badge**: a short saving or a single fact: "Save 20%", "Best value". Write percentages with the digit and the sign,
  and money with the currency sign and no decimals when the amount is whole.
- **Active badge**: the saving in money for the selected period, such as "You save $48". Keep it the same length as
  the badge when you can; a long active text makes the option wider and the row jumps.
- **Price period**: state the basis of the price, "per month, billed yearly", so the person knows the total they
  will be charged. A price without a period is ambiguous.
- **Accessible name**: name the group for what it selects when "Billing period" is not the right name, for example
  "Subscription term" in a renewal dialog.

| Instead of | Write |
|---|---|
| Pay annually | Yearly |
| Save 20 percent | Save 20% |
| $24/mo | $24 per month, billed yearly |
| Best deal!!! | Best value |
| Annual plan billing cycle | Billing period |

### Best practices

**Do**

- Keep the monthly price visible when the yearly price is shown, with `WasAmount` or nearby text, so the saving is
  checked against a real number.
- Set `Size="Large"` for the hero toggle of a pricing page and `Medium` for settings. Mixing sizes in one row makes the
  options look unrelated.
- Update the amount, the old price and the period together, in one handler or one view model method.
- Bind `Value` to a string property, and keep the option values stable ("monthly", "yearly") because your backend and
  analytics store them.

**Avoid**

- Using the toggle to switch a list or a dashboard view. People expect a period switch to change prices, so a view
  switch that moves the badges and numbers reads as a broken price change.
- Long badge text in a short option. The badge reserves the width of every text it can show, so a long active text
  forces the whole row wider.
- Setting `WasAmount` to a value equal to or lower than `Amount` expecting a strike-through. It hides the old price,
  so the saving disappears without an explanation.
- Coloring the badge with a warning color; the accent is for a saving or a benefit, and a warning hides the message.
- Hiding the period text, which tells the person whether the amount is monthly or a yearly total.

### Common questions

**Why does my toggle show Monthly and Yearly when I did not add options?** With no items of its own, the toggle installs
`DefaultOptions` (Monthly, and Yearly with "Save 20%") when it joins the logical tree. Add your own options, or
`ItemsSource`, before the toggle is attached to show different periods.

**What is selected when I do not set `Value`?** The first option is selected when `Value` is unset, and that choice is written back to `Value`. Set `Value` explicitly when the default period must be a
particular one, such as yearly, and not the first option.

**The old price does not appear.** `WasAmount` shows only when it is higher than `Amount`. Set it to `null` to hide the
old price, or check that the monthly price is the larger number.

**Can I use a number or an enum as the value?** The `Value` property and `BillingToggleOption.Value` are strings. Use a
string key such as "yearly" and map it to your enum in the view model. 

**How do I change the accessible name of the group?** Set `AutomationProperties.Name` on the toggle. The default name is
"Billing period". Each option is announced with its label and its current badge, such as "Yearly Save 20%".

## API reference

<!-- api: BillingToggle -->
<!-- api: BillingPrice -->
<!-- api: BillingToggleOption -->

### BillingToggleValueChangedEventArgs

| Property | Type | Description |
|---|---|---|
| `OldValue` | `string?` | The value before the change. |
| `NewValue` | `string?` | The value after the change. |

### BillingToggleSize

| Member | Description |
|---|---|
| `Medium` | 34 px options (the default). |
| `Large` | 42 px options with the base text size. |

## Accessibility

- The group is named "Billing period" by default. Set `AutomationProperties.Name` to name it differently.
- Each option is a radio button. Its name is the label and the current badge, such as "Yearly Save 20%". The badge text
  shown while the option is selected is the active badge, when one is set.
- The thumb is hidden from assistive technology. A `BillingPrice` reads as its amount, its old price and its period, such
  as "$24, was $30 per month".
- Tab focuses the selected option. Arrow keys select and focus the next or previous option and wrap around. Home and End
  select the first and last option.
- Keyboard focus draws a 2 px outline offset by 2 px. Pointer focus does not.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move focus into the group on the selected option, and out of it. Only the selected option is a tab stop. |
| `Right` / `Down` | Select and focus the next option; from the last option it wraps to the first. |
| `Left` / `Up` | Select and focus the previous option; from the first option it wraps to the last. |
| `Home` / `End` | Select and focus the first or the last option. |

## Motion

- The thumb slides under the selected option on a critically damped spring of about 0.34 seconds, with no bounce.
- The badge of the selected option takes the accent color, and its text swaps with a blur transition.
- Prices roll to the new amount. The old price collapses away, and the period text swaps in place.
- Reduced motion places the thumb at once and removes the press scale on unselected options. Off does the same.

| Change | Full | Reduced | Off |
|---|---|---|---|
| The selected option changes | The thumb glides on a critically damped spring, about 0.34 s, with no bounce | The thumb moves at once | The thumb moves at once |
| An unselected option is pressed | The option scales to 97% while pressed | No press scale | No press scale |
| The option text changes color on hover or selection | Color eases over the fast tween | Immediate | Immediate |
| The selected badge takes its accent | Background eases over the standard tween; the badge text swaps with a blur | Immediate | Immediate |

## Related

- `SegmentedControl` for views that switch content without a price.
- `RadioGroup` for options that need descriptions or a vertical layout.
- `RadioCards` for plans that need a price, a description and a reason when unavailable.
- `Switch` for a single setting that takes effect at once.

| Component | Choose it when |
|---|---|
| `SegmentedControl` | The options switch a view, a filter or a layout and carry no price or saving |
| `RadioGroup` | The options need a description or a vertical layout |
| `RadioCards` | Each option is a plan with its own price, features and an unavailable reason |
| `Switch` | A single setting takes effect at once |
| `Select` / `Combobox` | The list is long, or the options are not a short fixed set |
| `Checkbox` | The person chooses several independent items, not one period |
