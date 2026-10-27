---
title: Team directory
summary: A company directory with search, team filters and the picked person's profile beside the list, from bio and status to an email address to copy.
---

<Demo name="Preview" />

## When to use

`TeamDirectory` is the page where colleagues look each other up. The list on the left narrows as you type and as you pick a
team; the right side shows one person in full: a short bio, whether they are around, their team, what they work on, where
they are and an email address with a copy button.

- **An internal people page.** Everyone in the company or a department, searchable by name, role, team or city.
- **A team page with departments.** The team buttons give a one-click view of each department with fixed head counts.
- **A contact picker that shows context.** Pick someone, read who they are, and copy their address, or react to
  `PersonSelected` to open their full profile elsewhere.

## When not to use

- **Managing members.** Changing roles, inviting people or removing them belongs in Team members or Roles and
  permissions; this block only reads.
- **A public team section.** A marketing page with large portraits and short quotes wants an image-led layout, not a
  searchable list.
- **Large tables of people.** Hundreds of rows with sorting and columns are a `DataGrid` job. The directory filters in
  memory and lays out every row; for big organizations, filter on the server and give the block one page of people.

## Usage

The block derives from Avalonia's `TemplatedControl`. Each person is a `TeamDirectoryPerson`, an `AvaloniaObject` with a
name, a role, a team, an availability, the current work, a city with a time zone, an email address, a bio and an optional
portrait (initials show without one). Changing a field updates the row, the profile and the matches.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TeamDirectory x:Name="Directory">
        <arc:TeamDirectoryPerson Id="lena" Name="Lena Hartmann" Role="Interaction designer" Team="Design"
                                 Focus="Checkout redesign" City="Vienna" TimeZone="CET"
                                 Email="lena@northwind.test"
                                 Bio="Turns rough ideas into flows people can follow." />
        <arc:TeamDirectoryPerson Id="theo" Name="Theo Brandt" Role="Mobile engineer" Team="Engineering"
                                 Availability="Away" City="Manchester" TimeZone="GMT"
                                 Email="theo@northwind.test" />
    </arc:TeamDirectory>
</UserControl>
```

### Anatomy

- **Header**: the title with the number of people (always the total) and a search box with a clear button.
- **Team buttons**: All first, then each team with its total. A soft pill sits behind the pressed button and glides to the
  next one.
- **List**: one row per match: avatar with an availability dot, name and role. The row whose profile shows sits on a
  highlight that glides from row to row.
- **Profile**: a large avatar, name and role, the bio, and the fields Status, Team, Working on, Location (city and time
  zone) and Email (a mail link and a copy button). Empty fields are left out.

### Search and teams

Search runs on every keystroke. The text is trimmed and compared without regard to case, as one piece, with the name, the
role, the team and the city: "lena hart" finds Lena Hartmann, "hartmann lena" does not, and "operations" also finds people
whose role does not say it but whose team does. Accents are not folded, so "malmo" does not find Malmö. Set `SearchFilter`
to search differently; it receives each person and the text as typed.

The team buttons come from `Teams` when you fill it, otherwise from the people in order of first appearance. Their numbers
are totals and do not follow the search. `SelectedTeam` (null for All) and `SearchText` are two-way and independent:
changing one keeps the other. `MatchCount` and `VisiblePeople` tell what is listed.

### The person shown

`SelectedPersonId` holds the key of the person picked (their `Id`, or their name without one); only a pick, Enter or Space
on a row, or your code changes it. `DisplayedPerson` is the person whose profile shows: the picked one while they match,
otherwise the first match. Filter the picked person out and the first match shows; bring them back and they show again.
`PersonSelected` and `PersonSelectedCommand` report picks only, never the fallback, and not a second pick of the same
person. `SelectPerson` shows someone from code without raising the event.

```csharp
directory.PersonSelected += (_, e) => navigator.Open($"/people/{e.Person.Id}");
directory.SelectedTeam = "Engineering";
directory.SelectPerson("jonas");
```

<Demo name="BoundState" />

### Nobody matches

When nobody matches, the list says so with the search text and offers Clear filters, which empties the search and shows
every team again (the pick is kept); the profile area shows a short hint instead of a person. `ClearFilters()` does the
same from code.

### Copying an email address

The copy button copies the shown person's address and then reads Copied (with a check) or Retry for `CopyResetDelay`
(1.8 seconds) before it rests again; a status message tells screen readers how it went. By default it writes to the
system clipboard. Set `CopyAsync` to copy another way (a remote session, an audit call): an exception shows Retry, and
`EmailCopied` reports every attempt with the person, the address and the error. The address itself is a mail link;
`EmailLinkClicked` fires first, and setting `Handled` stops the mailto: link from opening.

<Demo name="CopyFailure" />

### Narrow containers

The block reads its own width, not the window's. At 640 or less the profile tightens and the copy button shows only its
glyph; at 500 or less the profile moves under the list and the button shows its label again; at 400 or less the search box
takes its own line under the title and the team counts are hidden.

<Demo name="Narrow" />

### Framed or flat

`IsFramed` (the default) draws a card with a 1px border and a 16 radius. Set it to false to place the directory flat on a
page or in a pane. `IsSearchVisible` and `IsFilterVisible` hide the search box and the team buttons; `Labels` takes a
`TeamDirectoryLabels` with every word and screen reader text, to translate or reword them.

<Demo name="Flat" />

## API reference

<!-- api: TeamDirectory -->

<!-- api: TeamDirectoryPerson -->

<!-- api: TeamDirectoryLabels -->

<!-- api: TeamDirectoryPersonSelectedEventArgs -->

<!-- api: TeamDirectoryEmailCopiedEventArgs -->

<!-- api: TeamDirectoryPersonEventArgs -->

## Accessibility

- **Tab** goes from the search box (and its clear button while there is text) to each team button, each row, the email
  link and the copy button. Every row and every team button is its own stop.
- **Enter** or **Space** presses a team button, picks a row, or presses the copy button.
- **Escape** in the search box empties it and keeps the focus there; with an empty box it is left to the page.
- The clear button empties the search and puts the focus back in the box. Clear filters moves the focus to the search box
  when it had it, instead of dropping it with the message.
- The team buttons and rows are toggle buttons: screen readers hear the pressed team and the row whose profile shows as
  pressed. The team buttons form a named group ("Team filter") and each reads its team and total ("Design, 2"); the list
  is named ("Directory").
- A row reads the name and the role; its avatar reads the name and whether the person is online or away.
- The profile area is a polite live region, so the new person is read when it changes; the profile is named with the
  name and role.
- The copy button is named "Copy email address" and becomes "Email address copied" after a copy; the outcome is also
  announced, and the announcement is cleared when the button rests.
- Every word and name is in `Labels`.

## Motion

| What | Full | Reduced | Off |
|---|---|---|---|
| Team pill moving to the pressed button | Spring (visual duration .42, no bounce), position and width | Jumps | Jumps |
| Row highlight moving to the shown row | Same spring | Jumps | Jumps |
| Rows that stay when the matches change | Glide to their new place on the same spring | Jump | Jump |
| Rows that leave | Fade out where they were in 170ms, taking no space | Removed at once | Removed at once |
| Rows that arrive | Fade in in place in 150ms | Appear | Appear |
| Profile change | Old fades out in 180ms; new fades in over 220ms and rises 4px | Swaps | Swaps |
| Copy confirmation | Glyphs cross-fade in 160ms and scale on a 580ms spring; colors change at once | Colors and glyph at once | At once |
| Hover and focus colors | 160ms | At once | At once |

An interrupted glide continues from where it is drawn with its speed; an interrupted profile fades out from where it
reached, so a quick run of picks never jumps.

## Related

- `Avatar`: the portraits with initials and the availability dot.
- `SearchField` and `CopyButton`: the standalone search input and copy button.
- `GlidingIndicator`: the highlight that glides between items.
