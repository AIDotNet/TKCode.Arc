// Ported from Arc Rating (MIT, Copyright (c) 2026 Elia Kuratli): the fill sweep, the kept mark, the press and soften
// scales and the commit pop.
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The row of stars of a <see cref="Rating"/>, drawn by one control. Each star has three layers: the empty shape, a
/// faint mark of the committed value and the live fill that sweeps left to right. Motion values are created the first
/// time a star moves, so a read-only rating in a long list costs no more than its drawing.
/// </summary>
internal sealed class RatingStars : Control
{
    private const double GlyphBox = 24;
    private const double StrokeWidth = 1.75;
    private const double PressedScale = 0.86;
    private const double SoftScale = 0.9;

    /// <summary>Scale per second handed to a resting star: it peaks near 1.18, then the spring settles it back to 1.</summary>
    private const double PopVelocity = 9;

    // Arc's snappy spring in physical terms: a time-defined spring would drop the velocity a pop is made of.
    private static readonly double s_snappyRoot = 2 * Math.PI / (0.26 * 1.2);
    private static readonly SpringSpec s_popSpring =
        SpringSpec.FromPhysical(s_snappyRoot * s_snappyRoot, 2 * (1 - 0.12) * s_snappyRoot);

    public static readonly StyledProperty<double> CellSizeProperty =
        AvaloniaProperty.Register<RatingStars, double>(nameof(CellSize), 36);

    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<RatingStars, double>(nameof(GlyphSize), 24);

    public static readonly StyledProperty<double> CellGapProperty =
        AvaloniaProperty.Register<RatingStars, double>(nameof(CellGap), 2);

    public static readonly StyledProperty<double> KeptOpacityProperty =
        AvaloniaProperty.Register<RatingStars, double>(nameof(KeptOpacity), 0.3);

    public static readonly StyledProperty<CornerRadius> CellRadiusProperty =
        AvaloniaProperty.Register<RatingStars, CornerRadius>(nameof(CellRadius));

    public static readonly StyledProperty<IBrush?> EmptyBrushProperty =
        AvaloniaProperty.Register<RatingStars, IBrush?>(nameof(EmptyBrush));

    public static readonly StyledProperty<IBrush?> SoftBrushProperty =
        AvaloniaProperty.Register<RatingStars, IBrush?>(nameof(SoftBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<RatingStars, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<IBrush?> FocusBrushProperty =
        AvaloniaProperty.Register<RatingStars, IBrush?>(nameof(FocusBrush));

    private sealed class Star
    {
        public double Fill;
        public double Kept;
        public double Lift = 1;
        public double Pop = 1;
        public MotionValue<double>? FillMotion;
        public MotionValue<double>? KeptMotion;
        public MotionValue<double>? LiftMotion;
        public MotionValue<double>? PopMotion;
    }

    private Star[] _stars = [];
    private double _rated;
    private MotionValue<double>? _ratedMotion;
    private IPen? _emptyPen;
    private IPen? _softPen;
    private IPen? _fillPen;
    private IPen? _focusPen;

    static RatingStars()
    {
        AffectsMeasure<RatingStars>(CellSizeProperty, CellGapProperty);
        AffectsRender<RatingStars>(GlyphSizeProperty, KeptOpacityProperty, CellRadiusProperty, EmptyBrushProperty,
            SoftBrushProperty, FillBrushProperty, FocusBrushProperty);
    }

    public double CellSize { get => GetValue(CellSizeProperty); set => SetValue(CellSizeProperty, value); }
    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }
    public double CellGap { get => GetValue(CellGapProperty); set => SetValue(CellGapProperty, value); }
    public double KeptOpacity { get => GetValue(KeptOpacityProperty); set => SetValue(KeptOpacityProperty, value); }
    public CornerRadius CellRadius { get => GetValue(CellRadiusProperty); set => SetValue(CellRadiusProperty, value); }
    public IBrush? EmptyBrush { get => GetValue(EmptyBrushProperty); set => SetValue(EmptyBrushProperty, value); }
    public IBrush? SoftBrush { get => GetValue(SoftBrushProperty); set => SetValue(SoftBrushProperty, value); }
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
    public IBrush? FocusBrush { get => GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }

    internal Rating? Owner { get; set; }

    /// <summary>The cell that shows the keyboard focus ring, or -1.</summary>
    internal int FocusedCell
    {
        get => _focusedCell;
        set
        {
            if (_focusedCell != value)
            {
                _focusedCell = value;
                InvalidateVisual();
            }
        }
    }

    private int _focusedCell = -1;

    /// <summary>The fill of a star as drawn right now (0 to 1), for tests.</summary>
    internal double FillOf(int index) => _stars[index].Fill;

    /// <summary>The scale of a star as drawn right now, for tests.</summary>
    internal double ScaleOf(int index) => _stars[index].Lift * _stars[index].Pop;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EmptyBrushProperty)
        {
            _emptyPen = CreatePen(EmptyBrush);
        }
        else if (change.Property == SoftBrushProperty)
        {
            _softPen = CreatePen(SoftBrush);
        }
        else if (change.Property == FillBrushProperty)
        {
            _fillPen = CreatePen(FillBrush);
        }
        else if (change.Property == FocusBrushProperty)
        {
            _focusPen = FocusBrush is null ? null : new Pen(FocusBrush, 2);
        }
        else if (change.Property == CellSizeProperty || change.Property == GlyphSizeProperty)
        {
            // Cells are wider than their stars; the negative margin lines the first star up with the label.
            var inset = (GlyphSize - CellSize) / 2;
            Margin = new Thickness(inset, 0);
        }
    }

    private static IPen? CreatePen(IBrush? brush)
        => brush is null ? null : new Pen(brush, StrokeWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = _stars.Length;
        return new Size(count * CellSize + Math.Max(0, count - 1) * CellGap, CellSize);
    }

    /// <summary>Sets the number of stars; they start at <paramref name="display"/> without motion.</summary>
    internal void Reset(int count, double display, double committed)
    {
        foreach (var star in _stars)
        {
            star.FillMotion?.Dispose();
            star.KeptMotion?.Dispose();
            star.LiftMotion?.Dispose();
            star.PopMotion?.Dispose();
        }
        _stars = new Star[count];
        for (var i = 0; i < count; i++)
        {
            _stars[i] = new Star
            {
                Fill = Portion(display, i),
                Kept = Portion(committed, i),
                Lift = display > 0 && Portion(display, i) == 0 ? SoftScale : 1
            };
        }
        _ratedMotion?.Dispose();
        _ratedMotion = null;
        _rated = display > 0 ? 1 : 0;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Moves the stars to a new shown value. The fill sweeps away from <paramref name="from"/>, one star after another.</summary>
    internal void Show(double display, double committed, double from, int pressed, bool animate)
    {
        if (!animate)
        {
            Reset(_stars.Length, display, committed);
            return;
        }
        for (var i = 0; i < _stars.Length; i++)
        {
            var star = _stars[i];
            var fill = Portion(display, i);
            var order = display > from ? i - Math.Floor(from) : Math.Ceiling(from) - 1 - i;
            var delay = TimeSpan.FromTicks((long)(Math.Max(0, order) * ArcMotionPresets.StaggerItem.Ticks));
            star.FillMotion = Drive(star.FillMotion, star.Fill, fill, MotionSpec.Spring(ArcSpring.Snappy, delay), v => star.Fill = v);
            star.KeptMotion = Drive(star.KeptMotion, star.Kept, Portion(committed, i), MotionSpec.Spring(ArcSpring.Smooth), v => star.Kept = v);
            var lift = pressed == i && ArcMotion.IsFull(this) ? PressedScale : display > 0 && fill == 0 ? SoftScale : 1;
            star.LiftMotion = Drive(star.LiftMotion, star.Lift, lift, MotionSpec.Spring(ArcSpring.Snappy), v => star.Lift = v);
        }
        var rated = display > 0 ? 1.0 : 0.0;
        _ratedMotion = Drive(_ratedMotion, _rated, rated,
            ArcMotion.IsFull(this) ? MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard) : MotionSpec.Instant,
            v => _rated = v);
    }

    /// <summary>Plays the commit impulse: a wave up to the chosen star, or only that star when commits come quickly.</summary>
    internal void Pop(double value, bool wave)
    {
        if (!ArcMotion.IsFull(this) || value <= 0)
        {
            return;
        }
        var chosen = (int)Math.Ceiling(value) - 1;
        for (var i = wave ? 0 : chosen; i <= chosen && i < _stars.Length; i++)
        {
            var star = _stars[i];
            star.PopMotion ??= CreateMotion(star.Pop, v => star.Pop = v);
            var delay = wave ? TimeSpan.FromTicks(i * ArcMotionPresets.StaggerItem.Ticks) : TimeSpan.Zero;
            _ = star.PopMotion.AnimateTo(1, MotionSpec.Spring(s_popSpring, delay), PopVelocity);
        }
    }

    private MotionValue<double>? Drive(MotionValue<double>? motion, double current, double target, MotionSpec spec, Action<double> write)
    {
        if (motion is null)
        {
            // Nothing is moving and nothing has to move, so no motion value is needed yet.
            if (current == target)
            {
                return null;
            }
            motion = CreateMotion(current, write);
        }
        if (motion.Target != target)
        {
            _ = motion.AnimateTo(target, spec);
        }
        return motion;
    }

    private MotionValue<double> CreateMotion(double initial, Action<double> write)
    {
        var motion = new MotionValue<double>(this, initial, MotionVectors.Double);
        motion.Changed += value =>
        {
            write(value);
            InvalidateVisual();
        };
        return motion;
    }

    private static double Portion(double value, int index) => Math.Clamp(value - index, 0, 1);

    /// <summary>The horizontal range of a cell.</summary>
    internal (double Left, double Right) CellRange(int index)
    {
        var left = index * (CellSize + CellGap);
        return (left, left + CellSize);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && Owner is { } owner)
        {
            e.Pointer.Capture(this);
            owner.OnStarsPressed(e.GetPosition(this).X, e.Pointer.Type != PointerType.Mouse);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Owner?.OnStarsMoved(e.GetPosition(this).X, e.Pointer.Type == PointerType.Mouse);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left)
        {
            Owner?.OnStarsReleased(e.GetPosition(this).X);
            e.Pointer.Capture(null);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        Owner?.OnStarsCancelled();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            Owner?.OnStarsLeft();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        // The whole row answers the pointer, gaps included.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        var geometry = ArcIcons.Star;
        var centerY = Bounds.Height / 2;
        var unit = GlyphSize / GlyphBox;
        for (var i = 0; i < _stars.Length; i++)
        {
            var star = _stars[i];
            var (left, _) = CellRange(i);
            if (i == _focusedCell && _focusPen is not null)
            {
                context.DrawRectangle(null, _focusPen, new RoundedRect(new Rect(left, centerY - CellSize / 2, CellSize, CellSize), CellRadius));
            }
            var scale = unit * star.Lift * star.Pop;
            var matrix = Matrix.CreateTranslation(-GlyphBox / 2, -GlyphBox / 2) * Matrix.CreateScale(scale, scale) *
                         Matrix.CreateTranslation(left + CellSize / 2, centerY);
            using (context.PushTransform(matrix))
            {
                // Once a value shows, empty stars soften from an outline into a quiet solid, so a partly filled star reads as one shape.
                if (_rated < 1)
                {
                    using (context.PushOpacity(1 - _rated))
                    {
                        context.DrawGeometry(null, _emptyPen, geometry);
                    }
                }
                if (_rated > 0)
                {
                    using (context.PushOpacity(_rated))
                    {
                        context.DrawGeometry(SoftBrush, _softPen, geometry);
                    }
                }
                if (star.Kept > 0)
                {
                    using (context.PushClip(new Rect(0, 0, GlyphBox * star.Kept, GlyphBox)))
                    using (context.PushOpacity(KeptOpacity))
                    {
                        context.DrawGeometry(FillBrush, _fillPen, geometry);
                    }
                }
                if (star.Fill > 0)
                {
                    using (context.PushClip(new Rect(0, 0, GlyphBox * star.Fill, GlyphBox)))
                    {
                        context.DrawGeometry(FillBrush, _fillPen, geometry);
                    }
                }
            }
        }
    }
}

/// <summary>Text whose changed words roll in the direction the value moved; unchanged words stay put.</summary>
internal sealed class RatingRollingText : StackPanel
{
    private string? _text;

    public RatingRollingText()
    {
        Orientation = Avalonia.Layout.Orientation.Horizontal;
    }

    /// <summary>The text shown.</summary>
    internal string? Text => _text;

    /// <summary>Shows <paramref name="text"/>; <paramref name="direction"/> is 1 when the value grew, -1 when it shrank.</summary>
    internal void Show(string? text, int direction)
    {
        if (_text == text)
        {
            return;
        }
        _text = text;
        var words = string.IsNullOrEmpty(text) ? [] : text.Split(' ');
        while (Children.Count > words.Length)
        {
            Children.RemoveAt(Children.Count - 1);
        }
        for (var i = 0; i < words.Length; i++)
        {
            if (i < Children.Count)
            {
                var word = (ArcContentSwap)Children[i];
                if (!Equals(word.Content, words[i]))
                {
                    word.Direction = direction;
                    word.Content = words[i];
                }
            }
            else
            {
                Children.Add(new ArcContentSwap { Mode = ContentSwapMode.Roll, Direction = direction, Content = words[i] });
            }
        }
    }
}
