using TKCode.Arc.Motion;
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

    internal static readonly StyledProperty<Thickness> ThumbMarginProperty =
        AvaloniaProperty.Register<Switch, Thickness>(nameof(ThumbMargin));

    internal static readonly StyledProperty<double> ThumbWidthProperty =
        AvaloniaProperty.Register<Switch, double>(nameof(ThumbWidth), 18);

    internal Thickness ThumbMargin
    {
        get => GetValue(ThumbMarginProperty);
        set => SetValue(ThumbMarginProperty, value);
    }

    internal double ThumbWidth
    {
        get => GetValue(ThumbWidthProperty);
        set => SetValue(ThumbWidthProperty, value);
    }

    #endregion

    private Border? _thumb;
    private ScaleTransform? _thumbScale;
    private MotionValue<double>? _stretch;
    private long _releasedAt = long.MinValue;

    protected override Type StyleKeyOverride => typeof(Switch);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
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
        if (change.Property == IsCheckedProperty && IsLoaded)
        {
            PlayStretch();
        }
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
