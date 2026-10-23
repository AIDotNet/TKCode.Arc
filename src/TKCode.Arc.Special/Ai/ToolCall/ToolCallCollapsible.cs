using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The animated clip boxes of the tool call parts (Arc's height containers): a row's Input / Output panel, a group's list,
/// a group appearing with its first call and a row appended by a live run. Opening springs the height from 0 to the
/// content on <see cref="ArcSpring.Smooth"/> (critically damped, measured ω 13.1) while the content fades in
/// (<see cref="FadeInDuration"/> on <see cref="FadeInEasing"/> after <see cref="FadeInDelay"/>); closing springs to 0 while it
/// fades out over <see cref="FadeOutDuration"/>, then the content leaves the visual tree (Arc unmounts it) unless
/// <see cref="KeepsContent"/>. At rest an open box is auto height: content that grows or shrinks (a fold opening inside, a
/// row's reason line) is followed at once, since those parts animate themselves. The box clips only while it moves.
/// Reduced motion and Off: everything changes at once.
/// <para>
/// Code-driven (<see cref="MotionValue{T}"/>) because the target is the measured content height, a toggle mid-way must keep
/// the spring's velocity and the unmount waits for the spring to settle; a ControlTheme transition can express none of
/// these.
/// </para>
/// </summary>
internal sealed class ToolCallCollapsible : Control
{
    /// <summary>Whether the content is shown.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ToolCallCollapsible, bool>(nameof(IsExpanded));

    /// <summary>What opens and closes.</summary>
    public static readonly StyledProperty<Control?> ContentProperty =
        AvaloniaProperty.Register<ToolCallCollapsible, Control?>(nameof(Content));

    private static readonly Easing s_easeOut = new SplineEasing(0, 0, 0.58, 1);

    private readonly RectangleGeometry _clip = new();
    private MotionValue<double>? _height;
    private MotionValue<double>? _fade;
    private Control? _mounted;
    private double _natural;
    private bool _opening;
    private bool _instantOpen;
    private int _closeRun;
    private double _laidOut = double.NaN;

    static ToolCallCollapsible()
    {
        ClipToBoundsProperty.OverrideDefaultValue<ToolCallCollapsible>(false);
    }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <inheritdoc cref="ContentProperty"/>
    [Content]
    public Control? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>The content's fade in (160ms by default).</summary>
    public TimeSpan FadeInDuration { get; set; } = ArcMotionPresets.Fast;

    /// <summary>The fade in's curve (Arc's standard curve by default; CSS ease-out for appearing rows and groups).</summary>
    public Easing FadeInEasing { get; set; } = ArcEasings.Standard;

    /// <summary>A wait before the fade in (60ms for a row appended by a live run).</summary>
    public TimeSpan FadeInDelay { get; set; }

    /// <summary>The content's fade out (160ms on the standard curve).</summary>
    public TimeSpan FadeOutDuration { get; set; } = ArcMotionPresets.Fast;

    /// <summary>Whether closing animates; false shuts the box at once (a group emptied for a new run).</summary>
    public bool AnimatesClose { get; set; } = true;

    /// <summary>Whether the content stays in the visual tree when closed (it never does in Arc's panels).</summary>
    public bool KeepsContent { get; set; }

    /// <summary>How far the content may overflow left and right while the box clips (focus rings).</summary>
    public double CrossClipOverflow { get; set; }

    /// <summary>CSS <c>ease-out</c>, the curve of the appear fades.</summary>
    internal static Easing EaseOut => s_easeOut;

    /// <summary>Whether the content is in the tree.</summary>
    internal bool IsMounted => _mounted is not null;

    /// <summary>The height drawn now.</summary>
    internal double DisplayedHeight => _mounted is null ? 0 : _height?.Value ?? 0;

    /// <summary>The content's opacity drawn now.</summary>
    internal double DisplayedOpacity => _mounted is null ? 0 : _fade?.Value ?? 1;

    /// <summary>True while the height or the fade runs.</summary>
    internal bool IsMoving => _height?.IsAnimating == true || _fade?.IsAnimating == true;

    /// <summary>The content's natural height (last measure).</summary>
    internal double NaturalHeight => _natural;

    /// <summary>Raised when the content left the tree after closing.</summary>
    internal event EventHandler? ContentUnmounted;

    /// <summary>Opens without motion (the first state, or a box that appears as part of a larger one).</summary>
    internal void OpenInstantly()
    {
        _instantOpen = true;
        SetCurrentValue(IsExpandedProperty, true);
        _instantOpen = false;
        _height?.Jump(_natural);
        _fade?.Jump(1);
        ApplyOpacity(1);
        InvalidateMeasure();
    }

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
            if (wasMounted || IsExpanded || KeepsContent)
            {
                Mount();
                _height?.Jump(_natural);
                if (IsExpanded)
                {
                    _fade?.Jump(1);
                    ApplyOpacity(1);
                }
            }
            InvalidateMeasure();
        }
    }

    private bool Animates => IsLoaded && !_instantOpen && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;

    private void Open()
    {
        _closeRun++;
        var first = _mounted is null;
        Mount();
        if (!Animates)
        {
            _opening = false;
            Fade.Jump(1);
            ApplyOpacity(1);
            _jumpOnMeasure = true;
            InvalidateMeasure();
            return;
        }
        if (first || (_height?.Value ?? 0) <= 0.01)
        {
            HeightValue.Jump(0);
            Fade.Jump(0);
            ApplyOpacity(0);
        }
        _opening = true;
        _ = Fade.AnimateTo(1, MotionSpec.Tween(FadeInDuration, FadeInEasing, FadeInDelay));
        InvalidateMeasure();
    }

    private bool _jumpOnMeasure;

    private void Close()
    {
        _opening = false;
        if (_mounted is null)
        {
            HeightValue.Jump(0);
            InvalidateMeasure();
            return;
        }
        var run = ++_closeRun;
        if (!Animates || !AnimatesClose)
        {
            HeightValue.Jump(0);
            Fade.Jump(0);
            ApplyOpacity(0);
            FinishClose(true, run);
            InvalidateMeasure();
            return;
        }
        _ = Fade.AnimateTo(0, MotionSpec.Tween(FadeOutDuration, ArcEasings.Standard));
        _ = AfterCloseAsync(HeightValue.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth)), run);
    }

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
            if (!KeepsContent)
            {
                Unmount(raise: true);
            }
            else
            {
                InvalidateMeasure();
            }
        }
    }

    private MotionValue<double> HeightValue
    {
        get
        {
            if (_height is null)
            {
                _height = new MotionValue<double>(this, IsExpanded ? _natural : 0, MotionVectors.Double);
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
                _fade = new MotionValue<double>(this, IsExpanded ? 1 : 0, MotionVectors.Double);
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
        // Layout snaps to device pixels: sub-pixel frames of the spring's tail would measure the parents for nothing.
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
        ApplyOpacity(_fade?.Value ?? (IsExpanded ? 1 : 0));
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
        if (_mounted is null && KeepsContent && Content is not null)
        {
            Mount();
        }
        if (_mounted is not { } content)
        {
            return default;
        }
        content.Measure(availableSize.WithHeight(double.PositiveInfinity));
        _natural = content.DesiredSize.Height;
        var height = HeightValue;
        if (IsExpanded)
        {
            if (_jumpOnMeasure)
            {
                _jumpOnMeasure = false;
                height.Jump(_natural);
            }
            else if (_opening)
            {
                _opening = false;
                if (Animates)
                {
                    _ = height.AnimateTo(_natural, MotionSpec.Spring(ArcSpring.Smooth));
                }
                else
                {
                    height.Jump(_natural);
                }
            }
            else if (Math.Abs(height.Target - _natural) > 0.5 || (!height.IsAnimating && Math.Abs(height.Value - _natural) > 0.5))
            {
                if (height.IsAnimating)
                {
                    // Still opening: the spring keeps its velocity toward the new content height.
                    _ = height.AnimateTo(_natural, MotionSpec.Spring(ArcSpring.Smooth));
                }
                else
                {
                    height.Jump(_natural);
                }
            }
        }
        UpdateClip();
        return new Size(content.DesiredSize.Width, Math.Max(0, height.Value));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_mounted is { } content)
        {
            content.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(_natural, 0)));
        }
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
        var cross = CrossClipOverflow;
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
        _opening = false;
        if (IsExpanded)
        {
            _fade?.Jump(1);
            ApplyOpacity(1);
            _height?.Jump(_natural);
        }
        else
        {
            _fade?.Jump(0);
            _height?.Jump(0);
            if (!KeepsContent)
            {
                Unmount(raise: false);
            }
        }
    }
}
