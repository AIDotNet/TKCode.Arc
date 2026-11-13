using System.Text.Json.Nodes;

namespace TKCode.Arc.A2UI;

/// <summary>
/// One surface: a component tree, its data model and its node tree. Created by <c>createSurface</c>, changed by
/// <c>updateComponents</c> and <c>updateDataModel</c>, removed by <c>deleteSurface</c>.
/// </summary>
public sealed class A2UISurface
{
    private readonly Dictionary<string, A2UIComponentModel> _components = new(StringComparer.Ordinal);

    internal A2UISurface(A2UIRenderer renderer, string id, A2UICatalog? defaultCatalog, IReadOnlyList<A2UICatalog> available,
                         bool sendDataModel, JsonObject? metadata)
    {
        Renderer = renderer;
        Id = id;
        DefaultCatalog = defaultCatalog;
        AvailableCatalogs = available;
        SendDataModel = sendDataModel;
        Metadata = metadata;
        DataModel = new A2UIDataModel();
        Nodes = new A2UINodeResolver(this);
    }

    /// <summary>The renderer that owns the surface.</summary>
    public A2UIRenderer Renderer { get; }

    /// <summary>The surface id.</summary>
    public string Id { get; }

    /// <summary>The protocol version the surface was created with.</summary>
    public string ProtocolVersion => "1.0";

    /// <summary>The surface's default catalog (null when <c>createSurface</c> named none).</summary>
    public A2UICatalog? DefaultCatalog { get; }

    /// <summary>The catalogs components and calls on this surface may use.</summary>
    public IReadOnlyList<A2UICatalog> AvailableCatalogs { get; }

    /// <summary>True when the agent asked for the data model with every outbound message.</summary>
    public bool SendDataModel { get; }

    /// <summary>The <c>metadata</c> of <c>createSurface</c>, if any.</summary>
    public JsonObject? Metadata { get; }

    /// <summary>The surface's data.</summary>
    public A2UIDataModel DataModel { get; }

    /// <summary>The components, by id.</summary>
    public IReadOnlyDictionary<string, A2UIComponentModel> Components => _components;

    /// <summary>The root node (the component with id <c>root</c>), or null until it arrives.</summary>
    public A2UINode? Root => Nodes.Root;

    /// <summary>True after <c>deleteSurface</c>.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Raised when the root node appears, is replaced or goes away.</summary>
    public event EventHandler? RootChanged;

    /// <summary>Raised after components were added or replaced, with their ids.</summary>
    public event EventHandler<IReadOnlyCollection<string>>? ComponentsChanged;

    internal A2UINodeResolver Nodes { get; }

    internal void ApplyComponents(IReadOnlyList<A2UIComponentModel> models)
    {
        var ids = new List<string>(models.Count);
        foreach (var model in models)
        {
            _components[model.Id] = model;
            ids.Add(model.Id);
        }

        Nodes.OnComponentsChanged(ids);
        ComponentsChanged?.Invoke(this, ids);
    }

    internal void RaiseRootChanged() => RootChanged?.Invoke(this, EventArgs.Empty);

    internal void Delete()
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        Nodes.Dispose();
        DataModel.Dispose();
        RaiseRootChanged();
    }

    /// <summary>
    /// Runs the action of a component property (default <c>action</c>) as the user's gesture: an event is sent to the
    /// agent, a local function call runs. Returns false when the action was not run (a failed check, an error).
    /// </summary>
    public bool DispatchAction(A2UINode node, string property = "action") => Renderer.DispatchAction(this, node, property, userActivation: true);

    public override string ToString() => Id;
}
