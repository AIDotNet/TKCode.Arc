---
title: Avatar
summary: A compact identity marker for people and accounts.
---

<Demo name="Initials" />

## When to use

- A single person next to a name, a comment or a record, when a photo or initials identify them.
- Presence in a page header or a list, with the `Status` dot.
- A photo that may be missing or fail to load: the initials take over.

## When not to use

- Several people in a small space with an overflow count: use `AvatarGroup`.
- An avatar that opens an account menu: use `UserMenu`.
- A full list of people with their roles: use `SortableDataTable`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Avatar DisplayName="Ada Lovelace" />
</UserControl>
```

### Photo and fallback

`Source` loads a photo. While it loads, the initials show; if it fails, the initials stay.

<Demo name="Photos" />

### Sizes

`Size` sets the diameter: `Small` is 28, `Medium` (the default) is 36, `Large` is 48 and `XLarge` is 88.

```xml
<arc:Avatar DisplayName="Ada Lovelace" Size="Small" />
<arc:Avatar DisplayName="Grace Hopper" Size="Medium" />
<arc:Avatar DisplayName="Alan Turing" Size="Large" />
<arc:Avatar DisplayName="Katherine Johnson" Size="XLarge" />
```

### Status

`Status` shows an online or offline dot at the lower right. `AvatarStatus.None` hides it.

<Demo name="Status" />

### Initials

The initials are the first letters of the first two words of `DisplayName`, in upper case: "Ada Lovelace" gives
"AL". `Initials` returns the same value, and `Avatar.ComputeInitials` computes it for any name.

## API reference

<!-- api: Avatar -->

## Accessibility

- The avatar is an image with the name `DisplayName`. A status adds it to the name, such as "Maya Chen, online".
- The photo and the initials are decorative and hidden from assistive technology, so the name is read once.
- The status dot is hidden; the status reaches assistive technology through the name.
- The avatar is not focusable and has no keyboard behavior.

## Motion

- A photo that is still loading fades in from a soft blur. A photo that is already cached shows at once.
- The status dot pops in from a small size on a snappy spring and shrinks out when it leaves.
- Reduced motion switches the status dot at once and shows the photo without a fade. Off changes everything at once.

## Related

- `AvatarGroup` for a stack of people with an overflow count.
- `UserMenu` when the avatar opens account actions.
- `Badge` for a status or category label that is not a person.
