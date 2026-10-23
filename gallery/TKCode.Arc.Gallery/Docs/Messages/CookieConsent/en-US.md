---
title: Cookie consent
summary: A consent bar that morphs into preferences and folds into a reopen button.
---

<Demo name="ConsentBar" />

## When to use

`CookieConsent` asks a visitor for a choice about cookies and tracking, and keeps that choice. It sits over the page as
a layer at the bottom edge, so the page stays readable behind it. The bar offers Accept all, Reject all and
Customize. Customize opens a panel of categories with switches, and a saved choice folds into a small reopen button, so
the visitor can change their mind later from any page.

- **A marketing site that loads analytics and advertising scripts**: the bar appears before any optional script runs.
  Accept all and Reject all have the same weight, so refusing is as easy as accepting.
- **A store or a SaaS web app with optional categories**: Preferences, Analytics and Marketing are separate switches
  that start off, while Strictly necessary (sign-in, security, the cart) is always on.
- **Changing the choice later**: after a choice the reopen button stays in the corner, so a visitor can open the
  preferences from any page without searching for a footer link.
- **A policy update that needs a new answer**: change `Version` when the categories or the policy change, and the saved
  choice of the previous version is ignored, so the visitor is asked again.

## When not to use

- **A site-wide notice that asks for no choice**, such as a sale or a maintenance window: use `AnnouncementBar`.
- **A short confirmation that closes by itself**: use `Toast`.
- **A decision that must block the page until it is answered**, such as an age check: use `Dialog`.
- **A plain message inside one section of the page**: use `Alert`.

## Usage

Place the control over the page, for example as the last child of the window's root `Grid`. The control is a layer:
it does not take pointer input except on its own surface. With no categories set, the four Arc defaults are used:
Strictly necessary, Preferences, Analytics and Marketing.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <Grid>
        <!-- Page content goes here. -->
        <arc:CookieConsent StorageKey="site-consent" Version="1" />
    </Grid>
</UserControl>
```

`CookieConsent` derives from Avalonia's `TemplatedControl`. Its bar, panel and reopen button are three faces of one
surface that the control moves between, so you configure one control and never place the faces yourself. Because the
control draws over the content around it, it must come after that content in the parent panel.

### Anatomy

1. **Layer**: keeps 16 px from the left, right and bottom edges of the control's parent. Only the surface takes pointer
   input, so the page under the layer stays clickable.
2. **Surface**: a raised surface with a 1 px border ring and the floating shadow. The bar has a 22 px corner radius and
   the panel a 26 px one.
3. **Bar** (`IsPreferencesOpen` is `False` and no choice is saved): a message in the secondary text color with an
   inline `Policy` link at its end. The bar is at most 660 px wide with 18 px before the message and 10 px after it.
   The three buttons are Customize (ghost), Reject all and Accept all (secondary), all small and 6 px apart. Below 560 px
   the message sits above the buttons.
4. **Panel** (`IsPreferencesOpen` is `True`): 420 px wide with 18 px padding. It holds the title, a 32 px close
   button, the description, the category list and the actions. The category list scrolls after 300 px. Each row has the
   category label and its description, and a switch, or the text `Always on` for a required category. The actions are
   Reject all and Accept all side by side, and Save choices across the full width below a subtle divider.
5. **Reopen button**: a 44 px round ghost button with the cookie icon. It appears after a choice when
   `IsReopenButtonVisible` is `True`, in the corner of the bar's side (`ReopenSide`, or the left side when the bar is
   centered).

### Categories and policy

Set `Categories` to define the categories. `IsRequired` keeps a category always on, and `IsEnabledByDefault` sets the
starting state of an optional category before a choice is saved. Optional categories start off unless you set this.
`Policy` adds a link to the full policy at the end of the bar message and the panel description.

The four default categories are:

| Id | Label | Starts as |
|---|---|---|
| `necessary` | Strictly necessary | On, always (required) |
| `preferences` | Preferences | Off |
| `analytics` | Analytics | Off |
| `marketing` | Marketing | Off |

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
visitor is asked again. The version is a string, so `"2"` and `"2.0"` are different versions.

### Reacting to the choice

`ConsentChanged` is raised when the visitor saves, accepts, rejects or resets the choice. `Consent` maps each category
id to `True` or `False`, and is `null` while the visitor has not decided. Start optional scripts only from this event,
or from a stored choice read at startup, never before the visitor has answered.

```csharp
public static class Analytics
{
    public static bool Enabled { get; set; }
}

consent.ConsentChanged += (_, e) =>
{
    if (e.Consent is not null && e.Consent.TryGetValue("analytics", out var allowed))
    {
        Analytics.Enabled = allowed;
    }
};
```

`Action` tells you how the choice was made: `AcceptAll`, `RejectAll`, `Save` (the panel) or `Reset`. After `Reset`
the `Consent` value is `null`, so check it to know that the visitor is being asked again.

### Reading a saved choice

Read the saved choice at startup, before the control is shown, to skip the bar for a visitor who already decided.

```csharp
var record = CookieConsent.Read("site-consent", "2");
if (record is not null)
{
    Analytics.Enabled = record.Value.TryGetValue("analytics", out var allowed) && allowed;
}
```

`Read` returns `null` when nothing is stored, when the storage is unavailable, and when the stored choice was saved for
another version. A record exposes its `Value` dictionary, its `Version` and its `UpdatedAt` time. The control reads the
same storage when it is first attached, so a visitor who decided does not see the bar flash before the chip appears.

### Binding to a view model

The control has a value (`Consent`), a list of categories (`ItemsSource`) and an open state (`IsPreferencesOpen`).
With CommunityToolkit.Mvvm the view model holds them, and the bindings keep them in step with the visitor's choices.

```csharp
public partial class ConsentViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<CookieCategory> _categories = new()
    {
        new CookieCategory { Id = "necessary", Label = "Strictly necessary", IsRequired = true, IsEnabledByDefault = true },
        new CookieCategory { Id = "analytics", Label = "Analytics", Description = "Counts visits in aggregate." }
    };

    [ObservableProperty]
    private IReadOnlyDictionary<string, bool>? _consent;

    [ObservableProperty]
    private bool _isPreferencesOpen;
}
```

```xml
<arc:CookieConsent StorageKey="site-consent"
                   Version="2"
                   ItemsSource="{Binding Categories}"
                   Consent="{Binding Consent, Mode=TwoWay}"
                   IsPreferencesOpen="{Binding IsPreferencesOpen, Mode=TwoWay}" />
```

`Consent` and `IsPreferencesOpen` are two-way. The control writes them when the visitor decides or opens and closes the
panel, so the view model can read the choice without a handler. Setting `Consent` from code does not store anything:
only a visitor's decision, or `Reset`, writes the storage when `StorageKey` is set. To save a choice that you computed
yourself, use `Read` for the stored value or your own store.

### Position and reopen button

`Position` sets the bar to the bottom, bottom left or bottom right. `IsReopenButtonVisible` keeps the 44px button
after a choice is saved. `OpenPreferences` opens the panel from another control, such as a footer link.

```xml
<arc:CookieConsent Position="BottomRight" IsReopenButtonVisible="True" />
```

```csharp
settingsLink.Click += (_, _) => consent.OpenPreferences();
```

Put the position where the page's own controls are not: a bottom right bar can cover a chat button or a back-to-top
button. Keep `IsReopenButtonVisible` at `True` unless the visitor can change the choice in another place, such as an
account settings page. Without the reopen button, a saved choice hides the control completely.

### Example: consent on a marketing site

A visitor arrives at Northwind Outdoors, a store that sells trail packs, and has not answered yet. The bar sits over the
product page with the message, the policy link and the three choices at equal reading weight. Accept all and Reject all
are side by side and the same size, which is the consent pattern the visitor can trust. Customize stays a quiet ghost
button, so it is available without competing with the two answers.

![A cookie consent bar over a store page with equal Accept all and Reject all buttons](images/StoreConsent.png)

```xml
<Border Width="720"
        Height="400"
        CornerRadius="12"
        ClipToBounds="True"
        Background="{arc:ArcDesignTokenResource Background}"
        BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}">
    <Grid>
        <StackPanel Margin="32,28" Spacing="10">
            <TextBlock Text="Northwind Outdoors" FontSize="24" FontWeight="Medium"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Trail packs for weekend hikes, shipped within two days."
                       FontSize="14" TextWrapping="Wrap"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            <TextBlock Text="Free returns within 30 days." FontSize="14"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        </StackPanel>
        <arc:CookieConsent StorageKey="northwind-consent">
            <arc:CookieConsent.Storage>
                <arc:InMemoryMessageStorage />
            </arc:CookieConsent.Storage>
            <arc:CookieConsent.Policy>
                <arc:CookieConsentPolicy Label="Cookie policy" />
            </arc:CookieConsent.Policy>
        </arc:CookieConsent>
    </Grid>
</Border>
```

### Content guidelines

- **Message**: say what the cookies are for and what the visitor gets. The default message names the essential
  cookies and the optional ones, so keep the same structure when you replace it.
- **Button labels**: keep the defaults (Accept all, Reject all, Customize, Save choices) so the visitor sees the same
  words on every site. They are sentence case and say what the button does.
- **Category descriptions**: one sentence on the purpose, in plain words: "Count visits and see which pages help, only
  in aggregate."
- **Localization**: copy comes from `Labels` (`CookieConsentLabels`). Each control has its own labels, so set them in
  the language of the page instead of editing the message in code.

| Instead of | Write |
|---|---|
| Accept | Accept all |
| We use cookies to improve your experience!!! | We use essential cookies to run this site. With your OK, we'd also use optional ones. |
| Essential | Strictly necessary |
| Cookie setting | Customize |

### Best practices

**Do**

- Give Accept all and Reject all the same weight, size and position. A consent that makes refusing harder reads as a
  dark pattern, and visitors who notice it stop trusting the rest of the site.
- Keep optional categories off until the visitor chooses, and start optional scripts only in `ConsentChanged` or from a
  stored choice read at startup.
- Change `Version` whenever a category or the policy changes, so the visitor is asked again.
- Keep the reopen button visible, so the visitor can change the choice at any time.

**Avoid**

- Pre-ticking optional categories with `IsEnabledByDefault` set to `True`. The visitor did not choose them.
- Setting `Consent` from code to record a choice. It is not stored; decide through the control or store the value yourself.
- Covering content that the visitor needs, such as the checkout button, with the bar. Test the layer on small windows.
- Using the bar for a question that is not about cookies or tracking. A consent dialog that asks for something else
  teaches visitors to dismiss it.

### Common questions

**Why does the bar not appear for a visitor who already answered?** A stored choice for the current `Version` folds the
control into the reopen button, so the bar is hidden by design. Use `Reset` to forget the choice and ask again. Check
whether `Read` returns a record with the same `StorageKey` and version if you expected the bar to show.

**Does Escape save the choice?** No. Escape closes the preferences panel without saving, and the bar stays as it was.
The draft of the switches is discarded, so reopening the panel shows the saved values.

**How do I open the preferences from a footer link?** Call `OpenPreferences()`, as in the usage example, or bind
`IsPreferencesOpen` two-way. `ClosePreferences()` closes the panel without saving. `Reset()` forgets the saved choice,
clears the storage and shows the bar again.

**Where is the choice stored?** In `Storage` when you set one on the control, or in `MessageStorage.Default`, which is
a file in the local application data folder. The stored text is JSON with the value, the version and the time, such as
`{"value":{"analytics":true},"version":"2","updatedAt":"2026-10-21T09:30:00.0000000+00:00"}`. Use an in-memory storage in
previews and tests.

**Can I use the control outside a web site?** It is a generic consent layer. In a desktop app the wording and the
categories are your decision: `Labels` changes the bar and panel copy, and your own categories name what is asked, so
the bar can ask about telemetry or data sharing instead of cookies.

## API reference

<!-- api: CookieConsent -->

<!-- api: CookieCategory -->

Companion types used by a `CookieConsent`:

| Type | Members |
|---|---|
| `CookieConsentPolicy` | `Label` (link text, default `Cookie policy`), `Href` (`Uri`, opened with the system launcher), `Command` (`ICommand`, run on invoke) |
| `CookieConsentLabels` | `Message`, `Customize`, `AcceptAll`, `RejectAll`, `Save`, `PreferencesTitle`, `PreferencesDescription`, `Reopen`, `AlwaysOn`, `Close` |
| `CookieConsentAction` | `AcceptAll`, `RejectAll`, `Save`, `Reset`: how the choice was made |
| `CookieConsentPosition` | `Bottom` (default), `BottomLeft`, `BottomRight` |
| `CookieConsentRecord` | `Value` (category id to allowed), `Version`, `UpdatedAt`: returned by `CookieConsent.Read` |

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
- The reopen button is named `Cookie preferences` (the `Reopen` label), so it is found by name after a choice.
- The Save button's help text counts the choices, for example "2 of 3 optional categories allowed", so a screen reader
  user hears the effect before saving.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Moves between the buttons and switches of the bar or the panel |
| `Space` | Toggles the focused category switch |
| `Enter` | Presses the focused button: Customize, Reject all, Accept all, Save choices, Close or the reopen button |
| `Escape` | In the panel, closes it without saving and returns focus to the bar |

## Motion

- The bar, the panel and the reopen button are one surface that changes its position, width, height and corner.
- Each face rises a few pixels out of a short blur. Leaving faces fade out and drop slightly.
- The bar enters from the bottom edge with a small scale.
- Reduced motion jumps the surface between its shapes and only fades the faces in. Off motion shows every change
  immediately.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Bar enters | Rises from the bottom edge with a small scale and fades in | Fades in | Immediate |
| Surface changes shape (bar, panel, reopen button) | Moves, resizes and changes its corner. Growing uses a small overshoot, shrinking settles without one | Jumps to the new shape | Immediate |
| Face changes | The new face rises about 10 px out of a short blur; the leaving face fades out and drops slightly | The new face fades in; the leaving face fades out quickly | Immediate |

## Related

- `AnnouncementBar` for a site-wide notice that does not ask for a choice.
- `Dialog` for a decision that blocks the page until the user answers.
- `Toast` for a short confirmation that closes by itself.

| Component | Choose it when |
|---|---|
| `AnnouncementBar` | The notice is for every visitor and asks for no choice |
| `Dialog` | The person must answer before the page continues |
| `Toast` | A short confirmation should close by itself |
| `Alert` | A plain message belongs to one section of the page |
| `Switch` | A single preference is set inside a form in your own page |
