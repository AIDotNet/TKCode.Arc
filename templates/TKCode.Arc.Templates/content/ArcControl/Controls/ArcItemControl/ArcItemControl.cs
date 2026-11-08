using Avalonia;
using Avalonia.Controls.Primitives;

namespace ArcItemNamespace.Controls;

// Arc finds this control's theme (Themes/ArcItemControlTheme.axaml) and its token values only once the app's assembly
// is registered. Do it once for all of the app's controls, in Program.cs inside UseArc(...):
//     .UsePackage("ArcItemAssemblyName", static p => TKCode.Arc.Generated.ArcItemAssemblyIdentifier.ArcGeneratedAssets.AddTo(p))
// (an app made with dotnet new tkcode-arc has this line commented out, ready to use). The generated class exists as soon
// as the project has one token class or theme file.

/// <summary>
/// A tile that shows a <see cref="Title"/>, a <see cref="Value"/> and an optional <see cref="Caption"/> on Arc's surface.
/// Its look is in Themes/ArcItemControlTheme.axaml; its sizes are design tokens (ArcItemControlToken), so a theme preset
/// or <c>ArcTheme.Apply(t =&gt; t.Token("ArcItemControl", "Padding", "20"))</c> can change them at run time.
/// </summary>
public class ArcItemControl : TemplatedControl
{
    /// <summary>Defines the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ArcItemControl, string?>(nameof(Title));

    /// <summary>Defines the <see cref="Value"/> property.</summary>
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ArcItemControl, string?>(nameof(Value));

    /// <summary>Defines the <see cref="Caption"/> property.</summary>
    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<ArcItemControl, string?>(nameof(Caption));

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
