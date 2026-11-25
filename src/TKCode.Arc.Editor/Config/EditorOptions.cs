// Ported from vscode (src/vs/editor/common/config/editorOptions.ts, commit b57d5c9e), MIT license.
//
// Upstream registers about 170 options under the EditorOption enum and reads them with options.get(EditorOption.x).
// This is the part the cursor reads (cursorCommon.ts), as one immutable record with upstream's option names in
// PascalCase and upstream's defaults. The rest (rendering, layout, find, suggest, ...) comes with the controls that use
// it (phase 3, design/architecture/code-editor.md).

namespace TKCode.Arc.Editor.Config;

/// <summary>When to auto close a bracket, quote or comment (Monaco's <c>EditorAutoClosingStrategy</c>).</summary>
internal enum EditorAutoClosingStrategy
{
    Always,
    LanguageDefined,
    BeforeWhitespace,
    Never,
}

/// <summary>When to delete or type over an auto inserted closing character (Monaco's <c>EditorAutoClosingEditStrategy</c>).</summary>
internal enum EditorAutoClosingEditStrategy
{
    Always,
    Auto,
    Never,
}

/// <summary>What typing a bracket or quote over a selection wraps it with (Monaco's <c>EditorAutoSurroundStrategy</c>).</summary>
internal enum EditorAutoSurroundStrategy
{
    LanguageDefined,
    Quotes,
    Brackets,
    Never,
}

/// <summary>How much the editor indents by itself (Monaco's <c>EditorAutoIndentStrategy</c>).</summary>
internal enum EditorAutoIndentStrategy
{
    /// <summary>The editor will not insert indentation automatically.</summary>
    None = 0,

    /// <summary>The editor will keep the current line's indentation.</summary>
    Keep = 1,

    /// <summary>Keep the current line's indentation and honor language defined brackets.</summary>
    Brackets = 2,

    /// <summary>Also invoke the onEnterRules defined by languages.</summary>
    Advanced = 3,

    /// <summary>Also honor the indentationRules defined by languages.</summary>
    Full = 4,
}

/// <summary>How a paste with as many lines as cursors is spread over them (Monaco's <c>multiCursorPaste</c>).</summary>
internal enum MultiCursorPaste
{
    /// <summary>Each cursor pastes one line of the text.</summary>
    Spread,

    /// <summary>Each cursor pastes the full text.</summary>
    Full,
}

/// <summary>The editor options the cursor reads, with Monaco's names and defaults.</summary>
internal sealed record EditorOptions
{
    /// <summary>Monaco's <c>USUAL_WORD_SEPARATORS</c>, the default of <see cref="WordSeparators"/>.</summary>
    public const string UsualWordSeparators = "`~!@#$%^&*()-=+[{]}\\|;:'\",.<>/?";

    public static EditorOptions Default { get; } = new();

    /// <summary>The editor does not take edits from the user.</summary>
    public bool ReadOnly { get; init; }

    /// <summary>Emulate selection behavior of tab characters when using spaces for indentation.</summary>
    public bool StickyTabStops { get; init; }

    /// <summary>Spaces and tabs are inserted and deleted in alignment with tab stops.</summary>
    public bool UseTabStops { get; init; } = true;

    /// <summary>Deleting a newline also deletes the next line's indentation whitespace.</summary>
    public bool TrimWhitespaceOnDelete { get; init; }

    /// <summary>Characters that separate words for word navigation and operations.</summary>
    public string WordSeparators { get; init; } = UsualWordSeparators;

    /// <summary>Copying without a selection copies the current line.</summary>
    public bool EmptySelectionClipboard { get; init; } = true;

    public bool CopyWithSyntaxHighlighting { get; init; } = true;

    /// <summary>Merge multiple cursors when they are overlapping.</summary>
    public bool MultiCursorMergeOverlapping { get; init; } = true;

    public MultiCursorPaste MultiCursorPaste { get; init; } = MultiCursorPaste.Spread;

    private readonly int _multiCursorLimit = 10000;

    /// <summary>The most cursors an editor has at once (1 to 100000).</summary>
    public int MultiCursorLimit
    {
        get => _multiCursorLimit;
        init => _multiCursorLimit = Math.Clamp(value, 1, 100000);
    }

    public EditorAutoClosingStrategy AutoClosingBrackets { get; init; } = EditorAutoClosingStrategy.LanguageDefined;

    public EditorAutoClosingStrategy AutoClosingComments { get; init; } = EditorAutoClosingStrategy.LanguageDefined;

    public EditorAutoClosingStrategy AutoClosingQuotes { get; init; } = EditorAutoClosingStrategy.LanguageDefined;

    public EditorAutoClosingEditStrategy AutoClosingDelete { get; init; } = EditorAutoClosingEditStrategy.Auto;

    public EditorAutoClosingEditStrategy AutoClosingOvertype { get; init; } = EditorAutoClosingEditStrategy.Auto;

    public EditorAutoSurroundStrategy AutoSurround { get; init; } = EditorAutoSurroundStrategy.LanguageDefined;

    public EditorAutoIndentStrategy AutoIndent { get; init; } = EditorAutoIndentStrategy.Full;

    /// <summary>Locales for word segmentation (upstream's Intl.Segmenter; not used yet, see WordCharacterClassifier).</summary>
    public IReadOnlyList<string> WordSegmenterLocales { get; init; } = [];

    /// <summary>Pasting in overtype mode overtypes.</summary>
    public bool OvertypeOnPaste { get; init; } = true;

    /// <summary>
    /// The line height in pixels (upstream's <c>fontInfo.lineHeight</c>). The view sets the measured value; the default
    /// is upstream's test configuration.
    /// </summary>
    public double LineHeight { get; init; } = 19;

    /// <summary>The width of a typical halfwidth character in pixels (upstream's <c>fontInfo</c>); measured by the view.</summary>
    public double TypicalHalfwidthCharacterWidth { get; init; } = 10;

    /// <summary>The height of the editor in pixels (upstream's <c>layoutInfo.height</c>); measured by the view.</summary>
    public double LayoutHeight { get; init; } = 100;
}
