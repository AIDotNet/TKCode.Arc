---
title: Textarea
summary: A multiline field for notes, descriptions, and longer text.
---

<Demo name="LiveCount" />

## When to use

`Textarea` is the field for free text that runs over several lines: a reply to a customer ticket, an incident summary, a
product description, a note on an invoice. It fills its column, starts at a usable height and lets the person drag it
taller, so a long value stays readable without the form jumping around it. The helper and error rows are the same as
`Input`, so a multi-line field fits a form column without special layout.

- **Replies in a support console**: an agent writes a reply of a few sentences to a ticket. A live count against the
  reply limit tells the agent how much room is left, and the field keeps the draft while the agent checks an order.
- **Incident summaries and change notes**: an operator writes what happened, when, and what changed. Several paragraphs
  need line breaks and room to re-read, which a single-line `Input` does not give.
- **Product and catalog descriptions**: a merchandising editor enters a description that the storefront shows, with a
  count of characters against the limit the storefront enforces.
- **Feedback and survey answers**: a short free-text question such as "What would make the export faster?" with a rule
  for a minimum length that is checked when the person submits.
- **Internal notes on an account or an invoice**: a billing operator records why a refund was approved. The note is
  saved with the record, so a resizable field helps when the note is long.

## When not to use

- **A single line such as a name, an email address or a subject**: use `Input`, which has the same rows and no line
  breaks to manage.
- **A secret**: use `PasswordField`, or `PasswordStrength` when the person chooses a new password.
- **Text with headings, lists, links or emphasis**: use `RichTextEditor`. A `Textarea` holds plain text only.
- **Editing a title or a short description in place on a page**: use `InlineEdit`, which looks like text until it is
  edited.
- **A list of short values such as tags or recipients**: use `TagInput`, which turns each entry into a token.

## Usage

Enter adds a line. The field fills its column and is at least 110 px tall.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Textarea Label="Bio"
                  Placeholder="A sentence or two about you"
                  Description="160 characters left"
                  MaxLength="160"
                  Width="420"
                  HorizontalAlignment="Left" />
</UserControl>
```

`Textarea` derives from the shared Arc field base over Avalonia's `TextBox`. It turns on `AcceptsReturn` and wraps the
text at the width of the frame, so a line break is a plain Enter. `Text` is two-way, `MaxLength` stops input at the limit,
`IsReadOnly`, the caret, selection, undo, IME and the clipboard are the native text box behavior. The field adds the
label, the Arc frame, the helper and error rows, the focus ring and the resize grip.

### Anatomy

1. **Label** (`Label`): 14 px, medium weight, 8 px above the frame, the same as `Input`. It is the field's accessible
   name. Hide it with `IsLabelVisible="False"` only when a visible heading already names the field.
2. **Frame**: a 1 px border with the control radius on the surface color, and 12 px of padding on every side. Hover and
   focus only darken the border. A focused field draws a 3 px focus ring around the border.
3. **Text and placeholder** (`Placeholder`): the text wraps at the inner width of the frame. The placeholder shows in the
   muted text color while the field is empty.
4. **Minimum height**: the frame is at least 110 px tall at every size. The frame does not grow with its content; when the
   text is longer than the frame, the text scrolls inside it.
5. **Resize grip** (`IsResizable`): a 12 px cell in the lower-right corner with two short diagonal strokes, pulled into
   the padding so it sits close to the border. It is hidden when `IsResizable` is false and when the field is disabled.
6. **Description row** (`Description`): 12 px helper copy in a muted color under the frame. A count such as "120
   characters left" lives here.
7. **Error row** (`ErrorMessage`): 12 px copy in the danger color. While it has text, the field is invalid.

### Choosing a height

The frame starts at 110 px and keeps that height until the person drags the grip or your code sets `FrameHeight`. The
grip writes the chosen height in pixels, and any value below the 110 px minimum is raised to the minimum.

- **Short notes in a form column** (a comment, a reason for a change): keep the 110 px minimum and leave the grip on, so
  the person can make room for a longer note without a separate screen.
- **Replies and summaries of several paragraphs**: start taller by setting `FrameHeight` to about 200, so the first
  view shows the whole draft.
- **A fixed layout such as a dashboard card or a side panel**: set `IsResizable="False"`, so the card keeps its
  geometry. The field then scrolls its text.
- **A field that must keep the size the person chose**: bind `FrameHeight` with `Mode=TwoWay` and store the value in the
  view model or in user settings. `FrameHeight` is one-way by default.

### Binding to a view model

The view model owns the reply and its rules. It counts the characters, reports the error from a submit, and clears the
error when the reply changes.

```csharp
public partial class ReplyViewModel : ObservableObject
{
    private const int MaxReplyLength = 500;

    [ObservableProperty]
    private string _replyText = string.Empty;

    [ObservableProperty]
    private string? _replyError;

    public string CountText => $"{MaxReplyLength - ReplyText.Length} characters left";

    partial void OnReplyTextChanged(string value)
    {
        ReplyError = null;
        OnPropertyChanged(nameof(CountText));
    }

    [RelayCommand]
    private void SendReply()
    {
        ReplyError = ReplyText.Trim().Length < 40
            ? "Add a little more detail: at least 40 characters."
            : null;
        if (ReplyError is not null)
        {
            return;
        }
        // Send the reply to the ticket service here.
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:Textarea Label="Reply to the customer"
                  Text="{Binding ReplyText}"
                  Description="{Binding CountText}"
                  ErrorMessage="{Binding ReplyError}"
                  MaxLength="500"
                  Width="520"
                  HorizontalAlignment="Left" />
    <arc:Button Content="Send reply"
                Command="{Binding SendReplyCommand}"
                HorizontalAlignment="Left" />
</StackPanel>
```

`Text` is two-way. `Description`, `ErrorMessage`, `IsResizable` and `IsEnabled` are one-way. `FrameHeight` is one-way
unless you bind it with `Mode=TwoWay`, as described above. The control has no command of its own; the send action is a
command on the view model.

### Live count and validation

Update the description as the text changes. Only the digits that change roll. Set `ErrorMessage` to mark the
field invalid; an empty value clears the error.

<Demo name="Validation" />

```csharp
const int limit = 160;
bio.TextChanged += (_, _) =>
{
    var length = bio.Text?.Length ?? 0;
    bio.Description = $"{limit - length} characters left";
    bio.ErrorMessage = length < 40 ? "Tell us a little more: at least 40 characters." : null;
};
```

`MaxLength` stops the person at the limit but shows nothing, so the count must come from your code, as above. Count
the length the way the storefront or the server counts it: a `TextBox` counts UTF-16 characters, so an emoji takes two.

The validation rules follow the same pattern as `Input`: check each rule at the moment the person can act on it.

| Rule | When to check | Where it shows |
|---|---|---|
| Maximum length | While typing; `MaxLength` stops input | `Description` counts down; no error needed |
| Minimum length of a reply | On submit, then while the reply is still short | `ErrorMessage`; move focus to the field |
| Required | On submit | `ErrorMessage` |
| Content checks on the server, such as a blocked phrase | After the request returns | `ErrorMessage`, cleared when the text changes |

### Bind the text

`Text` is a two-way property. Bind it to a view model string.

```xml
<arc:Textarea Label="Feedback"
              Text="{Binding Feedback}"
              ErrorMessage="{Binding FeedbackError}" />
```

Enter stores a line break in `Text`. On Windows the break can be stored as `\r\n`, so normalize line endings before you
compare two values or save the text to a system that expects `\n`.

### Resizing

The corner grip drags the field taller. The height starts at the minimum and does not follow the content;
the field scrolls instead. Turn the grip off with `IsResizable`.

```xml
<arc:Textarea Label="Notes"
              IsResizable="False"
              Text="Fixed height." />
```

When the user drags the grip, `FrameHeight` holds the chosen height in pixels. Set it from code to start
the field at a given height.

```csharp
notes.FrameHeight = 200;
```

### Disabled

A disabled field is faded to 0.6 and has no grip.

<Demo name="Disabled" />

### Example: Support reply form

A reply form in a support console. The agent has written a reply of 280 characters against a limit of 500, so the count
shows 220 characters left. The field is a little taller than the minimum because a reply is a few sentences, and the
grip in the lower-right corner shows that the field can be taller. The buttons below the field use the verbs and
objects the agent needs: save the draft, or send the reply. The screenshot is the settled state: no error, the count is
final and the text fits the frame.

![Support reply form with a live count and a resize grip](images/SupportReply.png)

```xml
<StackPanel Width="600" Spacing="20">
    <TextBlock Text="Reply to Northwind Logistics"
               FontSize="18"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:Textarea Label="Reply"
                  Text="Thanks for the detail. The export stopped at 2 million rows because the workspace reached its row limit. We raised the limit to 5 million rows on your plan, so the export should now finish. If the file is still incomplete tomorrow morning, reply here and we will rerun it for you."
                  Description="220 characters left"
                  MaxLength="500"
                  FrameHeight="180" />
    <StackPanel Orientation="Horizontal"
                HorizontalAlignment="Right"
                Spacing="12">
        <arc:Button Content="Save draft" Variant="Secondary" />
        <arc:Button Content="Send reply" />
    </StackPanel>
</StackPanel>
```

### Content guidelines

Name the content, not the control. A label says what the note is for, and an error says what to add.

| Instead of | Write |
|---|---|
| Notes | Reason for the refund |
| Enter text here | Describe what you tried before the error |
| Invalid input | Add a little more detail: at least 40 characters. |
| Max 500 | 220 characters left |
| Submit | Send reply |
| Field is required | Add a reason for the refund. |

Use sentence case for labels and messages, digits for counts, and a full sentence for each error. Keep the count in the
description in the same words everywhere it appears, so a reply shows "characters left" in the same form in every screen.

### Best practices

**Do**

- Show the count before the limit is near, and count the way the server counts. A person who can see the room left
  shortens the text before a save is rejected.
- Use `MaxLength` as the hard stop and the description as the feedback. `MaxLength` alone gives no feedback, so the
  person only learns about the limit when the text stops changing.
- Start at the 110 px minimum for a short note and set `FrameHeight` for long content. A tall empty field makes a short
  form look unfinished.
- Keep the draft in the view model when the person leaves the screen. A multi-line draft is the costliest value to retype.
- Normalize line endings before you compare or save the text. A `\r\n` break from Windows and a `\n` break from another
  system otherwise look like different values.

**Avoid**

- Expecting the field to grow with its content. It scrolls, so the person cannot see the whole draft at once; give it a
  height that fits the task.
- Showing a minimum-length error while the person is still writing the first sentence. It reads as blame; check on
  blur or on submit.
- Using a `Textarea` for a single line such as a name or a subject. Enter adds a line, and pasted text keeps its breaks.
- Turning off `IsResizable` in a form where people read long text. The field then has only its minimum height, and the
  text scrolls in a small window.
- Relying on the grip for keyboard users. The grip responds to the pointer only, so give the field a height that works
  without resizing.

### Common questions

**Why does the field stay the same height while I type more lines?** It holds its height so the form layout does not
move. The text scrolls inside the frame. Let the person drag the grip, or set `FrameHeight` to the height the task needs.

**Why does `FrameHeight` of 60 still show a 110 px field?** Any height below the 110 px minimum is raised to the minimum.
The minimum applies at every `Size`, so `Small` does not make the field shorter.

**Does the field insert a tab character when the person presses Tab?** No. `AcceptsTab` is false, so Tab moves focus to
the next control. Set `AcceptsTab="True"` only when the field is a code or table editor and a tab character is the value.

**Can keyboard users resize the field?** No. The grip responds to the pointer only. Set `FrameHeight` in code, or
choose a fixed layout that works at the minimum height.

## API reference

<!-- api: Textarea -->

## Accessibility

- `Label` names the field. `Description` and `ErrorMessage` are merged into its help text.
- An error is announced assertively when it appears. Animated copy is hidden from assistive technology, which reads
  a plain version instead.
- Enter inserts a line break. Caret, selection, IME, undo and clipboard shortcuts are the native `TextBox` behavior.
- Focus draws a 3 px focus ring around the frame.
- The resize grip responds to the pointer only. A keyboard user keeps the minimum height and scrolls the text, so the
  layout must work at 110 px. Set `FrameHeight` in code when the content needs more room for everyone.

### Keyboard

| Keys | Action |
|---|---|
| Text keys | Insert text; `Enter` inserts a line break |
| `Up` / `Down` | Move the caret between lines |
| `Home` / `End`, `Ctrl+Home` / `Ctrl+End` (`Cmd` on macOS) | Move to the start or end of the line, or of the text |
| `Shift` + any of the above | Extend the selection |
| `Ctrl+A`, `Ctrl+C`, `Ctrl+X`, `Ctrl+V`, `Ctrl+Z`, `Ctrl+Y` (`Cmd` on macOS) | Select all, copy, cut, paste, undo and redo |
| `Tab` / `Shift+Tab` | Move focus to the next or previous control; `AcceptsTab` stays false, so Tab does not insert a tab character |

## Motion

- The helper and error row opens its height on a smooth spring. Changed words rise in with a soft blur.
- The character count rolls only the digits that change.
- Dragging the grip sets the height directly, so the frame follows the pointer without a transition.
- Reduced motion removes the spring, the digit roll and the blur, and changes the copy at once. Off does the same.

| Change | Full | Reduced | Off |
|---|---|---|---|
| A message row appears or goes | Height opens on a smooth spring and the text fades in | Shown at full height | Immediate |
| A message changes | Only the changed words rise in from below with a soft blur | Instant fade | Immediate |
| A number in the count changes | Only the changed digits roll, up when it grows, down when it shrinks | Instant fade | Immediate |
| Hover, focus, invalid | The border color crossfades | The border color crossfades | Immediate |

## Related

- `Input` for a single line of text.
- `InlineEdit` to edit a value in place without a visible frame.
- `TagInput` for a list of short values rather than free text.
- `RichTextEditor` for text that needs headings or lists.

| Component | Choose it when |
|---|---|
| `Input` | The value is one line, such as a name, an email or a subject |
| `RichTextEditor` | The text needs headings, lists, links or emphasis |
| `InlineEdit` | A title or a short description is renamed where it is shown |
| `TagInput` | The value is a list of short tokens such as tags or recipients |
| `PasswordField` / `PasswordStrength` | The value is a secret |
| `SearchField` | The text filters a list instead of being saved |
