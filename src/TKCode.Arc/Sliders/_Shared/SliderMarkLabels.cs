using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Rendering;

namespace TKCode.Arc;

/// <summary>What the mark label row asks its slider.</summary>
internal interface ISliderMarkHost
{
    IList<SliderMark> Marks { get; }

    double Minimum { get; }

    double Maximum { get; }

    /// <summary>Half a thumb: the track is inset by it on both sides.</summary>
    double MarkInset { get; }

    bool IsMarkOn(double value);

    /// <summary>A label was clicked (a pointer shortcut; the same values are reachable with keys).</summary>
    void ActivateMark(double value);
}

/// <summary>
/// The labels of the labeled marks, printed under the track and drawn in one <see cref="Render"/>. Each label centers
/// on its tick; one at a limit keeps its outer edge within half a thumb of the track end. Hidden from assistive
/// technology (the values are reachable with the keyboard).
/// </summary>
internal sealed class SliderMarkLabels : Control, ICustomHitTest
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<SliderMarkLabels>();
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<SliderMarkLabels>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<SliderMarkLabels>();

    public static readonly StyledProperty<IBrush?> OnForegroundProperty =
        AvaloniaProperty.Register<SliderMarkLabels, IBrush?>(nameof(OnForeground));

    public static readonly StyledProperty<IBrush?> HoverForegroundProperty =
        AvaloniaProperty.Register<SliderMarkLabels, IBrush?>(nameof(HoverForeground));

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public IBrush? OnForeground { get => GetValue(OnForegroundProperty); set => SetValue(OnForegroundProperty, value); }
    public IBrush? HoverForeground { get => GetValue(HoverForegroundProperty); set => SetValue(HoverForegroundProperty, value); }

    // Which of the three colors a label is drawn in.
    private const int RestSlot = 0;
    private const int OnSlot = 1;
    private const int HoverSlot = 2;

    /// <summary>
    /// One label: its text shaped once per color (TextLayout; a cached FormattedText re-shapes on every draw, and
    /// setting its brush re-shapes again), and the on-state it was last drawn with.
    /// </summary>
    private sealed class Label
    {
        public double Value;
        public string Text = string.Empty;
        public bool On;
        public readonly TextLayout?[] Layouts = new TextLayout?[3];
        public readonly IBrush?[] Brushes = new IBrush?[3];

        public void Dispose()
        {
            for (var i = 0; i < Layouts.Length; i++)
            {
                Layouts[i]?.Dispose();
                Layouts[i] = null;
                Brushes[i] = null;
            }
        }
    }

    private readonly List<Label> _labels = new();
    private ISliderMarkHost? _host;
    private bool _dirty = true;
    private int _hover = -1;
    private int _pressed = -1;

    static SliderMarkLabels()
    {
        AffectsRender<SliderMarkLabels>(ForegroundProperty, OnForegroundProperty, HoverForegroundProperty);
        Avalonia.Automation.AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<SliderMarkLabels>(Avalonia.Automation.AccessibilityView.Raw);
    }

    public ISliderMarkHost? Host
    {
        get => _host;
        set
        {
            _host = value;
            Refresh();
        }
    }

    /// <summary>Marks or fonts changed: rebuild the cached texts.</summary>
    public void Refresh()
    {
        _dirty = true;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>
    /// The value moved: redraws only when a label changed between its resting and on color (a drag used to redraw
    /// every label on every value change).
    /// </summary>
    public void SyncMarkStates()
    {
        EnsureLabels();
        if (_host is null)
        {
            return;
        }
        var changed = false;
        for (var i = 0; i < _labels.Count; i++)
        {
            var label = _labels[i];
            var on = _host.IsMarkOn(label.Value);
            if (on != label.On)
            {
                label.On = on;
                changed = true;
            }
        }
        if (changed)
        {
            InvalidateVisual();
        }
    }

    public bool HasLabels
    {
        get
        {
            EnsureLabels();
            return _labels.Count > 0;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty)
        {
            Refresh();
        }
    }

    private void EnsureLabels()
    {
        if (!_dirty)
        {
            return;
        }
        _dirty = false;
        foreach (var label in _labels)
        {
            label.Dispose();
        }
        _labels.Clear();
        if (_host is null)
        {
            return;
        }
        foreach (var mark in _host.Marks)
        {
            if (string.IsNullOrEmpty(mark.Label) || mark.Value < _host.Minimum || mark.Value > _host.Maximum)
            {
                continue;
            }
            _labels.Add(new Label { Value = mark.Value, Text = mark.Label, On = _host.IsMarkOn(mark.Value) });
        }
    }

    private TextLayout Layout(Label label, int slot, IBrush? brush)
    {
        var layout = label.Layouts[slot];
        if (layout is null || !ReferenceEquals(label.Brushes[slot], brush))
        {
            layout?.Dispose();
            layout = new TextLayout(label.Text, new Typeface(FontFamily), FontSize, brush);
            label.Layouts[slot] = layout;
            label.Brushes[slot] = brush;
        }
        return layout;
    }

    /// <summary>Any shaped copy of the label (all colors share the metrics); the resting one is built when there is none.</summary>
    private TextLayout Metrics(Label label)
    {
        for (var i = 0; i < label.Layouts.Length; i++)
        {
            if (label.Layouts[i] is { } layout)
            {
                return layout;
            }
        }
        return Layout(label, RestSlot, Foreground);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureLabels();
        return new Size(0, 0);
    }

    private Rect LabelRect(int index)
    {
        var host = _host!;
        var label = _labels[index];
        var text = Metrics(label);
        var inset = host.MarkInset;
        var track = Math.Max(0, Bounds.Width - 2 * inset);
        var span = host.Maximum - host.Minimum;
        var x = inset + (span <= 0 ? 0 : (label.Value - host.Minimum) / span) * track;
        var width = text.Width;
        var left = x - width / 2;
        if (label.Value <= host.Minimum)
        {
            left = x + Math.Max(-width / 2, -inset);
        }
        else if (label.Value >= host.Maximum)
        {
            left = x + Math.Min(-width / 2, -width + inset);
        }
        return new Rect(left, 0, width, Math.Max(text.Height, Bounds.Height));
    }

    public override void Render(DrawingContext context)
    {
        EnsureLabels();
        if (_host is null)
        {
            return;
        }
        for (var i = 0; i < _labels.Count; i++)
        {
            var label = _labels[i];
            var on = _host.IsMarkOn(label.Value);
            label.On = on;
            var slot = i == _hover ? HoverSlot : on ? OnSlot : RestSlot;
            var brush = (slot == HoverSlot ? HoverForeground : slot == OnSlot ? OnForeground : Foreground) ?? Foreground;
            var text = brush is null ? Metrics(label) : Layout(label, slot, brush);
            text.Draw(context, LabelRect(i).TopLeft);
        }
    }

    public bool HitTest(Point point) => HitLabel(point) >= 0;

    private static Cursor? s_hand;

    private int HitLabel(Point point)
    {
        EnsureLabels();
        if (_host is null || !IsEffectivelyEnabled)
        {
            return -1;
        }
        for (var i = 0; i < _labels.Count; i++)
        {
            if (LabelRect(i).Contains(point))
            {
                return i;
            }
        }
        return -1;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var hit = e.Pointer.Type == PointerType.Touch ? -1 : HitLabel(e.GetPosition(this));
        if (hit != _hover)
        {
            _hover = hit;
            Cursor = hit >= 0 ? s_hand ??= new Cursor(StandardCursorType.Hand) : null;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hover >= 0)
        {
            _hover = -1;
            Cursor = null;
            InvalidateVisual();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = HitLabel(e.GetPosition(this));
            if (_pressed >= 0)
            {
                e.Handled = true;
            }
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var pressed = _pressed;
        _pressed = -1;
        if (pressed >= 0 && HitLabel(e.GetPosition(this)) == pressed && _host is not null)
        {
            _host.ActivateMark(_labels[pressed].Value);
            e.Handled = true;
        }
    }
}
