// Ported from vscode (src/vs/editor/common/languages/defaultDocumentColorsComputer.ts, commit b57d5c9e), MIT license.
// Differences:
//  - the target is the document's lines (upstream's IDocumentColorComputerTarget: the worker's mirror model, whose
//    findMatches runs the expression over each line); a TextModel overload reads them from the model;
//  - the expressions are .NET expressions with the same patterns (lookbehind, lookahead and multiline anchors behave
//    the same on a single line); \b is .NET's Unicode word boundary;
//  - a capture group parses as a number the way JavaScript's Number() does for these patterns (decimal, optional
//    leading or trailing dot).

using System.Globalization;
using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Contrib.ColorPicker;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Languages;

/// <summary>Finds CSS colors (hex, rgb, rgba, hsl, hsla) in a document (upstream's computeDefaultDocumentColors).</summary>
internal static partial class DefaultDocumentColorsComputer
{
    // Early validation for RGB and HSL (including CSS Level 4 syntax with / separator)
    [GeneratedRegex("""\b(rgb|rgba|hsl|hsla)(\([0-9\s,.\%\/]*\))|^(#)([A-Fa-f0-9]{3})(?=[\s'";,)]|$)|^(#)([A-Fa-f0-9]{4})(?=[\s'";,)]|$)|^(#)([A-Fa-f0-9]{6})(?=[\s'";,)]|$)|^(#)([A-Fa-f0-9]{8})(?=[\s'";,)]|$)|(?<=['"\s])(#)([A-Fa-f0-9]{3})(?=[\s'";,)]|$)|(?<=['"\s])(#)([A-Fa-f0-9]{4})(?=[\s'";,)]|$)|(?<=['"\s])(#)([A-Fa-f0-9]{6})(?=[\s'";,)]|$)|(?<=['"\s])(#)([A-Fa-f0-9]{8})(?=[\s'";,)]|$)""", RegexOptions.Multiline)]
    private static partial Regex InitialValidation();

    // Supports both comma-separated (rgb(255, 0, 0)) and CSS Level 4 space-separated syntax (rgb(255 0 0))
    [GeneratedRegex("""^\(\s*(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9][0-9]|[0-9])\s*[\s,]\s*(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9][0-9]|[0-9])\s*[\s,]\s*(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9][0-9]|[0-9])\s*\)$""", RegexOptions.Multiline)]
    private static partial Regex RgbParameters();

    // Supports both comma-separated (rgba(255, 0, 0, 0.5)) and CSS Level 4 syntax (rgba(255 0 0 / 0.5))
    [GeneratedRegex("""^\(\s*(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9][0-9]|[0-9])\s*[\s,]\s*(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9][0-9]|[0-9])\s*[\s,]\s*(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9][0-9]|[0-9])\s*(?:[\s,]|[\s]*\/)\s*(0[.][0-9]+|[.][0-9]+|[01][.]|[01])\s*\)$""", RegexOptions.Multiline)]
    private static partial Regex RgbaParameters();

    [GeneratedRegex("""^\(\s*((?:360(?:\.0+)?|(?:36[0]|3[0-5][0-9]|[12][0-9][0-9]|[1-9]?[0-9])(?:\.\d+)?))\s*[\s,]\s*(100(?:\.0+)?|\d{1,2}[.]\d*|\d{1,2})%\s*[\s,]\s*(100(?:\.0+)?|\d{1,2}[.]\d*|\d{1,2})%\s*\)$""", RegexOptions.Multiline)]
    private static partial Regex HslParameters();

    // Supports both comma-separated (hsla(253, 100%, 50%, 0.5)) and CSS Level 4 syntax (hsla(253 100% 50% / 0.5))
    [GeneratedRegex("""^\(\s*((?:360(?:\.0+)?|(?:36[0]|3[0-5][0-9]|[12][0-9][0-9]|[1-9]?[0-9])(?:\.\d+)?))\s*[\s,]\s*(100(?:\.0+)?|\d{1,2}[.]\d*|\d{1,2})%\s*[\s,]\s*(100(?:\.0+)?|\d{1,2}[.]\d*|\d{1,2})%\s*(?:[\s,]|[\s]*\/)\s*(0[.][0-9]+|[.][0-9]+|[01][.]0*|[01])\s*\)$""", RegexOptions.Multiline)]
    private static partial Regex HslaParameters();

    /// <summary>The colors in the model.</summary>
    public static IReadOnlyList<ColorInformation> ComputeDefaultDocumentColors(TextModel model)
    {
        var lines = new string[model.GetLineCount()];
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = model.GetLineContent(i + 1);
        }

        return ComputeDefaultDocumentColors(lines);
    }

    /// <summary>The colors in the lines of a document (upstream's computeDefaultDocumentColors).</summary>
    public static IReadOnlyList<ColorInformation> ComputeDefaultDocumentColors(IReadOnlyList<string> lines)
    {
        var result = new List<ColorInformation>();
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            foreach (Match initialMatch in InitialValidation().Matches(lines[lineIndex]))
            {
                // the full match, then the groups that took part (upstream: initialMatch.filter(g => g !== undefined))
                var initialCaptureGroups = new List<string> { initialMatch.Value };
                for (var g = 1; g < initialMatch.Groups.Count; g++)
                {
                    if (initialMatch.Groups[g].Success)
                    {
                        initialCaptureGroups.Add(initialMatch.Groups[g].Value);
                    }
                }

                if (initialCaptureGroups.Count < 3)
                {
                    continue;
                }

                var colorScheme = initialCaptureGroups[1];
                var colorParameters = initialCaptureGroups[2];
                if (string.IsNullOrEmpty(colorParameters))
                {
                    continue;
                }

                var range = new TextRange(lineIndex + 1, initialMatch.Index + 1, lineIndex + 1, initialMatch.Index + 1 + initialMatch.Length);
                ColorInformation? colorInformation = colorScheme switch
                {
                    "rgb" => FindRgbColorInformation(range, RgbParameters().Matches(colorParameters), isAlpha: false),
                    "rgba" => FindRgbColorInformation(range, RgbaParameters().Matches(colorParameters), isAlpha: true),
                    "hsl" => FindHslColorInformation(range, HslParameters().Matches(colorParameters), isAlpha: false),
                    "hsla" => FindHslColorInformation(range, HslaParameters().Matches(colorParameters), isAlpha: true),
                    "#" => FindHexColorInformation(range, colorScheme + colorParameters),
                    _ => null,
                };

                if (colorInformation is not null)
                {
                    result.Add(colorInformation);
                }
            }
        }

        return result;
    }

    private static LanguageColor ToColor(double r, double g, double b, double a) => new(r / 255, g / 255, b / 255, a);

    private static ColorInformation? FindHexColorInformation(TextRange range, string hexValue)
    {
        if (RgbaColor.TryParseHex(hexValue) is not { } parsed)
        {
            return null;
        }

        return new ColorInformation(range, ToColor(parsed.R, parsed.G, parsed.B, CssColor.RoundFloat(parsed.A / 255.0, 3)));
    }

    private static ColorInformation? FindRgbColorInformation(TextRange range, MatchCollection matches, bool isAlpha)
    {
        if (matches.Count != 1)
        {
            return null;
        }

        var values = ParseCaptureGroups(matches[0]);
        return new ColorInformation(range, ToColor(values[0], values[1], values[2], isAlpha ? values[3] : 1));
    }

    private static ColorInformation? FindHslColorInformation(TextRange range, MatchCollection matches, bool isAlpha)
    {
        if (matches.Count != 1)
        {
            return null;
        }

        var values = ParseCaptureGroups(matches[0]);
        var rgba = CssHsla.ToRgba(new CssHsla(values[0], values[1] / 100, values[2] / 100, isAlpha ? values[3] : 1));
        return new ColorInformation(range, ToColor(rgba.R, rgba.G, rgba.B, rgba.A));
    }

    // upstream's _parseCaptureGroups: the groups that are numbers (the full match never is)
    private static List<double> ParseCaptureGroups(Match match)
    {
        var values = new List<double>();
        for (var g = 1; g < match.Groups.Count; g++)
        {
            var group = match.Groups[g];
            if (group.Success
                && !string.IsNullOrWhiteSpace(group.Value)
                && double.TryParse(group.Value.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed))
            {
                values.Add(parsed);
            }
        }

        return values;
    }
}
