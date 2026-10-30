using System.Text;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Wrapped text that streams in, where each new chunk only fades in (Arc's per-chunk span fade, by default 140ms on the
/// standard curve). The text is one cached <see cref="TextLayout"/> drawn by <see cref="AiTextFades"/>, which fades the
/// glyphs themselves (no cover in the surface's color, so any background works) and never lays the text out during a
/// fade. Two ways to stream:
/// <list type="bullet">
/// <item><see cref="Append"/>: the text grows chunk by chunk (it rewraps as it grows);</item>
/// <item><see cref="Reserve"/> then <see cref="RevealTo"/>: the whole text is laid out first, so its size is known from the
/// start and nothing moves, and only the revealed part is drawn.</item>
/// </list>
/// <see cref="SetText"/> shows a text whole with no fade (a cached result). Reduced motion and Off show every chunk at
/// once. Each component keeps a thin subclass so its theme styles its own text and its measured timing stays its own
/// (<see cref="FadeDuration"/>, <see cref="FadeEasing"/>).
/// </summary>
internal class AiStreamText : Control
{
    /// <summary>The text color.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<AiStreamText>();

    /// <summary>The font family (inherited).</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<AiStreamText>();

    /// <summary>The font size (inherited).</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<AiStreamText>();

    /// <summary>The font weight (inherited).</summary>
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<AiStreamText>();

    /// <summary>The height of each line; NaN uses the font's.</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<AiStreamText, double>(nameof(LineHeight), double.NaN);

    private readonly StringBuilder _text = new();
    private readonly AiTextFades _fades;
    private string? _textCache;
    private int _revealed;
    private int _reveals;
    private TextLayout? _layout;
    private double _layoutWidth = double.NaN;
    private bool _layoutDirty = true;

    static AiStreamText()
    {
        AffectsMeasure<AiStreamText>(FontFamilyProperty, FontSizeProperty, FontWeightProperty, LineHeightProperty);
        AffectsRender<AiStreamText>(ForegroundProperty);
    }

    public AiStreamText()
    {
        _fades = new AiTextFades(this, InvalidateVisual);
    }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <inheritdoc cref="FontFamilyProperty"/>
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <inheritdoc cref="FontSizeProperty"/>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <inheritdoc cref="FontWeightProperty"/>
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    /// <inheritdoc cref="LineHeightProperty"/>
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    /// <summary>How long one chunk takes to fade in.</summary>
    public TimeSpan FadeDuration { get => _fades.Duration; set => _fades.Duration = value; }

    /// <summary>The fade's curve (the standard curve by default); null fades linearly.</summary>
    public Easing? FadeEasing { get => _fades.Easing; set => _fades.Easing = value; }

    /// <summary>
    /// False (default): the text takes the whole width it is given. True: it is as wide as its longest line (rounded up),
    /// like a text block, so a bubble or a chip can wrap around it.
    /// </summary>
    public bool SizesToText { get; set; }

    /// <summary>Keeps at least one line's height while the text is empty or short (a draft box that must not collapse).</summary>
    public bool HoldsOneLine { get; set; }

    /// <summary>Takes no space at all while there is no text (by default an empty text is one empty line high).</summary>
    public bool CollapsesWhenEmpty { get; set; }

    /// <summary>The whole text laid out (shown or still to be revealed).</summary>
    public string Text => _textCache ??= _text.ToString();

    /// <summary>The length of <see cref="Text"/>.</summary>
    public int Length => _text.Length;

    /// <summary>How many characters are drawn; the rest holds its place, unseen.</summary>
    public int Revealed => _revealed;

    /// <summary>The part of the text that is drawn.</summary>
    public string RevealedText => _revealed >= _text.Length ? Text : Text[.._revealed];

    /// <summary>How many times text was revealed (appended or revealed further) since the last <see cref="SetText"/> or <see cref="Reserve"/>.</summary>
    public int RevealCount => _reveals;

    /// <summary>The chunks still fading in.</summary>
    public int FadingCount => _fades.Count;

    /// <summary>True when the whole text shows and nothing fades: it is drawn as one piece.</summary>
    public bool DrawsWhole => _revealed >= _text.Length && _fades.Count == 0;

    /// <summary>The number of wrapped lines (after layout).</summary>
    public int LineCount => _layout?.TextLines.Count ?? 0;

    /// <summary>The fades, for subclasses that time something after them.</summary>
    internal AiTextFades Fades => _fades;

    /// <summary>The current layout (null before the first measure).</summary>
    internal TextLayout? CurrentLayout => _layout;

    /// <summary>The brush the layout draws the text with: <see cref="Foreground"/> unless a subclass mixes its own.</summary>
    protected virtual IBrush? TextBrush => Foreground;

    /// <summary>Shows <paramref name="text"/> whole, with no fade. The same text already shown whole changes nothing.</summary>
    public void SetText(string? text)
    {
        text ??= string.Empty;
        if (_revealed >= _text.Length && string.Equals(Text, text, StringComparison.Ordinal))
        {
            return;
        }
        Replace(text);
        _revealed = text.Length;
    }

    /// <summary>Lays <paramref name="text"/> out whole but draws none of it yet; <see cref="RevealTo"/> shows it.</summary>
    public void Reserve(string? text)
    {
        Replace(text ?? string.Empty);
        _revealed = 0;
    }

    /// <summary>
    /// Adds <paramref name="chunk"/> at the end and shows it; with <paramref name="fade"/> it fades in (full motion, and only
    /// once the text is on screen). Text still unrevealed before it shows at once.
    /// </summary>
    public void Append(string? chunk, bool fade = true)
    {
        if (string.IsNullOrEmpty(chunk))
        {
            return;
        }
        var start = Math.Max(_revealed, _text.Length);
        _text.Append(chunk);
        _textCache = null;
        _layoutDirty = true;
        _revealed = start;
        InvalidateMeasure();
        RevealTo(_text.Length, fade);
    }

    /// <summary>Draws the text up to <paramref name="end"/>; with <paramref name="fade"/> the newly shown part fades in.</summary>
    public void RevealTo(int end, bool fade)
    {
        end = Math.Clamp(end, 0, _text.Length);
        if (end <= _revealed)
        {
            return;
        }
        var start = _revealed;
        _revealed = end;
        _reveals++;
        if (fade)
        {
            _fades.Add(start, end);
        }
        InvalidateVisual();
    }

    /// <summary>Draws the whole text at once.</summary>
    public void RevealAll() => RevealTo(_text.Length, fade: false);

    /// <summary>Ends every fade at once.</summary>
    public void Settle()
    {
        _fades.Clear();
        InvalidateVisual();
    }

    private void Replace(string text)
    {
        _text.Clear().Append(text);
        _textCache = text;
        _reveals = 0;
        _fades.Clear();
        _layoutDirty = true;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Called after a new layout replaced the old one (a subclass drops what it measured on the old one).</summary>
    protected virtual void OnLayoutChanged(TextLayout layout)
    {
    }

    /// <summary>Draws over the text (after it), in the text's coordinates.</summary>
    protected virtual void RenderOverlay(DrawingContext context, TextLayout layout)
    {
    }

    /// <summary>Lays the text out again on the next measure (a subclass changed what <see cref="TextBrush"/> returns).</summary>
    protected void InvalidateTextLayout()
    {
        _layoutDirty = true;
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (CollapsesWhenEmpty && _text.Length == 0)
        {
            return default;
        }
        var layout = EnsureLayout(availableSize.Width);
        var width = SizesToText
            ? Math.Min(availableSize.Width, Math.Ceiling(layout.WidthIncludingTrailingWhitespace))
            : double.IsFinite(availableSize.Width) ? availableSize.Width : layout.WidthIncludingTrailingWhitespace;
        var height = layout.Height;
        if (HoldsOneLine)
        {
            height = Math.Max(double.IsNaN(LineHeight) ? FontSize * 1.4 : LineHeight, height);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Wrap at the width actually given (a text sized to its longest line keeps the same lines).
        if (_layout is not null && Math.Abs(_layoutWidth - finalSize.Width) > 0.5 && double.IsFinite(finalSize.Width))
        {
            EnsureLayout(finalSize.Width);
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (_layout is not { } layout || _text.Length == 0)
        {
            return;
        }
        if (DrawsWhole)
        {
            layout.Draw(context, default);
        }
        else if (_revealed > 0)
        {
            _fades.Draw(context, layout, default, _revealed);
        }
        RenderOverlay(context, layout);
    }

    private TextLayout EnsureLayout(double width)
    {
        var max = double.IsFinite(width) ? Math.Max(0, width) : double.PositiveInfinity;
        if (_layout is not null && !_layoutDirty && _layoutWidth.Equals(max))
        {
            return _layout;
        }
        _layout?.Dispose();
        _layout = new TextLayout(Text, new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize, TextBrush,
            TextAlignment.Left, TextWrapping.Wrap, maxWidth: max, lineHeight: LineHeight);
        _layoutWidth = max;
        _layoutDirty = false;
        _fades.ForgetLines();
        OnLayoutChanged(_layout);
        return _layout;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == FontFamilyProperty || change.Property == FontSizeProperty
            || change.Property == FontWeightProperty || change.Property == LineHeightProperty)
        {
            _layoutDirty = true;
            InvalidateMeasure();
        }
    }
}
