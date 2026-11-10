using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The map itself, drawn in one control (hundreds of seats, no child per seat): the stage, section names and every seat in
/// its tier's color, taken seats hollow, chosen seats in the accent with a check, a ring under the pointer, the keyboard
/// cursor with the focus ring, and a readout for the seat under the pointer or cursor. The block owns the state and passes
/// it in; the canvas reports presses through <see cref="SeatInvoked"/>.
/// </summary>
internal sealed class SeatMapCanvas : Control
{
    public static readonly StyledProperty<IBrush?> Tier1BrushProperty = AvaloniaProperty.Register<SeatMapCanvas, IBrush?>(nameof(Tier1Brush));
    public static readonly StyledProperty<IBrush?> Tier2BrushProperty = AvaloniaProperty.Register<SeatMapCanvas, IBrush?>(nameof(Tier2Brush));
    public static readonly StyledProperty<IBrush?> Tier3BrushProperty = AvaloniaProperty.Register<SeatMapCanvas, IBrush?>(nameof(Tier3Brush));
    public static readonly StyledProperty<IBrush?> Tier4BrushProperty = AvaloniaProperty.Register<SeatMapCanvas, IBrush?>(nameof(Tier4Brush));
    public static readonly StyledProperty<IBrush?> TakenBrushProperty = AvaloniaProperty.Register<SeatMapCanvas, IBrush?>(nameof(TakenBrush));
    public static readonly StyledProperty<IBrush?> ChosenBrushProperty = AvaloniaProperty.Register<SeatMapCanvas, IBrush?>(nameof(ChosenBrush));
    public static readonly StyledProperty<IBrush?> ChosenForegroundProperty = AvaloniaProperty.Register<SeatMapCanvas, IBrush?>(nameof(ChosenForeground));
    public static readonly StyledProperty<IBrush?> StageBrushProperty = AvaloniaProperty.Register<SeatMapCanvas, IBrush?>(nameof(StageBrush));
    public static readonly StyledProperty<IBrush?> TextBrushProperty = AvaloniaProperty.Register<SeatMapCanvas, IBrush?>(nameof(TextBrush));
    public static readonly StyledProperty<IBrush?> MutedBrushProperty = AvaloniaProperty.Register<SeatMapCanvas, IBrush?>(nameof(MutedBrush));
    public static readonly StyledProperty<IBrush?> FocusBrushProperty = AvaloniaProperty.Register<SeatMapCanvas, IBrush?>(nameof(FocusBrush));
    public static readonly StyledProperty<IBrush?> ReadoutBrushProperty = AvaloniaProperty.Register<SeatMapCanvas, IBrush?>(nameof(ReadoutBrush));
    public static readonly StyledProperty<IBrush?> ReadoutBorderBrushProperty = AvaloniaProperty.Register<SeatMapCanvas, IBrush?>(nameof(ReadoutBorderBrush));
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty = AvaloniaProperty.Register<SeatMapCanvas, CornerRadius>(nameof(CornerRadius));
    public static readonly StyledProperty<double> LabelFontSizeProperty = AvaloniaProperty.Register<SeatMapCanvas, double>(nameof(LabelFontSize), 12);
    public static readonly StyledProperty<FontFamily?> LabelFontFamilyProperty = AvaloniaProperty.Register<SeatMapCanvas, FontFamily?>(nameof(LabelFontFamily));

    private const double StageBand = 46;
    private const double SectionLabelBand = 20;

    private IReadOnlyList<SeatMapSeat> _seats = [];
    private bool[] _taken = [];
    private bool[] _chosen = [];
    private Rect _world = new(0, 0, 1, 1);
    private double _scale = 1;
    private Point _offset;
    private int _hover = -1;
    private int _cursor = -1;
    private string _stageText = "Stage";
    private Func<SeatMapSeat, string>? _describe;
    private FormattedText? _stageLabel;
    private readonly Dictionary<SeatMapSection, FormattedText> _sectionLabels = new();
    private (int Index, FormattedText Text)? _readout;
    private ImmutablePen? _takenPen;
    private ImmutablePen? _hoverPen;
    private ImmutablePen? _focusPen;
    private ImmutablePen? _checkPen;
    private ImmutablePen? _readoutPen;

    static SeatMapCanvas()
    {
        FocusableProperty.OverrideDefaultValue<SeatMapCanvas>(true);
        AffectsRender<SeatMapCanvas>(Tier1BrushProperty, Tier2BrushProperty, Tier3BrushProperty, Tier4BrushProperty, TakenBrushProperty,
            ChosenBrushProperty, ChosenForegroundProperty, StageBrushProperty, TextBrushProperty, MutedBrushProperty, FocusBrushProperty,
            ReadoutBrushProperty, CornerRadiusProperty);
    }

    public IBrush? Tier1Brush { get => GetValue(Tier1BrushProperty); set => SetValue(Tier1BrushProperty, value); }
    public IBrush? Tier2Brush { get => GetValue(Tier2BrushProperty); set => SetValue(Tier2BrushProperty, value); }
    public IBrush? Tier3Brush { get => GetValue(Tier3BrushProperty); set => SetValue(Tier3BrushProperty, value); }
    public IBrush? Tier4Brush { get => GetValue(Tier4BrushProperty); set => SetValue(Tier4BrushProperty, value); }
    public IBrush? TakenBrush { get => GetValue(TakenBrushProperty); set => SetValue(TakenBrushProperty, value); }
    public IBrush? ChosenBrush { get => GetValue(ChosenBrushProperty); set => SetValue(ChosenBrushProperty, value); }
    public IBrush? ChosenForeground { get => GetValue(ChosenForegroundProperty); set => SetValue(ChosenForegroundProperty, value); }
    public IBrush? StageBrush { get => GetValue(StageBrushProperty); set => SetValue(StageBrushProperty, value); }
    public IBrush? TextBrush { get => GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }
    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }
    public IBrush? FocusBrush { get => GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }
    public IBrush? ReadoutBrush { get => GetValue(ReadoutBrushProperty); set => SetValue(ReadoutBrushProperty, value); }
    public IBrush? ReadoutBorderBrush { get => GetValue(ReadoutBorderBrushProperty); set => SetValue(ReadoutBorderBrushProperty, value); }
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public double LabelFontSize { get => GetValue(LabelFontSizeProperty); set => SetValue(LabelFontSizeProperty, value); }
    public FontFamily? LabelFontFamily { get => GetValue(LabelFontFamilyProperty); set => SetValue(LabelFontFamilyProperty, value); }

    /// <summary>Raised with a seat's index when it is pressed or chosen with Enter or Space.</summary>
    public event Action<int>? SeatInvoked;

    /// <summary>Raised when the keyboard cursor moves (the block updates the accessible name).</summary>
    public event Action<int>? CursorMoved;

    /// <summary>The seat under the keyboard cursor, or −1.</summary>
    public int CursorIndex => _cursor;

    /// <summary>The seat under the pointer, or −1.</summary>
    public int HoverIndex => _hover;

    /// <summary>Replaces the seats and their states.</summary>
    public void SetSeats(IReadOnlyList<SeatMapSeat> seats, Func<SeatMapSeat, string> describe, string stage)
    {
        _seats = seats;
        _describe = describe;
        _stageText = stage;
        _taken = new bool[seats.Count];
        _chosen = new bool[seats.Count];
        _world = SeatMapLayout.Bounds(seats);
        _cursor = seats.Count > 0 ? Math.Clamp(_cursor, 0, seats.Count - 1) : -1;
        _hover = -1;
        _readout = null;
        _stageLabel = null;
        _sectionLabels.Clear();
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Updates the taken and chosen flags.</summary>
    public void SetStates(Func<int, bool> taken, Func<int, bool> chosen)
    {
        for (var i = 0; i < _seats.Count; i++)
        {
            _taken[i] = taken(i);
            _chosen[i] = chosen(i);
        }
        _readout = null;
        InvalidateVisual();
    }

    /// <summary>Where a seat's center is drawn (tests use it to press seats).</summary>
    public Point SeatCenter(int index) => ToScreen(_seats[index].Position);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TakenBrushProperty || change.Property == FocusBrushProperty || change.Property == ChosenForegroundProperty
            || change.Property == TextBrushProperty || change.Property == ReadoutBorderBrushProperty)
        {
            _takenPen = _hoverPen = _focusPen = _checkPen = _readoutPen = null;
        }
        if (change.Property == TextBrushProperty || change.Property == MutedBrushProperty || change.Property == LabelFontSizeProperty
            || change.Property == LabelFontFamilyProperty)
        {
            _stageLabel = null;
            _sectionLabels.Clear();
            _readout = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 640 : availableSize.Width;
        var scale = Math.Min(28, width / _world.Width);
        return new Size(width, StageBand + SectionLabelBand + _world.Height * scale + 8);
    }

    private void UpdateTransform()
    {
        var size = Bounds.Size;
        var available = new Size(size.Width, Math.Max(1, size.Height - StageBand - SectionLabelBand - 8));
        _scale = Math.Min(28, Math.Min(available.Width / _world.Width, available.Height / _world.Height));
        var used = new Size(_world.Width * _scale, _world.Height * _scale);
        _offset = new Point((size.Width - used.Width) / 2 - _world.X * _scale, StageBand + SectionLabelBand - _world.Y * _scale);
    }

    private Point ToScreen(Point world)
    {
        UpdateTransform();
        return new Point(world.X * _scale + _offset.X, world.Y * _scale + _offset.Y);
    }

    private int HitTest(Point point)
    {
        UpdateTransform();
        var radius = _scale * 0.5;
        var best = -1;
        var bestDistance = radius * radius;
        for (var i = 0; i < _seats.Count; i++)
        {
            var p = _seats[i].Position;
            var dx = p.X * _scale + _offset.X - point.X;
            var dy = p.Y * _scale + _offset.Y - point.Y;
            var d = dx * dx + dy * dy;
            if (d <= bestDistance)
            {
                bestDistance = d;
                best = i;
            }
        }
        return best;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var hit = HitTest(e.GetPosition(this));
        if (hit != _hover)
        {
            _hover = hit;
            _readout = null;
            Cursor = hit >= 0 && !_taken[hit] ? new Cursor(StandardCursorType.Hand) : null;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hover >= 0)
        {
            _hover = -1;
            _readout = null;
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
        var hit = HitTest(e.GetPosition(this));
        if (hit >= 0)
        {
            _cursor = hit;
            Focus(NavigationMethod.Pointer);
            SeatInvoked?.Invoke(hit);
            CursorMoved?.Invoke(hit);
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _seats.Count == 0 || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var target = e.Key switch
        {
            Key.Left => SeatMapLayout.Step(_seats, _cursor, -1, 0),
            Key.Right => SeatMapLayout.Step(_seats, _cursor, 1, 0),
            Key.Up => SeatMapLayout.Step(_seats, _cursor, 0, -1),
            Key.Down => SeatMapLayout.Step(_seats, _cursor, 0, 1),
            Key.Home => 0,
            Key.End => _seats.Count - 1,
            _ => int.MinValue,
        };
        if (e.Key is Key.Enter or Key.Space)
        {
            if (_cursor >= 0)
            {
                SeatInvoked?.Invoke(_cursor);
            }
            e.Handled = true;
            return;
        }
        if (target == int.MinValue)
        {
            return;
        }
        if (target >= 0)
        {
            _cursor = target;
            _readout = null;
            CursorMoved?.Invoke(target);
            InvalidateVisual();
        }
        e.Handled = true;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (_cursor < 0 && _seats.Count > 0)
        {
            _cursor = 0;
        }
        CursorMoved?.Invoke(_cursor);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    private IBrush? TierBrush(SeatMapTier? tier) => (tier?.ColorIndex ?? 1) switch
    {
        2 => Tier2Brush,
        3 => Tier3Brush,
        4 => Tier4Brush,
        _ => Tier1Brush,
    };

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        if (_seats.Count == 0 || size.Width <= 0)
        {
            return;
        }
        UpdateTransform();
        var typeface = new Typeface(LabelFontFamily ?? FontFamily.Default);

        // The stage: a wide rounded band across the top.
        var stageWidth = Math.Min(size.Width * 0.6, _world.Width * _scale * 0.7);
        var stage = new Rect((size.Width - stageWidth) / 2, 6, stageWidth, 24);
        var radius = CornerRadius.TopLeft;
        context.DrawRectangle(StageBrush, null, stage, radius, radius);
        _stageLabel ??= new FormattedText(_stageText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, LabelFontSize, TextBrush);
        context.DrawText(_stageLabel, new Point(stage.Center.X - _stageLabel.Width / 2, stage.Center.Y - _stageLabel.Height / 2));

        // Section names over each section.
        foreach (var group in _seats.GroupBy(s => s.Section))
        {
            if (!_sectionLabels.TryGetValue(group.Key, out var label))
            {
                label = new FormattedText(group.Key.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, LabelFontSize, MutedBrush);
                _sectionLabels[group.Key] = label;
            }
            var minX = group.Min(s => s.Position.X);
            var maxX = group.Max(s => s.Position.X);
            var top = group.Min(s => s.Position.Y);
            var center = ToScreen(new Point((minX + maxX) / 2, top));
            context.DrawText(label, new Point(center.X - label.Width / 2, center.Y - _scale * 0.5 - label.Height - 2));
        }

        var r = Math.Max(2, _scale * 0.36);
        _takenPen ??= new ImmutablePen((TakenBrush ?? Brushes.Gray).ToImmutable(), 1.2);
        _hoverPen ??= new ImmutablePen((TextBrush ?? Brushes.Black).ToImmutable(), 1.5);
        _focusPen ??= new ImmutablePen((FocusBrush ?? Brushes.Blue).ToImmutable(), 2);
        _checkPen ??= new ImmutablePen((ChosenForeground ?? Brushes.White).ToImmutable(), 1.6, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        for (var i = 0; i < _seats.Count; i++)
        {
            var c = ToScreen(_seats[i].Position);
            if (_taken[i])
            {
                context.DrawEllipse(null, _takenPen, c, r - 0.6, r - 0.6);
                continue;
            }
            if (_chosen[i])
            {
                context.DrawEllipse(ChosenBrush, null, c, r + 1, r + 1);
                var k = r * 0.5;
                context.DrawLine(_checkPen, new Point(c.X - k, c.Y), new Point(c.X - k * 0.25, c.Y + k * 0.7));
                context.DrawLine(_checkPen, new Point(c.X - k * 0.25, c.Y + k * 0.7), new Point(c.X + k, c.Y - k * 0.7));
            }
            else
            {
                context.DrawEllipse(TierBrush(_seats[i].Tier), null, c, r, r);
            }
            if (i == _hover)
            {
                context.DrawEllipse(null, _hoverPen, c, r + 3, r + 3);
            }
        }
        var focusVisible = IsFocused && Classes.Contains(":focus-visible");
        if (focusVisible && _cursor >= 0)
        {
            var c = ToScreen(_seats[_cursor].Position);
            context.DrawEllipse(null, _focusPen, c, r + 4, r + 4);
        }

        // Readout for the seat under the pointer, else the keyboard cursor.
        var shown = _hover >= 0 ? _hover : focusVisible ? _cursor : -1;
        if (shown >= 0 && _describe is not null)
        {
            if (_readout is not { } cached || cached.Index != shown)
            {
                _readout = (shown, new FormattedText(_describe(_seats[shown]), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface,
                    LabelFontSize, TextBrush));
            }
            var text = _readout.Value.Text;
            var c = ToScreen(_seats[shown].Position);
            var box = new Rect(0, 0, text.Width + 16, text.Height + 10);
            var x = Math.Clamp(c.X - box.Width / 2, 0, Math.Max(0, size.Width - box.Width));
            var y = c.Y - r - 8 - box.Height;
            if (y < 0)
            {
                y = c.Y + r + 8;
            }
            box = box.Translate(new Vector(x, y));
            _readoutPen ??= new ImmutablePen((ReadoutBorderBrush ?? Brushes.Gray).ToImmutable(), 1);
            context.DrawRectangle(ReadoutBrush, _readoutPen, box, radius, radius);
            context.DrawText(text, new Point(box.X + 8, box.Y + 5));
        }
    }
}
