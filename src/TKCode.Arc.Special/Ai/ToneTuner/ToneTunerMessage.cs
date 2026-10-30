using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The message of a <see cref="ToneTuner"/>: one <see cref="DecoratedTextBlock"/> per paragraph, one <see cref="Run"/> per
/// sentence. A new version crossfades only the sentences that changed: they fade out over 100ms, every paragraph swaps
/// its text on the same frame while they are invisible (one reflow, out of sight), and the new sentences fade in over
/// 160ms. Unchanged sentences are never touched. Words that are new against the original carry a rounded highlight
/// (<see cref="TextRangeDecoration"/>) whose opacity follows its sentence and the Changes switch. The control reserves
/// the height of the tallest of <see cref="Reserved"/>, so switching between those versions never resizes it.
/// <para>
/// Code-driven (<see cref="MotionValue{T}"/>) because the fades run on text runs and decorations inside one text layout,
/// with a swap between the two halves, which a ControlTheme transition cannot express. A frame changes brush colors and
/// decoration opacities only: no measure, no allocation. Reduced motion and Off swap at once.
/// </para>
/// </summary>
internal sealed class ToneTunerMessage : Control
{
    /// <summary>How long a changed sentence takes to fade out, and the new one to fade in.</summary>
    internal static readonly TimeSpan FadeOut = TimeSpan.FromMilliseconds(100);

    internal static readonly TimeSpan FadeIn = TimeSpan.FromMilliseconds(160);

    /// <summary>The highlights fade with the Changes switch over this long.</summary>
    internal static readonly TimeSpan ChangesFade = TimeSpan.FromMilliseconds(160);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<ToneTunerMessage>();

    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<ToneTunerMessage>();

    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<ToneTunerMessage, double>(nameof(LineHeight), double.NaN);

    public static readonly StyledProperty<double> ParagraphGapProperty =
        AvaloniaProperty.Register<ToneTunerMessage, double>(nameof(ParagraphGap), 10);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<ToneTunerMessage, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<ToneTunerMessage, IBrush?>(nameof(HighlightBrush));

    public static readonly StyledProperty<CornerRadius> HighlightCornerRadiusProperty =
        AvaloniaProperty.Register<ToneTunerMessage, CornerRadius>(nameof(HighlightCornerRadius), new CornerRadius(4));

    public static readonly StyledProperty<Thickness> HighlightOutsetProperty =
        AvaloniaProperty.Register<ToneTunerMessage, Thickness>(nameof(HighlightOutset), new Thickness(2, 1));

    private sealed class Piece
    {
        public required Run Run { get; init; }
        public required string Text { get; init; }
        public required int Start { get; init; }
        public required bool IsSentence { get; init; }
        public SolidColorBrush? Brush { get; init; }
        public double Alpha { get; set; } = 1;
        public double From { get; set; } = 1;
        public double To { get; set; } = 1;
        public List<TextRangeDecoration> Decorations { get; } = [];
    }

    private sealed class Paragraph
    {
        public required DecoratedTextBlock Block { get; init; }
        public string Text { get; set; } = string.Empty;
        public int Offset { get; set; }
        public List<Piece> Pieces { get; set; } = [];
    }

    private readonly List<Paragraph> _paragraphs = [];
    private readonly SolidColorBrush _separatorBrush = new();
    private readonly Dictionary<string, double> _heights = [];
    private double _heightsWidth = double.NaN;
    private IReadOnlyList<string> _reserved = [];
    private IReadOnlyList<(int Start, int Length)> _ranges = [];
    private string _text = string.Empty;
    private MotionValue<double>? _progress;
    private MotionValue<double>? _changes;
    private double _changesOpacity = 1;
    private bool _showChanges = true;
    private int _generation;
    private Color _foreground;

    public ToneTunerMessage()
    {
        ClipToBounds = false;
    }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    public double ParagraphGap { get => GetValue(ParagraphGapProperty); set => SetValue(ParagraphGapProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public IBrush? HighlightBrush { get => GetValue(HighlightBrushProperty); set => SetValue(HighlightBrushProperty, value); }

    public CornerRadius HighlightCornerRadius { get => GetValue(HighlightCornerRadiusProperty); set => SetValue(HighlightCornerRadiusProperty, value); }

    public Thickness HighlightOutset { get => GetValue(HighlightOutsetProperty); set => SetValue(HighlightOutsetProperty, value); }

    /// <summary>The text on screen (the newest one asked for once a swap has finished).</summary>
    public string Text => _text;

    /// <summary>True between a new version arriving and its sentences having faded in.</summary>
    public bool IsSwapping { get; private set; }

    /// <summary>The versions whose tallest height the control keeps, so switching between them never resizes it.</summary>
    public IReadOnlyList<string> Reserved
    {
        get => _reserved;
        set
        {
            if (_reserved.SequenceEqual(value))
            {
                return;
            }
            _reserved = [.. value];
            InvalidateMeasure();
        }
    }

    /// <summary>Whether the new words are highlighted; the highlights fade in and out.</summary>
    public bool ShowChanges
    {
        get => _showChanges;
        set
        {
            if (_showChanges == value)
            {
                return;
            }
            _showChanges = value;
            var target = value ? 1.0 : 0.0;
            if (!IsLoaded || !ArcMotion.IsFull(this))
            {
                _changes?.Jump(target);
                _changesOpacity = target;
                ApplyAll();
                return;
            }
            _changes ??= CreateChanges();
            _ = _changes.AnimateTo(target, MotionSpec.Tween(ChangesFade, ArcEasings.Standard));
        }
    }

    /// <summary>The paragraphs' texts as laid out (tests).</summary>
    internal IReadOnlyList<string> ParagraphTexts => _paragraphs.Select(p => p.Text).ToList();

    /// <summary>The highlighted text with its current opacity (tests).</summary>
    internal IReadOnlyList<(string Text, double Opacity)> Highlights =>
        _paragraphs.SelectMany(p => p.Pieces.SelectMany(piece => piece.Decorations.Select(d => (p.Text.Substring(d.Start, d.Length), d.Opacity)))).ToList();

    /// <summary>The opacity of each sentence on screen (tests).</summary>
    internal IReadOnlyList<(string Text, double Alpha)> Sentences =>
        _paragraphs.SelectMany(p => p.Pieces.Where(piece => piece.IsSentence).Select(piece => (piece.Text, piece.Alpha))).ToList();

    internal IEnumerable<DecoratedTextBlock> Blocks => _paragraphs.Select(p => p.Block);

    /// <summary>
    /// Shows <paramref name="text"/> with <paramref name="inserted"/> (ranges of it) highlighted. With
    /// <paramref name="animate"/> and full motion, changed sentences crossfade; otherwise everything swaps at once.
    /// </summary>
    public void Show(string text, IReadOnlyList<(int Start, int Length)> inserted, bool animate)
    {
        _generation++;
        _text = text;
        _ranges = inserted;
        var plan = Plan(text);
        if (!animate || !IsLoaded || !ArcMotion.IsFull(this) || _paragraphs.Count == 0)
        {
            _progress?.Stop();
            IsSwapping = false;
            Swap(plan, incoming: 1);
            foreach (var piece in AllPieces())
            {
                piece.Alpha = piece.From = piece.To = 1;
            }
            ApplyAll();
            return;
        }
        _ = Crossfade(plan, _generation);
    }

    private async Task Crossfade(List<(int Old, int New)> plan, int generation)
    {
        IsSwapping = true;
        var progress = _progress ??= CreateProgress();
        var leaving = Leaving(plan);
        foreach (var piece in AllPieces())
        {
            piece.From = piece.Alpha;
            piece.To = leaving.Contains(piece) ? 0 : piece.Alpha;
        }
        if (leaving.Count > 0)
        {
            progress.Jump(0);
            await progress.AnimateTo(1, MotionSpec.Tween(FadeOut, ArcEasings.Standard));
            if (generation != _generation)
            {
                return;
            }
        }
        // Every changed sentence is invisible now: swap all paragraphs on this frame so the text reflows once.
        Swap(Plan(_text), incoming: 0);
        foreach (var piece in AllPieces())
        {
            piece.From = piece.Alpha;
            piece.To = 1;
        }
        progress.Jump(0);
        ApplyAll();
        await progress.AnimateTo(1, MotionSpec.Tween(FadeIn, ArcEasings.Standard));
        if (generation != _generation)
        {
            return;
        }
        foreach (var piece in AllPieces())
        {
            piece.Alpha = piece.From = piece.To = 1;
        }
        ApplyAll();
        IsSwapping = false;
    }

    private MotionValue<double> CreateProgress()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += p =>
        {
            foreach (var paragraph in _paragraphs)
            {
                var changed = false;
                foreach (var piece in paragraph.Pieces)
                {
                    if (piece.From == piece.To && piece.Alpha == piece.To)
                    {
                        continue;
                    }
                    piece.Alpha = piece.From + (piece.To - piece.From) * p;
                    Apply(piece);
                    changed = true;
                }
                if (changed)
                {
                    paragraph.Block.InvalidateDecorations();
                }
            }
        };
        return value;
    }

    private MotionValue<double> CreateChanges()
    {
        var value = new MotionValue<double>(this, _changesOpacity, MotionVectors.Double);
        value.Changed += opacity =>
        {
            _changesOpacity = opacity;
            ApplyAll();
        };
        return value;
    }

    private IEnumerable<Piece> AllPieces() => _paragraphs.SelectMany(p => p.Pieces);

    /// <summary>
    /// How the new paragraphs line up with the ones on screen: equal paragraphs at the start and the end pair up, those in
    /// between pair by position, and the rest leave (Old only) or arrive (New only).
    /// </summary>
    private List<(int Old, int New)> Plan(string text)
    {
        var next = ToneTunerText.Paragraphs(text).Select(p => p.Text).ToList();
        var old = _paragraphs.Select(p => p.Text).ToList();
        var (prefix, suffix) = ToneTunerText.Align(old, next);
        var plan = new List<(int, int)>();
        for (var i = 0; i < prefix; i++)
        {
            plan.Add((i, i));
        }
        var oldMiddle = old.Count - prefix - suffix;
        var newMiddle = next.Count - prefix - suffix;
        for (var i = 0; i < Math.Max(oldMiddle, newMiddle); i++)
        {
            plan.Add((i < oldMiddle ? prefix + i : -1, i < newMiddle ? prefix + i : -1));
        }
        for (var i = 0; i < suffix; i++)
        {
            plan.Add((old.Count - suffix + i, next.Count - suffix + i));
        }
        return plan;
    }

    /// <summary>The sentences on screen that the new version replaces or drops.</summary>
    private HashSet<Piece> Leaving(List<(int Old, int New)> plan)
    {
        var next = ToneTunerText.Paragraphs(_text);
        var leaving = new HashSet<Piece>();
        foreach (var (oldIndex, newIndex) in plan)
        {
            if (oldIndex < 0)
            {
                continue;
            }
            var old = _paragraphs[oldIndex];
            var sentences = old.Pieces.Where(p => p.IsSentence).ToList();
            if (newIndex < 0)
            {
                leaving.UnionWith(sentences);
                continue;
            }
            if (old.Text == next[newIndex].Text)
            {
                continue;
            }
            var kept = ToneTunerText.Match(sentences.Select(s => s.Text).ToList(), next[newIndex].Sentences).Where(i => i >= 0).ToHashSet();
            for (var i = 0; i < sentences.Count; i++)
            {
                if (!kept.Contains(i))
                {
                    leaving.Add(sentences[i]);
                }
            }
        }
        return leaving;
    }

    /// <summary>Lays the new version out: kept paragraphs stay, changed ones get new runs (kept sentences keep their opacity, new ones start at <paramref name="incoming"/>).</summary>
    private void Swap(List<(int Old, int New)> plan, double incoming)
    {
        var next = ToneTunerText.Paragraphs(_text);
        var paragraphs = new List<Paragraph>(next.Count);
        var unused = new List<Paragraph>();
        foreach (var (oldIndex, newIndex) in plan)
        {
            var old = oldIndex >= 0 ? _paragraphs[oldIndex] : null;
            if (newIndex < 0)
            {
                unused.Add(old!);
                continue;
            }
            var model = next[newIndex];
            if (old is not null && old.Text == model.Text)
            {
                old.Offset = model.Offset;
                paragraphs.Add(old);
                continue;
            }
            var paragraph = old ?? NewParagraph();
            var previous = old?.Pieces.Where(p => p.IsSentence).ToList() ?? [];
            var match = ToneTunerText.Match(previous.Select(p => p.Text).ToList(), model.Sentences);
            var pieces = new List<Piece>(model.Pieces.Count);
            var inlines = new InlineCollection();
            var sentenceIndex = 0;
            foreach (var part in model.Pieces)
            {
                var text = model.Text.Substring(part.Start, part.Length);
                var alpha = 1.0;
                SolidColorBrush? brush = null;
                if (part.IsSentence)
                {
                    // A kept sentence keeps its opacity (it never animates); a changed or new one starts at incoming.
                    alpha = match[sentenceIndex] >= 0 ? previous[match[sentenceIndex]].Alpha : incoming;
                    brush = new SolidColorBrush(_foreground);
                    sentenceIndex++;
                }
                var run = new Run(text) { Foreground = brush ?? _separatorBrush };
                inlines.Add(run);
                pieces.Add(new Piece { Run = run, Text = text, Start = part.Start, IsSentence = part.IsSentence, Brush = brush, Alpha = alpha, From = alpha, To = alpha });
            }
            paragraph.Text = model.Text;
            paragraph.Offset = model.Offset;
            paragraph.Pieces = pieces;
            paragraph.Block.Decorations.Clear();
            paragraph.Block.Inlines = inlines;
            paragraphs.Add(paragraph);
        }
        foreach (var paragraph in unused)
        {
            paragraph.Block.Decorations.Clear();
            VisualChildren.Remove(paragraph.Block);
            LogicalChildren.Remove(paragraph.Block);
        }
        _paragraphs.Clear();
        _paragraphs.AddRange(paragraphs);
        // Keep the children in reading order.
        foreach (var paragraph in _paragraphs)
        {
            if (!VisualChildren.Contains(paragraph.Block))
            {
                VisualChildren.Add(paragraph.Block);
                LogicalChildren.Add(paragraph.Block);
            }
        }
        Decorate();
        InvalidateMeasure();
    }

    private Paragraph NewParagraph()
    {
        var block = new DecoratedTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Focusable = false,
            IsTabStop = false,
        };
        ApplyFont(block);
        return new Paragraph { Block = block };
    }

    private void ApplyFont(TextBlock block)
    {
        block.FontFamily = FontFamily;
        block.FontSize = FontSize;
        block.LineHeight = LineHeight;
        block.Foreground = Foreground;
    }

    /// <summary>Puts the highlights on the sentences they fall in, one decoration per sentence and range.</summary>
    private void Decorate()
    {
        var radius = HighlightCornerRadius.TopLeft;
        var outset = HighlightOutset;
        foreach (var paragraph in _paragraphs)
        {
            paragraph.Block.Decorations.Clear();
            foreach (var piece in paragraph.Pieces)
            {
                piece.Decorations.Clear();
                if (!piece.IsSentence)
                {
                    continue;
                }
                var start = paragraph.Offset + piece.Start;
                var end = start + piece.Text.Length;
                foreach (var (rangeStart, rangeLength) in _ranges)
                {
                    var from = Math.Max(start, rangeStart);
                    var to = Math.Min(end, rangeStart + rangeLength);
                    if (to <= from)
                    {
                        continue;
                    }
                    var decoration = new TextRangeDecoration(from - paragraph.Offset, to - from)
                    {
                        Background = HighlightBrush,
                        CornerRadius = radius,
                        Outset = outset,
                    };
                    piece.Decorations.Add(decoration);
                    paragraph.Block.Decorations.Add(decoration);
                }
            }
        }
    }

    private void Apply(Piece piece)
    {
        if (piece.Brush is { } brush)
        {
            brush.Color = Color.FromArgb((byte)Math.Round(_foreground.A * Math.Clamp(piece.Alpha, 0, 1)), _foreground.R, _foreground.G, _foreground.B);
        }
        foreach (var decoration in piece.Decorations)
        {
            decoration.Opacity = Math.Clamp(piece.Alpha, 0, 1) * _changesOpacity;
        }
    }

    private void ApplyAll()
    {
        foreach (var paragraph in _paragraphs)
        {
            foreach (var piece in paragraph.Pieces)
            {
                Apply(piece);
            }
            paragraph.Block.InvalidateDecorations();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty)
        {
            _foreground = Foreground is ISolidColorBrush solid ? Color.FromArgb((byte)(solid.Color.A * solid.Opacity), solid.Color.R, solid.Color.G, solid.Color.B) : Colors.Black;
            _separatorBrush.Color = _foreground;
            foreach (var paragraph in _paragraphs)
            {
                paragraph.Block.Foreground = Foreground;
            }
            ApplyAll();
        }
        else if (change.Property == HighlightBrushProperty || change.Property == HighlightCornerRadiusProperty || change.Property == HighlightOutsetProperty)
        {
            Decorate();
            ApplyAll();
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty || change.Property == LineHeightProperty)
        {
            foreach (var paragraph in _paragraphs)
            {
                ApplyFont(paragraph.Block);
            }
            _heights.Clear();
            InvalidateMeasure();
        }
        else if (change.Property == ParagraphGapProperty)
        {
            _heights.Clear();
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var height = 0.0;
        var widest = 0.0;
        for (var i = 0; i < _paragraphs.Count; i++)
        {
            var block = _paragraphs[i].Block;
            block.Measure(new Size(width, double.PositiveInfinity));
            height += block.DesiredSize.Height + (i > 0 ? ParagraphGap : 0);
            widest = Math.Max(widest, block.DesiredSize.Width);
        }
        if (double.IsFinite(width) && width > 0)
        {
            foreach (var text in _reserved)
            {
                height = Math.Max(height, HeightOf(text, width));
            }
        }
        return new Size(double.IsFinite(width) ? width : widest, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0.0;
        for (var i = 0; i < _paragraphs.Count; i++)
        {
            var block = _paragraphs[i].Block;
            if (i > 0)
            {
                y += ParagraphGap;
            }
            block.Arrange(new Rect(0, y, finalSize.Width, block.DesiredSize.Height));
            y += block.DesiredSize.Height;
        }
        return finalSize;
    }

    /// <summary>The height <paramref name="text"/> takes at <paramref name="width"/>, laid out like the blocks (cached per width).</summary>
    internal double HeightOf(string text, double width)
    {
        if (_heightsWidth != width)
        {
            _heights.Clear();
            _heightsWidth = width;
        }
        if (_heights.TryGetValue(text, out var cached))
        {
            return cached;
        }
        var typeface = new Typeface(FontFamily);
        var height = 0.0;
        var paragraphs = ToneTunerText.Paragraphs(text);
        for (var i = 0; i < paragraphs.Count; i++)
        {
            using var layout = new TextLayout(paragraphs[i].Text, typeface, FontSize, null, TextAlignment.Left, TextWrapping.Wrap,
                null, null, FlowDirection.LeftToRight, width, double.PositiveInfinity, LineHeight);
            height += layout.Height + (i > 0 ? ParagraphGap : 0);
        }
        _heights[text] = height;
        return height;
    }
}
