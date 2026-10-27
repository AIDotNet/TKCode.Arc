using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>One row of the ranked list.</summary>
internal sealed class ChoroplethRankItem
{
    public required ChoroplethRegion Region { get; init; }
    public required string Name { get; init; }
    public required string ValueText { get; init; }
    public required string RankText { get; init; }
    public required int Rank { get; init; }

    /// <summary>|value| / max |value| (0 without a value; drawn at least 1.5%).</summary>
    public required double Fraction { get; init; }

    public required bool IsNegative { get; init; }
    public required bool HasChevron { get; init; }
    public required string AutomationName { get; init; }
    public required int Bin { get; init; }
}

/// <summary>How the list moves to new rows.</summary>
internal enum ChoroplethListChange
{
    /// <summary>At once.</summary>
    Jump,

    /// <summary>The same regions in a new order: rows slide to their new ranks (FLIP) and bars grow or shrink.</summary>
    Reorder,

    /// <summary>Other regions (another level or area): the old rows fade out (180ms), the new ones fade in (240ms).</summary>
    Replace,
}

/// <summary>
/// The ranked list of a <see cref="ChoroplethExplorer"/>: one 44px row per region (rank, name, value, a 3px bar, a chevron
/// when it opens), 2 apart, inside a scroller. Rows slide to new ranks on the Smooth spring (measured ω ≈ 12.5, critically
/// damped), the scroller glides on the same spring to centre the row the map points at, and the keyboard moves one row at a
/// time (one tab stop; ↑ / ↓ / Home / End do not wrap).
/// </summary>
/// <remarks>
/// Why code-driven: the rows' positions (FLIP), bars, tints and dimming are springs and tweens advanced by one frame loop;
/// the scroller's offset is a spring that keeps its velocity when the target changes, which a ControlTheme cannot express.
/// </remarks>
internal sealed class ChoroplethRankList : Panel
{
    internal const double RowHeight = 44;
    internal const double RowGap = 2;
    internal const double PaddingTop = 2;
    internal const double PaddingBottom = 4;

    private static readonly TimeSpan FadeOut = ArcMotionPresets.Exit;
    private static readonly TimeSpan FadeIn = ArcMotionPresets.Standard;

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(512);
    private readonly List<ChoroplethRankRow> _rows = new();
    private readonly LiveLoop _loop;
    private AnalyticsTween _fade = new(1);
    private IReadOnlyList<ChoroplethRankItem>? _pending;
    private AnalyticsSpringChannel _scroll = new(0, 0.1, 1);
    private double _lastScroll = double.NaN;
    private ScrollViewer? _scroller;
    private ChoroplethRegion? _active;
    private ChoroplethRegion? _pinned;
    private int? _litBin;
    private Color _positive;
    private Color _negative;

    public ChoroplethRankList()
    {
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        ActualThemeVariantChanged += (_, _) => RefreshTheme();
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Once);
        ClipToBounds = false;
    }

    /// <summary>A row came under the pointer (null: off every row).</summary>
    public event Action<ChoroplethRegion?>? RowHovered;

    /// <summary>A row got the keyboard focus (null: the focus left the list).</summary>
    public event Action<ChoroplethRegion?>? RowFocused;

    /// <summary>A row was pressed.</summary>
    public event Action<ChoroplethRegion>? RowInvoked;

    /// <summary>The scroller the list sits in.</summary>
    public ScrollViewer? Scroller
    {
        get => _scroller;
        set
        {
            if (_scroller is not null)
            {
                _scroller.ScrollChanged -= OnScrollChanged;
            }
            _scroller = value;
            if (_scroller is not null)
            {
                _scroller.ScrollChanged += OnScrollChanged;
            }
        }
    }

    internal AnalyticsPalette Palette => _palette;

    internal AnalyticsTextCache Text => _text;

    internal Color PositiveColor => _positive;

    internal Color NegativeColor => _negative;

    /// <summary>The rows in rank order (tests).</summary>
    internal IReadOnlyList<ChoroplethRankRow> Rows => _rows;

    /// <summary>The list's fade (tests).</summary>
    internal double FadeValue => _fade.Value;

    /// <summary>The scroll target (tests).</summary>
    internal double ScrollTarget => _scroll.Target;

    internal bool Animates => IsLoaded && ArcMotion.IsFull(this);

    /// <summary>The bar colours (positive and negative values).</summary>
    public void SetColors(Color positive, Color negative)
    {
        _positive = positive;
        _negative = negative;
        foreach (var row in _rows)
        {
            row.InvalidateVisual();
        }
    }

    /// <summary>Shows <paramref name="items"/> in their order.</summary>
    public void SetItems(IReadOnlyList<ChoroplethRankItem> items, ChoroplethListChange change)
    {
        var animate = Animates;
        if (change == ChoroplethListChange.Replace && animate && _rows.Count > 0)
        {
            _pending = items;
            _fade.Start(0, FadeOut, AnalyticsEasings.EaseOut);
            _loop.Start();
            return;
        }
        _pending = null;
        if (change == ChoroplethListChange.Reorder && animate && _rows.Count > 0)
        {
            Reorder(items);
        }
        else
        {
            Rebuild(items);
            if (change == ChoroplethListChange.Replace)
            {
                _fade.Jump(1);
                Opacity = 1;
            }
        }
        _loop.Start();
    }

    private void Rebuild(IReadOnlyList<ChoroplethRankItem> items)
    {
        var focusWasInside = _rows.Any(r => r.IsFocused);
        Children.Clear();
        _rows.Clear();
        foreach (var item in items)
        {
            var row = new ChoroplethRankRow(this);
            row.SetItem(item, false);
            _rows.Add(row);
            Children.Add(row);
        }
        ApplyRowStates(false);
        if (_rows.Count > 0)
        {
            KeyboardNavigation.SetTabOnceActiveElement(this, _rows[0]);
        }
        if (focusWasInside && _rows.Count > 0)
        {
            _rows[0].Focus(NavigationMethod.Directional);
        }
        JumpScroll(0);
        InvalidateMeasure();
    }

    private void Reorder(IReadOnlyList<ChoroplethRankItem> items)
    {
        var byRegion = new Dictionary<ChoroplethRegion, ChoroplethRankRow>();
        foreach (var row in _rows)
        {
            byRegion[row.Region] = row;
        }
        var oldY = new Dictionary<ChoroplethRankRow, double>();
        for (var i = 0; i < _rows.Count; i++)
        {
            oldY[_rows[i]] = SlotY(i) + _rows[i].Offset;
        }
        var next = new List<ChoroplethRankRow>(items.Count);
        foreach (var item in items)
        {
            if (!byRegion.Remove(item.Region, out var row))
            {
                row = new ChoroplethRankRow(this);
                Children.Add(row);
            }
            row.SetItem(item, true);
            next.Add(row);
        }
        foreach (var gone in byRegion.Values)
        {
            Children.Remove(gone);
        }
        _rows.Clear();
        _rows.AddRange(next);
        for (var i = 0; i < _rows.Count; i++)
        {
            if (oldY.TryGetValue(_rows[i], out var y))
            {
                _rows[i].StartSlide(y - SlotY(i));
            }
        }
        ApplyRowStates(true);
        InvalidateArrange();
    }

    /// <summary>The row lit by the map or the list pointer (null: none); <paramref name="center"/> glides it to the scroller's middle.</summary>
    public void SetActive(ChoroplethRegion? region, bool center)
    {
        _active = region;
        ApplyRowStates(Animates);
        if (center && region is not null && IndexOf(region) is var i and >= 0)
        {
            var viewport = _scroller?.Viewport.Height ?? Bounds.Height;
            ScrollTo(SlotY(i) + RowHeight / 2 - viewport / 2);
        }
    }

    /// <summary>The pinned subregion's row (pressed, lit).</summary>
    public void SetPinned(ChoroplethRegion? region)
    {
        _pinned = region;
        ApplyRowStates(Animates);
    }

    /// <summary>Dims every row outside <paramref name="bin"/> to .32 (null: none) and brings the bin's rows into view.</summary>
    public void SetLitBin(int? bin)
    {
        if (_litBin == bin)
        {
            return;
        }
        _litBin = bin;
        ApplyRowStates(Animates);
        if (bin is { } b)
        {
            var first = _rows.FindIndex(r => r.Item?.Bin == b);
            var last = _rows.FindLastIndex(r => r.Item?.Bin == b);
            if (first >= 0)
            {
                var viewport = _scroller?.Viewport.Height ?? Bounds.Height;
                var top = SlotY(first);
                var bottom = SlotY(last) + RowHeight;
                var target = bottom - top <= viewport ? (top + bottom) / 2 - viewport / 2 : top - RowGap;
                ScrollTo(target);
            }
        }
    }

    internal int IndexOf(ChoroplethRegion region) => _rows.FindIndex(r => ReferenceEquals(r.Region, region));

    internal ChoroplethRankRow? RowOf(ChoroplethRegion region) => IndexOf(region) is var i and >= 0 ? _rows[i] : null;

    /// <summary>Focuses the row of <paramref name="region"/> (or the first) with a visible focus ring.</summary>
    public bool FocusRow(ChoroplethRegion? region)
    {
        var row = region is not null ? RowOf(region) : null;
        row ??= _rows.Count > 0 ? _rows[0] : null;
        if (row is null)
        {
            return false;
        }
        KeyboardNavigation.SetTabOnceActiveElement(this, row);
        return row.Focus(NavigationMethod.Directional);
    }

    private void ApplyRowStates(bool animate)
    {
        foreach (var row in _rows)
        {
            var on = ReferenceEquals(row.Region, _active) || ReferenceEquals(row.Region, _pinned) || row.IsFocused;
            row.SetState(on, ReferenceEquals(row.Region, _pinned), _litBin is { } b && row.Item?.Bin != b, animate);
        }
        _loop.Start();
    }

    internal static double SlotY(int index) => PaddingTop + index * (RowHeight + RowGap);

    // Row callbacks ------------------------------------------------------------------------------------------------------

    internal void OnRowPointer(ChoroplethRankRow row, bool entered) => RowHovered?.Invoke(entered ? row.Region : null);

    internal void OnRowFocus(ChoroplethRankRow row, bool focused, bool keyboard)
    {
        if (focused)
        {
            KeyboardNavigation.SetTabOnceActiveElement(this, row);
            if (keyboard && IndexOf(row.Region) is var i and >= 0)
            {
                ScrollNearest(i);
            }
        }
        ApplyRowStates(Animates);
        RowFocused?.Invoke(focused ? row.Region : null);
    }

    internal void OnRowClicked(ChoroplethRankRow row) => RowInvoked?.Invoke(row.Region);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || e.Source is not Visual source)
        {
            return;
        }
        var row = source as ChoroplethRankRow ?? source.FindAncestorOfType<ChoroplethRankRow>();
        if (row is null)
        {
            return;
        }
        var index = _rows.IndexOf(row);
        int? next = e.Key switch
        {
            Key.Up => Math.Max(0, index - 1),
            Key.Down => Math.Min(_rows.Count - 1, index + 1),
            Key.Home => 0,
            Key.End => _rows.Count - 1,
            _ => null,
        };
        if (next is { } n && n >= 0 && n < _rows.Count)
        {
            e.Handled = true;
            if (n != index)
            {
                _rows[n].Focus(NavigationMethod.Directional);
            }
        }
    }

    // Scrolling -----------------------------------------------------------------------------------------------------------

    private void ScrollNearest(int index)
    {
        if (_scroller is null)
        {
            return;
        }
        var viewport = _scroller.Viewport.Height;
        var current = _scroll.IsMoving ? _scroll.Target : _scroller.Offset.Y;
        var top = SlotY(index) - RowGap;
        var bottom = SlotY(index) + RowHeight + PaddingBottom;
        if (top < current)
        {
            ScrollTo(top);
        }
        else if (bottom > current + viewport)
        {
            ScrollTo(bottom - viewport);
        }
    }

    private void ScrollTo(double target)
    {
        if (_scroller is null)
        {
            return;
        }
        var max = Math.Max(0, _scroller.Extent.Height - _scroller.Viewport.Height);
        target = Math.Clamp(target, 0, max);
        if (!Animates)
        {
            JumpScroll(target);
            return;
        }
        if (!_scroll.IsMoving)
        {
            _scroll.Jump(_scroller.Offset.Y);
        }
        _scroll.SpringTo(target, AnalyticsSprings.Smooth);
        _loop.Start();
    }

    private void JumpScroll(double target)
    {
        _scroll.Jump(target);
        if (_scroller is not null)
        {
            _lastScroll = target;
            _scroller.Offset = new Vector(_scroller.Offset.X, target);
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        // The wheel, the scroll bar or a drag took over: stop gliding.
        if (_scroll.IsMoving && _scroller is not null && Math.Abs(_scroller.Offset.Y - _lastScroll) > 0.75)
        {
            _scroll.Stop();
        }
    }

    // Frames --------------------------------------------------------------------------------------------------------------

    private bool Advance(double dt)
    {
        var moving = false;
        if (_fade.IsRunning)
        {
            var running = _fade.Advance(dt);
            Opacity = Math.Clamp(_fade.Value, 0, 1);
            if (!running && _pending is { } pending && _fade.Target <= 0)
            {
                _pending = null;
                Rebuild(pending);
                _fade.Start(1, FadeIn, AnalyticsEasings.Enter);
                running = true;
            }
            moving |= running;
        }
        foreach (var row in _rows)
        {
            moving |= row.Advance(dt);
        }
        if (_scroll.IsMoving && _scroller is not null)
        {
            moving |= _scroll.Step(dt);
            _lastScroll = _scroll.Value;
            _scroller.Offset = new Vector(_scroller.Offset.X, _scroll.Value);
        }
        return moving;
    }

    // Layout --------------------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 300;
        foreach (var row in _rows)
        {
            row.Measure(new Size(width, RowHeight));
        }
        var height = _rows.Count == 0 ? 0 : SlotY(_rows.Count - 1) + RowHeight + PaddingBottom;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].Arrange(new Rect(0, SlotY(i), finalSize.Width, RowHeight));
        }
        return finalSize;
    }

    // Theme ----------------------------------------------------------------------------------------------------------------

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ResourcesChanged += OnResourcesChanged;
        RefreshTheme();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ResourcesChanged -= OnResourcesChanged;
    }

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e) => RefreshTheme();

    private void RefreshTheme()
    {
        if (!this.IsAttachedToVisualTree())
        {
            return;
        }
        _palette.Refresh(this);
        _text.Clear();
        foreach (var row in _rows)
        {
            row.InvalidateVisual();
        }
    }

    internal void EnsurePalette()
    {
        if (!_palette.IsReady && this.IsAttachedToVisualTree())
        {
            _palette.Refresh(this);
        }
    }
}

/// <summary>
/// One row of the ranked list (a self-drawn toggle button: pressed while its subregion is pinned). Grid of 22 | name | value |
/// 16 columns over a 22px line and a 3px bar, 7 / 8 padding; lit rows get the muted surface and foreground text (160ms).
/// </summary>
internal sealed class ChoroplethRankRow : ToggleButton
{
    private readonly ChoroplethRankList _owner;
    private readonly TranslateTransform _slide = new();
    private AnalyticsSpringChannel _offset = new(0, 0.05, 0.5);
    private AnalyticsTween _bar = new(0);
    private AnalyticsTween _barTone = new(0);
    private AnalyticsTween _on = new(0);
    private AnalyticsTween _dim = new(1);

    public ChoroplethRankRow(ChoroplethRankList owner)
    {
        _owner = owner;
        Focusable = true;
        FocusAdorner = null;
        ClipToBounds = false;
        RenderTransform = _slide;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    protected override Type StyleKeyOverride => typeof(ChoroplethRankRow);

    public ChoroplethRankItem? Item { get; private set; }

    public ChoroplethRegion Region => Item!.Region;

    /// <summary>The FLIP offset (px from its slot).</summary>
    public double Offset => _offset.Value;

    /// <summary>The drawn bar fraction (tests).</summary>
    internal double BarValue => _bar.Value;

    /// <summary>The lit tint 0…1 (tests).</summary>
    internal double OnValue => _on.Value;

    /// <summary>The dimming opacity (tests).</summary>
    internal double DimValue => _dim.Value;

    public void SetItem(ChoroplethRankItem item, bool animate)
    {
        Item = item;
        AutomationProperties.SetName(this, item.AutomationName);
        var fraction = item.Fraction;
        if (animate)
        {
            // Arc's 580ms CSS spring curve (scaleX); every bar animates, including rows that moved.
            _bar.Start(fraction, ArcMotionPresets.CssSpring, AnalyticsEasings.CssSpring);
            _barTone.Start(item.IsNegative ? 1 : 0, ArcMotionPresets.Standard, AnalyticsEasings.Standard);
        }
        else
        {
            _bar.Jump(fraction);
            _barTone.Jump(item.IsNegative ? 1 : 0);
        }
        InvalidateVisual();
    }

    public void StartSlide(double from)
    {
        // Keeps the velocity of a slide still under way (a second metric switch mid-flight).
        _offset.Value = from;
        _offset.SpringTo(0, AnalyticsSprings.Smooth);
        _slide.Y = _offset.Value;
    }

    public void SetState(bool on, bool pinned, bool dimmed, bool animate)
    {
        IsChecked = pinned;
        var onTarget = on ? 1 : 0;
        var dimTarget = dimmed ? 0.32 : 1;
        if (animate)
        {
            if (Math.Abs(_on.Target - onTarget) > 1e-6)
            {
                _on.Start(onTarget, ArcMotionPresets.Fast, AnalyticsEasings.Standard);
            }
            if (Math.Abs(_dim.Target - dimTarget) > 1e-6)
            {
                _dim.Start(dimTarget, ArcMotionPresets.Standard, AnalyticsEasings.Standard);
            }
        }
        else
        {
            _on.Jump(onTarget);
            _dim.Jump(dimTarget);
        }
        Opacity = _dim.Value;
        InvalidateVisual();
    }

    public bool Advance(double dt)
    {
        var moving = _bar.Advance(dt) | _barTone.Advance(dt) | _on.Advance(dt) | _dim.Advance(dt);
        if (_offset.IsMoving)
        {
            moving |= _offset.Step(dt);
            _slide.Y = _offset.Value;
        }
        if (moving)
        {
            Opacity = _dim.Value;
            InvalidateVisual();
        }
        return moving;
    }

    // The block owns the pinned state.
    protected override void Toggle()
    {
    }

    protected override void OnClick()
    {
        base.OnClick();
        _owner.OnRowClicked(this);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            _owner.OnRowPointer(this, true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _owner.OnRowPointer(this, false);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _owner.OnRowFocus(this, true, e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _owner.OnRowFocus(this, false, false);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsFinite(availableSize.Width) ? availableSize.Width : 300, ChoroplethRankList.RowHeight);

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    public override void Render(DrawingContext context)
    {
        _owner.EnsurePalette();
        var palette = _owner.Palette;
        if (!palette.IsReady || Item is not { } item)
        {
            return;
        }
        var size = Bounds.Size;
        var bounds = new Rect(size);
        var on = Math.Clamp(_on.Value, 0, 1);
        context.DrawRectangle(Brushes.Transparent, null, bounds);
        if (on > 0.002)
        {
            context.DrawRectangle(palette.Brush(palette.SurfaceMuted, on), null, new RoundedRect(bounds, 10));
        }
        var text = _owner.Text;
        var face = new Typeface(palette.BodyFont);
        var textColor = AnalyticsColors.Lerp(palette.TextSecondary, palette.Foreground, on);
        var textBrush = palette.Brush(textColor);

        // Columns: 8 | 22 rank | 8 | name … value | 8 | 16 chevron | 8.
        const double padX = 8, padY = 7, line = 22;
        var nameX = padX + 22 + 8;
        var valueRight = size.Width - padX - 16 - 8;
        var rank = text.Get(item.RankText, face, 12, true);
        rank.Draw(context, palette.Brush(palette.TextMuted), new Point(padX, padY + 1 + (16.8 - rank.Height) / 2));
        var value = text.Get(item.ValueText, face, 14, true);
        value.Draw(context, textBrush, new Point(valueRight - value.Width, padY + (line - value.Height) / 2));
        var nameWidth = Math.Max(0, valueRight - value.Width - 8 - nameX);
        var name = text.GetTrimmed(item.Name, face, 14, nameWidth);
        name.Draw(context, textBrush, new Point(nameX, padY + (line - name.Height) / 2));

        // The bar spans the name and value columns: track SurfaceMuted, fill positive or negative (pill, at least 1.5%).
        var barY = padY + line + 5;
        var track = new Rect(nameX, barY, Math.Max(0, valueRight - nameX), 3);
        context.DrawRectangle(palette.Brush(palette.SurfaceMuted), null, new RoundedRect(track, 1.5));
        var fraction = Math.Clamp(Math.Max(_bar.Value, item.Fraction > 0 || _bar.Value > 0 ? 0.015 : 0), 0, 1);
        if (fraction > 0)
        {
            var fill = AnalyticsColors.Lerp(_owner.PositiveColor, _owner.NegativeColor, Math.Clamp(_barTone.Value, 0, 1));
            context.DrawRectangle(palette.Brush(fill), null, new RoundedRect(new Rect(track.X, track.Y, track.Width * fraction, 3), 1.5));
        }

        if (item.HasChevron)
        {
            var glyph = ArcIcons.ChevronRight;
            var x = size.Width - padX - 16;
            var y = (size.Height - 16) / 2;
            using (context.PushTransform(Matrix.CreateScale(16.0 / 24, 16.0 / 24) * Matrix.CreateTranslation(x, y)))
            {
                context.DrawGeometry(null, palette.Pen(palette.TextMuted, 1.75 * 24 / 16), glyph);
            }
        }
        if (AnalyticsFocus.IsVisible(this))
        {
            AnalyticsFocus.Draw(context, palette, bounds, 10);
        }
    }
}
