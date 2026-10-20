using System.Collections.Frozen;

namespace TKCode.Arc;

/// <summary>Syntax role of a highlighted span (Arc's CSS classes).</summary>
internal enum CodeBlockSyntaxKind : byte
{
    None,
    Comment,
    String,
    Number,
    Keyword,
    Type,
    Function,
    Property,
    Tag,
    Punctuation
}

/// <summary>A highlighted range of the displayed source.</summary>
internal readonly record struct CodeBlockSyntaxSpan(int Start, int Length, CodeBlockSyntaxKind Kind);

/// <summary>
/// Port of Arc's lightweight highlighter: one left-to-right scan with the same alternatives and priorities as its
/// token regex (block / line / HTML comments, template and quoted strings, numbers, identifiers, punctuation) and
/// the same classification rules (keywords and literals, built-in types, JSX/HTML tags, calls, properties, JSON
/// keys). Hand-written instead of <c>Regex</c> so it allocates nothing besides the output list; it runs once per
/// source or language change. Extensions beyond Arc: <c>cs</c>/<c>csharp</c> keywords and types, and
/// <c>xml</c>/<c>xaml</c>/<c>axaml</c>/<c>svg</c> tags (with namespace prefixes) and attributes.
/// </summary>
internal static class CodeBlockTokenizer
{
    private static readonly FrozenSet<string> s_keywords = FrozenSet.ToFrozenSet(new[]
    {
        "abstract", "as", "async", "await", "break", "case", "catch", "class", "const", "continue", "default", "delete",
        "do", "else", "enum", "export", "extends", "finally", "for", "from", "function", "get", "if", "implements",
        "import", "in", "instanceof", "interface", "let", "new", "of", "private", "protected", "public", "readonly",
        "return", "set", "static", "switch", "throw", "try", "type", "typeof", "var", "void", "while", "with", "yield",
        "SELECT", "FROM", "WHERE", "INSERT", "UPDATE", "DELETE",
        // literals
        "true", "false", "null", "undefined", "NaN", "Infinity"
    }, StringComparer.Ordinal);

    private static readonly FrozenSet<string> s_types = FrozenSet.ToFrozenSet(new[]
    {
        "Array", "Boolean", "Date", "Error", "Map", "Number", "Promise", "Record", "Set", "String", "ReactNode",
        "HTMLElement", "HTMLButtonElement", "Event", "unknown", "never", "void", "any", "boolean", "number", "string",
        "object"
    }, StringComparer.Ordinal);

    private static readonly FrozenSet<string> s_csharpKeywords = FrozenSet.ToFrozenSet(new[]
    {
        "base", "bool", "byte", "char", "checked", "decimal", "delegate", "double", "event", "explicit", "extern",
        "fixed", "float", "foreach", "goto", "implicit", "init", "int", "internal", "is", "lock", "long", "namespace",
        "nameof", "object", "operator", "out", "override", "params", "partial", "record", "ref", "required", "sbyte",
        "sealed", "short", "sizeof", "stackalloc", "string", "struct", "this", "uint", "ulong", "unchecked", "unsafe",
        "ushort", "using", "value", "virtual", "volatile", "when", "where", "file", "scoped", "global"
    }, StringComparer.Ordinal);

    private static readonly FrozenSet<string> s_csharpTypes = FrozenSet.ToFrozenSet(new[]
    {
        "Task", "ValueTask", "List", "Dictionary", "HashSet", "IEnumerable", "IList", "IReadOnlyList", "Action",
        "Func", "Exception", "DateTime", "DateOnly", "TimeSpan", "Guid", "Span", "ReadOnlySpan", "Memory", "Console",
        "Control", "Window", "Application", "AvaloniaProperty", "StyledProperty", "DirectProperty", "Thickness",
        "CornerRadius", "Color", "Brush", "IBrush", "Size", "Rect", "Point"
    }, StringComparer.Ordinal);

    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> s_keywordLookup = s_keywords.GetAlternateLookup<ReadOnlySpan<char>>();
    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> s_typeLookup = s_types.GetAlternateLookup<ReadOnlySpan<char>>();
    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> s_csKeywordLookup = s_csharpKeywords.GetAlternateLookup<ReadOnlySpan<char>>();
    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> s_csTypeLookup = s_csharpTypes.GetAlternateLookup<ReadOnlySpan<char>>();

    /// <summary>Arc's <c>normaliseLanguage</c>: lower case, no leading dot.</summary>
    public static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrEmpty(language))
        {
            return string.Empty;
        }
        var lower = language.ToLowerInvariant();
        return lower.StartsWith('.') ? lower[1..] : lower;
    }

    private readonly struct Mode
    {
        public Mode(string language)
        {
            Json = language == "json";
            Xml = language is "xml" or "xaml" or "axaml" or "svg";
            Tags = Xml || language is "tsx" or "jsx" or "html" or "vue";
            Props = language is "css" or "tsx" or "jsx";
            CSharp = language is "cs" or "csharp" or "c#";
        }

        public bool Json { get; }
        public bool Xml { get; }
        public bool Tags { get; }
        public bool Props { get; }
        public bool CSharp { get; }
    }

    /// <summary>Appends the highlighted spans of <paramref name="code"/> (ascending, non-overlapping) to <paramref name="output"/>.</summary>
    public static void Tokenize(string code, string language, List<CodeBlockSyntaxSpan> output)
    {
        var mode = new Mode(language);
        var n = code.Length;
        var p = 0;
        while (p < n)
        {
            var c = code[p];
            var next = p + 1 < n ? code[p + 1] : '\0';
            int end;
            if (c == '/' && next == '*' && (end = code.IndexOf("*/", p + 2, StringComparison.Ordinal)) >= 0)
            {
                Add(output, p, end + 2, CodeBlockSyntaxKind.Comment);
                p = end + 2;
                continue;
            }
            if (c == '/' && next == '/')
            {
                end = code.IndexOf('\n', p);
                end = end < 0 ? n : end;
                Add(output, p, end, CodeBlockSyntaxKind.Comment);
                p = end;
                continue;
            }
            if (c == '<' && next == '!' && string.CompareOrdinal(code, p, "<!--", 0, 4) == 0
                && (end = code.IndexOf("-->", p + 4, StringComparison.Ordinal)) >= 0)
            {
                Add(output, p, end + 3, CodeBlockSyntaxKind.Comment);
                p = end + 3;
                continue;
            }
            if (c is '`' or '"' or '\'')
            {
                end = ScanString(code, p, c);
                if (end > 0)
                {
                    var kind = mode.Json && FollowedBy(code, end, ':') ? CodeBlockSyntaxKind.Property : CodeBlockSyntaxKind.String;
                    Add(output, p, end, kind);
                    p = end;
                    continue;
                }
                // An unterminated quote is plain text, as with Arc's regex.
                p++;
                continue;
            }
            if (IsDigit(c) && Boundary(code, p))
            {
                end = ScanNumber(code, p);
                if (end > 0)
                {
                    Add(output, p, end, CodeBlockSyntaxKind.Number);
                    p = end;
                    continue;
                }
            }
            if (IsIdentStart(c) && Boundary(code, p))
            {
                end = ScanIdentifier(code, p);
                if (end > 0)
                {
                    var kind = ClassifyWord(code, p, end, mode);
                    if (kind != CodeBlockSyntaxKind.None)
                    {
                        Add(output, p, end, kind);
                    }
                    p = end;
                    continue;
                }
            }
            if (IsPunctuation(c))
            {
                Add(output, p, p + 1, CodeBlockSyntaxKind.Punctuation);
            }
            p++;
        }
    }

    private static void Add(List<CodeBlockSyntaxSpan> output, int start, int end, CodeBlockSyntaxKind kind)
        => output.Add(new CodeBlockSyntaxSpan(start, end - start, kind));

    /// <summary><c>`(?:\\.|[^`\\])*`</c> and its quote variants; returns the end (exclusive) or -1.</summary>
    private static int ScanString(string code, int start, char quote)
    {
        var n = code.Length;
        var i = start + 1;
        while (i < n)
        {
            var ch = code[i];
            if (ch == '\\')
            {
                // `.` does not match line terminators, so an escaped line break ends the match attempt.
                if (i + 1 >= n || IsLineTerminator(code[i + 1]))
                {
                    return -1;
                }
                i += 2;
                continue;
            }
            if (ch == quote)
            {
                return i + 1;
            }
            i++;
        }
        return -1;
    }

    /// <summary><c>\b\d+(?:\.\d+)?\b</c> starting at a boundary; returns the end (exclusive) or -1.</summary>
    private static int ScanNumber(string code, int start)
    {
        var n = code.Length;
        var i = start;
        while (i < n && IsDigit(code[i]))
        {
            i++;
        }
        var integerEnd = i;
        if (i + 1 < n && code[i] == '.' && IsDigit(code[i + 1]))
        {
            i += 1;
            while (i < n && IsDigit(code[i]))
            {
                i++;
            }
            if (Boundary(code, i))
            {
                return i;
            }
        }
        // Shorter digit runs never end on a boundary (digit to digit), so only the whole integer part remains.
        return Boundary(code, integerEnd) ? integerEnd : -1;
    }

    /// <summary><c>\b[A-Za-z_$][\w$-]*\b</c> (greedy, then backtracking to a boundary); returns the end or -1.</summary>
    private static int ScanIdentifier(string code, int start)
    {
        var n = code.Length;
        var i = start + 1;
        while (i < n && IsIdentPart(code[i]))
        {
            i++;
        }
        for (var end = i; end > start; end--)
        {
            if (Boundary(code, end))
            {
                return end;
            }
        }
        return -1;
    }

    private static CodeBlockSyntaxKind ClassifyWord(string code, int start, int end, in Mode mode)
    {
        var word = code.AsSpan(start, end - start);
        if (s_keywordLookup.Contains(word) || (mode.CSharp && s_csKeywordLookup.Contains(word)))
        {
            return CodeBlockSyntaxKind.Keyword;
        }
        if (s_typeLookup.Contains(word) || (mode.CSharp && s_csTypeLookup.Contains(word)))
        {
            return CodeBlockSyntaxKind.Type;
        }
        if (mode.Tags && IsTagName(code, start, mode.Xml))
        {
            return CodeBlockSyntaxKind.Tag;
        }
        if (!mode.Json && FollowedBy(code, end, '('))
        {
            return CodeBlockSyntaxKind.Function;
        }
        if (mode.Props && (FollowedBy(code, end, ':') || FollowedBy(code, end, '=')))
        {
            return CodeBlockSyntaxKind.Property;
        }
        if (mode.Xml && FollowedBy(code, end, '='))
        {
            return CodeBlockSyntaxKind.Property;
        }
        return CodeBlockSyntaxKind.None;
    }

    /// <summary>Arc: the source before the word ends with <c>&lt;</c> or <c>&lt;/</c> (XML also allows a <c>prefix:</c>).</summary>
    private static bool IsTagName(string code, int start, bool xml)
    {
        var i = start - 1;
        if (xml && i >= 0 && code[i] == ':')
        {
            // <arc:Button → step over the namespace prefix.
            var j = i - 1;
            while (j >= 0 && IsIdentPart(code[j]))
            {
                j--;
            }
            if (j < i - 1)
            {
                i = j;
            }
        }
        if (i >= 0 && code[i] == '/')
        {
            i--;
        }
        return i >= 0 && code[i] == '<';
    }

    /// <summary>JS <c>/^\s*X/</c> on the text after <paramref name="index"/>.</summary>
    private static bool FollowedBy(string code, int index, char expected)
    {
        var i = index;
        while (i < code.Length && IsJsWhitespace(code[i]))
        {
            i++;
        }
        return i < code.Length && code[i] == expected;
    }

    /// <summary>A JS <c>\b</c> between <paramref name="index"/> - 1 and <paramref name="index"/>.</summary>
    private static bool Boundary(string code, int index)
    {
        var before = index > 0 && IsWord(code[index - 1]);
        var after = index < code.Length && IsWord(code[index]);
        return before != after;
    }

    private static bool IsDigit(char c) => c is >= '0' and <= '9';

    private static bool IsWord(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_';

    private static bool IsIdentStart(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '_' or '$';

    private static bool IsIdentPart(char c) => IsWord(c) || c is '$' or '-';

    private static bool IsPunctuation(char c) => c is '{' or '}' or '[' or ']' or '(' or ')' or ';' or ',' or '.' or '<'
        or '>' or ':' or '=' or '+' or '*' or '/' or '!' or '?' or '|' or '&' or '-';

    private static bool IsLineTerminator(char c) => c is '\n' or '\r' or '\u2028' or '\u2029';

    private static bool IsJsWhitespace(char c) => c is ' ' or '\t' or '\n' or '\r' or '\v' or '\f' or '\u00A0' or '\uFEFF'
        || (c > 127 && char.IsWhiteSpace(c));
}
