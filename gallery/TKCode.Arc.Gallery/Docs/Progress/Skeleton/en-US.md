---
title: Skeleton
summary: Reserve space while content is still loading.
---

<Demo name="Profile" />

## When to use

`Skeleton` holds the place of a block of content while its data loads. Its placeholder has the shape of the content it
stands for, an avatar and a number of text lines, so the page keeps its layout when the data arrives. Use it in the
regions of a screen that load on their own: a profile header, a comment list or an activity feed. The content replaces
the placeholder in the same place, so the person does not see the page jump.

- **Profile and account headers**: a user card in a SaaS admin console while the account record loads. The avatar and
  two text lines mark where the name and the role will appear.
- **Comment and activity feeds**: a team thread or an audit log that fetches its newest entries after the page opens.
  Five placeholder entries show the shape of the feed without implying a count.
- **Invoice and order details**: the summary block of an order page in e-commerce or billing, while line items and
  totals load from a separate service. Reserving the space keeps the totals from moving under the cursor.
- **Dashboard cards whose data arrives later**: a revenue card or a customer list in an analytics dashboard, where the
  card frame is already on screen and only its body is waiting for a query.
- **Content that swaps in place**: any region whose final height is known from its design, so the change from loading to
  loaded only fills the space that was held.

## When not to use

- **Work with a measurable amount of progress**: use `Progress` when the job reports how much is done, such as an upload
  or an import with a known row count. A skeleton says that content is coming, not how far along it is.
- **A loaded page with no content to show**: use `EmptyState`, which explains why the list is empty and offers the next
  action.
- **An inline status or an AI thinking line**: use `TextShimmer` for a short line of text that says the system is
  working, such as "Thinking through the invoice". A skeleton is for a block of content, not a sentence.
- **A small action that is waiting for a server reply**: use a button in its busy state. A skeleton is for a region of
  content, and a single button or field does not need a placeholder.
- **Grids, charts and media with their own shape**: draw the shape of the chart or image in a placeholder of your own.
  `Skeleton` draws only text lines and an avatar, so it cannot match a plot or a thumbnail.

## Usage

A skeleton shows its placeholder while `Content` is null. Set `Lines` for the number of text lines and
`IsAvatarVisible` for a round avatar placeholder in front of them.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Skeleton IsAvatarVisible="True" Lines="3" />
</UserControl>
```

`Skeleton` derives from Avalonia's `ContentControl`, so `Content`, `ContentTemplate` and the content alignment
properties work as they do on any content control. The placeholder and the content share one frame: the placeholder
fades out and the content takes its place inside that frame, so the control is the same element before and after the
load. The control stretches to the width of its slot by default, and its text lines are a share of that width, so the
placeholder follows the layout around it.

### Anatomy

1. **Avatar placeholder** (`IsAvatarVisible`): a circle 44 px in diameter, the medium control size, drawn in the block
   color. It appears only when the property is on.
2. **Text lines** (`Lines`): one to six rounded bars in the block color, 12 px tall, with a 12 px gap between them. The
   first line is 16 px tall and 48% of the width. The last line is 72% of the width, and the lines between are full
   width.
3. **Block color**: a mix of the strong border color into the surface color, so the placeholder reads as a quiet
   neutral in both themes and does not depend on the accent.
4. **Spacing above the blocks**: 4 px above the first block, so the placeholder does not touch the top edge of its
   slot.
5. **Content frame** (`Content`): the loaded content, which rises 4 px into place when `IsLoading` becomes false. The
   control then takes the height of the content.

The block corners are fully rounded. Each block's corner radius is the control radius clamped to half its height, so the
16 px first line and the 12 px lines keep round ends.

### Matching the layout

Set `Lines` and `IsAvatarVisible` to match what the content will look like, not to fill the slot. A profile header
with a name and a role is an avatar and two lines. A comment is an avatar and three lines. Match the count so the
height change on load is small, and the page does not move when the content arrives.

`Lines` is clamped to 1 to 6. A value above 6 is reduced to 6, and a value below 1 is raised to 1. Use one to three lines
for most cards and no more than six for long text blocks.

### Comment placeholder

`Lines` is clamped to 1 to 6. The first line is shorter and taller, and the last line ends at 72% of the width.

<Demo name="Lines" />

### Loaded content

Set `Content` to the real view and bind `IsLoading` to the loading state. When `IsLoading` becomes false, the
placeholder fades out, the content rises into place and the height changes smoothly.

<Demo name="IntoContent" />

```xml
<arc:Skeleton IsAvatarVisible="True"
              Lines="2"
              IsLoading="{Binding IsLoadingProfile}">
    <StackPanel Spacing="8">
        <TextBlock Text="{Binding UserName}" />
        <TextBlock Text="{Binding Bio}" TextWrapping="Wrap" />
    </StackPanel>
</arc:Skeleton>
```

`IsLoading` defaults to true. A skeleton with `Content` shows the content only when `IsLoading` is false, so a page that
sets `Content` and forgets `IsLoading` keeps showing the placeholder. With no content the placeholder always shows, even
when `IsLoading` is false.

### Binding to a view model

In an MVVM app the view model owns the loading state and the data. With CommunityToolkit.Mvvm, an `ObservableObject`
holds the flag and the values, and a `[RelayCommand]` starts the load.

```csharp
public interface IProfileService
{
    Task<UserProfile> GetCurrentAsync();
}

public partial class ProfileViewModel : ObservableObject
{
    private readonly IProfileService _profiles;

    [ObservableProperty]
    private bool _isLoadingProfile = true;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _bio = string.Empty;

    public ProfileViewModel(IProfileService profiles) => _profiles = profiles;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoadingProfile = true;
        var profile = await _profiles.GetCurrentAsync();
        UserName = profile.DisplayName;
        Bio = profile.Bio;
        IsLoadingProfile = false;
    }
}
```

```xml
<arc:Skeleton IsAvatarVisible="True"
              Lines="2"
              IsLoading="{Binding IsLoadingProfile, Mode=OneWay}">
    <StackPanel Spacing="8">
        <TextBlock Text="{Binding UserName}" />
        <TextBlock Text="{Binding Bio}" TextWrapping="Wrap" />
    </StackPanel>
</arc:Skeleton>
```

Bind `IsLoading` one way. The control never changes it, so the view model stays the only owner of the state. Set the flag
to false only after the values are assigned, so the content never shows empty text. A failed load should also set the
flag to false and show an error message in the content, because a skeleton that never resolves tells the person nothing.

### Example: Activity feed while the next entry loads

An account page shows its recent activity. The first entry has loaded, and the second is still waiting for its author
and text. The placeholder keeps the second entry at the height of a real entry, so the divider and the entries below
stay where they are. The copy around the feed names the workspace, so the page reads as finished even while one entry
is still loading.

![An account activity list with one loaded entry and a skeleton entry below it](images/ActivityFeed.png)

```xml
<Border Width="680"
        Padding="28"
        CornerRadius="12"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1">
    <StackPanel Spacing="20">
        <StackPanel Spacing="4">
            <TextBlock Text="Account activity"
                       FontSize="18"
                       FontWeight="SemiBold"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Latest changes in the Northwind workspace"
                       FontSize="13"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
        <StackPanel Orientation="Horizontal" Spacing="12">
            <arc:Avatar DisplayName="Maya Chen" Size="Medium" />
            <StackPanel Spacing="2">
                <TextBlock Text="Maya Chen"
                           FontWeight="Medium"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Changed the billing address on inv_2026_0412"
                           FontSize="13"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            </StackPanel>
        </StackPanel>
        <Border Height="1" Background="{arc:ArcDesignTokenResource Border}" />
        <arc:Skeleton IsAvatarVisible="True" Lines="2" />
    </StackPanel>
</Border>
```

The loaded entry uses the same avatar size as the placeholder, so the two rows line up. The placeholder has two lines
because a real activity entry is one name and one sentence. The divider sits between entries rather than inside the
skeleton, so it stays when the entry loads.

### Content guidelines

A skeleton has no text of its own on screen, so its words are the accessible name and the words around it. Name the
loading region with what it loads, in sentence case, and keep the surrounding copy specific. Do not put "Loading" in the
visible heading of the region, because the heading stays on screen after the content arrives.

| Instead of | Write |
|---|---|
| `Loading content` (the default name for a comments list) | `Loading comments` |
| `Please wait...` as a visible line under the placeholder | Nothing visible; the name carries the state for assistive technology |
| `Loading` as the section heading | The section heading, such as `Recent activity`, with the skeleton beneath it |
| `Error loading data` as the only text | `Could not load the billing history. Try again`, with a retry button |

Keep `Label` short and specific. A label such as "Loading invoice summary" tells a screen reader which region is
busy, which "Loading" alone does not.

### Best practices

**Do**

- Match `Lines` and `IsAvatarVisible` to the loaded content, because the height change on load is then small and the
  page does not jump.
- Give each independent region its own skeleton, so a slow query does not hold back the rest of the page.
- Set `IsLoading` to false after the view model has assigned the values, so the content never shows empty text.
- Show an error in the content area when a load fails, because the placeholder otherwise stays on screen with no
  explanation.

**Avoid**

- Using a skeleton for work with a known amount of progress, because a bar that reports the share of work done is more
  useful than a placeholder. Use `Progress` for those jobs.
- Rendering hundreds of skeletons at once, because each block is drawn and animated, which costs time on a long list.
  Show a few placeholder rows and let the list fill in as the data arrives.
- Placing a skeleton over a shape it cannot draw, such as a chart or a thumbnail, because the placeholder then promises a
  layout that the content does not have.
- Leaving `IsLoading` at its default of true when the page sets `Content` for a load that has already finished, because
  the content stays hidden behind the placeholder.

### Common questions

**Why does my content not show after I set `Content`?** `IsLoading` defaults to true, and the content shows only when it
is false. Bind `IsLoading` to your loading flag, or set it to false when the data is ready.

**Does the placeholder stop after a while?** The pulse runs about 20 seconds and then rests. The placeholder itself stays
on screen until `IsLoading` becomes false or `Content` replaces it. Show a status line if the load takes longer than the
pulse.

**Can I use `Skeleton` without content at all?** Yes. With `Content` null the placeholder always shows, and so does a
control with `IsLoading` set to false, which is useful for a region that is decorative while its data is not yet
connected.

**How do I draw a skeleton for a chart or an image?** `Skeleton` draws only text lines and an avatar. Build the shape of
the chart or the image in your own control or a plain border, and show it with the same fade you use for the content.

## API reference

<!-- api: Skeleton -->

## Accessibility

- `Label` is the accessible name while the placeholder shows. The default is "Loading content".
- The placeholder is a live region with Polite announcements, and its status reads "Loading".
- The placeholder shapes are not read. Give the real content its own labels once it loads.
- The control is not focusable and has no keyboard interaction.
- When the content is shown, the name and the status are cleared, so the loaded content is read as the page's own text.

### Keyboard

`Skeleton` takes no focus and has no keyboard interaction. Loading completes without moving the focus, so a field or a
button that the person was using keeps its focus when the content arrives.

## Motion

- The text lines and the avatar pulse from top to bottom in a calm wave. The wave runs about 20 seconds and then stops.
- When loading ends, the placeholder fades out, the content rises 4 px into place and the height springs to the content.
- Reduced motion stops the pulse, swaps the placeholder for the content with a short fade and does not animate the height.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Placeholder pulse | Each block pulses in turn, 90 ms after the one above, on a 1.8 s cycle, 11 times (about 20 s), then rests | Stops | Stops |
| Loading ends | The placeholder fades out quickly and the content rises 4 px into place | A short fade between the placeholder and the content | Immediate |
| Height change | The height springs from the placeholder to the content | Not animated | Immediate |

### Using motion with restraint

The pulse is a finite wave, so a long load stops moving after about 20 seconds and the placeholder stays on screen. Do
not add a second moving element beside a skeleton, such as a shimmering banner or a spinner, because the page then has
two signs of loading that compete. A load that takes longer than the wave should show a status line that says what the
page is waiting for, and the placeholder can stay as it is. Reduced motion needs no extra code: the pulse stops and the
content swaps with a fade.

## Related

- `Progress` when the amount of work is known and can be reported.
- `EmptyState` when loading is done and there is nothing to show.
- `TextShimmer` for an inline status or an AI thinking state.
- `Avatar` for the loaded avatar that the placeholder stands for.

| Component | Choose it when |
|---|---|
| `Progress` | The amount of work is known and can be reported as a share |
| `EmptyState` | Loading has finished and there is no content to show |
| `TextShimmer` | A short line of text says the system is working |
| `Avatar` | The loaded identity is shown in the same place as the placeholder |
