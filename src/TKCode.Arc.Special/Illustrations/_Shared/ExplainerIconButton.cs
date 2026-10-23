using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The explainers' 36px round icon button (play / pause, next step, restart, small header actions): an Arc secondary
/// button (Surface fill, 1px Border, hover SurfaceMuted with a resting shadow) with a 16px glyph. It uses the Arc
/// <see cref="Button"/> theme as is. Set <see cref="Icon"/> for a fixed glyph; the play button gets its swapping glyph
/// from <see cref="Explainer"/>. The name is both the accessible name and the tooltip.
/// </summary>
internal sealed class ExplainerIconButton : Button
{
    /// <summary>Defines the <see cref="Icon"/> property.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ExplainerIconButton, Geometry?>(nameof(Icon));

    /// <summary>Defines the <see cref="Label"/> property.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ExplainerIconButton, string?>(nameof(Label));

    public ExplainerIconButton()
    {
        Variant = ButtonVariant.Secondary;
        Size = ControlSize.Small;
        Width = 36;
        Height = 36;
        MinWidth = 36;
        Padding = new Thickness(0);
        CornerRadius = new CornerRadius(18);
    }

    protected override Type StyleKeyOverride => typeof(Button);

    /// <summary>A fixed glyph (24×24 stroked path) shown at 16px.</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>The accessible name and tooltip.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty)
        {
            Content = Icon is { } data ? new ArcGlyph { Data = data, Size = 16 } : null;
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
            Tooltip.SetTip(this, Label);
        }
    }
}
