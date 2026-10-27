using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The surface of a <see cref="DeveloperMorphPanel"/>: one rounded rectangle whose width and height follow the same
/// <see cref="MotionValue{T}"/> (Arc springs the inline size of one element, it is not a two-element FLIP). Closed it is as
/// big as its <see cref="Face"/>; open it takes the whole available width and the face plus the <see cref="Body"/>
/// measured at that width. The parent right-aligns it, so it grows leftward from its right edge and the face rides the
/// left edge. Content is laid out once at the final width and clipped to the moving rounded rectangle (no reflow per
/// frame); while closing, the body keeps the open width until it has faded. Code-driven because the target size is
/// measured at run time and a retarget (an error line appearing mid-morph) must keep the velocity.
/// </summary>
internal sealed class DeveloperMorphShell : Control
{
    /// <summary>The row that is always shown (toggle and title, 36px high).</summary>
    public static readonly StyledProperty<Control?> FaceProperty =
        AvaloniaProperty.Register<DeveloperMorphShell, Control?>(nameof(Face));

    /// <summary>The content below the face while open (its visibility is driven by the panel).</summary>
    public static readonly StyledProperty<Control?> BodyProperty =
        AvaloniaProperty.Register<DeveloperMorphShell, Control?>(nameof(Body));

    /// <summary>Open: full width, face + body high. Closed: the face.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<DeveloperMorphShell, bool>(nameof(IsOpen));

    /// <summary>Fill of the surface.</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        Border.BackgroundProperty.AddOwner<DeveloperMorphShell>();

    /// <summary>Corner radius of the surface (Arc 18, open and closed).</summary>
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<DeveloperMorphShell>();

    private readonly RectangleGeometry _clip = new();
    private MotionValue<Size>? _size;
    private Size _target;
    private double _lastAvailableWidth = double.NaN;
    private double _bodyWidth;
    private bool _openChanged;

    static DeveloperMorphShell()
    {
        AffectsMeasure<DeveloperMorphShell>(IsOpenProperty);
        AffectsRender<DeveloperMorphShell>(BackgroundProperty, CornerRadiusProperty);
    }

    /// <inheritdoc cref="FaceProperty"/>
    public Control? Face { get => GetValue(FaceProperty); set => SetValue(FaceProperty, value); }

    /// <inheritdoc cref="BodyProperty"/>
    public Control? Body { get => GetValue(BodyProperty); set => SetValue(BodyProperty, value); }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="BackgroundProperty"/>
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <inheritdoc cref="CornerRadiusProperty"/>
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>Spring of the morph when it opens (and of size changes while open; default Smooth).</summary>
    public SpringSpec OpenSpring { get; set; } = DeveloperSprings.Smooth;

    /// <summary>Spring of the morph when it closes (default Smooth; Arc's close fit is softer but noisy).</summary>
    public SpringSpec CloseSpring { get; set; } = DeveloperSprings.Smooth;

    /// <summary>The size the surface is drawn at right now.</summary>
    public Size CurrentSize => _size?.Value ?? _target;

    /// <summary>The size the surface is heading to.</summary>
    public Size TargetSize => _target;

    /// <summary>True while the size springs.</summary>
    public bool IsMoving => _size?.IsAnimating == true;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FaceProperty || change.Property == BodyProperty)
        {
            if (change.OldValue is Control old)
            {
                VisualChildren.Remove(old);
                LogicalChildren.Remove(old);
            }
            if (change.NewValue is Control added)
            {
                LogicalChildren.Add(added);
                VisualChildren.Add(added);
            }
            InvalidateMeasure();
        }
        else if (change.Property == IsOpenProperty)
        {
            _openChanged = true;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var face = Face;
        var body = Body;
        face?.Measure(Size.Infinity);
        var faceSize = face?.DesiredSize ?? default;
        var finite = !double.IsInfinity(availableSize.Width);
        var bodyShown = body is { IsVisible: true };
        if (IsOpen || !bodyShown)
        {
            _bodyWidth = finite ? availableSize.Width : Math.Max(faceSize.Width, _bodyWidth);
        }
        if (bodyShown)
        {
            body!.Measure(new Size(_bodyWidth, double.PositiveInfinity));
        }
        if (!finite && IsOpen && bodyShown)
        {
            _bodyWidth = Math.Max(faceSize.Width, body!.DesiredSize.Width);
        }
        var target = IsOpen
            ? new Size(_bodyWidth, faceSize.Height + (bodyShown ? body!.DesiredSize.Height : 0))
            : faceSize;

        var passive = !_openChanged && !double.IsNaN(_lastAvailableWidth) && Math.Abs(_lastAvailableWidth - availableSize.Width) > 0.5;
        _lastAvailableWidth = availableSize.Width;
        _openChanged = false;
        if (_size is null)
        {
            _target = target;
            _size = new MotionValue<Size>(this, target, MotionVectors.Size);
            _size.Changed += _ =>
            {
                InvalidateMeasure();
                InvalidateArrange();
            };
        }
        else if (!Same(target, _target))
        {
            _target = target;
            if (!IsLoaded || passive || !ArcMotion.IsFull(this))
            {
                _size.Jump(target);
            }
            else
            {
                // A retarget while moving keeps the velocity (MotionValue); the spring follows the direction of travel.
                _ = _size.AnimateTo(target, MotionSpec.Spring(IsOpen ? OpenSpring : CloseSpring));
            }
        }
        var current = _size.Value;
        return new Size(Math.Min(current.Width, availableSize.Width), current.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var current = _size?.Value ?? finalSize;
        if (Face is { } face)
        {
            face.Arrange(new Rect(0, 0, face.DesiredSize.Width, face.DesiredSize.Height));
        }
        if (Body is { IsVisible: true } body)
        {
            var top = Face?.DesiredSize.Height ?? 0;
            body.Arrange(new Rect(0, top, Math.Max(_bodyWidth, current.Width), body.DesiredSize.Height));
        }
        // Clip to the moving rounded rectangle only while it moves (or while the body still has the open width); at rest
        // the content fits, and an unclipped surface keeps focus rings whole.
        var clips = IsMoving || (Body is { IsVisible: true } && _bodyWidth > current.Width + 0.5);
        if (clips)
        {
            var radius = CornerRadius.TopLeft;
            _clip.Rect = new Rect(0, 0, Math.Min(current.Width, finalSize.Width), current.Height);
            _clip.RadiusX = radius;
            _clip.RadiusY = radius;
            Clip = _clip;
        }
        else
        {
            Clip = null;
        }
        InvalidateVisual();
        return new Size(Math.Min(current.Width, finalSize.Width), current.Height);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Background is not { } background)
        {
            return;
        }
        var current = _size?.Value ?? Bounds.Size;
        var radius = CornerRadius.TopLeft;
        context.DrawRectangle(background, null, new Rect(0, 0, Math.Min(current.Width, Bounds.Width), current.Height), radius, radius);
    }

    private static bool Same(Size a, Size b) => Math.Abs(a.Width - b.Width) < 0.5 && Math.Abs(a.Height - b.Height) < 0.5;
}
