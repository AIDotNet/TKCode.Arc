// The list of the suggest widget (upstream's List of suggestWidget.ts with the ItemRenderer of
// suggestWidgetRenderer.ts, commit b57d5c9e, MIT license): one row per completion with the kind icon, the label with the
// matched characters highlighted, the label's detail after it and the description (or, on the focused row, the item's
// detail) on the right. Only the visible rows are drawn (virtualized), so a list of thousands of items costs what
// twelve rows cost. Every color and size comes from the properties the widget's theme sets.
// Differences from upstream: no codicons (kind icons are Lucide glyphs from ArcIcons), no color swatches for color
// items, no file icons, no "read more" button on the row (Ctrl+Space toggles the details).

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Suggest;
using TKCode.Arc.Editor.Languages;

namespace TKCode.Arc;

/// <summary>The rows of the suggest widget, drawn and virtualized.</summary>
internal sealed class CodeEditorSuggestList : Control, ICustomHitTest
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<CodeEditorSuggestList>();
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<CodeEditorSuggestList>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<CodeEditorSuggestList>();

    /// <summary>Defines the <see cref="HighlightBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> HighlightBrushProperty = AvaloniaProperty.Register<CodeEditorSuggestList, IBrush?>(nameof(HighlightBrush));

    /// <summary>Defines the <see cref="MutedBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> MutedBrushProperty = AvaloniaProperty.Register<CodeEditorSuggestList, IBrush?>(nameof(MutedBrush));

    /// <summary>Defines the <see cref="IconBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> IconBrushProperty = AvaloniaProperty.Register<CodeEditorSuggestList, IBrush?>(nameof(IconBrush));

    /// <summary>Defines the <see cref="FocusedBackground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> FocusedBackgroundProperty = AvaloniaProperty.Register<CodeEditorSuggestList, IBrush?>(nameof(FocusedBackground));

    /// <summary>Defines the <see cref="FocusedForeground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> FocusedForegroundProperty = AvaloniaProperty.Register<CodeEditorSuggestList, IBrush?>(nameof(FocusedForeground));

    /// <summary>Defines the <see cref="HoverBackground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> HoverBackgroundProperty = AvaloniaProperty.Register<CodeEditorSuggestList, IBrush?>(nameof(HoverBackground));

    /// <summary>Defines the <see cref="RowHeight"/> property.</summary>
    public static readonly StyledProperty<double> RowHeightProperty = AvaloniaProperty.Register<CodeEditorSuggestList, double>(nameof(RowHeight), 24);

    /// <summary>Defines the <see cref="MaxVisibleItems"/> property.</summary>
    public static readonly StyledProperty<int> MaxVisibleItemsProperty = AvaloniaProperty.Register<CodeEditorSuggestList, int>(nameof(MaxVisibleItems), 12);

    /// <summary>Defines the <see cref="IconSize"/> property.</summary>
    public static readonly StyledProperty<double> IconSizeProperty = AvaloniaProperty.Register<CodeEditorSuggestList, double>(nameof(IconSize), 14);

    /// <summary>Defines the <see cref="IconStrokeWidth"/> property.</summary>
    public static readonly StyledProperty<double> IconStrokeWidthProperty = AvaloniaProperty.Register<CodeEditorSuggestList, double>(nameof(IconStrokeWidth), 2);

    /// <summary>Defines the <see cref="RowPadding"/> property.</summary>
    public static readonly StyledProperty<double> RowPaddingProperty = AvaloniaProperty.Register<CodeEditorSuggestList, double>(nameof(RowPadding), 6);

    /// <summary>Defines the <see cref="RowSpacing"/> property.</summary>
    public static readonly StyledProperty<double> RowSpacingProperty = AvaloniaProperty.Register<CodeEditorSuggestList, double>(nameof(RowSpacing), 6);

    /// <summary>Defines the <see cref="RowCornerRadius"/> property.</summary>
    public static readonly StyledProperty<double> RowCornerRadiusProperty = AvaloniaProperty.Register<CodeEditorSuggestList, double>(nameof(RowCornerRadius), 4);

    /// <summary>Defines the <see cref="ListWidth"/> property.</summary>
    public static readonly StyledProperty<double> ListWidthProperty = AvaloniaProperty.Register<CodeEditorSuggestList, double>(nameof(ListWidth), 430);

    private IReadOnlyList<SuggestCompletionItem> _items = [];
    private int _focusedIndex = -1;
    private int _hoverIndex = -1;
    private int _scrollTop;
    private bool _showFocusedDetail = true;
    private int _rowLimit = int.MaxValue;

    static CodeEditorSuggestList()
    {
        AffectsRender<CodeEditorSuggestList>(
            ForegroundProperty, HighlightBrushProperty, MutedBrushProperty, IconBrushProperty, FocusedBackgroundProperty,
            FocusedForegroundProperty, HoverBackgroundProperty, FontFamilyProperty, FontSizeProperty, IconSizeProperty,
            IconStrokeWidthProperty, RowPaddingProperty, RowSpacingProperty, RowCornerRadiusProperty);
        AffectsMeasure<CodeEditorSuggestList>(RowHeightProperty, MaxVisibleItemsProperty, ListWidthProperty);
    }

    public CodeEditorSuggestList()
    {
        ClipToBounds = true;
        Focusable = false;
    }

    /// <summary>The text color of the rows.</summary>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>The font of the rows.</summary>
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <summary>The font size of the rows.</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>The color of the characters that match what was typed.</summary>
    public IBrush? HighlightBrush { get => GetValue(HighlightBrushProperty); set => SetValue(HighlightBrushProperty, value); }

    /// <summary>The color of details and descriptions.</summary>
    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }

    /// <summary>The color of the kind icons.</summary>
    public IBrush? IconBrush { get => GetValue(IconBrushProperty); set => SetValue(IconBrushProperty, value); }

    /// <summary>The background of the focused row.</summary>
    public IBrush? FocusedBackground { get => GetValue(FocusedBackgroundProperty); set => SetValue(FocusedBackgroundProperty, value); }

    /// <summary>The text color of the focused row.</summary>
    public IBrush? FocusedForeground { get => GetValue(FocusedForegroundProperty); set => SetValue(FocusedForegroundProperty, value); }

    /// <summary>The background of the row under the pointer.</summary>
    public IBrush? HoverBackground { get => GetValue(HoverBackgroundProperty); set => SetValue(HoverBackgroundProperty, value); }

    /// <summary>The height of one row.</summary>
    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

    /// <summary>The most rows shown before the list scrolls.</summary>
    public int MaxVisibleItems { get => GetValue(MaxVisibleItemsProperty); set => SetValue(MaxVisibleItemsProperty, value); }

    /// <summary>The size of the kind icons.</summary>
    public double IconSize { get => GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }

    /// <summary>The stroke width of the kind icons in their 24 × 24 box.</summary>
    public double IconStrokeWidth { get => GetValue(IconStrokeWidthProperty); set => SetValue(IconStrokeWidthProperty, value); }

    /// <summary>The space left and right inside a row.</summary>
    public double RowPadding { get => GetValue(RowPaddingProperty); set => SetValue(RowPaddingProperty, value); }

    /// <summary>The space between the icon, the label and the details.</summary>
    public double RowSpacing { get => GetValue(RowSpacingProperty); set => SetValue(RowSpacingProperty, value); }

    /// <summary>The corner radius of the focused row.</summary>
    public double RowCornerRadius { get => GetValue(RowCornerRadiusProperty); set => SetValue(RowCornerRadiusProperty, value); }

    /// <summary>The width of the list.</summary>
    public double ListWidth { get => GetValue(ListWidthProperty); set => SetValue(ListWidthProperty, value); }

    /// <summary>The items, best first.</summary>
    public IReadOnlyList<SuggestCompletionItem> Items => _items;

    /// <summary>The focused row, or -1.</summary>
    public int FocusedIndex => _focusedIndex;

    /// <summary>The first row shown.</summary>
    public int ScrollTop => _scrollTop;

    /// <summary>How many rows fit.</summary>
    public int VisibleCount => Math.Max(1, Math.Min(PageSize, _items.Count));

    /// <summary>The most rows shown: <see cref="MaxVisibleItems"/>, fewer when the editor has no room for them.</summary>
    public int PageSize => Math.Max(1, Math.Min(MaxVisibleItems, _rowLimit));

    /// <summary>The most rows the editor has room for (set by the widget from where it is placed).</summary>
    public int RowLimit
    {
        get => _rowLimit;
        set
        {
            value = Math.Max(1, value);
            if (_rowLimit != value)
            {
                _rowLimit = value;
                ClampScroll();
                InvalidateMeasure();
                InvalidateVisual();
            }
        }
    }

    /// <summary>Whether the focused row shows the item's detail on the right (off while the details pane shows it).</summary>
    public bool ShowFocusedDetail
    {
        get => _showFocusedDetail;
        set
        {
            if (_showFocusedDetail != value)
            {
                _showFocusedDetail = value;
                InvalidateVisual();
            }
        }
    }

    /// <summary>A row was clicked.</summary>
    public event Action<int>? RowClicked;

    /// <summary>Replaces the items; nothing is focused.</summary>
    public void SetItems(IReadOnlyList<SuggestCompletionItem> items)
    {
        _items = items;
        _focusedIndex = -1;
        _hoverIndex = -1;
        _scrollTop = 0;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Focuses a row (-1: none) and scrolls it into view.</summary>
    public void SetFocus(int index)
    {
        _focusedIndex = index < 0 || index >= _items.Count ? -1 : index;
        Reveal(_focusedIndex);
        InvalidateVisual();
    }

    /// <summary>Scrolls so that the row is visible.</summary>
    public void Reveal(int index)
    {
        if (index < 0)
        {
            return;
        }
        var visible = Math.Min(PageSize, _items.Count);
        if (index < _scrollTop)
        {
            _scrollTop = index;
        }
        else if (index >= _scrollTop + visible)
        {
            _scrollTop = index - visible + 1;
        }
        ClampScroll();
        InvalidateVisual();
    }

    private void ClampScroll() => _scrollTop = Math.Clamp(_scrollTop, 0, Math.Max(0, _items.Count - PageSize));

    /// <summary>The whole list takes the pointer, not only what it draws.</summary>
    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    /// <summary>The row at a point, or -1.</summary>
    public int IndexAt(Point point)
    {
        if (point.Y < 0 || point.Y >= Bounds.Height || RowHeight <= 0)
        {
            return -1;
        }
        var index = _scrollTop + (int)(point.Y / RowHeight);
        return index < _items.Count ? index : -1;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = Math.Min(ListWidth, double.IsInfinity(availableSize.Width) ? ListWidth : availableSize.Width);
        return new Size(width, VisibleCount * RowHeight);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var index = IndexAt(e.GetPosition(this));
        if (index >= 0)
        {
            e.Handled = true;
            RowClicked?.Invoke(index);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var index = IndexAt(e.GetPosition(this));
        if (index != _hoverIndex)
        {
            _hoverIndex = index;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hoverIndex = -1;
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var old = _scrollTop;
        _scrollTop -= Math.Sign(e.Delta.Y) * Math.Max(1, (int)Math.Round(Math.Abs(e.Delta.Y)));
        ClampScroll();
        if (old != _scrollTop)
        {
            InvalidateVisual();
        }
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var rowHeight = RowHeight;
        if (_items.Count == 0 || rowHeight <= 0)
        {
            return;
        }

        var typeface = new Typeface(FontFamily);
        var width = Bounds.Width;
        var visible = Math.Min(PageSize, _items.Count - _scrollTop);
        for (var row = 0; row < visible; row++)
        {
            var index = _scrollTop + row;
            var rect = new Rect(0, row * rowHeight, width, rowHeight);
            DrawRow(context, _items[index], index, rect, typeface);
        }

        DrawScrollIndicator(context, rowHeight);
    }

    private void DrawRow(DrawingContext context, SuggestCompletionItem item, int index, Rect rect, Typeface typeface)
    {
        var focused = index == _focusedIndex;
        var background = focused ? FocusedBackground : index == _hoverIndex ? HoverBackground : null;
        if (background is not null)
        {
            var radius = RowCornerRadius;
            context.DrawRectangle(background, null, new RoundedRect(rect, radius));
        }

        if (((focused ? FocusedForeground : null) ?? Foreground) is not { } foreground)
        {
            return;
        }
        var muted = MutedBrush ?? foreground;
        var x = rect.X + RowPadding;
        var right = rect.Right - RowPadding;

        // kind icon
        var iconSize = IconSize;
        var icon = CodeEditorSuggestIcons.For(item.Completion.Kind);
        var iconTop = rect.Y + (rect.Height - iconSize) / 2;
        var scale = iconSize / 24;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, iconTop)))
        {
            var pen = new Pen(IconBrush ?? muted, IconStrokeWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            context.DrawGeometry(null, pen, icon);
        }
        x += iconSize + RowSpacing;

        var completion = item.Completion;
        var fontSize = FontSize;

        // right: the description, or the focused item's detail
        var rightText = completion.Label.Description;
        if (string.IsNullOrEmpty(rightText) && focused && _showFocusedDetail)
        {
            rightText = StripNewLines(completion.Detail);
        }

        // label + label detail
        var labelDetail = completion.Label.Detail ?? string.Empty;
        var label = item.TextLabel;
        var text = new FormattedText(label + labelDetail, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, fontSize, foreground)
        {
            Trimming = TextTrimming.CharacterEllipsis,
            MaxLineCount = 1,
        };
        if (labelDetail.Length > 0)
        {
            text.SetForegroundBrush(muted, label.Length, labelDetail.Length);
        }
        var highlight = HighlightBrush ?? foreground;
        foreach (var match in Filters.CreateMatches(item.Score))
        {
            var start = Math.Clamp(match.Start, 0, label.Length);
            var end = Math.Clamp(match.End, start, label.Length);
            if (end > start)
            {
                text.SetForegroundBrush(highlight, start, end - start);
                text.SetFontWeight(FontWeight.Bold, start, end - start);
            }
        }
        var deprecated = completion.Tags is { } tags && tags.Contains(CompletionItemTag.Deprecated);

        FormattedText? rightFormatted = null;
        var available = right - x;
        if (!string.IsNullOrEmpty(rightText))
        {
            rightFormatted = new FormattedText(rightText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, fontSize, muted)
            {
                Trimming = TextTrimming.CharacterEllipsis,
                MaxLineCount = 1,
                MaxTextWidth = Math.Max(0, available * 0.45),
            };
        }

        var labelWidth = available - (rightFormatted is null ? 0 : rightFormatted.WidthIncludingTrailingWhitespace + RowSpacing);
        text.MaxTextWidth = Math.Max(1, labelWidth);
        var textTop = rect.Y + (rect.Height - text.Height) / 2;
        context.DrawText(text, new Point(x, textTop));

        if (deprecated)
        {
            // struck through (upstream's deprecated class)
            var labelOnly = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, fontSize, foreground);
            var strikeY = Math.Round(textTop + text.Baseline - fontSize * 0.3) + 0.5;
            var strikeEnd = x + Math.Min(labelOnly.WidthIncludingTrailingWhitespace, text.MaxTextWidth);
            context.DrawLine(new Pen(foreground, 1), new Point(x, strikeY), new Point(strikeEnd, strikeY));
        }

        if (rightFormatted is not null)
        {
            var w = rightFormatted.WidthIncludingTrailingWhitespace;
            context.DrawText(rightFormatted, new Point(right - w, rect.Y + (rect.Height - rightFormatted.Height) / 2));
        }
    }

    private void DrawScrollIndicator(DrawingContext context, double rowHeight)
    {
        var count = _items.Count;
        var visible = PageSize;
        if (count <= visible || MutedBrush is not { } brush)
        {
            return;
        }

        var height = Bounds.Height;
        var thumbHeight = Math.Max(rowHeight, height * visible / count);
        var top = (height - thumbHeight) * _scrollTop / Math.Max(1, count - visible);
        var thumb = new Rect(Bounds.Width - 3, top, 2, thumbHeight);
        using (context.PushOpacity(0.5))
        {
            context.DrawRectangle(brush, null, new RoundedRect(thumb, 1));
        }
    }

    private static string? StripNewLines(string? text) =>
        text is null ? null : text.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ').Replace('\r', ' ');
}

/// <summary>The kind icons of the suggest widget: Lucide glyphs in a 24 × 24 box (upstream: the symbol codicons).</summary>
internal static class CodeEditorSuggestIcons
{
    public static Geometry For(CompletionItemKind kind) => kind switch
    {
        CompletionItemKind.Method or CompletionItemKind.Function or CompletionItemKind.Constructor => ArcIcons.Box,
        CompletionItemKind.Field => ArcIcons.Tag,
        CompletionItemKind.Variable => ArcIcons.SquareCode,
        CompletionItemKind.Class => ArcIcons.Layers,
        CompletionItemKind.Struct => ArcIcons.LayoutGrid,
        CompletionItemKind.Interface => ArcIcons.Link,
        CompletionItemKind.Module => ArcIcons.Package,
        CompletionItemKind.Property => ArcIcons.Wrench,
        CompletionItemKind.Event => ArcIcons.Zap,
        CompletionItemKind.Operator => ArcIcons.Equal,
        CompletionItemKind.Unit => ArcIcons.Gauge,
        CompletionItemKind.Value or CompletionItemKind.Constant => ArcIcons.Hash,
        CompletionItemKind.Enum or CompletionItemKind.EnumMember => ArcIcons.ListOrdered,
        CompletionItemKind.Keyword => ArcIcons.Key,
        CompletionItemKind.Text => ArcIcons.Type,
        CompletionItemKind.Color or CompletionItemKind.Customcolor => ArcIcons.Palette,
        CompletionItemKind.File => ArcIcons.File,
        CompletionItemKind.Reference => ArcIcons.ExternalLink,
        CompletionItemKind.Folder => ArcIcons.Folder,
        CompletionItemKind.TypeParameter => ArcIcons.Code,
        CompletionItemKind.User => ArcIcons.User,
        CompletionItemKind.Issue => ArcIcons.CircleDot,
        CompletionItemKind.Tool => ArcIcons.Settings,
        CompletionItemKind.Snippet => ArcIcons.SquareDashedText,
        _ => ArcIcons.Circle,
    };
}
