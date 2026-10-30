using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One paragraph of a <see cref="SpeechPlayer"/> reply. While the reply is read, the spoken word sits on one rounded
/// highlight (a Core <see cref="TextRangeDecoration"/> while it rests; drawn between the two words' rectangles while it
/// glides along a line), the words already spoken are in full color, the word just reached fades up to it and the words
/// still to come are drawn at <see cref="UpcomingOpacity"/>. The same cached <see cref="TextLayout"/> is drawn through
/// clips, so nothing is shaped again while reading; word rectangles come from <see cref="TextRangeRenderer.GetRects"/>
/// once per word and layout, never per frame. A new layout (a resize) re-measures and the highlight jumps instead of
/// gliding. While reading, a press on a word asks the player to jump there; otherwise the text selects as usual.
/// </summary>
internal sealed class SpeechPlayerParagraph : DecoratedTextBlock
{
    /// <summary>The paragraph is all spoken.</summary>
    public const int AllSpoken = int.MaxValue;

    /// <summary>The highlight's fill (theme: AccentSubtle).</summary>
    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<SpeechPlayerParagraph, IBrush?>(nameof(HighlightBrush));

    /// <summary>The highlight's corner radius (the top left value is used).</summary>
    public static readonly StyledProperty<CornerRadius> HighlightCornerRadiusProperty =
        AvaloniaProperty.Register<SpeechPlayerParagraph, CornerRadius>(nameof(HighlightCornerRadius));

    /// <summary>How far the highlight reaches past the word.</summary>
    public static readonly StyledProperty<Thickness> HighlightOutsetProperty =
        AvaloniaProperty.Register<SpeechPlayerParagraph, Thickness>(nameof(HighlightOutset));

    /// <summary>How opaque the words still to come are while reading.</summary>
    public static readonly StyledProperty<double> UpcomingOpacityProperty =
        AvaloniaProperty.Register<SpeechPlayerParagraph, double>(nameof(UpcomingOpacity), 0.58);

    /// <summary>The glide between two words on a line at 1× (theme: the token's HighlightGlide).</summary>
    public static readonly StyledProperty<TimeSpan> GlideDurationProperty =
        AvaloniaProperty.Register<SpeechPlayerParagraph, TimeSpan>(nameof(GlideDuration), TimeSpan.FromMilliseconds(150));

    private const double Far = 100_000;

    private readonly TextRangeDecoration _highlight = new();
    private SpeechPlayerParagraphText? _source;
    private int _local = -1;
    private double _reading;
    private double _fade = 1;
    private double _glide = 1;
    private Rect? _from;
    private TextLayout? _measured;
    private int _measuredWord = -2;
    private Rect _wordBox;
    private Rect _wordLine;
    private bool _hasWord;
    private Point _origin;
    private bool _wordsClickable;

    static SpeechPlayerParagraph()
    {
        AffectsRender<SpeechPlayerParagraph>(HighlightBrushProperty, HighlightCornerRadiusProperty, HighlightOutsetProperty, UpcomingOpacityProperty);
    }

    public SpeechPlayerParagraph()
    {
        Decorations.Add(_highlight);
        _highlight.Opacity = 0;
    }

    protected override Type StyleKeyOverride => typeof(SpeechPlayerParagraph);

    /// <inheritdoc cref="HighlightBrushProperty"/>
    public IBrush? HighlightBrush { get => GetValue(HighlightBrushProperty); set => SetValue(HighlightBrushProperty, value); }

    /// <inheritdoc cref="HighlightCornerRadiusProperty"/>
    public CornerRadius HighlightCornerRadius { get => GetValue(HighlightCornerRadiusProperty); set => SetValue(HighlightCornerRadiusProperty, value); }

    /// <inheritdoc cref="HighlightOutsetProperty"/>
    public Thickness HighlightOutset { get => GetValue(HighlightOutsetProperty); set => SetValue(HighlightOutsetProperty, value); }

    /// <inheritdoc cref="UpcomingOpacityProperty"/>
    public double UpcomingOpacity { get => GetValue(UpcomingOpacityProperty); set => SetValue(UpcomingOpacityProperty, value); }

    /// <inheritdoc cref="GlideDurationProperty"/>
    public TimeSpan GlideDuration { get => GetValue(GlideDurationProperty); set => SetValue(GlideDurationProperty, value); }

    /// <summary>Asks the player to jump to a word (its index in the whole reply).</summary>
    public Action<int>? WordPressed { get; set; }

    /// <summary>Whether presses jump to words (while reading) instead of selecting text.</summary>
    public bool WordsClickable
    {
        get => _wordsClickable;
        set
        {
            if (_wordsClickable != value)
            {
                _wordsClickable = value;
                Cursor = value ? new Cursor(StandardCursorType.Hand) : null;
                if (value)
                {
                    SelectionStart = 0;
                    SelectionEnd = 0;
                }
            }
        }
    }

    /// <summary>The paragraph shown.</summary>
    public SpeechPlayerParagraphText? Source
    {
        get => _source;
        set
        {
            _source = value;
            Text = value?.Text;
            _local = -1;
            _from = null;
            _measured = null;
            InvalidateVisual();
        }
    }

    /// <summary>The spoken word in this paragraph: −1 none yet, <see cref="AllSpoken"/> past it, else its index here.</summary>
    internal int LocalWord => _local;

    /// <summary>How much of the reading look applies (0 plain text, 1 dimmed upcoming words and the highlight).</summary>
    internal double Reading => _reading;

    /// <summary>True while the highlight is between two words.</summary>
    internal bool IsGliding => _from is not null && _glide < 1;

    /// <summary>The highlight rectangle drawn now (layout coordinates, before the outset), or null.</summary>
    internal Rect? HighlightRect
    {
        get
        {
            if (!EnsureWord() || !_hasWord)
            {
                return null;
            }
            return _from is { } from && _glide < 1 ? Lerp(from, _wordBox, _glide) : _wordBox;
        }
    }

    /// <summary>
    /// Moves the spoken word. Returns true when the highlight should glide there (same paragraph, same line, animated);
    /// otherwise it cuts. The owner then drives <see cref="SetGlide"/> from 0 to 1.
    /// </summary>
    public bool MoveTo(int local, bool animate)
    {
        if (local == _local)
        {
            return false;
        }
        var drawn = local >= 0 && local != AllSpoken && _local >= 0 && _local != AllSpoken ? HighlightRect : null;
        _local = local;
        _from = null;
        _glide = 1;
        var glides = false;
        if (animate && drawn is { } from && EnsureWord() && _hasWord && Math.Abs(from.Y - _wordBox.Y) < 1)
        {
            _from = from;
            _glide = 0;
            glides = true;
        }
        InvalidateVisual();
        return glides;
    }

    /// <summary>The glide's progress (0 at the previous word, 1 at the new one).</summary>
    public void SetGlide(double progress)
    {
        _glide = Math.Clamp(progress, 0, 1);
        if (_glide >= 1)
        {
            _from = null;
        }
        InvalidateVisual();
    }

    /// <summary>The reading look (0..1) and the new word's fade from dimmed to full color (0..1).</summary>
    public void SetReading(double reading, double fade)
    {
        if (Math.Abs(_reading - reading) < 1e-4 && Math.Abs(_fade - fade) < 1e-4)
        {
            return;
        }
        _reading = Math.Clamp(reading, 0, 1);
        _fade = Math.Clamp(fade, 0, 1);
        InvalidateVisual();
    }

    /// <summary>The index in the whole reply of the word at <paramref name="point"/> (this control's coordinates), or −1.</summary>
    internal int WordAtPoint(Point point)
    {
        if (_source is not { Words.Count: > 0 } source)
        {
            return -1;
        }
        var hit = TextLayout.HitTestPoint(point - _origin);
        var index = hit.TextPosition;
        var found = 0;
        for (var i = 0; i < source.Words.Count; i++)
        {
            if (source.Words[i].Start <= index)
            {
                found = i;
            }
            else
            {
                break;
            }
        }
        return source.WordOffset + found;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (WordsClickable && WordPressed is { } pressed && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var word = WordAtPoint(e.GetPosition(this));
            if (word >= 0)
            {
                e.Handled = true;
                pressed(word);
                return;
            }
        }
        base.OnPointerPressed(e);
    }

    /// <summary>Measures the current word against the current layout (once per word and layout). False without a layout.</summary>
    private bool EnsureWord()
    {
        TextLayout layout;
        try
        {
            layout = TextLayout;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        if (ReferenceEquals(layout, _measured) && _measuredWord == _local)
        {
            return true;
        }
        if (!ReferenceEquals(layout, _measured))
        {
            // A new layout (text, width or style): re-measure and never animate across it.
            _from = null;
            _glide = 1;
        }
        _measured = layout;
        _measuredWord = _local;
        _hasWord = false;
        if (_source is { } source && _local >= 0 && _local < source.Words.Count)
        {
            var word = source.Words[_local];
            var boxes = TextRangeRenderer.GetRects(layout, word.Start, word.Length);
            var lines = TextRangeRenderer.GetRects(layout, word.Start, word.Length, TextRangeExtent.Line);
            if (boxes.Count > 0 && lines.Count > 0)
            {
                _wordBox = boxes[0];
                _wordLine = lines[0];
                _hasWord = true;
            }
        }
        return true;
    }

    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        _origin = origin;
        if (_reading <= 0.001 || _source is null)
        {
            base.RenderTextLayout(context, origin);
            return;
        }
        var layout = TextLayout;
        EnsureWord();
        DrawHighlight(context, layout, origin);
        var upcoming = 1 - _reading * (1 - Math.Clamp(UpcomingOpacity, 0, 1));
        if (_local == AllSpoken)
        {
            layout.Draw(context, origin);
            return;
        }
        if (_local < 0 || !_hasWord)
        {
            using (context.PushOpacity(upcoming))
            {
                layout.Draw(context, origin);
            }
            return;
        }
        var line = _wordLine.Translate(new Vector(origin.X, origin.Y));
        var word = new Rect(_wordBox.X + origin.X, line.Y, _wordBox.Width, line.Height);
        // Spoken: every line above, and this line up to the word.
        DrawClipped(context, layout, origin, new Rect(-Far, -Far, 2 * Far, Far + line.Top), 1);
        DrawClipped(context, layout, origin, new Rect(-Far, line.Top, Far + word.Left, line.Height), 1);
        // The word just reached fades from dimmed to full.
        DrawClipped(context, layout, origin, word, upcoming + (1 - upcoming) * _fade);
        // Still to come: the rest of this line and every line below.
        DrawClipped(context, layout, origin, new Rect(word.Right, line.Top, Far, line.Height), upcoming);
        DrawClipped(context, layout, origin, new Rect(-Far, line.Bottom, 2 * Far, Far), upcoming);
    }

    private static void DrawClipped(DrawingContext context, TextLayout layout, Point origin, Rect clip, double opacity)
    {
        if (clip.Width <= 0 || clip.Height <= 0 || opacity <= 0.001)
        {
            return;
        }
        using (context.PushClip(clip))
        using (opacity < 0.999 ? context.PushOpacity(opacity) : default(DrawingContext.PushedState?))
        {
            layout.Draw(context, origin);
        }
    }

    private void DrawHighlight(DrawingContext context, TextLayout layout, Point origin)
    {
        if (!_hasWord || HighlightBrush is not { } brush || _local < 0 || _local == AllSpoken || _source is null)
        {
            _highlight.Opacity = 0;
            return;
        }
        var radius = HighlightCornerRadius.TopLeft;
        var outset = HighlightOutset;
        if (_from is { } from && _glide < 1)
        {
            // Between two words on one line: one rounded rect at the interpolated place.
            _highlight.Opacity = 0;
            var box = Lerp(from, _wordBox, _glide);
            var rect = new Rect(origin.X + box.X - outset.Left, origin.Y + box.Y - outset.Top,
                box.Width + outset.Left + outset.Right, box.Height + outset.Top + outset.Bottom);
            using (_reading < 0.999 ? context.PushOpacity(_reading) : default(DrawingContext.PushedState?))
            {
                context.DrawRectangle(brush, null, new RoundedRect(rect, Math.Min(radius, rect.Height / 2)));
            }
            return;
        }
        // At rest: the Core range decoration on the word.
        var word = _source.Words[_local];
        _highlight.Start = word.Start;
        _highlight.Length = word.Length;
        _highlight.Background = brush;
        _highlight.CornerRadius = radius;
        _highlight.Outset = outset;
        _highlight.Opacity = _reading;
        TextRangeRenderer.DrawBackgrounds(context, layout, Decorations, origin);
    }

    private static Rect Lerp(Rect a, Rect b, double t) => new(
        a.X + (b.X - a.X) * t,
        a.Y + (b.Y - a.Y) * t,
        a.Width + (b.Width - a.Width) * t,
        a.Height + (b.Height - a.Height) * t);
}
