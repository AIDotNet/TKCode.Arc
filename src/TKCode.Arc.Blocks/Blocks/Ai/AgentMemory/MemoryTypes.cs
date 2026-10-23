using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Where a memory came from: a chat (shown as "From a chat") or the person (shown as "Added by you").</summary>
public enum MemorySourceKind
{
    /// <summary>The assistant noted it during a conversation.</summary>
    Chat,

    /// <summary>The person added it.</summary>
    Manual
}

/// <summary>What happened to a memory, reported in <see cref="MemoryChange"/>.</summary>
public enum MemoryChangeKind
{
    /// <summary>The assistant noted a memory during a conversation (the captured pill, then filed into its group).</summary>
    Capture,

    /// <summary>The person added a memory from the add field.</summary>
    Add,

    /// <summary>The person changed the text of a memory.</summary>
    Edit,

    /// <summary>The person forgot a memory. Reported only once the undo window has ended.</summary>
    Forget
}

/// <summary>The two switches above the memory list.</summary>
public enum MemorySetting
{
    /// <summary>"Reference saved memories": off pauses the list and the captured pill.</summary>
    SavedMemories,

    /// <summary>"Reference chat history": off changes only the explanation.</summary>
    ChatHistory
}

/// <summary>
/// One change to the memories, reported by <see cref="AgentMemory"/> after it is committed. <see cref="Memory"/> is the
/// memory as it is now (for <see cref="MemoryChangeKind.Forget"/>, as it was); <see cref="PreviousText"/> is the text
/// before an edit and null otherwise.
/// </summary>
/// <param name="Kind">What happened.</param>
/// <param name="Memory">The memory that changed.</param>
/// <param name="PreviousText">The text before <see cref="MemoryChangeKind.Edit"/>; null for other kinds.</param>
public sealed record MemoryChange(MemoryChangeKind Kind, MemoryItem Memory, string? PreviousText = null);

/// <summary>
/// One thing the assistant remembers about the person. Data only: an <see cref="AvaloniaObject"/> so that the
/// properties bind and a change (an edit from elsewhere) reaches the row on screen.
/// </summary>
public class MemoryItem : AvaloniaObject
{
    /// <summary>Stable id, unique within <see cref="AgentMemory.Memories"/>.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<MemoryItem, string>(nameof(Id), string.Empty);

    /// <summary>The memory as one sentence without a final period, such as "Prefers tables to long bullet lists".</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MemoryItem, string>(nameof(Text), string.Empty);

    /// <summary>The key of the <see cref="MemoryGroup"/> the memory is filed under.</summary>
    public static readonly StyledProperty<string> GroupKeyProperty =
        AvaloniaProperty.Register<MemoryItem, string>(nameof(GroupKey), string.Empty);

    /// <summary>Whether the memory came from a chat or was added by the person.</summary>
    public static readonly StyledProperty<MemorySourceKind> SourceKindProperty =
        AvaloniaProperty.Register<MemoryItem, MemorySourceKind>(nameof(SourceKind), MemorySourceKind.Manual);

    /// <summary>The date of the source, shown as a short date such as "Sep 8".</summary>
    public static readonly StyledProperty<DateTimeOffset> SourceDateProperty =
        AvaloniaProperty.Register<MemoryItem, DateTimeOffset>(nameof(SourceDate), DateTimeOffset.MinValue);

    /// <summary>When set, the source text is a link: <see cref="AgentMemory.SourceRequested"/> reports it.</summary>
    public static readonly StyledProperty<Uri?> SourceUriProperty =
        AvaloniaProperty.Register<MemoryItem, Uri?>(nameof(SourceUri));

    /// <summary>When set (the memory is about a person), the row shows a small avatar and this name is its accessible name.</summary>
    public static readonly StyledProperty<string?> PersonNameProperty =
        AvaloniaProperty.Register<MemoryItem, string?>(nameof(PersonName));

    /// <summary>The avatar image for <see cref="PersonName"/>; without one the avatar shows the first letter.</summary>
    public static readonly StyledProperty<IImage?> PersonImageProperty =
        AvaloniaProperty.Register<MemoryItem, IImage?>(nameof(PersonImage));

    /// <summary>The id of the memory.</summary>
    public string Id
    {
        get => GetValue(IdProperty);
        set => SetValue(IdProperty, value);
    }

    /// <summary>The memory's sentence.</summary>
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>The key of the group the memory is filed under.</summary>
    public string GroupKey
    {
        get => GetValue(GroupKeyProperty);
        set => SetValue(GroupKeyProperty, value);
    }

    /// <summary>Where the memory came from.</summary>
    public MemorySourceKind SourceKind
    {
        get => GetValue(SourceKindProperty);
        set => SetValue(SourceKindProperty, value);
    }

    /// <summary>The date of the source.</summary>
    public DateTimeOffset SourceDate
    {
        get => GetValue(SourceDateProperty);
        set => SetValue(SourceDateProperty, value);
    }

    /// <summary>The link to the conversation the memory came from, if any.</summary>
    public Uri? SourceUri
    {
        get => GetValue(SourceUriProperty);
        set => SetValue(SourceUriProperty, value);
    }

    /// <summary>The name of the person the memory is about, if any.</summary>
    public string? PersonName
    {
        get => GetValue(PersonNameProperty);
        set => SetValue(PersonNameProperty, value);
    }

    /// <summary>The avatar image of <see cref="PersonName"/>.</summary>
    public IImage? PersonImage
    {
        get => GetValue(PersonImageProperty);
        set => SetValue(PersonImageProperty, value);
    }
}

/// <summary>A filing group of memories (About you, Preferences, Projects, People by default).</summary>
public class MemoryGroup : AvaloniaObject
{
    /// <summary>The key a memory's <see cref="MemoryItem.GroupKey"/> refers to.</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<MemoryGroup, string>(nameof(Key), string.Empty);

    /// <summary>The label of the filter pill and the group heading.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<MemoryGroup, string>(nameof(Label), string.Empty);

    /// <summary>The sentence shown in the group when it has no memories while it is the only group on screen.</summary>
    public static readonly StyledProperty<string?> EmptyTextProperty =
        AvaloniaProperty.Register<MemoryGroup, string?>(nameof(EmptyText));

    /// <summary>The key of the group, as <see cref="MemoryItem.GroupKey"/> refers to it.</summary>
    public string Key
    {
        get => GetValue(KeyProperty);
        set => SetValue(KeyProperty, value);
    }

    /// <summary>The group's label.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>The sentence for an empty group.</summary>
    public string? EmptyText
    {
        get => GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }
}

/// <summary>
/// A memory the assistant has just noted: <see cref="Note"/> (such as "Noted: you run builds with the dotnet CLI") shows in
/// the captured pill until the person files it, after which the pill shows "Saved to" the group.
/// </summary>
public class CapturedMemory : AvaloniaObject
{
    /// <summary>The short sentence the pill shows, such as "Noted: you run builds with the dotnet CLI".</summary>
    public static readonly StyledProperty<string> NoteProperty =
        AvaloniaProperty.Register<CapturedMemory, string>(nameof(Note), string.Empty);

    /// <summary>The memory to file; it is added to <see cref="AgentMemory.Memories"/> when the pill is pressed.</summary>
    public static readonly StyledProperty<MemoryItem?> MemoryProperty =
        AvaloniaProperty.Register<CapturedMemory, MemoryItem?>(nameof(Memory));

    /// <summary>The short sentence the pill shows.</summary>
    public string Note
    {
        get => GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    /// <summary>The memory to file.</summary>
    public MemoryItem? Memory
    {
        get => GetValue(MemoryProperty);
        set => SetValue(MemoryProperty, value);
    }
}
