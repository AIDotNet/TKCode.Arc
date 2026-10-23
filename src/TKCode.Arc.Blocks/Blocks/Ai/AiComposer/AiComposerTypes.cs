using System;
using System.Collections.Generic;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace TKCode.Arc;

/// <summary>What a token in the prompt stands for.</summary>
public enum AiComposerTokenKind
{
    /// <summary>A command typed with "/" at the start of a line.</summary>
    Command,

    /// <summary>An item of context typed with "@", such as a file or a document.</summary>
    Mention
}

/// <summary>Whether the send button can send, is waiting for something, or is stopping a reply.</summary>
public enum AiSendState
{
    /// <summary>Nothing to send.</summary>
    Empty,

    /// <summary>Something to send; pressing sends it.</summary>
    Ready,

    /// <summary>There is something to send, but an upload is still running or the text is over the limit.</summary>
    Blocked,

    /// <summary>A reply is being generated; pressing stops it.</summary>
    Generating
}

/// <summary>What an attachment is.</summary>
public enum AiAttachmentKind
{
    /// <summary>A file with an icon for its type.</summary>
    File,

    /// <summary>An image file, shown with a thumbnail.</summary>
    Image,

    /// <summary>A long piece of pasted text, kept out of the prompt.</summary>
    PastedText,

    /// <summary>Something picked from another place, such as a drive or a repository.</summary>
    Source
}

/// <summary>Where an attachment is in its life.</summary>
public enum AiAttachmentStatus
{
    /// <summary>Uploading; <see cref="AiAttachment.Progress"/> says how far.</summary>
    Uploading,

    /// <summary>Just finished uploading: a check mark shows for a moment.</summary>
    Done,

    /// <summary>Uploaded and ready to send; the chip offers a remove button.</summary>
    Ready,

    /// <summary>Failed or refused; <see cref="AiAttachment.Error"/> says why. It can be removed and does not block sending.</summary>
    Error
}

/// <summary>Which built-in behavior a <see cref="AiComposerAction"/> in the plus menu has.</summary>
public enum AiComposerActionKind
{
    /// <summary>Opens the system file picker.</summary>
    Upload,

    /// <summary>Types an "@" into the prompt to start a mention.</summary>
    Mention,

    /// <summary>Runs <see cref="AiComposerAction.Command"/>.</summary>
    Custom
}

/// <summary>A command or a mention inside the prompt text, as a range of it.</summary>
/// <param name="Kind">Command or mention.</param>
/// <param name="Id">The id of the <see cref="AiCommand"/> or <see cref="AiContextItem"/>.</param>
/// <param name="Label">Its name, without the "/" or "@".</param>
/// <param name="Start">Index of the first character of the token in the text (the "/" or "@").</param>
/// <param name="End">Index after its last character.</param>
public sealed record AiPromptToken(AiComposerTokenKind Kind, string Id, string Label, int Start, int End)
{
    /// <summary>The token as it appears in the text: "/" or "@" followed by the label.</summary>
    public string Text => (Kind == AiComposerTokenKind.Command ? "/" : "@") + Label;
}

/// <summary>The prompt: its text and the tokens in it. Immutable.</summary>
public sealed class AiComposerValue
{
    /// <summary>An empty prompt.</summary>
    public static AiComposerValue Empty { get; } = new(string.Empty, Array.Empty<AiPromptToken>());

    /// <summary>Creates a value.</summary>
    /// <param name="text">The text, including the text of the tokens.</param>
    /// <param name="tokens">The tokens, in text order.</param>
    public AiComposerValue(string text, IReadOnlyList<AiPromptToken> tokens)
    {
        Text = text;
        Tokens = tokens;
    }

    /// <summary>The prompt text, including the text of the tokens.</summary>
    public string Text { get; }

    /// <summary>The tokens in the text, in order.</summary>
    public IReadOnlyList<AiPromptToken> Tokens { get; }
}

/// <summary>A command the person can start a prompt with, such as "/recap".</summary>
public class AiCommand : AvaloniaObject
{
    /// <summary>Stable id reported in tokens.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<AiCommand, string>(nameof(Id), string.Empty);

    /// <summary>The name without the slash; the person filters by its start, ignoring case.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<AiCommand, string>(nameof(Name), string.Empty);

    /// <summary>One line that says what it does.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<AiCommand, string?>(nameof(Description));

    /// <summary>A 24 × 24 icon geometry (a Lucide path works).</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<AiCommand, Geometry?>(nameof(Icon));

    /// <summary>The id of the command.</summary>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <summary>The name without the slash.</summary>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <summary>What the command does.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>The icon of the command.</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
}

/// <summary>An item of context the person can mention with "@", such as a file or a document.</summary>
public class AiContextItem : AvaloniaObject
{
    /// <summary>Stable id reported in tokens.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<AiContextItem, string>(nameof(Id), string.Empty);

    /// <summary>The name the token shows; the person filters by it (and by <see cref="Hint"/>), ignoring case.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<AiContextItem, string>(nameof(Label), string.Empty);

    /// <summary>A second line: a path, a source or a number.</summary>
    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<AiContextItem, string?>(nameof(Hint));

    /// <summary>The heading of the group the item is listed under; groups follow <c>AiComposer.MentionGroups</c>.</summary>
    public static readonly StyledProperty<string?> GroupProperty =
        AvaloniaProperty.Register<AiContextItem, string?>(nameof(Group));

    /// <summary>A 24 × 24 icon geometry.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<AiContextItem, Geometry?>(nameof(Icon));

    /// <summary>The id of the item.</summary>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <summary>The name of the item.</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The second line.</summary>
    public string? Hint { get => GetValue(HintProperty); set => SetValue(HintProperty, value); }

    /// <summary>The group the item belongs to.</summary>
    public string? Group { get => GetValue(GroupProperty); set => SetValue(GroupProperty, value); }

    /// <summary>The icon of the item.</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
}

/// <summary>How <see cref="AiComposer"/> lays out its parts.</summary>
public enum AiComposerLayout
{
    /// <summary>The AI composer: the attachment cards, the text area and a toolbar (plus menu, tools, counter, model, send).</summary>
    Full,

    /// <summary>
    /// The AI chat's input: one row of a paperclip that opens the file picker, the text area (up to 7 lines) and the send
    /// button (a solid stop button while a reply is generated, without a progress ring), with compact attachment chips
    /// above it. No toolbar, tools, model pill, counter, "/" or "@" lists, or plus menu.
    /// </summary>
    Inline
}

/// <summary>A file, an image, a pasted text or a source attached to the prompt, with its upload state.</summary>
public class AiAttachment : AvaloniaObject
{
    /// <summary>Stable id, unique within the composer.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<AiAttachment, string>(nameof(Id), string.Empty);

    /// <summary>The name shown on the chip (a file name, "Pasted text", a source's title).</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<AiAttachment, string>(nameof(Name), string.Empty);

    /// <summary>What the attachment is.</summary>
    public static readonly StyledProperty<AiAttachmentKind> KindProperty =
        AvaloniaProperty.Register<AiAttachment, AiAttachmentKind>(nameof(Kind), AiAttachmentKind.File);

    /// <summary>The size in bytes, when known.</summary>
    public static readonly StyledProperty<long?> SizeProperty =
        AvaloniaProperty.Register<AiAttachment, long?>(nameof(Size));

    /// <summary>The file, for attachments picked, pasted or dropped from the system.</summary>
    public static readonly StyledProperty<IStorageFile?> FileProperty =
        AvaloniaProperty.Register<AiAttachment, IStorageFile?>(nameof(File));

    /// <summary>The text of a <see cref="AiAttachmentKind.PastedText"/> attachment.</summary>
    public static readonly StyledProperty<string?> PastedTextProperty =
        AvaloniaProperty.Register<AiAttachment, string?>(nameof(PastedText));

    /// <summary>Where the attachment is in its life.</summary>
    public static readonly StyledProperty<AiAttachmentStatus> StatusProperty =
        AvaloniaProperty.Register<AiAttachment, AiAttachmentStatus>(nameof(Status), AiAttachmentStatus.Ready);

    /// <summary>How far the upload is, from 0 to 1.</summary>
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<AiAttachment, double>(nameof(Progress));

    /// <summary>A short reason for <see cref="AiAttachmentStatus.Error"/>, such as "Over 30 MB".</summary>
    public static readonly StyledProperty<string?> ErrorProperty =
        AvaloniaProperty.Register<AiAttachment, string?>(nameof(Error));

    /// <summary>A thumbnail for images.</summary>
    public static readonly StyledProperty<IImage?> ThumbnailProperty =
        AvaloniaProperty.Register<AiAttachment, IImage?>(nameof(Thumbnail));

    /// <summary>The second line of the chip ("PDF, 469 KB"); null makes one from <see cref="Kind"/>, the name and <see cref="Size"/>.</summary>
    public static readonly StyledProperty<string?> MetaProperty =
        AvaloniaProperty.Register<AiAttachment, string?>(nameof(Meta));

    /// <summary>A 24 × 24 icon geometry for attachments without a thumbnail; null picks one from the kind and the file extension.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<AiAttachment, Geometry?>(nameof(Icon));

    /// <summary>The id of the attachment.</summary>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <summary>The name shown on the chip.</summary>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <summary>What the attachment is.</summary>
    public AiAttachmentKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <summary>The size in bytes.</summary>
    public long? Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>The file behind the attachment.</summary>
    public IStorageFile? File { get => GetValue(FileProperty); set => SetValue(FileProperty, value); }

    /// <summary>The pasted text.</summary>
    public string? PastedText { get => GetValue(PastedTextProperty); set => SetValue(PastedTextProperty, value); }

    /// <summary>Where the attachment is in its life.</summary>
    public AiAttachmentStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <summary>The upload progress, 0 to 1.</summary>
    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    /// <summary>The reason for an error.</summary>
    public string? Error { get => GetValue(ErrorProperty); set => SetValue(ErrorProperty, value); }

    /// <summary>The thumbnail of an image.</summary>
    public IImage? Thumbnail { get => GetValue(ThumbnailProperty); set => SetValue(ThumbnailProperty, value); }

    /// <summary>The second line of the chip.</summary>
    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }

    /// <summary>The icon of the attachment.</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
}

/// <summary>A toggle in the composer's toolbar, such as "Web search": it changes how the next reply is made.</summary>
public class AiComposerTool : AvaloniaObject
{
    /// <summary>Stable id reported in the sent message.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<AiComposerTool, string>(nameof(Id), string.Empty);

    /// <summary>The label next to the icon (hidden in a narrow composer) and the accessible name.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<AiComposerTool, string>(nameof(Label), string.Empty);

    /// <summary>The tooltip; null shows the label.</summary>
    public static readonly StyledProperty<string?> TipProperty =
        AvaloniaProperty.Register<AiComposerTool, string?>(nameof(Tip));

    /// <summary>A 24 × 24 icon geometry.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<AiComposerTool, Geometry?>(nameof(Icon));

    /// <summary>Whether the tool is on (two-way).</summary>
    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<AiComposerTool, bool>(nameof(IsChecked), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>The id of the tool.</summary>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <summary>The label of the tool.</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The tooltip of the tool.</summary>
    public string? Tip { get => GetValue(TipProperty); set => SetValue(TipProperty, value); }

    /// <summary>The icon of the tool.</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>Whether the tool is on.</summary>
    public bool IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }
}

/// <summary>An entry of the plus menu next to the prompt, besides the built-in "Upload file".</summary>
public class AiComposerAction : AvaloniaObject
{
    /// <summary>The text of the menu entry.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<AiComposerAction, string>(nameof(Label), string.Empty);

    /// <summary>A 24 × 24 icon geometry.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<AiComposerAction, Geometry?>(nameof(Icon));

    /// <summary>Run for <see cref="AiComposerActionKind.Custom"/> entries, with the composer as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<AiComposerAction, ICommand?>(nameof(Command));

    /// <summary>What the entry does.</summary>
    public static readonly StyledProperty<AiComposerActionKind> KindProperty =
        AvaloniaProperty.Register<AiComposerAction, AiComposerActionKind>(nameof(Kind), AiComposerActionKind.Custom);

    /// <summary>Whether a separator is drawn above the entry.</summary>
    public static readonly StyledProperty<bool> IsSeparatorBeforeProperty =
        AvaloniaProperty.Register<AiComposerAction, bool>(nameof(IsSeparatorBefore));

    /// <summary>The text of the entry.</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The icon of the entry.</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>The command of a custom entry.</summary>
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    /// <summary>What the entry does.</summary>
    public AiComposerActionKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <summary>Whether a separator is drawn above the entry.</summary>
    public bool IsSeparatorBefore { get => GetValue(IsSeparatorBeforeProperty); set => SetValue(IsSeparatorBeforeProperty, value); }
}

/// <summary>What <c>AiComposer</c> reports when the person sends the prompt.</summary>
public sealed class AiComposerMessage
{
    /// <summary>Creates a message.</summary>
    public AiComposerMessage(AiComposerValue value, IReadOnlyList<AiAttachment> attachments, AiModel? model, ModelEffort? effort,
        IReadOnlyDictionary<string, bool> tools)
    {
        Value = value;
        Attachments = attachments;
        Model = model;
        Effort = effort;
        Tools = tools;
    }

    /// <summary>The prompt and its tokens.</summary>
    public AiComposerValue Value { get; }

    /// <summary>The prompt text.</summary>
    public string Text => Value.Text;

    /// <summary>The tokens in the prompt.</summary>
    public IReadOnlyList<AiPromptToken> Tokens => Value.Tokens;

    /// <summary>The attachments that are ready (uploaded, or without an upload step); failed ones are left out.</summary>
    public IReadOnlyList<AiAttachment> Attachments { get; }

    /// <summary>The selected model, when the composer shows a model pill.</summary>
    public AiModel? Model { get; }

    /// <summary>The selected effort.</summary>
    public ModelEffort? Effort { get; }

    /// <summary>For each tool of the toolbar, whether it was on.</summary>
    public IReadOnlyDictionary<string, bool> Tools { get; }
}
