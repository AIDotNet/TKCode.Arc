using TKCode.Terminal.Core;

namespace TKCode.Terminal.Avalonia;

/// <summary>
/// Cell colors (05-rendering.md §9.1, §9.7): bold-is-bright, then inverse, then faint (mixed halfway towards the cell's
/// background, so it fades on light themes too), then the minimum contrast ratio. Selection colors, drawn by layers, come after.
/// </summary>
public sealed partial class TerminalView
{
    private const int MaxCachedContrast = 4096;

    private readonly Dictionary<(TerminalRgb Fg, TerminalRgb Bg), TerminalRgb> _contrast = [];

    /// <summary>The foreground and background a cell is drawn with (the default background when it has none).</summary>
    internal (TerminalRgb Foreground, TerminalRgb Background) ResolveCellColors(RowSnapshot row, ulong cell, TerminalPalette palette, bool contrastExempt)
    {
        var style = row.Styles[TCell.StyleIndex(cell)];
        var (fg, bg) = BaseColors(style, palette);
        if (style.Flags.HasFlag(StyleFlags.Faint))
        {
            fg = TerminalColorMath.Mix(fg, bg, 0.5);
        }
        if (!contrastExempt && MinimumContrastRatio > 1)
        {
            fg = Contrasted(fg, bg);
        }
        return (fg, bg);
    }

    /// <summary>
    /// The colors the cell is drawn with: custom glyphs, emoji and invisible text are exempt from the minimum contrast
    /// ratio (05-rendering.md §10.5), the rest is not.
    /// </summary>
    internal (TerminalRgb Foreground, TerminalRgb Background) ResolveDrawnColors(RowSnapshot row, int column, TerminalPalette palette)
    {
        var cell = row.Cells[column];
        return ResolveCellColors(row, cell, palette, contrastExempt: IsContrastExempt(row, column, cell));
    }

    private bool IsContrastExempt(RowSnapshot row, int column, ulong cell)
    {
        if (MinimumContrastRatio <= 1)
        {
            return true;
        }
        var cp = (int)TCell.Codepoint(cell);
        return row.Styles[TCell.StyleIndex(cell)].Flags.HasFlag(StyleFlags.Invisible) ||
               (cp is not (0 or ' ') && SpriteProviderFor(cp) is not null) ||
               ((cp >= 0x231A || TCell.HasGrapheme(cell)) && TerminalUnicode.IsEmojiPresentation(cp, GraphemesOf(row, column, cell)));
    }

    /// <summary>The cell's background rectangle color, or <c>null</c> for the default background (nothing to draw).</summary>
    private TerminalRgb? BackgroundOf(RowSnapshot row, ulong cell, TerminalPalette palette)
    {
        var style = row.Styles[TCell.StyleIndex(cell)];
        if (style.Flags.HasFlag(StyleFlags.Inverse))
        {
            return palette.Resolve(BrightForeground(style), foreground: true);
        }
        return style.Background.IsDefault ? null : palette.Resolve(style.Background, foreground: false);
    }

    private (TerminalRgb Foreground, TerminalRgb Background) BaseColors(in ResolvedStyle style, TerminalPalette palette)
    {
        var fg = palette.Resolve(BrightForeground(style), foreground: true);
        var bg = palette.Resolve(style.Background, foreground: false);
        return style.Flags.HasFlag(StyleFlags.Inverse) ? (bg, fg) : (fg, bg);
    }

    /// <summary>Bold text in palette colors 0–7 uses 8–15 when <see cref="BoldIsBright"/> is on.</summary>
    private TerminalColor BrightForeground(in ResolvedStyle style)
    {
        var fg = style.Foreground;
        return BoldIsBright && style.Flags.HasFlag(StyleFlags.Bold) && fg.IsPalette && fg.PaletteIndex < 8
            ? TerminalColor.Palette((byte)(fg.PaletteIndex + 8))
            : fg;
    }

    private TerminalRgb Contrasted(TerminalRgb fg, TerminalRgb bg)
    {
        if (_contrast.TryGetValue((fg, bg), out var adjusted))
        {
            return adjusted;
        }
        if (_contrast.Count >= MaxCachedContrast)
        {
            _contrast.Clear();
        }
        adjusted = TerminalColorMath.EnsureContrast(fg, bg, MinimumContrastRatio);
        _contrast[(fg, bg)] = adjusted;
        return adjusted;
    }
}
