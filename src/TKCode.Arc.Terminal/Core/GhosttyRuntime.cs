using System.Security.Cryptography;
using System.Text;
using TKCode.Terminal.Core.Interop;

namespace TKCode.Terminal.Core;

/// <summary>进程级的 libghostty-vt 信息：版本、构建标识、类型清单。</summary>
public static unsafe class GhosttyRuntime
{
    private static readonly Lazy<string> VersionValue = new(ReadVersion);

    public static void EnsureInitialized()
    {
        // M0：无进程级初始化。M6 在这里注入 PNG 解码器（GHOSTTY_SYS_OPT_DECODE_PNG）。
    }

    /// <summary>libghostty 的版本字符串（例如 <c>1.3.2-dev+b3dc3d7</c>）。</summary>
    public static string Version => VersionValue.Value;

    public static bool SimdEnabled
    {
        get
        {
            bool simd;
            TerminalCore.Check(GhosttyNative.BuildInfo(GhosttyBuildInfo.Simd, &simd));
            return simd;
        }
    }

    /// <summary>
    /// 握手用的构建标识：版本字符串 + 原生库文件的 SHA-256（build_info 中没有构建哈希，见 02 §3）。
    /// overlay 改变了库字节，所以自动体现在哈希里。
    /// </summary>
    public static string ComputeBuildId(string libraryPath)
    {
        using var stream = File.OpenRead(libraryPath);
        return $"{Version}+sha256:{Convert.ToHexStringLower(SHA256.HashData(stream))}";
    }

    /// <summary>ghostty_type_json() 的类型清单（结构体布局、枚举值、packed 位布局）。</summary>
    public static string TypeManifestJson()
    {
        var p = GhosttyNative.TypeJson();
        return p == null ? string.Empty : Encoding.UTF8.GetString(new ReadOnlySpan<byte>(p, int.MaxValue).SliceToNull());
    }

    private static string ReadVersion()
    {
        GhosttyString s;
        TerminalCore.Check(GhosttyNative.BuildInfo(GhosttyBuildInfo.VersionString, &s));
        return Encoding.UTF8.GetString(s.Ptr, checked((int)s.Len));
    }

    private static ReadOnlySpan<byte> SliceToNull(this ReadOnlySpan<byte> span) => span[..span.IndexOf((byte)0)];
}
