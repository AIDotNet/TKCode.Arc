using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>A titled run of emoji in an <see cref="EmojiPickerGrid"/>.</summary>
internal sealed class EmojiPickerSection
{
    public EmojiPickerSection(EmojiCategory category, string title, IReadOnlyList<EmojiPickerEmoji> items)
    {
        Category = category;
        Title = title;
        Items = items;
    }

    /// <summary>The section's category (<see cref="EmojiCategory.Recent"/> also titles the search results).</summary>
    public EmojiCategory Category { get; }

    public string Title { get; }

    public IReadOnlyList<EmojiPickerEmoji> Items { get; }
}

/// <summary>
/// The scrolling body of an <see cref="EmojiPicker"/>: section titles and rows of emoji, drawn by one control. It sits
/// in a <see cref="ScrollViewer"/> at its full height but draws only the rows in the viewport, so two thousand emoji
/// cost what one screen costs; each emoji's text layout is made the first time it shows and kept. The title of the
/// section under the top edge stays pinned there. One emoji is active: the arrow keys, Home, End, Page Up and Page
/// Down move it, Enter or Space picks it, and the pointer makes the emoji under it active.
/// </summary>
internal sealed class EmojiPickerGrid : Control
{
    public static readonly StyledProperty<double> CellSizeProperty =
        AvaloniaProperty.Register<EmojiPickerGrid, double>(nameof(CellSize), 36);

    public static readonly StyledProperty<double> EmojiFontSizeProperty =
        AvaloniaProperty.Register<EmojiPickerGrid, double>(nameof(EmojiFontSize), 22);

    public static readonly StyledProperty<FontFamily> EmojiFontFamilyProperty =
        AvaloniaProperty.Register<EmojiPickerGrid, FontFamily>(nameof(EmojiFontFamily), FontFamily.Default);

    public static readonly StyledProperty<double> HeaderHeightProperty =
        AvaloniaProperty.Register<EmojiPickerGrid, double>(nameof(HeaderHeight), 28);

    public static readonly StyledProperty<double> HeaderFontSizeProperty =
        AvaloniaProperty.Register<EmojiPickerGrid, double>(nameof(HeaderFontSize), 12);

    public static readonly StyledProperty<IBrush?> HeaderForegroundProperty =
        AvaloniaProperty.Register<EmojiPickerGrid, IBrush?>(nameof(HeaderForeground));

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<EmojiPickerGrid, IBrush?>(nameof(Background));

    public static readonly StyledProperty<IBrush?> HoverFillProperty =
        AvaloniaProperty.Register<EmojiPickerGrid, IBrush?>(nameof(HoverFill));

    public static readonly StyledProperty<IBrush?> FocusRingProperty =
        AvaloniaProperty.Register<EmojiPickerGrid, IBrush?>(nameof(FocusRing));

    public static readonly StyledProperty<CornerRadius> CellCornerRadiusProperty =
        AvaloniaProperty.Register<EmojiPickerGrid, CornerRadius>(nameof(CellCornerRadius));

    private readonly record struct Row(int Section, int Start, int Count, double Y, bool IsHeader);

    private readonly Dictionary<string, TextLayout> _glyphs = new(StringComparer.Ordinal);
    private readonly List<Row> _rows = new();
    private IReadOnlyList<EmojiPickerSection> _sections = Array.Empty<EmojiPickerSection>();
    private TextLayout?[] _headers = Array.Empty<TextLayout?>();
    private double[] _sectionTops = Array.Empty<double>();
    private EmojiSkinTone _tone;
    private double _maxVersion = double.MaxValue;
    private double _height;
    private int _columns = 1;
    private double _padX;
    private double _layoutWidth = -1;
    private Rect _viewport;
    private int _activeSection = -1;
    private int _activeIndex = -1;
    private int _hoverSection = -1;
    private int _hoverIndex = -1;
    private (int Section, int Index) _pressed = (-1, -1);
    private Pen? _focusPen;

    static EmojiPickerGrid()
    {
        FocusableProperty.OverrideDefaultValue<EmojiPickerGrid>(true);
        AffectsMeasure<EmojiPickerGrid>(CellSizeProperty, HeaderHeightProperty);
        AffectsRender<EmojiPickerGrid>(HeaderForegroundProperty, BackgroundProperty, HoverFillProperty, FocusRingProperty,
            CellCornerRadiusProperty);
    }

    public EmojiPickerGrid()
    {
        EffectiveViewportChanged += OnEffectiveViewportChanged;
    }

    public double CellSize { get => GetValue(CellSizeProperty); set => SetValue(CellSizeProperty, value); }

    public double EmojiFontSize { get => GetValue(EmojiFontSizeProperty); set => SetValue(EmojiFontSizeProperty, value); }
    public FontFamily EmojiFontFamily { get => GetValue(EmojiFontFamilyProperty); set => SetValue(EmojiFontFamilyProperty, value); }

    public double HeaderHeight { get => GetValue(HeaderHeightProperty); set => SetValue(HeaderHeightProperty, value); }

    public double HeaderFontSize { get => GetValue(HeaderFontSizeProperty); set => SetValue(HeaderFontSizeProperty, value); }

    public IBrush? HeaderForeground { get => GetValue(HeaderForegroundProperty); set => SetValue(HeaderForegroundProperty, value); }

    /// <summary>The fill behind the pinned section title.</summary>
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    public IBrush? HoverFill { get => GetValue(HoverFillProperty); set => SetValue(HoverFillProperty, value); }

    public IBrush? FocusRing { get => GetValue(FocusRingProperty); set => SetValue(FocusRingProperty, value); }

    public CornerRadius CellCornerRadius { get => GetValue(CellCornerRadiusProperty); set => SetValue(CellCornerRadiusProperty, value); }

    /// <summary>Raised when an emoji is picked (click, Enter or Space).</summary>
    public event Action<EmojiPickerEmoji>? Picked;

    /// <summary>Raised when the active emoji changes (pointer or keyboard); null when there is none.</summary>
    public event Action<EmojiPickerEmoji?>? ActiveChanged;

    public IReadOnlyList<EmojiPickerSection> Sections => _sections;

    public int Columns => _columns;

    /// <summary>The number of drawn rows the last render touched (for tests of the virtualization).</summary>
    internal int LastDrawnRows { get; private set; }

    /// <summary>The number of text layouts made so far (one per distinct emoji shown).</summary>
    internal int GlyphCacheCount => _glyphs.Count;

    public EmojiPickerEmoji? ActiveEmoji =>
        _activeSection >= 0 && _activeSection < _sections.Count && _activeIndex >= 0 && _activeIndex < _sections[_activeSection].Items.Count
            ? _sections[_activeSection].Items[_activeIndex]
            : null;

    public (int Section, int Index) Active => (_activeSection, _activeIndex);

    /// <summary>Replaces the sections; the active emoji becomes the first one (or stays, when <paramref name="keepActive"/> and it is still there).</summary>
    public void SetSections(IReadOnlyList<EmojiPickerSection> sections, bool keepActive = false)
    {
        var previous = keepActive ? ActiveEmoji : null;
        _sections = sections;
        foreach (var header in _headers)
        {
            header?.Dispose();
        }
        _headers = new TextLayout?[sections.Count];
        _layoutWidth = -1;
        _hoverSection = -1;
        _hoverIndex = -1;
        _activeSection = -1;
        _activeIndex = -1;
        if (previous is not null)
        {
            for (var s = 0; s < sections.Count && _activeSection < 0; s++)
            {
                for (var i = 0; i < sections[s].Items.Count; i++)
                {
                    if (ReferenceEquals(sections[s].Items[i], previous))
                    {
                        _activeSection = s;
                        _activeIndex = i;
                        break;
                    }
                }
            }
        }
        if (_activeSection < 0)
        {
            SetActiveCore(FirstItem(), bringIntoView: false);
        }
        else
        {
            RaiseActive();
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Applies a skin tone and an emoji version limit to what is drawn.</summary>
    public void SetDisplay(EmojiSkinTone tone, double maxVersion)
    {
        if (tone == _tone && Math.Abs(maxVersion - _maxVersion) < 1e-9)
        {
            return;
        }
        _tone = tone;
        _maxVersion = maxVersion;
        InvalidateVisual();
    }

    /// <summary>The text shown for <paramref name="emoji"/> with the current tone.</summary>
    public string Display(EmojiPickerEmoji emoji) => emoji.Display(_tone, _maxVersion);

    /// <summary>The top of a section's title, in the grid's coordinates (after a layout).</summary>
    public double SectionTop(int section) => section >= 0 && section < _sectionTops.Length ? _sectionTops[section] : 0;

    /// <summary>The section whose rows are under <paramref name="y"/>.</summary>
    public int SectionAt(double y)
    {
        var found = -1;
        for (var s = 0; s < _sectionTops.Length; s++)
        {
            if (_sections[s].Items.Count == 0)
            {
                continue;
            }
            if (_sectionTops[s] <= y + 0.5 || found < 0)
            {
                found = s;
            }
            else
            {
                break;
            }
        }
        return found;
    }

    /// <summary>Makes the first emoji of <paramref name="section"/> active.</summary>
    public void ActivateSection(int section)
    {
        if (section >= 0 && section < _sections.Count && _sections[section].Items.Count > 0)
        {
            SetActiveCore((section, 0), bringIntoView: false);
        }
    }

    /// <summary>Picks the active emoji.</summary>
    public bool PickActive()
    {
        if (ActiveEmoji is { } emoji)
        {
            Picked?.Invoke(emoji);
            return true;
        }
        return false;
    }

    #region Layout

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? CellSize * 9 : availableSize.Width;
        BuildRows(width);
        return new Size(width, _height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Math.Abs(finalSize.Width - _layoutWidth) > 0.5)
        {
            BuildRows(finalSize.Width);
        }
        return new Size(finalSize.Width, _height);
    }

    private void BuildRows(double width)
    {
        if (Math.Abs(width - _layoutWidth) < 0.5 && _rows.Count > 0)
        {
            return;
        }
        _layoutWidth = width;
        var cell = Math.Max(8, CellSize);
        _columns = Math.Max(1, (int)Math.Floor(width / cell));
        _padX = Math.Max(0, (width - _columns * cell) / 2);
        _rows.Clear();
        _sectionTops = new double[_sections.Count];
        var y = 0.0;
        for (var s = 0; s < _sections.Count; s++)
        {
            var items = _sections[s].Items.Count;
            _sectionTops[s] = y;
            if (items == 0)
            {
                continue;
            }
            _rows.Add(new Row(s, 0, 0, y, true));
            y += HeaderHeight;
            for (var start = 0; start < items; start += _columns)
            {
                _rows.Add(new Row(s, start, Math.Min(_columns, items - start), y, false));
                y += cell;
            }
        }
        _height = y + 4;
    }

    private int FirstRowAt(double y)
    {
        int lo = 0, hi = _rows.Count - 1, found = 0;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (_rows[mid].Y <= y)
            {
                found = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return found;
    }

    private double RowHeight(Row row) => row.IsHeader ? HeaderHeight : CellSize;

    private Rect CellRect(int section, int index)
    {
        foreach (var row in RowsOf(section))
        {
            if (!row.IsHeader && index >= row.Start && index < row.Start + row.Count)
            {
                var col = index - row.Start;
                return new Rect(_padX + col * CellSize, row.Y, CellSize, CellSize);
            }
        }
        return default;
    }

    private IEnumerable<Row> RowsOf(int section)
    {
        if (section < 0 || section >= _sectionTops.Length)
        {
            yield break;
        }
        for (var i = FirstRowAt(_sectionTops[section]); i < _rows.Count; i++)
        {
            var row = _rows[i];
            if (row.Section < section)
            {
                continue;
            }
            if (row.Section > section)
            {
                yield break;
            }
            yield return row;
        }
    }

    /// <summary>The emoji at a point of the grid, or (-1, -1).</summary>
    internal (int Section, int Index) HitTest(Point point)
    {
        if (_rows.Count == 0)
        {
            return (-1, -1);
        }
        var row = _rows[FirstRowAt(point.Y)];
        if (row.IsHeader || point.Y >= row.Y + CellSize || point.X < _padX)
        {
            return (-1, -1);
        }
        var col = (int)Math.Floor((point.X - _padX) / CellSize);
        if (col < 0 || col >= row.Count)
        {
            return (-1, -1);
        }
        return (row.Section, row.Start + col);
    }

    private void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        if (e.EffectiveViewport != _viewport)
        {
            _viewport = e.EffectiveViewport;
            InvalidateVisual();
        }
    }

    #endregion

    #region Drawing

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Brushes.Transparent, bounds);
        if (_rows.Count == 0)
        {
            LastDrawnRows = 0;
            return;
        }
        var view = _viewport.Width > 0 && _viewport.Height > 0 ? _viewport.Intersect(bounds) : bounds;
        var top = view.Y - CellSize;
        var bottom = view.Bottom + CellSize;
        var cell = CellSize;
        var radius = CellCornerRadius;
        var focusVisible = IsFocused && Classes.Contains(":focus-visible");
        var drawn = 0;
        for (var r = FirstRowAt(Math.Max(0, top)); r < _rows.Count; r++)
        {
            var row = _rows[r];
            if (row.Y > bottom)
            {
                break;
            }
            drawn++;
            if (row.IsHeader)
            {
                DrawHeader(context, row.Section, row.Y);
                continue;
            }
            var items = _sections[row.Section].Items;
            for (var i = 0; i < row.Count; i++)
            {
                var index = row.Start + i;
                var rect = new Rect(_padX + i * cell, row.Y, cell, cell);
                var isActive = row.Section == _activeSection && index == _activeIndex;
                var isHover = row.Section == _hoverSection && index == _hoverIndex;
                if ((isHover || (isActive && focusVisible)) && HoverFill is { } fill)
                {
                    context.DrawRectangle(fill, null, new RoundedRect(rect.Deflate(1), radius));
                }
                if (isActive && focusVisible && FocusRing is { } ring)
                {
                    _focusPen ??= new Pen(ring, 2);
                    context.DrawRectangle(null, _focusPen, new RoundedRect(rect.Deflate(1), radius));
                }
                var layout = Glyph(Display(items[index]));
                layout.Draw(context, new Point(rect.X + (cell - layout.WidthIncludingTrailingWhitespace) / 2, rect.Y + (cell - layout.Height) / 2));
            }
        }
        LastDrawnRows = drawn;
        DrawPinnedHeader(context, view);
    }

    private void DrawHeader(DrawingContext context, int section, double y)
    {
        var layout = Header(section);
        layout.Draw(context, new Point(_padX + 6, y + (HeaderHeight - layout.Height) / 2));
    }

    /// <summary>The title of the section under the top edge stays there until the next title pushes it up.</summary>
    private void DrawPinnedHeader(DrawingContext context, Rect view)
    {
        if (view.Y <= 0.5)
        {
            return;
        }
        var section = SectionAt(view.Y);
        if (section < 0)
        {
            return;
        }
        var y = view.Y;
        for (var next = section + 1; next < _sections.Count; next++)
        {
            if (_sections[next].Items.Count > 0)
            {
                y = Math.Min(y, _sectionTops[next] - HeaderHeight);
                break;
            }
        }
        if (Background is { } background)
        {
            context.FillRectangle(background, new Rect(0, y, Bounds.Width, HeaderHeight));
        }
        DrawHeader(context, section, y);
    }

    private TextLayout Glyph(string text)
    {
        if (_glyphs.TryGetValue(text, out var layout))
        {
            return layout;
        }
        layout = new TextLayout(text, new Typeface(EmojiFontFamily), EmojiFontSize, HeaderForeground ?? Brushes.Black,
            TextAlignment.Left, TextWrapping.NoWrap);
        _glyphs[text] = layout;
        return layout;
    }

    private TextLayout Header(int section)
    {
        if (_headers[section] is { } layout)
        {
            return layout;
        }
        layout = new TextLayout(_sections[section].Title, new Typeface(TextElement.GetFontFamily(this), FontStyle.Normal, FontWeight.Medium),
            HeaderFontSize, HeaderForeground ?? Brushes.Gray, TextAlignment.Left, TextWrapping.NoWrap);
        _headers[section] = layout;
        return layout;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EmojiFontSizeProperty || change.Property == EmojiFontFamilyProperty || change.Property == TextElement.FontFamilyProperty)
        {
            ClearGlyphs();
            InvalidateVisual();
        }
        else if (change.Property == HeaderForegroundProperty || change.Property == HeaderFontSizeProperty)
        {
            for (var i = 0; i < _headers.Length; i++)
            {
                _headers[i]?.Dispose();
                _headers[i] = null;
            }
        }
        else if (change.Property == FocusRingProperty)
        {
            _focusPen = null;
        }
    }

    private void ClearGlyphs()
    {
        foreach (var layout in _glyphs.Values)
        {
            layout.Dispose();
        }
        _glyphs.Clear();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // The layouts hold shaped glyph runs; a closed picker gives them back and rebuilds the visible ones next time.
        ClearGlyphs();
    }

    #endregion

    #region Active emoji and keyboard

    private (int Section, int Index) FirstItem()
    {
        for (var s = 0; s < _sections.Count; s++)
        {
            if (_sections[s].Items.Count > 0)
            {
                return (s, 0);
            }
        }
        return (-1, -1);
    }

    private (int Section, int Index) LastItem()
    {
        for (var s = _sections.Count - 1; s >= 0; s--)
        {
            if (_sections[s].Items.Count > 0)
            {
                return (s, _sections[s].Items.Count - 1);
            }
        }
        return (-1, -1);
    }

    private void SetActiveCore((int Section, int Index) target, bool bringIntoView)
    {
        if (target == (_activeSection, _activeIndex))
        {
            if (bringIntoView)
            {
                BringActiveIntoView();
            }
            return;
        }
        (_activeSection, _activeIndex) = target;
        RaiseActive();
        if (bringIntoView)
        {
            BringActiveIntoView();
        }
        InvalidateVisual();
    }

    private void RaiseActive()
    {
        var emoji = ActiveEmoji;
        AutomationProperties.SetName(this, emoji?.Name ?? string.Empty);
        ActiveChanged?.Invoke(emoji);
    }

    private void BringActiveIntoView()
    {
        if (_activeSection < 0 || _rows.Count == 0)
        {
            return;
        }
        var rect = CellRect(_activeSection, _activeIndex);
        // Scrolls only the picker's own viewer (BringIntoView would also scroll the page around it), leaving room for
        // the pinned title above the row.
        if (this.FindAncestorOfType<ScrollViewer>() is not { } scroll)
        {
            return;
        }
        var top = Math.Max(0, rect.Y - HeaderHeight);
        var offset = scroll.Offset.Y;
        if (top < offset)
        {
            scroll.Offset = new Vector(scroll.Offset.X, top);
        }
        else if (rect.Bottom > offset + scroll.Viewport.Height)
        {
            scroll.Offset = new Vector(scroll.Offset.X, rect.Bottom - scroll.Viewport.Height);
        }
    }

    /// <summary>Moves the active emoji by a key; true when the key was used.</summary>
    internal bool Navigate(Key key, KeyModifiers modifiers)
    {
        if (_activeSection < 0)
        {
            var first = FirstItem();
            if (first.Section < 0)
            {
                return false;
            }
            SetActiveCore(first, bringIntoView: true);
            return true;
        }
        var ctrl = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);
        var target = key switch
        {
            Key.Left => Step(-1),
            Key.Right => Step(1),
            Key.Up => Vertical(-1),
            Key.Down => Vertical(1),
            Key.Home when ctrl => FirstItem(),
            Key.End when ctrl => LastItem(),
            Key.Home => RowEdge(start: true),
            Key.End => RowEdge(start: false),
            Key.PageUp => Vertical(-PageRows()),
            Key.PageDown => Vertical(PageRows()),
            _ => (-2, -2)
        };
        if (target.Item1 == -2)
        {
            return false;
        }
        if (target.Item1 >= 0)
        {
            SetActiveCore(target, bringIntoView: true);
        }
        return true;
    }

    private int PageRows() => Math.Max(1, (int)Math.Floor((_viewport.Height > 0 ? _viewport.Height : CellSize * 6) / CellSize) - 1);

    private (int, int) Step(int delta)
    {
        var s = _activeSection;
        var i = _activeIndex + delta;
        while (s >= 0 && s < _sections.Count)
        {
            if (i >= 0 && i < _sections[s].Items.Count)
            {
                return (s, i);
            }
            s += delta;
            if (s < 0 || s >= _sections.Count)
            {
                break;
            }
            i = delta > 0 ? 0 : _sections[s].Items.Count - 1;
        }
        return (_activeSection, _activeIndex);
    }

    /// <summary>Moves by whole rows of emoji, across section titles, keeping the column where it can.</summary>
    private (int, int) Vertical(int rows)
    {
        var current = -1;
        for (var r = 0; r < _rows.Count; r++)
        {
            var row = _rows[r];
            if (!row.IsHeader && row.Section == _activeSection && _activeIndex >= row.Start && _activeIndex < row.Start + row.Count)
            {
                current = r;
                break;
            }
        }
        if (current < 0)
        {
            return (_activeSection, _activeIndex);
        }
        var column = _activeIndex - _rows[current].Start;
        var direction = Math.Sign(rows);
        var remaining = Math.Abs(rows);
        var target = current;
        for (var r = current + direction; r >= 0 && r < _rows.Count && remaining > 0; r += direction)
        {
            if (_rows[r].IsHeader)
            {
                continue;
            }
            target = r;
            remaining--;
        }
        if (target == current)
        {
            return (_activeSection, _activeIndex);
        }
        var found = _rows[target];
        return (found.Section, found.Start + Math.Min(column, found.Count - 1));
    }

    private (int, int) RowEdge(bool start)
    {
        foreach (var row in RowsOf(_activeSection))
        {
            if (!row.IsHeader && _activeIndex >= row.Start && _activeIndex < row.Start + row.Count)
            {
                return (row.Section, start ? row.Start : row.Start + row.Count - 1);
            }
        }
        return (_activeSection, _activeIndex);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = PickActive();
            return;
        }
        if (Navigate(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (_activeSection < 0)
        {
            SetActiveCore(FirstItem(), bringIntoView: false);
        }
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    #endregion

    #region Pointer

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var hit = HitTest(e.GetPosition(this));
        if (hit != (_hoverSection, _hoverIndex))
        {
            (_hoverSection, _hoverIndex) = hit;
            if (hit.Section >= 0)
            {
                SetActiveCore(hit, bringIntoView: false);
            }
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoverSection >= 0)
        {
            _hoverSection = -1;
            _hoverIndex = -1;
            InvalidateVisual();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressed = HitTest(e.GetPosition(this));
        if (_pressed.Section >= 0)
        {
            SetActiveCore(_pressed, bringIntoView: false);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var pressed = _pressed;
        _pressed = (-1, -1);
        if (pressed.Section >= 0 && HitTest(e.GetPosition(this)) == pressed)
        {
            SetActiveCore(pressed, bringIntoView: false);
            e.Handled = PickActive();
        }
    }

    #endregion
}
