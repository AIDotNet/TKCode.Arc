using System.Runtime.InteropServices;

namespace StreamMark.RaTeX;

/// <summary>RaTeX C ABI（native/ratex/crates/ratex-ffi/include/ratex.h）。</summary>
internal static unsafe partial class RaTeXNative
{
    private const string Library = "ratex_ffi";

    [StructLayout(LayoutKind.Sequential)]
    public struct RatexColor
    {
        public float R, G, B, A;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RatexOptions
    {
        public nuint StructSize;
        public int DisplayMode;
        public RatexColor* Color;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RatexResult
    {
        public byte* Data;
        public int ErrorCode;
    }

    [LibraryImport(Library, EntryPoint = "ratex_parse_and_layout", StringMarshalling = StringMarshalling.Utf8)]
    public static partial RatexResult ParseAndLayout(string latex, RatexOptions* options);

    [LibraryImport(Library, EntryPoint = "ratex_free_display_list")]
    public static partial void FreeDisplayList(byte* json);

    [LibraryImport(Library, EntryPoint = "ratex_get_last_error")]
    public static partial byte* GetLastError();
}
