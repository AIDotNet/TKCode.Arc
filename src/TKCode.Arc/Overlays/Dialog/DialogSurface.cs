using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace TKCode.Arc;

/// <summary>
/// The panel of a <see cref="Dialog"/>: header (title, description, close button) over the body and actions, the
/// whole panel scrolling when taller than the viewport. Reusable on its own with a
/// <see cref="TKCode.Arc.Primitives.ModalPresence"/> (Card's quick preview).
/// </summary>
[PseudoClasses(SmallPseudoClass, LargePseudoClass)]
public class DialogSurface : ModalSurface
{
    public const string SmallPseudoClass = ":small";
    public const string LargePseudoClass = ":large";

    /// <summary>Panel width preset: Small 360, Medium 440 (Arc), Large 560.</summary>
    public static readonly StyledProperty<ControlSize> SizeProperty =
        AvaloniaProperty.Register<DialogSurface, ControlSize>(nameof(Size), ControlSize.Medium);

    public ControlSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>
    /// False hides the header row: the title, the description, the close button and the line under them, for a panel
    /// that brings its own heading (a branded sign-in, for example). Keep <c>Title</c> set: it stays the accessible name
    /// of the panel. Without the close button the dialog closes with Escape, a click outside or its own buttons.
    /// Defaults to true.
    /// </summary>
    public static readonly StyledProperty<bool> ShowHeaderProperty =
        AvaloniaProperty.Register<DialogSurface, bool>(nameof(ShowHeader), true);

    /// <inheritdoc cref="ShowHeaderProperty"/>
    public bool ShowHeader { get => GetValue(ShowHeaderProperty); set => SetValue(ShowHeaderProperty, value); }

    static DialogSurface()
    {
        CloseButtonNameProperty.OverrideDefaultValue<DialogSurface>("Close dialog");
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty)
        {
            // The theme selects on :small / :large instead of property selectors (an activator and a boxed comparison each).
            PseudoClasses.Set(SmallPseudoClass, Size == ControlSize.Small);
            PseudoClasses.Set(LargePseudoClass, Size == ControlSize.Large);
        }
    }
}
