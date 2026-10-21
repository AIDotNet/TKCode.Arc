namespace TKCode.Arc;

/// <summary>
/// The built-in text of a <see cref="SettingsCommand"/>. Replace the whole object to localize it; texts with values are
/// functions.
/// </summary>
public sealed class SettingsCommandLabels
{
    /// <summary>The search field's placeholder.</summary>
    public string Placeholder { get; init; } = "Search settings";

    /// <summary>The search field's accessible name.</summary>
    public string SearchName { get; init; } = "Search settings";

    /// <summary>The accessible name of the result list.</summary>
    public string ResultsName { get; init; } = "Settings";

    /// <summary>The message when nothing matches the query.</summary>
    public Func<string, string> NoResults { get; init; } = query => $"No settings match “{query}”";

    /// <summary>The hint under <see cref="NoResults"/>.</summary>
    public string NoResultsHint { get; init; } = "Try another word, such as theme or email.";

    /// <summary>The footer hint beside the up and down keys.</summary>
    public string Navigate { get; init; } = "Navigate";

    /// <summary>The footer hint beside Enter while a toggle is active.</summary>
    public string Toggle { get; init; } = "Toggle";

    /// <summary>The footer hint beside Enter while a choice is active.</summary>
    public string Change { get; init; } = "Change";

    /// <summary>The footer hint beside Enter while an action is active.</summary>
    public string Open { get; init; } = "Open";

    /// <summary>The footer hint beside Escape.</summary>
    public string Close { get; init; } = "Close";

    /// <summary>The Escape key cap.</summary>
    public string EscapeKey { get; init; } = "Esc";

    /// <summary>The change notice after a toggle turns on, from its header.</summary>
    public Func<string, string> TurnedOn { get; init; } = header => $"{header} on";

    /// <summary>The change notice after a toggle turns off, from its header.</summary>
    public Func<string, string> TurnedOff { get; init; } = header => $"{header} off";

    /// <summary>The change notice after a choice changes, from its header and the new value.</summary>
    public Func<string, string, string> ChangedTo { get; init; } = (header, value) => $"{header}: {value}";

    /// <summary>The group title of settings without a <see cref="SettingsCommandItem.Group"/> (shown when other settings have one).</summary>
    public string Ungrouped { get; init; } = "Settings";

    /// <summary>The state of a toggle that is on, read with the row ("Dark mode, on").</summary>
    public string On { get; init; } = "on";

    /// <summary>The state of a toggle that is off.</summary>
    public string Off { get; init; } = "off";

    /// <summary>What assistive technology reads for a row: its header and its state or value.</summary>
    public Func<string, string, string> RowState { get; init; } = (header, state) => $"{header}, {state}";

    /// <summary>The accessible name of the arrow before a choice's value.</summary>
    public string PreviousOption { get; init; } = "Previous option";

    /// <summary>The accessible name of the arrow after a choice's value.</summary>
    public string NextOption { get; init; } = "Next option";
}
