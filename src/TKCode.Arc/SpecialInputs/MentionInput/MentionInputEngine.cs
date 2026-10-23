namespace TKCode.Arc;

/// <summary>An open trigger: <c>@</c> or <c>#</c> at <see cref="Start"/> followed by <see cref="Query"/> up to the caret.</summary>
internal readonly record struct MentionInputTrigger(MentionKind Kind, int Start, string Query);

/// <summary>One suggestion row: a person or a channel.</summary>
internal readonly record struct MentionInputSuggestion(MentionKind Kind, MentionPerson? Person, MentionChannel? Channel)
{
    public string Id => Person?.Id ?? Channel!.Id;

    public string Name => Person?.Name ?? Channel!.Name;

    public string? Meta => Person is not null ? Person.Role : Channel!.Description;
}

/// <summary>
/// The text model of <see cref="MentionInput"/>, ported from Arc's mention-input.tsx: moving mentions with edits
/// (<c>reconcile</c>), finding the open trigger (<c>findTrigger</c>) and ranking suggestions (<c>rank</c>).
/// Pure functions so the behaviour is testable without a window.
/// </summary>
internal static class MentionInputEngine
{
    /// <summary>How far before the caret a trigger is looked for.</summary>
    public const int TriggerReach = TokenTextEngine.TriggerReach;

    private static readonly char[] s_wordSeparators = [' ', '\t', '\n', '\r', '\f', '\v', '-', '_', '.'];

    /// <summary>
    /// Moves mentions along with an edit from <paramref name="previous"/> to <paramref name="text"/> and drops any the
    /// edit touched. <paramref name="hint"/> is the caret before the edit, which settles repeated characters.
    /// </summary>
    public static List<Mention> Reconcile(MentionValue previous, string text, int hint)
    {
        var (prefix, oldEnd, delta) = TokenTextEngine.EditWindow(previous.Text, text, hint);
        var result = new List<Mention>(previous.Mentions.Count);
        foreach (var mention in previous.Mentions)
        {
            Mention moved;
            if (mention.End <= prefix)
            {
                moved = mention;
            }
            else if (mention.Start >= oldEnd)
            {
                moved = mention with { Start = mention.Start + delta, End = mention.End + delta };
            }
            else
            {
                continue;
            }
            if (Holds(text, moved))
            {
                result.Add(moved);
            }
        }
        return result;
    }

    /// <summary>True when <paramref name="text"/> still reads <c>@Label</c> / <c>#Label</c> where the mention says.</summary>
    public static bool Holds(string text, Mention mention)
    {
        if (mention.Start < 0 || mention.End > text.Length || mention.End < mention.Start)
        {
            return false;
        }
        var expected = mention.Text;
        return mention.End - mention.Start == expected.Length &&
               string.CompareOrdinal(text, mention.Start, expected, 0, expected.Length) == 0;
    }

    /// <summary>
    /// Arc's <c>@</c> trigger: at the start or after whitespace or one of <c>( [ { " '</c>; the query may hold one space.
    /// <paramref name="enabled"/> false when there are no people (the symbol still ends a channel query).
    /// </summary>
    public static TriggerRule PersonRule(bool enabled) =>
        new('@', TokenTriggerPosition.AfterWhitespaceOrLineStart, 1, AllowAfterOpeningBracket: true, enabled);

    /// <summary>Arc's <c>#</c> trigger: as <see cref="PersonRule"/>, but the query takes no space.</summary>
    public static TriggerRule ChannelRule(bool enabled) =>
        new('#', TokenTriggerPosition.AfterWhitespaceOrLineStart, 0, AllowAfterOpeningBracket: true, enabled);

    /// <summary>
    /// The trigger the caret is in, if any: <c>@</c> or <c>#</c> at the start or after whitespace or one of
    /// <c>( [ { " '</c>, within 48 characters, not inside a mention and not across a line break. A person query
    /// allows one space; a channel query none. (The rules of <see cref="TokenTextEngine.FindTrigger(string, int, IReadOnlyList{TokenSpan}, ReadOnlySpan{TriggerRule})"/>.)
    /// </summary>
    public static MentionInputTrigger? FindTrigger(string text, int caret, IReadOnlyList<Mention> mentions, bool people, bool channels)
    {
        var tokens = new TokenSpan[mentions.Count];
        for (var i = 0; i < tokens.Length; i++)
        {
            tokens[i] = MentionInput.ToSpan(mentions[i]);
        }
        ReadOnlySpan<TriggerRule> rules = [PersonRule(people), ChannelRule(channels)];
        return TokenTextEngine.FindTrigger(text, caret, tokens, rules) is { } found
            ? new MentionInputTrigger(found.Rule == 0 ? MentionKind.Person : MentionKind.Channel, found.Start, found.Query)
            : null;
    }

    /// <summary>Arc ranking: prefix 4, word start 3, contains 2, role or description 1, otherwise 0.</summary>
    public static int Rank(string label, string? extra, string query)
    {
        if (query.Length == 0)
        {
            return 1;
        }
        var name = label.ToLowerInvariant();
        if (name.StartsWith(query, StringComparison.Ordinal))
        {
            return 4;
        }
        foreach (var word in name.Split(s_wordSeparators, StringSplitOptions.None))
        {
            if (word.Length > 0 && word.StartsWith(query, StringComparison.Ordinal))
            {
                return 3;
            }
        }
        if (name.Contains(query, StringComparison.Ordinal))
        {
            return 2;
        }
        return (extra ?? string.Empty).ToLowerInvariant().Contains(query, StringComparison.Ordinal) ? 1 : 0;
    }

    /// <summary>The ranked suggestions for <paramref name="trigger"/>, best first, at most <paramref name="max"/>.</summary>
    public static List<MentionInputSuggestion> Suggest(MentionInputTrigger trigger, IEnumerable<MentionPerson>? people,
                                                      IEnumerable<MentionChannel>? channels, int max)
    {
        var query = trigger.Query.ToLowerInvariant().Trim();
        var scored = new List<(MentionInputSuggestion Entry, int Score)>();
        if (trigger.Kind == MentionKind.Person)
        {
            foreach (var person in people ?? [])
            {
                scored.Add((new MentionInputSuggestion(MentionKind.Person, person, null), Rank(person.Name, person.Role, query)));
            }
        }
        else
        {
            foreach (var channel in channels ?? [])
            {
                scored.Add((new MentionInputSuggestion(MentionKind.Channel, null, channel), Rank(channel.Name, channel.Description, query)));
            }
        }
        var result = new List<MentionInputSuggestion>();
        // OrderByDescending is stable, like Array.prototype.sort.
        foreach (var (entry, _) in scored.Where(static s => s.Score > 0).OrderByDescending(static s => s.Score))
        {
            if (result.Count >= Math.Max(0, max))
            {
                break;
            }
            result.Add(entry);
        }
        return result;
    }

    /// <summary>Up to two initials from the first two words (Arc <c>initials</c>).</summary>
    public static string Initials(string name)
    {
        var parts = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var count = Math.Min(2, parts.Length);
        var chars = new char[count];
        for (var i = 0; i < count; i++)
        {
            chars[i] = char.ToUpperInvariant(parts[i][0]);
        }
        return new string(chars);
    }

    /// <summary>The polite announcement for a result count: "3 people", "1 channel", "No matches".</summary>
    public static string CountLabel(MentionKind kind, int count, string noMatches) => count == 0
        ? noMatches
        : kind == MentionKind.Person
            ? count == 1 ? "1 person" : $"{count} people"
            : count == 1 ? "1 channel" : $"{count} channels";
}
