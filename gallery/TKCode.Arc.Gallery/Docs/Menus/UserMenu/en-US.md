---
title: User menu
summary: Your account, settings, theme, and sign out behind the avatar. Opens as a bottom sheet on phones.
---

<Demo name="TopBar" />

## When to use

- The one account entry point in an app's top bar, behind the avatar.
- Two to four account destinations, such as Profile, Settings and Billing, with a theme choice and sign out.
- A presence status switch for a team app, shown on the avatar.

## When not to use

- A general list of commands that is not about the account: use `DropdownMenu`.
- A theme switch on its own, outside an account menu: use `ThemeSwitcher`.
- More than four destinations: move the extra ones into a page and link to it.
- A menu opened from a row or a card: use `ContextMenu` or `DropdownMenu`.

## Usage

Set the identity properties and declare the destinations under `UserMenu.Items`. The avatar is the trigger.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
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

### Asynchronous sign out

`SignOutAction` runs when the sign-out row is chosen. While the task runs, the row shows a spinner and the menu stays
open. The menu closes when the task finishes.

```csharp
userMenu.SignOutAction = () => auth.SignOutAsync();
userMenu.SignOutRequested += (_, _) => status.Text = "Signing out…";
```

### Photo and destinations as data

`AvatarSource` shows a photo. Without one, or while the photo loads, the initials are shown. `ItemsSource` creates
the destinations from data when `Items` is not enough.

```xml
<arc:UserMenu DisplayName="{Binding Name}"
              Email="{Binding Email}"
              AvatarSource="{Binding Avatar}"
              ItemsSource="{Binding AccountLinks}" />
```

## API reference

<!-- api: UserMenu -->

<!-- api: MenuEntry -->

## Accessibility

- The avatar trigger is announced as an account menu with the display name. The status is added to the name when shown.
- The rows have the menu item role. The theme and status switches are named radio groups.
- The arrow keys choose within a theme or status switch, and Enter or Space activates a row.
- Escape or Tab closes the menu and returns focus to the avatar.
- The presence dot changes shape as well as colour, so it does not rely on colour alone.

## Motion

- On wide windows the panel grows out of the avatar without overshoot, and its rows fade in one after another.
- A single highlight glides between rows, and the theme and status thumbs slide to the chosen option.
- Below 640px the menu opens as a bottom sheet that rises from the bottom edge. It follows a downward drag and closes
  when dragged far enough.
- Reduced motion uses a short fade for the panel and switches, jumps the highlight and disables the drag. Off shows the
  menu at once.

## Related

- `DropdownMenu` for a general command list behind a labelled trigger.
- `ThemeSwitcher` for a light and dark switch that is not part of an account menu.
- `Avatar` is the picture the trigger shows and can be reused in the identity header.
- `MenuSurface` is the shared menu popup that the panel and the bottom sheet are built on.
