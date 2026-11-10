using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The drawing of a <see cref="PrizeWheel"/>: slices, labels, pegs, rim, hub and the pointer's flapper. Slice outlines
/// and labels are built when the size or the slices change; a frame only pushes the wheel's rotation and the flapper's
/// swing, so turning allocates nothing.
/// </summary>
internal sealed class PrizeWheelFace : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<PrizeWheelFace>();

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<PrizeWheelFace>();

    public static readonly StyledProperty<IBrush?> SliceFillProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(SliceFill));

    public static readonly StyledProperty<IBrush?> AlternateSliceFillProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(AlternateSliceFill));

    public static readonly StyledProperty<IBrush?> WinnerFillProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(WinnerFill));

    public static readonly StyledProperty<IBrush?> WinnerForegroundProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(WinnerForeground));

    public static readonly StyledProperty<IBrush?> RimFillProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(RimFill));

    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(LineBrush));

    public static readonly StyledProperty<IBrush?> PegFillProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(PegFill));

    public static readonly StyledProperty<IBrush?> PointerFillProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(PointerFill));

    public static readonly StyledProperty<IBrush?> HubFillProperty =
        AvaloniaProperty.Register<PrizeWheelFace, IBrush?>(nameof(HubFill));

    public static readonly StyledProperty<double> RimWidthProperty =
        AvaloniaProperty.Register<PrizeWheelFace, double>(nameof(RimWidth), 10);

    public static readonly StyledProperty<double> PointerLengthProperty =
        AvaloniaProperty.Register<PrizeWheelFace, double>(nameof(PointerLength), 30);

    private readonly List<StreamGeometry> _slices = [];
    private readonly List<TextLayout?> _labels = [];
    private TextLayout? _winnerLabel;
    private int _winnerLabelIndex = -1;
    private TextLayout? _hub;
    private StreamGeometry? _pointer;
    private IPen? _linePen;
    private Size _builtFor;
    private bool _dirty = true;
    private double _radius;
    private double _inner;
    private double _labelSize = 13;

    static PrizeWheelFace()
    {
        AffectsRender<PrizeWheelFace>(SliceFillProperty, AlternateSliceFillProperty, WinnerFillProperty, RimFillProperty,
            PegFillProperty, PointerFillProperty, HubFillProperty);
    }

    internal PrizeWheel? Owner { get; set; }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public IBrush? SliceFill { get => GetValue(SliceFillProperty); set => SetValue(SliceFillProperty, value); }

    public IBrush? AlternateSliceFill { get => GetValue(AlternateSliceFillProperty); set => SetValue(AlternateSliceFillProperty, value); }

    public IBrush? WinnerFill { get => GetValue(WinnerFillProperty); set => SetValue(WinnerFillProperty, value); }

    public IBrush? WinnerForeground { get => GetValue(WinnerForegroundProperty); set => SetValue(WinnerForegroundProperty, value); }

    public IBrush? RimFill { get => GetValue(RimFillProperty); set => SetValue(RimFillProperty, value); }

    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    public IBrush? PegFill { get => GetValue(PegFillProperty); set => SetValue(PegFillProperty, value); }

    public IBrush? PointerFill { get => GetValue(PointerFillProperty); set => SetValue(PointerFillProperty, value); }

    public IBrush? HubFill { get => GetValue(HubFillProperty); set => SetValue(HubFillProperty, value); }

    public double RimWidth { get => GetValue(RimWidthProperty); set => SetValue(RimWidthProperty, value); }

    public double PointerLength { get => GetValue(PointerLengthProperty); set => SetValue(PointerLengthProperty, value); }

    /// <summary>The wheel's outer radius at the current size.</summary>
    internal double Radius => _radius;

    /// <summary>The slices or labels changed: rebuild them before the next frame.</summary>
    public void Reset()
    {
        _dirty = true;
        InvalidateArrange();
        InvalidateVisual();
    }

    /// <summary>Whether <paramref name="point"/> (in this control's coordinates) is on the wheel.</summary>
    public bool IsOnWheel(Point point)
    {
        var center = Center;
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        return _radius > 0 && dx * dx + dy * dy <= _radius * _radius;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ForegroundProperty || property == FontFamilyProperty || property == WinnerForegroundProperty
            || property == RimWidthProperty || property == PointerLengthProperty)
        {
            Reset();
        }
        else if (property == LineBrushProperty)
        {
            _linePen = null;
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Square: as wide as allowed, up to 360px when nothing limits it.
        var side = Math.Min(availableSize.Width, availableSize.Height);
        if (!double.IsFinite(side))
        {
            side = double.IsFinite(availableSize.Width) ? availableSize.Width
                : double.IsFinite(availableSize.Height) ? availableSize.Height : 360;
        }
        side = Math.Min(side, 360);
        return new Size(side, side);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_dirty || finalSize != _builtFor)
        {
            Build(finalSize);
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (Owner is not { } owner || _radius <= 0)
        {
            return;
        }
        var model = owner.Model;
        var center = Center;
        var turn = Matrix.CreateRotation(Radians(model.Rotation)) * Matrix.CreateTranslation(center.X, center.Y);
        var line = _linePen ??= LineBrush is { } lineBrush ? new ImmutablePen(lineBrush.ToImmutable(), 1) : null;

        // The rim, then the slices inside it.
        context.DrawEllipse(RimFill, line, center, _radius, _radius);
        var winner = owner.ResultIndex;
        var highlight = owner.Highlight;
        using (context.PushTransform(turn))
        {
            for (var i = 0; i < _slices.Count && i < owner.Segments.Count; i++)
            {
                var fill = owner.Segments[i].Fill ?? (i % 2 == 0 ? SliceFill : AlternateSliceFill);
                context.DrawGeometry(fill, line, _slices[i]);
                if (i == winner && highlight > 0)
                {
                    using (context.PushOpacity(highlight))
                    {
                        context.DrawGeometry(WinnerFill, null, _slices[i]);
                    }
                }
            }
            // Pegs on the rim at every slice edge.
            var pegRadius = Math.Max(2, RimWidth * 0.26);
            var pegDistance = _radius - RimWidth / 2;
            for (var i = 0; i < model.Count; i++)
            {
                var angle = Radians(model.Start(i));
                context.DrawEllipse(PegFill, null, new Point(Math.Sin(angle) * pegDistance, -Math.Cos(angle) * pegDistance),
                    pegRadius, pegRadius);
            }
        }
        DrawLabels(context, owner, model, center, winner, highlight);
        DrawHub(context, owner, center, line);
        DrawPointer(context, center, model.Flap, line);
    }

    private Point Center => new(Bounds.Width / 2, Bounds.Height / 2 + PointerLength * 0.25);

    private void DrawLabels(DrawingContext context, PrizeWheel owner, PrizeWheelModel model, Point center, int winner, double highlight)
    {
        for (var i = 0; i < _labels.Count && i < model.Count; i++)
        {
            if (_labels[i] is not { } label)
            {
                continue;
            }
            // Labels run along the slice's middle, from the hub out, ending just inside the rim.
            var middle = model.Start(i) + model.Sweep(i) / 2;
            var transform = Matrix.CreateRotation(Radians(model.Rotation + middle - 90)) * Matrix.CreateTranslation(center.X, center.Y);
            var origin = new Point(_inner - 12 - label.WidthIncludingTrailingWhitespace, -label.Height / 2);
            using (context.PushTransform(transform))
            {
                if (i == winner && highlight > 0 && _winnerLabel is { } lit && _winnerLabelIndex == i)
                {
                    using (context.PushOpacity(1 - highlight))
                    {
                        label.Draw(context, origin);
                    }
                    using (context.PushOpacity(highlight))
                    {
                        lit.Draw(context, origin);
                    }
                }
                else
                {
                    label.Draw(context, origin);
                }
            }
        }
    }

    private void DrawHub(DrawingContext context, PrizeWheel owner, Point center, IPen? line)
    {
        var hubRadius = _radius * 0.21;
        context.DrawEllipse(HubFill, line, center, hubRadius, hubRadius);
        if (_hub is { } hub)
        {
            using (context.PushOpacity(owner.IsSpinning ? 0.45 : 1))
            {
                hub.Draw(context, new Point(center.X - hub.WidthIncludingTrailingWhitespace / 2, center.Y - hub.Height / 2));
            }
        }
    }

    private void DrawPointer(DrawingContext context, Point center, double flap, IPen? line)
    {
        if (_pointer is null)
        {
            return;
        }
        // The flapper hangs from a pivot above the rim; a peg pushes its tip the way the wheel turns.
        var pivot = new Point(center.X, center.Y - _radius - PointerLength * 0.2);
        var transform = Matrix.CreateRotation(Radians(-flap)) * Matrix.CreateTranslation(pivot.X, pivot.Y);
        using (context.PushTransform(transform))
        {
            context.DrawGeometry(PointerFill, line, _pointer);
        }
    }

    private void Build(Size size)
    {
        _dirty = false;
        _builtFor = size;
        foreach (var label in _labels)
        {
            label?.Dispose();
        }
        _labels.Clear();
        _slices.Clear();
        _hub?.Dispose();
        _hub = null;
        _winnerLabel?.Dispose();
        _winnerLabel = null;
        _winnerLabelIndex = -1;
        var side = Math.Min(size.Width, size.Height - PointerLength * 0.5);
        _radius = Math.Max(0, side / 2 - 2);
        _inner = Math.Max(0, _radius - RimWidth);
        if (Owner is not { } owner || _radius <= 0)
        {
            return;
        }
        var model = owner.Model;
        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight.Medium);
        var fontSize = _labelSize = Math.Clamp(_radius * 0.085, 11, 16);
        var labelWidth = Math.Max(24, _inner * 0.62);
        for (var i = 0; i < model.Count; i++)
        {
            _slices.Add(Slice(model.Start(i), model.Sweep(i), _inner));
            var segment = owner.Segments[i];
            var text = segment.Label;
            // A slice too thin for a line of text keeps its color but no label.
            var arc = Radians(model.Sweep(i)) * _inner * 0.7;
            _labels.Add(string.IsNullOrEmpty(text) || arc < fontSize * 1.1
                ? null
                : new TextLayout(text, typeface, fontSize, segment.Foreground ?? Foreground, TextAlignment.Right, TextWrapping.NoWrap,
                    TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, labelWidth, double.PositiveInfinity, double.NaN, 0, 1));
        }
        if (owner.SpinText is { Length: > 0 } spin)
        {
            _hub = new TextLayout(spin, new Typeface(FontFamily, FontStyle.Normal, FontWeight.SemiBold), Math.Clamp(_radius * 0.09, 11, 17),
                Foreground, TextAlignment.Center, TextWrapping.NoWrap, TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight,
                _radius * 0.38, double.PositiveInfinity, double.NaN, 0, 1);
        }
        _pointer = Pointer(PointerLength);
        UpdateWinnerLabel();
    }

    /// <summary>Builds the lit label of the winning slice (once per result, not per frame).</summary>
    internal void UpdateWinnerLabel()
    {
        if (Owner is not { } owner)
        {
            return;
        }
        var index = owner.ResultIndex;
        if (index == _winnerLabelIndex)
        {
            return;
        }
        _winnerLabel?.Dispose();
        _winnerLabel = null;
        _winnerLabelIndex = index;
        if (index < 0 || index >= _labels.Count || _labels[index] is not { } plain || owner.Segments[index].Label is not { } text)
        {
            return;
        }
        _winnerLabel = new TextLayout(text, new Typeface(FontFamily, FontStyle.Normal, FontWeight.Medium), _labelSize,
            WinnerForeground ?? Foreground, TextAlignment.Right, TextWrapping.NoWrap, TextTrimming.CharacterEllipsis, null,
            FlowDirection.LeftToRight, plain.MaxWidth, double.PositiveInfinity, double.NaN, 0, 1);
    }

    private static StreamGeometry Slice(double start, double sweep, double radius)
    {
        var geometry = new StreamGeometry();
        using var path = geometry.Open();
        if (sweep >= 359.999)
        {
            path.BeginFigure(new Point(0, -radius), true);
            path.ArcTo(new Point(0, radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
            path.ArcTo(new Point(0, -radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
            path.EndFigure(true);
            return geometry;
        }
        if (sweep <= 0)
        {
            return geometry;
        }
        var a0 = Radians(start);
        var a1 = Radians(start + sweep);
        path.BeginFigure(default, true);
        path.LineTo(new Point(Math.Sin(a0) * radius, -Math.Cos(a0) * radius));
        path.ArcTo(new Point(Math.Sin(a1) * radius, -Math.Cos(a1) * radius), new Size(radius, radius), 0, sweep > 180,
            SweepDirection.Clockwise);
        path.EndFigure(true);
        return geometry;
    }

    /// <summary>The flapper: a rounded drop hanging from its pivot (at the origin) and tapering to a point below.</summary>
    private static StreamGeometry Pointer(double length)
    {
        var half = length * 0.32;
        var geometry = new StreamGeometry();
        using var path = geometry.Open();
        path.BeginFigure(new Point(-half, 0), true);
        path.ArcTo(new Point(half, 0), new Size(half, half), 0, false, SweepDirection.Clockwise);
        path.QuadraticBezierTo(new Point(half * 0.7, length * 0.45), new Point(0, length));
        path.QuadraticBezierTo(new Point(-half * 0.7, length * 0.45), new Point(-half, 0));
        path.EndFigure(true);
        return geometry;
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180;
}
