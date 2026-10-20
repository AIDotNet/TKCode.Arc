using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Reserves the width of the widest label (plus the icon space) without drawing anything.</summary>
internal sealed class ButtonsLabelSizer : Control
{
    public static readonly StyledProperty<IReadOnlyList<string>?> LabelsProperty =
        AvaloniaProperty.Register<ButtonsLabelSizer, IReadOnlyList<string>?>(nameof(Labels));

    public static readonly StyledProperty<double> LeadingProperty =
        AvaloniaProperty.Register<ButtonsLabelSizer, double>(nameof(Leading));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<ButtonsLabelSizer>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<ButtonsLabelSizer>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<ButtonsLabelSizer>();

    public IReadOnlyList<string>? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>Space before the text (icon plus gap) when the item has an icon.</summary>
    public double Leading { get => GetValue(LeadingProperty); set => SetValue(LeadingProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    static ButtonsLabelSizer()
    {
        AffectsMeasure<ButtonsLabelSizer>(LabelsProperty, LeadingProperty, FontFamilyProperty, FontSizeProperty, FontWeightProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ButtonsLabelSizer>(false);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var labels = Labels;
        if (labels is null || labels.Count == 0)
        {
            return default;
        }
        var key = (labels, FontFamily, FontSize, FontWeight);
        if (!key.Equals(_cacheKey))
        {
            // Measured once per label set and font, not on every layout pass.
            _cacheKey = key;
            var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight);
            double width = 0;
            foreach (var text in labels)
            {
                var formatted = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface,
                    FontSize, null);
                width = Math.Max(width, formatted.WidthIncludingTrailingWhitespace);
            }
            _width = Math.Ceiling(width);
        }
        return new Size(Leading + _width, 0);
    }

    private (IReadOnlyList<string>, FontFamily, double, FontWeight) _cacheKey;
    private double _width;
}
