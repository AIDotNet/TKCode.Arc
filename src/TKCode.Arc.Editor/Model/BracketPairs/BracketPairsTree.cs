// Ported from vscode (src/vs/editor/common/model/bracketPairsTextModelPart/bracketPairsTree/bracketPairsTree.ts,
// commit b57d5c9e), MIT license.
//
// Differences: the model is read through ITokenizerSource (TextModelTokenizerSource); the change event is an Emitter;
// a Map of levels per bracket type is a Dictionary; collectBrackets' labelled loop is a while loop with a flag.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Tokens;
using static TKCode.Arc.Editor.Model.BracketPairs.LengthHelpers;
using Token = TKCode.Arc.Editor.Model.BracketPairs.Token;

namespace TKCode.Arc.Editor.Model.BracketPairs;

/// <summary>Reads a text model for the bracket tokenizer.</summary>
internal sealed class TextModelTokenizerSource(TextModel textModel) : ITokenizerSource
{
    public string GetValue() => textModel.GetValue();

    public int GetLineCount() => textModel.GetLineCount();

    public int GetLineLength(int lineNumber) => textModel.GetLineLength(lineNumber);

    public LineTokens GetLineTokens(int lineNumber) => textModel.Tokenization.GetLineTokens(lineNumber);
}

/// <summary>The bracket pairs of a model as an incrementally updated AST.</summary>
internal sealed class BracketPairsTree : IDisposable
{
    private readonly TextModel _textModel;
    private readonly TextModelTokenizerSource _source;
    private readonly Emitter<bool> _didChangeEmitter = new();
    private readonly DenseKeyProvider<string> _denseKeyProvider;
    private readonly LanguageAgnosticBracketTokens _brackets;

    /*
        There are two trees:
        * The initial tree that has no token information and is used for performant initial bracket colorization.
        * The tree that used token information to detect bracket pairs.

        To prevent flickering, we only switch from the initial tree to tree with token information
        when tokenization completes.
        Since the text can be edited while background tokenization is in progress, we need to update both trees.
    */
    private AstNode? _initialAstWithoutTokens;
    private AstNode? _astWithTokens;
    private IReadOnlyList<TextEditInfo> _queuedTextEditsForInitialAstWithoutTokens = [];
    private IReadOnlyList<TextEditInfo> _queuedTextEdits = [];

    public BracketPairsTree(TextModel textModel, Func<string, ResolvedLanguageConfiguration> getLanguageConfiguration)
    {
        _textModel = textModel;
        _source = new TextModelTokenizerSource(textModel);
        _denseKeyProvider = new DenseKeyProvider<string>();
        _brackets = new LanguageAgnosticBracketTokens(_denseKeyProvider, getLanguageConfiguration);

        if (!textModel.Tokenization.HasTokens)
        {
            var brackets = _brackets.GetSingleLanguageBracketTokens(_textModel.GetLanguageId());
            var tokenizer = new FastTokenizer(_textModel.GetValue(), brackets);
            _initialAstWithoutTokens = Parser.ParseDocument(tokenizer, [], null, true);
            _astWithTokens = _initialAstWithoutTokens;
        }
        else if (textModel.Tokenization.BackgroundTokenizationState == BackgroundTokenizationState.Completed)
        {
            // Skip the initial ast, as there is no flickering.
            // Directly create the tree with token information.
            _initialAstWithoutTokens = null;
            _astWithTokens = ParseDocumentFromTextBuffer([], null, false);
        }
        else
        {
            // We missed some token changes already, so we cannot use the fast tokenizer + delta increments
            _initialAstWithoutTokens = ParseDocumentFromTextBuffer([], null, true);
            _astWithTokens = _initialAstWithoutTokens;
        }
    }

    public event Action DidChange
    {
        add => _didChangeEmitter.Add(_ => value(), value);
        remove => _didChangeEmitter.Remove(value);
    }

    public bool DidLanguageChange(string languageId) => _brackets.DidLanguageChange(languageId);

    public void Dispose() => _didChangeEmitter.Clear();

    // ---- TextModel events

    public void HandleDidChangeBackgroundTokenizationState()
    {
        if (_textModel.Tokenization.BackgroundTokenizationState == BackgroundTokenizationState.Completed)
        {
            var wasUndefined = _initialAstWithoutTokens is null;
            // Clear the initial tree as we can use the tree with token information now.
            _initialAstWithoutTokens = null;
            if (!wasUndefined)
            {
                _didChangeEmitter.Fire(true);
            }
        }
    }

    public void HandleDidChangeTokens(ModelTokensChangedEvent e)
    {
        var edits = e.Ranges
            .Select(r => new TextEditInfo(
                ToLength(r.FromLineNumber - 1, 0),
                ToLength(r.ToLineNumber, 0),
                ToLength(r.ToLineNumber - r.FromLineNumber + 1, 0)))
            .ToList();

        HandleEdits(edits, true);

        if (_initialAstWithoutTokens is null)
        {
            _didChangeEmitter.Fire(true);
        }
    }

    public void HandleContentChanged(ModelContentChangedEvent change)
    {
        var edits = TextEditInfo.FromModelContentChanges(change.Changes);
        HandleEdits(edits, false);
    }

    private void HandleEdits(IReadOnlyList<TextEditInfo> edits, bool tokenChange)
    {
        // Lazily queue the edits and only apply them when the tree is accessed.
        _queuedTextEdits = TextEditInfoCombiner.CombineTextEditInfos(_queuedTextEdits, edits);
        if (_initialAstWithoutTokens is not null && !tokenChange)
        {
            _queuedTextEditsForInitialAstWithoutTokens = TextEditInfoCombiner.CombineTextEditInfos(_queuedTextEditsForInitialAstWithoutTokens, edits);
        }
    }

    // ----

    private void FlushQueue()
    {
        if (_queuedTextEdits.Count > 0)
        {
            _astWithTokens = ParseDocumentFromTextBuffer(_queuedTextEdits, _astWithTokens, false);
            _queuedTextEdits = [];
        }

        if (_queuedTextEditsForInitialAstWithoutTokens.Count > 0)
        {
            if (_initialAstWithoutTokens is not null)
            {
                _initialAstWithoutTokens = ParseDocumentFromTextBuffer(_queuedTextEditsForInitialAstWithoutTokens, _initialAstWithoutTokens, false);
            }

            _queuedTextEditsForInitialAstWithoutTokens = [];
        }
    }

    private AstNode ParseDocumentFromTextBuffer(IReadOnlyList<TextEditInfo> edits, AstNode? previousAst, bool immutable)
    {
        // Is much faster if `isPure = false`.
        var tokenizer = new TextBufferTokenizer(_source, _brackets);
        return Parser.ParseDocument(tokenizer, edits, previousAst, immutable);
    }

    public CallbackIterable<BracketInfo> GetBracketsInRange(TextRange range, bool onlyColorizedBrackets)
    {
        FlushQueue();

        var startOffset = ToLength(range.StartLineNumber - 1, range.StartColumn - 1);
        var endOffset = ToLength(range.EndLineNumber - 1, range.EndColumn - 1);
        return new CallbackIterable<BracketInfo>(cb =>
        {
            var node = _initialAstWithoutTokens ?? _astWithTokens!;
            CollectBrackets(node, Length.Zero, node.Length, startOffset, endOffset, cb, 0, 0, new Dictionary<string, int>(StringComparer.Ordinal), onlyColorizedBrackets);
        });
    }

    public CallbackIterable<BracketPairWithMinIndentationInfo> GetBracketPairsInRange(TextRange range, bool includeMinIndentation)
    {
        FlushQueue();

        var startLength = PositionToLength(new TextPosition(range.StartLineNumber, range.StartColumn));
        var endLength = PositionToLength(new TextPosition(range.EndLineNumber, range.EndColumn));

        return new CallbackIterable<BracketPairWithMinIndentationInfo>(cb =>
        {
            var node = _initialAstWithoutTokens ?? _astWithTokens!;
            var context = new CollectBracketPairsContext(cb, includeMinIndentation, _textModel);
            CollectBracketPairs(node, Length.Zero, node.Length, startLength, endLength, context, 0, new Dictionary<string, int>(StringComparer.Ordinal));
        });
    }

    public bool HasUnmatchedClosingBracketAfter(TextPosition position, OpeningBracketKind openingBracket)
    {
        // Defer semantic bracket checks until tokenization is accurate for the entire model.
        if (!_textModel.Tokenization.HasAccurateTokensForLine(_textModel.GetLineCount()))
        {
            return false;
        }

        FlushQueue();
        var openingBracketToken = _brackets.GetToken(openingBracket.BracketText, openingBracket.LanguageId);
        if (openingBracketToken is null || openingBracketToken.Kind != TokenKind.OpeningBracket)
        {
            return false;
        }

        return HasUnmatchedClosingBracketAfterCore(_astWithTokens!, PositionToLength(position), openingBracketToken);
    }

    public FoundBracket? GetFirstBracketAfter(TextPosition position)
    {
        FlushQueue();

        var node = _initialAstWithoutTokens ?? _astWithTokens!;
        return GetFirstBracketAfterCore(node, Length.Zero, node.Length, PositionToLength(position));
    }

    public FoundBracket? GetFirstBracketBefore(TextPosition position)
    {
        FlushQueue();

        var node = _initialAstWithoutTokens ?? _astWithTokens!;
        return GetFirstBracketBeforeCore(node, Length.Zero, node.Length, PositionToLength(position));
    }

    private static bool HasUnmatchedClosingBracketAfterCore(AstNode root, Length positionLength, Token openingBracketToken)
    {
        var openingBracketIds = openingBracketToken.BracketIds;
        if (!root.MissingOpeningBracketIds.Intersects(openingBracketIds))
        {
            return false;
        }

        var nodesToVisit = new Stack<(AstNode Node, Length Offset)>();
        nodesToVisit.Push((root, Length.Zero));
        while (nodesToVisit.Count > 0)
        {
            var (node, offset) = nodesToVisit.Pop();
            // UnexpectedClosingBracket nodes represent globally unmatched closing brackets.
            if (node.Kind == AstNodeKind.UnexpectedClosingBracket)
            {
                // Ignore unmatched closers before the cursor because the new opener cannot pair with them.
                if (LengthGreaterThanEqual(offset, positionLength))
                {
                    return true;
                }

                continue;
            }

            var childOffset = offset;
            foreach (var child in node.Children)
            {
                // child.length is the span of source text represented by that AST child—not its number of descendants
                var childEndOffset = LengthAdd(childOffset, child.Length);
                var childEndsAfterPosition = LengthGreaterThanEqual(childEndOffset, positionLength);
                var childCanContainMatchingOrphan = child.MissingOpeningBracketIds.Intersects(openingBracketIds);
                // Visit only children that are after the cursor and may contain the matching orphan
                if (childEndsAfterPosition && childCanContainMatchingOrphan)
                {
                    nodesToVisit.Push((child, childOffset));
                }

                childOffset = childEndOffset;
            }
        }

        return false;
    }

    private static FoundBracket? GetFirstBracketBeforeCore(AstNode node, Length nodeOffsetStart, Length nodeOffsetEnd, Length position)
    {
        if (node.Kind is AstNodeKind.List or AstNodeKind.Pair)
        {
            var children = node.Children;
            var lengths = new List<(Length Start, Length End)>();
            foreach (var child in children)
            {
                nodeOffsetEnd = LengthAdd(nodeOffsetStart, child.Length);
                lengths.Add((nodeOffsetStart, nodeOffsetEnd));
                nodeOffsetStart = nodeOffsetEnd;
            }

            for (var i = lengths.Count - 1; i >= 0; i--)
            {
                var (start, end) = lengths[i];
                if (LengthLessThan(start, position))
                {
                    var result = GetFirstBracketBeforeCore(children[i], start, end, position);
                    if (result is not null)
                    {
                        return result;
                    }
                }
            }

            return null;
        }

        if (node is BracketAstNode bracket)
        {
            return new FoundBracket(LengthsToRange(nodeOffsetStart, nodeOffsetEnd), bracket.BracketInfo);
        }

        return null;
    }

    private static FoundBracket? GetFirstBracketAfterCore(AstNode node, Length nodeOffsetStart, Length nodeOffsetEnd, Length position)
    {
        if (node.Kind is AstNodeKind.List or AstNodeKind.Pair)
        {
            foreach (var child in node.Children)
            {
                nodeOffsetEnd = LengthAdd(nodeOffsetStart, child.Length);
                if (LengthLessThan(position, nodeOffsetEnd))
                {
                    var result = GetFirstBracketAfterCore(child, nodeOffsetStart, nodeOffsetEnd, position);
                    if (result is not null)
                    {
                        return result;
                    }
                }

                nodeOffsetStart = nodeOffsetEnd;
            }

            return null;
        }

        if (node is BracketAstNode bracket)
        {
            return new FoundBracket(LengthsToRange(nodeOffsetStart, nodeOffsetEnd), bracket.BracketInfo);
        }

        return null;
    }

    private static bool CollectBrackets(
        AstNode node,
        Length nodeOffsetStart,
        Length nodeOffsetEnd,
        Length startOffset,
        Length endOffset,
        Func<BracketInfo, bool> push,
        int level,
        int nestingLevelOfEqualBracketType,
        Dictionary<string, int> levelPerBracketType,
        bool onlyColorizedBrackets,
        bool parentPairIsIncomplete = false)
    {
        if (level > 200)
        {
            return true;
        }

        while (true)
        {
            var descended = false;
            switch (node)
            {
                case ListAstNode:
                {
                    var childCount = node.ChildrenLength;
                    for (var i = 0; i < childCount; i++)
                    {
                        var child = node.GetChild(i);
                        if (child is null)
                        {
                            continue;
                        }

                        nodeOffsetEnd = LengthAdd(nodeOffsetStart, child.Length);
                        if (LengthLessThanEqual(nodeOffsetStart, endOffset) && LengthGreaterThanEqual(nodeOffsetEnd, startOffset))
                        {
                            var childEndsAfterEnd = LengthGreaterThanEqual(nodeOffsetEnd, endOffset);
                            if (childEndsAfterEnd)
                            {
                                // No child after this child in the requested window, don't recurse
                                node = child;
                                descended = true;
                                break;
                            }

                            var shouldContinue = CollectBrackets(child, nodeOffsetStart, nodeOffsetEnd, startOffset, endOffset, push, level, 0, levelPerBracketType, onlyColorizedBrackets);
                            if (!shouldContinue)
                            {
                                return false;
                            }
                        }

                        nodeOffsetStart = nodeOffsetEnd;
                    }

                    if (descended)
                    {
                        continue;
                    }

                    return true;
                }

                case PairAstNode pair:
                {
                    var colorize = !onlyColorizedBrackets
                        || pair.ClosingBracket is null
                        || ((ClosingBracketKind)pair.ClosingBracket.BracketInfo).ClosesColorized((OpeningBracketKind)pair.OpeningBracket.BracketInfo);

                    var existing = levelPerBracketType.GetValueOrDefault(pair.OpeningBracket.Text);
                    var levelPerBracket = existing;
                    if (colorize)
                    {
                        existing++;
                        levelPerBracketType[pair.OpeningBracket.Text] = existing;
                    }

                    var childCount = pair.ChildrenLength;
                    for (var i = 0; i < childCount; i++)
                    {
                        var child = pair.GetChild(i);
                        if (child is null)
                        {
                            continue;
                        }

                        nodeOffsetEnd = LengthAdd(nodeOffsetStart, child.Length);
                        if (LengthLessThanEqual(nodeOffsetStart, endOffset) && LengthGreaterThanEqual(nodeOffsetEnd, startOffset))
                        {
                            var childEndsAfterEnd = LengthGreaterThanEqual(nodeOffsetEnd, endOffset);
                            if (childEndsAfterEnd && child.Kind != AstNodeKind.Bracket)
                            {
                                // No child after this child in the requested window, don't recurse
                                // Don't do this for brackets because of unclosed/unopened brackets
                                node = child;
                                if (colorize)
                                {
                                    level++;
                                    nestingLevelOfEqualBracketType = levelPerBracket + 1;
                                }
                                else
                                {
                                    nestingLevelOfEqualBracketType = levelPerBracket;
                                }

                                descended = true;
                                break;
                            }

                            if (colorize || child.Kind != AstNodeKind.Bracket || pair.ClosingBracket is null)
                            {
                                var shouldContinue = CollectBrackets(
                                    child,
                                    nodeOffsetStart,
                                    nodeOffsetEnd,
                                    startOffset,
                                    endOffset,
                                    push,
                                    colorize ? level + 1 : level,
                                    colorize ? levelPerBracket + 1 : levelPerBracket,
                                    levelPerBracketType,
                                    onlyColorizedBrackets,
                                    pair.ClosingBracket is null);
                                if (!shouldContinue)
                                {
                                    return false;
                                }
                            }
                        }

                        nodeOffsetStart = nodeOffsetEnd;
                    }

                    if (descended)
                    {
                        continue;
                    }

                    levelPerBracketType[pair.OpeningBracket.Text] = levelPerBracket;

                    return true;
                }

                case InvalidBracketAstNode:
                {
                    var range = LengthsToRange(nodeOffsetStart, nodeOffsetEnd);
                    return push(new BracketInfo(range, level - 1, 0, true));
                }

                case BracketAstNode:
                {
                    var range = LengthsToRange(nodeOffsetStart, nodeOffsetEnd);
                    return push(new BracketInfo(range, level - 1, nestingLevelOfEqualBracketType - 1, parentPairIsIncomplete));
                }

                default:
                    // Text
                    return true;
            }
        }
    }

    private sealed class CollectBracketPairsContext(Func<BracketPairWithMinIndentationInfo, bool> push, bool includeMinIndentation, TextModel textModel)
    {
        public Func<BracketPairWithMinIndentationInfo, bool> Push { get; } = push;

        public bool IncludeMinIndentation { get; } = includeMinIndentation;

        public TextModel TextModel { get; } = textModel;
    }

    private static bool CollectBracketPairs(
        AstNode node,
        Length nodeOffsetStart,
        Length nodeOffsetEnd,
        Length startOffset,
        Length endOffset,
        CollectBracketPairsContext context,
        int level,
        Dictionary<string, int> levelPerBracketType)
    {
        if (level > 200)
        {
            return true;
        }

        var shouldContinue = true;

        if (node is PairAstNode pair)
        {
            var existing = levelPerBracketType.GetValueOrDefault(pair.OpeningBracket.Text);
            var levelPerBracket = existing;
            existing++;
            levelPerBracketType[pair.OpeningBracket.Text] = existing;

            var openingBracketEnd = LengthAdd(nodeOffsetStart, pair.OpeningBracket.Length);
            var minIndentation = -1;
            if (context.IncludeMinIndentation)
            {
                minIndentation = pair.ComputeMinIndentation(nodeOffsetStart, context.TextModel);
            }

            shouldContinue = context.Push(new BracketPairWithMinIndentationInfo(
                LengthsToRange(nodeOffsetStart, nodeOffsetEnd),
                LengthsToRange(nodeOffsetStart, openingBracketEnd),
                pair.ClosingBracket is not null
                    ? LengthsToRange(LengthAdd(openingBracketEnd, pair.Child?.Length ?? Length.Zero), nodeOffsetEnd)
                    : null,
                level,
                levelPerBracket,
                pair,
                minIndentation));

            nodeOffsetStart = openingBracketEnd;
            if (shouldContinue && pair.Child is { } child)
            {
                nodeOffsetEnd = LengthAdd(nodeOffsetStart, child.Length);
                if (LengthLessThanEqual(nodeOffsetStart, endOffset) && LengthGreaterThanEqual(nodeOffsetEnd, startOffset))
                {
                    shouldContinue = CollectBracketPairs(child, nodeOffsetStart, nodeOffsetEnd, startOffset, endOffset, context, level + 1, levelPerBracketType);
                    if (!shouldContinue)
                    {
                        return false;
                    }
                }
            }

            levelPerBracketType[pair.OpeningBracket.Text] = levelPerBracket;
        }
        else
        {
            var curOffset = nodeOffsetStart;
            foreach (var child in node.Children)
            {
                var childOffset = curOffset;
                curOffset = LengthAdd(curOffset, child.Length);

                if (LengthLessThanEqual(childOffset, endOffset) && LengthLessThanEqual(startOffset, curOffset))
                {
                    shouldContinue = CollectBracketPairs(child, childOffset, curOffset, startOffset, endOffset, context, level, levelPerBracketType);
                    if (!shouldContinue)
                    {
                        return false;
                    }
                }
            }
        }

        return shouldContinue;
    }
}
