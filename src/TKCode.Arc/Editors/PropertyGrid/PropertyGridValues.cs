using System.Collections;
using System.Globalization;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The kinds of built-in editor a <see cref="PropertyGrid"/> picks from a property type.</summary>
internal enum PropertyGridEditorKind
{
    /// <summary>The value as muted text (read-only rows and types without an editor).</summary>
    Text,
    String,
    Number,
    Boolean,
    Choice,
    Color,
    Date
}

/// <summary>Conversions between property values and what the built-in editors show (no reflection).</summary>
internal static class PropertyGridValues
{
    /// <summary>Floating-point numbers are kept within ±10¹⁵ unless a range is given: the number editor computes in
    /// decimal, and its step grid needs room for 28 significant digits.</summary>
    private const decimal FloatingLimit = 1_000_000_000_000_000m;

    public const int DefaultFractionDigits = 4;

    public static Type Underlying(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    public static IReadOnlyList<PropertyGridChoice>? EnumChoices(Type type)
    {
        var enumType = Underlying(type);
        if (!enumType.IsEnum)
        {
            return null;
        }
        var choices = new List<PropertyGridChoice>();
        var seen = new HashSet<object>();
        foreach (var raw in Enum.GetValuesAsUnderlyingType(enumType))
        {
            var value = Enum.ToObject(enumType, raw!);
            if (seen.Add(value))
            {
                choices.Add(new PropertyGridChoice(value, value.ToString() ?? string.Empty));
            }
        }
        return choices;
    }

    public static PropertyGridEditorKind KindOf(PropertyGridProperty property, Type type)
    {
        if (property.Choices is { Count: > 0 })
        {
            return PropertyGridEditorKind.Choice;
        }
        var underlying = Underlying(type);
        if (underlying.IsEnum)
        {
            return PropertyGridEditorKind.Choice;
        }
        if (underlying == typeof(bool))
        {
            return PropertyGridEditorKind.Boolean;
        }
        if (IsNumber(underlying))
        {
            return PropertyGridEditorKind.Number;
        }
        if (underlying == typeof(Color))
        {
            return PropertyGridEditorKind.Color;
        }
        if (underlying == typeof(DateOnly) || underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset))
        {
            return PropertyGridEditorKind.Date;
        }
        if (underlying == typeof(string) || underlying == typeof(char) || underlying == typeof(Guid)
            || underlying == typeof(TimeSpan) || underlying == typeof(Version) || underlying == typeof(Uri))
        {
            return PropertyGridEditorKind.String;
        }
        return PropertyGridEditorKind.Text;
    }

    public static bool IsNumber(Type type) => Type.GetTypeCode(type) is TypeCode.Byte or TypeCode.SByte or TypeCode.Int16
        or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single
        or TypeCode.Double or TypeCode.Decimal && !type.IsEnum;

    public static bool IsWholeNumber(Type type) => Type.GetTypeCode(Underlying(type)) is TypeCode.Byte or TypeCode.SByte
        or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64;

    /// <summary>The limits of a number type, narrowed by the property's range.</summary>
    public static (decimal Minimum, decimal Maximum) Limits(PropertyGridProperty property, Type type)
    {
        var (minimum, maximum) = Type.GetTypeCode(Underlying(type)) switch
        {
            TypeCode.Byte => (byte.MinValue, byte.MaxValue),
            TypeCode.SByte => (sbyte.MinValue, sbyte.MaxValue),
            TypeCode.Int16 => (short.MinValue, short.MaxValue),
            TypeCode.UInt16 => (ushort.MinValue, ushort.MaxValue),
            TypeCode.Int32 => (int.MinValue, int.MaxValue),
            TypeCode.UInt32 => (uint.MinValue, uint.MaxValue),
            TypeCode.Int64 => (long.MinValue, long.MaxValue),
            TypeCode.UInt64 => (ulong.MinValue, (decimal)ulong.MaxValue),
            _ => (-FloatingLimit, FloatingLimit)
        };
        if (property.Minimum is { } min && !double.IsNaN(min))
        {
            minimum = Math.Max(minimum, ToDecimal(min, minimum));
        }
        if (property.Maximum is { } max && !double.IsNaN(max))
        {
            maximum = Math.Min(maximum, ToDecimal(max, maximum));
        }
        return (minimum, Math.Max(minimum, maximum));
    }

    public static int FractionDigits(PropertyGridProperty property, Type type)
        => IsWholeNumber(type) ? 0 : Math.Clamp(property.FractionDigits ?? DefaultFractionDigits, 0, 12);

    public static decimal Step(PropertyGridProperty property, Type type)
    {
        if (property.Step is { } step && step > 0 && !double.IsNaN(step))
        {
            return ToDecimal(step, 1m);
        }
        var digits = FractionDigits(property, type);
        var unit = 1m;
        for (var i = 0; i < digits; i++)
        {
            unit /= 10m;
        }
        return unit;
    }

    private static decimal ToDecimal(double value, decimal fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > (double)FloatingLimit * 1000)
        {
            return fallback;
        }
        return (decimal)value;
    }

    /// <summary>A number value as decimal for the number editor; out-of-range doubles are clamped.</summary>
    public static decimal NumberOf(object? value) => value switch
    {
        null => 0m,
        double d => double.IsNaN(d) ? 0m : d >= (double)FloatingLimit ? FloatingLimit : d <= -(double)FloatingLimit ? -FloatingLimit : (decimal)d,
        float f => float.IsNaN(f) ? 0m : f >= (float)FloatingLimit ? FloatingLimit : f <= -(float)FloatingLimit ? -FloatingLimit : (decimal)f,
        IConvertible convertible => convertible.ToDecimal(CultureInfo.InvariantCulture),
        _ => 0m
    };

    /// <summary>Converts <paramref name="value"/> to <paramref name="type"/>; false when it cannot.</summary>
    public static bool TryConvert(object? value, Type type, out object? result)
    {
        var underlying = Underlying(type);
        if (value is null)
        {
            result = null;
            return !type.IsValueType || underlying != type;
        }
        if (type.IsInstanceOfType(value) || underlying.IsInstanceOfType(value))
        {
            result = value;
            return true;
        }
        try
        {
            if (underlying.IsEnum)
            {
                if (value is string name && Enum.TryParse(underlying, name, ignoreCase: true, out var parsed))
                {
                    result = parsed;
                    return true;
                }
                if (IsNumber(value.GetType()))
                {
                    result = Enum.ToObject(underlying, value);
                    return true;
                }
                result = null;
                return false;
            }
            if (underlying == typeof(string))
            {
                result = Format(value, null);
                return true;
            }
            if (underlying == typeof(DateOnly))
            {
                result = value switch
                {
                    DateTime dateTime => DateOnly.FromDateTime(dateTime),
                    DateTimeOffset offset => DateOnly.FromDateTime(offset.Date),
                    _ => null
                };
                return result is not null;
            }
            if (underlying == typeof(DateTime))
            {
                result = value switch
                {
                    DateOnly date => date.ToDateTime(TimeOnly.MinValue),
                    DateTimeOffset offset => offset.DateTime,
                    _ => null
                };
                return result is not null;
            }
            if (underlying == typeof(DateTimeOffset))
            {
                result = value switch
                {
                    DateOnly date => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue)),
                    DateTime dateTime => new DateTimeOffset(dateTime),
                    _ => null
                };
                return result is not null;
            }
            if (value is string text)
            {
                return TryParse(text, underlying, out result);
            }
            if (value is IConvertible && Type.GetTypeCode(underlying) is not TypeCode.Object and not TypeCode.DBNull)
            {
                result = Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
                return true;
            }
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
        }
        result = null;
        return false;
    }

    /// <summary>Parses what was typed into the text editor.</summary>
    public static bool TryParse(string text, Type type, out object? result)
    {
        var underlying = Underlying(type);
        var culture = CultureInfo.CurrentCulture;
        result = null;
        if (underlying == typeof(string))
        {
            result = text;
            return true;
        }
        if (text.Length == 0 && underlying != type)
        {
            return true;
        }
        if (underlying == typeof(char))
        {
            if (text.Length != 1)
            {
                return false;
            }
            result = text[0];
            return true;
        }
        if (underlying == typeof(Guid))
        {
            var ok = Guid.TryParse(text, out var guid);
            result = guid;
            return ok;
        }
        if (underlying == typeof(TimeSpan))
        {
            var ok = TimeSpan.TryParse(text, culture, out var span);
            result = span;
            return ok;
        }
        if (underlying == typeof(Version))
        {
            var ok = Version.TryParse(text, out var version);
            result = version;
            return ok;
        }
        if (underlying == typeof(Uri))
        {
            var ok = Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out var uri);
            result = uri;
            return ok;
        }
        if (Type.GetTypeCode(underlying) is not TypeCode.Object and not TypeCode.DBNull)
        {
            try
            {
                result = Convert.ChangeType(text, underlying, culture);
                return true;
            }
            catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
            {
                return false;
            }
        }
        return false;
    }

    /// <summary>The text a value shows when no editor shows it.</summary>
    public static string Format(object? value, PropertyGridProperty? property)
    {
        switch (value)
        {
            case null:
                return string.Empty;
            case string text:
                return text;
            case bool flag:
                return flag ? "True" : "False";
            case double or float or decimal:
            {
                var digits = property is null ? DefaultFractionDigits : FractionDigits(property, value.GetType());
                return ((IFormattable)value).ToString("F" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.CurrentCulture);
            }
            case Color color:
                return color.A == 255 ? $"#{color.R:X2}{color.G:X2}{color.B:X2}" : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            case DateOnly date:
                return date.ToString("d", CultureInfo.CurrentCulture);
            case DateTime dateTime:
                return dateTime.TimeOfDay == TimeSpan.Zero
                    ? dateTime.ToString("d", CultureInfo.CurrentCulture)
                    : dateTime.ToString("g", CultureInfo.CurrentCulture);
            case IFormattable formattable:
                return formattable.ToString(null, CultureInfo.CurrentCulture);
            case ICollection collection:
                return collection.Count.ToString(CultureInfo.CurrentCulture);
            default:
                return value.ToString() ?? string.Empty;
        }
    }

    /// <summary>Equality used to tell whether several selected objects share a value.</summary>
    public static bool Same(object? a, object? b) => ReferenceEquals(a, b) || a is not null && a.Equals(b);
}
