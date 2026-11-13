using System.Text.Json.Nodes;

namespace TKCode.Arc.A2UI;

/// <summary>The state of a node.</summary>
public enum A2UINodeState
{
    /// <summary>The component exists and its type is known.</summary>
    Resolved,

    /// <summary>The component is referenced but has not arrived yet.</summary>
    Pending,

    /// <summary>The component's type is not in its catalog.</summary>
    UnknownType,

    /// <summary>The component is its own ancestor.</summary>
    Cyclic
}

/// <summary>The result of one <c>@checks</c> rule.</summary>
public sealed record A2UIValidationResult(bool Valid, string? Message, string Severity = "error", string? Code = null)
{
    /// <summary>True for a failed rule of severity <c>error</c> (it blocks buttons).</summary>
    public bool IsBlocking => !Valid && Severity == "error";
}

/// <summary>Resolved <c>@accessibility</c> attributes.</summary>
public sealed record A2UIAccessibility(string? Label, string? Description, string Live, bool Hidden)
{
    internal static readonly A2UIAccessibility None = new(null, null, "off", false);
}

/// <summary>What changed on a node.</summary>
public sealed class A2UINodeChangedEventArgs(bool props, bool children, bool state, bool type) : EventArgs
{
    /// <summary>Resolved property values, validation or accessibility changed.</summary>
    public bool PropsChanged { get; } = props;

    /// <summary>A child slot points to a different node, or a child list grew, shrank or changed order.</summary>
    public bool ChildrenChanged { get; } = children;

    /// <summary>The state changed (for example a pending node got its component).</summary>
    public bool StateChanged { get; } = state;

    /// <summary>The component's type changed: the view must be rebuilt.</summary>
    public bool TypeChanged { get; } = type;
}

/// <summary>
/// One component instance in the resolved tree. A component used inside a list template has one node per list
/// item, each with its own <see cref="DataPath"/>. Property values are already resolved (bindings read, calls run);
/// children are reached through <see cref="GetChild"/> and <see cref="GetChildren"/>.
/// </summary>
public sealed class A2UINode
{
    internal readonly Dictionary<string, A2UINode> Slots = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, List<A2UINode>> Lists = new(StringComparer.Ordinal);
    internal HashSet<string> Reads = new(StringComparer.Ordinal);
    internal HashSet<string> PendingCalls = new(StringComparer.Ordinal);

    internal A2UINode(A2UISurface surface, A2UINode? parent, string instanceId, string componentId, string dataPath, int? index)
    {
        Surface = surface;
        Parent = parent;
        InstanceId = instanceId;
        ComponentId = componentId;
        DataPath = dataPath;
        Index = index;
    }

    /// <summary>The surface the node belongs to.</summary>
    public A2UISurface Surface { get; }

    /// <summary>The parent node, or null for the root.</summary>
    public A2UINode? Parent { get; }

    /// <summary>A key unique among the node's siblings and stable across updates (use it to reuse views).</summary>
    public string InstanceId { get; }

    /// <summary>The id of the component the node shows.</summary>
    public string ComponentId { get; }

    /// <summary>The component type, or empty while pending.</summary>
    public string Type => Component?.Type ?? string.Empty;

    /// <summary>The node state.</summary>
    public A2UINodeState State { get; internal set; }

    /// <summary>The data scope: <c>/</c>, or the list item path inside a template.</summary>
    public string DataPath { get; }

    /// <summary>The index of the list item inside a template, or null.</summary>
    public int? Index { get; }

    /// <summary>The component, or null while pending.</summary>
    public A2UIComponentModel? Component { get; internal set; }

    /// <summary>The component type's definition, or null.</summary>
    public A2UIComponentDefinition? Definition => Component?.Definition;

    /// <summary>The resolved property values (child and action properties keep their written form).</summary>
    public JsonObject Props { get; internal set; } = new();

    /// <summary>The results of the component's <c>@checks</c>.</summary>
    public IReadOnlyList<A2UIValidationResult> Validation { get; internal set; } = [];

    /// <summary>False when a check of severity error fails.</summary>
    public bool IsValid => !Validation.Any(v => v.IsBlocking);

    /// <summary>The resolved <c>@accessibility</c> attributes.</summary>
    public A2UIAccessibility Accessibility { get; internal set; } = A2UIAccessibility.None;

    /// <summary>True after the node left the tree.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>Raised when the node's values, children or state change.</summary>
    public event EventHandler<A2UINodeChangedEventArgs>? Changed;

    /// <summary>Raised once when the node leaves the tree.</summary>
    public event EventHandler? Destroyed;

    /// <summary>The child in a single-child slot: a property name (<c>child</c>) or a path (<c>tabs/0/child</c>).</summary>
    public A2UINode? GetChild(string slot) => Slots.TryGetValue(slot, out var node) ? node : null;

    /// <summary>The children of a child-list property (<c>children</c>), static or generated by a template.</summary>
    public IReadOnlyList<A2UINode> GetChildren(string property = "children")
        => Lists.TryGetValue(property, out var list) ? list : [];

    /// <summary>All child nodes in slot order (for walking the tree).</summary>
    public IEnumerable<A2UINode> AllChildren()
    {
        foreach (var node in Slots.Values)
        {
            yield return node;
        }

        foreach (var list in Lists.Values)
        {
            foreach (var node in list)
            {
                yield return node;
            }
        }
    }

    /// <summary>A resolved property value, or null.</summary>
    public JsonNode? Get(string property) => Props[property];

    /// <summary>A property as text (numbers and booleans as their text, null as null).</summary>
    public string? GetString(string property) => Props[property] is { } value ? A2UIJson.ToDisplayString(value) : null;

    /// <summary>A property as a number, or null.</summary>
    public double? GetNumber(string property)
    {
        var value = Props[property];
        if (A2UIJson.TryGetDouble(value, out var d))
        {
            return d;
        }

        return A2UIJson.AsString(value) is { } s && double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)
            ? d
            : null;
    }

    /// <summary>A property as a boolean (protocol coercion), or null when absent.</summary>
    public bool? GetBoolean(string property) => Props.ContainsKey(property) ? A2UIJson.ToBoolean(Props[property]) : null;

    /// <summary>A property as a list of strings, or null.</summary>
    public IReadOnlyList<string>? GetStringList(string property)
        => Props[property] is JsonArray array ? array.Select(A2UIJson.ToDisplayString).ToList() : null;

    /// <summary>The written value of a property, as the agent sent it (before resolution).</summary>
    public JsonNode? GetRaw(string property) => Component?.Properties[property];

    /// <summary>The absolute data path a property is bound to, or null when it is not a <c>@path</c> binding.</summary>
    public string? GetBindingPath(string property)
        => GetRaw(property) is JsonObject obj && A2UIJson.AsString(obj["@path"]) is { } path ? A2UIJsonPointer.Resolve(DataPath, path) : null;

    /// <summary>
    /// Writes a value through a property's binding (two-way inputs). Returns false when the property is not bound to a
    /// path; the value then lives only in the control.
    /// </summary>
    public bool Write(string property, JsonNode? value)
    {
        if (IsDisposed || GetBindingPath(property) is not { } path)
        {
            return false;
        }

        Surface.Renderer.WriteData(Surface, path, value);
        return true;
    }

    /// <summary>True when the component has an action in <paramref name="property"/>.</summary>
    public bool HasAction(string property = "action") => GetRaw(property) is JsonObject;

    /// <summary>Runs the component's action as a user gesture (see <see cref="A2UISurface.DispatchAction"/>).</summary>
    public bool InvokeAction(string property = "action") => !IsDisposed && Surface.DispatchAction(this, property);

    internal void RaiseChanged(A2UINodeChangedEventArgs args) => Changed?.Invoke(this, args);

    internal void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        foreach (var child in AllChildren().ToList())
        {
            child.Dispose();
        }

        Slots.Clear();
        Lists.Clear();
        Destroyed?.Invoke(this, EventArgs.Empty);
        Changed = null;
        Destroyed = null;
    }

    public override string ToString() => $"{InstanceId} ({Type}, {State})";
}
