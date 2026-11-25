// Ported from vscode (src/vs/editor/common/viewModel.ts: IEditorWhitespace, IWhitespaceChangeAccessor,
// ILineHeightChangeAccessor, IPartialViewLinesViewportData and IViewWhitespaceViewportData, commit b57d5c9e), MIT license.
// Differences from upstream:
// - The two viewport data interfaces each get a record that implements them (upstream returns object literals).
// - Pixel values are doubles; whitespace heights and min widths are whole numbers, as upstream truncates them.

namespace TKCode.Arc.Editor.ViewLayout;

/// <summary>A whitespace (a view zone's space) between lines.</summary>
internal interface IEditorWhitespace
{
    string Id { get; }

    int AfterLineNumber { get; }

    double Height { get; }
}

/// <summary>An accessor that allows for whitespace to be added, removed or changed in bulk.</summary>
internal interface IWhitespaceChangeAccessor
{
    /// <summary>Adds a whitespace after <paramref name="afterLineNumber"/> (0 is before the first line); returns its id.</summary>
    string InsertWhitespace(int afterLineNumber, int ordinal, double heightInPx, double minWidth);

    void ChangeOneWhitespace(string id, int newAfterLineNumber, double newHeight);

    void RemoveWhitespace(string id);
}

/// <summary>An accessor that adds, changes and removes the custom line heights of decorations.</summary>
internal interface ILineHeightChangeAccessor
{
    void InsertOrChangeCustomLineHeight(string decorationId, int startLineNumber, int endLineNumber, double lineHeight);

    void RemoveCustomLineHeight(string decorationId);
}

/// <summary>The lines in a viewport and where they are.</summary>
internal interface IPartialViewLinesViewportData
{
    /// <summary>Value to be subtracted from <c>scrollTop</c> (in order to keep vertical offset numbers &lt; 1MM).</summary>
    double BigNumbersDelta { get; }

    /// <summary>The first (partially) visible line number.</summary>
    int StartLineNumber { get; }

    /// <summary>The last (partially) visible line number.</summary>
    int EndLineNumber { get; }

    /// <summary><c>RelativeVerticalOffset[i]</c> is the <c>top</c> position for line at <c>i</c> + <see cref="StartLineNumber"/>.</summary>
    IReadOnlyList<double> RelativeVerticalOffset { get; }

    /// <summary>The centered line in the viewport.</summary>
    int CenteredLineNumber { get; }

    /// <summary>The first completely visible line number.</summary>
    int CompletelyVisibleStartLineNumber { get; }

    /// <summary>The last completely visible line number.</summary>
    int CompletelyVisibleEndLineNumber { get; }

    /// <summary>The height of a line.</summary>
    double LineHeight { get; }
}

/// <summary>A whitespace in a viewport and where it is.</summary>
internal interface IViewWhitespaceViewportData
{
    string Id { get; }

    int AfterLineNumber { get; }

    double VerticalOffset { get; }

    double Height { get; }
}

/// <summary>The <see cref="IPartialViewLinesViewportData"/> that <see cref="LinesLayout.GetLinesViewportData"/> returns.</summary>
internal sealed record PartialViewLinesViewportData(
    double BigNumbersDelta,
    int StartLineNumber,
    int EndLineNumber,
    IReadOnlyList<double> RelativeVerticalOffset,
    int CenteredLineNumber,
    int CompletelyVisibleStartLineNumber,
    int CompletelyVisibleEndLineNumber,
    double LineHeight) : IPartialViewLinesViewportData;

/// <summary>The <see cref="IViewWhitespaceViewportData"/> that <see cref="LinesLayout"/> returns.</summary>
internal sealed record ViewWhitespaceViewportData(string Id, int AfterLineNumber, double VerticalOffset, double Height) : IViewWhitespaceViewportData;
