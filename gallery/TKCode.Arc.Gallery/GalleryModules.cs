using System.Runtime.CompilerServices;
using TKCode.Arc.Gallery.Catalog;

namespace TKCode.Arc.Gallery;

/// <summary>Loads an assembly that the host left out of its startup download (the WebAssembly host).</summary>
public interface IGalleryAssemblyLoader
{
    /// <summary>
    /// Loads the assembly <paramref name="name"/> (its simple name, such as <c>TKCode.Arc.Special</c>) into the
    /// application; true once it is loaded, also when it was loaded before; false when it could not be fetched.
    /// </summary>
    /// <param name="name">The assembly's simple name.</param>
    Task<bool> LoadAsync(string name);
}

/// <summary>
/// The parts of the gallery a host can load when a page first needs them: the Special components (TKCode.Arc.Special),
/// which only the pages of the Special section show. A host that passes an <see cref="IGalleryAssemblyLoader"/> to
/// <c>UseArcGallery</c> starts without them; opening a Special page loads the assembly and enables its package
/// (<see cref="ArcRuntime.UsePackages"/>) before the page builds its demos. Every other host has them from the start.
/// </summary>
/// <remarks>
/// The Blocks package cannot be one of these parts: the top bar (<c>SiteHeader</c>) and the home page (<c>CtaSection</c>,
/// <c>SiteFooter</c>, <c>ExplodedView</c>, <c>CommandPalette</c>) are blocks, so every visit needs it at startup.
/// </remarks>
public static class GalleryModules
{
    /// <summary>The Special components' assembly.</summary>
    public const string SpecialAssembly = "TKCode.Arc.Special";

    private static IGalleryAssemblyLoader? s_loader;
    private static Task<bool>? s_special;

    /// <summary>Whether the Special components are left out until a page needs them.</summary>
    public static bool LoadsOnDemand => s_loader is not null;

    /// <summary>Whether <paramref name="entry"/>'s page can be built now: it needs no part that still has to be loaded.</summary>
    /// <param name="entry">The page's entry.</param>
    public static bool IsReady(GalleryEntry entry)
        => s_loader is null || !NeedsSpecial(entry) || s_special is { IsCompletedSuccessfully: true, Result: true };

    /// <summary>
    /// Loads and enables what <paramref name="entry"/>'s page needs; true when the page can be built, false when a part
    /// could not be loaded (asking again tries again). Pages call it before they create their demos.
    /// </summary>
    /// <param name="entry">The page's entry.</param>
    public static Task<bool> EnsureAsync(GalleryEntry entry) => NeedsSpecial(entry) ? EnsureSpecialAsync() : Task.FromResult(true);

    /// <summary>
    /// Loads and enables the Special components; true when they are ready. Pages go through <see cref="EnsureAsync"/>; a
    /// host calls this when the assembly arrived another way (the runtime asked for it before a page did).
    /// </summary>
    public static Task<bool> EnsureSpecialAsync()
    {
        if (s_loader is not { } loader)
        {
            return Task.FromResult(true);
        }
        if (s_special is null || s_special.IsFaulted || s_special.IsCanceled || s_special is { IsCompletedSuccessfully: true, Result: false })
        {
            s_special = LoadSpecialAsync(loader);
        }
        return s_special;
    }

    /// <summary>
    /// Set by <c>UseArcGallery</c>: <paramref name="loader"/> loads the Special components when a page needs them; null when the
    /// host has them from the start.
    /// </summary>
    internal static void LoadOnDemand(IGalleryAssemblyLoader? loader)
    {
        s_loader = loader;
        s_special = null;
    }

    /// <summary>The Special section's pages (their demos are in <c>Demos/{Playful,Cursor,Ai,Illustrations}</c>).</summary>
    private static bool NeedsSpecial(GalleryEntry entry)
    {
        foreach (var category in GalleryCatalog.Special)
        {
            if (category.AllEntries.Contains(entry))
            {
                return true;
            }
        }
        return false;
    }

    private static async Task<bool> LoadSpecialAsync(IGalleryAssemblyLoader loader)
    {
        if (!await loader.LoadAsync(SpecialAssembly))
        {
            return false;
        }
        EnableSpecial();
        return true;
    }

    // Kept out of LoadSpecialAsync: the interpreter resolves what a method refers to when it first runs it, and the lambda here
    // calls the package registration in TKCode.Arc.Special, which may only be resolved once that assembly is loaded.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void EnableSpecial() => ArcRuntime.Current?.UsePackages(static arc => arc.UseArcSpecial());
}
