---
title: Rating
summary: A simple, accessible way to capture a numeric opinion.
---

<Demo name="Feedback" />

## When to use

A rating asks for a score on a short scale and shows what the score means. Pointing at a star previews the value, a
press or a drag commits it, and a word beside the stars rolls to the new meaning. The same control also shows a score
that nobody can change, such as an average.

- **A review or a feedback form**: one question such as "Overall experience", answered in one tap. The default words,
  Poor to Excellent, tell the person what each star stands for.
- **An average score on a product or a listing**: `IsReadOnly` with `AllowHalf` shows 4.5 stars next to the number of
  reviews, without any input behavior.
- **A support survey after a closed ticket**: `AllowClear="False"` keeps an answer once one is given, when the form
  needs a score to continue.
- **A scale with its own wording**: `ValueLabels` gives each star a word of your own, such as Too slow to Very fast.

## When not to use

- **A continuous value**: use `Slider`, which moves over a range instead of a few steps.
- **A quick reaction to a post or a comment**: use `Reactions`, which counts emoji from many people.
- **Named choices that are not a scale**: use `RadioGroup`, where each option has its own label and description.
- **A choice between views**: use `SegmentedControl`, which switches the content beside it.

## Usage

Set `Label` and bind `Value`, which is 0 until the person chooses a star. `ValueChanged` fires when a person commits a
new value, not when your code sets one.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Rating Label="Overall experience" Value="{Binding Stars}" />
</UserControl>
```

### Half stars and read-only scores

`AllowHalf` moves the pointer, touch and arrow keys in half stars. `IsReadOnly` removes the input and leaves the score,
so the control takes no focus and can repeat in a long list at low cost: it draws its stars in one control and creates
its motion only when a star first moves.

<Demo name="Average" />

### Sizes

`Size` sets the star and its target together: 18 px stars in 28 px cells, 24 in 36 (the default) and 30 in 44.

<Demo name="Sizes" />

### Clearing, the scale and the disabled state

Choosing the current value again clears the rating, and so do Backspace and Delete. Set `AllowClear="False"` when an
answer must stay. `Maximum` sets the number of stars; a scale other than five whole stars reads "7 out of 10" unless
you give it `ValueLabels`.

<Demo name="Required" />

### Custom icons

`Icon` replaces the star with any shape drawn in a 24 × 24 box, such as `{x:Static arc:ArcIcons.Heart}` or path data;
the stroke, fill, colors and motion stay the same. `EmptyIcon` sets a different shape for the part of a step that is
not filled. Half values keep working with any shape: the left half of `Icon` is drawn over the right half of
`EmptyIcon`.

<Demo name="Icons" />

## API reference

<!-- api: Rating -->

## Accessibility

| Key | Action |
|---|---|
| Right, Up | Raises the value by one step |
| Left, Down | Lowers the value by one step |
| Home, End | Goes to the first step or to the last star |
| 1 to 9 | Chooses that star, when the scale has it |
| Space, Enter | Chooses the first step when nothing is rated, otherwise confirms the value |
| Backspace, Delete | Clears the rating, unless `AllowClear` is false |

The rating is one tab stop and reports itself as a range from 0 to `Maximum`, with `Label` as its name. Its help text
states the current value in words, such as "Great, 4 of 5 stars". A read-only rating takes no focus.

## Motion

The fill sweeps from star to star on the snappy spring, each one 35 ms after the last, and the committed value stays as
a faint mark while another one is previewed. A commit sends a small pop along the chosen stars, or only the last star
when commits come within 260 ms of each other. With reduced motion the stars change at once and only the words fade.

## Responsive behavior

The value text wraps under the stars when the row runs out of room, and it keeps the width of its longest word so
nothing beside it moves. On touch, the value follows the finger across the row and commits on release, and a drag past
the first star clears the rating. Use `Size="Large"` on a phone for 44 px targets.

## Related

| Component | Choose it when |
|---|---|
| `Slider` | The value is continuous or has many steps |
| `Reactions` | People react to content with emoji and counts |
| `RadioGroup` | The options are named choices, not a scale |
| `SegmentedControl` | The choice switches the view beside it |
