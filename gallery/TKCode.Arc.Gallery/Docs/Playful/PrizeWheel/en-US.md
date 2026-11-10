---
title: Prize wheel
summary: A prize wheel with weighted slices. A spin draws the winner by its odds and slows to a stop on it while the pegs flick the pointer.
---

<Demo name="Preview" />

## When to use

`PrizeWheel` is a round wheel of slices, each a prize with its own odds, under a pointer at the top. A spin decides the
winner first, by the odds, then turns the wheel several times and lets it slow down under friction until the winner
stops under the pointer. Pegs on the rim flick the pointer as they pass, and the winning slice lights up in the accent
color when the wheel rests. The winner can also come from your server, so the wheel only shows a decision already made.

- **Rewards and promotions**: a daily spin, a discount after checkout, a reward for finishing onboarding.
- **Raffles and random picks** in front of an audience, such as choosing who presents next.
- **Games and events** where the suspense of the spin is part of the fun.

## When not to use

- **Fair draws with legal weight**: decide those on a server with an audited process, then show the result with
  `SpinTo`.
- **Choosing between options people care about**: a wheel hides the reasoning. Use a list or a comparison.
- **More than about 12 slices**: labels no longer fit. Group small prizes into one slice.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:PrizeWheel Label="Daily prize wheel" Width="320" Height="320">
        <arc:PrizeWheelSegment Label="5% off" Weight="4" />
        <arc:PrizeWheelSegment Label="Free shipping" Weight="3" />
        <arc:PrizeWheelSegment Label="Gift card" Weight="1" />
    </arc:PrizeWheel>
</UserControl>
```

`PrizeWheel` is a control in the Special package. Register the package with `UseArcSpecial()` next to
`UseArcControls()`. Its content is the slices, clockwise from the top: `PrizeWheelSegment`s written inline or added to
`Segments` from code. The wheel is square; give it a size, or it takes the width it is given up to 360 px.

### Odds and slice widths

`Weight` is a slice's odds relative to the others (1 by default). A slice with weight 4 wins four times as often as one
with weight 1, and a slice with weight 0 never wins. With `SizesByWeight` on (the default) each slice is as wide as its
odds, so the wheel is honest about them; turn it off for equal slices. Slices alternate between two surfaces; give a
slice its own `Fill` and `Foreground` to make it stand out.

### Spinning and the result

A click or a tap on the wheel, Enter or Space spins it, and so does `Spin()`, which returns the index it drew. While it
turns, `IsSpinning` is true and further spins are ignored. When it rests, `ResultIndex` and `Result` hold the winner,
the slice lights up and `SpinCompleted` fires with the index and the slice. `Ticked` fires each time a peg flicks the
pointer, for a click sound or a haptic tick. `ClearResult()` turns the light off.

`Duration` is how long a spin takes (4.8 s) and `Turns` the least number of full turns before it settles (5). The hub
shows `SpinText` ("Spin").

### A winner chosen elsewhere

When a server decides the prize, set `IsInteractive` to false so people cannot spin it themselves, and call
`SpinTo(index)` with the answer. The spin looks the same.

<Demo name="Server" />

### Best practices

**Do**

- Keep labels to a word or two; long labels are cut short to fit the slice.
- Say what was won in words next to the wheel, from `SpinCompleted`.
- Decide valuable prizes on the server and show them with `SpinTo`.

**Avoid**

- Slices with odds that do not match their width, unless you say so: people read width as chance.
- Making people spin to get something they were already promised.

### Common questions

**Is the spin random?** `Spin()` draws with `Random.Shared` by the slices' weights. For anything that matters, draw on
the server and call `SpinTo`.

**Why does the wheel land in a slightly different place each time?** It stops at a random spot inside the winning
slice, kept away from the edges, so it never looks staged.

## API reference

<!-- api: PrizeWheel -->

<!-- api: PrizeWheelSegment -->

<!-- api: PrizeWheelResultEventArgs -->

## Accessibility

- The wheel is a button named by `Label` ("Prize wheel" by default) and described as a "prize wheel". Assistive
  technology can invoke it, which spins it.
- A polite live region announces the prize when the wheel rests, and the wheel's help text is the last prize.
- Keyboard focus shows a round focus ring; pointer focus shows none. With `IsInteractive` off the wheel is not a Tab
  stop.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Focus the wheel |
| `Enter` / `Space` | Spin the wheel |

## Motion

- A spin turns at least `Turns` full turns and slows on a cubic ease out over `Duration`: fast at first, then a long,
  gentle stop, like a wheel slowing under friction.
- The pointer is a flapper hanging from a pivot. A peg pushes it aside as it approaches and lets it go as it passes, and
  a stiff, lightly damped spring swings it back with a small wobble.
- When the wheel rests the winning slice fills with the accent color and its label turns the accent's text color over
  240 ms.
- A frame only rotates the drawing and swings the flapper; slices and labels are built once per size. Frames stop when
  the wheel and the flapper are still, and pause while the wheel is out of view.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Spin | Turns and slows to the winner | Turns straight to the winner at once | Turns straight to the winner at once |
| Pegs | Flick the pointer | None | None |
| Winner | Lights up over 240 ms | Lights up over 240 ms | Lit at once |

## Responsive behavior

- The wheel is square and fills the smaller of the width and height it is given, up to 360 px when nothing limits it.
  On a phone, give it the full width.
- Label and hub text scale with the wheel between 11 and 16 px. A slice too thin for a line of text keeps its color but
  shows no label.
- A tap on the wheel spins it; taps outside the circle do nothing.

## Related

- `AnimatedCounter` reveals a number rather than a prize.
- `TicketTear` is another playful way to hand over a reward.

| Component | Choose it when |
|---|---|
| `PrizeWheel` | A prize should feel won, with suspense |
| `TicketTear` | A reward is redeemed with a gesture |
| `AnimatedCounter` | A number should count to its value |
