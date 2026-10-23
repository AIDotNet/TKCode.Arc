using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The box that opens under a low-confidence field for the model's reason (spec 5.2): its height runs on the
/// <see cref="ArcSpring.Smooth"/> spring while the content fades, 160ms in on the standard curve and 100ms out, both
/// starting with the height. It clips only while it moves; once fully closed the content leaves the visual tree (so a closed
/// reason holds no focus and costs no layout), staying a logical child. The first state shows as it is.
/// Reduced motion: the height jumps open while the content fades in over 120ms; closing fades out over 120ms, then the
/// height jumps shut. Off: everything jumps.
/// </summary>
/// <remarks>
/// The height is code-driven (<see cref="MotionValue{T}"/>): its target is the measured content height (one or two lines of
/// reason, wrapping with the width), it keeps its velocity when the value flips mid-way, and the unmount waits for the spring;
/// a ControlTheme transition can express none of these. (The AI chat's <c>AiCollapsible</c> does the same on the faster
/// <see cref="ArcSpring.Settle"/> spring, which it hard-codes.)
/// </remarks>
internal sealed class ExtractionReveal : Control
{
    /// <summary>Whether the content is shown.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ExtractionReveal, bool>(nameof(IsExpanded));

    /// <summary>What opens and closes.</summary>
    public static readonly StyledProperty<Control?> ContentProperty =
        AvaloniaProperty.Register<ExtractionReveal, Control?>(nameof(Content));

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <inheritdoc cref="ContentProperty"/>
    [Content]
    public Control? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>Content in, 160ms on the standard curve.</summary>
    internal static readonly TimeSpan FadeIn = ArcMotionPresets.Fast;

    /// <summary>Content out, 100ms.</summary>
    internal static readonly TimeSpan FadeOut = TimeSpan.FromMilliseconds(100);

    private static readonly Easing s_linear = new LinearEasing();

    private readonly RectangleGeometry _clip = new();
    private MotionValue<double>? _height;
    private MotionValue<double>? _fade;
    private Control? _mounted;
    private double _natural;
    private bool _opening;
    private int _closeRun;
    private double _laidOut = double.NaN;

    static ExtractionReveal()
    {
        ClipToBoundsProperty.OverrideDefaultValue<ExtractionReveal>(false);
    }

    /// <summary>Whether the content is in the tree (open, opening or closing).</summary>
    internal bool IsMounted => _mounted is not null;

    /// <summary>The height as drawn now.</summary>
    internal double DisplayedHeight => _mounted is null ? 0 : _height?.Value ?? 0;

    /// <summary>The content's opacity as drawn now.</summary>
    internal double DisplayedOpacity => _mounted is null ? 0 : _fade?.Value ?? 1;

    /// <summary>True while the height or the fade runs.</summary>
    internal bool IsMoving => _height?.IsAnimating == true || _fade?.IsAnimating == true;

    /// <summary>Raised when the content left the tree after closing.</summary>
    internal event EventHandler? ContentUnmounted;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            if (change.GetNewValue<bool>())
            {
                Open();
            }
            else
            {
                Close();
            }
        }
        else if (change.Property == ContentProperty)
        {
            var wasMounted = _mounted is not null;
            Unmount(raise: false);
            if (change.OldValue is Control oldContent)
            {
                LogicalChildren.Remove(oldContent);
            }
            if (change.NewValue is Control newContent)
            {
                LogicalChildren.Add(newContent);
            }
            if (wasMounted || IsExpanded)
            {
                Mount();
                _height?.Jump(_natural);
                Fade.Jump(1);
            }
            InvalidateMeasure();
        }
    }

    private void Open()
    {
        _closeRun++;
        var first = _mounted is null;
        Mount();
        var level = ArcMotion.GetEffectiveLevel(this);
        if (first)
        {
            HeightValue.Jump(0);
            Fade.Jump(IsLoaded && level != ArcMotionLevel.Off ? 0 : 1);
        }
        _opening = true;
        if (IsLoaded && level != ArcMotionLevel.Off)
        {
            _ = Fade.AnimateTo(1, level == ArcMotionLevel.Reduced
                ? MotionSpec.Tween(ArcMotionPresets.Instant, s_linear)
                : MotionSpec.Tween(FadeIn, ArcEasings.Standard));
        }
        else
        {
            Fade.Jump(1);
        }
        InvalidateMeasure();
    }

    private void Close()
    {
        _opening = false;
        if (_mounted is null)
        {
            return;
        }
        var run = ++_closeRun;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!IsLoaded || level == ArcMotionLevel.Off)
        {
            HeightValue.Jump(0);
            Fade.Jump(0);
            Unmount(raise: true);
            return;
        }
        if (level == ArcMotionLevel.Reduced)
        {
            // No spatial motion: the content fades out in place, then the box shuts.
            _ = AfterCloseAsync(Fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, s_linear)), run);
            return;
        }
        _ = Fade.AnimateTo(0, MotionSpec.Tween(FadeOut, ArcEasings.Standard));
        _ = AfterCloseAsync(HeightValue.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth)), run);
    }

    /// <summary>Shuts the box and unmounts the content when <paramref name="motion"/> finished and nothing reopened it since.</summary>
    private async Task AfterCloseAsync(Task<bool> motion, int run)
    {
        var reached = await motion;
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => FinishClose(reached, run));
            return;
        }
        FinishClose(reached, run);
    }

    private void FinishClose(bool reached, int run)
    {
        if (reached && run == _closeRun && !IsExpanded)
        {
            HeightValue.Jump(0);
            Unmount(raise: true);
        }
    }

    private MotionValue<double> HeightValue
    {
        get
        {
            if (_height is null)
            {
                _height = new MotionValue<double>(this, 0, MotionVectors.Double);
                _height.Changed += OnHeightChanged;
            }
            return _height;
        }
    }

    private MotionValue<double> Fade
    {
        get
        {
            if (_fade is null)
            {
                _fade = new MotionValue<double>(this, 1, MotionVectors.Double);
                _fade.Changed += ApplyOpacity;
            }
            return _fade;
        }
    }

    private void ApplyOpacity(double value)
    {
        if (_mounted is not null)
        {
            _mounted.Opacity = Math.Clamp(value, 0, 1);
        }
    }

    private void OnHeightChanged(double value)
    {
        // Layout snaps to device pixels: sub-pixel frames of the spring's tail would measure the parent chain for nothing.
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var snapped = Math.Round(value * scale);
        if (snapped != _laidOut || !HeightValue.IsAnimating || Math.Abs(value - HeightValue.Target) < 0.01)
        {
            _laidOut = snapped;
            InvalidateMeasure();
        }
        UpdateClip();
    }

    private void Mount()
    {
        if (_mounted is not null || Content is not { } content)
        {
            return;
        }
        _mounted = content;
        VisualChildren.Add(content);
        ApplyOpacity(_fade?.Value ?? 1);
    }

    private void Unmount(bool raise)
    {
        if (_mounted is not { } content)
        {
            return;
        }
        _mounted = null;
        VisualChildren.Remove(content);
        content.Opacity = 1;
        Clip = null;
        InvalidateMeasure();
        if (raise)
        {
            ContentUnmounted?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_mounted is not { } content)
        {
            return default;
        }
        content.Measure(availableSize.WithHeight(double.PositiveInfinity));
        _natural = content.DesiredSize.Height;
        var height = HeightValue;
        if (IsExpanded)
        {
            if (_opening)
            {
                _opening = false;
                if (IsLoaded && ArcMotion.IsFull(this))
                {
                    _ = height.AnimateTo(_natural, MotionSpec.Spring(ArcSpring.Smooth));
                }
                else
                {
                    // The first state, reduced motion and Off: the height is there at once.
                    height.Jump(_natural);
                }
            }
            else if (Math.Abs(height.Target - _natural) > 0.5)
            {
                if (height.IsAnimating)
                {
                    // Still opening: the spring keeps its velocity toward the new content height.
                    _ = height.AnimateTo(_natural, MotionSpec.Spring(ArcSpring.Smooth));
                }
                else
                {
                    // At rest the box is auto height: a reason that rewraps (a narrower form) follows at once.
                    height.Jump(_natural);
                }
            }
        }
        UpdateClip();
        return new Size(content.DesiredSize.Width, Math.Max(0, height.Value));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_mounted is not { } content)
        {
            return finalSize;
        }
        content.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(_natural, 0)));
        UpdateClip();
        return finalSize;
    }

    private void UpdateClip()
    {
        if (_mounted is null || _height is null)
        {
            Clip = null;
            return;
        }
        var shown = Math.Max(0, _height.Value);
        var moving = _height.IsAnimating && Math.Abs(_height.Value - _height.Target) > 0.01;
        if (!moving && shown >= _natural - 0.5)
        {
            Clip = null;
            return;
        }
        // The pill's focus ring may overflow sideways while the box clips.
        const double cross = 4;
        // One geometry, updated in place: a moving clip allocates nothing per frame.
        _clip.Rect = new Rect(-cross, 0, Math.Max(0, Bounds.Width) + 2 * cross, shown);
        Clip = _clip;
        InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Away from a window the motion would only jump later: settle now.
        _closeRun++;
        if (IsExpanded)
        {
            _fade?.Jump(1);
            _height?.Jump(_natural);
        }
        else
        {
            _fade?.Jump(0);
            _height?.Jump(0);
            Unmount(raise: false);
        }
    }
}
