using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// One step of the Add filter menu (fields, or the values of a field), drawn directly from Arc's menu CSS: 36px rows,
/// one highlight that glides under the pointer on a snappy spring and jumps with the keyboard, the field's current
/// value and a chevron for fields, the hint and a check for values. One tab stop: ↑/↓ wrap, Home/End, → enters a
/// field, ← goes back, Enter/Space chooses, letters jump (typeahead).
/// </summary>
internal sealed class FilterMenuList : Control
{
    internal readonly record struct Row(string Key, string Label, Geometry? Icon, string? Meta, bool Checked, bool Drill);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<FilterMenuList, IBrush?>(nameof(Foreground));
    public static readonly StyledProperty<IBrush?> SecondaryForegroundProperty =
        AvaloniaProperty.Register<FilterMenuList, IBrush?>(nameof(SecondaryForeground));
    public static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<FilterMenuList, IBrush?>(nameof(MutedForeground));
    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<FilterMenuList, IBrush?>(nameof(FocusRingBrush));
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<FilterMenuList, FontFamily>(nameof(FontFamily), FontFamily.Default);
    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<FilterMenuList, double>(nameof(FontSize), 14);
    public static readonly StyledProperty<double> SmallFontSizeProperty =
        AvaloniaProperty.Register<FilterMenuList, double>(nameof(SmallFontSize), 12);

    private const double RowInset = 10;
    private const double IconExtent = 16;
    private const double MetaGlyph = 15;

    private static readonly Cursor s_hand = new(StandardCursorType.Hand);

    private readonly Row[] _rows;
    private readonly double _rowHeight;
    private readonly double _radius;
    private readonly bool _hasIcons;
    private TextLayout?[] _labels = [];
    private TextLayout?[] _metas = [];
    private double[] _labelWidths = [];
    private double[] _metaWidths = [];
    private IBrush? _labelBrush;
    private IBrush? _metaBrush;
    private Typeface _typeface;
    private int _textKey = -1;
    private MotionValue<Rect>? _highlight;
    private MotionValue<double>? _highlightOpacity;
    private bool _shown;
    private bool _ring;
    private bool _pointerMode;
    private string _typed = string.Empty;
    private long _typedAt;
    private ImmutablePen? _iconPen;
    private Color _iconColor;
    private ImmutablePen? _metaPen;
    private Color _metaColor;
    private ImmutablePen? _checkPen;
    private Color _checkColor;
    private ImmutableSolidColorBrush? _fill;
    private ImmutablePen? _ringPen;
    private Color _ringColor;

    static FilterMenuList()
    {
        FocusableProperty.OverrideDefaultValue<FilterMenuList>(true);
        AffectsRender<FilterMenuList>(ForegroundProperty, SecondaryForegroundProperty, MutedForegroundProperty, FocusRingBrushProperty);
        AffectsMeasure<FilterMenuList>(FontFamilyProperty, FontSizeProperty, SmallFontSizeProperty);
    }

    public FilterMenuList(IReadOnlyList<Row> rows, double rowHeight, double radius)
    {
        _rows = rows.ToArray();
        _rowHeight = rowHeight;
        _radius = radius;
        _hasIcons = Array.Exists(_rows, r => r.Icon is not null);
        Current = Math.Max(0, Array.FindIndex(_rows, r => r.Checked));
        Cursor = s_hand;
        FocusAdorner = null;
        ArcTokens.Bind(this, ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(this, SecondaryForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(this, MutedForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(this, FocusRingBrushProperty, ArcDesignTokenKind.FocusRing);
        ArcTokens.Bind(this, FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(this, FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(this, SmallFontSizeProperty, ArcDesignTokenKind.TypeXS);
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public IBrush? SecondaryForeground { get => GetValue(SecondaryForegroundProperty); set => SetValue(SecondaryForegroundProperty, value); }
    public IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }
    public IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public double SmallFontSize { get => GetValue(SmallFontSizeProperty); set => SetValue(SmallFontSizeProperty, value); }

    public IReadOnlyList<Row> Rows => _rows;

    public int Current { get; private set; }

    /// <summary>True when the list was last focused or moved with the keyboard (its highlight and ring show).</summary>
    public bool UsesKeyboard => !_pointerMode;

    /// <summary>Chooses a row; the flag is true for keyboard activation.</summary>
    public Action<Row, bool>? Chosen { get; set; }

    /// <summary>Goes back to the field step (null on the field step).</summary>
    public Action<bool>? Back { get; set; }

    /// <summary>Focuses the list; with <paramref name="keyboard"/> the current row shows its highlight and ring.</summary>
    public void FocusRow(int index, bool keyboard)
    {
        Current = Math.Clamp(index, 0, Math.Max(0, _rows.Length - 1));
        _pointerMode = !keyboard;
        Focus(keyboard ? NavigationMethod.Directional : NavigationMethod.Pointer);
        if (keyboard)
        {
            ShowHighlight(glide: false, ring: true);
        }
    }

    public int IndexOf(string key) => Array.FindIndex(_rows, r => r.Key == key);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 244;
        return new Size(width, _rows.Length * _rowHeight);
    }

    private Rect RowRect(int index) => new(0, index * _rowHeight, Bounds.Width, _rowHeight);

    private void ShowHighlight(bool glide, bool ring)
    {
        if (_rows.Length == 0)
        {
            return;
        }
        var target = RowRect(Current);
        if (_highlight is null)
        {
            _highlight = new MotionValue<Rect>(this, target, MotionVectors.Rect);
            _highlight.Changed += _ => InvalidateVisual();
        }
        if (_highlightOpacity is null)
        {
            _highlightOpacity = new MotionValue<double>(this, 0, MotionVectors.Double);
            _highlightOpacity.Changed += _ => InvalidateVisual();
        }
        if (glide && _shown && ArcMotion.IsFull(this))
        {
            _ = _highlight.AnimateTo(target, MotionSpec.Spring(ArcSpring.Snappy));
        }
        else
        {
            _highlight.Jump(target);
        }
        _ring = ring;
        if (!_shown)
        {
            _shown = true;
            AnimateOpacity(1);
        }
        this.BringIntoView(target);
        UpdateAutomation();
        InvalidateVisual();
    }

    private void HideHighlight()
    {
        if (!_shown)
        {
            return;
        }
        _shown = false;
        _ring = false;
        AnimateOpacity(0);
        InvalidateVisual();
    }

    private void AnimateOpacity(double target)
    {
        if (_highlightOpacity is null)
        {
            return;
        }
        // The highlight fades where it is (80ms), so the fade stays visible.
        _ = _highlightOpacity.AnimateTo(target, ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full
            ? MotionSpec.Tween(TimeSpan.FromMilliseconds(80), ArcEasings.Standard)
            : MotionSpec.Instant);
    }

    private void UpdateAutomation()
    {
        if (Current < 0 || Current >= _rows.Length)
        {
            return;
        }
        var row = _rows[Current];
        var state = row.Drill ? (row.Meta is { Length: > 0 } meta ? $", {meta}" : string.Empty) : row.Checked ? ", checked" : string.Empty;
        AutomationProperties.SetName(this, $"{row.Label}{state}");
    }

    #region Input

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var index = (int)(e.GetPosition(this).Y / _rowHeight);
        if (index < 0 || index >= _rows.Length)
        {
            return;
        }
        _pointerMode = true;
        if (!IsFocused)
        {
            Focus(NavigationMethod.Pointer);
        }
        if (index != Current || !_shown)
        {
            Current = index;
            ShowHighlight(glide: true, ring: false);
        }
        else if (_ring)
        {
            _ring = false;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_pointerMode)
        {
            HideHighlight();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var index = (int)(e.GetPosition(this).Y / _rowHeight);
        if (e.InitialPressMouseButton == MouseButton.Left && index >= 0 && index < _rows.Length &&
            new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            Current = index;
            Chosen?.Invoke(_rows[index], false);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _rows.Length == 0)
        {
            return;
        }
        var count = _rows.Length;
        var visible = _shown;
        switch (e.Key)
        {
            case Key.Down:
                Move(visible ? (Current + 1) % count : Current);
                break;
            case Key.Up:
                Move(visible ? (Current - 1 + count) % count : Current);
                break;
            case Key.Home:
                Move(0);
                break;
            case Key.End:
                Move(count - 1);
                break;
            case Key.Right when _rows[Current].Drill:
                Chosen?.Invoke(_rows[Current], true);
                break;
            case Key.Left when Back is not null:
                Back(true);
                break;
            case Key.Enter:
            case Key.Space:
                Chosen?.Invoke(_rows[Current], true);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (string.IsNullOrWhiteSpace(e.Text) || e.Text == " " || _rows.Length == 0)
        {
            return;
        }
        var now = Environment.TickCount64;
        _typed = now - _typedAt < 700 ? _typed + e.Text.ToLowerInvariant() : e.Text.ToLowerInvariant();
        _typedAt = now;
        var count = _rows.Length;
        var start = _typed.Length > 1 ? Current : Current + 1;
        for (var i = 0; i < count; i++)
        {
            var index = (start + i) % count;
            if (_rows[index].Label.StartsWith(_typed, StringComparison.CurrentCultureIgnoreCase))
            {
                Move(index);
                e.Handled = true;
                return;
            }
        }
    }

    private void Move(int index)
    {
        _pointerMode = false;
        Current = index;
        ShowHighlight(glide: false, ring: true);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional)
        {
            _pointerMode = false;
            ShowHighlight(glide: false, ring: true);
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        HideHighlight();
    }

    #endregion

    #region Rendering

    private void EnsureText(IBrush labelBrush, IBrush metaBrush)
    {
        var key = HashCode.Combine(FontFamily, FontSize, SmallFontSize);
        if (_textKey == key && _labels.Length == _rows.Length && ReferenceEquals(_labelBrush, labelBrush) && ReferenceEquals(_metaBrush, metaBrush))
        {
            return;
        }
        // Layouts are rebuilt only when the font or the brushes change; their widths are set lazily per row in Render.
        _textKey = key;
        _labelBrush = labelBrush;
        _metaBrush = metaBrush;
        _typeface = new Typeface(FontFamily);
        DisposeText();
        _labels = new TextLayout?[_rows.Length];
        _metas = new TextLayout?[_rows.Length];
        _labelWidths = new double[_rows.Length];
        _metaWidths = new double[_rows.Length];
    }

    private void DisposeText()
    {
        foreach (var label in _labels)
        {
            label?.Dispose();
        }
        foreach (var meta in _metas)
        {
            meta?.Dispose();
        }
    }

    private TextLayout LabelAt(int index, double maxWidth)
    {
        maxWidth = Math.Max(1, maxWidth);
        var label = _labels[index];
        if (label is null || Math.Abs(_labelWidths[index] - maxWidth) > 0.5)
        {
            label?.Dispose();
            _labelWidths[index] = maxWidth;
            label = TablesBDrawing.Layout(_rows[index].Label, _typeface, FontSize, _labelBrush, maxWidth, 1);
            _labels[index] = label;
        }
        return label;
    }

    private TextLayout? MetaAt(int index, double maxWidth)
    {
        if (_rows[index].Meta is not { Length: > 0 } text)
        {
            return null;
        }
        maxWidth = Math.Max(1, maxWidth);
        var meta = _metas[index];
        if (meta is null || Math.Abs(_metaWidths[index] - maxWidth) > 0.5)
        {
            meta?.Dispose();
            _metaWidths[index] = maxWidth;
            meta = TablesBDrawing.Layout(text, _typeface, SmallFontSize, _metaBrush, maxWidth, 1, features: TablePalette.TabularNumbers);
            _metas[index] = meta;
        }
        return meta;
    }

    public override void Render(DrawingContext context)
    {
        var foregroundBrush = Foreground ?? Brushes.Black;
        EnsureText(foregroundBrush, MutedForeground ?? Brushes.Gray);
        var width = Bounds.Width;
        // Transparent fill so the empty part of a row takes pointer input too.
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        var foreground = TablesBDrawing.ColorOf(Foreground, Colors.Black);
        var secondary = TablesBDrawing.ColorOf(SecondaryForeground, Colors.Gray);
        var muted = TablesBDrawing.ColorOf(MutedForeground, Colors.Gray);
        var opacity = _highlightOpacity?.Value ?? 0;
        if (opacity > 0.001 && _highlight is not null)
        {
            var fillColor = Color.FromArgb(15, foreground.R, foreground.G, foreground.B);
            if (_fill is null || _fill.Color != fillColor)
            {
                _fill = new ImmutableSolidColorBrush(fillColor);
            }
            var rect = _highlight.Value;
            rect = new Rect(0, rect.Y, width, rect.Height);
            using (context.PushOpacity(opacity))
            {
                context.DrawRectangle(_fill, null, rect, _radius, _radius);
                if (_ring && FocusRingBrush is ISolidColorBrush ring)
                {
                    if (_ringPen is null || _ringColor != ring.Color)
                    {
                        _ringColor = ring.Color;
                        _ringPen = new ImmutablePen(new ImmutableSolidColorBrush(ring.Color), 2);
                    }
                    context.DrawRectangle(null, _ringPen, rect.Deflate(1), _radius - 1, _radius - 1);
                }
            }
        }
        for (var i = 0; i < _rows.Length; i++)
        {
            var row = _rows[i];
            var top = i * _rowHeight;
            var midY = top + _rowHeight / 2;
            var x = RowInset;
            if (_hasIcons)
            {
                if (row.Icon is { } icon)
                {
                    var pen = TablesBDrawing.GlyphPen(ref _iconPen, ref _iconColor, secondary, 1.8, IconExtent);
                    TablesBDrawing.DrawGlyph(context, icon, pen, new Point(x + IconExtent / 2, midY), IconExtent);
                }
                x += IconExtent + 10;
            }
            // Trailing meta: the field's current value and a chevron, or the value's hint and its check.
            var right = width - RowInset;
            var metaRight = right;
            {
                var glyphCenter = new Point(right - MetaGlyph / 2, midY);
                if (row.Drill)
                {
                    var pen = TablesBDrawing.GlyphPen(ref _metaPen, ref _metaColor, muted, 1.8, MetaGlyph);
                    TablesBDrawing.DrawGlyph(context, ArcIcons.ChevronRight, pen, glyphCenter, MetaGlyph);
                    metaRight = right - MetaGlyph - 6;
                }
                else if (row.Checked || row.Meta is not null)
                {
                    if (row.Checked)
                    {
                        var pen = TablesBDrawing.GlyphPen(ref _checkPen, ref _checkColor, foreground, 2, MetaGlyph);
                        TablesBDrawing.DrawGlyph(context, ArcIcons.Check, pen, glyphCenter, MetaGlyph);
                    }
                    metaRight = right - MetaGlyph - 6;
                }
            }
            var labelRoom = metaRight - x;
            if (MetaAt(i, (right - x) / 2) is { } meta)
            {
                var metaWidth = Math.Min(meta.WidthIncludingTrailingWhitespace, (right - x) / 2);
                meta.Draw(context, new Point(metaRight - metaWidth, midY - meta.Height / 2));
                labelRoom = metaRight - metaWidth - 10 - x;
            }
            var label = LabelAt(i, labelRoom);
            label.Draw(context, new Point(x, midY - label.Height / 2));
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new ListPeer(this);

    private sealed class ListPeer : ControlAutomationPeer
    {
        public ListPeer(FilterMenuList owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Menu;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
