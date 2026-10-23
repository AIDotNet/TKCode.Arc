using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>One line of an <see cref="AiSuggestionList"/>: a group heading or a row, with its place in the list.</summary>
/// <param name="RowIndex">The index of the row in the shown rows, or -1 for a heading.</param>
/// <param name="Heading">The heading text (headings only).</param>
/// <param name="Y">Top, in list coordinates.</param>
/// <param name="Height">Height.</param>
internal readonly record struct AiSuggestionEntry(int RowIndex, string? Heading, double Y, double Height)
{
    public bool IsHeading => RowIndex < 0;

    public double Bottom => Y + Height;
}

/// <summary>
/// The rows of an <see cref="AiSuggestionPop"/> (Arc's <c>.pop</c> options): headings (mentions only, 12 / 16.8 muted,
/// padding 6 10 4) and rows (min 44, two lines 46.4; padding 5 10; 10px gap; 16px icon at stroke 1.75; title 14 / 19.6
/// and hint 12 / 16.8, one line each with an ellipsis). The active row has a SurfaceMuted fill (radius 15) that jumps
/// with the active row: Arc draws it without a transition. Rows and headings are light render-based controls kept in
/// a pool; heights are fixed by the font sizes, so the layout needs no text measurement.
/// </summary>
internal sealed class AiSuggestionList : Control
{
    public static readonly StyledProperty<IBrush?> ActiveBackgroundProperty =
        AvaloniaProperty.Register<AiSuggestionList, IBrush?>(nameof(ActiveBackground));

    public static readonly StyledProperty<IBrush?> TitleForegroundProperty =
        AvaloniaProperty.Register<AiSuggestionList, IBrush?>(nameof(TitleForeground));

    public static readonly StyledProperty<IBrush?> HintForegroundProperty =
        AvaloniaProperty.Register<AiSuggestionList, IBrush?>(nameof(HintForeground));

    public static readonly StyledProperty<IBrush?> IconForegroundProperty =
        AvaloniaProperty.Register<AiSuggestionList, IBrush?>(nameof(IconForeground));

    public static readonly StyledProperty<IBrush?> HeadingForegroundProperty =
        AvaloniaProperty.Register<AiSuggestionList, IBrush?>(nameof(HeadingForeground));

    /// <summary>Title size (TypeSM, 14); its line is 1.4 times as high.</summary>
    public static readonly StyledProperty<double> TitleFontSizeProperty =
        AvaloniaProperty.Register<AiSuggestionList, double>(nameof(TitleFontSize), 14);

    /// <summary>Hint and heading size (TypeXS, 12); their lines are 1.4 times as high.</summary>
    public static readonly StyledProperty<double> HintFontSizeProperty =
        AvaloniaProperty.Register<AiSuggestionList, double>(nameof(HintFontSize), 12);

    internal const double Leading = 1.4;
    internal const double RowMinHeight = 44;
    internal const double RowPaddingX = 10;
    internal const double RowPaddingY = 5;
    internal const double RowRadius = 15;
    internal const double IconSize = 16;
    internal const double IconStroke = 1.75;
    internal const double Gap = 10;
    internal const double HeadingPaddingTop = 6;
    internal const double HeadingPaddingBottom = 4;

    /// <summary>Letter spacing of the body text, −0.01em.</summary>
    internal const double Tracking = -0.01;

    private readonly List<AiSuggestionRowView> _rows = new();
    private readonly List<AiSuggestionHeadingView> _headings = new();
    private readonly List<AiSuggestionEntry> _entries = new();
    private IReadOnlyList<AiSuggestionRow> _data = Array.Empty<AiSuggestionRow>();
    private AiSuggestionKind _kind;
    private int _active = -1;
    private bool _hasIcons;

    static AiSuggestionList()
    {
        AffectsRender<AiSuggestionList>(ActiveBackgroundProperty);
        AffectsMeasure<AiSuggestionList>(TitleFontSizeProperty, HintFontSizeProperty);
        FocusableProperty.OverrideDefaultValue<AiSuggestionList>(false);
    }

    public IBrush? ActiveBackground { get => GetValue(ActiveBackgroundProperty); set => SetValue(ActiveBackgroundProperty, value); }

    public IBrush? TitleForeground { get => GetValue(TitleForegroundProperty); set => SetValue(TitleForegroundProperty, value); }

    public IBrush? HintForeground { get => GetValue(HintForegroundProperty); set => SetValue(HintForegroundProperty, value); }

    public IBrush? IconForeground { get => GetValue(IconForegroundProperty); set => SetValue(IconForegroundProperty, value); }

    public IBrush? HeadingForeground { get => GetValue(HeadingForegroundProperty); set => SetValue(HeadingForegroundProperty, value); }

    public double TitleFontSize { get => GetValue(TitleFontSizeProperty); set => SetValue(TitleFontSizeProperty, value); }

    public double HintFontSize { get => GetValue(HintFontSizeProperty); set => SetValue(HintFontSizeProperty, value); }

    /// <summary>The pop that owns the list (hover, click and the accessible name go through it).</summary>
    internal AiSuggestionPop? Owner { get; set; }

    internal AiSuggestionKind Kind => _kind;

    internal IReadOnlyList<AiSuggestionRow> Data => _data;

    internal int ActiveIndex => _active;

    /// <summary>Headings and rows in order, with their positions (list coordinates).</summary>
    internal IReadOnlyList<AiSuggestionEntry> Entries => _entries;

    /// <summary>The realized row views; the first <see cref="Data"/>.Count are in use.</summary>
    internal IReadOnlyList<AiSuggestionRowView> RowViews => _rows;

    internal IReadOnlyList<AiSuggestionHeadingView> HeadingViews => _headings;

    /// <summary>Whether rows keep a 16px icon slot (some row has an icon).</summary>
    internal bool HasIcons => _hasIcons;

    internal double TitleLineHeight => TitleFontSize * Leading;

    internal double HintLineHeight => HintFontSize * Leading;

    /// <summary>Shows <paramref name="rows"/> with <paramref name="active"/> highlighted. Views are pooled and reused.</summary>
    public void SetRows(AiSuggestionKind kind, IReadOnlyList<AiSuggestionRow> rows, int active)
    {
        _kind = kind;
        _data = rows;
        _active = active;
        _hasIcons = false;
        foreach (var row in rows)
        {
            if (row.Icon is not null)
            {
                _hasIcons = true;
                break;
            }
        }
        Layout(kind, rows, TitleLineHeight, HintLineHeight, _entries);
        while (_rows.Count < rows.Count)
        {
            var view = new AiSuggestionRowView(this);
            _rows.Add(view);
            VisualChildren.Add(view);
            LogicalChildren.Add(view);
        }
        var headingCount = 0;
        foreach (var entry in _entries)
        {
            if (!entry.IsHeading)
            {
                continue;
            }
            if (_headings.Count <= headingCount)
            {
                var heading = new AiSuggestionHeadingView(this);
                _headings.Add(heading);
                VisualChildren.Add(heading);
                LogicalChildren.Add(heading);
            }
            _headings[headingCount++].SetText(entry.Heading!);
        }
        for (var i = 0; i < _rows.Count; i++)
        {
            var view = _rows[i];
            if (i < rows.Count)
            {
                view.IsVisible = true;
                view.SetData(i, rows[i], i == active);
                AutomationProperties.SetPositionInSet(view, i + 1);
                AutomationProperties.SetSizeOfSet(view, rows.Count);
            }
            else
            {
                view.IsVisible = false;
            }
        }
        for (var i = 0; i < _headings.Count; i++)
        {
            _headings[i].IsVisible = i < headingCount;
        }
        InvalidateMeasure();
    }

    /// <summary>Moves the highlight to <paramref name="index"/> (no layout: the fill is drawn by the rows).</summary>
    public void SetActive(int index)
    {
        if (index == _active)
        {
            return;
        }
        if (_active >= 0 && _active < _data.Count)
        {
            _rows[_active].SetActive(false);
        }
        _active = index;
        if (index >= 0 && index < _data.Count)
        {
            _rows[index].SetActive(true);
        }
    }

    /// <summary>The entry of row <paramref name="rowIndex"/>, or null.</summary>
    internal AiSuggestionEntry? FindRow(int rowIndex)
    {
        foreach (var entry in _entries)
        {
            if (entry.RowIndex == rowIndex)
            {
                return entry;
            }
        }
        return null;
    }

    /// <summary>
    /// Lays out headings and rows top to bottom from y = 0: a heading before the first row of each named group (mentions
    /// only), rows 44 high or (with a hint) 5 + title line + hint line + 5. Pure, so the popup can size the list before
    /// it is shown.
    /// </summary>
    internal static double Layout(AiSuggestionKind kind, IReadOnlyList<AiSuggestionRow> rows, double titleLine, double hintLine,
                                  List<AiSuggestionEntry>? entries)
    {
        entries?.Clear();
        var y = 0.0;
        string? group = null;
        var headingHeight = HeadingPaddingTop + hintLine + HeadingPaddingBottom;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (kind == AiSuggestionKind.Mention && row.Group is { } name && !string.Equals(name, group, StringComparison.Ordinal))
            {
                entries?.Add(new AiSuggestionEntry(-1, name, y, headingHeight));
                y += headingHeight;
            }
            group = kind == AiSuggestionKind.Mention ? row.Group : null;
            var height = Math.Max(RowMinHeight, 2 * RowPaddingY + titleLine + (string.IsNullOrEmpty(row.Hint) ? 0 : hintLine));
            entries?.Add(new AiSuggestionEntry(i, null, y, height));
            y += height;
        }
        return y;
    }

    internal void OnRowHovered(int index) => Owner?.SetActive(index);

    internal void OnRowClicked(int index) => Owner?.AcceptAt(index);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        var height = 0.0;
        var headingIndex = 0;
        foreach (var entry in _entries)
        {
            Control view = entry.IsHeading ? _headings[headingIndex++] : _rows[entry.RowIndex];
            view.Measure(new Size(width, entry.Height));
            height = entry.Bottom;
        }
        foreach (var row in _rows)
        {
            if (!row.IsVisible)
            {
                row.Measure(default);
            }
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var headingIndex = 0;
        foreach (var entry in _entries)
        {
            Control view = entry.IsHeading ? _headings[headingIndex++] : _rows[entry.RowIndex];
            view.Arrange(new Rect(0, entry.Y, finalSize.Width, entry.Height));
        }
        for (var i = _data.Count; i < _rows.Count; i++)
        {
            _rows[i].Arrange(default);
        }
        for (var i = headingIndex; i < _headings.Count; i++)
        {
            _headings[i].Arrange(default);
        }
        return finalSize;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleForegroundProperty || change.Property == HintForegroundProperty ||
            change.Property == IconForegroundProperty || change.Property == HeadingForegroundProperty ||
            change.Property == TitleFontSizeProperty || change.Property == HintFontSizeProperty ||
            change.Property == TextElement.FontFamilyProperty)
        {
            foreach (var row in _rows)
            {
                row.InvalidateText();
            }
            foreach (var heading in _headings)
            {
                heading.InvalidateText();
            }
        }
        if (change.Property == TitleFontSizeProperty || change.Property == HintFontSizeProperty)
        {
            Layout(_kind, _data, TitleLineHeight, HintLineHeight, _entries);
        }
        if (change.Property == ActiveBackgroundProperty && _active >= 0 && _active < _data.Count)
        {
            _rows[_active].InvalidateVisual();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ListPeer(this);

    private sealed class ListPeer(AiSuggestionList owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override string? GetNameCore() => ((AiSuggestionList)Owner).Owner?.ListName ?? base.GetNameCore();
    }
}

/// <summary>A group heading of the mention list (not a row: it cannot be hovered, chosen or reached by assistive technology).</summary>
internal sealed class AiSuggestionHeadingView : Control
{
    private readonly AiSuggestionList _list;
    private string _text = string.Empty;
    private TextLayout? _layout;
    private double _layoutWidth = -1;

    public AiSuggestionHeadingView(AiSuggestionList list)
    {
        _list = list;
        IsHitTestVisible = false;
        Focusable = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    internal string Text => _text;

    public void SetText(string text)
    {
        if (string.Equals(_text, text, StringComparison.Ordinal))
        {
            return;
        }
        _text = text;
        InvalidateText();
    }

    internal void InvalidateText()
    {
        _layout?.Dispose();
        _layout = null;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
            AiSuggestionList.HeadingPaddingTop + _list.HintLineHeight + AiSuggestionList.HeadingPaddingBottom);

    public override void Render(DrawingContext context)
    {
        var maxWidth = Math.Max(0, Bounds.Width - 2 * AiSuggestionList.RowPaddingX);
        if (_layout is null || Math.Abs(_layoutWidth - maxWidth) > 0.01)
        {
            _layout?.Dispose();
            var size = _list.HintFontSize;
            _layout = new TextLayout(_text, new Typeface(TextElement.GetFontFamily(this)), size, _list.HeadingForeground,
                TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, maxWidth,
                double.PositiveInfinity, _list.HintLineHeight, size * AiSuggestionList.Tracking);
            _layoutWidth = maxWidth;
        }
        _layout.Draw(context, new Point(AiSuggestionList.RowPaddingX, AiSuggestionList.HeadingPaddingTop));
    }
}

/// <summary>
/// One row of the list: the active fill, the icon and the title over the hint, all drawn in <see cref="Render"/>. The
/// pointer makes the row active on move (not for touch) and chooses it on a click; a press never moves focus, so the
/// prompt keeps the caret.
/// </summary>
internal sealed class AiSuggestionRowView : Control
{
    private readonly AiSuggestionList _list;
    private AiSuggestionRow? _row;
    private AiSuggestionKind _kind;
    private bool _active;
    private bool _pressed;
    private TextLayout? _title;
    private TextLayout? _hint;
    private double _layoutWidth = -1;
    private Pen? _iconPen;
    private IBrush? _iconPenBrush;

    public AiSuggestionRowView(AiSuggestionList list)
    {
        _list = list;
        Focusable = false;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    internal int Index { get; private set; } = -1;

    internal AiSuggestionRow? Row => _row;

    internal bool IsActive => _active;

    /// <summary>The text the row shows as its title: "/" + title for commands.</summary>
    internal string DisplayTitle => _row is null ? string.Empty : _kind == AiSuggestionKind.Command ? "/" + _row.Title : _row.Title;

    /// <summary>The accessible name: the shown title and the hint.</summary>
    internal string AccessibleName => _row?.Hint is { Length: > 0 } hint ? DisplayTitle + ", " + hint : DisplayTitle;

    /// <summary>The title as drawn (tests).</summary>
    internal TextLayout? TitleLayout => _title;

    public void SetData(int index, AiSuggestionRow row, bool active)
    {
        Index = index;
        _active = active;
        if (!ReferenceEquals(_row, row) || _kind != _list.Kind)
        {
            _row = row;
            _kind = _list.Kind;
            InvalidateText();
        }
        else
        {
            InvalidateVisual();
        }
    }

    public void SetActive(bool active)
    {
        if (_active == active)
        {
            return;
        }
        _active = active;
        InvalidateVisual();
    }

    internal void InvalidateText()
    {
        _title?.Dispose();
        _hint?.Dispose();
        _title = _hint = null;
        _layoutWidth = -1;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? AiSuggestionList.RowMinHeight : availableSize.Height);

    private double TextLeft => AiSuggestionList.RowPaddingX + (_list.HasIcons ? AiSuggestionList.IconSize + AiSuggestionList.Gap : 0);

    private void EnsureText(double width)
    {
        var maxWidth = Math.Max(0, width - TextLeft - AiSuggestionList.RowPaddingX);
        if (_title is not null && Math.Abs(_layoutWidth - maxWidth) < 0.01)
        {
            return;
        }
        _title?.Dispose();
        _hint?.Dispose();
        _hint = null;
        _layoutWidth = maxWidth;
        var face = new Typeface(TextElement.GetFontFamily(this));
        var titleSize = _list.TitleFontSize;
        _title = new TextLayout(DisplayTitle, face, titleSize, _list.TitleForeground, TextAlignment.Left, TextWrapping.NoWrap,
            TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, maxWidth, double.PositiveInfinity, _list.TitleLineHeight,
            titleSize * AiSuggestionList.Tracking);
        if (_row?.Hint is { Length: > 0 } hint)
        {
            var hintSize = _list.HintFontSize;
            _hint = new TextLayout(hint, face, hintSize, _list.HintForeground, TextAlignment.Left, TextWrapping.NoWrap,
                TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, maxWidth, double.PositiveInfinity, _list.HintLineHeight,
                hintSize * AiSuggestionList.Tracking);
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        // The whole row is the hit target, not only its glyphs.
        context.FillRectangle(Brushes.Transparent, bounds);
        if (_row is null)
        {
            return;
        }
        if (_active && _list.ActiveBackground is { } fill)
        {
            context.DrawRectangle(fill, null, bounds, AiSuggestionList.RowRadius, AiSuggestionList.RowRadius);
        }
        if (_row.Icon is { } icon && _list.IconForeground is { } iconBrush)
        {
            DrawIcon(context, icon, iconBrush, bounds.Height);
        }
        EnsureText(bounds.Width);
        var title = _title!;
        var textHeight = _list.TitleLineHeight + (_hint is null ? 0 : _list.HintLineHeight);
        var y = (bounds.Height - textHeight) / 2;
        title.Draw(context, new Point(TextLeft, y));
        _hint?.Draw(context, new Point(TextLeft, y + _list.TitleLineHeight));
    }

    private void DrawIcon(DrawingContext context, Geometry icon, IBrush brush, double height)
    {
        // A Lucide glyph at 16px: stroke 1.75 in its 24-unit view box, so it scales with the icon (lucide's strokeWidth).
        if (_iconPen is null || !ReferenceEquals(_iconPenBrush, brush))
        {
            _iconPen = new Pen(brush, AiSuggestionList.IconStroke, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _iconPenBrush = brush;
        }
        const double scale = AiSuggestionList.IconSize / 24;
        var top = (height - AiSuggestionList.IconSize) / 2;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(AiSuggestionList.RowPaddingX, top)))
        {
            context.DrawGeometry(null, _iconPen, icon);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        // Arc: hovering makes the row active; hover never follows touch.
        if (e.Pointer.Type != PointerType.Touch && Index >= 0)
        {
            _list.OnRowHovered(Index);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // Arc prevents the press default so the prompt keeps focus; nothing in the list is focusable either.
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
        }
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var pressed = _pressed;
        _pressed = false;
        if (pressed && e.InitialPressMouseButton == MouseButton.Left && new Rect(Bounds.Size).Contains(e.GetPosition(this)) && Index >= 0)
        {
            e.Handled = true;
            _list.OnRowClicked(Index);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    private sealed class RowPeer(AiSuggestionRowView owner) : ControlAutomationPeer(owner), ISelectionItemProvider, IInvokeProvider
    {
        private AiSuggestionRowView Row => (AiSuggestionRowView)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string? GetNameCore() => Row._row is null ? null : Row.AccessibleName;

        public bool IsSelected => Row._active;

        public ISelectionProvider? SelectionContainer => null;

        public void Select()
        {
            if (Row.Index >= 0)
            {
                Row._list.OnRowHovered(Row.Index);
            }
        }

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }

        public void Invoke()
        {
            if (Row.Index >= 0)
            {
                Row._list.OnRowClicked(Row.Index);
            }
        }
    }
}
