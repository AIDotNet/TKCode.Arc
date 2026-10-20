using System.Text;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>What a mention points at: a person (<c>@</c>) or a channel (<c>#</c>).</summary>
public enum MentionKind
{
    /// <summary>An <c>@</c> mention of a person.</summary>
    Person,

    /// <summary>A <c>#</c> mention of a channel.</summary>
    Channel
}

/// <summary>Where <see cref="MentionInput"/> opens its suggestions (Arc <c>placement</c>).</summary>
public enum MentionSuggestionPlacement
{
    /// <summary>Below the line, flipping above when there is less than 300px below and more room above.</summary>
    Auto,

    /// <summary>Always above the line.</summary>
    Top,

    /// <summary>Always below the line.</summary>
    Bottom
}

/// <summary>
/// A mention inside <see cref="MentionValue.Text"/>. <see cref="Start"/> and <see cref="End"/> are character offsets
/// (end exclusive); the text between them is <c>@Label</c> or <c>#Label</c>.
/// </summary>
/// <param name="Kind">Person or channel.</param>
/// <param name="Id">The id of the person or channel, for storage and notifications.</param>
/// <param name="Label">The name shown after the trigger character.</param>
/// <param name="Start">Offset of the trigger character.</param>
/// <param name="End">Offset just after the label.</param>
public sealed record Mention(MentionKind Kind, string Id, string Label, int Start, int End)
{
    /// <summary>The text the mention occupies: <c>@Label</c> or <c>#Label</c>.</summary>
    public string Text => MentionSerializer.MentionText(Kind, Label);
}

/// <summary>
/// The value of a <see cref="MentionInput"/>: plain text plus the mentions inside it, ordered by offset. Immutable;
/// equality compares the text and every mention.
/// </summary>
public sealed class MentionValue : IEquatable<MentionValue>
{
    /// <summary>No text and no mentions.</summary>
    public static readonly MentionValue Empty = new(string.Empty);

    /// <summary>Creates a value. Mentions are sorted by <see cref="Mention.Start"/>.</summary>
    public MentionValue(string? text, IEnumerable<Mention>? mentions = null)
    {
        Text = text ?? string.Empty;
        var list = mentions is null ? new List<Mention>() : new List<Mention>(mentions);
        list.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        Mentions = list.AsReadOnly();
    }

    /// <summary>The plain text, mentions included as <c>@Label</c> / <c>#Label</c>.</summary>
    public string Text { get; }

    /// <summary>The mentions inside <see cref="Text"/>, ordered by offset.</summary>
    public IReadOnlyList<Mention> Mentions { get; }

    /// <inheritdoc />
    public bool Equals(MentionValue? other)
    {
        if (other is null)
        {
            return false;
        }
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        if (!string.Equals(Text, other.Text, StringComparison.Ordinal) || Mentions.Count != other.Mentions.Count)
        {
            return false;
        }
        for (var i = 0; i < Mentions.Count; i++)
        {
            if (!Mentions[i].Equals(other.Mentions[i]))
            {
                return false;
            }
        }
        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is MentionValue other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Text, StringComparer.Ordinal);
        foreach (var mention in Mentions)
        {
            hash.Add(mention);
        }
        return hash.ToHashCode();
    }

    /// <summary>The text with mentions in their default storage form (see <see cref="MentionSerializer.Serialize"/>).</summary>
    public override string ToString() => MentionSerializer.Serialize(this);
}

/// <summary>A person offered after <c>@</c>.</summary>
public sealed class MentionPerson
{
    /// <summary>Creates a person.</summary>
    public MentionPerson(string id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>Stable id stored in the mention.</summary>
    public string Id { get; }

    /// <summary>Display name; the mention reads <c>@Name</c>.</summary>
    public string Name { get; }

    /// <summary>One short line under the name, such as a job title. Also matched by the search.</summary>
    public string? Role { get; init; }

    /// <summary>Portrait URL (http, file or avares). Initials show when it is missing.</summary>
    public string? Avatar { get; init; }

    /// <summary>Portrait image already in memory; wins over <see cref="Avatar"/>.</summary>
    public IImage? AvatarImage { get; init; }
}

/// <summary>A channel offered after <c>#</c>.</summary>
public sealed class MentionChannel
{
    /// <summary>Creates a channel.</summary>
    public MentionChannel(string id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>Stable id stored in the mention.</summary>
    public string Id { get; }

    /// <summary>Channel name without the <c>#</c>; the mention reads <c>#Name</c>.</summary>
    public string Name { get; }

    /// <summary>One short line under the name. Also matched by the search.</summary>
    public string? Description { get; init; }

    /// <summary>Member count shown at the right of the row.</summary>
    public int? Members { get; init; }
}

/// <summary>Turns a <see cref="MentionValue"/> into storage friendly text (Arc <c>serializeMentions</c>).</summary>
public static class MentionSerializer
{
    /// <summary>The trigger character of a kind: <c>@</c> for people, <c>#</c> for channels.</summary>
    public static char Symbol(MentionKind kind) => kind == MentionKind.Person ? '@' : '#';

    /// <summary>The text a mention occupies: <c>@label</c> or <c>#label</c> (Arc <c>mentionText</c>).</summary>
    public static string MentionText(MentionKind kind, string label) => Symbol(kind) + label;

    /// <summary>
    /// Replaces every mention with <paramref name="format"/>'s output; by default <c>&lt;@id&gt;</c> for people and
    /// <c>&lt;#id&gt;</c> for channels. The rest of the text is kept as is.
    /// </summary>
    public static string Serialize(MentionValue value, Func<Mention, string>? format = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        format ??= static mention => $"<{Symbol(mention.Kind)}{mention.Id}>";
        var text = value.Text;
        var builder = new StringBuilder(text.Length + 16);
        var at = 0;
        foreach (var mention in value.Mentions.OrderBy(static m => m.Start))
        {
            var start = Math.Clamp(mention.Start, at, text.Length);
            builder.Append(text, at, start - at);
            builder.Append(format(mention));
            at = Math.Clamp(mention.End, start, text.Length);
        }
        builder.Append(text, at, text.Length - at);
        return builder.ToString();
    }
}

/// <summary>Arguments of <see cref="MentionInput.ValueChangedEvent"/>.</summary>
public sealed class MentionInputValueChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MentionInputValueChangedEventArgs(MentionValue oldValue, MentionValue newValue)
        : base(MentionInput.ValueChangedEvent)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>The value before the change.</summary>
    public MentionValue OldValue { get; }

    /// <summary>The value after the change.</summary>
    public MentionValue NewValue { get; }
}

/// <summary>Arguments of <see cref="MentionInput.MentionAddedEvent"/> (Arc <c>onMentionAdd</c>).</summary>
public sealed class MentionAddedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MentionAddedEventArgs(Mention mention)
        : base(MentionInput.MentionAddedEvent)
    {
        Mention = mention;
    }

    /// <summary>The mention a suggestion became.</summary>
    public Mention Mention { get; }
}

/// <summary>Arguments of <see cref="MentionInput.SubmittedEvent"/> (Arc <c>onSubmit</c>).</summary>
public sealed class MentionSubmittedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MentionSubmittedEventArgs(MentionValue value)
        : base(MentionInput.SubmittedEvent)
    {
        Value = value;
    }

    /// <summary>The submitted value.</summary>
    public MentionValue Value { get; }
}
