// Ported from vscode (src/vs/editor/common/model/bracketPairsTextModelPart/bracketPairsTree/tokenizer.ts, commit
// b57d5c9e), MIT license.
//
// Differences: the bracket regular expressions are .NET Regex (ECMAScript, IgnoreCase), matched on the token's slice of
// the line with Regex.Match(line, start, length) instead of on a substring; Token is the tokenizer's token (code that
// also imports TKCode.Arc.Editor.Languages, which has a Token of its own, aliases it); ITokenizerSource.tokenization
// is the GetLineTokens method.

using TKCode.Arc.Editor.Tokens;
using static TKCode.Arc.Editor.Model.BracketPairs.LengthHelpers;

namespace TKCode.Arc.Editor.Model.BracketPairs;

internal interface ITokenizer
{
    Length Offset { get; }

    Length Length { get; }

    Token? Read();

    Token? Peek();

    void Skip(Length length);

    string GetText();
}

internal enum TokenKind
{
    Text = 0,
    OpeningBracket = 1,
    ClosingBracket = 2,
}

/// <summary>A token of the bracket pair tokenizer: text or a bracket.</summary>
internal sealed class Token(Length length, TokenKind kind, int bracketId, SmallImmutableSet<int> bracketIds, AstNode? astNode)
{
    public Length Length { get; } = length;

    public TokenKind Kind { get; } = kind;

    /// <summary>
    /// If this token is an opening bracket, this is the id of the opening bracket. If this token is a closing bracket,
    /// this is the id of the first opening bracket that is closed by this bracket. Otherwise, it is -1.
    /// </summary>
    public int BracketId { get; } = bracketId;

    /// <summary>
    /// If this token is an opening bracket, this just contains <c>bracketId</c>. If this token is a closing bracket,
    /// this lists all opening bracket ids, that it closes. Otherwise, it is empty.
    /// </summary>
    public SmallImmutableSet<int> BracketIds { get; } = bracketIds;

    /// <summary>A <see cref="BracketAstNode"/> or a <see cref="TextAstNode"/>.</summary>
    public AstNode? AstNode { get; } = astNode;
}

/// <summary>What the tokenizer reads: a model's text and tokens.</summary>
internal interface ITokenizerSource
{
    string GetValue();

    int GetLineCount();

    int GetLineLength(int lineNumber);

    LineTokens GetLineTokens(int lineNumber);
}

internal sealed class TextBufferTokenizer : ITokenizer
{
    private readonly ITokenizerSource _textModel;
    private readonly int _textBufferLineCount;
    private readonly int _textBufferLastLineLength;
    private readonly NonPeekableTextBufferTokenizer _reader;
    private bool _didPeek;
    private Token? _peeked;

    public TextBufferTokenizer(ITokenizerSource textModel, LanguageAgnosticBracketTokens bracketTokens)
    {
        _textModel = textModel;
        _reader = new NonPeekableTextBufferTokenizer(textModel, bracketTokens);
        Offset = Length.Zero;
        _textBufferLineCount = textModel.GetLineCount();
        _textBufferLastLineLength = textModel.GetLineLength(_textBufferLineCount);
    }

    public Length Offset { get; private set; }

    public Length Length => ToLength(_textBufferLineCount - 1, _textBufferLastLineLength);

    public string GetText() => _textModel.GetValue();

    public void Skip(Length length)
    {
        _didPeek = false;
        Offset = LengthAdd(Offset, length);
        var obj = LengthToObj(Offset);
        _reader.SetPosition(obj.LineCount, obj.ColumnCount);
    }

    public Token? Read()
    {
        Token? token;
        if (_peeked is not null)
        {
            _didPeek = false;
            token = _peeked;
        }
        else
        {
            token = _reader.Read();
        }

        if (token is not null)
        {
            Offset = LengthAdd(Offset, token.Length);
        }

        return token;
    }

    public Token? Peek()
    {
        if (!_didPeek)
        {
            _peeked = _reader.Read();
            _didPeek = true;
        }

        return _peeked;
    }
}

/// <summary>Does not support peek.</summary>
internal sealed class NonPeekableTextBufferTokenizer(ITokenizerSource textModel, LanguageAgnosticBracketTokens bracketTokens)
{
    private readonly int _textBufferLineCount = textModel.GetLineCount();
    private readonly int _textBufferLastLineLength = textModel.GetLineLength(textModel.GetLineCount());

    private int _lineIdx;
    private string? _line;
    private int _lineCharOffset;
    private LineTokens? _lineTokens;
    private int _lineTokenOffset;

    /// <summary>Must be a zero line token. The end of the document cannot be peeked.</summary>
    private Token? _peekedToken;

    public void SetPosition(int lineIdx, int column)
    {
        // We must not jump into a token!
        if (lineIdx == _lineIdx)
        {
            _lineCharOffset = column;
            if (_line is not null)
            {
                _lineTokenOffset = _lineCharOffset == 0 ? 0 : _lineTokens!.FindTokenIndexAtOffset(_lineCharOffset);
            }
        }
        else
        {
            _lineIdx = lineIdx;
            _lineCharOffset = column;
            _line = null;
        }

        _peekedToken = null;
    }

    public Token? Read()
    {
        if (_peekedToken is not null)
        {
            var token = _peekedToken;
            _peekedToken = null;
            _lineCharOffset += LengthGetColumnCountIfZeroLineCount(token.Length);
            return token;
        }

        if (_lineIdx > _textBufferLineCount - 1 || (_lineIdx == _textBufferLineCount - 1 && _lineCharOffset >= _textBufferLastLineLength))
        {
            // We are after the end
            return null;
        }

        if (_line is null)
        {
            _lineTokens = textModel.GetLineTokens(_lineIdx + 1);
            _line = _lineTokens.GetLineContent();
            _lineTokenOffset = _lineCharOffset == 0 ? 0 : _lineTokens.FindTokenIndexAtOffset(_lineCharOffset);
        }

        var startLineIdx = _lineIdx;
        var startLineCharOffset = _lineCharOffset;

        // limits the length of text tokens.
        // If text tokens get too long, incremental updates will be slow
        var lengthHeuristic = 0;
        while (true)
        {
            var lineTokens = _lineTokens!;
            var tokenCount = lineTokens.GetCount();

            Token? peekedBracketToken = null;

            if (_lineTokenOffset < tokenCount)
            {
                var tokenMetadata = lineTokens.GetMetadata(_lineTokenOffset);
                while (_lineTokenOffset + 1 < tokenCount && tokenMetadata == lineTokens.GetMetadata(_lineTokenOffset + 1))
                {
                    // Skip tokens that are identical.
                    // Sometimes, (bracket) identifiers are split up into multiple tokens.
                    _lineTokenOffset++;
                }

                var isOther = TokenMetadata.GetTokenType(tokenMetadata) == StandardTokenType.Other;
                var containsBracketType = TokenMetadata.ContainsBalancedBrackets(tokenMetadata);

                var endOffset = lineTokens.GetEndOffset(_lineTokenOffset);
                // Is there a bracket token next? Only consume text.
                if (containsBracketType && isOther && _lineCharOffset < endOffset)
                {
                    var languageId = lineTokens.GetLanguageId(_lineTokenOffset);

                    var brackets = bracketTokens.GetSingleLanguageBracketTokens(languageId);
                    var regexp = brackets.RegExpGlobal;
                    if (regexp is not null)
                    {
                        var match = regexp.Match(_line!, _lineCharOffset, endOffset - _lineCharOffset);
                        if (match.Success)
                        {
                            peekedBracketToken = brackets.GetToken(match.Value);
                            if (peekedBracketToken is not null)
                            {
                                // Consume leading text of the token
                                _lineCharOffset = match.Index;
                            }
                        }
                    }
                }

                lengthHeuristic += endOffset - _lineCharOffset;

                if (peekedBracketToken is not null)
                {
                    // Don't skip the entire token, as a single token could contain multiple brackets.

                    if (startLineIdx != _lineIdx || startLineCharOffset != _lineCharOffset)
                    {
                        // There is text before the bracket
                        _peekedToken = peekedBracketToken;
                        break;
                    }

                    // Consume the peeked token
                    _lineCharOffset += LengthGetColumnCountIfZeroLineCount(peekedBracketToken.Length);
                    return peekedBracketToken;
                }

                // Skip the entire token, as the token contains no brackets at all.
                _lineTokenOffset++;
                _lineCharOffset = endOffset;
            }
            else
            {
                if (_lineIdx == _textBufferLineCount - 1)
                {
                    break;
                }

                _lineIdx++;
                _lineTokens = textModel.GetLineTokens(_lineIdx + 1);
                _lineTokenOffset = 0;
                _line = _lineTokens.GetLineContent();
                _lineCharOffset = 0;

                lengthHeuristic += 33; // max 1000/33 = 30 lines
                // This limits the amount of work to recompute min-indentation

                if (lengthHeuristic > 1000)
                {
                    // only break (automatically) at the end of line.
                    break;
                }
            }

            if (lengthHeuristic > 1500)
            {
                // Eventually break regardless of the line length so that
                // very long lines do not cause bad performance.
                // This effective limits max indentation to 500, as
                // indentation is not computed across multiple text nodes.
                break;
            }
        }

        // If a token contains some proper indentation, it also contains \n{INDENTATION+}(?!{INDENTATION}),
        // unless the line is too long.
        // Thus, the min indentation of the document is the minimum min indentation of every text node.
        var length = LengthDiff(startLineIdx, startLineCharOffset, _lineIdx, _lineCharOffset);
        return new Token(length, TokenKind.Text, -1, SmallImmutableSet<int>.GetEmpty(), new TextAstNode(length));
    }
}

internal sealed class FastTokenizer : ITokenizer
{
    private readonly string _text;
    private readonly List<Token> _tokens;
    private int _idx;

    public FastTokenizer(string text, BracketTokens brackets)
    {
        _text = text;
        var regExpStr = brackets.GetRegExpStr();
        var regexp = regExpStr is not null ? BracketTokens.CreateRegex(regExpStr + "|\n") : null;

        var tokens = new List<Token>();

        var curLineCount = 0;
        var lastLineBreakOffset = 0;

        var lastTokenEndOffset = 0;
        var lastTokenEndLine = 0;

        var smallTextTokens0Line = new Token[60];
        for (var i = 0; i < 60; i++)
        {
            smallTextTokens0Line[i] = new Token(ToLength(0, i), TokenKind.Text, -1, SmallImmutableSet<int>.GetEmpty(), new TextAstNode(ToLength(0, i)));
        }

        var smallTextTokens1Line = new Token[60];
        for (var i = 0; i < 60; i++)
        {
            smallTextTokens1Line[i] = new Token(ToLength(1, i), TokenKind.Text, -1, SmallImmutableSet<int>.GetEmpty(), new TextAstNode(ToLength(1, i)));
        }

        if (regexp is not null)
        {
            // If a token contains indentation, it also contains \n{INDENTATION+}(?!{INDENTATION})
            for (var match = regexp.Match(text); match.Success; match = match.NextMatch())
            {
                var curOffset = match.Index;
                var value = match.Value;
                if (value == "\n")
                {
                    curLineCount++;
                    lastLineBreakOffset = curOffset + 1;
                }
                else
                {
                    if (lastTokenEndOffset != curOffset)
                    {
                        Token token;
                        if (lastTokenEndLine == curLineCount)
                        {
                            var colCount = curOffset - lastTokenEndOffset;
                            if (colCount < smallTextTokens0Line.Length)
                            {
                                token = smallTextTokens0Line[colCount];
                            }
                            else
                            {
                                var length = ToLength(0, colCount);
                                token = new Token(length, TokenKind.Text, -1, SmallImmutableSet<int>.GetEmpty(), new TextAstNode(length));
                            }
                        }
                        else
                        {
                            var lineCount = curLineCount - lastTokenEndLine;
                            var colCount = curOffset - lastLineBreakOffset;
                            if (lineCount == 1 && colCount < smallTextTokens1Line.Length)
                            {
                                token = smallTextTokens1Line[colCount];
                            }
                            else
                            {
                                var length = ToLength(lineCount, colCount);
                                token = new Token(length, TokenKind.Text, -1, SmallImmutableSet<int>.GetEmpty(), new TextAstNode(length));
                            }
                        }

                        tokens.Add(token);
                    }

                    // value is matched by regexp, so the token must exist
                    tokens.Add(brackets.GetToken(value)!);

                    lastTokenEndOffset = curOffset + value.Length;
                    lastTokenEndLine = curLineCount;
                }
            }
        }

        var offset = text.Length;

        if (lastTokenEndOffset != offset)
        {
            var length = lastTokenEndLine == curLineCount
                ? ToLength(0, offset - lastTokenEndOffset)
                : ToLength(curLineCount - lastTokenEndLine, offset - lastLineBreakOffset);
            tokens.Add(new Token(length, TokenKind.Text, -1, SmallImmutableSet<int>.GetEmpty(), new TextAstNode(length)));
        }

        Length = ToLength(curLineCount, offset - lastLineBreakOffset);
        _tokens = tokens;
    }

    public Length Offset => Length.Zero;

    public Length Length { get; }

    public Token? Read() => _idx < _tokens.Count ? _tokens[_idx++] : null;

    public Token? Peek() => _idx < _tokens.Count ? _tokens[_idx] : null;

    public void Skip(Length length) => throw new NotSupportedException();

    public string GetText() => _text;
}
