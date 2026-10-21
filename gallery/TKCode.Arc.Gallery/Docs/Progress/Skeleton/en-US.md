---
title: Skeleton
summary: Reserve space while content is still loading.
---

<Demo name="Profile" />

## When to use

- A profile, comment or card whose shape is known while its data is still loading.
- Content that should swap in place without the layout jumping when it arrives.

## When not to use

- Work with a measurable amount of progress: use `Progress`.
- A loaded page with no content to show: use `EmptyState`.
- An AI thinking state or a status line: use `TextShimmer`.

## Usage

A skeleton shows its placeholder while `Content` is null. Set `Lines` for the number of text lines and
`IsAvatarVisible` for a round avatar placeholder in front of them.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Skeleton IsAvatarVisible="True" Lines="3" />
</UserControl>
```

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

### Accessible name

`Label` names the placeholder while it is loading. The default is "Loading content".

```xml
<arc:Skeleton Label="Loading comments" Lines="5" />
```

```csharp
skeleton.IsLoading = false; // shows Content; the placeholder is hidden
```

## API reference

<!-- api: Skeleton -->

## Accessibility

- `Label` is the accessible name while the placeholder shows. The default is "Loading content".
- The placeholder is a live region with Polite announcements, and its status reads "Loading".
- The placeholder shapes are not read. Give the real content its own labels once it loads.
- The control is not focusable and has no keyboard interaction.

## Motion

- The text lines and the avatar pulse from top to bottom in a calm wave. The wave runs about 20 seconds and then stops.
- When loading ends, the placeholder fades out, the content rises 4 px into place and the height springs to the content.
- Reduced motion stops the pulse, swaps the placeholder for the content with a short fade and does not animate the height.

## Related

- `Progress` when the amount of work is known and can be reported.
- `EmptyState` when loading is done and there is nothing to show.
- `TextShimmer` for an inline status or an AI thinking state.
