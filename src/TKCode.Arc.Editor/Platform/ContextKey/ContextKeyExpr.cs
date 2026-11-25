// Ported from vscode (src/vs/platform/contextkey/common/contextkey.ts, commit b57d5c9e), MIT license.
// This file holds the expression type, the ContextKeyExpr factory and the free functions (implies,
// expressionsAreEqualWithConstantSubstitution, validateWhenClauses, cmp1, cmp2); the expression classes are in
// ContextKeyExpr.Terms.cs, ContextKeyExpr.Comparisons.cs and ContextKeyExpr.AndOr.cs, the parser in Parser.cs and
// RawContextKey and the service interfaces in ContextKey.cs. Differences from upstream:
//  - ContextKeyExpression is an abstract class (upstream: the IContextKeyExpression interface plus a union type over the
//    sixteen classes); Type is the union's discriminant. Equals(ContextKeyExpression) is an overload next to
//    object.Equals, which keeps reference equality.
//  - Values are object? with JavaScript's semantics (ContextKeyValues.cs); null stands for undefined. Regular
//    expressions are JsRegExp (JsRegExp.cs).
//  - The constants (CONSTANT_VALUES: true, false, isMac, isLinux, isWindows, ...) are immutable: setConstant, which lets
//    an application add constants at run time, is left out because it is process-wide mutable state. isWeb is
//    OperatingSystem.IsBrowser(), isChromeOS and the browser checks (isEdge, isFirefox, isChrome, isSafari) are false
//    since .NET does not know the browser.
//  - ContextKeyExpr.Deserialize makes a parser per call; upstream shares one static parser, which is mutable state.
//  - Sorting is stable (LINQ OrderBy), like JavaScript's Array.prototype.sort; List.Sort is not.
//  - A term caches its negation; the cache is filled with Interlocked.CompareExchange so expressions (the static
//    EditorContextKeys among them) can be negated from several threads.

using System.Collections.Frozen;

namespace TKCode.Arc.Editor.Platform;

internal enum ContextKeyExprType
{
    False = 0,
    True = 1,
    Defined = 2,
    Not = 3,
    Equals = 4,
    NotEquals = 5,
    And = 6,
    Regex = 7,
    NotRegex = 8,
    Or = 9,
    In = 10,
    NotIn = 11,
    Greater = 12,
    GreaterEquals = 13,
    Smaller = 14,
    SmallerEquals = 15,
}

/// <summary>Rewrites the terms of an expression (<see cref="ContextKeyExpression.Map"/>).</summary>
internal interface IContextKeyExprMapper
{
    ContextKeyExpression MapDefined(string key);
    ContextKeyExpression MapNot(string key);
    ContextKeyExpression MapEquals(string key, object? value);
    ContextKeyExpression MapNotEquals(string key, object? value);
    ContextKeyExpression MapGreater(string key, object? value);
    ContextKeyExpression MapGreaterEquals(string key, object? value);
    ContextKeyExpression MapSmaller(string key, object? value);
    ContextKeyExpression MapSmallerEquals(string key, object? value);
    ContextKeyRegexExpr MapRegex(string key, JsRegExp? regexp);
    ContextKeyInExpr MapIn(string key, string valueKey);
    ContextKeyNotInExpr MapNotIn(string key, string valueKey);
}

/// <summary>
/// A normalized context key (<c>when</c>) expression: evaluates against an <see cref="IContext"/>, serializes back to
/// text, and can be negated, compared and mapped.
/// </summary>
internal abstract class ContextKeyExpression
{
    public abstract ContextKeyExprType Type { get; }

    public abstract int Cmp(ContextKeyExpression other);

    public abstract bool Equals(ContextKeyExpression other);

    public abstract ContextKeyExpression? SubstituteConstants();

    public abstract bool Evaluate(IContext context);

    public abstract string Serialize();

    public abstract string[] Keys();

    public abstract ContextKeyExpression Map(IContextKeyExprMapper mapFnc);

    public abstract ContextKeyExpression Negate();

    public override string ToString() => Serialize();

    /// <summary>Stores <paramref name="created"/> in <paramref name="cache"/> unless another thread got there first.</summary>
    protected static ContextKeyExpression CacheNegated(ref ContextKeyExpression? cache, ContextKeyExpression created)
    {
        return Interlocked.CompareExchange(ref cache, created, null) ?? created;
    }
}

/// <summary>Builds normalized context key expressions.</summary>
internal static class ContextKeyExpr
{
    public static ContextKeyExpression False()
    {
        return ContextKeyFalseExpr.INSTANCE;
    }

    public static ContextKeyExpression True()
    {
        return ContextKeyTrueExpr.INSTANCE;
    }

    public static ContextKeyExpression Has(string key)
    {
        return ContextKeyDefinedExpr.Create(key);
    }

    public static ContextKeyExpression Equals(string key, object? value)
    {
        return ContextKeyEqualsExpr.Create(key, value);
    }

    public static ContextKeyExpression NotEquals(string key, object? value)
    {
        return ContextKeyNotEqualsExpr.Create(key, value);
    }

    public static ContextKeyExpression Regex(string key, JsRegExp value)
    {
        return ContextKeyRegexExpr.Create(key, value);
    }

    public static ContextKeyExpression In(string key, string value)
    {
        return ContextKeyInExpr.Create(key, value);
    }

    public static ContextKeyExpression NotIn(string key, string value)
    {
        return ContextKeyNotInExpr.Create(key, value);
    }

    public static ContextKeyExpression Not(string key)
    {
        return ContextKeyNotExpr.Create(key);
    }

    public static ContextKeyExpression? And(params ContextKeyExpression?[] expr)
    {
        return ContextKeyAndExpr.Create(expr, null, true);
    }

    public static ContextKeyExpression? Or(params ContextKeyExpression?[] expr)
    {
        return ContextKeyOrExpr.Create(expr, null, true);
    }

    public static ContextKeyExpression Greater(string key, double value)
    {
        return ContextKeyGreaterExpr.Create(key, value);
    }

    public static ContextKeyExpression GreaterEquals(string key, double value)
    {
        return ContextKeyGreaterEqualsExpr.Create(key, value);
    }

    public static ContextKeyExpression Smaller(string key, double value)
    {
        return ContextKeySmallerExpr.Create(key, value);
    }

    public static ContextKeyExpression SmallerEquals(string key, double value)
    {
        return ContextKeySmallerEqualsExpr.Create(key, value);
    }

    /// <summary>
    /// Parses <paramref name="serialized"/> without regex error recovery; null for null or for text with errors (an empty
    /// string goes to the parser, which reports it).
    /// </summary>
    public static ContextKeyExpression? Deserialize(string? serialized)
    {
        if (serialized is null)
        {
            return null;
        }

        var parser = new Parser(new ParserConfig(RegexParsingWithErrorRecovery: false));
        return parser.Parse(serialized);
    }
}

/// <summary>One problem in a <c>when</c> clause, as <see cref="ContextKeyExpressions.ValidateWhenClauses"/> reports it.</summary>
internal readonly record struct WhenClauseError(string ErrorMessage, int Offset, int Length);

/// <summary>The free functions of upstream's contextkey.ts.</summary>
internal static class ContextKeyExpressions
{
    /// <summary>
    /// The keys whose value is known when the expression is built (upstream's CONSTANT_VALUES); a term over one of them
    /// normalizes to true or false.
    /// </summary>
    public static readonly FrozenDictionary<string, bool> ConstantValues = CreateConstantValues();

    private static FrozenDictionary<string, bool> CreateConstantValues()
    {
        var isWeb = OperatingSystem.IsBrowser();
        var isMacintosh = OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst();
        return new Dictionary<string, bool>
        {
            ["false"] = false,
            ["true"] = true,
            ["isMac"] = isMacintosh,
            ["isLinux"] = OperatingSystem.IsLinux(),
            ["isWindows"] = OperatingSystem.IsWindows(),
            ["isChromeOS"] = false,
            ["isWeb"] = isWeb,
            ["isMacNative"] = isMacintosh && !isWeb,
            ["isEdge"] = false,
            ["isFirefox"] = false,
            ["isChrome"] = false,
            ["isSafari"] = false,
        }.ToFrozenDictionary(StringComparer.Ordinal);
    }

    internal static bool TryGetConstant(string key, out bool value) => ConstantValues.TryGetValue(key, out value);

    /// <summary>Checks <c>when</c> clauses (no regex error recovery); one list of errors per clause, empty when it is valid.</summary>
    public static List<List<WhenClauseError>> ValidateWhenClauses(IEnumerable<string> whenClauses)
    {
        var parser = new Parser(new ParserConfig(RegexParsingWithErrorRecovery: false)); // we run with no recovery to guide users to use correct regexes

        var result = new List<List<WhenClauseError>>();
        foreach (var whenClause in whenClauses)
        {
            parser.Parse(whenClause);

            var errors = new List<WhenClauseError>();
            if (parser.LexingErrors.Count > 0)
            {
                foreach (var se in parser.LexingErrors)
                {
                    errors.Add(new WhenClauseError(
                        se.AdditionalInfo is not null
                            ? $"Unexpected token. Hint: {se.AdditionalInfo}"
                            : "Unexpected token.",
                        se.Offset,
                        se.Lexeme.Length));
                }
            }
            else if (parser.ParsingErrors.Count > 0)
            {
                foreach (var pe in parser.ParsingErrors)
                {
                    errors.Add(new WhenClauseError(
                        pe.AdditionalInfo is not null ? $"{pe.Message}. {pe.AdditionalInfo}" : pe.Message,
                        pe.Offset,
                        pe.Lexeme.Length));
                }
            }
            result.Add(errors);
        }
        return result;
    }

    public static bool ExpressionsAreEqualWithConstantSubstitution(ContextKeyExpression? a, ContextKeyExpression? b)
    {
        var aExpr = a?.SubstituteConstants();
        var bExpr = b?.SubstituteConstants();
        if (aExpr is null && bExpr is null)
        {
            return true;
        }
        if (aExpr is null || bExpr is null)
        {
            return false;
        }
        return aExpr.Equals(bExpr);
    }

    internal static int Cmp(ContextKeyExpression a, ContextKeyExpression b)
    {
        return a.Cmp(b);
    }

    internal static readonly IComparer<ContextKeyExpression> Comparer = Comparer<ContextKeyExpression>.Create(Cmp);

    /// <summary>Sorts like JavaScript's (stable) Array.prototype.sort with <see cref="Cmp"/>.</summary>
    internal static void StableSort(List<ContextKeyExpression> expr)
    {
        if (expr.Count < 2)
        {
            return;
        }
        var sorted = expr.OrderBy(static e => e, Comparer).ToArray();
        expr.Clear();
        expr.AddRange(sorted);
    }

    internal static int Cmp1(string key1, string key2)
    {
        var r = string.CompareOrdinal(key1, key2);
        return r < 0 ? -1 : (r > 0 ? 1 : 0);
    }

    internal static int Cmp2(string key1, object? value1, string key2, object? value2)
    {
        var r = Cmp1(key1, key2);
        if (r != 0)
        {
            return r;
        }
        if (ContextKeyValues.LessThan(value1, value2))
        {
            return -1;
        }
        if (ContextKeyValues.LessThan(value2, value1))
        {
            return 1;
        }
        return 0;
    }

    /// <summary>Returns true if it is provable <paramref name="p"/> implies <paramref name="q"/>.</summary>
    public static bool Implies(ContextKeyExpression p, ContextKeyExpression q)
    {
        if (p.Type == ContextKeyExprType.False || q.Type == ContextKeyExprType.True)
        {
            // false implies anything
            // anything implies true
            return true;
        }

        if (p is ContextKeyOrExpr pOr)
        {
            if (q is ContextKeyOrExpr qOr)
            {
                // `a || b || c` can only imply something like `a || b || c || d`
                return AllElementsIncluded(pOr.Expr, qOr.Expr);
            }
            return false;
        }

        if (q is ContextKeyOrExpr qOr2)
        {
            foreach (var element in qOr2.Expr)
            {
                if (Implies(p, element))
                {
                    return true;
                }
            }
            return false;
        }

        if (p is ContextKeyAndExpr pAnd)
        {
            if (q is ContextKeyAndExpr qAnd)
            {
                // `a && b && c` implies `a && c`
                return AllElementsIncluded(qAnd.Expr, pAnd.Expr);
            }
            foreach (var element in pAnd.Expr)
            {
                if (Implies(element, q))
                {
                    return true;
                }
            }
            return false;
        }

        return p.Equals(q);
    }

    /// <summary>Returns true if all elements in <paramref name="p"/> are also present in <paramref name="q"/>; both are sorted.</summary>
    private static bool AllElementsIncluded(IReadOnlyList<ContextKeyExpression> p, IReadOnlyList<ContextKeyExpression> q)
    {
        var pIndex = 0;
        var qIndex = 0;
        while (pIndex < p.Count && qIndex < q.Count)
        {
            var cmp = p[pIndex].Cmp(q[qIndex]);

            if (cmp < 0)
            {
                // an element from `p` is missing from `q`
                return false;
            }
            else if (cmp == 0)
            {
                pIndex++;
                qIndex++;
            }
            else
            {
                qIndex++;
            }
        }
        return pIndex == p.Count;
    }

    internal static IReadOnlyList<ContextKeyExpression> GetTerminals(ContextKeyExpression node)
    {
        if (node is ContextKeyOrExpr or)
        {
            return or.Expr;
        }
        return new[] { node };
    }
}
