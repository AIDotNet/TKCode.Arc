using System.Collections.Immutable;
using Avalonia.Media;
using StreamMark.Core.Highlighting;

namespace TKCode.Arc;

/// <summary>
/// StreamMark highlighter for fenced code in <see cref="Markdown"/>: the same TextMate sessions and role table as
/// <see cref="CodeBlock"/> (<see cref="CodeBlockHighlighter"/>), with each role painted in Arc's palette for the
/// requested appearance instead of a TextMate theme's colors. Comments are italic, as in CodeBlock.
/// </summary>
/// <remarks>
/// StreamMark creates one session per code block and appearance and runs it on a worker; sessions are used by one
/// thread at a time. Unchanged lines keep their previous token arrays, so streaming only maps the lines that grew.
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
            : new Session(inner, CodeBlockHighlighter.FamilyOf(language), theme == HighlightThemeKind.Dark ? s_darkColors : s_lightColors);
    }

    public (uint Foreground, uint Background) GetThemeColors(HighlightThemeKind theme)
    {
        var neutrals = ArcPalette.Neutrals(theme == HighlightThemeKind.Dark);
        return (neutrals.Foreground.ToUInt32(), neutrals.Surface.ToUInt32());
    }

    private static uint[] BuildColors(bool isDarkMode)
    {
        var colors = new uint[10];
        for (var kind = CodeBlockSyntaxKind.Comment; kind <= CodeBlockSyntaxKind.Punctuation; kind++)
        {
            colors[(int)kind] = CodeBlockSyntaxColors.Get(kind, isDarkMode).ToUInt32();
        }
        return colors;
    }

    private sealed class Session(IHighlightSession inner, CodeBlockLanguageFamily family, uint[] colors) : IHighlightSession
    {
        private readonly List<CodeBlockSyntaxSpan> _scratch = new();
        private Line[] _lines = [];
        private string _code = string.Empty;

        public IReadOnlyList<ImmutableArray<HighlightToken>> Update(string code)
        {
            code ??= string.Empty;
            var source = inner.Update(code);
            var lines = new Line[source.Count];
            var result = new ImmutableArray<HighlightToken>[source.Count];
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
                    CodeBlockHighlighter.AppendLine(code, lineStart, Math.Max(lineStart, lineEnd), source[i], family, ref inComment, _scratch);
                    lines[i] = new Line(source[i], ToTokens(lineStart), lineStart, Math.Max(0, lineEnd - lineStart), startsInComment, inComment);
                }
                result[i] = lines[i].Mapped;
                lineStart = newline < 0 ? code.Length + 1 : newline + 1;
            }
            _lines = lines;
            _code = code;
            return result;
        }

        private bool SameText(string code, int start, int end, Line previous)
            => end - start == previous.Length && previous.Start + previous.Length <= _code.Length
               && code.AsSpan(start, end - start).SequenceEqual(_code.AsSpan(previous.Start, previous.Length));

        private ImmutableArray<HighlightToken> ToTokens(int lineStart)
        {
            if (_scratch.Count == 0)
            {
                return ImmutableArray<HighlightToken>.Empty;
            }
            var builder = ImmutableArray.CreateBuilder<HighlightToken>(_scratch.Count);
            foreach (var span in _scratch)
            {
                var style = span.Kind == CodeBlockSyntaxKind.Comment ? HighlightFontStyle.Italic : HighlightFontStyle.None;
                builder.Add(new HighlightToken(span.Start - lineStart, span.Length, colors[(int)span.Kind], style));
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
            ImmutableArray<HighlightToken> Mapped,
            int Start,
            int Length,
            bool StartsInComment,
            bool EndsInComment);
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
