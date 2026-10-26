---
title: Mobile text field
summary: A 44 px filled text field with an optional icon and a clear button, whose border takes the accent color while focused.
---

<Demo name="Basic" />

## When to use

`TextField` is a single-line text input for a mobile form: names, emails, short answers.

- **Forms on a phone**: the field is 44 px high, so it is easy to hit with a thumb.
- **Fields with a meaning**: an `Icon` before the text says what the field is for.
- **Fields that people correct often**: the clear button empties it in one tap.

The live demo runs inside a phone frame, a desktop simulation; the soft keyboard on a real device has not been verified here.

## When not to use

- **Search**: use `Searchbar`, which has the search glyph, a pill shape and a Search key.
- **Long, multi-line text**: a text area is the right control, and this one is single line.
- **A choice from a few values**: use a switch, a segmented control or a sheet with a list.

## Usage

`TextField` derives from Avalonia's `TextBox`, so `Text`, selection, `IsReadOnly` and binding work as they do there. Show a hint with `PlaceholderText`.

```xml
<arcm:TextField xmlns="https://github.com/avaloniaui"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
                xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
                PlaceholderText="Name"
                Icon="{x:Static arc:ArcIcons.User}"
                Text="{Binding Name}" />
```

The clear button shows only while the field has focus and text, and not when it is read only or `ShowClearButton` is false. It empties the field and keeps the focus. A field has no hover state: a finger does not hover.

### Tokens

`TextFieldToken` holds the geometry: `FieldHeight` (44), `ContentPadding` (12 at the sides), `BorderWidth` (1), `GlyphExtent` (18), `IconMargin` and `DisabledOpacity` (0.5). Colors, radii and type come from the ArcDesign tokens.

## API reference

<!-- api: MobileTextField -->

## Accessibility

Give every field a visible name next to it or an icon plus a placeholder, so the hint is not the only label once the person starts typing. The field is at least 44 px high and the clear button is a 44 px touch target.

## Motion

The border changes color on the fast curve when the field gains or loses focus. The clear button pops in from scale 0.6 with a fade (P26 through `PresenceHost`) and leaves faster than it arrived. With reduced motion the border changes at once and the clear button appears and disappears at once.

## Related

- `MobileSearchbar`: a text field made for search.
- `MobilePage`: the screen a form sits in.
- `MobileButton`: the usual way to submit a form.
