---
title: Avatar
summary: A compact identity marker for people and accounts.
---

<Demo name="Initials" />

## When to use

`Avatar` marks one person with a photo or with initials, and can show whether that person is online. It sits next to
a name in a page header, a record, a comment or a table row, so the person is recognizable before the text is read.
It is display only: it never takes focus, never opens anything and never writes a value back.

- **Owner and assignee fields** on a ticket, deal or invoice: an avatar beside "Assigned to" lets an operator spot the
  owner in a dense queue without reading each name.
- **Presence in a team directory or a support console**: a status dot beside the photo shows that an agent or a
  colleague is online, and `AvatarStatus.Offline` shows who has signed off. Use it where the product really knows
  presence.
- **The signed-in user in an admin header**: a `Medium` or `Large` avatar with the user's initials in the top bar of a
  SaaS console, where a profile photo may not exist yet.
- **Contact lists with missing photos**: a CRM list where many records have no picture. The initials take over when
  `Source` is empty or fails, so every row keeps a stable mark.
- **Activity and audit logs** where each entry names an actor: a `Small` avatar makes the actor scannable across
  hundreds of rows without taking the space of a table column.

## When not to use

- **Several people in a small space with a count of the rest**: use `AvatarGroup`, which folds the overflow into a
  "+N" chip.
- **An avatar that opens account actions** such as sign out or switch workspace: use `UserMenu`, which owns the menu,
  focus and the keyboard model.
- **A list where names, roles and dates must be compared**: use `SortableDataTable`. An avatar alone does not carry
  the text a reader needs.
- **A conversation where each message has an author and a time**: use `ChatThread` or `CommentThread`, which lay out
  the author beside the message text.
- **A status or category that is not a person**, such as Live, Draft or Failed: use `Badge`.
- **Editing a person's name or email**: use `Input`. The avatar only shows the value; it has no text to edit.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Avatar DisplayName="Ada Lovelace" />
</UserControl>
```

`Avatar` derives from Avalonia's `TemplatedControl`. Its frame uses the standard `Background`, `BorderBrush`,
`BorderThickness` and `CornerRadius` properties, so the theme's values can be overridden locally like any other
templated control. The size comes from `Size`: setting `Width` or `Height` on the instance replaces the diameter from
the theme, and the status dot and initials then no longer match the Arc scale. Prefer `Size`.

### Anatomy

1. **Frame** (the control itself): a circle with a 1 px `Border` outline on the `SurfaceMuted` fill. The corner
   radius is the pill radius, so the shape stays round at every size, and the content is clipped to it.
2. **Initials** (`Initials`, read only): centered text in the regular foreground color with a medium weight. The
   text is 10 px at `Small`; at `Medium`, `Large` and `XLarge` it follows the Arc type scale steps XS, SM and XL.
3. **Photo** (`Source`): an async image that fills the circle. It is shown only while `Source` is set and has not
   failed; while it loads, the frame stays empty.
4. **Status dot** (`Status`): a filled circle at the lower right with a surface-colored ring (1.5 px at `Small`,
   2 px at the other sizes) and a faint 1 px halo in the border color. `Online` uses the success color, `Offline` the
   muted text color, and `None` removes the dot entirely.

### Photo and fallback

`Source` loads a photo. The photo is cropped to fill the circle. A cached photo appears at once; a photo that is
still downloading after the avatar is on screen fades in from a 4 px blur, so the frame does not flash.

<Demo name="Photos" />

When the photo fails, the avatar falls back to `Initials` and keeps them. The failed address is remembered for that
`Source` value, so the same bad link is not requested again each time the avatar redraws. Assign a new value to
`Source` to try again. A photo is not required for the layout: every size reserves the same frame whether or not a
photo is present.

### Sizes

`Size` sets the diameter: `Small` is 28 px, `Medium` (the default) is 36 px, `Large` is 48 px and `XLarge` is 88 px.
The size does not follow the window; it is the same at every width.

| Size | Diameter | Initials text | Status dot | Use it for |
|---|---|---|---|---|
| `Small` | 28 px | 10 px | 8 px | Table rows, activity logs, dense lists |
| `Medium` | 36 px | Arc XS | 10 px | Default for headers, cards and form summaries |
| `Large` | 48 px | Arc SM | 12 px | Roster panels, profile cards |
| `XLarge` | 88 px | Arc XL | 14 px | Account and profile page headers |

```xml
<arc:Avatar DisplayName="Ada Lovelace" Size="Small" />
<arc:Avatar DisplayName="Grace Hopper" Size="Medium" />
<arc:Avatar DisplayName="Alan Turing" Size="Large" />
<arc:Avatar DisplayName="Katherine Johnson" Size="XLarge" />
```

Keep one size for each list or group of related avatars, so rows keep the same height and the dots line up. In a
dense table, `Small` with its 8 px dot is still readable because the dot has a ring that separates it from the
photo.

### Status

`Status` shows an online or offline dot at the lower right. `AvatarStatus.None`, the default, hides it.

<Demo name="Status" />

Show a status only when the product knows the presence: a person in the current room, an on-call engineer, an agent
who is taking chats. Do not invent `Offline` for a person whose presence is unknown; leave the status at `None`.
`Offline` is a neutral state, not an error, so it uses the muted text color and never the danger color. The status
also changes the accessible name (see Accessibility), so the dot is never the only signal.

### Initials

The initials are the first letters of the first two words of `DisplayName`, in upper case. Extra spaces are ignored,
and a name with one word gives one letter.

| `DisplayName` | `Initials` |
|---|---|
| `Ada Lovelace` | `AL` |
| `  grace   brewster hopper ` | `GB` |
| Plato | `P` |
| empty or white space | empty |

`Initials` is read only and updates whenever `DisplayName` changes. `Avatar.ComputeInitials` runs the same rule for
any string, for example to build a label in code.

### Binding to a view model

`Avatar` takes its values one way. Bind `DisplayName`, `Source`, `Status` and `Size` from the view model; the avatar
never writes a value back, and it raises no events that a view model needs to handle.

```csharp
public partial class OnCallMemberViewModel : ObservableObject
{
    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private ImageSource? _photo;

    [ObservableProperty]
    private AvatarStatus _status = AvatarStatus.None;
}
```

```xml
<arc:Avatar DisplayName="{Binding DisplayName}"
            Source="{Binding Photo}"
            Status="{Binding Status}"
            Size="Large" />
```

Set these properties on the UI thread, because Avalonia properties must change there. When a presence service reports
a change on a background thread, post the update with `Dispatcher.UIThread.Post`. The view model creates the
`ImageSource`, for example from a bitmap that your own service loads; leave `Photo` null when there is no picture
and the initials show.

### Photos in lists and tables

A photo costs memory and decoding time for every row on screen. `Source` loads asynchronously and the photo is decoded
at the avatar's display size, so a large original does not cost more than the circle needs. Still, a list of
hundreds of rows should follow three rules:

- Use `Small` in long lists and reserve `Large` and `XLarge` for the one or two avatars that are the focus of a page.
- Keep the list virtualized, as the Avalonia `ListBox` and `DataGrid` do by default, so only the visible rows create
  avatars.
- Give records without a photo no `Source` at all, rather than an empty or placeholder image, so the initials are
  used without a failed request.

### Example: on-call roster in an incident console

An incident console lists the responders for a live outage. Each row has a `Large` avatar, the name, the role and a
status dot. The two responders who are online are green-dotted by the presence service; the third has `Offline` and
keeps the same frame so the names line up. The roster uses initials only because the incident tool has no photos for
these responders yet; a missing photo costs nothing and the layout does not change when one is added.

![On-call roster with two online responders, one offline responder and initials avatars](images/AvatarRoster.png)

```xml
<StackPanel Width="560" Spacing="16">
    <TextBlock Text="Incident response roster"
               FontSize="16"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <Border Padding="20"
            Background="{arc:ArcDesignTokenResource Surface}"
            BorderBrush="{arc:ArcDesignTokenResource Border}"
            BorderThickness="1"
            CornerRadius="12">
        <StackPanel Spacing="18">
            <Grid ColumnDefinitions="Auto,*,Auto">
                <arc:Avatar Grid.Column="0"
                            DisplayName="Maya Chen"
                            Size="Large"
                            Status="Online" />
                <StackPanel Grid.Column="1" Margin="14,0,0,0" Spacing="2" VerticalAlignment="Center">
                    <TextBlock Text="Maya Chen" FontWeight="Medium"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="Incident commander" FontSize="12"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                </StackPanel>
                <TextBlock Grid.Column="2" Text="Primary" FontSize="12"
                           VerticalAlignment="Center"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            </Grid>
            <Grid ColumnDefinitions="Auto,*,Auto">
                <arc:Avatar Grid.Column="0"
                            DisplayName="Noah Park"
                            Size="Large"
                            Status="Online" />
                <StackPanel Grid.Column="1" Margin="14,0,0,0" Spacing="2" VerticalAlignment="Center">
                    <TextBlock Text="Noah Park" FontWeight="Medium"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="Database on-call" FontSize="12"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                </StackPanel>
                <TextBlock Grid.Column="2" Text="Secondary" FontSize="12"
                           VerticalAlignment="Center"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            </Grid>
            <Grid ColumnDefinitions="Auto,*,Auto">
                <arc:Avatar Grid.Column="0"
                            DisplayName="Leah Fox"
                            Size="Large"
                            Status="Offline" />
                <StackPanel Grid.Column="1" Margin="14,0,0,0" Spacing="2" VerticalAlignment="Center">
                    <TextBlock Text="Leah Fox" FontWeight="Medium"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="Payments on-call, off shift until 18:00" FontSize="12"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                </StackPanel>
                <TextBlock Grid.Column="2" Text="Standby" FontSize="12"
                           VerticalAlignment="Center"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            </Grid>
        </StackPanel>
    </Border>
</StackPanel>
```

### Content guidelines

- **Display name**: the full name in the form people use in the product, such as "Maya Chen". The initials and the
  accessible name both come from it, so a short handle gives a poor result.
- **Name text**: keep titles, roles and abbreviations out of `DisplayName`. A prefix such as "Dr." changes the
  initials, and the role belongs in the text beside the avatar.
- **Status**: only the two words "online" and "offline" exist in the control. Put anything more specific, such as
  "away until 18:00", in the text next to the avatar.

| Instead of | Write |
|---|---|
| `DisplayName="mchen"` | `DisplayName="Maya Chen"`, so the initials read "MC" |
| `DisplayName="Dr. Maya Chen"` | `DisplayName="Maya Chen"`, so the initials read "MC" and not "DM" |
| `DisplayName="Maya Chen (away)"` | `DisplayName="Maya Chen"` and "Away until 18:00" in the row text |
| `DisplayName="Maya"` | `DisplayName="Maya Chen"`, two words give two letters |

### Best practices

**Do**

- Pass the full display name, so the initials and the accessible name both read correctly. A single word gives one
  letter, which a reader cannot tell apart from a sibling's.
- Use one size for each list and `Small` in tables and logs. Rows of equal height scan faster and keep the dots in a
  straight line.
- Set `Status` only from presence the product knows. A dot that is always gray says nothing and teaches people to
  ignore the control.
- Use `Source` only for photos the product actually has. Records without a photo keep the initials, with no broken
  image in the frame.

**Avoid**

- Using an avatar as a button or menu trigger. The control is not focusable and has no keyboard handling; wrap the
  account menu in `UserMenu`, which owns focus and the keys.
- Setting `Width` or `Height` locally to pick a size. It overrides the diameters of `Size`, so the status dot and the
  initials stop matching the Arc scale.
- Loading full-size photos into a long list. Hundreds of 88 px photos cost memory; `Small` with the initials costs
  almost nothing.
- Coloring an offline dot as an error. Offline is a neutral state; the danger color belongs to failures.
- Relying on the dot alone for presence in a text-only export or a screen reader. The status reaches assistive
  technology through the name, so put the presence in the text as well when it matters.

### Common questions

**Why does a row show no status dot?** The default `Status` is `AvatarStatus.None`, which hides the dot. Set
`Online` or `Offline` to show it.

**Why does my photo not appear, while the initials stay?** Either the photo is still downloading, which shows an
empty frame until it arrives, or it failed to load and the initials are kept. A failed address is remembered for that
`Source` value; assign a new value to `Source` to retry.

**Why do two Chinese names show one character?** Initials use the first character of each space-separated word, so a
name without spaces gives one character, such as "张" for "张三". `Avatar` has no initials override in this release.
When two characters are required for such names, show your own label in place of the avatar.

**How does a screen reader announce the avatar?** The accessible name is `DisplayName`, followed by the status when
there is one: "Maya Chen, online". Without a status the name is read alone. Always set `DisplayName`, because an empty
name leaves the avatar unlabeled.

**How do I load a new photo for the same person?** Assign the new `ImageSource` to `Source`. Assigning a value clears
the failure memory for the old one, so the new photo is loaded even if the previous address had failed.

## API reference

<!-- api: Avatar -->

## Accessibility

- The avatar is an image with the name `DisplayName`. A status adds it to the name, such as "Maya Chen, online".
- The photo and the initials are decorative and hidden from assistive technology, so the name is read once.
- The status dot is hidden; the status reaches assistive technology through the name.
- The avatar is not focusable and has no keyboard behavior: `Tab` skips it, and keyboard work belongs to the control
  that owns the action, such as `UserMenu` or the list the avatar sits in.
- A name with no status is read alone. An empty `DisplayName` leaves the avatar with no name, so always set it.
- Keep the presence in the surrounding text when it matters for a decision, such as "Offline, on call from 18:00".

## Motion

- A photo that is still loading fades in from a 4 px blur. A photo that is already cached shows at once.
- The initials fade in over 0.24 seconds when the photo fails or is removed and the initials return.
- The status dot pops in from 0.6 scale on a snappy spring and shrinks out when it leaves.
- Changing the status crossfades the dot color.
- Reduced motion switches the status dot at once and shows the photo without a fade. Off changes everything at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| A photo that is still loading arrives | Fades in from a 4 px blur | Shown at once, no blur or fade | Immediate |
| A cached photo is shown | Shown at once | Shown at once | Immediate |
| The initials return after a failed photo | Fade in over 0.24 s | Immediate | Immediate |
| A status dot appears | Pops in from 0.6 scale on a snappy spring | Immediate | Immediate |
| A status dot leaves | Shrinks out over 160 ms | Immediate | Immediate |
| Online and offline change color | The dot color crossfades | Immediate | Immediate |

## Related

- `AvatarGroup` for a stack of people with an overflow count.
- `UserMenu` when the avatar opens account actions.
- `Badge` for a status or category label that is not a person.
- The table below lists the other components that show people.

| Component | Choose it when |
|---|---|
| `AvatarGroup` | A short team or set of editors must fit in a header, a card or a table cell |
| `UserMenu` | The avatar opens sign out, profile or workspace actions |
| `SortableDataTable` | Names, roles and dates must be read and compared as columns |
| `ChatThread` / `CommentThread` | Each message needs its author and time beside the text |
| `Badge` | The label is a status or category, not a person |
| `Input` | The person's name or email must be typed or edited |
