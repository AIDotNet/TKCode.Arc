// Ported from vscode (src/vs/editor/common/encodedTokenAttributes.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the metadata is a uint (upstream uses a number kept unsigned with `>>> 0`), and MetadataConsts is a static class
//    of uint constants instead of a const enum, so the masks combine without casts;
//  - ITokenPresentation is the record TokenPresentation.

namespace TKCode.Arc.Editor.Tokens;

/// <summary>An encoded language id. Open ended at runtime: values from 2 on are assigned by a <c>LanguageIdCodec</c>.</summary>
internal enum LanguageId
{
    Null = 0,
    PlainText = 1,
}

/// <summary>A font style. Values are 2^x such that a bit mask can be used.</summary>
[Flags]
internal enum FontStyle
{
    NotSet = -1,
    None = 0,
    Italic = 1,
    Bold = 2,
    Underline = 4,
    Strikethrough = 8,
}

/// <summary>An index into the color map. Open ended at runtime.</summary>
internal enum ColorId
{
    None = 0,
    DefaultForeground = 1,
    DefaultBackground = 2,
}

/// <summary>A standard token type.</summary>
internal enum StandardTokenType
{
    Other = 0,
    Comment = 1,
    String = 2,
    RegEx = 3,
}

/// <summary>
/// Helpers to manage the "collapsed" metadata of an entire StackElement stack.
/// The following assumptions have been made:
///  - languageId &lt; 256 =&gt; needs 8 bits
///  - unique color count &lt; 512 =&gt; needs 9 bits
/// <code>
///     3322 2222 2222 1111 1111 1100 0000 0000
///     1098 7654 3210 9876 5432 1098 7654 3210
///     bbbb bbbb ffff ffff fFFF FBTT LLLL LLLL
/// </code>
/// L = LanguageId (8 bits), T = StandardTokenType (2 bits), B = Balanced bracket (1 bit), F = FontStyle (4 bits),
/// f = foreground color (9 bits), b = background color (8 bits).
/// </summary>
internal static class MetadataConsts
{
    public const uint LanguageIdMask = 0b00000000_00000000_00000000_11111111;
    public const uint TokenTypeMask = 0b00000000_00000000_00000011_00000000;
    public const uint BalancedBracketsMask = 0b00000000_00000000_00000100_00000000;
    public const uint FontStyleMask = 0b00000000_00000000_01111000_00000000;
    public const uint ForegroundMask = 0b00000000_11111111_10000000_00000000;
    public const uint BackgroundMask = 0b11111111_00000000_00000000_00000000;

    public const uint ItalicMask = 0b00000000_00000000_00001000_00000000;
    public const uint BoldMask = 0b00000000_00000000_00010000_00000000;
    public const uint UnderlineMask = 0b00000000_00000000_00100000_00000000;
    public const uint StrikethroughMask = 0b00000000_00000000_01000000_00000000;

    // Semantic tokens cannot set the language id, so we can
    // use the first 8 bits for control purposes
    public const uint SemanticUseItalic = 0b00000000_00000000_00000000_00000001;
    public const uint SemanticUseBold = 0b00000000_00000000_00000000_00000010;
    public const uint SemanticUseUnderline = 0b00000000_00000000_00000000_00000100;
    public const uint SemanticUseStrikethrough = 0b00000000_00000000_00000000_00001000;
    public const uint SemanticUseForeground = 0b00000000_00000000_00000000_00010000;
    public const uint SemanticUseBackground = 0b00000000_00000000_00000000_00100000;

    public const int LanguageIdOffset = 0;
    public const int TokenTypeOffset = 8;
    public const int BalancedBracketsOffset = 10;
    public const int FontStyleOffset = 11;
    public const int ForegroundOffset = 15;
    public const int BackgroundOffset = 24;
}

/// <summary>Decodes the fields of token metadata.</summary>
internal static class TokenMetadata
{
    public static LanguageId GetLanguageId(uint metadata)
    {
        return (LanguageId)((metadata & MetadataConsts.LanguageIdMask) >> MetadataConsts.LanguageIdOffset);
    }

    public static StandardTokenType GetTokenType(uint metadata)
    {
        return (StandardTokenType)((metadata & MetadataConsts.TokenTypeMask) >> MetadataConsts.TokenTypeOffset);
    }

    public static bool ContainsBalancedBrackets(uint metadata)
    {
        return (metadata & MetadataConsts.BalancedBracketsMask) != 0;
    }

    public static FontStyle GetFontStyle(uint metadata)
    {
        return (FontStyle)((metadata & MetadataConsts.FontStyleMask) >> MetadataConsts.FontStyleOffset);
    }

    public static ColorId GetForeground(uint metadata)
    {
        return (ColorId)((metadata & MetadataConsts.ForegroundMask) >> MetadataConsts.ForegroundOffset);
    }

    public static ColorId GetBackground(uint metadata)
    {
        return (ColorId)((metadata & MetadataConsts.BackgroundMask) >> MetadataConsts.BackgroundOffset);
    }

    public static string GetClassNameFromMetadata(uint metadata)
    {
        var foreground = GetForeground(metadata);
        var className = "mtk" + ((int)foreground).ToString(System.Globalization.CultureInfo.InvariantCulture);

        var fontStyle = GetFontStyle(metadata);
        if ((fontStyle & FontStyle.Italic) != 0)
        {
            className += " mtki";
        }
        if ((fontStyle & FontStyle.Bold) != 0)
        {
            className += " mtkb";
        }
        if ((fontStyle & FontStyle.Underline) != 0)
        {
            className += " mtku";
        }
        if ((fontStyle & FontStyle.Strikethrough) != 0)
        {
            className += " mtks";
        }

        return className;
    }

    public static string GetInlineStyleFromMetadata(uint metadata, IReadOnlyList<string> colorMap)
    {
        var foreground = GetForeground(metadata);
        var fontStyle = GetFontStyle(metadata);

        var result = $"color: {colorMap[(int)foreground]};";
        if ((fontStyle & FontStyle.Italic) != 0)
        {
            result += "font-style: italic;";
        }
        if ((fontStyle & FontStyle.Bold) != 0)
        {
            result += "font-weight: bold;";
        }
        var textDecoration = "";
        if ((fontStyle & FontStyle.Underline) != 0)
        {
            textDecoration += " underline";
        }
        if ((fontStyle & FontStyle.Strikethrough) != 0)
        {
            textDecoration += " line-through";
        }
        if (textDecoration.Length > 0)
        {
            result += $"text-decoration:{textDecoration};";
        }
        return result;
    }

    public static TokenPresentation GetPresentationFromMetadata(uint metadata)
    {
        var foreground = GetForeground(metadata);
        var fontStyle = GetFontStyle(metadata);

        return new TokenPresentation(
            foreground,
            (fontStyle & FontStyle.Italic) != 0,
            (fontStyle & FontStyle.Bold) != 0,
            (fontStyle & FontStyle.Underline) != 0,
            (fontStyle & FontStyle.Strikethrough) != 0);
    }
}

/// <summary>How a token is drawn (upstream's <c>ITokenPresentation</c>).</summary>
internal readonly record struct TokenPresentation(
    ColorId Foreground,
    bool Italic,
    bool Bold,
    bool Underline,
    bool Strikethrough);
