using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TKCode.Arc.A2UI;

/// <summary>JSON helpers shared by the protocol layer: equality, cloning, type tests and text conversion. No reflection.</summary>
public static class A2UIJson
{
    private static readonly JsonWriterOptions s_compact = new() { Indented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Structural equality: numbers compare by value (<c>1</c> equals <c>1.0</c>), objects ignore key order.</summary>
    public static bool DeepEquals(JsonNode? a, JsonNode? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        switch (a)
        {
            case JsonObject oa:
                if (b is not JsonObject ob || oa.Count != ob.Count)
                {
                    return false;
                }

                foreach (var (key, value) in oa)
                {
                    if (!ob.TryGetPropertyValue(key, out var other) || !DeepEquals(value, other))
                    {
                        return false;
                    }
                }

                return true;
            case JsonArray aa:
                if (b is not JsonArray ab || aa.Count != ab.Count)
                {
                    return false;
                }

                for (var i = 0; i < aa.Count; i++)
                {
                    if (!DeepEquals(aa[i], ab[i]))
                    {
                        return false;
                    }
                }

                return true;
            default:
                if (b is JsonObject or JsonArray)
                {
                    return false;
                }

                var ka = a.GetValueKind();
                var kb = b.GetValueKind();
                if (ka == JsonValueKind.Number && kb == JsonValueKind.Number)
                {
                    return TryGetDouble(a, out var da) && TryGetDouble(b, out var db) && da.Equals(db);
                }

                if (IsBoolKind(ka) && IsBoolKind(kb))
                {
                    return ka == kb;
                }

                if (ka == JsonValueKind.String && kb == JsonValueKind.String)
                {
                    return string.Equals(a.GetValue<string>(), b.GetValue<string>(), StringComparison.Ordinal);
                }

                return ka == kb && ka == JsonValueKind.Null;
        }
    }

    /// <summary>A deep copy, or null.</summary>
    public static JsonNode? Clone(JsonNode? node) => node?.DeepClone();

    /// <summary>Parses JSON text into a node; a JSON <c>null</c> literal gives null.</summary>
    public static JsonNode? Parse(string json)
    {
        try
        {
            return JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = 256 });
        }
        catch (JsonException ex)
        {
            throw A2UIException.Parse($"Invalid JSON: {ex.Message}", ex);
        }
    }

    /// <summary>Parses UTF-8 JSON into a node.</summary>
    public static JsonNode? Parse(ReadOnlySpan<byte> utf8)
    {
        try
        {
            var reader = new Utf8JsonReader(utf8, new JsonReaderOptions { MaxDepth = 256 });
            return JsonNode.Parse(ref reader);
        }
        catch (JsonException ex)
        {
            throw A2UIException.Parse($"Invalid JSON: {ex.Message}", ex);
        }
    }

    /// <summary>Compact JSON text of a node (<c>null</c> for null).</summary>
    public static string ToJson(JsonNode? node)
    {
        if (node is null)
        {
            return "null";
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, s_compact))
        {
            node.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }

    /// <summary>True when the node is a JSON string.</summary>
    public static bool IsString(JsonNode? node) => node is JsonValue && node.GetValueKind() == JsonValueKind.String;

    /// <summary>True when the node is a JSON number.</summary>
    public static bool IsNumber(JsonNode? node) => node is JsonValue && node.GetValueKind() == JsonValueKind.Number;

    /// <summary>True when the node is a JSON boolean.</summary>
    public static bool IsBoolean(JsonNode? node) => node is JsonValue && IsBoolKind(node.GetValueKind());

    /// <summary>The string value, or null when the node is not a string.</summary>
    public static string? AsString(JsonNode? node) => IsString(node) ? node!.GetValue<string>() : null;

    /// <summary>The boolean value, or null when the node is not a boolean.</summary>
    public static bool? AsBoolean(JsonNode? node) => IsBoolean(node) ? node!.GetValueKind() == JsonValueKind.True : null;

    /// <summary>Reads a JSON number as a double.</summary>
    public static bool TryGetDouble(JsonNode? node, out double value)
    {
        value = 0;
        if (node is not JsonValue v || v.GetValueKind() != JsonValueKind.Number)
        {
            return false;
        }

        if (v.TryGetValue(out double d))
        {
            value = d;
            return true;
        }

        if (v.TryGetValue(out long l))
        {
            value = l;
            return true;
        }

        if (v.TryGetValue(out int i))
        {
            value = i;
            return true;
        }

        if (v.TryGetValue(out decimal m))
        {
            value = (double)m;
            return true;
        }

        if (v.TryGetValue(out JsonElement e) && e.TryGetDouble(out d))
        {
            value = d;
            return true;
        }

        return double.TryParse(ToJson(v), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// The text form used when a value is interpolated into a string: numbers and booleans as JSON text (invariant culture,
    /// <c>-0</c> as <c>0</c>), null as empty, strings as themselves, objects and arrays as compact JSON.
    /// </summary>
    public static string ToDisplayString(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return string.Empty;
            case JsonObject or JsonArray:
                return ToJson(node);
        }

        return node.GetValueKind() switch
        {
            JsonValueKind.String => node.GetValue<string>(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => FormatNumber(node),
            _ => string.Empty
        };
    }

    /// <summary>A number as the shortest round-trip text, without exponent for ordinary magnitudes.</summary>
    public static string FormatNumber(JsonNode node)
    {
        if (!TryGetDouble(node, out var d))
        {
            return ToJson(node);
        }

        return FormatNumber(d);
    }

    /// <summary>A double as JavaScript-like text: integers without a decimal point, <c>-0</c> as <c>0</c>.</summary>
    public static string FormatNumber(double d)
    {
        if (d == 0)
        {
            return "0";
        }

        if (double.IsNaN(d))
        {
            return "NaN";
        }

        if (double.IsInfinity(d))
        {
            return d > 0 ? "Infinity" : "-Infinity";
        }

        if (Math.Abs(d) < 1e21 && d == Math.Floor(d))
        {
            return d.ToString("0", CultureInfo.InvariantCulture);
        }

        return d.ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>A JSON value for a double (integers stored as integers so they print without a fraction).</summary>
    public static JsonNode Number(double d)
    {
        if (d == 0)
        {
            return JsonValue.Create(0);
        }

        if (Math.Abs(d) < 9e15 && d == Math.Floor(d))
        {
            return JsonValue.Create((long)d);
        }

        return JsonValue.Create(d);
    }

    /// <summary>The protocol's truthiness: booleans as themselves, <c>"true"</c>/<c>"false"</c> strings, 0 is false, null is false.</summary>
    public static bool ToBoolean(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return false;
            case JsonObject obj:
                return obj.TryGetPropertyValue("valid", out var valid) && IsBoolean(valid) && ToBoolean(valid);
            case JsonArray:
                return false;
        }

        return node.GetValueKind() switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => string.Equals(node.GetValue<string>(), "true", StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Number => TryGetDouble(node, out var d) && d != 0 && !double.IsNaN(d),
            _ => false
        };
    }

    /// <summary>The name of a node's JSON type: object, array, string, number, boolean or null.</summary>
    public static string TypeName(JsonNode? node) => node switch
    {
        null => "null",
        JsonObject => "object",
        JsonArray => "array",
        _ => node.GetValueKind() switch
        {
            JsonValueKind.String => "string",
            JsonValueKind.Number => "number",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            _ => "null"
        }
    };

    private static bool IsBoolKind(JsonValueKind kind) => kind is JsonValueKind.True or JsonValueKind.False;
}
