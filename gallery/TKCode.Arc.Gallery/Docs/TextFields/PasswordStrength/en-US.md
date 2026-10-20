---
title: Password strength
summary: Show how strong a new password is while it is typed.
---

## When to use

- Sign-up forms and any screen where the user chooses a new password.
- Password change forms, where the new value must meet a set of rules.
- Places where the user needs to see which rules are still missing.

## When not to use

- Signing in to an existing account: use `PasswordField`, which has no meter or rules.
- A measured quantity that is not a password, such as upload progress: use `Progress`.
- Text that is not secret: use `Input`.

## Usage

The meter, the strength word and the rule list update on every key press. Scoring runs on the device with the
default rules: a minimum length of 12 characters, upper and lower case, a number and a symbol.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:PasswordStrength Label="New password"
                          Placeholder="Choose a password"
                          Width="400"
                          HorizontalAlignment="Left" />
</UserControl>
```

### Bind the value

`Text` is a two-way property. Bind `ErrorMessage` to show a server-side or form-level error.

```xml
<arc:PasswordStrength Label="New password"
                      Text="{Binding NewPassword}"
                      ErrorMessage="{Binding NewPasswordError}" />
```

### React to the strength

`StrengthChanged` is raised after every scored change. `Level` runs from 0 (empty) to 4 (strong), and
`Score.MetRuleIds` lists the rules that are met.

```csharp
create.StrengthChanged += (_, e) =>
{
    result.Text = e.Level == 0
        ? "Strength: none yet"
        : $"Strength: {e.Label} (level {e.Level}, {e.Score.MetRuleIds.Count} rules met)";
};
```

### Custom rules

Pass a rule list to `Rules`. Build the default rules with a different minimum length, or write your own
`PasswordRule` with a test and an optional count of what remains.

```csharp
custom.Rules = PasswordRules.Create(minLength: 16);
custom.ShortLength = 10;
```

```csharp
var rules = new List<PasswordRule>
{
    new("length", "At least 10 characters", p => p.Length >= 10),
    new("digit", "Contains a digit", p => p.Any(char.IsDigit))
};
```

Below `ShortLength` characters, the meter stays at its first step and shows "Too short".

### Errors

Set `ErrorMessage` to mark the field invalid. Each new error shakes the field once.

```csharp
withError.ErrorMessage = withError.Level < 3 ? "Choose a stronger password." : null;
```

## API reference

<!-- api: PasswordStrength -->

## Accessibility

- `Label` names the field. The rule list and the error are part of its help text.
- The meter reports a value from 0 to 4 with the strength word as its text.
- A summary of the strength and the rules met is announced politely as the user types.
- Enter or Space on the reveal toggle shows or hides the password. Caret, selection, IME, undo and clipboard
  shortcuts are the native `TextBox` behavior.

## Motion

- The four meter segments fill in a wave and change tone. The strength word rises or drops with the level.
- A rule ticks off with a small scale-in and a line drawn through its mark. The count of remaining rules rolls.
- A new error shakes the field once.
- Reduced motion removes the shake, the wave and the rolling count, and changes the values at once. Off does the same.

## Related

- `PasswordField` for signing in, where the password is checked rather than chosen.
- `Input` for text that is not secret.
- `Progress` for a measured value that is not a password.
