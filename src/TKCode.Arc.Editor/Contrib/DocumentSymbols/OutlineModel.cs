// Ported from vscode (src/vs/editor/contrib/documentSymbols/browser/outlineModel.ts: TreeElement, OutlineElement,
// OutlineGroup, OutlineModel; commit b57d5c9e), MIT license.
// Differences:
//  - children are an OutlineChildren map that keeps insertion order (JavaScript's Map);
//  - the marker of an element is OutlineMarkerInfo (count and top severity);
//  - OutlineModel.Create re-runs when the registry's providers change while it waits (as upstream) through the
//    registry's DidChange event; a provider that throws is reported to UnexpectedErrors;
//  - the model's uri is the TextModel's Uri.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform.Markers;

namespace TKCode.Arc.Editor.Contrib.DocumentSymbols;

/// <summary>A map that iterates in insertion order (JavaScript's Map).</summary>
internal sealed class OutlineChildren<T> : IEnumerable<KeyValuePair<string, T>>
    where T : TreeElement
{
    private readonly Dictionary<string, T> _map = new(StringComparer.Ordinal);
    private readonly List<string> _order = [];

    public int Count => _map.Count;

    public IEnumerable<T> Values => _order.Select(key => _map[key]);

    public T? Get(string id) => _map.GetValueOrDefault(id);

    public bool Has(string id) => _map.ContainsKey(id);

    public void Set(string id, T value)
    {
        if (!_map.ContainsKey(id))
        {
            _order.Add(id);
        }

        _map[id] = value;
    }

    public bool Delete(string id)
    {
        if (_map.Remove(id))
        {
            _order.Remove(id);
            return true;
        }

        return false;
    }

    public void Clear()
    {
        _map.Clear();
        _order.Clear();
    }

    public IEnumerator<KeyValuePair<string, T>> GetEnumerator()
    {
        foreach (var key in _order.ToArray())
        {
            yield return new KeyValuePair<string, T>(key, _map[key]);
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>A node of the outline (upstream's TreeElement).</summary>
internal abstract class TreeElement
{
    public abstract string Id { get; }

    public abstract TreeElement? Parent { get; set; }

    /// <summary>The children, in the order they were added.</summary>
    public abstract IEnumerable<TreeElement> ChildElements { get; }

    internal abstract bool HasChild(string id);

    internal abstract void RemoveChild(string id);

    internal abstract int ChildCount { get; }

    public void Remove() => Parent?.RemoveChild(Id);

    /// <summary>
    /// A unique id for a symbol (or a provider group) inside <paramref name="container"/>: the container's id and the
    /// name, made unique with the start position and a counter (upstream's findId).
    /// </summary>
    public static string FindId(DocumentSymbol candidate, TreeElement container)
    {
        var candidateId = $"{container.Id}/{candidate.Name}";
        if (container.HasChild(candidateId))
        {
            candidateId = $"{container.Id}/{candidate.Name}_{candidate.Range.StartLineNumber}_{candidate.Range.StartColumn}";
        }

        return Dedupe(candidateId, container);
    }

    /// <summary>upstream's findId with a string candidate.</summary>
    public static string FindId(string candidate, TreeElement container) => Dedupe($"{container.Id}/{candidate}", container);

    private static string Dedupe(string candidateId, TreeElement container)
    {
        var id = candidateId;
        for (var i = 0; container.HasChild(id); i++)
        {
            id = $"{candidateId}_{i}";
        }

        return id;
    }

    /// <summary>The element with the id among <paramref name="element"/> and its descendants (upstream's getElementById).</summary>
    public static TreeElement? GetElementById(string id, TreeElement element)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var len = CommonPrefixLength(id, element.Id);
        if (len == id.Length)
        {
            return element;
        }

        if (len < element.Id.Length)
        {
            return null;
        }

        foreach (var child in element.ChildElements)
        {
            if (GetElementById(id, child) is { } candidate)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>The number of elements in the subtree (upstream's size).</summary>
    public static int Size(TreeElement element)
    {
        var res = 1;
        foreach (var child in element.ChildElements)
        {
            res += Size(child);
        }

        return res;
    }

    /// <summary>Whether the element has no children (upstream's empty).</summary>
    public static bool Empty(TreeElement element) => element.ChildCount == 0;

    private static int CommonPrefixLength(string a, string b)
    {
        var len = Math.Min(a.Length, b.Length);
        for (var i = 0; i < len; i++)
        {
            if (a[i] != b[i])
            {
                return i;
            }
        }

        return len;
    }
}

/// <summary>A marker's range and severity for the outline (upstream's IOutlineMarker).</summary>
internal readonly record struct OutlineMarker(TextRange Range, MarkerSeverity Severity);

/// <summary>How many markers an element has and the worst one (upstream's OutlineElement.marker).</summary>
internal sealed record OutlineMarkerInfo(int Count, MarkerSeverity TopSev);

/// <summary>A symbol in the outline (upstream's OutlineElement).</summary>
internal sealed class OutlineElement(string id, TreeElement? parent, DocumentSymbol symbol) : TreeElement
{
    public override string Id { get; } = id;

    public override TreeElement? Parent { get; set; } = parent;

    public DocumentSymbol Symbol { get; } = symbol;

    public OutlineChildren<OutlineElement> Children { get; } = new();

    public OutlineMarkerInfo? Marker { get; set; }

    public override IEnumerable<TreeElement> ChildElements => Children.Values;

    internal override bool HasChild(string childId) => Children.Has(childId);

    internal override void RemoveChild(string childId) => Children.Delete(childId);

    internal override int ChildCount => Children.Count;
}

/// <summary>The symbols of one provider (upstream's OutlineGroup).</summary>
internal sealed class OutlineGroup(string id, TreeElement? parent, string label, int order) : TreeElement
{
    public override string Id { get; } = id;

    public override TreeElement? Parent { get; set; } = parent;

    public string Label { get; } = label;

    public int Order { get; } = order;

    public OutlineChildren<OutlineElement> Children { get; } = new();

    public override IEnumerable<TreeElement> ChildElements => Children.Values;

    internal override bool HasChild(string childId) => Children.Has(childId);

    internal override void RemoveChild(string childId) => Children.Delete(childId);

    internal override int ChildCount => Children.Count;

    /// <summary>The innermost element whose range contains <paramref name="position"/> (upstream's getItemEnclosingPosition).</summary>
    public OutlineElement? GetItemEnclosingPosition(TextPosition position) => GetItemEnclosingPosition(position, Children);

    private static OutlineElement? GetItemEnclosingPosition(TextPosition position, OutlineChildren<OutlineElement> children)
    {
        foreach (var (_, item) in children)
        {
            if (!item.Symbol.Range.ContainsPosition(position))
            {
                continue;
            }

            return GetItemEnclosingPosition(position, item.Children) ?? item;
        }

        return null;
    }

    /// <summary>
    /// Gives every element the markers that intersect its range (upstream's updateMarker); <paramref name="marker"/> is
    /// sorted by start and is emptied of the markers the elements took.
    /// </summary>
    public void UpdateMarker(List<OutlineMarker> marker)
    {
        foreach (var (_, child) in Children)
        {
            UpdateMarker(marker, child);
        }
    }

    private static void UpdateMarker(List<OutlineMarker> markers, OutlineElement item)
    {
        item.Marker = null;

        // find the proper start index to check for item/marker overlap.
        var idx = Arrays.BinarySearch(markers.Select(static m => m.Range).ToList(), item.Symbol.Range, TextRange.CompareRangesUsingStarts);
        int start;
        if (idx < 0)
        {
            start = ~idx;
            if (start > 0 && TextRange.AreIntersecting(markers[start - 1].Range, item.Symbol.Range))
            {
                start -= 1;
            }
        }
        else
        {
            start = idx;
        }

        var myMarkers = new List<OutlineMarker>();
        MarkerSeverity? myTopSev = null;
        var taken = new List<int>();

        for (; start < markers.Count && TextRange.AreIntersecting(item.Symbol.Range, markers[start].Range); start++)
        {
            // remove markers intersecting with this outline element
            // and store them in a 'private' array.
            var marker = markers[start];
            myMarkers.Add(marker);
            taken.Add(start);
            if (myTopSev is null || marker.Severity > myTopSev)
            {
                myTopSev = marker.Severity;
            }
        }

        // Recurse into children and let them match markers that have matched
        // this outline element. This might remove markers from this element and
        // therefore we remember that we have had markers. That allows us to render
        // the dot, saying 'this element has children with markers'
        foreach (var (_, child) in item.Children)
        {
            UpdateMarker(myMarkers, child);
        }

        if (myTopSev is { } topSev)
        {
            item.Marker = new OutlineMarkerInfo(myMarkers.Count, topSev);
        }

        // upstream: coalesceInPlace(markers) after setting the taken ones to undefined
        for (var i = taken.Count - 1; i >= 0; i--)
        {
            markers.RemoveAt(taken[i]);
        }
    }
}

/// <summary>The outline of a document: its symbols from every provider (upstream's OutlineModel).</summary>
internal class OutlineModel : TreeElement
{
    private OutlineChildren<OutlineGroup> _groups = new();
    private OutlineChildren<TreeElement> _children = new();

    protected OutlineModel(Uri? uri)
    {
        Uri = uri;
    }

    public override string Id => "root";

    public override TreeElement? Parent
    {
        get => null;
        set
        {
        }
    }

    public Uri? Uri { get; }

    /// <summary>The groups (several providers) or the elements of the only provider.</summary>
    public OutlineChildren<TreeElement> Children => _children;

    /// <summary>The provider groups (for tests).</summary>
    protected OutlineChildren<OutlineGroup> Groups
    {
        get => _groups;
        set => _groups = value;
    }

    public override IEnumerable<TreeElement> ChildElements => _children.Values;

    internal override bool HasChild(string childId) => _children.Has(childId);

    internal override void RemoveChild(string childId) => _children.Delete(childId);

    internal override int ChildCount => _children.Count;

    /// <summary>
    /// Asks every provider of <paramref name="registry"/> for the symbols of <paramref name="textModel"/> and builds the
    /// outline (upstream's OutlineModel.create); starts over when the providers change before all have answered.
    /// </summary>
    public static async Task<OutlineModel> Create(LanguageFeatureRegistry<IDocumentSymbolProvider> registry, TextModel textModel, CancellationToken token)
    {
        while (true)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var result = new OutlineModel(textModel.Uri);
            var provider = registry.Ordered(textModel);
            var tasks = new List<Task>();
            for (var index = 0; index < provider.Count; index++)
            {
                var id = FindId($"provider_{index}", result);
                var group = new OutlineGroup(id, result, provider[index].DisplayName ?? "Unknown Outline Provider", index);
                tasks.Add(FillGroup(provider[index], textModel, cts.Token, result, id, group));
            }

            void OnDidChange(int count)
            {
                var newProvider = registry.Ordered(textModel);
                if (!newProvider.SequenceEqual(provider))
                {
                    cts.Cancel();
                }
            }

            registry.DidChange += OnDidChange;
            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(true);
            }
            finally
            {
                registry.DidChange -= OnDidChange;
            }

            if (cts.Token.IsCancellationRequested && !token.IsCancellationRequested)
            {
                continue;
            }

            return result.Compact();
        }
    }

    private static async Task FillGroup(IDocumentSymbolProvider provider, TextModel textModel, CancellationToken token, OutlineModel result, string id, OutlineGroup group)
    {
        try
        {
            var symbols = await provider.ProvideDocumentSymbols(textModel, token).ConfigureAwait(true);
            foreach (var info in symbols ?? [])
            {
                MakeOutlineElement(info, group);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            UnexpectedErrors.Report(e);
        }

        if (!Empty(group))
        {
            result._groups.Set(id, group);
        }
        else
        {
            group.Remove();
        }
    }

    private static void MakeOutlineElement(DocumentSymbol info, TreeElement container)
    {
        var id = FindId(info, container);
        var res = new OutlineElement(id, container, info);
        if (info.Children is { } children)
        {
            foreach (var childInfo in children)
            {
                MakeOutlineElement(childInfo, res);
            }
        }

        switch (container)
        {
            case OutlineGroup group:
                group.Children.Set(res.Id, res);
                break;
            case OutlineElement element:
                element.Children.Set(res.Id, res);
                break;
        }
    }

    /// <summary>The outline model an element belongs to (upstream's OutlineModel.get).</summary>
    public static OutlineModel? Get(TreeElement? element)
    {
        while (element is not null)
        {
            if (element is OutlineModel model)
            {
                return model;
            }

            element = element.Parent;
        }

        return null;
    }

    private OutlineModel Compact()
    {
        var count = 0;
        foreach (var (key, group) in _groups)
        {
            if (group.Children.Count == 0)
            {
                // empty
                _groups.Delete(key);
            }
            else
            {
                count += 1;
            }
        }

        if (count != 1)
        {
            _children = new OutlineChildren<TreeElement>();
            foreach (var (key, group) in _groups)
            {
                _children.Set(key, group);
            }
        }
        else
        {
            // adopt all elements of the first group
            var group = _groups.Values.First();
            foreach (var (_, child) in group.Children)
            {
                child.Parent = this;
                _children.Set(child.Id, child);
            }
        }

        return this;
    }

    /// <summary>Takes the groups of a newer model of the same document with as many groups (upstream's merge).</summary>
    public bool Merge(OutlineModel other)
    {
        if (Uri != other.Uri)
        {
            return false;
        }

        if (_groups.Count != other._groups.Count)
        {
            return false;
        }

        _groups = other._groups;
        _children = other._children;
        return true;
    }

    /// <summary>The innermost element at <paramref name="position"/>, preferring the group of <paramref name="context"/> (upstream's getItemEnclosingPosition).</summary>
    public OutlineElement? GetItemEnclosingPosition(TextPosition position, OutlineElement? context = null)
    {
        OutlineGroup? preferredGroup = null;
        if (context is not null)
        {
            var candidate = context.Parent;
            while (candidate is not null && preferredGroup is null)
            {
                if (candidate is OutlineGroup group)
                {
                    preferredGroup = group;
                }

                candidate = candidate.Parent;
            }
        }

        OutlineElement? result = null;
        foreach (var (_, group) in _groups)
        {
            result = group.GetItemEnclosingPosition(position);
            if (result is not null && (preferredGroup is null || preferredGroup == group))
            {
                break;
            }
        }

        return result;
    }

    /// <summary>The element with the id (upstream's getItemById).</summary>
    public TreeElement? GetItemById(string id) => GetElementById(id, this);

    /// <summary>Gives the elements of every group the markers that intersect them (upstream's updateMarker).</summary>
    public void UpdateMarker(List<OutlineMarker> marker)
    {
        // sort markers by start range so that we can use
        // outline element starts for quicker look up
        marker.Sort(static (a, b) => TextRange.CompareRangesUsingStarts(a.Range, b.Range));

        foreach (var (_, group) in _groups)
        {
            group.UpdateMarker([.. marker]);
        }
    }

    /// <summary>The top-level symbols, by start (upstream's getTopLevelSymbols).</summary>
    public List<DocumentSymbol> GetTopLevelSymbols()
    {
        var roots = new List<DocumentSymbol>();
        foreach (var child in _children.Values)
        {
            if (child is OutlineElement element)
            {
                roots.Add(element.Symbol);
            }
            else if (child is OutlineGroup group)
            {
                roots.AddRange(group.Children.Values.Select(static c => c.Symbol));
            }
        }

        roots.Sort(static (a, b) => TextRange.CompareRangesUsingStarts(a.Range, b.Range));
        return roots;
    }

    /// <summary>Every symbol, flattened with its container's name, by start then by end descending (upstream's asListOfDocumentSymbols).</summary>
    public List<DocumentSymbol> AsListOfDocumentSymbols()
    {
        var roots = GetTopLevelSymbols();
        var bucket = new List<DocumentSymbol>();
        FlattenDocumentSymbols(bucket, roots, string.Empty);
        return [.. bucket.OrderBy(static s => s.Range.Start).ThenByDescending(static s => s.Range.End)];
    }

    private static void FlattenDocumentSymbols(List<DocumentSymbol> bucket, IReadOnlyList<DocumentSymbol> entries, string overrideContainerLabel)
    {
        foreach (var entry in entries)
        {
            bucket.Add(entry with
            {
                ContainerName = string.IsNullOrEmpty(entry.ContainerName) ? overrideContainerLabel : entry.ContainerName,
                Children = null, // we flatten it...
            });

            // Recurse over children
            if (entry.Children is { } children)
            {
                FlattenDocumentSymbols(bucket, children, entry.Name);
            }
        }
    }
}
