using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TKCode.Arc;

/// <summary>Turns on the desktop PDF backend: PDFium, from the library under <c>runtimes/&lt;rid&gt;/native</c> (design/components/pdf/pdf.md section 5.8).</summary>
public static class ArcPdfNative
{
    /// <summary>A few of the newest functions the bindings use: a different PDFium found first would fail here, not in the middle of a document.</summary>
    private static readonly string[] s_requiredExports =
    [
        "FPDF_InitLibraryWithConfig", "FPDF_LoadMemDocument64", "FPDF_GetPageSizeByIndexF", "FPDFText_GetLooseCharBox", "FPDFDest_GetView"
    ];

    private static readonly object s_gate = new();
    private static bool s_resolverSet;
    private static PdfiumBackend? s_backend;

    /// <summary>
    /// Loads PDFium and makes it the <see cref="ArcPdf.Backend"/>. Returns false, and leaves the current backend in place, when
    /// the library cannot be loaded; the reason goes to <see cref="System.Diagnostics.Trace"/>. Safe to call more than once.
    /// </summary>
    public static bool Use()
    {
        lock (s_gate)
        {
            if (s_backend is not null)
            {
                ArcPdf.Backend = s_backend;
                return true;
            }
            EnsureResolver();
            try
            {
                if (!TryLoadLibrary(typeof(ArcPdfNative).Assembly, null, out var library))
                {
                    Trace.TraceWarning($"TKCode.Arc.Pdf.Native: the PDFium library was not found (run tools/pdf/sync-native.sh in a repository checkout); PDF stays on {ArcPdf.Backend.Name}.");
                    return false;
                }
                foreach (var export in s_requiredExports)
                {
                    if (!NativeLibrary.TryGetExport(library, export, out _))
                    {
                        Trace.TraceWarning($"TKCode.Arc.Pdf.Native: the loaded PDFium has no {export}; PDF stays on {ArcPdf.Backend.Name}.");
                        return false;
                    }
                }
                PdfiumThread.Instance.RunAsync(InitializeLibrary, PdfiumPriority.Lifetime, CancellationToken.None).GetAwaiter().GetResult();
                s_backend = new PdfiumBackend();
                ArcPdf.Backend = s_backend;
                return true;
            }
            catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                Trace.TraceWarning($"TKCode.Arc.Pdf.Native: {exception.Message}; PDF stays on {ArcPdf.Backend.Name}.");
                return false;
            }
        }
    }

    private static unsafe bool InitializeLibrary()
    {
        var config = new PdfiumInterop.LibraryConfig { Version = 2 };
        PdfiumInterop.FPDF_InitLibraryWithConfig(&config);
        return true;
    }

    private static void EnsureResolver()
    {
        if (s_resolverSet)
        {
            return;
        }
        s_resolverSet = true;
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(ArcPdfNative).Assembly, Resolve);
        }
        catch (InvalidOperationException)
        {
            // A resolver was already set for this assembly by the host; keep it.
        }
    }

    private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath) =>
        name == PdfiumInterop.Library && TryLoadLibrary(assembly, searchPath, out var handle) ? handle : 0;

    /// <summary>
    /// Our copy first: a project reference copies it to <c>runtimes/&lt;rid&gt;/native</c> beside the assembly, which the default
    /// probing does not look in. Then the default probing, which is how NuGet consumers find it (deps.json) and where a published
    /// or NativeAOT app has it (next to the executable).
    /// </summary>
    private static bool TryLoadLibrary(Assembly assembly, DllImportSearchPath? searchPath, out nint handle)
    {
        var file = OperatingSystem.IsWindows() ? "pdfium.dll" : OperatingSystem.IsMacOS() ? "libpdfium.dylib" : "libpdfium.so";
        var path = Path.Combine(AppContext.BaseDirectory, "runtimes", CurrentRid(), "native", file);
        return NativeLibrary.TryLoad(path, out handle) || NativeLibrary.TryLoad(PdfiumInterop.Library, assembly, searchPath, out handle);
    }

    private static string CurrentRid()
    {
        var os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            _ => "x64",
        };
        return $"{os}-{arch}";
    }
}
