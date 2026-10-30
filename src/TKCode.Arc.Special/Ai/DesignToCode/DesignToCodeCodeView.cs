using System.Collections.Immutable;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;
using Avalonia.VisualTree;
using StreamMark.Core.Highlighting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The generated code on the output side: line numbers in a gutter, the code in the monospace font, highlighted with the
/// library's code palette through the public <see cref="MarkdownHighlightPlugin"/> highlighter (on a worker, one TextMate
/// session per view). Long lines wrap with a hanging indent (continuation lines start a little right of the line's own
/// indentation), so nothing scrolls sideways. Each line knows the region its first character belongs to: the lines of
/// the region being written carry a quiet tint and the lines of a highlighted region a stronger one. Each streamed chunk
/// fades in over 140ms on its own (the shared <see cref="AiTextFades"/>, which fades the glyphs of each wrapped line in
/// place); earlier text is never drawn again with a fade.
/// </summary>
/// <remarks>
/// Code-driven motion: a chunk is a character range inside a text layout, which has no property a ControlTheme could
/// animate, and every chunk starts at 0 when it arrives.
/// </remarks>
internal sealed class DesignToCodeCodeView : Control
{
    /// <summary>How long a streamed chunk takes to fade in.</summary>
    public static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(140);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        Avalonia.Controls.Documents.TextElement.FontFamilyProperty.AddOwner<DesignToCodeCodeView>();

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<DesignToCodeCodeView, double>(nameof(FontSize), 12.5);

    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<DesignToCodeCodeView, double>(nameof(LineHeight), 20);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<DesignToCodeCodeView, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<IBrush?> GutterBrushProperty =
        AvaloniaProperty.Register<DesignToCodeCodeView, IBrush?>(nameof(GutterBrush));

    public static readonly StyledProperty<IBrush?> TintBrushProperty =
        AvaloniaProperty.Register<DesignToCodeCodeView, IBrush?>(nameof(TintBrush));

    public static readonly StyledProperty<double> WritingTintOpacityProperty =
        AvaloniaProperty.Register<DesignToCodeCodeView, double>(nameof(WritingTintOpacity), 0.45);

    public static readonly StyledProperty<double> HighlightTintOpacityProperty =
        AvaloniaProperty.Register<DesignToCodeCodeView, double>(nameof(HighlightTintOpacity), 0.9);

    public static readonly StyledProperty<IBrush?> PageBrushProperty =
        AvaloniaProperty.Register<DesignToCodeCodeView, IBrush?>(nameof(PageBrush));

    public static readonly StyledProperty<Thickness> PaddingProperty =
        Decorator.PaddingProperty.AddOwner<DesignToCodeCodeView>();

    public static readonly StyledProperty<double> GutterGapProperty =
        AvaloniaProperty.Register<DesignToCodeCodeView, double>(nameof(GutterGap), 14);

    public static readonly StyledProperty<double> HangingIndentProperty =
        AvaloniaProperty.Register<DesignToCodeCodeView, double>(nameof(HangingIndent), 2);

    private static readonly ISyntaxHighlighter s_highlighter = new MarkdownHighlightPlugin().Highlighter;

    private readonly List<CodeLine> _lines = [];
    private readonly AiTextFades _fades;
    private readonly Dictionary<uint, IBrush> _brushes = new();
    private readonly Dictionary<int, TextLayout> _numbers = new();
    private string _code = string.Empty;
    private string _language = "tsx";
    private Func<int, string?> _regionAt = _ => null;
    private string? _highlighted;
    private string? _writing;
    private double _charWidth = double.NaN;
    private IBrush? _writingTint;
    private IBrush? _highlightTint;
    private double _gutterWidth;
    private bool _dark;
    private IHighlightSession? _session;
    private string? _sessionKey;
    private IReadOnlyList<ImmutableArray<HighlightToken>>? _tokens;
    private string _tokensCode = string.Empty;
    private bool _busy;
    private bool _releaseWhenIdle;
    private int _version;
    private TaskCompletionSource? _highlightedTask;
    private string? _hoverRegion;

    static DesignToCodeCodeView()
    {
        AffectsMeasure<DesignToCodeCodeView>(FontFamilyProperty, FontSizeProperty, LineHeightProperty, PaddingProperty,
            GutterGapProperty, HangingIndentProperty);
        AffectsRender<DesignToCodeCodeView>(TintBrushProperty, GutterBrushProperty, WritingTintOpacityProperty, HighlightTintOpacityProperty);
    }

    public DesignToCodeCodeView()
    {
        _fades = new AiTextFades(this, InvalidateVisual) { Duration = FadeDuration };
    }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public IBrush? GutterBrush { get => GetValue(GutterBrushProperty); set => SetValue(GutterBrushProperty, value); }

    public IBrush? TintBrush { get => GetValue(TintBrushProperty); set => SetValue(TintBrushProperty, value); }

    public double WritingTintOpacity { get => GetValue(WritingTintOpacityProperty); set => SetValue(WritingTintOpacityProperty, value); }

    public double HighlightTintOpacity { get => GetValue(HighlightTintOpacityProperty); set => SetValue(HighlightTintOpacityProperty, value); }

    public IBrush? PageBrush { get => GetValue(PageBrushProperty); set => SetValue(PageBrushProperty, value); }

    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    public double GutterGap { get => GetValue(GutterGapProperty); set => SetValue(GutterGapProperty, value); }

    public double HangingIndent { get => GetValue(HangingIndentProperty); set => SetValue(HangingIndentProperty, value); }

    /// <summary>The code shown.</summary>
    public string Code => _code;

    /// <summary>The number of lines shown.</summary>
    public int LineCount => _lines.Count;

    /// <summary>The region whose lines carry the strong tint (pointed at or focused).</summary>
    public string? HighlightedRegion
    {
        get => _highlighted;
        set
        {
            if (_highlighted != value)
            {
                _highlighted = value;
                InvalidateVisual();
            }
        }
    }

    /// <summary>The region being written: its lines carry the quiet tint.</summary>
    public string? WritingRegion
    {
        get => _writing;
        set
        {
            if (_writing != value)
            {
                _writing = value;
                InvalidateVisual();
            }
        }
    }

    /// <summary>The highlighting language (a TextMate grammar name or alias: tsx, jsx, html, ...).</summary>
    public string Language
    {
        get => _language;
        set
        {
            if (_language == value)
            {
                return;
            }
            _language = value;
            _tokens = null;
            InvalidateLines(0);
            RequestHighlight();
        }
    }

    /// <summary>Raised when the pointer moves onto the lines of another region (null: no region).</summary>
    public event Action<string?>? HoverRegionChanged;

    /// <summary>Completes when the code shown has been highlighted (or cannot be); tests.</summary>
    internal Task WhenHighlighted => _highlightedTask?.Task ?? Task.CompletedTask;

    /// <summary>The fading chunks with their progress (tests).</summary>
    internal IEnumerable<(int Start, int End, double Progress)> Fades => _fades.Snapshot;

    /// <summary>The region of each line (tests).</summary>
    internal IEnumerable<string?> LineRegions => _lines.Select(line => line.Region);

    /// <summary>Whether line <paramref name="index"/> has highlighting tokens applied (tests).</summary>
    internal bool HasTokens(int index) => index < _lines.Count && !_lines[index].Applied.IsDefaultOrEmpty;

    /// <summary>
    /// Shows <paramref name="code"/>. What was added after the part both texts share fades in with <paramref name="fade"/>;
    /// <paramref name="regionAt"/> tells the region of a character offset.
    /// </summary>
    public void SetCode(string code, Func<int, string?> regionAt, bool fade)
    {
        _regionAt = regionAt;
        if (string.Equals(code, _code, StringComparison.Ordinal))
        {
            return;
        }
        var keep = 0;
        var n = Math.Min(code.Length, _code.Length);
        while (keep < n && code[keep] == _code[keep])
        {
            keep++;
        }
        _code = code;
        // Lines up to the one that holds the first changed character stay as they are.
        var first = 0;
        while (first < _lines.Count && _lines[first].Start + _lines[first].Length < keep)
        {
            first++;
        }
        for (var i = first; i < _lines.Count; i++)
        {
            _lines[i].Layout?.Dispose();
        }
        if (first < _lines.Count)
        {
            _lines.RemoveRange(first, _lines.Count - first);
        }
        var start = first == 0 ? 0 : _lines[first - 1].Start + _lines[first - 1].Length + 1;
        while (start <= code.Length)
        {
            var newline = code.IndexOf('\n', start);
            var end = newline < 0 ? code.Length : newline;
            if (newline < 0 && start == code.Length && _lines.Count > 0)
            {
                // The empty line after a final line break is not a line of its own.
                break;
            }
            _lines.Add(MakeLine(code, start, end));
            if (newline < 0)
            {
                break;
            }
            start = newline + 1;
        }
        _fades.Truncate(keep);
        if (fade && code.Length > keep)
        {
            _fades.Add(keep, code.Length);
        }
        RequestHighlight();
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Re-reads the region of every line (a region was linked after its lines arrived).</summary>
    public void RefreshRegions()
    {
        foreach (var line in _lines)
        {
            line.Region = _regionAt(line.Start + line.Indent);
        }
        InvalidateVisual();
    }

    /// <summary>Shows everything at once (no chunk is fading any more).</summary>
    public void SettleFades()
    {
        _fades.Clear();
        InvalidateVisual();
    }

    /// <summary>The first and last line of a region, or null when it has none.</summary>
    public (int First, int Last)? LinesOf(string region)
    {
        var first = -1;
        var last = -1;
        for (var i = 0; i < _lines.Count; i++)
        {
            if (_lines[i].Region == region)
            {
                if (first < 0)
                {
                    first = i;
                }
                last = i;
            }
        }
        return first < 0 ? null : (first, last);
    }

    /// <summary>The rectangle of lines <paramref name="first"/> to <paramref name="last"/> in the view's coordinates.</summary>
    public Rect LineRect(int first, int last)
    {
        if (_lines.Count == 0)
        {
            return default;
        }
        first = Math.Clamp(first, 0, _lines.Count - 1);
        last = Math.Clamp(last, first, _lines.Count - 1);
        var top = _lines[first].Top;
        var bottom = _lines[last].Top + _lines[last].Height;
        return new Rect(0, top, Bounds.Width, bottom - top);
    }

    private CodeLine MakeLine(string code, int start, int end)
    {
        var indent = 0;
        while (start + indent < end && code[start + indent] is ' ' or '\t')
        {
            indent++;
        }
        return new CodeLine { Start = start, Length = end - start, Indent = indent, Region = _regionAt(start + indent) };
    }

    private void InvalidateLines(int from)
    {
        for (var i = from; i < _lines.Count; i++)
        {
            _lines[i].Layout?.Dispose();
            _lines[i].Layout = null;
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty || change.Property == LineHeightProperty
            || change.Property == ForegroundProperty)
        {
            _charWidth = double.NaN;
            ClearNumbers();
            InvalidateLines(0);
        }
        else if (change.Property == PageBrushProperty)
        {
            var page = (PageBrush as ISolidColorBrush)?.Color ?? Colors.White;
            var dark = Oklch.FromColor(page).L < 0.5;
            if (dark != _dark)
            {
                _dark = dark;
                _tokens = null;
                InvalidateLines(0);
                RequestHighlight();
            }
        }
        else if (change.Property == GutterBrushProperty)
        {
            ClearNumbers();
        }
        else if (change.Property == TintBrushProperty || change.Property == WritingTintOpacityProperty
                 || change.Property == HighlightTintOpacityProperty)
        {
            _writingTint = null;
            _highlightTint = null;
        }
    }

    private Typeface Typeface => new(FontFamily);

    private double CharWidth
    {
        get
        {
            if (double.IsNaN(_charWidth))
            {
                using var probe = new TextLayout("0000000000", Typeface, FontSize, Brushes.Black);
                _charWidth = Math.Max(1, probe.WidthIncludingTrailingWhitespace / 10);
            }
            return _charWidth;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 640;
        var charWidth = CharWidth;
        var digits = Math.Max(2, _lines.Count.ToString(CultureInfo.InvariantCulture).Length);
        _gutterWidth = Math.Ceiling(digits * charWidth + 14 + GutterGap);
        var lineHeight = LineHeight;
        var top = padding.Top;
        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            var indent = line.Indent * charWidth;
            var room = Math.Max(charWidth * 8, width - padding.Left - padding.Right - _gutterWidth - indent - HangingIndent * charWidth);
            line.IndentWidth = indent;
            if (line.Layout is null || !line.LayoutRoom.Equals(room))
            {
                line.Layout?.Dispose();
                line.Layout = Build(i, line, room);
                line.LayoutRoom = room;
            }
            line.Top = top;
            line.Height = Math.Max(lineHeight, line.Layout.TextLines.Count * lineHeight);
            top += line.Height;
        }
        return new Size(width, top + padding.Bottom);
    }

    private TextLayout Build(int index, CodeLine line, double room)
    {
        var textStart = line.Start + line.Indent;
        var length = line.Length - line.Indent;
        var text = length > 0 ? _code.Substring(textStart, length) : string.Empty;
        var foreground = Foreground ?? Brushes.Black;
        var typeface = Typeface;
        var size = FontSize;
        List<ValueSpan<TextRunProperties>>? spans = null;
        var applied = ImmutableArray<HighlightToken>.Empty;
        if (_tokens is { } tokens && index < tokens.Count && SameLine(line.Start, line.Start + line.Length))
        {
            applied = tokens[index];
            foreach (var token in applied)
            {
                var from = Math.Max(token.Start - line.Indent, 0);
                var to = Math.Min(token.Start + token.Length - line.Indent, length);
                if (to <= from)
                {
                    continue;
                }
                spans ??= [];
                spans.Add(new ValueSpan<TextRunProperties>(from, to - from,
                    new GenericTextRunProperties(typeface, size, null, BrushFor(token.Foreground))));
            }
        }
        line.Applied = applied;
        return new TextLayout(text, typeface, size, foreground, TextAlignment.Left, TextWrapping.Wrap, TextTrimming.None, null,
            FlowDirection.LeftToRight, room, double.PositiveInfinity, LineHeight, 0, 0, null, spans);
    }

    /// <summary>Whether the tokens were made for this line as it is now (a line that grew waits for its new tokens).</summary>
    private bool SameLine(int start, int end) =>
        end <= _tokensCode.Length && (end == _tokensCode.Length || _tokensCode[end] == '\n')
        && _code.AsSpan(start, end - start).SequenceEqual(_tokensCode.AsSpan(start, end - start));

    private IBrush BrushFor(uint argb)
    {
        if (!_brushes.TryGetValue(argb, out var brush))
        {
            brush = new ImmutableSolidColorBrush(Color.FromUInt32(argb));
            _brushes[argb] = brush;
        }
        return brush;
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        // The whole area takes the pointer (hover links lines to regions).
        context.FillRectangle(Brushes.Transparent, bounds);
        if (_lines.Count == 0)
        {
            return;
        }
        var padding = Padding;
        var charWidth = CharWidth;
        var hang = HangingIndent * charWidth;
        var codeX = padding.Left + _gutterWidth;
        if (_writingTint is null && TintBrush is ISolidColorBrush tint)
        {
            _writingTint = new ImmutableSolidColorBrush(tint.Color, WritingTintOpacity * tint.Opacity);
            _highlightTint = new ImmutableSolidColorBrush(tint.Color, HighlightTintOpacity * tint.Opacity);
        }
        var writingTint = _writingTint;
        var highlightTint = _highlightTint;
        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            if (line.Layout is null)
            {
                continue;
            }
            var rowRect = new Rect(0, line.Top, bounds.Width, line.Height);
            if (line.Region is { } region)
            {
                var brush = region == _highlighted ? highlightTint : region == _writing ? writingTint : null;
                if (brush is not null)
                {
                    context.FillRectangle(brush, rowRect);
                }
            }
            var number = Number(i + 1);
            number.Draw(context, new Point(padding.Left + _gutterWidth - GutterGap - number.WidthIncludingTrailingWhitespace, line.Top));
            DrawLine(context, line, new Point(codeX + line.IndentWidth, line.Top), hang);
        }
    }

    /// <summary>
    /// Draws one source line: its first visual line at its indentation, wrapped lines a hanging indent further in; the
    /// settled characters once and each fading chunk at its opacity.
    /// </summary>
    private void DrawLine(DrawingContext context, CodeLine line, Point origin, double hang)
    {
        var layout = line.Layout!;
        var textStart = line.Start + line.Indent;
        var y = origin.Y;
        var lineHeight = LineHeight;
        var textLines = layout.TextLines;
        for (var k = 0; k < textLines.Count; k++)
        {
            var x = origin.X + (k > 0 ? hang : 0);
            _fades.DrawLine(context, textLines[k], new Point(x, y), textStart, int.MaxValue);
            y += lineHeight;
        }
    }

    private TextLayout Number(int number)
    {
        if (!_numbers.TryGetValue(number, out var layout))
        {
            layout = new TextLayout(number.ToString(CultureInfo.InvariantCulture), Typeface, FontSize, GutterBrush ?? Brushes.Gray,
                lineHeight: LineHeight);
            _numbers[number] = layout;
        }
        return layout;
    }

    private void ClearNumbers()
    {
        foreach (var layout in _numbers.Values)
        {
            layout.Dispose();
        }
        _numbers.Clear();
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        var y = e.GetPosition(this).Y;
        string? region = null;
        foreach (var line in _lines)
        {
            if (y >= line.Top && y < line.Top + line.Height)
            {
                region = line.Region;
                break;
            }
        }
        SetHover(region);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHover(null);
    }

    private void SetHover(string? region)
    {
        if (_hoverRegion == region)
        {
            return;
        }
        _hoverRegion = region;
        HoverRegionChanged?.Invoke(region);
    }

    private void RequestHighlight()
    {
        _version++;
        if (_highlightedTask is null || _highlightedTask.Task.IsCompleted)
        {
            _highlightedTask = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        if (_code.Length == 0 || string.IsNullOrEmpty(_language) || !s_highlighter.SupportsLanguage(_language))
        {
            _tokens = null;
            _highlightedTask.TrySetResult();
            return;
        }
        if (_busy)
        {
            return;
        }
        _ = HighlightAsync();
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
                    ApplyTokens(tokens, code);
                    _highlightedTask?.TrySetResult();
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
                ReleaseSession();
            }
        }
    }

    /// <summary>New tokens rebuild only the lines whose tokens changed.</summary>
    private void ApplyTokens(IReadOnlyList<ImmutableArray<HighlightToken>>? tokens, string code)
    {
        _tokens = tokens;
        _tokensCode = code;
        var changed = false;
        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            var next = tokens is not null && i < tokens.Count && SameLine(line.Start, line.Start + line.Length)
                ? tokens[i]
                : ImmutableArray<HighlightToken>.Empty;
            if (line.Applied.IsDefault || !line.Applied.SequenceEqual(next))
            {
                line.Layout?.Dispose();
                line.Layout = null;
                changed = true;
            }
        }
        if (changed)
        {
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private void ReleaseSession()
    {
        _session?.Dispose();
        _session = null;
        _sessionKey = null;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_busy)
        {
            _releaseWhenIdle = true;
        }
        else
        {
            ReleaseSession();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _releaseWhenIdle = false;
        if (_code.Length > 0)
        {
            RequestHighlight();
        }
    }

    private sealed class CodeLine
    {
        public int Start;
        public int Length;
        public int Indent;
        public string? Region;
        public TextLayout? Layout;
        public double LayoutRoom;
        public double IndentWidth;
        public double Top;
        public double Height;
        public ImmutableArray<HighlightToken> Applied;
    }
}
