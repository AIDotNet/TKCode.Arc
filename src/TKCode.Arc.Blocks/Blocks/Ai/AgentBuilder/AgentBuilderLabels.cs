using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word an <see cref="AgentBuilder"/> shows or reads out, with English defaults. Replace the whole object to reword
/// or translate; texts with values are functions so the word order can change.
/// </summary>
public sealed class AgentBuilderLabels
{
    /// <summary>The English defaults.</summary>
    public static AgentBuilderLabels Default { get; } = new();

    /// <summary>The accessible name of the settings column ("Agent settings").</summary>
    public string SettingsName { get; init; } = "Agent settings";

    /// <summary>The name shown in the header while the name field is empty ("Untitled agent").</summary>
    public string UntitledAgent { get; init; } = "Untitled agent";

    /// <summary>The status badge of an agent never published ("Draft").</summary>
    public string Draft { get; init; } = "Draft";

    /// <summary>The status badge of a published agent with nothing new ("Published").</summary>
    public string Published { get; init; } = "Published";

    /// <summary>The status badge of a published agent with newer settings ("Unpublished changes").</summary>
    public string Changed { get; init; } = "Unpublished changes";

    /// <summary>The Publish button.</summary>
    public string Publish { get; init; } = "Publish";

    /// <summary>The Publish button of an agent already published with nothing new ("Up to date").</summary>
    public string UpToDate { get; init; } = "Up to date";

    /// <summary>The title of the identity section.</summary>
    public string IdentityTitle { get; init; } = "Identity";

    /// <summary>The label of the name field.</summary>
    public string NameLabel { get; init; } = "Name";

    /// <summary>The placeholder of the name field.</summary>
    public string NamePlaceholder { get; init; } = "Give the agent a name";

    /// <summary>The error under an empty name.</summary>
    public string NameRequired { get; init; } = "A name is required to save.";

    /// <summary>The label of the description field.</summary>
    public string DescriptionLabel { get; init; } = "Description";

    /// <summary>The placeholder of the description field.</summary>
    public string DescriptionPlaceholder { get; init; } = "One line on what it helps with";

    /// <summary>The title of the instructions section.</summary>
    public string InstructionsTitle { get; init; } = "Instructions";

    /// <summary>The line under the instructions title.</summary>
    public string InstructionsHint { get; init; } = "What the agent does, how it sounds and what it must not do.";

    /// <summary>The placeholder of the instructions field.</summary>
    public string InstructionsPlaceholder { get; init; } = "You are a helpful agent that…";

    /// <summary>The counter under the instructions: characters and the rough token count.</summary>
    public Func<int, int, CultureInfo, string> InstructionsCount { get; init; } =
        static (characters, tokens, culture) => $"{characters.ToString("N0", culture)} characters · about {tokens.ToString("N0", culture)} tokens";

    /// <summary>The error when the instructions are longer than the limit; the argument is the limit.</summary>
    public Func<int, CultureInfo, string> InstructionsTooLong { get; init; } =
        static (limit, culture) => $"Keep the instructions under {limit.ToString("N0", culture)} characters.";

    /// <summary>The title of the model section.</summary>
    public string ModelTitle { get; init; } = "Model";

    /// <summary>The line under the model title.</summary>
    public string ModelHint { get; init; } = "The model that writes the agent's replies.";

    /// <summary>The title of the tools section.</summary>
    public string ToolsTitle { get; init; } = "Tools";

    /// <summary>The count next to the tools title; the arguments are the tools that are on and all tools ("2 of 5 on").</summary>
    public Func<int, int, string> ToolsCount { get; init; } = static (on, all) => $"{on} of {all} on";

    /// <summary>The line under the tools title.</summary>
    public string ToolsHint { get; init; } = "What the agent may do besides writing.";

    /// <summary>The title of the knowledge section.</summary>
    public string KnowledgeTitle { get; init; } = "Knowledge";

    /// <summary>The count next to the knowledge title ("3 sources").</summary>
    public Func<int, string> KnowledgeCount { get; init; } = static count => count == 1 ? "1 source" : $"{count} sources";

    /// <summary>The line under the knowledge title.</summary>
    public string KnowledgeHint { get; init; } = "Files and pages the agent searches before it answers.";

    /// <summary>The line in place of the list when there are no sources.</summary>
    public string NoSources { get; init; } = "No sources yet. Add a link or upload a file.";

    /// <summary>The placeholder of the link field.</summary>
    public string LinkPlaceholder { get; init; } = "https://";

    /// <summary>The accessible name of the link field.</summary>
    public string LinkName { get; init; } = "Link to a page";

    /// <summary>The button that adds the link.</summary>
    public string AddLink { get; init; } = "Add link";

    /// <summary>The button that uploads files.</summary>
    public string Upload { get; init; } = "Upload files";

    /// <summary>The error under a link that is not a full http or https address.</summary>
    public string InvalidLink { get; init; } = "Enter a full link that starts with https://";

    /// <summary>The error under a link that is already a source.</summary>
    public string DuplicateLink { get; init; } = "That page is already a source.";

    /// <summary>The kind line of a file source.</summary>
    public string FileKind { get; init; } = "File";

    /// <summary>The kind line of a website source.</summary>
    public string WebsiteKind { get; init; } = "Website";

    /// <summary>The status of a source being indexed; the argument is the percentage ("Indexing 45%").</summary>
    public Func<int, string> Indexing { get; init; } = static percent => $"Indexing {percent}%";

    /// <summary>The status of a ready source; the argument is the passage count or null ("Ready · 128 passages").</summary>
    public Func<int?, CultureInfo, string> Ready { get; init; } =
        static (chunks, culture) => chunks is { } n ? $"Ready · {n.ToString("N0", culture)} passages" : "Ready";

    /// <summary>The status of a failed source with no message.</summary>
    public string Failed { get; init; } = "Could not index";

    /// <summary>The Retry button of a failed source.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The accessible name of a Remove button; the argument is the source name.</summary>
    public Func<string, string> RemoveSource { get; init; } = static name => $"Remove {name}";

    /// <summary>The accessible name of a Retry button; the argument is the source name.</summary>
    public Func<string, string> RetrySource { get; init; } = static name => $"Retry {name}";

    /// <summary>The save bar while nothing is unsaved.</summary>
    public string AllSaved { get; init; } = "All changes saved";

    /// <summary>The save bar while the draft differs from what is saved.</summary>
    public string Unsaved { get; init; } = "Unsaved changes";

    /// <summary>The save bar while saving.</summary>
    public string Saving { get; init; } = "Saving…";

    /// <summary>The save bar after a failed save; the argument is the error message.</summary>
    public Func<string, string> SaveFailed { get; init; } = static message => $"Not saved: {message}";

    /// <summary>The Save button.</summary>
    public string Save { get; init; } = "Save";

    /// <summary>The Discard button.</summary>
    public string Discard { get; init; } = "Discard";

    /// <summary>The title above the test chat.</summary>
    public string TestTitle { get; init; } = "Test";

    /// <summary>The line next to the test title.</summary>
    public string TestHint { get; init; } = "Chats here use your draft and are not saved.";

    /// <summary>The greeting of the empty test chat; the argument is the agent's name.</summary>
    public Func<string, string> TestGreeting { get; init; } = static name => $"Try {name}";

    /// <summary>The placeholder of the test chat's message box; the argument is the agent's name.</summary>
    public Func<string, string> TestPlaceholder { get; init; } = static name => $"Message {name}";

    /// <summary>The note over the test chat when the settings changed after it started.</summary>
    public string StaleNotice { get; init; } = "Settings changed since this chat started.";

    /// <summary>The button that restarts the test chat.</summary>
    public string Restart { get; init; } = "Restart";

    /// <summary>The accessible name of the restart button.</summary>
    public string RestartName { get; init; } = "Restart the test chat with the current settings";

    /// <summary>Said after a save.</summary>
    public string SavedAnnouncement { get; init; } = "Changes saved";

    /// <summary>Said after Discard.</summary>
    public string DiscardedAnnouncement { get; init; } = "Changes discarded";

    /// <summary>Said after publishing; the argument is the agent's name.</summary>
    public Func<string, string> PublishedAnnouncement { get; init; } = static name => $"{name} published";

    /// <summary>Said when a tool is switched; the arguments are its name and whether it is on.</summary>
    public Func<string, bool, string> ToolAnnouncement { get; init; } = static (name, on) => on ? $"{name} on" : $"{name} off";

    /// <summary>Said when a source is added; the argument is its name.</summary>
    public Func<string, string> SourceAddedAnnouncement { get; init; } = static name => $"Added {name}, indexing";

    /// <summary>Said when a source is removed; the argument is its name.</summary>
    public Func<string, string> SourceRemovedAnnouncement { get; init; } = static name => $"Removed {name}";

    /// <summary>Said when a source is ready; the argument is its name.</summary>
    public Func<string, string> SourceReadyAnnouncement { get; init; } = static name => $"{name} is ready";

    /// <summary>Said when a source fails; the arguments are its name and the reason.</summary>
    public Func<string, string, string> SourceFailedAnnouncement { get; init; } = static (name, reason) => $"{name} could not be indexed: {reason}";

    /// <summary>Said when the test chat restarts.</summary>
    public string RestartedAnnouncement { get; init; } = "Test chat restarted with the current settings";

    /// <summary>Said when the settings change while a test chat is open.</summary>
    public string StaleAnnouncement { get; init; } = "Settings changed. Restart the test chat to try them.";

    /// <summary>The words of the test chat itself (its menu, buttons and reply actions).</summary>
    public AiChatLabels Chat { get; init; } = new();

    /// <summary>The words of the model pill and menu.</summary>
    public ModelPickerLabels Model { get; init; } = new();
}
