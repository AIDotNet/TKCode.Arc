---
title: OTP input
summary: A six digit verification flow with paste support and keyboard navigation.
---

<Demo name="Preview" />

## When to use

Use `OtpInput` for the step after you have sent someone a one-time code: a row of single-character slots, one per
character, with a visible label and helper copy underneath. Typing fills a slot and moves to the next, a pasted code
spreads over the slots, and the arrow keys and Backspace move through the row the way people expect. Check the code on
your server; the control only collects it.

- **Verification codes** sent by email or text message.
- **Two-factor authentication** codes from an authenticator app.
- **Short PINs and recovery codes**, with another `Length` or `InputMode="Text"`.

## When not to use

- **Secrets people type from memory** (passwords, passphrases): use `PasswordField`.
- **Any other text**: use `Input`.

## Usage

```xml
<arc:OtpInput x:Name="Code"
              Description="We sent a 6-digit code to your email."
              AutoFocus="True" />
```

```csharp
Code.Completed += async (_, e) =>
{
    Code.IsReadOnly = true;                       // keep focus, ignore typing while the code is checked
    var ok = await CheckCodeAsync(e.Code);       // your server call
    Code.IsReadOnly = false;
    if (!ok)
    {
        Code.Value = "";
        Code.ErrorMessage = "That code didn't match. Check the latest email and try again.";
        Code.FocusSlot(0);
    }
};

static Task<bool> CheckCodeAsync(string code) => Task.FromResult(code == "123456");
```

`Value` holds the code and binds two-way. `Completed` is raised after every edit that leaves all slots filled (typing the
last character, or a paste), with the code in `OtpInputCompletedEventArgs.Code`; `CompletedCommand` runs with the same
code. Setting `Value` from code never raises `Completed`, so resetting the slots cannot start another check.
`ValueChanged` reports every change with its `ChangeSource`: `Typing`, `Paste` and `Deletion` for the person's edits,
`Programmatic` for yours.

With no configuration the field has six digit slots labelled "Verification code". `Label` is also the name of the group
of slots for screen readers, so give it even when the surrounding page already says what the code is for.

### Verifying a code

<Demo name="Verify" />

The demo checks the code when the last slot is filled (700 ms, simulated). A wrong code clears the slots, shows the error
and shakes the row; typing again clears the error, the way the code step of Arc's sign-in does it:

```csharp
Code.ValueChanged += (_, e) =>
{
    if (e.IsUserEdit && e.NewValue.Length > 0)
    {
        Code.ErrorMessage = null;
    }
};
```

### Errors

<Demo name="Error" />

A non-empty `ErrorMessage` shows under the helper copy, turns every slot and the focus ring to the danger color and makes
`IsInvalid` true. Each new error shakes the row once, so a rejected code reads as an answer to the attempt; clearing the
error and setting the same text again shakes it again. Set `IsInvalid` alone for the danger look without a message, or
call `Shake()` to nudge the row yourself.

### Success

<Demo name="Success" />

`IsSuccess` gives the slots and the ring the success color. Arc's field has no success look (its flows move on to the next
step instead); use it when the field stays on screen after a correct code. An error wins over success.

### Disabled

<Demo name="Disabled" />

`IsEnabled="False"` disables every slot: dimmed to 48 % on the muted surface, skipped by `Tab`, no focus ring.
`IsReadOnly` is the busy state instead: the slots keep focus and the arrow keys, but typing, pasting and deleting are
ignored.

### Length and characters

<Demo name="Length" />

```xml
<arc:OtpInput Label="PIN" Length="4" />
<arc:OtpInput Label="Recovery code" Length="8" InputMode="Text" />
```

`Length` sets the number of slots (1 to 32). `InputMode="Numeric"` (the default) drops anything that is not a digit,
from typing and from a paste, and asks touch keyboards for the number pad. `InputMode="Text"` accepts any character.

### How the code fills the slots

The code is one string, as in Arc: slot `i` shows `Value[i]`. Clearing a slot in the middle (Delete) moves the characters
after it one slot to the left, and a character typed into a slot past the end of the code becomes the next character of
the code. A filled slot takes no more typing until its character is selected (double-click it, or reach it with `Tab`) or
deleted. A paste fills the slots from the focused one and ignores what does not fit; the focus moves to the slot after the
last character pasted. An input method or a platform's SMS autofill that inserts several characters at once spreads them
over the slots the same way.

### Narrow containers

<Demo name="Narrow" />

The field follows the width it is given, not the window. At 328 px or less (Arc's 360 px phone breakpoint less two 16 px
gutters) the slots move 6 px apart and drop to 44 px high. When even that does not fit, every slot shrinks by the same
amount; the gaps stay. A field that is not stretched (`HorizontalAlignment` other than `Stretch`) is as wide as its widest
part, like Arc's centered preview.

### Text and localization

`Label`, `Description` and `ErrorMessage` are plain text. Each slot's accessible name comes from `SlotLabelFormat`
(default `"{0}, digit {1} of {2}"`: the label, the slot's position and the number of slots).

## API reference

<!-- api: OtpInput -->

<!-- api: OtpInputValueChangedEventArgs -->

<!-- api: OtpInputCompletedEventArgs -->

## Accessibility

- The slots form a group named by `Label`; each slot is named like "Verification code, digit 1 of 6".
- The helper copy and the error are the help text of every slot. The error is announced assertively when it appears.
- The focus ring is the one gliding ring of the row, for the keyboard and the pointer alike. Hover styles apply to a mouse
  or a pen only.
- With `InputMode="Numeric"` touch keyboards show the number pad.

### Keyboard

| Keys | Action |
|---|---|
| `0`-`9` / characters | Fill the slot and move to the next one. A filled slot ignores typing until its character is selected. |
| `ArrowLeft` / `ArrowRight` | Move to the previous or next slot. |
| `Backspace` | On an empty slot, clear the previous one and move back; on a filled slot, delete the character before the caret. |
| `Delete` | Clear the current slot. |
| `Ctrl+V` / `Cmd+V` | Paste a code across the slots from the focused one. |
| `Tab` / `Shift+Tab` | Move through the slots one at a time; a slot reached with `Tab` has its character selected. |

## Motion

- A typed character rises into its slot out of a soft blur; a paste lands as a short left to right wave.
- One focus ring glides between the slots while focus stays in the row, and fades in where focus lands from elsewhere.
- A new error nudges the row side to side once. The helper and error rows open their height and rewrite only the words that
  change.

| Change | Full | Reduced | Off |
|---|---|---|---|
| A character arrives | Rises 0.35 em and unblurs, 240 ms; a paste waits 35 ms per slot | Fades in, 120 ms | Immediate |
| A character leaves | Fades, shrinks to 90 % and blurs, 120 ms | Immediate | Immediate |
| The ring moves | Spring (visual duration 0.26 s, no bounce) | Immediate | Immediate |
| The ring appears or leaves | Fades over 160 ms and scales from 94 % | Immediate | Immediate |
| Slot border and fill | 160 ms | Immediate | Immediate |
| Shake | 360 ms | None | None |

### Responsive behavior

One breakpoint, on the width the field is given: 328 px. Slots are 42 px wide and shrink when the row does not fit.

## Related

- `Input` for a single line of text with a label and messages.
- `PasswordField` for secrets people type from memory.
- The Login block, whose email code step uses this field.
