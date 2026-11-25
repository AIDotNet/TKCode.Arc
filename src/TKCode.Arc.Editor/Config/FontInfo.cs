// Ported from vscode (src/vs/editor/common/config/fontInfo.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - BareFontInfo.Create (upstream's _create) takes the editor zoom level as a parameter instead of reading the global
//    EditorZoom; it defaults to 0 (no zoom);
//  - the constructors take their values as parameters instead of an options object;
//  - FontInfo implements IEquatable with upstream's equals (which skips pixelRatio, isMonospace and isTrusted), and a
//    hash code over the same members;
//  - sanitizeFontFamily tests its characters by Unicode category instead of a regular expression;
//  - IValidatedEditorOptions is left out until the editor options are computed.

using System.Globalization;
using System.Text;

namespace TKCode.Arc.Editor.Config;

/// <summary>The font settings of an editor, before they are measured.</summary>
internal class BareFontInfo
{
    /// <summary>Determined from empirical observations.</summary>
    public static double GoldenLineHeightRatio => OperatingSystem.IsMacOS() ? 1.5 : 1.35;

    public const int MinimumLineHeight = 8;

    public const string FontVariationOff = "normal";

    public const string FontVariationTranslate = "translate";

    public const string DefaultWindowsFontFamily = "Consolas, 'Courier New', monospace";

    public const string DefaultMacFontFamily = "Menlo, Monaco, 'Courier New', monospace";

    public const string DefaultLinuxFontFamily = "'Droid Sans Mono', monospace";

    /// <summary>Upstream's <c>EDITOR_FONT_DEFAULTS.fontFamily</c>.</summary>
    public static string DefaultFontFamily =>
        OperatingSystem.IsMacOS() ? DefaultMacFontFamily : (OperatingSystem.IsWindows() ? DefaultWindowsFontFamily : DefaultLinuxFontFamily);

    /// <summary>Upstream's <c>EDITOR_FONT_DEFAULTS.fontWeight</c>.</summary>
    public const string DefaultFontWeight = "normal";

    /// <summary>Upstream's <c>EDITOR_FONT_DEFAULTS.fontSize</c>.</summary>
    public static double DefaultFontSize => OperatingSystem.IsMacOS() ? 12 : 14;

    /// <summary>Upstream's <c>EDITOR_FONT_DEFAULTS.lineHeight</c>.</summary>
    public const double DefaultLineHeight = 0;

    /// <summary>Upstream's <c>EDITOR_FONT_DEFAULTS.letterSpacing</c>.</summary>
    public const double DefaultLetterSpacing = 0;

    protected BareFontInfo(
        double pixelRatio,
        string fontFamily,
        string fontWeight,
        double fontSize,
        string fontFeatureSettings,
        string fontVariationSettings,
        double lineHeight,
        double letterSpacing)
    {
        PixelRatio = pixelRatio;
        FontFamily = fontFamily;
        FontWeight = fontWeight;
        FontSize = fontSize;
        FontFeatureSettings = fontFeatureSettings;
        FontVariationSettings = fontVariationSettings;
        LineHeight = ToInt32(lineHeight);
        LetterSpacing = letterSpacing;
    }

    public double PixelRatio { get; }

    public string FontFamily { get; }

    public string FontWeight { get; }

    public double FontSize { get; }

    public string FontFeatureSettings { get; }

    public string FontVariationSettings { get; }

    public int LineHeight { get; }

    public double LetterSpacing { get; }

    /// <summary>Validates the settings: a line height of 0 is derived from the font size, a small one is in ems.</summary>
    public static BareFontInfo Create(
        string fontFamily,
        string fontWeight,
        double fontSize,
        string fontFeatureSettings,
        string fontVariationSettings,
        double lineHeight,
        double letterSpacing,
        double pixelRatio,
        bool ignoreEditorZoom,
        double editorZoomLevel = 0)
    {
        if (lineHeight == 0)
        {
            lineHeight = GoldenLineHeightRatio * fontSize;
        }
        else if (lineHeight < MinimumLineHeight)
        {
            // Values too small to be line heights in pixels are in ems.
            lineHeight = lineHeight * fontSize;
        }

        // Enforce integer, minimum constraints
        lineHeight = JsRound(lineHeight);
        if (lineHeight < MinimumLineHeight)
        {
            lineHeight = MinimumLineHeight;
        }

        var editorZoomLevelMultiplier = 1 + (ignoreEditorZoom ? 0 : editorZoomLevel * 0.1);
        fontSize *= editorZoomLevelMultiplier;
        lineHeight *= editorZoomLevelMultiplier;

        if (fontVariationSettings == FontVariationTranslate)
        {
            if (fontWeight is "normal" or "bold")
            {
                fontVariationSettings = FontVariationOff;
            }
            else
            {
                var fontWeightAsNumber = ParseInt(fontWeight);
                fontVariationSettings = $"'wght' {fontWeightAsNumber}";
                fontWeight = "normal";
            }
        }

        return new BareFontInfo(pixelRatio, fontFamily, fontWeight, fontSize, fontFeatureSettings, fontVariationSettings, lineHeight, letterSpacing);
    }

    public string GetId() =>
        $"{Format(PixelRatio)}-{FontFamily}-{FontWeight}-{Format(FontSize)}-{FontFeatureSettings}-{FontVariationSettings}-{LineHeight}-{Format(LetterSpacing)}";

    public string GetMassagedFontFamily()
    {
        var fallbackFontFamily = DefaultFontFamily;
        var fontFamily = WrapInQuotes(FontFamily);
        if (!string.IsNullOrEmpty(fallbackFontFamily) && FontFamily != fallbackFontFamily)
        {
            return $"{fontFamily}, {fallbackFontFamily}";
        }

        return fontFamily;
    }

    /// <summary>Returns the font family if it has only letters, marks, numbers and the punctuation of font lists, else the default.</summary>
    public static string SanitizeFontFamily(string fontFamily)
    {
        if (fontFamily.Length == 0)
        {
            return DefaultFontFamily;
        }

        foreach (var rune in fontFamily.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            var allowed = category switch
            {
                UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
                    or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter => true,
                UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark => true,
                UnicodeCategory.DecimalDigitNumber or UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber => true,
                _ => rune.Value is ' ' or '_' or '+' or '\'' or '"' or ',' or '.' or '-',
            };
            if (!allowed)
            {
                return DefaultFontFamily;
            }
        }

        return fontFamily;
    }

    private static string WrapInQuotes(string fontFamily)
    {
        if (fontFamily.AsSpan().IndexOfAny(",\"'") >= 0)
        {
            // Looks like the font family might be already escaped
            return fontFamily;
        }

        if (fontFamily.AsSpan().IndexOfAny("+ ") >= 0)
        {
            // Wrap a font family using + or <space> with quotes
            return $"\"{fontFamily}\"";
        }

        return fontFamily;
    }

    /// <summary>JavaScript's <c>value | 0</c>.</summary>
    private static int ToInt32(double value) => double.IsFinite(value) ? unchecked((int)(long)value) : 0;

    /// <summary>JavaScript's <c>Math.round</c>: halves round up.</summary>
    private static double JsRound(double value) => Math.Floor(value + 0.5);

    /// <summary>JavaScript's <c>parseInt(value, 10)</c> for the leading digits; NaN prints as upstream's does.</summary>
    private static string ParseInt(string value)
    {
        var span = value.AsSpan().TrimStart();
        var length = 0;
        if (length < span.Length && span[length] is '+' or '-')
        {
            length++;
        }

        var digitsStart = length;
        while (length < span.Length && char.IsAsciiDigit(span[length]))
        {
            length++;
        }

        if (length == digitsStart)
        {
            return "NaN";
        }

        return long.TryParse(span[..length], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result)
            ? result.ToString(CultureInfo.InvariantCulture)
            : span[..length].ToString();
    }

    private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>The font settings of an editor together with the measured widths of its characters.</summary>
internal sealed class FontInfo : BareFontInfo, IEquatable<FontInfo>
{
    /// <summary>Change this whenever the members of <see cref="FontInfo"/> change.</summary>
    public const int SerializedFontInfoVersion = 2;

    public FontInfo(
        double pixelRatio,
        string fontFamily,
        string fontWeight,
        double fontSize,
        string fontFeatureSettings,
        string fontVariationSettings,
        double lineHeight,
        double letterSpacing,
        bool isMonospace,
        double typicalHalfwidthCharacterWidth,
        double typicalFullwidthCharacterWidth,
        bool canUseHalfwidthRightwardsArrow,
        double spaceWidth,
        double middotWidth,
        double wsmiddotWidth,
        double maxDigitWidth,
        bool isTrusted)
        : base(pixelRatio, fontFamily, fontWeight, fontSize, fontFeatureSettings, fontVariationSettings, lineHeight, letterSpacing)
    {
        IsTrusted = isTrusted;
        IsMonospace = isMonospace;
        TypicalHalfwidthCharacterWidth = typicalHalfwidthCharacterWidth;
        TypicalFullwidthCharacterWidth = typicalFullwidthCharacterWidth;
        CanUseHalfwidthRightwardsArrow = canUseHalfwidthRightwardsArrow;
        SpaceWidth = spaceWidth;
        MiddotWidth = middotWidth;
        WsmiddotWidth = wsmiddotWidth;
        MaxDigitWidth = maxDigitWidth;
    }

    public int Version => SerializedFontInfoVersion;

    public bool IsTrusted { get; }

    public bool IsMonospace { get; }

    public double TypicalHalfwidthCharacterWidth { get; }

    public double TypicalFullwidthCharacterWidth { get; }

    public bool CanUseHalfwidthRightwardsArrow { get; }

    public double SpaceWidth { get; }

    public double MiddotWidth { get; }

    public double WsmiddotWidth { get; }

    public double MaxDigitWidth { get; }

    public bool Equals(FontInfo? other)
    {
        if (other is null)
        {
            return false;
        }

        return FontFamily == other.FontFamily
            && FontWeight == other.FontWeight
            && FontSize == other.FontSize
            && FontFeatureSettings == other.FontFeatureSettings
            && FontVariationSettings == other.FontVariationSettings
            && LineHeight == other.LineHeight
            && LetterSpacing == other.LetterSpacing
            && TypicalHalfwidthCharacterWidth == other.TypicalHalfwidthCharacterWidth
            && TypicalFullwidthCharacterWidth == other.TypicalFullwidthCharacterWidth
            && CanUseHalfwidthRightwardsArrow == other.CanUseHalfwidthRightwardsArrow
            && SpaceWidth == other.SpaceWidth
            && MiddotWidth == other.MiddotWidth
            && WsmiddotWidth == other.WsmiddotWidth
            && MaxDigitWidth == other.MaxDigitWidth;
    }

    public override bool Equals(object? obj) => Equals(obj as FontInfo);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(FontFamily);
        hash.Add(FontWeight);
        hash.Add(FontSize);
        hash.Add(FontFeatureSettings);
        hash.Add(FontVariationSettings);
        hash.Add(LineHeight);
        hash.Add(LetterSpacing);
        hash.Add(TypicalHalfwidthCharacterWidth);
        hash.Add(TypicalFullwidthCharacterWidth);
        hash.Add(CanUseHalfwidthRightwardsArrow);
        hash.Add(SpaceWidth);
        hash.Add(MiddotWidth);
        hash.Add(WsmiddotWidth);
        hash.Add(MaxDigitWidth);
        return hash.ToHashCode();
    }
}
