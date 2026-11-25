// Ported from vscode (src/vs/platform/markers/common/markers.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - IMarkerData is the record MarkerData; IMarker is the class Marker (not a record: the marker decorations compare
//    markers by reference, as upstream does with its object instances); IResourceMarker, IMarkerReadOptions,
//    IMarkerChangeOptions and IRelatedInformation are records without the I;
//  - a marker's code (string | { value, target }) is Code plus an optional CodeTarget;
//  - MarkerSeverity.compare / toString / toStringPlural are the static class MarkerSeverities, with the English texts of
//    upstream's nls strings; fromSeverity / toSeverity (base/common/severity.ts) are not ported;
//  - IMarkerService is not an interface: MarkerService is the one implementation.

using System.Globalization;
using System.Text;

namespace TKCode.Arc.Editor.Platform.Markers;

/// <summary>How bad a marker is; a flag each, so that a read can ask for several.</summary>
[Flags]
internal enum MarkerSeverity
{
    Hint = 1,
    Info = 2,
    Warning = 4,
    Error = 8,
}

/// <summary>A marker's extra meaning.</summary>
internal enum MarkerTag
{
    /// <summary>Unused or unnecessary code: rendered faded.</summary>
    Unnecessary = 1,

    /// <summary>Deprecated code: rendered struck through.</summary>
    Deprecated = 2,
}

/// <summary>Comparisons and display texts of <see cref="MarkerSeverity"/> (upstream's namespace MarkerSeverity).</summary>
internal static class MarkerSeverities
{
    /// <summary>Orders more severe first.</summary>
    public static int Compare(MarkerSeverity a, MarkerSeverity b) => (int)b - (int)a;

    public static string ToString(MarkerSeverity a) => a switch
    {
        MarkerSeverity.Error => "Error",
        MarkerSeverity.Warning => "Warning",
        MarkerSeverity.Info => "Info",
        _ => string.Empty,
    };

    public static string ToStringPlural(MarkerSeverity a) => a switch
    {
        MarkerSeverity.Error => "Errors",
        MarkerSeverity.Warning => "Warnings",
        MarkerSeverity.Info => "Infos",
        _ => string.Empty,
    };
}

/// <summary>A location related to a marker (upstream's IRelatedInformation).</summary>
internal sealed record RelatedInformation(Uri Resource, string Message, int StartLineNumber, int StartColumn, int EndLineNumber, int EndColumn);

/// <summary>
/// A problem reported for a range of a document (upstream's IMarkerData): what a language service or linter hands to
/// <see cref="MarkerService.ChangeOne"/>.
/// </summary>
internal sealed record MarkerData
{
    public string? Code { get; init; }

    /// <summary>Where the code links to (upstream's <c>code: { value, target }</c>).</summary>
    public Uri? CodeTarget { get; init; }

    public MarkerSeverity Severity { get; init; }

    public string? Message { get; init; }

    public string? Source { get; init; }

    public int StartLineNumber { get; init; }

    public int StartColumn { get; init; }

    public int EndLineNumber { get; init; }

    public int EndColumn { get; init; }

    /// <summary>The model version the marker was computed for.</summary>
    public int? ModelVersionId { get; init; }

    public IReadOnlyList<RelatedInformation>? RelatedInformation { get; init; }

    public IReadOnlyList<MarkerTag>? Tags { get; init; }

    public string? Origin { get; init; }

    /// <summary>A key that identifies the marker data (upstream's IMarkerData.makeKey).</summary>
    public static string MakeKey(MarkerData markerData) => MakeKeyOptionalMessage(markerData, true);

    /// <summary>A key that identifies the marker data, with or without its message.</summary>
    public static string MakeKeyOptionalMessage(MarkerData markerData, bool useMessage)
    {
        var result = new List<string> { string.Empty };
        result.Add(markerData.Source is { Length: > 0 } source ? ReplaceFirst(source) : string.Empty);
        result.Add(markerData.Code is { Length: > 0 } code ? ReplaceFirst(code) : string.Empty);
        result.Add(MarkerSeverities.ToString(markerData.Severity));

        // Modifed to not include the message as part of the marker key to work around
        // https://github.com/microsoft/vscode/issues/77475
        result.Add(markerData.Message is { Length: > 0 } message && useMessage ? ReplaceFirst(message) : string.Empty);
        result.Add(markerData.StartLineNumber.ToString(CultureInfo.InvariantCulture));
        result.Add(markerData.StartColumn.ToString(CultureInfo.InvariantCulture));
        result.Add(markerData.EndLineNumber.ToString(CultureInfo.InvariantCulture));
        result.Add(markerData.EndColumn.ToString(CultureInfo.InvariantCulture));
        result.Add(string.Empty);
        return string.Join('¦', result);

        // JavaScript's String.replace with a string pattern replaces the first occurrence only
        static string ReplaceFirst(string value)
        {
            var index = value.IndexOf('¦');
            return index < 0 ? value : new StringBuilder(value).Remove(index, 1).Insert(index, "\\¦").ToString();
        }
    }
}

/// <summary>A marker of a resource, for <see cref="MarkerService.ChangeAll"/> (upstream's IResourceMarker).</summary>
internal sealed record ResourceMarker(Uri Resource, MarkerData Marker);

/// <summary>
/// A marker the service holds (upstream's IMarker): the data with its owner and resource, the range made valid.
/// Instances are kept until their owner changes the markers of the resource, so they can be compared by reference.
/// </summary>
internal sealed class Marker
{
    public required string Owner { get; init; }

    public required Uri Resource { get; init; }

    public MarkerSeverity Severity { get; init; }

    public string? Code { get; init; }

    public Uri? CodeTarget { get; init; }

    public required string Message { get; init; }

    public string? Source { get; init; }

    public int StartLineNumber { get; init; }

    /// <summary>The start column; the decorations widen a whole-line marker to the first non-whitespace column.</summary>
    public int StartColumn { get; set; }

    public int EndLineNumber { get; init; }

    public int EndColumn { get; init; }

    public int? ModelVersionId { get; init; }

    public IReadOnlyList<RelatedInformation>? RelatedInformation { get; init; }

    public IReadOnlyList<MarkerTag>? Tags { get; init; }

    public string? Origin { get; init; }

    /// <summary>The marker's range.</summary>
    public TextRange Range => new(StartLineNumber, StartColumn, EndLineNumber, EndColumn);

    /// <summary>Whether the marker has <paramref name="tag"/>.</summary>
    public bool HasTag(MarkerTag tag) => Tags is not null && Tags.Contains(tag);

    /// <inheritdoc />
    public override string ToString() =>
        $"{Owner} {Resource} [{StartLineNumber},{StartColumn} -> {EndLineNumber},{EndColumn}] {Severity}: {Message}";
}

/// <summary>How many markers of each severity there are (upstream's MarkerStatistics).</summary>
internal sealed record MarkerStatistics(int Errors, int Warnings, int Infos, int Unknowns);

/// <summary>Which markers <see cref="MarkerService.Read"/> returns (upstream's IMarkerReadOptions).</summary>
internal sealed record MarkerReadOptions
{
    public string? Owner { get; init; }

    public Uri? Resource { get; init; }

    /// <summary>The severities to return, as flags; null for all.</summary>
    public MarkerSeverity? Severities { get; init; }

    /// <summary>The most markers to return; 0 or less for all.</summary>
    public int Take { get; init; }

    public bool IgnoreResourceFilters { get; init; }
}

/// <summary>Options of <see cref="MarkerService.ChangeOne"/> (upstream's IMarkerChangeOptions).</summary>
internal sealed record MarkerChangeOptions
{
    /// <summary>The source reported only part of its markers.</summary>
    public bool IsTruncated { get; init; }
}
