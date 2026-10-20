---
title: Password field
summary: Capture sensitive text with a visible reveal control.
---

## When to use

- Sign-in forms and other places where the user types an existing secret.
- Confirmation fields that the user checks against the first entry.
- Values that must stay hidden by default but can be checked on demand.

## When not to use

- A new password that the user is choosing: use `PasswordStrength`, which shows how strong it is.
- Text that is not secret: use `Input`.
- A search query: use `SearchField`.

## Usage

The eye toggle inside the field shows or hides the text. Its name is read by assistive technology.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:PasswordField Label="Password"
                       Placeholder="Your password"
                       Description="Use the reveal toggle to check what you typed."
                       Width="360"
                       HorizontalAlignment="Left" />
</UserControl>
```

### Bind the value

`Text` is a two-way property, so bind it to a view model. Use `IsRevealed` to read or set the visible state.

```xml
<arc:PasswordField Label="Password"
                   Text="{Binding Password}"
                   ErrorMessage="{Binding PasswordError}" />
```

```csharp
signIn.PropertyChanged += (_, e) =>
{
    if (e.Property == PasswordField.IsRevealedProperty)
    {
        state.Text = signIn.IsRevealed ? "Shown" : "Hidden";
    }
};
```

### Confirmation

Set `ErrorMessage` from the second field while the two values differ. Clear it by setting it to `null`.

```csharp
void Check() => confirm.ErrorMessage = !string.IsNullOrEmpty(confirm.Text) && confirm.Text != first.Text
    ? "The passwords do not match yet."
    : null;

first.TextChanged += (_, _) => Check();
confirm.TextChanged += (_, _) => Check();
```

### Toggle names

The toggle names come from `ShowPasswordText` and `HidePasswordText`. Set them to translate the copy.

```xml
<arc:PasswordField Label="Mot de passe"
                   ShowPasswordText="Afficher"
                   HidePasswordText="Masquer" />
```

### Sizes and states

`Size` sets the frame height: `Small` is 36 and `Medium` (the default) is 44.

```xml
<arc:PasswordField Label="Small" Size="Small" Text="secret" />
<arc:PasswordField Label="Disabled" Text="secret" IsEnabled="False" />
```

## API reference

<!-- api: PasswordField -->

## Accessibility

- `Label` names the field. `Description` and `ErrorMessage` are merged into its help text.
- The reveal toggle is a toggle button with a pressed state. Its name changes between "Show password" and
  "Hide password".
- Enter or Space on the toggle shows or hides the password. Tab moves focus to and from it.
- The password is masked by default. Caret, selection, IME, undo and clipboard shortcuts are the native `TextBox`
  behavior.

## Motion

- The eye's slash draws itself across the icon when the state changes.
- After the first toggle, the text resolves from a light blur to sharp.
- The toggle scales down slightly when pressed. The helper and error row behaves like `Input`.
- Reduced motion draws the slash at once and removes the blur and the press scale. Off does the same.

## Related

- `PasswordStrength` for choosing a new password and showing its strength.
- `Input` for text that is not secret.
- `SearchField` for a query with a clear control.
