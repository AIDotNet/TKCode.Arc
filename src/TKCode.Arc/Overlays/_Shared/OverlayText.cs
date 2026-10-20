using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Overlay copy (titles, descriptions): wraps anywhere and keeps Arc's body line height (font size × 1.4).</summary>
internal sealed class OverlayText : TextBlock
{
    public static readonly StyledProperty<double> LeadingProperty =
        AvaloniaProperty.Register<OverlayText, double>(nameof(Leading), 1.4);

    public double Leading
    {
        get => GetValue(LeadingProperty);
        set => SetValue(LeadingProperty, value);
    }

    public OverlayText()
    {
        TextWrapping = TextWrapping.Wrap;
        UpdateLineHeight();
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontSizeProperty || change.Property == LeadingProperty)
        {
            UpdateLineHeight();
        }
    }

    private void UpdateLineHeight() => LineHeight = FontSize * Leading;
}

/// <summary>Builds <see cref="OverlayText"/> for string (or formattable) content inside overlay surfaces.</summary>
internal sealed class OverlayTextTemplate : IDataTemplate
{
    public double Leading { get; set; } = 1.4;

    public Control? Build(object? param) => new OverlayText
    {
        Text = param as string ?? Convert.ToString(param, System.Globalization.CultureInfo.CurrentCulture),
        Leading = Leading
    };

    public bool Match(object? data) => data is string or IFormattable;
}
