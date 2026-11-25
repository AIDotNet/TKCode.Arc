// Ported from vscode (src/vs/editor/common/viewEvents.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the events derive from an abstract ViewEvent (upstream's union type ViewEvent) whose Type tells them apart;
//  - ViewConfigurationChangedEvent, ViewCursorStateChangedEvent, ViewRevealRangeRequestEvent, ViewScrollChangedEvent
//    and ViewThemeChangedEvent are in ViewEvents.Extra.cs;
//  - ViewTokensChangedEvent's ranges are ViewTokensChangedRange values.

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.ViewModel;

/// <summary>The kinds of view events.</summary>
internal enum ViewEventType
{
    ViewCompositionStart,
    ViewCompositionEnd,
    ViewConfigurationChanged,
    ViewCursorStateChanged,
    ViewDecorationsChanged,
    ViewFlushed,
    ViewFocusChanged,
    ViewLanguageConfigurationChanged,
    ViewLineMappingChanged,
    ViewLinesChanged,
    ViewLinesDeleted,
    ViewLinesInserted,
    ViewRevealRangeRequest,
    ViewScrollChanged,
    ViewThemeChanged,
    ViewTokensChanged,
    ViewTokensColorsChanged,
    ViewZonesChanged,
}

/// <summary>An event the view model sends to the view.</summary>
internal abstract class ViewEvent
{
    public abstract ViewEventType Type { get; }
}

internal sealed class ViewCompositionStartEvent : ViewEvent
{
    public override ViewEventType Type => ViewEventType.ViewCompositionStart;
}

internal sealed class ViewCompositionEndEvent : ViewEvent
{
    public override ViewEventType Type => ViewEventType.ViewCompositionEnd;
}

internal sealed class ViewDecorationsChangedEvent : ViewEvent
{
    public ViewDecorationsChangedEvent(ModelDecorationsChangedEvent? source)
    {
        if (source is { } changed)
        {
            AffectsMinimap = changed.AffectsMinimap;
            AffectsOverviewRuler = changed.AffectsOverviewRuler;
            AffectsGlyphMargin = changed.AffectsGlyphMargin;
            AffectsLineNumber = changed.AffectsLineNumber;
        }
        else
        {
            AffectsMinimap = true;
            AffectsOverviewRuler = true;
            AffectsGlyphMargin = true;
            AffectsLineNumber = true;
        }
    }

    public override ViewEventType Type => ViewEventType.ViewDecorationsChanged;

    public bool AffectsMinimap { get; }

    public bool AffectsOverviewRuler { get; }

    public bool AffectsGlyphMargin { get; }

    public bool AffectsLineNumber { get; }
}

internal sealed class ViewFlushedEvent : ViewEvent
{
    public override ViewEventType Type => ViewEventType.ViewFlushed;
}

internal sealed class ViewFocusChangedEvent : ViewEvent
{
    public ViewFocusChangedEvent(bool isFocused)
    {
        IsFocused = isFocused;
    }

    public override ViewEventType Type => ViewEventType.ViewFocusChanged;

    public bool IsFocused { get; }
}

internal sealed class ViewLanguageConfigurationEvent : ViewEvent
{
    public override ViewEventType Type => ViewEventType.ViewLanguageConfigurationChanged;
}

internal sealed class ViewLineMappingChangedEvent : ViewEvent
{
    public override ViewEventType Type => ViewEventType.ViewLineMappingChanged;
}

internal sealed class ViewLinesChangedEvent : ViewEvent
{
    public ViewLinesChangedEvent(int fromLineNumber, int count)
    {
        FromLineNumber = fromLineNumber;
        Count = count;
    }

    public override ViewEventType Type => ViewEventType.ViewLinesChanged;

    /// <summary>The first line that has changed.</summary>
    public int FromLineNumber { get; }

    /// <summary>The number of lines that have changed.</summary>
    public int Count { get; }
}

internal sealed class ViewLinesDeletedEvent : ViewEvent
{
    public ViewLinesDeletedEvent(int fromLineNumber, int toLineNumber)
    {
        FromLineNumber = fromLineNumber;
        ToLineNumber = toLineNumber;
    }

    public override ViewEventType Type => ViewEventType.ViewLinesDeleted;

    /// <summary>At what line the deletion began (inclusive).</summary>
    public int FromLineNumber { get; }

    /// <summary>At what line the deletion stopped (inclusive).</summary>
    public int ToLineNumber { get; }
}

internal sealed class ViewLinesInsertedEvent : ViewEvent
{
    public ViewLinesInsertedEvent(int fromLineNumber, int toLineNumber)
    {
        FromLineNumber = fromLineNumber;
        ToLineNumber = toLineNumber;
    }

    public override ViewEventType Type => ViewEventType.ViewLinesInserted;

    /// <summary>Before what line did the insertion begin.</summary>
    public int FromLineNumber { get; }

    /// <summary><c>ToLineNumber - FromLineNumber + 1</c> denotes the number of lines that were inserted.</summary>
    public int ToLineNumber { get; }
}

/// <summary>How a range is revealed vertically.</summary>
internal enum VerticalRevealType
{
    Simple = 0,
    Center = 1,
    CenterIfOutsideViewport = 2,
    Top = 3,
    Bottom = 4,
    NearTop = 5,
    NearTopIfOutsideViewport = 6,
}

/// <summary>A range of view lines whose tokens changed.</summary>
internal readonly record struct ViewTokensChangedRange(int FromLineNumber, int ToLineNumber);

internal sealed class ViewTokensChangedEvent : ViewEvent
{
    public ViewTokensChangedEvent(IReadOnlyList<ViewTokensChangedRange> ranges)
    {
        Ranges = ranges;
    }

    public override ViewEventType Type => ViewEventType.ViewTokensChanged;

    public IReadOnlyList<ViewTokensChangedRange> Ranges { get; }
}

internal sealed class ViewTokensColorsChangedEvent : ViewEvent
{
    public override ViewEventType Type => ViewEventType.ViewTokensColorsChanged;
}

internal sealed class ViewZonesChangedEvent : ViewEvent
{
    public override ViewEventType Type => ViewEventType.ViewZonesChanged;
}
