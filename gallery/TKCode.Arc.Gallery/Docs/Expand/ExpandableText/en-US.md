---
title: Expandable text
summary: Long text clamped to a few lines with a soft fade that glides open with Read more and closes with Show less, shown only when the text overflows.
---

## When to use

- Comments, reviews or profile bios that would make cards in a list uneven in height.
- Long text in a fixed-width column, where the reader can choose to read the rest.
- Text that is optional to read in full, such as a product description.

## When not to use

- Required text, such as legal terms or error details. Do not clamp it.
- A group of titled sections or questions: use `Accordion`.
- Structured content that is hidden behind a summary: use `ExpandableCard`.
- Text that must stay at a fixed height with a scrollbar: use `ScrollArea`.

## Usage

Set `Text` for the content. The text is clamped to `Lines` lines, which defaults to 3. The toggle appears only when
the text overflows the clamp.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ExpandableText Text="We moved our whole design team onto the new workspace in a single afternoon. The import kept every comment and version."
                        MaxWidth="520"
                        HorizontalAlignment="Left" />
</UserControl>
```

### Line count

`Lines` sets how many lines show while the text is collapsed. Use 2 lines for dense lists such as comments, and
3 or 4 lines for profiles and product descriptions.

```xml
<arc:ExpandableText Lines="2"
                    Text="{Binding CommentText}"
                    MaxWidth="420" />
```

### Labels

`MoreLabel` and `LessLabel` set the toggle text. The defaults are `Read more` and `Show less`.

```xml
<arc:ExpandableText MoreLabel="Show full bio"
                    LessLabel="Show less"
                    Lines="4"
                    Text="{Binding Bio}" />
```

### Binding the expanded state

`IsExpanded` is two-way. Bind it to a view model property, or set it from code. `ExpandedChanged` is raised when the
text opens or closes.

```xml
<arc:ExpandableText Text="{Binding Bio}"
                    IsExpanded="{Binding BioExpanded, Mode=TwoWay}" />
```

```csharp
bio.ExpandedChanged += (_, e) =>
{
    state.Text = e.IsExpanded ? "Expanded" : "Collapsed";
};

bio.IsExpanded = true;
```

## API reference

<!-- api: ExpandableText -->

## Accessibility

- The whole text is always in the accessibility tree. Screen readers read all of it without using the toggle.
- The toggle is a button with the expand and collapse pattern. Its name is the current label, `MoreLabel` or `LessLabel`.
- Links inside the clamped part can take keyboard focus. Focusing a link that is hidden below the clamp opens the text.
- The toggle works with Enter and Space.

## Motion

- The height, the fade over the last line and the chevron move together on one curve. They ease out and do not overshoot.
- Opening takes about half a second and closing about 0.4 seconds. Longer distances take slightly longer.
- The fade over the last line dissolves as the text opens. Lines that appear rise through a soft edge.
- A click during the motion reverses from the current height.
- Reduced motion opens and closes the text at once. When the text closes far down the page, the start of the text is
  brought back into view.

## Related

- `Accordion` for several titled sections that open in place.
- `ExpandableCard` for a summary card with hidden details.
- `ScrollArea` for content that must keep a fixed height.
