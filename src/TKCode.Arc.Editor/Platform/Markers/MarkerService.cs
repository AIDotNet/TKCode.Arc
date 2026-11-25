// Ported from vscode (src/vs/platform/markers/common/markerService.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - an instance a host owns (an editor, or an application that shares one between editors), not a service from the
//    instantiation container;
//  - onMarkerChanged is the .NET event MarkerChanged and fires synchronously; upstream's MicrotaskEmitter delivers at
//    the end of the microtask, merging the resources of several changes into one event. A listener here gets one event
//    per change (several resources for ChangeAll);
//  - resources are keyed by their absolute URI text (upstream's ResourceMap keys by uri.toString());
//  - the statistics are computed on demand instead of being kept up to date by a listener.

using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc.Editor.Platform.Markers;

/// <summary>
/// Holds the markers (errors, warnings, ...) of documents, by owner (a language service, a linter) and resource (the
/// model's URI), as upstream's IMarkerService. Features read them (hover, go to error) and the
/// <see cref="Services.MarkerDecorationsService"/> turns them into squiggles.
/// </summary>
internal sealed class MarkerService : IDisposable
{
    /// <summary>Schemes whose markers the statistics leave out (upstream's unsupportedSchemas, the ones that exist here).</summary>
    internal static readonly IReadOnlySet<string> UnsupportedSchemas = new HashSet<string>(StringComparer.Ordinal) { "inmemory" };

    private readonly Emitter<IReadOnlyList<Uri>> _onMarkerChanged = new();
    private readonly DoubleResourceMap<Markers> _data = new();
    private readonly Dictionary<string, List<string>> _filteredResources = new(StringComparer.Ordinal);

    /// <summary>Raised with the resources whose markers changed.</summary>
    public event Action<IReadOnlyList<Uri>>? MarkerChanged
    {
        add => _onMarkerChanged.Add(value!);
        remove => _onMarkerChanged.Remove(value);
    }

    /// <summary>The number of resources and of owners with markers (for tests: upstream reads its private maps).</summary>
    internal (int Resources, int Owners) IndexSizes => (_data.ResourceCount, _data.OwnerCount);

    public void Dispose() => _onMarkerChanged.Clear();

    /// <summary>How many markers of each severity there are, leaving out the resources of <see cref="UnsupportedSchemas"/>.</summary>
    public MarkerStatistics GetStatistics()
    {
        int errors = 0, warnings = 0, infos = 0, unknowns = 0;
        foreach (var marker in Read())
        {
            if (UnsupportedSchemas.Contains(LanguagesHelpers.GetScheme(marker.Resource)))
            {
                continue;
            }

            switch (marker.Severity)
            {
                case MarkerSeverity.Error:
                    errors++;
                    break;
                case MarkerSeverity.Warning:
                    warnings++;
                    break;
                case MarkerSeverity.Info:
                    infos++;
                    break;
                default:
                    unknowns++;
                    break;
            }
        }

        return new MarkerStatistics(errors, warnings, infos, unknowns);
    }

    /// <summary>Whether an owner reported only part of the markers of <paramref name="resource"/>.</summary>
    public bool IsSourceTruncated(Uri resource) => _data.Values(resource).Any(markers => markers.IsTruncated);

    /// <summary>Removes the markers of <paramref name="owner"/> for <paramref name="resources"/>.</summary>
    public void Remove(string owner, IReadOnlyList<Uri>? resources)
    {
        foreach (var resource in resources ?? [])
        {
            ChangeOne(owner, resource, []);
        }
    }

    /// <summary>Replaces the markers of <paramref name="owner"/> for <paramref name="resource"/> (none removes them).</summary>
    public void ChangeOne(string owner, Uri resource, IReadOnlyList<MarkerData>? markerData, MarkerChangeOptions? options = null)
    {
        if (markerData is null || markerData.Count == 0)
        {
            // remove marker for this (owner,resource)-tuple
            var removed = _data.Delete(resource, owner);
            if (removed)
            {
                _onMarkerChanged.Fire([resource]);
            }
        }
        else
        {
            // insert marker for this (owner,resource)-tuple
            var markers = new List<Marker>();
            foreach (var data in markerData)
            {
                if (ToMarker(owner, resource, data) is { } marker)
                {
                    markers.Add(marker);
                }
            }

            _data.Set(resource, owner, new Markers(markers, options?.IsTruncated == true));
            _onMarkerChanged.Fire([resource]);
        }
    }

    /// <summary>
    /// Hides the markers of <paramref name="resource"/> behind one information marker that gives
    /// <paramref name="reason"/>, until the result is disposed.
    /// </summary>
    public IDisposable InstallResourceFilter(Uri resource, string reason)
    {
        var key = Key(resource);
        if (!_filteredResources.TryGetValue(key, out var reasons))
        {
            reasons = [];
            _filteredResources[key] = reasons;
        }

        reasons.Add(reason);
        _onMarkerChanged.Fire([resource]);

        return RegistrationDisposable.ToDisposable(() =>
        {
            if (!_filteredResources.TryGetValue(key, out var current))
            {
                return;
            }

            var reasonIndex = current.IndexOf(reason);
            if (reasonIndex != -1)
            {
                current.RemoveAt(reasonIndex);
                if (current.Count == 0)
                {
                    _filteredResources.Remove(key);
                }

                _onMarkerChanged.Fire([resource]);
            }
        });
    }

    private static Marker? ToMarker(string owner, Uri resource, MarkerData data)
    {
        if (string.IsNullOrEmpty(data.Message))
        {
            return null;
        }

        // santize data
        var startLineNumber = data.StartLineNumber > 0 ? data.StartLineNumber : 1;
        var startColumn = data.StartColumn > 0 ? data.StartColumn : 1;
        var endLineNumber = data.EndLineNumber >= startLineNumber ? data.EndLineNumber : startLineNumber;
        var endColumn = data.EndColumn > 0 ? data.EndColumn : startColumn;

        return new Marker
        {
            Resource = resource,
            Owner = owner,
            Code = data.Code,
            CodeTarget = data.CodeTarget,
            Severity = data.Severity,
            Message = data.Message,
            Source = data.Source,
            StartLineNumber = startLineNumber,
            StartColumn = startColumn,
            EndLineNumber = endLineNumber,
            EndColumn = endColumn,
            RelatedInformation = data.RelatedInformation,
            ModelVersionId = data.ModelVersionId,
            Tags = data.Tags,
            Origin = data.Origin,
        };
    }

    /// <summary>Replaces every marker of <paramref name="owner"/>, of every resource, with <paramref name="data"/>.</summary>
    public void ChangeAll(string owner, IReadOnlyList<ResourceMarker>? data)
    {
        var changes = new List<Uri>();

        // remove old marker
        foreach (var existing in _data.Values(owner).ToList())
        {
            if (existing.Items.Count > 0)
            {
                var first = existing.Items[0];
                changes.Add(first.Resource);
                _data.Delete(first.Resource, owner);
            }
        }

        // add new markers
        if (data is { Count: > 0 })
        {
            // group by resource
            var groups = new Dictionary<string, (Uri Resource, List<Marker> Markers)>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (var item in data)
            {
                if (ToMarker(owner, item.Resource, item.Marker) is not { } marker)
                {
                    // filter bad markers
                    continue;
                }

                var key = Key(item.Resource);
                if (!groups.TryGetValue(key, out var group))
                {
                    groups[key] = (item.Resource, [marker]);
                    order.Add(key);
                    changes.Add(item.Resource);
                }
                else
                {
                    group.Markers.Add(marker);
                }
            }

            // insert all
            foreach (var key in order)
            {
                var (resource, value) = groups[key];
                _data.Set(resource, owner, new Markers(value, false));
            }
        }

        if (changes.Count > 0)
        {
            _onMarkerChanged.Fire(changes);
        }
    }

    // Creates an information marker for filtered resources
    private static Marker CreateFilteredMarker(Uri resource, List<string> reasons)
    {
        var message = reasons.Count == 1
            ? $"Problems are paused because: \"{reasons[0]}\""
            : $"Problems are paused because: \"{reasons[0]}\" and {reasons.Count - 1} more";

        return new Marker
        {
            Owner = "markersFilter",
            Resource = resource,
            Severity = MarkerSeverity.Info,
            Message = message,
            StartLineNumber = 1,
            StartColumn = 1,
            EndLineNumber = 1,
            EndColumn = 1,
        };
    }

    /// <summary>The markers that match <paramref name="filter"/> (all of them without one).</summary>
    public IReadOnlyList<Marker> Read(MarkerReadOptions? filter = null)
    {
        filter ??= new MarkerReadOptions();
        var owner = string.IsNullOrEmpty(filter.Owner) ? null : filter.Owner;
        var resource = filter.Resource;
        var severities = filter.Severities;
        var take = filter.Take <= 0 ? -1 : filter.Take;

        if (owner is not null && resource is not null)
        {
            // exactly one owner AND resource
            var reasons = !filter.IgnoreResourceFilters ? GetFilterReasons(resource) : null;
            if (reasons is { Count: > 0 })
            {
                return [CreateFilteredMarker(resource, reasons)];
            }

            var data = _data.Get(resource, owner);
            if (data is null)
            {
                return [];
            }

            var result = new List<Marker>();
            foreach (var marker in data.Items)
            {
                if (take > 0 && result.Count == take)
                {
                    break;
                }

                if (Accept(marker, severities))
                {
                    result.Add(marker);
                }
            }

            return result;
        }
        else
        {
            // of one resource OR owner
            var iterable = owner is null && resource is null
                ? _data.Values()
                : resource is not null ? _data.Values(resource) : _data.Values(owner!);

            var result = new List<Marker>();
            var filtered = new HashSet<string>(StringComparer.Ordinal);

            foreach (var markers in iterable)
            {
                foreach (var data in markers.Items)
                {
                    var key = Key(data.Resource);
                    if (filtered.Contains(key))
                    {
                        continue;
                    }

                    if (take > 0 && result.Count == take)
                    {
                        break;
                    }

                    var reasons = !filter.IgnoreResourceFilters ? GetFilterReasons(data.Resource) : null;
                    if (reasons is { Count: > 0 })
                    {
                        result.Add(CreateFilteredMarker(data.Resource, reasons));
                        filtered.Add(key);
                    }
                    else if (Accept(data, severities))
                    {
                        result.Add(data);
                    }
                }
            }

            return result;
        }
    }

    private List<string>? GetFilterReasons(Uri resource) => _filteredResources.GetValueOrDefault(Key(resource));

    private static bool Accept(Marker marker, MarkerSeverity? severities) =>
        severities is null || (severities.Value & marker.Severity) == marker.Severity;

    /// <summary>The key a resource is stored by (upstream's ResourceMap: uri.toString()).</summary>
    internal static string Key(Uri resource) => resource.IsAbsoluteUri ? resource.AbsoluteUri : resource.OriginalString;

    private sealed record Markers(IReadOnlyList<Marker> Items, bool IsTruncated);

    // upstream's DoubleResourceMap: by resource, then owner; and by owner, then resource
    private sealed class DoubleResourceMap<V>
        where V : class
    {
        private readonly Dictionary<string, Dictionary<string, V>> _byResource = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, V>> _byOwner = new(StringComparer.Ordinal);

        public int ResourceCount => _byResource.Count;

        public int OwnerCount => _byOwner.Count;

        public void Set(Uri resource, string owner, V value)
        {
            var key = Key(resource);
            if (!_byResource.TryGetValue(key, out var ownerMap))
            {
                ownerMap = new Dictionary<string, V>(StringComparer.Ordinal);
                _byResource[key] = ownerMap;
            }

            ownerMap[owner] = value;

            if (!_byOwner.TryGetValue(owner, out var resourceMap))
            {
                resourceMap = new Dictionary<string, V>(StringComparer.Ordinal);
                _byOwner[owner] = resourceMap;
            }

            resourceMap[key] = value;
        }

        public V? Get(Uri resource, string owner) =>
            _byResource.TryGetValue(Key(resource), out var ownerMap) ? ownerMap.GetValueOrDefault(owner) : null;

        public bool Delete(Uri resource, string owner)
        {
            var key = Key(resource);
            var removedA = false;
            var removedB = false;
            if (_byResource.TryGetValue(key, out var ownerMap))
            {
                removedA = ownerMap.Remove(owner);
                if (ownerMap.Count == 0)
                {
                    _byResource.Remove(key);
                }
            }

            if (_byOwner.TryGetValue(owner, out var resourceMap))
            {
                removedB = resourceMap.Remove(key);
                if (resourceMap.Count == 0)
                {
                    _byOwner.Remove(owner);
                }
            }

            if (removedA != removedB)
            {
                throw new InvalidOperationException("illegal state");
            }

            return removedA && removedB;
        }

        public IEnumerable<V> Values(Uri resource) =>
            _byResource.TryGetValue(Key(resource), out var map) ? map.Values : [];

        public IEnumerable<V> Values(string owner) =>
            _byOwner.TryGetValue(owner, out var map) ? map.Values : [];

        public IEnumerable<V> Values() => _byOwner.Values.SelectMany(static map => map.Values);
    }

    private static class LanguagesHelpers
    {
        public static string GetScheme(Uri uri) => uri.IsAbsoluteUri ? uri.Scheme : string.Empty;
    }
}
