using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The week of a <see cref="SmartScheduler"/>, drawn in one pass: day columns and half-hour rows, each cell shaded by how
/// many invited people are busy then, and the proposed slots as numbered blocks. A ring glides to the selected slot on a
/// spring. Clicking a slot or the arrow keys select one (<see cref="SelectionRequested"/>). Busy shading is computed when
/// the data changes and text layouts are cached, so a frame of the glide allocates nothing.
/// </summary>
internal sealed class SmartSchedulerWeek : Control
{
    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<SmartSchedulerWeek, IBrush?>(nameof(GridBrush));

    public static readonly StyledProperty<IBrush?> TextBrushProperty =
        AvaloniaProperty.Register<SmartSchedulerWeek, IBrush?>(nameof(TextBrush));

    public static readonly StyledProperty<IBrush?> BusyBrushProperty =
        AvaloniaProperty.Register<SmartSchedulerWeek, IBrush?>(nameof(BusyBrush));

    public static readonly StyledProperty<IBrush?> SlotBrushProperty =
        AvaloniaProperty.Register<SmartSchedulerWeek, IBrush?>(nameof(SlotBrush));

    public static readonly StyledProperty<IBrush?> SlotTextBrushProperty =
        AvaloniaProperty.Register<SmartSchedulerWeek, IBrush?>(nameof(SlotTextBrush));

    public static readonly StyledProperty<IBrush?> SelectedBrushProperty =
        AvaloniaProperty.Register<SmartSchedulerWeek, IBrush?>(nameof(SelectedBrush));

    public static readonly StyledProperty<IBrush?> SelectedTextBrushProperty =
        AvaloniaProperty.Register<SmartSchedulerWeek, IBrush?>(nameof(SelectedTextBrush));

    public static readonly StyledProperty<IBrush?> FocusBrushProperty =
        AvaloniaProperty.Register<SmartSchedulerWeek, IBrush?>(nameof(FocusBrush));

    public static readonly StyledProperty<CornerRadius> SlotRadiusProperty =
        AvaloniaProperty.Register<SmartSchedulerWeek, CornerRadius>(nameof(SlotRadius), new CornerRadius(8));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<SmartSchedulerWeek>();

    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<SmartSchedulerWeek>();

    public const double HeaderHeight = 30;
    public const double GutterWidth = 48;

    private readonly List<TextLayout> _dayText = new();
    private readonly List<TextLayout> _hourText = new();
    private readonly List<TextLayout> _slotText = new();
    private double[] _density = [];
    private int _rowsPerDay;
    private int _days = 5;
    private double _dayStart = 9;
    private double _dayEnd = 18;
    private IReadOnlyList<string> _dayLabels = [];
    private Func<int, string> _hourLabel = static h => h.ToString(System.Globalization.CultureInfo.CurrentCulture);
    private IReadOnlyList<SmartSchedulerSlot> _slots = [];
    private int _selected = -1;
    private bool _textDirty = true;
    private Pen? _gridPen;
    private Pen? _selectedPen;
    private Pen? _focusPen;
    private MotionValue<Rect>? _ring;
    private Rect _ringRect;

    static SmartSchedulerWeek()
    {
        FocusableProperty.OverrideDefaultValue<SmartSchedulerWeek>(true);
        AffectsRender<SmartSchedulerWeek>(GridBrushProperty, TextBrushProperty, BusyBrushProperty, SlotBrushProperty,
            SlotTextBrushProperty, SelectedBrushProperty, SelectedTextBrushProperty, FocusBrushProperty, SlotRadiusProperty);
    }

    public IBrush? GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }

    public IBrush? TextBrush { get => GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }

    public IBrush? BusyBrush { get => GetValue(BusyBrushProperty); set => SetValue(BusyBrushProperty, value); }

    public IBrush? SlotBrush { get => GetValue(SlotBrushProperty); set => SetValue(SlotBrushProperty, value); }

    public IBrush? SlotTextBrush { get => GetValue(SlotTextBrushProperty); set => SetValue(SlotTextBrushProperty, value); }

    public IBrush? SelectedBrush { get => GetValue(SelectedBrushProperty); set => SetValue(SelectedBrushProperty, value); }

    public IBrush? SelectedTextBrush { get => GetValue(SelectedTextBrushProperty); set => SetValue(SelectedTextBrushProperty, value); }

    public IBrush? FocusBrush { get => GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }

    public CornerRadius SlotRadius { get => GetValue(SlotRadiusProperty); set => SetValue(SlotRadiusProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>Raised with the index of the slot the person picked.</summary>
    public event Action<int>? SelectionRequested;

    public int SelectedIndex => _selected;

    internal Rect RingRect => _ringRect;

    /// <summary>Sets the week's shape and the invited people's busy times.</summary>
    public void SetWeek(int days, double dayStart, double dayEnd, IReadOnlyList<string> dayLabels, Func<int, string> hourLabel,
        IReadOnlyList<SmartSchedulerPerson> people)
    {
        _days = Math.Max(1, days);
        _dayStart = dayStart;
        _dayEnd = Math.Max(dayStart + 1, dayEnd);
        _dayLabels = dayLabels;
        _hourLabel = hourLabel;
        _rowsPerDay = (int)Math.Round((_dayEnd - _dayStart) * 2);
        _density = new double[_days * _rowsPerDay];
        var count = Math.Max(1, people.Count);
        for (var d = 0; d < _days; d++)
        {
            for (var r = 0; r < _rowsPerDay; r++)
            {
                var start = _dayStart + r * 0.5;
                var busy = 0;
                foreach (var person in people)
                {
                    foreach (var b in person.Busy)
                    {
                        if (b.Day == d && b.Start < start + 0.5 - 1e-9 && b.End > start + 1e-9)
                        {
                            busy++;
                            break;
                        }
                    }
                }
                _density[d * _rowsPerDay + r] = (double)busy / count;
            }
        }
        _textDirty = true;
        InvalidateVisual();
    }

    /// <summary>Shows <paramref name="slots"/> and selects <paramref name="selected"/> (−1 for none).</summary>
    public void SetSlots(IReadOnlyList<SmartSchedulerSlot> slots, int selected)
    {
        _slots = slots;
        _textDirty = true;
        Select(selected, false);
        InvalidateVisual();
    }

    /// <summary>Moves the selection ring to <paramref name="index"/>, gliding when <paramref name="animate"/>.</summary>
    public void Select(int index, bool animate)
    {
        _selected = index >= 0 && index < _slots.Count ? index : -1;
        var target = _selected >= 0 ? SlotRect(_slots[_selected]) : default;
        if (!animate || _ringRect == default || target == default)
        {
            _ring?.Jump(target);
            _ringRect = target;
            InvalidateVisual();
            return;
        }
        _ring ??= CreateRing();
        _ = _ring.AnimateTo(target, MotionSpec.Spring(ArcSpring.Snappy));
    }

    private MotionValue<Rect> CreateRing()
    {
        var ring = new MotionValue<Rect>(this, _ringRect, MotionVectors.Rect);
        ring.Changed += rect =>
        {
            _ringRect = rect;
            InvalidateVisual();
        };
        return ring;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GridBrushProperty)
        {
            _gridPen = null;
        }
        else if (change.Property == SelectedBrushProperty)
        {
            _selectedPen = null;
        }
        else if (change.Property == FocusBrushProperty)
        {
            _focusPen = null;
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty || change.Property == TextBrushProperty
                 || change.Property == SlotTextBrushProperty || change.Property == SelectedTextBrushProperty)
        {
            _textDirty = true;
            InvalidateVisual();
        }
        else if (change.Property == BoundsProperty)
        {
            // Keep the ring on its slot when the week resizes.
            if (_selected >= 0)
            {
                Select(_selected, false);
            }
        }
    }

    private double ColumnWidth => Math.Max(0, (Bounds.Width - GutterWidth) / _days);

    private double RowHeight => _rowsPerDay == 0 ? 0 : Math.Max(0, (Bounds.Height - HeaderHeight) / _rowsPerDay);

    private Rect SlotRect(SmartSchedulerSlot slot)
    {
        var x = GutterWidth + slot.Day * ColumnWidth + 3;
        var y = HeaderHeight + (slot.Start - _dayStart) * 2 * RowHeight + 1;
        return new Rect(x, y, Math.Max(0, ColumnWidth - 6), Math.Max(0, (slot.End - slot.Start) * 2 * RowHeight - 2));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetPosition(this);
        for (var i = 0; i < _slots.Count; i++)
        {
            if (SlotRect(_slots[i]).Inflate(2).Contains(point))
            {
                SelectionRequested?.Invoke(i);
                e.Handled = true;
                return;
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_slots.Count == 0 || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var current = _selected < 0 ? 0 : _selected;
        int? next = e.Key switch
        {
            Key.Right or Key.Down => (current + 1) % _slots.Count,
            Key.Left or Key.Up => (current - 1 + _slots.Count) % _slots.Count,
            Key.Home => 0,
            Key.End => _slots.Count - 1,
            _ => null,
        };
        if (next is { } index)
        {
            SelectionRequested?.Invoke(index);
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

    private void BuildText()
    {
        foreach (var layout in _dayText.Concat(_hourText).Concat(_slotText))
        {
            layout.Dispose();
        }
        _dayText.Clear();
        _hourText.Clear();
        _slotText.Clear();
        var typeface = new Typeface(FontFamily);
        var bold = new Typeface(FontFamily, weight: FontWeight.Medium);
        var size = FontSize > 0 ? FontSize : 12;
        for (var d = 0; d < _days; d++)
        {
            _dayText.Add(new TextLayout(d < _dayLabels.Count ? _dayLabels[d] : "", typeface, size, TextBrush,
                TextAlignment.Center, maxWidth: Math.Max(1, ColumnWidth)));
        }
        for (var h = (int)Math.Ceiling(_dayStart); h < _dayEnd; h++)
        {
            _hourText.Add(new TextLayout(_hourLabel(h), typeface, size - 1, TextBrush));
        }
        for (var i = 0; i < _slots.Count; i++)
        {
            _slotText.Add(new TextLayout((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), bold, size,
                i == _selected ? SelectedTextBrush : SlotTextBrush));
        }
        _textDirty = false;
    }

    private int _textSelected = -2;
    private double _textWidth = -1;

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Brushes.Transparent, bounds);
        if (_rowsPerDay == 0 || bounds.Width <= GutterWidth)
        {
            return;
        }
        if (_textDirty || _textSelected != _selected || Math.Abs(_textWidth - bounds.Width) > 0.5)
        {
            BuildText();
            _textSelected = _selected;
            _textWidth = bounds.Width;
        }
        var column = ColumnWidth;
        var row = RowHeight;
        _gridPen ??= new Pen(GridBrush, 1);

        for (var d = 0; d < _days; d++)
        {
            var x = GutterWidth + d * column;
            _dayText[d].Draw(context, new Point(x, (HeaderHeight - _dayText[d].Height) / 2));
            if (BusyBrush is { } busy)
            {
                for (var r = 0; r < _rowsPerDay; r++)
                {
                    var density = _density[d * _rowsPerDay + r];
                    if (density <= 0)
                    {
                        continue;
                    }
                    using (context.PushOpacity(0.25 + density * 0.6))
                    {
                        context.FillRectangle(busy, new Rect(x + 1, HeaderHeight + r * row, column - 2, row));
                    }
                }
            }
            context.DrawLine(_gridPen, new Point(Math.Round(x) + 0.5, HeaderHeight), new Point(Math.Round(x) + 0.5, bounds.Height));
        }
        var hourIndex = 0;
        for (var h = (int)Math.Ceiling(_dayStart); h < _dayEnd; h++)
        {
            var y = Math.Round(HeaderHeight + (h - _dayStart) * 2 * row) + 0.5;
            context.DrawLine(_gridPen, new Point(GutterWidth, y), new Point(bounds.Width, y));
            var label = _hourText[hourIndex++];
            label.Draw(context, new Point(GutterWidth - 8 - label.Width, y - label.Height / 2 + (h == (int)Math.Ceiling(_dayStart) ? label.Height / 2 : 0)));
        }

        var radius = SlotRadius.TopLeft;
        for (var i = 0; i < _slots.Count; i++)
        {
            var rect = SlotRect(_slots[i]);
            var r = Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2);
            context.DrawRectangle(i == _selected ? SelectedBrush : SlotBrush, null, rect, r, r);
            var text = _slotText[i];
            if (rect.Height >= text.Height)
            {
                text.Draw(context, new Point(rect.X + 6, rect.Y + Math.Min(4, (rect.Height - text.Height) / 2)));
            }
        }
        if (_ringRect.Width > 0)
        {
            _selectedPen ??= new Pen(SelectedBrush, 2);
            var ring = _ringRect.Inflate(3);
            var r = Math.Min(radius + 3, Math.Min(ring.Width, ring.Height) / 2);
            context.DrawRectangle(null, _selectedPen, ring, r, r);
        }
        if (Classes.Contains(":focus-visible") && FocusBrush is not null)
        {
            _focusPen ??= new Pen(FocusBrush, 2);
            context.DrawRectangle(null, _focusPen, bounds.Inflate(3), radius, radius);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SmartSchedulerWeekPeer(this);

    private sealed class SmartSchedulerWeekPeer(SmartSchedulerWeek owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        protected override string? GetHelpTextCore() => AutomationProperties.GetHelpText(Owner);
    }
}
