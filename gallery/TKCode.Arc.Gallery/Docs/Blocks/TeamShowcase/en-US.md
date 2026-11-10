---
title: Team showcase
summary: A team section with generated portraits, names and roles, a bio over the portrait on hover, focus or tap, and a department filter that slides the grid into place.
---

<Demo name="Preview" />

## When to use

`TeamShowcase` introduces the people behind a product on an about, careers or company page. Each person is a card with
a square portrait, the name and the role; pointing at a card, focusing it with the keyboard or tapping it lays a short
bio and the person's city over the lower part of the portrait. People without a photo get a generated portrait: a
head-and-shoulders figure in the theme's chart colors, chosen from the name, so the same person always looks the same.
A segmented filter above the grid shows one department at a time: the others fade out and the remaining cards slide
into their new places.

- **An about page** with a team of five to thirty people.
- **A careers page** that shows who a candidate would work with, filtered by department.
- **A project or studio page** that credits the people who made it.

## When not to use

- **A searchable company directory** with contact details: use `TeamDirectory`.
- **Managing members and roles** in an app: use `TeamMembers` or `RolesPermissions`.
- **Quotes from customers**: use `StudioPerspectives`.

## Usage

```xml
<arc:TeamShowcase Title="The people behind Northwind" MaxColumns="4">
    <arc:TeamShowcaseMember Name="Mara Okonjo" Role="Head of Design" Department="Design" Location="Lisbon" Bio="Draws the first version of everything on paper." />
    <arc:TeamShowcaseMember Name="Teo Lindgren" Role="Staff Engineer" Department="Engineering" Location="Stockholm" Bio="Owns the sync engine." />
</arc:TeamShowcase>
```

With no members the block shows an invented team of ten; set `ShowsSampleTeam` to false to show an empty grid instead.
A member's `Image` replaces the generated portrait, and `Seed` picks another figure.

### Your own team

<Demo name="CustomTeam" />

The departments come from the members, in the order they first appear; the filter shows when there are at least two
(`IsFilterVisible` hides it). `SelectedDepartment` is two-way: null shows everyone. `MemberInvoked` reports a click, a tap,
Enter or Space on a card, which also pins its bio open.

### Columns

The grid adds columns while each stays at least `MinColumnWidth` (200) wide, up to `MaxColumns` (4).

## API reference

<!-- api: TeamShowcase -->

## Accessibility

- The section is named by its title, which is a level 2 heading.
- Each card is focusable and named by the person; its help text reads the role, department, bio and city.
- After a filter change the number of people shown is announced. Focus on a card that the filter removes moves to the
  filter.
- Portraits are decorative and hidden from screen readers.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Move to the filter, then through the cards. |
| Arrow keys | Move between cards by column and row. |
| `Home` / `End` | First or last card. |
| `Enter` / `Space` | Pin or unpin the card's bio. |
| `Escape` | Unpin the bio. |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Bio appears | Fades in 160 ms and rises 8 px on a spring; the portrait grows 3% | Fade | Immediate |
| Filter removes people | They fade out in 160 ms | Fade | Immediate |
| Remaining people move | They slide to their new places on a spring | Jump | Jump |
| Filter brings people back | They fade in with a short rise, staggered by 35 ms | Fade | Immediate |

## Responsive behavior

- Breakpoints use the block's own width: below 720 (`:compact`) the title steps down and the gaps tighten; below 480
  (`:narrow`) the paddings tighten again and the filter takes the full width and scrolls.
- The number of columns follows the width (`MinColumnWidth`, `MaxColumns`); on a phone the grid is one or two columns.
- The bio shows on a tap as well as on hover, so touch screens can read it.

## Related

- `TeamDirectory` for a searchable directory with profiles.
- `StudioPerspectives` for customer quotes.
- `SegmentedControl`, which is the department filter.
