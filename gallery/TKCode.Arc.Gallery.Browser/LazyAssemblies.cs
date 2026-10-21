using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Loader;

namespace TKCode.Arc.Gallery.Browser;

/// <summary>
/// Loads the assemblies the publish leaves out of the boot list (<c>ArcWasmLazyAssembly</c> in the project file) the first
/// time the runtime cannot find one. The bytes come from <c>_lazy/&lt;Name&gt;.dll</c> through the <c>arc-lazy</c> module
/// that main.js registers. The fetch is synchronous because the runtime asks in the middle of a type load; a build that
/// is not published has every assembly in the boot list and never gets here.
/// </summary>
internal static partial class LazyAssemblies
{
    private static readonly HashSet<string> Asked = [];

    public static void Install() => AssemblyLoadContext.Default.Resolving += Resolve;

    // Trimming and loading assemblies from bytes do not go together; this runs only in a publish without trimming, where the project
    // file left assemblies out of the boot list (Program checks the switch Arc.Gallery.LazyAssemblies, which a trimmed publish leaves unset).
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Used only when the publish is not trimmed.")]
    private static Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
    {
        // The runtime also asks for "<Name>.resources" per culture and for assemblies that do not exist; ask once, and
        // never for resources.
        if (name.Name is not { } simpleName || simpleName.EndsWith(".resources", StringComparison.Ordinal) || !Asked.Add(simpleName))
        {
            return null;
        }
        return Fetch(simpleName) is { } bytes ? context.LoadFromStream(new MemoryStream(bytes)) : null;
    }

    [JSImport("load", "arc-lazy")]
    private static partial byte[]? Fetch(string name);
}
