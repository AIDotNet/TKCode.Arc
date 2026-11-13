using System.Text.Json.Nodes;

namespace TKCode.Arc.A2UI;

/// <summary>How a component property is read: what the renderer resolves before the component sees it.</summary>
public enum A2UIPropertyKind
{
    /// <summary>A literal JSON value, used as written (enums, numbers such as <c>weight</c>).</summary>
    Literal,

    /// <summary>A string, a data binding or a function call (<c>DynamicString</c>).</summary>
    DynamicString,

    /// <summary>A number, a data binding or a function call (<c>DynamicNumber</c>).</summary>
    DynamicNumber,

    /// <summary>A boolean, a data binding or a function call (<c>DynamicBoolean</c>).</summary>
    DynamicBoolean,

    /// <summary>A list of strings, a data binding or a function call (<c>DynamicStringList</c>).</summary>
    DynamicStringList,

    /// <summary>Any value; bindings and calls inside objects and arrays are resolved too (<c>DynamicValue</c>).</summary>
    DynamicValue,

    /// <summary>The id of one child component.</summary>
    Child,

    /// <summary>A list of child ids, or a template over a data list.</summary>
    ChildList,

    /// <summary>An action: an event for the agent or a local function call.</summary>
    Action,

    /// <summary>An object whose fields have their own shapes (<see cref="A2UIPropertyShape.Fields"/>).</summary>
    Object,

    /// <summary>A list whose items share one shape (<see cref="A2UIPropertyShape.Items"/>).</summary>
    Array
}

/// <summary>The shape of a component property or function argument, derived from its schema.</summary>
public sealed class A2UIPropertyShape
{
    internal A2UIPropertyShape(A2UIPropertyKind kind)
    {
        Kind = kind;
    }

    /// <summary>How the property is read.</summary>
    public A2UIPropertyKind Kind { get; }

    /// <summary>For <see cref="A2UIPropertyKind.Object"/>: the shapes of its fields.</summary>
    public IReadOnlyDictionary<string, A2UIPropertyShape>? Fields { get; internal init; }

    /// <summary>For <see cref="A2UIPropertyKind.Array"/>: the shape of its items.</summary>
    public A2UIPropertyShape? Items { get; internal init; }

    /// <summary>The schema's default, if any.</summary>
    public JsonNode? Default { get; internal init; }

    /// <summary>The schema's description, if any.</summary>
    public string? Description { get; internal init; }

    /// <summary>True when the shape (or something inside it) refers to child components.</summary>
    public bool HasChildren => Kind is A2UIPropertyKind.Child or A2UIPropertyKind.ChildList
                               || Items?.HasChildren == true || Fields?.Values.Any(f => f.HasChildren) == true;

    internal static readonly A2UIPropertyShape LiteralShape = new(A2UIPropertyKind.Literal);
}

/// <summary>A component type a catalog defines.</summary>
public sealed class A2UIComponentDefinition
{
    internal A2UIComponentDefinition(A2UICatalog catalog, string name, JsonObject schema)
    {
        Catalog = catalog;
        Name = name;
        Schema = schema;
    }

    /// <summary>The catalog that defines the component.</summary>
    public A2UICatalog Catalog { get; }

    /// <summary>The component type name (<c>Text</c>, <c>Button</c>, ...).</summary>
    public string Name { get; }

    /// <summary>The component's JSON Schema in the catalog document.</summary>
    public JsonObject Schema { get; }

    /// <summary>The component's properties and how each is read.</summary>
    public IReadOnlyDictionary<string, A2UIPropertyShape> Properties { get; internal set; } = new Dictionary<string, A2UIPropertyShape>();

    /// <summary>The properties the schema requires.</summary>
    public IReadOnlyCollection<string> Required { get; internal set; } = [];

    /// <summary>Component types this one may be placed in (null: any). <c>Surface</c> means the surface root.</summary>
    public IReadOnlyList<string>? AllowedParents { get; internal set; }

    /// <summary>Component types this one may contain (null: any).</summary>
    public IReadOnlyList<string>? AllowedChildren { get; internal set; }

    internal bool DeclaresComponentKey { get; set; }

    /// <summary>True when the component accepts <c>@checks</c>.</summary>
    public bool IsCheckable { get; internal set; }

    /// <summary>The schema description, if any.</summary>
    public string? Description { get; internal set; }
}

/// <summary>Who may call a function.</summary>
public enum A2UIAllowedCallers
{
    /// <summary>Only the renderer (bindings, checks, actions). The default.</summary>
    RendererOnly,

    /// <summary>Only the agent (<c>callRendererFunction</c>).</summary>
    AgentOnly,

    /// <summary>Both.</summary>
    RendererOrAgent
}

/// <summary>A function a catalog defines.</summary>
public sealed class A2UIFunctionDefinition
{
    internal A2UIFunctionDefinition(A2UICatalog catalog, string name, JsonObject schema)
    {
        Catalog = catalog;
        Name = name;
        Schema = schema;
    }

    /// <summary>The catalog that defines the function.</summary>
    public A2UICatalog Catalog { get; }

    /// <summary>The function name.</summary>
    public string Name { get; }

    /// <summary>The function's JSON Schema (the <c>FunctionCall</c> shape with its <c>args</c>).</summary>
    public JsonObject Schema { get; }

    /// <summary>The declared return type (<c>string</c>, <c>number</c>, <c>boolean</c>, <c>array</c>, <c>object</c>, <c>validationResult</c>, <c>any</c>, <c>void</c>).</summary>
    public string ReturnType { get; internal set; } = "any";

    /// <summary>Who may call the function.</summary>
    public A2UIAllowedCallers AllowedCallers { get; internal set; }

    /// <summary>True when the function may only run inside a user gesture (for example <c>openUrl</c>).</summary>
    public bool RequiresUserActivation { get; internal set; }

    /// <summary>The arguments and how each is read.</summary>
    public IReadOnlyDictionary<string, A2UIPropertyShape> Args { get; internal set; } = new Dictionary<string, A2UIPropertyShape>();

    /// <summary>The renderer-side implementation, or null when the function runs on the agent.</summary>
    public A2UIFunction? Implementation { get; internal set; }

    /// <summary>The schema description, if any.</summary>
    public string? Description { get; internal set; }
}

/// <summary>A renderer-side function implementation.</summary>
/// <param name="context">The evaluation context: data scope, surface, culture, user activation.</param>
/// <param name="args">The resolved arguments, already coerced to each argument's declared kind.</param>
public delegate JsonNode? A2UIFunction(A2UIFunctionContext context, IReadOnlyDictionary<string, JsonNode?> args);

/// <summary>
/// A component and function catalog: the contract between agent and renderer. Load one from its JSON Schema
/// document with <see cref="FromJson(string)"/>; the basic catalog is <see cref="A2UICatalogs.Basic"/>.
/// </summary>
public sealed class A2UICatalog
{
    private readonly Dictionary<string, A2UIComponentDefinition> _components = new(StringComparer.Ordinal);
    private readonly Dictionary<string, A2UIFunctionDefinition> _functions = new(StringComparer.Ordinal);

    private A2UICatalog(string id, string protocolVersion, JsonObject document)
    {
        Id = id;
        ProtocolVersion = protocolVersion;
        Document = document;
    }

    /// <summary>The catalog id (<c>catalogId</c>), usually URI-shaped.</summary>
    public string Id { get; }

    /// <summary>The protocol version the catalog is written for (<c>1.0</c>).</summary>
    public string ProtocolVersion { get; }

    /// <summary>The catalog's guidance for language models (Markdown), if any.</summary>
    public string? Instructions => A2UIJson.AsString(Document["instructions"]);

    /// <summary>The catalog title, if any.</summary>
    public string? Title => A2UIJson.AsString(Document["title"]);

    /// <summary>The catalog's JSON Schema document. Do not change it.</summary>
    public JsonObject Document { get; }

    /// <summary>The component types.</summary>
    public IReadOnlyDictionary<string, A2UIComponentDefinition> Components => _components;

    /// <summary>The functions.</summary>
    public IReadOnlyDictionary<string, A2UIFunctionDefinition> Functions => _functions;

    /// <summary>The catalog document as JSON text.</summary>
    public string ToJson() => Document.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

    /// <summary>Loads a catalog from its JSON Schema document.</summary>
    public static A2UICatalog FromJson(string json)
    {
        if (A2UIJson.Parse(json) is not JsonObject doc)
        {
            throw A2UIException.Catalog("A catalog document must be a JSON object.");
        }

        return FromJson(doc);
    }

    /// <summary>Loads a catalog from its JSON Schema document (the document is copied).</summary>
    public static A2UICatalog FromJson(JsonObject document)
    {
        var doc = (JsonObject)document.DeepClone();
        var id = A2UIJson.AsString(doc["catalogId"]) ?? A2UIJson.AsString(doc["$id"])
                 ?? throw A2UIException.Catalog("A catalog needs a 'catalogId'.");
        var version = A2UIJson.AsString(doc["protocolVersion"]) ?? "0.9";
        var catalog = new A2UICatalog(id, version, doc);
        A2UICatalogReader.Read(catalog);
        return catalog;
    }

    /// <summary>
    /// Supplies the renderer-side implementation of a function this catalog declares. Returns the catalog for chaining.
    /// </summary>
    public A2UICatalog Implement(string functionName, A2UIFunction implementation)
    {
        if (!_functions.TryGetValue(functionName, out var definition))
        {
            throw new ArgumentException($"Catalog '{Id}' declares no function '{functionName}'.", nameof(functionName));
        }

        definition.Implementation = implementation;
        return this;
    }

    internal void AddComponent(A2UIComponentDefinition definition) => _components[definition.Name] = definition;

    internal void AddFunction(A2UIFunctionDefinition definition) => _functions[definition.Name] = definition;

    public override string ToString() => Id;
}
