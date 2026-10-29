using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using StreamMark.Core.Highlighting;

namespace TKCode.Arc;

/// <summary>Brushes for the syntax roles, resolved by <see cref="CodeBlock"/> from its token.</summary>
internal sealed class CodeBlockSyntaxPalette
{
    private readonly IBrush?[] _brushes = new IBrush?[10];

    public IBrush? this[CodeBlockSyntaxKind kind]
    {
        get => _brushes[(int)kind];
        set => _brushes[(int)kind] = value;
    }
}

/// <summary>
/// The selectable source text. One <see cref="TextLayout"/> per change, fed by a text source that hands out the
/// highlighted spans as runs (binary search, no per-token controls or inlines). Selection and Ctrl+C come from
/// <see cref="SelectableTextBlock"/>; the selection keeps the syntax colors (no selection foreground).
/// </summary>
/// <remarks>
/// Highlighting comes from one incremental StreamMark TextMate session per block and language
/// (<see cref="CodeBlockHighlighter"/>). It runs once per source change: on the UI thread when the language's grammar
/// is already warm, otherwise on a worker (the text shows plain until the colors arrive). A session is only ever used
/// by one thread at a time; changes that arrive meanwhile are coalesced into one more update.
/// </remarks>
internal sealed class CodeBlockText : SelectableTextBlock
{
    /// <summary>Sources up to this length highlight synchronously once their grammar is warm.</summary>
    private const int SyncHighlightLimit = 32 * 1024;

    /// <summary>Normalized languages whose grammar has finished loading and compiling at least once.</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> s_warmLanguages = new(StringComparer.Ordinal);

    private readonly List<CodeBlockSyntaxSpan> _spans = new();
    private readonly TextRunProperties?[] _kindProperties = new TextRunProperties?[10];
    private IHighlightSession? _session;
    private string _sessionLanguage = string.Empty;
    private CodeBlockLanguageFamily _family;
    private string _source = string.Empty;
    private bool _highlightRunning;
    private TaskCompletionSource? _highlighted;

    public CodeBlockSyntaxPalette? Palette { get; set; }

    protected override Type StyleKeyOverride => typeof(CodeBlockText);

    /// <summary>Number of highlighted spans (diagnostics and tests).</summary>
    public int SpanCount => _spans.Count;

    internal IReadOnlyList<CodeBlockSyntaxSpan> Spans => _spans;

    /// <summary>Completes when the spans of the current source are applied (tests).</summary>
    internal Task WhenHighlighted => _highlighted?.Task ?? Task.CompletedTask;

    /// <summary>Shows <paramref name="text"/> and highlights it as <paramref name="language"/>.</summary>
    public void SetSource(string text, string language)
    {
        _source = text;
        if (!string.Equals(language, _sessionLanguage, StringComparison.Ordinal))
        {
            _sessionLanguage = language;
            _family = CodeBlockHighlighter.FamilyOf(language);
            // A running update still owns the old session; it is dropped when that update returns.
            if (!_highlightRunning)
            {
                _session?.Dispose();
            }
            _session = CodeBlockHighlighter.CreateSession(language);
        }
        _spans.Clear();
        if (_session is { } session && text.Length > 0)
        {
            if (!_highlightRunning && text.Length <= SyncHighlightLimit && s_warmLanguages.ContainsKey(language))
            {
                try
                {
                    ApplySpans(text, session.Update(text));
                }
                catch (Exception)
                {
                    _spans.Clear();
                }
                CompleteHighlight();
            }
            else
            {
                _highlighted ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                RunHighlight();
            }
        }
        else
        {
            CompleteHighlight();
        }
        if (Text == text)
        {
            InvalidateTextLayout();
        }
        else
        {
            Text = text;
        }
    }

    private void RunHighlight()
    {
        if (_highlightRunning || _session is not { } session)
        {
            return;
        }
        _highlightRunning = true;
        var code = _source;
        var language = _sessionLanguage;
        var dispatcher = Dispatcher;
        _ = Task.Run(() =>
        {
            IReadOnlyList<System.Collections.Immutable.ImmutableArray<HighlightToken>>? lines;
            try
            {
                lines = session.Update(code);
                s_warmLanguages.TryAdd(language, 0);
            }
            catch (Exception)
            {
                lines = null;
            }
            dispatcher.Post(() => OnHighlighted(session, code, lines));
        });
    }

    private void OnHighlighted(IHighlightSession session, string code, IReadOnlyList<System.Collections.Immutable.ImmutableArray<HighlightToken>>? lines)
    {
        _highlightRunning = false;
        if (!ReferenceEquals(session, _session))
        {
            session.Dispose();
        }
        if (ReferenceEquals(session, _session) && string.Equals(code, _source, StringComparison.Ordinal))
        {
            _spans.Clear();
            if (lines is not null)
            {
                ApplySpans(code, lines);
            }
            InvalidateTextLayout();
            CompleteHighlight();
        }
        else if (_session is not null && _source.Length > 0)
        {
            // The source or language changed while the worker ran: highlight the latest one.
            RunHighlight();
        }
        else
        {
            CompleteHighlight();
        }
    }

    private void ApplySpans(string code, IReadOnlyList<System.Collections.Immutable.ImmutableArray<HighlightToken>> lines)
        => CodeBlockHighlighter.AppendSpans(code, lines, _family, _spans);

    private void CompleteHighlight()
    {
        var pending = _highlighted;
        _highlighted = null;
        pending?.TrySetResult();
    }

    /// <summary>Call after the palette brushes change.</summary>
    public void RefreshPalette() => InvalidateTextLayout();

    protected override TextLayout CreateTextLayout(string? text)
    {
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var defaults = new GenericTextRunProperties(typeface, FontSize, null, Foreground, null, BaselineAlignment.Baseline, null, FontFeatures);
        var italic = new Typeface(FontFamily, FontStyle.Italic, FontWeight, FontStretch);
        for (var i = 1; i < _kindProperties.Length; i++)
        {
            var kind = (CodeBlockSyntaxKind)i;
            var brush = Palette?[kind] ?? Foreground;
            _kindProperties[i] = new GenericTextRunProperties(kind == CodeBlockSyntaxKind.Comment ? italic : typeface, FontSize,
                null, brush, null, BaselineAlignment.Baseline, null, FontFeatures);
        }
        var paragraph = new GenericTextParagraphProperties(FlowDirection, TextAlignment.Left, true, false, defaults,
            TextWrapping.NoWrap, LineHeight, 0, LetterSpacing);
        var source = new CodeBlockTextSource(text ?? string.Empty, defaults, _spans, _kindProperties);
        return new TextLayout(source, paragraph, null, double.PositiveInfinity, double.PositiveInfinity, 0);
    }

    private readonly struct CodeBlockTextSource : ITextSource
    {
        private readonly string _text;
        private readonly TextRunProperties _defaults;
        private readonly List<CodeBlockSyntaxSpan> _spans;
        private readonly TextRunProperties?[] _kinds;

        public CodeBlockTextSource(string text, TextRunProperties defaults, List<CodeBlockSyntaxSpan> spans, TextRunProperties?[] kinds)
        {
            _text = text;
            _defaults = defaults;
            _spans = spans;
            _kinds = kinds;
        }

        public TextRun? GetTextRun(int textSourceIndex)
        {
            if (textSourceIndex >= _text.Length)
            {
                return new TextEndOfParagraph();
            }
            // First span that ends after the index.
            var lo = 0;
            var hi = _spans.Count - 1;
            var found = _spans.Count;
            while (lo <= hi)
            {
                var mid = (lo + hi) >> 1;
                var span = _spans[mid];
                if (span.Start + span.Length > textSourceIndex)
                {
                    found = mid;
                    hi = mid - 1;
                }
                else
                {
                    lo = mid + 1;
                }
            }
            if (found < _spans.Count)
            {
                var span = _spans[found];
                if (span.Start <= textSourceIndex)
                {
                    var end = span.Start + span.Length;
                    return new TextCharacters(_text.AsMemory(textSourceIndex, end - textSourceIndex), _kinds[(int)span.Kind] ?? _defaults);
                }
                return new TextCharacters(_text.AsMemory(textSourceIndex, span.Start - textSourceIndex), _defaults);
            }
            return new TextCharacters(_text.AsMemory(textSourceIndex), _defaults);
        }
    }
}
