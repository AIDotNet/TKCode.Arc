using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The country listbox of the phone picker, drawn rather than built from controls (49 countries plus the suggested
/// ones): group labels, rows with flag, name, calling code and a check on the selected country, and one highlight that
/// glides between rows on the glide spring. Row text is TextSecondary and turns Foreground (fast tween) on the active or
/// selected row; the check fades in (fast) while it scales from 0.6 on the CSS spring. Text layouts are built once per row
/// and reused; only rows inside the scroll viewport are drawn.
/// </summary>
internal sealed class PhoneInputCountryList : Control
{
    /// <summary>One section of the list; a null label renders an unlabeled group (search results).</summary>
    public sealed record Section(string Key, string? Label, IReadOnlyList<PhoneCountry> Countries);

    internal const double RowHeight = 36;
    internal const double RowGap = 2;
    internal const double GroupGap = 4;
    internal const double RowRadius = 16;
    internal const double LabelPaddingTop = 8;
    internal const double LabelPaddingBottom = 4;

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<PhoneInputCountryList, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<IBrush?> EmphasisForegroundProperty =
        AvaloniaProperty.Register<PhoneInputCountryList, IBrush?>(nameof(EmphasisForeground));

    public static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<PhoneInputCountryList, IBrush?>(nameof(MutedForeground));

    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<PhoneInputCountryList, IBrush?>(nameof(HighlightBrush));

    public static readonly StyledProperty<double> SmallFontSizeProperty =
        AvaloniaProperty.Register<PhoneInputCountryList, double>(nameof(SmallFontSize), 12);

    public static readonly StyledProperty<double> LineHeightFactorProperty =
        AvaloniaProperty.Register<PhoneInputCountryList, double>(nameof(LineHeightFactor), 1.4);

    /// <summary>Row text (TextSecondary).</summary>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>Active or selected row text and the check (Foreground).</summary>
    public IBrush? EmphasisForeground { get => GetValue(EmphasisForegroundProperty); set => SetValue(EmphasisForegroundProperty, value); }

    /// <summary>Group labels, calling codes and the empty message (TextMuted).</summary>
    public IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }

    /// <summary>The gliding highlight (foreground at 6.5%).</summary>
    public IBrush? HighlightBrush { get => GetValue(HighlightBrushProperty); set => SetValue(HighlightBrushProperty, value); }

    /// <summary>Group labels and calling codes (TypeXS).</summary>
    public double SmallFontSize { get => GetValue(SmallFontSizeProperty); set => SetValue(SmallFontSizeProperty, value); }

    /// <summary>Line height as a multiple of the font size (Arc's <c>--leading-body</c>).</summary>
    public double LineHeightFactor { get => GetValue(LineHeightFactorProperty); set => SetValue(LineHeightFactorProperty, value); }

    private readonly List<Item> _items = new();
    private readonly List<int> _rowItems = new();
    private readonly Dictionary<string, RowState> _states = new();
    private readonly Dictionary<string, TextLayout> _labels = new();
    private readonly Dictionary<PhoneCountry, TextLayout> _metas = new();
    private readonly HighlightLayer _highlight;
    private readonly RowsLayer _rows;
    private TextLayout? _empty;
    private string? _emptyText;
    private PhoneCountry? _selected;
    private int _active = -1;
    private double _extent;
    private double _layoutWidth = -1;
    private Rect? _viewport;
    private MotionValue<double>? _hy;
    private MotionValue<double>? _hh;
    private MotionValue<double>? _ho;
    private Pen? _checkPen;
    private int _pressedRow = -1;

    private struct Item
    {
        public int Section;
        public int Row;          // index into the row list, or -1 for a label / the empty message
        public string? Label;
        public string? Key;       // "p-US", "a-US", "r-US" (built once per content change)
        public PhoneCountry? Country;
        public bool IsEmpty;
        public double Top;
        public double Height;
    }

    static PhoneInputCountryList()
    {
        FocusableProperty.OverrideDefaultValue<PhoneInputCountryList>(false);
        AffectsMeasure<PhoneInputCountryList>(SmallFontSizeProperty, LineHeightFactorProperty, TextElement.FontSizeProperty,
            TextElement.FontFamilyProperty);
    }

    public PhoneInputCountryList()
    {
        _highlight = new HighlightLayer(this);
        _rows = new RowsLayer(this);
        VisualChildren.Add(_highlight);
        VisualChildren.Add(_rows);
        EffectiveViewportChanged += (_, e) =>
        {
            _viewport = e.EffectiveViewport;
            _rows.InvalidateVisual();
        };
    }

    /// <summary>Raised when the mouse moves onto a row (Arc moves the active row with the pointer).</summary>
    public event Action<int>? RowHovered;

    /// <summary>Raised when a row is clicked (or selected through automation).</summary>
    public event Action<int>? RowInvoked;

    /// <summary>Picks a row as a click would.</summary>
    internal void InvokeRow(int row)
    {
        if (row >= 0 && row < _rowItems.Count)
        {
            RowInvoked?.Invoke(row);
        }
    }

    /// <summary>The sections shown.</summary>
    public IReadOnlyList<Section> Sections { get; private set; } = Array.Empty<Section>();

    /// <summary>Number of rows (labels excluded).</summary>
    public int RowCount => _rowItems.Count;

    /// <summary>Height of the content (labels, rows, gaps and the empty message).</summary>
    public double Extent => _extent;

    /// <summary>The highlighted row, or −1.</summary>
    public int ActiveRow => _active;

    /// <summary>Highlight opacity (0 hidden).</summary>
    internal double HighlightOpacity => _ho?.Value ?? 0;

    /// <summary>Highlight top in list coordinates.</summary>
    internal double HighlightTop => _hy?.Value ?? 0;

    /// <summary>The empty message shown when there are no rows (null when rows exist).</summary>
    public string? EmptyText => _rowItems.Count == 0 ? _emptyText : null;

    public PhoneCountry? CountryAt(int row) => row >= 0 && row < _rowItems.Count ? CountryOf(_items[_rowItems[row]]) : null;

    public string? KeyAt(int row)
    {
        if (row < 0 || row >= _rowItems.Count)
        {
            return null;
        }
        var item = _items[_rowItems[row]];
        return KeyOf(item);
    }

    public int IndexOfKey(string? key)
    {
        if (key is null)
        {
            return -1;
        }
        for (var i = 0; i < _rowItems.Count; i++)
        {
            if (KeyOf(_items[_rowItems[i]]) == key)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>The label of the section a row belongs to (null for unlabeled results).</summary>
    public string? SectionLabelOf(int row) => row >= 0 && row < _rowItems.Count ? Sections[_items[_rowItems[row]].Section].Label : null;

    private static PhoneCountry CountryOf(in Item item) => item.Country!;

    private static string KeyOf(in Item item) => item.Key!;

    /// <summary>Replaces the content. Row states (emphasis, checks) survive for rows whose key stays.</summary>
    public void SetContent(IReadOnlyList<Section> sections, string? emptyText)
    {
        Sections = sections;
        if (_emptyText != emptyText)
        {
            _emptyText = emptyText;
            _empty = null;
        }
        _active = -1;
        _pressedRow = -1;
        RebuildItems();
        InvalidateMeasure();
        _rows.InvalidateVisual();
        _highlight.InvalidateVisual();
        (ControlAutomationPeer.FromElement(this) as PhoneInputCountryListAutomationPeer)?.Refresh();
    }

    /// <summary>The selected country (check and emphasis).</summary>
    public PhoneCountry? SelectedCountry
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value))
            {
                return;
            }
            _selected = value;
            RefreshStates(animate: true);
        }
    }

    /// <summary>
    /// Moves the highlight to <paramref name="row"/> (Arc: it jumps when hidden or under reduced motion, else glides on
    /// the glide spring, and fades in over 0.12s). −1 fades it out (instantly when <paramref name="instantHide"/>).
    /// </summary>
    public void SetActive(int row, bool instantHide = false)
    {
        _active = row >= 0 && row < _rowItems.Count ? row : -1;
        EnsureHighlight();
        var reduced = ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full;
        if (_active < 0)
        {
            if (instantHide || reduced)
            {
                _ho!.Jump(0);
            }
            else
            {
                _ = _ho!.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(120), ArcEasings.Standard));
            }
        }
        else
        {
            var rect = RowRect(_active);
            if (_ho!.Value < 0.05 || reduced || !IsLoaded)
            {
                _hy!.Jump(rect.Y);
                _hh!.Jump(rect.Height);
            }
            else
            {
                _ = _hy!.AnimateTo(rect.Y, MotionSpec.Spring(PhoneInputFace.Glide));
                _ = _hh!.AnimateTo(rect.Height, MotionSpec.Spring(PhoneInputFace.Glide));
            }
            if (reduced || !IsLoaded)
            {
                _ho.Jump(1);
            }
            else
            {
                _ = _ho.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(120), ArcEasings.Enter));
            }
        }
        RefreshStates(animate: true);
        _highlight.InvalidateVisual();
    }

    /// <summary>The rectangle of a row in this control's coordinates.</summary>
    public Rect RowRect(int row)
    {
        if (row < 0 || row >= _rowItems.Count)
        {
            return default;
        }
        var item = _items[_rowItems[row]];
        var width = Bounds.Width > 0 ? Bounds.Width : Math.Max(0, _layoutWidth);
        return new Rect(0, item.Top, width, item.Height);
    }

    /// <summary>Arc's scroll rule: keeps the row inside the scroller with 6px padding (in the list's own coordinates).</summary>
    public void ScrollIntoView(int row, ScrollViewer? scroller, double pad = 6)
    {
        if (scroller is null || row < 0 || row >= _rowItems.Count)
        {
            return;
        }
        var rect = RowRect(row);
        var scrollTop = scroller.Offset.Y;
        var clientHeight = scroller.Viewport.Height;
        if (rect.Y < scrollTop + pad)
        {
            scroller.Offset = new Vector(scroller.Offset.X, Math.Max(0, rect.Y - pad));
        }
        else if (rect.Bottom > scrollTop + clientHeight - pad)
        {
            scroller.Offset = new Vector(scroller.Offset.X, rect.Bottom - clientHeight + pad);
        }
    }

    private void EnsureHighlight()
    {
        if (_hy is not null)
        {
            return;
        }
        _hy = new MotionValue<double>(this, 0, MotionVectors.Double);
        _hh = new MotionValue<double>(this, RowHeight, MotionVectors.Double);
        _ho = new MotionValue<double>(this, 0, MotionVectors.Double);
        _hy.Changed += _ => _highlight.InvalidateVisual();
        _hh.Changed += _ => _highlight.InvalidateVisual();
        _ho.Changed += _ => _highlight.InvalidateVisual();
    }

    // ------------------------------------------------------------------ layout

    private double Leading(double size) => Math.Round(size * LineHeightFactor, 2);

    private double LabelHeight => LabelPaddingTop + Leading(SmallFontSize) + LabelPaddingBottom;

    private void RebuildItems()
    {
        _items.Clear();
        _rowItems.Clear();
        var y = 0.0;
        var row = 0;
        for (var s = 0; s < Sections.Count; s++)
        {
            var section = Sections[s];
            if (s > 0)
            {
                // The options grid gap plus `.group + .group { margin-top: 4px }`.
                y += RowGap + GroupGap;
            }
            var first = true;
            if (section.Label is not null)
            {
                _items.Add(new Item { Section = s, Row = -1, Label = section.Label, Top = y, Height = LabelHeight });
                y += LabelHeight;
                first = false;
            }
            foreach (var country in section.Countries)
            {
                if (!first)
                {
                    y += RowGap;
                }
                first = false;
                _rowItems.Add(_items.Count);
                _items.Add(new Item
                {
                    Section = s, Row = row, Key = section.Key + "-" + country.Iso, Country = country, Top = y, Height = RowHeight
                });
                y += RowHeight;
                row++;
            }
        }
        if (_rowItems.Count == 0 && _emptyText is not null)
        {
            if (Sections.Count > 0)
            {
                y += RowGap;
            }
            var height = 12 + EmptyLayout(Math.Max(0, _layoutWidth)).Height + 14;
            _items.Add(new Item { Section = -1, Row = -1, IsEmpty = true, Top = y, Height = height });
            y += height;
        }
        _extent = y;
        // Row states belong to keys; drop the ones that left.
        var keep = new HashSet<string>();
        for (var i = 0; i < _rowItems.Count; i++)
        {
            keep.Add(KeyOf(_items[_rowItems[i]]));
        }
        foreach (var key in _states.Keys.ToArray())
        {
            if (!keep.Contains(key))
            {
                _states[key].Dispose();
                _states.Remove(key);
            }
        }
        RefreshStates(animate: false);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        if (Math.Abs(width - _layoutWidth) > 0.01)
        {
            _layoutWidth = width;
            InvalidateTexts();
            if (_rowItems.Count == 0)
            {
                RebuildItems();
            }
        }
        _highlight.Measure(availableSize);
        _rows.Measure(availableSize);
        return new Size(width, _extent);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Math.Abs(finalSize.Width - _layoutWidth) > 0.01)
        {
            _layoutWidth = finalSize.Width;
            InvalidateTexts();
        }
        _highlight.Arrange(new Rect(finalSize));
        _rows.Arrange(new Rect(finalSize));
        return finalSize;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == EmphasisForegroundProperty ||
            change.Property == MutedForegroundProperty || change.Property == TextElement.FontFamilyProperty ||
            change.Property == TextElement.FontSizeProperty || change.Property == SmallFontSizeProperty ||
            change.Property == LineHeightFactorProperty)
        {
            _checkPen = null;
            _metas.Clear();
            _labels.Clear();
            InvalidateTexts();
            RefreshStates(animate: false);
            _rows.InvalidateVisual();
        }
        else if (change.Property == HighlightBrushProperty)
        {
            _highlight.InvalidateVisual();
        }
    }

    private void InvalidateTexts()
    {
        _empty = null;
        foreach (var state in _states.Values)
        {
            state.Name = null;
            state.Flag = null;
        }
    }

    private Typeface BodyTypeface(FontWeight weight) => new(TextElement.GetFontFamily(this), FontStyle.Normal, weight);

    private TextLayout EmptyLayout(double width)
    {
        if (_empty is null)
        {
            var text = _emptyText ?? string.Empty;
            var size = TextElement.GetFontSize(this);
            _empty = new TextLayout(text, BodyTypeface(FontWeight.Normal), size, MutedForeground, textWrapping: TextWrapping.Wrap,
                maxWidth: Math.Max(1, width - 20), lineHeight: Leading(size));
        }
        return _empty;
    }

    private TextLayout LabelLayout(string label)
    {
        if (!_labels.TryGetValue(label, out var layout))
        {
            layout = new TextLayout(label, BodyTypeface(FontWeight.Normal), SmallFontSize, MutedForeground, lineHeight: Leading(SmallFontSize));
            _labels[label] = layout;
        }
        return layout;
    }

    private TextLayout MetaLayout(PhoneCountry country)
    {
        if (!_metas.TryGetValue(country, out var layout))
        {
            layout = new TextLayout("+" + country.Dial, BodyTypeface(FontWeight.Normal), SmallFontSize, MutedForeground,
                fontFeatures: PhoneInputFace.TabularNumbers);
            _metas[country] = layout;
        }
        return layout;
    }

    // ------------------------------------------------------------------ row states

    private static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;

    private void RefreshStates(bool animate)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        for (var i = 0; i < _rowItems.Count; i++)
        {
            var item = _items[_rowItems[i]];
            var key = KeyOf(item);
            var country = CountryOf(item);
            var selected = ReferenceEquals(country, _selected);
            var emphasis = selected || i == _active ? 1.0 : 0.0;
            var check = selected ? 1.0 : 0.0;
            if (!_states.TryGetValue(key, out var state))
            {
                state = new RowState(this, emphasis, check);
                _states[key] = state;
                continue;
            }
            state.SetEmphasis(emphasis, animate && IsLoaded);
            state.SetCheck(check, animate && IsLoaded && level == ArcMotionLevel.Full);
        }
    }

    private sealed class RowState : IDisposable
    {
        private readonly PhoneInputCountryList _owner;
        private readonly SolidColorBrush _brush = new();
        private MotionValue<double>? _emphasis;
        private MotionValue<double>? _checkOpacity;
        private MotionValue<double>? _checkScale;
        private double _emphasisValue;
        private double _emphasisTarget;
        private double _checkTarget;

        public RowState(PhoneInputCountryList owner, double emphasis, double check)
        {
            _owner = owner;
            _emphasisValue = emphasis;
            _emphasisTarget = emphasis;
            _checkTarget = check;
            CheckOpacity = check;
            CheckScale = check > 0 ? 1 : 0.6;
            UpdateBrush();
        }

        public TextLayout? Name;
        public TextLayout? Flag;
        public double CheckOpacity;
        public double CheckScale;

        public IBrush Brush => _brush;

        public void UpdateBrush()
        {
            var from = ColorOf(_owner.Foreground);
            var to = ColorOf(_owner.EmphasisForeground);
            var t = Math.Clamp(_emphasisValue, 0, 1);
            _brush.Color = Color.FromArgb(
                (byte)Math.Round(from.A + (to.A - from.A) * t),
                (byte)Math.Round(from.R + (to.R - from.R) * t),
                (byte)Math.Round(from.G + (to.G - from.G) * t),
                (byte)Math.Round(from.B + (to.B - from.B) * t));
        }

        public void SetEmphasis(double target, bool animate)
        {
            if (Math.Abs(target - _emphasisTarget) < 0.001)
            {
                UpdateBrush();
                return;
            }
            _emphasisTarget = target;
            if (!animate)
            {
                _emphasis?.Jump(target);
                _emphasisValue = target;
                UpdateBrush();
                return;
            }
            _emphasis ??= CreateValue(_emphasisValue, v =>
            {
                _emphasisValue = v;
                UpdateBrush();
            });
            _ = _emphasis.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }

        public void SetCheck(double target, bool animate)
        {
            if (Math.Abs(target - _checkTarget) < 0.001)
            {
                return;
            }
            _checkTarget = target;
            if (!animate)
            {
                _checkOpacity?.Jump(target);
                _checkScale?.Jump(target > 0 ? 1 : 0.6);
                CheckOpacity = target;
                CheckScale = target > 0 ? 1 : 0.6;
                return;
            }
            _checkOpacity ??= CreateValue(CheckOpacity, v => CheckOpacity = v);
            _checkScale ??= CreateValue(CheckScale, v => CheckScale = v);
            _ = _checkOpacity.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            _ = _checkScale.AnimateTo(target > 0 ? 1 : 0.6, MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
        }

        private MotionValue<double> CreateValue(double initial, Action<double> apply)
        {
            var value = new MotionValue<double>(_owner, initial, MotionVectors.Double);
            value.Changed += v =>
            {
                apply(v);
                _owner._rows.InvalidateVisual();
            };
            return value;
        }

        public void Dispose()
        {
            _emphasis?.Dispose();
            _checkOpacity?.Dispose();
            _checkScale?.Dispose();
        }
    }

    // ------------------------------------------------------------------ input

    private int RowAt(Point point)
    {
        for (var i = 0; i < _rowItems.Count; i++)
        {
            var item = _items[_rowItems[i]];
            if (point.Y >= item.Top && point.Y < item.Top + item.Height)
            {
                return i;
            }
        }
        return -1;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type != PointerType.Mouse)
        {
            return;
        }
        var row = RowAt(e.GetPosition(this));
        if (row >= 0 && row != _active)
        {
            RowHovered?.Invoke(row);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // Keeps focus in the search field (Arc: preventDefault on pointerdown).
        _pressedRow = RowAt(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var row = RowAt(e.GetPosition(this));
        var pressed = _pressedRow;
        _pressedRow = -1;
        if (row >= 0 && row == pressed && e.InitialPressMouseButton == MouseButton.Left)
        {
            e.Handled = true;
            RowInvoked?.Invoke(row);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PhoneInputCountryListAutomationPeer(this);

    // ------------------------------------------------------------------ drawing

    public override void Render(DrawingContext context)
    {
        // Hit-test surface: the gaps between rows still track the pointer.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
    }

    private void RenderHighlight(DrawingContext context)
    {
        var opacity = Math.Clamp(_ho?.Value ?? 0, 0, 1);
        if (opacity <= 0.001 || HighlightBrush is not { } brush)
        {
            return;
        }
        var rect = new Rect(0, _hy!.Value, Bounds.Width, Math.Max(0, _hh!.Value));
        using (context.PushOpacity(opacity))
        {
            context.DrawRectangle(brush, null, new RoundedRect(rect, RowRadius));
        }
    }

    private void RenderRows(DrawingContext context)
    {
        var width = Bounds.Width;
        var viewport = _viewport ?? new Rect(Bounds.Size);
        var top = viewport.Y - RowHeight;
        var bottom = viewport.Bottom + RowHeight;
        var size = TextElement.GetFontSize(this);
        foreach (var item in _items)
        {
            if (item.Top + item.Height < top || item.Top > bottom)
            {
                continue;
            }
            if (item.IsEmpty)
            {
                EmptyLayout(width).Draw(context, new Point(10, item.Top + 12));
                continue;
            }
            if (item.Label is { } label)
            {
                LabelLayout(label).Draw(context, new Point(10, item.Top + LabelPaddingTop));
                continue;
            }
            var key = KeyOf(item);
            if (!_states.TryGetValue(key, out var state))
            {
                continue;
            }
            var country = CountryOf(item);
            var centerY = item.Top + item.Height / 2;
            // Flag: a 20px slot, 17px emoji.
            state.Flag ??= new TextLayout(country.Flag, new Typeface(PhoneInputFace.FlagFontFamily), PhoneInputFace.FlagFontSize,
                state.Brush, lineHeight: PhoneInputFace.FlagFontSize);
            var flag = state.Flag;
            flag.Draw(context, new Point(10 + (PhoneInputFace.FlagSlot - flag.WidthIncludingTrailingWhitespace) / 2, centerY - flag.Height / 2));
            // Trailing parts: calling code, then the 16px check, 10px apart, 10px row padding.
            var checkX = width - 10 - 16;
            var meta = MetaLayout(country);
            var metaX = checkX - 10 - meta.WidthIncludingTrailingWhitespace;
            meta.Draw(context, new Point(metaX, centerY - meta.Height / 2));
            var nameX = 10 + PhoneInputFace.FlagSlot + 10;
            state.Name ??= new TextLayout(country.Name, BodyTypeface(FontWeight.Medium), size, state.Brush,
                textTrimming: TextTrimming.CharacterEllipsis, maxWidth: Math.Max(1, metaX - 10 - nameX), lineHeight: Leading(size));
            state.Name.Draw(context, new Point(nameX, centerY - state.Name.Height / 2));
            if (state.CheckOpacity > 0.001)
            {
                DrawCheck(context, new Point(checkX + 8, centerY), state.CheckOpacity, state.CheckScale);
            }
        }
    }

    private void DrawCheck(DrawingContext context, Point center, double opacity, double scale)
    {
        if (EmphasisForeground is not { } brush)
        {
            return;
        }
        _checkPen ??= new Pen(brush is ISolidColorBrush solid ? new ImmutableSolidColorBrush(solid.Color) : brush, 1.75,
            lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var unit = 16.0 / 24 * scale;
        var matrix = Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(unit, unit) * Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushOpacity(Math.Clamp(opacity, 0, 1)))
        using (context.PushTransform(matrix))
        {
            context.DrawGeometry(null, _checkPen, ArcIcons.Check);
        }
    }

    private sealed class HighlightLayer : Control
    {
        private readonly PhoneInputCountryList _owner;

        public HighlightLayer(PhoneInputCountryList owner)
        {
            _owner = owner;
            IsHitTestVisible = false;
        }

        public override void Render(DrawingContext context) => _owner.RenderHighlight(context);
    }

    private sealed class RowsLayer : Control
    {
        private readonly PhoneInputCountryList _owner;

        public RowsLayer(PhoneInputCountryList owner)
        {
            _owner = owner;
            IsHitTestVisible = false;
        }

        public override void Render(DrawingContext context) => _owner.RenderRows(context);
    }
}
