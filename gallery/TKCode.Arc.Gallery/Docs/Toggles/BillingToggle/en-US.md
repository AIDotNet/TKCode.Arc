---
title: Billing toggle
summary: A monthly and yearly switch with a savings badge and prices that roll to the new amount.
---

## When to use

- The billing period switch at the top of a pricing page.
- Prices that change with the selected period or plan and should roll to the new amount.
- A small plan choice with a savings note, such as yearly or lifetime.

## When not to use

- Views that do not change a price: use `SegmentedControl`.
- Options that need a description or a vertical layout: use `RadioGroup`.
- A single on or off setting: use `Switch`.

## Usage

The default options are Monthly and Yearly, and Yearly carries the badge "Save 20%". Set `Value` to the selected option.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:BillingToggle Value="yearly" />
</UserControl>
```

### Custom options

Write the options inline or bind `ItemsSource`. `Badge` is shown on the option. `ActiveBadge` replaces it while the
option is selected, so a note can change in place without moving the content around it.

```xml
<arc:BillingToggle Value="monthly" Size="Large">
    <arc:BillingToggleOption Value="monthly" Label="Monthly" />
    <arc:BillingToggleOption Value="yearly" Label="Yearly" Badge="Save 20%" ActiveBadge="You save $48" />
    <arc:BillingToggleOption Value="lifetime" Label="Lifetime" Badge="Best value" />
</arc:BillingToggle>
```

### Prices

`BillingPrice` shows an amount. `Currency` defaults to `$` and `Decimals` to 0. `WasAmount` draws the old price
struck through when it is higher than `Amount`. `Period` is the text after the amount.

```xml
<arc:BillingPrice Amount="24" WasAmount="30" Period="per month, billed yearly" />
```

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

### Size and accent

`Size` sets the option height: `Medium` is 34 and `Large` is 42. `BadgeAccent` colors the selected badge. It defaults to the
Arc success color.

```xml
<arc:BillingToggle Size="Large" Value="yearly" />
```

### Binding

`Value` is two-way and stays in sync with `SelectedValue`.

```xml
<arc:BillingToggle Value="{Binding BillingPeriod}" />
```

## API reference

<!-- api: BillingToggle -->
<!-- api: BillingPrice -->
<!-- api: BillingToggleOption -->

## Accessibility

- The group is named "Billing period" by default. Set `AutomationProperties.Name` to name it differently.
- Each option is a radio button. Its name is the label and the current badge, such as "Yearly Save 20%".
- The thumb is hidden from assistive technology. A `BillingPrice` reads as its amount, its old price and its period, such as "$24, was $30 per month".
- Tab focuses the selected option. Arrow keys select and focus the next or previous option and wrap around. Home and End select the first and last option.
- Keyboard focus draws a 2px outline offset by 2px. Pointer focus does not.

## Motion

- The thumb slides under the selected option on a critically damped spring of about 0.34 seconds, with no bounce.
- The badge of the selected option takes the accent color, and its text swaps with a blur transition.
- Prices roll to the new amount. The old price collapses away, and the period text swaps in place.
- Reduced motion places the thumb at once and removes the press scale on unselected options. Off does the same.

## Related

- `SegmentedControl` for views that switch content without a price.
- `RadioGroup` for options that need descriptions or a vertical layout.
- `RadioCards` for plans that need a price, a description and a reason when unavailable.
- `Switch` for a single setting that takes effect at once.
