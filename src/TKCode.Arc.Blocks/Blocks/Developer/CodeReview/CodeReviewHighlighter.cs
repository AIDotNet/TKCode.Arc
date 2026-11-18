using System.Collections.Immutable;
using TKCode.Arc.CodeTokens;

namespace TKCode.Arc;

/// <summary>
/// Syntax colors for the lines of one diff or suggestion, from the library's highlighting contract
/// (<see cref="CodeTokenHighlighter"/>: the TextMate grammars that <c>UseArcHighlighting()</c> installs, in
/// <see cref="CodeBlock"/>'s role palette; without that package every line stays plain). The lines are highlighted as one
/// text on a worker, one update at a time; a newer text waits for the running one. <see cref="TokensFor"/> hands out a
/// line's tokens only while its text is the one they were made for.
/// </summary>
internal sealed class CodeReviewHighlighter : IDisposable
{
    private static readonly ISyntaxHighlighter s_highlighter = CodeTokenHighlighter.Shared;

    private readonly Action _changed;
    private string[] _lines = [];
    private string _language = string.Empty;
    private bool _dark;
    private IHighlightSession? _session;
    private string? _sessionKey;
    private IReadOnlyList<ImmutableArray<HighlightToken>>? _tokens;
    private string[] _tokenLines = [];
    private bool _busy;
    private bool _disposed;
    private int _version;
    private TaskCompletionSource? _highlighted;

    /// <param name="changed">Runs on the UI thread when new tokens are in.</param>
    public CodeReviewHighlighter(Action changed)
    {
        _changed = changed;
    }

    /// <summary>Completes when the current lines have their colors (or cannot have any).</summary>
    public Task WhenHighlighted => _highlighted?.Task ?? Task.CompletedTask;

    /// <summary>Whether the language has a grammar.</summary>
    public static bool Supports(string language) => language.Length > 0 && s_highlighter.SupportsLanguage(language);

    /// <summary>Highlights <paramref name="lines"/> as <paramref name="language"/> in the light or dark palette.</summary>
    public void Set(IReadOnlyList<string> lines, string language, bool dark)
    {
        if (_disposed)
        {
            return;
        }
        if (language == _language && dark == _dark && lines.SequenceEqual(_lines, StringComparer.Ordinal) && _highlighted is not null)
        {
            return;
        }
        if (language != _language || dark != _dark)
        {
            _tokens = null;
            _tokenLines = [];
        }
        _lines = lines.ToArray();
        _language = language;
        _dark = dark;
        _version++;
        if (_highlighted is null || _highlighted.Task.IsCompleted)
        {
            _highlighted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        if (_lines.Length == 0 || !Supports(language))
        {
            _tokens = null;
            _tokenLines = [];
            _highlighted.TrySetResult();
            return;
        }
        if (!_busy)
        {
            _ = HighlightAsync();
        }
    }

    /// <summary>The tokens of line <paramref name="index"/> when they were made for <paramref name="text"/>, else null.</summary>
    public ImmutableArray<HighlightToken>? TokensFor(int index, string text)
    {
        var tokens = _tokens;
        if (tokens is null || index < 0 || index >= tokens.Count || index >= _tokenLines.Length
            || !string.Equals(_tokenLines[index], text, StringComparison.Ordinal))
        {
            return null;
        }
        return tokens[index];
    }

    private async Task HighlightAsync()
    {
        _busy = true;
        try
        {
            while (!_disposed)
            {
                var version = _version;
                var lines = _lines;
                var code = string.Join('\n', lines);
                var key = _language + (_dark ? "/dark" : "/light");
                if (_session is null || _sessionKey != key)
                {
                    _session?.Dispose();
                    _session = s_highlighter.CreateSession(_language, _dark ? HighlightThemeKind.Dark : HighlightThemeKind.Light);
                    _sessionKey = key;
                }
                var session = _session;
                IReadOnlyList<ImmutableArray<HighlightToken>>? tokens;
                try
                {
                    tokens = await Task.Run(() => session.Update(code));
                }
                catch (Exception)
                {
                    // A grammar that cannot load leaves the code plain.
                    tokens = null;
                }
                if (version == _version && !_disposed)
                {
                    _tokens = tokens;
                    _tokenLines = lines;
                    _changed();
                    _highlighted?.TrySetResult();
                    return;
                }
            }
        }
        finally
        {
            _busy = false;
            if (_disposed)
            {
                _session?.Dispose();
                _session = null;
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _highlighted?.TrySetResult();
        if (!_busy)
        {
            _session?.Dispose();
            _session = null;
        }
    }
}
