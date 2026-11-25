// Ported from vscode (src/vs/editor/contrib/gotoError/browser/markerNavigationService.ts: MarkerCoordinate, MarkerList,
// commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the marker service and the word lookup are handed in (there is no instantiation service); the word lookup is the
//    marker decorations' (the language's word definition), as upstream's model.getWordAtPosition;
//  - the configuration value problems.sortOrder is a constructor argument (MarkerSortOrder; upstream's default is
//    'severity');
//  - IMarkerNavigationService and its providers are not ported: an editor's marker controller creates its own list
//    (upstream's default provider does the same);
//  - resources compare by their string form, as upstream's toString() comparisons.

using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform.Markers;

namespace TKCode.Arc.Editor.Contrib.GotoError;

/// <summary>A marker and where it stands in the list (upstream's MarkerCoordinate); <see cref="Index"/> is 1-based.</summary>
internal sealed record MarkerCoordinate(Marker Marker, int Index, int Total);

/// <summary>Upstream's <c>problems.sortOrder</c>.</summary>
internal enum MarkerSortOrder
{
    /// <summary>Most severe first, then by position.</summary>
    Severity,

    /// <summary>By position, then most severe first.</summary>
    Position,
}

/// <summary>
/// The errors, warnings and infos of a resource (or all resources) in navigation order, and the one F8 shows (upstream's
/// MarkerList). Hints are left out.
/// </summary>
internal sealed class MarkerList : IDisposable
{
    private readonly MarkerService _markerService;
    private readonly Uri? _resource;
    private readonly Func<Uri, bool>? _resourceFilter;
    private readonly Func<TextModel, TextPosition, WordAtPosition?>? _getWordAtPosition;
    private readonly Comparison<Marker> _compareMarker;

    private List<Marker> _markers = [];
    private int _nextIdx = -1;
    private bool _disposed;

    /// <param name="resourceFilter">The one resource whose markers to list, or null for all.</param>
    /// <param name="markerService">The markers.</param>
    /// <param name="getWordAtPosition">The word at a position (an empty marker covers it); null: empty markers stay empty.</param>
    /// <param name="sortOrder">Upstream's problems.sortOrder.</param>
    public MarkerList(
        Uri? resourceFilter,
        MarkerService markerService,
        Func<TextModel, TextPosition, WordAtPosition?>? getWordAtPosition = null,
        MarkerSortOrder sortOrder = MarkerSortOrder.Severity)
        : this(resourceFilter, null, markerService, getWordAtPosition, sortOrder)
    {
    }

    /// <summary>A list of the markers of the resources <paramref name="resourceFilter"/> accepts.</summary>
    public MarkerList(
        Func<Uri, bool> resourceFilter,
        MarkerService markerService,
        Func<TextModel, TextPosition, WordAtPosition?>? getWordAtPosition = null,
        MarkerSortOrder sortOrder = MarkerSortOrder.Severity)
        : this(null, resourceFilter, markerService, getWordAtPosition, sortOrder)
    {
    }

    private MarkerList(
        Uri? resource,
        Func<Uri, bool>? filter,
        MarkerService markerService,
        Func<TextModel, TextPosition, WordAtPosition?>? getWordAtPosition,
        MarkerSortOrder sortOrder)
    {
        _markerService = markerService;
        _resource = resource;
        _getWordAtPosition = getWordAtPosition;
        if (resource is not null)
        {
            var key = resource.ToString();
            _resourceFilter = uri => uri.ToString() == key;
        }
        else
        {
            _resourceFilter = filter;
        }

        _compareMarker = (a, b) =>
        {
            var res = string.CompareOrdinal(a.Resource.ToString(), b.Resource.ToString());
            if (res == 0)
            {
                if (sortOrder == MarkerSortOrder.Position)
                {
                    res = TextRange.CompareRangesUsingStarts(a.Range, b.Range);
                    if (res == 0)
                    {
                        res = MarkerSeverities.Compare(a.Severity, b.Severity);
                    }
                }
                else
                {
                    res = MarkerSeverities.Compare(a.Severity, b.Severity);
                    if (res == 0)
                    {
                        res = TextRange.CompareRangesUsingStarts(a.Range, b.Range);
                    }
                }
            }

            return res;
        };

        UpdateMarker();
        _markerService.MarkerChanged += OnMarkerChanged;
    }

    /// <summary>Raised when the markers changed (upstream's onDidChange); the selection is reset then.</summary>
    public event Action? OnDidChange;

    /// <summary>The markers in navigation order.</summary>
    public IReadOnlyList<Marker> Markers => _markers;

    /// <summary>The marker shown, or null (upstream's selected).</summary>
    public MarkerCoordinate? Selected =>
        _nextIdx >= 0 && _nextIdx < _markers.Count ? new MarkerCoordinate(_markers[_nextIdx], _nextIdx + 1, _markers.Count) : null;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _markerService.MarkerChanged -= OnMarkerChanged;
        OnDidChange = null;
    }

    private void OnMarkerChanged(IReadOnlyList<Uri> uris)
    {
        if (_resourceFilter is null || uris.Any(_resourceFilter))
        {
            if (UpdateMarker())
            {
                _nextIdx = -1;
                OnDidChange?.Invoke();
            }
        }
    }

    private bool UpdateMarker()
    {
        var newMarkers = _markerService.Read(new MarkerReadOptions
        {
            Resource = _resource,
            Severities = MarkerSeverity.Error | MarkerSeverity.Warning | MarkerSeverity.Info,
        }).ToList();
        if (_resource is null && _resourceFilter is not null)
        {
            newMarkers = newMarkers.Where(m => _resourceFilter(m.Resource)).ToList();
        }

        // a stable sort, as upstream's Array.prototype.sort
        newMarkers = [.. newMarkers.Select((m, i) => (m, i)).OrderBy(t => t, Comparer<(Marker M, int I)>.Create((x, y) =>
        {
            var res = _compareMarker(x.M, y.M);
            return res != 0 ? res : x.I.CompareTo(y.I);
        })).Select(t => t.m)];

        if (newMarkers.Count == _markers.Count && newMarkers.Zip(_markers).All(p => SameMarker(p.First, p.Second)))
        {
            return false;
        }

        _markers = newMarkers;
        return true;
    }

    private static bool SameMarker(Marker a, Marker b) =>
        a.Resource.ToString() == b.Resource.ToString()
        && a.StartLineNumber == b.StartLineNumber
        && a.StartColumn == b.StartColumn
        && a.EndLineNumber == b.EndLineNumber
        && a.EndColumn == b.EndColumn
        && a.Severity == b.Severity
        && a.Message == b.Message;

    /// <summary>Whether the list is for <paramref name="uri"/> (upstream's matches).</summary>
    public bool Matches(Uri? uri)
    {
        if (_resourceFilter is null && uri is null)
        {
            return true;
        }

        if (_resourceFilter is null || uri is null)
        {
            return false;
        }

        return _resourceFilter(uri);
    }

    private void InitIdx(TextModel model, TextPosition position, bool fwd)
    {
        var modelUri = model.Uri.ToString();
        var idx = _markers.FindIndex(marker => marker.Resource.ToString() == modelUri);
        if (idx < 0)
        {
            // ignore model, position because this will be a different file
            idx = BinarySearch(modelUri);
            if (idx < 0)
            {
                idx = ~idx;
            }

            _nextIdx = fwd ? idx : (_markers.Count + idx - 1) % _markers.Count;
        }
        else
        {
            // find marker for file
            var found = false;
            var wentPast = false;
            for (var i = idx; i < _markers.Count; i++)
            {
                var range = _markers[i].Range;

                if (range.IsEmpty && _getWordAtPosition?.Invoke(model, range.Start) is { } word)
                {
                    range = new TextRange(range.StartLineNumber, word.StartColumn, range.StartLineNumber, word.EndColumn);
                }

                if (range.ContainsPosition(position) || position.IsBeforeOrEqual(range.Start))
                {
                    _nextIdx = i;
                    found = true;
                    wentPast = !range.ContainsPosition(position);
                    break;
                }

                if (_markers[i].Resource.ToString() != modelUri)
                {
                    break;
                }
            }

            if (!found)
            {
                // after the last change
                _nextIdx = fwd ? 0 : _markers.Count - 1;
            }
            else if (wentPast && !fwd)
            {
                // we went past and have to go one back
                _nextIdx -= 1;
            }
        }

        if (_nextIdx < 0)
        {
            _nextIdx = _markers.Count - 1;
        }
    }

    // upstream's binarySearch2 over the markers' resources
    private int BinarySearch(string resource)
    {
        var low = 0;
        var high = _markers.Count - 1;
        while (low <= high)
        {
            var mid = (low + high) >>> 1;
            var comp = string.CompareOrdinal(_markers[mid].Resource.ToString(), resource);
            if (comp < 0)
            {
                low = mid + 1;
            }
            else if (comp > 0)
            {
                high = mid - 1;
            }
            else
            {
                return mid;
            }
        }

        return -(low + 1);
    }

    /// <summary>Forgets the shown marker; the next move starts from the cursor again (upstream's resetIndex).</summary>
    public void ResetIndex() => _nextIdx = -1;

    /// <summary>Selects the next (or previous) marker from the position; false when nothing changed (upstream's move).</summary>
    public bool Move(bool fwd, TextModel model, TextPosition position)
    {
        if (_markers.Count == 0)
        {
            return false;
        }

        var oldIdx = _nextIdx;
        if (_nextIdx == -1)
        {
            InitIdx(model, position, fwd);
        }
        else if (fwd)
        {
            _nextIdx = (_nextIdx + 1) % _markers.Count;
        }
        else
        {
            _nextIdx = (_nextIdx - 1 + _markers.Count) % _markers.Count;
        }

        return oldIdx != _nextIdx;
    }

    /// <summary>The first marker of <paramref name="uri"/> that contains the position (upstream's find).</summary>
    public MarkerCoordinate? Find(Uri uri, TextPosition position)
    {
        var key = uri.ToString();
        var idx = _markers.FindIndex(marker => marker.Resource.ToString() == key);
        if (idx < 0)
        {
            return null;
        }

        for (; idx < _markers.Count; idx++)
        {
            if (_markers[idx].Range.ContainsPosition(position))
            {
                return new MarkerCoordinate(_markers[idx], idx + 1, _markers.Count);
            }
        }

        return null;
    }
}
