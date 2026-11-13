using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The drawing of a <see cref="PrizeWheel"/>: the shadow, slices and their radial labels, divider lines, the metal rim with
/// its pegs and a still sheen, the pointer, and the hub with its charge ring. The wheel is laid out in a 400-unit box
/// scaled to the control's square, so lines and pegs scale with it. Slice outlines, labels and brushes are built when the
/// size, colors or slices change; a frame only pushes the wheel's rotation, the pointer's bend and the hub's state.
/// </summary>
internal sealed class PrizeWheelFace : Control
{
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<PrizeWheelFace, FontFamily>(nameof(FontFamily), FontFamily.Default);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<IBrush?> SliceFillProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(SliceFill));

    public static readonly StyledProperty<IBrush?> AlternateSliceFillProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(AlternateSliceFill));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(LabelBrush));

    public static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(MutedBrush));

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<IBrush?> AccentForegroundProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(AccentForeground));

    public static readonly StyledProperty<IBrush?> DividerBrushProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(DividerBrush));

    public static readonly StyledProperty<IBrush?> EdgeBrushProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(EdgeBrush));

    public static readonly StyledProperty<IBrush?> RimBrushProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(RimBrush));

    public static readonly StyledProperty<IBrush?> RimSheenStartProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(RimSheenStart));

    public static readonly StyledProperty<IBrush?> RimSheenEndProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(RimSheenEnd));

    public static readonly StyledProperty<IBrush?> HairlineBrushProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(HairlineBrush));

    public static readonly StyledProperty<IBrush?> PegFillProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(PegFill));

    public static readonly StyledProperty<IBrush?> PegStrokeProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(PegStroke));

    public static readonly StyledProperty<IBrush?> PegHighlightProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(PegHighlight));

    public static readonly StyledProperty<IBrush?> PointerStrokeProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(PointerStroke));

    public static readonly StyledProperty<IBrush?> HubTopProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(HubTop));

    public static readonly StyledProperty<IBrush?> HubMiddleProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(HubMiddle));

    public static readonly StyledProperty<IBrush?> HubBottomProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(HubBottom));

    public static readonly StyledProperty<IBrush?> HubForegroundProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(HubForeground));

    public static readonly StyledProperty<BoxShadows> HubShadowProperty =
        AvaloniaProperty.Register<PrizeWheelFace, BoxShadows>(nameof(HubShadow));

    public static readonly StyledProperty<BoxShadows> WheelShadowProperty =
        AvaloniaProperty.Register<PrizeWheelFace, BoxShadows>(nameof(WheelShadow));

    public static readonly StyledProperty<double> HubFontSizeProperty =
        AvaloniaProperty.Register<PrizeWheelFace, double>(nameof(HubFontSize), 15);

    public static readonly StyledProperty<double> HubCompactFontSizeProperty =
        AvaloniaProperty.Register<PrizeWheelFace, double>(nameof(HubCompactFontSize), 13);

    public static readonly StyledProperty<double> RimWidthProperty =
        AvaloniaProperty.Register<PrizeWheelFace, double>(nameof(RimWidth), 16);

    public static readonly StyledProperty<double> PointerLengthProperty =
        AvaloniaProperty.Register<PrizeWheelFace, double>(nameof(PointerLength), 29);

    /// <summary>The drawing box's side, in units; everything on the wheel is measured in it.</summary>
    internal const double Box = 400;
    internal const double SliceRadius = 162;
    internal const double RimRadius = 170;
    internal const double HairlineRadius = 177.5;
    internal const double ShadowRadius = 184;
    internal const double HubRadius = 48;
    internal const double LabelEnd = 148;
    internal const double LabelSize = 14;

    private const double LabelRoom = 92;
    private const double DefaultPointerLength = 29;

    private readonly List<StreamGeometry> _slices = [];
    private readonly List<TextLayout?> _labels = [];
    private readonly List<IBrush?> _fills = [];
    private TextLayout? _winnerLabel;
    private int _winnerLabelIndex = -1;
    private TextLayout? _hubLabel;
    private string? _hubText;
    private IBrush? _hubLabelBrush;
    private double _hubLabelSize;
    private StreamGeometry? _pointer;
    private StreamGeometry? _ring;
    private double _ringValue = -1;
    private double _ringSide;
    private IPen? _dividerPen;
    private IPen? _edgePen;
    private IPen? _rimPen;
    private IPen? _sheenPen;
    private IPen? _hairlinePen;
    private IPen? _pegPen;
    private IPen? _pointerPen;
    private IPen? _ringPen;
    private IBrush? _hubBrush;
    private bool _dirty = true;
    private bool _brushesDirty = true;

    static PrizeWheelFace()
    {
        AffectsRender<PrizeWheelFace>(SliceFillProperty, AlternateSliceFillProperty, AccentBrushProperty, PegFillProperty,
            PegHighlightProperty, HubShadowProperty, WheelShadowProperty);
    }

    internal PrizeWheel? Owner { get; set; }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public IBrush? SliceFill { get => GetValue(SliceFillProperty); set => SetValue(SliceFillProperty, value); }

    public IBrush? AlternateSliceFill { get => GetValue(AlternateSliceFillProperty); set => SetValue(AlternateSliceFillProperty, value); }

    public IBrush? LabelBrush { get => GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }

    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }

    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    public IBrush? AccentForeground { get => GetValue(AccentForegroundProperty); set => SetValue(AccentForegroundProperty, value); }

    public IBrush? DividerBrush { get => GetValue(DividerBrushProperty); set => SetValue(DividerBrushProperty, value); }

    public IBrush? EdgeBrush { get => GetValue(EdgeBrushProperty); set => SetValue(EdgeBrushProperty, value); }

    public IBrush? RimBrush { get => GetValue(RimBrushProperty); set => SetValue(RimBrushProperty, value); }

    public IBrush? RimSheenStart { get => GetValue(RimSheenStartProperty); set => SetValue(RimSheenStartProperty, value); }

    public IBrush? RimSheenEnd { get => GetValue(RimSheenEndProperty); set => SetValue(RimSheenEndProperty, value); }

    public IBrush? HairlineBrush { get => GetValue(HairlineBrushProperty); set => SetValue(HairlineBrushProperty, value); }

    public IBrush? PegFill { get => GetValue(PegFillProperty); set => SetValue(PegFillProperty, value); }

    public IBrush? PegStroke { get => GetValue(PegStrokeProperty); set => SetValue(PegStrokeProperty, value); }

    public IBrush? PegHighlight { get => GetValue(PegHighlightProperty); set => SetValue(PegHighlightProperty, value); }

    public IBrush? PointerStroke { get => GetValue(PointerStrokeProperty); set => SetValue(PointerStrokeProperty, value); }

    public IBrush? HubTop { get => GetValue(HubTopProperty); set => SetValue(HubTopProperty, value); }

    public IBrush? HubMiddle { get => GetValue(HubMiddleProperty); set => SetValue(HubMiddleProperty, value); }

    public IBrush? HubBottom { get => GetValue(HubBottomProperty); set => SetValue(HubBottomProperty, value); }

    public IBrush? HubForeground { get => GetValue(HubForegroundProperty); set => SetValue(HubForegroundProperty, value); }

    public BoxShadows HubShadow { get => GetValue(HubShadowProperty); set => SetValue(HubShadowProperty, value); }

    public BoxShadows WheelShadow { get => GetValue(WheelShadowProperty); set => SetValue(WheelShadowProperty, value); }

    public double HubFontSize { get => GetValue(HubFontSizeProperty); set => SetValue(HubFontSizeProperty, value); }

    public double HubCompactFontSize { get => GetValue(HubCompactFontSizeProperty); set => SetValue(HubCompactFontSizeProperty, value); }

    public double RimWidth { get => GetValue(RimWidthProperty); set => SetValue(RimWidthProperty, value); }

    public double PointerLength { get => GetValue(PointerLengthProperty); set => SetValue(PointerLengthProperty, value); }

    /// <summary>The wheel's square side in pixels at the current size.</summary>
    internal double Side => Math.Min(Bounds.Width, Bounds.Height);

    /// <summary>Pixels per drawing unit.</summary>
    internal double Unit => Side / Box;

    /// <summary>The wheel's centre in this control's coordinates.</summary>
    internal Point Center => new(Bounds.Width / 2, Bounds.Height / 2);

    /// <summary>The wheel's outer radius in pixels (to the outside of the rim).</summary>
    internal double Radius => (RimRadius + RimWidth / 2) * Unit;

    /// <summary>The hub's radius in pixels.</summary>
    internal double HubPixelRadius => HubRadius * Unit;

    /// <summary>The slices or labels changed: rebuild them before the next frame.</summary>
    public void Reset()
    {
        _dirty = true;
        _brushesDirty = true;
        InvalidateVisual();
    }

    /// <summary>Whether <paramref name="point"/> (in this control's coordinates) is on the wheel, rim included.</summary>
    public bool IsOnWheel(Point point) => Distance(point) <= Radius;

    /// <summary>Whether <paramref name="point"/> (in this control's coordinates) is on the hub.</summary>
    public bool IsOnHub(Point point) => Distance(point) <= HubPixelRadius;

    /// <summary>The angle of <paramref name="point"/> around the wheel's centre, degrees clockwise from the top.</summary>
    public double AngleOf(Point point)
    {
        var center = Center;
        return Math.Atan2(point.X - center.X, center.Y - point.Y) * 180 / Math.PI;
    }

    /// <summary>The middle of the slice under the pointer, where a won reward card starts its flight.</summary>
    public Point FlightStart => new(Center.X, Center.Y - (HubRadius + SliceRadius) / 2 * Unit);

    private double Distance(Point point)
    {
        var center = Center;
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FacePeer(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == FontFamilyProperty || property == LabelBrushProperty || property == AccentForegroundProperty
            || property == ForegroundProperty || property == AccentBrushProperty || property == SliceFillProperty
            || property == AlternateSliceFillProperty)
        {
            Reset();
        }
        else if (property == DividerBrushProperty || property == EdgeBrushProperty || property == RimBrushProperty
                 || property == RimSheenStartProperty || property == RimSheenEndProperty || property == HairlineBrushProperty
                 || property == PegStrokeProperty || property == PointerStrokeProperty || property == HubTopProperty
                 || property == HubMiddleProperty || property == HubBottomProperty || property == RimWidthProperty
                 || property == HubForegroundProperty || property == MutedBrushProperty || property == PointerLengthProperty)
        {
            _brushesDirty = true;
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var side = Math.Min(availableSize.Width, availableSize.Height);
        if (!double.IsFinite(side))
        {
            side = double.IsFinite(availableSize.Width) ? availableSize.Width
                : double.IsFinite(availableSize.Height) ? availableSize.Height : 480;
        }
        return new Size(side, side);
    }

    public override void Render(DrawingContext context)
    {
        if (Owner is not { } owner || Side <= 0)
        {
            return;
        }
        if (_dirty)
        {
            Build(owner);
        }
        if (_brushesDirty)
        {
            BuildBrushes();
        }
        var model = owner.Model;
        var unit = Unit;
        var center = Center;
        var winner = owner.ResultIndex;
        var highlight = owner.Highlight;

        // The soft shadow under the wheel, in pixels (it does not scale with the wheel).
        var shadowRadius = ShadowRadius * unit;
        context.DrawRectangle(Brushes.Transparent, null,
            new RoundedRect(new Rect(center.X - shadowRadius, center.Y - shadowRadius, shadowRadius * 2, shadowRadius * 2), shadowRadius),
            WheelShadow);

        var box = Matrix.CreateScale(unit, unit) * Matrix.CreateTranslation(center.X - Box / 2 * unit, center.Y - Box / 2 * unit);
        using (context.PushTransform(box))
        {
            var spin = Matrix.CreateTranslation(-200, -200) * Matrix.CreateRotation(Radians(model.Rotation)) * Matrix.CreateTranslation(200, 200);
            using (context.PushTransform(spin))
            {
                DrawWheel(context, owner, model, winner, highlight);
            }
            // The sheen stays still while the wheel turns under it.
            context.DrawEllipse(null, _sheenPen, new Point(200, 200), RimRadius, RimRadius);
            DrawPointer(context, model.Flap);
        }
        DrawHub(context, owner, center, unit);
    }

    private void DrawWheel(DrawingContext context, PrizeWheel owner, PrizeWheelModel model, int winner, double highlight)
    {
        var centre = new Point(200, 200);
        var count = Math.Min(_slices.Count, model.Count);
        for (var i = 0; i < count; i++)
        {
            context.DrawGeometry(_fills[i], null, _slices[i]);
            if (i == winner && highlight > 0)
            {
                using (context.PushOpacity(highlight))
                {
                    context.DrawGeometry(AccentBrush, null, _slices[i]);
                }
            }
        }
        if (count > 1)
        {
            for (var i = 0; i < count; i++)
            {
                var a = Radians(model.Start(i));
                context.DrawLine(_dividerPen!, centre, new Point(200 + Math.Sin(a) * SliceRadius, 200 - Math.Cos(a) * SliceRadius));
            }
        }
        context.DrawEllipse(null, _edgePen, centre, SliceRadius, SliceRadius);
        context.DrawEllipse(null, _rimPen, centre, RimRadius, RimRadius);
        context.DrawEllipse(null, _hairlinePen, centre, HairlineRadius, HairlineRadius);
        for (var i = 0; i < count; i++)
        {
            var a = Radians(model.Start(i));
            var peg = new Point(200 + Math.Sin(a) * RimRadius, 200 - Math.Cos(a) * RimRadius);
            context.DrawEllipse(PegFill, _pegPen, peg, PrizeWheelModel.PegRadius, PrizeWheelModel.PegRadius);
            context.DrawEllipse(PegHighlight, null, new Point(peg.X - 0.9, peg.Y - 0.9), 1.2, 1.2);
        }
        // Labels run outward from the hub along the middle of their slice and end just inside the rim.
        var dim = winner >= 0 ? 1 - 0.5 * highlight : 1;
        for (var i = 0; i < count && i < _labels.Count; i++)
        {
            if (_labels[i] is not { } label)
            {
                continue;
            }
            var middle = model.Start(i) + model.Sweep(i) / 2;
            var turn = Matrix.CreateTranslation(-200, -200) * Matrix.CreateRotation(Radians(middle - 90)) * Matrix.CreateTranslation(200, 200);
            var origin = new Point(200 + LabelEnd - label.WidthIncludingTrailingWhitespace, 200 - label.Height / 2);
            using (context.PushTransform(turn))
            {
                if (i == winner && highlight > 0 && _winnerLabelIndex == i && _winnerLabel is { } lit)
                {
                    using (context.PushOpacity(1 - highlight))
                    {
                        label.Draw(context, origin);
                    }
                    using (context.PushOpacity(highlight))
                    {
                        lit.Draw(context, new Point(200 + LabelEnd - lit.WidthIncludingTrailingWhitespace, origin.Y));
                    }
                }
                else if (dim < 1)
                {
                    using (context.PushOpacity(dim))
                    {
                        label.Draw(context, origin);
                    }
                }
                else
                {
                    label.Draw(context, origin);
                }
            }
        }
    }

    private void DrawPointer(DrawingContext context, double flap)
    {
        if (_pointer is null)
        {
            return;
        }
        var pivot = new Point(PrizeWheelModel.PivotX, PrizeWheelModel.PivotY);
        var bend = Matrix.CreateRotation(Radians(flap)) * Matrix.CreateTranslation(pivot.X, pivot.Y);
        using (context.PushTransform(bend))
        {
            context.DrawGeometry(AccentBrush, _pointerPen, _pointer);
        }
        context.DrawEllipse(PegFill, _pegPen, pivot, 2.6, 2.6);
    }

    private void DrawHub(DrawingContext context, PrizeWheel owner, Point center, double unit)
    {
        var radius = HubRadius * unit;
        var scale = owner.HubScale;
        var transform = Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushTransform(transform))
        {
            context.DrawRectangle(_hubBrush, null, new RoundedRect(new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2), radius),
                HubShadow);
            var charge = owner.Charge;
            if (charge > 0.001)
            {
                var side = radius * 2;
                if (_ring is null || Math.Abs(_ringValue - charge) > 0.0005 || _ringSide != side)
                {
                    _ring = Ring(charge, side * 0.46);
                    _ringValue = charge;
                    _ringSide = side;
                    _ringPen = new ImmutablePen((AccentBrush ?? Brushes.Gray).ToImmutable(), side * 0.04, lineCap: PenLineCap.Round);
                }
                using (context.PushTransform(Matrix.CreateTranslation(center.X, center.Y)))
                {
                    context.DrawGeometry(null, _ringPen, _ring);
                }
            }
            var text = owner.HubText;
            var size = Side >= 420 ? HubFontSize : HubCompactFontSize;
            var brush = owner.IsClaimed ? MutedBrush ?? HubForeground : HubForeground;
            if (_hubLabel is null || _hubText != text || _hubLabelSize != size || !ReferenceEquals(_hubLabelBrush, brush))
            {
                _hubLabel?.Dispose();
                _hubLabel = string.IsNullOrEmpty(text) ? null : new TextLayout(text, new Typeface(FontFamily, FontStyle.Normal, FontWeight.Medium), size,
                    brush, TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight,
                    radius * 1.7, double.PositiveInfinity, double.NaN, -size * 0.01, 1);
                _hubText = text;
                _hubLabelSize = size;
                _hubLabelBrush = brush;
            }
            if (_hubLabel is { } hub)
            {
                using (context.PushOpacity(owner.HubLabelOpacity))
                {
                    hub.Draw(context, new Point(center.X - hub.WidthIncludingTrailingWhitespace / 2, center.Y - hub.Height / 2));
                }
            }
        }
    }

    private void Build(PrizeWheel owner)
    {
        _dirty = false;
        foreach (var label in _labels)
        {
            label?.Dispose();
        }
        _labels.Clear();
        _slices.Clear();
        _fills.Clear();
        _winnerLabel?.Dispose();
        _winnerLabel = null;
        _winnerLabelIndex = -1;
        var model = owner.Model;
        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight.Medium);
        var surface = ColorOf(SliceFill, Colors.White);
        var accent = ColorOf(AccentBrush, Colors.Gray);
        var featuredFill = new ImmutableSolidColorBrush(Oklch.MixOklab(surface, accent, 0.16));
        var featuredLabel = new ImmutableSolidColorBrush(Oklch.MixOklab(ColorOf(Foreground, Colors.Black), accent, 0.78));
        for (var i = 0; i < model.Count; i++)
        {
            var segment = owner.Segments[i];
            _slices.Add(Slice(model.Start(i), model.Sweep(i)));
            _fills.Add(segment.Fill ?? (segment.IsFeatured ? featuredFill : i % 2 == 0 ? SliceFill : AlternateSliceFill));
            var text = segment.Label;
            // A slice too thin for a line of text keeps its color but drops its label (the odds list still names it).
            var room = Radians(model.Sweep(i)) * (HubRadius + LabelEnd) / 2;
            var foreground = segment.Foreground ?? (segment.IsFeatured && segment.Fill is null ? featuredLabel : LabelBrush);
            _labels.Add(string.IsNullOrEmpty(text) || room < LabelSize * 1.25
                ? null
                : Label(text, typeface, foreground));
        }
        UpdateWinnerLabel();
        _pointer = Pointer(PointerLength / DefaultPointerLength);
    }

    private static TextLayout Label(string text, Typeface typeface, IBrush? foreground) =>
        new(text, typeface, LabelSize, foreground, TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.CharacterEllipsis, null,
            FlowDirection.LeftToRight, LabelRoom, double.PositiveInfinity, double.NaN, -0.14, 1);

    private void BuildBrushes()
    {
        _brushesDirty = false;
        _dividerPen = new ImmutablePen((DividerBrush ?? Brushes.Gray).ToImmutable(), 1);
        _edgePen = new ImmutablePen((EdgeBrush ?? Brushes.Gray).ToImmutable(), 1);
        _rimPen = new ImmutablePen((RimBrush ?? Brushes.Gray).ToImmutable(), RimWidth);
        _hairlinePen = new ImmutablePen((HairlineBrush ?? Brushes.Transparent).ToImmutable(), 1);
        _pegPen = new ImmutablePen((PegStroke ?? Brushes.Black).ToImmutable(), 0.8);
        _pointerPen = new ImmutablePen((PointerStroke ?? Brushes.Black).ToImmutable(), 0.8);
        var sheen = new ImmutableLinearGradientBrush(
            [new ImmutableGradientStop(0, ColorOf(RimSheenStart, Colors.Transparent)),
             new ImmutableGradientStop(0.45, Colors.Transparent),
             new ImmutableGradientStop(1, ColorOf(RimSheenEnd, Colors.Transparent))],
            startPoint: new RelativePoint(0.15, 0, RelativeUnit.Relative),
            endPoint: new RelativePoint(0.85, 1, RelativeUnit.Relative));
        _sheenPen = new ImmutablePen(sheen, RimWidth);
        _hubBrush = new ImmutableLinearGradientBrush(
            [new ImmutableGradientStop(0, ColorOf(HubTop, Colors.LightGray)),
             new ImmutableGradientStop(0.55, ColorOf(HubMiddle, Colors.Gray)),
             new ImmutableGradientStop(1, ColorOf(HubBottom, Colors.LightGray))],
            startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
            endPoint: new RelativePoint(1, 1, RelativeUnit.Relative));
        _ring = null;
        _hubLabel?.Dispose();
        _hubLabel = null;
        _pointer = Pointer(PointerLength / DefaultPointerLength);
    }

    /// <summary>Builds the lit label of the winning slice (once per result, not per frame).</summary>
    internal void UpdateWinnerLabel()
    {
        if (Owner is not { } owner)
        {
            return;
        }
        var index = owner.ResultIndex;
        if (index == _winnerLabelIndex && _winnerLabel is not null)
        {
            return;
        }
        _winnerLabel?.Dispose();
        _winnerLabel = null;
        _winnerLabelIndex = index;
        if (index < 0 || index >= _labels.Count || _labels[index] is null || owner.Segments[index].Label is not { } text)
        {
            return;
        }
        _winnerLabel = Label(text, new Typeface(FontFamily, FontStyle.Normal, FontWeight.Medium), AccentForeground ?? Foreground);
        InvalidateVisual();
    }

    private static StreamGeometry Slice(double start, double sweep)
    {
        var geometry = new StreamGeometry();
        using var path = geometry.Open();
        var centre = new Point(200, 200);
        const double r = SliceRadius;
        if (sweep >= 359.999)
        {
            path.BeginFigure(new Point(200, 200 - r), true);
            path.ArcTo(new Point(200, 200 + r), new Size(r, r), 0, false, SweepDirection.Clockwise);
            path.ArcTo(new Point(200, 200 - r), new Size(r, r), 0, false, SweepDirection.Clockwise);
            path.EndFigure(true);
            return geometry;
        }
        if (sweep <= 0)
        {
            return geometry;
        }
        var a0 = Radians(start);
        var a1 = Radians(start + sweep);
        path.BeginFigure(centre, true);
        path.LineTo(new Point(200 + Math.Sin(a0) * r, 200 - Math.Cos(a0) * r));
        path.ArcTo(new Point(200 + Math.Sin(a1) * r, 200 - Math.Cos(a1) * r), new Size(r, r), 0, sweep > 180, SweepDirection.Clockwise);
        path.EndFigure(true);
        return geometry;
    }

    /// <summary>
    /// The pointer: a capsule hanging from its pivot (the origin) whose round head narrows to a round tip below, built
    /// from the two circles and the lines that touch both.
    /// </summary>
    private static StreamGeometry Pointer(double scale)
    {
        var r1 = PrizeWheelModel.HeadRadius * scale;
        var r2 = PrizeWheelModel.TipRadius * scale;
        var h = PrizeWheelModel.TipLength * scale;
        var b = (r1 - r2) / h;
        var a = Math.Sqrt(1 - b * b);
        var geometry = new StreamGeometry();
        using var path = geometry.Open();
        path.BeginFigure(new Point(r1 * a, r1 * b), true);
        path.LineTo(new Point(r2 * a, h + r2 * b));
        path.ArcTo(new Point(-r2 * a, h + r2 * b), new Size(r2, r2), 0, false, SweepDirection.Clockwise);
        path.LineTo(new Point(-r1 * a, r1 * b));
        path.ArcTo(new Point(r1 * a, r1 * b), new Size(r1, r1), 0, true, SweepDirection.Clockwise);
        path.EndFigure(true);
        return geometry;
    }

    /// <summary>The charge ring: an arc from 12 o'clock, clockwise, around the origin.</summary>
    private static StreamGeometry Ring(double value, double radius)
    {
        var geometry = new StreamGeometry();
        using var path = geometry.Open();
        var sweep = Math.Clamp(value, 0, 1) * 360;
        path.BeginFigure(new Point(0, -radius), false);
        if (sweep >= 359.9)
        {
            path.ArcTo(new Point(0, radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
            path.ArcTo(new Point(0, -radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
        }
        else
        {
            var a = Radians(sweep);
            path.ArcTo(new Point(Math.Sin(a) * radius, -Math.Cos(a) * radius), new Size(radius, radius), 0, sweep > 180,
                SweepDirection.Clockwise);
        }
        path.EndFigure(false);
        return geometry;
    }

    private static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid ? solid.Color : fallback;

    private static double Radians(double degrees) => degrees * Math.PI / 180;

    /// <summary>The wheel reads as one image named with every prize and its chance, so the odds are never visual only.</summary>
    private sealed class FacePeer(PrizeWheelFace owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override string? GetNameCore() => owner.Owner?.OddsDescription ?? base.GetNameCore();

        protected override bool IsContentElementCore() => true;
    }
}
