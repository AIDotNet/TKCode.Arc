using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Browser;

/// <summary>
/// Loads the assemblies a trimmed publish leaves out of the boot list to load on demand (<c>BlazorWebAssemblyLazyLoad</c> in
/// the project file; today the Special components). <see cref="GalleryModules"/> asks before a page that needs one builds its
/// demos, and the runtime's own lazy loader fetches the file (main.js, <c>arc-lazy</c> module). Should a type of one be needed
/// earlier, the runtime raises the Resolving event: the file is then fetched synchronously and the console says which
/// assembly, because the gallery has a path that skips GalleryModules. To find that path, run the site with Mono's assembly
/// log (<c>MONO_LOG_MASK=asm</c>), which names the assembly that asked.
/// </summary>
internal sealed partial class BrowserAssemblyLoader : IGalleryAssemblyLoader
{
    private readonly HashSet<string> _loaded = new(StringComparer.Ordinal);

    public async Task<bool> LoadAsync(string name)
    {
        if (_loaded.Contains(name))
        {
            return true;
        }
        try
        {
            // False only when the runtime had loaded it already.
            await LoadLazy(name);
        }
        catch (JSException e)
        {
            Console.Error.WriteLine($"Arc gallery: could not load {name}: {e.Message}");
            return false;
        }
        _loaded.Add(name);
        return true;
    }

    public void InstallFallback() => AssemblyLoadContext.Default.Resolving += Resolve;

    // The bytes are the trimmed assembly the publish set aside, so the members it calls are in the trimmed runtime.
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The file is the app's own trimmed assembly.")]
    private Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
    {
        if (name.Name != GalleryModules.SpecialAssembly || _loaded.Contains(name.Name) || LoadLazySync(name.Name) is not { } bytes)
        {
            return null;
        }
        Console.Error.WriteLine($"Arc gallery: {name.Name} was needed before a page asked GalleryModules for it; fetched it synchronously.");
        _loaded.Add(name.Name);
        var assembly = context.LoadFromStream(new MemoryStream(bytes));
        // Its package (themes, tokens, text) is enabled once the code that asked for it has finished.
        Dispatcher.UIThread.Post(() => _ = GalleryModules.EnsureSpecialAsync());
        return assembly;
    }

    [JSImport("loadAsync", "arc-lazy")]
    [return: JSMarshalAs<JSType.Promise<JSType.Boolean>>]
    private static partial Task<bool> LoadLazy(string name);

    [JSImport("loadSync", "arc-lazy")]
    private static partial byte[]? LoadLazySync(string name);
}
