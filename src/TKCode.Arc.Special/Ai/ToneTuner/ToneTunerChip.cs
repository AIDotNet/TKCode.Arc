using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A voice chip of <see cref="ToneTuner"/>: a toggle button (pressed state reported to assistive tech) with an optional
/// 14px glyph and a label. Its look lives in the ToneTuner theme.
/// </summary>
internal sealed class ToneTunerChip : ToggleButton
{
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ToneTunerChip, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ToneTunerChip, string?>(nameof(Label));

    public static readonly StyledProperty<bool> ShowsIconProperty =
        AvaloniaProperty.Register<ToneTunerChip, bool>(nameof(ShowsIcon), true);

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>False drops the glyph (narrow cards) so the label never clips.</summary>
    public bool ShowsIcon { get => GetValue(ShowsIconProperty); set => SetValue(ShowsIconProperty, value); }

    /// <summary>The preset the chip stands for.</summary>
    public TonePreset? Preset { get; set; }

    protected override Type StyleKeyOverride => typeof(ToneTunerChip);

    /// <summary>The tuner decides what a press does (a second press returns to neutral), so the chip does not flip itself.</summary>
    protected override void Toggle()
    {
    }
}

/// <summary>
/// Lays the voice chips out: left to right at their own widths, or (narrow cards) in one row of equal columns that fill
/// the width.
/// </summary>
internal sealed class ToneTunerChipRow : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<ToneTunerChipRow, double>(nameof(Spacing), 8);

    public static readonly StyledProperty<bool> IsUniformProperty =
        AvaloniaProperty.Register<ToneTunerChipRow, bool>(nameof(IsUniform));

    static ToneTunerChipRow()
    {
        AffectsMeasure<ToneTunerChipRow>(SpacingProperty, IsUniformProperty);
    }

    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    public bool IsUniform { get => GetValue(IsUniformProperty); set => SetValue(IsUniformProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var visible = Children.Where(c => c.IsVisible).ToList();
        var gaps = Spacing * Math.Max(0, visible.Count - 1);
        var height = 0.0;
        var width = 0.0;
        if (IsUniform && double.IsFinite(availableSize.Width) && visible.Count > 0)
        {
            var column = Math.Max(0, (availableSize.Width - gaps) / visible.Count);
            foreach (var child in visible)
            {
                child.Measure(new Size(column, availableSize.Height));
                height = Math.Max(height, child.DesiredSize.Height);
            }
            return new Size(availableSize.Width, height);
        }
        foreach (var child in visible)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(width + gaps, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var visible = Children.Where(c => c.IsVisible).ToList();
        var gaps = Spacing * Math.Max(0, visible.Count - 1);
        var column = visible.Count > 0 ? Math.Max(0, (finalSize.Width - gaps) / visible.Count) : 0;
        var x = 0.0;
        foreach (var child in visible)
        {
            var width = IsUniform ? column : child.DesiredSize.Width;
            child.Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width + Spacing;
        }
        return finalSize;
    }
}
