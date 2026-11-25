// Ported from vscode (src/vs/platform/contextkey/common/scanner.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - Token is one record struct instead of a discriminated union: Lexeme is set for RegexStr, Str, QuotedStr and Error
//    (null otherwise), IsTripleEq only for Eq and NotEq.
//  - The sticky, unicode string regex /[a-zA-Z0-9_<>\-\./\\:\*\?\+\[\]\^,#@;"%\$\p{L}-]+/uy is a loop over code points
//    (IsStringCodePoint), with \p{L} as the five Unicode letter categories; a character it does not match is skipped,
//    as upstream does.
//  - The engine has no localization yet: the hints are the English texts of upstream's nls strings.

using System.Globalization;
using System.Text;

namespace TKCode.Arc.Editor.Platform;

/// <summary>The kinds of tokens in a context key expression.</summary>
internal enum TokenType
{
    LParen,
    RParen,
    Neg,
    Eq,
    NotEq,
    Lt,
    LtEq,
    Gt,
    GtEq,
    RegexOp,
    RegexStr,
    True,
    False,
    In,
    Not,
    And,
    Or,
    Str,
    QuotedStr,
    Error,
    EOF,
}

/// <summary>
/// A token of a context key expression. <see cref="Lexeme"/> is set for <see cref="TokenType.RegexStr"/>,
/// <see cref="TokenType.Str"/>, <see cref="TokenType.QuotedStr"/> and <see cref="TokenType.Error"/>;
/// <see cref="IsTripleEq"/> tells <c>===</c> from <c>==</c> (and <c>!==</c> from <c>!=</c>).
/// </summary>
internal readonly record struct Token(TokenType Type, int Offset, string? Lexeme = null, bool IsTripleEq = false);

/// <summary>
/// A character sequence the scanner could not make sense of, e.g. the unmatched quote in <c>foo == bar'</c>.
/// <see cref="Offset"/> does not take into account escape characters from the original encoding of the string (e.g.
/// within an extension manifest file's JSON encoding).
/// </summary>
internal readonly record struct LexingError(int Offset, string Lexeme, string? AdditionalInfo);

/// <summary>
/// A simple scanner for context keys:
/// <code>
/// var scanner = new Scanner().Reset("resourceFileName =~ /docker/ &amp;&amp; !config.docker.enabled");
/// var tokens = scanner.Scan();
/// if (scanner.Errors.Count > 0) { /* report */ } else { /* process tokens */ }
/// </code>
/// </summary>
internal sealed class Scanner
{
    internal const string HintDidYouForgetToOpenOrCloseQuote = "Did you forget to open or close the quote?";

    internal const string HintDidYouForgetToEscapeSlash =
        "Did you forget to escape the '/' (slash) character? Put two backslashes before it to escape, e.g., '\\\\/'.";

    private string _input = "";
    private int _start;
    private int _current;
    private List<Token> _tokens = new();
    private List<LexingError> _errors = new();

    public IReadOnlyList<LexingError> Errors => _errors;

    private static string? HintDidYouMean(params string[] meant)
    {
        switch (meant.Length)
        {
            case 1:
                return $"Did you mean {meant[0]}?";
            case 2:
                return $"Did you mean {meant[0]} or {meant[1]}?";
            case 3:
                return $"Did you mean {meant[0]}, {meant[1]} or {meant[2]}?";
            default: // we just don't expect that many
                return null;
        }
    }

    public static string GetLexeme(Token token)
    {
        switch (token.Type)
        {
            case TokenType.LParen:
                return "(";
            case TokenType.RParen:
                return ")";
            case TokenType.Neg:
                return "!";
            case TokenType.Eq:
                return token.IsTripleEq ? "===" : "==";
            case TokenType.NotEq:
                return token.IsTripleEq ? "!==" : "!=";
            case TokenType.Lt:
                return "<";
            case TokenType.LtEq:
                return "<=";
            case TokenType.Gt:
                return ">";
            case TokenType.GtEq:
                return ">=";
            case TokenType.RegexOp:
                return "=~";
            case TokenType.RegexStr:
                return token.Lexeme!;
            case TokenType.True:
                return "true";
            case TokenType.False:
                return "false";
            case TokenType.In:
                return "in";
            case TokenType.Not:
                return "not";
            case TokenType.And:
                return "&&";
            case TokenType.Or:
                return "||";
            case TokenType.Str:
                return token.Lexeme!;
            case TokenType.QuotedStr:
                return token.Lexeme!;
            case TokenType.Error:
                return token.Lexeme!;
            case TokenType.EOF:
                return "EOF";
            default:
                throw new InvalidOperationException($"unhandled token type: {token}; have you forgotten to add a case?");
        }
    }

    private static bool IsRegexFlag(char ch) => ch is 'i' or 'g' or 's' or 'm' or 'y' or 'u';

    private static TokenType? GetKeyword(string lexeme)
    {
        switch (lexeme)
        {
            case "not":
                return TokenType.Not;
            case "in":
                return TokenType.In;
            case "false":
                return TokenType.False;
            case "true":
                return TokenType.True;
            default:
                return null;
        }
    }

    public Scanner Reset(string value)
    {
        _input = value;

        _start = 0;
        _current = 0;
        _tokens = new List<Token>();
        _errors = new List<LexingError>();

        return this;
    }

    public List<Token> Scan()
    {
        while (!IsAtEnd())
        {
            _start = _current;

            var ch = Advance();
            switch (ch)
            {
                case '(':
                    AddToken(TokenType.LParen);
                    break;
                case ')':
                    AddToken(TokenType.RParen);
                    break;

                case '!':
                    if (Match('='))
                    {
                        var isTripleEq = Match('='); // eat last `=` if `!==`
                        _tokens.Add(new Token(TokenType.NotEq, _start, IsTripleEq: isTripleEq));
                    }
                    else
                    {
                        AddToken(TokenType.Neg);
                    }
                    break;

                case '\'':
                    QuotedString();
                    break;
                case '/':
                    Regex();
                    break;

                case '=':
                    if (Match('=')) // support `==`
                    {
                        var isTripleEq = Match('='); // eat last `=` if `===`
                        _tokens.Add(new Token(TokenType.Eq, _start, IsTripleEq: isTripleEq));
                    }
                    else if (Match('~'))
                    {
                        AddToken(TokenType.RegexOp);
                    }
                    else
                    {
                        Error(HintDidYouMean("==", "=~"));
                    }
                    break;

                case '<':
                    AddToken(Match('=') ? TokenType.LtEq : TokenType.Lt);
                    break;

                case '>':
                    AddToken(Match('=') ? TokenType.GtEq : TokenType.Gt);
                    break;

                case '&':
                    if (Match('&'))
                    {
                        AddToken(TokenType.And);
                    }
                    else
                    {
                        Error(HintDidYouMean("&&"));
                    }
                    break;

                case '|':
                    if (Match('|'))
                    {
                        AddToken(TokenType.Or);
                    }
                    else
                    {
                        Error(HintDidYouMean("||"));
                    }
                    break;

                // TODO@ulugbekna: 1) rewrite using a regex 2) reconsider what characters are considered whitespace, including unicode, nbsp, etc.
                case ' ':
                case '\r':
                case '\t':
                case '\n':
                case '\u00A0': // &nbsp
                    break;

                default:
                    String();
                    break;
            }
        }

        _start = _current;
        AddToken(TokenType.EOF);

        return new List<Token>(_tokens);
    }

    private bool Match(char expected)
    {
        if (IsAtEnd())
        {
            return false;
        }
        if (_input[_current] != expected)
        {
            return false;
        }
        _current++;
        return true;
    }

    private char Advance()
    {
        return _input[_current++];
    }

    private char Peek()
    {
        return IsAtEnd() ? '\0' : _input[_current];
    }

    private void AddToken(TokenType type)
    {
        _tokens.Add(new Token(type, _start));
    }

    private void Error(string? additional)
    {
        var offset = _start;
        var lexeme = _input.Substring(_start, _current - _start);
        var errToken = new Token(TokenType.Error, _start, lexeme);
        _errors.Add(new LexingError(offset, lexeme, additional));
        _tokens.Add(errToken);
    }

    // u - unicode, y - sticky // TODO@ulugbekna: we accept double quotes as part of the string rather than as a delimiter (to preserve old parser's behavior)
    private void String()
    {
        var end = _start;
        while (end < _input.Length)
        {
            if (Rune.DecodeFromUtf16(_input.AsSpan(end), out var rune, out var consumed) != System.Buffers.OperationStatus.Done
                || !IsStringCodePoint(rune))
            {
                break;
            }
            end += consumed;
        }

        if (end > _start)
        {
            _current = end;
            var lexeme = _input.Substring(_start, _current - _start);
            var keyword = GetKeyword(lexeme);
            if (keyword is { } k)
            {
                AddToken(k);
            }
            else
            {
                _tokens.Add(new Token(TokenType.Str, _start, lexeme));
            }
        }
    }

    // [a-zA-Z0-9_<>\-\./\\:\*\?\+\[\]\^,#@;"%\$\p{L}-]
    private static bool IsStringCodePoint(Rune rune)
    {
        if (rune.IsAscii)
        {
            var ch = (char)rune.Value;
            if (ch is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
            {
                return true;
            }
            return ch is '_' or '<' or '>' or '-' or '.' or '/' or '\\' or ':' or '*' or '?' or '+' or '[' or ']'
                or '^' or ',' or '#' or '@' or ';' or '"' or '%' or '$';
        }

        switch (Rune.GetUnicodeCategory(rune))
        {
            case UnicodeCategory.UppercaseLetter:
            case UnicodeCategory.LowercaseLetter:
            case UnicodeCategory.TitlecaseLetter:
            case UnicodeCategory.ModifierLetter:
            case UnicodeCategory.OtherLetter:
                return true;
            default:
                return false;
        }
    }

    // captures the lexeme without the leading and trailing '
    private void QuotedString()
    {
        while (Peek() != '\'' && !IsAtEnd()) // TODO@ulugbekna: add support for escaping ' ?
        {
            Advance();
        }

        if (IsAtEnd())
        {
            Error(HintDidYouForgetToOpenOrCloseQuote);
            return;
        }

        // consume the closing '
        Advance();

        _tokens.Add(new Token(TokenType.QuotedStr, _start + 1, _input.Substring(_start + 1, _current - 1 - (_start + 1))));
    }

    /*
     * Lexing a regex expression: /.../[igsmyu]*
     * Based on https://github.com/microsoft/TypeScript/blob/9247ef115e617805983740ba795d7a8164babf89/src/compiler/scanner.ts#L2129-L2181
     *
     * Note that we want slashes within a regex to be escaped, e.g., /file:\\/\\/\\// should match `file:///`
     */
    private void Regex()
    {
        var p = _current;

        var inEscape = false;
        var inCharacterClass = false;
        while (true)
        {
            if (p >= _input.Length)
            {
                _current = p;
                Error(HintDidYouForgetToEscapeSlash);
                return;
            }

            var ch = _input[p];

            if (inEscape) // parsing an escape character
            {
                inEscape = false;
            }
            else if (ch == '/' && !inCharacterClass) // end of regex
            {
                p++;
                break;
            }
            else if (ch == '[')
            {
                inCharacterClass = true;
            }
            else if (ch == '\\')
            {
                inEscape = true;
            }
            else if (ch == ']')
            {
                inCharacterClass = false;
            }
            p++;
        }

        // Consume flags // TODO@ulugbekna: use regex instead
        while (p < _input.Length && IsRegexFlag(_input[p]))
        {
            p++;
        }

        _current = p;

        var lexeme = _input.Substring(_start, _current - _start);
        _tokens.Add(new Token(TokenType.RegexStr, _start, lexeme));
    }

    private bool IsAtEnd()
    {
        return _current >= _input.Length;
    }
}
