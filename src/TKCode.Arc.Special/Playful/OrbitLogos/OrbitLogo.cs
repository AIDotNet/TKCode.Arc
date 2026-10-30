using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One integration shown by <see cref="OrbitLogos"/>: a mark on a ring, plus the text its detail card shows.
/// </summary>
/// <remarks>
/// <see cref="Icon"/> takes SVG path data on a 24 by 24 viewBox (the Simple Icons format), an image address (a
/// <c>.png</c>, <c>.jpg</c> or <c>.webp</c> file, or an http(s), avares, file or data URL), an <c>ImageSource</c> or any
/// control. A control is shown on the ring only; the card falls back to the first letter of <see cref="Name"/>.
/// </remarks>
public class OrbitLogo : AvaloniaObject
{
    /// <summary>Stable identifier. When empty, <see cref="Name"/> identifies the logo.</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<OrbitLogo, string?>(nameof(Id));

    /// <summary>The brand or product name, used in the chip's name tag, the card and the accessible name.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<OrbitLogo, string>(nameof(Name), string.Empty);

    /// <summary>The mark: SVG path data (24 by 24), an image address, an image source or a control.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<OrbitLogo, object?>(nameof(Icon));

    /// <summary>
    /// The color a path-data mark is painted in. Leave it out for black-and-white brands so the mark follows the
    /// text color. Image and control marks keep their own colors.
    /// </summary>
    public static readonly StyledProperty<Color?> ColorProperty =
        AvaloniaProperty.Register<OrbitLogo, Color?>(nameof(Color));

    /// <summary>The category the logo belongs to. The category chips are built from these.</summary>
    public static readonly StyledProperty<string?> CategoryProperty =
        AvaloniaProperty.Register<OrbitLogo, string?>(nameof(Category));

    /// <summary>The line of text in the card.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<OrbitLogo, string?>(nameof(Description));

    /// <summary>Whether the integration is connected. It starts the status and is set when a connection succeeds.</summary>
    public static readonly StyledProperty<bool> IsConnectedProperty =
        AvaloniaProperty.Register<OrbitLogo, bool>(nameof(IsConnected));

    /// <summary>Address of the documentation. When set, the card has a Docs link.</summary>
    public static readonly StyledProperty<string?> HrefProperty =
        AvaloniaProperty.Register<OrbitLogo, string?>(nameof(Href));

    /// <inheritdoc cref="IdProperty"/>
    public string? Id
    {
        get => GetValue(IdProperty);
        set => SetValue(IdProperty, value);
    }

    /// <inheritdoc cref="NameProperty"/>
    public string Name
    {
        get => GetValue(NameProperty);
        set => SetValue(NameProperty, value);
    }

    /// <inheritdoc cref="IconProperty"/>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="ColorProperty"/>
    public Color? Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <inheritdoc cref="CategoryProperty"/>
    public string? Category
    {
        get => GetValue(CategoryProperty);
        set => SetValue(CategoryProperty, value);
    }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <inheritdoc cref="IsConnectedProperty"/>
    public bool IsConnected
    {
        get => GetValue(IsConnectedProperty);
        set => SetValue(IsConnectedProperty, value);
    }

    /// <inheritdoc cref="HrefProperty"/>
    public string? Href
    {
        get => GetValue(HrefProperty);
        set => SetValue(HrefProperty, value);
    }

    /// <summary>The identifier selections use: <see cref="Id"/> when set, otherwise <see cref="Name"/>.</summary>
    internal string Key => string.IsNullOrEmpty(Id) ? Name : Id;
}
