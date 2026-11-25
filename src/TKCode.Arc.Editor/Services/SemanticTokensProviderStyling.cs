// Ported from vscode (src/vs/editor/common/services/semanticTokensProviderStyling.ts, commit b57d5c9e), MIT license.
// Also the part of src/vs/platform/theme/common/themeService.ts a theme answers for semantic tokens (ITokenStyle,
// IColorTheme.getTokenStyleMetadata and semanticHighlighting).
//
// Differences:
//  - the theme is an ISemanticTokensColorTheme (upstream's IColorTheme reduced to the two members used here); the
//    standalone theme's adapter is StandaloneSemanticTokensTheme (SemanticTokensStylingService.cs);
//  - the warnings go to an optional Action<string> instead of a log service (each is still reported once);
//  - the language id is encoded with the model's ILanguageIdCodec (upstream: the language service's);
//  - token data is uint[] (upstream's Uint32Array); "| 0" casts to int32 are unchecked int casts.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Services;

/// <summary>How a theme styles a semantic token (upstream's ITokenStyle); null members are not set by the theme.</summary>
internal sealed record TokenStyle(int? Foreground, bool? Bold = null, bool? Italic = null, bool? Underline = null, bool? Strikethrough = null);

/// <summary>The part of a color theme semantic tokens read (upstream's IColorTheme).</summary>
internal interface ISemanticTokensColorTheme
{
    /// <summary>Whether the theme asks for semantic highlighting (used when the setting is <c>configuredByTheme</c>).</summary>
    bool SemanticHighlighting { get; }

    /// <summary>The style of a semantic token type with modifiers in a language; null when the theme has none.</summary>
    TokenStyle? GetTokenStyleMetadata(string type, IReadOnlyList<string> modifiers, string modelLanguage);
}

/// <summary>Where semantic tokens find the current theme (upstream's IThemeService, the part used here).</summary>
internal interface ISemanticTokensThemeService
{
    ISemanticTokensColorTheme GetColorTheme();

    /// <summary>Raised after the current theme changed.</summary>
    event Action? DidColorThemeChange;
}

/// <summary>Maps a provider's token types and modifiers to token metadata through the theme, with a cache.</summary>
internal sealed class SemanticTokensProviderStyling
{
    /// <summary>The metadata of a token the theme does not style (upstream's NO_STYLING).</summary>
    public const uint NoStyling = 0b01111111111111111111111111111111;

    private readonly SemanticTokensLegend _legend;
    private readonly ISemanticTokensThemeService _themeService;
    private readonly ILanguageIdCodec _languageIdCodec;
    private readonly Action<string>? _log;
    private readonly HashTable _hashTable;
    private bool _hasWarnedOverlappingTokens;
    private bool _hasWarnedInvalidLengthTokens;
    private bool _hasWarnedInvalidEditStart;
    private bool _hasWarnedInvalidEditDeleteCount;

    public SemanticTokensProviderStyling(
        SemanticTokensLegend legend,
        ISemanticTokensThemeService themeService,
        ILanguageIdCodec languageIdCodec,
        Action<string>? log = null)
    {
        _legend = legend;
        _themeService = themeService;
        _languageIdCodec = languageIdCodec;
        _log = log;
        _hashTable = new HashTable();
    }

    public uint GetMetadata(uint tokenTypeIndex, uint tokenModifierSet, string languageId)
    {
        var encodedLanguageId = (int)_languageIdCodec.EncodeLanguageId(languageId);
        var entry = _hashTable.Get(tokenTypeIndex, tokenModifierSet, encodedLanguageId);
        if (entry is not null)
        {
            return entry.Metadata;
        }

        uint metadata;
        var tokenType = tokenTypeIndex < (uint)_legend.TokenTypes.Count ? _legend.TokenTypes[(int)tokenTypeIndex] : null;
        if (tokenType is not null)
        {
            var tokenModifiers = new List<string>();
            var modifierSet = tokenModifierSet;
            for (var modifierIndex = 0; modifierSet > 0 && modifierIndex < _legend.TokenModifiers.Count; modifierIndex++)
            {
                if ((modifierSet & 1) != 0)
                {
                    tokenModifiers.Add(_legend.TokenModifiers[modifierIndex]);
                }

                modifierSet >>= 1;
            }

            var tokenStyle = _themeService.GetColorTheme().GetTokenStyleMetadata(tokenType, tokenModifiers, languageId);
            if (tokenStyle is null)
            {
                metadata = NoStyling;
            }
            else
            {
                metadata = 0;
                if (tokenStyle.Italic is { } italic)
                {
                    var italicBit = (uint)(italic ? FontStyle.Italic : 0) << MetadataConsts.FontStyleOffset;
                    metadata |= italicBit | MetadataConsts.SemanticUseItalic;
                }

                if (tokenStyle.Bold is { } bold)
                {
                    var boldBit = (uint)(bold ? FontStyle.Bold : 0) << MetadataConsts.FontStyleOffset;
                    metadata |= boldBit | MetadataConsts.SemanticUseBold;
                }

                if (tokenStyle.Underline is { } underline)
                {
                    var underlineBit = (uint)(underline ? FontStyle.Underline : 0) << MetadataConsts.FontStyleOffset;
                    metadata |= underlineBit | MetadataConsts.SemanticUseUnderline;
                }

                if (tokenStyle.Strikethrough is { } strikethrough)
                {
                    var strikethroughBit = (uint)(strikethrough ? FontStyle.Strikethrough : 0) << MetadataConsts.FontStyleOffset;
                    metadata |= strikethroughBit | MetadataConsts.SemanticUseStrikethrough;
                }

                if (tokenStyle.Foreground is { } foreground and not 0)
                {
                    var foregroundBits = (uint)foreground << MetadataConsts.ForegroundOffset;
                    metadata |= foregroundBits | MetadataConsts.SemanticUseForeground;
                }

                if (metadata == 0)
                {
                    // Nothing!
                    metadata = NoStyling;
                }
            }
        }
        else
        {
            metadata = NoStyling;
        }

        _hashTable.Add(tokenTypeIndex, tokenModifierSet, encodedLanguageId, metadata);
        return metadata;
    }

    public void WarnOverlappingSemanticTokens(int lineNumber, int startColumn)
    {
        if (!_hasWarnedOverlappingTokens)
        {
            _hasWarnedOverlappingTokens = true;
            _log?.Invoke($"Overlapping semantic tokens detected at lineNumber {lineNumber}, column {startColumn}");
        }
    }

    public void WarnInvalidLengthSemanticTokens(int lineNumber, int startColumn)
    {
        if (!_hasWarnedInvalidLengthTokens)
        {
            _hasWarnedInvalidLengthTokens = true;
            _log?.Invoke($"Semantic token with invalid length detected at lineNumber {lineNumber}, column {startColumn}");
        }
    }

    public void WarnInvalidEditStart(string? previousResultId, string? resultId, int editIndex, long editStart, int maxExpectedStart)
    {
        if (!_hasWarnedInvalidEditStart)
        {
            _hasWarnedInvalidEditStart = true;
            _log?.Invoke($"Invalid semantic tokens edit detected (previousResultId: {previousResultId}, resultId: {resultId}) at edit #{editIndex}: The provided start offset {editStart} is outside the previous data (length {maxExpectedStart}).");
        }
    }

    public void WarnInvalidEditDeleteCount(string? previousResultId, string? resultId, int srcLength, long deltaLength)
    {
        if (!_hasWarnedInvalidEditDeleteCount)
        {
            _hasWarnedInvalidEditDeleteCount = true;
            _log?.Invoke($"Invalid semantic tokens edit detected (previousResultId: {previousResultId}, resultId: {resultId}): The edits delete more tokens than the previous data contains (previous length {srcLength}, computed length {srcLength + deltaLength}).");
        }
    }

    private sealed class HashTableEntry(uint tokenTypeIndex, uint tokenModifierSet, int languageId, uint metadata)
    {
        public uint TokenTypeIndex { get; } = tokenTypeIndex;

        public uint TokenModifierSet { get; } = tokenModifierSet;

        public int LanguageId { get; } = languageId;

        public uint Metadata { get; } = metadata;

        public HashTableEntry? Next { get; set; }
    }

    private sealed class HashTable
    {
        private static readonly int[] s_sizes = [3, 7, 13, 31, 61, 127, 251, 509, 1021, 2039, 4093, 8191, 16381, 32749, 65521, 131071, 262139, 524287, 1048573, 2097143];

        private int _elementsCount;
        private int _currentLengthIndex;
        private int _currentLength;
        private int _growCount;
        private HashTableEntry?[] _elements;

        public HashTable()
        {
            _elementsCount = 0;
            _currentLengthIndex = 0;
            _currentLength = s_sizes[_currentLengthIndex];
            _growCount = ComputeGrowCount();
            _elements = new HashTableEntry?[_currentLength];
        }

        private int ComputeGrowCount() =>
            (int)Math.Round(_currentLengthIndex + 1 < s_sizes.Length ? 2.0 / 3 * _currentLength : 0, MidpointRounding.AwayFromZero);

        private static int Hash2(int n1, int n2) => unchecked(((n1 << 5) - n1) + n2); // n1 * 31 + n2, keep as int32

        private int HashFunc(uint tokenTypeIndex, uint tokenModifierSet, int languageId)
        {
            // upstream's % keeps the sign of a negative hash, and indexing an array with a negative number misses;
            // the remainder is made positive here so that every entry has a bucket
            var hash = Hash2(Hash2(unchecked((int)tokenTypeIndex), unchecked((int)tokenModifierSet)), languageId) % _currentLength;
            return hash < 0 ? hash + _currentLength : hash;
        }

        public HashTableEntry? Get(uint tokenTypeIndex, uint tokenModifierSet, int languageId)
        {
            var hash = HashFunc(tokenTypeIndex, tokenModifierSet, languageId);
            var p = _elements[hash];
            while (p is not null)
            {
                if (p.TokenTypeIndex == tokenTypeIndex && p.TokenModifierSet == tokenModifierSet && p.LanguageId == languageId)
                {
                    return p;
                }

                p = p.Next;
            }

            return null;
        }

        public void Add(uint tokenTypeIndex, uint tokenModifierSet, int languageId, uint metadata)
        {
            _elementsCount++;
            if (_growCount != 0 && _elementsCount >= _growCount)
            {
                // expand!
                var oldElements = _elements;

                _currentLengthIndex++;
                _currentLength = s_sizes[_currentLengthIndex];
                _growCount = ComputeGrowCount();
                _elements = new HashTableEntry?[_currentLength];

                foreach (var first in oldElements)
                {
                    var p = first;
                    while (p is not null)
                    {
                        var oldNext = p.Next;
                        p.Next = null;
                        AddEntry(p);
                        p = oldNext;
                    }
                }
            }

            AddEntry(new HashTableEntry(tokenTypeIndex, tokenModifierSet, languageId, metadata));
        }

        private void AddEntry(HashTableEntry element)
        {
            var hash = HashFunc(element.TokenTypeIndex, element.TokenModifierSet, element.LanguageId);
            element.Next = _elements[hash];
            _elements[hash] = element;
        }
    }
}

/// <summary>Turns semantic token data into the model's sparse tokens (upstream's toMultilineTokens2).</summary>
internal static class SemanticTokensConversion
{
    // Let's aim at having 8KB buffers if possible... So that would be 8192 / (5 * 4) = 409.6 tokens per area
    private const int DesiredTokensPerArea = 400;

    // Try to keep the total number of areas under 1024 if possible, simply compensate by having more tokens per area...
    private const int DesiredMaxAreas = 1024;

    public static List<SparseMultilineTokens> ToMultilineTokens2(SemanticTokens tokens, SemanticTokensProviderStyling styling, string languageId)
    {
        var srcData = tokens.Data;
        var tokenCount = srcData.Length / 5;
        var tokensPerArea = Math.Max((int)Math.Ceiling(tokenCount / (double)DesiredMaxAreas), DesiredTokensPerArea);
        var result = new List<SparseMultilineTokens>();

        var tokenIndex = 0;
        var lastLineNumber = 1;
        var lastStartCharacter = 0;
        while (tokenIndex < tokenCount)
        {
            var tokenStartIndex = tokenIndex;
            var tokenEndIndex = Math.Min(tokenStartIndex + tokensPerArea, tokenCount);

            // Keep tokens on the same line in the same area...
            if (tokenEndIndex < tokenCount)
            {
                var smallTokenEndIndex = tokenEndIndex;
                while (smallTokenEndIndex - 1 > tokenStartIndex && srcData[5 * smallTokenEndIndex] == 0)
                {
                    smallTokenEndIndex--;
                }

                if (smallTokenEndIndex - 1 == tokenStartIndex)
                {
                    // there are so many tokens on this line that our area would be empty, we must now go right
                    var bigTokenEndIndex = tokenEndIndex;
                    while (bigTokenEndIndex + 1 < tokenCount && srcData[5 * bigTokenEndIndex] == 0)
                    {
                        bigTokenEndIndex++;
                    }

                    tokenEndIndex = bigTokenEndIndex;
                }
                else
                {
                    tokenEndIndex = smallTokenEndIndex;
                }
            }

            var destData = new uint[(tokenEndIndex - tokenStartIndex) * 4];
            var destOffset = 0;
            var areaLine = 0;
            var prevLineNumber = 0;
            var prevEndCharacter = 0;
            while (tokenIndex < tokenEndIndex)
            {
                var srcOffset = 5 * tokenIndex;
                var deltaLine = srcData[srcOffset];
                var deltaCharacter = srcData[srcOffset + 1];

                // Casting both lineNumber, startCharacter and endCharacter here to int32 (upstream's `|0`) to validate
                // below with the actual values that will be inserted in the result
                var lineNumber = unchecked(lastLineNumber + (int)deltaLine);
                var startCharacter = deltaLine == 0 ? unchecked(lastStartCharacter + (int)deltaCharacter) : unchecked((int)deltaCharacter);
                var length = srcData[srcOffset + 2];
                var endCharacter = unchecked(startCharacter + (int)length);
                var tokenTypeIndex = srcData[srcOffset + 3];
                var tokenModifierSet = srcData[srcOffset + 4];

                if (endCharacter <= startCharacter)
                {
                    // this token is invalid (most likely a negative length casted to uint32)
                    styling.WarnInvalidLengthSemanticTokens(lineNumber, startCharacter + 1);
                }
                else if (prevLineNumber == lineNumber && prevEndCharacter > startCharacter)
                {
                    // this token overlaps with the previous token
                    styling.WarnOverlappingSemanticTokens(lineNumber, startCharacter + 1);
                }
                else
                {
                    var metadata = styling.GetMetadata(tokenTypeIndex, tokenModifierSet, languageId);
                    if (metadata != SemanticTokensProviderStyling.NoStyling)
                    {
                        if (areaLine == 0)
                        {
                            areaLine = lineNumber;
                        }

                        destData[destOffset] = unchecked((uint)(lineNumber - areaLine));
                        destData[destOffset + 1] = unchecked((uint)startCharacter);
                        destData[destOffset + 2] = unchecked((uint)endCharacter);
                        destData[destOffset + 3] = metadata;
                        destOffset += 4;

                        prevLineNumber = lineNumber;
                        prevEndCharacter = endCharacter;
                    }
                }

                lastLineNumber = lineNumber;
                lastStartCharacter = startCharacter;
                tokenIndex++;
            }

            if (destOffset != destData.Length)
            {
                destData = destData.AsSpan(0, destOffset).ToArray();
            }

            result.Add(SparseMultilineTokens.Create(areaLine, destData));
        }

        return result;
    }
}
