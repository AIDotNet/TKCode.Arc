// Ported from vscode (src/vs/editor/common/textModelGuides.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - IActiveIndentGuideInfo and BracketGuideOptions are record structs (ActiveIndentGuideInfo, BracketGuideOptions);
//  - "-1" members are plain ints with -1 for none, as upstream's `number | -1`;
//  - the guides part (model/guidesTextModelPart.ts) is GuidesTextModelPart, which TextModel.GetGuides creates; the view
//    model lines take an IGuidesTextModelPart from whoever creates them.

namespace TKCode.Arc.Editor.Model;

/// <summary>The indent and bracket guides of a model (upstream's <c>ITextModel.guides</c>).</summary>
internal interface IGuidesTextModelPart
{
    ActiveIndentGuideInfo GetActiveIndentGuide(int lineNumber, int minLineNumber, int maxLineNumber);

    IReadOnlyList<int> GetLinesIndentGuides(int startLineNumber, int endLineNumber);

    /// <summary>
    /// Requests the indent guides for the given range of lines. <c>result[i]</c> will contain the indent guides of the
    /// <c>startLineNumber + i</c>th line.
    /// </summary>
    IReadOnlyList<IReadOnlyList<IndentGuide>> GetLinesBracketGuides(
        int startLineNumber,
        int endLineNumber,
        TextPosition? activePosition,
        BracketGuideOptions options);
}

/// <summary>The indent guide that contains a line.</summary>
internal readonly record struct ActiveIndentGuideInfo(int StartLineNumber, int EndLineNumber, int Indent);

/// <summary>Which horizontal bracket guides are shown.</summary>
internal enum HorizontalGuidesState
{
    Disabled,
    EnabledForActive,
    Enabled,
}

/// <summary>Which bracket guides are requested.</summary>
internal readonly record struct BracketGuideOptions(bool IncludeInactive, HorizontalGuidesState HorizontalGuides, bool HighlightActive);

/// <summary>A vertical or horizontal guide of a line.</summary>
internal sealed class IndentGuide
{
    public IndentGuide(
        int visibleColumn,
        int column,
        string className,
        IndentGuideHorizontalLine? horizontalLine,
        int forWrappedLinesAfterColumn,
        int forWrappedLinesBeforeOrAtColumn)
    {
        if ((visibleColumn != -1) == (column != -1))
        {
            throw new ArgumentException("Exactly one of visibleColumn and column must be set.");
        }

        VisibleColumn = visibleColumn;
        Column = column;
        ClassName = className;
        HorizontalLine = horizontalLine;
        ForWrappedLinesAfterColumn = forWrappedLinesAfterColumn;
        ForWrappedLinesBeforeOrAtColumn = forWrappedLinesBeforeOrAtColumn;
    }

    /// <summary>The visible column of the guide, or -1.</summary>
    public int VisibleColumn { get; }

    /// <summary>The column of the guide, or -1.</summary>
    public int Column { get; }

    public string ClassName { get; }

    /// <summary>If set, this indent guide is a horizontal guide (no vertical part). It starts at visibleColumn and continues until endColumn.</summary>
    public IndentGuideHorizontalLine? HorizontalLine { get; }

    /// <summary>If set (!= -1), only show this guide for wrapped lines that don't contain this model column, but are after it.</summary>
    public int ForWrappedLinesAfterColumn { get; }

    public int ForWrappedLinesBeforeOrAtColumn { get; }
}

/// <summary>The horizontal part of a bracket guide.</summary>
internal sealed record IndentGuideHorizontalLine(bool Top, int EndColumn);
