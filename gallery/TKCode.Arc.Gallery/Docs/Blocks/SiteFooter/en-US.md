---
title: Site footer
summary: A website footer with link columns and newsletter, a minimal layout, and a large fading Arc mark.
---

<Demo name="Preview" />

## When to use

`SiteFooter` closes a marketing or documentation site. It carries the links people look for at the bottom of a page
(product, resources, company), the legal lines, a system status and an email signup, in one of three layouts.

- **A product site with a newsletter**: the Columns layout puts the brand, a tagline and a release-notes signup beside
  three link columns.
- **A docs site or an app's help pages**: the Minimal layout is one quiet row of links and one line of copyright, status
  and social links.
- **A landing page that ends with the brand**: the Logo layout drops the newsletter and ends in a large arch that fades
  into the page and rises in when it scrolls into view.

## When not to use

- **A dense app screen**: an in-app status bar or a sidebar footer is smaller; this block is a page-level footer.
- **A signup that is the point of the page**: use the Newsletter signup block, which gives the form the whole section.
- **A closing call to action**: use the CTA section block above the footer and keep the footer for navigation.

## Usage

With nothing set the footer shows Arc's sample: the Arc brand, three columns, legal and social links and an "All systems
normal" status.

```xml
<arc:SiteFooter />
```

Everything is data: name the brand, give your own columns and links, and handle what is pressed.

<Demo name="Custom" />

```xml
<arc:SiteFooter BrandName="Northwind"
                BrandHref="https://northwind.example"
                Tagline="Freight tracking for small fleets."
                IsNewsletterVisible="False"
                StatusLabel="Delays in EU routing"
                StatusTone="Warning">
    <arc:SiteFooterColumn Title="Platform">
        <arc:SiteFooterLink Label="Tracking" Href="https://northwind.example/tracking" />
        <arc:SiteFooterLink Label="API" Href="https://developers.northwind.example" IsExternal="True" />
    </arc:SiteFooterColumn>
</arc:SiteFooter>
```

### Layouts

`Variant` picks `Columns`, `Minimal` or `Logo`. Columns and Logo share the intro, the three link columns and the legal row;
only Columns shows the newsletter, and only Logo shows the arch. Minimal shows the brand with one row of links, then
the copyright and tagline with the status and social links. Its links are `Links`; when that is null it uses the first
link of each column and the first two legal links.

### Links

A `SiteFooterLink` has a `Label`, an optional `Href` and `IsExternal` (an outward arrow). Pressing a link, the brand or
the status raises `Navigate` with the link and where it sits (`Brand`, `Column`, `Legal`, `Social`, `Status`) and runs
`NavigateCommand`. When the event is not marked handled and `Href` is an absolute http, https or mailto address, the
footer opens it with the platform launcher. A link without `Href` only raises the event.

`Columns` holds the link columns (or bind `ColumnsSource`, which wins and is observed). `LegalLinks` and `SocialLinks`
replace the sample links beside the copyright and at the end of the legal row.

### Newsletter

<Demo name="Subscribe" />

The signup validates in place. An empty field says "Enter your email address" and a malformed one "That email doesn't
look right". Validation starts when the field is left with text in it, and then runs on every keystroke. Submitting
loads (the button spins and the field is read only), awaits `SubscribeAsync`, and ends in the done state: the button
reads "Subscribed" with a check mark and the line under it says "Check {address} to confirm". Throwing from
`SubscribeAsync` returns to idle with the failure message. `Subscribed` is raised on success.

```csharp
footer.SubscribeAsync = async (email, cancellationToken) =>
{
    await Task.Delay(500, cancellationToken); // call your list here; throw to show the failure message
};
footer.Subscribed += (_, e) => Console.WriteLine($"Subscribed {e.Email}");
```

Every text of the form is a property (`NewsletterTitle`, `NewsletterDescription`, `NewsletterPlaceholder`,
`NewsletterSubscribeLabel`, `NewsletterSubscribedLabel`, `NewsletterEmptyEmailMessage`, `NewsletterInvalidEmailMessage`,
`NewsletterFailedMessage`, `NewsletterConfirmFormat`) with Arc's English as the default.

### Status

`StatusLabel`, `StatusTone` (`Success`, `Warning`, `Danger`) and `StatusHref` describe the status link; hide it with
`IsStatusVisible="False"`.

### Responsive behavior

<Demo name="Narrow" />

The breakpoints follow the footer's own width, not the window's. At 760 or less the intro sits above the link columns
with a 40 px gap. At 520 or less the columns take two across with 32 / 16 gaps, the newsletter loses its 400 px limit
and the legal row becomes a column, left aligned.

## API reference

<!-- api: SiteFooter -->

<!-- api: SiteFooterLink -->

<!-- api: SiteFooterColumn -->

<!-- api: SiteFooterNavigateEventArgs -->

<!-- api: SiteFooterSubscribedEventArgs -->

## Accessibility

- The footer is a content-info landmark. The link columns are a navigation named by `NavigationLabel`; the legal and
  social links are lists named by `LegalLabel` and `SocialLabel`.
- Links with an address are exposed as links; those without are buttons.
- The email field is named by `NewsletterEmailLabel`. The hint, the confirmation and the failure are announced
  politely, and an error is announced as an alert.
- The mark, the arrows and the status dot are decorative.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move through the brand, the newsletter, the links and the status |
| `Enter` | In the email field: subscribe. On a link or the button: activate it |
| `Space` | Activate the focused link or button |

Keyboard focus draws a 2 px ring around a link; a pointer press focuses without a ring.

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Link and status color | 160 ms | Immediate | Immediate |
| Outward arrow on hover | Moves 1 px right and up in 160 ms | Still | Still |
| Input border (focus, invalid) | 160 ms | Immediate | Immediate |
| Newsletter line | Rises from 4 px below through a blur in 240 ms; the old line lifts away in 120 ms | Fades only | Immediate |
| Subscribe button | The label cross-fades with a blur, the width springs and a spinner shows while loading | The button's reduced rules | Immediate |
| Logo arch | Once, when 30% is on screen: fades in and rises 40 px over 900 ms | Shown at once | Shown at once |

## Related

- The Newsletter signup block for a full-width signup section.
- The CTA section block for a closing call to action above the footer.
- The Site header block for the matching top bar.
