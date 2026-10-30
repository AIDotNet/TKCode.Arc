---
title: Domain verify
summary: A custom domain setup card where customers add a domain and copy the DNS records, then a live checker marks each record valid, not found or misconfigured with a one-line fix and finishes with the domain verified and its SSL certificate issued.
---

<Demo name="Preview" />

## When to use

`DomainVerify` is the "Custom domain" step of a product's settings. A customer types a domain, gets the DNS records to
create with a copy button on each, and then watches a live checker work through them:

- **Letting customers serve an app, docs site, status page or storefront from their own domain.**
- **Verifying a sending domain for email** (SPF, DKIM, DMARC) before turning on delivery.
- **Any setup step where a person must copy values into a third party and wait for propagation.**

## When not to use

- **Buying or searching domains.** Use a search field with results instead.
- **Managing many domains at once.** Use a table with one row per domain and open this card for one of them.
- **A one-off copy of a single value.** A copy button next to the value is enough.

## Usage

The card derives from Avalonia's `TemplatedControl`. It needs no data: with no action set it plays a simulated flow
(add the domain, check it, find a conflicting record, fix it, verified) once it is on screen, and any click or key press
inside it hands control to the person. The status line announces "Simulated, no DNS was queried" while that is so.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:DomainVerify Title="Custom domain" DefaultDomain="" />
</UserControl>
```

### The flow

`Phase` tells where the card is, and `PhaseChanged` reports every change:

1. **Enter**: the domain field and "Add domain". Behind them, dimmed placeholder rows and the line "DNS records appear
   here after you add a domain". Protocols, paths, ports and trailing dots are stripped from what was typed before it is
   checked.
2. **Adding**: the button shows a spinner while `GetRecordsAction` runs.
3. **Records**: the domain row replaces the field, the records are listed, and the footer offers "Check records".
4. **Checking**: the resolvers are polled every `PollInterval`. Rows that are not valid yet show a spinner; the footer
   shimmers and counts the regions that propagated.
5. **Invalid**: a record holds a conflicting value. The polling stops, the row says "Misconfigured" and a hint opens
   under it with what was found and the fix. "Check again" starts a new run.
6. **Verified**: every record is valid. The domain row says "Issuing SSL certificate" until `IssueCertificateAction`
   returns, then "SSL by Let's Encrypt · renews in 60 days". `Verified` is raised, and "Add another" starts over.

### Connecting your server

Three async delegates stand in for your backend. Each receives a cancellation token that removing the domain or the card
leaving the window cancels.

```csharp
card.GetRecordsAction = async (domain, cancellation) =>
    await api.AddDomainAsync(projectId, domain, cancellation);          // IReadOnlyList<DomainRecord>

card.VerifyAction = async (domain, records, context, cancellation) =>
    await api.CheckDomainAsync(projectId, domain, cancellation);        // DomainVerifyResult

card.IssueCertificateAction = async (domain, cancellation) =>
    await api.IssueCertificateAsync(projectId, domain, cancellation);   // DomainCertificate

card.Verified += (_, e) => Console.WriteLine($"{e.Domain} is live");
```

`VerifyAction` is one check, not a loop. Return `DomainRecordStatus.Missing` while a record is still propagating, so the
card keeps polling, and `DomainRecordStatus.Invalid` only for a conflicting value, with `Found` and a one-sentence `Fix`.
The card stops on "all valid" or on any invalid record. `PollInterval` is 800 ms while the check is simulated and
5 seconds with a `VerifyAction`. An exception from any delegate is shown in the footer, raises `Failed` and leaves the
card ready for another try.

<Demo name="Handlers" />

### Narrow containers

The card fills its container up to 680 wide. Below 544 the column header leaves and each record stacks: the type, name
and status on the first line, the value with its copy button below, then the purpose. Long names and values truncate with
an ellipsis and keep their full text in a tooltip; the copy button always copies the whole value.

<Demo name="Narrow" />

### Sending domains

For email, return MX, SPF (TXT), DKIM (CNAME or TXT) and DMARC records from `GetRecordsAction`, set `Title` to
"Sending domain" and reword the card with `Labels`: the last step then reads "Turning on delivery" instead of issuing a
certificate. Pass `Provider="{x:Null}"` to hide the "DNS hosted at" line when you do not know the host.

<Demo name="SendingDomain" />

### Texts

Every word the card shows or announces is a property of `DomainVerifyLabels`; assign a new instance to `Labels` to
translate or reword. Formatted texts, such as the footer's "Checking records · 2 of 6 regions propagated", are functions
so that word order can change.

## API reference

<!-- api: DomainVerify -->

<!-- api: DomainRecord -->

<!-- api: DomainRecordCheck -->

<!-- api: DomainVerifyResult -->

<!-- api: DomainCertificate -->

<!-- api: DomainProvider -->

## Accessibility

- The card is named by its title, a level 2 heading. The records are a list named "DNS records"; each row reads its type,
  name and status ("CNAME app, Valid").
- Every status is an icon plus a short word, never a color alone. The conflicting record's hint is part of its row.
- A polite live region announces each step: the domain was added, checking, a record needs a fix, the domain is verified,
  the certificate was issued, a failure. While no `VerifyAction` is set it adds "Simulated, no DNS was queried".
- The domain field is labelled "Domain"; a bad domain shows an error under it. Copy buttons are named "Copy TXT value",
  "Copy CNAME value" and so on. The dimmed placeholder rows are inert and hidden from screen readers.

### Keyboard

- **Enter** in the domain field adds the domain, as "Add domain" does.
- **Tab** moves through the field and button, then the close button, each record's copy button and the footer action.
- **Enter** or **Space** activates a focused copy, close or footer button.
- After a domain is added the focus moves to the footer action; after it is removed it returns to the domain field.

## Motion

- **Status**: the status words and icons crossfade (100 ms out, 160 ms in, rising 2 px); a checking row shows a spinner and
  the footer a shimmer.
- **Rows**: the records fade in over 160 ms, each 40 ms after the one before. The fix hint opens on the smooth spring.
- Reduced motion keeps every state, drops the stagger and only fades briefly. Off applies every change at once. A simulated flow only plays while the card is on screen.

## Related

- `CopyButton`, `Button`, `Input` and `TextShimmer`, the controls the card is made of.
- API keys, Webhooks, Integrations, Env variables and Webhook tester, the other Developer blocks.
