using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TKCode.Arc.A2UI;

/// <summary>The renderer-side implementations of the basic catalog's functions.</summary>
internal static partial class A2UIBasicFunctions
{
    public static void ImplementAll(A2UICatalog catalog)
    {
        Implement(catalog, "required", Required);
        Implement(catalog, "regex", RegexCheck);
        Implement(catalog, "length", Length);
        Implement(catalog, "numeric", Numeric);
        Implement(catalog, "email", Email);
        Implement(catalog, "formatString", FormatString);
        Implement(catalog, "formatNumber", FormatNumber);
        Implement(catalog, "formatCurrency", FormatCurrency);
        Implement(catalog, "formatDate", FormatDate);
        Implement(catalog, "pluralize", Pluralize);
        Implement(catalog, "openUrl", OpenUrl);
        Implement(catalog, "and", And);
        Implement(catalog, "or", Or);
        Implement(catalog, "not", Not);
    }

    private static void Implement(A2UICatalog catalog, string name, A2UIFunction function)
    {
        if (catalog.Functions.ContainsKey(name))
        {
            catalog.Implement(name, function);
        }
    }

    /// <summary>A validation result object: <c>{"valid": bool}</c> plus an optional code and message.</summary>
    public static JsonObject Result(bool valid, string? code = null, string? message = null)
    {
        var result = new JsonObject { ["valid"] = valid };
        if (!valid && code is not null)
        {
            result["code"] = code;
        }

        if (!valid && message is not null)
        {
            result["message"] = message;
        }

        return result;
    }

    private static JsonNode? Arg(IReadOnlyDictionary<string, JsonNode?> args, string name)
        => args.TryGetValue(name, out var value) ? value : null;

    private static JsonNode Required(A2UIFunctionContext context, IReadOnlyDictionary<string, JsonNode?> args)
    {
        var value = Arg(args, "value");
        var present = value switch
        {
            null => false,
            JsonArray array => array.Count > 0,
            JsonObject => true,
            _ => A2UIJson.AsString(value) is not { Length: 0 }
        };
        return Result(present, "required", "This field is required.");
    }

    private static JsonNode RegexCheck(A2UIFunctionContext context, IReadOnlyDictionary<string, JsonNode?> args)
    {
        var pattern = A2UIJson.AsString(Arg(args, "pattern")) ?? throw A2UIException.Expression("'pattern' must be a string.");
        var value = A2UIJson.ToDisplayString(Arg(args, "value"));
        return Result(A2UIRegex.IsMatch(pattern, value), "pattern", "The value does not have the expected format.");
    }

    private static JsonNode Length(A2UIFunctionContext context, IReadOnlyDictionary<string, JsonNode?> args)
    {
        var value = Arg(args, "value");
        var length = value switch
        {
            null => 0,
            JsonArray array => array.Count,
            _ => A2UIJson.ToDisplayString(value).EnumerateRunes().Count()
        };
        var ok = true;
        if (A2UIJson.TryGetDouble(Arg(args, "min"), out var min) && length < min)
        {
            ok = false;
        }

        if (A2UIJson.TryGetDouble(Arg(args, "max"), out var max) && length > max)
        {
            ok = false;
        }

        return Result(ok, "length", "The value has the wrong length.");
    }

    private static JsonNode Numeric(A2UIFunctionContext context, IReadOnlyDictionary<string, JsonNode?> args)
    {
        var value = Arg(args, "value");
        double number;
        if (A2UIJson.IsNumber(value))
        {
            A2UIJson.TryGetDouble(value, out number);
        }
        else if (A2UIJson.AsString(value) is { } text
                 && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
        {
        }
        else
        {
            return Result(false, "numeric", "Enter a number.");
        }

        var ok = !double.IsNaN(number);
        if (A2UIJson.TryGetDouble(Arg(args, "min"), out var min) && number < min)
        {
            ok = false;
        }

        if (A2UIJson.TryGetDouble(Arg(args, "max"), out var max) && number > max)
        {
            ok = false;
        }

        return Result(ok, "numeric", "The number is out of range.");
    }

    [GeneratedRegex(@"^[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)*\.[A-Za-z]{2,}$", RegexOptions.CultureInvariant, 200)]
    private static partial Regex EmailPattern();

    private static JsonNode Email(A2UIFunctionContext context, IReadOnlyDictionary<string, JsonNode?> args)
    {
        var text = A2UIJson.AsString(Arg(args, "value")) ?? string.Empty;
        var ok = text.Length <= 254 && EmailPattern().IsMatch(text);
        return Result(ok, "email", "Enter a valid email address.");
    }

    private static JsonNode FormatString(A2UIFunctionContext context, IReadOnlyDictionary<string, JsonNode?> args)
    {
        var value = Arg(args, "value");
        if (value is not null && !A2UIJson.IsString(value))
        {
            return JsonValue.Create(A2UIJson.ToDisplayString(value));
        }

        var template = A2UIJson.AsString(value) ?? string.Empty;
        return JsonValue.Create(context.Evaluator.FormatTemplate(template, context.Scope, context.Evaluation));
    }

    private static JsonNode? FormatNumber(A2UIFunctionContext context, IReadOnlyDictionary<string, JsonNode?> args)
    {
        if (!A2UIJson.TryGetDouble(Arg(args, "value"), out var value))
        {
            return JsonValue.Create(string.Empty);
        }

        int? decimals = A2UIJson.TryGetDouble(Arg(args, "decimals"), out var d) ? (int)Math.Round(d) : null;
        var grouping = Arg(args, "grouping") is not { } g || A2UIJson.ToBoolean(g);
        return JsonValue.Create(A2UIEvaluator.FormatNumberText(value, decimals, grouping, context.Culture));
    }

    private static JsonNode? FormatCurrency(A2UIFunctionContext context, IReadOnlyDictionary<string, JsonNode?> args)
    {
        if (!A2UIJson.TryGetDouble(Arg(args, "value"), out var value))
        {
            return JsonValue.Create(string.Empty);
        }

        var code = (A2UIJson.AsString(Arg(args, "currency")) ?? string.Empty).Trim().ToUpperInvariant();
        int? decimals = A2UIJson.TryGetDouble(Arg(args, "decimals"), out var d) ? (int)Math.Round(d) : null;
        var grouping = Arg(args, "grouping") is not { } g || A2UIJson.ToBoolean(g);
        return JsonValue.Create(A2UICurrency.Format(value, code, decimals, grouping, context.Culture));
    }

    private static JsonNode? FormatDate(A2UIFunctionContext context, IReadOnlyDictionary<string, JsonNode?> args)
    {
        var format = A2UIJson.AsString(Arg(args, "format")) ?? "yyyy-MM-dd";
        var value = Arg(args, "value");
        if (!A2UIDateFormat.TryParse(value, out var date, out var original))
        {
            return JsonValue.Create(string.Empty);
        }

        if (format == "ISO")
        {
            return JsonValue.Create(original ?? date.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture));
        }

        return JsonValue.Create(A2UIDateFormat.Format(date, format, context.Culture));
    }

    private static JsonNode? Pluralize(A2UIFunctionContext context, IReadOnlyDictionary<string, JsonNode?> args)
    {
        A2UIJson.TryGetDouble(Arg(args, "value"), out var count);
        string? Pick(string name) => A2UIJson.AsString(Arg(args, name));

        // Exact counts win when the agent gave a string for them (so "zero" works in English too).
        if (count == 0 && Pick("zero") is { } zero)
        {
            return JsonValue.Create(zero);
        }

        if (count == 1 && Pick("one") is { } one)
        {
            return JsonValue.Create(one);
        }

        if (count == 2 && Pick("two") is { } two)
        {
            return JsonValue.Create(two);
        }

        var category = A2UIPluralRules.Category(context.Culture, count);
        return JsonValue.Create(Pick(category) ?? Pick("other") ?? string.Empty);
    }

    private static JsonNode? OpenUrl(A2UIFunctionContext context, IReadOnlyDictionary<string, JsonNode?> args)
    {
        var text = A2UIJson.AsString(Arg(args, "url"));
        if (text is null || !Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw A2UIException.Expression("openUrl only opens http and https URLs.", context.Surface?.Id);
        }

        context.RequestOpenUrl(uri);
        return null;
    }

    private static List<bool> Values(IReadOnlyDictionary<string, JsonNode?> args)
    {
        if (Arg(args, "values") is not JsonArray array || array.Count < 2)
        {
            throw A2UIException.Expression("'values' must be a list of at least two values.");
        }

        return array.Select(A2UIJson.ToBoolean).ToList();
    }

    private static JsonNode And(A2UIFunctionContext context, IReadOnlyDictionary<string, JsonNode?> args)
        => JsonValue.Create(Values(args).All(v => v));

    private static JsonNode Or(A2UIFunctionContext context, IReadOnlyDictionary<string, JsonNode?> args)
        => JsonValue.Create(Values(args).Any(v => v));

    private static JsonNode Not(A2UIFunctionContext context, IReadOnlyDictionary<string, JsonNode?> args)
        => JsonValue.Create(!A2UIJson.ToBoolean(Arg(args, "value")));
}
