// Ported from vscode (src/vs/platform/contextkey/common/contextkey.ts: ParserConfig, ParsingError, Parser, commit
// b57d5c9e), MIT license. Differences from upstream:
//  - the regular expressions are JsRegExp (JsRegExp.cs says how they map onto .NET); a pattern .NET rejects is a parse
//    error, as a pattern JavaScript rejects is upstream, but the two do not reject exactly the same patterns.
//  - the parse error that unwinds the recursive descent is a private exception type (upstream throws one shared Error).
//  - the engine has no localization yet: the messages are the English texts of upstream's nls strings.
//
// Syntax grammar:
//
//   expression ::= or
//   or ::= and { '||' and }*
//   and ::= term { '&&' term }*
//   term ::=
//       | '!' (KEY | true | false | parenthesized)
//       | primary
//   primary ::=
//       | 'true'
//       | 'false'
//       | parenthesized
//       | KEY '=~' REGEX
//       | KEY [ ('==' | '!=' | '<' | '<=' | '>' | '>=' | 'not' 'in' | 'in') value ]
//   parenthesized ::=
//       | '(' expression ')'
//   value ::=
//       | 'true'
//       | 'false'
//       | 'in'          // we support `in` as a value because there's an extension that uses it, ie "when": "languageId == in"
//       | VALUE         // matched by the same regex as KEY; consider putting the value in single quotes if it's a string (e.g., with spaces)
//       | SINGLE_QUOTED_STR
//       | EMPTY_STR     // this allows "when": "foo == " which's used by existing extensions

using System.Text;

namespace TKCode.Arc.Editor.Platform;

/// <summary>
/// <see cref="RegexParsingWithErrorRecovery"/>: the parser can recover from regex parsing errors, e.g., unescaped
/// slashes: <c>/src//</c> is accepted as <c>/src\//</c> would be.
/// </summary>
internal readonly record struct ParserConfig(bool RegexParsingWithErrorRecovery);

internal readonly record struct ParsingError(string Message, int Offset, string Lexeme, string? AdditionalInfo);

/// <summary>
/// A parser for context key expressions:
/// <code>
/// var parser = new Parser();
/// var expr = parser.Parse("foo == 'bar' &amp;&amp; baz == true");
/// if (expr is null) { /* see parser.LexingErrors and parser.ParsingErrors */ }
/// </code>
/// </summary>
internal sealed class Parser
{
    // Note: this doesn't produce an exact syntax tree but a normalized one
    // ContextKeyExpression's that we use as AST nodes do not expose constructors that do not normalize

    internal const string ErrorEmptyString = "Empty context key expression";
    internal const string HintEmptyString = "Did you forget to write an expression? You can also put 'false' or 'true' to always evaluate to false or true, respectively.";
    internal const string ErrorNoInAfterNot = "'in' after 'not'.";
    internal const string ErrorClosingParenthesis = "closing parenthesis ')'";
    internal const string ErrorUnexpectedToken = "Unexpected token";
    internal const string HintUnexpectedToken = "Did you forget to put && or || before the token?";
    internal const string ErrorUnexpectedEOF = "Unexpected end of expression";
    internal const string HintUnexpectedEOF = "Did you forget to put a context key?";

    private static readonly ParserConfig DefaultConfig = new(RegexParsingWithErrorRecovery: true);

    private readonly ParserConfig _config;

    // lifetime note: `_scanner` lives as long as the parser does, i.e., is not reset between calls to `parse`
    private readonly Scanner _scanner = new();

    // lifetime note: `_tokens`, `_current`, and `_parsingErrors` must be reset between calls to `parse`
    private List<Token> _tokens = new();
    private int _current; // invariant: 0 <= this._current < this._tokens.length ; any incrementation of this value must first call `_isAtEnd`
    private List<ParsingError> _parsingErrors = new();

    public Parser()
        : this(DefaultConfig)
    {
    }

    public Parser(ParserConfig config)
    {
        _config = config;
    }

    public IReadOnlyList<LexingError> LexingErrors => _scanner.Errors;

    public IReadOnlyList<ParsingError> ParsingErrors => _parsingErrors;

    /// <summary>
    /// Parses a context key expression; null if there's an error (see <see cref="LexingErrors"/> and
    /// <see cref="ParsingErrors"/>).
    /// </summary>
    public ContextKeyExpression? Parse(string input)
    {
        if (input.Length == 0)
        {
            _parsingErrors.Add(new ParsingError(ErrorEmptyString, 0, "", HintEmptyString));
            return null;
        }

        _tokens = _scanner.Reset(input).Scan();
        // @ulugbekna: we do not stop parsing if there are lexing errors to be able to reconstruct regexes with unescaped slashes; TODO@ulugbekna: make this respect config option for recovery

        _current = 0;
        _parsingErrors = new List<ParsingError>();

        try
        {
            var expr = Expr();
            if (!IsAtEnd())
            {
                var peek = Peek();
                var additionalInfo = peek.Type == TokenType.Str ? HintUnexpectedToken : null;
                _parsingErrors.Add(new ParsingError(ErrorUnexpectedToken, peek.Offset, Scanner.GetLexeme(peek), additionalInfo));
                throw new ParseErrorException();
            }
            return expr;
        }
        catch (ParseErrorException)
        {
            return null;
        }
    }

    private ContextKeyExpression? Expr()
    {
        return Or();
    }

    private ContextKeyExpression? Or()
    {
        var expr = new List<ContextKeyExpression?> { And() };

        while (MatchOne(TokenType.Or))
        {
            var right = And();
            expr.Add(right);
        }

        return expr.Count == 1 ? expr[0] : ContextKeyExpr.Or(expr.ToArray());
    }

    private ContextKeyExpression? And()
    {
        var expr = new List<ContextKeyExpression?> { Term() };

        while (MatchOne(TokenType.And))
        {
            var right = Term();
            expr.Add(right);
        }

        return expr.Count == 1 ? expr[0] : ContextKeyExpr.And(expr.ToArray());
    }

    private ContextKeyExpression? Term()
    {
        if (MatchOne(TokenType.Neg))
        {
            var peek = Peek();
            switch (peek.Type)
            {
                case TokenType.True:
                    Advance();
                    return ContextKeyFalseExpr.INSTANCE;
                case TokenType.False:
                    Advance();
                    return ContextKeyTrueExpr.INSTANCE;
                case TokenType.LParen:
                {
                    Advance();
                    var expr = Expr();
                    Consume(TokenType.RParen, ErrorClosingParenthesis);
                    return expr?.Negate();
                }
                case TokenType.Str:
                    Advance();
                    return ContextKeyNotExpr.Create(peek.Lexeme!);
                default:
                    throw ErrExpectedButGot("KEY | true | false | '(' expression ')'", peek);
            }
        }
        return Primary();
    }

    private ContextKeyExpression? Primary()
    {
        var peek = Peek();
        switch (peek.Type)
        {
            case TokenType.True:
                Advance();
                return ContextKeyExpr.True();

            case TokenType.False:
                Advance();
                return ContextKeyExpr.False();

            case TokenType.LParen:
            {
                Advance();
                var expr = Expr();
                Consume(TokenType.RParen, ErrorClosingParenthesis);
                return expr;
            }

            case TokenType.Str:
            {
                // KEY
                var key = peek.Lexeme!;
                Advance();

                // =~ regex
                if (MatchOne(TokenType.RegexOp))
                {
                    return RegexAfterOperator(key);
                }

                // [ 'not' 'in' value ]
                if (MatchOne(TokenType.Not))
                {
                    Consume(TokenType.In, ErrorNoInAfterNot);
                    var right = Value();
                    return ContextKeyExpr.NotIn(key, right);
                }

                // [ ('==' | '!=' | '<' | '<=' | '>' | '>=' | 'in') value ]
                var maybeOp = Peek().Type;
                switch (maybeOp)
                {
                    case TokenType.Eq:
                    {
                        Advance();

                        var right = Value();
                        if (Previous().Type == TokenType.QuotedStr) // to preserve old parser behavior: "foo == 'true'" is preserved as "foo == 'true'", but "foo == true" is optimized as "foo"
                        {
                            return ContextKeyExpr.Equals(key, right);
                        }
                        switch (right)
                        {
                            case "true":
                                return ContextKeyExpr.Has(key);
                            case "false":
                                return ContextKeyExpr.Not(key);
                            default:
                                return ContextKeyExpr.Equals(key, right);
                        }
                    }

                    case TokenType.NotEq:
                    {
                        Advance();

                        var right = Value();
                        if (Previous().Type == TokenType.QuotedStr) // same as above with "foo != 'true'"
                        {
                            return ContextKeyExpr.NotEquals(key, right);
                        }
                        switch (right)
                        {
                            case "true":
                                return ContextKeyExpr.Not(key);
                            case "false":
                                return ContextKeyExpr.Has(key);
                            default:
                                return ContextKeyExpr.NotEquals(key, right);
                        }
                    }
                    // TODO: ContextKeyExpr.smaller(key, right) accepts only `number` as `right` AND during eval of this node, we just eval to `false` if `right` is not a number
                    // consequently, package.json linter should _warn_ the user if they're passing undesired things to ops
                    case TokenType.Lt:
                        Advance();
                        return ContextKeySmallerExpr.Create(key, Value());

                    case TokenType.LtEq:
                        Advance();
                        return ContextKeySmallerEqualsExpr.Create(key, Value());

                    case TokenType.Gt:
                        Advance();
                        return ContextKeyGreaterExpr.Create(key, Value());

                    case TokenType.GtEq:
                        Advance();
                        return ContextKeyGreaterEqualsExpr.Create(key, Value());

                    case TokenType.In:
                        Advance();
                        return ContextKeyExpr.In(key, Value());

                    default:
                        return ContextKeyExpr.Has(key);
                }
            }

            case TokenType.EOF:
                _parsingErrors.Add(new ParsingError(ErrorUnexpectedEOF, peek.Offset, "", HintUnexpectedEOF));
                throw new ParseErrorException();

            default:
                throw ErrExpectedButGot("true | false | KEY \n\t| KEY '=~' REGEX \n\t| KEY ('==' | '!=' | '<' | '<=' | '>' | '>=' | 'in' | 'not' 'in') value", Peek());
        }
    }

    // The part of Primary after `KEY =~`.
    private ContextKeyExpression RegexAfterOperator(string key)
    {
        // @ulugbekna: we need to reconstruct the regex from the tokens because some extensions use unescaped slashes in regexes
        var expr = Peek();

        if (!_config.RegexParsingWithErrorRecovery)
        {
            Advance();
            if (expr.Type != TokenType.RegexStr)
            {
                throw ErrExpectedButGot("REGEX", expr);
            }
            var regexLexeme = expr.Lexeme!;
            var closingSlashIndex = regexLexeme.LastIndexOf('/');
            var flags = closingSlashIndex == regexLexeme.Length - 1 ? null : RemoveFlagsGY(regexLexeme.Substring(closingSlashIndex + 1));
            JsRegExp regexp;
            try
            {
                regexp = new JsRegExp(SubstringJs(regexLexeme, 1, closingSlashIndex), flags);
            }
            catch (ArgumentException)
            {
                throw ErrExpectedButGot("REGEX", expr);
            }
            return ContextKeyRegexExpr.Create(key, regexp);
        }

        switch (expr.Type)
        {
            case TokenType.RegexStr:
            case TokenType.Error: // also handle an ErrorToken in case of smth such as /(/file)/
            {
                var exprLexeme = expr.Lexeme!;
                var lexemeReconstruction = new StringBuilder(exprLexeme); // /REGEX/ or /REGEX/FLAGS
                Advance();

                var followingToken = Peek();
                var parenBalance = 0;
                for (var i = 0; i < exprLexeme.Length; i++)
                {
                    if (exprLexeme[i] == '(')
                    {
                        parenBalance++;
                    }
                    else if (exprLexeme[i] == ')')
                    {
                        parenBalance--;
                    }
                }

                while (!IsAtEnd() && followingToken.Type != TokenType.And && followingToken.Type != TokenType.Or)
                {
                    switch (followingToken.Type)
                    {
                        case TokenType.LParen:
                            parenBalance++;
                            break;
                        case TokenType.RParen:
                            parenBalance--;
                            break;
                        case TokenType.RegexStr:
                        case TokenType.QuotedStr:
                            var followingLexeme = followingToken.Lexeme!;
                            for (var i = 0; i < followingLexeme.Length; i++)
                            {
                                if (followingLexeme[i] == '(')
                                {
                                    parenBalance++;
                                }
                                // upstream reads the first token's lexeme here (expr.lexeme), kept as is
                                else if (i < exprLexeme.Length && exprLexeme[i] == ')')
                                {
                                    parenBalance--;
                                }
                            }
                            break;
                    }
                    if (parenBalance < 0)
                    {
                        break;
                    }
                    lexemeReconstruction.Append(Scanner.GetLexeme(followingToken));
                    Advance();
                    followingToken = Peek();
                }

                var regexLexeme = lexemeReconstruction.ToString();
                var closingSlashIndex = regexLexeme.LastIndexOf('/');
                var flags = closingSlashIndex == regexLexeme.Length - 1 ? null : RemoveFlagsGY(regexLexeme.Substring(closingSlashIndex + 1));
                JsRegExp regexp;
                try
                {
                    regexp = new JsRegExp(SubstringJs(regexLexeme, 1, closingSlashIndex), flags);
                }
                catch (ArgumentException)
                {
                    throw ErrExpectedButGot("REGEX", expr);
                }
                return ContextKeyExpr.Regex(key, regexp);
            }

            case TokenType.QuotedStr:
            {
                var serializedValue = expr.Lexeme!;
                Advance();
                // replicate old regex parsing behavior

                JsRegExp? regex = null;

                if (!string.IsNullOrWhiteSpace(serializedValue))
                {
                    var start = serializedValue.IndexOf('/');
                    var end = serializedValue.LastIndexOf('/');
                    if (start != end && start >= 0)
                    {
                        var value = serializedValue.Substring(start + 1, end - start - 1);
                        var caseIgnoreFlag = end + 1 < serializedValue.Length && serializedValue[end + 1] == 'i' ? "i" : "";
                        try
                        {
                            regex = new JsRegExp(value, caseIgnoreFlag);
                        }
                        catch (ArgumentException)
                        {
                            throw ErrExpectedButGot("REGEX", expr);
                        }
                    }
                }

                if (regex is null)
                {
                    throw ErrExpectedButGot("REGEX", expr);
                }

                return ContextKeyRegexExpr.Create(key, regex);
            }

            default:
                throw ErrExpectedButGot("REGEX", Peek());
        }
    }

    // JavaScript's substring(start, end): swaps the bounds when end < start and clamps them to the string.
    private static string SubstringJs(string s, int start, int end)
    {
        start = Math.Clamp(start, 0, s.Length);
        end = Math.Clamp(end, 0, s.Length);
        if (end < start)
        {
            (start, end) = (end, start);
        }
        return s.Substring(start, end - start);
    }

    private string Value()
    {
        var token = Peek();
        switch (token.Type)
        {
            case TokenType.Str:
            case TokenType.QuotedStr:
                Advance();
                return token.Lexeme!;
            case TokenType.True:
                Advance();
                return "true";
            case TokenType.False:
                Advance();
                return "false";
            case TokenType.In: // we support `in` as a value, e.g., "when": "languageId == in" - exists in existing extensions
                Advance();
                return "in";
            default:
                // this allows "when": "foo == " which's used by existing extensions
                // we do not call `_advance` on purpose - we don't want to eat unintended tokens
                return "";
        }
    }

    private static string RemoveFlagsGY(string flags)
    {
        return flags.Replace("g", "", StringComparison.Ordinal).Replace("y", "", StringComparison.Ordinal);
    }

    // careful: this can throw if current token is the initial one (ie index = 0)
    private Token Previous()
    {
        return _tokens[_current - 1];
    }

    private bool MatchOne(TokenType token)
    {
        if (Check(token))
        {
            Advance();
            return true;
        }

        return false;
    }

    private Token Advance()
    {
        if (!IsAtEnd())
        {
            _current++;
        }
        return Previous();
    }

    private Token Consume(TokenType type, string message)
    {
        if (Check(type))
        {
            return Advance();
        }

        throw ErrExpectedButGot(message, Peek());
    }

    private ParseErrorException ErrExpectedButGot(string expected, Token got, string? additionalInfo = null)
    {
        var message = $"Expected: {expected}\nReceived: '{Scanner.GetLexeme(got)}'.";
        var offset = got.Offset;
        var lexeme = Scanner.GetLexeme(got);
        _parsingErrors.Add(new ParsingError(message, offset, lexeme, additionalInfo));
        return new ParseErrorException();
    }

    private bool Check(TokenType type)
    {
        return Peek().Type == type;
    }

    private Token Peek()
    {
        return _tokens[_current];
    }

    private bool IsAtEnd()
    {
        return Peek().Type == TokenType.EOF;
    }

    /// <summary>Unwinds the recursive descent once an error is recorded (upstream throws one shared <c>_parseError</c>).</summary>
    private sealed class ParseErrorException : Exception
    {
        // A new instance per error: a thrown exception records its stack trace, so a shared one would be shared state.
    }
}
