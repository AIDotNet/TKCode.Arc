using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Embedding;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace TKCode.Arc;

/// <summary>
/// Arc's base theme: default ControlThemes, in Arc's look, for the Avalonia built-in controls that Arc templates use
/// without naming a theme, and for the hosts every app meets (<see cref="StyledTypes"/>;
/// design/architecture/base-theme-inventory.md). An app built on Arc alone has no other source for them.
/// </summary>
/// <remarks>
/// <para>
/// The base theme yields per type: for each of <see cref="StyledTypes"/> the application already resolves a default
/// ControlTheme for (FluentTheme and SimpleTheme in <c>App.axaml</c> do for all of them, an application's own theme for
/// one of them), that type's file is not registered, so their keys never clash and their look stays whole; the rest is
/// registered. ScrollViewer and ScrollBar are one unit: if either is defined, neither is registered. The check runs when
/// Arc is set up, after the application loaded its own styles.
/// </para>
/// <para>
/// The theme files live in <c>Theme/Base/</c>, outside the <c>Themes/*Theme.axaml</c> pattern, so the Foundation generator
/// never registers them unconditionally; <see cref="TryRegister"/> is the only way in (Core's package calls it from its
/// commit callback, <c>UseArcCore()</c>).
/// </para>
/// </remarks>
public static class ArcBaseTheme
{
    /// <summary>The built-in control types the base theme gives a default ControlTheme.</summary>
    public static IReadOnlyList<Type> StyledTypes { get; } =
    [
        typeof(ScrollViewer), typeof(ScrollBar), typeof(ItemsControl), typeof(PathIcon), typeof(TextSelectionHandle),
        typeof(OverlayPopupHost), typeof(PopupRoot), typeof(EmbeddableControlRoot), typeof(Window)
    ];

    // One entry per theme file; a file holds the types that must stay together. ScrollViewer and ScrollBar are one entry so
    // an application never ends up with another theme's ScrollViewer next to Arc's ScrollBar.
    private static readonly (Type[] Types, Func<ResourceDictionary> Create)[] Entries =
    [
        ([typeof(ScrollViewer), typeof(ScrollBar)], static () => new ScrollViewerBaseTheme()),
        ([typeof(ItemsControl)], static () => new ItemsControlBaseTheme()),
        ([typeof(PathIcon)], static () => new PathIconBaseTheme()),
        ([typeof(TextSelectionHandle)], static () => new TextSelectionHandleBaseTheme()),
        ([typeof(OverlayPopupHost)], static () => new OverlayPopupHostBaseTheme()),
        ([typeof(PopupRoot)], static () => new PopupRootBaseTheme()),
        ([typeof(EmbeddableControlRoot)], static () => new EmbeddableControlRootBaseTheme()),
        ([typeof(Window)], static () => new WindowBaseTheme())
    ];

    /// <summary>
    /// Creates the base theme's resources: one dictionary that merges the theme files. They are small, so they are
    /// loaded together.
    /// </summary>
    public static ResourceDictionary Create() => Create(static _ => true);

    private static ResourceDictionary Create(Func<Type[], bool> include)
    {
        var resources = new ResourceDictionary();
        foreach (var (types, create) in Entries)
        {
            if (include(types))
            {
                resources.MergedDictionaries.Add(create());
            }
        }
        return resources;
    }

    /// <summary>
    /// Whether <paramref name="resources"/> already resolve a default ControlTheme for every one of
    /// <see cref="StyledTypes"/> (other than <see cref="Window"/>), as FluentTheme and SimpleTheme do, or the base theme
    /// itself. A theme for only some of them, an application's own <see cref="PopupRoot"/> theme for example, does not
    /// count: <see cref="TryRegister"/> then registers the missing types.
    /// </summary>
    /// <param name="resources">An application, a theme (<c>new FluentTheme()</c>) or any resource node.</param>
    public static bool DefinesBuiltInThemes(IResourceNode resources)
    {
        foreach (var type in StyledTypes)
        {
            if (type != typeof(Window) && !DefinesTheme(resources, type))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Whether <see cref="TryRegister"/> would register anything in <paramref name="application"/>.</summary>
    /// <param name="application">The application, after it loaded its own styles.</param>
    public static bool ShouldRegister(Application application)
        => !DefinesBuiltInThemes(application) && Create(types => !DefinesAny(application, types)).MergedDictionaries.Count > 0;

    /// <summary>
    /// Adds the theme files for the types <paramref name="application"/> has no default ControlTheme for yet to its merged
    /// dictionaries; a type it already styles (FluentTheme, SimpleTheme, its own theme) is left alone. Calling it again
    /// does nothing.
    /// </summary>
    /// <param name="application">The application, after it loaded its own styles.</param>
    /// <returns>True when the base theme registered at least one theme in this call.</returns>
    public static bool TryRegister(Application application)
    {
        var resources = Create(types => !DefinesAny(application, types));
        if (resources.MergedDictionaries.Count == 0)
        {
            return false;
        }
        application.Resources.MergedDictionaries.Add(resources);
        // Avalonia.Browser creates its root (an EmbeddableControlRoot) and applies its template before Arc is set up (its
        // AfterApplicationSetup callback runs ahead of AfterSetup), and a control looks up its default theme only when it
        // joins a tree, so the root would keep Avalonia's template, without the overlay layer popups need. A style reaches
        // controls that exist already; it sets the same theme.
        if (resources.TryGetResource(typeof(EmbeddableControlRoot), null, out var root) && root is ControlTheme rootTheme)
        {
            application.Styles.Add(new Style(static x => x.OfType<EmbeddableControlRoot>())
            {
                Setters = { new Setter(StyledElement.ThemeProperty, rootTheme) }
            });
        }
        return true;
    }

    private static bool DefinesAny(IResourceNode resources, Type[] types)
    {
        foreach (var type in types)
        {
            if (DefinesTheme(resources, type))
            {
                return true;
            }
        }
        return false;
    }

    private static bool DefinesTheme(IResourceNode resources, Type type)
        => resources.TryGetResource(type, null, out var value) && value is ControlTheme;
}

// The theme files' x:Class types. Compiled XAML turns the Load call into a direct call of the generated populate method,
// so creating a file is a plain constructor call (no URI loading, AOT safe; design/architecture/foundation.md 5.3).

/// <summary>ScrollViewer and ScrollBar (<c>ScrollViewerBaseTheme.axaml</c>).</summary>
internal sealed partial class ScrollViewerBaseTheme : ResourceDictionary
{
    public ScrollViewerBaseTheme() => AvaloniaXamlLoader.Load(this);
}

/// <summary>ItemsControl (<c>ItemsControlBaseTheme.axaml</c>).</summary>
internal sealed partial class ItemsControlBaseTheme : ResourceDictionary
{
    public ItemsControlBaseTheme() => AvaloniaXamlLoader.Load(this);
}

/// <summary>PathIcon (<c>PathIconBaseTheme.axaml</c>).</summary>
internal sealed partial class PathIconBaseTheme : ResourceDictionary
{
    public PathIconBaseTheme() => AvaloniaXamlLoader.Load(this);
}

/// <summary>TextSelectionHandle (<c>TextSelectionHandleBaseTheme.axaml</c>).</summary>
internal sealed partial class TextSelectionHandleBaseTheme : ResourceDictionary
{
    public TextSelectionHandleBaseTheme() => AvaloniaXamlLoader.Load(this);
}

/// <summary>OverlayPopupHost (<c>OverlayPopupHostBaseTheme.axaml</c>).</summary>
internal sealed partial class OverlayPopupHostBaseTheme : ResourceDictionary
{
    public OverlayPopupHostBaseTheme() => AvaloniaXamlLoader.Load(this);
}

/// <summary>PopupRoot (<c>PopupRootBaseTheme.axaml</c>).</summary>
internal sealed partial class PopupRootBaseTheme : ResourceDictionary
{
    public PopupRootBaseTheme() => AvaloniaXamlLoader.Load(this);
}

/// <summary>EmbeddableControlRoot (<c>EmbeddableControlRootBaseTheme.axaml</c>).</summary>
internal sealed partial class EmbeddableControlRootBaseTheme : ResourceDictionary
{
    public EmbeddableControlRootBaseTheme() => AvaloniaXamlLoader.Load(this);
}

/// <summary>Window (<c>WindowBaseTheme.axaml</c>).</summary>
internal sealed partial class WindowBaseTheme : ResourceDictionary
{
    public WindowBaseTheme() => AvaloniaXamlLoader.Load(this);
}
