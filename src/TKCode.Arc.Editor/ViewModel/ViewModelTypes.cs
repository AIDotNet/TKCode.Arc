// Ported from vscode (src/vs/editor/common/viewModel.ts: IViewLayout, Viewport, ViewLineRenderingData and
// OverviewRulerDecorationsGroup, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - IViewModel is not here (ViewModel/IViewModel.cs); ViewLineData and MinimapLinesRenderingData are in ViewLineData.cs,
//    ViewModelDecoration in ViewModelDecoration.cs, and the whitespace and viewport data types in
//    ViewLayout/ViewLayoutTypes.cs;
//  - Viewport is a sealed class with upstream's `x | 0` truncations (doubles holding whole numbers);
//  - INewScrollPosition is NewScrollPosition (Core/Scrollable.cs);
//  - ViewLineRenderingData.ContainsRTL uses the model's RTL test (Model/PieceTree/PieceTreeStrings.ContainsRtl, the
//    same expression as upstream's strings.containsRTL); StartVisibleColumn is a double as in ViewLineData;
//  - OverviewRulerDecorationsGroup compares colors ordinally, as JavaScript's `<` on strings does.

using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;
using TKCode.Arc.Editor.ViewLayout;

namespace TKCode.Arc.Editor.ViewModel;

/// <summary>The scroll position and the size of the visible part of the editor.</summary>
internal sealed class Viewport
{
    public Viewport(double top, double left, double width, double height)
    {
        Top = Int32(top);
        Left = Int32(left);
        Width = Int32(width);
        Height = Int32(height);
    }

    public double Top { get; }

    public double Left { get; }

    public double Width { get; }

    public double Height { get; }

    // JavaScript's `value | 0`.
    private static double Int32(double value) => double.IsFinite(value) ? unchecked((int)(long)value) : 0;
}

/// <summary>The vertical layout of the view lines and the scroll state over it.</summary>
internal interface IViewLayout
{
    Scrollable GetScrollable();

    double GetScrollWidth();

    double GetScrollHeight();

    double GetCurrentScrollLeft();

    double GetCurrentScrollTop();

    Viewport GetCurrentViewport();

    Viewport GetFutureViewport();

    void SetScrollPosition(NewScrollPosition position, ScrollType type);

    void DeltaScrollNow(double deltaScrollLeft, double deltaScrollTop);

    IScrollPosition ValidateScrollPosition(NewScrollPosition scrollPosition);

    void SetMaxLineWidth(double maxLineWidth);

    void SetOverlayWidgetsMinWidth(double overlayWidgetsMinWidth);

    IPartialViewLinesViewportData GetLinesViewportData();

    IPartialViewLinesViewportData GetLinesViewportDataAtScrollTop(double scrollTop);

    IEditorWhitespace[] GetWhitespaces();

    bool IsAfterLines(double verticalOffset);

    bool IsInTopPadding(double verticalOffset);

    bool IsInBottomPadding(double verticalOffset);

    int GetLineNumberAtVerticalOffset(double verticalOffset);

    double GetVerticalOffsetForLineNumber(int lineNumber, bool includeViewZones = false);

    double GetVerticalOffsetAfterLineNumber(int lineNumber, bool includeViewZones = false);

    double GetLineHeightForLineNumber(int lineNumber);

    IViewWhitespaceViewportData? GetWhitespaceAtVerticalOffset(double verticalOffset);

    /// <summary>Get the layout information for whitespaces currently in the viewport.</summary>
    IViewWhitespaceViewportData[] GetWhitespaceViewportData();
}

/// <summary>What the view needs to render a view line.</summary>
internal sealed class ViewLineRenderingData
{
    public ViewLineRenderingData(
        int minColumn,
        int maxColumn,
        string content,
        bool continuesWithWrappedLine,
        bool mightContainRTL,
        bool mightContainNonBasicASCII,
        IViewLineTokens tokens,
        IReadOnlyList<InlineDecoration> inlineDecorations,
        int tabSize,
        double startVisibleColumn,
        TextDirection textDirection,
        bool hasVariableFonts)
    {
        MinColumn = minColumn;
        MaxColumn = maxColumn;
        Content = content;
        ContinuesWithWrappedLine = continuesWithWrappedLine;

        IsBasicASCII = ComputeIsBasicASCII(content, mightContainNonBasicASCII);
        ContainsRTL = ComputeContainsRTL(content, IsBasicASCII, mightContainRTL);

        Tokens = tokens;
        InlineDecorations = inlineDecorations;
        TabSize = tabSize;
        StartVisibleColumn = startVisibleColumn;
        TextDirection = textDirection;
        HasVariableFonts = hasVariableFonts;
    }

    /// <summary>The minimum allowed column at this view line.</summary>
    public int MinColumn { get; }

    /// <summary>The maximum allowed column at this view line.</summary>
    public int MaxColumn { get; }

    /// <summary>The content at this view line.</summary>
    public string Content { get; }

    /// <summary>Does this line continue with a wrapped line?</summary>
    public bool ContinuesWithWrappedLine { get; }

    /// <summary>Describes if <see cref="Content"/> contains RTL characters.</summary>
    public bool ContainsRTL { get; }

    /// <summary>Describes if <see cref="Content"/> contains non basic ASCII chars.</summary>
    public bool IsBasicASCII { get; }

    /// <summary>The tokens at this view line.</summary>
    public IViewLineTokens Tokens { get; }

    /// <summary>Inline decorations at this view line.</summary>
    public IReadOnlyList<InlineDecoration> InlineDecorations { get; }

    /// <summary>The tab size for this view model.</summary>
    public int TabSize { get; }

    /// <summary>The visible column at the start of the line (after the fauxIndent).</summary>
    public double StartVisibleColumn { get; }

    /// <summary>The direction to use for rendering the line.</summary>
    public TextDirection TextDirection { get; }

    /// <summary>Whether the line has variable fonts.</summary>
    public bool HasVariableFonts { get; }

    /// <summary>Upstream's static isBasicASCII.</summary>
    public static bool ComputeIsBasicASCII(string lineContent, bool mightContainNonBasicASCII)
    {
        if (mightContainNonBasicASCII)
        {
            return Strings.IsBasicASCII(lineContent);
        }

        return true;
    }

    /// <summary>Upstream's static containsRTL.</summary>
    public static bool ComputeContainsRTL(string lineContent, bool isBasicASCII, bool mightContainRTL)
    {
        if (!isBasicASCII && mightContainRTL)
        {
            return PieceTreeStrings.ContainsRtl(lineContent);
        }

        return false;
    }
}

/// <summary>The overview ruler decorations of one color and z-index.</summary>
internal sealed class OverviewRulerDecorationsGroup
{
    public OverviewRulerDecorationsGroup(string color, int zIndex, IReadOnlyList<int> data)
    {
        Color = color;
        ZIndex = zIndex;
        Data = data;
    }

    public string Color { get; }

    public int ZIndex { get; }

    /// <summary>
    /// Decorations are encoded in a number array using the following scheme: <c>3*i</c> = lane,
    /// <c>3*i+1</c> = startLineNumber, <c>3*i+2</c> = endLineNumber.
    /// </summary>
    public IReadOnlyList<int> Data { get; }

    public static int CompareByRenderingProps(OverviewRulerDecorationsGroup a, OverviewRulerDecorationsGroup b)
    {
        if (a.ZIndex == b.ZIndex)
        {
            var colorComparison = string.CompareOrdinal(a.Color, b.Color);
            if (colorComparison < 0)
            {
                return -1;
            }

            if (colorComparison > 0)
            {
                return 1;
            }

            return 0;
        }

        return a.ZIndex - b.ZIndex;
    }

    public static bool Equals(OverviewRulerDecorationsGroup a, OverviewRulerDecorationsGroup b)
    {
        return a.Color == b.Color
            && a.ZIndex == b.ZIndex
            && a.Data.SequenceEqual(b.Data);
    }

    public static bool EqualsArr(IReadOnlyList<OverviewRulerDecorationsGroup> a, IReadOnlyList<OverviewRulerDecorationsGroup> b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (!Equals(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }
}
