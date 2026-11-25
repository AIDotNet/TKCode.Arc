// Ported from vscode (src/vs/editor/common/services/markerDecorationsService.ts and markerDecorations.ts, commit
// b57d5c9e), MIT license.
// Differences from upstream:
//  - there is no model service: a host adds each model it shows with AddModel (upstream: onModelAdded) and disposes
//    the result when the model goes (onModelRemoved); the markers of an inmemory model are removed with it, as upstream
//    does for its transient schemes;
//  - markers are compared by reference (ReferenceEqualityComparer), as upstream's Set of marker objects does;
//  - the word a collapsed marker grows to comes from the language configuration's word definition
//    (WordHelper.GetWordAtText) when the host passes a LanguageConfigurationService, else from the default word
//    definition (upstream: textModel.getWordAtPosition, which also looks at embedded languages);
//  - the events are .NET events (DidChangeMarker, DidChangeMarkerLimit) and fire synchronously, after the marker
//    service's synchronous MarkerChanged.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.Platform.Markers;

namespace TKCode.Arc.Editor.Services;

/// <summary>How many markers a model shows and whether its sources truncated them (upstream's IMarkerLimit).</summary>
internal sealed record MarkerLimit(bool IsMarkersSourceTruncated, int? IsMarkersTruncated);

/// <summary>
/// Shows the markers of a <see cref="MarkerService"/> as decorations of the models a host adds: a squiggle class name
/// per severity (<see cref="DecorationClassNames"/>, which the view draws), an overview ruler and minimap color, and an
/// inline class name for unnecessary and deprecated code. <see cref="GetMarker"/> maps a decoration back to its marker
/// (the hover does), <see cref="GetLiveMarkers"/> gives the markers with their ranges as edited since.
/// </summary>
internal sealed class MarkerDecorationsService : IDisposable
{
    /// <summary>The theme colors of the overview ruler and the minimap per severity (upstream's editorColorRegistry ids).</summary>
    internal const string OverviewRulerError = "editorOverviewRuler.errorForeground";

    internal const string OverviewRulerWarning = "editorOverviewRuler.warningForeground";

    internal const string OverviewRulerInfo = "editorOverviewRuler.infoForeground";

    internal const string MinimapError = "minimap.errorHighlight";

    internal const string MinimapWarning = "minimap.warningHighlight";

    internal const string MinimapInfo = "minimap.infoHighlight";

    private readonly MarkerService _markerService;
    private readonly LanguageConfigurationService? _languageConfigurationService;
    private readonly Emitter<TextModel> _onDidChangeMarker = new();
    private readonly Emitter<TextModel> _onDidChangeMarkerLimit = new();
    private readonly Dictionary<string, List<TextRange>> _suppressedRanges = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MarkerDecorations> _markerDecorations = new(StringComparer.Ordinal);
    private bool _disposed;

    public MarkerDecorationsService(MarkerService markerService, LanguageConfigurationService? languageConfigurationService = null)
    {
        _markerService = markerService;
        _languageConfigurationService = languageConfigurationService;
        _markerService.MarkerChanged += HandleMarkerChange;
    }

    /// <summary>Raised when the marker decorations of a model changed.</summary>
    public event Action<TextModel>? DidChangeMarker
    {
        add => _onDidChangeMarker.Add(value!);
        remove => _onDidChangeMarker.Remove(value);
    }

    /// <summary>Raised when the <see cref="MarkerLimit"/> of a model changed.</summary>
    public event Action<TextModel>? DidChangeMarkerLimit
    {
        add => _onDidChangeMarkerLimit.Add(value!);
        remove => _onDidChangeMarkerLimit.Remove(value);
    }

    /// <summary>The service whose markers are shown.</summary>
    public MarkerService MarkerService => _markerService;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _markerService.MarkerChanged -= HandleMarkerChange;
        foreach (var value in _markerDecorations.Values)
        {
            value.Dispose();
        }

        _markerDecorations.Clear();
        _onDidChangeMarker.Clear();
        _onDidChangeMarkerLimit.Clear();
    }

    /// <summary>
    /// Starts showing the markers of <paramref name="model"/>'s URI on it (upstream: the model service's onModelAdded);
    /// dispose the result when the model goes (onModelRemoved).
    /// </summary>
    public IDisposable AddModel(TextModel model)
    {
        var key = MarkerService.Key(model.Uri);
        if (_markerDecorations.Remove(key, out var previous))
        {
            previous.Dispose();
        }

        var markerDecorations = new MarkerDecorations(model, this);
        _markerDecorations[key] = markerDecorations;
        UpdateDecorations(markerDecorations);
        return RegistrationDisposable.ToDisposable(() => OnModelRemoved(model, markerDecorations));
    }

    /// <summary>The marker a decoration shows, or null.</summary>
    public Marker? GetMarker(Uri uri, IModelDecoration decoration) =>
        _markerDecorations.GetValueOrDefault(MarkerService.Key(uri))?.GetMarker(decoration);

    /// <summary>The markers shown on the model of <paramref name="uri"/>, each with its decoration's current range.</summary>
    public IReadOnlyList<(TextRange Range, Marker Marker)> GetLiveMarkers(Uri uri) =>
        _markerDecorations.GetValueOrDefault(MarkerService.Key(uri))?.GetMarkers() ?? [];

    /// <summary>Whether the markers of the model of <paramref name="uri"/> were limited, or null when it is not shown.</summary>
    public MarkerLimit? GetMarkerLimits(Uri uri) => _markerDecorations.GetValueOrDefault(MarkerService.Key(uri))?.MarkerLimit;

    /// <summary>Hides the markers that touch <paramref name="range"/> until the result is disposed.</summary>
    public IDisposable AddMarkerSuppression(Uri uri, TextRange range)
    {
        var key = MarkerService.Key(uri);
        if (!_suppressedRanges.TryGetValue(key, out var suppressedRanges))
        {
            suppressedRanges = [];
            _suppressedRanges[key] = suppressedRanges;
        }

        suppressedRanges.Add(range);
        HandleMarkerChange([uri]);

        return RegistrationDisposable.ToDisposable(() =>
        {
            if (_suppressedRanges.TryGetValue(key, out var current))
            {
                current.Remove(range);
                if (current.Count == 0)
                {
                    _suppressedRanges.Remove(key);
                }

                HandleMarkerChange([uri]);
            }
        });
    }

    private void HandleMarkerChange(IReadOnlyList<Uri> changedResources)
    {
        foreach (var resource in changedResources)
        {
            if (_markerDecorations.TryGetValue(MarkerService.Key(resource), out var markerDecorations))
            {
                UpdateDecorations(markerDecorations);
            }
        }
    }

    private void OnModelRemoved(TextModel model, MarkerDecorations markerDecorations)
    {
        var key = MarkerService.Key(model.Uri);
        if (_markerDecorations.TryGetValue(key, out var current) && ReferenceEquals(current, markerDecorations))
        {
            markerDecorations.Dispose();
            _markerDecorations.Remove(key);
        }

        // clean up markers for internal, transient models
        if (model.Uri.IsAbsoluteUri && model.Uri.Scheme == "inmemory")
        {
            foreach (var owner in _markerService.Read(new MarkerReadOptions { Resource = model.Uri }).Select(static m => m.Owner).Distinct().ToList())
            {
                _markerService.Remove(owner, [model.Uri]);
            }
        }
    }

    private void UpdateDecorations(MarkerDecorations markerDecorations)
    {
        var uri = markerDecorations.Model.Uri;
        IReadOnlyList<Marker> markers = _markerService.Read(new MarkerReadOptions { Resource = uri, Take = MarkerDecorations.MaxMarkerDecorationsLimit + 1 });
        var isMarkerLimitExceeded = markers.Count > MarkerDecorations.MaxMarkerDecorationsLimit;
        if (isMarkerLimitExceeded)
        {
            markers = [.. markers.Take(MarkerDecorations.MaxMarkerDecorationsLimit)];
        }

        // filter markers from suppressed ranges
        if (_suppressedRanges.TryGetValue(MarkerService.Key(uri), out var suppressedRanges))
        {
            markers = [.. markers.Where(marker => !suppressedRanges.Any(candidate => TextRange.AreIntersectingOrTouching(candidate, marker.Range)))];
        }

        if (markerDecorations.Update(markers))
        {
            _onDidChangeMarker.Fire(markerDecorations.Model);
        }

        var isMarkersSourceTruncated = _markerService.IsSourceTruncated(uri);
        if (markerDecorations.UpdateMarkerLimit(isMarkersSourceTruncated, isMarkerLimitExceeded))
        {
            _onDidChangeMarkerLimit.Fire(markerDecorations.Model);
        }
    }

    internal WordAtPosition? GetWordAtPosition(TextModel model, TextPosition position)
    {
        var wordDefinition = _languageConfigurationService?.GetLanguageConfiguration(model.GetLanguageId()).GetWordDefinition()
            ?? WordHelper.DefaultWordRegExp;
        return WordHelper.GetWordAtText(position.Column, wordDefinition, model.GetLineContent(position.LineNumber), 0);
    }

    private sealed class MarkerDecorations : IDisposable
    {
        public const int MaxMarkerDecorationsLimit = 5000;

        private readonly MarkerDecorationsService _service;
        private readonly Dictionary<Marker, string> _idByMarker = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<string, Marker> _markerById = new(StringComparer.Ordinal);
        private bool _isMarkerLimitExceeded;
        private bool _isMarkersSourceTruncated;

        public MarkerDecorations(TextModel model, MarkerDecorationsService service)
        {
            Model = model;
            _service = service;
        }

        public TextModel Model { get; }

        /// <summary>The current diagnostic limit state.</summary>
        public MarkerLimit MarkerLimit => new(_isMarkersSourceTruncated, _isMarkerLimitExceeded ? MaxMarkerDecorationsLimit : null);

        public void Dispose()
        {
            if (!Model.IsDisposed() && _markerById.Count > 0)
            {
                Model.DeltaDecorations([.. _markerById.Keys], []);
            }

            _idByMarker.Clear();
            _markerById.Clear();
        }

        public bool Update(IReadOnlyList<Marker> markers)
        {
            // We use the fact that marker instances are not recreated when different owners
            // update. So we can compare references to find out what changed since the last update.
            var next = new HashSet<Marker>(markers, ReferenceEqualityComparer.Instance);
            var removed = _idByMarker.Keys.Where(marker => !next.Contains(marker)).ToList();
            var added = markers.Where(marker => !_idByMarker.ContainsKey(marker)).Distinct(ReferenceEqualityComparer.Instance).Cast<Marker>().ToList();

            if (added.Count == 0 && removed.Count == 0)
            {
                return false;
            }

            var oldIds = removed.Select(marker => _idByMarker[marker]).ToList();
            var newDecorations = added
                .Select(marker => new ModelDeltaDecoration(CreateDecorationRange(Model, marker), CreateDecorationOption(marker)))
                .ToList();

            var ids = Model.DeltaDecorations(oldIds, newDecorations);
            foreach (var removedMarker in removed)
            {
                _markerById.Remove(_idByMarker[removedMarker]);
                _idByMarker.Remove(removedMarker);
            }

            for (var index = 0; index < ids.Length; index++)
            {
                _idByMarker[added[index]] = ids[index];
                _markerById[ids[index]] = added[index];
            }

            return true;
        }

        public bool UpdateMarkerLimit(bool isMarkersSourceTruncated, bool isMarkerLimitExceeded)
        {
            if (_isMarkersSourceTruncated == isMarkersSourceTruncated && _isMarkerLimitExceeded == isMarkerLimitExceeded)
            {
                return false;
            }

            _isMarkersSourceTruncated = isMarkersSourceTruncated;
            _isMarkerLimitExceeded = isMarkerLimitExceeded;
            return true;
        }

        public Marker? GetMarker(IModelDecoration decoration) => _markerById.GetValueOrDefault(decoration.Id);

        public IReadOnlyList<(TextRange Range, Marker Marker)> GetMarkers()
        {
            var res = new List<(TextRange, Marker)>();
            foreach (var (id, marker) in _markerById)
            {
                if (Model.GetDecorationRange(id) is { } range)
                {
                    res.Add((range, marker));
                }
            }

            return res;
        }

        private TextRange CreateDecorationRange(TextModel model, Marker rawMarker)
        {
            var ret = rawMarker.Range;

            if (rawMarker.Severity == MarkerSeverity.Hint && !rawMarker.HasTag(MarkerTag.Unnecessary) && !rawMarker.HasTag(MarkerTag.Deprecated))
            {
                // * never render hints on multiple lines
                // * make enough space for three dots
                ret = ret.SetEndPosition(ret.StartLineNumber, ret.StartColumn + 2);
            }

            ret = model.ValidateRange(ret);

            if (ret.IsEmpty)
            {
                var lastNonWhitespace = model.GetLineLastNonWhitespaceColumn(ret.StartLineNumber);
                var maxColumn = lastNonWhitespace != 0 ? lastNonWhitespace : model.GetLineMaxColumn(ret.StartLineNumber);

                if (maxColumn == 1 || ret.EndColumn >= maxColumn)
                {
                    // empty line or behind eol
                    // keep the range as is, it will be rendered 1ch wide
                    return ret;
                }

                if (_service.GetWordAtPosition(model, ret.Start) is { } word)
                {
                    ret = new TextRange(ret.StartLineNumber, word.StartColumn, ret.EndLineNumber, word.EndColumn);
                }
            }
            else if (rawMarker.EndColumn == int.MaxValue && rawMarker.StartColumn == 1 && ret.StartLineNumber == ret.EndLineNumber)
            {
                var minColumn = model.GetLineFirstNonWhitespaceColumn(rawMarker.StartLineNumber);
                if (minColumn < ret.EndColumn)
                {
                    ret = new TextRange(ret.StartLineNumber, minColumn, ret.EndLineNumber, ret.EndColumn);
                    rawMarker.StartColumn = minColumn;
                }
            }

            return ret;
        }

        private static ModelDecorationOptions CreateDecorationOption(Marker marker)
        {
            string? className;
            DecorationColor color = default;
            int zIndex;
            string? inlineClassName = null;
            ModelDecorationMinimapOptions? minimap = null;

            switch (marker.Severity)
            {
                case MarkerSeverity.Hint:
                    if (marker.HasTag(MarkerTag.Deprecated))
                    {
                        className = null;
                    }
                    else if (marker.HasTag(MarkerTag.Unnecessary))
                    {
                        className = DecorationClassNames.EditorUnnecessaryDecoration;
                    }
                    else
                    {
                        className = DecorationClassNames.EditorHintDecoration;
                    }

                    zIndex = 0;
                    break;
                case MarkerSeverity.Info:
                    className = DecorationClassNames.EditorInfoDecoration;
                    color = DecorationColor.FromTheme(OverviewRulerInfo);
                    zIndex = 10;
                    minimap = new ModelDecorationMinimapOptions { Color = DecorationColor.FromTheme(MinimapInfo), Position = MinimapPosition.Inline };
                    break;
                case MarkerSeverity.Warning:
                    className = DecorationClassNames.EditorWarningDecoration;
                    color = DecorationColor.FromTheme(OverviewRulerWarning);
                    zIndex = 20;
                    minimap = new ModelDecorationMinimapOptions { Color = DecorationColor.FromTheme(MinimapWarning), Position = MinimapPosition.Inline };
                    break;
                default:
                    className = DecorationClassNames.EditorErrorDecoration;
                    color = DecorationColor.FromTheme(OverviewRulerError);
                    zIndex = 30;
                    minimap = new ModelDecorationMinimapOptions { Color = DecorationColor.FromTheme(MinimapError), Position = MinimapPosition.Inline };
                    break;
            }

            if (marker.Tags is not null)
            {
                if (marker.HasTag(MarkerTag.Unnecessary))
                {
                    inlineClassName = DecorationClassNames.EditorUnnecessaryInlineDecoration;
                }

                if (marker.HasTag(MarkerTag.Deprecated))
                {
                    inlineClassName = DecorationClassNames.EditorDeprecatedInlineDecoration;
                }
            }

            return new ModelDecorationOptions("marker-decoration")
            {
                Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
                ClassName = className,
                ShowIfCollapsed = true,
                OverviewRuler = new ModelDecorationOverviewRulerOptions { Color = color, Position = OverviewRulerLane.Right },
                Minimap = minimap,
                ZIndex = zIndex,
                InlineClassName = inlineClassName,
            };
        }
    }
}
