using System.Text.Json.Nodes;

namespace TKCode.Arc.A2UI;

/// <summary>One component as the agent last wrote it, with its envelope keys separated from its properties.</summary>
public sealed class A2UIComponentModel
{
    internal A2UIComponentModel(string id, string type, A2UICatalog? catalog, A2UIComponentDefinition? definition, JsonObject properties,
                                JsonObject? accessibility, JsonObject? metadata, JsonArray? checks, JsonObject raw)
    {
        Id = id;
        Type = type;
        Catalog = catalog;
        Definition = definition;
        Properties = properties;
        Accessibility = accessibility;
        Metadata = metadata;
        Checks = checks;
        Raw = raw;
    }

    /// <summary>The component id.</summary>
    public string Id { get; }

    /// <summary>The component type (<c>Text</c>, <c>Button</c>, ...).</summary>
    public string Type { get; }

    /// <summary>The catalog the component resolved to (null in lenient mode when it could not be resolved).</summary>
    public A2UICatalog? Catalog { get; }

    /// <summary>The type's definition in <see cref="Catalog"/>, or null when the type is unknown.</summary>
    public A2UIComponentDefinition? Definition { get; }

    /// <summary>The component's properties: everything except <c>id</c>, <c>component</c> and the <c>@</c> envelope keys.</summary>
    public JsonObject Properties { get; }

    /// <summary>The <c>@accessibility</c> object, if any.</summary>
    public JsonObject? Accessibility { get; }

    /// <summary>The <c>@metadata</c> object, if any.</summary>
    public JsonObject? Metadata { get; }

    /// <summary>The <c>@checks</c> rules, if any.</summary>
    public JsonArray? Checks { get; }

    /// <summary>The component object as received.</summary>
    public JsonObject Raw { get; }

    /// <summary>The ids of the components this one refers to through child properties (static references only).</summary>
    public IEnumerable<string> ChildIds()
    {
        if (Definition is null)
        {
            foreach (var id in GuessChildren(Properties))
            {
                yield return id;
            }

            yield break;
        }

        foreach (var (name, shape) in Definition.Properties)
        {
            if (shape.HasChildren && Properties[name] is { } value)
            {
                foreach (var id in ChildIdsOf(value, shape))
                {
                    yield return id;
                }
            }
        }
    }

    /// <summary>Each static child reference with the field it is written in (<c>child</c>, <c>children[1]</c>, <c>tabs[0].child</c>).</summary>
    public IEnumerable<(string Field, string Id)> ChildReferences()
    {
        if (Definition is null)
        {
            yield break;
        }

        foreach (var (name, shape) in Definition.Properties)
        {
            if (shape.HasChildren && Properties[name] is { } value)
            {
                foreach (var reference in References(name, value, shape))
                {
                    yield return reference;
                }
            }
        }
    }

    private static IEnumerable<(string, string)> References(string field, JsonNode? value, A2UIPropertyShape shape)
    {
        switch (shape.Kind)
        {
            case A2UIPropertyKind.Child when A2UIJson.AsString(value) is { } id:
                yield return (field, id);
                break;
            case A2UIPropertyKind.ChildList when value is JsonArray list:
                for (var i = 0; i < list.Count; i++)
                {
                    if (A2UIJson.AsString(list[i]) is { } id)
                    {
                        yield return ($"{field}[{i}]", id);
                    }
                }

                break;
            case A2UIPropertyKind.ChildList when value is JsonObject template && A2UIJson.AsString(template["componentId"]) is { } templateId:
                yield return (field + ".componentId", templateId);
                break;
            case A2UIPropertyKind.Array when value is JsonArray array && shape.Items is { } items:
                for (var i = 0; i < array.Count; i++)
                {
                    foreach (var r in References($"{field}[{i}]", array[i], items))
                    {
                        yield return r;
                    }
                }

                break;
            case A2UIPropertyKind.Object when value is JsonObject obj && shape.Fields is { } fields:
                foreach (var (key, fieldShape) in fields)
                {
                    if (fieldShape.HasChildren)
                    {
                        foreach (var r in References(field + "." + key, obj[key], fieldShape))
                        {
                            yield return r;
                        }
                    }
                }

                break;
        }
    }

    /// <summary>The template list (<c>{componentId, path}</c>) children of this component.</summary>
    public IEnumerable<(string ComponentId, string Path)> Templates()
    {
        if (Definition is null)
        {
            yield break;
        }

        foreach (var (name, shape) in Definition.Properties)
        {
            if (shape.Kind == A2UIPropertyKind.ChildList && Properties[name] is JsonObject template
                && A2UIJson.AsString(template["componentId"]) is { } componentId && A2UIJson.AsString(template["path"]) is { } path)
            {
                yield return (componentId, path);
            }
        }
    }

    internal static IEnumerable<string> ChildIdsOf(JsonNode? value, A2UIPropertyShape shape)
    {
        switch (shape.Kind)
        {
            case A2UIPropertyKind.Child when A2UIJson.AsString(value) is { } id:
                yield return id;
                break;
            case A2UIPropertyKind.ChildList when value is JsonArray list:
                foreach (var item in list)
                {
                    if (A2UIJson.AsString(item) is { } id)
                    {
                        yield return id;
                    }
                }

                break;
            case A2UIPropertyKind.ChildList when value is JsonObject template && A2UIJson.AsString(template["componentId"]) is { } templateId:
                yield return templateId;
                break;
            case A2UIPropertyKind.Array when value is JsonArray array && shape.Items is { } items:
                foreach (var item in array)
                {
                    foreach (var id in ChildIdsOf(item, items))
                    {
                        yield return id;
                    }
                }

                break;
            case A2UIPropertyKind.Object when value is JsonObject obj && shape.Fields is { } fields:
                foreach (var (key, fieldShape) in fields)
                {
                    if (fieldShape.HasChildren)
                    {
                        foreach (var id in ChildIdsOf(obj[key], fieldShape))
                        {
                            yield return id;
                        }
                    }
                }

                break;
        }
    }

    // Unknown types: the conventional names.
    private static IEnumerable<string> GuessChildren(JsonObject properties)
    {
        if (A2UIJson.AsString(properties["child"]) is { } child)
        {
            yield return child;
        }

        if (properties["children"] is JsonArray children)
        {
            foreach (var item in children)
            {
                if (A2UIJson.AsString(item) is { } id)
                {
                    yield return id;
                }
            }
        }
    }
}
