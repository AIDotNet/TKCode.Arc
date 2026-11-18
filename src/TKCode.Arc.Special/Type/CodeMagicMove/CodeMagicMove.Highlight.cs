using System.Collections.Immutable;
using Avalonia.Threading;
using TKCode.Arc.CodeTokens;

namespace TKCode.Arc;

// Syntax colors: the library's highlighting contract (CodeTokenHighlighter, the TextMate sessions and Arc role palette of
// CodeBlock) colors each version on a worker; a word takes the color of the role under its first character. Without
// UseArcHighlighting() every word keeps the plain text color.
public partial class CodeMagicMove
{
    private bool _isDark = true;
    private int _highlightVersion;

    /// <summary>Completes when the current versions have their colors (tests).</summary>
    internal Task WhenHighlighted { get; private set; } = Task.CompletedTask;

    private void StartHighlighting()
    {
        var version = ++_highlightVersion;
        EnsureSteps();
        var sources = _sources;
        var language = Language;
        var highlighter = CodeTokenHighlighter.Shared;
        if (VisualRoot is null || sources.Length == 0 || string.IsNullOrWhiteSpace(language) || !highlighter.SupportsLanguage(language))
        {
            foreach (var source in sources)
            {
                source.Colors = null;
            }
            _surface?.InvalidateAllWords();
            WhenHighlighted = Task.CompletedTask;
            return;
        }
        var theme = _isDark ? HighlightThemeKind.Dark : HighlightThemeKind.Light;
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        WhenHighlighted = done.Task;
        _ = Task.Run(() =>
        {
            uint[][]? colors = new uint[sources.Length][];
            try
            {
                using var session = highlighter.CreateSession(language, theme);
                for (var i = 0; i < sources.Length; i++)
                {
                    colors![i] = ColorsOf(sources[i], session.Update(sources[i].Text));
                }
            }
            catch (Exception)
            {
                // A grammar that cannot load leaves the code plain.
                colors = null;
            }
            Dispatcher.UIThread.Post(() =>
            {
                if (version == _highlightVersion && ReferenceEquals(sources, _sources))
                {
                    for (var i = 0; i < sources.Length; i++)
                    {
                        sources[i].Colors = colors?[i];
                    }
                    _surface?.InvalidateAllWords();
                }
                done.TrySetResult();
            });
        });
    }

    private void ReleaseHighlighting() => _highlightVersion++;

    /// <summary>Each word's color: the role span that covers its first character, or 0 for plain text.</summary>
    internal static uint[] ColorsOf(CodeMagicMoveSource source, IReadOnlyList<ImmutableArray<HighlightToken>> lines)
    {
        var colors = new uint[source.Words.Length];
        for (var i = 0; i < source.Words.Length; i++)
        {
            var word = source.Words[i];
            if (word.Line >= lines.Count)
            {
                continue;
            }
            var tokens = lines[word.Line];
            if (tokens.IsDefaultOrEmpty)
            {
                continue;
            }
            foreach (var token in tokens)
            {
                if (word.Column >= token.Start && word.Column < token.Start + token.Length)
                {
                    colors[i] = token.Foreground;
                    break;
                }
            }
        }
        return colors;
    }
}
