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
internal sealed partial class RichTextEditorSurface : Control
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
    public static readonly StyledProperty<IBrush?> SyntaxCommentBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(SyntaxCommentBrush));
    public static readonly StyledProperty<IBrush?> SyntaxStringBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(SyntaxStringBrush));
    public static readonly StyledProperty<IBrush?> SyntaxNumberBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(SyntaxNumberBrush));
    public static readonly StyledProperty<IBrush?> SyntaxKeywordBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(SyntaxKeywordBrush));
    public static readonly StyledProperty<IBrush?> SyntaxTypeBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(SyntaxTypeBrush));
    public static readonly StyledProperty<IBrush?> SyntaxFunctionBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(SyntaxFunctionBrush));
    public static readonly StyledProperty<IBrush?> SyntaxPropertyBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(SyntaxPropertyBrush));
    public static readonly StyledProperty<IBrush?> SyntaxTagBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(SyntaxTagBrush));
    public static readonly StyledProperty<IBrush?> SyntaxPunctuationBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(SyntaxPunctuationBrush));
    public static readonly StyledProperty<IBrush?> SyntaxInsertedBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(SyntaxInsertedBrush));
    public static readonly StyledProperty<IBrush?> SyntaxDeletedBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(SyntaxDeletedBrush));
    public static readonly StyledProperty<bool> ShowCodeHeaderProperty = AvaloniaProperty.Register<RichTextEditorSurface, bool>(nameof(ShowCodeHeader));
    public static readonly StyledProperty<IBrush?> CheckBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(CheckBrush));
    public static readonly StyledProperty<IBrush?> TextGrayBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(TextGrayBrush));
    public static readonly StyledProperty<IBrush?> TextRedBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(TextRedBrush));
    public static readonly StyledProperty<IBrush?> TextOrangeBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(TextOrangeBrush));
    public static readonly StyledProperty<IBrush?> TextYellowBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(TextYellowBrush));
    public static readonly StyledProperty<IBrush?> TextGreenBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(TextGreenBrush));
    public static readonly StyledProperty<IBrush?> TextBlueBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(TextBlueBrush));
    public static readonly StyledProperty<IBrush?> TextPurpleBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(TextPurpleBrush));
    public static readonly StyledProperty<IBrush?> FillGrayBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(FillGrayBrush));
    public static readonly StyledProperty<IBrush?> FillRedBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(FillRedBrush));
    public static readonly StyledProperty<IBrush?> FillOrangeBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(FillOrangeBrush));
    public static readonly StyledProperty<IBrush?> FillYellowBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(FillYellowBrush));
    public static readonly StyledProperty<IBrush?> FillGreenBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(FillGreenBrush));
    public static readonly StyledProperty<IBrush?> FillBlueBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(FillBlueBrush));
    public static readonly StyledProperty<IBrush?> FillPurpleBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(FillPurpleBrush));
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
    public IBrush? SyntaxCommentBrush { get => GetValue(SyntaxCommentBrushProperty); set => SetValue(SyntaxCommentBrushProperty, value); }
    public IBrush? SyntaxStringBrush { get => GetValue(SyntaxStringBrushProperty); set => SetValue(SyntaxStringBrushProperty, value); }
    public IBrush? SyntaxNumberBrush { get => GetValue(SyntaxNumberBrushProperty); set => SetValue(SyntaxNumberBrushProperty, value); }
    public IBrush? SyntaxKeywordBrush { get => GetValue(SyntaxKeywordBrushProperty); set => SetValue(SyntaxKeywordBrushProperty, value); }
    public IBrush? SyntaxTypeBrush { get => GetValue(SyntaxTypeBrushProperty); set => SetValue(SyntaxTypeBrushProperty, value); }
    public IBrush? SyntaxFunctionBrush { get => GetValue(SyntaxFunctionBrushProperty); set => SetValue(SyntaxFunctionBrushProperty, value); }
    public IBrush? SyntaxPropertyBrush { get => GetValue(SyntaxPropertyBrushProperty); set => SetValue(SyntaxPropertyBrushProperty, value); }
    public IBrush? SyntaxTagBrush { get => GetValue(SyntaxTagBrushProperty); set => SetValue(SyntaxTagBrushProperty, value); }
    public IBrush? SyntaxPunctuationBrush { get => GetValue(SyntaxPunctuationBrushProperty); set => SetValue(SyntaxPunctuationBrushProperty, value); }
    public IBrush? SyntaxInsertedBrush { get => GetValue(SyntaxInsertedBrushProperty); set => SetValue(SyntaxInsertedBrushProperty, value); }
    public IBrush? SyntaxDeletedBrush { get => GetValue(SyntaxDeletedBrushProperty); set => SetValue(SyntaxDeletedBrushProperty, value); }

    /// <summary>True when every code block shows its language header (the Docs profile); otherwise only blocks with a language do.</summary>
    public bool ShowCodeHeader { get => GetValue(ShowCodeHeaderProperty); set => SetValue(ShowCodeHeaderProperty, value); }

    private IBrush? SyntaxBrushOf(CodeBlockSyntaxKind kind) => kind switch
    {
        CodeBlockSyntaxKind.Comment => SyntaxCommentBrush,
        CodeBlockSyntaxKind.String => SyntaxStringBrush,
        CodeBlockSyntaxKind.Number => SyntaxNumberBrush,
        CodeBlockSyntaxKind.Keyword => SyntaxKeywordBrush,
        CodeBlockSyntaxKind.Type => SyntaxTypeBrush,
        CodeBlockSyntaxKind.Function => SyntaxFunctionBrush,
        CodeBlockSyntaxKind.Property => SyntaxPropertyBrush,
        CodeBlockSyntaxKind.Tag => SyntaxTagBrush,
        CodeBlockSyntaxKind.Punctuation => SyntaxPunctuationBrush,
        CodeBlockSyntaxKind.Inserted => SyntaxInsertedBrush,
        CodeBlockSyntaxKind.Deleted => SyntaxDeletedBrush,
        _ => null
    };

    public IBrush? CheckBrush { get => GetValue(CheckBrushProperty); set => SetValue(CheckBrushProperty, value); }
    public IBrush? TextGrayBrush { get => GetValue(TextGrayBrushProperty); set => SetValue(TextGrayBrushProperty, value); }
    public IBrush? TextRedBrush { get => GetValue(TextRedBrushProperty); set => SetValue(TextRedBrushProperty, value); }
    public IBrush? TextOrangeBrush { get => GetValue(TextOrangeBrushProperty); set => SetValue(TextOrangeBrushProperty, value); }
    public IBrush? TextYellowBrush { get => GetValue(TextYellowBrushProperty); set => SetValue(TextYellowBrushProperty, value); }
    public IBrush? TextGreenBrush { get => GetValue(TextGreenBrushProperty); set => SetValue(TextGreenBrushProperty, value); }
    public IBrush? TextBlueBrush { get => GetValue(TextBlueBrushProperty); set => SetValue(TextBlueBrushProperty, value); }
    public IBrush? TextPurpleBrush { get => GetValue(TextPurpleBrushProperty); set => SetValue(TextPurpleBrushProperty, value); }
    public IBrush? FillGrayBrush { get => GetValue(FillGrayBrushProperty); set => SetValue(FillGrayBrushProperty, value); }
    public IBrush? FillRedBrush { get => GetValue(FillRedBrushProperty); set => SetValue(FillRedBrushProperty, value); }
    public IBrush? FillOrangeBrush { get => GetValue(FillOrangeBrushProperty); set => SetValue(FillOrangeBrushProperty, value); }
    public IBrush? FillYellowBrush { get => GetValue(FillYellowBrushProperty); set => SetValue(FillYellowBrushProperty, value); }
    public IBrush? FillGreenBrush { get => GetValue(FillGreenBrushProperty); set => SetValue(FillGreenBrushProperty, value); }
    public IBrush? FillBlueBrush { get => GetValue(FillBlueBrushProperty); set => SetValue(FillBlueBrushProperty, value); }
    public IBrush? FillPurpleBrush { get => GetValue(FillPurpleBrushProperty); set => SetValue(FillPurpleBrushProperty, value); }

    /// <summary>The text color brush of a palette color (Gray to Purple), or null for <see cref="RichTextColor.Default"/>.</summary>
    internal IBrush? TextBrushOf(RichTextColor color) => color switch
    {
        RichTextColor.Gray => TextGrayBrush,
        RichTextColor.Red => TextRedBrush,
        RichTextColor.Orange => TextOrangeBrush,
        RichTextColor.Yellow => TextYellowBrush,
        RichTextColor.Green => TextGreenBrush,
        RichTextColor.Blue => TextBlueBrush,
        RichTextColor.Purple => TextPurpleBrush,
        _ => null
    };

    internal IBrush? FillBrushOf(RichTextColor color) => color switch
    {
        RichTextColor.Gray => FillGrayBrush,
        RichTextColor.Red => FillRedBrush,
        RichTextColor.Orange => FillOrangeBrush,
        RichTextColor.Yellow => FillYellowBrush,
        RichTextColor.Green => FillGreenBrush,
        RichTextColor.Blue => FillBlueBrush,
        RichTextColor.Purple => FillPurpleBrush,
        _ => null
    };

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
    // Render draws the same few pens on every pass (code frames, markers, the selected image): one immutable pen per
    // slot, rebuilt only when its brush object changes, instead of a new pen per block per frame.
    private readonly IBrush?[] _penBrushes = new IBrush?[PenSlotCount];
    private readonly IPen?[] _pens = new IPen?[PenSlotCount];
    private readonly Dictionary<(double Radius, bool Collapsed), StreamGeometry> _toggleArrows = new();
    private static readonly StreamGeometry s_codeChevron = BuildCodeChevron();
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
    private readonly TextDecorationCollection?[] _decorations = new TextDecorationCollection?[4];
    private TextDecorationCollection? _preeditUnderline;
    private Point? _touchStart;
    private int _dragClicks;
    private RichTextEditorBlock? _resizeBlock;
    private int _resizeIndex;
    private double _resizeWidth;
    private double _resizeStartX;
    private double _resizeStartWidth;
    private RichTextEditorPosition _dragOrigin;
    private readonly List<TableBox> _tables = new();
    private (int Start, int Border, double StartX, double[] StartWidths, double[] Widths)? _columnDrag;
    private GripDrag? _gripDrag;
    private int _hoverTable = -1;
    private const double ColumnGrip = 4;

    /// <summary>A laid out table: its first cell, its size and where its columns and rows start (each array ends with the far edge).</summary>
    private readonly record struct TableBox(int Start, int Rows, int Cols, double Top, double Bottom, double[] Offsets, double[] RowTops);

    /// <summary>A press on a row or column grip: a click selects the row or column, a drag moves it to the gap under the pointer.</summary>
    private sealed record GripDrag(int Start, bool Column, int Index, Point From)
    {
        public bool Moved { get; set; }

        public int Gap { get; set; } = -1;
    }

    private static readonly Cursor s_ibeamCursor = new(StandardCursorType.Ibeam);
    private static readonly Cursor s_handCursor = new(StandardCursorType.Hand);
    private static readonly Cursor s_resizeCursor = new(StandardCursorType.BottomRightCorner);
    private static readonly Cursor s_columnCursor = new(StandardCursorType.SizeWestEast);

    static RichTextEditorSurface()
    {
        FocusableProperty.OverrideDefaultValue<RichTextEditorSurface>(true);
        CursorProperty.OverrideDefaultValue<RichTextEditorSurface>(s_ibeamCursor);
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
        if (property == CheckBrushProperty || property == ShowCodeHeaderProperty || property.Name.StartsWith("Syntax", StringComparison.Ordinal) && property.Name.EndsWith("Brush", StringComparison.Ordinal)
            || property.Name.StartsWith("Text", StringComparison.Ordinal) && property.Name.EndsWith("Brush", StringComparison.Ordinal)
            || property.Name.StartsWith("Fill", StringComparison.Ordinal) && property.Name.EndsWith("Brush", StringComparison.Ordinal))
        {
            InvalidateStyle();
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
        Array.Clear(_decorations);
        _preeditUnderline = null;
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

    private bool ShowsCaret => IsKeyboardFocusWithin && Owner is { IsReadOnly: false } && Engine is { Selection.IsCollapsed: true, IsImageSelected: false };

    private const double ResizeGrip = 14;

    /// <summary>An image finished loading (or failed): its blocks lay out again at the picture's size.</summary>
    internal void OnImageChanged(string src)
    {
        foreach (var view in _cache.Values)
        {
            if (view.Block.IsImage && string.Equals(view.Block.Src, src, StringComparison.Ordinal))
            {
                view.StyleVersion = -1;
            }
        }
        InvalidateMeasure();
        InvalidateVisual();
    }
}
