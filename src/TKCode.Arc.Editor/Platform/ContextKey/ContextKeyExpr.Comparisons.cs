// Ported from vscode (src/vs/platform/contextkey/common/contextkey.ts: withFloatOrStr, ContextKeyGreaterExpr,
// ContextKeyGreaterEqualsExpr, ContextKeySmallerExpr, ContextKeySmallerEqualsExpr, ContextKeyRegexExpr,
// ContextKeyNotRegexExpr, commit b57d5c9e), MIT license. Differences from upstream (see also ContextKeyExpr.cs):
//  - the value of a comparison is a double or a string (upstream: number | string); parseFloat is
//    ContextKeyValues.ParseFloat and Serialize prints numbers as JavaScript does.
//  - the four comparisons share an abstract ContextKeyComparisonExpr (upstream repeats the same members in each class).
//  - a regular expression is a JsRegExp (JsRegExp.cs explains how it maps JavaScript's RegExp onto .NET).

namespace TKCode.Arc.Editor.Platform;

/// <summary>The shared shape of <c>&gt;</c>, <c>&gt;=</c>, <c>&lt;</c> and <c>&lt;=</c>.</summary>
internal abstract class ContextKeyComparisonExpr : ContextKeyExpression
{
    private ContextKeyExpression? _negated;

    protected ContextKeyComparisonExpr(string key, object value, ContextKeyExpression? negated)
    {
        Key = key;
        Value = value;
        _negated = negated;
    }

    protected string Key { get; }

    /// <summary>A double or a string.</summary>
    protected object Value { get; }

    protected abstract string Operator { get; }

    /// <summary>
    /// withFloatOrStr: a string that parses as a float becomes the number; a number or string goes to
    /// <paramref name="callback"/>, anything else is false.
    /// </summary>
    protected static ContextKeyExpression WithFloatOrStr(object? value, Func<object, ContextKeyExpression> callback)
    {
        if (value is string s)
        {
            var n = ContextKeyValues.ParseFloat(s);
            if (!double.IsNaN(n))
            {
                return callback(n);
            }
            return callback(s);
        }
        if (ContextKeyValues.TryGetNumber(value, out var number))
        {
            return callback(number);
        }
        return ContextKeyFalseExpr.INSTANCE;
    }

    public sealed override int Cmp(ContextKeyExpression other)
    {
        if (other.Type != Type)
        {
            return Type - other.Type;
        }
        var o = (ContextKeyComparisonExpr)other;
        return ContextKeyExpressions.Cmp2(Key, Value, o.Key, o.Value);
    }

    public sealed override bool Equals(ContextKeyExpression other)
    {
        if (other.Type == Type)
        {
            var o = (ContextKeyComparisonExpr)other;
            return Key == o.Key && ContextKeyValues.StrictEquals(Value, o.Value);
        }
        return false;
    }

    public sealed override ContextKeyExpression? SubstituteConstants()
    {
        return this;
    }

    public sealed override bool Evaluate(IContext context)
    {
        if (Value is not double value)
        {
            return false;
        }
        return Compare(ContextKeyValues.ParseFloat(context.GetValue(Key)), value);
    }

    protected abstract bool Compare(double actual, double value);

    public sealed override string Serialize()
    {
        return $"{Key} {Operator} {ContextKeyValues.ToJsString(Value)}";
    }

    public sealed override string[] Keys()
    {
        return [Key];
    }

    public sealed override ContextKeyExpression Negate()
    {
        return _negated ?? CacheNegated(ref _negated, CreateNegated());
    }

    protected abstract ContextKeyExpression CreateNegated();
}

internal sealed class ContextKeyGreaterExpr : ContextKeyComparisonExpr
{
    public static ContextKeyExpression Create(string key, object? value, ContextKeyExpression? negated = null)
    {
        return WithFloatOrStr(value, v => new ContextKeyGreaterExpr(key, v, negated));
    }

    private ContextKeyGreaterExpr(string key, object value, ContextKeyExpression? negated)
        : base(key, value, negated)
    {
    }

    public override ContextKeyExprType Type => ContextKeyExprType.Greater;

    protected override string Operator => ">";

    protected override bool Compare(double actual, double value) => actual > value;

    public override ContextKeyExpression Map(IContextKeyExprMapper mapFnc)
    {
        return mapFnc.MapGreater(Key, Value);
    }

    protected override ContextKeyExpression CreateNegated() => ContextKeySmallerEqualsExpr.Create(Key, Value, this);
}

internal sealed class ContextKeyGreaterEqualsExpr : ContextKeyComparisonExpr
{
    public static ContextKeyExpression Create(string key, object? value, ContextKeyExpression? negated = null)
    {
        return WithFloatOrStr(value, v => new ContextKeyGreaterEqualsExpr(key, v, negated));
    }

    private ContextKeyGreaterEqualsExpr(string key, object value, ContextKeyExpression? negated)
        : base(key, value, negated)
    {
    }

    public override ContextKeyExprType Type => ContextKeyExprType.GreaterEquals;

    protected override string Operator => ">=";

    protected override bool Compare(double actual, double value) => actual >= value;

    public override ContextKeyExpression Map(IContextKeyExprMapper mapFnc)
    {
        return mapFnc.MapGreaterEquals(Key, Value);
    }

    protected override ContextKeyExpression CreateNegated() => ContextKeySmallerExpr.Create(Key, Value, this);
}

internal sealed class ContextKeySmallerExpr : ContextKeyComparisonExpr
{
    public static ContextKeyExpression Create(string key, object? value, ContextKeyExpression? negated = null)
    {
        return WithFloatOrStr(value, v => new ContextKeySmallerExpr(key, v, negated));
    }

    private ContextKeySmallerExpr(string key, object value, ContextKeyExpression? negated)
        : base(key, value, negated)
    {
    }

    public override ContextKeyExprType Type => ContextKeyExprType.Smaller;

    protected override string Operator => "<";

    protected override bool Compare(double actual, double value) => actual < value;

    public override ContextKeyExpression Map(IContextKeyExprMapper mapFnc)
    {
        return mapFnc.MapSmaller(Key, Value);
    }

    protected override ContextKeyExpression CreateNegated() => ContextKeyGreaterEqualsExpr.Create(Key, Value, this);
}

internal sealed class ContextKeySmallerEqualsExpr : ContextKeyComparisonExpr
{
    public static ContextKeyExpression Create(string key, object? value, ContextKeyExpression? negated = null)
    {
        return WithFloatOrStr(value, v => new ContextKeySmallerEqualsExpr(key, v, negated));
    }

    private ContextKeySmallerEqualsExpr(string key, object value, ContextKeyExpression? negated)
        : base(key, value, negated)
    {
    }

    public override ContextKeyExprType Type => ContextKeyExprType.SmallerEquals;

    protected override string Operator => "<=";

    protected override bool Compare(double actual, double value) => actual <= value;

    public override ContextKeyExpression Map(IContextKeyExprMapper mapFnc)
    {
        return mapFnc.MapSmallerEquals(Key, Value);
    }

    protected override ContextKeyExpression CreateNegated() => ContextKeyGreaterExpr.Create(Key, Value, this);
}

internal sealed class ContextKeyRegexExpr : ContextKeyExpression
{
    private readonly string _key;
    private readonly JsRegExp? _regexp;
    private ContextKeyExpression? _negated;

    public static ContextKeyRegexExpr Create(string key, JsRegExp? regexp)
    {
        return new ContextKeyRegexExpr(key, regexp);
    }

    private ContextKeyRegexExpr(string key, JsRegExp? regexp)
    {
        _key = key;
        _regexp = regexp;
    }

    public override ContextKeyExprType Type => ContextKeyExprType.Regex;

    public override int Cmp(ContextKeyExpression other)
    {
        if (other.Type != Type)
        {
            return Type - other.Type;
        }
        var o = (ContextKeyRegexExpr)other;
        var r = ContextKeyExpressions.Cmp1(_key, o._key);
        if (r != 0)
        {
            return r;
        }
        var thisSource = _regexp?.Source ?? "";
        var otherSource = o._regexp?.Source ?? "";
        return ContextKeyExpressions.Cmp1(thisSource, otherSource);
    }

    public override bool Equals(ContextKeyExpression other)
    {
        if (other.Type == Type)
        {
            var o = (ContextKeyRegexExpr)other;
            var thisSource = _regexp?.Source ?? "";
            var otherSource = o._regexp?.Source ?? "";
            return _key == o._key && thisSource == otherSource;
        }
        return false;
    }

    public override ContextKeyExpression? SubstituteConstants()
    {
        return this;
    }

    public override bool Evaluate(IContext context)
    {
        var value = context.GetValue(_key);
        return _regexp is not null && _regexp.Test(value);
    }

    public override string Serialize()
    {
        var value = _regexp is not null
            ? $"/{_regexp.Source}/{_regexp.Flags}"
            : "/invalid/";
        return $"{_key} =~ {value}";
    }

    public override string[] Keys()
    {
        return [_key];
    }

    public override ContextKeyRegexExpr Map(IContextKeyExprMapper mapFnc)
    {
        return mapFnc.MapRegex(_key, _regexp);
    }

    public override ContextKeyExpression Negate()
    {
        return _negated ?? CacheNegated(ref _negated, ContextKeyNotRegexExpr.Create(this));
    }
}

internal sealed class ContextKeyNotRegexExpr : ContextKeyExpression
{
    private readonly ContextKeyRegexExpr _actual;

    public static ContextKeyExpression Create(ContextKeyRegexExpr actual)
    {
        return new ContextKeyNotRegexExpr(actual);
    }

    private ContextKeyNotRegexExpr(ContextKeyRegexExpr actual)
    {
        _actual = actual;
    }

    public override ContextKeyExprType Type => ContextKeyExprType.NotRegex;

    public override int Cmp(ContextKeyExpression other)
    {
        if (other.Type != Type)
        {
            return Type - other.Type;
        }
        return _actual.Cmp(((ContextKeyNotRegexExpr)other)._actual);
    }

    public override bool Equals(ContextKeyExpression other)
    {
        if (other.Type == Type)
        {
            return _actual.Equals(((ContextKeyNotRegexExpr)other)._actual);
        }
        return false;
    }

    public override ContextKeyExpression? SubstituteConstants()
    {
        return this;
    }

    public override bool Evaluate(IContext context)
    {
        return !_actual.Evaluate(context);
    }

    public override string Serialize()
    {
        return $"!({_actual.Serialize()})";
    }

    public override string[] Keys()
    {
        return _actual.Keys();
    }

    public override ContextKeyExpression Map(IContextKeyExprMapper mapFnc)
    {
        return new ContextKeyNotRegexExpr(_actual.Map(mapFnc));
    }

    public override ContextKeyExpression Negate()
    {
        return _actual;
    }
}
