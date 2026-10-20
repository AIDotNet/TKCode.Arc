using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The drawing pad of <see cref="SignaturePad"/>: keeps the 600 × 260 ratio of the ink coordinates, draws the guide
/// (mark, dashed baseline, signer), the resting hint, and an ink layer above them. Pointer input is captured on press
/// so touch drawing never scrolls the page.
/// </summary>
[PseudoClasses(DrawingPseudoClass)]
internal sealed class SignaturePadCanvas : Control
{
    public const string DrawingPseudoClass = ":drawing";

    public static readonly StyledProperty<IBrush?> BackgroundProperty = AvaloniaProperty.Register<SignaturePadCanvas, IBrush?>(nameof(Background));
    public static readonly StyledProperty<IBrush?> BorderBrushProperty = AvaloniaProperty.Register<SignaturePadCanvas, IBrush?>(nameof(BorderBrush));
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty = AvaloniaProperty.Register<SignaturePadCanvas, CornerRadius>(nameof(CornerRadius));
    public static readonly StyledProperty<IBrush?> GuideBrushProperty = AvaloniaProperty.Register<SignaturePadCanvas, IBrush?>(nameof(GuideBrush));
    public static readonly StyledProperty<IBrush?> MutedBrushProperty = AvaloniaProperty.Register<SignaturePadCanvas, IBrush?>(nameof(MutedBrush));
    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty = AvaloniaProperty.Register<SignaturePadCanvas, IBrush?>(nameof(FocusRingBrush));
    public static readonly StyledProperty<IBrush?> InkBlackProperty = AvaloniaProperty.Register<SignaturePadCanvas, IBrush?>(nameof(InkBlack));
    public static readonly StyledProperty<IBrush?> InkBlueProperty = AvaloniaProperty.Register<SignaturePadCanvas, IBrush?>(nameof(InkBlue));
    public static readonly StyledProperty<IBrush?> InkVioletProperty = AvaloniaProperty.Register<SignaturePadCanvas, IBrush?>(nameof(InkViolet));
    public static readonly StyledProperty<IBrush?> SweepBrushProperty = AvaloniaProperty.Register<SignaturePadCanvas, IBrush?>(nameof(SweepBrush));
    public static readonly StyledProperty<double> HintFontSizeProperty = AvaloniaProperty.Register<SignaturePadCanvas, double>(nameof(HintFontSize), 18);
    public static readonly StyledProperty<double> SignerFontSizeProperty = AvaloniaProperty.Register<SignaturePadCanvas, double>(nameof(SignerFontSize), 12);
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = Avalonia.Controls.Documents.TextElement.FontFamilyProperty.AddOwner<SignaturePadCanvas>();

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public IBrush? GuideBrush { get => GetValue(GuideBrushProperty); set => SetValue(GuideBrushProperty, value); }
    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }
    public IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }
    public IBrush? InkBlack { get => GetValue(InkBlackProperty); set => SetValue(InkBlackProperty, value); }
    public IBrush? InkBlue { get => GetValue(InkBlueProperty); set => SetValue(InkBlueProperty, value); }
    public IBrush? InkViolet { get => GetValue(InkVioletProperty); set => SetValue(InkVioletProperty, value); }
    public IBrush? SweepBrush { get => GetValue(SweepBrushProperty); set => SetValue(SweepBrushProperty, value); }
    public double HintFontSize { get => GetValue(HintFontSizeProperty); set => SetValue(HintFontSizeProperty, value); }
    public double SignerFontSize { get => GetValue(SignerFontSizeProperty); set => SetValue(SignerFontSizeProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    private readonly ArcGlyph _mark = new() { Data = ArcIcons.X, Size = 14, StrokeThickness = 1.75, IsHitTestVisible = false };
    private readonly TextBlock _hint = new() { IsHitTestVisible = false };
    private readonly TextBlock _signer = new() { IsHitTestVisible = false };
    private readonly MatrixTransform _hintTransform = new();
    private readonly BlurEffect _hintBlur = new() { Radius = 0 };
    private readonly MotionValue<double> _hintProgress;
    private bool _hintShown = true;
    private bool _hintBlurs;
    private IPen? _borderPen;
    private IPen? _guidePen;
    private IPen? _focusPen;

    static SignaturePadCanvas()
    {
        FocusableProperty.OverrideDefaultValue<SignaturePadCanvas>(true);
        CursorProperty.OverrideDefaultValue<SignaturePadCanvas>(new Cursor(StandardCursorType.Cross));
        AffectsRender<SignaturePadCanvas>(BackgroundProperty, BorderBrushProperty, CornerRadiusProperty, GuideBrushProperty, FocusRingBrushProperty);
    }

    public SignaturePadCanvas()
    {
        Ink = new SignaturePadInkLayer(this);
        _hint.RenderTransform = _hintTransform;
        _hint.RenderTransformOrigin = RelativePoint.Center;
        _hintProgress = new MotionValue<double>(this, 1, MotionVectors.Double);
        _hintProgress.Changed += ApplyHint;
        _hint.FontSize = HintFontSize;
        _signer.FontSize = SignerFontSize;
        foreach (var child in new Control[] { _mark, _hint, _signer, Ink })
        {
            VisualChildren.Add(child);
            LogicalChildren.Add(child);
        }
    }

    internal SignaturePad? Owner { get; set; }

    internal SignaturePadInkLayer Ink { get; }

    internal TextBlock HintText => _hint;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BorderBrushProperty)
        {
            _borderPen = null;
        }
        else if (change.Property == GuideBrushProperty)
        {
            _guidePen = null;
        }
        else if (change.Property == FocusRingBrushProperty)
        {
            _focusPen = null;
        }
        else if (change.Property == MutedBrushProperty)
        {
            _mark.Foreground = MutedBrush;
            _hint.Foreground = MutedBrush;
            _signer.Foreground = MutedBrush;
        }
        else if (change.Property == HintFontSizeProperty)
        {
            _hint.FontSize = HintFontSize;
        }
        else if (change.Property == SignerFontSizeProperty)
        {
            _signer.FontSize = SignerFontSize;
        }
        else if (change.Property == FontFamilyProperty)
        {
            _hint.FontFamily = FontFamily;
            _signer.FontFamily = FontFamily;
        }
        else if (change.Property == InkBlackProperty || change.Property == InkBlueProperty || change.Property == InkVioletProperty || change.Property == SweepBrushProperty)
        {
            Ink.InvalidateVisual();
        }
    }

    public void SetDrawing(bool drawing) => PseudoClasses.Set(DrawingPseudoClass, drawing);

    public void SetTexts(string? signer, string hint)
    {
        _signer.Text = signer;
        _signer.IsVisible = !string.IsNullOrEmpty(signer);
        _hint.Text = hint;
    }

    public IBrush? InkBrush(SignatureInkColor color) => color switch
    {
        SignatureInkColor.Blue => InkBlue,
        SignatureInkColor.Violet => InkViolet,
        _ => InkBlack
    };

    /// <summary>
    /// Shows or hides the resting hint: it rises 6px and unblurs on the standard tween in, and leaves faster than it came.
    /// Code driven because the enter and exit use different durations and easings, and the blur and scale ride along.
    /// </summary>
    public void SetHintShown(bool shown, bool animate)
    {
        if (shown == _hintShown)
        {
            return;
        }
        _hintShown = shown;
        var level = animate ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        _hintBlurs = level == ArcMotionLevel.Full;
        if (level == ArcMotionLevel.Off)
        {
            _hintProgress.Jump(shown ? 1 : 0);
            return;
        }
        var spec = level == ArcMotionLevel.Reduced
            ? MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)
            : shown ? MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter) : MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);
        _ = _hintProgress.AnimateTo(shown ? 1 : 0, spec);
    }

    public bool IsHintShown => _hintShown;

    private void ApplyHint(double p)
    {
        _hint.Opacity = p;
        _hint.IsVisible = p > 0.001;
        if (_hintBlurs)
        {
            var scale = 0.97 + 0.03 * p;
            _hintTransform.Matrix = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(0, (1 - p) * 6);
            _hintBlur.Radius = (1 - p) * 4;
            _hint.Effect = p < 0.999 ? _hintBlur : null;
        }
        else
        {
            _hintTransform.Matrix = Matrix.Identity;
            _hint.Effect = null;
        }
    }

    /// <summary>The pad keeps Arc's 600 : 260 aspect ratio at every width.</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : SignatureExport.PadWidth;
        foreach (var child in new Control[] { _mark, _hint, _signer, Ink })
        {
            child.Measure(Size.Infinity);
        }
        return new Size(width, width * SignatureExport.PadHeight / SignatureExport.PadWidth);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var baseline = finalSize.Height * 0.72;
        var left = finalSize.Width * 0.07;
        _mark.Arrange(new Rect(left, baseline - 6 - 14, 14, 14));
        var hint = _hint.DesiredSize;
        _hint.Arrange(new Rect((finalSize.Width - hint.Width) / 2, baseline - 8 - hint.Height, hint.Width, hint.Height));
        _signer.Arrange(new Rect(left, baseline + 8, Math.Max(0, finalSize.Width - left * 2), _signer.DesiredSize.Height));
        Ink.Arrange(new Rect(finalSize));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var radius = CornerRadius;
        _borderPen ??= BorderBrush is { } border ? new ImmutablePen(border.ToImmutable(), 1) : null;
        context.DrawRectangle(Background, _borderPen, new RoundedRect(bounds.Deflate(0.5), radius));
        if (GuideBrush is { } guide)
        {
            // Arc: a 1.5px dashed border-top from 7% to 93% at 72% of the height.
            _guidePen ??= new ImmutablePen(guide.ToImmutable(), 1.5, new ImmutableDashStyle(new double[] { 2, 2 }, 0));
            var y = Math.Round(bounds.Height * 0.72) + 0.75;
            context.DrawLine(_guidePen, new Point(bounds.Width * 0.07, y), new Point(bounds.Width * 0.93, y));
        }
        if (IsFocused && Classes.Contains(":focus-visible") && FocusRingBrush is { } ring)
        {
            _focusPen ??= new ImmutablePen(ring.ToImmutable(), 2);
            var outer = new CornerRadius(radius.TopLeft + 3, radius.TopRight + 3, radius.BottomRight + 3, radius.BottomLeft + 3);
            context.DrawRectangle(null, _focusPen, new RoundedRect(bounds.Inflate(3), outer));
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

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Owner?.OnPadPointerPressed(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Owner?.OnPadPointerMoved(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        Owner?.OnPadPointerReleased(e.Pointer);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        Owner?.OnPadPointerReleased(e.Pointer);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled)
        {
            Owner?.OnPadKeyDown(e);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SignaturePadCanvasAutomationPeer(this);

    /// <summary>Converts a point in the pad to pad units.</summary>
    public Point ToPad(Point point) => new(
        Bounds.Width > 0 ? point.X / Bounds.Width * SignatureExport.PadWidth : 0,
        Bounds.Height > 0 ? point.Y / Bounds.Height * SignatureExport.PadHeight : 0);
}

/// <summary>The pad is an image of the signature for assistive technology, named with its stroke count.</summary>
internal sealed class SignaturePadCanvasAutomationPeer(SignaturePadCanvas owner) : ControlAutomationPeer(owner)
{
    private SignaturePad? Pad => ((SignaturePadCanvas)Owner).Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

    protected override string GetLocalizedControlTypeCore() => "signature pad";

    protected override string? GetNameCore() => Pad?.AccessibleDescription ?? base.GetNameCore();

    protected override string? GetHelpTextCore() => Pad?.KeyboardHelp;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}

/// <summary>
/// The ink: committed strokes from cached geometry, the stroke being drawn, the replay nib and the clear wipe.
/// Only this layer repaints while drawing; the guide underneath stays as it was recorded.
/// </summary>
internal sealed class SignaturePadInkLayer : Control
{
    private readonly SignaturePadCanvas _canvas;
    private readonly Dictionary<InkStroke, Geometry> _geometry = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<InkStroke> _appearing = new(ReferenceEqualityComparer.Instance);
    private readonly List<InkStroke> _vanishing = new();
    private readonly MotionValue<double> _appear;
    private readonly MotionValue<double> _vanish;
    private IReadOnlyList<InkStroke> _strokes = Array.Empty<InkStroke>();
    private Color _sweepColor;
    private IBrush? _sweepTrail;
    private IBrush? _sweepEdge;
    private Geometry? _live;
    private SignatureInkColor _liveColor;

    public SignaturePadInkLayer(SignaturePadCanvas canvas)
    {
        _canvas = canvas;
        IsHitTestVisible = false;
        ClipToBounds = true;
        _appear = new MotionValue<double>(this, 1, MotionVectors.Double);
        _vanish = new MotionValue<double>(this, 0, MotionVectors.Double);
        _appear.Changed += _ => InvalidateVisual();
        _vanish.Changed += value =>
        {
            if (value <= 0.001)
            {
                _vanishing.Clear();
            }
            InvalidateVisual();
        };
    }

    /// <summary>Replay state: the playhead in ms and each stroke's start on the timeline; null when not replaying.</summary>
    public (double At, double[] Offsets)? Replay { get; set; }

    /// <summary>Clear wipe progress from 0 to 1; null when not wiping.</summary>
    public double? Wipe { get; set; }

    /// <summary>The nib size (in pad units) of the replay dot: Arc sizes it by the selected width.</summary>
    public double NibSize { get; set; } = 5.6;

    public IReadOnlyList<InkStroke> Strokes => _strokes;

    /// <summary>Shows a new stroke list. Strokes that come back (undo, redo) fade in; strokes that go fade out, except the one just drawn and anything wiped.</summary>
    public void SetStrokes(IReadOnlyList<InkStroke> strokes, InkStroke? fresh, bool wiped)
    {
        var previous = _strokes;
        _strokes = strokes;
        var before = new HashSet<InkStroke>(previous, ReferenceEqualityComparer.Instance);
        var after = new HashSet<InkStroke>(strokes, ReferenceEqualityComparer.Instance);
        _appearing.Clear();
        foreach (var stroke in strokes)
        {
            if (!before.Contains(stroke) && !ReferenceEquals(stroke, fresh))
            {
                _appearing.Add(stroke);
            }
        }
        _vanishing.Clear();
        if (!wiped)
        {
            foreach (var stroke in previous)
            {
                if (!after.Contains(stroke))
                {
                    _vanishing.Add(stroke);
                }
            }
        }
        foreach (var key in _geometry.Keys.Where(key => !after.Contains(key) && !_vanishing.Contains(key)).ToList())
        {
            _geometry.Remove(key);
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (_appearing.Count > 0)
        {
            _appear.Jump(0);
            _ = _appear.AnimateTo(1, level == ArcMotionLevel.Off ? MotionSpec.Instant : MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
        }
        if (_vanishing.Count > 0)
        {
            _vanish.Jump(1);
            _ = _vanish.AnimateTo(0, level == ArcMotionLevel.Off ? MotionSpec.Instant : MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
        }
        InvalidateVisual();
    }

    /// <summary>Sets the outline of the stroke being drawn (rebuilt per pointer move, not per frame).</summary>
    public void SetLive(Geometry? geometry, SignatureInkColor color)
    {
        _live = geometry;
        _liveColor = color;
        InvalidateVisual();
    }

    private Geometry GeometryOf(InkStroke stroke)
    {
        if (!_geometry.TryGetValue(stroke, out var geometry))
        {
            geometry = SignatureExport.ToGeometry(stroke);
            _geometry[stroke] = geometry;
        }
        return geometry;
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        var wipe = Wipe;
        var x0 = wipe is { } w ? size.Width * w : 0;
        using (context.PushClip(new Rect(x0, 0, Math.Max(0, size.Width - x0), size.Height)))
        using (context.PushTransform(Matrix.CreateScale(size.Width / SignatureExport.PadWidth, size.Height / SignatureExport.PadHeight)))
        {
            foreach (var stroke in _vanishing)
            {
                using (context.PushOpacity(_vanish.Value))
                {
                    context.DrawGeometry(_canvas.InkBrush(stroke.Color), null, GeometryOf(stroke));
                }
            }
            InkPoint? nib = null;
            var nibColor = SignatureInkColor.Black;
            for (var i = 0; i < _strokes.Count; i++)
            {
                var stroke = _strokes[i];
                var brush = _canvas.InkBrush(stroke.Color);
                if (Replay is { } replay && stroke.Points.Count > 0)
                {
                    var local = replay.At - (i < replay.Offsets.Length ? replay.Offsets[i] : 0);
                    if (local < 0)
                    {
                        continue;
                    }
                    if (local < stroke.Points[^1].Time)
                    {
                        var (points, _) = SignatureExport.PointsUpTo(stroke, local);
                        context.DrawGeometry(brush, null, SignatureExport.ToGeometry(SignatureExport.GetStrokeOutline(points, SignatureExport.OptionsFor(stroke.Width, points, false))));
                        nib = points.Count > 0 ? points[^1] : stroke.Points[0];
                        nibColor = stroke.Color;
                        continue;
                    }
                }
                if (_appearing.Contains(stroke) && _appear.Value < 0.999)
                {
                    using (context.PushOpacity(_appear.Value))
                    {
                        context.DrawGeometry(brush, null, GeometryOf(stroke));
                    }
                    continue;
                }
                context.DrawGeometry(brush, null, GeometryOf(stroke));
            }
            if (_live is { } live)
            {
                context.DrawGeometry(_canvas.InkBrush(_liveColor), null, live);
            }
            if (nib is { } point)
            {
                using (context.PushOpacity(0.35))
                {
                    var r = NibSize * 0.9;
                    context.DrawEllipse(_canvas.InkBrush(nibColor), null, new Point(point.X, point.Y), r, r);
                }
            }
        }
        if (wipe is { } progress)
        {
            DrawSweep(context, size, progress);
        }
    }

    /// <summary>The squeegee edge that clears the ink as it crosses the pad: a soft trail and a thin bright edge.</summary>
    private void DrawSweep(DrawingContext context, Size size, double progress)
    {
        if (_canvas.SweepBrush is not ISolidColorBrush sweep)
        {
            return;
        }
        // Opacity keyframes [0, 1, 1, 0] at [0, .12, .85, 1].
        var opacity = progress < 0.12 ? progress / 0.12 : progress > 0.85 ? Math.Max(0, (1 - progress) / 0.15) : 1;
        var x = size.Width * progress;
        var color = sweep.Color;
        if (_sweepTrail is null || _sweepColor != color)
        {
            _sweepColor = color;
            _sweepTrail = new ImmutableLinearGradientBrush(new[]
            {
                new ImmutableGradientStop(0, Color.FromArgb(0, color.R, color.G, color.B)),
                new ImmutableGradientStop(1, Color.FromArgb((byte)(color.A * 0.05), color.R, color.G, color.B))
            }, 1, null, null, GradientSpreadMethod.Pad, new RelativePoint(0, 0, RelativeUnit.Relative), new RelativePoint(1, 0, RelativeUnit.Relative));
            _sweepEdge = new ImmutableSolidColorBrush(color, 0.5);
        }
        using (context.PushOpacity(opacity))
        {
            context.FillRectangle(_sweepTrail, new Rect(x - 28, 0, 28, size.Height));
            context.DrawRectangle(_sweepEdge, null, new RoundedRect(new Rect(x - 1, size.Height * 0.08, 2, size.Height * 0.84), 2));
        }
    }
}

/// <summary>A round icon button of the signature toolbar (36px pill); <see cref="IsChecked"/> is Arc's aria-pressed.</summary>
[PseudoClasses(CheckedPseudoClass)]
internal sealed class SignaturePadIconButton : Avalonia.Controls.Button
{
    public const string CheckedPseudoClass = ":checked";

    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<SignaturePadIconButton, bool>(nameof(IsChecked));

    public bool IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    protected override Type StyleKeyOverride => typeof(SignaturePadIconButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty)
        {
            PseudoClasses.Set(CheckedPseudoClass, IsChecked);
        }
    }
}

/// <summary>One option of a <see cref="SignaturePadChoice"/>: a 36px round radio button.</summary>
internal sealed class SignaturePadOption : Avalonia.Controls.RadioButton
{
    protected override Type StyleKeyOverride => typeof(SignaturePadOption);
}

/// <summary>
/// A compact radio group with a highlight that glides to the chosen option (Arc's shared layout highlight on the
/// morph spring). Only the chosen option is a tab stop; arrow keys move the choice.
/// </summary>
internal sealed class SignaturePadChoice : Panel
{
    public static readonly StyledProperty<IBrush?> HighlightFillProperty = AvaloniaProperty.Register<SignaturePadChoice, IBrush?>(nameof(HighlightFill));
    public static readonly StyledProperty<IBrush?> HighlightStrokeProperty = AvaloniaProperty.Register<SignaturePadChoice, IBrush?>(nameof(HighlightStroke));

    public IBrush? HighlightFill { get => GetValue(HighlightFillProperty); set => SetValue(HighlightFillProperty, value); }
    public IBrush? HighlightStroke { get => GetValue(HighlightStrokeProperty); set => SetValue(HighlightStrokeProperty, value); }

    private readonly Primitives.GlidingIndicator _indicator = new() { Spring = ArcSpring.Morph, CornerRadius = new CornerRadius(18), StrokeThickness = 1, IsHitTestVisible = false };
    private readonly List<SignaturePadOption> _options = new();
    private int _selected = -1;
    private bool _placed;

    public SignaturePadChoice()
    {
        Children.Add(_indicator);
        KeyDown += OnKeyDown;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new EditorsGroupAutomationPeer(this, AutomationControlType.Group);

    /// <summary>Raised with the index chosen by click or keyboard.</summary>
    public event Action<int>? Chosen;

    public IReadOnlyList<SignaturePadOption> Options => _options;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HighlightFillProperty)
        {
            _indicator.Fill = HighlightFill;
        }
        else if (change.Property == HighlightStrokeProperty)
        {
            _indicator.Stroke = HighlightStroke;
        }
    }

    public void SetOptions(IEnumerable<(Control Content, string Name)> options)
    {
        foreach (var option in _options)
        {
            Children.Remove(option);
        }
        _options.Clear();
        foreach (var (content, name) in options)
        {
            var option = new SignaturePadOption { Content = content };
            Avalonia.Automation.AutomationProperties.SetName(option, name);
            Tooltip.SetTip(option, name);
            var index = _options.Count;
            option.Click += (_, _) => Chosen?.Invoke(index);
            _options.Add(option);
            Children.Add(option);
        }
        Select(_selected < 0 ? 0 : Math.Min(_selected, _options.Count - 1), animate: false);
    }

    public int Selected => _selected;

    public void Select(int index, bool animate)
    {
        if (index < 0 || index >= _options.Count)
        {
            return;
        }
        _selected = index;
        for (var i = 0; i < _options.Count; i++)
        {
            _options[i].IsChecked = i == index;
            _options[i].IsTabStop = i == index;
        }
        PlaceIndicator(animate && _placed);
    }

    private void PlaceIndicator(bool animate)
    {
        if (_selected < 0 || _selected >= _options.Count)
        {
            return;
        }
        var option = _options[_selected];
        if (option.Bounds.Width <= 0)
        {
            return;
        }
        _indicator.MoveTo(option.Bounds, animate);
        _placed = true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var step = e.Key is Key.Right or Key.Down ? 1 : e.Key is Key.Left or Key.Up ? -1 : 0;
        if (step == 0 || _options.Count == 0)
        {
            return;
        }
        e.Handled = true;
        var index = (_selected + step + _options.Count) % _options.Count;
        Chosen?.Invoke(index);
        _options[index].Focus(NavigationMethod.Directional);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0;
        double height = 0;
        foreach (var option in _options)
        {
            option.Measure(Size.Infinity);
            width += option.DesiredSize.Width;
            height = Math.Max(height, option.DesiredSize.Height);
        }
        width += Math.Max(0, _options.Count - 1) * 2;
        _indicator.Measure(Size.Infinity);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0;
        foreach (var option in _options)
        {
            option.Arrange(new Rect(x, 0, option.DesiredSize.Width, finalSize.Height));
            x += option.DesiredSize.Width + 2;
        }
        _indicator.Arrange(new Rect(finalSize));
        if (!_placed)
        {
            PlaceIndicator(animate: false);
        }
        return finalSize;
    }
}

/// <summary>Lays the signature toolbar's two groups at either end of a row, wrapping the second under the first when narrow (Arc's flex wrap with space-between).</summary>
internal sealed class SignaturePadToolbarPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0;
        double height = 0;
        foreach (var child in Children)
        {
            child.Measure(Size.Infinity);
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }
        width += Math.Max(0, Children.Count - 1) * 8;
        if (double.IsFinite(availableSize.Width) && width > availableSize.Width && Children.Count > 1)
        {
            return new Size(availableSize.Width, Children.Sum(c => c.DesiredSize.Height) + 8 * (Children.Count - 1));
        }
        return new Size(double.IsFinite(availableSize.Width) ? Math.Max(width, availableSize.Width) : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0)
        {
            return finalSize;
        }
        var total = Children.Sum(c => c.DesiredSize.Width) + 8 * (Children.Count - 1);
        if (total > finalSize.Width && Children.Count > 1)
        {
            double y = 0;
            foreach (var child in Children)
            {
                child.Arrange(new Rect(0, y, child.DesiredSize.Width, child.DesiredSize.Height));
                y += child.DesiredSize.Height + 8;
            }
            return finalSize;
        }
        var first = Children[0];
        first.Arrange(new Rect(0, (finalSize.Height - first.DesiredSize.Height) / 2, first.DesiredSize.Width, first.DesiredSize.Height));
        var last = Children[^1];
        if (!ReferenceEquals(first, last))
        {
            last.Arrange(new Rect(finalSize.Width - last.DesiredSize.Width, (finalSize.Height - last.DesiredSize.Height) / 2, last.DesiredSize.Width, last.DesiredSize.Height));
        }
        return finalSize;
    }
}
