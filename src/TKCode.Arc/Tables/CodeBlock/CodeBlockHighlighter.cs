using System.Collections.Frozen;
using System.Collections.Immutable;
using StreamMark.Core.Highlighting;
using StreamMark.Highlighting.TextMate;
using TextMateSharp.Grammars;

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
/// Language groups whose TextMate colors mean different roles (an attribute value in XML and a keyword in C# share
/// Light+'s blue).
/// </summary>
internal enum CodeBlockLanguageFamily : byte
{
    Code,
    Markup,
    Json,
    Yaml,
    Css
}

/// <summary>
/// Syntax highlighting for <see cref="CodeBlock"/> and the Markdown control's fenced code, on StreamMark's TextMate
/// sessions. Sources are always tokenized with VS Code's Light+ theme (whatever the app's appearance) and each Light+
/// token color is read back as one of Arc's nine roles, which are then painted with Arc's own palette. The role table
/// was built from <c>light_plus.json</c> + <c>light_vs.json</c> in TextMateSharp.Grammars 2.0.4; a few colors mean
/// different roles per language family. Operators and brackets keep the editor foreground in TextMate themes, so
/// Arc's punctuation role is restored by character class (Arc's own tokenizer rule) inside uncolored, keyword, type,
/// function and tag runs.
/// </summary>
internal static class CodeBlockHighlighter
{
    // Light+ token colors (0xAARRGGBB), see light_plus.json / light_vs.json.
    private const uint LpComment = 0xFF008000;      // comment
    private const uint LpString = 0xFFA31515;       // string
    private const uint LpRegex = 0xFF811F3F;        // string.regexp, constant.regexp, regex character classes
    private const uint LpRegexGroup = 0xFFD16969;   // regex groups
    private const uint LpEscape = 0xFFEE0000;       // constant.character.escape, regex anchors
    private const uint LpNumber = 0xFF098658;       // constant.numeric, keyword.other.unit
    private const uint LpKeyword = 0xFF0000FF;      // keyword, storage, constant.language; attribute values in markup
    private const uint LpControl = 0xFFAF00DB;      // keyword.control
    private const uint LpType = 0xFF267F99;         // entity.name.type, support.class/type; JSON keys, CSS properties
    private const uint LpFunction = 0xFF795E26;     // entity.name.function, support.function
    private const uint LpAttribute = 0xFFFF0000;    // entity.other.attribute-name, support.type.property-name
    private const uint LpTag = 0xFF800000;          // entity.name.tag, punctuation.definition.tag; YAML keys, CSS selectors
    private const uint LpValue = 0xFF0451A5;        // CSS property values, JSON keys (light_vs)

    private static readonly FrozenDictionary<uint, CodeBlockSyntaxKind> s_roles = new Dictionary<uint, CodeBlockSyntaxKind>
    {
        [LpComment] = CodeBlockSyntaxKind.Comment,
        [LpString] = CodeBlockSyntaxKind.String,
        [LpRegex] = CodeBlockSyntaxKind.String,
        [LpRegexGroup] = CodeBlockSyntaxKind.String,
        [LpEscape] = CodeBlockSyntaxKind.String,
        [LpNumber] = CodeBlockSyntaxKind.Number,
        [LpKeyword] = CodeBlockSyntaxKind.Keyword,
        [LpControl] = CodeBlockSyntaxKind.Keyword,
        [LpType] = CodeBlockSyntaxKind.Type,
        [LpFunction] = CodeBlockSyntaxKind.Function,
        [LpAttribute] = CodeBlockSyntaxKind.Property,
        [LpTag] = CodeBlockSyntaxKind.Tag,
        // Variables (#001080), constants (#0070C1), CSS values (#0451A5), diff headers and invalid text stay plain.
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, CodeBlockLanguageFamily> s_families = BuildFamilies();

    // Arc's punctuation class: [{}[\]();,.<>:=+*/!?|&-]
    private const string PunctuationCharacters = "{}[]();,.<>:=+*/!?|&-";

    private static readonly Lazy<TextMateSyntaxHighlighter> s_engine = new(
        static () => new TextMateSyntaxHighlighter(ThemeName.LightPlus, ThemeName.LightPlus), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The shared TextMate highlighter (grammars load once per process, lazily, on first use).</summary>
    public static TextMateSyntaxHighlighter Engine => s_engine.Value;

    /// <summary>The theme every session is created with: the role table belongs to Light+.</summary>
    public const HighlightThemeKind SessionTheme = HighlightThemeKind.Light;

    /// <summary>Arc's <c>normaliseLanguage</c> for the header label: lower case, no leading dot.</summary>
    public static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrEmpty(language))
        {
            return string.Empty;
        }
        var lower = language.ToLowerInvariant();
        return lower.StartsWith('.') ? lower[1..] : lower;
    }

    /// <summary>Whether TextMate has a grammar for <paramref name="language"/> (StreamMark's aliases apply).</summary>
    public static bool SupportsLanguage(string? language) => !string.IsNullOrWhiteSpace(language) && Engine.SupportsLanguage(language);

    /// <summary>
    /// Creates an incremental session, or null when the language has no grammar (plain text). The session must be
    /// used by one thread at a time.
    /// </summary>
    public static IHighlightSession? CreateSession(string? language)
        => SupportsLanguage(language) ? Engine.CreateSession(language!, SessionTheme) : null;

    /// <summary>
    /// The language family of a fence or <see cref="CodeBlock.Language"/> name, normalized the way StreamMark's
    /// alias table reads it ("{.xml}", "language-xml", "xml title=a" all mean xml).
    /// </summary>
    public static CodeBlockLanguageFamily FamilyOf(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return CodeBlockLanguageFamily.Code;
        }
        var span = language.AsSpan().Trim().TrimStart('{').TrimStart('.');
        var cut = span.IndexOfAny(" \t{},:;");
        if (cut >= 0)
        {
            span = span[..cut];
        }
        if (span.StartsWith("language-", StringComparison.OrdinalIgnoreCase))
        {
            span = span["language-".Length..];
        }
        if (span.IsEmpty || span.Length > 32)
        {
            return CodeBlockLanguageFamily.Code;
        }
        Span<char> lower = stackalloc char[span.Length];
        span.ToLowerInvariant(lower);
        return s_families.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(lower, out var family) ? family : CodeBlockLanguageFamily.Code;
    }

    /// <summary>The Arc role of a Light+ token color in <paramref name="family"/>; 0 (editor foreground) is plain.</summary>
    public static CodeBlockSyntaxKind RoleOf(uint foreground, CodeBlockLanguageFamily family)
    {
        if (foreground == 0)
        {
            return CodeBlockSyntaxKind.None;
        }
        switch (family)
        {
            case CodeBlockLanguageFamily.Markup when foreground == LpKeyword:
                return CodeBlockSyntaxKind.String; // attribute values
            case CodeBlockLanguageFamily.Json when foreground is LpType or LpValue:
                return CodeBlockSyntaxKind.Property; // keys
            case CodeBlockLanguageFamily.Yaml when foreground == LpTag:
                return CodeBlockSyntaxKind.Property; // keys
            case CodeBlockLanguageFamily.Yaml when foreground == LpKeyword:
                return CodeBlockSyntaxKind.String; // unquoted scalars
            case CodeBlockLanguageFamily.Css when foreground == LpType:
                return CodeBlockSyntaxKind.Property; // property names
        }
        return s_roles.GetValueOrDefault(foreground);
    }

    /// <summary>
    /// Appends the role spans of <paramref name="code"/> (ascending, non-overlapping, adjacent equal roles merged) from
    /// a session result: one token array per '\n'-separated line, offsets relative to the line start.
    /// </summary>
    public static void AppendSpans(string code, IReadOnlyList<ImmutableArray<HighlightToken>> lines, CodeBlockLanguageFamily family,
                                   List<CodeBlockSyntaxSpan> output)
    {
        var lineStart = 0;
        var inComment = false;
        for (var i = 0; i < lines.Count && lineStart <= code.Length; i++)
        {
            var newline = code.IndexOf('\n', lineStart);
            var lineEnd = newline < 0 ? code.Length : newline;
            AppendLine(code, lineStart, lineEnd, lines[i], family, ref inComment, output);
            if (newline < 0)
            {
                break;
            }
            lineStart = newline + 1;
        }
    }

    /// <summary>
    /// Appends the role spans of one line (absolute offsets, adjacent equal roles merged). Plain text is not added.
    /// <paramref name="inMarkupComment"/> carries an open <c>&lt;!--</c> from line to line.
    /// </summary>
    public static void AppendLine(string code, int lineStart, int lineEnd, ImmutableArray<HighlightToken> tokens, CodeBlockLanguageFamily family,
                                  ref bool inMarkupComment, List<CodeBlockSyntaxSpan> output)
    {
        var first = output.Count;
        var position = lineStart;
        if (!tokens.IsDefault)
        {
            foreach (var token in tokens)
            {
                var start = lineStart + token.Start;
                if (start >= lineEnd)
                {
                    break;
                }
                var end = Math.Min(lineEnd, start + token.Length);
                if (start > position)
                {
                    EmitRun(code, position, start, CodeBlockSyntaxKind.None, output);
                }
                if (end > start && start >= position)
                {
                    EmitRun(code, start, end, RoleOf(token.Foreground, family), output);
                    position = end;
                }
            }
        }
        if (lineEnd > position)
        {
            EmitRun(code, position, lineEnd, CodeBlockSyntaxKind.None, output);
        }
        if (family == CodeBlockLanguageFamily.Markup)
        {
            MarkMarkupComments(code, lineStart, lineEnd, ref inMarkupComment, output, first);
        }
    }

    /// <summary>
    /// TextMateSharp's XML grammar scopes only the delimiters of <c>&lt;!-- … --&gt;</c>; the whole comment becomes
    /// the comment role here (HTML is already right and stays unchanged).
    /// </summary>
    private static void MarkMarkupComments(string code, int lineStart, int lineEnd, ref bool inComment, List<CodeBlockSyntaxSpan> output, int first)
    {
        var position = lineStart;
        while (position < lineEnd)
        {
            int start;
            int searchFrom;
            if (inComment)
            {
                start = position;
                searchFrom = position;
            }
            else
            {
                start = code.IndexOf("<!--", position, lineEnd - position, StringComparison.Ordinal);
                if (start < 0)
                {
                    return;
                }
                inComment = true;
                searchFrom = start + 4;
            }
            var close = code.IndexOf("-->", searchFrom, lineEnd - searchFrom, StringComparison.Ordinal);
            var end = close < 0 ? lineEnd : close + 3;
            if (close >= 0)
            {
                inComment = false;
            }
            Override(output, first, start, end, CodeBlockSyntaxKind.Comment);
            position = end;
        }
    }

    /// <summary>Replaces whatever covers [<paramref name="start"/>, <paramref name="end"/>) in output[first..] with one span.</summary>
    private static void Override(List<CodeBlockSyntaxSpan> output, int first, int start, int end, CodeBlockSyntaxKind kind)
    {
        for (var i = first; i < output.Count; i++)
        {
            var span = output[i];
            var spanEnd = span.Start + span.Length;
            if (spanEnd <= start || span.Start >= end)
            {
                continue;
            }
            if (span.Start < start && spanEnd > end)
            {
                output[i] = span with { Length = start - span.Start };
                output.Insert(i + 1, new CodeBlockSyntaxSpan(end, spanEnd - end, span.Kind));
                break;
            }
            if (span.Start < start)
            {
                output[i] = span with { Length = start - span.Start };
            }
            else if (spanEnd > end)
            {
                output[i] = new CodeBlockSyntaxSpan(end, spanEnd - end, span.Kind);
            }
            else
            {
                output.RemoveAt(i);
                i--;
            }
        }
        var at = first;
        while (at < output.Count && output[at].Start < start)
        {
            at++;
        }
        output.Insert(at, new CodeBlockSyntaxSpan(start, end - start, kind));
    }

    private static void EmitRun(string code, int start, int end, CodeBlockSyntaxKind kind, List<CodeBlockSyntaxSpan> output)
    {
        if (kind is CodeBlockSyntaxKind.Comment or CodeBlockSyntaxKind.String or CodeBlockSyntaxKind.Number or CodeBlockSyntaxKind.Property)
        {
            Add(output, start, end - start, kind);
            return;
        }
        var runStart = start;
        for (var p = start; p < end; p++)
        {
            if (!IsPunctuation(code, p))
            {
                continue;
            }
            if (p > runStart && kind != CodeBlockSyntaxKind.None)
            {
                Add(output, runStart, p - runStart, kind);
            }
            Add(output, p, 1, CodeBlockSyntaxKind.Punctuation);
            runStart = p + 1;
        }
        if (end > runStart && kind != CodeBlockSyntaxKind.None)
        {
            Add(output, runStart, end - runStart, kind);
        }
    }

    /// <summary>Arc's punctuation characters; a hyphen inside a word (<c>ease-out</c>, <c>my-element</c>) is not one.</summary>
    private static bool IsPunctuation(string code, int index)
    {
        var c = code[index];
        if (PunctuationCharacters.IndexOf(c) < 0)
        {
            return false;
        }
        if (c == '-' && index > 0 && index + 1 < code.Length && char.IsLetterOrDigit(code[index - 1]) && char.IsLetterOrDigit(code[index + 1]))
        {
            return false;
        }
        return true;
    }

    private static void Add(List<CodeBlockSyntaxSpan> output, int start, int length, CodeBlockSyntaxKind kind)
    {
        if (output.Count > 0)
        {
            var last = output[^1];
            if (last.Kind == kind && last.Start + last.Length == start)
            {
                output[^1] = last with { Length = last.Length + length };
                return;
            }
        }
        output.Add(new CodeBlockSyntaxSpan(start, length, kind));
    }

    private static FrozenDictionary<string, CodeBlockLanguageFamily> BuildFamilies()
    {
        var families = new Dictionary<string, CodeBlockLanguageFamily>(StringComparer.Ordinal);
        foreach (var name in new[]
                 {
                     "xml", "svg", "xaml", "axaml", "xsd", "xsl", "xslt", "plist", "csproj", "fsproj", "vbproj", "props", "targets",
                     "msbuild", "nuspec", "resx", "rss", "atom", "wsdl", "manifest", "html", "htm", "xhtml", "shtml", "vue", "svelte"
                 })
        {
            families[name] = CodeBlockLanguageFamily.Markup;
        }
        foreach (var name in new[] { "json", "jsonc", "json5", "jsonl", "ndjson", "geojson" })
        {
            families[name] = CodeBlockLanguageFamily.Json;
        }
        foreach (var name in new[] { "yaml", "yml" })
        {
            families[name] = CodeBlockLanguageFamily.Yaml;
        }
        foreach (var name in new[] { "css", "scss", "less" })
        {
            families[name] = CodeBlockLanguageFamily.Css;
        }
        return families.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
