namespace TKCode.Terminal.Core;

/// <summary>
/// 创建核心时的配置。镜像核心与 UI 侧核心中"影响状态"的项必须一致（01 §3.2 规则 2、02 §7.1），
/// 由 pty host 在 session/attach 的应答中下发。
/// </summary>
public readonly record struct TerminalCoreOptions(TerminalSize Size)
{
    /// <summary>主屏回滚的字节上限。libghostty 的默认值很小，必须显式设置。</summary>
    public long ScrollbackMaxBytes { get; init; } = 64L * 1024 * 1024;

    /// <summary>回滚行数上限；null 表示只受字节上限约束。</summary>
    public int? ScrollbackMaxLines { get; init; } = 10_000;

    /// <summary>
    /// 保留的未完成输入（转义序列、UTF-8）上限，0 关闭。开启后快照可以在任意字节位置编码；
    /// 超过上限的超长序列（例如巨大的 Kitty 图像 APC）在其完成之前无法编码快照。
    /// </summary>
    public long ContinuationMaxBytes { get; init; } = 8L * 1024 * 1024;

    /// <summary>OSC 52 读剪贴板改为延后应答（overlay O3）。镜像核心设为 true，由 host 询问 UI 后再写回。</summary>
    public bool DeferClipboardRead { get; init; }

    /// <summary>
    /// 宽度策略：mode 2027（字形簇）的复位默认值。Compat（macOS / Linux 默认）按码点计宽，程序可开启 2027；
    /// Grapheme（Windows，与 ConPTY 的 GLYPH_WIDTH_GRAPHEMES 一致）默认按字形簇（10 §5）。
    /// </summary>
    public WidthPolicy Width { get; init; } = WidthPolicy.Compat;

    public CursorStyle DefaultCursorStyle { get; init; } = CursorStyle.Block;

    public bool DefaultCursorBlink { get; init; }

    /// <summary>XTGETTCAP TN 的应答。</summary>
    public string TerminfoName { get; init; } = "xterm-256color";

    /// <summary>查询应答（DA、XTVERSION、明暗）。</summary>
    public TerminalReplyProfile Replies { get; init; } = TerminalReplyProfile.Default;

    /// <summary>
    /// 一致性测试模式（esctest2 驱动专用，11 §1.1.4）：开启 DECRQCRA 校验和应答、以 VT520 作答 DA、转发 CSI 8 t 窗口尺寸请求。
    /// 校验和应答让程序能逐格读回屏幕内容，产品构建中永远为 null。
    /// </summary>
    public ConformanceMode? Conformance { get; init; }
}

/// <param name="XtChecksumExtension">DECRQCRA 算法位（xterm 的 checksumExtension；esctest 的 --xterm-checksum ≥ 334 对应 23）。</param>
public sealed record ConformanceMode(byte XtChecksumExtension = 23);

public enum WidthPolicy
{
    Compat,
    Grapheme,
}

public enum ColorScheme
{
    Dark,
    Light,
}

/// <summary>由核心自己作答的查询（10 §2）。</summary>
public sealed record TerminalReplyProfile(string XtVersion, bool AdvertiseOsc52, ColorScheme Scheme)
{
    public static readonly TerminalReplyProfile Default = new("TKCode", AdvertiseOsc52: true, ColorScheme.Dark);
}
