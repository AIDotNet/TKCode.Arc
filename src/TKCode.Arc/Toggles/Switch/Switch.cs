using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Arc switch: a tactile toggle for settings that take effect immediately. While pressed the thumb widens
/// toward the other side (far edge anchored); on release it glides on a critically damped spring. Toggles
/// that did not come from a press (keyboard, code) play a brief stretch instead.
/// </summary>
public class Switch : ToggleButton
{
    #region 内部属性定义

    internal static readonly StyledProperty<double> ThumbWidthProperty =
        AvaloniaProperty.Register<Switch, double>(nameof(ThumbWidth), 18);

    internal double ThumbWidth
    {
        get => GetValue(ThumbWidthProperty);
        set => SetValue(ThumbWidthProperty, value);
    }

    internal static readonly StyledProperty<double> ThumbOffsetProperty =
        AvaloniaProperty.Register<Switch, double>(nameof(ThumbOffset), 0);

    /// <summary>
    /// The thumb's horizontal offset in pixels. Styled per state (off/checked/pressed) so the glide is a plain
    /// double transition; it drives the thumb host's render transform (wired in OnApplyTemplate). Using a render-only offset
    /// (instead of a Margin/Thickness) keeps a toggle out of the measure pass.
    /// </summary>
    internal double ThumbOffset
    {
        get => GetValue(ThumbOffsetProperty);
        set => SetValue(ThumbOffsetProperty, value);
    }

    #endregion

    private Border? _thumb;
    private ScaleTransform? _thumbScale;
    private TranslateTransform? _thumbOffset;
    private MotionValue<double>? _stretch;
    private long _releasedAt = long.MinValue;
    private LazyFocusRing? _focusRing;

    protected override Type StyleKeyOverride => typeof(Switch);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        // Set here, not via TemplateBinding in the template: a Transform has no templated parent, so the binding
        // would never resolve and the thumb would stay at the off position.
        _thumbOffset = new TranslateTransform(ThumbOffset, 0);
        var thumbHost = e.NameScope.Find<Panel>("PART_ThumbHost");
        if (thumbHost is not null)
        {
            thumbHost.RenderTransform = _thumbOffset;
        }
        _thumb = e.NameScope.Find<Border>("PART_Thumb");
        if (_thumb is not null)
        {
            _thumbScale = new ScaleTransform();
            _thumb.RenderTransform = _thumbScale;
            _stretch = new MotionValue<double>(_thumb, 1, MotionVectors.Double);
            _stretch.Changed += v =>
            {
                if (_thumbScale is not null)
                {
                    _thumbScale.ScaleX = v;
                }
            };
        }
        // The pill ring sits under the track; built the first time the switch is keyboard focused.
        _focusRing ??= new LazyFocusRing(this, SwitchTokens.Key(SwitchTokenKind.FocusRingMargin), ArcDesignTokens.Key(ArcDesignTokenKind.RadiusPill));
        _focusRing.Attach(e.NameScope.Find<Panel>("PART_TrackHost"), below: true);
    }

    /// <summary>
    /// The thumb's rendered horizontal offset in pixels (read from the host's transform, so tests see what is drawn).
    /// The value is animated during a glide.
    /// </summary>
    internal double ThumbOffsetX => _thumbOffset?.X ?? 0;

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (IsPressed && IsEnabled)
        {
            _releasedAt = Environment.TickCount64;
        }
        base.OnPointerReleased(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.Space && IsPressed && IsEnabled)
        {
            _releasedAt = Environment.TickCount64;
        }
        base.OnKeyUp(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ThumbOffsetProperty && _thumbOffset is not null)
        {
            _thumbOffset.X = ThumbOffset;
        }
        if (change.Property == IsCheckedProperty && IsLoaded && !ArcMotion.IsSettling(this))
        {
            PlayStretch();
        }
    }

    /// <summary>A value that arrives with a new data context is not a toggle: the thumb jumps, no slide or stretch.</summary>
    protected override void OnDataContextBeginUpdate()
    {
        base.OnDataContextBeginUpdate();
        ArcMotion.BeginSettle(this);
    }

    protected override void OnDataContextEndUpdate()
    {
        base.OnDataContextEndUpdate();
        ArcMotion.EndSettle(this);
    }

    /// <summary>
    /// The 1 → 1.16 → 1 stretch for toggles that did not come from a press. Done in code (not ControlTheme)
    /// because it depends on how recently the thumb was released, which styles cannot express.
    /// </summary>
    private async void PlayStretch()
    {
        if (_stretch is null || !ArcMotion.IsFull(this))
        {
            return;
        }
        var fromPress = Environment.TickCount64 - _releasedAt < 250;
        if (fromPress)
        {
            return;
        }
        await _stretch.AnimateTo(1.16, MotionSpec.Tween(TimeSpan.FromSeconds(0.136), ArcEasings.Standard));
        await _stretch.AnimateTo(1.0, MotionSpec.Tween(TimeSpan.FromSeconds(0.204), ArcEasings.InOut));
    }
}
