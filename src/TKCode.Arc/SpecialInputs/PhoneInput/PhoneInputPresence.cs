using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// Presence of the phone field's small marks (AnimatePresence for one child): the valid check pops in on the snappy
/// spring from scale 0.6 through a 2px blur and leaves to scale 0.8 in 0.1s; the search clear button pops on the glide
/// spring from 0.6 and leaves back to 0.6. Reduced motion fades (<see cref="ReducedDuration"/>); Off swaps at once. The
/// first state is applied without motion and the child leaves the layout once it has gone.
/// </summary>
internal sealed class PhoneInputPresence : Decorator
{
    public static readonly StyledProperty<bool> IsPresentProperty =
        AvaloniaProperty.Register<PhoneInputPresence, bool>(nameof(IsPresent));

    public bool IsPresent { get => GetValue(IsPresentProperty); set => SetValue(IsPresentProperty, value); }

    /// <summary>Spring of the entrance (opacity, scale and blur ride it together, as in framer-motion).</summary>
    public SpringSpec EnterSpring { get; set; } = ArcMotionPresets.Snappy;

    public double EnterScale { get; set; } = 0.6;

    public double ExitScale { get; set; } = 0.8;

    /// <summary>Blur at the start of the entrance (Arc: <c>blur.subtle</c>, 2px; 0 for none).</summary>
    public double EnterBlur { get; set; }

    public TimeSpan ExitDuration { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Fade used under reduced motion (zero swaps at once).</summary>
    public TimeSpan ReducedDuration { get; set; } = TimeSpan.FromMilliseconds(120);

    private readonly ScaleTransform _scale = new();
    private MotionValue<double>? _progress;
    private BlurEffect? _blur;
    private bool _initialized;
    private bool _leaving;
    private double _exitFromOpacity;
    private double _exitFromScale;
    private int _generation;

    public PhoneInputPresence()
    {
        RenderTransform = _scale;
        RenderTransformOrigin = RelativePoint.Center;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_initialized)
        {
            _initialized = true;
            Snap(IsPresent);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPresentProperty)
        {
            Run(change.GetNewValue<bool>());
        }
    }

    private void Snap(bool present)
    {
        _generation++;
        _progress?.Stop();
        Apply(present ? 1 : 0, 1, null);
        IsVisible = present;
    }

    private void Run(bool present)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!_initialized || !IsLoaded || level == ArcMotionLevel.Off || (level == ArcMotionLevel.Reduced && ReducedDuration <= TimeSpan.Zero))
        {
            Snap(present);
            return;
        }
        _progress ??= CreateProgress();
        var generation = ++_generation;
        var full = level == ArcMotionLevel.Full;
        if (present)
        {
            IsVisible = true;
            if (_leaving)
            {
                // Coming back mid exit: continue from what is visible now.
                _leaving = false;
                _progress.Jump(Math.Clamp(Opacity, 0, 1));
            }
            else if (!_progress.IsAnimating && Opacity < 0.01)
            {
                _progress.Jump(0);
            }
            _ = _progress.AnimateTo(1, full ? MotionSpec.Spring(EnterSpring) : MotionSpec.Tween(ReducedDuration, ArcEasings.Standard));
            return;
        }
        _exitFromOpacity = Opacity;
        _exitFromScale = _scale.ScaleX;
        _exitScaleTarget = full ? ExitScale : 1;
        _leaving = true;
        _progress.Jump(0);
        var spec = MotionSpec.Tween(full ? ExitDuration : TimeSpan.FromMilliseconds(Math.Min(100, ReducedDuration.TotalMilliseconds)), ArcEasings.Standard);
        _progress.AnimateTo(1, spec).ContinueWith(t => Dispatcher.Post(() =>
        {
            if (t.Result && generation == _generation && !IsPresent)
            {
                IsVisible = false;
                _leaving = false;
                Apply(0, 1, null);
            }
        }), TaskScheduler.Default);
    }

    private double _exitScaleTarget = 1;

    private MotionValue<double> CreateProgress()
    {
        var value = new MotionValue<double>(this, IsPresent ? 1 : 0, MotionVectors.Double);
        value.Changed += p =>
        {
            if (_leaving)
            {
                var q = Math.Clamp(p, 0, 1);
                Apply(_exitFromOpacity * (1 - q), _exitFromScale + (_exitScaleTarget - _exitFromScale) * q, null);
                return;
            }
            var full = ArcMotion.IsFull(this);
            var scale = full ? EnterScale + (1 - EnterScale) * p : 1;
            var blur = full && EnterBlur > 0 ? EnterBlur * Math.Clamp(1 - p, 0, 1) : 0;
            Apply(Math.Clamp(p, 0, 1), scale, blur);
        };
        return value;
    }

    private void Apply(double opacity, double scale, double? blur)
    {
        Opacity = opacity;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        if (blur is not { } radius || radius < 0.05)
        {
            if (Effect is not null)
            {
                Effect = null;
            }
            return;
        }
        _blur ??= new BlurEffect();
        _blur.Radius = radius;
        Effect = _blur;
    }
}
