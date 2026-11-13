using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TKCode.Arc.A2UI;

/// <summary>
/// Checks incoming components: envelope keys, catalog resolution, the catalog schema of each type (including the
/// function calls inside), composition rules and, in strict mode, the topology of the whole surface.
/// </summary>
internal sealed partial class A2UIMessageValidator
{
    private static readonly HashSet<string> s_envelopeKeys = new(StringComparer.Ordinal) { "@catalogId", "@accessibility", "@metadata", "@checks" };

    private readonly A2UIRenderer _renderer;
    private readonly A2UISchemaValidator _schema;
    private readonly Stack<A2UICatalog> _catalogs = new();
    private A2UISurface? _surface;
    private bool _strict;

    public A2UIMessageValidator(A2UIRenderer renderer)
    {
        _renderer = renderer;
        _schema = new A2UISchemaValidator((reference, doc) =>
            A2UICatalogReader.ResolveRef(reference, doc, _catalogs.Count > 0 ? _catalogs.Peek() : null))
        {
            RefHook = FunctionCallHook
        };
    }

    /// <summary>The deepest nesting of a JSON value in a message.</summary>
    public const int MaxJsonDepth = 50;

    /// <summary>The deepest nesting of function calls inside one value.</summary>
    public const int MaxCallDepth = 5;

    /// <summary>The deepest component tree (edges from the root).</summary>
    public const int MaxTreeDepth = 50;

    private static readonly string[] s_legacyKeys = ["catalogId", "metadata", "accessibility", "checks"];

    /// <summary>True when the text is a Unicode identifier (UAX #31): a letter or '_' first, then letters, marks, digits, '_'.</summary>
    public static bool IsIdentifier(string text) => A2UIExpressionParser.IsName(text) && !text.StartsWith('@');

    /// <summary>The nesting depth of a JSON value (a scalar is 0).</summary>
    public static int Depth(JsonNode? node)
    {
        var max = 0;
        var stack = new Stack<(JsonNode? Node, int Depth)>();
        stack.Push((node, 0));
        while (stack.Count > 0)
        {
            var (current, depth) = stack.Pop();
            max = Math.Max(max, depth);
            switch (current)
            {
                case JsonObject obj:
                    foreach (var (_, value) in obj)
                    {
                        stack.Push((value, depth + 1));
                    }

                    break;
                case JsonArray array:
                    foreach (var value in array)
                    {
                        stack.Push((value, depth + 1));
                    }

                    break;
            }
        }

        return max;
    }

    private static int CallDepth(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                var inner = 0;
                foreach (var (_, value) in obj)
                {
                    inner = Math.Max(inner, CallDepth(value));
                }

                return obj.ContainsKey("@call") ? inner + 1 : inner;
            }
            case JsonArray array:
                return array.Count == 0 ? 0 : array.Max(CallDepth);
            default:
                return 0;
        }
    }

    // Every "@path" string in a value must be a well-formed pointer.
    private static void CheckPaths(JsonNode? node, string? surfaceId, string path)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (key == "@path" && A2UIJson.AsString(value) is { } pointer)
                    {
                        try
                        {
                            A2UIJsonPointer.Parse(pointer);
                        }
                        catch (A2UIException ex)
                        {
                            throw A2UIException.Validation(ex.Message, surfaceId, path + "/@path");
                        }
                    }
                    else
                    {
                        CheckPaths(value, surfaceId, path + "/" + A2UIJsonPointer.Escape(key));
                    }
                }

                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    CheckPaths(array[i], surfaceId, path + "/" + i);
                }

                break;
        }
    }

    /// <summary>Reads and checks a batch of components; throws <see cref="A2UIException"/> when any is invalid.</summary>
    public List<A2UIComponentModel> ReadComponents(A2UISurface surface, JsonArray components, string basePath, bool strict)
    {
        _surface = surface;
        _strict = strict;
        try
        {
            var models = new List<A2UIComponentModel>(components.Count);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < components.Count; i++)
            {
                var path = basePath + "/" + i;
                if (components[i] is not JsonObject raw)
                {
                    throw A2UIException.Validation("A component must be an object.", surface.Id, path);
                }

                var model = ReadComponent(surface, raw, path, strict);
                if (!ids.Add(model.Id))
                {
                    throw A2UIException.Integrity($"Duplicate component id '{model.Id}' in one message.", surface.Id, path + "/id");
                }

                models.Add(model);
            }

            return models;
        }
        finally
        {
            _surface = null;
            _catalogs.Clear();
        }
    }

    private A2UIComponentModel ReadComponent(A2UISurface surface, JsonObject raw, string path, bool strict)
    {
        var id = A2UIJson.AsString(raw["id"]);
        if (string.IsNullOrEmpty(id))
        {
            throw A2UIException.Validation("A component needs a string 'id'.", surface.Id, path + "/id");
        }

        if (!IsIdentifier(id))
        {
            throw A2UIException.Validation($"Component id '{id}' must be a valid UAX #31 identifier (letters, digits and '_', not starting with a digit).",
                                           surface.Id, path + "/id");
        }

        var type = A2UIJson.AsString(raw["component"]);
        if (string.IsNullOrEmpty(type))
        {
            throw A2UIException.Validation($"Component '{id}' needs a string 'component' type.", surface.Id, path + "/component");
        }

        if (type == "Surface")
        {
            throw A2UIException.Validation("'Surface' is reserved and cannot be written.", surface.Id, path + "/component");
        }

        if (!IsIdentifier(type))
        {
            throw A2UIException.Validation($"Component type '{type}' must be a valid UAX #31 identifier.", surface.Id, path + "/component");
        }

        if (Depth(raw) > MaxJsonDepth)
        {
            throw new A2UIException(A2UIErrorCategory.Recursion, A2UIErrorCodes.ValidationFailed,
                                    $"Global recursion limit exceeded: component '{id}' nests deeper than {MaxJsonDepth}.", surface.Id, path);
        }

        if (CallDepth(raw) > MaxCallDepth)
        {
            throw new A2UIException(A2UIErrorCategory.Recursion, A2UIErrorCodes.ValidationFailed,
                                    $"Maximum functionCall depth of {MaxCallDepth} exceeded in component '{id}'.", surface.Id, path);
        }

        CheckPaths(raw, surface.Id, path);
        raw = NormalizeLegacyKeys(surface, raw, type);

        var properties = new JsonObject();
        foreach (var (key, value) in raw)
        {
            if (key is "id" or "component")
            {
                continue;
            }

            if (key.StartsWith('@') && !key.StartsWith("@@", StringComparison.Ordinal))
            {
                if (!s_envelopeKeys.Contains(key))
                {
                    throw new A2UIException(A2UIErrorCategory.Validation, A2UIErrorCodes.InvalidReservedKey,
                                            $"Unknown reserved key '{key}' on component '{id}'.", surface.Id, path + "/" + A2UIJsonPointer.Escape(key));
                }

                continue;
            }

            properties[key] = value?.DeepClone();
        }

        string? explicitCatalog = null;
        if (raw.TryGetPropertyValue("@catalogId", out var catalogNode))
        {
            explicitCatalog = A2UIJson.AsString(catalogNode)
                              ?? throw A2UIException.Validation("'@catalogId' must be a string.", surface.Id, path + "/@catalogId");
        }

        var catalog = ResolveCatalog(surface, explicitCatalog, path + "/@catalogId");
        catalog.Components.TryGetValue(type, out var definition);
        if (definition is null)
        {
            if (strict)
            {
                throw A2UIException.Validation($"Unknown component type '{type}' in catalog '{catalog.Id}'.", surface.Id, path + "/component");
            }
        }
        else
        {
            Validate(raw, catalog, definition, surface, path);
        }

        if (raw["@metadata"] is JsonObject metadata)
        {
            CheckMetadata(metadata, surface.Id, path + "/@metadata");
        }

        return new A2UIComponentModel(id, type, catalog, definition, properties, raw["@accessibility"]?.DeepClone() as JsonObject,
                                      raw["@metadata"]?.DeepClone() as JsonObject, raw["@checks"]?.DeepClone() as JsonArray,
                                      (JsonObject)raw.DeepClone());
    }

    // Bare catalogId / metadata / accessibility / checks are envelope keys unless the type (in the catalog the
    // component would otherwise use) declares them as its own properties.
    private static JsonObject NormalizeLegacyKeys(A2UISurface surface, JsonObject raw, string type)
    {
        JsonObject? normalized = null;
        foreach (var key in s_legacyKeys)
        {
            if (!raw.ContainsKey(key) || raw.ContainsKey("@" + key))
            {
                continue;
            }

            var catalog = A2UIJson.AsString(raw["@catalogId"]) is { } explicitId
                ? surface.AvailableCatalogs.FirstOrDefault(c => c.Id == explicitId)
                : surface.DefaultCatalog;
            if (catalog?.Components.TryGetValue(type, out var definition) == true && definition.Properties.ContainsKey(key))
            {
                continue;
            }

            normalized ??= new JsonObject();
            normalized["@" + key] = raw[key]?.DeepClone();
        }

        if (normalized is null)
        {
            return raw;
        }

        var result = new JsonObject();
        foreach (var (key, value) in raw)
        {
            if (normalized.ContainsKey("@" + key) && s_legacyKeys.Contains(key))
            {
                continue;
            }

            result[key] = value?.DeepClone();
        }

        foreach (var (key, value) in normalized)
        {
            result[key] = value?.DeepClone();
        }

        return result;
    }

    private A2UICatalog ResolveCatalog(A2UISurface surface, string? explicitId, string path)
    {
        if (explicitId is not null)
        {
            if (explicitId.Length == 0)
            {
                throw A2UIException.Catalog("Catalog not found: '' (catalogId must not be empty).", surface.Id, path);
            }

            var found = surface.AvailableCatalogs.FirstOrDefault(c => c.Id == explicitId)
                        ?? throw A2UIException.Catalog($"Catalog not found: {explicitId}", surface.Id, path);
            return found;
        }

        return surface.DefaultCatalog
               ?? throw A2UIException.Catalog("No catalogId is given and the surface has no default catalogId.", surface.Id, path);
    }

    private void Validate(JsonObject raw, A2UICatalog catalog, A2UIComponentDefinition definition, A2UISurface surface, string path)
    {
        var errors = new List<A2UISchemaError>();
        _catalogs.Push(catalog);
        try
        {
            // The envelope (id and the "@" keys) against the common rules.
            var envelope = new JsonObject();
            foreach (var key in new[] { "id", "@catalogId", "@accessibility", "@metadata" })
            {
                if (raw.TryGetPropertyValue(key, out var value))
                {
                    envelope[key] = value?.DeepClone();
                }
            }

            _schema.Validate(envelope, new JsonObject { ["$ref"] = "common_types.json#/$defs/ComponentCommon" }, catalog.Document, path, errors);

            // The body against the catalog's schema for the type. "component" is kept only when the schema declares it,
            // so closed schemas that list only their own properties still accept the component.
            var body = new JsonObject();
            foreach (var (key, value) in raw)
            {
                if (key is "id" or "@catalogId" or "@accessibility" or "@metadata")
                {
                    continue;
                }

                if (key == "component" && !definition.DeclaresComponentKey)
                {
                    continue;
                }

                if (key == "@checks" && !definition.IsCheckable)
                {
                    errors.Add(new(path + "/@checks", $"'{definition.Name}' does not accept '@checks'."));
                    continue;
                }

                body[key] = value?.DeepClone();
            }

            var schema = new JsonObject
            {
                ["properties"] = new JsonObject { ["component"] = new JsonObject { ["type"] = "string" } },
                ["allOf"] = new JsonArray { (JsonNode)new JsonObject { ["$ref"] = "catalog.json#/components/" + A2UIJsonPointer.Escape(definition.Name) } },
                ["unevaluatedProperties"] = false
            };
            if (body.ContainsKey("@checks") && !definition.Properties.ContainsKey("@checks"))
            {
                schema["properties"] = new JsonObject { ["@checks"] = new JsonObject { ["$ref"] = "common_types.json#/$defs/Checkable/properties/@checks" } };
            }

            if (_schema.Validate(body, schema, catalog.Document, path, errors))
            {
                // Calls inside values the schema leaves open (untyped lists and objects) are still checked as calls.
                CheckCallsIn(body, path, errors);
            }
        }
        finally
        {
            _catalogs.Pop();
        }

        if (errors.Count > 0)
        {
            var first = errors[0];
            var where = first.InstancePath.Length > path.Length ? first.InstancePath[path.Length..] : string.Empty;
            throw A2UIException.Validation($"Component '{A2UIJson.AsString(raw["id"])}' ({definition.Name}){(where.Length > 0 ? " at " + where : string.Empty)}: {first.Message}",
                                           surface.Id, first.InstancePath);
        }
    }

    // Function calls are checked against the catalog they resolve to: their own catalogId, else the surface default.
    private bool? FunctionCallHook(string reference, JsonNode? instance, string path, List<A2UISchemaError> errors)
    {
        if (!reference.EndsWith("$defs/FunctionCall", StringComparison.Ordinal) || instance is not JsonObject call || _surface is null)
        {
            return null;
        }

        var name = A2UIJson.AsString(call["@call"]);
        if (name is null)
        {
            return null;
        }

        if (name.Length == 0 || (!name.StartsWith('@') && !IsIdentifier(name)))
        {
            errors.Add(new(path + "/@call", $"Function name '{name}' must be a valid UAX #31 identifier."));
            return false;
        }

        if (name == "@index")
        {
            return ValidateAgainst(call, new JsonObject { ["$ref"] = "common_types.json#/$defs/IndexSystemFunction" }, _catalogs.Peek(), path, errors);
        }

        if (name.StartsWith('@'))
        {
            errors.Add(new(path + "/@call", $"Unknown system function '{name}'."));
            return false;
        }

        A2UICatalog catalog;
        if (call.TryGetPropertyValue("catalogId", out var catalogNode))
        {
            if (A2UIJson.AsString(catalogNode) is not { } catalogId)
            {
                errors.Add(new(path + "/catalogId", "'catalogId' must be a string."));
                return false;
            }

            catalog = ResolveCatalog(_surface, catalogId, path + "/catalogId");
        }
        else
        {
            catalog = _surface.DefaultCatalog
                      ?? throw A2UIException.Catalog($"Call of '{name}' names no catalogId and the surface has no default catalogId.", _surface.Id, path);
        }

        if (!catalog.Functions.ContainsKey(name))
        {
            if (!_strict)
            {
                return true;
            }

            errors.Add(new(path + "/@call", $"Unrecognized function '{name}' in catalog '{catalog.Id}'."));
            return false;
        }

        if (catalog.Functions[name].AllowedCallers == A2UIAllowedCallers.AgentOnly)
        {
            errors.Add(new(path, $"Function '{name}' is agentOnly and cannot be called by the renderer."));
            return false;
        }

        var schema = new JsonObject
        {
            ["allOf"] = new JsonArray
            {
                (JsonNode)new JsonObject { ["$ref"] = "common_types.json#/$defs/FunctionCommon" },
                (JsonNode)new JsonObject { ["$ref"] = "catalog.json#/functions/" + A2UIJsonPointer.Escape(name) }
            },
            ["unevaluatedProperties"] = false
        };
        // Arguments that are bindings or calls are not checked against the argument schema: nested calls are checked as
        // calls in their own right (their own catalog), bindings are resolved at run time.
        var shallow = (JsonObject)call.DeepClone();
        var dynamicArgs = new List<string>();
        if (shallow["args"] is JsonObject shallowArgs)
        {
            foreach (var (key, value) in shallowArgs.ToList())
            {
                if (value is JsonObject obj && (obj.ContainsKey("@call") || obj.ContainsKey("@path")))
                {
                    dynamicArgs.Add(key);
                    shallowArgs.Remove(key);
                    if (obj.ContainsKey("@call") && FunctionCallHook("#/$defs/FunctionCall", obj, path + "/args/" + A2UIJsonPointer.Escape(key), errors) == false)
                    {
                        return false;
                    }
                }
            }
        }

        var argErrors = new List<A2UISchemaError>();
        ValidateAgainst(shallow, schema, catalog, path, argErrors);
        argErrors.RemoveAll(e => dynamicArgs.Any(arg => e.Message == $"Missing required property '{arg}'."));
        if (argErrors.Count > 0)
        {
            errors.AddRange(argErrors);
            return false;
        }

        if (name == "formatString" && call["args"] is JsonObject args && A2UIJson.AsString(args["value"]) is { } template)
        {
            try
            {
                A2UIExpressionParser.ParseTemplate(template);
            }
            catch (A2UIException ex)
            {
                errors.Add(new(path + "/args/value", ex.Message));
                return false;
            }
        }

        return true;
    }

    private void CheckCallsIn(JsonNode? node, string path, List<A2UISchemaError> errors)
    {
        switch (node)
        {
            case JsonObject obj when obj.ContainsKey("@call"):
                FunctionCallHook("#/$defs/FunctionCall", obj, path, errors);
                break;
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    CheckCallsIn(value, path + "/" + A2UIJsonPointer.Escape(key), errors);
                }

                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    CheckCallsIn(array[i], path + "/" + i, errors);
                }

                break;
        }
    }

    private bool ValidateAgainst(JsonObject call, JsonObject schema, A2UICatalog catalog, string path, List<A2UISchemaError> errors)
    {
        _catalogs.Push(catalog);
        try
        {
            return _schema.Validate(call, schema, catalog.Document, path, errors);
        }
        finally
        {
            _catalogs.Pop();
        }
    }

    public static void CheckMetadata(JsonObject metadata, string? surfaceId, string path)
    {
        foreach (var (key, value) in metadata)
        {
            if (key != "extensions")
            {
                throw A2UIException.Validation($"'metadata' may only hold 'extensions', not '{key}'.", surfaceId, path + "/" + A2UIJsonPointer.Escape(key));
            }

            if (value is not JsonObject extensions)
            {
                throw A2UIException.Validation("'extensions' must be an object.", surfaceId, path + "/extensions");
            }

            foreach (var (name, _) in extensions)
            {
                if (!IsIdentifier(name))
                {
                    throw A2UIException.Validation($"Extension key '{name}' is not an identifier.", surfaceId, path + "/extensions/" + A2UIJsonPointer.Escape(name));
                }
            }
        }
    }

    private static string Quote(IEnumerable<string> names) => string.Join(", ", names.Select(n => "'" + n + "'"));

    /// <summary>Composition rules and (strict) topology of the surface after a batch: candidate = current + batch.</summary>
    public void CheckGraph(A2UISurface surface, IReadOnlyList<A2UIComponentModel> batch, string basePath, bool strict, int maxDepth)
    {
        var candidate = new Dictionary<string, A2UIComponentModel>(surface.Components, StringComparer.Ordinal);
        foreach (var model in batch)
        {
            candidate[model.Id] = model;
        }

        var batchIndex = batch.Select((m, i) => (m.Id, i)).ToDictionary(x => x.Id, x => x.i, StringComparer.Ordinal);
        string PathOf(string id) => batchIndex.TryGetValue(id, out var i) ? basePath + "/" + i : basePath;

        // Composition: only pairs involving a component of this batch, so old valid parents stay valid.
        foreach (var parent in candidate.Values)
        {
            foreach (var childId in parent.ChildIds())
            {
                if (!candidate.TryGetValue(childId, out var child))
                {
                    continue;
                }

                if (!batchIndex.ContainsKey(parent.Id) && !batchIndex.ContainsKey(child.Id))
                {
                    continue;
                }

                if (child.Definition?.AllowedParents is { } parents && !parents.Contains(parent.Type))
                {
                    throw new A2UIException(A2UIErrorCategory.Validation, A2UIErrorCodes.UnallowedParent,
                                            $"Component '{child.Id}' ({child.Type}) cannot be placed under parent '{parent.Id}' ({parent.Type}). Allowed parents: [{Quote(parents)}].", surface.Id, PathOf(child.Id));
                }

                if (parent.Definition?.AllowedChildren is { } children && !children.Contains(child.Type))
                {
                    throw new A2UIException(A2UIErrorCategory.Validation, A2UIErrorCodes.UnallowedChild,
                                            $"Component '{parent.Id}' ({parent.Type}) cannot contain child '{child.Id}' ({child.Type}). Allowed children: [{Quote(children)}].", surface.Id, PathOf(parent.Id));
                }
            }
        }

        if (candidate.TryGetValue("root", out var root) && root.Definition?.AllowedParents is { } rootParents && !rootParents.Contains("Surface")
            && batchIndex.ContainsKey("root"))
        {
            throw new A2UIException(A2UIErrorCategory.Validation, A2UIErrorCodes.UnallowedParent,
                                    $"Component 'root' ({root.Type}) cannot be placed under parent 'Surface' (Surface). Allowed parents: [{Quote(rootParents)}].", surface.Id, PathOf("root"));
        }

        // Cycles anywhere in the candidate (strict), and the depth of the tree under root (always).
        if (strict)
        {
            var state = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var id in candidate.Keys)
            {
                FindCycle(id, new List<string>());
            }

            void FindCycle(string id, List<string> trail)
            {
                if (!candidate.TryGetValue(id, out var model) || state.GetValueOrDefault(id) == 2)
                {
                    return;
                }

                if (state.GetValueOrDefault(id) == 1)
                {
                    var start = trail.IndexOf(id);
                    var loop = string.Join(" -> ", trail.Skip(start).Append(id));
                    var self = trail.Count - start == 1;
                    throw new A2UIException(A2UIErrorCategory.Recursion, "RECURSION_LIMIT_EXCEEDED",
                                            (self ? $"Self-reference detected: component '{id}' contains itself" : "Circular reference detected")
                                            + $" (circular component reference: {loop}).", surface.Id, PathOf(id));
                }

                state[id] = 1;
                trail.Add(id);
                foreach (var childId in model.ChildIds().Distinct())
                {
                    FindCycle(childId, trail);
                }

                trail.RemoveAt(trail.Count - 1);
                state[id] = 2;
            }
        }

        if (root is not null)
        {
            var onPath = new HashSet<string>(StringComparer.Ordinal);
            Walk("root", 0);

            void Walk(string id, int depth)
            {
                if (depth > MaxTreeDepth || depth > maxDepth)
                {
                    throw new A2UIException(A2UIErrorCategory.Recursion, A2UIErrorCodes.ValidationFailed,
                                            $"Global recursion limit exceeded: the component tree is deeper than {Math.Min(MaxTreeDepth, maxDepth)}.",
                                            surface.Id, PathOf(id));
                }

                if (!candidate.TryGetValue(id, out var model) || !onPath.Add(id))
                {
                    return;
                }

                foreach (var childId in model.ChildIds().Distinct())
                {
                    Walk(childId, depth + 1);
                }

                onPath.Remove(id);
            }
        }

        if (!strict)
        {
            return;
        }

        if (root is null)
        {
            throw A2UIException.Integrity("Missing root component: the surface has no component with id 'root'.", surface.Id, basePath);
        }

        foreach (var model in candidate.Values)
        {
            foreach (var (field, childId) in model.ChildReferences())
            {
                if (!candidate.ContainsKey(childId))
                {
                    throw A2UIException.Integrity($"Dangling reference: component '{model.Id}' references non-existent component '{childId}' in field '{field}'.",
                                                  surface.Id, PathOf(model.Id));
                }
            }
        }

        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        stack.Push("root");
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!reachable.Add(id) || !candidate.TryGetValue(id, out var model))
            {
                continue;
            }

            foreach (var childId in model.ChildIds())
            {
                stack.Push(childId);
            }
        }

        foreach (var id in candidate.Keys)
        {
            if (!reachable.Contains(id))
            {
                throw A2UIException.Integrity($"Component '{id}' is not reachable from 'root'.", surface.Id, PathOf(id));
            }
        }
    }
}
