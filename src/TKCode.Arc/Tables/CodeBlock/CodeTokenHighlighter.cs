using System.Collections.Immutable;

namespace TKCode.Arc.CodeTokens;

/// <summary>Font style of a <see cref="HighlightToken"/>.</summary>
[Flags]
internal enum HighlightFontStyle : byte
{
    None = 0,
    Italic = 1
}

/// <summary>Which palette a <see cref="IHighlightSession"/> colors with.</summary>
internal enum HighlightThemeKind : byte
{
    Light,
    Dark
}

/// <summary>A colored range of one line (offsets relative to the line start).</summary>
/// <param name="Start">The first character, relative to the line start.</param>
/// <param name="Length">The number of characters.</param>
/// <param name="Foreground">The color as ARGB (<see cref="Avalonia.Media.Color.ToUInt32"/>).</param>
/// <param name="Style">Italic for comments.</param>
internal readonly record struct HighlightToken(int Start, int Length, uint Foreground, HighlightFontStyle Style);

/// <summary>An incremental session that colors one source in one language and palette.</summary>
internal interface IHighlightSession : IDisposable
{
    /// <summary>
    /// Colors <paramref name="code"/>: one entry per '\n'-separated line. An unchanged line keeps the same array as in the
    /// previous result.
    /// </summary>
    IReadOnlyList<ImmutableArray<HighlightToken>> Update(string code);
}

/// <summary>Colors code in Arc's role palette (<see cref="CodeBlockSyntaxColors"/>).</summary>
internal interface ISyntaxHighlighter
{
    /// <summary>Whether <paramref name="language"/> has a grammar.</summary>
    bool SupportsLanguage(string? language);

    /// <summary>Creates a session; a language without a grammar gets one that leaves every line plain.</summary>
    IHighlightSession CreateSession(string language, HighlightThemeKind theme);
}

/// <summary>
/// Colored code for controls that draw code themselves, without the Markdown package: the roles of the installed
/// <see cref="CodeBlockHighlighter.Provider"/> (<c>UseArcHighlighting()</c>, TKCode.Arc.Highlighting) in the colors of
/// <see cref="CodeBlock"/>. Without a provider every line stays plain. The shapes mirror StreamMark's highlighting
/// contract, so code written against <c>MarkdownHighlightPlugin().Highlighter</c> moves over by changing its using.
/// </summary>
internal static class CodeTokenHighlighter
{
    /// <summary>The highlighter (thread safe; each session is used by one thread at a time).</summary>
    public static ISyntaxHighlighter Shared { get; } = new Highlighter();

    private sealed class Highlighter : ISyntaxHighlighter
    {
        private static readonly uint[] s_lightColors = BuildColors(isDarkMode: false);
        private static readonly uint[] s_darkColors = BuildColors(isDarkMode: true);

        public bool SupportsLanguage(string? language) => CodeBlockHighlighter.SupportsLanguage(language);

        public IHighlightSession CreateSession(string language, HighlightThemeKind theme)
        {
            var inner = CodeBlockHighlighter.CreateSession(language);
            return inner is null
                ? EmptySession.Instance
                : new Session(inner, theme == HighlightThemeKind.Dark ? s_darkColors : s_lightColors);
        }

        private static uint[] BuildColors(bool isDarkMode)
        {
            var colors = new uint[CodeBlockHighlighter.KindCount];
            for (var kind = CodeBlockSyntaxKind.Comment; kind <= CodeBlockSyntaxKind.Deleted; kind++)
            {
                colors[(int)kind] = CodeBlockSyntaxColors.Get(kind, isDarkMode).ToUInt32();
            }
            return colors;
        }
    }

    private sealed class Session(ICodeSyntaxSession inner, uint[] colors) : IHighlightSession
    {
        private ImmutableArray<CodeBlockSyntaxSpan>[] _spans = [];
        private ImmutableArray<HighlightToken>[] _tokens = [];

        public IReadOnlyList<ImmutableArray<HighlightToken>> Update(string code)
        {
            var source = inner.Update(code ?? string.Empty);
            var spans = new ImmutableArray<CodeBlockSyntaxSpan>[source.Count];
            var tokens = new ImmutableArray<HighlightToken>[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                spans[i] = source[i];
                // The highlighter hands back the same array for an unchanged line; its tokens are reused too.
                tokens[i] = i < _spans.Length && _spans[i] == source[i] ? _tokens[i] : ToTokens(source[i]);
            }
            _spans = spans;
            _tokens = tokens;
            return tokens;
        }

        private ImmutableArray<HighlightToken> ToTokens(ImmutableArray<CodeBlockSyntaxSpan> line)
        {
            if (line.IsDefaultOrEmpty)
            {
                return ImmutableArray<HighlightToken>.Empty;
            }
            var builder = ImmutableArray.CreateBuilder<HighlightToken>(line.Length);
            foreach (var span in line)
            {
                var style = span.Kind == CodeBlockSyntaxKind.Comment ? HighlightFontStyle.Italic : HighlightFontStyle.None;
                builder.Add(new HighlightToken(span.Start, span.Length, colors[(int)span.Kind], style));
            }
            return builder.MoveToImmutable();
        }

        public void Dispose()
        {
            inner.Dispose();
            _spans = [];
            _tokens = [];
        }
    }

    private sealed class EmptySession : IHighlightSession
    {
        public static EmptySession Instance { get; } = new();

        public IReadOnlyList<ImmutableArray<HighlightToken>> Update(string code)
        {
            var count = 1;
            foreach (var ch in (code ?? string.Empty).AsSpan())
            {
                if (ch == '\n')
                {
                    count++;
                }
            }
            var lines = new ImmutableArray<HighlightToken>[count];
            Array.Fill(lines, ImmutableArray<HighlightToken>.Empty);
            return lines;
        }

        public void Dispose()
        {
        }
    }
}
