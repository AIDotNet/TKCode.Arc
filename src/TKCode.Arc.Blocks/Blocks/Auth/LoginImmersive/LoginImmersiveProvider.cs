using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A single sign-on provider of a <see cref="LoginImmersive"/> page: one equal-width button in the row under "or", with
/// the provider's mark before its name. The name also fills the busy and success sentences ("Opening {name}").
/// </summary>
public sealed class LoginImmersiveProvider : AvaloniaObject
{
    /// <summary>The provider's name, shown on its button.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<LoginImmersiveProvider, string>(nameof(Name), string.Empty);

    /// <summary>The provider's mark on a 24-unit box, filled with <see cref="MarkBrush"/> (null: the button's text colour).</summary>
    public static readonly StyledProperty<Geometry?> MarkProperty =
        AvaloniaProperty.Register<LoginImmersiveProvider, Geometry?>(nameof(Mark));

    /// <summary>The fill of <see cref="Mark"/>; null follows the button's text colour in both themes.</summary>
    public static readonly StyledProperty<IBrush?> MarkBrushProperty =
        AvaloniaProperty.Register<LoginImmersiveProvider, IBrush?>(nameof(MarkBrush));

    /// <summary>An app-defined value that identifies the provider in handlers (for example an OAuth scheme name).</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<LoginImmersiveProvider, object?>(nameof(Tag));

    /// <summary>Creates an empty provider; set <see cref="Name"/> and <see cref="Mark"/>.</summary>
    public LoginImmersiveProvider()
    {
    }

    /// <summary>Creates a provider with a name and a one-colour mark.</summary>
    public LoginImmersiveProvider(string name, Geometry? mark = null)
    {
        Name = name;
        Mark = mark;
    }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="MarkProperty"/>
    public Geometry? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    /// <inheritdoc cref="MarkBrushProperty"/>
    public IBrush? MarkBrush { get => GetValue(MarkBrushProperty); set => SetValue(MarkBrushProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <summary>Several coloured layers drawn instead of <see cref="Mark"/> (the default Google G).</summary>
    internal IReadOnlyList<(Geometry Geometry, IBrush Brush)>? Layers { get; init; }

    /// <summary>Google with its four-colour G.</summary>
    public static LoginImmersiveProvider Google() => new() { Name = "Google", Layers = AuthProviderMarks.Google };

    /// <summary>Apple with its one-colour mark.</summary>
    public static LoginImmersiveProvider Apple() => new("Apple", Geometry.Parse(AuthProviderMarks.AppleData));

    /// <summary>GitHub with its one-colour mark.</summary>
    public static LoginImmersiveProvider GitHub() => new("GitHub", Geometry.Parse(AuthProviderMarks.GitHubData));

    /// <summary>The three providers a page shows while its <see cref="LoginImmersive.Providers"/> list is empty: Google, Apple and GitHub.</summary>
    public static IReadOnlyList<LoginImmersiveProvider> CreateDefaults() => [Google(), Apple(), GitHub()];
}
