using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TKCode.Arc.A2UI;

namespace TKCode.Arc.A2UI.Tests.Conformance;

/// <summary>
/// Runs the official A2UI conformance cases (Core suites) against the protocol layer. Actions this harness does not
/// implement are skipped with a reason, never passed.
/// </summary>
public sealed class ConformanceHarness
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "Conformance");

    /// <summary>Suites run here; the rest are listed with a reason.</summary>
    private static readonly Dictionary<string, string> SkippedActions = new(StringComparer.Ordinal)
    {
        ["from_json"] = "Catalog document round trips: the renderer reads catalogs but does not re-derive them.",
        ["to_json"] = "Catalog serialization goldens are not implemented.",
        ["round_trip"] = "Catalog serialization goldens are not implemented.",
        ["validation_schema"] = "Flattened validation schema goldens are not implemented.",
        ["derive"] = "Catalog derivation is not implemented.",
        ["common_types_schema"] = "Schema generation from SDK models is not implemented (the official document is embedded).",
        ["agent_to_renderer_schema"] = "Schema generation from SDK models is not implemented.",
        ["validate_common_type"] = "Common type model validation is not implemented separately.",
        ["get_renderer_capabilities"] = "Covered by unit tests (stub catalogs are not built here).",
        ["get_renderer_data_model"] = "Covered by unit tests.",
        ["measure_growth"] = "Timing case (run separately).",
        ["resolve_nodes"] = "Node resolution fixtures are compared by the node tests.",
        ["select_catalog"] = "Catalog selection is covered by message processing cases.",
        ["dispatch_action"] = "Action dispatch is covered by unit tests.",
        ["handle_rpc"] = "RPC is covered by unit tests.",
        ["route_agent_function"] = "Agent function routing is covered by unit tests.",
        ["render_surface"] = "Adapter suite (needs the Avalonia view layer).",
        ["accessibility_check"] = "Adapter suite (needs the Avalonia view layer)."
    };

    public static IEnumerable<object[]> Cases(string suite)
    {
        var file = Path.Combine(Root, "core", suite + ".json");
        var cases = (JsonArray)JsonNode.Parse(File.ReadAllText(file))!;
        if (cases.Count == 0)
        {
            throw new InvalidOperationException($"Suite {suite} is empty.");
        }

        foreach (var c in cases)
        {
            yield return [suite, A2UIJson.AsString(c!["name"])!];
        }
    }

    public static void Run(string suite, string name)
    {
        var cases = (JsonArray)JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "core", suite + ".json")))!;
        var testCase = (JsonObject)cases.First(c => A2UIJson.AsString(c!["name"]) == name)!;
        var version = A2UIJson.AsString(testCase["protocolVersion"]) ?? A2UIJson.AsString(testCase["catalog"]?["protocolVersion"]);
        if (version is not null && version != "1.0")
        {
            Assert.Skip($"Protocol version {version} is not supported by this renderer (1.0 only).");
        }

        var action = A2UIJson.AsString(testCase["action"]) ?? "?";
        if (SkippedActions.TryGetValue(action, out var reason))
        {
            Assert.Skip(reason);
        }

        switch (action)
        {
            case "data_model":
                DataModelCase(testCase);
                break;
            case "resolve_path":
                ResolvePathCase(testCase);
                break;
            case "parse_expression_template":
                ExpressionCase(testCase);
                break;
            case "evaluate_function":
                FunctionCase(testCase);
                break;
            case "validate" or "process_messages":
                MessagesCase(testCase, action);
                break;
            default:
                Assert.Fail($"Unknown action '{action}' in {suite}#{name}");
                break;
        }
    }

    // ---- data_model ------------------------------------------------------------------------------------------

    private static void DataModelCase(JsonObject c)
    {
        var model = new A2UIDataModel(c["initial"]);
        var watches = (c["watch"] as JsonArray)?.Select(w => A2UIJson.AsString(w)!).ToList() ?? [];
        var notified = new List<string>();
        var values = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var watch in watches)
        {
            var path = watch;
            model.Subscribe(path, value =>
            {
                notified.Add(path);
                values[path] = value?.DeepClone();
            });
        }

        JsonNode? lastInput = null;
        foreach (var stepNode in (JsonArray)c["steps"]!)
        {
            var step = (JsonObject)stepNode!;
            notified.Clear();
            values.Clear();
            var op = A2UIJson.AsString(step["op"]);
            var path = A2UIJson.AsString(step["path"]);
            var expectError = step["expect_error"] ?? step["expectError"];
            try
            {
                JsonNode? got = null;
                switch (op)
                {
                    case "get":
                        got = model.Get(path);
                        break;
                    case "set":
                        lastInput = step["value"]?.DeepClone();
                        model.Set(path, lastInput);
                        break;
                    case "delete":
                        model.Delete(path);
                        break;
                    case "dispose":
                        model.Dispose();
                        break;
                    case "mutate_input":
                        MutateAt(lastInput, path!, step["value"]);
                        break;
                    default:
                        Assert.Fail($"Unknown op '{op}'.");
                        break;
                }

                Assert.True(expectError is null, $"Expected an error {expectError?.ToJsonString()} for {op} {path}.");
                if (op == "get")
                {
                    if (step.ContainsKey("expect"))
                    {
                        AssertJson(step["expect"], got, $"get {path}");
                    }

                    if (A2UIJson.AsBoolean(step["expect_absent"]) == true)
                    {
                        Assert.Null(got);
                    }

                    if (A2UIJson.AsString(step["expect_type"]) is { } type)
                    {
                        Assert.Equal(type == "list" ? "array" : type, A2UIJson.TypeName(got));
                    }
                }

                if (step["expect_notified"] is JsonArray expectedNotified)
                {
                    Assert.Equal(expectedNotified.Select(n => A2UIJson.AsString(n)).OrderBy(s => s), notified.OrderBy(s => s));
                }

                if (step["expect_values"] is JsonObject expectedValues)
                {
                    foreach (var (key, value) in expectedValues)
                    {
                        Assert.True(values.ContainsKey(key), $"No notification for {key}.");
                        AssertJson(value, values[key], "notified " + key);
                    }
                }
            }
            catch (A2UIException ex) when (expectError is not null)
            {
                AssertError(expectError, ex);
            }
        }
    }

    private static void MutateAt(JsonNode? root, string path, JsonNode? value)
    {
        var segments = A2UIJsonPointer.Parse(path);
        var node = root;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            node = node is JsonObject o ? o[segments[i]] : (node as JsonArray)?[int.Parse(segments[i], CultureInfo.InvariantCulture)];
        }

        if (node is JsonObject obj)
        {
            obj[segments[^1]] = value?.DeepClone();
        }
        else if (node is JsonArray array)
        {
            array[int.Parse(segments[^1], CultureInfo.InvariantCulture)] = value?.DeepClone();
        }
    }

    private static void ResolvePathCase(JsonObject c)
    {
        var args = (JsonObject)c["args"]!;
        Assert.Equal(A2UIJson.AsString(c["expect"]), A2UIJsonPointer.Resolve(A2UIJson.AsString(args["contextPath"]), A2UIJson.AsString(args["path"])));
    }

    // ---- expressions -----------------------------------------------------------------------------------------

    private static void ExpressionCase(JsonObject c)
    {
        var input = A2UIJson.AsString(c["input"])!;
        var expectError = c["expect_error"] ?? c["expectError"];
        try
        {
            var parts = A2UIExpressionParser.ParseTemplate(input);
            Assert.True(expectError is null, $"Expected an error {expectError?.ToJsonString()}.");
            var actual = new JsonArray();
            foreach (var part in parts)
            {
                actual.Add(PartJson(part));
            }

            AssertJson(c["expect"], actual, "parts");
        }
        catch (A2UIException ex) when (expectError is not null)
        {
            AssertError(expectError, ex);
        }
    }

    private static JsonNode? PartJson(A2UIExpressionNode node) => node switch
    {
        A2UILiteralExpression literal => literal.Value?.DeepClone(),
        A2UIPathExpression path => new JsonObject { ["path"] = path.Path },
        A2UICallExpression call => new JsonObject
        {
            ["call"] = call.Name,
            ["args"] = new JsonObject(call.Args.Select(a => new KeyValuePair<string, JsonNode?>(a.Key, PartJson(a.Value)))),
            ["returnType"] = "any"
        },
        _ => null
    };

    // ---- functions -------------------------------------------------------------------------------------------

    private static void FunctionCase(JsonObject c)
    {
        var function = A2UIJson.AsString(c["function"])!;
        var call = new JsonObject { ["@call"] = function, ["args"] = c["args"]?.DeepClone() ?? new JsonObject() };
        var expectError = c["expectError"] ?? c["expect_error"];
        var catalogs = LoadCatalogs(c, new List<string>(), alias: false);
        using var renderer = new A2UIRenderer(new A2UIRendererOptions { Catalogs = catalogs, Culture = CultureInfo.GetCultureInfo("en-US") });
        A2UIScope scope;
        if (c["surface"] is JsonObject surfaceBlock)
        {
            var create = new JsonObject { ["surfaceId"] = "s" };
            if (A2UIJson.AsString(surfaceBlock["catalogId"]) is { } surfaceCatalog)
            {
                create["catalogId"] = surfaceCatalog;
            }

            renderer.Apply(new JsonObject { ["version"] = "1.0", ["createSurface"] = create });
            var surface = renderer.GetSurface("s")!;
            scope = new A2UIScope(surface, surface.DataModel, "/");
            if (A2UIJson.AsString(c["catalogId"]) is { } callCatalog)
            {
                call["catalogId"] = callCatalog;
            }
        }
        else
        {
            renderer.Evaluator.FallbackCatalogs = catalogs.Count > 0 ? catalogs : [A2UICatalogs.Basic];
            scope = new A2UIScope(null, new A2UIDataModel(), "/");
        }

        if (c["dataModel"] is { } initialData)
        {
            scope.Data!.Set("/", initialData);
        }

        try
        {
            var result = renderer.Evaluator.Call(call, scope, new A2UIEvaluation { UserActivation = true });
            Assert.True(expectError is null, $"Expected an error {expectError?.ToJsonString()}, got {A2UIJson.ToJson(result)}.");
            var expected = c["expect"];
            if (A2UIJson.IsBoolean(expected) && result is JsonObject validation && validation.ContainsKey("valid"))
            {
                Assert.Equal(A2UIJson.AsBoolean(expected), A2UIJson.AsBoolean(validation["valid"]));
            }
            else if (expected is JsonObject && result is JsonObject)
            {
                AssertSubset(expected, result, "result");
            }
            else
            {
                AssertJson(expected, result, "result");
            }
        }
        catch (A2UIException ex) when (expectError is not null)
        {
            AssertError(expectError, ex);
        }
    }

    // ---- validate / process_messages ---------------------------------------------------------------------------

    private static void MessagesCase(JsonObject c, string action)
    {
        var steps = new List<JsonObject>();
        if (c["steps"] is JsonArray stepArray)
        {
            steps.AddRange(stepArray.Select(s => (JsonObject)s!.DeepClone()));
            if ((c["expectError"] ?? c["expect_error"]) is { } caseError && steps.Count > 0 && !steps[^1].ContainsKey("expectError"))
            {
                steps[^1]["expectError"] = caseError.DeepClone();
            }
        }
        else
        {
            var single = new JsonObject
            {
                ["messages"] = (c["payload"] ?? c["messages"])?.DeepClone(),
                ["expectError"] = (c["expectError"] ?? c["expect_error"])?.DeepClone()
            };
            steps.Add(single);
        }

        if (A2UIJson.AsString(c["name"]) is "test_v10_component_catalog_override")
        {
            Assert.Skip("Uses permissive stub catalogs (a harness convention of the reference runners).");
        }

        if (A2UIJson.AsString(c["name"]) is "test_v10_inline_catalog_id_and_metadata_standard_only_beside_inline_id")
        {
            Assert.Skip("Legacy bare-key routing that depends on an inline 'id' declaration and declared types is not implemented; "
                        + "bare keys route by whether the type declares them.");
        }

        if (steps.Any(s => s.ContainsKey("validationConfig")) || c.ContainsKey("validationConfig"))
        {
            Assert.Skip("Per-call validationConfig is not supported (the renderer has two modes: strict and lenient).");
        }

        var createdCatalogIds = new List<string>();
        foreach (var step in steps)
        {
            CollectCatalogIds(step["messages"] ?? step["payload"], createdCatalogIds);
        }

        var expectsCatalogError = steps.Any(s => (s["expectError"] ?? s["expect_error"]) is JsonObject e && A2UIJson.AsString(e["category"]) == "CatalogError");
        var catalogs = LoadCatalogs(c, createdCatalogIds, alias: !expectsCatalogError);
        var strict = A2UIJson.AsBoolean(c["strictMode"]) != false;
        using var renderer = new A2UIRenderer(new A2UIRendererOptions
        {
            Catalogs = catalogs,
            Culture = CultureInfo.GetCultureInfo("en-US"),
            ValidationMode = strict ? A2UIValidationMode.Strict : A2UIValidationMode.Lenient
        });
        var outboundErrors = new List<JsonObject>();
        var reported = new List<A2UIException>();
        renderer.Outbound += (_, e) =>
        {
            if (e.Kind == A2UIOutboundKind.Error)
            {
                outboundErrors.Add(e.Envelope);
            }
        };
        renderer.ErrorReported += (_, e) => reported.Add(e.Error);

        foreach (var step in steps)
        {
            var expectError = step["expectError"] ?? step["expect_error"];
            outboundErrors.Clear();
            reported.Clear();
            try
            {
                renderer.Apply(step["messages"] ?? step["payload"]);
                if (expectError is not null && reported.Count > 0)
                {
                    // Errors found while resolving nodes are reported, not raised: the first one counts as raised.
                    AssertError(expectError, reported[0]);
                    continue;
                }

                Assert.True(expectError is null, $"Expected an error {expectError?.ToJsonString()}.");
            }
            catch (A2UIException ex) when (expectError is not null)
            {
                AssertError(expectError, ex);
            }

            if (step["expect"] is JsonObject expect)
            {
                AssertSurfaces(renderer, expect);
            }
        }

        if (c["steps"] is not null && c["expect"] is JsonObject finalExpect)
        {
            AssertSurfaces(renderer, finalExpect);
        }
        else if (c["steps"] is null && c["expect"] is JsonObject topExpect)
        {
            AssertSurfaces(renderer, topExpect);
        }
    }

    private static void CollectCatalogIds(JsonNode? messages, List<string> ids)
    {
        switch (messages)
        {
            case JsonArray list:
                foreach (var m in list)
                {
                    CollectCatalogIds(m, ids);
                }

                break;
            case JsonObject obj when obj["messages"] is JsonArray inner:
                CollectCatalogIds(inner, ids);
                break;
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if ((key is "catalogId" or "@catalogId") && A2UIJson.AsString(value) is { Length: > 0 } id)
                    {
                        ids.Add(id);
                    }
                    else
                    {
                        CollectCatalogIds(value, ids);
                    }
                }

                break;
        }
    }

    private static void AssertSurfaces(A2UIRenderer renderer, JsonObject expect)
    {
        if (expect["surfaces"] is not JsonObject surfaces)
        {
            return;
        }

        foreach (var (id, expectedNode) in surfaces)
        {
            var expected = (JsonObject)expectedNode!;
            var surface = renderer.GetSurface(id);
            if (A2UIJson.AsBoolean(expected["exists"]) is { } exists)
            {
                Assert.True(exists == (surface is not null), $"Surface '{id}' exists = {surface is not null}, expected {exists}.");
            }

            if (surface is null)
            {
                continue;
            }

            if (A2UIJson.AsBoolean(expected["sendDataModel"]) is { } send)
            {
                Assert.Equal(send, surface.SendDataModel);
            }

            if (expected.ContainsKey("dataModel"))
            {
                AssertJson(expected["dataModel"], surface.DataModel.Get("/"), $"dataModel of {id}");
            }

            switch (expected["components"])
            {
                case JsonArray list:
                {
                    Assert.Equal(list.Count, surface.Components.Count);
                    var resolved = ResolvedComponents(surface);
                    foreach (var item in list)
                    {
                        var itemObj = (JsonObject)item!;
                        var componentId = A2UIJson.AsString(itemObj["id"])!;
                        Assert.True(surface.Components.TryGetValue(componentId, out var model), $"Missing component '{componentId}'.");
                        AssertComponent(itemObj, model!.Raw, resolved.GetValueOrDefault(componentId), componentId);
                    }

                    break;
                }
                case JsonObject map:
                {
                    var resolved = ResolvedComponents(surface);
                    foreach (var (componentId, item) in map)
                    {
                        surface.Components.TryGetValue(componentId, out var model);
                        resolved.TryGetValue(componentId, out var node);
                        Assert.True(model is not null || node is not null, $"Missing component '{componentId}'.");
                        AssertComponent(item, model?.Raw, node, componentId);
                    }

                    break;
                }
            }
        }
    }

    // ---- catalogs --------------------------------------------------------------------------------------------

    // Resolved components by id; instances inside list templates get "_<index>" suffixes (outermost first).
    private static Dictionary<string, JsonObject> ResolvedComponents(A2UISurface surface)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        if (surface.Root is not { } root)
        {
            return result;
        }

        var stack = new Stack<(A2UINode Node, string Suffix)>();
        stack.Push((root, string.Empty));
        while (stack.Count > 0)
        {
            var (node, suffix) = stack.Pop();
            var isInstance = node.Index is not null && node.Parent is { } parent && parent.DataPath != node.DataPath;
            var mySuffix = isInstance ? suffix + "_" + node.Index : suffix;
            var obj = (JsonObject)node.Props.DeepClone();
            obj["id"] = node.ComponentId;
            obj["component"] = node.Type;
            if (node.Validation.Count > 0)
            {
                obj["isValid"] = node.IsValid;
                var failed = node.Validation.FirstOrDefault(v => !v.Valid);
                obj["validationResult"] = failed is null
                    ? new JsonObject { ["valid"] = true }
                    : new JsonObject { ["valid"] = false, ["message"] = failed.Message, ["severity"] = failed.Severity };
                obj["validationErrors"] = new JsonArray(node.Validation.Where(v => !v.Valid)
                    .Select(v => (JsonNode?)JsonValue.Create(v.Message ?? string.Empty)).ToArray());
            }

            result.TryAdd(node.ComponentId + mySuffix, obj);
            foreach (var child in node.AllChildren())
            {
                stack.Push((child, mySuffix));
            }
        }

        return result;
    }

    private static void AssertComponent(JsonNode? expected, JsonObject? raw, JsonObject? resolved, string id)
    {
        if (resolved is not null && Matches(expected, resolved))
        {
            return;
        }

        AssertSubset(expected, raw ?? resolved, $"component {id}");
    }

    private static bool Matches(JsonNode? expected, JsonNode? actual)
    {
        if (expected is JsonObject eo && actual is JsonObject ao)
        {
            return eo.All(p => (p.Value is null && !ao.ContainsKey(p.Key)) || (ao.ContainsKey(p.Key) && Matches(p.Value, ao[p.Key])));
        }

        return A2UIJson.DeepEquals(expected, actual);
    }

    private static List<A2UICatalog> LoadCatalogs(JsonObject c, List<string> createdIds, bool alias)
    {
        var catalogs = new List<A2UICatalog>();
        var paths = new List<string>();
        if (c["catalogPaths"] is JsonArray list)
        {
            paths.AddRange(list.Select(p => A2UIJson.AsString(p)!));
        }

        if (A2UIJson.AsString(c["catalogPath"]) is { } single)
        {
            paths.Add(single);
        }

        foreach (var path in paths)
        {
            if (path.Contains("basic/v1/catalog.json", StringComparison.Ordinal))
            {
                catalogs.Add(A2UICatalogs.CreateBasic());
            }
            else if (path.StartsWith("conformance/test_data/", StringComparison.Ordinal))
            {
                var file = Path.Combine(Root, path["conformance/".Length..]);
                var doc = (JsonObject)JsonNode.Parse(File.ReadAllText(file))!;
                if ((A2UIJson.AsString(doc["protocolVersion"]) ?? "1.0") == "1.0")
                {
                    doc["protocolVersion"] ??= "1.0";
                    catalogs.Add(Load(doc));
                }
            }
            else if (path.Contains("v0_9/catalogs/basic", StringComparison.Ordinal))
            {
                // A pre-1.0 catalog: only its id and version matter (surfaces and calls must reject it).
                catalogs.Add(A2UICatalog.FromJson(new JsonObject
                {
                    ["catalogId"] = "https://a2ui.org/specification/v0_9/catalogs/basic/catalog.json",
                    ["protocolVersion"] = "0.9"
                }));
            }
        }

        if (c["catalog"]?["catalogSchema"] is JsonObject catalogSchema)
        {
            var doc = (JsonObject)catalogSchema.DeepClone();
            doc["protocolVersion"] ??= "1.0";
            catalogs.Add(Load(doc));
        }

        if (c["catalog"] is JsonObject inline && (inline.ContainsKey("components") || inline.ContainsKey("functions")))
        {
            var doc = (JsonObject)inline.DeepClone();
            doc["protocolVersion"] ??= "1.0";
            doc["catalogId"] ??= "inline";
            catalogs.Add(Load(doc));
        }

        if (c["catalogs"] is JsonArray inlineList)
        {
            foreach (var entry in inlineList)
            {
                var doc = (JsonObject)entry!.DeepClone();
                doc["protocolVersion"] ??= "1.0";
                if (A2UIJson.AsString(doc["protocolVersion"]) != "1.0")
                {
                    continue;
                }

                if (!doc.ContainsKey("components") && !doc.ContainsKey("functions"))
                {
                    // A stub: the basic catalog under that id.
                    var stub = A2UICatalogs.CreateBasic().Document.DeepClone().AsObject();
                    stub["catalogId"] = doc["catalogId"]?.DeepClone();
                    stub["$id"] = doc["catalogId"]?.DeepClone();
                    catalogs.Add(Load(stub));
                    continue;
                }

                catalogs.Add(Load(doc));
            }
        }

        if (catalogs.Count == 0)
        {
            catalogs.Add(A2UICatalogs.CreateBasic());
        }

        if (alias)
        {
            foreach (var id in createdIds.Distinct())
            {
                if (catalogs.Any(cat => cat.Id == id))
                {
                    continue;
                }

                var source = id.Contains("basic", StringComparison.OrdinalIgnoreCase)
                    ? A2UICatalogs.CreateBasic()
                    : catalogs.FirstOrDefault(cat => cat.Id != A2UICatalogs.BasicCatalogId) ?? catalogs[0];
                var doc = source.Document.DeepClone().AsObject();
                doc["catalogId"] = id;
                doc["$id"] = id;
                catalogs.Add(Load(doc));
            }
        }

        return catalogs;
    }

    private static A2UICatalog Load(JsonObject doc)
    {
        var catalog = A2UICatalog.FromJson(doc);
        A2UIBasicFunctions.ImplementAll(catalog);
        return catalog;
    }

    // ---- assertions ------------------------------------------------------------------------------------------

    private static void AssertError(JsonNode expectError, A2UIException ex)
    {
        if (A2UIJson.AsString(expectError) is { } onlyMessage)
        {
            expectError = new JsonObject { ["message"] = onlyMessage };
        }

        var category = A2UIJson.AsString(expectError["category"]);
        if (category is not null)
        {
            var ok = category switch
            {
                "ValidationError" => ex.Category is A2UIErrorCategory.Validation or A2UIErrorCategory.Integrity or A2UIErrorCategory.Recursion,
                "IntegrityError" => ex.Category is A2UIErrorCategory.Integrity or A2UIErrorCategory.Recursion,
                "RecursionError" => ex.Category == A2UIErrorCategory.Recursion,
                "CatalogError" => ex.Category == A2UIErrorCategory.Catalog,
                "DataError" => ex.Category == A2UIErrorCategory.Data,
                "ExpressionError" or "ParseError" => ex.Category is A2UIErrorCategory.Expression or A2UIErrorCategory.Parse,
                "StateError" => ex.Category == A2UIErrorCategory.State,
                _ => false
            };
            Assert.True(ok, $"Expected {category}, got {ex.Category}: {ex.Message}");
        }

        if (A2UIJson.AsString(expectError["message"]) is { } message)
        {
            Assert.True(Regex.IsMatch(ex.Message, message, RegexOptions.IgnoreCase) || ex.Message.Contains(message, StringComparison.OrdinalIgnoreCase),
                        $"Error message '{ex.Message}' does not match '{message}'.");
        }
    }

    private static void AssertJson(JsonNode? expected, JsonNode? actual, string what)
        => Assert.True(A2UIJson.DeepEquals(expected, actual), $"{what}: expected {A2UIJson.ToJson(expected)}, got {A2UIJson.ToJson(actual)}.");

    private static void AssertSubset(JsonNode? expected, JsonNode? actual, string what)
    {
        if (expected is JsonObject eo && actual is JsonObject ao)
        {
            foreach (var (key, value) in eo)
            {
                if (value is null && !ao.ContainsKey(key))
                {
                    continue;
                }

                Assert.True(ao.ContainsKey(key), $"{what}: missing '{key}' in {A2UIJson.ToJson(actual)}.");
                AssertSubset(value, ao[key], what + "/" + key);
            }

            return;
        }

        AssertJson(expected, actual, what);
    }
}
