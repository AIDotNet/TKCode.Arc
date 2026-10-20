---
title: Signature pad
summary: Smooth ink that thins with speed, with undo, replay, and PNG or SVG export.
---

## When to use

- Contracts, delivery confirmations and consent forms that need a hand-drawn signature.
- A signature that is kept as an image or vector file, exported with `ExportPng`, `ExportBitmap` or `ExportSvg`.
- Signing that must be reviewed before it is saved, with undo, redo and replay.

## When not to use

- A typed name is enough: use `Input`.
- A deliberate confirmation with no signature to keep: use `HoldToConfirm`.
- Signing a document that is already a file: use `FileDropzone` to attach it.

## Usage

The pad draws the ink with the pen's pressure, or with the speed of a mouse or finger. `Signer` prints a name under
the baseline and `Hint` shows until the first stroke.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SignaturePad Signer="Ada Lovelace"
                      Hint="Sign here" />
</UserControl>
```

### Ink color and width

`InkColor` and `InkWidth` set the defaults for new strokes. Each stroke keeps the color and width it was drawn with.
The arrow keys move between the options in the toolbar.

```xml
<arc:SignaturePad InkColor="Blue"
                  InkWidth="Bold"
                  Hint="Sign in blue" />
```

### Reading the strokes

`StrokesChanged` runs after every stroke, undo, redo and clear. `Strokes` is read-only and `IsEmpty` is true until
the first stroke.

```csharp
pad.StrokesChanged += (_, e) =>
{
    saveButton.IsEnabled = e.Strokes.Count > 0;
};
```

### Export

`ExportPng` and `ExportSvg` return the ink cropped to its bounds. `ExportPng` takes an optional `scale`, which
defaults to 3. `SaveAsync` opens the save dialog with `FileName` as the default name. It returns false when the user
cancels or when there are no strokes.

```csharp
var png = pad.ExportPng();
var svg = pad.ExportSvg();

var saved = await pad.SaveAsync(SignatureFileKind.Png);
```

### Restoring a signature

`Reset` replaces the strokes without the wipe animation and clears the undo history. Pass the strokes you stored
earlier to show them again, or call it with no argument to start over.

```csharp
pad.Reset(storedStrokes);
```

## API reference

<!-- api: SignaturePad -->

## Accessibility

- The pad is a focusable image named "Signature pad". Its name includes the number of strokes, and the keyboard
  shortcuts are in its help text.
- Mod+Z undoes, and Shift+Mod+Z or Mod+Y redoes, while the pad has focus. Delete and Backspace clear it, and Clear
  can be undone.
- Ink color and width are single-choice groups. Only the selected option is in the tab order, and the arrow keys
  move between options.
- Toolbar buttons have a name and a tooltip. Strokes, undo, redo, clear, replay and export results are announced
  politely.

## Motion

- Clear sweeps a wiper across the pad and wipes the ink away with it, over about half a second.
- Replay redraws each stroke at its original pace, with a dot at the pen tip, and the whole replay takes about 3 seconds.
- The hint fades in and rises, and the color and width highlight slides to the chosen option.
- Reduced motion removes the wipe and the blur and uses short fades.

## Related

- `Input` for a typed name next to or instead of a drawn signature.
- `HoldToConfirm` for a deliberate confirmation that does not need a signature.
- `FileDropzone` for attaching files such as an already signed document.
