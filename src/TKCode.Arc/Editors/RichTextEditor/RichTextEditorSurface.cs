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
    public static readonly StyledProperty<IBrush?> SyntaxCommentBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(SyntaxCommentBrush));
    public static readonly StyledProperty<IBrush?> SyntaxStringBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(SyntaxStringBrush));
    public static readonly StyledProperty<IBrush?> SyntaxNumberBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(SyntaxNumberBrush));
    public static readonly StyledProperty<IBrush?> SyntaxKeywordBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(SyntaxKeywordBrush));
    public static readonly StyledProperty<IBrush?> SyntaxTypeBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(SyntaxTypeBrush));
    public static readonly StyledProperty<IBrush?> SyntaxFunctionBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(SyntaxFunctionBrush));
    public static readonly StyledProperty<IBrush?> SyntaxPropertyBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(SyntaxPropertyBrush));
    public static readonly StyledProperty<IBrush?> SyntaxTagBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(SyntaxTagBrush));
    public static readonly StyledProperty<IBrush?> SyntaxPunctuationBrushProperty = AvaloniaProperty.Register<RichTextEditorSurface, IBrush?>(nameof(SyntaxPunctuationBrush));
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

    /// <summary>The corner grip of the selected image at <paramref name="index"/>: dragging it resizes the picture.</summary>
    private Rect ResizeGripRect(int index)
    {
        var view = _views[index];
        var corner = new Point(view.ImageSize.Width, _tops[index] + view.ImageSize.Height);
        return new Rect(corner.X - ResizeGrip, corner.Y - ResizeGrip, ResizeGrip + 4, ResizeGrip + 4);
    }

    private bool OverResizeGrip(Point point, out int index)
    {
        index = -1;
        var engine = Engine;
        if (engine is not { IsImageSelected: true } || Owner is not { IsReadOnly: false })
        {
            return false;
        }
        index = engine.Selection.Focus.Block;
        return index < _views.Count && ResizeGripRect(index).Contains(point);
    }

    private void UpdateResize(double x)
    {
        _resizeWidth = Math.Clamp(Math.Round(_resizeStartWidth + x - _resizeStartX), 48, Math.Max(48, Bounds.Width));
        if (_resizeBlock is not null && _cache.TryGetValue(_resizeBlock, out var view))
        {
            view.StyleVersion = -1;
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void EndResize(bool commit)
    {
        var block = _resizeBlock;
        _resizeBlock = null;
        if (block is not null && commit && Engine is { } engine && _resizeIndex < engine.Blocks.Count && ReferenceEquals(engine.Blocks[_resizeIndex], block))
        {
            engine.SetImageWidth(_resizeIndex, (int)_resizeWidth);
        }
        else
        {
            InvalidateMeasure();
        }
        InvalidateVisual();
    }

    /// <summary>The drawn column widths of <paramref name="table"/>, or the dragged ones while its border is being dragged.</summary>
    private double[] ColumnWidths(IReadOnlyList<RichTextEditorBlock> blocks, RichTextEditorTableExtent table, double width) =>
        _columnDrag is { } drag && drag.Start == table.Start && drag.Widths.Length == table.Cols
            ? drag.Widths
            : RichTextEditorTables.ColumnWidths(blocks, table, width);

    /// <summary>The table and inner column border under <paramref name="point"/> (border <c>b</c> lies between columns <c>b - 1</c> and <c>b</c>).</summary>
    internal (int Start, int Border)? ColumnBorderAt(Point point)
    {
        if (Engine is not { Docs: true } || Owner is not { IsReadOnly: false })
        {
            return null;
        }
        foreach (var table in _tables)
        {
            if (point.Y < table.Top || point.Y > table.Bottom)
            {
                continue;
            }
            for (var b = 1; b < table.Cols; b++)
            {
                if (Math.Abs(point.X - table.Offsets[b]) <= ColumnGrip)
                {
                    return (table.Start, b);
                }
            }
        }
        return null;
    }

    private void BeginColumnDrag(int start, int border, double x)
    {
        var table = _tables.First(t => t.Start == start);
        var widths = new double[table.Cols];
        for (var c = 0; c < table.Cols; c++)
        {
            widths[c] = table.Offsets[c + 1] - table.Offsets[c];
        }
        _columnDrag = (start, border, x, widths, (double[])widths.Clone());
    }

    private void UpdateColumnDrag(double x)
    {
        if (_columnDrag is not { } drag)
        {
            return;
        }
        var left = drag.StartWidths[drag.Border - 1];
        var right = drag.StartWidths[drag.Border];
        var min = Math.Min(RichTextEditorTables.MinColumnWidth, Math.Min(left, right));
        var delta = Math.Clamp(x - drag.StartX, min - left, right - min);
        drag.Widths[drag.Border - 1] = left + delta;
        drag.Widths[drag.Border] = right - delta;
        _laidOut = null;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void EndColumnDrag(bool commit)
    {
        if (_columnDrag is not { } drag)
        {
            return;
        }
        _columnDrag = null;
        _laidOut = null;
        var moved = drag.Widths.Where((w, c) => Math.Abs(w - drag.StartWidths[c]) >= 0.5).Any();
        if (!commit || !moved || Engine is not { } engine || !engine.SetColumnWidths(drag.Start, drag.Widths.Select(w => (int)Math.Round(w)).ToArray()))
        {
            InvalidateMeasure();
        }
        InvalidateVisual();
    }

    /// <summary>The table whose grips show: the one under the pointer, else the one holding the caret while the editor has focus.</summary>
    private TableBox? GripTable()
    {
        if (Engine is not { Docs: true } engine || Owner is not { IsReadOnly: false })
        {
            return null;
        }
        if (_gripDrag is { } drag)
        {
            return _tables.FirstOrDefault(t => t.Start == drag.Start) is { Rows: > 0 } dragged ? dragged : null;
        }
        if (_hoverTable >= 0)
        {
            return _tables[_hoverTable];
        }
        var focus = engine.Selection.Focus.Block;
        if (!IsKeyboardFocusWithin || focus >= engine.Blocks.Count || !engine.Blocks[focus].IsCell)
        {
            return null;
        }
        foreach (var table in _tables)
        {
            if (focus >= table.Start && focus < table.Start + table.Rows * table.Cols)
            {
                return table;
            }
        }
        return null;
    }

    /// <summary>The drawn pill of a column's grip (centred in the top of the first row) or a row's (in the left of the first column).</summary>
    private static Rect GripRect(TableBox table, bool column, int index) => column
        ? new Rect((table.Offsets[index] + table.Offsets[index + 1]) / 2 - 12, table.Top + 2, 24, 4)
        : new Rect(table.Offsets[0] + 2, (table.RowTops[index] + table.RowTops[index + 1]) / 2 - 12, 4, 24);

    /// <summary>The row or column grip under <paramref name="point"/> (its pill grown to an easy target).</summary>
    internal (int Start, bool Column, int Index)? GripAt(Point point)
    {
        if (GripTable() is not { } table)
        {
            return null;
        }
        for (var c = 0; c < table.Cols; c++)
        {
            if (GripRect(table, true, c).Inflate(new Thickness(4, 3, 4, 5)).Contains(point))
            {
                return (table.Start, true, c);
            }
        }
        for (var r = 0; r < table.Rows; r++)
        {
            if (GripRect(table, false, r).Inflate(new Thickness(3, 4, 5, 4)).Contains(point))
            {
                return (table.Start, false, r);
            }
        }
        return null;
    }

    /// <summary>The gap (0 before the first row or column) nearest to <paramref name="point"/> that the dragged row or column may move to, or -1.</summary>
    private int DropGap(GripDrag drag, Point point)
    {
        if (Engine is not { } engine || _tables.FirstOrDefault(t => t.Start == drag.Start) is not { Rows: > 0 } box)
        {
            return -1;
        }
        var edges = drag.Column ? box.Offsets : box.RowTops;
        var at = drag.Column ? point.X : point.Y;
        var gap = 0;
        for (var k = 1; k < edges.Length; k++)
        {
            if (Math.Abs(edges[k] - at) < Math.Abs(edges[gap] - at))
            {
                gap = k;
            }
        }
        var blocks = engine.Blocks;
        var table = RichTextEditorTables.Extent(blocks, drag.Start);
        RichTextEditorBlock At(int r, int c) => blocks[table.IndexOf(r, c)];
        var allowed = drag.Column
            ? RichTextEditorTables.CanMoveColumn(At, table.Rows, table.Cols, drag.Index, gap)
            : RichTextEditorTables.CanMoveRow(At, table.Rows, table.Cols, drag.Index, gap);
        return allowed ? gap : -1;
    }

    private void EndGripDrag(bool commit)
    {
        if (_gripDrag is not { } drag)
        {
            return;
        }
        _gripDrag = null;
        InvalidateVisual();
        if (!commit || Engine is not { } engine || Owner is not { } owner || drag.Start >= engine.Blocks.Count || !engine.Blocks[drag.Start].IsCell)
        {
            return;
        }
        var table = RichTextEditorTables.Extent(engine.Blocks, drag.Start);
        if (!drag.Moved)
        {
            engine.SelectTableCells(drag.Start, drag.Column ? -1 : drag.Index, drag.Column ? drag.Index : -1);
            owner.Announce(drag.Column ? "Column selected" : "Row selected");
            return;
        }
        if (drag.Gap < 0)
        {
            return;
        }
        // The caret goes into the moved row or column, so the move works on this table and the caret travels with it.
        var cell = drag.Column ? RichTextEditorTables.OwnerIndex(engine.Blocks, table, 0, drag.Index) : RichTextEditorTables.OwnerIndex(engine.Blocks, table, drag.Index, 0);
        engine.SetSelection(RichTextEditorSelection.Caret(cell, 0));
        var moved = drag.Column ? engine.MoveTableColumn(drag.Index, drag.Gap) : engine.MoveTableRow(drag.Index, drag.Gap);
        if (moved)
        {
            owner.Announce(drag.Column ? "Column moved" : "Row moved");
        }
    }

    /// <summary>The grips of the active table, and where a dragged row or column would land.</summary>
    private void DrawTableGrips(DrawingContext context)
    {
        if (GripTable() is not { } table)
        {
            return;
        }
        var rest = MutedBrush ?? RuleBrush;
        for (var c = 0; c < table.Cols; c++)
        {
            var brush = _gripDrag is { Column: true } d && d.Index == c ? AccentBrush : rest;
            context.DrawRectangle(brush, null, new RoundedRect(GripRect(table, true, c), 2));
        }
        for (var r = 0; r < table.Rows; r++)
        {
            var brush = _gripDrag is { Column: false } d && d.Index == r ? AccentBrush : rest;
            context.DrawRectangle(brush, null, new RoundedRect(GripRect(table, false, r), 2));
        }
        if (_gripDrag is { Moved: true, Gap: >= 0 } drag && AccentBrush is { } accent)
        {
            var line = drag.Column
                ? new Rect(Math.Clamp(table.Offsets[drag.Gap] - 1, 0, Math.Max(0, table.Offsets[^1] - 2)), table.Top, 2, table.Bottom - table.Top)
                : new Rect(table.Offsets[0], Math.Clamp(table.RowTops[drag.Gap] - 1, table.Top, Math.Max(table.Top, table.Bottom - 2)), table.Offsets[^1] - table.Offsets[0], 2);
            context.FillRectangle(accent, line);
        }
    }

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
        var tableEnd = 0;
        var columns = Array.Empty<double>();
        RichTextEditorBlock? tableHead = null;
        foreach (var block in blocks)
        {
            used.Add(block);
            if (block.IsCell && index >= tableEnd)
            {
                var table = RichTextEditorTables.Extent(blocks, index);
                tableEnd = table.End;
                columns = ColumnWidths(blocks, table, width);
                tableHead = blocks[table.Start];
            }
            var blockWidth = width;
            var header = false;
            if (block.IsCell)
            {
                blockWidth = 0;
                for (var c = block.Col; c < Math.Min(columns.Length, block.Col + (block.IsCovered ? 1 : block.ColSpan)); c++)
                {
                    blockWidth += columns[c];
                }
                header = RichTextEditorTables.IsHeader(tableHead!, block);
            }
            var preedit = IsComposing && index == caretBlock ? _preedit : null;
            if (preedit is null && _cache.TryGetValue(block, out var cached) && cached.StyleVersion == _styleVersion
                && Math.Abs(cached.LayoutWidth - blockWidth) < 0.01 && cached.Preedit is null && cached.IsHeader == header)
            {
                _views.Add(cached);
            }
            else
            {
                var view = Build(block, blockWidth, header, preedit, preedit is null ? 0 : engine.Selection.Focus.Offset);
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
        _tables.Clear();
        _hoverTable = -1;
        var y = VerticalPadding;
        var hidden = engine.HiddenMask;
        var lastShown = -1;
        for (var i = 0; i < _views.Count;)
        {
            var view = _views[i];
            if (hidden is not null && hidden[i])
            {
                // Folded under a collapsed toggle: no room, no gap; it sits where the next shown block starts.
                view.BoxX = 0;
                view.BoxWidth = width;
                view.TextX = view.PadX;
                _tops.Add(y);
                i++;
                continue;
            }
            if (lastShown >= 0)
            {
                y += Gap(_views[lastShown], view);
            }
            lastShown = view.Block.IsCell ? RichTextEditorTables.Extent(blocks, i).End - 1 : i;
            if (!view.Block.IsCell)
            {
                view.BoxX = 0;
                view.BoxWidth = width;
                view.TextX = view.PadX;
                _tops.Add(y);
                y += view.Height;
                i++;
                continue;
            }
            // A table: columns at their stored widths (scaled to fill the width); every row as tall as its tallest cell, and a merged cell
            // as tall as the rows it spans (the last one grows when its text needs more).
            var grid = RichTextEditorTables.Extent(blocks, i);
            var widths = ColumnWidths(blocks, grid, width);
            var offsets = new double[grid.Cols + 1];
            for (var c = 0; c < grid.Cols; c++)
            {
                offsets[c + 1] = offsets[c] + widths[c];
            }
            var rowHeights = new double[grid.Rows];
            for (var r = 0; r < grid.Rows; r++)
            {
                for (var c = 0; c < grid.Cols; c++)
                {
                    var cell = _views[grid.IndexOf(r, c)];
                    if (!cell.Block.IsCovered && cell.Block.RowSpan == 1)
                    {
                        rowHeights[r] = Math.Max(rowHeights[r], cell.ContentHeight);
                    }
                }
            }
            for (var span = 2; span <= grid.Rows; span++)
            {
                for (var r = 0; r + span <= grid.Rows; r++)
                {
                    for (var c = 0; c < grid.Cols; c++)
                    {
                        var cell = _views[grid.IndexOf(r, c)];
                        if (cell.Block.IsCovered || cell.Block.RowSpan != span)
                        {
                            continue;
                        }
                        var have = 0d;
                        for (var k = 0; k < span; k++)
                        {
                            have += rowHeights[r + k];
                        }
                        if (cell.ContentHeight > have)
                        {
                            rowHeights[r + span - 1] += cell.ContentHeight - have;
                        }
                    }
                }
            }
            var rowTop = y;
            var rowTops = new double[grid.Rows + 1];
            for (var r = 0; r < grid.Rows; r++)
            {
                rowTops[r] = rowTop;
                for (var c = 0; c < grid.Cols; c++)
                {
                    var cell = _views[grid.IndexOf(r, c)];
                    var block = cell.Block;
                    var rows = block.IsCovered ? 1 : block.RowSpan;
                    var height = 0d;
                    for (var k = 0; k < rows; k++)
                    {
                        height += rowHeights[r + k];
                    }
                    cell.Height = height;
                    cell.BoxX = offsets[c];
                    cell.BoxWidth = offsets[Math.Min(grid.Cols, c + (block.IsCovered ? 1 : block.ColSpan))] - offsets[c];
                    cell.TextX = cell.PadX + cell.BoxX;
                    cell.IsLastColumn = c + (block.IsCovered ? 1 : block.ColSpan) == grid.Cols;
                    cell.IsLastRow = r + rows == grid.Rows;
                    _tops.Add(rowTop);
                }
                rowTop += rowHeights[r];
            }
            rowTops[grid.Rows] = rowTop;
            _tables.Add(new TableBox(grid.Start, grid.Rows, grid.Cols, y, rowTop, offsets, rowTops));
            y = rowTop;
            i = grid.End;
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

    private bool HasCodeHeader(RichTextEditorBlock block) => block.IsCode && (ShowCodeHeader || block.Language is not null);

    /// <summary>The syntax spans of a code block in its language, or null for plain text, an unknown language or a very long block.</summary>
    internal static List<CodeBlockSyntaxSpan>? HighlightSyntax(RichTextEditorBlock block)
    {
        if (block.Language is not { } language || block.Length == 0 || block.Length > 30000 || !CodeBlockHighlighter.SupportsLanguage(language))
        {
            return null;
        }
        try
        {
            using var session = CodeBlockHighlighter.CreateSession(language);
            if (session is null)
            {
                return null;
            }
            var spans = new List<CodeBlockSyntaxSpan>();
            CodeBlockHighlighter.AppendSpans(block.Text, session.Update(block.Text), CodeBlockHighlighter.FamilyOf(language), spans);
            return spans;
        }
        catch (Exception)
        {
            // A grammar that fails to load or tokenize leaves the block plain; editing never depends on highlighting.
            return null;
        }
    }

    private double FontSizeOf(RichTextBlockType type) => type switch
    {
        RichTextBlockType.Heading1 => Heading1FontSize,
        RichTextBlockType.Heading2 => Heading2FontSize,
        RichTextBlockType.Heading3 => Heading3FontSize,
        RichTextBlockType.Heading4 => Math.Round((Heading3FontSize + BaseFontSize) / 2),
        RichTextBlockType.Heading5 => BaseFontSize,
        RichTextBlockType.Heading6 => Math.Round(BaseFontSize * 0.875),
        RichTextBlockType.CodeBlock => CodeFontSize,
        _ => BaseFontSize
    };

    /// <summary>Arc's block spacing with CSS margin collapsing: <c>.6em</c> between blocks, headings <c>1.1em</c> above and <c>.45em</c> below, rules <c>1.1em</c> around, list items <c>.2em</c>.</summary>
    private double Gap(RichTextEditorBlockView previous, RichTextEditorBlockView next)
    {
        var a = previous.Block;
        var b = next.Block;
        if (a.IsCell && b.IsCell && !b.StartsTable)
        {
            return 0;
        }
        if (a.IsList && b.IsList && (b.Level > 0 || a.Level > 0 || a.Type == b.Type))
        {
            return ListItemPaddingEm * BaseFontSize;
        }
        if (a.SharesCalloutBox(b))
        {
            return 0;
        }
        var nextSize = FontSizeOf(b.Type);
        var bottom = a.IsDivider ? 1.1 * BaseFontSize : a.IsImage ? 0.8 * BaseFontSize : 0;
        double top;
        if (b.IsImage)
        {
            top = 0.8 * BaseFontSize;
        }
        else if (b.IsDivider)
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
            if (block.IsTodo)
            {
                _markers.Add("todo");
                continue;
            }
            if (block.IsToggle)
            {
                _markers.Add("toggle");
                continue;
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

    /// <summary>Strikethrough, the accent underline of a link, or the plain underline mark (a link already has its own underline).</summary>
    private TextDecorationCollection? Decorations(bool strike, bool link, bool underline)
    {
        var key = (strike ? 1 : 0) | (link || underline ? 2 : 0);
        if (key == 0)
        {
            return null;
        }
        if (_decorations[key] is { } cached)
        {
            return cached;
        }
        var collection = new TextDecorationCollection();
        if (strike)
        {
            collection.Add(new TextDecoration { Location = TextDecorationLocation.Strikethrough, StrokeThickness = 1, StrokeThicknessUnit = TextDecorationUnit.Pixel });
        }
        if (link || underline)
        {
            collection.Add(new TextDecoration
            {
                Location = TextDecorationLocation.Underline, StrokeThickness = 1, StrokeThicknessUnit = TextDecorationUnit.Pixel,
                Stroke = link ? LinkUnderlineBrush : null, StrokeOffset = 1.5, StrokeOffsetUnit = TextDecorationUnit.Pixel
            });
        }
        return _decorations[key] = collection;
    }

    private TextDecorationCollection PreeditDecorations => _preeditUnderline ??= new TextDecorationCollection
    {
        new TextDecoration { Location = TextDecorationLocation.Underline, StrokeThickness = 1, StrokeThicknessUnit = TextDecorationUnit.Pixel }
    };

    /// <summary>How much smaller superscript and subscript text is than the text around it.</summary>
    private const double ScriptScale = 0.72;

    /// <summary>
    /// The superscript and subscript pieces of a laid out block, one per line they fall on: each is laid out on its own and placed over
    /// the room the block's layout keeps for it, raised by about a third of the surrounding text's size or lowered by a sixth.
    /// </summary>
    private static List<(TextLayout Layout, Point At)> ScriptRuns(TextLayout layout, string text,
        List<(int Start, int Length, GenericTextRunProperties Properties, bool Raised)> spans, double tracking)
    {
        var runs = new List<(TextLayout Layout, Point At)>();
        foreach (var line in layout.TextLines)
        {
            var lineStart = line.FirstTextSourceIndex;
            var lineEnd = lineStart + line.Length;
            foreach (var (start, length, properties, raised) in spans)
            {
                var from = Math.Max(start, lineStart);
                var to = Math.Min(start + length, Math.Min(lineEnd, text.Length));
                if (to <= from)
                {
                    continue;
                }
                var piece = text[from..to].TrimEnd('\n', '\r');
                if (piece.Length == 0 || layout.HitTestTextRange(from, piece.Length).FirstOrDefault() is not { Width: > 0 } box)
                {
                    continue;
                }
                var small = new TextLayout(piece, properties.Typeface, properties.FontRenderingEmSize, properties.ForegroundBrush,
                    TextAlignment.Left, TextWrapping.NoWrap, null, properties.TextDecorations, FlowDirection.LeftToRight,
                    double.PositiveInfinity, double.PositiveInfinity, double.NaN, tracking * ScriptScale);
                // Relative to the size the text around it has (a sized run raises further).
                var around = properties.FontRenderingEmSize / ScriptScale;
                var shift = raised ? -0.36 * around : 0.16 * around;
                runs.Add((small, new Point(box.X, box.Y + line.Baseline - small.TextLines[0].Baseline + shift)));
            }
        }
        return runs;
    }

    private RichTextEditorBlockView Build(RichTextEditorBlock block, double width, bool header, string? preedit, int preeditAt)
    {
        var view = new RichTextEditorBlockView(block) { LayoutWidth = width, StyleVersion = _styleVersion, Preedit = preedit, PreeditAt = preeditAt, IsHeader = header };
        if (block.IsCovered)
        {
            // A merged cell covers it: no text, no box of its own.
            view.FontSize = BaseFontSize;
            view.LineHeight = 1;
            return view;
        }
        if (block.IsDivider)
        {
            view.Height = 1;
            view.ContentHeight = 1;
            view.FontSize = BaseFontSize;
            view.LineHeight = 1;
            return view;
        }
        if (block.IsImage)
        {
            var entry = block.Src is { } src ? Owner?.Images.Get(src) : null;
            var available = Math.Max(1, width);
            var chosen = ReferenceEquals(block, _resizeBlock) ? _resizeWidth : block.Width;
            if (entry is { State: RichTextEditorImageState.Ready, Image: { } picture })
            {
                var natural = picture.Size;
                var target = Math.Min(chosen > 0 ? chosen : natural.Width, available);
                view.ImageSize = natural.Width > 0 && natural.Height > 0
                    ? new Size(target, natural.Height * target / natural.Width)
                    : RichTextEditorImageCache.Fit(natural, available);
            }
            else
            {
                view.ImageSize = new Size(Math.Min(chosen > 0 ? chosen : 320, available), entry is { State: RichTextEditorImageState.Failed } ? 72 : 160);
            }
            view.Height = view.ImageSize.Height;
            view.ContentHeight = view.Height;
            view.FontSize = BaseFontSize;
            view.LineHeight = 1;
            return view;
        }
        var size = FontSizeOf(block.Type);
        // Lines are as tall as the block's largest text needs, stretched by the block's spacing.
        var largest = 1d;
        if (!block.IsCode)
        {
            foreach (var span in block.Spans)
            {
                largest = Math.Max(largest, span.Marks.Size().Scale());
            }
        }
        var lineHeight = size * largest * (block.IsHeading ? 1.25 : block.IsCode ? 1.6 : 1.65) * block.Spacing.Scale();
        var weight = block.IsHeading || header ? FontWeight.Medium : FontWeight.Normal;
        var tracking = (block.IsHeading ? -0.02 : Tracking) * size;
        var checkedTodo = block.IsTodo && block.IsChecked;
        var baseBrush = block.Type is RichTextBlockType.Quote or RichTextBlockType.Heading6 || checkedTodo ? SecondaryBrush ?? Foreground : Foreground;
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
            case RichTextBlockType.TodoList:
            case RichTextBlockType.Toggle:
                textX = (block.Level + 1) * (ListIndentEm + ListItemPaddingEm) * BaseFontSize;
                break;
            case RichTextBlockType.Callout:
                textX = 46;
                textY = 10;
                right = 16;
                break;
            case RichTextBlockType.CodeBlock:
                textX = 15;
                textY = HasCodeHeader(block) ? 28 : 13;
                right = 15;
                break;
            case RichTextBlockType.TableCell:
                textX = 12;
                textY = 8;
                right = 12;
                break;
        }
        view.FontSize = size;
        view.PadX = textX;
        view.LineHeight = lineHeight;
        view.Weight = weight;
        view.TextX = textX;
        view.TextY = textY;

        var text = preedit is null ? block.Text : block.Text.Insert(Math.Clamp(preeditAt, 0, block.Length), preedit);
        var overrides = new List<ValueSpan<TextRunProperties>>();
        var codeSpans = new List<(int Start, int Length)>();
        var fillSpans = new List<(int Start, int Length, RichTextColor Color)>();
        var scriptSpans = new List<(int Start, int Length, GenericTextRunProperties Properties, bool Raised)>();
        if (block.IsCode)
        {
            var plain = new GenericTextRunProperties(TypefaceFor(RichTextEditorMarks.Code, weight), size, null, baseBrush);
            var syntax = HighlightSyntax(block);
            if (syntax is null || syntax.Count == 0)
            {
                overrides.Add(new ValueSpan<TextRunProperties>(0, Math.Max(1, text.Length), plain));
            }
            else
            {
                var italic = new Typeface(MonoFontFamily ?? FontFamily, FontStyle.Italic, weight);
                var at = 0;
                foreach (var span in syntax)
                {
                    if (span.Start > at)
                    {
                        AddShifted(overrides, at, span.Start - at, plain, view);
                    }
                    var brush = SyntaxBrushOf(span.Kind) ?? baseBrush;
                    var face = span.Kind == CodeBlockSyntaxKind.Comment ? italic : TypefaceFor(RichTextEditorMarks.Code, weight);
                    AddShifted(overrides, span.Start, span.Length, new GenericTextRunProperties(face, size, null, brush), view);
                    at = span.Start + span.Length;
                }
                if (at < block.Length)
                {
                    AddShifted(overrides, at, block.Length - at, plain, view);
                }
            }
        }
        else
        {
            foreach (var span in block.Spans)
            {
                if (span.Marks == RichTextEditorMarks.None && span.Href is null && !checkedTodo)
                {
                    continue;
                }
                var strike = (span.Marks & RichTextEditorMarks.Strike) != 0 || checkedTodo;
                var underline = (span.Marks & RichTextEditorMarks.Underline) != 0;
                var link = span.Href is not null;
                var code = (span.Marks & RichTextEditorMarks.Code) != 0;
                var colored = TextBrushOf(span.Marks.TextColor());
                var brush = strike ? SecondaryBrush ?? baseBrush : link ? LinkBrush ?? baseBrush : colored ?? baseBrush;
                var script = span.Marks & RichTextEditorMarks.ScriptMask;
                var runSize = (code ? size * 0.86 : size) * span.Marks.Size().Scale() * (script != 0 ? ScriptScale : 1);
                var properties = new GenericTextRunProperties(TypefaceFor(span.Marks, weight), runSize, Decorations(strike, link, underline), brush);
                if (script != 0)
                {
                    // The layout keeps the small glyphs' room (so the caret and hit tests fit) but draws nothing; the glyphs are drawn
                    // raised or lowered on top, as Avalonia's text lines draw every run on the shared baseline.
                    scriptSpans.Add((view.ToLayout(span.Start), span.Length, properties, script == RichTextEditorMarks.Superscript));
                    properties = new GenericTextRunProperties(properties.Typeface, runSize, null, Brushes.Transparent);
                }
                if (span.Marks.Highlight() != RichTextColor.Default)
                {
                    fillSpans.Add((view.ToLayout(span.Start), span.Length, span.Marks.Highlight()));
                }
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
                new GenericTextRunProperties(TypefaceFor(block.IsCode ? RichTextEditorMarks.Code : marks, weight), block.IsCode ? size : size * marks.Size().Scale(), PreeditDecorations, baseBrush)));
        }
        var maxWidth = Math.Max(1, width - textX - right);
        view.Layout = new TextLayout(text, TypefaceFor(block.IsCode ? RichTextEditorMarks.Code : RichTextEditorMarks.None, weight), size, baseBrush,
            block.Align switch { RichTextAlign.Center => TextAlignment.Center, RichTextAlign.End => TextAlignment.Right, _ => TextAlignment.Left },
            TextWrapping.Wrap, null, null, FlowDirection.LeftToRight, maxWidth, double.PositiveInfinity,
            lineHeight, tracking, 0, null, overrides.Count == 0 ? null : overrides);
        view.Height = view.Layout.Height + textY + (block.IsCode ? 13 : textY);
        view.ContentHeight = view.Height;
        if (scriptSpans.Count > 0)
        {
            view.ScriptRuns = ScriptRuns(view.Layout, text, scriptSpans, tracking);
        }
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
        if (fillSpans.Count > 0)
        {
            var fills = new List<(Rect Rect, RichTextColor Color)>();
            foreach (var (start, length, color) in fillSpans)
            {
                foreach (var rect in view.Layout.HitTestTextRange(start, length))
                {
                    fills.Add((rect, color));
                }
            }
            view.FillRects = fills;
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
        var index = CellAtX(BlockAtY(point.Y), point.X);
        var view = _views[index];
        if (view.Layout is null)
        {
            if (view.Block.IsImage)
            {
                return new RichTextEditorPosition(index, 0);
            }
            var center = _tops[index] + view.Height / 2;
            var engine = Engine!;
            var target = RichTextEditorEngine.NearestTextual(engine.Blocks, index, forward: point.Y >= center);
            var targetView = _views[target.Block];
            return HitTestInBlock(target.Block, new Point(point.X, point.Y >= center ? _tops[target.Block] : _tops[target.Block] + targetView.Height - 1));
        }
        return HitTestInBlock(index, point);
    }

    /// <summary>For a table cell, the cell of the same row at <paramref name="x"/>; any other block stays.</summary>
    private int CellAtX(int index, double x)
    {
        var blocks = Engine!.Blocks;
        if (!blocks[index].IsCell)
        {
            return index;
        }
        var table = RichTextEditorTables.Extent(blocks, index);
        var rowStart = index - blocks[index].Col;
        var row = blocks[index].Row;
        for (var c = 0; c < table.Cols; c++)
        {
            var view = _views[rowStart + c];
            if (x < view.BoxX + view.BoxWidth)
            {
                return RichTextEditorTables.OwnerIndex(blocks, table, row, c);
            }
        }
        return RichTextEditorTables.OwnerIndex(blocks, table, row, table.Cols - 1);
    }

    /// <summary>The block at <paramref name="y"/> (surface coordinates; the nearer block in a gap).</summary>
    internal int BlockIndexAtY(double y) => _views.Count == 0 ? 0 : BlockAtY(y);

    /// <summary>The vertical centre of the first line of <paramref name="block"/> (a tall atomic block or table: near its top).</summary>
    internal double HandleY(int block)
    {
        if (block < 0 || block >= _views.Count)
        {
            return 0;
        }
        var view = _views[block];
        return _tops[block] + (view.Layout is null ? Math.Min(14, view.Height / 2) : view.TextY + view.LineHeight / 2);
    }

    /// <summary>The left edge of what <paramref name="block"/> draws first: a list item's marker column (so the handle sits beside a nested item, not at the page edge), 0 for any other block.</summary>
    internal double HandleX(int block)
    {
        if (block < 0 || block >= _views.Count)
        {
            return 0;
        }
        var item = _views[block].Block;
        return item.Type is RichTextBlockType.BulletedList or RichTextBlockType.NumberedList or RichTextBlockType.TodoList or RichTextBlockType.Toggle
            ? item.Level * (ListIndentEm + ListItemPaddingEm) * BaseFontSize
            : 0;
    }

    /// <summary>The y of the block boundary <paramref name="gap"/> (0 to the block count), in surface coordinates.</summary>
    internal double BlockHeightForTests(int block) => _views[block].Height;

    /// <summary>The laid out view of <paramref name="block"/> (tests).</summary>
    internal RichTextEditorBlockView ViewForTests(int block) => _views[block];

    private Dictionary<int, List<RichTextEditorMatch>>? _matchesByBlock;

    /// <summary>The find bar's matches, drawn as highlights (null clears them).</summary>
    internal void SetMatches(List<RichTextEditorMatch>? matches, int current)
    {
        _matchesByBlock = matches is { Count: > 0 } ? matches.GroupBy(m => m.Block).ToDictionary(g => g.Key, g => g.ToList()) : null;
        InvalidateVisual();
    }

    internal int MatchCountForTests => _matchesByBlock?.Values.Sum(list => list.Count) ?? 0;

    internal double GapY(int gap)
    {
        if (_views.Count == 0)
        {
            return 0;
        }
        if (gap >= _views.Count)
        {
            var last = _views.Count - 1;
            var end = _tops[last] + _views[last].Height;
            return end;
        }
        return Math.Max(0, _tops[gap] - (gap > 0 ? 3 : 0));
    }

    /// <summary>The valid drop boundary nearest to <paramref name="y"/> (surface coordinates).</summary>
    internal int DropGapAt(double y, IReadOnlyList<RichTextEditorBlock> blocks)
    {
        var best = 0;
        var distance = double.MaxValue;
        for (var gap = 0; gap <= blocks.Count && gap <= _views.Count; gap++)
        {
            if (!RichTextEditorEngine.IsDropGap(blocks, gap))
            {
                continue;
            }
            var d = Math.Abs(GapY(gap) - y);
            if (d < distance)
            {
                distance = d;
                best = gap;
            }
        }
        return best;
    }

    private (int Start, int End)? _activeUnit;

    /// <summary>The blocks of the unit the block handle is acting on (tinted like a selection), or null.</summary>
    internal (int Start, int End)? ActiveUnit
    {
        get => _activeUnit;
        set
        {
            if (_activeUnit != value)
            {
                _activeUnit = value;
                InvalidateVisual();
            }
        }
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
                lo++;
            }
        }
        // Folded blocks take no room: the point belongs to the toggle that hides them.
        if (Engine is { } engine && engine.HiddenMask is { } hidden)
        {
            while (lo > 0 && lo < hidden.Length && hidden[lo])
            {
                lo--;
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
        var origin = blocks[from.Block];
        var start = from.Block + direction;
        if (origin.IsCell)
        {
            // Inside a table, up and down stay in the column; the first and last rows leave the table.
            var table = RichTextEditorTables.Extent(blocks, from.Block);
            start = direction < 0
                ? origin.Row > 0 ? RichTextEditorTables.OwnerIndex(blocks, table, origin.Row - 1, origin.Col) : table.Start - 1
                : origin.Row + origin.RowSpan < table.Rows ? RichTextEditorTables.OwnerIndex(blocks, table, origin.Row + origin.RowSpan, origin.Col) : table.End;
        }
        for (var i = start; i >= 0 && i < _views.Count; i += direction)
        {
            if (blocks[i].IsDivider || Engine!.IsHidden(i))
            {
                continue;
            }
            if (blocks[i].IsImage)
            {
                return new RichTextEditorPosition(i, 0);
            }
            if (blocks[i].IsCell && !(origin.IsCell && i >= RichTextEditorTables.Extent(blocks, from.Block).Start && i < RichTextEditorTables.Extent(blocks, from.Block).End))
            {
                i = CellAtX(i, goalX);
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
        if (engine.Blocks[first].IsCell)
        {
            first -= engine.Blocks[first].Col;
        }
        var selection = engine.Selection;
        var focused = IsKeyboardFocusWithin || Owner?.KeepsSelectionVisible == true;
        var showSelection = !selection.IsCollapsed && focused;
        var blocks = engine.Blocks;
        var width = bounds.Width;
        var cells = showSelection ? engine.CellSelection : null;
        for (var i = Math.Max(0, first - 1); i < _views.Count; i++)
        {
            var top = _tops[i];
            if (top > band.Bottom)
            {
                break;
            }
            var view = _views[i];
            var block = view.Block;
            if (block.IsCovered || engine.IsHidden(i))
            {
                continue;
            }
            if (_activeUnit is { } unit && i >= unit.Start && i < unit.End)
            {
                context.DrawRectangle(SelectionBrush, null, new RoundedRect(new Rect(0, top, width, Math.Max(1, view.Height)), 6));
            }
            DrawDecoration(context, i, view, top, width);
            var inCells = cells is { } picked && i >= picked.Table.Start && i < picked.Table.End;
            if (inCells && block.Row >= cells!.Value.R1 && block.Row <= cells.Value.R2 && block.Col >= cells.Value.C1 && block.Col <= cells.Value.C2)
            {
                context.FillRectangle(SelectionBrush, new Rect(view.BoxX, top, view.BoxWidth, view.Height));
            }
            if (view.Layout is null)
            {
                if (block.IsImage)
                {
                    var covered = showSelection && selection.Start.Block <= i && selection.End.Block >= i;
                    DrawImage(context, view, top, covered || (focused && selection.IsCollapsed && selection.Focus.Block == i));
                }
                else if (showSelection && selection.Start.Block <= i && selection.End.Block >= i)
                {
                    context.FillRectangle(SelectionBrush, new Rect(0, top - 3, width, 7));
                }
                continue;
            }
            var origin = new Point(view.TextX, top + view.TextY);
            if (view.FillRects is { } fillRects)
            {
                foreach (var (rect, color) in fillRects)
                {
                    if (FillBrushOf(color) is { } fill)
                    {
                        context.DrawRectangle(fill, null, new RoundedRect(rect.Translate(origin), 3));
                    }
                }
            }
            if (view.CodeRects is { } codeRects && CodeBackground is { } codeBrush)
            {
                foreach (var rect in codeRects)
                {
                    context.DrawRectangle(codeBrush, null, new RoundedRect(rect.Translate(origin), 6));
                }
            }
            if (_matchesByBlock is { } found && found.TryGetValue(i, out var blockMatches) && (FillYellowBrush ?? SelectionBrush) is { } matchBrush)
            {
                foreach (var match in blockMatches)
                {
                    foreach (var rect in view.Layout.HitTestTextRange(view.ToLayout(match.Start), match.Length))
                    {
                        context.DrawRectangle(matchBrush, null, new RoundedRect(rect.Translate(origin), 3));
                    }
                }
            }
            if (showSelection && !inCells && selection.Start.Block <= i && selection.End.Block >= i)
            {
                DrawSelection(context, i, view, origin, selection);
            }
            view.Layout.Draw(context, origin);
            if (view.ScriptRuns is { } scriptRuns)
            {
                foreach (var (layout, at) in scriptRuns)
                {
                    layout.Draw(context, origin + at);
                }
            }
            DrawPlaceholder(context, i, view, origin, blocks);
        }
        DrawTableGrips(context);
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
            case RichTextBlockType.TableCell:
                DrawCell(context, view, top);
                break;
            case RichTextBlockType.Quote:
                if (QuoteBarBrush is { } bar)
                {
                    context.FillRectangle(bar, new Rect(0, top, 2, view.Height));
                }
                break;
            case RichTextBlockType.Callout:
                DrawCallout(context, index, view, top, width);
                break;
            case RichTextBlockType.CodeBlock:
                var pen = CodeBorderBrush is { } border ? new ImmutablePen(border.ToImmutable(), 1) : null;
                context.DrawRectangle(CodeBackground, pen, new RoundedRect(new Rect(0.5, top + 0.5, Math.Max(0, width - 1), Math.Max(0, view.Height - 1)), 14));
                if (HasCodeHeader(block) && MutedBrush is { } muted)
                {
                    var language = block.Language ?? "Plain text";
                    var key = "lang:" + language;
                    if (!_markerLayouts.TryGetValue(key, out var label))
                    {
                        label = new TextLayout(language, new Typeface(MonoFontFamily ?? FontFamily), CodeFontSize * 0.85, muted, TextAlignment.Left, TextWrapping.NoWrap);
                        _markerLayouts[key] = label;
                    }
                    label.Draw(context, new Point(15, top + 7));
                    if (ShowCodeHeader)
                    {
                        // A chevron after the name marks the label as a picker.
                        var chevron = new StreamGeometry();
                        using (var g = chevron.Open())
                        {
                            var x = 15 + label.WidthIncludingTrailingWhitespace + 8;
                            var y = top + 7 + label.Height / 2;
                            g.BeginFigure(new Point(x, y - 1.5), false);
                            g.LineTo(new Point(x + 3.5, y + 2));
                            g.LineTo(new Point(x + 7, y - 1.5));
                            g.EndFigure(false);
                        }
                        context.DrawGeometry(null, new ImmutablePen(muted.ToImmutable(), 1.25, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), chevron);
                    }
                }
                break;
            case RichTextBlockType.BulletedList:
            case RichTextBlockType.NumberedList:
            case RichTextBlockType.TodoList:
            case RichTextBlockType.Toggle:
                DrawMarker(context, index, view, top);
                break;
        }
    }

    /// <summary>
    /// A table cell's box: filled with its color, or tinted when it is a header cell, and sharing its borders with the others (each draws
    /// its top and left edge, the last ones the closing edges).
    /// </summary>
    private void DrawCell(DrawingContext context, RichTextEditorBlockView view, double top)
    {
        var rect = new Rect(view.BoxX, top, view.BoxWidth, view.Height);
        if ((FillBrushOf(view.Block.Tone) ?? (view.IsHeader ? CodeBackground : null)) is { } fill)
        {
            context.FillRectangle(fill, rect);
        }
        if (RuleBrush is not { } rule)
        {
            return;
        }
        context.FillRectangle(rule, new Rect(rect.X, rect.Y, rect.Width, 1));
        context.FillRectangle(rule, new Rect(rect.X, rect.Y, 1, rect.Height));
        if (view.IsLastColumn)
        {
            context.FillRectangle(rule, new Rect(rect.Right - 1, rect.Y, 1, rect.Height));
        }
        if (view.IsLastRow)
        {
            context.FillRectangle(rule, new Rect(rect.X, rect.Bottom - 1, rect.Width, 1));
        }
    }

    /// <summary>An image block: the picture (or a placeholder while it loads or when it failed), outlined when selected.</summary>
    private void DrawImage(DrawingContext context, RichTextEditorBlockView view, double top, bool selected)
    {
        var rect = new Rect(0, top, view.ImageSize.Width, view.ImageSize.Height);
        var entry = view.Block.Src is { } src ? Owner?.Images.Get(src) : null;
        if (entry is { State: RichTextEditorImageState.Ready, Image: { } image })
        {
            using (context.PushClip(new RoundedRect(rect, 8)))
            {
                context.DrawImage(image, new Rect(image.Size), rect);
            }
        }
        else
        {
            var pen = CodeBorderBrush is { } border ? new ImmutablePen(border.ToImmutable(), 1) : null;
            context.DrawRectangle(CodeBackground, pen, new RoundedRect(rect.Deflate(0.5), 8));
            if (MutedBrush is { } muted)
            {
                var label = entry is { State: RichTextEditorImageState.Failed } ? view.Block.Alt ?? "Image unavailable" : "Loading image…";
                var key = "image:" + label;
                if (!_markerLayouts.TryGetValue(key, out var layout))
                {
                    layout = new TextLayout(label, new Typeface(FontFamily), BaseFontSize * 0.9, muted, TextAlignment.Center, TextWrapping.NoWrap, TextTrimming.CharacterEllipsis, maxWidth: Math.Max(1, rect.Width - 24));
                    _markerLayouts[key] = layout;
                }
                layout.Draw(context, new Point(rect.X + 12, rect.Y + (rect.Height - layout.Height) / 2));
            }
        }
        if (selected && AccentBrush is { } accent)
        {
            context.DrawRectangle(null, new ImmutablePen(accent.ToImmutable(), 2), new RoundedRect(rect.Inflate(1), 9));
            var grip = new Rect(rect.Right - 7, rect.Bottom - 7, 12, 12);
            context.DrawRectangle(accent, CheckBrush is { } edge ? new ImmutablePen(edge.ToImmutable(), 1.5) : null, new RoundedRect(grip, 4));
        }
    }

    /// <summary>One paragraph of a callout box: tinted background (rounded where the box begins or ends) and, on the first, the icon.</summary>
    private void DrawCallout(DrawingContext context, int index, RichTextEditorBlockView view, double top, double width)
    {
        var block = view.Block;
        var first = index == 0 || !_views[index - 1].Block.SharesCalloutBox(block);
        var last = index + 1 >= _views.Count || !_views[index + 1].Block.SharesCalloutBox(block);
        var fill = FillBrushOf(block.Tone) ?? CodeBackground;
        const double radius = 14;
        context.DrawRectangle(fill, null, new RoundedRect(new Rect(0, top, width, Math.Max(1, view.Height)),
            first ? radius : 0, first ? radius : 0, last ? radius : 0, last ? radius : 0));
        if (first && block.Icon is { } icon)
        {
            var key = "icon:" + icon;
            if (!_markerLayouts.TryGetValue(key, out var layout))
            {
                layout = new TextLayout(icon, new Typeface(FontFamily), BaseFontSize * 1.15, Foreground, TextAlignment.Left, TextWrapping.NoWrap, lineHeight: view.LineHeight);
                _markerLayouts[key] = layout;
            }
            layout.Draw(context, new Point(16, top + view.TextY));
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
        if (marker == "todo")
        {
            DrawTodoBox(context, view, top, view.Block.IsChecked);
            return;
        }
        if (marker == "toggle")
        {
            DrawToggleArrow(context, view, top, brush);
            return;
        }
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

    /// <summary>The disclosure arrow of a toggle item, where a to-do item has its checkbox.</summary>
    private Rect ToggleArrowRect(RichTextEditorBlockView view, double top) => TodoBoxRect(view, top);

    /// <summary>A chevron pointing right (collapsed) or down (open), in the muted text color.</summary>
    private void DrawToggleArrow(DrawingContext context, RichTextEditorBlockView view, double top, IBrush brush)
    {
        var box = ToggleArrowRect(view, top);
        var c = box.Center;
        var r = box.Width * 0.22;
        var arrow = new StreamGeometry();
        using (var g = arrow.Open())
        {
            if (view.Block.IsCollapsed)
            {
                g.BeginFigure(new Point(c.X - r * 0.6, c.Y - r * 1.2), false);
                g.LineTo(new Point(c.X + r * 0.6, c.Y));
                g.LineTo(new Point(c.X - r * 0.6, c.Y + r * 1.2));
            }
            else
            {
                g.BeginFigure(new Point(c.X - r * 1.2, c.Y - r * 0.6), false);
                g.LineTo(new Point(c.X, c.Y + r * 0.6));
                g.LineTo(new Point(c.X + r * 1.2, c.Y - r * 0.6));
            }
            g.EndFigure(false);
        }
        context.DrawGeometry(null, new ImmutablePen(brush.ToImmutable(), 1.6, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), arrow);
    }

    /// <summary>The toggle item whose arrow is under <paramref name="point"/>, or -1.</summary>
    private int ToggleAt(Point point)
    {
        if (_views.Count == 0)
        {
            return -1;
        }
        var index = BlockAtY(point.Y);
        var view = _views[index];
        return view.Block.IsToggle && view.Layout is not null && ToggleArrowRect(view, _tops[index]).Inflate(4).Contains(point) ? index : -1;
    }

    /// <summary>The arrow of the toggle item at <paramref name="block"/>, in surface coordinates.</summary>
    internal Rect ToggleArrowBounds(int block) => ToggleArrowRect(_views[block], _tops[block]);

    /// <summary>The checkbox of a to-do item, relative to the surface: left of the text, centered on the first line's x-height.</summary>
    private Rect TodoBoxRect(RichTextEditorBlockView view, double top)
    {
        var size = BaseFontSize;
        var side = size * 0.95;
        var itemLeft = view.TextX - ListItemPaddingEm * size;
        var centerY = top + view.TextY + (view.Layout is { } layout ? layout.TextLines[0].Baseline : size) - size * 0.31;
        return new Rect(itemLeft - size * 0.3 - side, centerY - side / 2, side, side);
    }

    /// <summary>The checkbox of the to-do item at <paramref name="block"/>, in surface coordinates.</summary>
    internal Rect TodoBoxBounds(int block) => TodoBoxRect(_views[block], _tops[block]);

    private void DrawTodoBox(DrawingContext context, RichTextEditorBlockView view, double top, bool isChecked)
    {
        var box = TodoBoxRect(view, top);
        if (isChecked)
        {
            context.DrawRectangle(AccentBrush, null, new RoundedRect(box, 4));
            if (CheckBrush is { } check)
            {
                var pen = new ImmutablePen(check.ToImmutable(), 1.6, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                var tick = new StreamGeometry();
                using (var g = tick.Open())
                {
                    g.BeginFigure(new Point(box.X + box.Width * 0.27, box.Y + box.Height * 0.53), false);
                    g.LineTo(new Point(box.X + box.Width * 0.44, box.Y + box.Height * 0.70));
                    g.LineTo(new Point(box.X + box.Width * 0.74, box.Y + box.Height * 0.32));
                    g.EndFigure(false);
                }
                context.DrawGeometry(null, pen, tick);
            }
            return;
        }
        var border = QuoteBarBrush is { } brush ? new ImmutablePen(brush.ToImmutable(), 1.5) : null;
        context.DrawRectangle(null, border, new RoundedRect(box.Deflate(0.75), 3.5));
    }

    /// <summary>The language label of the code block under <paramref name="point"/> (surface coordinates), as a block index and its rectangle; null elsewhere.</summary>
    internal (int Block, Rect Rect)? CodeHeaderAt(Point point)
    {
        if (!ShowCodeHeader || _views.Count == 0)
        {
            return null;
        }
        var index = BlockAtY(point.Y);
        var view = _views[index];
        if (!view.Block.IsCode)
        {
            return null;
        }
        var rect = CodeHeaderRect(index);
        return rect.Contains(point) ? (index, rect) : null;
    }

    /// <summary>The clickable language label of a code block, in surface coordinates.</summary>
    internal Rect CodeHeaderRect(int block)
    {
        var key = "lang:" + (_views[block].Block.Language ?? "Plain text");
        var width = _markerLayouts.TryGetValue(key, out var label) ? label.WidthIncludingTrailingWhitespace + 24 : 96;
        return new Rect(9, _tops[block] + 3, width, 24);
    }

    /// <summary>The to-do item whose checkbox is under <paramref name="point"/> (surface coordinates), or -1.</summary>
    private int TodoAt(Point point)
    {
        if (_views.Count == 0)
        {
            return -1;
        }
        var index = BlockAtY(point.Y);
        var view = _views[index];
        if (!view.Block.IsTodo || view.Layout is null)
        {
            return -1;
        }
        return TodoBoxRect(view, _tops[index]).Inflate(4).Contains(point) ? index : -1;
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
        if (OperatingSystem.IsBrowser() && !IsComposing && !_composed && e.Text!.Length == 1 && !char.IsWhiteSpace(e.Text[0]))
        {
            // The browser host also sends the keydown's letter as text while the input method starts composing it; the
            // composition events arrive just after, so wait for them and drop the letter when one began.
            e.Handled = true;
            var letter = e.Text;
            Dispatcher.UIThread.Post(() =>
            {
                if (!IsComposing && !_composed)
                {
                    Owner?.HandleTextInput(letter, false);
                }
            }, DispatcherPriority.Background);
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
        if (OverResizeGrip(point.Position, out var gripBlock))
        {
            _resizeIndex = gripBlock;
            _resizeBlock = engine.Blocks[gripBlock];
            _resizeStartX = point.Position.X;
            _resizeStartWidth = _views[gripBlock].ImageSize.Width;
            _resizeWidth = _resizeStartWidth;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }
        if (GripAt(point.Position) is { } grip)
        {
            _gripDrag = new GripDrag(grip.Start, grip.Column, grip.Index, point.Position);
            e.Pointer.Capture(this);
            InvalidateVisual();
            e.Handled = true;
            return;
        }
        if (ColumnBorderAt(point.Position) is { } border)
        {
            if (e.ClickCount >= 2)
            {
                // A double click on a border shares the width evenly again.
                engine.SetColumnWidths(border.Start, Array.Empty<int>());
            }
            else
            {
                BeginColumnDrag(border.Start, border.Border, point.Position.X);
                e.Pointer.Capture(this);
            }
            e.Handled = true;
            return;
        }
        if (CodeHeaderAt(point.Position) is { } header)
        {
            owner.OnCodeLanguagePressed(header.Block, header.Rect);
            e.Handled = true;
            return;
        }
        if (ToggleAt(point.Position) is >= 0 and var toggle)
        {
            owner.OnTogglePressed(toggle);
            e.Handled = true;
            return;
        }
        if (TodoAt(point.Position) is >= 0 and var todo)
        {
            owner.OnTodoPressed(todo);
            e.Handled = true;
            return;
        }
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
        if (_resizeBlock is not null)
        {
            UpdateResize(e.GetPosition(this).X);
            return;
        }
        if (_columnDrag is not null)
        {
            UpdateColumnDrag(e.GetPosition(this).X);
            return;
        }
        var here = e.GetPosition(this);
        if (_gripDrag is { } grip)
        {
            grip.Moved |= Math.Abs(here.X - grip.From.X) + Math.Abs(here.Y - grip.From.Y) > 4;
            grip.Gap = grip.Moved ? DropGap(grip, here) : -1;
            InvalidateVisual();
            return;
        }
        var hover = _tables.FindIndex(t => here.Y >= t.Top - 4 && here.Y <= t.Bottom + 4);
        if (hover != _hoverTable)
        {
            _hoverTable = hover;
            InvalidateVisual();
        }
        Cursor = OverResizeGrip(here, out _) ? new Cursor(StandardCursorType.BottomRightCorner)
            : GripAt(here) is not null ? new Cursor(StandardCursorType.Hand)
            : ColumnBorderAt(here) is not null ? new Cursor(StandardCursorType.SizeWestEast)
            : new Cursor(StandardCursorType.Ibeam);
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
        if (_resizeBlock is not null)
        {
            EndResize(commit: true);
            e.Pointer.Capture(null);
            return;
        }
        if (_columnDrag is not null)
        {
            EndColumnDrag(commit: true);
            e.Pointer.Capture(null);
            return;
        }
        if (_gripDrag is not null)
        {
            EndGripDrag(commit: true);
            e.Pointer.Capture(null);
            return;
        }
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
        EndColumnDrag(commit: false);
        EndGripDrag(commit: false);
        if (IsDragging)
        {
            IsDragging = false;
            Owner?.OnDragEnded();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoverTable >= 0 && _gripDrag is null)
        {
            _hoverTable = -1;
            InvalidateVisual();
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
