---
title: Avatar group
summary: Show a team or set of contributors in a small space.
---

<Demo name="Team" />

## When to use

`AvatarGroup` shows a small set of people who own, edit or review one item: a header, a card or a table cell. The
avatars overlap, the first people are drawn in full and the rest fold into a "+N" chip, so the group keeps the same
footprint whether the team has four people or forty. It is display only; the names are read through the accessible
names or the list the group summarizes.

- **Editors on a shared document or pricing experiment**: the header of a page shows who is working on the item now,
  and the stack changes as people join or leave the session.
- **Reviewers on a pull request, invoice or contract** in a card: the first reviewers are visible and "+3" marks the
  rest, without a list that would take over the card.
- **Assignees in a table column** on a support ticket or a deal: `Size="Small"` keeps the column narrow while the owner
  of each row stays visible.
- **A project team panel that an admin edits while it is open**: when members are added or removed, the stack slides
  instead of jumping, so the change is visible without a refresh.
- **Watchers on a live record**, such as the people viewing an incident timeline: the first watchers show and a count
  covers the rest.

## When not to use

- **One person**: use `Avatar`, which carries the name and the status of a single person.
- **A list where each person's name and role must be read or compared**: use `SortableDataTable` or a plain list.
- **A person's account actions** such as sign out or switch workspace: use `UserMenu`.
- **Choosing several people from a directory**: use `MultiSelect`, which searches and selects. A group only shows people.
- **Each person needs an action of their own**, such as Remove or Message: use `SortableDataTable`, where each row can
  hold its own controls. The group is not focusable and has no per-person actions.

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

`AvatarGroup` derives from Avalonia's `TemplatedControl`. Its template is one panel that lays the people out and
draws the overflow chip, so the group has no focus and no pointer behavior of its own beyond the hover fan. Its
members are `AvatarGroupMember` objects; `Avatar` instances and plain strings are accepted too. The inline members
in the `Items` content are used until `ItemsSource` is set, and then `ItemsSource` takes over completely.

### Anatomy

1. **Stack** (the group): the people are placed from left to right with a 6 px overlap. Later people are drawn over
   earlier ones, so each avatar's left edge stays visible.
2. **Person slot**: each avatar has a 1 px ring in the `Border` color around it. The avatar itself has a 2 px
   `Surface` edge, which separates it from its neighbours. The ring darkens to the strong border color on hover.
3. **Avatar** (`AvatarGroupMember`): the member's photo or initials and optional status dot, at the group's size.
   The dot and its meaning are described in `Avatar`.
4. **Overflow chip**: a circle with the `SurfaceMuted` fill and `TextSecondary` text that shows "+N". Its text is
   10 px at `Small`, the Arc XS step at `Medium` and the Arc SM step at `Large`.
5. **Name pill**: a pill above the hovered person with the foreground color as its fill, the background color as its
   text, and 8 px horizontal and 4 px vertical padding.

The group's `Label` is not drawn. It names the group for assistive technology and builds the overflow label, so put
a visible heading beside the group when people need to read what it is.

### Bound members

Bind `ItemsSource` to a collection of `AvatarGroupMember`, `Avatar` or plain names. The collection takes precedence
over the inline `Items`.

```xml
<arc:AvatarGroup ItemsSource="{Binding Team}"
                 Label="Team members" />
```

### Binding to a view model

With CommunityToolkit.Mvvm, the view model owns the collection and the commands that change it, and the group only
displays it. `ItemsSource` is one-way: the group never edits the collection. `OverflowCount` is read only and is
computed from the collection and `MaxVisibleCount`. The group has no command or click event to bind to; the actions
belong to the view model or to the list beside the group.

```csharp
public partial class ProjectViewModel : ObservableObject
{
    public ObservableCollection<AvatarGroupMember> Editors { get; } = new()
    {
        new("Maya Chen", status: AvatarStatus.Online),
        new("Noah Park"),
        new("Leah Fox", status: AvatarStatus.Offline),
    };

    [ObservableProperty]
    private int _maxVisibleCount = 4;

    [RelayCommand]
    private void AddEditor(string name) => Editors.Add(new AvatarGroupMember(name));

    [RelayCommand]
    private void RemoveEditor(AvatarGroupMember member) => Editors.Remove(member);
}
```

```xml
<arc:AvatarGroup ItemsSource="{Binding Editors}"
                 MaxVisibleCount="{Binding MaxVisibleCount}"
                 Label="Editors" />
```

Joining and leaving animate the slot, and the list is updated in place, so use `Add`, `Insert` and `Remove` on the
collection. Replacing the whole collection makes every person leave and rejoin. To change how someone is shown, set
the `Status`, `DisplayName` or `Source` of the existing `AvatarGroupMember`; the avatar follows the change without a
slot animation.

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

`MaxVisibleCount` (4 by default) sets how many people show before the "+N" chip. `OverflowCount` reports the number
folded into the chip. The first people in the list are shown and the rest are counted, so put the most relevant
people first. Only the visible people get controls, so long lists stay cheap.

```xml
<arc:AvatarGroup ItemsSource="{Binding Team}"
                 MaxVisibleCount="3" />
```

The chip's accessible name is the overflow count followed by the lowercased `Label`, such as "2 more team members".

### Sizes

`Size` applies to every avatar and the overflow chip: `Small` is 28 px, `Medium` (the default) is 36 px and `Large` is
48 px. `XLarge` is drawn as `Large`, the largest group size the theme defines.

<Demo name="SizesAndLimits" />

```xml
<arc:AvatarGroup ItemsSource="{Binding Team}" Size="Small" MaxVisibleCount="3" />
<arc:AvatarGroup ItemsSource="{Binding Team}" Size="Large" MaxVisibleCount="5" />
```

The group's width is fixed by its members and size: 6 px of padding plus each visible slot's width minus the overlap.
With four visible people and a chip at `Medium`, the group is 156 px wide, so measure the cell it goes into before
choosing `Large`.

### Inline members

Members can be written in place of a bound list. This suits a fixed set, such as the people on a static card.

<Demo name="InlineMembers" />

### Data shape and performance

- **Entries**: each item is an `AvatarGroupMember`, an `Avatar` or a plain string. Null entries are skipped, so a
  list with gaps still counts correctly.
- **Visible work**: only the first `MaxVisibleCount` entries get avatars, plus one chip. A group over 500 members
  builds the same few controls as one over 5.
- **Identity**: entries are tracked by their object and by their occurrence, not by name. Two people with the same
  name are two slots, so a duplicate name does not break the animation. Keep one object per person.
- **Updates**: a live update needs a collection that raises change notifications, such as `AvaloniaList` or
  `ObservableCollection`. The group observes the source only while it is attached to the visual tree, so a detached
  group does not keep the list alive. With a plain list, assign a new collection to `ItemsSource` to refresh.
- **Animation**: joins and leaves animate only while the group is loaded and the motion level is Full. A group that
  is not loaded yet, or a page with reduced motion, takes the change at once.

### Example: project card with a team stack

A pricing project card in a SaaS planning tool lists the people working on the experiment. The title and the
description are on the left, and the group sits on the right, where the eye reads the team after the name. Six
editors are assigned, so four are drawn and the chip reads "+2". The first editor is online and the fifth is
offline, which tells a reviewer who is around to answer a question. The group's `Label` is "Editors", so the overflow
chip is named "2 more editors" for assistive technology.

![Project card header with six editors, four avatars shown and a +2 chip](images/AvatarGroupHeader.png)

```xml
<Border Width="640"
        Padding="20"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1"
        CornerRadius="12">
    <StackPanel Spacing="16">
        <Grid ColumnDefinitions="*,Auto">
            <StackPanel Spacing="4" VerticalAlignment="Center">
                <TextBlock Text="Q3 pricing experiment"
                           FontSize="16"
                           FontWeight="SemiBold"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Shared with the growth and finance teams"
                           FontSize="12"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            </StackPanel>
            <arc:AvatarGroup Grid.Column="1" Label="Editors" VerticalAlignment="Center">
                <arc:AvatarGroupMember DisplayName="Maya Chen" Status="Online" />
                <arc:AvatarGroupMember DisplayName="Noah Park" />
                <arc:AvatarGroupMember DisplayName="Leah Fox" Status="Offline" />
                <arc:AvatarGroupMember DisplayName="Omar Haddad" />
                <arc:AvatarGroupMember DisplayName="Ines Duarte" />
                <arc:AvatarGroupMember DisplayName="Kenji Sato" />
            </arc:AvatarGroup>
        </Grid>
        <Border Height="1" Background="{arc:ArcDesignTokenResource Border}" />
        <Grid ColumnDefinitions="*,Auto">
            <TextBlock Text="Last edited by Maya Chen on 18 Oct 2026"
                       FontSize="12"
                       VerticalAlignment="Center"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            <arc:Button Grid.Column="1" Content="Invite editor" Variant="Secondary" />
        </Grid>
    </StackPanel>
</Border>
```

### Content guidelines

- **Label**: a plural noun for the role, in sentence case and without a colon: "Editors", "Reviewers", "Assignees".
  The label is not drawn, and it is read aloud, so the colon would be spoken.
- **Overflow phrase**: the chip reads the count, then "more", then the lowercased label. Choose a label that reads
  well after "more": "2 more reviewers" works, "2 more people" is vague when the group is about one role.
- **Heading**: put a visible sentence-case heading or a short line of text beside the group, such as "Editing now",
  because the group shows no words of its own.

| Instead of | Write |
|---|---|
| `Label="People"` | `Label="Reviewers"`, so the chip reads "2 more reviewers" |
| `Label="Editors:"` | `Label="Editors"`, without the colon |
| `Label="Users"` | `Label="Assignees"` when the item has owners |
| `Label="team"` | `Label="Team members"`, a full noun phrase reads well after "more" |

### Best practices

**Do**

- Order the members so that the most relevant people come first. The overflow folds the tail of the list, so the
  people at the end are the ones a reader is least likely to see.
- Set `Label` to a plural noun for the role. The label is the accessible name of the group and the source of the
  overflow phrase, so one word serves both.
- Put a visible heading next to the group. The label is not drawn, so a sighted reader needs the words somewhere.
- Change `Status` on the existing `AvatarGroupMember` when presence changes. The avatar updates in place and the
  person keeps their slot, instead of leaving and rejoining the stack.

**Avoid**

- Replacing the whole collection on every refresh. Each replacement makes every person leave and join again, so the
  stack animates all of its slots at once.
- Relying on the hover name in a touch layout. Touch does not fan the stack or show the name, so the list beside it
  must carry the names.
- Placing a group with `Large` avatars in a narrow cell. Each person adds about 42 px at `Large`, and the group does not
  wrap or shrink, so the cell overflows.
- Setting `MaxVisibleCount` high to show everyone in a narrow area. The stack grows by one slot per visible person, so
  the width is set by the count and not by the space available.

### Common questions

**Why does the hover name not show on a touch screen?** The fan, the lifted person and the name pill respond only to
mouse and pen pointers. Touch input is ignored by design, so show the names in the list or card that holds the group
when touch matters.

**How do I show everyone without a "+N" chip?** Set `MaxVisibleCount` to the number of members or more. The overflow
count is then 0 and no chip is created.

**Why does my list change not appear in the group?** The group listens only to collections that raise change
notifications, such as `AvaloniaList` and `ObservableCollection`, and only while it is attached to the visual tree. A
plain `List` never reports a change. Assign a new collection to `ItemsSource`, or use an observable collection.

**Can two members have the same name?** Yes. Each member object is its own slot, and the group tracks objects, not
names. Both people are drawn and both count toward the overflow. Give each person their own `AvatarGroupMember`.

**How do I make a person clickable?** The group has no click event and takes no focus. Place a list, a table or
buttons beside it that act on the same collection, and use the group only to display who is on the item.

## API reference

<!-- api: AvatarGroup -->

<!-- api: AvatarGroupMember -->

## Accessibility

- The group is named by `Label`. Each avatar keeps its own name and status for assistive technology.
- The overflow chip is an image named from `Label`, such as "2 more team members".
- The stack has no keyboard behavior and is not focusable. Use the list or table that the group summarizes for
  keyboard work.
- The hover name is visual only: the name of each person is also in the accessible name of the avatar.
- Touch input does not fan the stack or show a name, so the names must also be visible in the layout that holds the
  group when touch is a supported input.

### Keyboard

The group takes no focus and has no keyboard commands, so `Tab` skips it. The people are reachable through the list,
table or form that the group summarizes, where each person can be focused and acted on.

## Motion

- When someone joins or leaves, their slot grows or shrinks on a morph spring while it fades and scales slightly, so
  the stack slides instead of jumping.
- The overflow count rolls up when it grows and down when it shrinks.
- With the pointer over the stack, the people loosen from the center, the person under the pointer rises a little
  and shows a ring and their name, and the neighbours move aside.
- Reduced motion removes the hover spread and keeps the ring and name; the count fades. Off changes everything at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| A person joins | Their slot opens on the morph spring and the stack slides | Shown at once | Immediate |
| A person leaves | Their slot closes on the morph spring while they fade and scale | Removed at once | Immediate |
| The overflow count changes | Rolls up or down with the change | Fades | Immediate |
| The pointer moves over the stack | People fan out 4 px per step, the hovered person rises 2 px and neighbours move 3 px | No fan; ring and name stay | Immediate |
| The name above the hovered person | Shows after a 60 ms delay | Shows at once | Immediate |

## Related

- `Avatar` for one person.
- `UserMenu` when the group member's avatar opens account actions.
- `SortableDataTable` when names, roles and other columns must be compared.

| Component | Choose it when |
|---|---|
| `Avatar` | One person is shown next to a name, a comment or a record |
| `UserMenu` | An avatar opens sign out, profile or workspace actions |
| `SortableDataTable` | Names, roles and other columns must be read and compared |
| `MultiSelect` | People are searched and selected from a directory |
| `Badge` | The label is a status or category rather than a set of people |
| `ChatThread` | The people are the authors of a conversation |
