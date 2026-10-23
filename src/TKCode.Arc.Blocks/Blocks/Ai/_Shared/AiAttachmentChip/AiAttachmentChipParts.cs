using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The chip's 28px round action button (cancel while uploading, remove otherwise). Hover is mouse and pen only (Arc's
/// <c>(hover: hover) and (pointer: fine)</c>): a 6% <c>Foreground</c> fill, 10% while pressed; the glyph turns from
/// <c>TextSecondary</c> to <c>Foreground</c>. Keyboard focus draws the 2px ring 2px outside.
/// </summary>
[PseudoClasses(HoverPseudoClass)]
internal sealed class AiAttachmentChipButton : AvaloniaButton
{
    /// <summary>Mouse or pen over the button.</summary>
    public const string HoverPseudoClass = ":hover";

    /// <summary>The corners of the keyboard focus ring (the button's own corners + 4: it sits 2px outside with a 2px stroke).</summary>
    public static readonly StyledProperty<CornerRadius> RingCornerRadiusProperty =
        AvaloniaProperty.Register<AiAttachmentChipButton, CornerRadius>(nameof(RingCornerRadius));

    public CornerRadius RingCornerRadius { get => GetValue(RingCornerRadiusProperty); set => SetValue(RingCornerRadiusProperty, value); }

    protected override Type StyleKeyOverride => typeof(AiAttachmentChipButton);

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }
}

/// <summary>
/// The chip's meta line: 12 / 15.6 <c>TextMuted</c> on one line; tabular figures while it counts an upload; for an error a
/// 12px circle-alert and the reason, both <c>Danger</c>. A new line is made for each kind of content, so the chip can
/// crossfade from one to the next.
/// </summary>
[PseudoClasses(ErrorPseudoClass, ProgressPseudoClass, TabularPseudoClass)]
internal sealed class AiAttachmentChipMeta : TemplatedControl
{
    public const string ErrorPseudoClass = ":error";
    public const string ProgressPseudoClass = ":progress";

    /// <summary>Tabular figures (the compact chip's size).</summary>
    public const string TabularPseudoClass = ":tabular";

    public static readonly StyledProperty<bool> IsTabularProperty =
        AvaloniaProperty.Register<AiAttachmentChipMeta, bool>(nameof(IsTabular));

    public bool IsTabular { get => GetValue(IsTabularProperty); set => SetValue(IsTabularProperty, value); }

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AiAttachmentChipMeta, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsErrorProperty =
        AvaloniaProperty.Register<AiAttachmentChipMeta, bool>(nameof(IsError));

    public static readonly StyledProperty<bool> IsProgressProperty =
        AvaloniaProperty.Register<AiAttachmentChipMeta, bool>(nameof(IsProgress));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public bool IsError { get => GetValue(IsErrorProperty); set => SetValue(IsErrorProperty, value); }

    public bool IsProgress { get => GetValue(IsProgressProperty); set => SetValue(IsProgressProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsErrorProperty)
        {
            PseudoClasses.Set(ErrorPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == IsProgressProperty)
        {
            PseudoClasses.Set(ProgressPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == IsTabularProperty)
        {
            PseudoClasses.Set(TabularPseudoClass, change.GetNewValue<bool>());
        }
    }
}

/// <summary>
/// The glyph inside the chip's button: an upload ring (16px, 2px round-capped stroke, <c>Border</c> track and
/// <c>Foreground</c> fill, from 12 o'clock, never spinning) while uploading, a check when done, an X when ready or in
/// error (14px, stroke 2, <see cref="Foreground"/>). The ring follows <see cref="Progress"/>, smoothing each step like
/// Arc's 90ms dash transition.
/// <para>
/// Swaps are code-driven (<see cref="MotionValue{T}"/>): the leaving glyph shrinks to 0.6 and fades on a short ease-out
/// tween (120ms for the ring, 160ms for the check) while the new one grows from 0.5 (check) or 0.6 turned −45° (X) on a
/// (0.35, 0.1) spring. A ControlTheme transition has one curve per property, so it cannot give the same glyph a spring on
/// the way in and a tween on the way out, nor keep the spring's velocity when the status flips again mid-swap. Reduced
/// motion crossfades in 120ms without scale or turn; Off swaps at once.
/// </para>
/// </summary>
internal sealed class AiAttachmentStatusGlyph : Panel
{
    /// <summary>The spring of a glyph coming in (Arc: visualDuration 0.35, bounce 0.1).</summary>
    internal static readonly SpringSpec EnterSpring = SpringSpec.FromVisual(0.35, 0.1);

    /// <summary>CSS <c>ease-out</c>, the curve of a glyph going out.</summary>
    internal static readonly Easing EaseOut = new SplineEasing(0, 0, 0.58, 1);

    internal static readonly TimeSpan RingExit = TimeSpan.FromMilliseconds(120);
    internal static readonly TimeSpan GlyphExit = TimeSpan.FromMilliseconds(160);

    /// <summary>How fast the ring catches up with a new progress (per 1/60s), close to a 90ms linear step.</summary>
    internal const double RingChaseRate = 0.35;

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<AiAttachmentStatusGlyph, double>(nameof(Progress));

    /// <summary>The check and the X; inherited from the button, so its hover color reaches them.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        Avalonia.Controls.Documents.TextElement.ForegroundProperty.AddOwner<AiAttachmentStatusGlyph>();

    /// <summary>The ring's filled arc.</summary>
    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<AiAttachmentStatusGlyph, IBrush?>(nameof(RingBrush));

    /// <summary>The ring's track.</summary>
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<AiAttachmentStatusGlyph, IBrush?>(nameof(TrackBrush));

    /// <summary>The compact chip's smaller marks: a 14px ring and 13px check and X, all at stroke 1.75 (default 16 / 14 at 2).</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<AiAttachmentStatusGlyph, bool>(nameof(IsCompact));

    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public IBrush? RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    /// <summary>Which glyph shows.</summary>
    internal enum Shown
    {
        Ring,
        Check,
        Remove
    }

    /// <summary>One glyph with its own scale, turn and opacity.</summary>
    internal sealed class Layer
    {
        private readonly AiAttachmentStatusGlyph _owner;
        private readonly ScaleTransform _scale = new();
        private readonly RotateTransform _rotate = new();
        private MotionValue<double>? _opacity;
        private MotionValue<double>? _size;
        private MotionValue<double>? _angle;

        public Layer(AiAttachmentStatusGlyph owner, Control visual)
        {
            _owner = owner;
            Visual = visual;
            visual.HorizontalAlignment = HorizontalAlignment.Center;
            visual.VerticalAlignment = VerticalAlignment.Center;
            visual.RenderTransformOrigin = RelativePoint.Center;
            visual.RenderTransform = new TransformGroup { Children = { _scale, _rotate } };
            visual.IsHitTestVisible = false;
            SetOpacity(0);
        }

        public Control Visual { get; }

        public double Opacity => Visual.Opacity;

        public double Scale => _scale.ScaleX;

        public double Angle => _rotate.Angle;

        public void Jump(bool shown)
        {
            _opacity?.Stop();
            _size?.Stop();
            _angle?.Stop();
            SetOpacity(shown ? 1 : 0);
            SetScale(1);
            _rotate.Angle = 0;
        }

        public void Enter(ArcMotionLevel level, double fromScale, double fromAngle)
        {
            Values();
            if (level == ArcMotionLevel.Full)
            {
                // From hidden it starts small (and turned); caught mid-exit it turns back from where it is, with its speed.
                if (Visual.Opacity < 0.01)
                {
                    _size!.Jump(fromScale);
                    _angle!.Jump(fromAngle);
                }
                _ = _opacity!.AnimateTo(1, MotionSpec.Spring(EnterSpring));
                _ = _size!.AnimateTo(1, MotionSpec.Spring(EnterSpring));
                _ = _angle!.AnimateTo(0, MotionSpec.Spring(EnterSpring));
                return;
            }
            _size!.Jump(1);
            _angle!.Jump(0);
            _ = _opacity!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, new LinearEasing()));
        }

        public void Exit(ArcMotionLevel level, TimeSpan duration)
        {
            Values();
            if (level == ArcMotionLevel.Full)
            {
                _ = _opacity!.AnimateTo(0, MotionSpec.Tween(duration, EaseOut));
                _ = _size!.AnimateTo(0.6, MotionSpec.Tween(duration, EaseOut));
                return;
            }
            _ = _opacity!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, new LinearEasing()));
        }

        private void Values()
        {
            _opacity ??= Create(Visual.Opacity, SetOpacity);
            _size ??= Create(_scale.ScaleX, SetScale);
            _angle ??= Create(_rotate.Angle, a => _rotate.Angle = a);
        }

        private MotionValue<double> Create(double initial, Action<double> apply)
        {
            var value = new MotionValue<double>(_owner, initial, MotionVectors.Double);
            value.Changed += apply;
            return value;
        }

        private void SetOpacity(double value)
        {
            // A spring may overshoot; opacity stays within 0..1, and a hidden glyph leaves the render pass.
            var opacity = Math.Clamp(value, 0, 1);
            Visual.Opacity = opacity;
            Visual.IsVisible = opacity > 0.001;
        }

        private void SetScale(double value)
        {
            _scale.ScaleX = value;
            _scale.ScaleY = value;
        }
    }

    private readonly ArcRing _ring;
    private readonly ArcGlyph _check;
    private readonly ArcGlyph _remove;
    private readonly Layer _ringLayer;
    private readonly Layer _checkLayer;
    private readonly Layer _removeLayer;
    private Shown? _shown;

    public AiAttachmentStatusGlyph()
    {
        _ring = new ArcRing { Width = 16, Height = 16, Thickness = 2, IsRoundCap = true, ChaseRate = RingChaseRate };
        _check = new ArcGlyph { Data = ArcIcons.Check, Size = 14, StrokeThickness = 2 };
        _remove = new ArcGlyph { Data = ArcIcons.X, Size = 14, StrokeThickness = 2 };
        _ringLayer = new Layer(this, _ring);
        _checkLayer = new Layer(this, _check);
        _removeLayer = new Layer(this, _remove);
        Children.Add(_ring);
        Children.Add(_check);
        Children.Add(_remove);
        IsHitTestVisible = false;
    }

    internal ArcRing Ring => _ring;

    internal Layer RingLayer => _ringLayer;

    internal Layer CheckLayer => _checkLayer;

    internal Layer RemoveLayer => _removeLayer;

    /// <summary>The glyph showing (or coming in), null before the first status.</summary>
    internal Shown? Current => _shown;

    /// <summary>Shows the glyph for <paramref name="status"/>; <paramref name="instant"/> (or a first status) skips the swap.</summary>
    public void SetStatus(AiAttachmentStatus status, bool instant)
    {
        var next = status switch
        {
            AiAttachmentStatus.Uploading => Shown.Ring,
            AiAttachmentStatus.Done => Shown.Check,
            _ => Shown.Remove
        };
        if (next == _shown && !instant)
        {
            return;
        }
        var previous = _shown;
        _shown = next;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (instant || previous is null || !IsLoaded || level == ArcMotionLevel.Off)
        {
            _ringLayer.Jump(next == Shown.Ring);
            _checkLayer.Jump(next == Shown.Check);
            _removeLayer.Jump(next == Shown.Remove);
            if (next == Shown.Ring)
            {
                _ring.Value = Math.Clamp(Progress, 0, 1);
            }
            return;
        }
        if (previous is { } leaving)
        {
            LayerOf(leaving).Exit(level, leaving == Shown.Ring ? RingExit : GlyphExit);
        }
        switch (next)
        {
            case Shown.Ring:
                _ringLayer.Enter(level, 0.6, 0);
                break;
            case Shown.Check:
                _checkLayer.Enter(level, 0.5, 0);
                break;
            default:
                _removeLayer.Enter(level, 0.6, -45);
                break;
        }
    }

    private Layer LayerOf(Shown shown) => shown switch
    {
        Shown.Ring => _ringLayer,
        Shown.Check => _checkLayer,
        _ => _removeLayer
    };

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ProgressProperty)
        {
            // The ring keeps its last value while it leaves (the check takes over at 100%).
            if (_shown == Shown.Ring)
            {
                _ring.Value = Math.Clamp(change.GetNewValue<double>(), 0, 1);
            }
        }
        else if (change.Property == ForegroundProperty)
        {
            _check.Foreground = change.GetNewValue<IBrush?>();
            _remove.Foreground = change.GetNewValue<IBrush?>();
        }
        else if (change.Property == RingBrushProperty)
        {
            _ring.Foreground = change.GetNewValue<IBrush?>();
        }
        else if (change.Property == TrackBrushProperty)
        {
            _ring.TrackBrush = change.GetNewValue<IBrush?>();
        }
        else if (change.Property == IsCompactProperty)
        {
            var compact = change.GetNewValue<bool>();
            _ring.Width = _ring.Height = compact ? 14 : 16;
            _ring.Thickness = compact ? 1.75 : 2;
            _check.Size = _remove.Size = compact ? 13 : 14;
            _check.StrokeThickness = _remove.StrokeThickness = compact ? 1.75 : 2;
        }
    }
}
