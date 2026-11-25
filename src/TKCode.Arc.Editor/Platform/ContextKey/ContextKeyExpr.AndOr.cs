// Ported from vscode (src/vs/platform/contextkey/common/contextkey.ts: eliminateConstantsInArray, ContextKeyAndExpr,
// ContextKeyOrExpr, commit b57d5c9e), MIT license. Differences from upstream (see also ContextKeyExpr.cs): the terms
// are an array exposed as IReadOnlyList, and sorting is stable (ContextKeyExpressions.StableSort) as JavaScript's sort.

namespace TKCode.Arc.Editor.Platform;

internal sealed class ContextKeyAndExpr : ContextKeyExpression
{
    private readonly ContextKeyExpression[] _expr;
    private ContextKeyExpression? _negated;

    public static ContextKeyExpression? Create(IReadOnlyList<ContextKeyExpression?> expr, ContextKeyExpression? negated, bool extraRedundantCheck)
    {
        if (expr is ContextKeyExpression?[] array)
        {
            return NormalizeArr(array.AsSpan(), negated, extraRedundantCheck);
        }
        if (expr is List<ContextKeyExpression?> list)
        {
            return NormalizeArr(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list), negated, extraRedundantCheck);
        }
        var copy = new ContextKeyExpression?[expr.Count];
        for (var i = 0; i < copy.Length; i++)
        {
            copy[i] = expr[i];
        }
        return NormalizeArr(copy.AsSpan(), negated, extraRedundantCheck);
    }

    public static ContextKeyExpression? Create(ReadOnlySpan<ContextKeyExpression?> expr, ContextKeyExpression? negated, bool extraRedundantCheck)
    {
        return NormalizeArr(expr, negated, extraRedundantCheck);
    }

    private ContextKeyAndExpr(ContextKeyExpression[] expr, ContextKeyExpression? negated)
    {
        _expr = expr;
        _negated = negated;
    }

    /// <summary>The terms, sorted.</summary>
    public IReadOnlyList<ContextKeyExpression> Expr => _expr;

    public override ContextKeyExprType Type => ContextKeyExprType.And;

    public override int Cmp(ContextKeyExpression other)
    {
        if (other.Type != Type)
        {
            return Type - other.Type;
        }
        var o = (ContextKeyAndExpr)other;
        if (_expr.Length < o._expr.Length)
        {
            return -1;
        }
        if (_expr.Length > o._expr.Length)
        {
            return 1;
        }
        for (var i = 0; i < _expr.Length; i++)
        {
            var r = ContextKeyExpressions.Cmp(_expr[i], o._expr[i]);
            if (r != 0)
            {
                return r;
            }
        }
        return 0;
    }

    public override bool Equals(ContextKeyExpression other)
    {
        if (other.Type == Type)
        {
            var o = (ContextKeyAndExpr)other;
            if (_expr.Length != o._expr.Length)
            {
                return false;
            }
            for (var i = 0; i < _expr.Length; i++)
            {
                if (!_expr[i].Equals(o._expr[i]))
                {
                    return false;
                }
            }
            return true;
        }
        return false;
    }

    public override ContextKeyExpression? SubstituteConstants()
    {
        var exprArr = EliminateConstantsInArray(_expr);
        if (exprArr is null)
        {
            // no change
            return this;
        }
        return Create(exprArr, _negated, false);
    }

    /// <summary>The terms with their constants substituted, or null when nothing changed.</summary>
    internal static ContextKeyExpression?[]? EliminateConstantsInArray(ContextKeyExpression[] arr)
    {
        // Allocate array only if there is a difference
        ContextKeyExpression?[]? newArr = null;
        for (var i = 0; i < arr.Length; i++)
        {
            var newExpr = arr[i].SubstituteConstants();

            if (!ReferenceEquals(arr[i], newExpr))
            {
                // something has changed!

                // allocate array on first difference
                if (newArr is null)
                {
                    newArr = new ContextKeyExpression?[arr.Length];
                    for (var j = 0; j < i; j++)
                    {
                        newArr[j] = arr[j];
                    }
                }
            }

            if (newArr is not null)
            {
                newArr[i] = newExpr;
            }
        }

        return newArr;
    }

    public override bool Evaluate(IContext context)
    {
        for (var i = 0; i < _expr.Length; i++)
        {
            if (!_expr[i].Evaluate(context))
            {
                return false;
            }
        }
        return true;
    }

    private static ContextKeyExpression? NormalizeArr(ReadOnlySpan<ContextKeyExpression?> arr, ContextKeyExpression? negated, bool extraRedundantCheck)
    {
        var expr = new List<ContextKeyExpression>(arr.Length);
        var hasTrue = false;

        foreach (var e in arr)
        {
            if (e is null)
            {
                continue;
            }

            if (e.Type == ContextKeyExprType.True)
            {
                // anything && true ==> anything
                hasTrue = true;
                continue;
            }

            if (e.Type == ContextKeyExprType.False)
            {
                // anything && false ==> false
                return ContextKeyFalseExpr.INSTANCE;
            }

            if (e is ContextKeyAndExpr and)
            {
                expr.AddRange(and._expr);
                continue;
            }

            expr.Add(e);
        }

        if (expr.Count == 0 && hasTrue)
        {
            return ContextKeyTrueExpr.INSTANCE;
        }

        if (expr.Count == 0)
        {
            return null;
        }

        if (expr.Count == 1)
        {
            return expr[0];
        }

        ContextKeyExpressions.StableSort(expr);

        // eliminate duplicate terms
        for (var i = 1; i < expr.Count; i++)
        {
            if (expr[i - 1].Equals(expr[i]))
            {
                expr.RemoveAt(i);
                i--;
            }
        }

        if (expr.Count == 1)
        {
            return expr[0];
        }

        // We must distribute any OR expression because we don't support parens
        // OR extensions sort near the end, but `in` and comparison terms sort after them
        while (expr.Count > 1)
        {
            var orIndex = expr.FindLastIndex(static e => e.Type == ContextKeyExprType.Or);
            if (orIndex == -1)
            {
                break;
            }
            // take out the last OR element
            var lastElement = (ContextKeyOrExpr)expr[orIndex];
            expr.RemoveAt(orIndex);

            // pop the second to last element
            var secondToLastElement = expr[^1];
            expr.RemoveAt(expr.Count - 1);

            var isFinished = expr.Count == 0;

            // distribute `lastElement` over `secondToLastElement`
            var distributed = new ContextKeyExpression?[lastElement.Expr.Count];
            for (var i = 0; i < distributed.Length; i++)
            {
                distributed[i] = Create(new[] { lastElement.Expr[i], secondToLastElement }, null, extraRedundantCheck);
            }
            var resultElement = ContextKeyOrExpr.Create(distributed, null, isFinished);

            if (resultElement is not null)
            {
                if (resultElement.Type == ContextKeyExprType.False)
                {
                    // anything && false ==> false
                    return ContextKeyFalseExpr.INSTANCE;
                }
                expr.Add(resultElement);
                ContextKeyExpressions.StableSort(expr);
            }
        }

        if (expr.Count == 1)
        {
            return expr[0];
        }

        if (expr.Exists(static e => e.Type == ContextKeyExprType.And))
        {
            // a distributed OR that collapsed into a single AND must not stay nested
            var terms = new List<ContextKeyExpression>();
            foreach (var e in expr)
            {
                if (e is ContextKeyAndExpr nested)
                {
                    terms.AddRange(nested._expr);
                }
                else
                {
                    terms.Add(e);
                }
            }
            ContextKeyExpressions.StableSort(terms);
            expr.Clear();
            foreach (var term in terms)
            {
                if (expr.Count == 0 || !expr[^1].Equals(term))
                {
                    expr.Add(term);
                }
            }
        }

        // resolve false AND expressions
        if (extraRedundantCheck)
        {
            for (var i = 0; i < expr.Count; i++)
            {
                for (var j = i + 1; j < expr.Count; j++)
                {
                    if (expr[i].Negate().Equals(expr[j]))
                    {
                        // A && !A case
                        return ContextKeyFalseExpr.INSTANCE;
                    }
                }
            }

            if (expr.Count == 1)
            {
                return expr[0];
            }
        }

        return new ContextKeyAndExpr(expr.ToArray(), negated);
    }

    public override string Serialize()
    {
        return string.Join(" && ", _expr.Select(static e => e.Serialize()));
    }

    public override string[] Keys()
    {
        var result = new List<string>();
        foreach (var expr in _expr)
        {
            result.AddRange(expr.Keys());
        }
        return result.ToArray();
    }

    public override ContextKeyExpression Map(IContextKeyExprMapper mapFnc)
    {
        return new ContextKeyAndExpr(_expr.Select(expr => expr.Map(mapFnc)).ToArray(), null);
    }

    public override ContextKeyExpression Negate()
    {
        if (_negated is not null)
        {
            return _negated;
        }
        var result = new ContextKeyExpression?[_expr.Length];
        for (var i = 0; i < _expr.Length; i++)
        {
            result[i] = _expr[i].Negate();
        }
        return CacheNegated(ref _negated, ContextKeyOrExpr.Create(result, this, true)!);
    }
}

internal sealed class ContextKeyOrExpr : ContextKeyExpression
{
    private readonly ContextKeyExpression[] _expr;
    private ContextKeyExpression? _negated;

    public static ContextKeyExpression? Create(IReadOnlyList<ContextKeyExpression?> expr, ContextKeyExpression? negated, bool extraRedundantCheck)
    {
        if (expr is ContextKeyExpression?[] array)
        {
            return NormalizeArr(array.AsSpan(), negated, extraRedundantCheck);
        }
        if (expr is List<ContextKeyExpression?> list)
        {
            return NormalizeArr(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list), negated, extraRedundantCheck);
        }
        var copy = new ContextKeyExpression?[expr.Count];
        for (var i = 0; i < copy.Length; i++)
        {
            copy[i] = expr[i];
        }
        return NormalizeArr(copy.AsSpan(), negated, extraRedundantCheck);
    }

    public static ContextKeyExpression? Create(ReadOnlySpan<ContextKeyExpression?> expr, ContextKeyExpression? negated, bool extraRedundantCheck)
    {
        return NormalizeArr(expr, negated, extraRedundantCheck);
    }

    private ContextKeyOrExpr(ContextKeyExpression[] expr, ContextKeyExpression? negated)
    {
        _expr = expr;
        _negated = negated;
    }

    /// <summary>The terms, sorted.</summary>
    public IReadOnlyList<ContextKeyExpression> Expr => _expr;

    public override ContextKeyExprType Type => ContextKeyExprType.Or;

    public override int Cmp(ContextKeyExpression other)
    {
        if (other.Type != Type)
        {
            return Type - other.Type;
        }
        var o = (ContextKeyOrExpr)other;
        if (_expr.Length < o._expr.Length)
        {
            return -1;
        }
        if (_expr.Length > o._expr.Length)
        {
            return 1;
        }
        for (var i = 0; i < _expr.Length; i++)
        {
            var r = ContextKeyExpressions.Cmp(_expr[i], o._expr[i]);
            if (r != 0)
            {
                return r;
            }
        }
        return 0;
    }

    public override bool Equals(ContextKeyExpression other)
    {
        if (other.Type == Type)
        {
            var o = (ContextKeyOrExpr)other;
            if (_expr.Length != o._expr.Length)
            {
                return false;
            }
            for (var i = 0; i < _expr.Length; i++)
            {
                if (!_expr[i].Equals(o._expr[i]))
                {
                    return false;
                }
            }
            return true;
        }
        return false;
    }

    public override ContextKeyExpression? SubstituteConstants()
    {
        var exprArr = ContextKeyAndExpr.EliminateConstantsInArray(_expr);
        if (exprArr is null)
        {
            // no change
            return this;
        }
        return Create(exprArr, _negated, false);
    }

    public override bool Evaluate(IContext context)
    {
        for (var i = 0; i < _expr.Length; i++)
        {
            if (_expr[i].Evaluate(context))
            {
                return true;
            }
        }
        return false;
    }

    private static ContextKeyExpression? NormalizeArr(ReadOnlySpan<ContextKeyExpression?> arr, ContextKeyExpression? negated, bool extraRedundantCheck)
    {
        var expr = new List<ContextKeyExpression>(arr.Length);
        var hasFalse = false;

        for (var i = 0; i < arr.Length; i++)
        {
            var e = arr[i];
            if (e is null)
            {
                continue;
            }

            if (e.Type == ContextKeyExprType.False)
            {
                // anything || false ==> anything
                hasFalse = true;
                continue;
            }

            if (e.Type == ContextKeyExprType.True)
            {
                // anything || true ==> true
                return ContextKeyTrueExpr.INSTANCE;
            }

            if (e is ContextKeyOrExpr or)
            {
                expr.AddRange(or._expr);
                continue;
            }

            expr.Add(e);
        }

        if (expr.Count == 0 && hasFalse)
        {
            return ContextKeyFalseExpr.INSTANCE;
        }

        ContextKeyExpressions.StableSort(expr);

        if (expr.Count == 0)
        {
            return null;
        }

        if (expr.Count == 1)
        {
            return expr[0];
        }

        // eliminate duplicate terms
        for (var i = 1; i < expr.Count; i++)
        {
            if (expr[i - 1].Equals(expr[i]))
            {
                expr.RemoveAt(i);
                i--;
            }
        }

        if (expr.Count == 1)
        {
            return expr[0];
        }

        // resolve true OR expressions
        if (extraRedundantCheck)
        {
            for (var i = 0; i < expr.Count; i++)
            {
                for (var j = i + 1; j < expr.Count; j++)
                {
                    if (expr[i].Negate().Equals(expr[j]))
                    {
                        // A || !A case
                        return ContextKeyTrueExpr.INSTANCE;
                    }
                }
            }

            if (expr.Count == 1)
            {
                return expr[0];
            }
        }

        return new ContextKeyOrExpr(expr.ToArray(), negated);
    }

    public override string Serialize()
    {
        return string.Join(" || ", _expr.Select(static e => e.Serialize()));
    }

    public override string[] Keys()
    {
        var result = new List<string>();
        foreach (var expr in _expr)
        {
            result.AddRange(expr.Keys());
        }
        return result.ToArray();
    }

    public override ContextKeyExpression Map(IContextKeyExprMapper mapFnc)
    {
        return new ContextKeyOrExpr(_expr.Select(expr => expr.Map(mapFnc)).ToArray(), null);
    }

    public override ContextKeyExpression Negate()
    {
        if (_negated is not null)
        {
            return _negated;
        }
        var result = new List<ContextKeyExpression>();
        foreach (var expr in _expr)
        {
            result.Add(expr.Negate());
        }

        // We don't support parens, so here we distribute the AND over the OR terminals
        // We always take the first 2 AND pairs and distribute them
        while (result.Count > 1)
        {
            var left = result[0];
            var right = result[1];
            result.RemoveRange(0, 2);

            var all = new List<ContextKeyExpression?>();
            foreach (var l in ContextKeyExpressions.GetTerminals(left))
            {
                foreach (var r in ContextKeyExpressions.GetTerminals(right))
                {
                    all.Add(ContextKeyAndExpr.Create(new[] { l, r }, null, false)!);
                }
            }

            result.Insert(0, Create(all, null, false)!);
        }

        return CacheNegated(ref _negated, Create(result, this, true)!);
    }
}
