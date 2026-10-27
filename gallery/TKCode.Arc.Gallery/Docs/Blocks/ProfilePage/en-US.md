---
title: Profile page
summary: A person's profile as one page: a cover and avatar, an about column with stats, Activity and Projects tabs, and an edit sheet whose saved fields glow once.
---

<Demo name="Preview" />

## When to use

`ProfilePage` is the screen a person lands on when they open their own profile or someone else's. A wide cover sits on top
with the round avatar riding its bottom edge; the name, handle and headline follow, with Share and Edit profile on the
right. Underneath, a narrow about column holds the bio, the location, the website, when the person joined and up to three
numbers, and two tabs fill the rest: Activity, a grouped list of what the person did recently, and Projects, a grid of
cards that visitors can star.

Edit profile slides a sheet in from the right inside the block. It edits the name, headline, location, website and bio,
checks them, and on save writes the new values back to the page, where each field that changed glows once so the reader
sees what moved.

- **A member's own profile in a product.** People see what others see and fix their details in place, without a separate
  settings page.
- **A team member's page.** Turn editing off (`IsEditable="False"`) and the same layout becomes a read-only page about a
  colleague, with their recent work and projects.
- **A creator or portfolio page.** The project cards carry a cover, the collaborators and a star count, enough for a
  public showcase.

## When not to use

- **Account settings.** Passwords, security, notifications and billing belong in `SettingsPage`, `SecuritySettings` and
  `BillingOverview`. This block edits only the public details.
- **Many people at once.** Use the Team directory block or a table to browse or search a team; this block is about one person.
- **A marketing team section.** A landing page that introduces a team needs portraits and short bios in a grid, not tabs
  and an edit sheet.
- **Long feeds.** The Activity tab is a short summary (a handful of rows per group). Use a timeline or a paged list for an
  endless history.

## Usage

The block derives from Avalonia's `TemplatedControl`. Give it a `ProfileDetails` and fill the three collections; the
portraits and covers are yours (the block shows initials when there is no portrait, and the muted surface or your
`CoverBrush` when there is no cover).

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ProfilePage ProfileUrl="https://fieldnote.app/@rowan">
        <arc:ProfilePage.Profile>
            <arc:ProfileDetails Name="Rowan Ellery"
                                Handle="rowan"
                                Headline="Product designer at Fieldnote"
                                Location="Bergen, Norway"
                                Website="rowanellery.studio"
                                JoinedText="June 2020"
                                Bio="I design tools for field researchers and care about honest defaults and quiet motion." />
        </arc:ProfilePage.Profile>
        <arc:ProfilePage.Stats>
            <arc:ProfileStat Label="Followers" Value="3107" />
            <arc:ProfileStat Label="Following" Value="212" />
            <arc:ProfileStat Label="Shipped" Value="41" />
        </arc:ProfilePage.Stats>
        <arc:ProfilePage.Activity>
            <arc:ProfileActivity Kind="Shipped" Action="shipped" Target="Color tokens v3" TimeText="3h"
                                 Group="This week" Detail="New contrast pairs for charts and a calmer dark palette." />
            <arc:ProfileActivity Kind="Reviewed" Action="approved" Target="Compact table mode" TimeText="Wed" Group="This week" />
        </arc:ProfilePage.Activity>
    </arc:ProfilePage>
</UserControl>
```

### Anatomy

- **Cover**: 240 high on a wide page (200 below 1000, 128 below 640). `ProfileDetails.Cover` is drawn over
  `ProfileDetails.CoverBrush`.
- **Identity**: the 104 avatar with a 4px ring in the surface color, half over the cover; Share and Edit profile on the
  right; the name (28px display type) and "@handle · headline" under it.
- **About**: the bio as one paragraph, then location, website (a link that opens in the browser) and "Joined …", each line
  hidden while its value is empty, then the stats in three equal columns under a hairline.
- **Tabs**: Activity and Projects with their counts; a 2px underline marks the chosen one. Below 760 the about block moves
  above the tabs.
- **Edit sheet**: 420 wide on the right of the block, over a scrim; it covers the visible part of the block wherever the
  page is scrolled. Below 420 it spans the block.

### Activity

Each `ProfileActivity` reads "{first name} {Action} **{Target}**", with an optional `Detail` line and a `TimeText` at the
end. The first name is the first word of `ProfileDetails.Name`, so the rows follow a rename. Rows are grouped under their
`Group` heading in the order the groups first appear. `Kind` picks the icon: a green rocket for `Shipped`, an amber star
for `Starred`, and muted icons for `Commented`, `Created` and `Reviewed`.

### Projects and stars

Each `ProfileProject` is a card with a 96px cover, the title and `Visibility` (Public or Private), a description, its
`Collaborators` as overlapping faces, `UpdatedText` and a star button. Pressing the star toggles `IsStarred` and changes
`StarCount` by one on the item itself (both are two-way), then raises `StarChanged` with the project, the new state and
the count, so your code can tell the server. Cards sit in two columns, one below 560.

### Controlling the tab

`Tab` is two-way. Bind it to keep the tab in your own state (for example in the address), or set it from other buttons;
`TabChanged` reports every change with the old and new tab, whether it came from a tab, the arrow keys or a binding.

<Demo name="ControlledTab" />

### Saving

`SaveHandler` receives the edited `ProfileDetails` (trimmed, with the handle, pictures and joined text carried over) and
returns a task. While it runs the Save button shows a spinner and "Saving"; the fields, Cancel and the close button stay
usable. When it completes, `Profile` is replaced by the new object, the sheet closes, each field whose trimmed value
changed glows once and `Saved` reports the previous profile, the new one and `ChangedFields`. Without a handler the block
simulates a save that succeeds after `SimulatedSaveDelay` (700ms).

Throw from the handler to keep the sheet open: the exception's message appears in a red strip above the sheet's buttons,
is announced, and `SaveFailed` is raised. The draft stays as it was, so the person can try again.

```csharp
var http = new HttpClient();
page.SaveHandler = async profile =>
{
    var json = JsonSerializer.Serialize(new { profile.Name, profile.Headline, profile.Location, profile.Website, profile.Bio });
    var body = new StringContent(json, Encoding.UTF8, "application/json");
    var response = await http.PutAsync("https://api.example.com/me", body);
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException("We could not save your profile. Try again in a moment.");
    }
};
```

<Demo name="SaveError" />

Closing the sheet while a save runs does not cancel it: the values are written back when it completes, and a failure is
announced and reported through `SaveFailed`.

### Rules in the sheet

- **Name** is required: it must contain something other than spaces.
- **Website** is optional. When filled, it must look like a web address: an optional lower-case `http://` or `https://`,
  at least two dot-separated parts of letters, digits, `-` or `_` (no port), and an optional path that starts with `/`
  and has no spaces. `example.com`, `www.example.com/work` and `https://example.com/p?q=1` pass; `example`,
  `localhost:3000` and `ftp://example.com` do not.
- **Bio** stops at `BioMaxLength` characters (200); the counter beside its label shows how many are left.

Errors stay hidden until the first save attempt and then follow every keystroke. Save is disabled until the draft differs
from the profile.

### Share

Share copies `ProfileUrl` (or "https://example.com/@{handle}" when it is not set) and the button reads Copied or Failed for
`ShareFeedbackDuration` (1800ms) without changing width. `CopyHandler` replaces the system clipboard, for example to add
tracking or to test the failure; returning false or throwing shows Failed. `Shared` reports the result.

### Read-only profiles

With `IsEditable="False"` the Edit profile button is gone and `OpenEditor` does nothing; everything else, Share and the
stars included, still works.

<Demo name="ReadOnly" />

### Narrow containers and height

The layout follows the block's own width, not the window's: the cover shrinks below 1000, the columns stack below 760, the
header tightens below 640 (a 128 cover, an 84 avatar and a 22px name), the cards go to one column below 560 and the sheet's
Location and Website fields stack below 420. The block is `PageHeight` tall (760) or less when its parent offers less, and
scrolls inside. `IsFramed="False"` removes the border and the rounded corners for a full-window page.

<Demo name="Narrow" />

### Texts

Every word the block shows or announces is in `Labels`, a `ProfilePageLabels` object with English defaults. Replace the
whole object to translate it; texts that include values (the bio counter, "Joined …", the star button names) are
functions so the word order can change.

## API reference

<!-- api: ProfilePage -->

<!-- api: ProfileDetails -->

<!-- api: ProfileStat -->

<!-- api: ProfileActivity -->

<!-- api: ProfileProject -->

<!-- api: ProfileCollaborator -->

<!-- api: ProfilePageLabels -->

## Accessibility

Keyboard:

- **Tab** moves through Share, Edit profile, the website link, the chosen tab and the star buttons of the Projects tab.
- **Left / Right** on a tab move to the other tab and show it at once, wrapping around.
- **Enter / Space** press the focused button or star.
- In the sheet the focus starts in Name and **Tab / Shift+Tab** stay inside it. **Enter** in a one-line field saves; in
  the bio it starts a new line. **Escape** closes the sheet without saving, and the focus returns to Edit profile.

Screen readers:

- The about column, the tab column and the tab row are named ("About", "Profile content", "Profile sections"). The tabs
  are tab items; only the chosen one is a tab stop. Icons and the dot between the handle and the headline are hidden from
  screen readers.
- The Share button is a polite live region: its new name (Copied, Failed) is read, and the result is announced.
- Each star button is named with the project and its count ("Star Signup study, 34 stars", "Unstar …" once starred).
- The sheet is named by its title. Each field is named by its label; an error is given as the field's help text. A failed
  save is shown in an assertive live region and announced; a successful one is announced as "Profile saved".
- Keyboard focus rings are 2px, outside the element; focus moved by the pointer shows none.

## Motion

- **Tabs**: the underline glides to the chosen tab, position and width together, on a critically damped spring (Smooth,
  about 0.9 s). The old content fades out over 120ms, then the new one fades in and rises 6px over 240ms; the panel's
  height follows on the same spring once the new content is in. Turning back mid-way fades the old content back in.
- **Edit sheet**: slides in from its full width on the Smooth spring while the scrim fades in over 240ms; it leaves on a
  faster critically damped spring (about 250ms).
- **Saved fields**: each changed field gets an accent-subtle background reaching 3px past it, held for 480ms and faded
  over 1120ms.
- **Stars**: starring pops the star from 60% on a quick spring; the count changes at once. Unstarring does not animate.
- **Labels**: Share and Save swap their labels with a short fade, a 2px blur and a 3px rise, without changing width.
- **Reduced motion**: the underline and the content swap at once, the sheet fades in (160ms) and out instead of sliding,
  labels swap at once and the star does not pop. The save glow is kept (nothing moves). **Off** applies every change at
  once and skips the glow.

## Related

- `SettingsPage` and `SecuritySettings` for account settings.
- `Avatar` and `AvatarGroup` for portraits elsewhere.
- `Tabs` for tabs outside this block.
- `Drawer` for a window-level side panel.
