---
title: Cookie consent
summary: A consent bar that morphs into preferences and folds into a reopen button.
---

## When to use

- A marketing site or app that asks visitors to accept or refuse cookies and tracking.
- Consent by category, where optional categories start off and the visitor can change the choice later.
- A reopen button that lets the visitor change the choice after it is saved.

## When not to use

- A site-wide notice that needs no choice: use `AnnouncementBar`.
- A message that disappears on its own without a choice: use `Toast`.
- A decision that blocks the page until the user answers: use `Dialog`.
- A plain message inside a section of the page: use `Alert`.

## Usage

Place the control over the page, for example as the last child of the window's root `Grid`. The control is a layer:
it does not take pointer input except on its own surface. With no categories set, the four Arc defaults are used:
Strictly necessary, Preferences, Analytics and Marketing.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <Grid>
        <!-- Page content goes here. -->
        <arc:CookieConsent StorageKey="site-consent" Version="1" />
    </Grid>
</UserControl>
```

### Categories and policy

Set `Categories` to define the categories. `IsRequired` keeps a category always on, and `IsEnabledByDefault` sets
the starting state of an optional category. `Policy` adds a link to the full policy in the bar and the panel.

```xml
<arc:CookieConsent StorageKey="site-consent" Version="2">
    <arc:CookieConsent.Categories>
        <arc:CookieCategory Id="necessary" Label="Strictly necessary"
                            Description="Needed for the site to work."
                            IsRequired="True" IsEnabledByDefault="True" />
        <arc:CookieCategory Id="analytics" Label="Analytics"
                            Description="Helps us see which pages are useful." />
    </arc:CookieConsent.Categories>
    <arc:CookieConsent.Policy>
        <arc:CookieConsentPolicy Label="Cookie policy" Href="https://example.com/cookies" />
    </arc:CookieConsent.Policy>
</arc:CookieConsent>
```

Change `Version` when the categories or the policy change. A saved choice with another version is ignored and the
visitor is asked again.

### Reacting to the choice

`ConsentChanged` is raised when the visitor saves, accepts, rejects or resets the choice. `Consent` maps each
category id to `True` or `False`, and is `null` while the visitor has not decided.

```csharp
consent.ConsentChanged += (_, e) =>
{
    if (e.Consent is not null && e.Consent.TryGetValue("analytics", out var allowed))
    {
        Analytics.Enabled = allowed;
    }
};
```

### Reading a saved choice

Read the saved choice at startup, before the control is shown, to skip the bar for a visitor who already decided.

```csharp
var record = CookieConsent.Read("site-consent", "2");
if (record is not null)
{
    Analytics.Enabled = record.Value.TryGetValue("analytics", out var allowed) && allowed;
}
```

### Position and reopen button

`Position` sets the bar to the bottom, bottom left or bottom right. `IsReopenButtonVisible` keeps the 44px button
after a choice is saved. `OpenPreferences` opens the panel from another control, such as a footer link.

```xml
<arc:CookieConsent Position="BottomRight" IsReopenButtonVisible="True" />
```

```csharp
settingsLink.Click += (_, _) => consent.OpenPreferences();
```

## API reference

<!-- api: CookieConsent -->

<!-- api: CookieCategory -->

## Accessibility

- The bar and the panel are non-modal dialogs. Each is named by its title, and its message or description is the
  help text.
- Opening the panel moves focus to its title. Saving the panel moves focus to the reopen button.
- Each category switch is named by the category label, with its description as help text. Required categories show
  `Always on` text instead of a switch.
- Tab moves between the buttons and switches. Space toggles the focused switch, and clicking the category name also
  toggles it.
- Escape closes the panel without saving and returns focus to the bar.
- Reject all and Accept all have the same weight and sit side by side.

## Motion

- The bar, the panel and the reopen button are one surface that changes its position, width, height and corner.
- Each face rises a few pixels out of a short blur. Leaving faces fade out and drop slightly.
- The bar enters from the bottom edge with a small scale.
- Reduced motion jumps the surface between its shapes and only fades the faces in. Off motion shows every change
  immediately.

## Related

- `AnnouncementBar` for a site-wide notice that does not ask for a choice.
- `Dialog` for a decision that blocks the page until the user answers.
- `Toast` for a short confirmation that closes by itself.
