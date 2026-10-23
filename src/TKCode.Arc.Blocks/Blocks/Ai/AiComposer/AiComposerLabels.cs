using System;
using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="AiComposer"/> shows or announces, in English by default. Replace the instance to translate or
/// reword; formatted texts are functions so that word order can change.
/// </summary>
public sealed class AiComposerLabels
{
    /// <summary>The hint in the empty box.</summary>
    public string Placeholder { get; init; } = "Ask a question — type / for actions, @ to pull in a file";

    /// <summary>The accessible name of the text area.</summary>
    public string Message { get; init; } = "Message";

    /// <summary>The accessible name and tooltip of the send button when there is something to send.</summary>
    public string Send { get; init; } = "Send message";

    /// <summary>The accessible name and tooltip of the send button while a reply is generated.</summary>
    public string Stop { get; init; } = "Stop generating";

    /// <summary>The send button's name and tooltip when the box is empty.</summary>
    public string SendEmpty { get; init; } = "Type a message to send";

    /// <summary>The send button's name while an upload is running.</summary>
    public string SendUploading { get; init; } = "Wait for uploads to finish";

    /// <summary>The send button's name when the prompt is over the limit; the argument is how many characters are over.</summary>
    public Func<int, string> SendOverLimit { get; init; } = over => $"Shorten the message by {over} characters";

    /// <summary>The tooltip and accessible name of the plus button.</summary>
    public string Attach { get; init; } = "Add to message";

    /// <summary>The built-in entry of the plus menu that opens the file picker.</summary>
    public string UploadFile { get; init; } = "Upload file";

    /// <summary>The built-in entry of the plus menu that starts a mention.</summary>
    public string MentionEntry { get; init; } = "Mention a file or doc";

    /// <summary>The title of the file picker.</summary>
    public string PickerTitle { get; init; } = "Attach files";

    /// <summary>The accessible name of the group of attachments.</summary>
    public string Attachments { get; init; } = "Attachments";

    /// <summary>"Uploading 40%" on a chip; the argument is the percentage.</summary>
    public Func<int, string> Uploading { get; init; } = percent => $"Uploading {percent}%";

    /// <summary>The name of the button that cancels an upload.</summary>
    public Func<string, string> CancelUpload { get; init; } = name => $"Cancel upload of {name}";

    /// <summary>The name of the button that removes an attachment.</summary>
    public Func<string, string> RemoveAttachment { get; init; } = name => $"Remove {name}";

    /// <summary>The reason on a chip whose file is too large; the argument is the limit in bytes.</summary>
    public Func<long, string> TooLarge { get; init; } = limit => $"Over {AiFileSizeText(limit)}";

    /// <summary>The reason on a chip whose file type is not accepted.</summary>
    public string TypeNotAccepted { get; init; } = "Type not accepted";

    /// <summary>The reason on a chip when the upload failed without a message.</summary>
    public string UploadFailed { get; init; } = "Upload failed";

    /// <summary>The name of an attachment made from a long pasted text.</summary>
    public string PastedText { get; init; } = "Pasted text";

    /// <summary>The second line of a pasted text chip; the argument is the number of lines.</summary>
    public Func<int, string> PastedLines { get; init; } = lines => lines == 1 ? "1 line" : $"{lines} lines";

    /// <summary>The title of the cover shown while files are dragged over the box.</summary>
    public string DropTitle { get; init; } = "Drop to attach";

    /// <summary>The explanation under it; the argument is the size limit in bytes (0 = none).</summary>
    public Func<long, string> DropHint { get; init; } = limit => limit > 0 ? $"Documents, images or code, {AiFileSizeText(limit)} max per file" : "Documents, images or code";

    /// <summary>The counter next to the box when it nears the limit; the argument is how many characters are left.</summary>
    public Func<int, string> CharactersLeft { get; init; } = left => $"{Number(left)} left";

    /// <summary>The counter when the limit is passed; the argument is how many characters are over.</summary>
    public Func<int, string> CharactersOver { get; init; } = over => $"{Number(over)} over";

    /// <summary>The tooltip and accessible name of the counter; arguments are the length and the limit.</summary>
    public Func<int, int, string> CounterTip { get; init; } = (length, max) => length > max
        ? $"{Number(length - max)} characters over the limit"
        : $"{Number(max - length)} of {Number(max)} characters left";

    /// <summary>The accessible name of the model pill; arguments are the model and the effort (null when it has none).</summary>
    public Func<string, string?, string> ModelName { get; init; } = (model, effort) => effort is null ? $"Model: {model}" : $"Model: {model}, {effort} effort";

    /// <summary>The name of an effort level in the pill, the accessible names and the announcements ("Medium").</summary>
    public Func<ModelEffort, string> EffortName { get; init; } = ModelEffortNames.Name;

    /// <summary>The words of the model menu (its rows, the effort header and cells).</summary>
    public ModelMenuLabels ModelMenu { get; init; } = new();

    /// <summary>The accessible name of the "/" list.</summary>
    public string CommandList { get; init; } = "Commands";

    /// <summary>The accessible name of the "@" list.</summary>
    public string MentionList { get; init; } = "Files and docs";

    /// <summary>The first heading of the "@" list when <c>AiComposer.MentionGroups</c> is empty.</summary>
    public string FilesGroup { get; init; } = "Files";

    /// <summary>The second heading of the "@" list when <c>AiComposer.MentionGroups</c> is empty.</summary>
    public string DocsGroup { get; init; } = "Docs";

    /// <summary>The help text of the text area: how to send and how to start a command or a mention.</summary>
    public string KeyHint { get; init; } = "Press Enter to send and Shift+Enter for a new line. Type / at the start of a line for commands, @ to mention a file or doc.";

    /// <summary>The type word in an image chip's second line ("Image, 67 KB").</summary>
    public string ImageType { get; init; } = "Image";

    // Announcements (polite live region).

    /// <summary>Announced when a tool is turned on or off; the argument is its label and whether it is on.</summary>
    public Func<string, bool, string> AnnounceTool { get; init; } = (label, on) => on ? $"{label} on" : $"{label} off";

    /// <summary>Announced when a model is picked.</summary>
    public Func<string, string> AnnounceModel { get; init; } = model => $"Model: {model}";

    /// <summary>Announced when an effort is picked.</summary>
    public Func<string, string> AnnounceEffort { get; init; } = effort => $"Effort: {effort}";

    /// <summary>Announced when an upload finishes.</summary>
    public Func<string, string> AnnounceUploaded { get; init; } = name => $"{name} uploaded";

    /// <summary>Announced when an attachment fails.</summary>
    public Func<string, string, string> AnnounceUploadFailed { get; init; } = (name, reason) => $"{name}: {reason}";

    /// <summary>Announced when an attachment is removed.</summary>
    public Func<string, string> AnnounceRemoved { get; init; } = name => $"Removed {name}";

    /// <summary>Announced when a long paste became an attachment.</summary>
    public string AnnouncePasted { get; init; } = "Long text attached as a file";

    /// <summary>Announced when the number of matches in the "/" or "@" list changes; the argument is the count.</summary>
    public Func<int, string> AnnounceSuggestions { get; init; } = count => count == 1 ? "1 suggestion" : $"{count} suggestions";

    /// <summary>Announced when a reply is stopped.</summary>
    public string AnnounceStopped { get; init; } = "Stopped";

    /// <summary>Announced when the prompt was sent.</summary>
    public string AnnounceSent { get; init; } = "Message sent";

    private static string AiFileSizeText(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / (1024 * 1024)} MB" : $"{bytes / 1024} KB";

    private static string Number(int value) => value.ToString("N0", CultureInfo.CurrentCulture);
}
