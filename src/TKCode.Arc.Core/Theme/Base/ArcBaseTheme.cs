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
/// The base theme yields to any theme that already styles these controls: when the application resolves a default
/// ControlTheme for one of <see cref="StyledTypes"/> other than <see cref="Window"/> (FluentTheme and SimpleTheme in
/// <c>App.axaml</c> do), nothing is registered, so their keys never clash and their look stays whole. The check runs when
/// Arc is set up, after the application loaded its own styles.
/// </para>
/// <para>
/// <see cref="Window"/> yields on its own: an application that themes only its window keeps that theme and still gets the
/// rest of the base theme. A window that sets its template itself (the gallery's) is not affected either way.
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

    /// <summary>
    /// Creates the base theme's resources: one dictionary that merges the theme files. They are small, so they are
    /// loaded together.
    /// </summary>
    public static ResourceDictionary Create() => Create(includeWindow: true);

    private static ResourceDictionary Create(bool includeWindow)
    {
        var resources = new ResourceDictionary();
        resources.MergedDictionaries.Add(new ScrollViewerBaseTheme());
        resources.MergedDictionaries.Add(new ItemsControlBaseTheme());
        resources.MergedDictionaries.Add(new PathIconBaseTheme());
        resources.MergedDictionaries.Add(new TextSelectionHandleBaseTheme());
        resources.MergedDictionaries.Add(new PopupHostBaseTheme());
        if (includeWindow)
        {
            resources.MergedDictionaries.Add(new WindowBaseTheme());
        }
        return resources;
    }

    /// <summary>
    /// Whether <paramref name="resources"/> already resolve a default ControlTheme for one of <see cref="StyledTypes"/>
    /// (FluentTheme, SimpleTheme, another theme, or the base theme itself). A theme for <see cref="Window"/> alone does not
    /// count: an application's own window theme is not a theme for the built-in controls.
    /// </summary>
    /// <param name="resources">An application, a theme (<c>new FluentTheme()</c>) or any resource node.</param>
    public static bool DefinesBuiltInThemes(IResourceNode resources)
    {
        foreach (var type in StyledTypes)
        {
            if (type != typeof(Window) && DefinesTheme(resources, type))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Whether <see cref="TryRegister"/> would register the base theme in <paramref name="application"/>.</summary>
    /// <param name="application">The application, after it loaded its own styles.</param>
    public static bool ShouldRegister(Application application) => !DefinesBuiltInThemes(application);

    /// <summary>
    /// Adds the base theme to <paramref name="application"/>'s merged dictionaries unless another theme already styles the
    /// built-in controls (see <see cref="ShouldRegister"/>). The <see cref="Window"/> theme is left out when the application
    /// already has one. Calling it again does nothing.
    /// </summary>
    /// <param name="application">The application, after it loaded its own styles.</param>
    /// <returns>True when the base theme was registered by this call.</returns>
    public static bool TryRegister(Application application)
    {
        if (!ShouldRegister(application))
        {
            return false;
        }
        var resources = Create(includeWindow: !DefinesTheme(application, typeof(Window)));
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

/// <summary>OverlayPopupHost, PopupRoot and EmbeddableControlRoot (<c>PopupHostBaseTheme.axaml</c>).</summary>
internal sealed partial class PopupHostBaseTheme : ResourceDictionary
{
    public PopupHostBaseTheme() => AvaloniaXamlLoader.Load(this);
}

/// <summary>Window (<c>WindowBaseTheme.axaml</c>).</summary>
internal sealed partial class WindowBaseTheme : ResourceDictionary
{
    public WindowBaseTheme() => AvaloniaXamlLoader.Load(this);
}
