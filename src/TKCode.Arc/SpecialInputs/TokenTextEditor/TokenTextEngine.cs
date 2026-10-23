namespace TKCode.Arc;

/// <summary>An open query: <see cref="Trigger"/>'s symbol at <see cref="Start"/> followed by <see cref="Query"/> up to the caret.</summary>
internal readonly record struct TokenTriggerMatch(TokenTrigger Trigger, int Start, string Query);

/// <summary>A <see cref="TokenTrigger"/>'s rules as plain values (the scan needs no Avalonia objects).</summary>
/// <param name="Symbol">The character that opens the query.</param>
/// <param name="Position">What may stand before the symbol.</param>
/// <param name="MaxQuerySpaces">Spaces allowed in the query (0 = none).</param>
/// <param name="AllowAfterOpeningBracket">Also after <c>( [ {</c> or a straight quote.</param>
/// <param name="IsEnabled">False: the symbol opens nothing but still ends other queries.</param>
internal readonly record struct TriggerRule(char Symbol, TokenTriggerPosition Position, int MaxQuerySpaces, bool AllowAfterOpeningBracket, bool IsEnabled)
{
    public static TriggerRule From(TokenTrigger trigger) => new(trigger.Symbol, trigger.Position,
        trigger.AllowSpacesInQuery ? trigger.MaxQuerySpaces : 0, trigger.AllowAfterOpeningBracket, trigger.IsEnabled);
}

/// <summary>
/// The text model of <see cref="TokenTextEditor"/> (generalized from MentionInput, which ports Arc's mention-input.tsx):
/// moving tokens along with edits and finding the open trigger. Pure functions, testable without a window.
/// </summary>
internal static class TokenTextEngine
{
    /// <summary>How far before the caret a trigger symbol is looked for.</summary>
    public const int TriggerReach = 48;

    /// <summary>
    /// The edit between <paramref name="previous"/> and <paramref name="text"/>: the unchanged prefix (at most up to
    /// <paramref name="hint"/>, the caret before the edit, which settles repeated characters), the end of the replaced
    /// range in the old text, and the length change.
    /// </summary>
    public static (int Prefix, int OldEnd, int Delta) EditWindow(string previous, string text, int hint)
    {
        var prefix = 0;
        var maxPrefix = Math.Min(Math.Min(previous.Length, text.Length), Math.Max(0, hint));
        while (prefix < maxPrefix && previous[prefix] == text[prefix])
        {
            prefix++;
        }
        var suffix = 0;
        while (suffix < previous.Length - prefix && suffix < text.Length - prefix &&
               previous[previous.Length - 1 - suffix] == text[text.Length - 1 - suffix])
        {
            suffix++;
        }
        return (prefix, previous.Length - suffix, text.Length - previous.Length);
    }

    /// <summary>
    /// Moves <paramref name="tokens"/> along with the edit from <paramref name="previous"/> to <paramref name="text"/>
    /// and drops every token the edit touched (an edit inside a token dissolves it into plain text).
    /// </summary>
    public static List<TokenSpan> Reconcile(string previous, IReadOnlyList<TokenSpan> tokens, string text, int hint)
    {
        var (prefix, oldEnd, delta) = EditWindow(previous, text, hint);
        var result = new List<TokenSpan>(tokens.Count);
        foreach (var token in tokens)
        {
            TokenSpan moved;
            if (token.End <= prefix)
            {
                moved = token;
            }
            else if (token.Start >= oldEnd)
            {
                moved = token with { Start = token.Start + delta, End = token.End + delta };
            }
            else
            {
                continue;
            }
            if (Holds(text, moved, previous, token.Start))
            {
                result.Add(moved);
            }
        }
        return result;
    }

    /// <summary>True when <paramref name="text"/> holds <paramref name="expected"/> where <paramref name="token"/> says.</summary>
    public static bool Holds(string text, TokenSpan token, string expected)
        => Holds(text, token, expected, 0) && token.Length == expected.Length;

    private static bool Holds(string text, TokenSpan token, string source, int sourceStart)
    {
        var length = token.Length;
        return token.Start >= 0 && length > 0 && token.End <= text.Length && sourceStart >= 0 &&
               sourceStart + length <= source.Length &&
               string.CompareOrdinal(text, token.Start, source, sourceStart, length) == 0;
    }

    /// <summary>
    /// The trigger the caret is in, if any. Walking back from the caret (at most <see cref="TriggerReach"/>
    /// characters, never across a line break), the first symbol of any trigger decides: it opens a query when its
    /// trigger is enabled, its position rule holds, it is not inside a token, and the query up to the caret has the
    /// trigger's shape (no line break, spaces only as allowed). So another trigger's symbol always ends a query.
    /// </summary>
    public static TokenTriggerMatch? FindTrigger(string text, int caret, IReadOnlyList<TokenSpan> tokens, IReadOnlyList<TokenTrigger> triggers)
    {
        var count = triggers.Count;
        if (count == 0)
        {
            return null;
        }
        Span<TriggerRule> rules = count <= 8 ? stackalloc TriggerRule[count] : new TriggerRule[count];
        for (var i = 0; i < count; i++)
        {
            rules[i] = TriggerRule.From(triggers[i]);
        }
        var found = FindTrigger(text, caret, tokens, rules);
        return found is { } f ? new TokenTriggerMatch(triggers[f.Rule], f.Start, f.Query) : null;
    }

    /// <summary><see cref="FindTrigger(string, int, IReadOnlyList{TokenSpan}, IReadOnlyList{TokenTrigger})"/> on plain rules.</summary>
    public static (int Rule, int Start, string Query)? FindTrigger(string text, int caret, IReadOnlyList<TokenSpan> tokens, ReadOnlySpan<TriggerRule> rules)
    {
        if (rules.IsEmpty)
        {
            return null;
        }
        caret = Math.Clamp(caret, 0, text.Length);
        for (var index = caret - 1; index >= 0 && index >= caret - TriggerReach; index--)
        {
            var c = text[index];
            if (c == '\n')
            {
                return null;
            }
            var ruleIndex = RuleFor(rules, c);
            if (ruleIndex < 0)
            {
                continue;
            }
            var rule = rules[ruleIndex];
            if (!rule.IsEnabled || !IsLeadAllowed(text, index, rule))
            {
                return null;
            }
            foreach (var token in tokens)
            {
                if (index >= token.Start && index < token.End)
                {
                    return null;
                }
            }
            var query = text.Substring(index + 1, caret - index - 1);
            return IsQueryShape(query, rule) ? (ruleIndex, index, query) : null;
        }
        return null;
    }

    private static int RuleFor(ReadOnlySpan<TriggerRule> rules, char c)
    {
        for (var i = 0; i < rules.Length; i++)
        {
            if (rules[i].Symbol == c)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>The first trigger in <paramref name="triggers"/> that inserts tokens of <paramref name="kind"/>.</summary>
    public static TokenTrigger? TriggerForKind(IReadOnlyList<TokenTrigger> triggers, string kind)
    {
        foreach (var trigger in triggers)
        {
            if (string.Equals(trigger.Kind, kind, StringComparison.Ordinal))
            {
                return trigger;
            }
        }
        return null;
    }

    /// <summary>The position rule: what may stand right before the symbol.</summary>
    private static bool IsLeadAllowed(string text, int index, in TriggerRule rule)
    {
        if (index == 0)
        {
            return true;
        }
        var lead = text[index - 1];
        return rule.Position switch
        {
            TokenTriggerPosition.Anywhere => true,
            TokenTriggerPosition.LineStart => lead == '\n',
            _ => char.IsWhiteSpace(lead) || (rule.AllowAfterOpeningBracket && lead is '(' or '[' or '{' or '"' or '\'')
        };
    }

    /// <summary>No whitespace in the query except plain spaces when the trigger allows them (MentionInput: one).</summary>
    private static bool IsQueryShape(string query, in TriggerRule rule)
    {
        var spaces = 0;
        var allowed = rule.MaxQuerySpaces;
        foreach (var c in query)
        {
            if (c == ' ' && allowed > 0)
            {
                spaces++;
                if (spaces > allowed)
                {
                    return false;
                }
                continue;
            }
            if (char.IsWhiteSpace(c))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Sorts <paramref name="tokens"/> by offset and keeps those inside <paramref name="text"/> that do not overlap an
    /// earlier one.
    /// </summary>
    public static List<TokenSpan> Normalize(string text, IEnumerable<TokenSpan> tokens)
    {
        var sorted = new List<TokenSpan>(tokens);
        sorted.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        var result = new List<TokenSpan>(sorted.Count);
        var end = 0;
        foreach (var token in sorted)
        {
            if (token.Start < end || token.Start < 0 || token.Length <= 0 || token.End > text.Length)
            {
                continue;
            }
            result.Add(token);
            end = token.End;
        }
        return result;
    }
}
