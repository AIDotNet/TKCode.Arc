---
title: Hold to confirm
summary: Confirm a destructive action by holding, not tapping.
---

<Demo name="Default" />

## When to use

`HoldToConfirm` is a button that runs its action only after the person has held it for a set time. It fits a
destructive or hard to undo action whose consequence is already visible on the screen, so the person does not need
to read a separate message. The fill grows across the button while it is held, and letting go early rewinds it, so
the control shows exactly how far the action has progressed before it commits.

- **Deleting a project from a settings page**: the danger zone at the bottom of a project's settings lists "Hold to
  delete project" beside the project name. The name is on screen, so a hold is enough to confirm the choice.
- **Revoking an API key or a webhook secret in a developer console**: a key row shows its prefix and last-used date,
  and the revoke button holds for two seconds. A key that is still in use by a build pipeline breaks at once, so the
  longer hold guards against a stray tap.
- **Cancelling a subscription in a compact billing row**: the plan row already shows the renewal date and the amount
  that stops. A hold keeps the decision on the row instead of a confirmation dialog that repeats the same facts.
- **Emptying a trash or resetting a sandbox in an internal tool**: a warehouse or support tool used on a shared tablet,
  where a stray tap on "Empty trash" must not remove 300 records.

## When not to use

- **Safe actions or ones that are easy to undo, such as saving a draft or muting a channel**: use `Button` or
  `ActionButton`. A hold on a reversible action makes people wait for nothing.
- **Actions that need the consequences read first, such as a refund, a plan downgrade with proration or a bulk
  change to billing details**: use `Dialog`, which can show the amount, the affected records and a clear cancel
  choice.
- **Removing one row from a touch list, such as an inbox, an order queue or a notification list**: use
  `SwipeActions`, which reveals the action on a swipe and keeps it next to the row.
- **Choosing a value or an option from a set**: use `Select`, `Combobox` or `MorphSelect`. A hold only commits one
  action.
- **Confirming a change that the person made on a form before saving**: use the form's primary `Button` and validate
  on submit. Holding a save button adds a delay to every save.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:HoldToConfirm Label="Hold to delete project"
                       ConfirmedLabel="Deleted"
                       Command="{Binding DeleteProject}" />
</UserControl>
```

`HoldToConfirm` derives from Avalonia's `Button`, so `Command`, `CommandParameter`, `IsEnabled` and the button's
template and style mechanics work as they do on a button. The difference is the trigger: the `Click` event and the
`Command` run only when a hold completes. A normal click, a tap that is shorter than the hold, or an automation
invoke does nothing. The control adds the fill, the done state, the `IsHolding` state and the `Confirmed` event on
top of the button.

### Anatomy

1. **Icon** (`Icon`): a 24 by 24 Lucide geometry drawn at 18 px before the label. The default is a trash icon. It is
   replaced by a drawn check mark when the hold completes.
2. **Label** (`Label`): the instruction and the action, for example "Hold to delete project". After completion the
   text shows `ConfirmedLabel` instead, "Done" by default. The icon and label are separated by 8 px and centered in
   the button.
3. **Surface**: a 1 px border in the strong border color, the control radius (`RadiusControl`) and the surface
   background. The minimum height is the medium control size, 44 px in the default theme. Horizontal padding is 20 px.
4. **Hover tint**: while the pointer is over an unconfirmed button, the tone color is mixed over the surface at 6%.
   It is a tint, not a second fill, and it never hides the label.
5. **Fill**: a layer in the tone color, revealed from the left edge as the hold progresses. The same icon and label
   are drawn again inside the fill in the tone's contrasting color, so the text changes color exactly at the edge.
6. **Focus ring** (keyboard focus only): a 2 px ring in the focus color, drawn 4 px outside the button with a 22 px
   radius. It is not shown for pointer focus.
7. **Disabled state**: the whole button drops to 50% opacity and shows the "not allowed" cursor.

### Choosing a tone

`Tone` sets the color of the fill. The label stays readable at every point of the hold because the text changes
color at the fill edge.

| Tone | Fill | Use it for |
|---|---|---|
| `Accent` (default) | The selected accent color | A deliberate action that is not destructive, such as "Hold to publish" |
| `Danger` | The danger color, with a danger-tinted border and label | An irreversible action: delete, revoke, purge, close an account |
| `Neutral` | The foreground color | A reversible action that still needs a deliberate hold, such as "Hold to archive" |

Use `Danger` for the actions people cannot take back, and keep one `Danger` control per group. A list of several red
holds in a single panel makes the real danger hard to read. Pick `Neutral` for archive-style actions so the danger
color keeps its meaning.

<Demo name="Tones" />

### Hold duration

`HoldDuration` sets how long the button must be held before it completes. The default is 1.2 seconds, which is long
enough to ignore a tap and short enough that a deliberate hold does not feel slow. Set a longer value, such as two
seconds, for an action with a wide impact, like revoking a key used by production systems. Keep the value the same for
all holds on one screen so the person learns one rhythm. The text in the help copy is generated from the duration, so
it stays correct when you change it.

The value is a `TimeSpan`. In XAML, write it as `HoldDuration="0:0:2"` for two seconds.

### Icon

`Icon` is a `Geometry`. The default is Lucide's trash icon, which suits deletion. Choose an icon that matches the
action's noun, such as an archive box for archiving or a key for revoking. Pass `null` when the label is enough on its own.

```csharp
var archive = new HoldToConfirm
{
    Label = "Hold to archive",
    Tone = HoldToConfirmTone.Neutral,
    Icon = ArcIcons.Archive,
    ConfirmedLabel = "Archived"
};
```

In XAML, use a static resource: `Icon="{x:Static arc:ArcIcons.Archive}"`.

### Hint while holding

`HoldingChanged` is raised when a hold starts and when it ends. `IsHolding` tells you the current state. Use them
for a hint next to the button, such as "Keep holding" while the fill grows.

```csharp
project.HoldingChanged += (_, e) =>
{
    hint.Text = e.IsHolding
        ? "Keep holding…"
        : project.IsConfirmed ? "Deleted." : "Let go early and the fill rewinds.";
};
```

Letting go early rewinds the fill on a spring and nothing is confirmed. When the hold completes, `HoldingChanged`
reports `IsHolding` as `false` before `IsConfirmed` becomes `true`. The check in the example above therefore reads
`false` at completion. Show the done text from the `Confirmed` event instead:

```csharp
project.Confirmed += (_, _) => hint.Text = "Deleted.";
```

### Done state

`IsConfirmed` is two-way. It becomes `true` when the hold completes, before `Confirmed` is raised and before `Command`
runs. Once it is `true`, the button shows `ConfirmedLabel`, draws the check mark, reports itself as unavailable to
assistive technology, and cannot be held again. Set it back to `false` to reset the button; the fill rewinds as the
label morphs back to `Label`.

```xml
<arc:HoldToConfirm Label="Hold to delete project"
                   IsConfirmed="{Binding IsDeleted}" />
```

```csharp
reset.Click += (_, _) => project.IsConfirmed = false;
```

A confirmation set from code, for example when a project is deleted in another window, fills the button without a
hold.

### Disabled

A disabled button cannot be held, and a hold in progress is cancelled when the button becomes disabled. Use the
disabled state only while the action cannot apply, and say why in the text next to it, for example "Transfer the
ownership first".

<Demo name="Disabled" />

### Cancelling a hold

A hold cancels in these cases, and the fill rewinds without confirming:

- The person lets go of the pointer or the key before `HoldDuration` has passed.
- The pointer moves more than 24 px beyond the edge of the button while held, the same slack a native press allows.
  Moving the pointer back inside does not restart the hold.
- Focus moves away from the button, for example with Tab.
- The window loses activation, because a key-up can be lost when the window deactivates mid-hold.
- The button is disabled or removed from the visual tree during the hold.

To cancel a hold from code, for example when a dialog opens over the page, call `CancelHold()`.

### Binding to a view model

In an MVVM app the view model owns the state of the action. With CommunityToolkit.Mvvm, the view model holds the
`IsDeleted` flag and the delete command, and the button binds to both. The command runs once, after the hold
completes and after `IsDeleted` has become `true`.

```csharp
public partial class ProjectDangerZoneViewModel : ObservableObject
{
    private readonly IProjectService _projects;
    private readonly string _projectId;

    public ProjectDangerZoneViewModel(IProjectService projects, string projectId)
    {
        _projects = projects;
        _projectId = projectId;
    }

    [ObservableProperty]
    private bool _isDeleted;

    [ObservableProperty]
    private string? _error;

    [RelayCommand]
    private async Task DeleteProjectAsync()
    {
        var result = await _projects.DeleteAsync(_projectId);
        if (!result.IsSuccess)
        {
            // The button shows "Deleted" until the flag is cleared, so reset it on failure.
            IsDeleted = false;
            Error = result.Error;
        }
    }
}
```

```xml
<StackPanel Spacing="12">
    <arc:HoldToConfirm Label="Hold to delete project"
                       ConfirmedLabel="Deleted"
                       Tone="Danger"
                       IsConfirmed="{Binding IsDeleted}"
                       Command="{Binding DeleteProjectCommand}" />
    <TextBlock Text="{Binding Error}"
               Foreground="{arc:ArcDesignTokenResource Danger}"
               TextWrapping="Wrap" />
</StackPanel>
```

- `IsConfirmed` is two-way by default, so the view model's `IsDeleted` flag follows the completed hold. Set it to
  `true` from code and the button shows the done state without a hold.
- `Command` is one-way. Bind `Command` only to the action that runs when the hold completes; do not also handle
  `Confirmed` for the same work, or the action runs twice.
- Use `Confirmed` for a side effect that is not the action itself, such as analytics or a hint.
- A failed action must set `IsConfirmed` back to `false`. Otherwise the button still shows "Deleted" for an action that
  did not happen.
- Compiled bindings need `x:DataType` on the view (the examples here leave it out for brevity).

### Example: danger zone on a project settings page

The danger zone at the bottom of a project settings page in a SaaS admin console. Each row names the consequence in
plain words ("Deletes the project and its 42 invoices"), and the button shows the action in the same words. Both
actions use the `Danger` tone because neither can be undone. The button sits on the right so the text is read first.

![A danger zone with two hold to confirm buttons beside their consequence text](images/DangerZone.png)

```xml
<Border Width="640"
        Padding="24"
        CornerRadius="12"
        BorderThickness="1"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        Background="{arc:ArcDesignTokenResource Surface}">
    <StackPanel Spacing="20">
        <TextBlock Text="Danger zone"
                   FontSize="16"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Danger}" />
        <Grid ColumnDefinitions="*,Auto" ColumnSpacing="24">
            <StackPanel Spacing="4">
                <TextBlock Text="Transfer project ownership"
                           FontSize="14"
                           FontWeight="Medium"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="The new owner receives billing contact and admin rights."
                           FontSize="13"
                           TextWrapping="Wrap"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            </StackPanel>
            <arc:HoldToConfirm Grid.Column="1"
                               Label="Hold to transfer"
                               ConfirmedLabel="Transferred"
                               Tone="Neutral"
                               Icon="{x:Static arc:ArcIcons.Users}"
                               VerticalAlignment="Center" />
        </Grid>
        <Border Height="1" Background="{arc:ArcDesignTokenResource Border}" />
        <Grid ColumnDefinitions="*,Auto" ColumnSpacing="24">
            <StackPanel Spacing="4">
                <TextBlock Text="Delete project"
                           FontSize="14"
                           FontWeight="Medium"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Deletes Acme Analytics and its 42 invoices. This cannot be undone."
                           FontSize="13"
                           TextWrapping="Wrap"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            </StackPanel>
            <arc:HoldToConfirm Grid.Column="1"
                               Label="Hold to delete"
                               ConfirmedLabel="Deleted"
                               Tone="Danger"
                               VerticalAlignment="Center" />
        </Grid>
    </StackPanel>
</Border>
```

Hold-to-confirm is the right tool for the delete row. The transfer row uses `Neutral` because a transfer can be
reversed by a second transfer, so the danger color is kept for the action that cannot be undone. Each action has
its own hold, so the person confirms the row they meant, and each row names the consequence beside the button.

### Content guidelines

- **Label**: an instruction and an object, "Hold to delete project", so the person knows both the verb and the
  target before they hold. Do not write only "Delete", which reads as a normal button.
- **Confirmed label**: the result in past tense, "Deleted", "Archived", "Revoked". It replaces the instruction once
  the action is committed, so it must describe what now stands.
- **Consequence text**: a sentence next to the button that names what is lost, with the count when it is known:
  "Deletes Acme Analytics and its 42 invoices." Keep it in the same row, not in a tooltip, because a tooltip is not
  visible to a touch user.
- **Tone**: sentence case, no exclamation marks, no "Are you sure?" The hold is the confirmation.

| Instead of | Write |
|---|---|
| Delete | Hold to delete project |
| Click to delete | Hold to delete project |
| Deleted! | Deleted |
| Are you sure you want to delete this project? | Deletes the project and its 42 invoices. This cannot be undone. |
| Hold this button for 1.2 seconds to delete | Hold to delete project |

### Best practices

**Do**

- Put the consequence in visible text next to the button. A hold confirms a choice the person can see, so it does not
  replace the explanation.
- Use `Danger` only for actions that cannot be undone, and `Neutral` or `Accent` for the rest. A danger color on every
  control removes its meaning.
- Set `IsConfirmed` back to `false` when the action fails, so the button does not report success for an action that
  did not happen.
- Keep one hold per row and do not put several holds on one line. A stray hold on the wrong row is harder to spot
  when the rows are close together.
- Tell keyboard users the way to confirm in the visible copy when the form is dense, because the hint is hidden from
  sight.

**Avoid**

- Holds for reversible or safe actions. People wait for a delay that protects nothing and stop trusting the control.
- Bulk actions on many selected rows. A single hold that deletes 50 invoices hides the count; use `Dialog` with the
  list of affected records.
- Showing the done state for an action that has not completed on the server. Set `IsConfirmed` after the save
  succeeds, or reset it on failure.
- Long labels. The button widens with its label, and a long instruction pushes the consequence text out of view.
- Relying on `Confirmed` and `Command` together for one action. Choose one trigger for the work so it does not run
  twice.

### Common questions

**Why does a click not run the command?** A hold is the only path to completion. A click, a short press or an
automation invoke does nothing. Show the hold in the label, "Hold to delete project", so people know how to act.

**Can keyboard users confirm an action?** Yes. Hold Space or Enter. Auto-repeat from holding the key does not restart
the fill, and releasing the key before the hold completes rewinds it. Enter is handled by the button, so it does not
press the window's default button while the control has focus.

**How do I reset the button after a successful undo?** Set `IsConfirmed` to `false`, for example `project.IsConfirmed = false;`.
The fill rewinds as the label changes back to `Label`, so the change is visible without a separate message.

**How do I change the hold time for one button only?** Set `HoldDuration` on that button. The help text for
assistive technology is recalculated when `HoldDuration` changes, so it stays accurate.

**Why does `IsHolding` read false when the hold completes?** `HoldingChanged` is raised for the end of the hold before
`IsConfirmed` is set. Read `IsConfirmed` in the `Confirmed` event for the done state, not in `HoldingChanged`.

## API reference

<!-- api: HoldToConfirm -->

### Tone values

| Value | Meaning |
|---|---|
| `Accent` | Fills with the selected accent (the Arc default) |
| `Danger` | For an irreversible action: danger ink and fill |
| `Neutral` | Fills with the foreground color |

## Accessibility

- `Label` names the button. After the hold completes, the name follows `ConfirmedLabel`.
- The help text says how to confirm: press and hold for the duration, or hold Space or Enter with a keyboard. It is
  generated from `HoldDuration` and is removed once the action is done.
- The completed state is announced once through a polite status region, and the button reports that it is no longer
  available but keeps focus.
- Holding with Space or Enter uses the keyboard; key auto-repeat does not restart the hold. Moving focus away cancels
  the hold, and so does moving the pointer more than 24 px from the button.
- The text is drawn twice, once on the surface and once inside the fill. Its color changes exactly at the fill edge, so
  the contrast holds at every point of the hold.
- Assistive technology reads the hold instruction as the button's help text, so a screen reader user hears how to
  confirm before the action runs.

### Keyboard

| Keys | Action |
|---|---|
| `Space` or `Enter`, held | Starts the fill; the hold completes after `HoldDuration` |
| `Space` or `Enter`, released early | Rewinds the fill; nothing is confirmed |
| Key auto-repeat while held | Ignored; it does not restart the fill |
| `Tab` / `Shift+Tab` | Moves focus; leaving the button cancels a hold in progress |
| A quick press of `Space` or `Enter` | Does nothing; a hold must last for `HoldDuration` |

## Motion

- While held, the fill moves across the button at a steady rate for the time that is left. Letting go early rewinds
  it on a smooth spring that keeps no overshoot.
- The label and icon change with a short blur-and-slide when the hold completes, and the icon draws a check.
- Reduced motion removes the press scale and the rewind spring. The label and icon fade, and the fill still follows
  the hold because it shows progress.
- Off motion keeps only the fill progress. Everything else changes at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Fill while held | Advances across the button at a steady rate | Advances with the hold | Advances with the hold |
| Early release | Rewinds on a smooth spring with no overshoot | Jumps back without the spring | Immediate |
| Press scale while held | Scales to 0.97 (0.985 when wider than 220 px) on a snappy spring | None | None |
| Label and icon at completion | Blur-and-slide; the check mark draws itself | Fade | Immediate |
| Label and icon on reset | The label morphs back as the fill rewinds | Fade | Immediate |

Use motion to show progress, not decoration. The fill is the only element that matters while the person holds the
button, so it stays on every motion level. Reduced motion removes the press scale and the spring, which move the
button itself and can cause discomfort for some people.

## Related

- `Button` and `ActionButton` for actions that do not need a hold.
- `Dialog` when the person must read the consequences before confirming.
- `SwipeActions` for deleting a row in a touch list.
- `ConfirmMorph` when the button should ask a question in place, with a Cancel path or a countdown.

| Component | Choose it when |
|---|---|
| `Button` / `ActionButton` | The action is safe, reversible or common, and one click is enough |
| `ConfirmMorph` | The button should ask in place, with a Cancel path or a countdown, before it acts |
| `Dialog` | The consequences must be read, or the action affects several records at once |
| `SwipeActions` | The action belongs to one row in a touch list such as an inbox or an order queue |
| `Select` / `Combobox` | The person chooses a value from a set instead of committing an action |
| `Toast` | The action already ran and the result should be reported without blocking the page |
