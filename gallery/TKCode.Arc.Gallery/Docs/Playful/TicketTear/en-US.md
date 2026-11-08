---
title: Ticket tear
summary: An event ticket with a stub you tear off. It rips along the perforation one hole at a time, following your hand, and once free it falls away.
---

<Demo name="Preview" />

## When to use

`TicketTear` turns a one-time step into something physical. The stub rips along a row of holes as the hand pulls it,
and because a tear cannot be undone, the gesture feels as final as the step it stands for.

- **Passes and admissions**: event tickets, boarding passes and door passes that get used once.
- **Coupons and vouchers**: a single redemption you want people to enjoy.
- **A playful confirmation**: checking in, where tearing the stub is the action itself.

## When not to use

- **Dangerous actions**: deleting or paying needs words people can read; use a dialog or a hold-to-confirm button.
- **Tickets that are only shown**: if nothing gets redeemed, a plain card says it better.
- **Anything you may need to undo often**: the gesture promises it is final.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TicketTear Label="Tear off stub to check in">
        <TextBlock Text="Night of Paper Boats" />
        <arc:TicketTear.Stub>
            <TextBlock Text="Bench 7C" />
        </arc:TicketTear.Stub>
    </arc:TicketTear>
</UserControl>
```

`TicketTear` is a `ContentControl` in the Special package. Register the package with `UseArcSpecial()` next to
`UseArcControls()`. Its `Content` is the body of the ticket and `Stub` is what is printed on the stub. It fills the
width it is given; the stub keeps `StubWidth` and the body takes the rest. Keep the stub short: a seat, a gate or a code.

### Knowing when it was torn, and tearing it yourself

`Torn` is raised once when the stub comes off, whether the hand, the keyboard or your code tore it. `IsTorn` is the
state and can be bound both ways: set it to true to tear the stub from a server event, and to false to put a fresh stub
on. A ticket that is already true when it first appears simply has no stub, with no tear and no event. `Tear()` and
`Reset()` do the same from code.

<Demo name="CheckIn" />

### Paper and size

`Background` is the body's paper, and `StubBackground` and `StubForeground` are the stub's paper and text. By default
the stub stands out from the body: light on a dark theme and dark on a light one. The holes and notches are real gaps, so
whatever is behind the ticket shows through them. `StubWidth` sets the width of the stub.

<Demo name="Coupon" />

### Best practices

**Do**

- Use it for one step that is meant to feel final, such as a check-in or redeeming a voucher.
- Give `Label` the action, like "Tear off stub to board": it is what assistive technology reads.
- Handle `Torn` to do the work, and keep the state in `IsTorn` if the server decides too.

**Avoid**

- Hiding information on the stub that people still need afterwards: it is gone once torn.
- Long text on the stub. It only has a little over 100 px.

### Common questions

**Can the tear be undone by pulling back?** No. The tear only grows. Letting go early leaves it where it got to, and
the loose flap settles most of the way back; pulling again carries on from there.

**Does it vibrate on a phone?** No. Avalonia has no public vibration API, so as the tear passes each hole the flap gives
a small visible jolt instead.

## API reference

<!-- api: TicketTear -->

## Accessibility

- The stub is a button named by `Label`, and it can be invoked by assistive technology. The flap is drawn from a picture
  of the stub and hidden from assistive technology, so the stub is announced once.
- A polite status region says when the stub has been torn off.
- Only the stub is a tab stop. When the stub leaves while it has focus, focus moves to the ticket rather than being lost.
- Pointer focus draws no ring; keyboard focus shows the shared focus ring around the stub.

### Keyboard interactions

| Key | Action |
|---|---|
| Tab | Moves focus to the stub. |
| Enter or Space | Tears the rest of the stub off on its own, and it drops away. |

## Motion

- The tear starts at the top notch and follows the hand 1:1, down or away from the body, whichever the hand has done
  more of. It never goes back.
- The torn edge runs through the middle of every hole and wanders between them, on a coarse and a fine scale. The line
  is fixed for the ticket, and short loose fibers stand just past it on both pieces.
- The loose flap curls from the front of the tear and follows the pull on a spring. Letting go early returns it most of
  the way, not all of it.
- Enter, Space or setting `IsTorn` rips the rest on its own, faster with each hole that gives way. A flick, released at
  700 px/s or more, does the same from its own speed.
- Once free, the stub pops up a little, falls under gravity, drifts the way it was flung and spins on a spring. It fades
  out in half a second.
- Frames run only while something moves, and pause while the ticket is out of view. Each frame writes the paper's shapes
  and the flap's picture; nothing is laid out.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Pulling | The tear follows with a curling flap and fibers | A plain crack follows the hand | A plain crack follows the hand |
| Finishing | Rips faster and faster | Immediate | Immediate |
| Leaving | Pops, falls, spins and fades in 500ms | Fades in place in 160ms | Disappears |
| Fresh stub | Fades in over 180ms | Fades in over 180ms | Appears |

## Responsive behavior

- The ticket fills its container's width, and it is at least 148 px high (`TicketMinHeight`). Its height follows the
  taller of the body and the stub, and the holes spread evenly over it.
- The stub keeps its width and the body shrinks, so the body's text should wrap on a narrow screen. It stays usable on a
  320 px phone with the default stub.
- Pointer capture keeps the tear going when the hand leaves the stub, and a touch drag that starts on the stub tears it.
  A swipe on the body still scrolls the page.
- A falling stub is drawn outside the ticket's box. Leave it room, and do not clip the ticket's parent if you want to see
  the whole fall.

## Related

- `StickerPeel` and `PageCurl` are other paper you can pull.
- `HoldToConfirm` is the choice when a final step has to be stated in words.

| Component | Choose it when |
|---|---|
| `TicketTear` | One redemption should feel physical and final |
| `HoldToConfirm` | An action is risky and needs a plain label |
| `StickerPeel` | People arrange marks for fun, and nothing is used up |
