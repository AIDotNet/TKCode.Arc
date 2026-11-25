// Ported from vscode (src/vs/editor/common/services/unicodeTextModelHighlighter.ts, commit b57d5c9e; the result type
// IUnicodeHighlightsResult from src/vs/editor/common/services/editorWorker.ts), MIT license.
// Differences:
//  - the candidates are tested per code point instead of with a regular expression over UTF-16 code units, so a
//    character outside the BMP is one range (upstream finds it once per surrogate when it is at the end of a line);
//  - UnicodeHighlighterReason is a record with a kind instead of a union type.

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Services;

/// <summary>What the unicode highlighter reads (upstream's IUnicodeCharacterSearcherTarget).</summary>
internal interface IUnicodeCharacterSearcherTarget
{
    int GetLineCount();

    string GetLineContent(int lineNumber);
}

/// <summary>Which characters are highlighted (upstream's UnicodeHighlighterOptions).</summary>
internal sealed record UnicodeHighlighterOptions
{
    /// <summary>Every character that is not basic ASCII.</summary>
    public bool NonBasicAscii { get; init; }

    /// <summary>Characters that can be confused with basic ASCII ones.</summary>
    public bool AmbiguousCharacters { get; init; } = true;

    /// <summary>Characters that only reserve space or have no width.</summary>
    public bool InvisibleCharacters { get; init; } = true;

    /// <summary>Whether characters in comments are highlighted too.</summary>
    public bool IncludeComments { get; init; } = true;

    /// <summary>Whether characters in strings are highlighted too.</summary>
    public bool IncludeStrings { get; init; } = true;

    /// <summary>Code points that are never highlighted.</summary>
    public IReadOnlyList<int> AllowedCodePoints { get; init; } = [];

    /// <summary>Locales whose common characters are not ambiguous.</summary>
    public IReadOnlyList<string> AllowedLocales { get; init; } = [];
}

/// <summary>The highlighted ranges of a model and why (upstream's IUnicodeHighlightsResult).</summary>
internal sealed record UnicodeHighlightsResult(
    IReadOnlyList<TextRange> Ranges,
    bool HasMore,
    int NonBasicAsciiCharacterCount,
    int InvisibleCharacterCount,
    int AmbiguousCharacterCount);

/// <summary>Why a character is highlighted (upstream's UnicodeHighlighterReasonKind).</summary>
internal enum UnicodeHighlighterReasonKind
{
    Ambiguous,
    Invisible,
    NonBasicAscii,
}

/// <summary>Why a character is highlighted, with what it looks like (upstream's UnicodeHighlighterReason).</summary>
internal sealed record UnicodeHighlighterReason(UnicodeHighlighterReasonKind Kind)
{
    /// <summary>The ASCII character an ambiguous one can be confused with.</summary>
    public string? ConfusableWith { get; init; }

    /// <summary>The locales in which an ambiguous character is common.</summary>
    public IReadOnlyList<string> NotAmbiguousInLocales { get; init; } = [];
}

/// <summary>Finds the characters to highlight (upstream's UnicodeTextModelHighlighter).</summary>
internal static class UnicodeTextModelHighlighter
{
    private const int MaxResultLength = 1000;

    /// <summary>The ranges to highlight between the lines of <paramref name="range"/>, or the whole model.</summary>
    public static UnicodeHighlightsResult ComputeUnicodeHighlights(IUnicodeCharacterSearcherTarget model, UnicodeHighlighterOptions options, TextRange? range = null)
    {
        var startLine = range?.StartLineNumber ?? 1;
        var endLine = range?.EndLineNumber ?? model.GetLineCount();

        var codePointHighlighter = new CodePointHighlighter(options);
        var candidates = codePointHighlighter.GetCandidateCodePoints();

        var ranges = new List<TextRange>();
        var hasMore = false;

        var ambiguousCharacterCount = 0;
        var invisibleCharacterCount = 0;
        var nonBasicAsciiCharacterCount = 0;

        for (var lineNumber = startLine; lineNumber <= endLine; lineNumber++)
        {
            var lineContent = model.GetLineContent(lineNumber);
            var lineLength = lineContent.Length;

            for (var index = 0; index < lineLength;)
            {
                var codePoint = UnicodeCharacterHelpers.CodePointAt(lineContent, index);
                var startIndex = index;
                var endIndex = index + (codePoint > 0xFFFF ? 2 : 1);
                index = endIndex;

                var isCandidate = candidates is null
                    ? !(codePoint is '\t' or '\n' or '\r' || (codePoint >= 0x20 && codePoint <= 0x7E))
                    : candidates.Contains(codePoint);
                if (!isCandidate)
                {
                    continue;
                }

                var str = lineContent.Substring(startIndex, endIndex - startIndex);
                var word = WordHelper.GetWordAtText(startIndex + 1, WordHelper.DefaultWordRegExp, lineContent, 0);
                if (word is not null && word.EndColumn <= startIndex + 1)
                {
                    // The word does not include the problematic character, ignore the word
                    word = null;
                }

                var highlightReason = codePointHighlighter.ShouldHighlightNonBasicAscii(str, word?.Word);
                if (highlightReason == SimpleHighlightReason.None)
                {
                    continue;
                }

                switch (highlightReason)
                {
                    case SimpleHighlightReason.Ambiguous:
                        ambiguousCharacterCount++;
                        break;
                    case SimpleHighlightReason.Invisible:
                        invisibleCharacterCount++;
                        break;
                    default:
                        nonBasicAsciiCharacterCount++;
                        break;
                }

                if (ranges.Count >= MaxResultLength)
                {
                    hasMore = true;
                    return Result();
                }

                ranges.Add(new TextRange(lineNumber, startIndex + 1, lineNumber, endIndex + 1));
            }
        }

        return Result();

        UnicodeHighlightsResult Result() =>
            new(ranges, hasMore, nonBasicAsciiCharacterCount, invisibleCharacterCount, ambiguousCharacterCount);
    }

    /// <summary>Why <paramref name="character"/> is highlighted, or null (upstream's computeUnicodeHighlightReason).</summary>
    public static UnicodeHighlighterReason? ComputeUnicodeHighlightReason(string character, UnicodeHighlighterOptions options)
    {
        var codePointHighlighter = new CodePointHighlighter(options);

        var reason = codePointHighlighter.ShouldHighlightNonBasicAscii(character, null);
        switch (reason)
        {
            case SimpleHighlightReason.None:
                return null;
            case SimpleHighlightReason.Invisible:
                return new UnicodeHighlighterReason(UnicodeHighlighterReasonKind.Invisible);
            case SimpleHighlightReason.Ambiguous:
            {
                var codePoint = UnicodeCharacterHelpers.CodePointAt(character, 0);
                var primaryConfusable = codePointHighlighter.AmbiguousCharacters.GetPrimaryConfusable(codePoint)!.Value;
                var notAmbiguousInLocales = AmbiguousCharacters.GetLocales()
                    .Where(l => !AmbiguousCharacters.GetInstance(options.AllowedLocales.Append(l).Distinct()).IsAmbiguous(codePoint))
                    .ToList();
                return new UnicodeHighlighterReason(UnicodeHighlighterReasonKind.Ambiguous)
                {
                    ConfusableWith = char.ConvertFromUtf32(primaryConfusable),
                    NotAmbiguousInLocales = notAmbiguousInLocales,
                };
            }

            default:
                return new UnicodeHighlighterReason(UnicodeHighlighterReasonKind.NonBasicAscii);
        }
    }

    private enum SimpleHighlightReason
    {
        None,
        NonBasicAscii,
        Invisible,
        Ambiguous,
    }

    private static bool IsAllowedInvisibleCharacter(int codePoint) => codePoint is ' ' or '\n' or '\t';

    private sealed class CodePointHighlighter
    {
        private readonly UnicodeHighlighterOptions _options;
        private readonly HashSet<int> _allowedCodePoints;

        public CodePointHighlighter(UnicodeHighlighterOptions options)
        {
            _options = options;
            _allowedCodePoints = [.. options.AllowedCodePoints];
            AmbiguousCharacters = global::TKCode.Arc.Editor.AmbiguousCharacters.GetInstance(options.AllowedLocales.Distinct());
        }

        public AmbiguousCharacters AmbiguousCharacters { get; }

        /// <summary>The code points to test, or null for every non basic ASCII one.</summary>
        public HashSet<int>? GetCandidateCodePoints()
        {
            if (_options.NonBasicAscii)
            {
                return null;
            }

            var set = new HashSet<int>();

            if (_options.InvisibleCharacters)
            {
                foreach (var cp in InvisibleCharacters.CodePoints)
                {
                    if (!IsAllowedInvisibleCharacter(cp))
                    {
                        set.Add(cp);
                    }
                }
            }

            if (_options.AmbiguousCharacters)
            {
                foreach (var cp in AmbiguousCharacters.GetConfusableCodePoints())
                {
                    set.Add(cp);
                }
            }

            foreach (var cp in _allowedCodePoints)
            {
                set.Remove(cp);
            }

            return set;
        }

        public SimpleHighlightReason ShouldHighlightNonBasicAscii(string character, string? wordContext)
        {
            var codePoint = UnicodeCharacterHelpers.CodePointAt(character, 0);

            if (_allowedCodePoints.Contains(codePoint))
            {
                return SimpleHighlightReason.None;
            }

            if (_options.NonBasicAscii)
            {
                return SimpleHighlightReason.NonBasicAscii;
            }

            var hasBasicAsciiCharacters = false;
            var hasNonConfusableNonBasicAsciiCharacter = false;
            if (!string.IsNullOrEmpty(wordContext))
            {
                for (var i = 0; i < wordContext.Length;)
                {
                    var cp = UnicodeCharacterHelpers.CodePointAt(wordContext, i);
                    var length = cp > 0xFFFF ? 2 : 1;
                    var isBasicAscii = Strings.IsBasicASCII(wordContext.Substring(i, length));
                    i += length;
                    hasBasicAsciiCharacters = hasBasicAsciiCharacters || isBasicAscii;

                    if (!isBasicAscii && !AmbiguousCharacters.IsAmbiguous(cp) && !InvisibleCharacters.IsInvisibleCharacter(cp))
                    {
                        hasNonConfusableNonBasicAsciiCharacter = true;
                    }
                }
            }

            if (/* Don't allow mixing weird looking characters with ASCII */ !hasBasicAsciiCharacters
                && /* Is there an obviously weird looking character? */ hasNonConfusableNonBasicAsciiCharacter)
            {
                return SimpleHighlightReason.None;
            }

            if (_options.InvisibleCharacters)
            {
                // TODO check for emojis
                if (!IsAllowedInvisibleCharacter(codePoint) && InvisibleCharacters.IsInvisibleCharacter(codePoint))
                {
                    return SimpleHighlightReason.Invisible;
                }
            }

            if (_options.AmbiguousCharacters && AmbiguousCharacters.IsAmbiguous(codePoint))
            {
                return SimpleHighlightReason.Ambiguous;
            }

            return SimpleHighlightReason.None;
        }
    }
}

/// <summary>A model as the unicode highlighter's target.</summary>
internal sealed class TextModelUnicodeTarget(TextModel model) : IUnicodeCharacterSearcherTarget
{
    public int GetLineCount() => model.GetLineCount();

    public string GetLineContent(int lineNumber) => model.GetLineContent(lineNumber);
}
