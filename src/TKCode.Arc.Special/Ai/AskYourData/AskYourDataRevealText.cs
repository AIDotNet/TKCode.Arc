using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using StreamMark.Core.Highlighting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Text that reveals itself in chunks: the SQL of <see cref="AskYourData"/> (syntax tinted through the library highlighter,
/// the public <see cref="MarkdownHighlightPlugin"/>) and its insight. The whole text is laid out first, so the block holds
/// its final size while it streams; only the revealed part is drawn, and each chunk fades in on its own over 140ms (the
/// shared <see cref="AiTextFades"/>: each wrapped line's glyphs at the chunk's opacity). Text appended later (SQL streamed
/// by the app's model) grows the layout.
/// <para>
/// Code-driven because each chunk is a character range of one text layout with its own opacity, which a ControlTheme
/// cannot reach. Reduced motion and Off: chunks appear whole.
/// </para>
/// </summary>
internal sealed class AskYourDataRevealText : SelectableTextBlock
{
    /// <summary>How long one chunk fades in.</summary>
    internal static readonly TimeSpan ChunkFade = TimeSpan.FromMilliseconds(140);

    private static ISyntaxHighlighter? s_highlighter;

    private readonly Dictionary<uint, IBrush> _brushes = new();
    private readonly AiTextFades _fades;
    private string _text = string.Empty;
    private string? _language;
    private bool _dark;
    private int _revealed;
    private IHighlightSession? _session;
    private string? _sessionKey;
    private IReadOnlyList<ImmutableArray<HighlightToken>>? _tokens;
    private string _tokensText = string.Empty;
    private bool _busy;
    private int _version;
    private TaskCompletionSource? _highlighted;

    static AskYourDataRevealText()
    {
        // Selectable, but the Tab stop is the scrolling well around the SQL (Arc: the focusable pre).
        KeyboardNavigation.IsTabStopProperty.OverrideDefaultValue<AskYourDataRevealText>(false);
    }

    public AskYourDataRevealText()
    {
        Inlines = new InlineCollection();
        _fades = new AiTextFades(this, InvalidateVisual) { Duration = ChunkFade };
    }

    protected override Type StyleKeyOverride => typeof(AskYourDataRevealText);

    /// <summary>The whole text laid out.</summary>
    public string FullText => _text;

    /// <summary>How many characters are drawn (the rest holds its place, unseen).</summary>
    public int RevealedLength => _revealed;

    /// <summary>Chunks still fading in.</summary>
    internal int FadingCount => _fades.Count;

    /// <summary>The highlighted tokens per line in use (tests).</summary>
    internal IReadOnlyList<ImmutableArray<HighlightToken>>? Tokens => _tokens;

    /// <summary>Completes when the text shown has been highlighted (or cannot be).</summary>
    internal Task WhenHighlighted => _highlighted?.Task ?? Task.CompletedTask;

    /// <summary>Lays out <paramref name="text"/> with <paramref name="revealed"/> characters already shown.</summary>
    public void SetText(string text, string? language, int revealed)
    {
        var changed = !string.Equals(text, _text, StringComparison.Ordinal) || !string.Equals(language, _language, StringComparison.Ordinal);
        _text = text;
        _language = language;
        _revealed = Math.Clamp(revealed, 0, text.Length);
        _fades.Clear();
        if (changed)
        {
            BuildRuns();
            RequestHighlight();
        }
        InvalidateVisual();
    }

    /// <summary>Appends text (streamed SQL); it is laid out and revealed with <see cref="RevealTo"/>.</summary>
    public void Append(string more)
    {
        if (more.Length == 0)
        {
            return;
        }
        _text += more;
        BuildRuns();
        RequestHighlight();
        InvalidateVisual();
    }

    /// <summary>Shows the text up to <paramref name="end"/>; with <paramref name="fade"/> the new chunk fades in over 140ms.</summary>
    public void RevealTo(int end, bool fade)
    {
        end = Math.Clamp(end, 0, _text.Length);
        if (end <= _revealed)
        {
            return;
        }
        if (fade)
        {
            _fades.Add(_revealed, end);
        }
        _revealed = end;
        InvalidateVisual();
    }

    /// <summary>Shows everything at once.</summary>
    public void RevealAll()
    {
        _revealed = _text.Length;
        _fades.Clear();
        InvalidateVisual();
    }

    /// <summary>Light or dark: the palette of the highlighting session.</summary>
    public void SetTheme(bool dark)
    {
        if (_dark == dark)
        {
            return;
        }
        _dark = dark;
        _tokens = null;
        BuildRuns();
        RequestHighlight();
    }

    private void RequestHighlight()
    {
        _version++;
        if (_highlighted is null || _highlighted.Task.IsCompleted)
        {
            _highlighted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        if (_text.Length == 0 || string.IsNullOrEmpty(_language))
        {
            _tokens = null;
            _highlighted.TrySetResult();
            return;
        }
        s_highlighter ??= new MarkdownHighlightPlugin().Highlighter;
        if (!s_highlighter.SupportsLanguage(_language))
        {
            _tokens = null;
            _highlighted.TrySetResult();
            return;
        }
        if (_busy)
        {
            return;
        }
        _ = HighlightAsync(s_highlighter);
    }

    private async Task HighlightAsync(ISyntaxHighlighter highlighter)
    {
        _busy = true;
        try
        {
            while (true)
            {
                var version = _version;
                var text = _text;
                var key = _language + (_dark ? "/dark" : "/light");
                if (_session is null || _sessionKey != key)
                {
                    _session?.Dispose();
                    _session = highlighter.CreateSession(_language!, _dark ? HighlightThemeKind.Dark : HighlightThemeKind.Light);
                    _sessionKey = key;
                }
                var session = _session;
                IReadOnlyList<ImmutableArray<HighlightToken>>? tokens;
                try
                {
                    tokens = await Task.Run(() => session.Update(text));
                }
                catch (Exception)
                {
                    // A grammar that cannot load leaves the SQL plain.
                    tokens = null;
                }
                if (version == _version)
                {
                    _tokens = tokens;
                    _tokensText = text;
                    BuildRuns();
                    _highlighted?.TrySetResult();
                    return;
                }
            }
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>One run per token of the lines that have tokens for this text; the rest in the text color.</summary>
    private void BuildRuns()
    {
        var inlines = Inlines!;
        inlines.Clear();
        if (_text.Length == 0)
        {
            return;
        }
        var tokens = _tokens;
        var lineStart = 0;
        var line = 0;
        while (lineStart <= _text.Length)
        {
            var newline = _text.IndexOf('\n', lineStart);
            var lineEnd = newline < 0 ? _text.Length : newline;
            var cursor = lineStart;
            if (tokens is not null && line < tokens.Count && SameLine(lineStart, lineEnd))
            {
                foreach (var token in tokens[line])
                {
                    var start = lineStart + token.Start;
                    var end = Math.Min(lineEnd, start + token.Length);
                    if (start < cursor || start >= end)
                    {
                        continue;
                    }
                    if (start > cursor)
                    {
                        inlines.Add(new Run(_text[cursor..start]));
                    }
                    inlines.Add(new Run(_text[start..end]) { Foreground = BrushFor(token.Foreground) });
                    cursor = end;
                }
            }
            if (lineEnd > cursor)
            {
                inlines.Add(new Run(_text[cursor..lineEnd]));
            }
            if (newline < 0)
            {
                break;
            }
            inlines.Add(new Run("\n"));
            lineStart = newline + 1;
            line++;
        }
    }

    /// <summary>Whether the tokens were made for this line as it is now.</summary>
    private bool SameLine(int start, int end) =>
        end <= _tokensText.Length && (end == _tokensText.Length || _tokensText[end] == '\n')
        && _text.AsSpan(start, end - start).SequenceEqual(_tokensText.AsSpan(start, end - start));

    private IBrush BrushFor(uint argb)
    {
        if (!_brushes.TryGetValue(argb, out var brush))
        {
            brush = new ImmutableSolidColorBrush(Color.FromUInt32(argb));
            _brushes[argb] = brush;
        }
        return brush;
    }

    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        var layout = TextLayout;
        var start = Math.Min(SelectionStart, SelectionEnd);
        var length = Math.Abs(SelectionEnd - SelectionStart);
        if (length > 0 && SelectionBrush is { } selection)
        {
            foreach (var rect in layout.HitTestTextRange(start, length))
            {
                context.FillRectangle(selection, rect.Translate(new Vector(origin.X, origin.Y)));
            }
        }
        if (_revealed >= _text.Length && _fades.Count == 0)
        {
            layout.Draw(context, origin);
            return;
        }
        // The settled part, then each chunk still fading at its own opacity; the rest is not drawn.
        _fades.Draw(context, layout, origin, _revealed);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (!_busy)
        {
            _session?.Dispose();
            _session = null;
            _sessionKey = null;
        }
    }
}
