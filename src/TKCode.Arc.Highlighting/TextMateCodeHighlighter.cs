using System.Collections.Frozen;
using System.Collections.Immutable;
using StreamMark.Core.Highlighting;
using StreamMark.Highlighting.TextMate;
using TextMateSharp.Grammars;

namespace TKCode.Arc;

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
    Css,

    /// <summary>Unified diffs and patches: whole lines take a role from their first character.</summary>
    Diff
}

/// <summary>
/// The TextMate highlighter <c>UseArcHighlighting()</c> installs for <see cref="CodeBlock"/>, the rich text editor's code
/// blocks and the Markdown package's fenced code, on StreamMark's TextMate sessions. Sources are always tokenized with
/// VS Code's Light+ theme (whatever the app's appearance) and each Light+ token color is read back as one of Arc's nine
/// roles, which are then painted with Arc's own palette. The role table was built from <c>light_plus.json</c> +
/// <c>light_vs.json</c> in TextMateSharp.Grammars 2.0.4; a few colors mean different roles per language family.
/// Operators and brackets keep the editor foreground in TextMate themes, so Arc's punctuation role is restored by
/// character class (Arc's own tokenizer rule) inside uncolored, keyword, type, function and tag runs.
/// </summary>
/// <remarks>
/// A trimmed app that sets <c>ArcHighlightLanguages</c> ships only those grammars. StreamMark still reports the others
/// as supported (their package.json stays), but their grammar fails to load and the session returns no tokens at all,
/// which <see cref="Session"/> shows as plain text.
/// </remarks>
internal sealed class TextMateCodeHighlighter : ICodeSyntaxHighlighter
{
    /// <summary>The instance <c>UseArcHighlighting()</c> installs.</summary>
    public static TextMateCodeHighlighter Shared { get; } = new();

    private TextMateCodeHighlighter()
    {
    }

    /// <inheritdoc />
    public bool SupportsLanguage(string language) => !string.IsNullOrWhiteSpace(language) && Engine.SupportsLanguage(language);

    /// <inheritdoc />
    public ICodeSyntaxSession CreateSession(string language)
        => new Session(Engine.CreateSession(language, SessionTheme), FamilyOf(language));

    /// <inheritdoc />
    public Task PrewarmAsync(IEnumerable<string> languages)
        // Sessions always use the Light+ context (the role table), so only that one is warmed.
        => Engine.PrewarmAsync(SessionTheme, languages);

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
#if NET9_0_OR_GREATER
        return s_families.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(lower, out var family) ? family : CodeBlockLanguageFamily.Code;
#else
        return s_families.TryGetValue(lower.ToString(), out var family) ? family : CodeBlockLanguageFamily.Code;
#endif
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
    /// Appends the role spans of one line (absolute offsets, adjacent equal roles merged). Plain text is not added.
    /// <paramref name="inMarkupComment"/> carries an open <c>&lt;!--</c> from line to line.
    /// </summary>
    public static void AppendLine(string code, int lineStart, int lineEnd, ImmutableArray<HighlightToken> tokens, CodeBlockLanguageFamily family,
                                  ref bool inMarkupComment, List<CodeBlockSyntaxSpan> output)
    {
        if (family == CodeBlockLanguageFamily.Diff)
        {
            AppendDiffLine(code, lineStart, lineEnd, output);
            return;
        }
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
    /// A diff line is one role: <c>+</c> added, <c>-</c> removed, the file and hunk headers (<c>+++</c>, <c>---</c>,
    /// <c>@@</c>, <c>diff</c>, <c>index</c>) in the punctuation role, context lines plain. TextMate's diff grammar colors
    /// these by Light+ colors that mean other roles (an added line read as a number), so its tokens are not used.
    /// </summary>
    private static void AppendDiffLine(string code, int lineStart, int lineEnd, List<CodeBlockSyntaxSpan> output)
    {
        if (lineEnd <= lineStart)
        {
            return;
        }
        var line = code.AsSpan(lineStart, lineEnd - lineStart);
        var kind = CodeBlockSyntaxKind.None;
        if (line.StartsWith("+++") || line.StartsWith("---") || line.StartsWith("@@") || line.StartsWith("diff ") ||
            line.StartsWith("index ") || line.StartsWith("\\ "))
        {
            kind = CodeBlockSyntaxKind.Punctuation;
        }
        else if (line[0] == '+')
        {
            kind = CodeBlockSyntaxKind.Inserted;
        }
        else if (line[0] == '-')
        {
            kind = CodeBlockSyntaxKind.Deleted;
        }
        if (kind != CodeBlockSyntaxKind.None)
        {
            Add(output, lineStart, lineEnd - lineStart, kind);
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
        foreach (var name in new[] { "diff", "patch" })
        {
            families[name] = CodeBlockLanguageFamily.Diff;
        }
        return families.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    /// One TextMate session mapped to Arc roles line by line. A line whose text, TextMate tokens and open markup
    /// comment are unchanged keeps its previous span array, so streaming and edits only map the lines that changed.
    /// </summary>
    private sealed class Session(IHighlightSession inner, CodeBlockLanguageFamily family) : ICodeSyntaxSession
    {
        private readonly List<CodeBlockSyntaxSpan> _scratch = new();
        private Line[] _lines = [];
        private string _code = string.Empty;

        public IReadOnlyList<ImmutableArray<CodeBlockSyntaxSpan>> Update(string code)
        {
            code ??= string.Empty;
            var source = inner.Update(code);
            if (family != CodeBlockLanguageFamily.Diff && !HasTokens(source) && code.Length > 0 && !string.IsNullOrWhiteSpace(code))
            {
                // A grammar gives every non-empty line at least one token, so none at all means it did not load (an app
                // that trims grammars with ArcHighlightLanguages): plain text, without the punctuation role either.
                _lines = [];
                _code = string.Empty;
                var plain = new ImmutableArray<CodeBlockSyntaxSpan>[source.Count];
                Array.Fill(plain, ImmutableArray<CodeBlockSyntaxSpan>.Empty);
                return plain;
            }
            var lines = new Line[source.Count];
            var result = new ImmutableArray<CodeBlockSyntaxSpan>[source.Count];
            var lineStart = 0;
            var inComment = false;
            for (var i = 0; i < source.Count; i++)
            {
                var newline = lineStart <= code.Length ? code.IndexOf('\n', lineStart) : -1;
                var lineEnd = newline < 0 ? code.Length : newline;
                var startsInComment = inComment;
                if (i < _lines.Length && _lines[i].Source == source[i] && _lines[i].StartsInComment == startsInComment
                    && SameText(code, lineStart, lineEnd, _lines[i]))
                {
                    lines[i] = _lines[i] with { Start = lineStart };
                    inComment = _lines[i].EndsInComment;
                }
                else
                {
                    _scratch.Clear();
                    AppendLine(code, lineStart, Math.Max(lineStart, lineEnd), source[i], family, ref inComment, _scratch);
                    lines[i] = new Line(source[i], ToLineSpans(lineStart), lineStart, Math.Max(0, lineEnd - lineStart), startsInComment, inComment);
                }
                result[i] = lines[i].Spans;
                lineStart = newline < 0 ? code.Length + 1 : newline + 1;
            }
            _lines = lines;
            _code = code;
            return result;
        }

        private static bool HasTokens(IReadOnlyList<ImmutableArray<HighlightToken>> source)
        {
            foreach (var line in source)
            {
                if (!line.IsDefaultOrEmpty)
                {
                    return true;
                }
            }
            return false;
        }

        private bool SameText(string code, int start, int end, Line previous)
            => end - start == previous.Length && previous.Start + previous.Length <= _code.Length
               && code.AsSpan(start, end - start).SequenceEqual(_code.AsSpan(previous.Start, previous.Length));

        private ImmutableArray<CodeBlockSyntaxSpan> ToLineSpans(int lineStart)
        {
            if (_scratch.Count == 0)
            {
                return ImmutableArray<CodeBlockSyntaxSpan>.Empty;
            }
            var builder = ImmutableArray.CreateBuilder<CodeBlockSyntaxSpan>(_scratch.Count);
            foreach (var span in _scratch)
            {
                builder.Add(span with { Start = span.Start - lineStart });
            }
            return builder.MoveToImmutable();
        }

        public void Dispose()
        {
            inner.Dispose();
            _lines = [];
            _code = string.Empty;
        }

        private readonly record struct Line(
            ImmutableArray<HighlightToken> Source,
            ImmutableArray<CodeBlockSyntaxSpan> Spans,
            int Start,
            int Length,
            bool StartsInComment,
            bool EndsInComment);
    }
}
