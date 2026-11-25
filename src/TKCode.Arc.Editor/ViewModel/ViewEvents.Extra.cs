// Ported from vscode (src/vs/editor/common/viewEvents.ts: ViewConfigurationChangedEvent, ViewCursorStateChangedEvent,
// ViewRevealRangeRequestEvent, ViewScrollChangedEvent and ViewThemeChangedEvent; src/vs/editor/common/editorCommon.ts:
// ScrollType; src/vs/editor/common/config/editorOptions.ts: ConfigurationChangedEvent, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the view events whose types ViewEvents.cs was waiting for; VerticalRevealType is in ViewEvents.cs;
//  - ConfigurationChangedEvent is minimal: upstream indexes a boolean array by the EditorOption enum, which is not
//    ported (Config/EditorOptions.cs is one record of the options the cursor reads). Here it is the set of the names of
//    the options that changed, upstream's option names (EditorOptionNames has the ones read so far), so HasChanged takes
//    a name; ViewConfigurationChangedEvent.HasChanged forwards to it, as upstream;
//  - ViewThemeChangedEvent's theme is an opaque object until a theme type (upstream's IColorTheme) is ported;
//  - upstream's `source: string | null | undefined` is a nullable string.

using TKCode.Arc.Editor.Cursor;

namespace TKCode.Arc.Editor.ViewModel;

/// <summary>How a scroll is performed (upstream's ScrollType in editorCommon.ts).</summary>
internal enum ScrollType
{
    Smooth = 0,
    Immediate = 1,
}

/// <summary>
/// Which editor options changed (upstream's ConfigurationChangedEvent). Options are named as upstream names them
/// (<c>lineHeight</c>, <c>layoutInfo</c>, ...); see <see cref="EditorOptionNames"/>.
/// </summary>
internal sealed class ConfigurationChangedEvent
{
    private readonly HashSet<string> _values;

    public ConfigurationChangedEvent(IEnumerable<string> changedOptions)
    {
        _values = new HashSet<string>(changedOptions, StringComparer.Ordinal);
    }

    /// <summary>Whether the option named <paramref name="id"/> changed.</summary>
    public bool HasChanged(string id) => _values.Contains(id);
}

/// <summary>Upstream's names of the editor options that the view layout and the view model read so far.</summary>
internal static class EditorOptionNames
{
    public const string LayoutInfo = "layoutInfo";
    public const string LineHeight = "lineHeight";
    public const string Padding = "padding";
    public const string SmoothScrolling = "smoothScrolling";
    public const string ScrollBeyondLastLine = "scrollBeyondLastLine";
    public const string ScrollBeyondLastColumn = "scrollBeyondLastColumn";
    public const string Scrollbar = "scrollbar";
    public const string WrappingInfo = "wrappingInfo";
    public const string FontInfo = "fontInfo";
    public const string Minimap = "minimap";

    // The options the view model reads (ViewModel/ViewModelConfiguration.cs).
    public const string ReadOnly = "readOnly";
    public const string RenderValidationDecorations = "renderValidationDecorations";
    public const string WrappingStrategy = "wrappingStrategy";
    public const string WrappingIndent = "wrappingIndent";
    public const string WordBreak = "wordBreak";
    public const string WrapOnEscapedLineFeeds = "wrapOnEscapedLineFeeds";
    public const string EffectiveFullwidthCharacterWidth = "effectiveFullwidthCharacterWidth";
    public const string AllowVariableLineHeights = "allowVariableLineHeights";
    public const string EffectiveAllowVariableFonts = "effectiveAllowVariableFonts";
    public const string CursorSurroundingLines = "cursorSurroundingLines";
    public const string StickyScroll = "stickyScroll";

    // The options the cursor reads (Config/EditorOptions.cs) that are not named above.
    public const string StickyTabStops = "stickyTabStops";
    public const string UseTabStops = "useTabStops";
    public const string TrimWhitespaceOnDelete = "trimWhitespaceOnDelete";
    public const string WordSeparators = "wordSeparators";
    public const string EmptySelectionClipboard = "emptySelectionClipboard";
    public const string CopyWithSyntaxHighlighting = "copyWithSyntaxHighlighting";
    public const string MultiCursorMergeOverlapping = "multiCursorMergeOverlapping";
    public const string MultiCursorPaste = "multiCursorPaste";
    public const string MultiCursorLimit = "multiCursorLimit";
    public const string AutoClosingBrackets = "autoClosingBrackets";
    public const string AutoClosingComments = "autoClosingComments";
    public const string AutoClosingQuotes = "autoClosingQuotes";
    public const string AutoClosingDelete = "autoClosingDelete";
    public const string AutoClosingOvertype = "autoClosingOvertype";
    public const string AutoSurround = "autoSurround";
    public const string AutoIndent = "autoIndent";
    public const string WordSegmenterLocales = "wordSegmenterLocales";
    public const string OvertypeOnPaste = "overtypeOnPaste";
}

internal sealed class ViewConfigurationChangedEvent : ViewEvent
{
    public ViewConfigurationChangedEvent(ConfigurationChangedEvent source)
    {
        Source = source;
    }

    public override ViewEventType Type => ViewEventType.ViewConfigurationChanged;

    /// <summary>The configuration event (upstream's <c>_source</c>).</summary>
    public ConfigurationChangedEvent Source { get; }

    public bool HasChanged(string id) => Source.HasChanged(id);
}

internal sealed class ViewCursorStateChangedEvent : ViewEvent
{
    public ViewCursorStateChangedEvent(IReadOnlyList<TextSelection> selections, IReadOnlyList<TextSelection> modelSelections, CursorChangeReason reason)
    {
        Selections = selections;
        ModelSelections = modelSelections;
        Reason = reason;
    }

    public override ViewEventType Type => ViewEventType.ViewCursorStateChanged;

    /// <summary>The selections in view coordinates.</summary>
    public IReadOnlyList<TextSelection> Selections { get; }

    /// <summary>The selections in model coordinates.</summary>
    public IReadOnlyList<TextSelection> ModelSelections { get; }

    public CursorChangeReason Reason { get; }
}

internal sealed class ViewRevealRangeRequestEvent : ViewEvent
{
    public ViewRevealRangeRequestEvent(
        string? source,
        bool minimalReveal,
        TextRange? range,
        IReadOnlyList<TextSelection>? selections,
        VerticalRevealType verticalType,
        bool revealHorizontal,
        ScrollType scrollType)
    {
        Source = source;
        MinimalReveal = minimalReveal;
        Range = range;
        Selections = selections;
        VerticalType = verticalType;
        RevealHorizontal = revealHorizontal;
        ScrollType = scrollType;
    }

    public override ViewEventType Type => ViewEventType.ViewRevealRangeRequest;

    /// <summary>Source of the call that caused the event.</summary>
    public string? Source { get; }

    /// <summary>Reduce the revealing to a minimum (e.g. avoid scrolling if the bounding box is visible and near the viewport edge).</summary>
    public bool MinimalReveal { get; }

    /// <summary>Range to be revealed.</summary>
    public TextRange? Range { get; }

    /// <summary>Selections to be revealed.</summary>
    public IReadOnlyList<TextSelection>? Selections { get; }

    /// <summary>The vertical reveal strategy.</summary>
    public VerticalRevealType VerticalType { get; }

    /// <summary>If true: there should be a horizontal &amp; vertical revealing. If false: there should be just a vertical revealing.</summary>
    public bool RevealHorizontal { get; }

    /// <summary>The scroll type.</summary>
    public ScrollType ScrollType { get; }
}

internal sealed class ViewScrollChangedEvent : ViewEvent
{
    public ViewScrollChangedEvent(ScrollEvent source)
    {
        ScrollWidth = source.ScrollWidth;
        ScrollLeft = source.ScrollLeft;
        ScrollHeight = source.ScrollHeight;
        ScrollTop = source.ScrollTop;

        ScrollWidthChanged = source.ScrollWidthChanged;
        ScrollLeftChanged = source.ScrollLeftChanged;
        ScrollHeightChanged = source.ScrollHeightChanged;
        ScrollTopChanged = source.ScrollTopChanged;
    }

    public override ViewEventType Type => ViewEventType.ViewScrollChanged;

    public double ScrollWidth { get; }

    public double ScrollLeft { get; }

    public double ScrollHeight { get; }

    public double ScrollTop { get; }

    public bool ScrollWidthChanged { get; }

    public bool ScrollLeftChanged { get; }

    public bool ScrollHeightChanged { get; }

    public bool ScrollTopChanged { get; }
}

internal sealed class ViewThemeChangedEvent : ViewEvent
{
    public ViewThemeChangedEvent(object theme)
    {
        Theme = theme;
    }

    public override ViewEventType Type => ViewEventType.ViewThemeChanged;

    /// <summary>The new theme; opaque until a theme type is ported (upstream's IColorTheme).</summary>
    public object Theme { get; }
}
