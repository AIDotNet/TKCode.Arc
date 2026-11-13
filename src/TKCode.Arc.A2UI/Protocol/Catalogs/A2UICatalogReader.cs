using System.Text.Json.Nodes;

namespace TKCode.Arc.A2UI;

/// <summary>Reads component and function definitions out of a catalog document and resolves schema references.</summary>
internal static class A2UICatalogReader
{
    public const string CommonTypesId = "https://a2ui.org/specification/v1_0/common_types.json";

    private static readonly Lazy<JsonObject> s_commonTypes = new(() => LoadEmbedded("common_types.json"));

    /// <summary>The protocol's common type definitions (<c>common_types.json</c>), shared and read-only.</summary>
    public static JsonObject CommonTypes => s_commonTypes.Value;

    public static JsonObject LoadEmbedded(string name)
    {
        var assembly = typeof(A2UICatalogReader).Assembly;
        using var stream = assembly.GetManifestResourceStream("TKCode.Arc.A2UI.Schemas." + name)
                           ?? throw new InvalidOperationException($"Embedded schema '{name}' is missing.");
        using var reader = new StreamReader(stream);
        return (JsonObject)A2UIJson.Parse(reader.ReadToEnd())!;
    }

    public static void Read(A2UICatalog catalog)
    {
        var doc = catalog.Document;
        if (doc["components"] is JsonObject components)
        {
            foreach (var (name, schemaNode) in components)
            {
                if (schemaNode is not JsonObject schema)
                {
                    continue;
                }

                if (name == "Surface")
                {
                    throw A2UIException.Catalog("A catalog must not define a component named 'Surface'.");
                }

                var definition = new A2UIComponentDefinition(catalog, name, schema);
                var properties = new Dictionary<string, A2UIPropertyShape>(StringComparer.Ordinal);
                var required = new HashSet<string>(StringComparer.Ordinal);
                var checkable = false;
                CollectProperties(schema, doc, catalog, properties, required, ref checkable, 0);
                definition.DeclaresComponentKey = properties.Remove("component") || required.Contains("component");
                definition.Properties = properties;
                definition.Required = required;
                definition.IsCheckable = checkable || properties.ContainsKey("@checks");
                definition.AllowedParents = Strings(FindKeyword(schema, "allowedParents", doc));
                definition.AllowedChildren = Strings(FindKeyword(schema, "allowedChildren", doc));
                definition.Description = A2UIJson.AsString(schema["description"]);
                catalog.AddComponent(definition);
            }
        }

        if (doc["functions"] is JsonObject functions)
        {
            foreach (var (name, schemaNode) in functions)
            {
                if (schemaNode is not JsonObject schema)
                {
                    continue;
                }

                var definition = new A2UIFunctionDefinition(catalog, name, schema)
                {
                    ReturnType = A2UIJson.AsString(schema["returnType"]) ?? "any",
                    RequiresUserActivation = A2UIJson.AsBoolean(schema["requiresUserActivation"]) == true,
                    AllowedCallers = A2UIJson.AsString(schema["allowedCallers"]) switch
                    {
                        "agentOnly" => A2UIAllowedCallers.AgentOnly,
                        "rendererOrAgent" => A2UIAllowedCallers.RendererOrAgent,
                        _ => A2UIAllowedCallers.RendererOnly
                    },
                    Description = A2UIJson.AsString(schema["description"])
                };
                var args = new Dictionary<string, A2UIPropertyShape>(StringComparer.Ordinal);
                if (schema["properties"] is JsonObject props && Resolve(props["args"], doc, catalog) is JsonObject argsSchema)
                {
                    var ignored = new HashSet<string>();
                    var flag = false;
                    CollectProperties(argsSchema, doc, catalog, args, ignored, ref flag, 0);
                }

                definition.Args = args;
                catalog.AddFunction(definition);
            }
        }
    }

    private static void CollectProperties(JsonObject schema, JsonObject doc, A2UICatalog catalog, Dictionary<string, A2UIPropertyShape> into,
                                          HashSet<string> required, ref bool checkable, int depth)
    {
        if (depth > 16)
        {
            return;
        }

        if (A2UIJson.AsString(schema["$ref"]) is { } reference)
        {
            if (reference.EndsWith("/Checkable", StringComparison.Ordinal))
            {
                checkable = true;
            }

            if (Resolve(schema, doc, catalog) is JsonObject target && !ReferenceEquals(target, schema))
            {
                CollectProperties(target, RootOf(target) ?? doc, catalog, into, required, ref checkable, depth + 1);
            }
        }

        if (schema["properties"] is JsonObject props)
        {
            foreach (var (name, propSchema) in props)
            {
                into[name] = Shape(propSchema, doc, catalog, 0);
            }
        }

        if (schema["required"] is JsonArray req)
        {
            foreach (var r in req)
            {
                if (A2UIJson.AsString(r) is { } s)
                {
                    required.Add(s);
                }
            }
        }

        if (schema["allOf"] is JsonArray allOf)
        {
            foreach (var sub in allOf)
            {
                if (sub is JsonObject subSchema)
                {
                    CollectProperties(subSchema, doc, catalog, into, required, ref checkable, depth + 1);
                }
            }
        }

        // Properties declared in alternatives count as declared (they are not required).
        foreach (var key in new[] { "anyOf", "oneOf" })
        {
            if (schema[key] is JsonArray branches)
            {
                foreach (var branch in branches)
                {
                    if (branch is JsonObject branchSchema && branchSchema["properties"] is JsonObject branchProps)
                    {
                        foreach (var (name, propSchema) in branchProps)
                        {
                            if (!into.ContainsKey(name))
                            {
                                into[name] = Shape(propSchema, doc, catalog, 0);
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>The shape of a property schema.</summary>
    public static A2UIPropertyShape Shape(JsonNode? schemaNode, JsonObject doc, A2UICatalog? catalog, int depth)
    {
        if (schemaNode is not JsonObject schema || depth > 12)
        {
            return A2UIPropertyShape.LiteralShape;
        }

        var description = A2UIJson.AsString(schema["description"]);
        var defaultValue = schema["default"];
        if (A2UIJson.AsString(schema["$ref"]) is { } reference)
        {
            var name = reference[(reference.LastIndexOf('/') + 1)..];
            var localCommon = reference == "#/$defs/" + name
                              && (ReferenceEquals(doc, CommonTypes) || doc["$defs"]?[name] is null);
            if (IsCommonRef(reference) || localCommon)
            {
                A2UIPropertyKind? kind = name switch
                {
                    "DynamicString" => A2UIPropertyKind.DynamicString,
                    "DynamicNumber" => A2UIPropertyKind.DynamicNumber,
                    "DynamicBoolean" => A2UIPropertyKind.DynamicBoolean,
                    "DynamicStringList" => A2UIPropertyKind.DynamicStringList,
                    "DynamicValue" or "DataBinding" or "FunctionCall" => A2UIPropertyKind.DynamicValue,
                    "Child" or "ComponentId" => A2UIPropertyKind.Child,
                    "ChildList" => A2UIPropertyKind.ChildList,
                    "Action" => A2UIPropertyKind.Action,
                    _ => null
                };
                if (kind is { } k)
                {
                    return new A2UIPropertyShape(k) { Description = description, Default = defaultValue };
                }
            }

            if (Resolve(schema, doc, catalog) is JsonObject target && !ReferenceEquals(target, schema))
            {
                var inner = Shape(target, RootOf(target) ?? doc, catalog, depth + 1);
                return description is null ? inner : Copy(inner, description, defaultValue ?? inner.Default);
            }
        }

        foreach (var key in new[] { "oneOf", "anyOf" })
        {
            if (schema[key] is JsonArray branches)
            {
                var dynamic = false;
                foreach (var branch in branches)
                {
                    var shape = Shape(branch, doc, catalog, depth + 1);
                    if (shape.Kind != A2UIPropertyKind.Literal)
                    {
                        dynamic = true;
                    }
                }

                if (dynamic)
                {
                    return new A2UIPropertyShape(A2UIPropertyKind.DynamicValue) { Description = description, Default = defaultValue };
                }
            }
        }

        var type = A2UIJson.AsString(schema["type"]);
        if ((type == "object" || schema["properties"] is JsonObject) && schema["properties"] is JsonObject props)
        {
            var fields = new Dictionary<string, A2UIPropertyShape>(StringComparer.Ordinal);
            var any = false;
            foreach (var (name, propSchema) in props)
            {
                var shape = Shape(propSchema, doc, catalog, depth + 1);
                fields[name] = shape;
                any |= shape.Kind != A2UIPropertyKind.Literal;
            }

            if (any)
            {
                return new A2UIPropertyShape(A2UIPropertyKind.Object) { Fields = fields, Description = description, Default = defaultValue };
            }
        }

        if (type == "array" && schema["items"] is JsonObject items)
        {
            var itemShape = Shape(items, doc, catalog, depth + 1);
            if (itemShape.Kind != A2UIPropertyKind.Literal)
            {
                return new A2UIPropertyShape(A2UIPropertyKind.Array) { Items = itemShape, Description = description, Default = defaultValue };
            }
        }

        return description is null && defaultValue is null
            ? A2UIPropertyShape.LiteralShape
            : new A2UIPropertyShape(A2UIPropertyKind.Literal) { Description = description, Default = defaultValue };
    }

    private static A2UIPropertyShape Copy(A2UIPropertyShape shape, string? description, JsonNode? defaultValue)
        => new(shape.Kind) { Fields = shape.Fields, Items = shape.Items, Description = description, Default = defaultValue };

    private static bool IsCommonRef(string reference)
        => reference.StartsWith("common_types.json", StringComparison.Ordinal)
           || reference.StartsWith(CommonTypesId, StringComparison.Ordinal)
           || reference.Contains("/common_types.json", StringComparison.Ordinal);

    /// <summary>Follows <c>$ref</c> chains from a schema node to its target.</summary>
    public static JsonNode? Resolve(JsonNode? schema, JsonObject doc, A2UICatalog? catalog)
    {
        var guard = 0;
        var currentDoc = doc;
        while (schema is JsonObject obj && A2UIJson.AsString(obj["$ref"]) is { } reference && guard++ < 16)
        {
            var target = ResolveRef(reference, currentDoc, catalog);
            if (target is null)
            {
                return schema;
            }

            if (target is JsonObject t && t["$ref"] is null)
            {
                return target;
            }

            currentDoc = RootOf(target) ?? currentDoc;
            schema = target;
        }

        return schema;
    }

    /// <summary>
    /// Resolves a reference: <c>#/...</c> in the current document, <c>common_types.json#/...</c> (or its URL) in the
    /// common types, <c>catalog.json#/...</c> in <paramref name="catalog"/>.
    /// </summary>
    public static JsonNode? ResolveRef(string reference, JsonObject? currentDoc, A2UICatalog? catalog)
    {
        var hash = reference.IndexOf('#');
        var file = hash < 0 ? reference : reference[..hash];
        var fragment = hash < 0 ? string.Empty : reference[(hash + 1)..];
        JsonObject? doc;
        if (file.Length == 0)
        {
            doc = currentDoc;
            if (doc is not null && fragment.StartsWith("/$defs/", StringComparison.Ordinal) && Walk(doc, fragment) is null)
            {
                doc = CommonTypes;
            }
        }
        else if (file.EndsWith("common_types.json", StringComparison.Ordinal))
        {
            doc = CommonTypes;
        }
        else if (file.EndsWith("catalog.json", StringComparison.Ordinal) || (catalog is not null && file == catalog.Id))
        {
            doc = catalog?.Document ?? currentDoc;
        }
        else
        {
            return null;
        }

        return doc is null ? null : Walk(doc, fragment);
    }

    private static JsonNode? Walk(JsonObject doc, string fragment)
    {
        JsonNode? node = doc;
        foreach (var segment in fragment.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var key = A2UIJsonPointer.Unescape(Uri.UnescapeDataString(segment));
            node = node is JsonObject o && o.TryGetPropertyValue(key, out var next) ? next : null;
            if (node is null)
            {
                return null;
            }
        }

        return node;
    }

    public static JsonObject? RootOf(JsonNode node)
    {
        var root = node;
        while (root.Parent is not null)
        {
            root = root.Parent;
        }

        return root as JsonObject;
    }

    private static JsonNode? FindKeyword(JsonObject schema, string keyword, JsonObject doc)
    {
        if (schema.TryGetPropertyValue(keyword, out var value))
        {
            return value;
        }

        if (schema["allOf"] is JsonArray allOf)
        {
            foreach (var sub in allOf)
            {
                if (sub is JsonObject s && s.TryGetPropertyValue(keyword, out var v))
                {
                    return v;
                }
            }
        }

        return null;
    }

    private static IReadOnlyList<string>? Strings(JsonNode? node)
        => node is JsonArray array ? array.Select(A2UIJson.AsString).Where(s => s is not null).Select(s => s!).ToList() : null;
}
