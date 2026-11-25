// Ported from vscode (src/vs/editor/common/viewModel/monospaceLineBreaksComputer.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - MonospaceLineBreaksComputerFactory.create(options) is left out until the editor options hold the wrapping
//    options; the constructor takes the two break character lists (WrappingOptions has their defaults);
//  - upstream reuses two module-level arrays (arrPool1, arrPool2) between computations; that is static mutable state,
//    so each computation here builds new arrays. As upstream, a computation from previous line breaks writes its result
//    into the previous ModelLineProjectionData and returns it;
//  - WrappingCharacterClassifier wraps a CharacterClassifier (whose Get is not virtual) instead of deriving from it;
//  - charCodeAt past the end of the line gives -1 where upstream gets NaN: it classifies as CharacterClass.NONE, is no
//    tab, space or surrogate, and counts as one column, as NaN does upstream;
//  - an empty list of injected texts from the context is "some injected text" for the fast path, as upstream's `!injectedText`
//    is false for an empty array; only null takes the path from the previous line breaks.

using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.ViewModel;

/// <summary>Breaks lines on the assumption that the font is monospace.</summary>
internal sealed class MonospaceLineBreaksComputerFactory : ILineBreaksComputerFactory
{
    private const int CharCodeNull = 0;
    private const int CharCodeTab = '\t';
    private const int CharCodeSpace = ' ';

    private readonly WrappingCharacterClassifier _classifier;

    public MonospaceLineBreaksComputerFactory(string breakBeforeChars, string breakAfterChars)
    {
        _classifier = new WrappingCharacterClassifier(breakBeforeChars, breakAfterChars);
    }

    public ILineBreaksComputer CreateLineBreaksComputer(
        ILineBreaksComputerContext context,
        FontInfo fontInfo,
        int tabSize,
        int wrappingColumn,
        WrappingIndent wrappingIndent,
        WordBreak wordBreak,
        bool wrapOnEscapedLineFeeds,
        bool useTwoCellFullwidthCharacters) =>
        new LineBreaksComputer(this, context, fontInfo, tabSize, wrappingColumn, wrappingIndent, wordBreak, wrapOnEscapedLineFeeds, useTwoCellFullwidthCharacters);

    private sealed class LineBreaksComputer : ILineBreaksComputer
    {
        private readonly MonospaceLineBreaksComputerFactory _factory;
        private readonly ILineBreaksComputerContext _context;
        private readonly FontInfo _fontInfo;
        private readonly int _tabSize;
        private readonly int _wrappingColumn;
        private readonly WrappingIndent _wrappingIndent;
        private readonly WordBreak _wordBreak;
        private readonly bool _wrapOnEscapedLineFeeds;
        private readonly bool _useTwoCellFullwidthCharacters;
        private readonly List<int> _lineNumbers = new();
        private readonly List<ModelLineProjectionData?> _previousBreakingData = new();

        public LineBreaksComputer(
            MonospaceLineBreaksComputerFactory factory,
            ILineBreaksComputerContext context,
            FontInfo fontInfo,
            int tabSize,
            int wrappingColumn,
            WrappingIndent wrappingIndent,
            WordBreak wordBreak,
            bool wrapOnEscapedLineFeeds,
            bool useTwoCellFullwidthCharacters)
        {
            _factory = factory;
            _context = context;
            _fontInfo = fontInfo;
            _tabSize = tabSize;
            _wrappingColumn = wrappingColumn;
            _wrappingIndent = wrappingIndent;
            _wordBreak = wordBreak;
            _wrapOnEscapedLineFeeds = wrapOnEscapedLineFeeds;
            _useTwoCellFullwidthCharacters = useTwoCellFullwidthCharacters;
        }

        public void AddRequest(int lineNumber, ModelLineProjectionData? previousLineBreakData)
        {
            _lineNumbers.Add(lineNumber);
            _previousBreakingData.Add(previousLineBreakData);
        }

        public ModelLineProjectionData?[] Finalize()
        {
            var columnsForFullWidthChar = _fontInfo.TypicalFullwidthCharacterWidth / _fontInfo.TypicalHalfwidthCharacterWidth;
            var result = new ModelLineProjectionData?[_lineNumbers.Count];
            for (int i = 0, len = _lineNumbers.Count; i < len; i++)
            {
                var lineNumber = _lineNumbers[i];
                var injectedText = _context.GetLineInjectedText(lineNumber);
                var lineText = _context.GetLineContent(lineNumber);
                var previousLineBreakData = _previousBreakingData[i];
                var isLineFeedWrappingEnabled = _wrapOnEscapedLineFeeds && lineText.Contains('"') && lineText.Contains("\\n", StringComparison.Ordinal);
                if (previousLineBreakData is not null && previousLineBreakData.InjectionOptions is null && injectedText is null && !isLineFeedWrappingEnabled)
                {
                    result[i] = CreateLineBreaksFromPreviousLineBreaks(_factory._classifier, previousLineBreakData, lineText, _tabSize, _wrappingColumn, columnsForFullWidthChar, _wrappingIndent, _wordBreak, _useTwoCellFullwidthCharacters);
                }
                else
                {
                    result[i] = CreateLineBreaks(_factory._classifier, lineText, injectedText, _tabSize, _wrappingColumn, columnsForFullWidthChar, _fontInfo, _wrappingIndent, _wordBreak, isLineFeedWrappingEnabled, _useTwoCellFullwidthCharacters);
                }
            }

            return result;
        }
    }

    private enum CharacterClass : byte
    {
        NONE = 0,
        BREAK_BEFORE = 1,
        BREAK_AFTER = 2,

        /// <summary>For Han and Kana.</summary>
        BREAK_IDEOGRAPHIC = 3,
    }

    private sealed class WrappingCharacterClassifier
    {
        private readonly CharacterClassifier _classifier = new((byte)CharacterClass.NONE);

        public WrappingCharacterClassifier(string breakBefore, string breakAfter)
        {
            for (var i = 0; i < breakBefore.Length; i++)
            {
                _classifier.Set(breakBefore[i], (byte)CharacterClass.BREAK_BEFORE);
            }

            for (var i = 0; i < breakAfter.Length; i++)
            {
                _classifier.Set(breakAfter[i], (byte)CharacterClass.BREAK_AFTER);
            }
        }

        public CharacterClass Get(int charCode)
        {
            if (charCode >= 0 && charCode < 256)
            {
                return (CharacterClass)_classifier.Get(charCode);
            }

            // Initialize CharacterClass.BREAK_IDEOGRAPHIC for these Unicode ranges:
            // 1. CJK Unified Ideographs (0x4E00 -- 0x9FFF)
            // 2. CJK Unified Ideographs Extension A (0x3400 -- 0x4DBF)
            // 3. Hiragana and Katakana (0x3040 -- 0x30FF)
            if ((charCode >= 0x3040 && charCode <= 0x30FF)
                || (charCode >= 0x3400 && charCode <= 0x4DBF)
                || (charCode >= 0x4E00 && charCode <= 0x9FFF))
            {
                return CharacterClass.BREAK_IDEOGRAPHIC;
            }

            return (CharacterClass)_classifier.Get(charCode);
        }
    }

    /// <summary>JavaScript's <c>charCodeAt</c>, with -1 for upstream's NaN past the end.</summary>
    private static int CharCodeAt(string text, int index) => index >= 0 && index < text.Length ? text[index] : -1;

    /// <summary>An array read that gives NaN past the end, as a JavaScript array read gives undefined.</summary>
    private static double At(double[] array, int index) => index >= 0 && index < array.Length ? array[index] : double.NaN;

    private static ModelLineProjectionData? CreateLineBreaksFromPreviousLineBreaks(
        WrappingCharacterClassifier classifier,
        ModelLineProjectionData previousBreakingData,
        string lineText,
        int tabSize,
        int firstLineBreakColumn,
        double columnsForFullWidthChar,
        WrappingIndent wrappingIndent,
        WordBreak wordBreak,
        bool useTwoCellFullwidthCharacters)
    {
        if (firstLineBreakColumn == -1)
        {
            return null;
        }

        var len = lineText.Length;
        if (len <= 1)
        {
            return null;
        }

        var isKeepAll = wordBreak == WordBreak.KeepAll;

        var prevBreakingOffsets = previousBreakingData.BreakOffsets;
        var prevBreakingOffsetsVisibleColumn = previousBreakingData.BreakOffsetsVisibleColumn;

        var wrappedTextIndentLength = ComputeWrappedTextIndentLength(lineText, tabSize, firstLineBreakColumn, FullWidthCharacterColumns(columnsForFullWidthChar, useTwoCellFullwidthCharacters), wrappingIndent);
        var wrappedLineBreakColumn = firstLineBreakColumn - wrappedTextIndentLength;

        var breakingOffsets = new List<int>();
        var breakingOffsetsVisibleColumn = new List<double>();
        var lastBreakingOffset = 0;
        double lastBreakingOffsetVisibleColumn = 0;

        double breakingColumn = firstLineBreakColumn;
        var prevLen = prevBreakingOffsets.Length;
        var prevIndex = 0;

        if (prevIndex >= 0)
        {
            var bestDistance = Math.Abs(At(prevBreakingOffsetsVisibleColumn, prevIndex) - breakingColumn);
            while (prevIndex + 1 < prevLen)
            {
                var distance = Math.Abs(prevBreakingOffsetsVisibleColumn[prevIndex + 1] - breakingColumn);
                if (distance >= bestDistance)
                {
                    break;
                }

                bestDistance = distance;
                prevIndex++;
            }
        }

        while (prevIndex < prevLen)
        {
            // Allow for prevIndex to be -1 (for the case where we hit a tab when walking backwards from the first break)
            var prevBreakOffset = prevIndex < 0 ? 0 : prevBreakingOffsets[prevIndex];
            var prevBreakOffsetVisibleColumn = prevIndex < 0 ? 0 : prevBreakingOffsetsVisibleColumn[prevIndex];
            if (lastBreakingOffset > prevBreakOffset)
            {
                prevBreakOffset = lastBreakingOffset;
                prevBreakOffsetVisibleColumn = lastBreakingOffsetVisibleColumn;
            }

            var breakOffset = 0;
            double breakOffsetVisibleColumn = 0;

            var forcedBreakOffset = 0;
            double forcedBreakOffsetVisibleColumn = 0;

            // initially, we search as much as possible to the right (if it fits)
            if (prevBreakOffsetVisibleColumn <= breakingColumn)
            {
                var visibleColumn = prevBreakOffsetVisibleColumn;
                var prevCharCode = prevBreakOffset == 0 ? CharCodeNull : CharCodeAt(lineText, prevBreakOffset - 1);
                var prevCharCodeClass = prevBreakOffset == 0 ? CharacterClass.NONE : classifier.Get(prevCharCode);
                var entireLineFits = true;
                for (var i = prevBreakOffset; i < len; i++)
                {
                    var charStartOffset = i;
                    var charCode = CharCodeAt(lineText, i);
                    CharacterClass charCodeClass;
                    double charWidth;

                    if (Strings.IsHighSurrogate(charCode))
                    {
                        // A surrogate pair must always be considered as a single unit, so it is never to be broken
                        i++;
                        charCodeClass = CharacterClass.NONE;
                        charWidth = 2;
                    }
                    else
                    {
                        charCodeClass = classifier.Get(charCode);
                        charWidth = ComputeCharWidth(charCode, visibleColumn, tabSize, columnsForFullWidthChar, useTwoCellFullwidthCharacters);
                    }

                    if (charStartOffset > lastBreakingOffset && CanBreak(prevCharCode, prevCharCodeClass, charCode, charCodeClass, isKeepAll))
                    {
                        breakOffset = charStartOffset;
                        breakOffsetVisibleColumn = visibleColumn;
                    }

                    visibleColumn += charWidth;

                    // check if adding character at `i` will go over the breaking column
                    if (visibleColumn > breakingColumn)
                    {
                        // We need to break at least before character at `i`:
                        if (charStartOffset > lastBreakingOffset)
                        {
                            forcedBreakOffset = charStartOffset;
                            forcedBreakOffsetVisibleColumn = visibleColumn - charWidth;
                        }
                        else
                        {
                            // we need to advance at least by one character
                            forcedBreakOffset = i + 1;
                            forcedBreakOffsetVisibleColumn = visibleColumn;
                        }

                        if (visibleColumn - breakOffsetVisibleColumn > wrappedLineBreakColumn)
                        {
                            // Cannot break at `breakOffset` => reset it if it was set
                            breakOffset = 0;
                        }

                        entireLineFits = false;
                        break;
                    }

                    prevCharCode = charCode;
                    prevCharCodeClass = charCodeClass;
                }

                if (entireLineFits)
                {
                    // there is no more need to break => stop the outer loop!
                    if (breakingOffsets.Count > 0)
                    {
                        // Add last segment, no need to assign to `lastBreakingOffset` and `lastBreakingOffsetVisibleColumn`
                        breakingOffsets.Add(prevBreakingOffsets[^1]);
                        breakingOffsetsVisibleColumn.Add(prevBreakingOffsetsVisibleColumn[prevBreakingOffsets.Length - 1]);
                    }

                    break;
                }
            }

            if (breakOffset == 0)
            {
                // must search left
                var visibleColumn = prevBreakOffsetVisibleColumn;
                var charCode = CharCodeAt(lineText, prevBreakOffset);
                var charCodeClass = classifier.Get(charCode);
                var hitATabCharacter = false;
                for (var i = prevBreakOffset - 1; i >= lastBreakingOffset; i--)
                {
                    var charStartOffset = i + 1;
                    var prevCharCode = CharCodeAt(lineText, i);

                    if (prevCharCode == CharCodeTab)
                    {
                        // cannot determine the width of a tab when going backwards, so we must go forwards
                        hitATabCharacter = true;
                        break;
                    }

                    CharacterClass prevCharCodeClass;
                    double prevCharWidth;

                    if (Strings.IsLowSurrogate(prevCharCode))
                    {
                        // A surrogate pair must always be considered as a single unit, so it is never to be broken
                        i--;
                        prevCharCodeClass = CharacterClass.NONE;
                        prevCharWidth = 2;
                    }
                    else
                    {
                        prevCharCodeClass = classifier.Get(prevCharCode);
                        prevCharWidth = IsFullWidthCharacter(prevCharCode) ? FullWidthCharacterColumns(columnsForFullWidthChar, useTwoCellFullwidthCharacters) : 1;
                    }

                    if (visibleColumn <= breakingColumn)
                    {
                        if (forcedBreakOffset == 0)
                        {
                            forcedBreakOffset = charStartOffset;
                            forcedBreakOffsetVisibleColumn = visibleColumn;
                        }

                        if (visibleColumn <= breakingColumn - wrappedLineBreakColumn)
                        {
                            // went too far!
                            break;
                        }

                        if (CanBreak(prevCharCode, prevCharCodeClass, charCode, charCodeClass, isKeepAll))
                        {
                            breakOffset = charStartOffset;
                            breakOffsetVisibleColumn = visibleColumn;
                            break;
                        }
                    }

                    visibleColumn -= prevCharWidth;
                    charCode = prevCharCode;
                    charCodeClass = prevCharCodeClass;
                }

                if (breakOffset != 0)
                {
                    var remainingWidthOfNextLine = wrappedLineBreakColumn - (forcedBreakOffsetVisibleColumn - breakOffsetVisibleColumn);
                    if (remainingWidthOfNextLine <= tabSize)
                    {
                        var charCodeAtForcedBreakOffset = CharCodeAt(lineText, forcedBreakOffset);
                        double charWidth;
                        if (Strings.IsHighSurrogate(charCodeAtForcedBreakOffset))
                        {
                            // A surrogate pair must always be considered as a single unit, so it is never to be broken
                            charWidth = 2;
                        }
                        else
                        {
                            charWidth = ComputeCharWidth(charCodeAtForcedBreakOffset, forcedBreakOffsetVisibleColumn, tabSize, columnsForFullWidthChar, useTwoCellFullwidthCharacters);
                        }

                        if (remainingWidthOfNextLine - charWidth < 0)
                        {
                            // it is not worth it to break at breakOffset, it just introduces an extra needless line!
                            breakOffset = 0;
                        }
                    }
                }

                if (hitATabCharacter)
                {
                    // cannot determine the width of a tab when going backwards, so we must go forwards from the previous break
                    prevIndex--;
                    continue;
                }
            }

            if (breakOffset == 0)
            {
                // Could not find a good breaking point
                breakOffset = forcedBreakOffset;
                breakOffsetVisibleColumn = forcedBreakOffsetVisibleColumn;
            }

            if (breakOffset <= lastBreakingOffset)
            {
                // Make sure that we are advancing (at least one character)
                var charCode = CharCodeAt(lineText, lastBreakingOffset);
                if (Strings.IsHighSurrogate(charCode))
                {
                    // A surrogate pair must always be considered as a single unit, so it is never to be broken
                    breakOffset = lastBreakingOffset + 2;
                    breakOffsetVisibleColumn = lastBreakingOffsetVisibleColumn + 2;
                }
                else
                {
                    breakOffset = lastBreakingOffset + 1;
                    breakOffsetVisibleColumn = lastBreakingOffsetVisibleColumn + ComputeCharWidth(charCode, lastBreakingOffsetVisibleColumn, tabSize, columnsForFullWidthChar, useTwoCellFullwidthCharacters);
                }
            }

            lastBreakingOffset = breakOffset;
            breakingOffsets.Add(breakOffset);
            lastBreakingOffsetVisibleColumn = breakOffsetVisibleColumn;
            breakingOffsetsVisibleColumn.Add(breakOffsetVisibleColumn);
            breakingColumn = breakOffsetVisibleColumn + wrappedLineBreakColumn;

            while (prevIndex < 0 || (prevIndex < prevLen && prevBreakingOffsetsVisibleColumn[prevIndex] < breakOffsetVisibleColumn))
            {
                prevIndex++;
            }

            var bestDistance2 = Math.Abs(At(prevBreakingOffsetsVisibleColumn, prevIndex) - breakingColumn);
            while (prevIndex + 1 < prevLen)
            {
                var distance = Math.Abs(prevBreakingOffsetsVisibleColumn[prevIndex + 1] - breakingColumn);
                if (distance >= bestDistance2)
                {
                    break;
                }

                bestDistance2 = distance;
                prevIndex++;
            }
        }

        if (breakingOffsets.Count == 0)
        {
            return null;
        }

        previousBreakingData.BreakOffsets = breakingOffsets.ToArray();
        previousBreakingData.BreakOffsetsVisibleColumn = breakingOffsetsVisibleColumn.ToArray();
        previousBreakingData.WrappedTextIndentLength = wrappedTextIndentLength;
        return previousBreakingData;
    }

    private static ModelLineProjectionData? CreateLineBreaks(
        WrappingCharacterClassifier classifier,
        string lineTextWithoutInjections,
        IReadOnlyList<LineInjectedText>? injectedTexts,
        int tabSize,
        int firstLineBreakColumn,
        double columnsForFullWidthChar,
        FontInfo fontInfo,
        WrappingIndent wrappingIndent,
        WordBreak wordBreak,
        bool wrapOnEscapedLineFeeds,
        bool useTwoCellFullwidthCharacters)
    {
        var lineText = LineInjectedText.ApplyInjectedText(lineTextWithoutInjections, injectedTexts);
        var fixedWidthRanges = LineInjectedText.GetFixedWidthInjectedTextRanges(injectedTexts);

        ModelDecorationInjectedTextOptions[]? injectionOptions;
        int[]? injectionOffsets;
        if (injectedTexts is not null && injectedTexts.Count > 0)
        {
            injectionOptions = new ModelDecorationInjectedTextOptions[injectedTexts.Count];
            injectionOffsets = new int[injectedTexts.Count];
            for (var i = 0; i < injectedTexts.Count; i++)
            {
                injectionOptions[i] = injectedTexts[i].Options;
                injectionOffsets[i] = injectedTexts[i].Column - 1;
            }
        }
        else
        {
            injectionOptions = null;
            injectionOffsets = null;
        }

        if (firstLineBreakColumn == -1)
        {
            if (injectionOptions is null)
            {
                return null;
            }

            // creating a `LineBreakData` with an invalid `breakOffsetsVisibleColumn` is OK
            // because `breakOffsetsVisibleColumn` will never be used because it contains injected text
            return new ModelLineProjectionData(injectionOffsets, injectionOptions, [lineText.Length], [], 0);
        }

        var len = lineText.Length;
        if (len <= 1)
        {
            if (injectionOptions is null)
            {
                return null;
            }

            // creating a `LineBreakData` with an invalid `breakOffsetsVisibleColumn` is OK
            // because `breakOffsetsVisibleColumn` will never be used because it contains injected text
            return new ModelLineProjectionData(injectionOffsets, injectionOptions, [lineText.Length], [], 0);
        }

        var isKeepAll = wordBreak == WordBreak.KeepAll;
        var wrappedTextIndentLength = ComputeWrappedTextIndentLength(lineText, tabSize, firstLineBreakColumn, FullWidthCharacterColumns(columnsForFullWidthChar, useTwoCellFullwidthCharacters), wrappingIndent, fixedWidthRanges);

        // The wrapping decision is taken in pixels, because injected text can request an arbitrary
        // width via `widthInEm` which does not map to a whole number of columns.
        var typicalHalfwidthCharacterWidth = fontInfo.TypicalHalfwidthCharacterWidth;
        var wrappedLineBreakPixelWidth = (firstLineBreakColumn - wrappedTextIndentLength) * typicalHalfwidthCharacterWidth;

        var breakingOffsets = new List<int>();
        var breakingOffsetsVisibleColumn = new List<double>();
        var breakOffset = 0;
        double breakOffsetVisibleColumn = 0;
        double breakOffsetPixelWidth = 0;

        var breakingPixelWidth = firstLineBreakColumn * typicalHalfwidthCharacterWidth;
        var fixedWidthRangeIndex = 0;
        FixedWidthInjectedTextRange? firstFixedWidthRange = fixedWidthRanges.Count > 0 ? fixedWidthRanges[0] : null;
        var startsWithFixedWidth = firstFixedWidthRange is { StartOffset: 0 };

        int prevCharCode;
        CharacterClass prevCharCodeClass;
        double visibleColumn;
        double currentLinePixelWidth;
        int startOffset;
        if (startsWithFixedWidth)
        {
            var range = firstFixedWidthRange!.Value;
            prevCharCode = CharCodeNull;
            prevCharCodeClass = CharacterClass.NONE;
            visibleColumn = ComputeFixedWidthRangeColumnWidth(lineText, range, 0, tabSize, columnsForFullWidthChar, useTwoCellFullwidthCharacters);
            currentLinePixelWidth = range.WidthInEm * fontInfo.FontSize;
            startOffset = range.EndOffset;
            fixedWidthRangeIndex++;
        }
        else
        {
            prevCharCode = CharCodeAt(lineText, 0);
            visibleColumn = ComputeCharWidth(prevCharCode, 0, tabSize, columnsForFullWidthChar, useTwoCellFullwidthCharacters);
            currentLinePixelWidth = ComputeCharPixelWidth(prevCharCode, 0, tabSize, fontInfo, useTwoCellFullwidthCharacters);
            startOffset = 1;
            if (Strings.IsHighSurrogate(prevCharCode))
            {
                // A surrogate pair must always be considered as a single unit, so it is never to be broken
                visibleColumn += 1;
                currentLinePixelWidth += typicalHalfwidthCharacterWidth;
                prevCharCode = CharCodeAt(lineText, 1);
                startOffset++;
            }

            prevCharCodeClass = classifier.Get(prevCharCode);
        }

        for (var i = startOffset; i < len; i++)
        {
            FixedWidthInjectedTextRange? fixedWidthRange = fixedWidthRanges.Count > 0 && fixedWidthRangeIndex < fixedWidthRanges.Count ? fixedWidthRanges[fixedWidthRangeIndex] : null;
            var charStartOffset = i;
            var charCode = CharCodeAt(lineText, i);
            CharacterClass charCodeClass;
            double charWidth;
            double charPixelWidth;
            var wrapEscapedLineFeed = false;

            if (fixedWidthRange is { } range && range.StartOffset == i)
            {
                charCode = CharCodeNull;
                charCodeClass = CharacterClass.NONE;
                charWidth = ComputeFixedWidthRangeColumnWidth(lineText, range, visibleColumn, tabSize, columnsForFullWidthChar, useTwoCellFullwidthCharacters);
                charPixelWidth = range.WidthInEm * fontInfo.FontSize;
                i = range.EndOffset - 1;
                fixedWidthRangeIndex++;
            }
            else if (Strings.IsHighSurrogate(charCode))
            {
                // A surrogate pair must always be considered as a single unit, so it is never to be broken
                i++;
                charCodeClass = CharacterClass.NONE;
                charWidth = 2;
                charPixelWidth = 2 * typicalHalfwidthCharacterWidth;
            }
            else
            {
                charCodeClass = classifier.Get(charCode);
                charWidth = ComputeCharWidth(charCode, visibleColumn, tabSize, columnsForFullWidthChar, useTwoCellFullwidthCharacters);
                charPixelWidth = ComputeCharPixelWidth(charCode, visibleColumn, tabSize, fontInfo, useTwoCellFullwidthCharacters);
            }

            // literal \n shall trigger a softwrap
            if (wrapOnEscapedLineFeeds && IsEscapedLineBreakAtPosition(lineText, charStartOffset))
            {
                breakOffset = charStartOffset;
                breakOffsetVisibleColumn = visibleColumn;
                breakOffsetPixelWidth = currentLinePixelWidth;
                wrapEscapedLineFeed = true;
            }
            else if (CanBreak(prevCharCode, prevCharCodeClass, charCode, charCodeClass, isKeepAll))
            {
                breakOffset = charStartOffset;
                breakOffsetVisibleColumn = visibleColumn;
                breakOffsetPixelWidth = currentLinePixelWidth;
            }

            visibleColumn += charWidth;
            currentLinePixelWidth += charPixelWidth;

            // check if adding character at `i` will go over the breaking width
            if (currentLinePixelWidth > breakingPixelWidth || wrapEscapedLineFeed)
            {
                // We need to break at least before character at `i`:

                if (breakOffset == 0 || currentLinePixelWidth - breakOffsetPixelWidth > wrappedLineBreakPixelWidth)
                {
                    // Cannot break at `breakOffset`, must break at `i`
                    breakOffset = charStartOffset;
                    breakOffsetVisibleColumn = visibleColumn - charWidth;
                    breakOffsetPixelWidth = currentLinePixelWidth - charPixelWidth;
                }

                var currentLineStartOffset = breakingOffsets.Count > 0 ? breakingOffsets[^1] : 0;
                if (breakOffset > currentLineStartOffset)
                {
                    // Breaking at the start of the current output line would emit an empty line, which
                    // happens when an oversized leading width-only injection cannot fit before the first
                    // character. In that case keep the content on the current line and defer the break.
                    breakingOffsets.Add(breakOffset);
                    breakingOffsetsVisibleColumn.Add(breakOffsetVisibleColumn);
                    breakingPixelWidth = breakOffsetPixelWidth + wrappedLineBreakPixelWidth;
                    breakOffset = 0;
                }
            }

            prevCharCode = charCode;
            prevCharCodeClass = charCodeClass;
        }

        if (breakingOffsets.Count == 0 && (injectedTexts is null || injectedTexts.Count == 0))
        {
            return null;
        }

        // Add last segment
        breakingOffsets.Add(len);
        breakingOffsetsVisibleColumn.Add(visibleColumn);

        return new ModelLineProjectionData(injectionOffsets, injectionOptions, breakingOffsets.ToArray(), breakingOffsetsVisibleColumn.ToArray(), wrappedTextIndentLength);
    }

    private static bool IsFullWidthCharacter(int charCode) => charCode >= 0 && Strings.IsFullWidthCharacter(charCode);

    private static double ComputeCharWidth(int charCode, double visibleColumn, int tabSize, double columnsForFullWidthChar, bool useTwoCellFullwidthCharacters)
    {
        if (charCode == CharCodeTab)
        {
            return tabSize - (visibleColumn % tabSize);
        }

        if (IsFullWidthCharacter(charCode))
        {
            return FullWidthCharacterColumns(columnsForFullWidthChar, useTwoCellFullwidthCharacters);
        }

        if (charCode >= 0 && charCode < 32)
        {
            // when using `editor.renderControlCharacters`, the substitutions are often wide
            return columnsForFullWidthChar;
        }

        return 1;
    }

    /// <summary>
    /// The number of columns a full-width character occupies: exactly two cells when full-width characters are forced
    /// into two monospace cells, otherwise the ratio measured from the font.
    /// </summary>
    private static double FullWidthCharacterColumns(double columnsForFullWidthChar, bool useTwoCellFullwidthCharacters) =>
        useTwoCellFullwidthCharacters ? 2 : columnsForFullWidthChar;

    private static double TabCharacterWidth(double visibleColumn, int tabSize) => tabSize - (visibleColumn % tabSize);

    /// <summary>
    /// The width in pixels a character occupies. Used for the wrapping decision, which must reason in real widths
    /// because injected text can request an arbitrary width via <c>widthInEm</c>.
    /// </summary>
    private static double ComputeCharPixelWidth(int charCode, double visibleColumn, int tabSize, FontInfo fontInfo, bool useTwoCellFullwidthCharacters)
    {
        if (charCode == CharCodeTab)
        {
            return TabCharacterWidth(visibleColumn, tabSize) * fontInfo.TypicalHalfwidthCharacterWidth;
        }

        if (IsFullWidthCharacter(charCode))
        {
            return useTwoCellFullwidthCharacters ? 2 * fontInfo.TypicalHalfwidthCharacterWidth : fontInfo.TypicalFullwidthCharacterWidth;
        }

        if (charCode >= 0 && charCode < 32)
        {
            // when using `editor.renderControlCharacters`, the substitutions are often wide
            return fontInfo.TypicalFullwidthCharacterWidth;
        }

        return fontInfo.TypicalHalfwidthCharacterWidth;
    }

    /// <summary>
    /// The number of columns the characters of a fixed width injected text range occupy, which is 0 for a width-only
    /// injection. <c>widthInEm</c> is deliberately ignored here: <c>visibleColumn</c> only drives tab expansion, which
    /// must stay in sync with the line rendering, and the rendering does not know about <c>widthInEm</c>.
    /// </summary>
    private static double ComputeFixedWidthRangeColumnWidth(string lineText, FixedWidthInjectedTextRange range, double visibleColumn, int tabSize, double columnsForFullWidthChar, bool useTwoCellFullwidthCharacters)
    {
        double width = 0;
        for (var i = range.StartOffset; i < range.EndOffset; i++)
        {
            width += ComputeCharWidth(CharCodeAt(lineText, i), visibleColumn + width, tabSize, columnsForFullWidthChar, useTwoCellFullwidthCharacters);
        }

        return width;
    }

    /// <summary>
    /// Checks if the current position in the text should trigger a soft wrap due to escaped line feeds. This handles
    /// the wrapOnEscapedLineFeeds feature which allows \n sequences in strings to trigger wrapping.
    /// </summary>
    private static bool IsEscapedLineBreakAtPosition(string lineText, int i)
    {
        if (i >= 2 && lineText[i - 1] == 'n')
        {
            // Check if there's an odd number of backslashes
            var escapeCount = 0;
            for (var j = i - 2; j >= 0; j--)
            {
                if (lineText[j] == '\\')
                {
                    escapeCount++;
                }
                else
                {
                    return escapeCount % 2 == 1;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Kinsoku Shori: Don't break after a leading character, like an open bracket.
    /// Kinsoku Shori: Don't break before a trailing character, like a period.
    /// </summary>
    private static bool CanBreak(int prevCharCode, CharacterClass prevCharCodeClass, int charCode, CharacterClass charCodeClass, bool isKeepAll)
    {
        _ = prevCharCode;
        return charCode != CharCodeSpace
            && ((prevCharCodeClass == CharacterClass.BREAK_AFTER && charCodeClass != CharacterClass.BREAK_AFTER) // break at the end of multiple BREAK_AFTER
                || (prevCharCodeClass != CharacterClass.BREAK_BEFORE && charCodeClass == CharacterClass.BREAK_BEFORE) // break at the start of multiple BREAK_BEFORE
                || (!isKeepAll && prevCharCodeClass == CharacterClass.BREAK_IDEOGRAPHIC && charCodeClass != CharacterClass.BREAK_AFTER)
                || (!isKeepAll && charCodeClass == CharacterClass.BREAK_IDEOGRAPHIC && prevCharCodeClass != CharacterClass.BREAK_BEFORE));
    }

    private static int ComputeWrappedTextIndentLength(string lineText, int tabSize, int firstLineBreakColumn, double columnsForFullWidthChar, WrappingIndent wrappingIndent, IReadOnlyList<FixedWidthInjectedTextRange>? fixedWidthRanges = null)
    {
        var wrappedTextIndentLength = 0;
        if (wrappingIndent != WrappingIndent.None)
        {
            var firstNonWhitespaceIndex = Strings.FirstNonWhitespaceIndex(lineText);
            if (firstNonWhitespaceIndex != -1)
            {
                // Track existing indent

                for (var i = 0; i < firstNonWhitespaceIndex; i++)
                {
                    if (fixedWidthRanges is { Count: > 0 } && fixedWidthRanges[0].StartOffset == i)
                    {
                        break;
                    }

                    var charWidth = lineText[i] == '\t' ? (int)TabCharacterWidth(wrappedTextIndentLength, tabSize) : 1;
                    wrappedTextIndentLength += charWidth;
                }

                // Increase indent of continuation lines, if desired
                var numberOfAdditionalTabs = wrappingIndent == WrappingIndent.DeepIndent ? 2 : wrappingIndent == WrappingIndent.Indent ? 1 : 0;
                for (var i = 0; i < numberOfAdditionalTabs; i++)
                {
                    var charWidth = (int)TabCharacterWidth(wrappedTextIndentLength, tabSize);
                    wrappedTextIndentLength += charWidth;
                }

                // Force sticking to beginning of line if no character would fit except for the indentation
                if (wrappedTextIndentLength + columnsForFullWidthChar > firstLineBreakColumn)
                {
                    wrappedTextIndentLength = 0;
                }
            }
        }

        return wrappedTextIndentLength;
    }
}
