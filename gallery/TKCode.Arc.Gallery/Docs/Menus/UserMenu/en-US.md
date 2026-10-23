---
title: User menu
summary: Your account, settings, theme, and sign out behind the avatar. Opens as a bottom sheet on phones.
---

<Demo name="TopBar" />

## When to use

A user menu is the one account entry point in an app's top bar. It sits behind the avatar and gathers what belongs to
the signed-in person: the identity header, two to four account destinations, a theme choice, an optional presence
status and sign out. It keeps account concerns out of the page toolbar, so the page's own actions stay visible and the
account stays one click away.

- **Account destinations in a SaaS admin console**: Profile, Workspace settings and Billing behind the avatar of an
  admin who manages several workspaces. The plan badge shows Admin or Pro beside the name.
- **Presence in a team app**: an incident console where the on-call engineer sets Busy during a response, and the dot
  on the avatar tells teammates the status without a chat message.
- **Theme choice in a dashboard or design tool**: Light, Dark or System, chosen in the menu. The menu stays open after
  the choice so the page can change theme in front of the person.
- **Sign out of a session that takes time**: a sign-out that revokes tokens on the server. The row shows a spinner until
  the task finishes, and the menu then closes.
- **Narrow windows and phones**: the same menu opens as a bottom sheet below 640 px, so the account stays reachable on
  a phone without a second navigation pattern.

## When not to use

- **A general list of commands that is not about the account**: use `DropdownMenu` behind a labelled trigger, such as
  an Actions menu on a report page.
- **A theme switch on its own, outside an account menu**: use `ThemeSwitcher`, which is a light and dark control that
  is not tied to a person.
- **More than four destinations**: move the extra ones to a page and link to it from one destination. A long menu
  stops being a shortcut.
- **A menu opened from a row or a card**: use `ContextMenu` for right-click actions on the object, or `DropdownMenu` on
  a visible trigger.
- **A form value picked from a list**: use `Select` or `Combobox`. The user menu carries commands and two switches, not
  a value that feeds a form.

## Usage

Set the identity properties and declare the destinations under `UserMenu.Items`. The avatar is the trigger.

`UserMenu` derives from Avalonia's `TemplatedControl`. Its trigger is an Arc `Button` (Ghost, a 40 px pill) that holds
the avatar, the presence dot and the name. Its panel is a `MenuSurface` in the Account variant, which the control
configures and owns. Below 640 px the same surface switches to the Sheet variant. The panel holds a header built from
the identity properties, the destination rows, the theme and status switches and the sign-out row.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:UserMenu DisplayName="Ada Lovelace"
                  Email="ada@analytical.dev"
                  Plan="Pro"
                  IsNameVisible="True"
                  SignOutGestureText="⇧⌘Q">
        <arc:UserMenu.Items>
            <arc:MenuEntry Header="Profile" />
            <arc:MenuEntry Header="Settings" InputGestureText="⌘ ," />
            <arc:MenuEntry Header="Billing" />
        </arc:UserMenu.Items>
    </arc:UserMenu>
</UserControl>
```

### Anatomy

1. **Trigger** (the Ghost `arc:Button` in the template): a 40 px pill with a 4 px inset. It has a 10 px right inset when
   the name shows. It never scales when pressed; pressing and opening change only its background color.
2. **Avatar** (`DisplayName`, `AvatarSource`): a 32 px avatar. With `AvatarSource` set, the photo shows; while the photo
   loads, or when it fails, the initials show.
3. **Presence dot** (`Status`, `IsStatusVisible`): an 11 px dot at the bottom right of the avatar, ringed by 2 px of the
   trigger's background color. Available is a solid success-colored dot, Busy is a danger-colored dot with a bar and
   Away is a warning-colored dot with a hollow center, so the status reads without color.
4. **Name** (`DisplayName`, `IsNameVisible`): medium weight, up to 176 px, truncated with an ellipsis. It shows beside
   the avatar only when `IsNameVisible` is `True` and the window is at least 640 px wide.
5. **Chevron**: 16 px in the muted text color. It shows with the name and turns 180 degrees while the menu is open.
6. **Panel** (`PanelAlignment`): an Account panel 248 px wide (15.5 rem), 8 px away from the avatar. The default
   alignment is `End`, so the panel lines up with the trigger's right edge.
7. **Identity header**: a 34 px avatar with the presence dot when status shows, the name with a `Plan` badge beside
   it, and the `Email` under the name. A long name, plan or email ends in an ellipsis instead of widening the panel.
8. **Destinations** (`Items` or `ItemsSource`): rows of 32 px with 13 px text, each with an optional icon and a shortcut
   hint at the end.
9. **Theme switch** (`ThemePreference`, `IsThemeSwitchVisible`): a Theme row with three segments, Light (sun icon),
   Dark (moon icon) and System (monitor icon). It is 36 px tall in the panel and 52 px in the sheet.
10. **Status switch** (`Status`, `IsStatusVisible`): a Status row with three segments, Available, Busy and Away, at the
    same heights as the theme switch.
11. **Sign out row**: a LogOut icon, the label "Sign out" and the `SignOutGestureText` hint. While the sign-out task
    runs, the label reads "Signing out" and the icon becomes a spinner.
12. **Separators**: a line between the destinations, the switches and the sign-out row.

### Choosing the layout

- Show the name (`IsNameVisible="True"`) in a wide top bar where the person's name helps them confirm the account. Hide
  it in a narrow toolbar, where the avatar alone is enough.
- Keep `IsThemeSwitchVisible` at its default `True` in an app that supports both themes. Set it to `False` when the app
  has one theme, so the menu does not offer a choice that does nothing.
- Show the presence dot (`IsStatusVisible="True"`) only in an app where teammates use the status. A dot that nobody
  sets adds noise to every avatar.
- Use `PanelAlignment="Start"` when the avatar sits at the left edge of a window, so the panel opens toward the content.

### Theme and status

`ThemePreference` is `Light`, `Dark` or `System`. The menu reports the choice and stays open. The app applies the theme
itself. `IsThemeSwitchVisible="False"` hides the switch.

<Demo name="Presence" />

```xml
<arc:UserMenu DisplayName="Grace Hopper"
              Email="grace@compiler.dev"
              ThemePreference="{Binding ThemePreference, Mode=TwoWay}"
              Status="{Binding Status, Mode=TwoWay}"
              IsStatusVisible="True" />
```

```csharp
userMenu.ThemePreferenceChanged += (_, e) => ApplyTheme(e.ThemePreference);
userMenu.StatusChanged += (_, e) => status.Text = $"Status: {e.Status}";
```

`Status` and `ThemePreference` are two-way bound, so the view model always holds the current choice. The events fire
only for a choice made in the menu. Use them to apply the theme or to send the status to the server, because the
menu does neither itself.

### Asynchronous sign out

`SignOutAction` runs when the sign-out row is chosen. While the task runs, the row shows a spinner and the menu stays
open. The menu closes when the task finishes.

```csharp
userMenu.SignOutAction = () => auth.SignOutAsync();
userMenu.SignOutRequested += (_, _) => status.Text = "Signing out…";
```

`SignOutRequested` fires before the action runs, so it is the place to show a status line or to clear a local cache.
If the action throws, the menu still stops waiting and closes; report the failure from the action itself, because the
menu does not show it. When no action is set, or the task has already finished, the menu closes at once and returns
focus to the avatar.

### Photo and destinations as data

`AvatarSource` shows a photo. Without one, or while the photo loads, the initials are shown. `ItemsSource` creates
the destinations from data when `Items` is not enough.

```xml
<arc:UserMenu DisplayName="{Binding Name}"
              Email="{Binding Email}"
              AvatarSource="{Binding Avatar}"
              ItemsSource="{Binding AccountLinks}" />
```

Each generated row shows the item, or the `ItemTemplate` result when one is set. `ItemClicked` reports the row and its
data item in `e.Item`, so a single handler can route each destination to a page.

### Binding to a view model

The view model owns the identity, the theme, the status and the sign-out logic. The menu shows them and reports back
through its two-way properties and its events. Keep the account destinations in the view model as data, so the menu
and the navigation stay in step.

```csharp
public partial class AccountViewModel : ObservableObject
{
    private readonly IAuthService _auth;

    public AccountViewModel(IAuthService auth) => _auth = auth;

    [ObservableProperty]
    private string _displayName = "Ada Lovelace";

    [ObservableProperty]
    private string _email = "ada@analytical.dev";

    [ObservableProperty]
    private UserMenuThemePreference _themePreference = UserMenuThemePreference.System;

    [ObservableProperty]
    private UserMenuStatus _status = UserMenuStatus.Available;

    [RelayCommand]
    private Task SignOutAsync() => _auth.SignOutAsync();
}
```

```xml
<arc:UserMenu DisplayName="{Binding DisplayName}"
              Email="{Binding Email}"
              IsNameVisible="True"
              ThemePreference="{Binding ThemePreference, Mode=TwoWay}"
              IsStatusVisible="True"
              Status="{Binding Status, Mode=TwoWay}" />
```

```csharp
userMenu.SignOutAction = () => viewModel.SignOutCommand.ExecuteAsync(null);
```

- `ThemePreference` and `Status` are two-way, so a theme chosen in the menu reaches the view model, and a status set in
  code reaches the dot and the switch.
- `DisplayName` and `Email` are one-way: the view model changes them after a profile update and the header follows.
- Use `ThemePreferenceChanged` to apply the theme, and `StatusChanged` to save the status. The two-way value only
  records the choice; the events are where the app acts on it.
- The sign-out command is a `Func<Task>`, so assign it in code, or wrap the command in a method that returns a `Task`.

### Focus and dismissal

A pointer opens the menu with focus on the panel and no row highlighted. A keyboard opening highlights the first row
from Enter, Space or ArrowDown, or the last row from ArrowUp. A menu that starts open, with `IsOpen` set when the page
loads, does not take focus or scroll the page.

Choosing a destination closes the menu. The theme and status switches keep it open, so a person can flip both in one
visit. The sign-out row keeps the menu open with its spinner until the task settles, then closes it. An outside press
closes the menu; on a sheet, a press on the scrim does the same.

The menu changes its form with the window. When the window crosses the 640 px breakpoint while the menu is open, the
menu closes rather than turning a panel into a sheet, so the person reopens it in the new form. Escape and Tab close the
menu and return focus to the avatar.

### Example: operations console top bar

The top bar of an incident console shows the on-call engineer's account. The name is visible because the console is a
wide window, and the presence dot shows Busy so teammates know the engineer is in a response. The plan badge reads
Admin, and the menu holds three destinations, with sign out last and its shortcut hint. The theme switch stays at its
default, because responders work in dark rooms and need the choice without a settings page.

```xml
<Border Padding="16,8"
        CornerRadius="18"
        BorderThickness="1"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}">
    <DockPanel>
        <TextBlock DockPanel.Dock="Left"
                   Text="Incident console"
                   FontSize="15"
                   FontWeight="Medium"
                   VerticalAlignment="Center"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <arc:UserMenu DockPanel.Dock="Right"
                      DisplayName="Grace Hopper"
                      Email="grace@compiler.dev"
                      Plan="Admin"
                      IsNameVisible="True"
                      IsStatusVisible="True"
                      Status="Busy"
                      SignOutGestureText="⇧⌘Q">
            <arc:MenuEntry Header="Profile" Icon="{x:Static arc:ArcIcons.User}" />
            <arc:MenuEntry Header="On-call schedule" Icon="{x:Static arc:ArcIcons.Clock}" />
            <arc:MenuEntry Header="Billing" Icon="{x:Static arc:ArcIcons.CreditCard}" />
        </arc:UserMenu>
    </DockPanel>
</Border>
```

### Content guidelines

The menu is short and read at a glance. Destinations are nouns for the page they open, and the switches and the sign-out
row use the words the app uses everywhere else.

- Name a destination after its page: "Billing", "Workspace settings". Keep it under about 24 characters.
- Use "Sign out" for the sign-out row, a verb and its particle, with no exclamation mark or confirmation text in the label.
- Keep the plan badge to a word such as "Pro" or "Admin"; it is a badge, not a sentence.
- Use the three statuses as they are. The menu offers Available, Busy and Away; an "Offline" status is not one of them.
- Write theme options as single words: Light, Dark, System.

| Instead of | Write |
|---|---|
| Logout | Sign out |
| My account settings | Profile |
| Dark mode | Dark |
| Offline | Away |
| Sign out now! | Sign out |

### Best practices

**Do**

- Keep the destinations to two to four. A fifth destination belongs on a page, linked from one of the four.
- Keep sign out as the last row. The layout already separates it; do not add rows below it.
- Show the presence dot only in an app where teammates set and read the status. A dot that nobody sets is noise on
  every avatar.
- Apply the theme in `ThemePreferenceChanged`, so the choice takes effect while the menu is still open.
- Set `Email` and `Plan` to values that fit. Long values are truncated, so a short name and plan read better.

**Avoid**

- Long settings forms in the menu. A form belongs in a `Drawer` or on a page; the menu is for choices and navigation.
- A long list of accounts or workspaces. Use a `Select` or a page for a switcher, because the menu shows only a few rows
  comfortably.
- Designing the page around a setting in the menu. The breakpoint is fixed at 640 px, so design the page for both the
  panel and the sheet.
- Running the sign-out work without feedback. An unfinished task shows the spinner; a task that finishes at once closes
  the menu without one.
- Adding a custom switch to the menu. The API offers the theme and status switches only; other choices belong in a
  `DropdownMenu` or a page.

### Common questions

**Why does the name disappear on my narrow window?** Below 640 px the trigger shows the avatar alone, and the menu opens as a
bottom sheet. `CompactBreakpoint` is a fixed constant of 640, so plan the layout for the avatar-only trigger in narrow
windows.

**Why is the presence dot missing?** `IsStatusVisible` defaults to `False`. Set it to `True` to show the dot and the
status switch.

**How do I hide the theme choice?** Set `IsThemeSwitchVisible="False"`. The theme row then leaves the panel, and the app
can apply its own theme control elsewhere.

**How does the sign-out spinner appear?** Return an unfinished `Task` from `SignOutAction`. The row shows the spinner and
the label "Signing out" until the task completes, then the menu closes.

**Can I show the name and the chevron on a narrow window?** No. The name and the chevron show only on windows at least
640 px wide, and the sheet keeps the avatar alone. Plan the page layout around that, because the menu does not expose the
breakpoint as a setting.

## API reference

<!-- api: UserMenu -->

<!-- api: MenuEntry -->

The two choice types that the menu reports:

| Type | Values | Meaning |
|---|---|---|
| `UserMenuThemePreference` | `Light`, `Dark`, `System` | The theme the person chose; the default is `System` |
| `UserMenuStatus` | `Available`, `Busy`, `Away` | The presence shown by the dot and the status switch; the default is `Available` |

## Accessibility

- The avatar trigger is announced as an account menu with the display name. The status is added to the name when shown.
- The rows have the menu item role. The theme and status switches are named groups, and each option is selectable.
- The arrow keys choose within a theme or status switch, and Enter or Space activates a row.
- Escape or Tab closes the menu and returns focus to the avatar.
- The presence dot changes shape as well as colour, so it does not rely on colour alone.
- While the sign-out task runs, the sign-out row reports itself as busy.
- Give the account destinations a `Header` that says where they go, such as "Billing" or "Workspace settings". The
  rows are read out by their header.

### Keyboard

| Keys | Action |
|---|---|
| `Enter`, `Space` or `ArrowDown` on the avatar | Opens the menu with the first row highlighted |
| `ArrowUp` on the avatar | Opens the menu with the last row highlighted |
| Click or tap on the avatar | Toggles the menu |
| `ArrowDown` / `ArrowUp` | Move the highlight to the next or previous row; the list loops and skips disabled rows |
| `Home` / `End` | Move the highlight to the first or last row |
| Letters | Typeahead moves to a row whose text starts with the typed letters, such as `S` for Sign out |
| `ArrowLeft` / `ArrowRight` on a switch | Choose the previous or next option, wrapping at the ends; the menu stays open |
| `Enter` / `Space` | Activates a destination or the sign-out row; on a switch row they do nothing |
| `Escape` / `Tab` | Closes the menu and returns focus to the avatar |

## Motion

- On wide windows the panel grows out of the avatar without overshoot, and its rows fade in one after another.
- A single highlight glides between rows, and the theme and status thumbs slide to the chosen option.
- Below 640px the menu opens as a bottom sheet that rises from the bottom edge. It follows a downward drag and closes
  when dragged far enough.
- Reduced motion uses a short fade for the panel and switches, jumps the highlight and disables the drag. Off shows the
  menu at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Wide panel opens | Grows out of the avatar from 0.94 on a smooth spring with no overshoot, then the rows fade in 16 ms apart | Short fade | Immediate |
| Wide panel closes | Scales to 0.97 and fades out within 120 ms | Short fade | Immediate |
| Highlight moves | Glides between rows for the pointer and the keyboard | Jumps | Immediate |
| Switch thumb moves | Slides to the chosen option on the 580 ms spring | Short fade | Immediate |
| Sheet opens (below 640 px) | Rises from the bottom edge on a smooth spring; rows enter 20 ms apart after 60 ms | Short fade | Immediate |
| Sheet drag | Follows a downward drag; closes past 80 px or on a fast flick | Drag disabled | Immediate |
| Chevron | Turns 180 degrees on the spring as the menu opens | Turns at once | Immediate |

## Related

- `DropdownMenu` for a general command list behind a labelled trigger.
- `ThemeSwitcher` for a light and dark switch that is not part of an account menu.
- `Avatar` is the picture the trigger shows and can be reused in the identity header.
- `MenuSurface` is the shared menu popup that the panel and the bottom sheet are built on.

| Component | Choose it when |
|---|---|
| `DropdownMenu` | The commands are not about the account and need a labelled trigger |
| `ThemeSwitcher` | A light and dark switch stands alone, outside an account |
| `Avatar` | A picture or initials identify a person in a list or a header |
| `ContextMenu` | The actions belong to one object and open at the pointer |
| `Drawer` | Account settings need a full side panel with forms, not a short menu |
| `Dialog` | A sign-out or account change needs a confirmation before it runs |
