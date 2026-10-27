using System.Runtime.CompilerServices;

[assembly: DisableRuntimeMarshalling]

namespace TKCode.Terminal.Core.Interop;

/// <summary>头文件中的 static inline 帮助函数与宏（不是导出符号，手写等价实现，02 §4.1）。</summary>
internal static class GhosttyHelpers
{
    /// <summary><c>ghostty_mode_new</c>：低 15 位为模式号，最高位表示 ANSI 模式。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ModeNew(ushort value, bool ansi) => (ushort)((value & 0x7FFF) | (ansi ? 0x8000 : 0));

    /// <summary>纯文本 / VT / HTML 的 formatter 选项：外层与两层嵌套的 sized struct 都填好 size。</summary>
    public static unsafe GhosttyFormatterTerminalOptions FormatterOptions(GhosttyFormatterFormat emit)
    {
        var options = GhosttyFormatterTerminalOptions.Create();
        options.Emit = emit;
        options.Extra = GhosttyFormatterTerminalExtra.Create();
        options.Extra.Screen = GhosttyFormatterScreenExtra.Create();
        return options;
    }
}
