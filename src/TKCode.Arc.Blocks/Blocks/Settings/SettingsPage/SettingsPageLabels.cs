namespace TKCode.Arc;

/// <summary>The built-in text of a <see cref="SettingsPage"/>. Replace the whole object to translate it.</summary>
public sealed class SettingsPageLabels
{
    /// <summary>The note in the save bar.</summary>
    public string UnsavedChanges { get; init; } = "Unsaved changes";

    /// <summary>The save bar's primary button.</summary>
    public string Save { get; init; } = "Save changes";

    /// <summary>The save bar's secondary button.</summary>
    public string Discard { get; init; } = "Discard";

    /// <summary>The accessible name of the section navigation.</summary>
    public string NavigationName { get; init; } = "Settings sections";

    /// <summary>The accessible text of the dot on a section with unsaved changes.</summary>
    public string UnsavedSection { get; init; } = "has unsaved changes";
}
