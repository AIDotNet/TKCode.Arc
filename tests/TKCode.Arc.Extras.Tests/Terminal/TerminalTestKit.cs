using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using TKCode.Terminal.Avalonia;
using TKCode.Terminal.Core;

namespace TKCode.Arc.Tests.Terminal;

/// <summary>Rows, frames and a shown <see cref="TerminalView"/> for the rendering tests (the headless font maps no characters, so glyphs come from <see cref="TerminalView.GlyphOverride"/>).</summary>
internal static class TerminalTestKit
{
    public static readonly TerminalPalette DarkPalette = Palette(new TerminalRgb(200, 200, 200), new TerminalRgb(0, 0, 0));

    public static readonly TerminalPalette LightPalette = Palette(new TerminalRgb(30, 30, 30), new TerminalRgb(255, 255, 255));

    public static TerminalPalette Palette(TerminalRgb fg, TerminalRgb bg, TerminalRgb? cursor = null)
    {
        var colors = new TerminalRgb[256];
        for (var i = 0; i < 16; i++)
        {
            // Palette 0–7 are (10 + i, 0, 0) and 8–15 are (100 + i, 0, 0), so tests can tell a color from its bright variant; 4 is xterm blue.
            colors[i] = i < 8 ? new TerminalRgb((byte)(10 + i), 0, 0) : new TerminalRgb((byte)(100 + i), 0, 0);
        }
        colors[4] = new TerminalRgb(0, 0, 238);
        return new TerminalPalette(fg, bg, cursor, colors);
    }

    public static ulong Cell(int codepoint, CellWidth width = CellWidth.Narrow, int style = 0, bool grapheme = false) =>
        TCell.Pack((uint)codepoint, width, grapheme, style, SemanticContent.Output, false, false);

    public static RowSnapshot Row(ulong[] cells, ResolvedStyle[]? styles = null, IReadOnlyDictionary<int, int[]>? graphemes = null, ulong id = 1) => new()
    {
        RowId = id,
        Version = 1,
        ContentHash = id,
        Cells = cells,
        Styles = styles ?? [ResolvedStyle.Default],
        Graphemes = graphemes,
        Flags = RowFlags.None
    };

    /// <summary>A row of <paramref name="text"/> in one style (style index 1 when <paramref name="style"/> is given).</summary>
    public static RowSnapshot Text(string text, ResolvedStyle? style = null, ulong id = 1) =>
        Row(text.EnumerateRunes().Select(r => Cell(r.Value, style: style is null ? 0 : 1)).ToArray(),
            style is { } s ? [ResolvedStyle.Default, s] : null, id: id);

    public static FrameSnapshot Frame(TerminalPalette palette, CursorState cursor, params RowSnapshot[] rows) => new()
    {
        Generation = 1,
        Columns = rows[0].Cells.Length,
        Rows = rows,
        Cursor = cursor,
        Palette = palette,
        Scrollbar = new ScrollbarState(rows.Length, 0, rows.Length)
    };

    public static FrameSnapshot Frame(params RowSnapshot[] rows) => Frame(DarkPalette, new CursorState(0, 0, false, CursorStyle.Block), rows);

    public static void Paint()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    public static (TerminalView View, Window Window) Show(FrameSnapshot frame, Action<TerminalView>? configure = null)
    {
        var view = new TerminalView { Snapshot = frame, GlyphOverride = static codepoint => (ushort)(codepoint & 0xFFFF) };
        configure?.Invoke(view);
        var window = TestHelpers.Show(view);
        Paint();
        return (view, window);
    }

    /// <summary>Shows a view, runs <paramref name="test"/> and closes the window.</summary>
    public static void With(FrameSnapshot frame, Action<TerminalView> test, Action<TerminalView>? configure = null)
    {
        var (view, window) = Show(frame, configure);
        try
        {
            test(view);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    public static double ColumnX(TerminalView view, int column)
    {
        var m = view.Metrics;
        return (Math.Round(view.Padding * m.Scaling) + column * m.CellWidth) / m.Scaling;
    }

    public static Avalonia.Media.Color ColorOf(TerminalRgb c) => Avalonia.Media.Color.FromRgb(c.R, c.G, c.B);
}
