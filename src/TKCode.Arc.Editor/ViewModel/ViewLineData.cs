// Ported from vscode (src/vs/editor/common/viewModel.ts: ViewLineData and MinimapLinesRenderingData, commit b57d5c9e),
// MIT license.
// Differences from upstream:
//  - only the data classes the view model lines produce; the rest of viewModel.ts is in IViewModel.cs (the interface),
//    ViewModelTypes.cs (Viewport, IViewLayout, ViewLineRenderingData), ViewLayout/ViewLayoutTypes.cs (whitespaces,
//    viewport data) and ViewModelDecoration.cs;
//  - StartVisibleColumn is a double: with a font whose full-width characters are not two columns wide the visible
//    column where a wrapped line starts is fractional (upstream's number).

using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.ViewModel;

/// <summary>The content of a view line, with its tokens and inline decorations.</summary>
internal sealed class ViewLineData
{
    public ViewLineData(
        string content,
        bool continuesWithWrappedLine,
        int minColumn,
        int maxColumn,
        double startVisibleColumn,
        IViewLineTokens tokens,
        IReadOnlyList<InlineDecoration>? inlineDecorations)
    {
        Content = content;
        ContinuesWithWrappedLine = continuesWithWrappedLine;
        MinColumn = minColumn;
        MaxColumn = maxColumn;
        StartVisibleColumn = startVisibleColumn;
        Tokens = tokens;
        InlineDecorations = inlineDecorations;
    }

    /// <summary>The content at this view line.</summary>
    public string Content { get; }

    /// <summary>Does this line continue with a wrapped line?</summary>
    public bool ContinuesWithWrappedLine { get; }

    /// <summary>The minimum allowed column at this view line.</summary>
    public int MinColumn { get; }

    /// <summary>The maximum allowed column at this view line.</summary>
    public int MaxColumn { get; }

    /// <summary>The visible column at the start of the line (after the fauxIndent).</summary>
    public double StartVisibleColumn { get; }

    /// <summary>The tokens at this view line.</summary>
    public IViewLineTokens Tokens { get; }

    /// <summary>Additional inline decorations for this line.</summary>
    public IReadOnlyList<InlineDecoration>? InlineDecorations { get; }
}

/// <summary>The view lines the minimap renders.</summary>
internal sealed class MinimapLinesRenderingData
{
    public MinimapLinesRenderingData(int tabSize, IReadOnlyList<ViewLineData?> data)
    {
        TabSize = tabSize;
        Data = data;
    }

    public int TabSize { get; }

    public IReadOnlyList<ViewLineData?> Data { get; }
}
