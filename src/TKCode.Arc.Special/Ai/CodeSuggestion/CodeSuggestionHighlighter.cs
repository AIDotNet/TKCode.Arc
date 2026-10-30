using System.Collections.Immutable;
using StreamMark.Core.Highlighting;

namespace TKCode.Arc;

/// <summary>
/// Syntax colors for one text of a <see cref="CodeSuggestion"/> (the original file, or the file with every hunk applied),
/// from the library's highlighter: <see cref="MarkdownHighlightPlugin"/>, the TextMate sessions and Arc role palette of
/// <see cref="CodeBlock"/>. One session per text and appearance runs on a worker, one update at a time (a newer text waits
/// for the running one, then the latest is highlighted). Sessions work line by line and keep unchanged lines, so a
/// streamed line never re-highlights the others; <see cref="TokensFor"/> hands out a line's tokens only while its text is
/// the one they were made for.
/// </summary>
internal sealed class CodeSuggestionHighlighter
{
    private static readonly ISyntaxHighlighter s_highlighter = new MarkdownHighlightPlugin().Highlighter;

    private readonly Action _changed;
    private string _code = string.Empty;
    private string _language = string.Empty;
    private bool _dark;
    private IHighlightSession? _session;
    private string? _sessionKey;
    private IReadOnlyList<ImmutableArray<HighlightToken>>? _tokens;
    private string[] _tokenLines = [];
    private bool _busy;
    private bool _releaseWhenIdle;
    private int _version;
    private TaskCompletionSource? _highlighted;

    /// <param name="changed">Runs on the UI thread when new tokens are in.</param>
    public CodeSuggestionHighlighter(Action changed)
    {
        _changed = changed;
    }

    /// <summary>Completes when the current text has its colors (or cannot have any).</summary>
    public Task WhenHighlighted => _highlighted?.Task ?? Task.CompletedTask;

    /// <summary>Whether a session is held (tests).</summary>
    public bool HasSession => _session is not null;

    /// <summary>Whether the language has a grammar.</summary>
    public static bool Supports(string language) => language.Length > 0 && s_highlighter.SupportsLanguage(language);

    /// <summary>Highlights <paramref name="code"/> as <paramref name="language"/> in the light or dark palette.</summary>
    public void Set(string code, string language, bool dark)
    {
        if (string.Equals(code, _code, StringComparison.Ordinal) && language == _language && dark == _dark && _highlighted is not null)
        {
            return;
        }
        if (language != _language || dark != _dark)
        {
            _tokens = null;
            _tokenLines = [];
        }
        _code = code;
        _language = language;
        _dark = dark;
        _version++;
        if (_highlighted is null || _highlighted.Task.IsCompleted)
        {
            _highlighted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        if (code.Length == 0 || !Supports(language))
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

    /// <summary>The tokens of line <paramref name="line"/> when they were made for <paramref name="text"/>, else null.</summary>
    public ImmutableArray<HighlightToken>? TokensFor(int line, string text)
    {
        var tokens = _tokens;
        if (tokens is null || line < 0 || line >= tokens.Count || line >= _tokenLines.Length
            || !string.Equals(_tokenLines[line], text, StringComparison.Ordinal))
        {
            return null;
        }
        return tokens[line];
    }

    private async Task HighlightAsync()
    {
        _busy = true;
        try
        {
            while (true)
            {
                var version = _version;
                var code = _code;
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
                if (version == _version)
                {
                    _tokens = tokens;
                    _tokenLines = CodeSuggestionDiff.SplitLines(code);
                    _changed();
                    _highlighted?.TrySetResult();
                    return;
                }
            }
        }
        finally
        {
            _busy = false;
            if (_releaseWhenIdle)
            {
                _releaseWhenIdle = false;
                Release();
            }
        }
    }

    /// <summary>Lets the session go (the view left the window); the next <see cref="Set"/> makes a new one.</summary>
    public void Release()
    {
        if (_busy)
        {
            _releaseWhenIdle = true;
            return;
        }
        _session?.Dispose();
        _session = null;
        _sessionKey = null;
        _highlighted = null;
    }

    /// <summary>Called when the view is back in a window: highlight again if the session was released.</summary>
    public void Resume()
    {
        _releaseWhenIdle = false;
        if (_session is null && _highlighted is null)
        {
            // Released: Set does not skip the same text without a pending highlight.
            Set(_code, _language, _dark);
        }
    }
}
