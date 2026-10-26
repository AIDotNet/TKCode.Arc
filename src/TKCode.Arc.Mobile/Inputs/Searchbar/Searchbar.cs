using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;

namespace TKCode.Arc.Mobile;

/// <summary>
/// Mobile search bar: a <see cref="TextField"/> with a search glyph before the text and a pill shape. Enter raises
/// <see cref="Submitted"/>, and the soft keyboard offers a Search key.
/// </summary>
public class Searchbar : TextField
{
    /// <summary>Raised when the person presses Enter or the keyboard's Search key.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SubmittedEvent =
        RoutedEvent.Register<Searchbar, RoutedEventArgs>(nameof(Submitted), RoutingStrategies.Bubble);

    static Searchbar()
    {
        IconProperty.OverrideDefaultValue<Searchbar>(ArcIcons.Search);
    }

    public Searchbar()
    {
        PseudoClasses.Set(":search", true);
        TextInputOptions.SetReturnKeyType(this, TextInputReturnKeyType.Search);
    }

    /// <inheritdoc cref="SubmittedEvent"/>
    public event EventHandler<RoutedEventArgs>? Submitted
    {
        add => AddHandler(SubmittedEvent, value);
        remove => RemoveHandler(SubmittedEvent, value);
    }

    // Searchbar shares TextField's theme; the :search pseudo-class makes it a pill.
    protected override Type StyleKeyOverride => typeof(TextField);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Enter)
        {
            e.Handled = true;
            RaiseEvent(new RoutedEventArgs(SubmittedEvent));
        }
    }
}
