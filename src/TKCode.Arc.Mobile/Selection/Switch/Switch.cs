using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc.Mobile;

/// <summary>
/// Mobile switch: a 51 by 31 toggle for settings that take effect immediately. The thumb glides on a spring, the
/// hit area is at least 44 by 44, and there is no hover state. The track fills with the on color when checked.
/// </summary>
public class Switch : ToggleButton
{
    internal static readonly StyledProperty<Thickness> ThumbMarginProperty =
        AvaloniaProperty.Register<Switch, Thickness>(nameof(ThumbMargin));

    internal Thickness ThumbMargin
    {
        get => GetValue(ThumbMarginProperty);
        set => SetValue(ThumbMarginProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(Switch);

    private readonly TranslateTransform _slide = new();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<Control>("PART_ThumbSlide") is { } slide)
        {
            slide.RenderTransform = _slide;
        }
        ApplyThumbMargin();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ThumbMarginProperty)
        {
            ApplyThumbMargin();
        }
    }

    // The spring still runs on ThumbMargin; a transform shows it, so the thumb's glide does not lay out on every frame.
    // These are the offsets the margin gives a left-aligned, vertically centered thumb, rounded to device pixels as
    // layout rounds a margin, so every frame of the glide lands where it did.
    private void ApplyThumbMargin()
    {
        var margin = ThumbMargin;
        var scale = LayoutHelper.GetLayoutScale(this);
        _slide.X = LayoutHelper.RoundLayoutValue(margin.Left, scale);
        _slide.Y = LayoutHelper.RoundLayoutValue((margin.Top - margin.Bottom) / 2, scale);
    }
}
