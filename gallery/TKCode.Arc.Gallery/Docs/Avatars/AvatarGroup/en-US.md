---
title: Avatar group
summary: Show a team or set of contributors in a small space.
---

## When to use

- Who owns or is editing an item, in a page header, a card or a table cell.
- A short team list where the first few people matter and the rest can fold into a count.
- A group that changes while the page is open: people join or leave and the stack moves.

## When not to use

- One person: use `Avatar`.
- A list where each person's name and role must be read: use `SortableDataTable` or a list.
- A person's account menu: use `UserMenu`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:AvatarGroup Label="Editors">
        <arc:AvatarGroupMember DisplayName="Maya Chen" Status="Online" />
        <arc:AvatarGroupMember DisplayName="Noah Park" />
        <arc:AvatarGroupMember DisplayName="Leah Fox" Status="Offline" />
    </arc:AvatarGroup>
</UserControl>
```

### Bound members

Bind `ItemsSource` to a collection of `AvatarGroupMember`, `Avatar` or plain names. The collection takes
precedence over the inline `Items`.

```xml
<arc:AvatarGroup ItemsSource="{Binding Team}"
                 Label="Team members" />
```

```csharp
var team = new AvaloniaList<AvatarGroupMember>
{
    new("Maya Chen", status: AvatarStatus.Online),
    new("Noah Park"),
    new("Leah Fox", status: AvatarStatus.Offline),
};
group.ItemsSource = team;

// Joining and leaving animate the slot; the list is updated in place.
team.Insert(1, new AvatarGroupMember("Omar Haddad"));
team.RemoveAt(1);
```

### Overflow

`MaxVisibleCount` (4 by default) sets how many people show before the "+N" chip. `OverflowCount` reports the
number folded into the chip. Only the visible people get controls, so long lists stay cheap.

```xml
<arc:AvatarGroup ItemsSource="{Binding Team}"
                 MaxVisibleCount="3" />
```

### Sizes

`Size` applies to every avatar and the overflow chip: `Small` is 28, `Medium` (the default) is 36 and `Large` is 48. `XLarge` is drawn as `Large`.

```xml
<arc:AvatarGroup ItemsSource="{Binding Team}" Size="Small" MaxVisibleCount="3" />
<arc:AvatarGroup ItemsSource="{Binding Team}" Size="Large" MaxVisibleCount="5" />
```

## API reference

<!-- api: AvatarGroup -->

<!-- api: AvatarGroupMember -->

## Accessibility

- The group is named by `Label`. Each avatar keeps its own name and status for assistive technology.
- The overflow chip is an image named from `Label`, such as "2 more team members".
- The stack has no keyboard behavior and is not focusable. Use the list or table that the group summarizes for
  keyboard work.
- The hover name is visual only: the name of each person is also in the accessible name of the avatar.

## Motion

- When someone joins or leaves, their slot grows or shrinks on a morph spring while it fades and scales slightly, so
  the stack slides instead of jumping.
- The overflow count rolls up when it grows and down when it shrinks.
- With the pointer over the stack, the people loosen from the center, the person under the pointer rises a little
  and shows a ring and their name, and the neighbours move aside.
- Reduced motion removes the hover spread and keeps the ring and name; the count fades. Off changes everything at once.

## Related

- `Avatar` for one person.
- `UserMenu` when the group member's avatar opens account actions.
- `SortableDataTable` when names, roles and other columns must be compared.
