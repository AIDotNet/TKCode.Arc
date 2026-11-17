using System.Collections.Immutable;
using Avalonia.Media;
using StreamMark.Core.Highlighting;

namespace TKCode.Arc;

/// <summary>
/// StreamMark highlighter for fenced code in <see cref="Markdown"/>: the sessions and role table of the installed code
/// highlighter (<see cref="CodeBlockHighlighter.Provider"/>, from <c>UseArcHighlighting()</c>), the same ones
/// <see cref="CodeBlock"/> uses, with each role painted in Arc's palette for the requested appearance instead of a
/// TextMate theme's colors. Comments are italic, as in CodeBlock. Without an installed highlighter no language is
/// supported and StreamMark shows the code plain.
/// </summary>
/// <remarks>
/// StreamMark creates one session per code block and appearance and runs it on a worker; sessions are used by one
/// thread at a time. Unchanged lines keep their previous span arrays, so streaming only maps the lines that grew.
/// </remarks>
internal sealed class MarkdownCodeHighlighter : ISyntaxHighlighter
{
    public static MarkdownCodeHighlighter Shared { get; } = new();

    private static readonly uint[] s_lightColors = BuildColors(isDarkMode: false);
    private static readonly uint[] s_darkColors = BuildColors(isDarkMode: true);

    private MarkdownCodeHighlighter()
    {
    }

    public bool SupportsLanguage(string? language) => CodeBlockHighlighter.SupportsLanguage(language);

    public IHighlightSession CreateSession(string language, HighlightThemeKind theme)
    {
        var inner = CodeBlockHighlighter.CreateSession(language);
        return inner is null
            ? EmptySession.Instance
            : new Session(inner, theme == HighlightThemeKind.Dark ? s_darkColors : s_lightColors);
    }

    public (uint Foreground, uint Background) GetThemeColors(HighlightThemeKind theme)
    {
        var neutrals = ArcPalette.Neutrals(theme == HighlightThemeKind.Dark);
        return (neutrals.Foreground.ToUInt32(), neutrals.Surface.ToUInt32());
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

    /// <summary>Plain text for languages without a grammar (StreamMark asks only after SupportsLanguage).</summary>
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
