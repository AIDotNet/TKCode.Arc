using System.Runtime.CompilerServices;

namespace TKCode.Terminal.Core;

/// <summary>
/// 一帧屏幕的只读快照，渲染层只依赖它，不依赖 libghostty 的任何类型（01-architecture.md §6.1，02 §6.4）。
/// 发布后不可变；未变化的行与上一帧共享同一个 <see cref="RowSnapshot"/> 对象。
/// </summary>
public sealed class FrameSnapshot
{
    public required long Generation { get; init; }
    public required int Columns { get; init; }
    public required RowSnapshot[] Rows { get; init; }
    public required CursorState Cursor { get; init; }

    /// <summary>默认前景 / 背景 / 光标色与 256 色调色板。调色板颜色在渲染层解析（粗体变亮、最小对比度等策略在那里统一处理）。</summary>
    public required TerminalPalette Palette { get; init; }

    public required ScrollbarState Scrollbar { get; init; }

    /// <summary>选区在视口内的逐行区间（没有选区时为空数组）。没变化时沿用上一帧的数组引用。</summary>
    public CellSpan[] Selection { get; init; } = [];

    /// <summary>活动搜索的视口高亮；没有搜索或搜索词为空时为 null。</summary>
    public SearchHighlights? Search { get; init; }

    /// <summary>超链接表：<see cref="ResolvedStyle.HyperlinkId"/> - 1 为下标。只增不减（同一地址跨帧 id 不变）。</summary>
    public IReadOnlyList<string> Links { get; init; } = [];

    /// <summary>某样式对应的超链接地址。</summary>
    public string? LinkOf(in ResolvedStyle style) => style.HyperlinkId > 0 && style.HyperlinkId <= Links.Count ? Links[style.HyperlinkId - 1] : null;

    /// <summary>mode 2026 期间被冻结的帧。</summary>
    public bool RenderHeld { get; init; }
}

public sealed class RowSnapshot
{
    /// <summary>行 id：跨滚动不变、永不复用（由 libghostty 的 128 位行 id 映射而来，02 §8.3）。</summary>
    public required ulong RowId { get; init; }

    /// <summary>同一 RowId 的内容每变化一次加一。</summary>
    public required int Version { get; init; }

    /// <summary>Cells + Styles + Graphemes 的 64 位哈希；渲染层的整形缓存以它为键。</summary>
    public required ulong ContentHash { get; init; }

    /// <summary>8 字节 cell（<see cref="TCell"/>），长度等于列数。</summary>
    public required ulong[] Cells { get; init; }

    /// <summary>行内去重后的样式；[0] 永远是默认样式。</summary>
    public required ResolvedStyle[] Styles { get; init; }

    /// <summary>稀疏表：列 → 基字符之外的码点（字形簇）。</summary>
    public required IReadOnlyDictionary<int, int[]>? Graphemes { get; init; }

    public required RowFlags Flags { get; init; }

    /// <summary>该行的完整文本（基字符 + 字形簇）。</summary>
    public string? TextAt(int column)
    {
        var cell = Cells[column];
        var cp = TCell.Codepoint(cell);
        if (cp == 0)
        {
            return null;
        }
        if (!TCell.HasGrapheme(cell) || Graphemes is null || !Graphemes.TryGetValue(column, out var extra))
        {
            return char.ConvertFromUtf32((int)cp);
        }
        var sb = new System.Text.StringBuilder(char.ConvertFromUtf32((int)cp));
        foreach (var e in extra)
        {
            sb.Append(char.ConvertFromUtf32(e));
        }
        return sb.ToString();
    }
}

[Flags]
public enum RowFlags : byte
{
    None = 0,
    SoftWrapped = 1,
    WrapContinuation = 2,
    PromptLine = 4,
    HasHyperlink = 8,
    KittyPlaceholder = 16,
}

/// <summary>
/// 8 字节 cell（02 §6.5）：0–20 码点，21–22 宽度，23 有字形簇，24–39 样式下标，40–41 语义（输出 / 输入 / 提示符），
/// 42 超链接，43 Kitty 占位符，44–63 保留。与 GhosttyCell 解耦（上游位布局不受 ABI 保护）。
/// </summary>
public static class TCell
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Pack(uint codepoint, CellWidth width, bool grapheme, int styleIndex, SemanticContent semantic, bool hyperlink, bool kittyPlaceholder) =>
        (codepoint & 0x1FFFFFUL) | ((ulong)width << 21) | (grapheme ? 1UL << 23 : 0) | ((ulong)(ushort)styleIndex << 24) |
        ((ulong)semantic << 40) | (hyperlink ? 1UL << 42 : 0) | (kittyPlaceholder ? 1UL << 43 : 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Codepoint(ulong cell) => (uint)(cell & 0x1FFFFF);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CellWidth Width(ulong cell) => (CellWidth)((cell >> 21) & 3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasGrapheme(ulong cell) => ((cell >> 23) & 1) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int StyleIndex(ulong cell) => (int)((cell >> 24) & 0xFFFF);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SemanticContent Semantic(ulong cell) => (SemanticContent)((cell >> 40) & 3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Hyperlink(ulong cell) => ((cell >> 42) & 1) != 0;
}

public enum CellWidth : byte
{
    Narrow = 0,
    Wide = 1,
    SpacerTail = 2,
    SpacerHead = 3,
}

public enum SemanticContent : byte
{
    Output = 0,
    Input = 1,
    Prompt = 2,
}

/// <summary>32 位颜色：高 8 位种类（0 默认、1 调色板、2 RGB），低 24 位值。</summary>
public readonly record struct TerminalColor(uint Raw)
{
    public static readonly TerminalColor Default = new(0);

    public static TerminalColor Palette(byte index) => new((1u << 24) | index);

    public static TerminalColor Rgb(byte r, byte g, byte b) => new((2u << 24) | ((uint)r << 16) | ((uint)g << 8) | b);

    public bool IsDefault => Raw >> 24 == 0;

    public bool IsPalette => Raw >> 24 == 1;

    public byte PaletteIndex => (byte)Raw;

    public TerminalRgb ToRgb() => new((byte)(Raw >> 16), (byte)(Raw >> 8), (byte)Raw);
}

[Flags]
public enum StyleFlags : byte
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Faint = 4,
    Blink = 8,
    Inverse = 16,
    Invisible = 32,
    Strikethrough = 64,
    Overline = 128,
}

/// <summary>下划线样式，取值同 GHOSTTY_SGR_UNDERLINE_*。</summary>
public enum UnderlineStyle : byte
{
    None = 0,
    Single = 1,
    Double = 2,
    Curly = 3,
    Dotted = 4,
    Dashed = 5,
}

public readonly record struct ResolvedStyle(
    TerminalColor Foreground, TerminalColor Background, TerminalColor UnderlineColor,
    StyleFlags Flags, UnderlineStyle Underline, int HyperlinkId = 0)
{
    public static readonly ResolvedStyle Default = new(TerminalColor.Default, TerminalColor.Default, TerminalColor.Default, StyleFlags.None, UnderlineStyle.None);
}

public readonly record struct TerminalRgb(byte R, byte G, byte B);

/// <summary>默认颜色与 256 色调色板（数组只读，跨帧共享）。</summary>
public sealed record TerminalPalette(TerminalRgb Foreground, TerminalRgb Background, TerminalRgb? Cursor, TerminalRgb[] Colors)
{
    public TerminalRgb Resolve(TerminalColor color, bool foreground) => (color.Raw >> 24) switch
    {
        1 => Colors[color.PaletteIndex],
        2 => color.ToRgb(),
        _ => foreground ? Foreground : Background,
    };
}

public enum CursorStyle
{
    Bar = 0,
    Block = 1,
    Underline = 2,
    BlockHollow = 3,
}

/// <summary>光标。<see cref="Visible"/> 为 false 时（DECTCEM 关闭或不在视口内）不绘制。</summary>
public readonly record struct CursorState(int Column, int Row, bool Visible, CursorStyle Style, bool Blinking = false, bool WideTail = false, bool PasswordInput = false);

/// <summary>滚动条：总行数、视口顶部所在行、视口行数。</summary>
public readonly record struct ScrollbarState(long Total, long Offset, long Length);
