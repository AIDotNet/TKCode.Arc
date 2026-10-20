using TKCode.Arc.DesignTokens;
using AtomUI.Theme.Resources;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>C# helpers for consuming ArcDesign tokens from any element.</summary>
public static class ArcTokens
{
    /// <summary>
    /// Binds <paramref name="property"/> to an ArcDesign token so it follows theme changes. Uses the raw
    /// own-token key: AtomUI's <c>CreateControlTokenBinding</c> would wrap the key with the owner type.
    /// </summary>
    public static IDisposable Bind(StyledElement target, AvaloniaProperty property, ArcDesignTokenKind kind,
                                   BindingPriority priority = BindingPriority.Template)
        => target.Bind(property, target.GetResourceObservable(kind), priority);

    /// <summary>Reads the current value of an ArcDesign token for <paramref name="owner"/>'s theme scope.</summary>
    public static T Get<T>(Control owner, ArcDesignTokenKind kind)
        => ControlTokenAccessor.Capture<ArcDesign>(owner).GetOwn<T, ArcDesignTokenKind>(kind);

    /// <summary>Reads a color token (brush tokens are unwrapped).</summary>
    public static Color GetColor(Control owner, ArcDesignTokenKind kind)
    {
        if (owner.TryFindResource(kind, owner.ActualThemeVariant, out var value))
        {
            return value switch
            {
                ISolidColorBrush brush => brush.Color,
                Color color => color,
                _ => Colors.Transparent
            };
        }
        return Colors.Transparent;
    }
}

/// <summary>
/// One-shot (non-dynamic) ArcDesign token value for properties that cannot take a dynamic resource,
/// such as <c>Transition.Duration</c>. Mirrors AtomUI's <c>SharedTokenResourceValue</c>.
/// </summary>
public sealed class ArcTokenValueExtension : MarkupExtension
{
    public ArcTokenValueExtension()
    {
    }

    public ArcTokenValueExtension(ArcDesignTokenKind kind) => Kind = kind;

    public ArcDesignTokenKind Kind { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var application = Application.Current;
        if (application is not null && application.TryGetResource(Kind, application.ActualThemeVariant, out var value) && value is not null)
        {
            return value;
        }
        return AvaloniaProperty.UnsetValue;
    }
}
