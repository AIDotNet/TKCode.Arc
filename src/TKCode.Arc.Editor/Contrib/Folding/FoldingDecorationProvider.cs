// Ported from vscode (src/vs/editor/contrib/folding/browser/foldingDecorations.ts, commit b57d5c9e), MIT license.
// Differences:
//  - the provider works on the text model with an owner id (upstream: editor.changeDecorations, which uses the editor's
//    id), so it has no editor reference;
//  - the colors (editor.foldBackground, editor.foldPlaceholderForeground, editorGutter.foldingControlForeground) and the
//    codicons are not registered here: the class names below are style keys the control registers
//    (FoldingDecorationClassNames), and the minimap color names upstream's theme color id;
//  - showFoldingControls is the enum ShowFoldingControls.

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Folding;

/// <summary>When the folding chevrons show in the gutter (upstream's showFoldingControls option).</summary>
internal enum ShowFoldingControls
{
    /// <summary>Always.</summary>
    Always,

    /// <summary>Never: the folding column is hidden; collapsed ranges still show their placeholder.</summary>
    Never,

    /// <summary>While the pointer is over the gutter (collapsed ranges always show theirs); the default.</summary>
    Mouseover,
}

/// <summary>The class names of the folding decorations (upstream's CSS classes and codicon class names).</summary>
internal static class FoldingDecorationClassNames
{
    /// <summary>After the text of a collapsed region's first line: the placeholder (upstream's ⋯).</summary>
    public const string InlineFolded = "inline-folded";

    /// <summary>The background of a collapsed region's first line (upstream's editor.foldBackground).</summary>
    public const string FoldedBackground = "folded-background";

    /// <summary>The chevron of an expanded region.</summary>
    public const string FoldingExpanded = "codicon codicon-folding-expanded";

    /// <summary>The chevron of a collapsed region.</summary>
    public const string FoldingCollapsed = "codicon codicon-folding-collapsed";

    /// <summary>The chevron of a collapsed manual region.</summary>
    public const string FoldingManualCollapsed = "codicon codicon-folding-manual-collapsed";

    /// <summary>The chevron of an expanded manual region.</summary>
    public const string FoldingManualExpanded = "codicon codicon-folding-manual-expanded";

    /// <summary>Marks chevrons that show without the pointer over the gutter.</summary>
    public const string AlwaysShowFoldIcons = "alwaysShowFoldIcons";

    /// <summary>The theme color id of the folded background (upstream's editor.foldBackground).</summary>
    public const string FoldBackgroundColorId = "editor.foldBackground";
}

/// <summary>The decorations of folding regions by state (upstream's FoldingDecorationProvider).</summary>
internal sealed class FoldingDecorationProvider : IFoldingDecorationProvider
{
    private const string Collapsed = "Click to expand the range.";
    private const string Expanded = "Click to collapse the range.";

    private static readonly ModelDecorationMinimapOptions s_foldedBackgroundMinimap = new()
    {
        Color = DecorationColor.FromTheme(FoldingDecorationClassNames.FoldBackgroundColorId),
        Position = MinimapPosition.Inline,
    };

    internal static readonly ModelDecorationOptions CollapsedVisualDecoration = new("folding-collapsed-visual-decoration")
    {
        Stickiness = TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges,
        AfterContentClassName = FoldingDecorationClassNames.InlineFolded,
        IsWholeLine = true,
        LinesDecorationsTooltip = Collapsed,
        FirstLineDecorationClassName = FoldingDecorationClassNames.FoldingCollapsed,
    };

    internal static readonly ModelDecorationOptions CollapsedHighlightedVisualDecoration = new("folding-collapsed-highlighted-visual-decoration")
    {
        Stickiness = TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges,
        AfterContentClassName = FoldingDecorationClassNames.InlineFolded,
        ClassName = FoldingDecorationClassNames.FoldedBackground,
        Minimap = s_foldedBackgroundMinimap,
        IsWholeLine = true,
        LinesDecorationsTooltip = Collapsed,
        FirstLineDecorationClassName = FoldingDecorationClassNames.FoldingCollapsed,
    };

    internal static readonly ModelDecorationOptions ManuallyCollapsedVisualDecoration = new("folding-manually-collapsed-visual-decoration")
    {
        Stickiness = TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges,
        AfterContentClassName = FoldingDecorationClassNames.InlineFolded,
        IsWholeLine = true,
        LinesDecorationsTooltip = Collapsed,
        FirstLineDecorationClassName = FoldingDecorationClassNames.FoldingManualCollapsed,
    };

    internal static readonly ModelDecorationOptions ManuallyCollapsedHighlightedVisualDecoration = new("folding-manually-collapsed-highlighted-visual-decoration")
    {
        Stickiness = TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges,
        AfterContentClassName = FoldingDecorationClassNames.InlineFolded,
        ClassName = FoldingDecorationClassNames.FoldedBackground,
        Minimap = s_foldedBackgroundMinimap,
        IsWholeLine = true,
        LinesDecorationsTooltip = Collapsed,
        FirstLineDecorationClassName = FoldingDecorationClassNames.FoldingManualCollapsed,
    };

    internal static readonly ModelDecorationOptions NoControlsCollapsedRangeDecoration = new("folding-no-controls-range-decoration")
    {
        Stickiness = TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges,
        AfterContentClassName = FoldingDecorationClassNames.InlineFolded,
        IsWholeLine = true,
        LinesDecorationsTooltip = Collapsed,
    };

    internal static readonly ModelDecorationOptions NoControlsCollapsedHighlightedRangeDecoration = new("folding-no-controls-range-decoration")
    {
        Stickiness = TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges,
        AfterContentClassName = FoldingDecorationClassNames.InlineFolded,
        ClassName = FoldingDecorationClassNames.FoldedBackground,
        Minimap = s_foldedBackgroundMinimap,
        IsWholeLine = true,
        LinesDecorationsTooltip = Collapsed,
    };

    internal static readonly ModelDecorationOptions ExpandedVisualDecoration = new("folding-expanded-visual-decoration")
    {
        Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
        IsWholeLine = true,
        FirstLineDecorationClassName = FoldingDecorationClassNames.AlwaysShowFoldIcons + " " + FoldingDecorationClassNames.FoldingExpanded,
        LinesDecorationsTooltip = Expanded,
    };

    internal static readonly ModelDecorationOptions ExpandedAutoHideVisualDecoration = new("folding-expanded-auto-hide-visual-decoration")
    {
        Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
        IsWholeLine = true,
        FirstLineDecorationClassName = FoldingDecorationClassNames.FoldingExpanded,
        LinesDecorationsTooltip = Expanded,
    };

    internal static readonly ModelDecorationOptions ManuallyExpandedVisualDecoration = new("folding-manually-expanded-visual-decoration")
    {
        Stickiness = TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges,
        IsWholeLine = true,
        FirstLineDecorationClassName = FoldingDecorationClassNames.AlwaysShowFoldIcons + " " + FoldingDecorationClassNames.FoldingManualExpanded,
        LinesDecorationsTooltip = Expanded,
    };

    internal static readonly ModelDecorationOptions ManuallyExpandedAutoHideVisualDecoration = new("folding-manually-expanded-auto-hide-visual-decoration")
    {
        Stickiness = TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges,
        IsWholeLine = true,
        FirstLineDecorationClassName = FoldingDecorationClassNames.FoldingManualExpanded,
        LinesDecorationsTooltip = Expanded,
    };

    internal static readonly ModelDecorationOptions NoControlsExpandedRangeDecoration = new("folding-no-controls-range-decoration")
    {
        Stickiness = TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges,
        IsWholeLine = true,
    };

    internal static readonly ModelDecorationOptions HiddenRangeDecoration = new("folding-hidden-range-decoration")
    {
        Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
    };

    private readonly TextModel _model;
    private readonly int _ownerId;

    public FoldingDecorationProvider(TextModel model, int ownerId = 0)
    {
        _model = model;
        _ownerId = ownerId;
    }

    /// <summary>When the chevrons show (upstream's showFoldingControls; default mouseover).</summary>
    public ShowFoldingControls ShowFoldingControls { get; set; } = ShowFoldingControls.Mouseover;

    /// <summary>Whether collapsed regions get a background (upstream's foldingHighlight; default true).</summary>
    public bool ShowFoldingHighlights { get; set; } = true;

    public ModelDecorationOptions GetDecorationOption(bool isCollapsed, bool isHidden, bool isManual)
    {
        if (isHidden)
        {
            // is inside another collapsed region
            return HiddenRangeDecoration;
        }

        if (ShowFoldingControls == ShowFoldingControls.Never)
        {
            if (isCollapsed)
            {
                return ShowFoldingHighlights ? NoControlsCollapsedHighlightedRangeDecoration : NoControlsCollapsedRangeDecoration;
            }

            return NoControlsExpandedRangeDecoration;
        }

        if (isCollapsed)
        {
            return isManual
                ? (ShowFoldingHighlights ? ManuallyCollapsedHighlightedVisualDecoration : ManuallyCollapsedVisualDecoration)
                : (ShowFoldingHighlights ? CollapsedHighlightedVisualDecoration : CollapsedVisualDecoration);
        }

        if (ShowFoldingControls == ShowFoldingControls.Mouseover)
        {
            return isManual ? ManuallyExpandedAutoHideVisualDecoration : ExpandedAutoHideVisualDecoration;
        }

        return isManual ? ManuallyExpandedVisualDecoration : ExpandedVisualDecoration;
    }

    public T? ChangeDecorations<T>(Func<TextModel.ModelDecorationsChangeAccessor, T> callback) =>
        _model.IsDisposed() ? default : _model.ChangeDecorations(callback, _ownerId);

    public void RemoveDecorations(IReadOnlyList<string> decorationIds)
    {
        if (_model.IsDisposed() || decorationIds.Count == 0)
        {
            return;
        }

        _model.ChangeDecorations(accessor => accessor.DeltaDecorations(decorationIds, []), _ownerId);
    }
}
