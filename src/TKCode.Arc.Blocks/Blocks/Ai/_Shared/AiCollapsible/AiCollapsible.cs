using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The disclosure body of the AI chat parts (a reply's reasoning, a tool's detail panel): Arc's <c>overflow: clip</c> box
/// whose height runs on a <see cref="ArcSpring.Settle"/> spring while the content fades, 160ms in on the standard curve and
/// 100ms out, both starting with the height. The box clips only while it moves. Once fully collapsed the content leaves
/// the visual tree (Arc unmounts it), so a closed panel costs no layout or rendering and holds no focus; it stays a logical
/// child, so its template bindings and resources hold while it is away.
/// <see cref="ContentMargin"/> is space that exists only while the content is mounted and does not animate (Arc's panels
/// gain their top gap at once and lose it after the unmount).
/// Reduced motion: the height jumps open while the content fades in over 120ms; closing fades out over 120ms, then the
/// height jumps shut. Off: everything jumps.
/// The height is code-driven (<see cref="MotionValue{T}"/>) because its target is the measured content height, it must keep
/// its velocity when the person toggles mid-way, and the unmount waits for the spring to settle; a ControlTheme transition
/// can express none of these.
/// </summary>
internal sealed class AiCollapsible : Control
{
    /// <summary>Whether the content is shown.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AiCollapsible, bool>(nameof(IsExpanded));

    /// <summary>What opens and closes.</summary>
    public static readonly StyledProperty<Control?> ContentProperty =
        AvaloniaProperty.Register<AiCollapsible, Control?>(nameof(Content));

    /// <summary>Space around the clipped box while the content is mounted; it appears and goes at once.</summary>
    public static readonly StyledProperty<Thickness> ContentMarginProperty =
        AvaloniaProperty.Register<AiCollapsible, Thickness>(nameof(ContentMargin));

    /// <summary>How far the content may overflow left and right while the box clips (a focus ring, a row's hover fill).</summary>
    public static readonly StyledProperty<double> CrossClipOverflowProperty =
        AvaloniaProperty.Register<AiCollapsible, double>(nameof(CrossClipOverflow));

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <inheritdoc cref="ContentProperty"/>
    [Content]
    public Control? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <inheritdoc cref="ContentMarginProperty"/>
    public Thickness ContentMargin { get => GetValue(ContentMarginProperty); set => SetValue(ContentMarginProperty, value); }

    /// <inheritdoc cref="CrossClipOverflowProperty"/>
    public double CrossClipOverflow { get => GetValue(CrossClipOverflowProperty); set => SetValue(CrossClipOverflowProperty, value); }

    /// <summary>Content in, 160ms on the standard curve (Arc's WAAPI fade).</summary>
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

    static AiCollapsible()
    {
        AffectsMeasure<AiCollapsible>(ContentMarginProperty);
        ClipToBoundsProperty.OverrideDefaultValue<AiCollapsible>(false);
    }

    /// <summary>Whether the content is in the tree (open, opening or closing).</summary>
    internal bool IsMounted => _mounted is not null;

    /// <summary>The height of the clipped box as drawn now (without <see cref="ContentMargin"/>).</summary>
    internal double DisplayedHeight => _mounted is null ? 0 : _height?.Value ?? 0;

    /// <summary>The content's opacity as drawn now.</summary>
    internal double DisplayedOpacity => _mounted is null ? 0 : _fade?.Value ?? 1;

    /// <summary>True while the height or the fade runs.</summary>
    internal bool IsMoving => _height?.IsAnimating == true || _fade?.IsAnimating == true;

    /// <summary>Raised when the content was unmounted after closing.</summary>
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
            // The content stays a logical child all along (its template parent, resources and bindings hold); only its place
            // in the visual tree comes and goes.
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
                // A new content shows as it is: no motion for a swap of what is inside.
                Mount();
                _height?.Jump(_natural);
                SetOpacity(1, instant: true);
            }
            InvalidateMeasure();
        }
    }

    private void Open()
    {
        _closeRun++;
        var first = _mounted is null;
        Mount();
        if (first)
        {
            HeightValue.Jump(0);
            Fade.Jump(0);
            ApplyOpacity(0);
        }
        // The height starts in the next measure, once the content height is known.
        _opening = true;
        SetOpacity(1, instant: !IsLoaded);
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
        _ = AfterCloseAsync(HeightValue.AnimateTo(0, MotionSpec.Spring(ArcSpring.Settle)), run);
    }

    /// <summary>Shuts the box and unmounts the content when <paramref name="motion"/> finished and nothing reopened it since.</summary>
    private async Task AfterCloseAsync(Task<bool> motion, int run)
    {
        // The await resumes on the UI thread's context (the motion completes from a frame callback there).
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

    private void SetOpacity(double target, bool instant)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (instant || level == ArcMotionLevel.Off)
        {
            Fade.Jump(target);
            return;
        }
        _ = Fade.AnimateTo(target, level == ArcMotionLevel.Reduced
            ? MotionSpec.Tween(ArcMotionPresets.Instant, s_linear)
            : MotionSpec.Tween(FadeIn, ArcEasings.Standard));
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
        var margin = ContentMargin;
        var inner = availableSize.Deflate(margin).WithHeight(double.PositiveInfinity);
        content.Measure(inner);
        _natural = content.DesiredSize.Height;
        var height = HeightValue;
        if (IsExpanded)
        {
            if (_opening)
            {
                _opening = false;
                if (IsLoaded)
                {
                    _ = height.AnimateTo(_natural, MotionSpec.Spring(ArcSpring.Settle));
                }
                else
                {
                    // The first state shows as it is.
                    height.Jump(_natural);
                }
            }
            else if (Math.Abs(height.Target - _natural) > 0.5)
            {
                if (height.IsAnimating)
                {
                    // Still opening: the spring keeps its velocity toward the new content height.
                    _ = height.AnimateTo(_natural, MotionSpec.Spring(ArcSpring.Settle));
                }
                else
                {
                    // At rest the box is auto height: content that grows (streamed reasoning) is followed at once.
                    height.Jump(_natural);
                }
            }
        }
        UpdateClip();
        var shown = Math.Max(0, height.Value);
        return new Size(content.DesiredSize.Width + margin.Left + margin.Right, shown + margin.Top + margin.Bottom);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_mounted is not { } content)
        {
            return finalSize;
        }
        var margin = ContentMargin;
        var width = Math.Max(0, finalSize.Width - margin.Left - margin.Right);
        content.Arrange(new Rect(margin.Left, margin.Top, width, Math.Max(_natural, 0)));
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
        // At rest (or on the last frame of the spring, which lands on its target) an open box does not clip.
        var moving = _height.IsAnimating && Math.Abs(_height.Value - _height.Target) > 0.01;
        if (!moving && shown >= _natural - 0.5)
        {
            Clip = null;
            return;
        }
        var margin = ContentMargin;
        var cross = CrossClipOverflow;
        // One geometry, updated in place: a moving clip allocates nothing per frame.
        _clip.Rect = new Rect(margin.Left - cross, margin.Top, Math.Max(0, Bounds.Width - margin.Left - margin.Right) + 2 * cross, shown);
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
