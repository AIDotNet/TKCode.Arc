using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>A field label that can override the font weight supplied by its theme.</summary>
internal sealed class FieldLabel : TextBlock
{
    static FieldLabel()
    {
        // Keep the theme's FontWeight binding active so clearing the override restores the current token value.
        FontWeightProperty.OverrideMetadata<FieldLabel>(new StyledPropertyMetadata<FontWeight>(
            coerce: static (label, weight) => label.GetValue(Field.LabelFontWeightProperty) ?? weight));
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == Field.LabelFontWeightProperty)
        {
            CoerceValue(FontWeightProperty);
        }
    }
}
