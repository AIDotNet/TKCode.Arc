using Avalonia.Controls;

namespace TKCode.Arc.A2UI;

/// <summary>
/// The view of one component type: builds an Arc control for a node and keeps it in step with the node. Instances
/// are stateful and created per node; <see cref="Update"/> changes the existing control instead of rebuilding it.
/// </summary>
public interface IA2UIComponentView
{
    /// <summary>The control that shows the node (created in <see cref="Attach"/>).</summary>
    Control Control { get; }

    /// <summary>Creates the control and applies the node's current values.</summary>
    void Attach(A2UINode node, A2UIBuildContext context);

    /// <summary>Applies a change of the node (values, children). The node instance is the same as in <see cref="Attach"/>.</summary>
    void Update(A2UINode node, A2UINodeChangedEventArgs change);

    /// <summary>Releases subscriptions and resources; the control is about to leave the tree.</summary>
    void Detach();
}

/// <summary>A convenient base for component views: implement <see cref="Create"/> and <see cref="Apply"/>.</summary>
/// <typeparam name="TControl">The control type.</typeparam>
public abstract class A2UIComponentView<TControl> : IA2UIComponentView where TControl : Control
{
    private TControl? _control;

    /// <summary>The control (valid after attach).</summary>
    protected TControl View => _control ?? throw new InvalidOperationException("The view is not attached.");

    /// <summary>The node shown.</summary>
    protected A2UINode Node { get; private set; } = null!;

    /// <summary>The build context.</summary>
    protected A2UIBuildContext Context { get; private set; } = null!;

    Control IA2UIComponentView.Control => View;

    /// <summary>True while values are pushed into the control, so change handlers can ignore their own echo.</summary>
    protected bool IsApplying { get; private set; }

    public void Attach(A2UINode node, A2UIBuildContext context)
    {
        Node = node;
        Context = context;
        _control = Create();
        RunApply(new A2UINodeChangedEventArgs(true, true, true, false));
    }

    public void Update(A2UINode node, A2UINodeChangedEventArgs change)
    {
        Node = node;
        RunApply(change);
    }

    public virtual void Detach()
    {
    }

    /// <summary>Creates the control (once).</summary>
    protected abstract TControl Create();

    /// <summary>Pushes the node's values (and children, when <paramref name="change"/> says so) into the control.</summary>
    protected abstract void Apply(A2UINodeChangedEventArgs change);

    private void RunApply(A2UINodeChangedEventArgs change)
    {
        IsApplying = true;
        try
        {
            Apply(change);
        }
        finally
        {
            IsApplying = false;
        }
    }
}

/// <summary>Creates component views by catalog and type.</summary>
public sealed class A2UIComponentRegistry
{
    private readonly Dictionary<(string Catalog, string Type), Func<IA2UIComponentView>> _byCatalog = new();
    private readonly Dictionary<string, Func<IA2UIComponentView>> _byType = new(StringComparer.Ordinal);

    /// <summary>The registry with the views of the basic and the Arc catalogs; apps add their own with <see cref="Register"/>.</summary>
    public static A2UIComponentRegistry Default { get; } = CreateDefault();

    /// <summary>A new registry with the views of the basic and the Arc catalogs.</summary>
    public static A2UIComponentRegistry CreateDefault()
    {
        var registry = new A2UIComponentRegistry();
        A2UIBasicViews.RegisterAll(registry);
        A2UIArcViews.RegisterAll(registry);
        return registry;
    }

    /// <summary>Registers the view of a component type in one catalog (or, with a null catalog id, in any catalog).</summary>
    public A2UIComponentRegistry Register(string? catalogId, string type, Func<IA2UIComponentView> factory)
    {
        if (catalogId is null)
        {
            _byType[type] = factory;
        }
        else
        {
            _byCatalog[(catalogId, type)] = factory;
        }

        return this;
    }

    /// <summary>A new view for the node's type, or null when none is registered.</summary>
    public IA2UIComponentView? Create(A2UINode node)
    {
        var catalog = node.Component?.Catalog?.Id;
        if (catalog is not null && _byCatalog.TryGetValue((catalog, node.Type), out var factory))
        {
            return factory();
        }

        return _byType.TryGetValue(node.Type, out factory) ? factory() : null;
    }
}

/// <summary>What a component view can use while building: child views, the surface and the renderer.</summary>
public sealed class A2UIBuildContext
{
    private readonly Dictionary<A2UINode, A2UINodePresenter> _presenters = new();

    internal A2UIBuildContext(A2UISurfaceView view, A2UISurface surface, A2UIComponentRegistry registry)
    {
        View = view;
        Surface = surface;
        Registry = registry;
    }

    /// <summary>The surface view.</summary>
    public A2UISurfaceView View { get; }

    /// <summary>The surface.</summary>
    public A2UISurface Surface { get; }

    /// <summary>The renderer.</summary>
    public A2UIRenderer Renderer => Surface.Renderer;

    /// <summary>The view registry.</summary>
    public A2UIComponentRegistry Registry { get; }

    /// <summary>True after the surface's first frame: nodes arriving later play the enter animation.</summary>
    internal bool IsStreaming { get; set; }

    /// <summary>The control of a child node; the same node always gets the same control.</summary>
    public Control BuildChild(A2UINode child)
    {
        if (!_presenters.TryGetValue(child, out var presenter))
        {
            presenter = new A2UINodePresenter(this, child);
            _presenters[child] = presenter;
            child.Destroyed += (_, _) =>
            {
                if (_presenters.Remove(child, out var gone))
                {
                    gone.Release();
                }
            };
        }

        return presenter;
    }

    /// <summary>The controls of a list of child nodes, in order.</summary>
    public IReadOnlyList<Control> BuildChildren(IReadOnlyList<A2UINode> children)
    {
        var list = new List<Control>(children.Count);
        foreach (var child in children)
        {
            list.Add(BuildChild(child));
        }

        return list;
    }

    internal void ReleaseAll()
    {
        foreach (var presenter in _presenters.Values.ToList())
        {
            presenter.Release();
        }

        _presenters.Clear();
    }
}
