// Adaptation of vscode's editor configuration as the view model reads it (src/vs/editor/common/config/
// editorConfiguration.ts: IEditorConfiguration, and the options of src/vs/editor/common/config/editorOptions.ts that
// viewModelImpl.ts reads; commit b57d5c9e), MIT license.
//
// Upstream's IEditorConfiguration holds about 170 computed options read with options.get(EditorOption.x) and fires
// onDidChange / onDidChangeFast with a ConfigurationChangedEvent. The editor options are not ported as a whole
// (Config/EditorOptions.cs is the cursor's part, ViewLayout/ViewLayoutOptions.cs the view layout's), so this is the
// subset the view model reads, as one immutable record (ViewModelOptions) inside a configuration object that tells the
// view model when it changes:
//  - Cursor: the cursor's options (EditorOptions), from which the view model builds its CursorConfiguration;
//  - InputMode: insert or overtype (upstream's global InputMode, which is part of the cursor configuration here);
//  - Layout: the options ViewLayout reads; ViewModelConfiguration is the layout's IViewLayoutConfiguration;
//  - FontInfo, WrappingStrategy, WrappingColumn (upstream's computed wrappingInfo.wrappingColumn, -1 for no wrapping),
//    WrappingIndent, WordBreak, WrapOnEscapedLineFeeds and UseTwoCellFullwidthCharacters (upstream's
//    effectiveFullwidthCharacterWidth === 'twoCells'): the line wrapping;
//  - RenderValidationDecorations, AllowVariableLineHeights, EffectiveAllowVariableFonts, CursorSurroundingLines and
//    StickyScroll: upstream's options of those names, with upstream's defaults;
//  - TokenColorMap: the colors of the token color ids (upstream's global TokenizationRegistry.getColorMap(), as hex
//    strings; index 0 is unused), read for the rich text to copy.
// Upstream computes several of these from each other (layoutInfo, wrappingInfo and lineHeight from the font and the
// editor size); here the host computes them and keeps them consistent: Cursor.LineHeight, Layout.LineHeight and
// FontInfo.LineHeight, Cursor.TypicalHalfwidthCharacterWidth, Layout.FontInfo and FontInfo, Cursor.LayoutHeight and
// Layout.LayoutInfo.Height, WrappingColumn and Layout.WrappingInfo.IsViewportWrapping.
// UpdateOptions compares the old and the new options and fires DidChangeFast with the names (upstream's option names,
// EditorOptionNames) of the ones that changed. setViewLineCount and setModelLineCount store the counts (upstream feeds
// them to the computed layout, which is the host's job here).

using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.ViewLayout;

namespace TKCode.Arc.Editor.ViewModel;

/// <summary>When validation decorations (squiggles) are shown (upstream's <c>renderValidationDecorations</c>).</summary>
internal enum RenderValidationDecorations
{
    /// <summary>Only in an editor that is not read-only.</summary>
    Editable,

    /// <summary>Always.</summary>
    On,

    /// <summary>Never.</summary>
    Off,
}

/// <summary>The sticky scroll options the view model reads.</summary>
/// <param name="Enabled">Whether sticky scroll is on.</param>
/// <param name="MaxLineCount">The most lines sticky scroll shows.</param>
internal sealed record StickyScrollOptions(bool Enabled, int MaxLineCount);

/// <summary>The editor options the view model reads, with upstream's names and defaults.</summary>
internal sealed record ViewModelOptions
{
    /// <summary>The cursor's options.</summary>
    public EditorOptions Cursor { get; init; } = EditorOptions.Default;

    /// <summary>Insert or overtype.</summary>
    public EditorInputMode InputMode { get; init; } = EditorInputMode.Insert;

    /// <summary>The options the view layout reads.</summary>
    public ViewLayoutOptions Layout { get; init; } = new();

    /// <summary>The measured font.</summary>
    public required FontInfo FontInfo { get; init; }

    /// <summary>Which algorithm computes the wrapping points.</summary>
    public WrappingStrategy WrappingStrategy { get; init; } = WrappingStrategy.Simple;

    /// <summary>The column lines wrap at; -1 when they do not wrap (upstream's wrappingInfo.wrappingColumn).</summary>
    public int WrappingColumn { get; init; } = -1;

    /// <summary>How wrapped lines are indented.</summary>
    public WrappingIndent WrappingIndent { get; init; } = WrappingIndent.Same;

    /// <summary>The word break rules for Chinese, Japanese and Korean text.</summary>
    public WordBreak WordBreak { get; init; } = WordBreak.Normal;

    /// <summary>Whether a literal <c>\n</c> wraps the line.</summary>
    public bool WrapOnEscapedLineFeeds { get; init; }

    /// <summary>Whether full width characters take two cells (upstream's effectiveFullwidthCharacterWidth 'twoCells').</summary>
    public bool UseTwoCellFullwidthCharacters { get; init; }

    /// <summary>When validation decorations are shown.</summary>
    public RenderValidationDecorations RenderValidationDecorations { get; init; } = RenderValidationDecorations.Editable;

    /// <summary>Whether decorations may give lines their own height.</summary>
    public bool AllowVariableLineHeights { get; init; } = true;

    /// <summary>Whether decorations may give text its own font.</summary>
    public bool EffectiveAllowVariableFonts { get; init; } = true;

    /// <summary>The lines kept visible around the cursor.</summary>
    public int CursorSurroundingLines { get; init; }

    /// <summary>The sticky scroll options.</summary>
    public StickyScrollOptions StickyScroll { get; init; } = new(true, 5);

    /// <summary>The colors of the token color ids as hex strings (index 0 is unused); null when none are known.</summary>
    public IReadOnlyList<string>? TokenColorMap { get; init; }
}

/// <summary>
/// The configuration of an editor as its view model reads it (upstream's IEditorConfiguration): the current options and
/// an event when they change.
/// </summary>
internal sealed class ViewModelConfiguration : IViewLayoutConfiguration
{
    private readonly Emitter<ConfigurationChangedEvent> _onDidChangeFast = new();

    public ViewModelConfiguration(ViewModelOptions options)
    {
        Options = options;
    }

    /// <summary>The current options.</summary>
    public ViewModelOptions Options { get; private set; }

    ViewLayoutOptions IViewLayoutConfiguration.Options => Options.Layout;

    /// <summary>The number of view lines, as the view model last reported it.</summary>
    public int ViewLineCount { get; private set; } = 1;

    /// <summary>The number of model lines, as the view model last reported it.</summary>
    public int ModelLineCount { get; private set; } = 1;

    /// <summary>Some options changed (upstream's onDidChangeFast).</summary>
    public event Action<ConfigurationChangedEvent>? DidChangeFast
    {
        add
        {
            if (value is not null)
            {
                _onDidChangeFast.Add(value);
            }
        }
        remove => _onDidChangeFast.Remove(value);
    }

    /// <summary>Replaces the options and raises <see cref="DidChangeFast"/> with the ones that changed, if any.</summary>
    public void UpdateOptions(ViewModelOptions newOptions)
    {
        var oldOptions = Options;
        Options = newOptions;
        var changed = ComputeChanges(oldOptions, newOptions);
        if (changed.Count > 0)
        {
            _onDidChangeFast.Fire(new ConfigurationChangedEvent(changed));
        }
    }

    public void SetViewLineCount(int viewLineCount) => ViewLineCount = viewLineCount;

    public void SetModelLineCount(int modelLineCount) => ModelLineCount = modelLineCount;

    /// <summary>Whether validation decorations are left out (upstream's filterValidationDecorations(options)).</summary>
    public bool FilterValidationDecorations()
    {
        var renderValidationDecorations = Options.RenderValidationDecorations;
        if (renderValidationDecorations == RenderValidationDecorations.Editable)
        {
            return Options.Cursor.ReadOnly;
        }
        return renderValidationDecorations != RenderValidationDecorations.On;
    }

    /// <summary>Whether font decorations are left out (upstream's filterFontDecorations(options)).</summary>
    public bool FilterFontDecorations() => !Options.EffectiveAllowVariableFonts;

    /// <summary>The names (<see cref="EditorOptionNames"/>) of the options that differ between the two.</summary>
    public static List<string> ComputeChanges(ViewModelOptions a, ViewModelOptions b)
    {
        var result = new List<string>();

        void Check(bool changed, string name)
        {
            if (changed && !result.Contains(name))
            {
                result.Add(name);
            }
        }

        var la = a.Layout;
        var lb = b.Layout;
        Check(la.LayoutInfo != lb.LayoutInfo || a.Cursor.LayoutHeight != b.Cursor.LayoutHeight, EditorOptionNames.LayoutInfo);
        Check(la.LineHeight != lb.LineHeight || a.Cursor.LineHeight != b.Cursor.LineHeight, EditorOptionNames.LineHeight);
        Check(la.Padding != lb.Padding, EditorOptionNames.Padding);
        Check(la.ScrollBeyondLastLine != lb.ScrollBeyondLastLine, EditorOptionNames.ScrollBeyondLastLine);
        Check(la.ScrollBeyondLastColumn != lb.ScrollBeyondLastColumn, EditorOptionNames.ScrollBeyondLastColumn);
        Check(la.SmoothScrolling != lb.SmoothScrolling, EditorOptionNames.SmoothScrolling);
        Check(la.Scrollbar != lb.Scrollbar, EditorOptionNames.Scrollbar);
        Check(la.WrappingInfo != lb.WrappingInfo || a.WrappingColumn != b.WrappingColumn, EditorOptionNames.WrappingInfo);
        Check(
            la.FontInfo != lb.FontInfo
            || !a.FontInfo.Equals(b.FontInfo)
            || a.Cursor.TypicalHalfwidthCharacterWidth != b.Cursor.TypicalHalfwidthCharacterWidth,
            EditorOptionNames.FontInfo);
        Check(la.Minimap != lb.Minimap, EditorOptionNames.Minimap);

        Check(a.WrappingStrategy != b.WrappingStrategy, EditorOptionNames.WrappingStrategy);
        Check(a.WrappingIndent != b.WrappingIndent, EditorOptionNames.WrappingIndent);
        Check(a.WordBreak != b.WordBreak, EditorOptionNames.WordBreak);
        Check(a.WrapOnEscapedLineFeeds != b.WrapOnEscapedLineFeeds, EditorOptionNames.WrapOnEscapedLineFeeds);
        Check(a.UseTwoCellFullwidthCharacters != b.UseTwoCellFullwidthCharacters, EditorOptionNames.EffectiveFullwidthCharacterWidth);
        Check(a.RenderValidationDecorations != b.RenderValidationDecorations, EditorOptionNames.RenderValidationDecorations);
        Check(a.AllowVariableLineHeights != b.AllowVariableLineHeights, EditorOptionNames.AllowVariableLineHeights);
        Check(a.EffectiveAllowVariableFonts != b.EffectiveAllowVariableFonts, EditorOptionNames.EffectiveAllowVariableFonts);
        Check(a.CursorSurroundingLines != b.CursorSurroundingLines, EditorOptionNames.CursorSurroundingLines);
        Check(a.StickyScroll != b.StickyScroll, EditorOptionNames.StickyScroll);

        var ca = a.Cursor;
        var cb = b.Cursor;
        Check(ca.ReadOnly != cb.ReadOnly, EditorOptionNames.ReadOnly);
        Check(ca.StickyTabStops != cb.StickyTabStops, EditorOptionNames.StickyTabStops);
        Check(ca.UseTabStops != cb.UseTabStops, EditorOptionNames.UseTabStops);
        Check(ca.TrimWhitespaceOnDelete != cb.TrimWhitespaceOnDelete, EditorOptionNames.TrimWhitespaceOnDelete);
        Check(ca.WordSeparators != cb.WordSeparators, EditorOptionNames.WordSeparators);
        Check(ca.EmptySelectionClipboard != cb.EmptySelectionClipboard, EditorOptionNames.EmptySelectionClipboard);
        Check(ca.CopyWithSyntaxHighlighting != cb.CopyWithSyntaxHighlighting, EditorOptionNames.CopyWithSyntaxHighlighting);
        Check(ca.MultiCursorMergeOverlapping != cb.MultiCursorMergeOverlapping, EditorOptionNames.MultiCursorMergeOverlapping);
        Check(ca.MultiCursorPaste != cb.MultiCursorPaste, EditorOptionNames.MultiCursorPaste);
        Check(ca.MultiCursorLimit != cb.MultiCursorLimit, EditorOptionNames.MultiCursorLimit);
        Check(ca.AutoClosingBrackets != cb.AutoClosingBrackets, EditorOptionNames.AutoClosingBrackets);
        Check(ca.AutoClosingComments != cb.AutoClosingComments, EditorOptionNames.AutoClosingComments);
        Check(ca.AutoClosingQuotes != cb.AutoClosingQuotes, EditorOptionNames.AutoClosingQuotes);
        Check(ca.AutoClosingDelete != cb.AutoClosingDelete, EditorOptionNames.AutoClosingDelete);
        Check(ca.AutoClosingOvertype != cb.AutoClosingOvertype, EditorOptionNames.AutoClosingOvertype);
        Check(ca.AutoSurround != cb.AutoSurround, EditorOptionNames.AutoSurround);
        Check(ca.AutoIndent != cb.AutoIndent, EditorOptionNames.AutoIndent);
        Check(!ca.WordSegmenterLocales.SequenceEqual(cb.WordSegmenterLocales), EditorOptionNames.WordSegmenterLocales);
        Check(ca.OvertypeOnPaste != cb.OvertypeOnPaste, EditorOptionNames.OvertypeOnPaste);

        return result;
    }
}
