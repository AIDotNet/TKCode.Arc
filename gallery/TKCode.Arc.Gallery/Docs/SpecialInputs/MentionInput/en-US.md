---
title: Mention input
summary: A textarea with @people and #channel mentions that act as single tokens, with suggestions at the caret.
---

<Demo name="CommentBox" />

## When to use

`MentionInput` is a multi-line field for writing to people and channels. A person types `@` and picks a teammate, or
types `#` and picks a channel, and the mention becomes one token that the field keeps whole. It belongs in the composer
of a comment thread, a reply box, a chat message or a task description, where the text reads as a sentence and the
mentions must still point at records the application can notify or link.

- **Comment threads on an incident, an order or a ticket**: a support lead writes "Thanks @Alex Rivera, can @Priya
  Natarajan confirm the refund ID?", and each mention stores a person's ID, so the application notifies the right
  people instead of matching names afterwards.
- **Release notes and updates posted to a channel**: an engineer links `#launch-q3` inside the sentence, so the
  message carries a channel reference that the chat service can render as a link.
- **Task and review descriptions in a project tool**: a designer assigns follow-ups with `@` mentions inside the
  description, and the saved value keeps the people as structured data for the assignment history.
- **Chat composers with a send action**: a message box submits on Enter with `IsSubmitOnEnter`, clears itself and
  passes the `MentionValue` to the send command.

## When not to use

- **Plain multi-line text with no references**: use `Textarea`, which has no suggestion list and no tokens to keep
  whole.
- **Values that stand apart from the sentence**: use `TagInput`, which keeps each value as a chip outside the text.
- **Headings, lists, links or bold text**: use a rich text editor. `MentionInput` holds plain text plus mentions and
  has no formatting model.
- **A single line such as a name, a title or a search query**: use `Input` or `SearchField`. A one-line field does
  not need an auto-growing editor or a submit-on-Enter flow.
- **Picking one record from a large directory**: use `Combobox` or `Select` with a search. The mention list shows only
  the best `MaxSuggestions` matches, so it suits a short list of people and channels, not a full directory lookup.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:MentionInput Label="Comment"
                      Placeholder="Leave a comment. Use @ to mention, # for a channel"
                      MinLines="1"
                      MaxLines="8" />
</UserControl>
```

The field has no visible label: `Label` only names the editor for assistive technology, and the placeholder carries
the instruction. The field is not a tab stop of its own; focus moves into the editor.

`MentionInput` derives from `TemplatedControl`. Its template holds a real multi-line `TextBox`, so the caret,
selection, input method composition, undo and the clipboard behave as they do in a text box. The editor's own text is
transparent, and a mirror layer behind it paints the same text with the same metrics and tints the mention tokens, so
colors and wrapping stay aligned with the caret. You read and write the content through `Value`, a `MentionValue`:
the plain text plus the mentions with their character offsets.

### Anatomy

1. **Frame** (PART_Shell): a 1 px border with the control radius on the surface color. It uses the border color at
   rest and the strong border color while the editor has focus. Hover does not change it.
2. **Editor** (PART_TextBox): the editable text in the body font at 16 px, with a 24 px line height, letter spacing
   of -0.01 em and 14 px of padding on each side, 11 px at the top and bottom.
3. **Placeholder** (`Placeholder`): shown in the muted text color while the value is empty and no IME text is pending.
4. **Mention token**: the text `@Label` or `#Label` on a rounded background, with 3 px of horizontal and 1 px of
   vertical padding. A person token uses the accent subtle fill with the accent strong text. A channel token uses the
   foreground color at 7% opacity with the foreground text.
5. **Suggestion panel**: a popup at the trigger character, 272 px wide (or as wide as the field when it is narrower),
   placed 6 px from the line. Each row has a 28 px avatar or icon, then the name, then the role or the description. A
   channel row shows its member count on the right.

### When suggestions open

Typing `@` opens the people list and typing `#` opens the channel list. A trigger counts only at the start of a word:
after a space, at the start of a line, or after an opening bracket or a quote. So the `@` in `billing@acme.com` does
not open the list. The query after the trigger has these limits:

- A people query may contain one space, so `@maya chen` matches. A second space closes the list.
- A channel query takes no spaces, so `#launch q3` closes the list at the space.
- A query longer than 48 characters, or one that spans a line break, closes the list.
- Typing a second trigger character inside a query closes the list.

Set `People` or `Channels` to `null` or an empty collection to turn off that trigger. The field then treats `@` or `#`
as plain text, which suits a comment thread where only people can be notified.

Results are ranked before the first `MaxSuggestions` are kept (the default is 6). A name that starts with the query
ranks highest, then a word in the name that starts with it, then a name that contains it, then a match in the role or
the description. A query with no match shows an empty state ("No people match “zz”" or "No channels match “zz”"), and
the list stays open so the person can keep typing or press Escape.

### Binding to a view model

The view model owns the stored value. `Value` is two-way by default, so every edit flows back to the view model.
With CommunityToolkit.Mvvm:

```csharp
public partial class ReplyComposerViewModel : ObservableObject
{
    private readonly ICommentService _comments;

    public ReplyComposerViewModel(ICommentService comments,
                                  IReadOnlyList<MentionPerson> people,
                                  IReadOnlyList<MentionChannel> channels)
    {
        _comments = comments;
        People = people;
        Channels = channels;
    }

    [ObservableProperty]
    private MentionValue _draft = MentionValue.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isSending;

    public IReadOnlyList<MentionPerson> People { get; }

    public IReadOnlyList<MentionChannel> Channels { get; }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync(MentionValue value)
    {
        IsSending = true;
        try
        {
            await _comments.PostAsync("inc_2026_0412", value);
            Draft = MentionValue.Empty;
        }
        finally
        {
            IsSending = false;
        }
    }

    private bool CanSend(MentionValue? value) =>
        !IsSending && value is not null && !string.IsNullOrWhiteSpace(value.Text);
}
```

```xml
<StackPanel Width="560" Spacing="12">
    <arc:MentionInput Label="Reply"
                      Value="{Binding Draft, Mode=TwoWay}"
                      People="{Binding People}"
                      Channels="{Binding Channels}"
                      IsSubmitOnEnter="True"
                      SubmitCommand="{Binding SendCommand}"
                      MinLines="2"
                      MaxLines="6" />
    <arc:Button Content="Post reply"
                Command="{Binding SendCommand}"
                CommandParameter="{Binding Draft}"
                HorizontalAlignment="Right" />
</StackPanel>
```

- Bind `Value`, not the text, because the text alone loses the mentions. `People` and `Channels` are one-way.
- Use `SubmitCommand` or the `Submitted` event for the send action. The command runs only when its `CanExecute` returns
  `true` for the submitted value.
- Use `MentionAdded` for side effects of choosing a person or a channel, and `ValueChanged` for anything that depends on
  the whole text.

### Prefilled value

Set `Value` with the text and its mentions. Each offset points at the trigger character (`Start`) and just after the
label (`End`). Editing around a mention moves it: typing before it shifts both offsets. Editing inside it removes the
mention, and its text stays in the field as plain text.

<Demo name="Prefilled" />

```csharp
var text = "Thanks @Maya Chen. Can @Jordan Lee review it in #design?";
comment.Value = new MentionValue(text,
[
    new Mention(MentionKind.Person, "u_maya", "Maya Chen", 7, 17),
    new Mention(MentionKind.Person, "u_jordan", "Jordan Lee", 24, 34),
    new Mention(MentionKind.Channel, "c_design", "design", 47, 54)
]);
```

Compute the offsets from the text when it comes from a template, because a wrong offset attaches the mention to the
wrong characters.

### Submit on Enter

With `IsSubmitOnEnter` set to `True`, Enter submits and Shift+Enter adds a line. With the default `False`, Enter adds a
line and the send action is a button. Enter never submits while an input method is composing, so a candidate can be
confirmed with Enter. A blank value does not submit. While the suggestion list has results, Enter chooses the
highlighted row instead, so a half-typed mention is never sent.

<Demo name="SubmitOnEnter" />

```csharp
message.IsSubmitOnEnter = true;
message.Submitted += (_, e) =>
{
    Console.WriteLine($"{e.Value.Text} ({e.Value.Mentions.Count} mentions)");
    message.Clear();
};
```

### Lines and growth

`MinLines` and `MaxLines` set the height range in lines. The field starts at `MinLines` and grows one 24 px line at a
time as the text wraps or new lines are typed, up to `MaxLines`. Past that height the editor scrolls inside the frame
with the scrollbar hidden, so the composer does not push the rest of the page down. The defaults are 1 and 8.

A thread composer typically uses `MinLines="2"` and `MaxLines="6"`.

<Demo name="Growth" />

### Size and placement

`SuggestionPlacement` sets where the list opens. `Auto` (the default) opens below the line and flips above it when
there is less than 300 px below and more room above. `Top` and `Bottom` always use the chosen side.

<Demo name="Placement" />

### Commands and events

`Insert`, `OpenSuggestions` and `Clear` act on the field from code. `Insert` types the text at the caret as if the
person typed it, so it joins the undo stack. `OpenSuggestions` types the trigger for a kind, adding a space first when
the character before the caret is not whitespace. `Clear` empties the value and resets the caret.

```csharp
button.Click += (_, _) => comment.OpenSuggestions(MentionKind.Person);

comment.MentionAdded += (_, e) => Console.WriteLine($"Added {e.Mention.Text}");
```

`ValueChanged` fires on every change to `Value` and carries the old and new values. `MentionAdded` fires only when a
suggestion becomes a mention.

### Disabled

The whole field fades to 55% and takes no input. Its text and mentions stay readable.

<Demo name="Disabled" />

### Focus and dismissal

The suggestion list is a popup that closes on its own:

- **Escape** closes the list for the current trigger only. Typing more characters in the same query keeps it closed,
  and a new trigger after a space opens it again.
- **A press outside the field and the list** closes the list. Presses inside the field, such as moving the caret,
  do not.
- **Tab** chooses the highlighted suggestion while the list has results. Otherwise it moves focus.
- **A click on a row** keeps focus in the editor and places the caret after the token and its trailing space, so the
  person can continue typing. Undo (Ctrl+Z, Cmd+Z on macOS) takes the choice back, but not the mention object.

### Serialized output

<Demo name="Serialized" />


`MentionSerializer.Serialize` turns the value into storage text. By default each person becomes `<@id>` and each
channel becomes `<#id>`, and the rest of the text is unchanged. Pass a format function to write another form, such as
a display name in brackets. Store the default form when names can change, because the stored text keeps pointing at
the same ID.

```csharp
var stored = MentionSerializer.Serialize(comment.Value);
// "Thanks <@u_maya>. Can <@u_jordan> review it in <#c_design>?"
```

### Example: incident reply thread

A reply box in the thread of an incident (`inc_2026_0412`, a duplicate charge on a Northwind order). The message being
answered sits above it for context. The reply mentions two people and one channel, each drawn as a token in the field. The primary action sits below the field on the right.

![An incident reply box with two person mentions and a channel mention](images/ReplyThread.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
             xmlns:generic="using:System.Collections.Generic">
    <StackPanel Width="600" Spacing="14">
        <StackPanel Spacing="4">
            <TextBlock Text="Duplicate charge on order NW-48213"
                       FontSize="14"
                       FontWeight="SemiBold"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Priya Natarajan, 2 hours ago"
                       FontSize="12"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            <TextBlock Text="The card was charged twice at 09:14. The second charge is pending a refund."
                       FontSize="13"
                       TextWrapping="Wrap"
                       Margin="0,6,0,0"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        </StackPanel>
        <arc:MentionInput Label="Reply to incident" MinLines="2" MaxLines="6">
            <arc:MentionInput.Value>
                <arc:MentionValue>
                    <x:Arguments>
                        <x:String>Thanks @Maya Chen. Can @Jordan Lee confirm the refund window before we post to #launch-q3?</x:String>
                        <generic:List x:TypeArguments="arc:Mention">
                            <arc:Mention>
                                <x:Arguments>
                                    <arc:MentionKind>Person</arc:MentionKind>
                                    <x:String>u_maya</x:String>
                                    <x:String>Maya Chen</x:String>
                                    <x:Int32>7</x:Int32>
                                    <x:Int32>17</x:Int32>
                                </x:Arguments>
                            </arc:Mention>
                            <arc:Mention>
                                <x:Arguments>
                                    <arc:MentionKind>Person</arc:MentionKind>
                                    <x:String>u_jordan</x:String>
                                    <x:String>Jordan Lee</x:String>
                                    <x:Int32>23</x:Int32>
                                    <x:Int32>34</x:Int32>
                                </x:Arguments>
                            </arc:Mention>
                            <arc:Mention>
                                <x:Arguments>
                                    <arc:MentionKind>Channel</arc:MentionKind>
                                    <x:String>c_launch</x:String>
                                    <x:String>launch-q3</x:String>
                                    <x:Int32>78</x:Int32>
                                    <x:Int32>88</x:Int32>
                                </x:Arguments>
                            </arc:Mention>
                        </generic:List>
                    </x:Arguments>
                </arc:MentionValue>
            </arc:MentionInput.Value>
        </arc:MentionInput>
        <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
            <arc:Button Content="Discard" Variant="Secondary" />
            <arc:Button Content="Post reply" />
        </StackPanel>
    </StackPanel>
</UserControl>
```

A short reply keeps the two-line height that `MinLines` sets, so the box does not jump while typing.

### Content guidelines

- **Placeholder**: tell the person what the field is for and how to mention someone: "Leave a comment. Use @ to
  mention, # for a channel". Keep it to one line at the field's width.
- **Label**: a short noun phrase for assistive technology, such as "Reply to incident". It is not shown on screen.
- **Actions**: name the send action with a verb and object, "Post reply" or "Send message", not "Submit". Keep the
  empty-state copy ("No people match “{0}”") as the default, because it names the query.

| Instead of | Write |
|---|---|
| Enter your comment here! | Leave a comment. Use @ to mention, # for a channel |
| Mention someone | Use @ to mention a person |
| Submit | Post reply |
| Mention (Maya Chen, Product designer) | Maya Chen |
| No results | No people match “zz” |

### Best practices

**Do**

- Store the `MentionValue`, or its serialized form, rather than the plain text, so the IDs survive edits and name
  changes.
- Set `MinLines` to the height of a typical reply and `MaxLines` to the space free below the field, so the composer
  does not move the page while the person types.
- Turn off a trigger with an empty collection when that kind of mention does not apply, so `@` or `#` stays plain text.
- Use stable IDs from your directory, not display names, so a renamed person keeps the same reference.

**Avoid**

- Storing only `Text`. The mentions then cannot be notified by ID or restored on the next edit.
- Using `IsSubmitOnEnter` in a document editor, where Enter must add a line. Use a button to send there.
- Loading a whole directory into `People`. The list shows the best matches only, so a large directory needs a search
  that updates `People` for each query.

### Common questions

**Why does Enter pick a suggestion instead of submitting?** While the list is open and has results, Enter and Tab choose
the highlighted row. Once the list closes (after a choice, Escape, or a space that ends the query), Enter submits.

**Why are my mentions missing after I undo?** Undo restores the typed text of a token, but not the mention object.
Handle `ValueChanged` and rebuild your own mention list from `NewValue` when undo must keep the mentions.


## API reference

<!-- api: MentionInput -->
<!-- api: MentionPerson -->
<!-- api: MentionChannel -->

`Mention` is the unit of the value; `Start` and `End` are character offsets into `Text`.

| Member | Type | Meaning |
|---|---|---|
| `Kind` | `MentionKind` | `Person` (`@`) or `Channel` (`#`) |
| `Id` | `string` | The ID of the person or channel, for storage and notifications |
| `Label` | `string` | The name shown after the trigger character |
| `Start` | `int` | Offset of the trigger character |
| `End` | `int` | Offset just after the label (end exclusive) |
| `Text` | `string` | The token text: `@Label` or `#Label` |

## Accessibility

- The editor is a combo box with list autocomplete. `Label` names it; when `Label` is null, the control's
  `AutomationProperties.Name` is used instead.
- The suggestion list is named by `PeopleListName` (People) or `ChannelsListName` (Channels). Each row reads its name
  and role or description, for example "Maya Chen, Product designer".
- The result count is announced politely ("3 people", "1 channel", "No matches") and clears when the list closes.
- Mention colors come from a hidden layer, so assistive technology reads the plain text.

### Keyboard

| Keys | Action |
|---|---|
| `@` or `#` after a space, a line start, or an opening bracket or quote | Opens the people or channel suggestions |
| `Down` / `Up` | Moves the highlight through the suggestions and wraps at the ends (list open) |
| `Enter` / `Tab` | Chooses the highlighted suggestion when the list has results |
| `Escape` | Closes the list for the current trigger only |
| `Enter` | Submits when `IsSubmitOnEnter` is `True` and the text is not blank; otherwise adds a line |
| `Shift+Enter` | Adds a line |
| `Left` / `Right` | Moves the caret one character, or hops over a whole mention token |
| `Backspace` / `Delete` next to a mention | Removes the whole token in one keystroke |
| `Ctrl+Z` / `Ctrl+Y` (`Cmd+Z` / `Cmd+Shift+Z` on macOS) | Undo and redo, including a choice made from the list |

Enter never submits during IME composition.

## Motion

- The field follows its content on a smooth spring, the suggestion panel opens from the caret with a small scale, and the
  highlight glides between rows. Reduced motion jumps the height and the highlight, and the panel only fades.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Field height follows the text | Smooth spring | Jumps to the new height | Immediate |
| Suggestion panel opens | Small scale from the caret, snappy | Fades in, no scale | Immediate |
| Highlight moves between rows | Glides in 0.3 s with a slight bounce | Jumps | Immediate |
| Panel height when results change | Spring | Jumps | Immediate |
| Border on focus | Border color crossfades | Short fade | Immediate |

## Related

| Component | Choose it when |
|---|---|
| `Textarea` | The text is several lines of plain text with no people or channels to reference |
| `Input` / `SearchField` | The value is one line, such as a name or a search query |
| `TagInput` | The values are a list of chips that stand apart from the sentence |
| `Select` / `Combobox` | The person must pick one value from a full list rather than mention it in text |
| `Field` + `FieldFrame` | You build your own editor and want the same label, frame and message rows |
