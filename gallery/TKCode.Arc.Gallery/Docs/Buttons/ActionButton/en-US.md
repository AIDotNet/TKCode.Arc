---
title: Action button
summary: A compact button for frequent toolbar actions.
---

<Demo name="Default" />

## When to use

`ActionButton` is the call to action for one asynchronous submit, such as Save, Publish or Send invite. It shows the
work on the button itself: the label changes to a pending word while the task runs, a spinner turns, and a success word
with a drawn check confirms the result. The person learns the outcome where they clicked, without a toast, and the
button returns to its idle label by itself. Place it at the end of a form, in a document header or in a settings card,
wherever the submit is the one action that matters.

- **Publishing a post in a content editor**: the Publish post button in the editor header reads Publishing while the
  post is sent and Published when it is live, so the editor knows the change landed without leaving the page.
- **Saving a settings page in an admin console**: the Save settings button at the foot of a workspace settings page
  shows Settings saved after the request succeeds, and the form stays on screen for the next change.
- **Sending an invitation from a team page**: Send invite becomes Sent once the mail service accepts the request, then
  returns to Send invite after 2.4 seconds so the admin can invite the next person.
- **Sending a test event from a webhook editor**: Send test event shows Sending while the endpoint is called and
  Delivered when it answers. A failed call returns the button to idle and the editor explains the error beside it.

## When not to use

- **An ordinary form submit with a spinner and no result word**: use `Button` with `IsLoading`. It keeps its label and
  width while the work runs and needs no success state.
- **A destructive action that needs a deliberate press**: use `HoldToConfirm` from Gestures. A hold makes an accidental
  delete hard, while a success word appears only after the data is already gone.
- **A destructive action that asks for confirmation in place**: use `ConfirmMorph`, which asks the question where the
  control sits before anything runs.
- **Several alternatives for the same action**: use `SplitButton`, for example Merge pull request with Squash and merge
  and Rebase and merge in its menu.
- **A row action repeated in a dense table**: keep the result in the row with a small `Button`. An animated label per row
  is noise, and each letter of the label is an animated element.
- **Copying a value to the clipboard**: use `CopyButton`, which confirms the copy inline and is built for that job.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ActionButton Label="Save" PendingLabel="Saving" SuccessLabel="Saved" />
</UserControl>
```

```csharp
var save = new ActionButton
{
    Label = "Save",
    PendingLabel = "Saving",
    SuccessLabel = "Saved",
    AsyncAction = () => documentService.SaveAsync()
};
```

`ActionButton` derives from Avalonia's `Button`, not from the Arc `Button`. It keeps everything a button does: `Click`,
`Command`, `IsEnabled`, activation with Enter and Space, the focus model and the automation peer. It does not expose
`Variant`, `Size` or `IsLoading`, because it has one look and one height, and its pending state comes from
`AsyncAction`. Set `Label` rather than `Content`: the template draws the text from `Label`, `PendingLabel` and
`SuccessLabel`.

### Anatomy

1. **Label** (`Label`): the idle text, in the small body size (`TypeSM`) and medium weight. It is drawn by a text
   morph, so the pending and success words replace it in the same place. It is also the accessible name.
2. **Icon slot**: a 17 px square 8 px after the label. It shows a right arrow at idle, a
   16 px spinner while the action is pending and a check on success.
3. **Frame**: a 1 px border with the control radius (`RadiusControl`). The fill is the Foreground token and the text is
   the Background token, so the button is the inverse of the page: dark on a light page, light on a dark page.
4. **Padding and height**: 20 px inline padding and a minimum height of the medium control size (44 px). It has one
   size and does not follow the window width.
5. **Focus ring**: a 2 px ring in the focus color, drawn 2 px outside the frame. It appears for keyboard focus only.

The button aligns to the left by default. Set `HorizontalAlignment` when it sits in a centred column. Hover adds the
resting shadow to the frame. A press scales the button to 97 percent, or to 98.5 percent when it is wider than 220 px,
on a snappy spring. A pending button does not scale. Disabled, it fades to 70 percent opacity and shows the "not
allowed" cursor.

### Labels and reset

`Label` is the idle text and the accessible name. `PendingLabel` (default "Saving") shows while `AsyncAction` runs, and
`SuccessLabel` (default "Saved") shows after it completes. Use a verb for the idle label, its present participle for the
pending word and the past tense for the success word: "Publish post", "Publishing", "Published". A label set like this
keeps the width change small when the label morphs.

`ResetAfter` sets how long the success word stays before the button returns to idle. The default is 2.4 seconds
(2400 ms). A zero value keeps the success state until `Reset()` is called. Use the default in a toolbar, where the
result is a glance. Use zero on a settings page that the person keeps editing, so the confirmation does not vanish while
they read it.

<Demo name="Labels" />

<Demo name="KeepResult" />

```xml
<arc:ActionButton Label="Publish post"
                  PendingLabel="Publishing"
                  SuccessLabel="Published"
                  ResetAfter="00:00:03" />
```

Reset the button yourself when the state it reports is no longer true. `Reset()` returns to idle at once and cancels a
pending success reset. It also invalidates a run that is still in progress: when that task completes later, its result
is ignored and the button stays idle.

### Lifecycle

| Moment | State | Visible word | What the person hears |
|---|---|---|---|
| Button is ready | `Idle` | `Label` and the arrow | Name: `Label` |
| Activation starts `AsyncAction` | `Pending` | `PendingLabel` and the spinner | The pending label, once, from the status region |
| The task completes | `Success` | `SuccessLabel` and the check | The success label, once |
| `ResetAfter` elapses or `Reset()` runs | `Idle` | `Label` and the arrow | Nothing |
| The task throws | `Idle` | `Label`; `ActionFailed` is raised | Nothing from the button; show the error nearby |
| The button leaves the visual tree during success | `Idle` | `Label` | Nothing |

The state changes only through these moments, so you can read `State` (a read-only `ActionButtonState`) to decide what
else on the screen should change.

### Failure

When `AsyncAction` throws, the button returns to idle and raises `ActionFailed`. The event is a bubbling routed event,
and its `ActionButtonFailedEventArgs.Exception` holds what the action threw. The success word never shows, and the reset
timer does not start.

<Demo name="Failure" />

```csharp
sync.ActionFailed += (_, e) => status.Text = $"Back to idle: {e.Exception.Message}";
```

Show the message beside the button, not on it. The button is already back to its idle label, so the person can retry at
once, and the text next to it explains what went wrong. Keep the input the person typed, so the retry does not start from
an empty form.

### Commands and clicks

`Click` and `Command` run first, before `AsyncAction`. The button raises `Click` while it is still idle, then sets the
pending state and starts the action. Without an `AsyncAction`, the command or click counts as an immediate success, and
the success word shows at once. Setting both is valid: the command runs synchronously and the async action starts after
it, so the command should be the quick part of the work.

`ICommand.Execute` returns nothing, so a command cannot hold the pending state for a task. For asynchronous work, use
`AsyncAction` (see the view model example below).

### Disabled

Set `IsEnabled` to `False` to dim the button to 70 percent opacity and remove it from the tab order. While
`AsyncAction` is pending, the button keeps keyboard focus and ignores repeated activation without becoming disabled.
Use `IsEnabled` to block an action that cannot run, for example when there is nothing to save, and say why nearby.

<Demo name="Disabled" />

Do not disable the button to show that a form is invalid. Validate when the person presses it, then move focus to the
first field that needs attention.

### Binding to a view model

An `ActionButton` has a commit action and reports a state, so the view model owns the work and the button only shows its
progress. The view model below uses CommunityToolkit.Mvvm. `PublishAsync` is an ordinary public method that returns a
`Task`.

```csharp
public partial class PostEditorViewModel : ObservableValidator
{
    private readonly IPostService _posts;

    public PostEditorViewModel(IPostService posts)
    {
        _posts = posts;
    }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Enter a post title.")]
    private string _title = string.Empty;

    [RelayCommand]
    private void Discard()
    {
        Title = string.Empty;
    }

    public async Task PublishAsync()
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            return;
        }
        await _posts.PublishAsync(Title);
    }
}
```

```xml
<StackPanel Width="420" Spacing="16">
    <arc:Input Label="Post title" Text="{Binding Title}" />
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="Discard" Variant="Ghost" Command="{Binding DiscardCommand}" />
        <arc:ActionButton x:Name="Publish" Label="Publish post" PendingLabel="Publishing" SuccessLabel="Published" />
    </StackPanel>
</StackPanel>
```

```csharp
public partial class PostEditorView : UserControl
{
    public PostEditorView()
    {
        InitializeComponent();
        Publish.AsyncAction = () => ((PostEditorViewModel)DataContext!).PublishAsync();
    }
}
```

The bindings are one way. `Label`, `PendingLabel` and `SuccessLabel` come from the view or from localized resources, and
`Title` is two-way on the input. `ActionButton` writes nothing back: `State` is read-only, and its result is reported by
the view model's own state. The `AsyncAction` assignment stays in code because a method group cannot be bound to a
`Func<Task>` in XAML. Do not bind the same work to `Command` and `AsyncAction`, or it runs twice. The `Discard` button
uses `Command`, since it completes at once.

### Example: publishing a post in a content editor

A post editor header in a content management system. The title is the heading of the editor, and the footer holds the
three actions a writer uses: discard the draft, keep it as a draft and publish it. Publish is the only primary action on
the surface, so it is the only `ActionButton`. The explanatory copy says what publishing changes for readers, which is
the consequence the writer needs before pressing it. The success word Published confirms the change where the press
happened, and the button returns to Publish post in a few seconds, so the header stays ready for the next edit.

![A post editor footer with Discard, Save draft and an idle Publish post action button](images/PostEditorFooter.png)

```xml
<StackPanel Width="600" Spacing="20">
    <StackPanel Spacing="6">
        <TextBlock Text="Q2 pricing update"
                   FontSize="20"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="Draft saved 4 minutes ago. Starter plan readers see the new prices after you publish."
                   TextWrapping="Wrap"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    </StackPanel>
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="Discard" Variant="Ghost" />
        <arc:Button Content="Save draft" Variant="Secondary" />
        <arc:ActionButton Label="Publish post" PendingLabel="Publishing" SuccessLabel="Published" />
    </StackPanel>
</StackPanel>
```

### Content guidelines

- **Idle label**: a verb and its object in sentence case: "Publish post", "Save settings", "Send invite". The object
  tells the person what will change, which a bare "Submit" does not.
- **Pending label**: the present participle with no ellipsis: "Publishing". The spinner already signals progress, and a
  trailing "..." repeats it.
- **Success label**: the past tense of the action, in one or two words: "Published", "Settings saved". Keep it short so
  the width change stays small.
- **Scope**: one result per button. A success word that describes several things at once ("Saved and sent") is hard to
  trust and hard to translate.

| Instead of | Write |
|---|---|
| Submit | Publish post |
| Saving... | Saving |
| Done! | Published |
| Click to save | Save settings |

### Best practices

**Do**

- Use `AsyncAction` for any work that must hold the pending state. A command returns before an async task finishes, so
  the button would show success too early.
- Keep one `ActionButton` on a surface, for the one submit that matters. Two animated results compete for attention and
  the person cannot tell which one the result belongs to.
- Set `Label`, `PendingLabel` and `SuccessLabel` before the button can run. A change to `Label` shows only when the
  button is idle, so while it is pending or showing success the visible word stays as it was until the button returns
  to idle.
- Show a failure beside the button. The button has already returned to its idle label, so the message belongs next to
  the field or the form that failed.

**Avoid**

- Placing it in a dense table or a list with one button per row. Each button animates its own letters, and the motion
  becomes noise (see the table guidance above).
- Setting `ResetAfter` to a fraction of a second. A success word that disappears before it is read gives no result, and
  the polite announcement is easy to miss.
- Disabling the button to show that a request is running. Pending already keeps the button focused and ignores
  activation, and it tells assistive technology the button is unavailable.
- Using it for a destructive action. A success word appears after the data is gone; ask for a deliberate press with
  `HoldToConfirm` or confirm in place with `ConfirmMorph`.

### Common questions

**Why does my async command show Saved before the save finishes?** `ICommand.Execute` returns nothing, so the button
sees the command as complete as soon as it returns. Move the work to `AsyncAction`, which the button awaits.

**Can I use `Command` and `AsyncAction` together?** Yes, but use each for a different job. The command runs first and
synchronously, then the async action starts. Two paths that save the same data run the save twice.

**How do I keep the success word on screen until the next save?** Set `ResetAfter` to zero (`ResetAfter="00:00:00"`).
The button then holds `Success` until you call `Reset()`, for example when the person edits the form again.

**What happens when the button leaves the visual tree while it shows success?** The reset timer stops and the button
returns to idle at once. A pending action is not cancelled: it keeps running, and when it completes the button records
the success only if that run is still the current one.

**Why does the label not change when I set `Content`?** The template draws the text from `Label`, `PendingLabel` and
`SuccessLabel`. `Content` is inherited from `Button` but is not part of the visual.

## API reference

<!-- api: ActionButton -->

Companion types used on this page:

| `ActionButtonState` value | Meaning |
|---|---|
| `Idle` | Shows `Label`. Activation runs `Click`, `Command` and `AsyncAction`. |
| `Pending` | `AsyncAction` is running. Shows `PendingLabel`. Activation is ignored. |
| `Success` | The action completed. Shows `SuccessLabel` until `ResetAfter` elapses or `Reset()` is called. |

| `ActionButtonFailedEventArgs` member | Meaning |
|---|---|
| `Exception` | What the action threw. The button is back at idle when the event is raised. |

## Accessibility

- The accessible name is always `Label`, so the letters of the animated label are not read one by one.
- While pending, the button reports itself as unavailable but keeps keyboard focus.
- A hidden polite status region announces the pending label and the success label, so the change is heard without
  moving focus.
- Enter and Space run the action. Repeated activation is ignored while pending.
- The button does not announce a failure. Put the error text in a place screen readers reach, such as a live region or
  a text block next to the button, so the person hears why the button went back to idle.

### Keyboard

| Keys | Action |
|---|---|
| `Enter` / `Space` | Runs the action. Ignored while the button is pending. |
| `Tab` / `Shift+Tab` | Moves to the next or previous control. A pending button keeps its focus stop, so focus does not jump. |

## Motion

- Changed letters rise out of a soft blur and kept letters glide to their new positions. The width follows on a spring.
- The arrow leaves in the direction of the action and returns from the left. A spinner turns while the action runs, and
  a check draws itself on success.
- Reduced motion removes the press scale, the width spring and the drawn check. Label changes become an instant
  crossfade. Off replaces the label at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Label changes to pending or success | Changed letters rise from a soft blur, kept letters glide, the width springs | Instant crossfade | Immediate |
| Success check | Draws itself in 240 ms | Appears without drawing | Immediate |
| Press | Scales to 97 percent (98.5 percent when wide) on a snappy spring; never while pending | No press scale | No press scale |

## Related

- `Button` for a plain action, with `IsLoading` for a spinner and no result word.
- `CopyButton` for copying a value, with the same kind of inline confirmation.
- `ConfirmMorph` for a destructive action that asks for confirmation in place.
- `HoldToConfirm` for a destructive action that needs a deliberate press.

| Component | Choose it when |
|---|---|
| `Button` | The action has no result word, or it shows a spinner while the request runs |
| `SplitButton` | One default submit sits next to a menu of close alternatives |
| `CopyButton` | The action copies a value and the confirmation is an inline word |
| `ConfirmMorph` | A destructive action should ask a question in place before it runs |
| `HoldToConfirm` | A destructive action needs a deliberate press that cannot be triggered by accident |
| `ButtonGroup` | Several related actions on one object share a single surface |
