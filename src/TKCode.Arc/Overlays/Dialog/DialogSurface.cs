using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The panel of a <see cref="Dialog"/>: header (title, description, close button) over the body and actions, the
/// whole panel scrolling when taller than the viewport. Reusable on its own with a
/// <see cref="TKCode.Arc.Primitives.ModalPresence"/> (Card's quick preview).
/// </summary>
public class DialogSurface : ModalSurface
{
    /// <summary>Panel width preset: Small 360, Medium 440 (Arc), Large 560.</summary>
    public static readonly StyledProperty<ControlSize> SizeProperty =
        AvaloniaProperty.Register<DialogSurface, ControlSize>(nameof(Size), ControlSize.Medium);

    public ControlSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    static DialogSurface()
    {
        CloseButtonNameProperty.OverrideDefaultValue<DialogSurface>("Close dialog");
    }
}
