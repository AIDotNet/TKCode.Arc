// Ported from vscode (src/vs/editor/common/core/editorColorRegistry.ts, the bracket pair colorization colors; commit
// b57d5c9e), MIT license.
//
// Differences: the color registry is not ported; this table holds the defaults of the colors the editor's view reads,
// by color id and color scheme, and StandaloneTheme.GetColorOrDefault falls back to it when a theme does not define
// the color (upstream: the registry resolves the default for the theme's type).

namespace TKCode.Arc.Editor.Standalone;

/// <summary>The ids and defaults of the editor colors the view reads (upstream's registerColor calls).</summary>
internal static class EditorColorRegistry
{
    public const string EditorBracketHighlightingForeground1 = "editorBracketHighlight.foreground1";
    public const string EditorBracketHighlightingForeground2 = "editorBracketHighlight.foreground2";
    public const string EditorBracketHighlightingForeground3 = "editorBracketHighlight.foreground3";
    public const string EditorBracketHighlightingForeground4 = "editorBracketHighlight.foreground4";
    public const string EditorBracketHighlightingForeground5 = "editorBracketHighlight.foreground5";
    public const string EditorBracketHighlightingForeground6 = "editorBracketHighlight.foreground6";
    public const string EditorBracketHighlightingUnexpectedBracketForeground = "editorBracketHighlight.unexpectedBracket.foreground";

    /// <summary>The bracket colors by nesting level, in order.</summary>
    public static readonly IReadOnlyList<string> BracketHighlightingForegrounds =
    [
        EditorBracketHighlightingForeground1,
        EditorBracketHighlightingForeground2,
        EditorBracketHighlightingForeground3,
        EditorBracketHighlightingForeground4,
        EditorBracketHighlightingForeground5,
        EditorBracketHighlightingForeground6,
    ];

    // (dark, light, hcDark, hcLight)
    private static readonly Dictionary<string, (string Dark, string Light, string HcDark, string HcLight)> s_defaults =
        new(StringComparer.Ordinal)
        {
            [EditorBracketHighlightingForeground1] = ("#FFD700", "#0431FAFF", "#FFD700", "#0431FAFF"),
            [EditorBracketHighlightingForeground2] = ("#DA70D6", "#319331FF", "#DA70D6", "#319331FF"),
            [EditorBracketHighlightingForeground3] = ("#179FFF", "#7B3814FF", "#87CEFA", "#7B3814FF"),
            [EditorBracketHighlightingForeground4] = ("#00000000", "#00000000", "#00000000", "#00000000"),
            [EditorBracketHighlightingForeground5] = ("#00000000", "#00000000", "#00000000", "#00000000"),
            [EditorBracketHighlightingForeground6] = ("#00000000", "#00000000", "#00000000", "#00000000"),
            // new Color(new RGBA(255, 18, 18, 0.8)) is #FF1212CC; new RGBA(255, 50, 50, 1) is #FF3232
            [EditorBracketHighlightingUnexpectedBracketForeground] = ("#FF1212CC", "#FF1212CC", "#FF3232", "#B5200D"),
        };

    /// <summary>The default of a color for a color scheme; null for an id this table does not know.</summary>
    public static RgbaColor? GetDefault(string colorId, ColorScheme scheme)
    {
        if (!s_defaults.TryGetValue(colorId, out var value))
        {
            return null;
        }

        return RgbaColor.FromHex(scheme switch
        {
            ColorScheme.Light => value.Light,
            ColorScheme.HighContrastDark => value.HcDark,
            ColorScheme.HighContrastLight => value.HcLight,
            _ => value.Dark,
        });
    }
}
