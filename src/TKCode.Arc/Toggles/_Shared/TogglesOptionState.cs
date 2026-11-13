using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Shared by the option classes whose <c>IsDisabled</c> became <c>IsEnabled</c> (and gained <c>IsVisible</c>): the old
/// property stays as an obsolete alias until a major version removes it.
/// </summary>
internal static class TogglesOptionState
{
    /// <summary>
    /// Keeps the obsolete <c>IsDisabled</c> the inverse of <c>IsEnabled</c> in both directions, so XAML, bindings and code
    /// written against either name keep working. Setting one writes the other; the second write changes nothing, so the
    /// pair settles without a guard.
    /// </summary>
    public static void KeepDisabledAliasInStep(
        AvaloniaObject option,
        AvaloniaPropertyChangedEventArgs change,
        StyledProperty<bool> enabled,
        StyledProperty<bool> disabled)
    {
        if (change.Property == disabled)
        {
            option.SetCurrentValue(enabled, !change.GetNewValue<bool>());
        }
        else if (change.Property == enabled)
        {
            option.SetCurrentValue(disabled, !change.GetNewValue<bool>());
        }
    }
}
