using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Terminal.Core;

namespace TKCode.Terminal.Avalonia;

/// <summary>
/// Faces (05-rendering.md §6.2, §6.5, §7.4): regular, bold, italic and bold-italic faces of <see cref="FontFamily"/>, a
/// per-code-point fallback keyed by style, the emoji face, and the shaping path for grapheme clusters. Cells without a
/// cluster and with text presentation stay on the plain cmap path, so ASCII is never shaped.
/// </summary>
public sealed partial class TerminalView
{
    private const int MaxShapedClusters = 4096;

    private readonly GlyphTypeface?[] _styledFaces = new GlyphTypeface?[4];
    private readonly Dictionary<(int Codepoint, FaceStyle Style), GlyphTypeface?> _fallback = [];
    private readonly Dictionary<ShapeKey, GlyphInfo[]> _shaped = [];
    private FontFamily? _primaryFamily;
    private GlyphTypeface? _emojiFace;
    private bool _emojiFaceResolved;

    /// <summary>Shaping calls so far (tests: ASCII rows never shape).</summary>
    internal int ShapeCalls { get; private set; }

    /// <summary>The four faces, indexed by the SGR bold and italic flags.</summary>
    internal enum FaceStyle
    {
        Regular = 0,
        Bold = 1,
        Italic = 2,
        BoldItalic = 3,
    }

    private readonly record struct ShapeKey(string Text, GlyphTypeface Face, double EmSize);

    /// <summary>One cell's glyphs: either one cmap glyph or a shaped cluster, positioned relative to the cell's left edge and the row's top.</summary>
    private readonly struct CellGlyphs
    {
        public GlyphTypeface Face { get; init; }
        public double EmSize { get; init; }
        public ushort Glyph { get; init; }
        public GlyphInfo[]? Shaped { get; init; }
        public string Text { get; init; }

        /// <summary>Emoji: scaled to their cells, so they get their own run with its own size and origin.</summary>
        public bool OwnRun { get; init; }
        public double OffsetX { get; init; }
        public double Baseline { get; init; }
    }

    internal static FaceStyle StyleOf(in ResolvedStyle style) =>
        (style.Flags.HasFlag(StyleFlags.Bold) ? FaceStyle.Bold : 0) | (style.Flags.HasFlag(StyleFlags.Italic) ? FaceStyle.Italic : 0);

    private (FontStyle Style, FontWeight Weight) FontOf(FaceStyle style) =>
        ((style & FaceStyle.Italic) != 0 ? FontStyle.Italic : FontStyle.Normal, (style & FaceStyle.Bold) != 0 ? BoldFontWeight : FontWeight);

    private void ClearFaces()
    {
        Array.Clear(_styledFaces);
        _fallback.Clear();
        _shaped.Clear();
        _sprites.Clear();
        _primaryFamily = null;
        _emojiFace = null;
        _emojiFaceResolved = false;
    }

    /// <summary>
    /// The face of the first family in <see cref="FontFamily"/> that the platform has. Avalonia on Android answers a family
    /// it does not have with the system default (proportional) face instead of a miss, which would end the list at its
    /// first name; so a face named differently from the family asked for counts as a miss here, except for the generic
    /// <c>monospace</c>. A family from a font source (<c>avares://...#Name</c>, <c>fonts:...#Name</c>), or no match at all:
    /// Avalonia's choice.
    /// </summary>
    private GlyphTypeface PrimaryFace()
    {
        if (_face is { } face)
        {
            return face;
        }
        var family = FontFamily;
        var weight = FontWeight;
        // System families only: no key (one name) or a comma-separated list of names (a "compositefont:" key without '#').
        if (family.Key is null || (family.Key.Source.Scheme == "compositefont" && !family.Key.Source.OriginalString.Contains('#')))
        {
            foreach (var name in family.FamilyNames)
            {
                if (FontManager.Current.TryGetGlyphTypeface(new Typeface(name, FontStyle.Normal, weight), out var candidate) &&
                    (string.Equals(candidate.FamilyName, name, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(name, "monospace", StringComparison.OrdinalIgnoreCase)))
                {
                    _primaryFamily = new FontFamily(name);
                    return _face = candidate;
                }
            }
        }
        _primaryFamily = family;
        return _face = new Typeface(family, FontStyle.Normal, weight).GlyphTypeface;
    }

    /// <summary>
    /// The primary family's face for <paramref name="style"/>: the family's own bold or italic when it has one, otherwise
    /// the regular face with Avalonia's synthesized bold or oblique (what xterm.js and Ghostty do by default). A bold weight
    /// below <see cref="FontWeight.SemiBold"/> (<see cref="BoldFontWeight"/>) is not emboldened.
    /// </summary>
    internal GlyphTypeface PrimaryFace(FaceStyle style)
    {
        var regular = PrimaryFace();
        if (style == FaceStyle.Regular)
        {
            return regular;
        }
        if (_styledFaces[(int)style] is { } cached)
        {
            return cached;
        }
        var (fontStyle, weight) = FontOf(style);
        var face = FontManager.Current.TryGetGlyphTypeface(new Typeface(_primaryFamily ?? FontFamily, fontStyle, weight), out var candidate) &&
                   string.Equals(candidate.FamilyName, regular.FamilyName, StringComparison.OrdinalIgnoreCase)
            ? candidate
            : regular;
        // A font manager may answer with a face that has only part of the request (an italic for bold italic): synthesize the rest.
        var missing = FontSimulations.None;
        if (weight >= FontWeight.SemiBold && face.Weight < FontWeight.SemiBold && !face.FontSimulations.HasFlag(FontSimulations.Bold))
        {
            missing |= FontSimulations.Bold;
        }
        if (fontStyle != FontStyle.Normal && face.Style == FontStyle.Normal && !face.FontSimulations.HasFlag(FontSimulations.Oblique))
        {
            missing |= FontSimulations.Oblique;
        }
        if (missing != FontSimulations.None)
        {
            face = new GlyphTypeface(face.PlatformTypeface, face.FontSimulations | missing);
        }
        return _styledFaces[(int)style] = face;
    }

    /// <summary>
    /// The face that draws <paramref name="codepoint"/> in <paramref name="style"/>: the primary face, or a fallback font
    /// asked for the same style and weight. A fallback font without that style is drawn with its regular face rather than
    /// a synthesized one.
    /// </summary>
    private GlyphTypeface? FaceFor(int codepoint, FaceStyle style = FaceStyle.Regular)
    {
        var primary = PrimaryFace(style);
        if (primary.CharacterToGlyphMap.TryGetGlyph(codepoint, out _))
        {
            return primary;
        }
        if (_fallback.TryGetValue((codepoint, style), out var face))
        {
            return face;
        }
        var (fontStyle, weight) = FontOf(style);
        face = FontManager.Current.TryMatchCharacter(codepoint, fontStyle, weight, FontStretch.Normal, FontFamily, null, out var typeface)
            ? typeface.GlyphTypeface
            : null;
        if (style != FaceStyle.Regular && face is not null && face.FontSimulations != FontSimulations.None)
        {
            face = FaceFor(codepoint);
        }
        _fallback[(codepoint, style)] = face;
        return face;
    }

    /// <summary>The emoji face for <paramref name="codepoint"/>: <see cref="EmojiFontFamily"/>, the platform's color emoji font, or any fallback that has it.</summary>
    private GlyphTypeface? EmojiFaceFor(int codepoint)
    {
        if (!_emojiFaceResolved)
        {
            _emojiFaceResolved = true;
            if (EmojiFontFamily is { } family)
            {
                _emojiFace = new Typeface(family).GlyphTypeface;
            }
            else if (FontManager.Current.TryMatchCharacter(0x1F600, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, FontFamily, null, out var typeface))
            {
                _emojiFace = typeface.GlyphTypeface;
            }
        }
        if (_emojiFace is { } emoji && emoji.CharacterToGlyphMap.TryGetGlyph(codepoint, out _))
        {
            return emoji;
        }
        return FaceFor(codepoint);
    }

    /// <summary>
    /// The glyphs of the cell at <paramref name="column"/>, or <c>false</c> when no font has them. <paramref name="cellsWidth"/>
    /// is the width of the cell's columns in DIP (the core's cell width, never the shaper's).
    /// </summary>
    private bool TryCellGlyphs(RowSnapshot row, int column, FaceStyle style, double cellsWidth, CellMetrics m, out CellGlyphs glyphs)
    {
        var cell = row.Cells[column];
        var cp = (int)TCell.Codepoint(cell);
        int[]? extra = null;
        if (TCell.HasGrapheme(cell) && row.Graphemes is { } graphemes)
        {
            graphemes.TryGetValue(column, out extra);
        }
        return TryGlyphs(cp, extra, style, cellsWidth, m, out glyphs);
    }

    private bool TryGlyphs(int cp, int[]? extra, FaceStyle style, double cellsWidth, CellMetrics m, out CellGlyphs glyphs)
    {
        var scale = m.Scaling;
        var emSize = m.EmSizePx / scale;
        var baseline = m.BaselineY / scale;
        var emoji = TerminalUnicode.IsEmojiPresentation(cp, extra);
        if (extra is not { Length: > 0 } && !emoji)
        {
            // The plain path: one cmap lookup.
            GlyphTypeface? plainFace;
            ushort glyph;
            if (GlyphOverride is { } lookup)
            {
                plainFace = PrimaryFace(style);
                glyph = lookup(cp);
            }
            else
            {
                plainFace = FaceFor(cp, style);
                if (plainFace is null || !plainFace.CharacterToGlyphMap.TryGetGlyph(cp, out glyph))
                {
                    glyphs = default;
                    return false;
                }
            }
            glyphs = new CellGlyphs { Face = plainFace, EmSize = emSize, Glyph = glyph, Text = char.ConvertFromUtf32(cp), Baseline = baseline };
            return true;
        }

        var text = TerminalUnicode.ClusterText(cp, extra);
        var face = GlyphOverride is not null ? PrimaryFace(emoji ? FaceStyle.Regular : style) : emoji ? EmojiFaceFor(cp) : FaceFor(cp, style);
        if (face is null)
        {
            glyphs = default;
            return false;
        }
        var shaped = Shape(text, face, emSize);
        if (shaped.Length == 0)
        {
            glyphs = default;
            return false;
        }
        if (!emoji)
        {
            glyphs = new CellGlyphs { Face = face, EmSize = emSize, Shaped = FitAdvances(shaped, cellsWidth), Text = text, Baseline = baseline };
            return true;
        }

        // Emoji: scale the face so the cluster fits its cells (Ghostty: min(2·cellW·0.95 / w, cellH / h)), centered.
        double natural = 0;
        foreach (var info in shaped)
        {
            natural += info.GlyphAdvance;
        }
        var metrics = face.Metrics;
        var height = (Math.Abs(metrics.Ascent) + Math.Abs(metrics.Descent)) * emSize / metrics.DesignEmHeight;
        var cellHeight = m.CellHeight / scale;
        var k = natural > 0 && height > 0 ? Math.Min(cellsWidth * 0.95 / natural, cellHeight / height) : 1;
        var scaled = new GlyphInfo[shaped.Length];
        for (var i = 0; i < shaped.Length; i++)
        {
            var info = shaped[i];
            scaled[i] = new GlyphInfo(info.GlyphIndex, info.GlyphCluster, info.GlyphAdvance * k, info.GlyphOffset * k);
        }
        glyphs = new CellGlyphs
        {
            Face = face,
            EmSize = emSize * k,
            Shaped = scaled,
            Text = text,
            OwnRun = true,
            OffsetX = (cellsWidth - natural * k) / 2,
            Baseline = (cellHeight - height * k) / 2 + Math.Abs(metrics.Ascent) * emSize * k / metrics.DesignEmHeight,
        };
        return true;
    }

    /// <summary>HarfBuzz's glyphs for a cluster, cached by text, face and size (colors are not part of the key).</summary>
    private GlyphInfo[] Shape(string text, GlyphTypeface face, double emSize)
    {
        var key = new ShapeKey(text, face, emSize);
        if (_shaped.TryGetValue(key, out var infos))
        {
            return infos;
        }
        if (_shaped.Count >= MaxShapedClusters)
        {
            _shaped.Clear();
        }
        ShapeCalls++;
        using (var buffer = TextShaper.Current.ShapeText(text.AsMemory(), new TextShaperOptions(face, emSize)))
        {
            infos = new GlyphInfo[buffer.Length];
            for (var i = 0; i < infos.Length; i++)
            {
                infos[i] = buffer[i];
            }
        }
        _shaped[key] = infos;
        return infos;
    }

    /// <summary>Keeps HarfBuzz's relative positions inside the cluster and makes its advances add up to its cells, so the next cell starts on the grid.</summary>
    private static GlyphInfo[] FitAdvances(GlyphInfo[] shaped, double cellsWidth)
    {
        var fitted = new GlyphInfo[shaped.Length];
        double used = 0;
        for (var i = 0; i < shaped.Length - 1; i++)
        {
            fitted[i] = shaped[i];
            used += shaped[i].GlyphAdvance;
        }
        var last = shaped[^1];
        fitted[^1] = new GlyphInfo(last.GlyphIndex, last.GlyphCluster, cellsWidth - used, last.GlyphOffset);
        return fitted;
    }

    /// <summary>Draws one cell's text outside the row cache (the character under a block cursor, the IME composition).</summary>
    private void DrawCellText(DrawingContext context, int cp, int[]? extra, FaceStyle style, int columns, TerminalRgb color, Point cellOrigin, CellMetrics m)
    {
        var cellsWidth = columns * m.CellWidth / m.Scaling;
        if (!TryGlyphs(cp, extra, style, cellsWidth, m, out var g))
        {
            return;
        }
        var origin = new Point(cellOrigin.X + g.OffsetX, cellOrigin.Y + g.Baseline);
        IReadOnlyList<GlyphInfo> infos = g.Shaped ?? [new GlyphInfo(g.Glyph, 0, cellsWidth)];
        using var run = new GlyphRun(g.Face, g.EmSize, g.Text.AsMemory(), infos, origin);
        context.DrawGlyphRun(Brush(color), run);
    }
}
