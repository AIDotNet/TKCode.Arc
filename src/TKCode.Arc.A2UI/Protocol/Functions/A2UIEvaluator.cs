using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace TKCode.Arc.A2UI;

/// <summary>Where an expression is evaluated: the surface, its data, the data path of the scope and the template index.</summary>
internal sealed record A2UIScope(A2UISurface? Surface, A2UIDataModel? Data, string DataPath, int? Index = null, A2UICatalog? CatalogOverride = null,
                                 bool InTemplate = false)
{
    public A2UIScope Nested(string dataPath, int? index) => this with { DataPath = dataPath, Index = index };
}

/// <summary>Bookkeeping of one evaluation: the data paths and pending calls it read, and whether a gesture is active.</summary>
internal sealed class A2UIEvaluation
{
    public HashSet<string> Reads { get; } = new(StringComparer.Ordinal);

    public HashSet<string> PendingCalls { get; } = new(StringComparer.Ordinal);

    public bool UserActivation { get; init; }

    /// <summary>True when the caller is the agent (<c>callRendererFunction</c>), which may not call renderer-only functions.</summary>
    public bool CalledByAgent { get; init; }

    public void Read(string absolutePath) => Reads.Add(absolutePath);
}

/// <summary>Resolves dynamic values (literals, <c>@path</c> bindings, <c>@call</c> function calls) and runs functions.</summary>
internal sealed class A2UIEvaluator
{
    private readonly A2UIRenderer? _renderer;

    public A2UIEvaluator(A2UIRenderer? renderer, CultureInfo culture)
    {
        _renderer = renderer;
        Culture = culture;
    }

    public CultureInfo Culture { get; set; }

    /// <summary>Catalogs available when no surface is involved (surface-less agent calls, direct evaluation).</summary>
    public IReadOnlyList<A2UICatalog> FallbackCatalogs { get; set; } = [];

    public A2UIRpc? Rpc => _renderer?.Rpc;

    private static readonly A2UIPropertyShape DynamicValueShape = new(A2UIPropertyKind.DynamicValue);

    public void RequestOpenUrl(Uri url, A2UISurface? surface) => _renderer?.OpenUrl(url, surface);

    /// <summary>Resolves a value written for a property of the given shape.</summary>
    public JsonNode? Resolve(JsonNode? value, A2UIPropertyShape shape, A2UIScope scope, A2UIEvaluation ev)
    {
        switch (shape.Kind)
        {
            case A2UIPropertyKind.Literal or A2UIPropertyKind.Child or A2UIPropertyKind.ChildList or A2UIPropertyKind.Action:
                return value;
            case A2UIPropertyKind.DynamicBoolean:
            {
                var resolved = ResolveDynamic(value, scope, ev, deep: false);
                return resolved is null && value is null ? null : JsonValue.Create(A2UIJson.ToBoolean(resolved));
            }
            case A2UIPropertyKind.DynamicString:
            {
                var resolved = ResolveDynamic(value, scope, ev, deep: false);
                // Numbers become text; booleans, lists and objects stay as they are (the view decides how to show them).
                return A2UIJson.IsNumber(resolved) ? JsonValue.Create(A2UIJson.ToDisplayString(resolved)) : resolved;
            }
            case A2UIPropertyKind.DynamicNumber or A2UIPropertyKind.DynamicStringList:
                return ResolveDynamic(value, scope, ev, deep: false);
            case A2UIPropertyKind.DynamicValue:
                return ResolveDynamic(value, scope, ev, deep: true);
            case A2UIPropertyKind.Object when value is JsonObject obj && shape.Fields is { } fields:
            {
                if (IsBinding(obj) || IsCall(obj))
                {
                    return ResolveDynamic(value, scope, ev, deep: true);
                }

                var result = new JsonObject();
                foreach (var (key, fieldValue) in obj)
                {
                    result[key] = fields.TryGetValue(key, out var fieldShape)
                        ? Resolve(fieldValue, fieldShape, scope, ev)?.DeepClone()
                        : fieldValue?.DeepClone();
                }

                return result;
            }
            case A2UIPropertyKind.Array when value is JsonArray array && shape.Items is { } items:
            {
                var result = new JsonArray();
                foreach (var item in array)
                {
                    result.Add(Resolve(item, items, scope, ev)?.DeepClone());
                }

                return result;
            }
            default:
                return ResolveDynamic(value, scope, ev, deep: true);
        }
    }

    /// <summary>Resolves a binding or call; with <paramref name="deep"/>, also inside plain objects and arrays.</summary>
    public JsonNode? ResolveDynamic(JsonNode? value, A2UIScope scope, A2UIEvaluation ev, bool deep)
    {
        switch (value)
        {
            case JsonObject obj when IsBinding(obj):
            {
                var path = A2UIJson.AsString(obj["@path"]) ?? throw A2UIException.Validation("'@path' must be a string.");
                var absolute = A2UIJsonPointer.Resolve(scope.DataPath, path);
                ev.Read(absolute);
                return scope.Data?.Peek(absolute);
            }
            case JsonObject obj when IsCall(obj):
                return Call(obj, scope, ev);
            case JsonObject obj when deep:
            {
                var result = new JsonObject();
                foreach (var (key, item) in obj)
                {
                    var name = key.StartsWith("@@", StringComparison.Ordinal) ? key[1..] : key;
                    result[name] = ResolveDynamic(item, scope, ev, deep: true)?.DeepClone();
                }

                return result;
            }
            case JsonArray array when deep:
            {
                var result = new JsonArray();
                foreach (var item in array)
                {
                    result.Add(ResolveDynamic(item, scope, ev, deep: true)?.DeepClone());
                }

                return result;
            }
            default:
                return value;
        }
    }

    public static bool IsBinding(JsonObject obj) => obj.ContainsKey("@path");

    public static bool IsCall(JsonObject obj) => obj.ContainsKey("@call");

    /// <summary>Runs a function call object (<c>{"@call", "args", "catalogId"?}</c>).</summary>
    public JsonNode? Call(JsonObject call, A2UIScope scope, A2UIEvaluation ev)
    {
        var name = A2UIJson.AsString(call["@call"]);
        if (string.IsNullOrEmpty(name))
        {
            throw A2UIException.Validation("'@call' must be a non-empty string.");
        }

        var argsNode = call["args"] as JsonObject;
        if (name == "@index")
        {
            return Index(argsNode, scope, ev);
        }

        if (name.StartsWith('@'))
        {
            throw A2UIException.Expression($"Unknown system function '{name}'.", scope.Surface?.Id);
        }

        string? explicitCatalog = null;
        if (call.TryGetPropertyValue("catalogId", out var catalogNode))
        {
            explicitCatalog = A2UIJson.AsString(catalogNode) ?? throw A2UIException.Validation("'catalogId' must be a string.");
        }

        var catalog = ResolveCatalog(explicitCatalog, scope);
        if (!catalog.Functions.TryGetValue(name, out var definition))
        {
            throw A2UIException.Expression($"Unrecognized function '{name}' in catalog '{catalog.Id}'.", scope.Surface?.Id);
        }

        // Calls written inside a formatString template (and calls nested in their arguments) resolve against the
        // template's catalog; calls written as JSON resolve on their own.
        var argsScope = scope.InTemplate ? scope with { CatalogOverride = catalog } : scope with { CatalogOverride = null };
        return Invoke(definition, argsNode, argsScope, ev, catalog);
    }

    public JsonNode? Invoke(A2UIFunctionDefinition definition, JsonObject? argsNode, A2UIScope scope, A2UIEvaluation ev, A2UICatalog catalog)
    {
        if (ev.CalledByAgent ? definition.AllowedCallers == A2UIAllowedCallers.RendererOnly : definition.AllowedCallers == A2UIAllowedCallers.AgentOnly)
        {
            throw new A2UIException(A2UIErrorCategory.Expression, ev.CalledByAgent ? A2UIErrorCodes.InvalidFunctionCall : A2UIErrorCodes.ExpressionError,
                                    $"Function '{definition.Name}' is {(ev.CalledByAgent ? "rendererOnly and cannot be called by agent" : "agentOnly and cannot be called by renderer")}.", scope.Surface?.Id);
        }

        if (definition.RequiresUserActivation && !ev.UserActivation)
        {
            throw A2UIException.Expression($"Function '{definition.Name}' requires a user gesture.", scope.Surface?.Id);
        }

        var args = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        if (argsNode is not null)
        {
            foreach (var (key, raw) in argsNode)
            {
                // Any argument may be a binding or a call; an untyped or literal argument resolves as a dynamic value.
                var shape = definition.Args.TryGetValue(key, out var s) && s.Kind != A2UIPropertyKind.Literal ? s : DynamicValueShape;
                // formatString's value is a template: inner calls resolve against the catalog of this call.
                args[key] = Resolve(raw, shape, scope, ev);
            }
        }

        if (definition.Implementation is null)
        {
            // Declared but implemented by the agent: routed through callAgentFunction, pending until it answers.
            if (Rpc is null || scope.Surface is null)
            {
                throw A2UIException.Expression($"Function '{definition.Name}' has no renderer implementation.", scope.Surface?.Id);
            }

            return Rpc.GetAgentValue(scope.Surface, definition, catalog, args, ev);
        }

        var context = new A2UIFunctionContext(this, scope with { CatalogOverride = catalog, InTemplate = false }, definition, ev);
        try
        {
            return definition.Implementation(context, args);
        }
        catch (A2UIException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new A2UIException(A2UIErrorCategory.Expression, A2UIErrorCodes.ExecutionError,
                                    $"Function '{definition.Name}' failed: {ex.Message}", scope.Surface?.Id, innerException: ex);
        }
    }

    private JsonNode Index(JsonObject? args, A2UIScope scope, A2UIEvaluation ev)
    {
        if (scope.Index is not { } index)
        {
            throw new A2UIException(scope.InTemplate ? A2UIErrorCategory.Expression : A2UIErrorCategory.Validation, A2UIErrorCodes.ExecutionError,
                                    "@index function can only be evaluated inside a collection template iteration scope.", scope.Surface?.Id);
        }

        var offset = 0d;
        if (args?["offset"] is { } offsetNode)
        {
            A2UIJson.TryGetDouble(ResolveDynamic(offsetNode, scope, ev, deep: false), out offset);
        }

        return A2UIJson.Number(index + offset);
    }

    /// <summary>
    /// The catalog a call or component resolves to: an explicit id (which must be available), else the scope's override
    /// (inside <c>formatString</c>), else the surface default. Fails with a catalog error otherwise.
    /// </summary>
    public A2UICatalog ResolveCatalog(string? explicitId, A2UIScope scope)
    {
        var available = scope.Surface?.AvailableCatalogs ?? FallbackCatalogs;
        if (explicitId is not null)
        {
            if (explicitId.Length == 0)
            {
                throw A2UIException.Catalog("Catalog not found: '' (catalogId must not be empty).", scope.Surface?.Id);
            }

            foreach (var c in available)
            {
                if (c.Id == explicitId)
                {
                    return c;
                }
            }

            throw A2UIException.Catalog($"Catalog not found: {explicitId}", scope.Surface?.Id);
        }

        if (scope.CatalogOverride is { } overrideCatalog)
        {
            return overrideCatalog;
        }

        if (scope.Surface is { DefaultCatalog: { } surfaceDefault })
        {
            return surfaceDefault;
        }

        if (scope.Surface is null && FallbackCatalogs.Count > 0)
        {
            return FallbackCatalogs[0];
        }

        throw A2UIException.Catalog("The call names no catalogId and the surface has no default catalogId.", scope.Surface?.Id);
    }

    /// <summary>Evaluates a <c>formatString</c> template.</summary>
    public string FormatTemplate(string template, A2UIScope scope, A2UIEvaluation ev)
    {
        if (A2UIExpressionParser.IsPlainText(template) && !template.Contains("\\${", StringComparison.Ordinal))
        {
            return template;
        }

        var parts = A2UIExpressionParser.ParseTemplate(template);
        scope = scope with { InTemplate = true };
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            sb.Append(A2UIJson.ToDisplayString(EvaluateExpression(part, scope, ev)));
        }

        return sb.ToString();
    }

    private JsonNode? EvaluateExpression(A2UIExpressionNode node, A2UIScope scope, A2UIEvaluation ev)
    {
        switch (node)
        {
            case A2UILiteralExpression literal:
                return literal.Value;
            case A2UIPathExpression path:
            {
                var absolute = A2UIJsonPointer.Resolve(scope.DataPath, path.Path);
                ev.Read(absolute);
                return scope.Data?.Peek(absolute);
            }
            case A2UICallExpression call:
                return Call((JsonObject)call.ToDynamicValue()!, scope, ev);
            default:
                return null;
        }
    }

    internal static string FormatNumberText(double value, int? decimals, bool grouping, CultureInfo culture)
    {
        var digits = decimals ?? (value == Math.Floor(value) ? 0 : 2);
        digits = Math.Clamp(digits, 0, 20);
        var format = (grouping ? "N" : "F") + digits.ToString(CultureInfo.InvariantCulture);
        return value.ToString(format, culture);
    }
}
