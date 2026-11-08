using Avalonia;
using Avalonia.Controls.Primitives;

namespace ArcLibrary;

/// <summary>
/// A tile with a <see cref="Title"/>, a <see cref="Value"/> and an optional <see cref="Caption"/> on Arc's surface. Its
/// look is in Themes/StatTileTheme.axaml; its sizes are design tokens (<see cref="StatTileToken"/>), so a theme preset or
/// <c>ArcTheme.Apply(t =&gt; t.Token("ArcLibraryIdentifierStatTile", "Padding", "20"))</c> changes them at run time.
/// Add your own controls next to it the same way: the control, its token class and Themes/&lt;Control&gt;Theme.axaml.
/// </summary>
public class StatTile : TemplatedControl
{
    /// <summary>Defines the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<StatTile, string?>(nameof(Title));

    /// <summary>Defines the <see cref="Value"/> property.</summary>
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<StatTile, string?>(nameof(Value));

    /// <summary>Defines the <see cref="Caption"/> property.</summary>
    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<StatTile, string?>(nameof(Caption));

    /// <summary>The small label above the value.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>The value, in large type.</summary>
    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>A line under the value; hidden when empty.</summary>
    public string? Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }
}
