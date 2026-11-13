using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TKCode.Arc.A2UI;

/// <summary>One schema violation: where in the instance, and why.</summary>
public sealed record A2UISchemaError(string InstancePath, string Message);

/// <summary>
/// A JSON Schema (2020-12) validator covering the keywords A2UI catalogs and protocol schemas use: <c>$ref</c>,
/// <c>allOf</c>/<c>anyOf</c>/<c>oneOf</c>/<c>not</c>, <c>if</c>/<c>then</c>, <c>type</c>, <c>const</c>, <c>enum</c>,
/// <c>properties</c>, <c>required</c>, <c>additionalProperties</c>, <c>patternProperties</c>, <c>propertyNames</c>,
/// <c>unevaluatedProperties</c>, <c>items</c>, <c>min</c>/<c>maxItems</c>, <c>contains</c>, <c>uniqueItems</c>,
/// <c>minimum</c>/<c>maximum</c>, <c>min</c>/<c>maxLength</c>, <c>pattern</c>, <c>min</c>/<c>maxProperties</c> and the
/// <c>uri</c>/<c>date-time</c> formats. Unknown keywords are ignored, as the specification allows.
/// </summary>
internal sealed class A2UISchemaValidator
{
    private static readonly ConcurrentDictionary<string, Regex> s_patterns = new(StringComparer.Ordinal);
    private readonly Func<string, JsonObject?, JsonNode?> _resolveRef;

    /// <param name="resolveRef">
    /// Resolves a <c>$ref</c> given the reference and the document the reference appears in; returns the target
    /// schema, or null when it cannot be resolved.
    /// </param>
    public A2UISchemaValidator(Func<string, JsonObject?, JsonNode?> resolveRef)
    {
        _resolveRef = resolveRef;
    }

    /// <summary>
    /// Called before a <c>$ref</c> is followed; may return a replacement result (true/false) to short-circuit
    /// validation of that reference, or null to continue normally.
    /// </summary>
    public Func<string, JsonNode?, string, List<A2UISchemaError>, bool?>? RefHook { get; set; }

    /// <summary>Validates <paramref name="instance"/>; errors are appended to <paramref name="errors"/>.</summary>
    public bool Validate(JsonNode? instance, JsonNode? schema, JsonObject? document, string path, List<A2UISchemaError> errors)
        => Check(instance, schema, document, path, errors, out _, 0);

    private bool Check(JsonNode? instance, JsonNode? schemaNode, JsonObject? doc, string path, List<A2UISchemaError> errors,
                       out HashSet<string>? evaluated, int depth)
    {
        evaluated = null;
        if (depth > 128)
        {
            errors.Add(new(path, "Schema nesting too deep."));
            return false;
        }

        if (schemaNode is null)
        {
            return true;
        }

        if (schemaNode is JsonValue boolSchema && A2UIJson.AsBoolean(boolSchema) is { } b)
        {
            if (!b)
            {
                errors.Add(new(path, "No value is allowed here."));
            }

            return b;
        }

        if (schemaNode is not JsonObject schema)
        {
            return true;
        }

        var ok = true;
        var names = new HashSet<string>(StringComparer.Ordinal);

        if (schema.TryGetPropertyValue("$ref", out var refNode) && A2UIJson.AsString(refNode) is { } reference)
        {
            var hooked = RefHook?.Invoke(reference, instance, path, errors);
            if (hooked is { } hookResult)
            {
                ok &= hookResult;
                // A hooked reference counts as evaluating every property of the instance.
                if (instance is JsonObject hookedObj)
                {
                    foreach (var (key, _) in hookedObj)
                    {
                        names.Add(key);
                    }
                }
            }
            else
            {
                var (target, targetDoc) = Resolve(reference, doc);
                if (target is null)
                {
                    errors.Add(new(path, $"Unresolvable schema reference '{reference}'."));
                    ok = false;
                }
                else
                {
                    ok &= Check(instance, target, targetDoc, path, errors, out var refEvaluated, depth + 1);
                    Merge(names, refEvaluated);
                }
            }
        }

        if (schema.TryGetPropertyValue("type", out var typeNode) && !MatchesType(instance, typeNode))
        {
            errors.Add(new(path, $"Expected {TypeText(typeNode)}, got {A2UIJson.TypeName(instance)}."));
            return false;
        }

        if (schema.TryGetPropertyValue("const", out var constNode) && !A2UIJson.DeepEquals(instance, constNode))
        {
            errors.Add(new(path, $"Expected the value {A2UIJson.ToJson(constNode)}."));
            return false;
        }

        if (schema.TryGetPropertyValue("enum", out var enumNode) && enumNode is JsonArray enumValues
            && !enumValues.Any(v => A2UIJson.DeepEquals(instance, v)))
        {
            errors.Add(new(path, $"Value {A2UIJson.ToJson(instance)} is not one of {A2UIJson.ToJson(enumValues)}."));
            return false;
        }

        if (schema.TryGetPropertyValue("allOf", out var allOf) && allOf is JsonArray allOfArray)
        {
            foreach (var sub in allOfArray)
            {
                ok &= Check(instance, sub, doc, path, errors, out var subEvaluated, depth + 1);
                Merge(names, subEvaluated);
            }
        }

        if (schema.TryGetPropertyValue("anyOf", out var anyOf) && anyOf is JsonArray anyOfArray)
        {
            var any = false;
            var branchErrors = new List<A2UISchemaError>();
            foreach (var sub in anyOfArray)
            {
                var local = new List<A2UISchemaError>();
                if (Check(instance, sub, doc, path, local, out var subEvaluated, depth + 1))
                {
                    any = true;
                    Merge(names, subEvaluated);
                }
                else
                {
                    branchErrors.AddRange(local);
                }
            }

            if (!any)
            {
                AddBest(errors, branchErrors, path, "Value does not match any allowed form.");
                ok = false;
            }
        }

        if (schema.TryGetPropertyValue("oneOf", out var oneOf) && oneOf is JsonArray oneOfArray)
        {
            ok &= CheckOneOf(instance, schema, oneOfArray, doc, path, errors, names, depth);
        }

        if (schema.TryGetPropertyValue("not", out var notNode))
        {
            if (Check(instance, notNode, doc, path, new List<A2UISchemaError>(), out _, depth + 1))
            {
                errors.Add(new(path, "Value matches a form that is not allowed."));
                ok = false;
            }
        }

        if (schema.TryGetPropertyValue("if", out var ifNode))
        {
            if (Check(instance, ifNode, doc, path, new List<A2UISchemaError>(), out var ifEvaluated, depth + 1))
            {
                Merge(names, ifEvaluated);
                if (schema.TryGetPropertyValue("then", out var thenNode))
                {
                    ok &= Check(instance, thenNode, doc, path, errors, out var thenEvaluated, depth + 1);
                    Merge(names, thenEvaluated);
                }
            }
            else if (schema.TryGetPropertyValue("else", out var elseNode))
            {
                ok &= Check(instance, elseNode, doc, path, errors, out var elseEvaluated, depth + 1);
                Merge(names, elseEvaluated);
            }
        }

        switch (instance)
        {
            case JsonObject obj:
                ok &= CheckObject(obj, schema, doc, path, errors, names, depth);
                break;
            case JsonArray array:
                ok &= CheckArray(array, schema, doc, path, errors, depth);
                break;
            case JsonValue value:
                ok &= CheckScalar(value, schema, path, errors);
                break;
        }

        evaluated = names.Count > 0 ? names : null;
        return ok;
    }

    private bool CheckOneOf(JsonNode? instance, JsonObject schema, JsonArray branches, JsonObject? doc, string path,
                            List<A2UISchemaError> errors, HashSet<string> names, int depth)
    {
        // With a discriminator, only the branch whose discriminating const matches is tried (clearer errors, faster).
        if (instance is JsonObject obj && schema["discriminator"] is JsonObject disc && A2UIJson.AsString(disc["propertyName"]) is { } prop
            && obj[prop] is { } discValue)
        {
            foreach (var branch in branches)
            {
                var resolved = ResolveShallow(branch, doc, out var branchDoc);
                if (resolved is JsonObject bs && ConstOf(bs, prop, branchDoc) is { } c && A2UIJson.DeepEquals(c, discValue))
                {
                    var ok = Check(instance, branch, doc, path, errors, out var evaluated, depth + 1);
                    Merge(names, evaluated);
                    return ok;
                }
            }

            errors.Add(new(path + "/" + A2UIJsonPointer.Escape(prop), $"Unknown {prop} {A2UIJson.ToJson(discValue)}."));
            return false;
        }

        var matches = 0;
        HashSet<string>? matchedNames = null;
        var allErrors = new List<List<A2UISchemaError>>();
        foreach (var branch in branches)
        {
            var local = new List<A2UISchemaError>();
            if (Check(instance, branch, doc, path, local, out var evaluated, depth + 1))
            {
                matches++;
                matchedNames = evaluated;
            }
            else
            {
                allErrors.Add(local);
            }
        }

        if (matches == 1)
        {
            Merge(names, matchedNames);
            return true;
        }

        if (matches > 1)
        {
            errors.Add(new(path, "Value matches more than one allowed form."));
            return false;
        }

        // Report the branch that got furthest: skip branches that failed on the shape of the value itself (wrong type,
        // missing their defining key), then take the deepest error path.
        var candidates = allErrors.Where(list => !IsShapeMismatch(list, path)).ToList();
        var best = (candidates.Count > 0 ? candidates : allErrors)
            .OrderByDescending(list => list.Count == 0 ? 0 : list.Max(e => e.InstancePath.Length)).FirstOrDefault();
        AddBest(errors, best ?? new List<A2UISchemaError>(), path, "Value does not match any allowed form.");
        return false;
    }

    // A branch failed on the shape of the value itself: its defining "@" key is missing, or the value has the wrong type.
    private static bool IsShapeMismatch(List<A2UISchemaError> errors, string path)
        => errors.Any(e => e.Message.StartsWith("Missing required property '@", StringComparison.Ordinal) && e.InstancePath.LastIndexOf('/') == path.Length)
           || errors.All(e => e.InstancePath == path && (e.Message.StartsWith("Expected ", StringComparison.Ordinal)
                                                         || e.Message.StartsWith("Value ", StringComparison.Ordinal)
                                                         || e.Message.StartsWith("No value", StringComparison.Ordinal)));

    private static void AddBest(List<A2UISchemaError> errors, List<A2UISchemaError> candidates, string path, string fallback)
    {
        var deeper = candidates.Where(e => e.InstancePath.Length > path.Length).ToList();
        if (deeper.Count > 0)
        {
            errors.AddRange(deeper);
        }
        else
        {
            errors.Add(new(path, candidates.Count > 0 ? fallback + " " + candidates[0].Message : fallback));
        }
    }

    private bool CheckObject(JsonObject obj, JsonObject schema, JsonObject? doc, string path, List<A2UISchemaError> errors,
                             HashSet<string> names, int depth)
    {
        var ok = true;
        if (schema["required"] is JsonArray required)
        {
            foreach (var r in required)
            {
                if (A2UIJson.AsString(r) is { } key && !obj.ContainsKey(key))
                {
                    errors.Add(new(path + "/" + A2UIJsonPointer.Escape(key), $"Missing required property '{key}'."));
                    ok = false;
                }
            }
        }

        if (schema["minProperties"] is { } minP && A2UIJson.TryGetDouble(minP, out var minPv) && obj.Count < minPv)
        {
            errors.Add(new(path, $"Expected at least {minPv} properties."));
            ok = false;
        }

        if (schema["maxProperties"] is { } maxP && A2UIJson.TryGetDouble(maxP, out var maxPv) && obj.Count > maxPv)
        {
            errors.Add(new(path, $"Expected at most {maxPv} properties."));
            ok = false;
        }

        var properties = schema["properties"] as JsonObject;
        var patterns = schema["patternProperties"] as JsonObject;
        var hasAdditional = schema.TryGetPropertyValue("additionalProperties", out var additional);
        var localNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (key, value) in obj)
        {
            var childPath = path + "/" + A2UIJsonPointer.Escape(key);
            var matched = false;
            if (properties is not null && properties.TryGetPropertyValue(key, out var propSchema))
            {
                matched = true;
                ok &= Check(value, propSchema, doc, childPath, errors, out _, depth + 1);
            }

            if (patterns is not null)
            {
                foreach (var (pattern, patternSchema) in patterns)
                {
                    if (Pattern(pattern).IsMatch(key))
                    {
                        matched = true;
                        ok &= Check(value, patternSchema, doc, childPath, errors, out _, depth + 1);
                    }
                }
            }

            if (!matched && hasAdditional)
            {
                if (additional is JsonValue av && A2UIJson.AsBoolean(av) == false)
                {
                    errors.Add(new(childPath, $"Unknown property '{key}'."));
                    ok = false;
                }
                else
                {
                    ok &= Check(value, additional, doc, childPath, errors, out _, depth + 1);
                }

                matched = true;
            }

            if (matched)
            {
                localNames.Add(key);
            }

            if (schema["propertyNames"] is { } propertyNames)
            {
                var nameErrors = new List<A2UISchemaError>();
                if (!Check(JsonValue.Create(key), propertyNames, doc, childPath, nameErrors, out _, depth + 1))
                {
                    errors.Add(new(childPath, $"Property name '{key}' is not allowed."));
                    ok = false;
                }
            }
        }

        names.UnionWith(localNames);

        if (schema.TryGetPropertyValue("unevaluatedProperties", out var unevaluated))
        {
            foreach (var (key, value) in obj)
            {
                if (names.Contains(key))
                {
                    continue;
                }

                var childPath = path + "/" + A2UIJsonPointer.Escape(key);
                if (unevaluated is JsonValue uv && A2UIJson.AsBoolean(uv) == false)
                {
                    errors.Add(new(childPath, $"Unknown property '{key}'."));
                    ok = false;
                }
                else
                {
                    ok &= Check(value, unevaluated, doc, childPath, errors, out _, depth + 1);
                    names.Add(key);
                }
            }
        }

        return ok;
    }

    private bool CheckArray(JsonArray array, JsonObject schema, JsonObject? doc, string path, List<A2UISchemaError> errors, int depth)
    {
        var ok = true;
        if (schema["minItems"] is { } minI && A2UIJson.TryGetDouble(minI, out var minIv) && array.Count < minIv)
        {
            errors.Add(new(path, $"Expected at least {minIv} items."));
            ok = false;
        }

        if (schema["maxItems"] is { } maxI && A2UIJson.TryGetDouble(maxI, out var maxIv) && array.Count > maxIv)
        {
            errors.Add(new(path, $"Expected at most {maxIv} items."));
            ok = false;
        }

        if (schema.TryGetPropertyValue("items", out var items) && items is not null)
        {
            for (var i = 0; i < array.Count; i++)
            {
                ok &= Check(array[i], items, doc, path + "/" + i, errors, out _, depth + 1);
            }
        }

        if (schema.TryGetPropertyValue("contains", out var contains) && contains is not null
            && !array.Any(item => Check(item, contains, doc, path, new List<A2UISchemaError>(), out _, depth + 1)))
        {
            errors.Add(new(path, "No item matches the required form."));
            ok = false;
        }

        if (A2UIJson.AsBoolean(schema["uniqueItems"]) == true)
        {
            for (var i = 0; i < array.Count; i++)
            {
                for (var j = i + 1; j < array.Count; j++)
                {
                    if (A2UIJson.DeepEquals(array[i], array[j]))
                    {
                        errors.Add(new(path + "/" + j, "Duplicate item."));
                        return false;
                    }
                }
            }
        }

        return ok;
    }

    private static bool CheckScalar(JsonValue value, JsonObject schema, string path, List<A2UISchemaError> errors)
    {
        var ok = true;
        if (A2UIJson.TryGetDouble(value, out var number) && A2UIJson.IsNumber(value))
        {
            if (schema["minimum"] is { } min && A2UIJson.TryGetDouble(min, out var minV) && number < minV)
            {
                errors.Add(new(path, $"Expected a number of at least {A2UIJson.FormatNumber(minV)}."));
                ok = false;
            }

            if (schema["maximum"] is { } max && A2UIJson.TryGetDouble(max, out var maxV) && number > maxV)
            {
                errors.Add(new(path, $"Expected a number of at most {A2UIJson.FormatNumber(maxV)}."));
                ok = false;
            }

            if (schema["exclusiveMinimum"] is { } xmin && A2UIJson.TryGetDouble(xmin, out var xminV) && number <= xminV)
            {
                errors.Add(new(path, $"Expected a number above {A2UIJson.FormatNumber(xminV)}."));
                ok = false;
            }

            if (schema["exclusiveMaximum"] is { } xmax && A2UIJson.TryGetDouble(xmax, out var xmaxV) && number >= xmaxV)
            {
                errors.Add(new(path, $"Expected a number below {A2UIJson.FormatNumber(xmaxV)}."));
                ok = false;
            }
        }

        if (A2UIJson.AsString(value) is { } text)
        {
            var length = text.EnumerateRunes().Count();
            if (schema["minLength"] is { } minL && A2UIJson.TryGetDouble(minL, out var minLv) && length < minLv)
            {
                errors.Add(new(path, $"Expected at least {minLv} characters."));
                ok = false;
            }

            if (schema["maxLength"] is { } maxL && A2UIJson.TryGetDouble(maxL, out var maxLv) && length > maxLv)
            {
                errors.Add(new(path, $"Expected at most {maxLv} characters."));
                ok = false;
            }

            if (A2UIJson.AsString(schema["pattern"]) is { } pattern && !Pattern(pattern).IsMatch(text))
            {
                errors.Add(new(path, $"Value does not match the pattern '{pattern}'."));
                ok = false;
            }

            if (A2UIJson.AsString(schema["format"]) is { } format && !MatchesFormat(text, format))
            {
                errors.Add(new(path, $"Value is not a valid {format}."));
                ok = false;
            }
        }

        return ok;
    }

    private static bool MatchesFormat(string text, string format) => format switch
    {
        "uri" => Uri.TryCreate(text, UriKind.Absolute, out _),
        // RFC 3339 shapes, kept apart so a oneOf of date / time / date-time picks exactly one.
        "date" => IsDate(text),
        "time" => s_time.IsMatch(text),
        "date-time" => text.Length > 11 && (text[10] == 'T' || text[10] == 't') && IsDate(text[..10]) && s_time.IsMatch(text[11..]),
        _ => true
    };

    private static readonly System.Text.RegularExpressions.Regex s_time = new(
        @"^([01][0-9]|2[0-3]):[0-5][0-9](:([0-5][0-9]|60)(\.[0-9]+)?)?([Zz]|[+-]([01][0-9]|2[0-3]):[0-5][0-9])?$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static bool IsDate(string text)
        => text.Length == 10 && DateTime.TryParseExact(text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                                                       System.Globalization.DateTimeStyles.None, out _);

    private static bool MatchesType(JsonNode? instance, JsonNode? typeNode)
    {
        if (typeNode is JsonArray types)
        {
            return types.Any(t => MatchesType(instance, t));
        }

        var name = A2UIJson.AsString(typeNode);
        return name switch
        {
            "object" => instance is JsonObject,
            "array" => instance is JsonArray,
            "string" => A2UIJson.IsString(instance),
            "number" => A2UIJson.IsNumber(instance),
            "integer" => A2UIJson.TryGetDouble(instance, out var d) && A2UIJson.IsNumber(instance) && d == Math.Floor(d) && !double.IsInfinity(d),
            "boolean" => A2UIJson.IsBoolean(instance),
            "null" => instance is null,
            _ => true
        };
    }

    private static string TypeText(JsonNode? typeNode)
        => typeNode is JsonArray types ? string.Join(" or ", types.Select(t => A2UIJson.AsString(t))) : A2UIJson.AsString(typeNode) ?? "?";

    private (JsonNode? Schema, JsonObject? Doc) Resolve(string reference, JsonObject? doc)
    {
        var target = _resolveRef(reference, doc);
        if (target is null)
        {
            return (null, doc);
        }

        // The document the target lives in: the root of its tree.
        var root = target;
        while (root.Parent is not null)
        {
            root = root.Parent;
        }

        return (target, root as JsonObject ?? doc);
    }

    private JsonNode? ResolveShallow(JsonNode? schema, JsonObject? doc, out JsonObject? targetDoc)
    {
        targetDoc = doc;
        var guard = 0;
        while (schema is JsonObject obj && A2UIJson.AsString(obj["$ref"]) is { } reference && guard++ < 16)
        {
            (schema, targetDoc) = Resolve(reference, targetDoc);
        }

        return schema;
    }

    // The const of a property in a (possibly allOf-composed) schema.
    private JsonNode? ConstOf(JsonObject schema, string property, JsonObject? doc)
    {
        if (schema["properties"] is JsonObject props && props[property] is JsonObject p && p.TryGetPropertyValue("const", out var c))
        {
            return c;
        }

        if (schema["allOf"] is JsonArray all)
        {
            foreach (var sub in all)
            {
                if (ResolveShallow(sub, doc, out var subDoc) is JsonObject so && ConstOf(so, property, subDoc) is { } found)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static void Merge(HashSet<string> into, HashSet<string>? from)
    {
        if (from is not null)
        {
            into.UnionWith(from);
        }
    }

    private static Regex Pattern(string pattern)
        => s_patterns.GetOrAdd(pattern, static p => new Regex(TranslateXid(p), RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)));

    // .NET has no \p{XID_Start} / \p{XID_Continue}; approximate them with general categories.
    private static string TranslateXid(string pattern)
        => pattern.Replace(@"\p{XID_Start}", @"\p{L}\p{Nl}").Replace(@"\p{XID_Continue}", @"\p{L}\p{Nl}\p{Mn}\p{Mc}\p{Nd}\p{Pc}");
}
