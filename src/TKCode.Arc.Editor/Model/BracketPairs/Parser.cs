// Ported from vscode (src/vs/editor/common/model/bracketPairsTextModelPart/bracketPairsTree/parser.ts, commit b57d5c9e),
// MIT license.

using static TKCode.Arc.Editor.Model.BracketPairs.LengthHelpers;

namespace TKCode.Arc.Editor.Model.BracketPairs;

/// <summary>Parses a document into a bracket pair AST, reusing the nodes of an older AST where the text did not change.</summary>
internal sealed class Parser
{
    private readonly ITokenizer _tokenizer;
    private readonly NodeReader? _oldNodeReader;
    private readonly BeforeEditPositionMapper _positionMapper;
    private readonly bool _createImmutableLists;

    /// <summary>Non incrementally built ASTs are immutable.</summary>
    public static AstNode ParseDocument(ITokenizer tokenizer, IReadOnlyList<TextEditInfo> edits, AstNode? oldNode, bool createImmutableLists)
    {
        var parser = new Parser(tokenizer, edits, oldNode, createImmutableLists);
        return parser.ParseDocument();
    }

    public Parser(ITokenizer tokenizer, IReadOnlyList<TextEditInfo> edits, AstNode? oldNode, bool createImmutableLists)
    {
        if (oldNode is not null && createImmutableLists)
        {
            throw new NotSupportedException("Not supported");
        }

        _tokenizer = tokenizer;
        _createImmutableLists = createImmutableLists;
        _oldNodeReader = oldNode is not null ? new NodeReader(oldNode) : null;
        _positionMapper = new BeforeEditPositionMapper(edits);
    }

    /// <summary>Reports how many nodes were constructed in the last parse operation.</summary>
    public int NodesConstructed { get; private set; }

    /// <summary>Reports how many nodes were reused in the last parse operation.</summary>
    public int NodesReused { get; private set; }

    public AstNode ParseDocument()
    {
        NodesConstructed = 0;
        NodesReused = 0;

        return ParseList(SmallImmutableSet<int>.GetEmpty(), 0) ?? ListAstNode.GetEmpty();
    }

    private AstNode? ParseList(SmallImmutableSet<int> openedBracketIds, int level)
    {
        var items = new List<AstNode>();

        while (true)
        {
            var child = TryReadChildFromCache(openedBracketIds);

            if (child is null)
            {
                var token = _tokenizer.Peek();
                if (token is null || (token.Kind == TokenKind.ClosingBracket && token.BracketIds.Intersects(openedBracketIds)))
                {
                    break;
                }

                child = ParseChild(openedBracketIds, level + 1);
            }

            if (child is ListAstNode { ChildrenLength: 0 })
            {
                continue;
            }

            items.Add(child);
        }

        // When there is no oldNodeReader, all items are created from scratch and must have the same height.
        return _oldNodeReader is not null ? Concat23Trees.Concat(items) : Concat23Trees.ConcatOfSameHeight(items, _createImmutableLists);
    }

    private AstNode? TryReadChildFromCache(SmallImmutableSet<int> openedBracketIds)
    {
        if (_oldNodeReader is not null)
        {
            var maxCacheableLength = _positionMapper.GetDistanceToNextChange(_tokenizer.Offset);
            if (maxCacheableLength is null || !LengthIsZero(maxCacheableLength.Value))
            {
                var cachedNode = _oldNodeReader.ReadLongestNodeAt(_positionMapper.GetOffsetBeforeChange(_tokenizer.Offset), curNode =>
                {
                    // The edit could extend the ending token, thus we cannot re-use nodes that touch the edit.
                    // If there is no edit anymore, we can re-use the node in any case.
                    if (maxCacheableLength is { } max && !LengthLessThan(curNode.Length, max))
                    {
                        // Either the node contains edited text or touches edited text.
                        // In the latter case, brackets might have been extended (`end` -> `ending`), so even touching nodes cannot be reused.
                        return false;
                    }

                    return curNode.CanBeReused(openedBracketIds);
                });

                if (cachedNode is not null)
                {
                    NodesReused++;
                    _tokenizer.Skip(cachedNode.Length);
                    return cachedNode;
                }
            }
        }

        return null;
    }

    private AstNode ParseChild(SmallImmutableSet<int> openedBracketIds, int level)
    {
        NodesConstructed++;

        var token = _tokenizer.Read()!;

        switch (token.Kind)
        {
            case TokenKind.ClosingBracket:
                return new InvalidBracketAstNode(token.BracketIds, token.Length);

            case TokenKind.Text:
                return (TextAstNode)token.AstNode!;

            case TokenKind.OpeningBracket:
            {
                if (level > 300)
                {
                    // To prevent stack overflows
                    return new TextAstNode(token.Length);
                }

                var set = openedBracketIds.Merge(token.BracketIds);
                var child = ParseList(set, level + 1);

                var nextToken = _tokenizer.Peek();
                if (nextToken is not null
                    && nextToken.Kind == TokenKind.ClosingBracket
                    && (nextToken.BracketId == token.BracketId || nextToken.BracketIds.Intersects(token.BracketIds)))
                {
                    _tokenizer.Read();
                    return PairAstNode.Create((BracketAstNode)token.AstNode!, child, (BracketAstNode)nextToken.AstNode!);
                }

                return PairAstNode.Create((BracketAstNode)token.AstNode!, child, null);
            }

            default:
                throw new InvalidOperationException("unexpected");
        }
    }
}
