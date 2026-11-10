---
title: DNS resolution
summary: A browser asks its resolver for a name, the resolver walks from a root server to the domain's own name server, caches the answer and replies from the cache next time.
---

<Demo name="Lookup" />

## When to use

`DnsResolution` explains what happens between typing a name and reaching a server. The browser asks its recursive
resolver for the address of a name. The resolver's cache is empty, so it starts at the top: a root server does not know
the address but refers it to the servers of the top-level domain (".dev"), those refer it to the domain's own name
servers, and the authoritative server answers with the address and how long it may be kept. The resolver stores the
answer, hands it to the browser, and when the same question comes again it answers from its cache without asking
anyone. Packets travel between the parts, a log names each message, and two readouts count the round trips and the
time taken. The timings and the address are illustrative.

- **Documentation and courses**: a networking lesson, or the "how domains work" page of a hosting product.
- **Product pages**: a visual next to text about a resolver, a CDN or a registrar.
- **Troubleshooting guides**: why a changed record can take time to appear (the answer is cached for its time to live).

## When not to use

- **Real lookups or monitoring**: the card does not resolve anything. Show real query logs in a `Table`.
- **Record types beyond an address**: it tells the story of one address lookup, not of mail, text or alias records.
- **Many cards on a page**: each animates while it plays. Keep one per page, or pause the others with `IsPlaying`.

## Usage

The card needs no data: it looks up an invented name, `docs.lumen-garden.dev`, and answers with `203.0.113.42`, an
address set aside for documentation. It starts the first time it is mostly on screen.

```xml
<arc:DnsResolution />
```

`DnsResolution` derives from `Explainer`, the base of the animated explainers in the Special package. The base supplies
the story clock (`IsPlaying`, `Speed`, `IsLooping`, `Play()`, `Pause()`, `StepForward()`, `Restart()`), start on view,
the play, step and restart buttons and the polite live region; this control adds the speed control and the diagram.

### The story

| Chapter | What happens |
|---|---|
| `Query` | The browser asks; the resolver's cache has nothing for the name |
| `Root` | A root server refers the resolver to the top-level domain's servers |
| `Tld` | The top-level domain's server refers it to the domain's name server (`ns1.` plus the domain's last two labels) |
| `Authoritative` | The authoritative server answers with the address and its time to live |
| `Answer` | The resolver caches the answer and hands it to the browser |
| `Cached` | The same question again, answered from the cache in one round trip |

Each chapter ends on a stop, so "Next step" moves one chapter at a time. `Step` reports the chapter and `StepChanged`
is raised as each one starts.

### Your own name

`Domain`, `Address` and `TimeToLive` change what the story says; `IsRepeatLookupShown="False"` ends after the first
lookup. Use a name with at least two labels.

<Demo name="OwnDomain" />

```xml
<arc:DnsResolution Domain="status.paper-kite.io" Address="198.51.100.7" TimeToLive="60" IsRepeatLookupShown="False" />
```

### Following the chapters

<Demo name="StepLog" />

```csharp
card.StepChanged += (_, e) => log.Add(e.Step.ToString());
```

`Labels` holds every word the card shows or speaks; replace it to translate or reword. The playback buttons take their
names from `PlaybackLabels`.

### Reduced motion

Under reduced motion the card opens on its end. Play and Next step still work, and each chapter applies in place with
no packets travelling.

<Demo name="ReducedMotion" />

## API reference

<!-- api: DnsResolution -->

<!-- api: DnsResolutionLabels -->

<!-- api: DnsResolutionStepChangedEventArgs -->

## Accessibility

- The card is a group named by `Title` ("DNS resolution"). The status line says Ready, Resolving, Paused or Resolved
  with an icon and words.
- The diagram's accessible name follows the story: the last message ("Root server to Resolver: Ask the .dev servers")
  and both readouts.
- The live region speaks only when the reader drives the story (a step, a pause, a restart): it reads the chapter's
  caption. It stays quiet while the story plays on its own.
- Play, Next step and Restart are buttons with names and tooltips; the speed control is a segmented control named
  "Speed". Tab moves through them.
- Colors are not the only signal: the log names every sender, receiver and message.

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Packets | A dot with a short trail travels each curve in 0.9 s | Not shown | Not shown |
| The part the story is at | An accent ring fades in | Shown at once | Shown at once |
| Log lines | Rise 6 px and fade in over 240 ms | Appear | Appear |
| Lookup time | Counts up over 0.5 s | Changes at once | Changes at once |
| Cache check | The box's border flashes, then fades | Shown at once | Shown at once |
| Caption | Crossfade | Changes in place | Changes in place |

Everything runs on the story clock, so it pauses, steps and scales with `Speed`. The clock stops while the card is off
screen.

## Responsive behavior

- From 520 px the servers stand in a column on the right of the resolver; narrower, they sit in a row under it and
  their names shrink to fit.
- The card is at most 760 px wide; under 560 px its padding tightens and under 420 px the "Speed" words hide (the
  control keeps its name).
- Every control is a button, so touch works the same as a pointer.

## Related

| Component | Choose it when |
|---|---|
| `PaymentFlow` | The story is a card payment between parties |
| `MultiRegionFailover` | The story is traffic moving between regions |
| `DataFlow` | The story is events flowing through a pipeline |
