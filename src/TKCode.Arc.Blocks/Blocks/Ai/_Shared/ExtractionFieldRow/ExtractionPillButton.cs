using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The "Looks right" pill of a low-confidence field (spec 3.4): 26 high, padding 0 10 0 8, a 13px check 5 before the text
/// (12 / 16.8, weight 500, Foreground), a pill with a 1px Border on Surface; under a mouse or pen SurfaceMuted with
/// BorderStrong (160ms). Keyboard focus draws the shared ring 2px out. The text is also the accessible name.
/// </summary>
[PseudoClasses(HoverPseudoClass)]
internal sealed class ExtractionPillButton : AvaloniaButton
{
    public const string HoverPseudoClass = ":hover";

    /// <summary>The text ("Looks right").</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ExtractionPillButton, string?>(nameof(Text));

    protected override Type StyleKeyOverride => typeof(ExtractionPillButton);

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }
}
