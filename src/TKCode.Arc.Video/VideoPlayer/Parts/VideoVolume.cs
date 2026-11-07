using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace TKCode.Arc;

/// <summary>
/// The volume control (spec section 5): the mute toggle and, to its right, a horizontal slider that slides open while the
/// pointer is over either, the slider has keyboard focus or it is dragged (never in the compact layout). The wheel over
/// it changes the volume by 5%. The player wires <see cref="MuteButton"/> and <see cref="Slider"/> and keeps their state.
/// </summary>
internal sealed class VideoVolume : StackPanel
{
    /// <summary>Whether the slider is open.</summary>
    public static readonly DirectProperty<VideoVolume, bool> IsExpandedProperty =
        AvaloniaProperty.RegisterDirect<VideoVolume, bool>(nameof(IsExpanded), o => o.IsExpanded);

    /// <summary>Whether the player is in its compact layout (the slider stays closed).</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<VideoVolume, bool>(nameof(IsCompact));

    private bool _expanded;
    private bool _sliderFocused;

    public VideoVolume()
    {
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Center;
        MuteButton = new VideoToggleButton { Name = "PART_Mute" };
        Glyph = new VideoGlyph { Data = VideoGlyphs.Volume, StrokeThickness = 2 };
        MuteButton.Content = Glyph;
        Slider = new VideoRange { Name = "PART_VolumeSlider", Minimum = 0, Maximum = 1, SmallChange = 0.05, VerticalAlignment = VerticalAlignment.Center };
        SliderFrame = new VideoVolumeClip { Child = Slider };
        Children.Add(MuteButton);
        Children.Add(SliderFrame);
        Slider.GotFocus += (_, _) => SetSliderFocused(true);
        Slider.LostFocus += (_, _) => SetSliderFocused(false);
        Slider.DraggingChanged += _ => UpdateExpanded();
    }

    /// <summary>The mute toggle.</summary>
    public VideoToggleButton MuteButton { get; }

    /// <summary>The mute toggle's glyph (volume, low volume or muted).</summary>
    public VideoGlyph Glyph { get; }

    /// <summary>The volume slider, 0 to 1.</summary>
    public VideoRange Slider { get; }

    /// <summary>The frame that clips the slider while it slides open.</summary>
    public VideoVolumeClip SliderFrame { get; }

    /// <summary>Raised when the wheel asks for a volume change (+0.05 or -0.05).</summary>
    public event Action<double>? WheelStep;

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded
    {
        get => _expanded;
        private set
        {
            if (SetAndRaise(IsExpandedProperty, ref _expanded, value))
            {
                SliderFrame.SetExpanded(value);
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPointerOverProperty || change.Property == IsCompactProperty)
        {
            UpdateExpanded();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.Delta.Y != 0)
        {
            WheelStep?.Invoke(e.Delta.Y > 0 ? 0.05 : -0.05);
            e.Handled = true;
        }
    }

    private void SetSliderFocused(bool focused)
    {
        _sliderFocused = focused;
        UpdateExpanded();
    }

    private void UpdateExpanded() => IsExpanded = !IsCompact && (IsPointerOver || _sliderFocused || Slider.IsDragging);
}

/// <summary>Clips the volume slider while it slides open; the theme animates its width under <c>:motion-full</c>.</summary>
internal sealed class VideoVolumeClip : Border
{
    protected override Type StyleKeyOverride => typeof(VideoVolumeClip);

    public void SetExpanded(bool expanded) => PseudoClasses.Set(":expanded", expanded);
}
