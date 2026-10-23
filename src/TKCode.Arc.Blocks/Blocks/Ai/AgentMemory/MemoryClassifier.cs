namespace TKCode.Arc;

/// <summary>
/// The built-in guess at the group of a typed memory, used by <see cref="AgentMemory"/> when no
/// <see cref="AgentMemory.ClassifyAsync"/> is set. It reads the sentence in this order and stops at the first rule that
/// matches:
/// <list type="number">
/// <item><b>People</b>: a known first name (<see cref="Names"/>), or two capitalised words in a row that are not the
/// sentence's first word ("Pairs with Tomás Ferreira", but not "Uses British spelling").</item>
/// <item><b>Preferences</b>: one of the <see cref="PreferenceWords"/> ("prefers", "wants", "uses", "rather" ...).</item>
/// <item><b>Projects</b>: one of the <see cref="ProjectWords"/> ("project", "moving", "drafting", "building" ...).</item>
/// <item>Otherwise the first group (About you by default).</item>
/// </list>
/// Words are compared whole and without case; a guessed key the card does not have falls back to the first group.
/// Pure and allocation-light: no patterns, no state.
/// </summary>
internal static class MemoryClassifier
{
    /// <summary>The default group keys the guess can return.</summary>
    public const string AboutKey = "about";

    /// <inheritdoc cref="AboutKey"/>
    public const string PreferencesKey = "preferences";

    /// <inheritdoc cref="AboutKey"/>
    public const string ProjectsKey = "projects";

    /// <inheritdoc cref="AboutKey"/>
    public const string PeopleKey = "people";

    /// <summary>First names that always mean a person (the invented people of the sample data).</summary>
    public static readonly IReadOnlySet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Mina", "Tomás", "Tomas", "Ingrid"
    };

    /// <summary>Words that state a liking, a habit or a rule for answers.</summary>
    public static readonly IReadOnlySet<string> PreferenceWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "prefer", "prefers", "preferred", "preference", "like", "likes", "love", "loves", "want", "wants", "use", "uses",
        "rather", "avoid", "avoids", "dislike", "dislikes", "hate", "hates", "favourite", "favorite", "always", "never",
        "style", "tone"
    };

    /// <summary>Words about ongoing work.</summary>
    public static readonly IReadOnlySet<string> ProjectWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "project", "projects", "moving", "migrating", "migration", "drafting", "draft", "building", "launching", "launch",
        "shipping", "rewriting", "rewrite", "proposal", "roadmap", "deadline", "sprint", "release", "quarter", "prototype"
    };

    /// <summary>
    /// The group key for <paramref name="text"/> among <paramref name="groupKeys"/> (in filter order). A guess that is not
    /// in the list returns the first key; an empty list returns the guess itself (<see cref="AboutKey"/> when nothing matches).
    /// </summary>
    public static string Classify(string? text, IReadOnlyList<string> groupKeys)
    {
        var guess = Guess(text ?? string.Empty);
        if (groupKeys.Count == 0)
        {
            return guess ?? AboutKey;
        }
        if (guess is not null)
        {
            foreach (var key in groupKeys)
            {
                if (string.Equals(key, guess, StringComparison.Ordinal))
                {
                    return key;
                }
            }
        }
        return groupKeys[0];
    }

    /// <summary>The key of the first rule that matches, or null (the first group).</summary>
    internal static string? Guess(string text)
    {
        if (MentionsPerson(text))
        {
            return PeopleKey;
        }
        if (HasWord(text, PreferenceWords))
        {
            return PreferencesKey;
        }
        if (HasWord(text, ProjectWords))
        {
            return ProjectsKey;
        }
        return null;
    }

    /// <summary>A known name anywhere, or two name-like words in a row after the first word.</summary>
    internal static bool MentionsPerson(string text)
    {
        var index = 0;
        var previousNameLike = false;
        foreach (var range in Words(text))
        {
            var word = text.AsSpan(range.Start, range.Length);
            if (Contains(Names, word))
            {
                return true;
            }
            var nameLike = IsNameLike(word);
            if (nameLike && previousNameLike)
            {
                return true;
            }
            // The first word is capitalised because it starts the sentence: it never counts as half of a name.
            previousNameLike = nameLike && index > 0;
            index++;
        }
        return false;
    }

    /// <summary>A capital letter followed by at least one lowercase letter ("Ferreira", not "CLI" or "C#").</summary>
    private static bool IsNameLike(ReadOnlySpan<char> word)
    {
        if (word.Length < 2 || !char.IsUpper(word[0]))
        {
            return false;
        }
        for (var i = 1; i < word.Length; i++)
        {
            if (!char.IsLower(word[i]))
            {
                return false;
            }
        }
        return true;
    }

    private static bool HasWord(string text, IReadOnlySet<string> words)
    {
        foreach (var range in Words(text))
        {
            if (Contains(words, text.AsSpan(range.Start, range.Length)))
            {
                return true;
            }
        }
        return false;
    }

    private static bool Contains(IReadOnlySet<string> set, ReadOnlySpan<char> word)
    {
        // The sets are small and case-insensitive; a span lookup keeps the scan free of substrings.
        foreach (var entry in set)
        {
            if (word.Equals(entry, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The letter runs of <paramref name="text"/> (apostrophes inside a word are kept: "don't").</summary>
    internal static IEnumerable<(int Start, int Length)> Words(string text)
    {
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var c = i < text.Length ? text[i] : ' ';
            var letter = char.IsLetter(c) || (start >= 0 && (c == '\'' || c == '’') && i + 1 < text.Length && char.IsLetter(text[i + 1]));
            if (letter)
            {
                if (start < 0)
                {
                    start = i;
                }
            }
            else if (start >= 0)
            {
                yield return (start, i - start);
                start = -1;
            }
        }
    }
}
