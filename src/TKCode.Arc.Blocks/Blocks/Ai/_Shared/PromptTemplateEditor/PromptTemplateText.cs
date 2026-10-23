using System.Text;

namespace TKCode.Arc;

/// <summary>A row of the "{{" list: an existing variable, or the last row that adds a new one.</summary>
/// <param name="Name">The variable's name.</param>
/// <param name="IsAdd">True for the "+ Add name" row.</param>
internal readonly record struct PromptVariableRow(string Name, bool IsAdd);

/// <summary>
/// The text model of <see cref="PromptTemplateEditor"/>: a template is plain text in which <c>{{name}}</c> marks a
/// variable (letters, digits and underscores; any script). Pure functions, testable without a window.
/// </summary>
internal static class PromptTemplateText
{
    /// <summary>The token kind of a variable chip.</summary>
    public const string VariableKind = "Variable";

    /// <summary>A character a variable name may hold.</summary>
    public static bool IsNameChar(char c) => c == '_' || char.IsLetterOrDigit(c);

    /// <summary>Whether <paramref name="name"/> is a valid variable name (not empty; letters, digits, underscores).</summary>
    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }
        foreach (var c in name)
        {
            if (!IsNameChar(c))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>The <c>{{name}}</c> markers of <paramref name="text"/> as chips, in order (no spaces inside the braces).</summary>
    public static List<TokenSpan> Parse(string text)
    {
        var result = new List<TokenSpan>();
        var i = 0;
        while ((i = text.IndexOf("{{", i, StringComparison.Ordinal)) >= 0)
        {
            var nameStart = i + 2;
            var j = nameStart;
            while (j < text.Length && IsNameChar(text[j]))
            {
                j++;
            }
            if (j > nameStart && j + 1 < text.Length && text[j] == '}' && text[j + 1] == '}')
            {
                var name = text.Substring(nameStart, j - nameStart);
                result.Add(new TokenSpan(VariableKind, name, name, i, j + 2));
                i = j + 2;
            }
            else
            {
                // "{{{x}}": the marker may start one brace later.
                i++;
            }
        }
        return result;
    }

    /// <summary>The variable names of <paramref name="text"/>, each once, in order of first use.</summary>
    public static List<string> Names(string text)
    {
        var names = new List<string>();
        foreach (var chip in Parse(text))
        {
            if (!names.Contains(chip.Id))
            {
                names.Add(chip.Id);
            }
        }
        return names;
    }

    /// <summary>
    /// Arc's normalization: <c>{{ name }}</c> (spaces or tabs inside the braces) becomes <c>{{name}}</c>. Returns the same
    /// string instance when nothing changes.
    /// </summary>
    public static string Normalize(string text) => Normalize(text, -1, out _);

    /// <summary>
    /// <see cref="Normalize(string)"/>, also moving <paramref name="caret"/> (an index in <paramref name="text"/>) to the
    /// same place in the result.
    /// </summary>
    public static string Normalize(string text, int caret, out int newCaret)
    {
        newCaret = caret;
        if (text.IndexOf("{{", StringComparison.Ordinal) < 0)
        {
            return text;
        }
        StringBuilder? builder = null;
        var copied = 0;
        var i = 0;
        while ((i = text.IndexOf("{{", i, StringComparison.Ordinal)) >= 0)
        {
            var j = i + 2;
            var leading = SkipBlanks(text, j);
            var nameEnd = leading;
            while (nameEnd < text.Length && IsNameChar(text[nameEnd]))
            {
                nameEnd++;
            }
            var trailing = SkipBlanks(text, nameEnd);
            var spaced = leading > j || trailing > nameEnd;
            if (nameEnd > leading && spaced && trailing + 1 < text.Length && text[trailing] == '}' && text[trailing + 1] == '}')
            {
                builder ??= new StringBuilder(text.Length);
                builder.Append(text, copied, j - copied);
                builder.Append(text, leading, nameEnd - leading);
                copied = trailing;
                // Characters removed before the caret move it left (a caret inside the blanks lands on the name's edge).
                if (caret > j)
                {
                    newCaret -= Math.Min(caret, leading) - j;
                }
                if (caret > nameEnd)
                {
                    newCaret -= Math.Min(caret, trailing) - nameEnd;
                }
                i = trailing + 2;
            }
            else
            {
                i++;
            }
        }
        if (builder is null)
        {
            return text;
        }
        builder.Append(text, copied, text.Length - copied);
        return builder.ToString();
    }

    private static int SkipBlanks(string text, int at)
    {
        while (at < text.Length && text[at] is ' ' or '\t')
        {
            at++;
        }
        return at;
    }

    /// <summary>
    /// Whether the open query at <paramref name="symbol"/> (the editor's '{' trigger) is a variable query: the brace
    /// before it is a '{' too, so the person typed "{{".
    /// </summary>
    public static bool IsVariableQuery(string text, int symbol) => symbol >= 1 && symbol < text.Length && text[symbol - 1] == '{' && text[symbol] == '{';

    /// <summary>
    /// The rows of the "{{" list for <paramref name="query"/>: the variables whose name contains it (ignoring case), then
    /// "+ Add query" when the query is a valid name that is not a variable yet. An invalid query (not a name) lists
    /// nothing; an empty one lists every variable.
    /// </summary>
    public static List<PromptVariableRow> Rows(IEnumerable<string> variables, string query)
    {
        var rows = new List<PromptVariableRow>();
        if (query.Length > 0 && !IsValidName(query))
        {
            return rows;
        }
        var exists = false;
        foreach (var name in variables)
        {
            if (string.Equals(name, query, StringComparison.Ordinal))
            {
                exists = true;
            }
            if (query.Length == 0 || name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                rows.Add(new PromptVariableRow(name, false));
            }
        }
        if (query.Length > 0 && !exists)
        {
            rows.Add(new PromptVariableRow(query, true));
        }
        return rows;
    }

    /// <summary>
    /// Where Alt+Backspace at <paramref name="caret"/> deletes from: the blanks before the caret, then a whole chip (a chip
    /// counts as one word), a word (stopping at a chip), or a run of other symbols. Never crosses a line break unless the
    /// caret sits right after one.
    /// </summary>
    public static int WordDeleteStart(string text, IReadOnlyList<TokenSpan> chips, int caret)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        if (caret == 0)
        {
            return 0;
        }
        if (text[caret - 1] == '\n')
        {
            return caret - 1;
        }
        var at = caret;
        while (at > 0 && text[at - 1] is ' ' or '\t' && ChipEndingAt(chips, at) is null)
        {
            at--;
        }
        if (at == 0 || text[at - 1] == '\n')
        {
            return at;
        }
        if (ChipEndingAt(chips, at) is { } chip)
        {
            return chip.Start;
        }
        var word = IsNameChar(text[at - 1]);
        while (at > 0 && ChipEndingAt(chips, at) is null)
        {
            var c = text[at - 1];
            if (char.IsWhiteSpace(c) || IsNameChar(c) != word)
            {
                break;
            }
            at--;
        }
        return at;
    }

    private static TokenSpan? ChipEndingAt(IReadOnlyList<TokenSpan> chips, int index)
    {
        foreach (var chip in chips)
        {
            if (chip.End == index)
            {
                return chip;
            }
        }
        return null;
    }

    /// <summary>
    /// Where the caret goes when the text changes from <paramref name="current"/> to <paramref name="restored"/> (undo and
    /// redo): after the part of <paramref name="restored"/> that differs, so the restored text is just before it.
    /// </summary>
    public static int CaretAfterChange(string current, string restored)
    {
        var prefix = 0;
        var max = Math.Min(current.Length, restored.Length);
        while (prefix < max && current[prefix] == restored[prefix])
        {
            prefix++;
        }
        var suffix = 0;
        while (suffix < current.Length - prefix && suffix < restored.Length - prefix &&
               current[current.Length - 1 - suffix] == restored[restored.Length - 1 - suffix])
        {
            suffix++;
        }
        return restored.Length - suffix;
    }
}
