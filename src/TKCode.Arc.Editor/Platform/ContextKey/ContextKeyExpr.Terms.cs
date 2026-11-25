// Ported from vscode (src/vs/platform/contextkey/common/contextkey.ts: ContextKeyFalseExpr, ContextKeyTrueExpr,
// ContextKeyDefinedExpr, ContextKeyEqualsExpr, ContextKeyInExpr, ContextKeyNotInExpr, ContextKeyNotEqualsExpr,
// ContextKeyNotExpr, commit b57d5c9e), MIT license. Differences from upstream (see also ContextKeyExpr.cs):
//  - `==` and `!=` in Evaluate are JavaScript's loose equality (ContextKeyValues.LooseEquals); Equals compares values
//    with `===` (ContextKeyValues.StrictEquals) and Serialize prints them with String(value).
//  - In and NotIn take an array from any System.Collections.IList and a record from any System.Collections.IDictionary
//    (hasOwnProperty becomes IDictionary.Contains). The Windows file URI comparison is case-insensitive, as upstream
//    (OperatingSystem.IsWindows()).

using System.Collections;

namespace TKCode.Arc.Editor.Platform;

internal sealed class ContextKeyFalseExpr : ContextKeyExpression
{
    public static readonly ContextKeyFalseExpr INSTANCE = new();

    private ContextKeyFalseExpr()
    {
    }

    public override ContextKeyExprType Type => ContextKeyExprType.False;

    public override int Cmp(ContextKeyExpression other)
    {
        return Type - other.Type;
    }

    public override bool Equals(ContextKeyExpression other)
    {
        return other.Type == Type;
    }

    public override ContextKeyExpression? SubstituteConstants()
    {
        return this;
    }

    public override bool Evaluate(IContext context)
    {
        return false;
    }

    public override string Serialize()
    {
        return "false";
    }

    public override string[] Keys()
    {
        return [];
    }

    public override ContextKeyExpression Map(IContextKeyExprMapper mapFnc)
    {
        return this;
    }

    public override ContextKeyExpression Negate()
    {
        return ContextKeyTrueExpr.INSTANCE;
    }
}

internal sealed class ContextKeyTrueExpr : ContextKeyExpression
{
    public static readonly ContextKeyTrueExpr INSTANCE = new();

    private ContextKeyTrueExpr()
    {
    }

    public override ContextKeyExprType Type => ContextKeyExprType.True;

    public override int Cmp(ContextKeyExpression other)
    {
        return Type - other.Type;
    }

    public override bool Equals(ContextKeyExpression other)
    {
        return other.Type == Type;
    }

    public override ContextKeyExpression? SubstituteConstants()
    {
        return this;
    }

    public override bool Evaluate(IContext context)
    {
        return true;
    }

    public override string Serialize()
    {
        return "true";
    }

    public override string[] Keys()
    {
        return [];
    }

    public override ContextKeyExpression Map(IContextKeyExprMapper mapFnc)
    {
        return this;
    }

    public override ContextKeyExpression Negate()
    {
        return ContextKeyFalseExpr.INSTANCE;
    }
}

internal class ContextKeyDefinedExpr : ContextKeyExpression
{
    private ContextKeyExpression? _negated;

    public static ContextKeyExpression Create(string key, ContextKeyExpression? negated = null)
    {
        if (ContextKeyExpressions.TryGetConstant(key, out var constantValue))
        {
            return constantValue ? ContextKeyTrueExpr.INSTANCE : ContextKeyFalseExpr.INSTANCE;
        }
        return new ContextKeyDefinedExpr(key, negated);
    }

    protected ContextKeyDefinedExpr(string key, ContextKeyExpression? negated)
    {
        Key = key;
        _negated = negated;
    }

    public string Key { get; }

    public override ContextKeyExprType Type => ContextKeyExprType.Defined;

    public override int Cmp(ContextKeyExpression other)
    {
        if (other.Type != Type)
        {
            return Type - other.Type;
        }
        return ContextKeyExpressions.Cmp1(Key, ((ContextKeyDefinedExpr)other).Key);
    }

    public override bool Equals(ContextKeyExpression other)
    {
        if (other.Type == Type)
        {
            return Key == ((ContextKeyDefinedExpr)other).Key;
        }
        return false;
    }

    public override ContextKeyExpression? SubstituteConstants()
    {
        if (ContextKeyExpressions.TryGetConstant(Key, out var constantValue))
        {
            return constantValue ? ContextKeyTrueExpr.INSTANCE : ContextKeyFalseExpr.INSTANCE;
        }
        return this;
    }

    public override bool Evaluate(IContext context)
    {
        return ContextKeyValues.IsTruthy(context.GetValue(Key));
    }

    public override string Serialize()
    {
        return Key;
    }

    public override string[] Keys()
    {
        return [Key];
    }

    public override ContextKeyExpression Map(IContextKeyExprMapper mapFnc)
    {
        return mapFnc.MapDefined(Key);
    }

    public override ContextKeyExpression Negate()
    {
        return _negated ?? CacheNegated(ref _negated, ContextKeyNotExpr.Create(Key, this));
    }
}

internal sealed class ContextKeyEqualsExpr : ContextKeyExpression
{
    private readonly string _key;
    private readonly object? _value;
    private ContextKeyExpression? _negated;

    public static ContextKeyExpression Create(string key, object? value, ContextKeyExpression? negated = null)
    {
        if (value is bool b)
        {
            return b ? ContextKeyDefinedExpr.Create(key, negated) : ContextKeyNotExpr.Create(key, negated);
        }
        if (ContextKeyExpressions.TryGetConstant(key, out var constantValue))
        {
            var trueValue = constantValue ? "true" : "false";
            return ContextKeyValues.StrictEquals(value, trueValue) ? ContextKeyTrueExpr.INSTANCE : ContextKeyFalseExpr.INSTANCE;
        }
        return new ContextKeyEqualsExpr(key, value, negated);
    }

    private ContextKeyEqualsExpr(string key, object? value, ContextKeyExpression? negated)
    {
        _key = key;
        _value = value;
        _negated = negated;
    }

    public override ContextKeyExprType Type => ContextKeyExprType.Equals;

    public override int Cmp(ContextKeyExpression other)
    {
        if (other.Type != Type)
        {
            return Type - other.Type;
        }
        var o = (ContextKeyEqualsExpr)other;
        return ContextKeyExpressions.Cmp2(_key, _value, o._key, o._value);
    }

    public override bool Equals(ContextKeyExpression other)
    {
        if (other.Type == Type)
        {
            var o = (ContextKeyEqualsExpr)other;
            return _key == o._key && ContextKeyValues.StrictEquals(_value, o._value);
        }
        return false;
    }

    public override ContextKeyExpression? SubstituteConstants()
    {
        if (ContextKeyExpressions.TryGetConstant(_key, out var constantValue))
        {
            var trueValue = constantValue ? "true" : "false";
            return ContextKeyValues.StrictEquals(_value, trueValue) ? ContextKeyTrueExpr.INSTANCE : ContextKeyFalseExpr.INSTANCE;
        }
        return this;
    }

    public override bool Evaluate(IContext context)
    {
        // Intentional ==
        return ContextKeyValues.LooseEquals(context.GetValue(_key), _value);
    }

    public override string Serialize()
    {
        return $"{_key} == '{ContextKeyValues.ToJsString(_value)}'";
    }

    public override string[] Keys()
    {
        return [_key];
    }

    public override ContextKeyExpression Map(IContextKeyExprMapper mapFnc)
    {
        return mapFnc.MapEquals(_key, _value);
    }

    public override ContextKeyExpression Negate()
    {
        return _negated ?? CacheNegated(ref _negated, ContextKeyNotEqualsExpr.Create(_key, _value, this));
    }
}

internal sealed class ContextKeyInExpr : ContextKeyExpression
{
    private readonly string _key;
    private readonly string _valueKey;
    private ContextKeyExpression? _negated;

    public static ContextKeyInExpr Create(string key, string valueKey)
    {
        return new ContextKeyInExpr(key, valueKey);
    }

    private ContextKeyInExpr(string key, string valueKey)
    {
        _key = key;
        _valueKey = valueKey;
    }

    public override ContextKeyExprType Type => ContextKeyExprType.In;

    public override int Cmp(ContextKeyExpression other)
    {
        if (other.Type != Type)
        {
            return Type - other.Type;
        }
        var o = (ContextKeyInExpr)other;
        return ContextKeyExpressions.Cmp2(_key, _valueKey, o._key, o._valueKey);
    }

    public override bool Equals(ContextKeyExpression other)
    {
        if (other.Type == Type)
        {
            var o = (ContextKeyInExpr)other;
            return _key == o._key && _valueKey == o._valueKey;
        }
        return false;
    }

    public override ContextKeyExpression? SubstituteConstants()
    {
        return this;
    }

    public override bool Evaluate(IContext context)
    {
        var source = context.GetValue(_valueKey);

        var item = context.GetValue(_key);

        if (source is IList list)
        {
            foreach (var element in list)
            {
                if (ContextKeyValues.SameValueZero(element, item))
                {
                    return true;
                }
            }
            // On Windows, file paths are case-insensitive so file URI
            // comparisons must be done in a case-insensitive manner.
            if (OperatingSystem.IsWindows() && item is string itemString && itemString.StartsWith("file:///", StringComparison.Ordinal))
            {
                var itemLower = itemString.ToLowerInvariant();
                foreach (var element in list)
                {
                    if (element is string s && s.ToLowerInvariant() == itemLower)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        if (item is string itemKey && source is IDictionary record)
        {
            if (record.Contains(itemKey))
            {
                return true;
            }
            // On Windows, file paths are case-insensitive so file URI
            // property lookups must be done in a case-insensitive manner.
            if (OperatingSystem.IsWindows() && itemKey.StartsWith("file:///", StringComparison.Ordinal))
            {
                var itemLower = itemKey.ToLowerInvariant();
                foreach (var key in record.Keys)
                {
                    if (key is string s && s.ToLowerInvariant() == itemLower)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        return false;
    }

    public override string Serialize()
    {
        return $"{_key} in '{_valueKey}'";
    }

    public override string[] Keys()
    {
        return [_key, _valueKey];
    }

    public override ContextKeyExpression Map(IContextKeyExprMapper mapFnc)
    {
        return mapFnc.MapIn(_key, _valueKey);
    }

    public override ContextKeyExpression Negate()
    {
        return _negated ?? CacheNegated(ref _negated, ContextKeyNotInExpr.Create(_key, _valueKey));
    }
}

internal sealed class ContextKeyNotInExpr : ContextKeyExpression
{
    private readonly string _key;
    private readonly string _valueKey;
    private readonly ContextKeyInExpr _negated;

    public static ContextKeyNotInExpr Create(string key, string valueKey)
    {
        return new ContextKeyNotInExpr(key, valueKey);
    }

    private ContextKeyNotInExpr(string key, string valueKey)
    {
        _key = key;
        _valueKey = valueKey;
        _negated = ContextKeyInExpr.Create(key, valueKey);
    }

    public override ContextKeyExprType Type => ContextKeyExprType.NotIn;

    public override int Cmp(ContextKeyExpression other)
    {
        if (other.Type != Type)
        {
            return Type - other.Type;
        }
        return _negated.Cmp(((ContextKeyNotInExpr)other)._negated);
    }

    public override bool Equals(ContextKeyExpression other)
    {
        if (other.Type == Type)
        {
            return _negated.Equals(((ContextKeyNotInExpr)other)._negated);
        }
        return false;
    }

    public override ContextKeyExpression? SubstituteConstants()
    {
        return this;
    }

    public override bool Evaluate(IContext context)
    {
        return !_negated.Evaluate(context);
    }

    public override string Serialize()
    {
        return $"{_key} not in '{_valueKey}'";
    }

    public override string[] Keys()
    {
        return _negated.Keys();
    }

    public override ContextKeyExpression Map(IContextKeyExprMapper mapFnc)
    {
        return mapFnc.MapNotIn(_key, _valueKey);
    }

    public override ContextKeyExpression Negate()
    {
        return _negated;
    }
}

internal sealed class ContextKeyNotEqualsExpr : ContextKeyExpression
{
    private readonly string _key;
    private readonly object? _value;
    private ContextKeyExpression? _negated;

    public static ContextKeyExpression Create(string key, object? value, ContextKeyExpression? negated = null)
    {
        if (value is bool b)
        {
            if (b)
            {
                return ContextKeyNotExpr.Create(key, negated);
            }
            return ContextKeyDefinedExpr.Create(key, negated);
        }
        if (ContextKeyExpressions.TryGetConstant(key, out var constantValue))
        {
            var falseValue = constantValue ? "true" : "false";
            return ContextKeyValues.StrictEquals(value, falseValue) ? ContextKeyFalseExpr.INSTANCE : ContextKeyTrueExpr.INSTANCE;
        }
        return new ContextKeyNotEqualsExpr(key, value, negated);
    }

    private ContextKeyNotEqualsExpr(string key, object? value, ContextKeyExpression? negated)
    {
        _key = key;
        _value = value;
        _negated = negated;
    }

    public override ContextKeyExprType Type => ContextKeyExprType.NotEquals;

    public override int Cmp(ContextKeyExpression other)
    {
        if (other.Type != Type)
        {
            return Type - other.Type;
        }
        var o = (ContextKeyNotEqualsExpr)other;
        return ContextKeyExpressions.Cmp2(_key, _value, o._key, o._value);
    }

    public override bool Equals(ContextKeyExpression other)
    {
        if (other.Type == Type)
        {
            var o = (ContextKeyNotEqualsExpr)other;
            return _key == o._key && ContextKeyValues.StrictEquals(_value, o._value);
        }
        return false;
    }

    public override ContextKeyExpression? SubstituteConstants()
    {
        if (ContextKeyExpressions.TryGetConstant(_key, out var constantValue))
        {
            var falseValue = constantValue ? "true" : "false";
            return ContextKeyValues.StrictEquals(_value, falseValue) ? ContextKeyFalseExpr.INSTANCE : ContextKeyTrueExpr.INSTANCE;
        }
        return this;
    }

    public override bool Evaluate(IContext context)
    {
        // Intentional !=
        return !ContextKeyValues.LooseEquals(context.GetValue(_key), _value);
    }

    public override string Serialize()
    {
        return $"{_key} != '{ContextKeyValues.ToJsString(_value)}'";
    }

    public override string[] Keys()
    {
        return [_key];
    }

    public override ContextKeyExpression Map(IContextKeyExprMapper mapFnc)
    {
        return mapFnc.MapNotEquals(_key, _value);
    }

    public override ContextKeyExpression Negate()
    {
        return _negated ?? CacheNegated(ref _negated, ContextKeyEqualsExpr.Create(_key, _value, this));
    }
}

internal sealed class ContextKeyNotExpr : ContextKeyExpression
{
    private readonly string _key;
    private ContextKeyExpression? _negated;

    public static ContextKeyExpression Create(string key, ContextKeyExpression? negated = null)
    {
        if (ContextKeyExpressions.TryGetConstant(key, out var constantValue))
        {
            return constantValue ? ContextKeyFalseExpr.INSTANCE : ContextKeyTrueExpr.INSTANCE;
        }
        return new ContextKeyNotExpr(key, negated);
    }

    private ContextKeyNotExpr(string key, ContextKeyExpression? negated)
    {
        _key = key;
        _negated = negated;
    }

    public override ContextKeyExprType Type => ContextKeyExprType.Not;

    public override int Cmp(ContextKeyExpression other)
    {
        if (other.Type != Type)
        {
            return Type - other.Type;
        }
        return ContextKeyExpressions.Cmp1(_key, ((ContextKeyNotExpr)other)._key);
    }

    public override bool Equals(ContextKeyExpression other)
    {
        if (other.Type == Type)
        {
            return _key == ((ContextKeyNotExpr)other)._key;
        }
        return false;
    }

    public override ContextKeyExpression? SubstituteConstants()
    {
        if (ContextKeyExpressions.TryGetConstant(_key, out var constantValue))
        {
            return constantValue ? ContextKeyFalseExpr.INSTANCE : ContextKeyTrueExpr.INSTANCE;
        }
        return this;
    }

    public override bool Evaluate(IContext context)
    {
        return !ContextKeyValues.IsTruthy(context.GetValue(_key));
    }

    public override string Serialize()
    {
        return $"!{_key}";
    }

    public override string[] Keys()
    {
        return [_key];
    }

    public override ContextKeyExpression Map(IContextKeyExprMapper mapFnc)
    {
        return mapFnc.MapNot(_key);
    }

    public override ContextKeyExpression Negate()
    {
        return _negated ?? CacheNegated(ref _negated, ContextKeyDefinedExpr.Create(_key, this));
    }
}
