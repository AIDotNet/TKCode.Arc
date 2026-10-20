using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc.Primitives;

/// <summary>How a <see cref="PresenceHost"/> enters and leaves.</summary>
public enum PresenceMode
{
    /// <summary>Opacity only (standard + ease enter in, fast out).</summary>
    Fade,

    /// <summary>P26: grows from <see cref="PresenceHost.EnterScale"/> (default 0.6) on a snappy spring (status dots, badges).</summary>
    Pop,

    /// <summary>Scales from 0.94–0.97 with a fade (menus, cards).</summary>
    Scale,

    /// <summary>Rises <see cref="PresenceHost.Offset"/> px while fading in; leaves upward.</summary>
    Rise,

    /// <summary>Drops in from above by <see cref="PresenceHost.Offset"/> px.</summary>
    Drop
}

/// <summary>Which dimension collapses while a <see cref="PresenceHost"/> is hidden.</summary>
public enum PresenceCollapse
{
    None,
    Width,
    Height
}

/// <summary>
/// S3 PresenceHost: shows and hides its child with enter / exit motion (AnimatePresence for a single child).
/// The child stays in the tree until the exit finishes. Motion is applied to the host's own opacity and
/// render transform, so no layout happens while animating unless <see cref="Collapse"/> is set (then the
/// collapsing dimension follows the same motion, like Arc's <c>width: 0 → auto</c>). The first state is
/// applied without animation (<c>initial={false}</c>).
/// </summary>
public class PresenceHost : Decorator
{
    public static readonly StyledProperty<bool> IsPresentProperty =
        AvaloniaProperty.Register<PresenceHost, bool>(nameof(IsPresent), true);

    public static readonly StyledProperty<PresenceMode> ModeProperty =
        AvaloniaProperty.Register<PresenceHost, PresenceMode>(nameof(Mode));

    public static readonly StyledProperty<PresenceCollapse> CollapseProperty =
        AvaloniaProperty.Register<PresenceHost, PresenceCollapse>(nameof(Collapse));

    /// <summary>Starting scale for <see cref="PresenceMode.Pop"/> and <see cref="PresenceMode.Scale"/>; NaN uses the effect default.</summary>
    public static readonly StyledProperty<double> EnterScaleProperty =
        AvaloniaProperty.Register<PresenceHost, double>(nameof(EnterScale), double.NaN);

    /// <summary>Travel for <see cref="PresenceMode.Rise"/> / <see cref="PresenceMode.Drop"/> in px.</summary>
    public static readonly StyledProperty<double> OffsetProperty =
        AvaloniaProperty.Register<PresenceHost, double>(nameof(Offset), 4);

    /// <summary>Spring used by springy effects (Pop, Scale) and by collapse.</summary>
    public static readonly StyledProperty<ArcSpring> SpringProperty =
        AvaloniaProperty.Register<PresenceHost, ArcSpring>(nameof(Spring), ArcSpring.Snappy);

    public static readonly StyledProperty<RelativePoint> OriginProperty =
        AvaloniaProperty.Register<PresenceHost, RelativePoint>(nameof(Origin), RelativePoint.Center);

    public bool IsPresent { get => GetValue(IsPresentProperty); set => SetValue(IsPresentProperty, value); }
    public PresenceMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public PresenceCollapse Collapse { get => GetValue(CollapseProperty); set => SetValue(CollapseProperty, value); }
    public double EnterScale { get => GetValue(EnterScaleProperty); set => SetValue(EnterScaleProperty, value); }
    public double Offset { get => GetValue(OffsetProperty); set => SetValue(OffsetProperty, value); }
    public ArcSpring Spring { get => GetValue(SpringProperty); set => SetValue(SpringProperty, value); }
    public RelativePoint Origin { get => GetValue(OriginProperty); set => SetValue(OriginProperty, value); }

    /// <summary>Raised when an exit finished and the child was hidden.</summary>
    public event EventHandler? Exited;

    private MotionValue<double>? _progress;
    private readonly ScaleTransform _scale = new();
    private readonly TranslateTransform _translate = new();
    private bool _initialized;

    static PresenceHost()
    {
        AffectsMeasure<PresenceHost>(CollapseProperty);
    }

    public PresenceHost()
    {
        RenderTransform = new TransformGroup { Children = { _scale, _translate } };
    }

    /// <summary>Current presence (0 hidden, 1 shown; may overshoot on springs).</summary>
    public double Progress => _progress?.Value ?? (IsPresent ? 1 : 0);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPresentProperty)
        {
            Run(change.GetNewValue<bool>());
        }
        else if (change.Property == OriginProperty)
        {
            RenderTransformOrigin = Origin;
        }
        else if (change.Property == ChildProperty && !IsPresent && change.NewValue is Control child)
        {
            child.IsVisible = false;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_initialized)
        {
            _initialized = true;
            RenderTransformOrigin = Origin;
            Apply(IsPresent ? 1 : 0);
            if (Child is { } child)
            {
                child.IsVisible = IsPresent;
            }
        }
    }

    private void Run(bool present)
    {
        var child = Child;
        if (!_initialized || !IsLoaded)
        {
            Apply(present ? 1 : 0);
            if (child is not null)
            {
                child.IsVisible = present;
            }
            return;
        }
        _progress ??= CreateProgress(present ? 0 : 1);
        var level = ArcMotion.GetEffectiveLevel(this);
        if (present && child is not null)
        {
            child.IsVisible = true;
        }
        MotionSpec spec;
        if (level == ArcMotionLevel.Off)
        {
            spec = MotionSpec.Instant;
        }
        else if (level == ArcMotionLevel.Reduced)
        {
            spec = MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);
        }
        else if (present)
        {
            spec = Mode is PresenceMode.Pop or PresenceMode.Scale || Collapse != PresenceCollapse.None
                ? MotionSpec.Spring(Spring)
                : MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter);
        }
        else
        {
            spec = Collapse != PresenceCollapse.None
                ? MotionSpec.Spring(ArcSpring.Smooth)
                : MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);
        }
        _reduced = level == ArcMotionLevel.Reduced;
        var task = _progress.AnimateTo(present ? 1 : 0, spec);
        if (!present)
        {
            _ = HideWhenDone(task);
        }
    }

    private bool _reduced;

    private async Task HideWhenDone(Task<bool> task)
    {
        var reached = await task;
        if (reached && !IsPresent && Child is { } child)
        {
            child.IsVisible = false;
            Exited?.Invoke(this, EventArgs.Empty);
        }
    }

    private MotionValue<double> CreateProgress(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += Apply;
        return value;
    }

    private void Apply(double p)
    {
        Opacity = Math.Clamp(p, 0, 1);
        var effect = _reduced ? PresenceMode.Fade : Mode;
        var scale = 1.0;
        double dy = 0;
        switch (effect)
        {
            case PresenceMode.Pop:
                scale = Lerp(double.IsNaN(EnterScale) ? 0.6 : EnterScale, 1, p);
                break;
            case PresenceMode.Scale:
                scale = Lerp(double.IsNaN(EnterScale) ? 0.96 : EnterScale, 1, p);
                break;
            case PresenceMode.Rise:
                dy = (1 - Math.Clamp(p, 0, 1)) * Offset * (IsPresent ? 1 : -1);
                break;
            case PresenceMode.Drop:
                dy = -(1 - Math.Clamp(p, 0, 1)) * Offset;
                break;
        }
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        _translate.Y = dy;
        if (Collapse != PresenceCollapse.None)
        {
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        if (Collapse == PresenceCollapse.None)
        {
            return size;
        }
        var p = Math.Clamp(Progress, 0, 1);
        if (Child is { IsVisible: false })
        {
            p = 0;
        }
        return Collapse == PresenceCollapse.Width ? size.WithWidth(size.Width * p) : size.WithHeight(size.Height * p);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Collapse == PresenceCollapse.None || Child is not { } child)
        {
            return base.ArrangeOverride(finalSize);
        }
        var desired = child.DesiredSize;
        var rect = Collapse == PresenceCollapse.Width
            ? new Rect(0, 0, Math.Max(desired.Width, finalSize.Width), finalSize.Height)
            : new Rect(0, 0, finalSize.Width, Math.Max(desired.Height, finalSize.Height));
        child.Arrange(rect);
        return finalSize;
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}
