// Ported from vscode (src/vs/editor/contrib/hover/browser/hoverTypes.ts and the HoverStartMode / HoverStartSource /
// HoverResult parts of hoverOperation.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the participant interface holds the computing half only (hoverOrdinal, computeSync, computeAsync,
//    createLoadingMessage, getAccessibleContent); rendering a part is the controls' business (TKCode.Arc's
//    ContentHoverRenderer), since the engine has no Avalonia reference;
//  - suggestHoverAnchor (only the color and inlay hint participants implement it) is not ported;
//  - the participant registry (HoverParticipantRegistry, process-wide) is not ported: a hover controller gets its
//    participants from its editor;
//  - the editor a participant computes for is the small IHoverEditor (its model, its line decorations, its language
//    features and markers) instead of ICodeEditor;
//  - HoverAnchor is an abstract class with the two kinds as subclasses instead of a union.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Services;
using EditorViewModel = TKCode.Arc.Editor.ViewModel.ViewModel;

namespace TKCode.Arc.Editor.Contrib.Hover;

/// <summary>What a hover computes for (upstream's ICodeEditor, reduced to what the participants read).</summary>
internal interface IHoverEditor
{
    /// <summary>The model, or null (upstream's hasModel()).</summary>
    TextModel? Model { get; }

    /// <summary>The decorations of a line, this editor's included (upstream's editor.getLineDecorations).</summary>
    IReadOnlyList<IModelDecoration> GetLineDecorations(int lineNumber);

    /// <summary>The language feature providers (the hover providers).</summary>
    LanguageFeatures LanguageFeatures { get; }

    /// <summary>The marker decorations of the editor's model (null: no marker hovers).</summary>
    MarkerDecorationsService? MarkerDecorationsService { get; }

    /// <summary>The view model, for the coordinates of wrapped lines; null without a view.</summary>
    EditorViewModel? ViewModel { get; }

    /// <summary>The editor's hover options.</summary>
    HoverOptions HoverOptions { get; }
}

/// <summary>Upstream's <c>editor.hover.enabled</c>.</summary>
internal enum HoverEnabled
{
    On,
    Off,
    OnKeyboardModifier,
}

/// <summary>Upstream's <c>editor.multiCursorModifier</c> (the key that adds a cursor with the mouse).</summary>
internal enum MultiCursorModifier
{
    AltKey,
    CtrlKey,
    MetaKey,
}

/// <summary>The hover options (upstream's EditorOption.hover with stopRenderingLineAfter and maxTokenizationLineLength).</summary>
internal sealed record HoverOptions
{
    public static HoverOptions Default { get; } = new();

    public HoverEnabled Enabled { get; init; } = HoverEnabled.On;

    /// <summary>The milliseconds before a hover shows.</summary>
    public int Delay { get; init; } = 300;

    /// <summary>Whether the pointer can move into the hover.</summary>
    public bool Sticky { get; init; } = true;

    /// <summary>The milliseconds a sticky hover waits before it hides.</summary>
    public int HidingDelay { get; init; } = 300;

    /// <summary>Whether the hover prefers to show above the line.</summary>
    public bool Above { get; init; } = true;

    public bool ShowLongLineWarning { get; init; } = true;

    public MultiCursorModifier MultiCursorModifier { get; init; } = MultiCursorModifier.AltKey;

    /// <summary>Upstream's editor.stopRenderingLineAfter.</summary>
    public int StopRenderingLineAfter { get; init; } = 10000;

    /// <summary>Upstream's editor.maxTokenizationLineLength.</summary>
    public int? MaxTokenizationLineLength { get; init; } = 20000;
}

/// <summary>One piece of a hover (a provider's Markdown, a marker) and the range it applies to (upstream's IHoverPart).</summary>
internal interface IHoverPart
{
    /// <summary>The participant that created it.</summary>
    IEditorHoverParticipant Owner { get; }

    /// <summary>The range the part applies to (upstream: forces the hover to show while the pointer is in it).</summary>
    TextRange Range { get; }

    /// <summary>Whether the hover shows at <see cref="Range"/> instead of the pointer.</summary>
    bool ForceShowAtRange => false;

    /// <summary>Whether the part belongs to a decoration before the text (an inlay hint).</summary>
    bool IsBeforeContent => false;

    /// <summary>Whether the part still applies when the pointer moved to <paramref name="anchor"/>.</summary>
    bool IsValidForHoverAnchor(HoverAnchor anchor);
}

/// <summary>The kind of a <see cref="HoverAnchor"/> (upstream's HoverAnchorType).</summary>
internal enum HoverAnchorType
{
    Range = 1,
    ForeignElement = 2,
}

/// <summary>What a hover is computed for (upstream's HoverAnchor).</summary>
internal abstract class HoverAnchor
{
    protected HoverAnchor(int priority, TextRange range, double? initialMousePosX, double? initialMousePosY)
    {
        Priority = priority;
        Range = range;
        InitialMousePosX = initialMousePosX;
        InitialMousePosY = initialMousePosY;
    }

    public abstract HoverAnchorType Type { get; }

    public int Priority { get; }

    public TextRange Range { get; }

    public double? InitialMousePosX { get; }

    public double? InitialMousePosY { get; }

    /// <summary>Whether markers at the anchor show (always for a range anchor).</summary>
    public virtual bool SupportsMarkerHover => Type == HoverAnchorType.Range;

    public abstract bool Equals(HoverAnchor other);

    /// <summary>Whether a visible hover computed for <paramref name="lastAnchor"/> may stay while this one is computed.</summary>
    public abstract bool CanAdoptVisibleHover(HoverAnchor lastAnchor, TextPosition showAtPosition);
}

/// <summary>A hover over a range of text (upstream's HoverRangeAnchor).</summary>
internal sealed class HoverRangeAnchor(int priority, TextRange range, double? initialMousePosX, double? initialMousePosY)
    : HoverAnchor(priority, range, initialMousePosX, initialMousePosY)
{
    public override HoverAnchorType Type => HoverAnchorType.Range;

    public override bool Equals(HoverAnchor other) => other.Type == HoverAnchorType.Range && Range.Equals(other.Range);

    public override bool CanAdoptVisibleHover(HoverAnchor lastAnchor, TextPosition showAtPosition) =>
        lastAnchor.Type == HoverAnchorType.Range && showAtPosition.LineNumber == Range.StartLineNumber;
}

/// <summary>A hover over something a participant drew (upstream's HoverForeignElementAnchor).</summary>
internal sealed class HoverForeignElementAnchor(
    int priority,
    IEditorHoverParticipant owner,
    TextRange range,
    double? initialMousePosX,
    double? initialMousePosY,
    bool? supportsMarkerHover)
    : HoverAnchor(priority, range, initialMousePosX, initialMousePosY)
{
    public IEditorHoverParticipant Owner { get; } = owner;

    public override HoverAnchorType Type => HoverAnchorType.ForeignElement;

    public override bool SupportsMarkerHover => supportsMarkerHover ?? false;

    public override bool Equals(HoverAnchor other) =>
        other is HoverForeignElementAnchor foreign && ReferenceEquals(Owner, foreign.Owner);

    public override bool CanAdoptVisibleHover(HoverAnchor lastAnchor, TextPosition showAtPosition) =>
        lastAnchor is HoverForeignElementAnchor foreign && ReferenceEquals(Owner, foreign.Owner);
}

/// <summary>
/// Computes one kind of hover part (upstream's IEditorHoverParticipant, computing half): Markdown from providers and
/// decorations, markers.
/// </summary>
internal interface IEditorHoverParticipant
{
    /// <summary>The order of the participants' parts in the hover (smaller first).</summary>
    int HoverOrdinal { get; }

    /// <summary>The parts known at once (after the full hover delay).</summary>
    IReadOnlyList<IHoverPart> ComputeSync(HoverAnchor anchor, IReadOnlyList<IModelDecoration> lineDecorations, HoverStartSource source);

    /// <summary>The parts that take time (started after half the hover delay); null for none.</summary>
    IAsyncEnumerable<IHoverPart>? ComputeAsync(HoverAnchor anchor, IReadOnlyList<IModelDecoration> lineDecorations, HoverStartSource source, CancellationToken token) => null;

    /// <summary>A part that says the hover is still loading; null for none.</summary>
    IHoverPart? CreateLoadingMessage(HoverAnchor anchor) => null;

    /// <summary>The text a screen reader reads for the part.</summary>
    string GetAccessibleContent(IHoverPart hoverPart);
}

/// <summary>How a hover operation starts (upstream's HoverStartMode).</summary>
internal enum HoverStartMode
{
    /// <summary>After the hover delay (the pointer rests).</summary>
    Delayed = 0,

    /// <summary>At once (a keyboard command).</summary>
    Immediate = 1,
}

/// <summary>What started a hover (upstream's HoverStartSource).</summary>
internal enum HoverStartSource
{
    Mouse = 0,
    Click = 1,
    Keyboard = 2,
}
