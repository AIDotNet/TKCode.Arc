// Ported from vscode (src/vs/editor/contrib/hover/browser/markerHoverParticipant.ts: MarkerHover and
// MarkerHoverParticipant's computeSync, getAccessibleContent and the severity order of renderHoverParts, commit
// b57d5c9e), MIT license.
// Differences from upstream:
//  - rendering (the marker rows, the status bar with "View Problem") is the controls' (ContentHoverRenderer); the
//    participant keeps the computing half, the order the rows render in (SortForRendering) and the accessible text;
//  - the quick fix actions of the status bar (code actions for the marker) and menu-contributed actions are not ported;
//  - basename(resource) is the last segment of the URI's path.

using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform.Markers;

namespace TKCode.Arc.Editor.Contrib.Hover;

/// <summary>A marker in a hover (upstream's MarkerHover).</summary>
internal sealed class MarkerHover(IEditorHoverParticipant owner, TextRange range, Marker marker) : IHoverPart
{
    public IEditorHoverParticipant Owner { get; } = owner;

    public TextRange Range { get; } = range;

    public Marker Marker { get; } = marker;

    public bool IsValidForHoverAnchor(HoverAnchor anchor) =>
        anchor.Type == HoverAnchorType.Range
        && Range.StartColumn <= anchor.Range.StartColumn
        && Range.EndColumn >= anchor.Range.EndColumn;
}

/// <summary>The problems under the pointer (upstream's MarkerHoverParticipant).</summary>
internal sealed class MarkerHoverParticipant(IHoverEditor editor) : IEditorHoverParticipant
{
    public int HoverOrdinal => 1;

    /// <summary>Upstream's getSeverityLabel.</summary>
    public static string GetSeverityLabel(MarkerSeverity severity) =>
        severity == MarkerSeverity.Hint ? "Hint" : MarkerSeverities.ToString(severity);

    public IReadOnlyList<IHoverPart> ComputeSync(HoverAnchor anchor, IReadOnlyList<IModelDecoration> lineDecorations, HoverStartSource source)
    {
        var model = editor.Model;
        if (model is null || (anchor.Type != HoverAnchorType.Range && !anchor.SupportsMarkerHover))
        {
            return [];
        }

        var service = editor.MarkerDecorationsService;
        var anchorRange = anchor.Range;
        if (service is null || !model.IsValidRange(anchorRange))
        {
            return [];
        }

        var lineNumber = anchorRange.StartLineNumber;
        var maxColumn = model.GetLineMaxColumn(lineNumber);
        var result = new List<IHoverPart>();
        foreach (var d in lineDecorations)
        {
            var startColumn = d.Range.StartLineNumber == lineNumber ? d.Range.StartColumn : 1;
            var endColumn = d.Range.EndLineNumber == lineNumber ? d.Range.EndColumn : maxColumn;

            var marker = service.GetMarker(model.Uri, d);
            if (marker is null)
            {
                continue;
            }

            var range = new TextRange(anchor.Range.StartLineNumber, startColumn, anchor.Range.StartLineNumber, endColumn);
            result.Add(new MarkerHover(this, range, marker));
        }

        return result;
    }

    /// <summary>The marker parts in the order they render: most severe first, equal severities as they came (upstream's renderHoverParts sort).</summary>
    public static IReadOnlyList<MarkerHover> SortForRendering(IReadOnlyList<MarkerHover> hoverParts) =>
        [.. hoverParts.OrderBy(p => p.Marker.Severity, Comparer<MarkerSeverity>.Create(MarkerSeverities.Compare))];

    public string GetAccessibleContent(IHoverPart hoverPart)
    {
        if (hoverPart is not MarkerHover { Marker: var marker })
        {
            return string.Empty;
        }

        var relatedInformation = marker.RelatedInformation is { Count: > 0 } related
            ? string.Join("\n", related.Select(FormatRelatedInformation))
            : null;
        var message = $"{GetSeverityLabel(marker.Severity)}: {marker.Message}";
        return string.IsNullOrEmpty(relatedInformation) ? message : message + "\n" + relatedInformation;
    }

    /// <summary>A related location as upstream shows it: <c>file.ts(2, 3): message</c>.</summary>
    public static string FormatRelatedInformation(RelatedInformation related) =>
        $"{Basename(related.Resource)}({related.StartLineNumber}, {related.StartColumn}): {related.Message}";

    /// <summary>The last segment of the URI's path (upstream's resources.basename).</summary>
    public static string Basename(Uri resource)
    {
        var path = resource.IsAbsoluteUri ? resource.AbsolutePath : resource.OriginalString;
        var slash = path.LastIndexOf('/');
        return Uri.UnescapeDataString(slash >= 0 ? path[(slash + 1)..] : path);
    }
}
