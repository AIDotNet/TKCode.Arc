using System;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="AgentMemory"/> shows or announces, in English by default. Replace the instance to translate or
/// reword; formatted texts are functions so that word order can change.
/// </summary>
public sealed class AgentMemoryLabels
{
    /// <summary>The title of the card.</summary>
    public string Title { get; init; } = "Memory";

    /// <summary>The line under the title; the argument is <see cref="AgentMemory.AssistantName"/>.</summary>
    public Func<string, string> Subtitle { get; init; } = name => $"What {name} remembers to keep your chats on track";

    /// <summary>The accessible name of the capacity meter.</summary>
    public string MeterName { get; init; } = "Memory used";

    /// <summary>The visible word after the meter's percentage.</summary>
    public string MeterFull { get; init; } = "full";

    /// <summary>The meter's accessible value text; the argument is the percentage.</summary>
    public Func<int, string> MeterValueText { get; init; } = percent => $"{percent}% full";

    /// <summary>The title of the first switch.</summary>
    public string SavedMemoriesTitle { get; init; } = "Reference saved memories";

    /// <summary>The first switch's explanation while on; the argument is the assistant's name.</summary>
    public Func<string, string> SavedMemoriesOn { get; init; } = name => $"{name} uses these memories to personalize replies";

    /// <summary>The first switch's explanation while off.</summary>
    public Func<string, string> SavedMemoriesOff { get; init; } = name => $"Paused. {name} does not use saved memories";

    /// <summary>The title of the second switch.</summary>
    public string ChatHistoryTitle { get; init; } = "Reference chat history";

    /// <summary>The second switch's explanation while on.</summary>
    public Func<string, string> ChatHistoryOn { get; init; } = name => $"{name} can look back at past chats for context";

    /// <summary>The second switch's explanation while off.</summary>
    public Func<string, string> ChatHistoryOff { get; init; } = name => $"{name} starts every chat fresh";

    /// <summary>The line that replaces the captured pill while saved memories are paused.</summary>
    public Func<string, string> PausedNotice { get; init; } =
        name => $"Saved memories are paused. {name} will not note or use new ones until you turn them back on.";

    /// <summary>The name of the section ("Saved memories").</summary>
    public string SectionName { get; init; } = "Saved memories";

    /// <summary>The filter pill that shows every group.</summary>
    public string FilterAll { get; init; } = "All";

    // The four default groups, used while AgentMemory.Groups is empty. Their keys are "about", "preferences",
    // "projects" and "people"; the empty sentences take the assistant's name.

    /// <summary>The label of the default group "about".</summary>
    public string GroupAbout { get; init; } = "About you";

    /// <summary>The label of the default group "preferences".</summary>
    public string GroupPreferences { get; init; } = "Preferences";

    /// <summary>The label of the default group "projects".</summary>
    public string GroupProjects { get; init; } = "Projects";

    /// <summary>The label of the default group "people".</summary>
    public string GroupPeople { get; init; } = "People";

    /// <summary>The sentence of the empty default group "about"; the argument is the assistant's name.</summary>
    public Func<string, string> EmptyAbout { get; init; } = _ => "Facts about you show up here once you mention them.";

    /// <summary>The sentence of the empty default group "preferences"; the argument is the assistant's name.</summary>
    public Func<string, string> EmptyPreferences { get; init; } = name => $"No preferences yet. Tell {name} how you like answers.";

    /// <summary>The sentence of the empty default group "projects"; the argument is the assistant's name.</summary>
    public Func<string, string> EmptyProjects { get; init; } = _ => "No projects yet. Ongoing work you bring up will be listed here.";

    /// <summary>The sentence of the empty default group "people"; the argument is the assistant's name.</summary>
    public Func<string, string> EmptyPeople { get; init; } = _ => "No people yet. Colleagues you mention often appear here.";

    /// <summary>The accessible name of the filter group.</summary>
    public string FilterName { get; init; } = "Filter memories";

    /// <summary>The accessible name of the search button, and the search box.</summary>
    public string Search { get; init; } = "Search memories";

    /// <summary>The placeholder of the search box.</summary>
    public string SearchPlaceholder { get; init; } = "Search memories";

    /// <summary>The accessible name of the button that closes the search.</summary>
    public string CloseSearch { get; init; } = "Close search";

    /// <summary>The sentence for a search without matches; the argument is the query.</summary>
    public Func<string, string> NoMatches { get; init; } = query => $"No memories match “{query}”";

    /// <summary>The text button under it that clears the search.</summary>
    public string ClearSearch { get; init; } = "Clear search";

    /// <summary>The title of the captured pill.</summary>
    public string Captured { get; init; } = "Memory updated";

    /// <summary>The captured pill after filing; the argument is the group's label.</summary>
    public Func<string, string> SavedTo { get; init; } = group => $"Saved to {group}";

    /// <summary>The accessible name of the captured pill; arguments are the note and the group's label.</summary>
    public Func<string, string, string> CapturedName { get; init; } = (note, group) => $"Memory updated. {note}. File under {group}";

    /// <summary>"From a chat" in a memory's source line.</summary>
    public string SourceChat { get; init; } = "From a chat";

    /// <summary>"Added by you" in a memory's source line.</summary>
    public string SourceManual { get; init; } = "Added by you";

    /// <summary>The short date in a source line.</summary>
    public Func<DateTimeOffset, string> Date { get; init; } = date => date.ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The tooltip of the edit button.</summary>
    public string Edit { get; init; } = "Edit";

    /// <summary>The tooltip of the forget button.</summary>
    public string Forget { get; init; } = "Forget";

    /// <summary>The accessible name of the edit button; the argument is the memory's sentence.</summary>
    public Func<string, string> EditName { get; init; } = text => $"Edit memory: {text}";

    /// <summary>The accessible name of the forget button.</summary>
    public Func<string, string> ForgetName { get; init; } = text => $"Forget memory: {text}";

    /// <summary>The hint under the editor.</summary>
    public string EditHint { get; init; } = "Enter to save, Esc to cancel";

    /// <summary>The cancel button of the editor.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The save button of the editor.</summary>
    public string Save { get; init; } = "Save";

    /// <summary>The live character count once the limit is passed; arguments are the length and the limit.</summary>
    public Func<int, int, string> CharacterCount { get; init; } = (length, limit) => $"{length} of {limit} characters";

    /// <summary>The error for an empty memory.</summary>
    public string EmptyError { get; init; } = "Write something to remember";

    /// <summary>The error for a memory over the limit; the argument is the limit.</summary>
    public Func<int, string> TooLongError { get; init; } = limit => $"Keep it under {limit} characters";

    /// <summary>The error when saving failed and no message came with the exception.</summary>
    public string SaveFailed { get; init; } = "Couldn’t save. Try again";

    /// <summary>The text of a forgotten memory's bar; the argument is the memory's sentence.</summary>
    public Func<string, string> Forgot { get; init; } = text => $"Forgot “{text}”";

    /// <summary>The accessible name of the forgotten memory's bar.</summary>
    public string ForgottenName { get; init; } = "Forgotten memory";

    /// <summary>The undo button.</summary>
    public string Undo { get; init; } = "Undo";

    /// <summary>The remaining seconds in the undo button; the argument is the seconds left.</summary>
    public Func<int, string> Seconds { get; init; } = seconds => $"{seconds}s";

    /// <summary>The accessible name of the remaining seconds.</summary>
    public Func<int, string> SecondsLeft { get; init; } = seconds => seconds == 1 ? "1 second left" : $"{seconds} seconds left";

    /// <summary>The placeholder (and accessible name) of the add field.</summary>
    public string AddPlaceholder { get; init; } = "Add memory";

    /// <summary>The add button.</summary>
    public string Add { get; init; } = "Add";

    /// <summary>The shimmering word while a new memory is sorted into a group.</summary>
    public string Sorting { get; init; } = "Sorting into a group";

    /// <summary>The status after adding; the argument is the group's label.</summary>
    public Func<string, string> AddedTo { get; init; } = group => $"Added to {group}";

    /// <summary>The status when the capacity is used up.</summary>
    public string Full { get; init; } = "Memory is full";

    /// <summary>The status when sorting failed.</summary>
    public string SortFailed { get; init; } = "Couldn’t sort. Try again";

    // Announcements (polite live region).

    /// <summary>Announced while searching; the argument is the number of matches.</summary>
    public Func<int, string> MatchCount { get; init; } = count => count switch
    {
        0 => "No matches",
        1 => "1 match",
        _ => $"{count} matches"
    };

    /// <summary>Announced after an edit was saved.</summary>
    public string AnnounceUpdated { get; init; } = "Memory updated";

    /// <summary>Announced when a memory is forgotten; the argument is its sentence and the seconds of the undo window.</summary>
    public Func<string, int, string> AnnounceForgot { get; init; } = (text, seconds) => $"Forgot “{text}”. You can undo for {seconds} seconds";

    /// <summary>Announced when the undo window ends.</summary>
    public string AnnounceForgotten { get; init; } = "Memory forgotten";

    /// <summary>Announced when forgetting could not be saved and the memory came back (the hook's message wins when it has one).</summary>
    public string ForgetFailed { get; init; } = "Couldn’t forget that memory. It is back in the list";

    /// <summary>Announced after undoing.</summary>
    public string AnnounceRestored { get; init; } = "Memory restored";

    /// <summary>Announced after filing a captured memory; arguments are the group's label and the sentence.</summary>
    public Func<string, string, string> AnnounceSaved { get; init; } = (group, text) => $"Saved to {group}: {text}";

    /// <summary>Announced after adding; arguments are the group's label and the sentence.</summary>
    public Func<string, string, string> AnnounceAdded { get; init; } = (group, text) => $"Added to {group}: {text}";

    /// <summary>Announced when the capacity is used up.</summary>
    public string AnnounceFull { get; init; } = "Memory is full. Forget something to add more";

    /// <summary>Announced when a switch changes; the argument is the setting and whether it is on.</summary>
    public Func<MemorySetting, bool, string> AnnounceSetting { get; init; } = (setting, isOn) => setting switch
    {
        MemorySetting.SavedMemories => isOn ? "Saved memories on" : "Saved memories paused",
        _ => isOn ? "Chat history on" : "Chat history off"
    };
}
