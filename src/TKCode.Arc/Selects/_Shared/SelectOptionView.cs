using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>How the selected mark of an option appears.</summary>
internal enum SelectCheckMode
{
    /// <summary>No check at all.</summary>
    None,

    /// <summary>Shown while selected, no motion (Combobox).</summary>
    Static,

    /// <summary>Grows from 0.6 with a fade (Select indicator on open, MorphSelect on pick).</summary>
    ScaleIn,

    /// <summary>The stroke draws itself and retracts (MultiSelect, P15).</summary>
    Draw
}

/// <summary>Callbacks from a <see cref="SelectOptionView"/> to the control that owns it.</summary>
internal interface ISelectOptionHost
{
    SelectEntrySource Entries { get; }

    bool IsEntrySelected(int entryIndex);

    /// <summary>The pointer moved onto the enabled option at <paramref name="position"/> (mouse only).</summary>
    void OnOptionHovered(int position);

    /// <summary>The option at <paramref name="position"/> was clicked or tapped.</summary>
    void OnOptionInvoked(int position);

    void OnOptionsPointerExited();
}

/// <summary>
/// The option list of every popup select in this category. Rows are drawn in <see cref="Render"/> for the
/// visible part of the scroll viewport only, so a list of thousands of options costs a few text layouts and no
/// containers; filtering swaps the row index list. A Core <see cref="GlidingIndicator"/> under the rows draws the
/// highlight (P7). Optional group headings are inserted where the group of consecutive options changes.
/// </summary>
internal sealed class SelectOptionView : Control
{
    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<SelectOptionView, double>(nameof(RowHeight), 36);

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<SelectOptionView, double>(nameof(RowSpacing));

    public static readonly StyledProperty<CornerRadius> RowCornerRadiusProperty =
        AvaloniaProperty.Register<SelectOptionView, CornerRadius>(nameof(RowCornerRadius), new CornerRadius(14));

    public static readonly StyledProperty<Thickness> RowPaddingProperty =
        AvaloniaProperty.Register<SelectOptionView, Thickness>(nameof(RowPadding), new Thickness(12, 0));

    public static readonly StyledProperty<double> ContentSpacingProperty =
        AvaloniaProperty.Register<SelectOptionView, double>(nameof(ContentSpacing), 12);

    /// <summary>Distance from the right edge to the check (absolute check like Radix' indicator); NaN = flex.</summary>
    public static readonly StyledProperty<double> CheckInsetProperty =
        AvaloniaProperty.Register<SelectOptionView, double>(nameof(CheckInset), double.NaN);

    public static readonly StyledProperty<SelectCheckMode> CheckModeProperty =
        AvaloniaProperty.Register<SelectOptionView, SelectCheckMode>(nameof(CheckMode), SelectCheckMode.Static);

    /// <summary>Keeps room for the check on every row (MorphSelect) instead of only on selected rows.</summary>
    public static readonly StyledProperty<bool> ReserveCheckProperty =
        AvaloniaProperty.Register<SelectOptionView, bool>(nameof(ReserveCheck));

    public static readonly StyledProperty<bool> GlideHighlightProperty =
        AvaloniaProperty.Register<SelectOptionView, bool>(nameof(GlideHighlight));

    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<SelectOptionView, IBrush?>(nameof(HighlightBrush));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<SelectOptionView>();

    /// <summary>Label color of the active or selected row; null keeps <see cref="Foreground"/>.</summary>
    public static readonly StyledProperty<IBrush?> EmphasisForegroundProperty =
        AvaloniaProperty.Register<SelectOptionView, IBrush?>(nameof(EmphasisForeground));

    public static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<SelectOptionView, IBrush?>(nameof(MutedForeground));

    public static readonly StyledProperty<IBrush?> CheckForegroundProperty =
        AvaloniaProperty.Register<SelectOptionView, IBrush?>(nameof(CheckForeground));

    public static readonly StyledProperty<double> DisabledOpacityProperty =
        AvaloniaProperty.Register<SelectOptionView, double>(nameof(DisabledOpacity), 0.45);

    public static readonly StyledProperty<FontWeight> LabelFontWeightProperty =
        AvaloniaProperty.Register<SelectOptionView, FontWeight>(nameof(LabelFontWeight), FontWeight.Normal);

    public static readonly StyledProperty<bool> ShowGroupsProperty =
        AvaloniaProperty.Register<SelectOptionView, bool>(nameof(ShowGroups));

    public static readonly StyledProperty<double> GroupHeaderHeightProperty =
        AvaloniaProperty.Register<SelectOptionView, double>(nameof(GroupHeaderHeight), 28);

    public static readonly StyledProperty<double> GroupSpacingProperty =
        AvaloniaProperty.Register<SelectOptionView, double>(nameof(GroupSpacing), 4);

    public static readonly StyledProperty<double> MetaFontSizeProperty =
        AvaloniaProperty.Register<SelectOptionView, double>(nameof(MetaFontSize), 12);

    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }
    public double RowSpacing { get => GetValue(RowSpacingProperty); set => SetValue(RowSpacingProperty, value); }
    public CornerRadius RowCornerRadius { get => GetValue(RowCornerRadiusProperty); set => SetValue(RowCornerRadiusProperty, value); }
    public Thickness RowPadding { get => GetValue(RowPaddingProperty); set => SetValue(RowPaddingProperty, value); }
    public double ContentSpacing { get => GetValue(ContentSpacingProperty); set => SetValue(ContentSpacingProperty, value); }
    public double CheckInset { get => GetValue(CheckInsetProperty); set => SetValue(CheckInsetProperty, value); }
    public SelectCheckMode CheckMode { get => GetValue(CheckModeProperty); set => SetValue(CheckModeProperty, value); }
    public bool ReserveCheck { get => GetValue(ReserveCheckProperty); set => SetValue(ReserveCheckProperty, value); }
    public bool GlideHighlight { get => GetValue(GlideHighlightProperty); set => SetValue(GlideHighlightProperty, value); }
    public IBrush? HighlightBrush { get => GetValue(HighlightBrushProperty); set => SetValue(HighlightBrushProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public IBrush? EmphasisForeground { get => GetValue(EmphasisForegroundProperty); set => SetValue(EmphasisForegroundProperty, value); }
    public IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }
    public IBrush? CheckForeground { get => GetValue(CheckForegroundProperty); set => SetValue(CheckForegroundProperty, value); }
    public double DisabledOpacity { get => GetValue(DisabledOpacityProperty); set => SetValue(DisabledOpacityProperty, value); }
    public FontWeight LabelFontWeight { get => GetValue(LabelFontWeightProperty); set => SetValue(LabelFontWeightProperty, value); }
    public bool ShowGroups { get => GetValue(ShowGroupsProperty); set => SetValue(ShowGroupsProperty, value); }
    public double GroupHeaderHeight { get => GetValue(GroupHeaderHeightProperty); set => SetValue(GroupHeaderHeightProperty, value); }
    public double GroupSpacing { get => GetValue(GroupSpacingProperty); set => SetValue(GroupSpacingProperty, value); }
    public double MetaFontSize { get => GetValue(MetaFontSizeProperty); set => SetValue(MetaFontSizeProperty, value); }

    /// <summary>Lucide check (Radix item indicator, Combobox and MorphSelect).</summary>
    internal static readonly Geometry CheckGlyph = ArcIcons.Check;

    /// <summary>MultiSelect's own check path, drawn with pathLength.</summary>
    internal static readonly Geometry DrawnCheckGlyph = ArcIcons.Parse("M4 12.5 9 17.5 20 6.5");

    private readonly GlidingIndicator _highlight = new();
    private readonly RowsLayer _rows;
    private readonly List<Row> _layout = new();
    private readonly List<int> _optionRows = new();
    private readonly Dictionary<long, TextLayout> _texts = new();
    private readonly Dictionary<int, MotionValue<double>> _checks = new();
    private IReadOnlyList<int> _positions = Array.Empty<int>();
    private ISelectOptionHost? _host;
    private Rect? _viewport;
    private double _extent;
    private double _textWidth = -1;
    private int _active = -1;
    private int _pressed = -1;
    private Pen? _checkPen;
    private Pen? _iconPen;

    private struct Row
    {
        public int Position;
        public string? Header;
        public double Top;
        public double Height;
    }

    static SelectOptionView()
    {
        AffectsMeasure<SelectOptionView>(RowHeightProperty, RowSpacingProperty, ShowGroupsProperty, GroupHeaderHeightProperty,
            GroupSpacingProperty);
        FocusableProperty.OverrideDefaultValue<SelectOptionView>(false);
    }

    public SelectOptionView()
    {
        _rows = new RowsLayer(this);
        VisualChildren.Add(_highlight);
        VisualChildren.Add(_rows);
        LogicalChildren.Add(_highlight);
        EffectiveViewportChanged += (_, e) =>
        {
            _viewport = e.EffectiveViewport;
            _rows.InvalidateVisual();
        };
        Background = Brushes.Transparent;
    }

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<SelectOptionView, IBrush?>(nameof(Background));

    /// <summary>Hit-test surface (transparent by default so gaps between rows still track the pointer).</summary>
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    public ISelectOptionHost? Host
    {
        get => _host;
        set => _host = value;
    }

    /// <summary>Number of option rows (filtered entries).</summary>
    public int OptionCount => _positions.Count;

    /// <summary>Height of all rows (headers included), available right after <see cref="SetOptions"/>.</summary>
    public double Extent => _extent;

    /// <summary>The highlighted option position, or -1.</summary>
    public int ActiveOption => _active;

    internal GlidingIndicator Highlight => _highlight;

    /// <summary>Entry index (into <see cref="ISelectOptionHost.Entries"/>) shown at <paramref name="position"/>.</summary>
    public int EntryAt(int position) => position >= 0 && position < _positions.Count ? _positions[position] : -1;

    /// <summary>Replaces the rows with <paramref name="entryIndices"/> (in display order).</summary>
    public void SetOptions(IReadOnlyList<int> entryIndices)
    {
        _positions = entryIndices;
        _active = -1;
        _highlight.Hide(animate: false);
        _texts.Clear();
        RebuildLayout();
        InvalidateMeasure();
        _rows.InvalidateVisual();
    }

    /// <summary>Re-reads entry data (labels or groups changed) without changing the row list.</summary>
    public void Refresh()
    {
        _texts.Clear();
        RebuildLayout();
        InvalidateMeasure();
        _rows.InvalidateVisual();
    }

    /// <summary>Selection changed somewhere: repaint checks and emphasis.</summary>
    public void InvalidateRows() => _rows.InvalidateVisual();

    /// <summary>Moves the highlight to <paramref name="position"/> (-1 hides it).</summary>
    public void SetActive(int position, bool animate)
    {
        _active = position >= 0 && position < _positions.Count ? position : -1;
        if (_active < 0)
        {
            _highlight.Hide(animate);
        }
        else
        {
            _highlight.MoveTo(OptionRect(_active), animate && GlideHighlight, ArcSpring.Snappy);
        }
        _rows.InvalidateVisual();
    }

    /// <summary>Rectangle of the option at <paramref name="position"/> in this control's coordinates.</summary>
    public Rect OptionRect(int position)
    {
        if (position < 0 || position >= _optionRows.Count)
        {
            return default;
        }
        var row = _layout[_optionRows[position]];
        var width = Bounds.Width > 0 ? Bounds.Width : _textWidth;
        return new Rect(0, row.Top, Math.Max(0, width), row.Height);
    }

    /// <summary>Rectangle of the label (icon included) of the option at <paramref name="position"/>.</summary>
    public Rect OptionValueRect(int position)
    {
        var rect = OptionRect(position);
        return new Rect(rect.X + RowPadding.Left, rect.Y, Math.Max(0, rect.Width - RowPadding.Left - RowPadding.Right), rect.Height);
    }

    /// <summary>Scrolls the nearest scroll viewer so the option at <paramref name="position"/> is visible.</summary>
    public void ScrollIntoView(int position, double padding = 0)
    {
        if (position < 0 || position >= _positions.Count)
        {
            return;
        }
        var rect = OptionRect(position);
        this.BringIntoView(new Rect(rect.X, rect.Y - padding, rect.Width, rect.Height + 2 * padding));
    }

    /// <summary>Animates the check of <paramref name="entryIndex"/> toward <paramref name="on"/>.</summary>
    public void PlayCheck(int entryIndex, bool on, bool fromZero = false, TimeSpan delay = default)
    {
        if (CheckMode is SelectCheckMode.None or SelectCheckMode.Static || entryIndex < 0)
        {
            _rows.InvalidateVisual();
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        // Reduced motion: the drawn check fades instantly; the scaled indicator simply appears (Arc drops it).
        if (!IsLoaded || level == ArcMotionLevel.Off || (level == ArcMotionLevel.Reduced && CheckMode == SelectCheckMode.ScaleIn))
        {
            if (_checks.Remove(entryIndex, out var stale))
            {
                stale.Dispose();
            }
            _rows.InvalidateVisual();
            return;
        }
        if (!_checks.TryGetValue(entryIndex, out var value))
        {
            var start = fromZero ? 0 : (on ? 0 : 1);
            value = new MotionValue<double>(this, start, MotionVectors.Double);
            value.Changed += _ => _rows.InvalidateVisual();
            _checks[entryIndex] = value;
        }
        else if (fromZero)
        {
            value.Jump(0);
        }
        var spec = level == ArcMotionLevel.Reduced
            ? MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, delay);
        _ = Finish(entryIndex, value, value.AnimateTo(on ? 1 : 0, spec));
    }

    private async Task Finish(int entryIndex, MotionValue<double> value, Task<bool> run)
    {
        if (await run && _checks.TryGetValue(entryIndex, out var current) && ReferenceEquals(current, value))
        {
            _checks.Remove(entryIndex);
            value.Dispose();
            _rows.InvalidateVisual();
        }
    }

    private void RebuildLayout()
    {
        _layout.Clear();
        _optionRows.Clear();
        var entries = _host?.Entries;
        var rowHeight = RowHeight;
        var spacing = RowSpacing;
        var groups = ShowGroups;
        double y = 0;
        string? group = null;
        var first = true;
        for (var position = 0; position < _positions.Count; position++)
        {
            var entry = entries is not null && _positions[position] < entries.Count ? entries[_positions[position]] : null;
            if (groups)
            {
                var next = entry?.Group;
                if (first || !string.Equals(next, group, StringComparison.Ordinal))
                {
                    if (!first)
                    {
                        y += GroupSpacing;
                    }
                    group = next;
                    if (!string.IsNullOrEmpty(next))
                    {
                        _layout.Add(new Row { Position = -1, Header = next, Top = y, Height = GroupHeaderHeight });
                        y += GroupHeaderHeight + spacing;
                    }
                }
            }
            first = false;
            _optionRows.Add(_layout.Count);
            _layout.Add(new Row { Position = position, Top = y, Height = rowHeight });
            y += rowHeight + spacing;
        }
        _extent = _positions.Count > 0 ? Math.Max(0, y - spacing) : 0;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RowHeightProperty || change.Property == RowSpacingProperty || change.Property == ShowGroupsProperty ||
            change.Property == GroupHeaderHeightProperty || change.Property == GroupSpacingProperty)
        {
            RebuildLayout();
        }
        else if (change.Property == ForegroundProperty || change.Property == EmphasisForegroundProperty ||
                 change.Property == MutedForegroundProperty || change.Property == LabelFontWeightProperty ||
                 change.Property == TextElement.FontFamilyProperty || change.Property == TextElement.FontSizeProperty ||
                 change.Property == MetaFontSizeProperty)
        {
            _texts.Clear();
            _iconPen = null;
            _rows.InvalidateVisual();
        }
        else if (change.Property == CheckForegroundProperty)
        {
            _checkPen = null;
            _iconPen = null;
            _rows.InvalidateVisual();
        }
        else if (change.Property == HighlightBrushProperty)
        {
            _highlight.Fill = HighlightBrush;
        }
        else if (change.Property == RowCornerRadiusProperty)
        {
            _highlight.CornerRadius = RowCornerRadius;
        }
        else if (change.Property == RowPaddingProperty || change.Property == ContentSpacingProperty || change.Property == CheckInsetProperty ||
                 change.Property == ReserveCheckProperty || change.Property == CheckModeProperty)
        {
            _texts.Clear();
            _rows.InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_layout.Count == 0 && _positions.Count > 0)
        {
            RebuildLayout();
        }
        _highlight.Measure(availableSize);
        _rows.Measure(availableSize);
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        return new Size(width, _extent);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rect = new Rect(finalSize);
        _highlight.Arrange(rect);
        _rows.Arrange(rect);
        if (Math.Abs(finalSize.Width - _textWidth) > 0.5)
        {
            _textWidth = finalSize.Width;
            _texts.Clear();
            if (_active >= 0)
            {
                _highlight.MoveTo(OptionRect(_active), animate: false);
            }
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (Background is { } background)
        {
            context.FillRectangle(background, new Rect(Bounds.Size));
        }
    }

    // ---------------------------------------------------------------- input

    private int PositionAt(Point point)
    {
        var index = RowIndexAt(point.Y);
        if (index < 0)
        {
            return -1;
        }
        var row = _layout[index];
        return row.Position >= 0 && point.Y >= row.Top && point.Y < row.Top + row.Height ? row.Position : -1;
    }

    private bool IsEnabledPosition(int position)
    {
        var entry = EntryAt(position);
        return entry >= 0 && _host?.Entries.IsEnabledAt(entry) == true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type != PointerType.Mouse || _host is null)
        {
            return;
        }
        var position = PositionAt(e.GetPosition(this));
        if (position >= 0 && position != _active && IsEnabledPosition(position))
        {
            _host.OnOptionHovered(position);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _host?.OnOptionsPointerExited();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _pressed = PositionAt(e.GetPosition(this));
        // Keeps keyboard focus where it is (the trigger or search field), like Arc's preventDefault on mousedown.
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var position = PositionAt(e.GetPosition(this));
        var pressed = _pressed;
        _pressed = -1;
        if (position >= 0 && position == pressed && IsEnabledPosition(position) && _host is not null)
        {
            e.Handled = true;
            _host.OnOptionInvoked(position);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = -1;
    }

    private int RowIndexAt(double y)
    {
        if (_layout.Count == 0)
        {
            return -1;
        }
        int lo = 0, hi = _layout.Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (_layout[mid].Top <= y)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return lo;
    }

    // ---------------------------------------------------------------- rendering

    private double CheckProgress(int entryIndex, bool selected)
    {
        if (_checks.TryGetValue(entryIndex, out var value))
        {
            return value.Value;
        }
        return selected ? 1 : 0;
    }

    private Typeface Typeface(FontWeight weight) =>
        new(TextElement.GetFontFamily(this), FontStyle.Normal, weight);

    private TextLayout GetText(int key, int variant, string text, double maxWidth, IBrush? brush, FontWeight weight, double size,
                               bool tabular = false)
    {
        var cacheKey = ((long)key << 4) | (long)variant;
        if (_texts.TryGetValue(cacheKey, out var layout) && Math.Abs(layout.MaxWidth - Math.Max(0, maxWidth)) < 0.5)
        {
            return layout;
        }
        if (_texts.Count > 512)
        {
            _texts.Clear();
        }
        layout = new TextLayout(text, Typeface(weight), size, brush, TextAlignment.Left, TextWrapping.NoWrap,
            TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, Math.Max(0, maxWidth), double.PositiveInfinity,
            double.NaN, 0, 1, tabular ? s_tabular : null);
        _texts[cacheKey] = layout;
        return layout;
    }

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    private void RenderRows(DrawingContext context)
    {
        var host = _host;
        if (host is null || _layout.Count == 0)
        {
            return;
        }
        var entries = host.Entries;
        var width = Bounds.Width;
        var top = 0.0;
        var bottom = Bounds.Height;
        if (_viewport is { } viewport && viewport.Width > 0)
        {
            top = viewport.Top;
            bottom = viewport.Bottom;
        }
        var fontSize = TextElement.GetFontSize(this);
        var foreground = Foreground;
        var emphasis = EmphasisForeground ?? foreground;
        var muted = MutedForeground ?? foreground;
        var padding = RowPadding;
        var gap = ContentSpacing;
        var checkSize = 16.0;
        var mode = CheckMode;
        var inset = CheckInset;
        var reserve = ReserveCheck;
        for (var index = Math.Max(0, RowIndexAt(top)); index < _layout.Count; index++)
        {
            var row = _layout[index];
            if (row.Top > bottom)
            {
                break;
            }
            if (row.Top + row.Height < top)
            {
                continue;
            }
            if (row.Header is { } header)
            {
                var headerText = GetText(-index - 1, 0, header, width - 20, muted, FontWeight.Normal, MetaFontSize);
                headerText.Draw(context, new Point(10, row.Top + 8));
                continue;
            }
            var entryIndex = _positions[row.Position];
            if (entryIndex < 0 || entryIndex >= entries.Count)
            {
                continue;
            }
            var entry = entries[entryIndex];
            var selected = host.IsEntrySelected(entryIndex);
            var active = row.Position == _active;
            var cy = row.Top + row.Height / 2;
            var x = padding.Left;
            var right = width - padding.Right;
            using var _ = entry.IsDisabled ? context.PushOpacity(DisabledOpacity) : default(DrawingContext.PushedState?);

            var labelBrush = entry.IsDisabled ? muted : (active || selected) ? emphasis : foreground;
            if (entry.Icon is { } icon)
            {
                DrawIcon(context, icon, new Rect(x, cy - 10, 20, 20), entry.IsDisabled ? muted : muted);
                x += 20 + 8;
            }

            var progress = mode == SelectCheckMode.None ? 0 : CheckProgress(entryIndex, selected);
            Rect checkRect;
            if (!double.IsNaN(inset))
            {
                checkRect = new Rect(width - inset - checkSize, cy - checkSize / 2, checkSize, checkSize);
            }
            else
            {
                checkRect = new Rect(right - checkSize, cy - checkSize / 2, checkSize, checkSize);
                if (mode != SelectCheckMode.None && (reserve || progress > 0 || selected))
                {
                    right -= checkSize + gap;
                }
            }
            if (progress > 0.001)
            {
                DrawCheck(context, checkRect, progress, mode);
            }

            if (!string.IsNullOrEmpty(entry.Meta))
            {
                var meta = GetText(entryIndex, 2, entry.Meta, Math.Max(0, right - x) / 2, muted, FontWeight.Normal, MetaFontSize, tabular: true);
                meta.Draw(context, new Point(right - meta.WidthIncludingTrailingWhitespace, cy - meta.Height / 2));
                right -= meta.WidthIncludingTrailingWhitespace + gap;
            }

            var variant = ReferenceEquals(labelBrush, foreground) ? 0 : ReferenceEquals(labelBrush, emphasis) ? 1 : 3;
            var label = GetText(entryIndex, variant, entry.Text, right - x, labelBrush, LabelFontWeight, fontSize);
            label.Draw(context, new Point(x, cy - label.Height / 2));
        }
    }

    private void DrawCheck(DrawingContext context, Rect rect, double progress, SelectCheckMode mode)
    {
        var brush = CheckForeground ?? Foreground;
        if (brush is null)
        {
            return;
        }
        var scale = rect.Width / 24.0;
        _checkPen ??= new Pen(brush, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var clamped = Math.Clamp(progress, 0, 1);
        switch (mode)
        {
            case SelectCheckMode.Static:
                using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(rect.X, rect.Y)))
                {
                    context.DrawGeometry(null, _checkPen, CheckGlyph);
                }
                break;
            case SelectCheckMode.ScaleIn:
            {
                var s = scale * (0.6 + 0.4 * progress);
                var cx = rect.Center.X;
                var cy = rect.Center.Y;
                using (context.PushOpacity(clamped))
                using (context.PushTransform(Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(s, s) * Matrix.CreateTranslation(cx, cy)))
                {
                    context.DrawGeometry(null, _checkPen, CheckGlyph);
                }
                break;
            }
            case SelectCheckMode.Draw:
                using (context.PushOpacity(Math.Min(1, clamped * 3)))
                using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(rect.X, rect.Y)))
                {
                    if (clamped >= 0.999)
                    {
                        context.DrawGeometry(null, _checkPen, DrawnCheckGlyph);
                    }
                    else
                    {
                        var length = DrawnCheckGlyph.ContourLength;
                        if (length > 0 && DrawnCheckGlyph.TryGetSegment(0, length * clamped, true, out var segment) && segment is not null)
                        {
                            context.DrawGeometry(null, _checkPen, segment);
                        }
                    }
                }
                break;
        }
    }

    private void DrawIcon(DrawingContext context, object icon, Rect box, IBrush? brush)
    {
        switch (icon)
        {
            case Geometry glyph when brush is not null:
            {
                _iconPen ??= new Pen(brush, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                var scale = 16 / 24.0;
                using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(box.X + 2, box.Y + 2)))
                {
                    context.DrawGeometry(null, _iconPen, glyph);
                }
                break;
            }
            case IImage image:
                using (context.PushClip(new RoundedRect(box, box.Width / 2)))
                {
                    context.DrawImage(image, box);
                }
                break;
        }
    }

    /// <summary>The visual layer that paints rows above the gliding highlight.</summary>
    private sealed class RowsLayer : Control
    {
        private readonly SelectOptionView _owner;

        public RowsLayer(SelectOptionView owner)
        {
            _owner = owner;
            IsHitTestVisible = false;
        }

        public override void Render(DrawingContext context) => _owner.RenderRows(context);
    }

    internal static string Describe(SelectEntrySource entries, int entryIndex, int position, int count) =>
        entryIndex < 0 || entryIndex >= entries.Count
            ? string.Empty
            : string.Format(CultureInfo.CurrentCulture, "{0}, {1} of {2}", entries[entryIndex].Text, position + 1, count);
}
