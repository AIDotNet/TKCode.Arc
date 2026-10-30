using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace TKCode.Arc;

/// <summary>Delivery state of the current user's own messages (Arc <c>ChatMessageStatus</c>).</summary>
public enum ChatMessageStatus
{
    /// <summary>On its way ("Sending").</summary>
    Sending,

    /// <summary>Accepted by the server ("Sent", the default for your own messages).</summary>
    Sent,

    /// <summary>Reached the other side ("Delivered").</summary>
    Delivered,

    /// <summary>Read; also shown when a reader's receipt sits on or after the message ("Read").</summary>
    Read,

    /// <summary>Not delivered: the bubble dims and Retry appears on your last message ("Not delivered").</summary>
    Failed
}

/// <summary>What a <see cref="ChatAttachment"/> shows as: a picture in the bubble's gallery or a file row.</summary>
public enum ChatAttachmentKind
{
    /// <summary>A picture: shown in the message's image gallery (up to four tiles, then "+N").</summary>
    Image,

    /// <summary>Any other file: a row with its name and size that opens the file.</summary>
    File
}

/// <summary>How a message's text is rendered.</summary>
public enum ChatMessageFormat
{
    /// <summary>Verbatim text: line breaks kept, no Markdown (Arc's <c>white-space: pre-wrap</c> bubble).</summary>
    PlainText,

    /// <summary>
    /// Markdown through the <see cref="Markdown"/> control (emphasis, links, lists, quotes, tables and highlighted
    /// fenced code); single line breaks stay line breaks, as in a chat.
    /// </summary>
    Markdown
}

/// <summary>
/// A person in the conversation (Arc <c>ChatParticipant</c>). Its properties can be bound in XAML; the thread follows
/// later changes.
/// </summary>
public sealed class ChatParticipant : AvaloniaObject
{
    /// <summary>Stable id, matched by <see cref="ChatMessage.AuthorId"/>.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<ChatParticipant, string>(nameof(Id), string.Empty);

    /// <summary>Display name ("Sam Rivera"); initials show when there is no <see cref="Avatar"/>.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<ChatParticipant, string>(nameof(Name), string.Empty);

    /// <summary>Portrait URL (http(s), file or avares).</summary>
    public static readonly StyledProperty<string?> AvatarProperty =
        AvaloniaProperty.Register<ChatParticipant, string?>(nameof(Avatar));

    /// <summary>Creates an empty participant (for XAML).</summary>
    public ChatParticipant()
    {
    }

    /// <summary>Creates a participant.</summary>
    public ChatParticipant(string id, string name, string? avatar = null)
    {
        Id = id;
        Name = name;
        Avatar = avatar;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="AvatarProperty"/>
    public string? Avatar { get => GetValue(AvatarProperty); set => SetValue(AvatarProperty, value); }
}

/// <summary>A file or picture attached to a message (Arc <c>ChatAttachment</c>).</summary>
public sealed class ChatAttachment
{
    /// <summary>Creates an empty attachment (for XAML).</summary>
    public ChatAttachment()
    {
    }

    /// <summary>Creates an attachment.</summary>
    public ChatAttachment(string id, string name, ChatAttachmentKind kind, string? url = null)
    {
        Id = id;
        Name = name;
        Kind = kind;
        Url = url;
    }

    /// <summary>Stable id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>File name, shown on file rows and used as the picture's fallback description.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Picture or file.</summary>
    public ChatAttachmentKind Kind { get; set; }

    /// <summary>Where the file lives (http(s), file or avares URL); pictures load from it and clicks open it.</summary>
    public string? Url { get; set; }

    /// <summary>A local file (attachments sent from the composer); pictures load from it when there is no <see cref="Url"/>.</summary>
    public IStorageFile? File { get; set; }

    /// <summary>Size in bytes, shown on file rows ("18 KB").</summary>
    public long? Size { get; set; }

    /// <summary>Intrinsic picture width, used with <see cref="Height"/> to reserve a lone picture's space before it loads.</summary>
    public double? Width { get; set; }

    /// <summary>Intrinsic picture height.</summary>
    public double? Height { get; set; }

    /// <summary>Description of a picture for assistive technology; defaults to <see cref="Name"/>.</summary>
    public string? Alt { get; set; }

    /// <summary>The picture source: <see cref="File"/> when set, else <see cref="Url"/>.</summary>
    internal ImageSource? ImageSource =>
        File is not null ? new StorageFileImageSource(File) : ActivityAvatar.Parse(Url);

    /// <summary>True when this attachment shows in the picture gallery (a picture with a source).</summary>
    internal bool ShowsAsImage => Kind == ChatAttachmentKind.Image && (File is not null || !string.IsNullOrWhiteSpace(Url));
}

/// <summary>One reaction under a message (Arc <c>ChatReaction</c>).</summary>
/// <param name="Emoji">The emoji.</param>
/// <param name="Count">How many people reacted.</param>
/// <param name="Mine">True when the current user reacted with it.</param>
public sealed record ChatReaction(string Emoji, int Count, bool Mine = false);

/// <summary>
/// A message (Arc <c>ChatMessage</c>). Update <see cref="Text"/>, <see cref="Status"/>, <see cref="Reactions"/> or
/// <see cref="Attachments"/> in place and the thread follows (the message raises <see cref="PropertyChanged"/>).
/// </summary>
public class ChatMessage : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _authorId = string.Empty;
    private string? _text;
    private DateTimeOffset _createdAt;
    private IReadOnlyList<ChatAttachment>? _attachments;
    private IReadOnlyList<ChatReaction>? _reactions;
    private ChatMessageStatus? _status;
    private ChatMessageFormat? _format;

    /// <summary>Creates an empty message (for XAML).</summary>
    public ChatMessage()
    {
    }

    /// <summary>Creates a message.</summary>
    public ChatMessage(string id, string authorId, string? text, DateTimeOffset createdAt)
    {
        _id = id;
        _authorId = authorId;
        _text = text;
        _createdAt = createdAt;
    }

    /// <summary>Raised when a property changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Stable id (receipts, reactions, retries and streaming refer to it).</summary>
    public string Id { get => _id; set => Set(ref _id, value); }

    /// <summary>The <see cref="ChatParticipant.Id"/> of the author.</summary>
    public string AuthorId { get => _authorId; set => Set(ref _authorId, value); }

    /// <summary>The message text (Markdown or plain, see <see cref="Format"/>); null for an attachment-only message.</summary>
    public string? Text { get => _text; set => Set(ref _text, value); }

    /// <summary>When it was written: groups, day separators and the time label come from it.</summary>
    public DateTimeOffset CreatedAt { get => _createdAt; set => Set(ref _createdAt, value); }

    /// <summary>Pictures and files.</summary>
    public IReadOnlyList<ChatAttachment>? Attachments { get => _attachments; set => Set(ref _attachments, value); }

    /// <summary>Reactions, in the order they show.</summary>
    public IReadOnlyList<ChatReaction>? Reactions { get => _reactions; set => Set(ref _reactions, value); }

    /// <summary>Delivery state of the current user's own message; null reads as <see cref="ChatMessageStatus.Sent"/>.</summary>
    public ChatMessageStatus? Status { get => _status; set => Set(ref _status, value); }

    /// <summary>How <see cref="Text"/> renders; null uses <see cref="ChatThread.MessageFormat"/>.</summary>
    public ChatMessageFormat? Format { get => _format; set => Set(ref _format, value); }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>What the composer sends (Arc <c>ChatDraft</c>): the trimmed text and the attached files.</summary>
public sealed class ChatDraft
{
    /// <summary>Creates a draft.</summary>
    public ChatDraft(string text, IReadOnlyList<IStorageFile> files, IReadOnlyList<ChatAttachment> attachments)
    {
        Text = text;
        Files = files;
        Attachments = attachments;
    }

    /// <summary>The trimmed text (may be empty when only files are sent).</summary>
    public string Text { get; }

    /// <summary>The attached files, in the order they were added.</summary>
    public IReadOnlyList<IStorageFile> Files { get; }

    /// <summary>The files as message attachments (name, size, picture or file, the local file), ready to show.</summary>
    public IReadOnlyList<ChatAttachment> Attachments { get; }
}

/// <summary>Arguments of <see cref="ChatComposer.Sent"/> and <see cref="ChatThread.SendRequested"/>.</summary>
public sealed class ChatSendEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ChatSendEventArgs(RoutedEvent routedEvent, ChatDraft draft)
        : base(routedEvent)
    {
        Draft = draft;
    }

    /// <summary>The text and files.</summary>
    public ChatDraft Draft { get; }
}

/// <summary>Arguments of <see cref="ChatThread.ReactionToggled"/> (Arc <c>onReact(messageId, emoji)</c>).</summary>
public sealed class ChatReactionToggledEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ChatReactionToggledEventArgs(RoutedEvent routedEvent, ChatMessage message, string emoji)
        : base(routedEvent)
    {
        Message = message;
        Emoji = emoji;
    }

    /// <summary>The message reacted to.</summary>
    public ChatMessage Message { get; }

    /// <summary>The emoji picked or the chip clicked.</summary>
    public string Emoji { get; }
}

/// <summary>Arguments of <see cref="ChatThread.RetryRequested"/> (Arc <c>onRetry(messageId)</c>).</summary>
public sealed class ChatRetryEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ChatRetryEventArgs(RoutedEvent routedEvent, ChatMessage message)
        : base(routedEvent)
    {
        Message = message;
    }

    /// <summary>The failed message.</summary>
    public ChatMessage Message { get; }
}

/// <summary>
/// Words the composer shows or announces (Arc <c>ChatComposerLabels</c>). Every one has an English default; words with a
/// name are functions so an app can apply its own grammar.
/// </summary>
public class ChatComposerLabels
{
    /// <summary>Accessible name of the send button ("Send").</summary>
    public string Send { get; set; } = "Send";

    /// <summary>Accessible name of the attach button ("Attach files").</summary>
    public string Attach { get; set; } = "Attach files";

    /// <summary>Accessible name of the list of files waiting to send ("Attachments").</summary>
    public string Attachments { get; set; } = "Attachments";

    /// <summary>Accessible name of the button that takes a file back out ("Remove report.pdf").</summary>
    public Func<string, string> RemoveFile { get; set; } = static name => $"Remove {name}";
}

/// <summary>
/// Every word the thread shows or announces (Arc <c>ChatThreadLabels</c>). Each has an English default; words with a count
/// or names are functions, so an app can apply its own plural rules.
/// </summary>
public sealed class ChatThreadLabels : ChatComposerLabels
{
    /// <summary>Status of your message on its way ("Sending").</summary>
    public string Sending { get; set; } = "Sending";

    /// <summary>"Sent".</summary>
    public string Sent { get; set; } = "Sent";

    /// <summary>"Delivered".</summary>
    public string Delivered { get; set; } = "Delivered";

    /// <summary>"Read".</summary>
    public string Read { get; set; } = "Read";

    /// <summary>A failed message ("Not delivered").</summary>
    public string Failed { get; set; } = "Not delivered";

    /// <summary>The retry button ("Retry").</summary>
    public string Retry { get; set; } = "Retry";

    /// <summary>Accessible name of the read receipts under a message ("Read by Sam, Lee").</summary>
    public Func<IReadOnlyList<string>, string> ReadBy { get; set; } = static names => $"Read by {string.Join(", ", names)}";

    /// <summary>Day separator for today. Arc takes it from <c>Intl.RelativeTimeFormat</c>; set it for other languages.</summary>
    public string Today { get; set; } = "Today";

    /// <summary>Day separator for yesterday.</summary>
    public string Yesterday { get; set; } = "Yesterday";

    /// <summary>Announcement of who is typing ("Sam is typing", "Sam and Lee are typing", "Several people are typing").</summary>
    public Func<IReadOnlyList<string>, string> Typing { get; set; } = static names => names.Count switch
    {
        1 => $"{FirstName(names[0])} is typing",
        2 => $"{FirstName(names[0])} and {FirstName(names[1])} are typing",
        _ => "Several people are typing"
    };

    /// <summary>The pill when new messages land out of view ("3 new messages"); the number in it rolls.</summary>
    public Func<int, string> NewMessages { get; set; } = static count => $"{count} new {(count == 1 ? "message" : "messages")}";

    /// <summary>The pill when you scrolled far up with nothing new ("Jump to latest").</summary>
    public string JumpToLatest { get; set; } = "Jump to latest";

    /// <summary>Name shown for a message whose author is not in the participants ("Unknown").</summary>
    public string UnknownAuthor { get; set; } = "Unknown";

    /// <summary>Accessible name of the add-reaction button ("Add reaction").</summary>
    public string AddReaction { get; set; } = "Add reaction";

    /// <summary>Accessible name of the reaction picker and of the reactions under a message ("Reactions").</summary>
    public string Reactions { get; set; } = "Reactions";

    /// <summary>Accessible name of an emoji in the picker ("React with 👍").</summary>
    public Func<string, string> ReactWith { get; set; } = static emoji => $"React with {emoji}";

    /// <summary>Accessible name of a reaction under a message ("👍 3, including you").</summary>
    public Func<string, int, bool, string> ReactionCount { get; set; } =
        static (emoji, count, includesYou) => $"{emoji} {count}{(includesYou ? ", including you" : string.Empty)}";

    private static string FirstName(string name)
    {
        var space = name.IndexOf(' ');
        return space < 0 ? name : name[..space];
    }
}
