using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The metric tab strip: one pill per metric (its short name and, when there is room, its current value) and a highlight
/// that glides between them on a Smooth spring (position and width together). One tab stop, the selected tab; ← / → move
/// and select at once (wrapping), Home / End jump to the ends. Under 640 the values go; in the compact layout the tabs share
/// the row equally and centre their names.
/// </summary>
/// <remarks>
/// Code-driven motion: the highlight changes position and width together and a second switch mid-glide keeps the velocity,
/// which a ControlTheme transition on a child element cannot do (the tabs are drawn, not elements).
/// </remarks>
internal sealed class MetricExplorerTabs : Control
{
    private const double TabHeight = 32;
    private const double Gap = 2;
    private const double PadX = 12;
    private const double InnerGap = 8;

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(128);
    private readonly LiveLoop _loop;
    private string[] _names = [];
    private string[] _values = [];
    private Rect[] _rects = [];
    private AnalyticsTween[] _selected = [];
    private AnalyticsTween[] _hover = [];
    private AnalyticsSpringChannel _x = new(0, 0.01, 0.05);
    private AnalyticsSpringChannel _w = new(0, 0.01, 0.05);
    private int _index;
    private int _hot = -1;
    private bool _showValues = true;
    private bool _stretched;
    private bool _placed;
    private int _paletteVersion = -1;
    private double _layoutWidth = -1;
    private bool _valuesFit = true;

    static MetricExplorerTabs()
    {
        FocusableProperty.OverrideDefaultValue<MetricExplorerTabs>(true);
    }

    public MetricExplorerTabs()
    {
        // Function, not decoration: the loop also lands the highlight after the motion level drops.
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        ActualThemeVariantChanged += (_, _) => InvalidatePalette();
        ResourcesChanged += (_, _) => InvalidatePalette();
    }

    /// <summary>Raised when a tab is chosen by the person: the new index.</summary>
    public event Action<int>? SelectionChanged;

    /// <summary>The selected tab.</summary>
    public int SelectedIndex => _index;

    /// <summary>True while the highlight moves (tests).</summary>
    internal bool IsMoving => _loop.IsActive;

    /// <summary>The highlight's rectangle now (tests).</summary>
    internal Rect Highlight => new(_x.Value, 0, _w.Value, TabHeight);

    /// <summary>The tabs' rectangles at the current width (tests).</summary>
    internal IReadOnlyList<Rect> TabRects => _rects;

    /// <summary>Whether the values are drawn (tests).</summary>
    internal bool ValuesShown => _showValues && _valuesFit;

    /// <summary>Tabs share the row equally (compact layout).</summary>
    public bool IsStretched
    {
        get => _stretched;
        set
        {
            if (_stretched != value)
            {
                _stretched = value;
                _layoutWidth = -1;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }
    }

    /// <summary>Whether tabs show their values (off under 640).</summary>
    public bool ShowValues
    {
        get => _showValues;
        set
        {
            if (_showValues != value)
            {
                _showValues = value;
                _layoutWidth = -1;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }
    }

    /// <summary>Sets the tabs (names and values, same length) and the selected one.</summary>
    public void SetItems(string[] names, string[] values, int selected, string automationName)
    {
        _names = names;
        _values = values;
        _selected = new AnalyticsTween[names.Length];
        _hover = new AnalyticsTween[names.Length];
        _index = names.Length == 0 ? 0 : Math.Clamp(selected, 0, names.Length - 1);
        for (var i = 0; i < names.Length; i++)
        {
            _selected[i] = new AnalyticsTween(i == _index ? 1 : 0);
        }
        _layoutWidth = -1;
        AutomationProperties.SetName(this, automationName);
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Updates the values shown beside the names (a metric's latest value).</summary>
    public void SetValues(string[] values)
    {
        _values = values;
        _layoutWidth = -1;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Selects a tab (no event); the highlight glides there at Full motion.</summary>
    public void Select(int index)
    {
        if (_names.Length == 0)
        {
            return;
        }
        index = Math.Clamp(index, 0, _names.Length - 1);
        if (index == _index)
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        var tween = level == ArcMotionLevel.Full && this.IsAttachedToVisualTree();
        for (var i = 0; i < _selected.Length; i++)
        {
            var target = i == index ? 1.0 : 0.0;
            if (tween)
            {
                _selected[i].Start(target, ArcMotionPresets.Fast, AnalyticsEasings.Standard);
            }
            else
            {
                _selected[i].Jump(target);
            }
        }
        _index = index;
        Layout(Bounds.Width);
        MoveHighlight(tween);
        AutomationProperties.SetItemStatus(this, _names[index]);
        InvalidateVisual();
    }

    #region Layout

    private void EnsurePalette()
    {
        if (!this.IsAttachedToVisualTree() || (_paletteVersion >= 0 && _palette.IsReady))
        {
            return;
        }
        _palette.Refresh(this);
        _paletteVersion = _palette.Version;
    }

    private void InvalidatePalette()
    {
        _paletteVersion = -1;
        _text.Clear();
        _layoutWidth = -1;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private (Typeface Name, Typeface Value) Faces()
    {
        var family = _palette.IsReady ? _palette.BodyFont : FontFamily.Default;
        var weight = _palette.IsReady ? _palette.Medium : FontWeight.Medium;
        return (new Typeface(family, FontStyle.Normal, weight), new Typeface(family));
    }

    private double NameWidth(int i) => _text.Get(_names[i], Faces().Name, 14).WidthIncludingTrailingWhitespace;

    private double ValueWidth(int i) => _text.Get(_values[i], Faces().Value, 14, true).WidthIncludingTrailingWhitespace;

    private double NaturalWidth(bool values)
    {
        var total = 0.0;
        for (var i = 0; i < _names.Length; i++)
        {
            total += (_stretched ? 16 : 2 * PadX) + NameWidth(i) + (values ? InnerGap + ValueWidth(i) : 0);
        }
        return total + Math.Max(0, _names.Length - 1) * Gap;
    }

    private void Layout(double width)
    {
        EnsurePalette();
        var count = _names.Length;
        if (_rects.Length != count)
        {
            _rects = new Rect[count];
        }
        if (count == 0)
        {
            return;
        }
        var showValues = _showValues && _values.Length == count && !_stretched;
        _valuesFit = !showValues || NaturalWidth(true) <= width + 0.5 || double.IsInfinity(width);
        var values = showValues && _valuesFit;
        var x = 0.0;
        var natural = NaturalWidth(values);
        var spare = _stretched && !double.IsInfinity(width) ? Math.Max(0, width - natural) / count : 0;
        for (var i = 0; i < count; i++)
        {
            var w = (_stretched ? 16 : 2 * PadX) + NameWidth(i) + (values ? InnerGap + ValueWidth(i) : 0) + spare;
            _rects[i] = new Rect(x, 0, w, TabHeight);
            x += w + Gap;
        }
        _layoutWidth = width;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsurePalette();
        Layout(availableSize.Width);
        var width = _rects.Length == 0 ? 0 : _rects[^1].Right;
        return new Size(_stretched && !double.IsInfinity(availableSize.Width) ? availableSize.Width : width, _stretched ? 32.8 : TabHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Math.Abs(_layoutWidth - finalSize.Width) > 0.01)
        {
            Layout(finalSize.Width);
            MoveHighlight(false);
        }
        else if (!_placed)
        {
            MoveHighlight(false);
        }
        return base.ArrangeOverride(finalSize);
    }

    private void MoveHighlight(bool animate)
    {
        if (_rects.Length == 0)
        {
            return;
        }
        var rect = _rects[Math.Clamp(_index, 0, _rects.Length - 1)];
        if (animate && _placed)
        {
            _x.SpringTo(rect.X, AnalyticsSprings.Smooth);
            _w.SpringTo(rect.Width, AnalyticsSprings.Smooth);
            _loop.Start();
        }
        else
        {
            _x.Jump(rect.X);
            _w.Jump(rect.Width);
        }
        _placed = true;
    }

    private bool Advance(double dt)
    {
        var full = ArcMotion.IsFull(this);
        var moving = false;
        if (full)
        {
            _x.Step(dt);
            _w.Step(dt);
        }
        else
        {
            _x.Jump(_x.Target);
            _w.Jump(_w.Target);
        }
        moving |= _x.IsMoving || _w.IsMoving;
        for (var i = 0; i < _selected.Length; i++)
        {
            moving |= _selected[i].Advance(dt);
            moving |= _hover[i].Advance(dt);
        }
        InvalidateVisual();
        return moving;
    }

    #endregion

    #region Input

    private int HitIndex(Point p)
    {
        for (var i = 0; i < _rects.Length; i++)
        {
            if (_rects[i].Contains(p) || p.X >= _rects[i].X && p.X < _rects[i].Right + Gap && p.Y >= 0 && p.Y <= TabHeight)
            {
                return i;
            }
        }
        return -1;
    }

    private void SetHot(int index)
    {
        if (index == _hot)
        {
            return;
        }
        var tween = ArcMotion.IsFull(this) && this.IsAttachedToVisualTree();
        if (_hot >= 0 && _hot < _hover.Length)
        {
            Fade(_hot, 0, tween);
        }
        _hot = index;
        if (index >= 0 && index < _hover.Length)
        {
            Fade(index, 1, tween);
        }
        InvalidateVisual();
    }

    private void Fade(int i, double target, bool tween)
    {
        if (tween)
        {
            _hover[i].Start(target, ArcMotionPresets.Fast, AnalyticsEasings.Standard);
            _loop.Start();
        }
        else
        {
            _hover[i].Jump(target);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        SetHot(e.Pointer.Type == PointerType.Touch ? -1 : HitIndex(e.GetPosition(this)));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHot(-1);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        Focus(NavigationMethod.Pointer);
        var i = HitIndex(e.GetPosition(this));
        if (i >= 0)
        {
            Choose(i);
            e.Handled = true;
        }
    }

    private void Choose(int index)
    {
        if (index == _index || index < 0 || index >= _names.Length)
        {
            return;
        }
        Select(index);
        SelectionChanged?.Invoke(index);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || _names.Length == 0)
        {
            return;
        }
        int? next = e.Key switch
        {
            Key.Right or Key.Down => (_index + 1) % _names.Length,
            Key.Left or Key.Up => (_index - 1 + _names.Length) % _names.Length,
            Key.Home => 0,
            Key.End => _names.Length - 1,
            _ => null
        };
        if (next is { } n)
        {
            Choose(n);
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TabsPeer(this);

    private sealed class TabsPeer(MetricExplorerTabs owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        protected override string? GetHelpTextCore() => owner._names.Length == 0 ? null : $"{owner._names[owner._index]} {owner._values.ElementAtOrDefault(owner._index)}";
    }

    #endregion

    #region Render

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        EnsurePalette();
        if (!_palette.IsReady || _names.Length == 0)
        {
            return;
        }
        if (_rects.Length != _names.Length || Math.Abs(_layoutWidth - size.Width) > 0.01)
        {
            Layout(size.Width);
        }
        var (nameFace, valueFace) = Faces();
        var top = (size.Height - TabHeight) / 2;
        var highlight = new Rect(_x.Value, top, _w.Value, TabHeight);
        context.DrawRectangle(_palette.Brush(_palette.SurfaceMuted), null, new RoundedRect(highlight, TabHeight / 2));
        var showValues = _showValues && _valuesFit && _values.Length == _names.Length && !_stretched;
        for (var i = 0; i < _rects.Length; i++)
        {
            var rect = _rects[i];
            var emphasis = Math.Max(Math.Clamp(_selected[i].Value, 0, 1), Math.Clamp(_hover[i].Value, 0, 1));
            var nameColor = AnalyticsColors.Lerp(_palette.TextMuted, _palette.Foreground, emphasis);
            var name = _text.Get(_names[i], nameFace, 14);
            var contentWidth = name.WidthIncludingTrailingWhitespace + (showValues ? InnerGap + ValueWidth(i) : 0);
            var x = rect.X + (rect.Width - contentWidth) / 2;
            name.Draw(context, _palette.Brush(nameColor), new Point(x, top + (TabHeight - name.Height) / 2));
            if (showValues)
            {
                var value = _text.Get(_values[i], valueFace, 14, true);
                var valueColor = AnalyticsColors.Lerp(_palette.TextMuted, _palette.TextSecondary, Math.Clamp(_selected[i].Value, 0, 1));
                value.Draw(context, _palette.Brush(valueColor), new Point(x + name.WidthIncludingTrailingWhitespace + InnerGap, top + (TabHeight - value.Height) / 2));
            }
        }
        if (AnalyticsFocus.IsVisible(this))
        {
            var rect = _rects[Math.Clamp(_index, 0, _rects.Length - 1)];
            AnalyticsFocus.Draw(context, _palette, new Rect(rect.X, top, rect.Width, TabHeight), TabHeight / 2);
        }
    }

    #endregion
}
