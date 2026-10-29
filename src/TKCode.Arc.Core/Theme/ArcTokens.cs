using TKCode.Arc.DesignTokens;
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
    /// Binds <paramref name="property"/> to an ArcDesign token so it follows theme changes. The key is the
    /// boxed token kind, the key the theme manager publishes the token under.
    /// </summary>
    public static IDisposable Bind(StyledElement target, AvaloniaProperty property, ArcDesignTokenKind kind,
                                   BindingPriority priority = BindingPriority.Template)
        => target.Bind(property, target.GetResourceObservable(kind), priority);

    /// <summary>
    /// Binds the <c>CornerRadius</c> of a code-built part to an ArcDesign token and returns the target, so it follows the
    /// theme's radii (the pixel style zeroes them) and can sit in an expression:
    /// <c>new Border { ... }.BindCornerRadius(ArcDesignTokenKind.RadiusStep12)</c>. Works for any control that registers a
    /// <c>CornerRadius</c> property (Border, templated controls, GlidingIndicator, ...). Use <c>RadiusPill</c> for a capsule
    /// or circle and the <c>RadiusStepN</c> token whose number is the radius the part had.
    /// </summary>
    public static TTarget BindCornerRadius<TTarget>(this TTarget target, ArcDesignTokenKind kind)
        where TTarget : Control
    {
        var property = AvaloniaPropertyRegistry.Instance.FindRegistered(target, nameof(Border.CornerRadius))
                       ?? throw new InvalidOperationException($"{target.GetType().Name} has no CornerRadius property to bind.");
        Bind(target, property, kind);
        return target;
    }

    /// <summary>
    /// Reads the current value of an ArcDesign token for <paramref name="owner"/>'s theme scope. Color tokens are published
    /// as brushes; asking for a <see cref="Color"/> unwraps them. Throws when the token is not found (Arc is not set up, or
    /// the owner is not in a tree that reaches the application's resources) or has another type.
    /// </summary>
    public static T Get<T>(Control owner, ArcDesignTokenKind kind)
    {
        if (!owner.TryFindResource(kind, owner.ActualThemeVariant, out var value))
        {
            throw new InvalidOperationException(
                $"ArcDesign token '{kind}' was not found from {owner.GetType().Name}: is Arc set up (UseArc) and the control in a tree?");
        }
        if (value is T typed)
        {
            return typed;
        }
        if (value is ISolidColorBrush brush && brush.Color is T color)
        {
            return color;
        }
        throw new InvalidOperationException(
            $"ArcDesign token '{kind}' is a {value?.GetType().Name ?? "null"}, not a {typeof(T).Name}.");
    }

    /// <summary>
    /// Reads a corner radius token like <see cref="Get{T}"/>, but returns <paramref name="fallback"/> instead of throwing when
    /// the owner is not in a themed tree yet (a control that reads its radii when it attaches may be built before that).
    /// </summary>
    public static CornerRadius GetCornerRadius(Control owner, ArcDesignTokenKind kind, CornerRadius fallback)
        => owner.TryFindResource(kind, owner.ActualThemeVariant, out var value) && value is CornerRadius radius ? radius : fallback;

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
/// such as <c>Transition.Duration</c>. Reads the application's current value once.
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
