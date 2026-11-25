// Ported from vscode (src/vs/editor/common/languages/supports/tokenization.ts, commit b57d5c9e), MIT license.
//
// Differences:
// - ITokenThemeRule is the record TokenThemeRule; the module functions (parseTokenTheme, strcmp, toStandardTokenType,
//   generateTokensCSSForColorMap) are in the static class TokenThemeParser.
// - The font token parts (IFontTokenOptions, generateTokensCSSForFontMap, classNameForFontTokenDecorations) are left
//   out: upstream fills font tokens only from tree-sitter and its experimental font decorations (see Tokenization.cs).
// - ColorMap.GetColorMap returns RgbaColor[] whose index 0 (ColorId.None, a hole upstream) holds a transparent color.
// - The rules are sorted with a stable sort, as JavaScript's Array.prototype.sort is: rules with the same token and the
//   same index keep their order.
// - TokenTheme's match cache is a ConcurrentDictionary, because tokenizers read the theme off the UI thread.
// - ThemeTrieElementRule and ExternalThemeTrieElement compare by value (the tests use assert.deepStrictEqual).

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Languages;

/// <summary>A token theme rule as a theme defines it (upstream's ITokenThemeRule).</summary>
/// <param name="Token">The token type the rule applies to, such as <c>keyword</c> or <c>string.key.json</c>; empty for the defaults.</param>
internal sealed record TokenThemeRule(string Token)
{
    /// <summary>The text color as six hex digits (an optional leading <c>#</c> and two alpha digits are ignored).</summary>
    public string? Foreground { get; init; }

    /// <summary>The background color, in the same form as <see cref="Foreground"/>.</summary>
    public string? Background { get; init; }

    /// <summary>Space-separated <c>italic</c>, <c>bold</c>, <c>underline</c>, <c>strikethrough</c>; empty for none.</summary>
    public string? FontStyle { get; init; }
}

/// <summary>A theme rule after parsing (upstream's ParsedTokenThemeRule).</summary>
internal sealed record ParsedTokenThemeRule(string Token, int Index, FontStyle FontStyle, string? Foreground, string? Background);

/// <summary>Maps the colors of a theme to the ids stored in token metadata.</summary>
internal sealed class ColorMap
{
    private int _lastColorId;
    private readonly List<RgbaColor> _id2color = [default];
    private readonly Dictionary<string, ColorId> _color2id = new(StringComparer.Ordinal);

    public ColorId GetId(string? color)
    {
        if (color is null)
        {
            return 0;
        }

        // colorRegExp = /^#?([0-9A-Fa-f]{6})([0-9A-Fa-f]{2})?$/
        var digits = color.StartsWith('#') ? color.AsSpan(1) : color.AsSpan();
        if ((digits.Length != 6 && digits.Length != 8) || !IsHex(digits))
        {
            throw new ArgumentException("Illegal value for token color: " + color, nameof(color));
        }

        var key = digits[..6].ToString().ToUpperInvariant();
        if (_color2id.TryGetValue(key, out var value))
        {
            return value;
        }

        value = (ColorId)(++_lastColorId);
        _color2id[key] = value;
        _id2color.Add(RgbaColor.FromHex("#" + key));
        return value;
    }

    public RgbaColor[] GetColorMap() => [.. _id2color];

    private static bool IsHex(ReadOnlySpan<char> text)
    {
        foreach (var ch in text)
        {
            if (!char.IsAsciiHexDigit(ch))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>The resolved token rules of a theme: matches token types to metadata (colors, font style, standard type).</summary>
internal sealed class TokenTheme
{
    private readonly ColorMap _colorMap;
    private readonly ThemeTrieElement _root;
    private readonly ConcurrentDictionary<string, uint> _cache = new(StringComparer.Ordinal);

    public TokenTheme(ColorMap colorMap, ThemeTrieElement root)
    {
        _colorMap = colorMap;
        _root = root;
    }

    public static TokenTheme CreateFromRawTokenTheme(IReadOnlyList<TokenThemeRule> source, IReadOnlyList<string> customTokenColors) =>
        CreateFromParsedTokenTheme(TokenThemeParser.ParseTokenTheme(source), customTokenColors);

    public static TokenTheme CreateFromParsedTokenTheme(IReadOnlyList<ParsedTokenThemeRule> source, IReadOnlyList<string> customTokenColors) =>
        TokenThemeParser.ResolveParsedTokenThemeRules(source, customTokenColors);

    public RgbaColor[] GetColorMap() => _colorMap.GetColorMap();

    /// <summary>Used for testing purposes.</summary>
    public ExternalThemeTrieElement GetThemeTrieElement() => _root.ToExternalThemeTrieElement();

    public ThemeTrieElementRule MatchRule(string token) => _root.Match(token);

    /// <summary>The metadata of a token type in a language (upstream's match).</summary>
    public uint Match(LanguageId languageId, string token)
    {
        // The cache contains the metadata without the language bits set.
        if (!_cache.TryGetValue(token, out var result))
        {
            var rule = MatchRule(token);
            var standardToken = TokenThemeParser.ToStandardTokenType(token);
            result = rule.Metadata | ((uint)standardToken << MetadataConsts.TokenTypeOffset);
            _cache[token] = result;
        }

        return result | ((uint)languageId << MetadataConsts.LanguageIdOffset);
    }
}

/// <summary>The style a theme trie node gives its token types.</summary>
internal sealed class ThemeTrieElementRule : IEquatable<ThemeTrieElementRule>
{
    private FontStyle _fontStyle;
    private ColorId _foreground;
    private ColorId _background;

    public ThemeTrieElementRule(FontStyle fontStyle, ColorId foreground, ColorId background)
    {
        _fontStyle = fontStyle;
        _foreground = foreground;
        _background = background;
        Metadata = ComputeMetadata();
    }

    public uint Metadata { get; private set; }

    public FontStyle FontStyle => _fontStyle;

    public ColorId Foreground => _foreground;

    public ColorId Background => _background;

    public ThemeTrieElementRule Clone() => new(_fontStyle, _foreground, _background);

    public void AcceptOverwrite(FontStyle fontStyle, ColorId foreground, ColorId background)
    {
        if (fontStyle != FontStyle.NotSet)
        {
            _fontStyle = fontStyle;
        }

        if (foreground != ColorId.None)
        {
            _foreground = foreground;
        }

        if (background != ColorId.None)
        {
            _background = background;
        }

        Metadata = ComputeMetadata();
    }

    public bool Equals(ThemeTrieElementRule? other) =>
        other is not null && other._fontStyle == _fontStyle && other._foreground == _foreground && other._background == _background;

    public override bool Equals(object? obj) => Equals(obj as ThemeTrieElementRule);

    public override int GetHashCode() => HashCode.Combine(_fontStyle, _foreground, _background);

    public override string ToString() => $"({_fontStyle}, {(int)_foreground}, {(int)_background})";

    private uint ComputeMetadata() =>
        ((uint)_fontStyle << MetadataConsts.FontStyleOffset)
        | ((uint)_foreground << MetadataConsts.ForegroundOffset)
        | ((uint)_background << MetadataConsts.BackgroundOffset);
}

/// <summary>A snapshot of a theme trie node and its children, for tests.</summary>
internal sealed class ExternalThemeTrieElement : IEquatable<ExternalThemeTrieElement>
{
    public ExternalThemeTrieElement(ThemeTrieElementRule mainRule, IReadOnlyDictionary<string, ExternalThemeTrieElement>? children = null)
    {
        MainRule = mainRule;
        Children = children ?? new Dictionary<string, ExternalThemeTrieElement>(StringComparer.Ordinal);
    }

    public ThemeTrieElementRule MainRule { get; }

    public IReadOnlyDictionary<string, ExternalThemeTrieElement> Children { get; }

    public bool Equals(ExternalThemeTrieElement? other)
    {
        if (other is null || !MainRule.Equals(other.MainRule) || Children.Count != other.Children.Count)
        {
            return false;
        }

        foreach (var (key, child) in Children)
        {
            if (!other.Children.TryGetValue(key, out var otherChild) || !child.Equals(otherChild))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as ExternalThemeTrieElement);

    public override int GetHashCode() => HashCode.Combine(MainRule, Children.Count);
}

/// <summary>A node of the theme trie: one segment of a dotted token type.</summary>
internal sealed class ThemeTrieElement
{
    private readonly ThemeTrieElementRule _mainRule;
    private readonly Dictionary<string, ThemeTrieElement> _children = new(StringComparer.Ordinal);

    public ThemeTrieElement(ThemeTrieElementRule mainRule)
    {
        _mainRule = mainRule;
    }

    /// <summary>Used for testing purposes.</summary>
    public ExternalThemeTrieElement ToExternalThemeTrieElement()
    {
        var children = new Dictionary<string, ExternalThemeTrieElement>(StringComparer.Ordinal);
        foreach (var (index, element) in _children)
        {
            children[index] = element.ToExternalThemeTrieElement();
        }

        return new ExternalThemeTrieElement(_mainRule, children);
    }

    public ThemeTrieElementRule Match(string token)
    {
        var node = this;
        var rest = token.AsSpan();
        while (true)
        {
            if (rest.IsEmpty)
            {
                return node._mainRule;
            }

            var dotIndex = rest.IndexOf('.');
            ReadOnlySpan<char> head;
            if (dotIndex == -1)
            {
                head = rest;
                rest = default;
            }
            else
            {
                head = rest[..dotIndex];
                rest = rest[(dotIndex + 1)..];
            }

            if (!node.TryGetChild(head, out var child))
            {
                return node._mainRule;
            }

            node = child;
        }
    }

    public void Insert(string token, FontStyle fontStyle, ColorId foreground, ColorId background)
    {
        if (token.Length == 0)
        {
            // Merge into the main rule
            _mainRule.AcceptOverwrite(fontStyle, foreground, background);
            return;
        }

        var dotIndex = token.IndexOf('.', StringComparison.Ordinal);
        string head;
        string tail;
        if (dotIndex == -1)
        {
            head = token;
            tail = "";
        }
        else
        {
            head = token[..dotIndex];
            tail = token[(dotIndex + 1)..];
        }

        if (!_children.TryGetValue(head, out var child))
        {
            child = new ThemeTrieElement(_mainRule.Clone());
            _children[head] = child;
        }

        child.Insert(tail, fontStyle, foreground, background);
    }

    private bool TryGetChild(ReadOnlySpan<char> head, out ThemeTrieElement child)
    {
#if NET9_0_OR_GREATER
        return _children.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(head, out child!);
#else
        return _children.TryGetValue(head.ToString(), out child!);
#endif
    }
}

/// <summary>The module functions of tokenization.ts.</summary>
internal static class TokenThemeParser
{
    private static readonly Regex StandardTokenTypeRegExp = new(
        @"\b(comment|string|regex|regexp)\b",
        RegexOptions.ECMAScript | RegexOptions.CultureInvariant);

    /// <summary>Parse a raw theme into rules.</summary>
    public static List<ParsedTokenThemeRule> ParseTokenTheme(IReadOnlyList<TokenThemeRule>? source)
    {
        var result = new List<ParsedTokenThemeRule>();
        if (source is null)
        {
            return result;
        }

        for (var i = 0; i < source.Count; i++)
        {
            var entry = source[i];

            var fontStyle = FontStyle.NotSet;
            if (entry.FontStyle is { } fontStyleText)
            {
                fontStyle = FontStyle.None;

                foreach (var segment in fontStyleText.Split(' '))
                {
                    switch (segment)
                    {
                        case "italic":
                            fontStyle |= FontStyle.Italic;
                            break;
                        case "bold":
                            fontStyle |= FontStyle.Bold;
                            break;
                        case "underline":
                            fontStyle |= FontStyle.Underline;
                            break;
                        case "strikethrough":
                            fontStyle |= FontStyle.Strikethrough;
                            break;
                    }
                }
            }

            result.Add(new ParsedTokenThemeRule(entry.Token ?? "", i, fontStyle, entry.Foreground, entry.Background));
        }

        return result;
    }

    /// <summary>Resolve rules (i.e. inheritance).</summary>
    internal static TokenTheme ResolveParsedTokenThemeRules(IReadOnlyList<ParsedTokenThemeRule> source, IReadOnlyList<string> customTokenColors)
    {
        // Sort rules lexicographically, and then by index if necessary (stably, as JavaScript does)
        var parsedThemeRules = source
            .Select((rule, position) => (rule, position))
            .OrderBy(x => x, Comparer<(ParsedTokenThemeRule Rule, int Position)>.Create(static (a, b) =>
            {
                var r = Strcmp(a.Rule.Token, b.Rule.Token);
                if (r != 0)
                {
                    return r;
                }

                r = a.Rule.Index - b.Rule.Index;
                return r != 0 ? r : a.Position - b.Position;
            }))
            .Select(x => x.rule)
            .ToList();

        // Determine defaults
        var defaultFontStyle = FontStyle.None;
        var defaultForeground = "000000";
        var defaultBackground = "ffffff";
        var start = 0;
        while (start < parsedThemeRules.Count && parsedThemeRules[start].Token.Length == 0)
        {
            var incomingDefaults = parsedThemeRules[start++];
            if (incomingDefaults.FontStyle != FontStyle.NotSet)
            {
                defaultFontStyle = incomingDefaults.FontStyle;
            }

            if (incomingDefaults.Foreground is not null)
            {
                defaultForeground = incomingDefaults.Foreground;
            }

            if (incomingDefaults.Background is not null)
            {
                defaultBackground = incomingDefaults.Background;
            }
        }

        var colorMap = new ColorMap();

        // start with token colors from custom token themes
        foreach (var color in customTokenColors)
        {
            colorMap.GetId(color);
        }

        var foregroundColorId = colorMap.GetId(defaultForeground);
        var backgroundColorId = colorMap.GetId(defaultBackground);

        var defaults = new ThemeTrieElementRule(defaultFontStyle, foregroundColorId, backgroundColorId);
        var root = new ThemeTrieElement(defaults);
        for (var i = start; i < parsedThemeRules.Count; i++)
        {
            var rule = parsedThemeRules[i];
            root.Insert(rule.Token, rule.FontStyle, colorMap.GetId(rule.Foreground), colorMap.GetId(rule.Background));
        }

        return new TokenTheme(colorMap, root);
    }

    public static StandardTokenType ToStandardTokenType(string tokenType)
    {
        var m = StandardTokenTypeRegExp.Match(tokenType);
        if (!m.Success)
        {
            return StandardTokenType.Other;
        }

        return m.Groups[1].Value switch
        {
            "comment" => StandardTokenType.Comment,
            "string" => StandardTokenType.String,
            "regex" => StandardTokenType.RegEx,
            "regexp" => StandardTokenType.RegEx,
            _ => throw new InvalidOperationException("Unexpected match for standard token type!"),
        };
    }

    /// <summary>Compares by UTF-16 code units, as JavaScript's <c>&lt;</c> on strings.</summary>
    public static int Strcmp(string a, string b)
    {
        var r = string.CompareOrdinal(a, b);
        return r < 0 ? -1 : r > 0 ? 1 : 0;
    }

    /// <summary>The CSS classes <c>.mtkN</c> of a color map and the font style classes.</summary>
    public static string GenerateTokensCSSForColorMap(IReadOnlyList<RgbaColor> colorMap)
    {
        var rules = new StringBuilder();
        for (var i = 1; i < colorMap.Count; i++)
        {
            // Upstream joins an array whose index 0 is a hole, so the text starts with an empty line.
            rules.Append('\n').Append(CultureInfo.InvariantCulture, $".mtk{i} {{ color: {colorMap[i].ToHex()}; }}");
        }

        if (colorMap.Count > 1)
        {
            rules.Append('\n');
        }

        rules.Append(".mtki { font-style: italic; }\n");
        rules.Append(".mtkb { font-weight: bold; }\n");
        rules.Append(".mtku { text-decoration: underline; text-underline-position: under; }\n");
        rules.Append(".mtks { text-decoration: line-through; }\n");
        rules.Append(".mtks.mtku { text-decoration: underline line-through; text-underline-position: under; }");
        return rules.ToString();
    }
}
