---
title: Mobile search bar
summary: A pill-shaped text field with a search glyph, a clear button and a Search key that raises Submitted.
---

<Demo name="Basic" />

## When to use

`Searchbar` is the field a person types a query into: at the top of a list, or on a search screen.

- **Filtering or searching a list**: put it at the top of the page, under the navbar.
- **A search screen**: it is the first thing on the page, and it takes focus when the screen opens.

The live demo runs inside a phone frame, a desktop simulation; the soft keyboard's Search key on a real device has not been verified here.

## When not to use

- **Ordinary text input**: use `TextField`.
- **A desktop search box with suggestions**: use the desktop search controls.

## Usage

`Searchbar` is a `TextField`, so `Text`, `PlaceholderText` and binding work as they do there. Handle `Submitted` to run the search: it is raised when the person presses Enter or the soft keyboard's Search key.

```xml
<arcm:Searchbar xmlns="https://github.com/avaloniaui"
                xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
                PlaceholderText="Search messages"
                Text="{Binding Query}" />
```

```csharp
searchbar.Submitted += (_, _) => Search(searchbar.Text);
```

The search glyph is the field's `Icon`, set by default; the clear button shows while the field has focus and text. To filter as the person types, bind `Text` and react to its changes instead.

### Tokens

`Searchbar` shares `TextFieldToken` with `TextField`: the same 44 px height, padding and glyph size. Only its radius differs: the pill radius from the ArcDesign tokens.

## API reference

<!-- api: MobileSearchbar -->

## Accessibility

Give the field a placeholder that says what is searched, such as "Search messages". The field is at least 44 px high.

## Motion

The border changes color on the fast curve when the field gains or loses focus. The clear button pops in from scale 0.6 with a fade (P26 through `PresenceHost`) and leaves faster than it arrived. With reduced motion the border changes at once and the clear button appears and disappears at once.

## Related

- `MobileTextField`: the field it is built on.
- `MobileNavbar`: the bar the search bar usually sits under.
- `MobileListGroup`: the usual list of results.
