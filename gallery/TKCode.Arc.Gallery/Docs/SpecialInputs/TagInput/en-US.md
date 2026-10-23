---
title: Tag input
summary: Turn short text values into removable tags.
---

<Demo name="Default" />

## When to use

`TagInput` turns short typed values into removable tags inside one form field. It fits forms where a person builds a
list of short strings and needs to see every entry at once: the labels on a support ticket, the recipients of a
scheduled report, the domains allowed to sign in to a workspace. The field draws its own label, description and error
row, so the list reads as a single form control next to the inputs around it.

- **Labels on a support ticket or a CRM contact**: an agent types `billing`, presses Enter, then `refund`. Tags are free
  text, so the set grows with the team's vocabulary instead of waiting for an admin to edit a list.
- **Recipients of a scheduled report**: a finance lead adds `cfo@northwind.com` and the other addresses one after
  another, or pastes a column copied from a spreadsheet. Pasted commas, new lines and tabs split into tags, and each
  address is visible before the report is saved.
- **Allowed domains in a single sign-on setup**: an administrator enters `acme.com` and `acme.co.uk`. Each domain
  stays on screen as its own tag, so a missing or misspelled one is easy to spot before the form is saved.
- **Keywords on a knowledge base article or a product listing**: the author adds several search terms. A duplicate is
  refused without regard to case, so Refund and refund cannot both be saved.
- **Capped lists in a plan form**: a plan that includes five integrations sets `MaxTags="5"`. The sixth add is refused
  with a shake and an announcement, so the limit is explained where the person is working.

## When not to use

- **Values picked from an existing catalog**: roles, plans, warehouses or regions that already exist as records: use
  `MultiSelect`, which chooses from a list, or `Combobox` when the list is long and needs search.
- **Facet filters on a results page**: a few toggles that narrow a table or a feed: use `ChipGroup`, which is built for
  choosing among filter facets.
- **A single value**: one email address, one name or one URL: use `Input`. A tag field with one tag is harder to read
  and validate than a single input.
- **Amounts and quantities**: a list of prices, weights or counts needs parsing and formatting: use `MoneyInput` or
  `NumberField`. `TagInput` stores plain strings and does no parsing.
- **Mentions in running text**: @people and #channel references inside a comment or a message: use `MentionInput`,
  which keeps each mention as one token inside the sentence.
- **Several lines of text**: notes, descriptions or pasted paragraphs: use `Textarea`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TagInput Label="Tags"
                  Placeholder="Add a tag"
                  Description="Press Enter or a comma to add a tag." />
</UserControl>
```

`TagInput` derives from Avalonia's `TemplatedControl`. Its frame is the shared `Field` and `FieldFrame` that the other
Arc fields use, so the label, the message rows, the focus halo and the invalid border match `Input`. Inside the frame,
the tags and a borderless text editor sit on wrapping rows. The editor is a text box, so the caret, selection, IME and
the clipboard behave as they do in `Input`; the control adds Enter, the comma, Backspace and the arrow keys for tags.

### Anatomy

1. **Label** (`Label`): the field label, with the same style as `Input` (14 px, medium weight, 8 px above the frame).
   It is also the accessible name of the editor. Hide it with `IsLabelVisible="False"` only when a visible heading
   already names the field.
2. **Frame** (`FieldFrame`): a 1 px border in the border color on the surface. Hover darkens the border only while the
   field is not focused and not invalid. Focus shows the 3 px focus halo. An error (`ErrorMessage` or `IsInvalid`)
   turns the border to the danger color. The frame has a minimum height of 44 px for one row of tags.
3. **Content**: 7 px padding on every side and 6 px between tags, the editor and wrapped rows. Each extra row adds
   34 px to the height. The height follows the rows on a smooth spring.
4. **Tag** (an item of `Value`): at least 28 px high, with 9 px of padding before the label and 4 px after it, inside a
   1 px pill border on the muted surface color. The label uses the body font at the small type size and the foreground
   color, with a 3 px gap before the remove button. A tag wider than the field is truncated with an ellipsis.
5. **Remove button**: a 22 px circle with a 14 px X glyph drawn at a 2 px stroke, in the secondary text color. Hover
   gives it the surface background. Keyboard focus draws a 1.5 px focus ring inside the circle. A press shrinks it to
   96%.
6. **Editor** (the draft): takes the rest of the line. It is at least 80 px wide, starts from a 100 px basis, is 28 px
   high and has 5 px of horizontal padding. When fewer than 100 px are left on a line, the editor moves to the next row.
7. **Placeholder** (`Placeholder`): "Add a tag" by default, drawn in the muted text color at a 12 px inset. It is shown
   only while there are no tags and no draft text.
8. **Description** (`Description`) and **error row** (`ErrorMessage`): helper copy and error copy under the frame, the
   same rows as `Input`. Non-empty `ErrorMessage` makes the field invalid.

`TagInput` has no `Size` property. Its height follows the number of rows, so let the tags wrap rather than shrink
the text.

### Initial tags

`Value` holds the tags as an `IReadOnlyList<string>`. Setting it replaces the whole list and raises `ValueChanged`;
`null` reads back as an empty list.

```csharp
var tags = new TagInput
{
    Label = "Tags",
    Value = ["design", "motion", "react"]
};
```

### Binding to a view model

In an MVVM app the view model owns the list and its rules. The control never edits the list it was given: each add,
remove or paste assigns a new array. Hold an `IReadOnlyList<string>` property and replace it; do not expect an
`ObservableCollection<T>` to change in place. The example checks the recipients' email format.

```csharp
public partial class ReportViewModel : ObservableObject
{
    [ObservableProperty]
    private IReadOnlyList<string> _labels = Array.Empty<string>();

    [ObservableProperty]
    private IReadOnlyList<string> _recipients = Array.Empty<string>();

    [ObservableProperty]
    private string? _recipientsError;

    partial void OnRecipientsChanged(IReadOnlyList<string> value)
    {
        // The control adds a tag at once, so a rule that rejects it removes it and explains why.
        var valid = value.Where(IsEmailAddress).ToArray();
        if (valid.Length != value.Count)
        {
            Recipients = valid;
            RecipientsError = "Enter a full email address, such as ops@acme.com.";
            return;
        }
        RecipientsError = null;
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (Recipients.Count == 0)
        {
            RecipientsError = "Add at least one recipient.";
            return;
        }
        await _reports.ScheduleAsync(Labels, Recipients);
    }

    private static bool IsEmailAddress(string tag) =>
        Regex.IsMatch(tag, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
}
```

```xml
<StackPanel Spacing="16">
    <arc:TagInput Label="Labels"
                  Value="{Binding Labels, Mode=TwoWay}"
                  Description="Press Enter or a comma to add a label." />
    <arc:TagInput Label="Recipients"
                  Value="{Binding Recipients, Mode=TwoWay}"
                  ErrorMessage="{Binding RecipientsError}"
                  Placeholder="name@company.com" />
    <arc:Button Content="Schedule report" Command="{Binding SendCommand}" />
</StackPanel>
```

- `Value` is two-way by default, so `Mode=TwoWay` documents the intent. The view model receives each change, including
  tags added by paste.
- `ErrorMessage` is one-way: the view model owns the message and clears it when the list is valid again.
- Compiled bindings need `x:DataType` on the view (the example leaves it out for brevity).

### Binding and events

`ValueChanged` reports the old and new lists, plus the tags that were `Added` and `Removed`. Use the two-way `Value`
binding for the full state, and the event when you need the difference, for example to write an audit entry or to call
a server once per added tag.

<Demo name="TwoWayValue" />

```xml
<arc:TagInput Label="Keywords" Value="{Binding Keywords, Mode=TwoWay}" />
```

```csharp
tags.ValueChanged += (_, e) =>
{
    Console.WriteLine($"Added: {string.Join(", ", e.Added)}; removed: {string.Join(", ", e.Removed)}");
};

tags.AddTag("release");
tags.FocusEditor();
```

`AddTag` and `RemoveTag` raise `ValueChanged` but announce nothing, because the person did not act on the field.

### Limit and errors

`MaxTags` refuses adds past the limit. The field shakes and the limit is announced. An `ErrorMessage` marks the
field invalid and shows the error row.

<Demo name="Limit" />

<Demo name="Error" />

`MaxTags` applies to every add: typing, Enter, comma, paste and `AddTag`. `null` (the default) means no limit. A paste
past the limit adds the pieces that fit and keeps the refused ones in the draft.

Use `ErrorMessage` for a rule the control cannot know, such as a server error. Set it to `null` or an empty string to
clear it.

### Validation

`TagInput` has no pre-commit hook: Enter adds the draft as soon as it is pressed. The control enforces the limit and
duplicates; format and server rules run in the view model after the change.

| Rule | When it runs | Where it shows |
|---|---|---|
| Maximum count (`MaxTags`) | At once, on each add | The field shakes and the limit is announced; no error row. Put a counter in `Description` if people need to see it |
| Duplicate | On Enter, comma or paste | The existing tag pulses and "<tag> is already added" is announced; the draft stays |
| Format (email, domain, slug) | After the tag appears, in the view model | `ErrorMessage` bound from the view model, until the list is valid again |
| At least one tag | On submit | `ErrorMessage`; move focus to the field with `FocusEditor()` |
| Server or uniqueness rule | After the request returns | `ErrorMessage` on the field the error is about; clear it when the list changes |

The duplicate check ignores case, so Design is refused when design is present, and the existing spelling is kept.

### Data shape and performance

A tag is a string with no id, color or display text. Keep extra data, such as a color for each label, in the view model
keyed by the tag text.

Every add or removal copies the list, which is negligible for the tens of tags a form holds. Load many tags with one
`Value` assignment: it raises one `ValueChanged` event, where a loop of `AddTag` calls raises one per tag.

### Pasted lists

Pasting text that contains commas, new lines or tabs splits it into tags. The last piece stays in the draft.

<Demo name="Paste" />

Pieces are trimmed; empty pieces and duplicates are skipped. Text without a separator pastes into the draft.

### Empty with placeholder

The placeholder rises in whenever the field has no tags and no draft.

<Demo name="Empty" />

Keep the default "Add a tag" unless a better example helps, such as `name@company.com` for recipients.

### Description

Helper copy under the field also describes the editor for screen readers.

<Demo name="Description" />

Write one sentence about the rule or the effect of the list: "Up to 5 skills". Screen readers read it as the editor's
help text, after its name.

### Wrapping rows

Tags wrap onto new rows and the field grows with them. Remove one to watch the rest glide in.

<Demo name="Wrapping" />

A long list wraps and the field grows downward; it never scrolls sideways. Tags wider than the field are truncated
with an ellipsis.

### Keyboard

Backspace at the start picks the last tag, Backspace or Delete removes the pick, the arrows move it and Escape clears
it. Clicking a tag picks it too.

<Demo name="Keyboard" />

The keys are listed under Accessibility.

### Disabled

Disabled fields fade to 0.5 opacity on `SurfaceMuted`.

<Demo name="Disabled" />

Use `IsEnabled="False"` while the list cannot change, and say why nearby. `TagInput` has no read-only state, so a
disabled field is the only way to lock the list; Tab skips it.

### Example: support ticket form

A support console form for a ticket. The recipients field shows a rule that rejected an address: the bad tag was
removed and the message says how to fix it. Both fields share a width, so their message rows line up.

![A ticket form with a label list and a recipient field that shows an invalid address](images/TagInputForm.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
             xmlns:generic="using:System.Collections.Generic"
             Width="640">
    <StackPanel Spacing="20">
        <arc:Input Label="Ticket title"
                   Text="Invoice export times out for large workspaces"
                   Description="Shown to the customer in the ticket list." />
        <arc:TagInput Label="Labels"
                      Description="Press Enter or a comma to add a label. Use lowercase words."
                      HorizontalAlignment="Stretch">
            <arc:TagInput.Value>
                <generic:List x:TypeArguments="x:String">
                    <x:String>billing</x:String>
                    <x:String>export</x:String>
                    <x:String>p2</x:String>
                </generic:List>
            </arc:TagInput.Value>
        </arc:TagInput>
        <arc:TagInput Label="Notify"
                      ErrorMessage="Enter a full email address, such as ops@acme.com."
                      HorizontalAlignment="Stretch">
            <arc:TagInput.Value>
                <generic:List x:TypeArguments="x:String">
                    <x:String>finance@northwind.com</x:String>
                    <x:String>ops@acme.com</x:String>
                    <x:String>on-call-team</x:String>
                </generic:List>
            </arc:TagInput.Value>
        </arc:TagInput>
        <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
            <arc:Button Content="Cancel" Variant="Secondary" />
            <arc:Button Content="Save ticket" />
        </StackPanel>
    </StackPanel>
</UserControl>
```

### Content guidelines

- **Label**: a plural noun in sentence case, without a colon: "Labels", "Recipients", "Allowed domains". Do not use
  "Please enter labels:". The label stays visible while the field has tags, so it carries the meaning.
- **Placeholder**: an example of one tag ("name@company.com", "billing"), shown only while the field is empty.
- **Description**: one sentence about the rule or the effect: "Up to 5 skills.", "Shown on the invoice header." Write
  numbers as digits.
- **Tags**: store the form the person should see, with consistent case.
- **Error**: name the rule and the fix in the same words as the description: "Enter a full email address, such as
  ops@acme.com."

| Instead of | Write |
|---|---|
| Enter tags here | Add a tag |
| Invalid email! | Enter a full email address, such as ops@acme.com. |
| Too many tags | You can add up to 5 skills. Remove one to add another. |
| Tag already exists | Design is already added. |
| Please add something | Add at least one recipient. |

### Best practices

**Do**

- Give every tag field a visible label, and a description when the rule is not obvious. The label is the editor's
  accessible name, so an unlabeled field is an unnamed control.
- Keep the list as an `IReadOnlyList<string>` property and assign a new list for each change, as the control does.
- Use `MaxTags` for a limit the form already states, so the add is refused at once and announced.
- Keep a format error on the field until the list is valid, so the rule is visible where the person is working.
- Move focus to the field with `FocusEditor()` when a submit fails.

**Avoid**

- Using a tag field for a fixed catalog. People cannot tell which tags are allowed, and typos become new values.
  Use `MultiSelect` or `Combobox` for a list that already exists.
- Removing a rejected tag without a message. Say which rule it broke and how to fix it, on the field.
- Relying on the placeholder for the rule. It disappears when the field gets a tag or a draft.

### Common questions

**Why does Enter not submit my form?** The editor consumes Enter to add the draft, so the form's default button
(`IsDefault="True"`) does not fire on that key. People press the save button or Tab to it.

**How do I show how many tags are left?** `TagInput` has no counter. Bind `Description` to a string the view model
builds from `Value.Count` and `MaxTags`, for example "3 of 5 used". The counter then updates with every change.

**How do I clear all tags?** Set `Value` to an empty list, for example `tags.Value = [];`. The placeholder returns once the draft is empty too.


## API reference

<!-- api: TagInput -->

### Event arguments

`ValueChanged` passes a `TagInputValueChangedEventArgs`.

| Member | Type | Description |
|---|---|---|
| `OldValue` | `IReadOnlyList<string>` | The tags before the change |
| `NewValue` | `IReadOnlyList<string>` | The tags after the change |
| `Added` | `IReadOnlyList<string>` | Tags in `NewValue` that were not in `OldValue` |
| `Removed` | `IReadOnlyList<string>` | Tags in `OldValue` that are not in `NewValue` |

## Accessibility

- `Label` names the field. With `IsLabelVisible="False"` the label is hidden but still names it.
- The editor's help text comes from `Description` and `ErrorMessage`, so a screen reader reads the rule or the error
  after the name.
- Each tag's remove button is named "Remove <tag>" (the `RemoveLabelFormat` pattern, default `Remove {0}`). The tag
  itself reads as plain text; its remove button is the only control inside it.
- Adding, removing, picking, duplicates and refused adds are announced politely. Focus does not move for an add or a
  pick; removing a tag with its button or with Backspace returns focus to the editor. A duplicate is not added: the
  existing tag pulses so the ignored Enter still gets an answer.

### Keyboard

| Keys | Action |
|---|---|
| `Enter` or `,` | Adds the trimmed draft as a tag. A duplicate is not added: the existing tag pulses and the duplicate is announced. An empty draft does nothing |
| `Backspace` with the caret at the start of an empty draft | Picks the last tag |
| `ArrowLeft` at the start of the draft, or with a tag picked | Picks the previous tag; at the first tag the pick stays where it is |
| `ArrowRight` with a tag picked | Picks the next tag; past the last tag the pick is cleared and the caret returns to the draft |
| `Backspace` or `Delete` with a tag picked | Removes the picked tag and returns focus to the editor |
| `Escape` with a tag picked | Clears the pick |
| `Backspace` inside the draft | Edits the text as usual |
| `Tab` / `Shift+Tab` | Moves through the remove buttons in value order, then to the editor |
| `Enter` on a focused remove button | Removes that tag |
| Click a tag | Picks it; a second click clears the pick. Focus stays in the editor |
| Click the empty part of the field | Focuses the editor |

Typing any character clears the pick. Leaving the field commits the draft as a tag and clears the pick.

## Motion

- A new tag fades in with a blur and a small scale where it was typed. A removed tag leaves, and the others glide to
  their new places.
- The selection ring slides between tags, and the shell height follows when tags wrap onto a new row.
- A duplicate pulses the existing tag once. A refused add shakes the field.
- Reduced motion removes the layout movement, the scale and the blur, and keeps a short fade. Off motion jumps to the end
  state.

| Change | Full | Reduced | Off |
|---|---|---|---|
| A tag is added | Fades in with a blur, grows from 90% where it was typed | Fades in | Immediate |
| A tag is removed | Leaves from its slot, the others glide into place | Disappears at once | Immediate |
| Tags reflow or wrap | Glide on a morph spring; the shell height follows on a smooth spring | Jump to the new place | Immediate |
| The picked tag changes | The selection ring glides to the new tag | The ring jumps to the tag | Immediate |
| A duplicate is entered | The existing tag pulses once | No pulse | No pulse |
| A refused add (limit) | The content kicks sideways and rings home on a bouncy spring | No shake | No shake |
| The placeholder appears | Rises by 0.3 em with a soft blur | Fades in | Immediate |

## Related

- `MultiSelect` or `Combobox` when the values come from a list that already exists.
- `ChipGroup` for facet filters that narrow a result set.
- `Input` for a single value.
- `MentionInput` for @people and #channel references in running text.

| Component | Choose it when |
|---|---|
| `Input` | The value is one line of text, not a list |
| `MultiSelect` | The values come from a fixed list and people choose several |
| `Combobox` | The list is long, and people search it before they choose |
| `ChipGroup` | The chips are filter facets over a result set |
| `MentionInput` | @people or #channel references sit inside running text |
| `Textarea` | The text runs over several lines or people paste paragraphs |
| `MoneyInput` / `NumberField` | The value is an amount or a number that needs parsing |
