using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Presence with a collapsing size (Arc P6 + AnimatePresence): opening grows the frame from zero to its content
/// size on a spring while the content fades in; closing fades it out while the size springs back to zero, so the
/// content around closes the gap instead of jumping. It clips only while moving or closed, so shadows and focus
/// rings stay whole at rest. The first state never animates (<c>initial={false}</c>).
/// <para>
/// Code-driven because the owner needs the completion (<see cref="Settled"/>) to raise its own events after the
/// collapse, and size and opacity run on different curves, which a ControlTheme transition cannot express.
/// </para>
/// </summary>
internal sealed class MessagesPresenceFrame : Decorator
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<MessagesPresenceFrame, bool>(nameof(IsOpen), true);

    /// <summary>Vertical collapses the height (default), Horizontal the width.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<MessagesPresenceFrame, Orientation>(nameof(Orientation), Orientation.Vertical);

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Vertical collapses the height (default), Horizontal the width.</summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Spring for the size (default Arc smooth).</summary>
    public SpringSpec SizeSpring { get; set; } = ArcMotionPresets.Smooth;

    /// <summary>Opacity on open (default standard 240ms on the enter curve).</summary>
    public TimeSpan EnterFade { get; set; } = ArcMotionPresets.Standard;

    public Easing EnterEasing { get; set; } = ArcEasings.Enter;

    /// <summary>Opacity on close (default fast 160ms on the standard curve).</summary>
    public TimeSpan ExitFade { get; set; } = ArcMotionPresets.Fast;

    /// <summary>Under reduced motion the size jumps and only this fade plays (zero: everything is instant).</summary>
    public TimeSpan ReducedFade { get; set; } = TimeSpan.Zero;

    /// <summary>Raised when an open or close finished (the argument is the final open state).</summary>
    public event EventHandler<bool>? Settled;

    /// <summary>Current size fraction (0 closed, 1 open). Exposed for tests.</summary>
    internal double SizeFraction => _size?.Value ?? (IsOpen ? 1 : 0);

    private MotionValue<double>? _size;
    private MotionValue<double>? _fade;
    private RectangleGeometry? _clip;
    private bool _initialized;
    private int _generation;
    private static readonly Easing s_standard = ArcEasings.Standard;

    static MessagesPresenceFrame()
    {
        AffectsMeasure<MessagesPresenceFrame>(OrientationProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            Run(change.GetNewValue<bool>());
        }
        else if (change.Property == ChildProperty && change.NewValue is Control child && _initialized)
        {
            child.IsVisible = IsOpen || (_size?.IsAnimating ?? false);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_initialized)
        {
            _initialized = true;
            Snap(IsOpen);
        }
    }

    private void Snap(bool open)
    {
        _generation++;
        EnsureValues();
        _size!.Jump(open ? 1 : 0);
        _fade!.Jump(open ? 1 : 0);
        if (Child is { } child)
        {
            child.IsVisible = open;
        }
        InvalidateMeasure();
    }

    private void EnsureValues()
    {
        if (_size is not null)
        {
            return;
        }
        _size = new MotionValue<double>(this, IsOpen ? 1 : 0, MotionVectors.Double);
        _size.Changed += _ => InvalidateMeasure();
        _fade = new MotionValue<double>(this, IsOpen ? 1 : 0, MotionVectors.Double);
        _fade.Changed += v => Opacity = Math.Clamp(v, 0, 1);
    }

    private void Run(bool open)
    {
        if (!_initialized || !IsLoaded)
        {
            if (_initialized)
            {
                Snap(open);
                Settled?.Invoke(this, open);
            }
            return;
        }
        EnsureValues();
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off || (level == ArcMotionLevel.Reduced && ReducedFade <= TimeSpan.Zero))
        {
            Snap(open);
            Settled?.Invoke(this, open);
            return;
        }
        var generation = ++_generation;
        if (open && Child is { } child)
        {
            child.IsVisible = true;
        }
        Task<bool> sizeTask;
        Task<bool> fadeTask;
        if (level == ArcMotionLevel.Reduced)
        {
            if (open)
            {
                _size!.Jump(1);
            }
            sizeTask = Task.FromResult(true);
            fadeTask = _fade!.AnimateTo(open ? 1 : 0, MotionSpec.Tween(ReducedFade, s_standard));
        }
        else
        {
            sizeTask = _size!.AnimateTo(open ? 1 : 0, MotionSpec.Spring(SizeSpring));
            fadeTask = _fade!.AnimateTo(open ? 1 : 0, open
                ? MotionSpec.Tween(EnterFade, EnterEasing)
                : MotionSpec.Tween(ExitFade, s_standard));
        }
        InvalidateMeasure();
        Complete(generation, open, sizeTask, fadeTask);
    }

    private void Complete(int generation, bool open, Task<bool> sizeTask, Task<bool> fadeTask)
    {
        Task.WhenAll(sizeTask, fadeTask).ThenOnUiThread(() =>
        {
            if (generation != _generation)
            {
                return;
            }
            if (!open)
            {
                _size?.Jump(0);
                if (Child is { } child)
                {
                    child.IsVisible = false;
                }
            }
            InvalidateMeasure();
            Settled?.Invoke(this, open);
        });
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var child = Child;
        if (child is null)
        {
            return default;
        }
        var vertical = Orientation == Orientation.Vertical;
        child.Measure(vertical ? availableSize.WithHeight(double.PositiveInfinity) : availableSize.WithWidth(double.PositiveInfinity));
        var desired = child.IsVisible ? child.DesiredSize : default;
        var fraction = Math.Max(0, _size?.Value ?? (IsOpen ? 1 : 0));
        var result = vertical ? desired.WithHeight(desired.Height * fraction) : desired.WithWidth(desired.Width * fraction);
        UpdateClip(result, fraction);
        return new Size(Math.Min(result.Width, availableSize.Width), Math.Min(result.Height, availableSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var child = Child;
        if (child is null)
        {
            return finalSize;
        }
        var desired = child.DesiredSize;
        var rect = Orientation == Orientation.Vertical
            ? new Rect(0, 0, finalSize.Width, Math.Max(desired.Height, finalSize.Height))
            : new Rect(0, 0, Math.Max(desired.Width, finalSize.Width), finalSize.Height);
        child.Arrange(rect);
        return finalSize;
    }

    private void UpdateClip(Size size, double fraction)
    {
        var moving = (_size?.IsAnimating ?? false) || fraction < 0.999;
        if (!moving)
        {
            Clip = null;
            return;
        }
        // Width collapses keep vertical overflow (focus rings) visible; height collapses keep horizontal overflow.
        var rect = Orientation == Orientation.Vertical
            ? new Rect(-16, 0, size.Width + 32, size.Height)
            : new Rect(0, -16, size.Width, size.Height + 32);
        _clip ??= new RectangleGeometry();
        _clip.Rect = rect;
        Clip = _clip;
        InvalidateVisual();
    }
}
