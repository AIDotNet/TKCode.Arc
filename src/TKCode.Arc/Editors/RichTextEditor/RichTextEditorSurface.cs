using System.Collections.Immutable;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.Utilities;

namespace TKCode.Arc;

/// <summary>
/// The writing surface of <see cref="RichTextEditor"/>: lays out each block with a <see cref="TextLayout"/> (marks are
/// run overrides), draws block decorations, selection and the caret itself, hit tests pointer positions, and hosts the
/// IME client. Block layouts are cached by block identity, so an edit reflows only the blocks it changed; drawing is
/// limited to the blocks near the visible viewport.
/// </summary>
internal sealed class RichTextEditorSurface : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<RichTextEditorSurface>();
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<RichTextEditorSurface>();
    public static readonly StyledProperty<FontFamily?> MonoFontFamilyProperty = AvaloniaProperty.Register<RichTextEditorSurface, FontFamily?>(nameof(MonoFontFamily));
    public static readonly StyledProperty<IBrush?> AccentBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(AccentBrush));
    public static readonly StyledProperty<IBrush?> SecondaryBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(SecondaryBrush));
    public static readonly StyledProperty<IBrush?> MutedBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(MutedBrush));
    public static readonly StyledProperty<IBrush?> LinkBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(LinkBrush));
    public static readonly StyledProperty<IBrush?> CodeBackgroundProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(CodeBackground));
    public static readonly StyledProperty<IBrush?> CodeBorderBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(CodeBorderBrush));
    public static readonly StyledProperty<IBrush?> QuoteBarBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(QuoteBarBrush));
    public static readonly StyledProperty<IBrush?> RuleBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(RuleBrush));
    public static readonly StyledProperty<double> BaseFontSizeProperty = AvaloniaProperty.Register<RichTextEditorSurface, double>(nameof(BaseFontSize), 16);
    public static readonly StyledProperty<double> Heading1FontSizeProperty = AvaloniaProperty.Register<RichTextEditorSurface, double>(nameof(Heading1FontSize), 28);
    public static readonly StyledProperty<double> Heading2FontSizeProperty = AvaloniaProperty.Register<RichTextEditorSurface, double>(nameof(Heading2FontSize), 22);
    public static readonly StyledProperty<double> Heading3FontSizeProperty = AvaloniaProperty.Register<RichTextEditorSurface, double>(nameof(Heading3FontSize), 18);
    public static readonly StyledProperty<double> CodeFontSizeProperty = AvaloniaProperty.Register<RichTextEditorSurface, double>(nameof(CodeFontSize), 14);
    public static readonly StyledProperty<double> TrackingProperty = AvaloniaProperty.Register<RichTextEditorSurface, double>(nameof(Tracking), -0.01);

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public FontFamily? MonoFontFamily { get => GetValue(MonoFontFamilyProperty); set => SetValue(MonoFontFamilyProperty, value); }
    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public IBrush? SecondaryBrush { get => GetValue(SecondaryBrushProperty); set => SetValue(SecondaryBrushProperty, value); }
    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }
    public IBrush? LinkBrush { get => GetValue(LinkBrushProperty); set => SetValue(LinkBrushProperty, value); }
    public IBrush? CodeBackground { get => GetValue(CodeBackgroundProperty); set => SetValue(CodeBackgroundProperty, value); }
    public IBrush? CodeBorderBrush { get => GetValue(CodeBorderBrushProperty); set => SetValue(CodeBorderBrushProperty, value); }
    public IBrush? QuoteBarBrush { get => GetValue(QuoteBarBrushProperty); set => SetValue(QuoteBarBrushProperty, value); }
    public IBrush? RuleBrush { get => GetValue(RuleBrushProperty); set => SetValue(RuleBrushProperty, value); }
    public double BaseFontSize { get => GetValue(BaseFontSizeProperty); set => SetValue(BaseFontSizeProperty, value); }
    public double Heading1FontSize { get => GetValue(Heading1FontSizeProperty); set => SetValue(Heading1FontSizeProperty, value); }
    public double Heading2FontSize { get => GetValue(Heading2FontSizeProperty); set => SetValue(Heading2FontSizeProperty, value); }
    public double Heading3FontSize { get => GetValue(Heading3FontSizeProperty); set => SetValue(Heading3FontSizeProperty, value); }
    public double CodeFontSize { get => GetValue(CodeFontSizeProperty); set => SetValue(CodeFontSizeProperty, value); }

    /// <summary>Letter spacing in em (Arc's body tracking).</summary>
    public double Tracking { get => GetValue(TrackingProperty); set => SetValue(TrackingProperty, value); }

    private const double VerticalPadding = 2;
    private const double ListIndentEm = 1.45;
    private const double ListItemPaddingEm = 0.2;

    private readonly Dictionary<RichTextEditorBlock, RichTextEditorBlockView> _cache = new(ReferenceEqualityComparer.Instance);
    private readonly List<RichTextEditorBlockView> _views = new();
    private readonly List<double> _tops = new();
    private readonly List<string?> _markers = new();
    private readonly Dictionary<string, TextLayout> _markerLayouts = new(StringComparer.Ordinal);
    private readonly RichTextEditorCaret _caret = new();
    private readonly RichTextEditorImeClient _ime;
    private ImmutableList<RichTextEditorBlock>? _laidOut;
    private double _layoutWidth = double.NaN;
    private int _styleVersion = 1;
    private Rect _viewport;
    private Rect _renderedBand;
    private string? _preedit;
    private int? _preeditCursor;
    private bool _composed;
    private TextLayout? _placeholderLayout;
    private (string Text, double Size, FontWeight Weight, int Version) _placeholderKey;
    private IBrush? _selectionBrush;
    private IBrush? _linkUnderlineBrush;
    private TextDecorationCollection? _strike;
    private TextDecorationCollection? _underline;
    private TextDecorationCollection? _strikeUnderline;
    private TextDecorationCollection? _preeditUnderline;
    private Point? _touchStart;
    private int _dragClicks;
    private RichTextEditorPosition _dragOrigin;

    static RichTextEditorSurface()
    {
        FocusableProperty.OverrideDefaultValue<RichTextEditorSurface>(true);
        CursorProperty.OverrideDefaultValue<RichTextEditorSurface>(new Cursor(StandardCursorType.Ibeam));
        AffectsMeasure<RichTextEditorSurface>(FontFamilyProperty, MonoFontFamilyProperty, BaseFontSizeProperty, Heading1FontSizeProperty,
            Heading2FontSizeProperty, Heading3FontSizeProperty, CodeFontSizeProperty, TrackingProperty);
        TextInputMethodClientRequestedEvent.AddClassHandler<RichTextEditorSurface>((surface, e) =>
        {
            if (surface.Owner is { IsReadOnly: false })
            {
                e.Client = surface._ime;
            }
        });
    }

    public RichTextEditorSurface()
    {
        _ime = new RichTextEditorImeClient(this);
        VisualChildren.Add(_caret);
        EffectiveViewportChanged += OnEffectiveViewportChanged;
    }

    internal RichTextEditor? Owner { get; set; }

    private RichTextEditorEngine? Engine => Owner?.Engine;

    /// <summary>True while a mouse drag is selecting (Arc keeps the toolbar hidden until the button is released).</summary>
    public bool IsDragging { get; private set; }

    public bool IsComposing => !string.IsNullOrEmpty(_preedit);

    internal RichTextEditorImeClient Ime => _ime;

    internal IReadOnlyList<RichTextEditorBlockView> Views => _views;

    /// <summary>The goal x of vertical caret movement, kept across Up and Down presses.</summary>
    internal double? GoalX { get; set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ForegroundProperty || property == AccentBrushProperty || property == SecondaryBrushProperty || property == MutedBrushProperty
            || property == LinkBrushProperty || property == FontFamilyProperty || property == MonoFontFamilyProperty || property == BaseFontSizeProperty
            || property == Heading1FontSizeProperty || property == Heading2FontSizeProperty || property == Heading3FontSizeProperty
            || property == CodeFontSizeProperty || property == TrackingProperty)
        {
            InvalidateStyle();
        }
        else if (property == CodeBackgroundProperty || property == CodeBorderBrushProperty || property == QuoteBarBrushProperty || property == RuleBrushProperty)
        {
            InvalidateVisual();
        }
        if (property == AccentBrushProperty)
        {
            _caret.Brush = AccentBrush;
        }
    }

    internal void InvalidateStyle()
    {
        _styleVersion++;
        _selectionBrush = null;
        _linkUnderlineBrush = null;
        _strike = _underline = _strikeUnderline = _preeditUnderline = null;
        foreach (var layout in _markerLayouts.Values)
        {
            layout.Dispose();
        }
        _markerLayouts.Clear();
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Called by the owner after the document or the selection changed.</summary>
    internal void OnDocumentChanged(bool content)
    {
        if (content)
        {
            InvalidateMeasure();
        }
        else
        {
            InvalidateArrange();
        }
        InvalidateVisual();
        _caret.Restart();
        _ime.NotifySurroundingText();
        _ime.NotifySelection();
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Layout
     * -------------------------------------------------------------------------------------------------------- */

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 600;
        EnsureLayout(width);
        var height = VerticalPadding;
        if (_views.Count > 0)
        {
            height = _tops[^1] + _views[^1].Height + VerticalPadding;
        }
        _caret.Measure(Size.Infinity);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Math.Abs(finalSize.Width - _layoutWidth) > 0.01)
        {
            EnsureLayout(finalSize.Width);
        }
        var caret = CaretBounds();
        _caret.IsVisible = caret is not null && ShowsCaret;
        _caret.Arrange(caret ?? default);
        _ime.NotifyCursor();
        return finalSize;
    }

    private bool ShowsCaret => IsKeyboardFocusWithin && Owner is { IsReadOnly: false } && Engine is { Selection.IsCollapsed: true };

    private void EnsureLayout(double width)
    {
        var engine = Engine;
        if (engine is null)
        {
            return;
        }
        var blocks = engine.Blocks;
        var widthChanged = Math.Abs(width - _layoutWidth) > 0.01;
        if (!widthChanged && ReferenceEquals(blocks, _laidOut) && !_views.Any(v => v.StyleVersion != _styleVersion) && !PreeditStale())
        {
            return;
        }
        _layoutWidth = width;
        _laidOut = blocks;
        var used = new HashSet<RichTextEditorBlock>(ReferenceEqualityComparer.Instance);
        _views.Clear();
        var caretBlock = engine.Selection.Focus.Block;
        var index = 0;
        foreach (var block in blocks)
        {
            used.Add(block);
            var preedit = IsComposing && index == caretBlock ? _preedit : null;
            if (preedit is null && _cache.TryGetValue(block, out var cached) && cached.StyleVersion == _styleVersion
                && Math.Abs(cached.LayoutWidth - width) < 0.01 && cached.Preedit is null)
            {
                _views.Add(cached);
            }
            else
            {
                var view = Build(block, width, preedit, preedit is null ? 0 : engine.Selection.Focus.Offset);
                if (preedit is null)
                {
                    if (_cache.Remove(block, out var old))
                    {
                        old.Dispose();
                    }
                    _cache[block] = view;
                }
                _views.Add(view);
            }
            index++;
        }
        // Drop layouts of blocks that left the document.
        if (_cache.Count > used.Count)
        {
            var stale = _cache.Keys.Where(key => !used.Contains(key)).ToList();
            foreach (var key in stale)
            {
                _cache[key].Dispose();
                _cache.Remove(key);
            }
        }
        _tops.Clear();
        _markers.Clear();
        var y = VerticalPadding;
        for (var i = 0; i < _views.Count; i++)
        {
            if (i > 0)
            {
                y += Gap(_views[i - 1], _views[i]);
            }
            _tops.Add(y);
            y += _views[i].Height;
        }
        ComputeMarkers();
        _renderedBand = default;
    }

    private bool PreeditStale()
    {
        var engine = Engine!;
        var caret = engine.Selection.Focus.Block;
        for (var i = 0; i < _views.Count; i++)
        {
            var view = _views[i];
            var wanted = IsComposing && i == caret ? _preedit : null;
            if (!string.Equals(view.Preedit, wanted, StringComparison.Ordinal))
            {
                return true;
            }
            if (wanted is not null && view.PreeditAt != engine.Selection.Focus.Offset)
            {
                return true;
            }
        }
        return false;
    }

    private double FontSizeOf(RichTextBlockType type) => type switch
    {
        RichTextBlockType.Heading1 => Heading1FontSize,
        RichTextBlockType.Heading2 => Heading2FontSize,
        RichTextBlockType.Heading3 => Heading3FontSize,
        RichTextBlockType.CodeBlock => CodeFontSize,
        _ => BaseFontSize
    };

    /// <summary>Arc's block spacing with CSS margin collapsing: <c>.6em</c> between blocks, headings <c>1.1em</c> above and <c>.45em</c> below, rules <c>1.1em</c> around, list items <c>.2em</c>.</summary>
    private double Gap(RichTextEditorBlockView previous, RichTextEditorBlockView next)
    {
        var a = previous.Block;
        var b = next.Block;
        if (a.IsList && b.IsList && (b.Level > 0 || a.Level > 0 || a.Type == b.Type))
        {
            return ListItemPaddingEm * BaseFontSize;
        }
        var nextSize = FontSizeOf(b.Type);
        var bottom = a.IsDivider ? 1.1 * BaseFontSize : 0;
        double top;
        if (b.IsDivider)
        {
            top = 1.1 * BaseFontSize;
        }
        else if (a.IsHeading)
        {
            top = 0.45 * nextSize;
        }
        else if (b.IsHeading)
        {
            top = 1.1 * nextSize;
        }
        else
        {
            top = 0.6 * nextSize;
        }
        return Math.Max(top, bottom);
    }

    /// <summary>
    /// List markers in one forward pass: numbers count siblings of the same list (a change of list type at a level
    /// starts a new list), bullets are discs at the outer level and circles inside a bulleted list (Arc's <c>ul ul</c>).
    /// </summary>
    private void ComputeMarkers()
    {
        var counters = new List<(RichTextBlockType Type, int Count)>();
        foreach (var view in _views)
        {
            var block = view.Block;
            if (!block.IsList)
            {
                counters.Clear();
                _markers.Add(null);
                continue;
            }
            var level = Math.Min(block.Level, counters.Count);
            if (counters.Count > level + 1)
            {
                counters.RemoveRange(level + 1, counters.Count - level - 1);
            }
            if (counters.Count == level + 1)
            {
                var (type, count) = counters[level];
                counters[level] = type == block.Type ? (type, count + 1) : (block.Type, 1);
            }
            else
            {
                counters.Add((block.Type, 1));
            }
            if (block.Type == RichTextBlockType.NumberedList)
            {
                _markers.Add(counters[level].Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
                continue;
            }
            var nestedInBullets = false;
            for (var l = 0; l < level; l++)
            {
                if (counters[l].Type == RichTextBlockType.BulletedList)
                {
                    nestedInBullets = true;
                    break;
                }
            }
            _markers.Add(nestedInBullets ? "circle" : "disc");
        }
    }

    private Typeface TypefaceFor(RichTextEditorMarks marks, FontWeight baseWeight)
    {
        var family = (marks & RichTextEditorMarks.Code) != 0 && MonoFontFamily is { } mono ? mono : FontFamily;
        var weight = (marks & RichTextEditorMarks.Bold) != 0 ? FontWeight.SemiBold : baseWeight;
        var style = (marks & RichTextEditorMarks.Italic) != 0 ? FontStyle.Italic : FontStyle.Normal;
        return new Typeface(family, style, weight);
    }

    private IBrush LinkUnderlineBrush
    {
        get
        {
            if (_linkUnderlineBrush is null)
            {
                var accent = (AccentBrush as ISolidColorBrush)?.Color ?? Colors.Gray;
                _linkUnderlineBrush = new ImmutableSolidColorBrush(Color.FromArgb((byte)(accent.A * 0.45), accent.R, accent.G, accent.B));
            }
            return _linkUnderlineBrush;
        }
    }

    internal IBrush SelectionBrush
    {
        get
        {
            if (_selectionBrush is null)
            {
                var accent = (AccentBrush as ISolidColorBrush)?.Color ?? Colors.SteelBlue;
                _selectionBrush = new ImmutableSolidColorBrush(Color.FromArgb((byte)(accent.A * 0.22), accent.R, accent.G, accent.B));
            }
            return _selectionBrush;
        }
    }

    private TextDecorationCollection? Decorations(bool strike, bool link)
    {
        if (!strike && !link)
        {
            return null;
        }
        TextDecoration Strike() => new() { Location = TextDecorationLocation.Strikethrough, StrokeThickness = 1, StrokeThicknessUnit = TextDecorationUnit.Pixel };
        TextDecoration Underline() => new()
        {
            Location = TextDecorationLocation.Underline, StrokeThickness = 1, StrokeThicknessUnit = TextDecorationUnit.Pixel,
            Stroke = LinkUnderlineBrush, StrokeOffset = 1.5, StrokeOffsetUnit = TextDecorationUnit.Pixel
        };
        if (strike && link)
        {
            return _strikeUnderline ??= new TextDecorationCollection { Strike(), Underline() };
        }
        return strike ? _strike ??= new TextDecorationCollection { Strike() } : _underline ??= new TextDecorationCollection { Underline() };
    }

    private TextDecorationCollection PreeditDecorations => _preeditUnderline ??= new TextDecorationCollection
    {
        new TextDecoration { Location = TextDecorationLocation.Underline, StrokeThickness = 1, StrokeThicknessUnit = TextDecorationUnit.Pixel }
    };

    private RichTextEditorBlockView Build(RichTextEditorBlock block, double width, string? preedit, int preeditAt)
    {
        var view = new RichTextEditorBlockView(block) { LayoutWidth = width, StyleVersion = _styleVersion, Preedit = preedit, PreeditAt = preeditAt };
        if (block.IsDivider)
        {
            view.Height = 1;
            view.FontSize = BaseFontSize;
            view.LineHeight = 1;
            return view;
        }
        var size = FontSizeOf(block.Type);
        var lineHeight = size * (block.IsHeading ? 1.25 : block.IsCode ? 1.6 : 1.65);
        var weight = block.IsHeading ? FontWeight.Medium : FontWeight.Normal;
        var tracking = (block.IsHeading ? -0.02 : Tracking) * size;
        var baseBrush = block.Type == RichTextBlockType.Quote ? SecondaryBrush ?? Foreground : Foreground;
        double textX = 0;
        double textY = 0;
        double right = 0;
        switch (block.Type)
        {
            case RichTextBlockType.Quote:
                textX = 16;
                break;
            case RichTextBlockType.BulletedList:
            case RichTextBlockType.NumberedList:
                textX = (block.Level + 1) * (ListIndentEm + ListItemPaddingEm) * BaseFontSize;
                break;
            case RichTextBlockType.CodeBlock:
                textX = 15;
                textY = 13;
                right = 15;
                break;
        }
        view.FontSize = size;
        view.LineHeight = lineHeight;
        view.Weight = weight;
        view.TextX = textX;
        view.TextY = textY;

        var text = preedit is null ? block.Text : block.Text.Insert(Math.Clamp(preeditAt, 0, block.Length), preedit);
        var overrides = new List<ValueSpan<TextRunProperties>>();
        var codeSpans = new List<(int Start, int Length)>();
        if (block.IsCode)
        {
            overrides.Add(new ValueSpan<TextRunProperties>(0, Math.Max(1, text.Length), new GenericTextRunProperties(TypefaceFor(RichTextEditorMarks.Code, weight), size, null, baseBrush)));
        }
        else
        {
            foreach (var span in block.Spans)
            {
                if (span.Marks == RichTextEditorMarks.None && span.Href is null)
                {
                    continue;
                }
                var strike = (span.Marks & RichTextEditorMarks.Strike) != 0;
                var link = span.Href is not null;
                var code = (span.Marks & RichTextEditorMarks.Code) != 0;
                var brush = strike ? SecondaryBrush ?? baseBrush : link ? LinkBrush ?? baseBrush : baseBrush;
                var properties = new GenericTextRunProperties(TypefaceFor(span.Marks, weight), code ? size * 0.86 : size, Decorations(strike, link), brush);
                AddShifted(overrides, span.Start, span.Length, properties, view);
                if (code)
                {
                    codeSpans.Add((view.ToLayout(span.Start), span.Length));
                }
            }
        }
        if (preedit is not null)
        {
            var at = Math.Clamp(preeditAt, 0, block.Length);
            var marks = Engine?.CurrentTypingMarks() ?? RichTextEditorMarks.None;
            overrides.Add(new ValueSpan<TextRunProperties>(at, preedit.Length,
                new GenericTextRunProperties(TypefaceFor(block.IsCode ? RichTextEditorMarks.Code : marks, weight), size, PreeditDecorations, baseBrush)));
        }
        var maxWidth = Math.Max(1, width - textX - right);
        view.Layout = new TextLayout(text, TypefaceFor(block.IsCode ? RichTextEditorMarks.Code : RichTextEditorMarks.None, weight), size, baseBrush,
            TextAlignment.Left, TextWrapping.Wrap, null, null, FlowDirection.LeftToRight, maxWidth, double.PositiveInfinity,
            lineHeight, tracking, 0, null, overrides.Count == 0 ? null : overrides);
        view.Height = view.Layout.Height + textY * 2;
        if (codeSpans.Count > 0)
        {
            var rects = new List<Rect>();
            foreach (var (start, length) in codeSpans)
            {
                foreach (var rect in view.Layout.HitTestTextRange(start, length))
                {
                    var box = size * 0.86 * 1.2 + size * 0.24;
                    rects.Add(new Rect(rect.X - 4, rect.Y + (rect.Height - box) / 2, rect.Width + 8, box));
                }
            }
            view.CodeRects = rects;
        }
        return view;
    }

    private static void AddShifted(List<ValueSpan<TextRunProperties>> overrides, int start, int length, TextRunProperties properties, RichTextEditorBlockView view)
    {
        if (view.Preedit is not { Length: > 0 } preedit || start + length <= view.PreeditAt)
        {
            overrides.Add(new ValueSpan<TextRunProperties>(start, length, properties));
            return;
        }
        if (start >= view.PreeditAt)
        {
            overrides.Add(new ValueSpan<TextRunProperties>(start + preedit.Length, length, properties));
            return;
        }
        var head = view.PreeditAt - start;
        overrides.Add(new ValueSpan<TextRunProperties>(start, head, properties));
        overrides.Add(new ValueSpan<TextRunProperties>(view.PreeditAt + preedit.Length, length - head, properties));
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Geometry
     * -------------------------------------------------------------------------------------------------------- */

    public double BlockTop(int index) => index >= 0 && index < _tops.Count ? _tops[index] : 0;

    /// <summary>The caret rectangle for a position, in surface coordinates (full line box height).</summary>
    public Rect LineRect(RichTextEditorPosition position, bool preeditCursor = false)
    {
        if (position.Block < 0 || position.Block >= _views.Count)
        {
            return new Rect(0, VerticalPadding, 1, BaseFontSize * 1.65);
        }
        var view = _views[position.Block];
        var top = _tops[position.Block];
        if (view.Layout is null)
        {
            return new Rect(0, top, 1, Math.Max(1, view.Height));
        }
        var layoutPosition = view.ToLayout(position.Offset);
        if (preeditCursor && view.Preedit is { } preedit)
        {
            layoutPosition = view.PreeditAt + Math.Clamp(_preeditCursor ?? preedit.Length, 0, preedit.Length);
        }
        var lineIndex = LineOfLayout(view, layoutPosition);
        var line = view.Layout.TextLines[lineIndex];
        var x = line.GetDistanceFromCharacterHit(new CharacterHit(layoutPosition));
        return new Rect(view.TextX + x, top + view.TextY + view.LineTop(lineIndex), 1, line.Height);
    }

    private static int LineOfLayout(RichTextEditorBlockView view, int layoutPosition)
    {
        var lines = view.Layout!.TextLines;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var contentEnd = line.FirstTextSourceIndex + line.Length - line.NewLineLength;
            if (layoutPosition < contentEnd || (layoutPosition == contentEnd && (line.NewLineLength > 0 || i == lines.Count - 1)))
            {
                return i;
            }
        }
        return lines.Count - 1;
    }

    /// <summary>The drawn caret: the line box narrowed to the font's height.</summary>
    public Rect CaretRect(RichTextEditorPosition position, bool preeditCursor = false)
    {
        var line = LineRect(position, preeditCursor);
        var view = position.Block >= 0 && position.Block < _views.Count ? _views[position.Block] : null;
        var size = view?.FontSize ?? BaseFontSize;
        var height = Math.Min(line.Height, size * 1.3);
        return new Rect(Math.Round(line.X - 0.5, 1), line.Y + (line.Height - height) / 2, 1.5, height);
    }

    private Rect? CaretBounds()
    {
        var engine = Engine;
        if (engine is null || _views.Count == 0)
        {
            return null;
        }
        return CaretRect(engine.Selection.Focus, preeditCursor: IsComposing);
    }

    /// <summary>The position under a point in surface coordinates (dividers resolve to the nearest text).</summary>
    public RichTextEditorPosition HitTest(Point point)
    {
        if (_views.Count == 0)
        {
            return new RichTextEditorPosition(0, 0);
        }
        var index = BlockAtY(point.Y);
        var view = _views[index];
        if (view.Layout is null)
        {
            var center = _tops[index] + view.Height / 2;
            var engine = Engine!;
            var target = RichTextEditorEngine.NearestTextual(engine.Blocks, index, forward: point.Y >= center);
            var targetView = _views[target.Block];
            return HitTestInBlock(target.Block, new Point(point.X, point.Y >= center ? _tops[target.Block] : _tops[target.Block] + targetView.Height - 1));
        }
        return HitTestInBlock(index, point);
    }

    private int BlockAtY(double y)
    {
        var lo = 0;
        var hi = _tops.Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) >> 1;
            if (_tops[mid] <= y)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }
        // In the gap below a block, the nearer edge wins.
        if (lo + 1 < _tops.Count)
        {
            var bottom = _tops[lo] + _views[lo].Height;
            if (y > bottom && y - bottom > _tops[lo + 1] - y)
            {
                return lo + 1;
            }
        }
        return lo;
    }

    public RichTextEditorPosition HitTestInBlock(int index, Point point)
    {
        var view = _views[index];
        if (view.Layout is null)
        {
            return new RichTextEditorPosition(index, 0);
        }
        var local = new Point(point.X - view.TextX, point.Y - _tops[index] - view.TextY);
        var lines = view.Layout.TextLines;
        var lineIndex = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            lineIndex = i;
            if (local.Y < view.LineTop(i + 1))
            {
                break;
            }
        }
        return new RichTextEditorPosition(index, view.FromLayout(PositionInLine(view, lineIndex, local.X)));
    }

    private static int PositionInLine(RichTextEditorBlockView view, int lineIndex, double x)
    {
        var lines = view.Layout!.TextLines;
        var line = lines[lineIndex];
        var hit = line.GetCharacterHitFromDistance(x);
        var position = hit.FirstCharacterIndex + hit.TrailingLength;
        var start = line.FirstTextSourceIndex;
        var contentEnd = start + line.Length - line.NewLineLength;
        if (line.NewLineLength == 0 && lineIndex < lines.Count - 1 && line.TrailingWhitespaceLength > 0)
        {
            // Past the end of a wrapped line: stay before its trailing space, on this line.
            contentEnd -= line.TrailingWhitespaceLength;
        }
        return Math.Clamp(position, start, Math.Max(start, contentEnd));
    }

    /// <summary>The position one visual line up or down from <paramref name="from"/>, at <paramref name="goalX"/>.</summary>
    public RichTextEditorPosition MoveVertical(RichTextEditorPosition from, double goalX, int direction)
    {
        var blocks = Engine!.Blocks;
        var view = _views[from.Block];
        var line = view.Layout is null ? 0 : view.LineOf(from.Offset);
        if (view.Layout is not null && (direction < 0 ? line > 0 : line < view.LineCount - 1))
        {
            var target = line + direction;
            return new RichTextEditorPosition(from.Block, view.FromLayout(PositionInLine(view, target, goalX - view.TextX)));
        }
        for (var i = from.Block + direction; i >= 0 && i < _views.Count; i += direction)
        {
            if (!blocks[i].IsTextual)
            {
                continue;
            }
            var next = _views[i];
            var targetLine = direction < 0 ? next.LineCount - 1 : 0;
            return new RichTextEditorPosition(i, next.FromLayout(PositionInLine(next, targetLine, goalX - next.TextX)));
        }
        return direction < 0 ? new RichTextEditorPosition(from.Block, 0) : new RichTextEditorPosition(from.Block, blocks[from.Block].Length);
    }

    /// <summary>Start and end of the visual line holding <paramref name="position"/>.</summary>
    public (int Start, int End) LineBounds(RichTextEditorPosition position)
    {
        var view = _views[position.Block];
        if (view.Layout is null)
        {
            return (0, 0);
        }
        var lines = view.Layout.TextLines;
        var index = view.LineOf(position.Offset);
        var line = lines[index];
        var start = line.FirstTextSourceIndex;
        var end = start + line.Length - line.NewLineLength;
        if (line.NewLineLength == 0 && index < lines.Count - 1 && line.TrailingWhitespaceLength > 0)
        {
            end -= line.TrailingWhitespaceLength;
        }
        return (view.FromLayout(start), view.FromLayout(end));
    }

    /// <summary>
    /// The first line's rectangle and the bounding box of a range (Arc's getClientRects()[0] and getBoundingClientRect()),
    /// measuring only the two end blocks so long selections stay cheap.
    /// </summary>
    public (Rect First, Rect Whole)? SelectionBounds(RichTextEditorPosition start, RichTextEditorPosition end)
    {
        var engine = Engine;
        if (engine is null || start.Block >= _views.Count || end.Block >= _views.Count)
        {
            return null;
        }
        var head = RangeRects(start, start.Block == end.Block ? end : new RichTextEditorPosition(start.Block, engine.Blocks[start.Block].Length));
        if (start.Block == end.Block)
        {
            if (head.Count == 0)
            {
                return null;
            }
            var union = head[0];
            foreach (var rect in head)
            {
                union = union.Union(rect);
            }
            return (head[0], union);
        }
        var tail = RangeRects(new RichTextEditorPosition(end.Block, 0), end);
        var first = head.Count > 0 ? head[0] : LineRect(start);
        var whole = first;
        foreach (var rect in head.Concat(tail))
        {
            whole = whole.Union(rect);
        }
        for (var i = start.Block + 1; i < end.Block; i++)
        {
            var view = _views[i];
            var width = view.Layout?.WidthIncludingTrailingWhitespace ?? Bounds.Width;
            whole = whole.Union(new Rect(view.TextX, _tops[i], Math.Max(1, width), view.Height));
        }
        if (tail.Count == 0)
        {
            whole = whole.Union(LineRect(end));
        }
        return (first, whole);
    }

    /// <summary>Rectangles covering a range, in surface coordinates, line by line (for toolbar placement).</summary>
    public List<Rect> RangeRects(RichTextEditorPosition start, RichTextEditorPosition end)
    {
        var rects = new List<Rect>();
        var engine = Engine;
        if (engine is null)
        {
            return rects;
        }
        foreach (var (index, from, to) in RichTextEditorEngine.Segments(engine.Blocks, start, end))
        {
            if (index >= _views.Count)
            {
                break;
            }
            var view = _views[index];
            if (view.Layout is null || from == to)
            {
                continue;
            }
            foreach (var rect in view.Layout.HitTestTextRange(view.ToLayout(from), to - from))
            {
                rects.Add(rect.Translate(new Vector(view.TextX, _tops[index] + view.TextY)));
            }
        }
        return rects;
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Rendering
     * -------------------------------------------------------------------------------------------------------- */

    private void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        _viewport = e.EffectiveViewport;
        if (_renderedBand != default && (_viewport.Top < _renderedBand.Top || _viewport.Bottom > _renderedBand.Bottom))
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Brushes.Transparent, bounds);
        var engine = Engine;
        if (engine is null || _views.Count == 0)
        {
            return;
        }
        var band = bounds;
        if (_viewport.Height > 0 && _viewport.Width > 0)
        {
            band = new Rect(0, _viewport.Top - _viewport.Height, bounds.Width, _viewport.Height * 3);
        }
        _renderedBand = _viewport.Height > 0 ? band : default;
        var first = BlockAtY(Math.Max(0, band.Top));
        var selection = engine.Selection;
        var focused = IsKeyboardFocusWithin || Owner?.KeepsSelectionVisible == true;
        var showSelection = !selection.IsCollapsed && focused;
        var blocks = engine.Blocks;
        var width = bounds.Width;
        for (var i = Math.Max(0, first - 1); i < _views.Count; i++)
        {
            var top = _tops[i];
            if (top > band.Bottom)
            {
                break;
            }
            var view = _views[i];
            var block = view.Block;
            DrawDecoration(context, i, view, top, width);
            if (view.Layout is null)
            {
                if (showSelection && selection.Start.Block <= i && selection.End.Block >= i)
                {
                    context.FillRectangle(SelectionBrush, new Rect(0, top - 3, width, 7));
                }
                continue;
            }
            var origin = new Point(view.TextX, top + view.TextY);
            if (view.CodeRects is { } codeRects && CodeBackground is { } codeBrush)
            {
                foreach (var rect in codeRects)
                {
                    context.DrawRectangle(codeBrush, null, new RoundedRect(rect.Translate(origin), 6));
                }
            }
            if (showSelection && selection.Start.Block <= i && selection.End.Block >= i)
            {
                DrawSelection(context, i, view, origin, selection);
            }
            view.Layout.Draw(context, origin);
            DrawPlaceholder(context, i, view, origin, blocks);
        }
    }

    private void DrawDecoration(DrawingContext context, int index, RichTextEditorBlockView view, double top, double width)
    {
        var block = view.Block;
        switch (block.Type)
        {
            case RichTextBlockType.Divider:
                if (RuleBrush is { } rule)
                {
                    context.FillRectangle(rule, new Rect(0, top, width, 1));
                }
                break;
            case RichTextBlockType.Quote:
                if (QuoteBarBrush is { } bar)
                {
                    context.FillRectangle(bar, new Rect(0, top, 2, view.Height));
                }
                break;
            case RichTextBlockType.CodeBlock:
                var pen = CodeBorderBrush is { } border ? new ImmutablePen(border.ToImmutable(), 1) : null;
                context.DrawRectangle(CodeBackground, pen, new RoundedRect(new Rect(0.5, top + 0.5, Math.Max(0, width - 1), Math.Max(0, view.Height - 1)), 14));
                break;
            case RichTextBlockType.BulletedList:
            case RichTextBlockType.NumberedList:
                DrawMarker(context, index, view, top);
                break;
        }
    }

    private void DrawMarker(DrawingContext context, int index, RichTextEditorBlockView view, double top)
    {
        var marker = index < _markers.Count ? _markers[index] : null;
        if (marker is null || view.Layout is null || MutedBrush is not { } brush)
        {
            return;
        }
        var size = BaseFontSize;
        var itemLeft = view.TextX - ListItemPaddingEm * size;
        var line = view.Layout.TextLines[0];
        if (marker is "disc" or "circle")
        {
            var radius = size * 0.165;
            var center = new Point(itemLeft - size * 0.62, top + view.TextY + line.Baseline - size * 0.31);
            if (marker == "disc")
            {
                context.DrawEllipse(brush, null, center, radius, radius);
            }
            else
            {
                context.DrawEllipse(null, new ImmutablePen(brush.ToImmutable(), 1), center, radius - 0.5, radius - 0.5);
            }
            return;
        }
        if (!_markerLayouts.TryGetValue(marker, out var layout))
        {
            layout = new TextLayout(marker, new Typeface(FontFamily), size, brush, TextAlignment.Left, TextWrapping.NoWrap,
                lineHeight: view.LineHeight, fontFeatures: new FontFeatureCollection { new FontFeature { Tag = "tnum", Value = 1 } });
            _markerLayouts[marker] = layout;
        }
        layout.Draw(context, new Point(itemLeft - size * 0.3 - layout.WidthIncludingTrailingWhitespace, top + view.TextY));
    }

    private void DrawSelection(DrawingContext context, int index, RichTextEditorBlockView view, Point origin, RichTextEditorSelection selection)
    {
        var from = index == selection.Start.Block ? selection.Start.Offset : 0;
        var to = index == selection.End.Block ? selection.End.Offset : view.Block.Length;
        var brush = SelectionBrush;
        if (to > from)
        {
            foreach (var rect in view.Layout!.HitTestTextRange(view.ToLayout(from), to - from))
            {
                context.FillRectangle(brush, rect.Translate(origin));
            }
        }
        if (index < selection.End.Block)
        {
            // The selection runs past the block end: show the line break as a narrow selected cell.
            var end = LineRect(new RichTextEditorPosition(index, view.Block.Length));
            context.FillRectangle(brush, new Rect(end.X, end.Y, view.FontSize * 0.32, end.Height));
        }
    }

    private void DrawPlaceholder(DrawingContext context, int index, RichTextEditorBlockView view, Point origin, ImmutableList<RichTextEditorBlock> blocks)
    {
        var owner = Owner;
        if (owner is null || !view.Block.IsEmpty || view.Preedit is { Length: > 0 } || view.Block.IsCode)
        {
            return;
        }
        var engine = Engine!;
        var empty = owner.IsDocumentEmpty;
        var focused = IsKeyboardFocusWithin && !owner.IsReadOnly;
        var hinted = focused && engine.Selection.IsCollapsed && engine.Selection.Focus.Block == index;
        string? text = null;
        if (empty && (index == 0 || hinted))
        {
            text = owner.Placeholder;
        }
        else if (hinted)
        {
            text = owner.BlockHint;
        }
        if (string.IsNullOrEmpty(text) || MutedBrush is not { } brush)
        {
            return;
        }
        var key = (text, view.FontSize, view.Weight, _styleVersion);
        if (_placeholderLayout is null || _placeholderKey != key)
        {
            _placeholderLayout?.Dispose();
            _placeholderLayout = new TextLayout(text, new Typeface(FontFamily, FontStyle.Normal, view.Weight), view.FontSize, brush,
                TextAlignment.Left, TextWrapping.NoWrap, lineHeight: view.LineHeight, letterSpacing: (view.Block.IsHeading ? -0.02 : Tracking) * view.FontSize);
            _placeholderKey = key;
        }
        using (context.PushClip(new Rect(origin.X, origin.Y, Math.Max(0, Bounds.Width - origin.X), _placeholderLayout.Height)))
        {
            _placeholderLayout.Draw(context, origin);
        }
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Input
     * -------------------------------------------------------------------------------------------------------- */

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _caret.Restart();
        InvalidateArrange();
        InvalidateVisual();
        Owner?.OnSurfaceFocusChanged(true);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (IsComposing)
        {
            SetPreedit(null, null);
        }
        InvalidateArrange();
        InvalidateVisual();
        Owner?.OnSurfaceFocusChanged(false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && !IsComposing)
        {
            Owner?.HandleKeyDown(e);
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || string.IsNullOrEmpty(e.Text))
        {
            return;
        }
        var composition = _composed || IsComposing;
        if (IsComposing)
        {
            _preedit = null;
            _preeditCursor = null;
        }
        _composed = false;
        Owner?.HandleTextInput(e.Text!, composition);
        e.Handled = true;
    }

    internal void SetPreedit(string? text, int? cursor)
    {
        var next = string.IsNullOrEmpty(text) ? null : text;
        if (next is null && _preedit is not null)
        {
            // A commit usually follows in the same input batch; it must not run Markdown shortcuts.
            _composed = true;
            Dispatcher.UIThread.Post(() => _composed = false, DispatcherPriority.Background);
        }
        if (string.Equals(next, _preedit, StringComparison.Ordinal) && cursor == _preeditCursor)
        {
            return;
        }
        if (next is not null && _preedit is null)
        {
            Owner?.OnCompositionStarted();
        }
        _preedit = next;
        _preeditCursor = cursor;
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var owner = Owner;
        var engine = Engine;
        if (owner is null || engine is null)
        {
            return;
        }
        var point = e.GetCurrentPoint(this);
        if (e.Pointer.Type == PointerType.Touch)
        {
            _touchStart = point.Position;
            return;
        }
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }
        Focus(NavigationMethod.Pointer);
        if (IsComposing)
        {
            // A click ends the composition where it is; the input method starts over at the new caret.
            SetPreedit(null, null);
            _ime.Reset();
        }
        owner.OnSurfacePointerDown();
        var position = HitTest(point.Position);
        GoalX = null;
        _dragClicks = e.ClickCount;
        if (e.ClickCount == 2)
        {
            var (start, end) = RichTextEditorText.WordAt(engine.Blocks[position.Block].Text, position.Offset);
            engine.SetSelection(RichTextEditorSelection.Range(position.Block, start, end));
            _dragOrigin = position;
        }
        else if (e.ClickCount >= 3)
        {
            engine.SetSelection(RichTextEditorSelection.Range(position.Block, 0, engine.Blocks[position.Block].Length));
            _dragOrigin = position;
        }
        else if ((e.KeyModifiers & KeyModifiers.Shift) != 0)
        {
            engine.SetSelection(engine.Selection with { Focus = position });
        }
        else
        {
            engine.SetSelection(RichTextEditorSelection.Caret(position));
        }
        IsDragging = true;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var engine = Engine;
        if (!IsDragging || engine is null || !ReferenceEquals(e.Pointer.Captured, this))
        {
            return;
        }
        var position = HitTest(e.GetPosition(this));
        if (_dragClicks >= 2)
        {
            // Word and block drags extend by whole words or blocks from the original one.
            var origin = _dragClicks == 2 ? RichTextEditorText.WordAt(engine.Blocks[_dragOrigin.Block].Text, _dragOrigin.Offset) : (0, engine.Blocks[_dragOrigin.Block].Length);
            var forward = position >= _dragOrigin;
            var text = engine.Blocks[position.Block].Text;
            var extent = _dragClicks == 2 ? RichTextEditorText.WordAt(text, position.Offset) : (0, text.Length);
            var anchor = new RichTextEditorPosition(_dragOrigin.Block, forward ? origin.Item1 : origin.Item2);
            var focus = new RichTextEditorPosition(position.Block, forward ? extent.Item2 : extent.Item1);
            engine.SetSelection(new RichTextEditorSelection(anchor, focus));
        }
        else
        {
            engine.SetSelection(engine.Selection with { Focus = position });
        }
        Owner?.RevealCaret();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var engine = Engine;
        if (e.Pointer.Type == PointerType.Touch && _touchStart is { } start && engine is not null)
        {
            var position = e.GetPosition(this);
            _touchStart = null;
            if (Math.Abs(position.X - start.X) < 8 && Math.Abs(position.Y - start.Y) < 8)
            {
                Focus(NavigationMethod.Pointer);
                Owner?.OnSurfacePointerDown();
                engine.SetSelection(RichTextEditorSelection.Caret(HitTest(position)));
                Owner?.OnDragEnded();
            }
            return;
        }
        if (IsDragging)
        {
            IsDragging = false;
            if (ReferenceEquals(e.Pointer.Captured, this))
            {
                e.Pointer.Capture(null);
            }
            Owner?.OnDragEnded();
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (IsDragging)
        {
            IsDragging = false;
            Owner?.OnDragEnded();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RichTextEditorSurfaceAutomationPeer(this);

    /* ----------------------------------------------------------------------------------------------------------
     * IME support
     * -------------------------------------------------------------------------------------------------------- */

    internal string ImeSurroundingText
    {
        get
        {
            var engine = Engine;
            return engine is null ? string.Empty : engine.Blocks[engine.Selection.Focus.Block].Text;
        }
    }

    internal TextSelection ImeSelection
    {
        get
        {
            var engine = Engine;
            if (engine is null)
            {
                return default;
            }
            var selection = engine.Selection;
            if (selection.Anchor.Block != selection.Focus.Block)
            {
                return new TextSelection(selection.Focus.Offset, selection.Focus.Offset);
            }
            return new TextSelection(selection.Anchor.Offset, selection.Focus.Offset);
        }
    }

    internal void ImeSetSelection(TextSelection value)
    {
        var engine = Engine;
        if (engine is null)
        {
            return;
        }
        var block = engine.Selection.Focus.Block;
        engine.SetSelection(new RichTextEditorSelection(new RichTextEditorPosition(block, value.Start), new RichTextEditorPosition(block, value.End)));
    }

    internal Rect ImeCursorRect
    {
        get
        {
            var caret = CaretBounds();
            return caret ?? default;
        }
    }
}

/// <summary>The self-drawn caret: a thin accent bar that blinks without re-rendering the text.</summary>
internal sealed class RichTextEditorCaret : Control
{
    private readonly DispatcherTimer _timer;

    public RichTextEditorCaret()
    {
        IsHitTestVisible = false;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(530) };
        _timer.Tick += (_, _) => Opacity = Opacity > 0.5 ? 0 : 1;
    }

    private IBrush? _brush;

    public IBrush? Brush
    {
        get => _brush;
        set
        {
            _brush = value;
            InvalidateVisual();
        }
    }

    /// <summary>Shows the caret solid and restarts the blink (after every move or edit).</summary>
    public void Restart()
    {
        Opacity = 1;
        _timer.Stop();
        if (IsVisible)
        {
            _timer.Start();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty)
        {
            Restart();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    public override void Render(DrawingContext context)
    {
        if (_brush is not null)
        {
            context.FillRectangle(_brush, new Rect(Bounds.Size));
        }
    }
}

/// <summary>Connects the platform input method (composition, candidate window placement, surrounding text) to the surface.</summary>
internal sealed class RichTextEditorImeClient : TextInputMethodClient
{
    private readonly RichTextEditorSurface _surface;

    public RichTextEditorImeClient(RichTextEditorSurface surface)
    {
        _surface = surface;
    }

    public override Visual TextViewVisual => _surface;

    public override bool SupportsPreedit => true;

    public override bool SupportsSurroundingText => true;

    public override string SurroundingText => _surface.ImeSurroundingText;

    public override Rect CursorRectangle => _surface.ImeCursorRect;

    public override TextSelection Selection
    {
        get => _surface.ImeSelection;
        set => _surface.ImeSetSelection(value);
    }

    public override void SetPreeditText(string? preeditText) => SetPreeditText(preeditText, null);

    public override void SetPreeditText(string? preeditText, int? cursorPos) => _surface.SetPreedit(preeditText, cursorPos);

    public void NotifyCursor() => RaiseCursorRectangleChanged();

    public void NotifySurroundingText() => RaiseSurroundingTextChanged();

    public void NotifySelection() => RaiseSelectionChanged();

    public void Reset() => RequestReset();
}

/// <summary>Exposes the surface as a multi-line edit field whose value is the plain text.</summary>
internal sealed class RichTextEditorSurfaceAutomationPeer : ControlAutomationPeer, Avalonia.Automation.Provider.IValueProvider
{
    public RichTextEditorSurfaceAutomationPeer(RichTextEditorSurface owner) : base(owner)
    {
    }

    private RichTextEditor? Editor => ((RichTextEditorSurface)Owner).Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit;

    protected override string? GetNameCore() => Editor is { } editor ? Avalonia.Automation.AutomationProperties.GetName(editor) ?? "Editor" : base.GetNameCore();

    protected override string? GetHelpTextCore() => Editor is { } editor ? Avalonia.Automation.AutomationProperties.GetHelpText(editor) : base.GetHelpTextCore();

    public bool IsReadOnly => Editor?.IsReadOnly ?? true;

    public string? Value => Editor?.GetText();

    public void SetValue(string? value)
    {
        if (Editor is { IsReadOnly: false } editor)
        {
            editor.SetMarkdown(value ?? string.Empty);
        }
    }
}
