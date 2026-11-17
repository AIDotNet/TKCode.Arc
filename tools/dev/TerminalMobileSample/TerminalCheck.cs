using System.Text;
using TKCode.Terminal.Core;

namespace TKCode.Arc.TerminalMobileSample;

/// <summary>
/// Drives libghostty-vt through <see cref="TerminalCore"/>: writes VT output (SGR, a cursor move, wide characters), reads
/// the screen back as text and as a frame, and encodes a key. Every step crosses into the native library.
/// </summary>
internal static class TerminalCheck
{
    public sealed record Result(bool Passed, string Detail, FrameSnapshot? Frame);

    public static Result Run()
    {
        var failures = new List<string>();
        using var core = TerminalCore.Create(new TerminalCoreOptions(new TerminalSize(40, 8, 8, 16)));
        core.Write("\e[1;32mHello\e[0m, ghostty 世界\r\nline 2\e[4;6Hxyz"u8);

        var text = core.FormatPlainText();
        Expect(text.Contains("Hello, ghostty 世界"), "row 1 text", failures);
        Expect(text.Contains("line 2"), "row 2 text", failures);
        Expect(core.Cursor == (8, 3), $"cursor {core.Cursor}", failures);

        var frame = core.CaptureFrame(force: true);
        if (frame is null)
        {
            failures.Add("no frame");
        }
        else
        {
            var row = frame.Rows[0];
            Expect(row.TextAt(0) == "H" && row.TextAt(15) == "世", "frame cells", failures);
            Expect((row.Styles[TCell.StyleIndex(row.Cells[0])].Flags & StyleFlags.Bold) != 0, "bold style", failures);
            Expect(frame.Rows[3].TextAt(5) == "x", "frame row 4", failures);
        }

        using var encoder = new InputEncoder();
        Span<byte> key = stackalloc byte[16];
        var length = encoder.EncodeKey(new KeyInput(TerminalKey.ArrowUp, KeyAction.Press, KeyMods.None), key);
        Expect(Encoding.ASCII.GetString(key[..length]) == "\e[A", "arrow key encoding", failures);

        var detail = $"ghostty={GhosttyRuntime.Version} simd={GhosttyRuntime.SimdEnabled}" +
                     (failures.Count == 0 ? string.Empty : $" failed={string.Join(",", failures)}");
        return new Result(failures.Count == 0, detail, frame);
    }

    private static void Expect(bool condition, string what, List<string> failures)
    {
        if (!condition)
        {
            failures.Add(what);
        }
    }
}
