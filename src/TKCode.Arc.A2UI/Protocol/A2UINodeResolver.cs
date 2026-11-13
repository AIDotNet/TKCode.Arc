using System.Text.Json.Nodes;

namespace TKCode.Arc.A2UI;

/// <summary>
/// Builds and maintains the node tree of a surface: resolves property values, child slots and list templates, and
/// re-resolves exactly the nodes whose components or data changed.
/// </summary>
internal sealed class A2UINodeResolver : IDisposable
{
    private readonly A2UISurface _surface;
    private readonly HashSet<A2UINode> _live = new();
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);
    private bool _disposed;

    public A2UINodeResolver(A2UISurface surface)
    {
        _surface = surface;
        surface.DataModel.Changed += OnDataChanged;
    }

    public A2UINode? Root { get; private set; }

    public int LiveCount => _live.Count;

    private A2UIRenderer Renderer => _surface.Renderer;

    public void OnComponentsChanged(IReadOnlyCollection<string> ids)
    {
        if (_disposed)
        {
            return;
        }

        if (Root is null)
        {
            if (_surface.Components.ContainsKey("root"))
            {
                Root = Create(null, "root", "root", "/", null);
                _surface.RaiseRootChanged();
            }

            return;
        }

        var set = ids as ISet<string> ?? new HashSet<string>(ids, StringComparer.Ordinal);
        foreach (var node in _live.Where(n => !n.IsDisposed && set.Contains(n.ComponentId)).ToList())
        {
            if (!node.IsDisposed)
            {
                Refresh(node, componentChanged: true);
            }
        }
    }

    public void OnRpcSettled(string key)
    {
        foreach (var node in _live.Where(n => n.PendingCalls.Contains(key)).ToList())
        {
            if (!node.IsDisposed)
            {
                Refresh(node, componentChanged: false);
            }
        }
    }

    private void OnDataChanged(string path)
    {
        if (_disposed)
        {
            return;
        }

        foreach (var node in _live.Where(n => Touches(n, path)).ToList())
        {
            if (!node.IsDisposed)
            {
                Refresh(node, componentChanged: false);
            }
        }
    }

    private static bool Touches(A2UINode node, string path)
    {
        foreach (var read in node.Reads)
        {
            if (A2UIJsonPointer.IsAncestorOrSelf(path, read) || A2UIJsonPointer.IsAncestorOrSelf(read, path))
            {
                return true;
            }
        }

        return false;
    }

    private A2UINode Create(A2UINode? parent, string instanceId, string componentId, string dataPath, int? index)
    {
        var node = new A2UINode(_surface, parent, instanceId, componentId, dataPath, index);
        _live.Add(node);
        node.Destroyed += (_, _) => _live.Remove(node);
        Evaluate(node, out _, out _);
        return node;
    }

    private void Refresh(A2UINode node, bool componentChanged)
    {
        var oldType = node.Type;
        var oldState = node.State;
        var oldProps = node.Props;
        var oldValidation = node.Validation;
        var oldA11y = node.Accessibility;
        Evaluate(node, out var childrenChanged, out _);
        var propsChanged = !A2UIJson.DeepEquals(oldProps, node.Props) || !oldValidation.SequenceEqual(node.Validation) || oldA11y != node.Accessibility;
        var stateChanged = oldState != node.State;
        var typeChanged = componentChanged && oldType != node.Type && oldType.Length > 0;
        if (propsChanged || childrenChanged || stateChanged || typeChanged)
        {
            node.RaiseChanged(new A2UINodeChangedEventArgs(propsChanged, childrenChanged, stateChanged, typeChanged));
        }
    }

    // Resolves the node's component, values, checks, accessibility and children.
    private void Evaluate(A2UINode node, out bool childrenChanged, out bool unused)
    {
        unused = false;
        childrenChanged = false;
        _surface.Components.TryGetValue(node.ComponentId, out var component);
        node.Component = component;
        node.Reads = new HashSet<string>(StringComparer.Ordinal);
        node.PendingCalls = new HashSet<string>(StringComparer.Ordinal);
        if (component is null)
        {
            node.State = A2UINodeState.Pending;
            node.Props = new JsonObject();
            childrenChanged = ClearChildren(node);
            return;
        }

        if (IsCyclic(node))
        {
            node.State = A2UINodeState.Cyclic;
            node.Props = new JsonObject();
            Report(node, "cycle", new A2UIException(A2UIErrorCategory.Recursion, A2UIErrorCodes.ValidationFailed,
                                                    $"Component '{node.ComponentId}' is its own ancestor.", _surface.Id));
            childrenChanged = ClearChildren(node);
            return;
        }

        if (component.Definition is not { } definition)
        {
            node.State = A2UINodeState.UnknownType;
            node.Props = new JsonObject();
            Report(node, "type", A2UIException.Validation($"Unknown component type '{component.Type}'.", _surface.Id));
            childrenChanged = ClearChildren(node);
            return;
        }

        node.State = A2UINodeState.Resolved;
        var scope = new A2UIScope(_surface, _surface.DataModel, node.DataPath, node.Index ?? InheritedIndex(node));
        var ev = new A2UIEvaluation();
        var props = new JsonObject();
        foreach (var (name, value) in component.Properties)
        {
            var shape = definition.Properties.TryGetValue(name, out var s) ? s : A2UIPropertyShape.LiteralShape;
            if (shape.Kind is A2UIPropertyKind.Child or A2UIPropertyKind.ChildList or A2UIPropertyKind.Action)
            {
                props[name] = value?.DeepClone();
                continue;
            }

            try
            {
                props[name] = Renderer.Evaluator.Resolve(value, shape, scope, ev)?.DeepClone();
            }
            catch (A2UIException ex)
            {
                props[name] = null;
                Report(node, name, ex);
            }
        }

        // Defaults for absent properties, so views read one place.
        foreach (var (name, shape) in definition.Properties)
        {
            if (!props.ContainsKey(name) && shape.Default is { } defaultValue)
            {
                props[name] = defaultValue.DeepClone();
            }
        }

        node.Props = props;
        node.Validation = EvaluateChecks(node, component, scope, ev);
        node.Accessibility = EvaluateAccessibility(node, component, scope, ev);
        childrenChanged = ResolveChildren(node, component, definition, scope, ev);
        node.Reads = ev.Reads;
        node.PendingCalls = ev.PendingCalls;
    }

    private static int? InheritedIndex(A2UINode node)
    {
        for (var p = node.Parent; p is not null; p = p.Parent)
        {
            if (p.Index is { } i)
            {
                return i;
            }
        }

        return null;
    }

    private static bool IsCyclic(A2UINode node)
    {
        for (var p = node.Parent; p is not null; p = p.Parent)
        {
            if (p.ComponentId == node.ComponentId && p.DataPath == node.DataPath)
            {
                return true;
            }
        }

        return false;
    }

    private List<A2UIValidationResult> EvaluateChecks(A2UINode node, A2UIComponentModel component, A2UIScope scope, A2UIEvaluation ev)
    {
        if (component.Checks is not { Count: > 0 } checks)
        {
            return [];
        }

        var results = new List<A2UIValidationResult>(checks.Count);
        foreach (var ruleNode in checks)
        {
            if (ruleNode is not JsonObject rule)
            {
                continue;
            }

            var message = A2UIJson.AsString(rule["message"]);
            JsonNode? value;
            try
            {
                value = Renderer.Evaluator.ResolveDynamic(rule["condition"], scope, ev, deep: false);
            }
            catch (A2UIException ex)
            {
                Report(node, "@checks", ex);
                results.Add(new A2UIValidationResult(false, message ?? ex.Message));
                continue;
            }

            if (value is null && ev.PendingCalls.Count > 0)
            {
                // Waiting for the agent: not shown as invalid.
                results.Add(new A2UIValidationResult(true, null));
                continue;
            }

            if (value is JsonObject result && result.ContainsKey("valid"))
            {
                var valid = A2UIJson.ToBoolean(result["valid"]);
                results.Add(new A2UIValidationResult(valid, valid ? null : A2UIJson.AsString(result["message"]) ?? message,
                                                     A2UIJson.AsString(result["severity"]) ?? "error", A2UIJson.AsString(result["code"])));
            }
            else
            {
                var valid = A2UIJson.ToBoolean(value);
                results.Add(new A2UIValidationResult(valid, valid ? null : message));
            }
        }

        return results;
    }

    private A2UIAccessibility EvaluateAccessibility(A2UINode node, A2UIComponentModel component, A2UIScope scope, A2UIEvaluation ev)
    {
        if (component.Accessibility is not { } a11y)
        {
            return A2UIAccessibility.None;
        }

        try
        {
            var label = Renderer.Evaluator.ResolveDynamic(a11y["label"], scope, ev, deep: false);
            var description = Renderer.Evaluator.ResolveDynamic(a11y["description"], scope, ev, deep: false);
            var hidden = a11y["hidden"] is { } h && A2UIJson.ToBoolean(Renderer.Evaluator.ResolveDynamic(h, scope, ev, deep: false));
            return new A2UIAccessibility(label is null ? null : A2UIJson.ToDisplayString(label),
                                         description is null ? null : A2UIJson.ToDisplayString(description),
                                         A2UIJson.AsString(a11y["live"]) ?? "off", hidden);
        }
        catch (A2UIException ex)
        {
            Report(node, "@accessibility", ex);
            return A2UIAccessibility.None;
        }
    }

    // Single slots and lists, reusing child nodes whose key and component stay the same.
    private bool ResolveChildren(A2UINode node, A2UIComponentModel component, A2UIComponentDefinition definition, A2UIScope scope, A2UIEvaluation ev)
    {
        var newSlots = new Dictionary<string, string>(StringComparer.Ordinal);
        var newLists = new Dictionary<string, List<(string Key, string ComponentId, string DataPath, int? Index)>>(StringComparer.Ordinal);
        foreach (var (name, shape) in definition.Properties)
        {
            if (!shape.HasChildren || component.Properties[name] is not { } value)
            {
                continue;
            }

            CollectSlots(node, name, value, shape, scope, ev, newSlots, newLists);
        }

        var changed = false;

        // Single slots.
        foreach (var slot in node.Slots.Keys.Where(k => !newSlots.ContainsKey(k)).ToList())
        {
            node.Slots[slot].Dispose();
            node.Slots.Remove(slot);
            changed = true;
        }

        foreach (var (slot, childId) in newSlots)
        {
            if (node.Slots.TryGetValue(slot, out var existing) && existing.ComponentId == childId)
            {
                continue;
            }

            existing?.Dispose();
            node.Slots[slot] = Create(node, node.InstanceId + "/" + slot, childId, node.DataPath, null);
            changed = true;
        }

        // Lists.
        foreach (var list in node.Lists.Keys.Where(k => !newLists.ContainsKey(k)).ToList())
        {
            foreach (var child in node.Lists[list])
            {
                child.Dispose();
            }

            node.Lists.Remove(list);
            changed = true;
        }

        foreach (var (list, entries) in newLists)
        {
            node.Lists.TryGetValue(list, out var old);
            var byKey = old?.ToDictionary(n => n.InstanceId, StringComparer.Ordinal) ?? new Dictionary<string, A2UINode>(StringComparer.Ordinal);
            var result = new List<A2UINode>(entries.Count);
            foreach (var (key, childId, dataPath, index) in entries)
            {
                if (byKey.Remove(key, out var reuse) && reuse.ComponentId == childId && reuse.DataPath == dataPath)
                {
                    result.Add(reuse);
                }
                else
                {
                    reuse?.Dispose();
                    result.Add(Create(node, key, childId, dataPath, index));
                    changed = true;
                }
            }

            foreach (var stale in byKey.Values)
            {
                stale.Dispose();
                changed = true;
            }

            if (old is null || !old.SequenceEqual(result))
            {
                changed = true;
            }

            node.Lists[list] = result;
        }

        return changed;
    }

    private void CollectSlots(A2UINode node, string path, JsonNode? value, A2UIPropertyShape shape, A2UIScope scope, A2UIEvaluation ev,
                              Dictionary<string, string> slots, Dictionary<string, List<(string, string, string, int?)>> lists)
    {
        switch (shape.Kind)
        {
            case A2UIPropertyKind.Child when A2UIJson.AsString(value) is { } id:
                slots[path] = id;
                break;
            case A2UIPropertyKind.ChildList when value is JsonArray array:
            {
                var entries = new List<(string, string, string, int?)>();
                var seen = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var item in array)
                {
                    if (A2UIJson.AsString(item) is not { } id)
                    {
                        continue;
                    }

                    // The same id listed twice gets two nodes with distinct keys.
                    var n = seen.TryGetValue(id, out var count) ? count + 1 : 0;
                    seen[id] = n;
                    var key = node.InstanceId + "/" + path + "/" + id + (n > 0 ? "#" + n : string.Empty);
                    entries.Add((key, id, node.DataPath, null));
                }

                lists[path] = entries;
                break;
            }
            case A2UIPropertyKind.ChildList when value is JsonObject template:
            {
                var entries = new List<(string, string, string, int?)>();
                if (A2UIJson.AsString(template["componentId"]) is { } templateId && A2UIJson.AsString(template["path"]) is { } listPath)
                {
                    var absolute = A2UIJsonPointer.Resolve(scope.DataPath, listPath);
                    ev.Read(absolute);
                    var data = _surface.DataModel.Peek(absolute);
                    var count = data switch
                    {
                        JsonArray items => items.Count,
                        JsonObject map => map.Count,
                        _ => 0
                    };
                    var limit = Math.Min(count, Renderer.Options.Limits.MaxTemplateItems);
                    if (count > limit)
                    {
                        Report(node, path, new A2UIException(A2UIErrorCategory.Validation, A2UIErrorCodes.LimitExceeded,
                                                             $"The list at '{absolute}' has {count} items; only {limit} are shown.", _surface.Id));
                    }

                    if (data is JsonArray)
                    {
                        for (var i = 0; i < limit; i++)
                        {
                            var itemPath = absolute == "/" ? "/" + i : absolute + "/" + i;
                            entries.Add((node.InstanceId + "/" + path + "/" + templateId + "@" + itemPath, templateId, itemPath, i));
                        }
                    }
                    else if (data is JsonObject map)
                    {
                        var i = 0;
                        foreach (var (key, _) in map)
                        {
                            if (i >= limit)
                            {
                                break;
                            }

                            var itemPath = (absolute == "/" ? string.Empty : absolute) + "/" + A2UIJsonPointer.Escape(key);
                            entries.Add((node.InstanceId + "/" + path + "/" + templateId + "@" + itemPath, templateId, itemPath, i));
                            i++;
                        }
                    }
                }

                lists[path] = entries;
                break;
            }
            case A2UIPropertyKind.Array when value is JsonArray array && shape.Items is { } items:
                for (var i = 0; i < array.Count; i++)
                {
                    CollectSlots(node, path + "/" + i, array[i], items, scope, ev, slots, lists);
                }

                break;
            case A2UIPropertyKind.Object when value is JsonObject obj && shape.Fields is { } fields:
                foreach (var (key, fieldShape) in fields)
                {
                    if (fieldShape.HasChildren)
                    {
                        CollectSlots(node, path + "/" + key, obj[key], fieldShape, scope, ev, slots, lists);
                    }
                }

                break;
        }
    }

    private static bool ClearChildren(A2UINode node)
    {
        if (node.Slots.Count == 0 && node.Lists.Count == 0)
        {
            return false;
        }

        foreach (var child in node.AllChildren().ToList())
        {
            child.Dispose();
        }

        node.Slots.Clear();
        node.Lists.Clear();
        return true;
    }

    private void Report(A2UINode node, string property, A2UIException error)
    {
        var key = node.InstanceId + "|" + property + "|" + error.Message;
        if (_reported.Add(key))
        {
            Renderer.ReportError(error, _surface.Id);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _surface.DataModel.Changed -= OnDataChanged;
        Root?.Dispose();
        Root = null;
        _live.Clear();
    }
}
