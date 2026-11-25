// Decoration styles: upstream draws a model decoration's class names (className, inlineClassName,
// glyphMarginClassName, linesDecorationsClassName, ...) through CSS rules that features register with the theme
// (registerThemingParticipant, the .squiggly-error rule of editor.css, the decoration type CSS of
// codeEditorService.registerDecorationType). Here a class name is a key into the editor's CodeEditorDecorationStyles,
// and its style says how the view draws it. Colors are either a fixed brush or a CodeEditorThemeColor, a brush the
// control theme sets on the view from Arc tokens (CodeEditorTheme.axaml), so a style follows light and dark.

using Avalonia.Media;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using TokenFontStyle = TKCode.Arc.Editor.Tokens.FontStyle;

namespace TKCode.Arc;

/// <summary>
/// A themed color of the editor's view, set by the control theme (each has a brush property on
/// <see cref="CodeEditorView"/>). Decoration styles name these instead of fixed brushes.
/// ONE LINE PER COLOR: a feature that needs another adds a member here, a brush property in CodeEditorView.Decorations.cs
/// (and its case in <see cref="CodeEditorView.GetThemeBrush"/>) and a setter in CodeEditorTheme.axaml.
/// </summary>
internal enum CodeEditorThemeColor
{
    /// <summary>Errors (squiggles, glyphs).</summary>
    Error,

    /// <summary>Warnings.</summary>
    Warning,

    /// <summary>Information.</summary>
    Info,

    /// <summary>Hints (the three dots under the start of a hint).</summary>
    Hint,

    /// <summary>Text that is unused or unnecessary (faded); also the color of muted inline text such as ghost text.</summary>
    Muted,

    /// <summary>The background of the current find match.</summary>
    FindMatch,

    /// <summary>The background of the other find matches.</summary>
    FindMatchHighlight,

    /// <summary>The background of the range find is limited to.</summary>
    FindRangeHighlight,

    /// <summary>The background of read occurrences of the symbol at the cursor.</summary>
    WordHighlight,

    /// <summary>The background of write occurrences of the symbol at the cursor.</summary>
    WordHighlightStrong,

    /// <summary>The background of other occurrences of the selected text.</summary>
    SelectionHighlight,

    /// <summary>The background of the bracket matching the one at the cursor.</summary>
    BracketMatch,

    /// <summary>The border of the bracket matching the one at the cursor.</summary>
    BracketMatchBorder,

    /// <summary>The background of ranges edited together (linked editing, snippet placeholders).</summary>
    LinkedEditing,

    /// <summary>The background of a highlighted range (go to line, peek, hover target).</summary>
    RangeHighlight,

    /// <summary>The foreground of inlay hints and code lenses.</summary>
    InlayHintForeground,

    /// <summary>The background of inlay hints.</summary>
    InlayHintBackground,

    /// <summary>Glyphs in the gutter (folding chevrons, lightbulbs).</summary>
    GutterGlyph,

    /// <summary>The background of the first line of a folded range (upstream's editor.foldBackground).</summary>
    FoldBackground,

    /// <summary>Links under the pointer.</summary>
    Link,

    /// <summary>The tab stops of a snippet (upstream's editor.snippetTabstopHighlightBackground).</summary>
    SnippetTabstopHighlight,

    /// <summary>The outline of a snippet's final tab stop (upstream's editor.snippetFinalTabstopHighlightBorder).</summary>
    SnippetFinalTabstopHighlightBorder,

    /// <summary>The border of the color swatches before color values (color decorators).</summary>
    ColorDecoratorBorder,

    /// <summary>The marker where dragged text would be dropped (upstream's dnd-target, the cursor's color).</summary>
    DropTarget,

    /// <summary>The selection anchor's marker (upstream's selection-anchor).</summary>
    SelectionAnchor,
}

/// <summary>A brush of a decoration style: fixed, or a <see cref="CodeEditorThemeColor"/> the view resolves when it draws.</summary>
internal readonly record struct DecorationBrush
{
    private DecorationBrush(IBrush? brush, CodeEditorThemeColor? themeColor)
    {
        Brush = brush;
        ThemeColor = themeColor;
    }

    /// <summary>The fixed brush, or null.</summary>
    public IBrush? Brush { get; }

    /// <summary>The theme color, or null.</summary>
    public CodeEditorThemeColor? ThemeColor { get; }

    /// <summary>Whether neither is set.</summary>
    public bool IsEmpty => Brush is null && ThemeColor is null;

    public static implicit operator DecorationBrush(CodeEditorThemeColor color) => new(null, color);

    /// <summary>A fixed brush.</summary>
    public static DecorationBrush From(IBrush brush) => new(brush, null);
}

/// <summary>How an underline is drawn.</summary>
internal enum DecorationUnderlineKind
{
    None,

    /// <summary>A straight line.</summary>
    Solid,

    /// <summary>A wavy line (errors, warnings, info).</summary>
    Squiggly,

    /// <summary>Dots (upstream's hint: three dots under the start of the range).</summary>
    Dotted,

    /// <summary>Dashes.</summary>
    Dashed,
}

/// <summary>
/// How the view draws a decoration class name. Which members count depends on where the decoration uses the name:
/// <list type="bullet">
/// <item><c>ClassName</c> (and whole-line decorations): <see cref="Background"/>, <see cref="Border"/>, and
/// <see cref="Underline"/> drawn above the text.</item>
/// <item><c>InlineClassName</c>: <see cref="Foreground"/>, <see cref="FontStyle"/> and <see cref="Opacity"/> of the text
/// (and the underline, as for ClassName).</item>
/// <item><c>GlyphMarginClassName</c> / <c>LinesDecorationsClassName</c> / <c>MarginClassName</c>: <see cref="Glyph"/>
/// drawn in <see cref="GlyphBrush"/>, else a fill in <see cref="MarginBackground"/>.</item>
/// <item><c>BeforeContentClassName</c> / <c>AfterContentClassName</c>: <see cref="ContentText"/> in
/// <see cref="Foreground"/> over <see cref="Background"/>.</item>
/// </list>
/// </summary>
internal sealed record CodeEditorDecorationStyle
{
    /// <summary>The background of the range (or of the whole lines).</summary>
    public DecorationBrush Background { get; init; }

    /// <summary>A border around the range.</summary>
    public DecorationBrush Border { get; init; }

    /// <summary>The width of <see cref="Border"/>.</summary>
    public double BorderThickness { get; init; } = 1;

    /// <summary>The text color in the range (inline class names).</summary>
    public DecorationBrush Foreground { get; init; }

    /// <summary>Bold, italic, underline or strikethrough added to the text in the range (inline class names).</summary>
    public TokenFontStyle FontStyle { get; init; }

    /// <summary>The opacity of the text in the range, 0 to 1 (inline class names; upstream's unnecessary code is 0.67).</summary>
    public double Opacity { get; init; } = 1;

    /// <summary>A line under the range, drawn above the text.</summary>
    public DecorationUnderlineKind Underline { get; init; }

    /// <summary>The color of <see cref="Underline"/>.</summary>
    public DecorationBrush UnderlineBrush { get; init; }

    /// <summary>A shape for gutter class names, in a 16 × 16 box; it is scaled to the column and centered.</summary>
    public Geometry? Glyph { get; init; }

    /// <summary>The color of <see cref="Glyph"/>.</summary>
    public DecorationBrush GlyphBrush { get; init; }

    /// <summary>A fill of the gutter cell for gutter class names without a glyph (upstream's lines-decorations bars).</summary>
    public DecorationBrush MarginBackground { get; init; }

    /// <summary>The width of <see cref="MarginBackground"/> in the cell (0 fills it), left-aligned.</summary>
    public double MarginWidth { get; init; }

    /// <summary>A background behind the text of inline class names (injected text such as inlay hints), in the background layer.</summary>
    public DecorationBrush InlineBackground { get; init; }

    /// <summary>The corner radius of <see cref="InlineBackground"/>.</summary>
    public double InlineCornerRadius { get; init; }

    /// <summary>
    /// For inline class names with a <see cref="Background"/> (injected text included): the side of a square box drawn
    /// centered in the text, as a fraction of the line height (upstream's color swatch: an inline-block of 0.8em); 0
    /// fills the text's whole cell.
    /// </summary>
    public double InlineBoxSize { get; init; }

    /// <summary>
    /// Text drawn before (<c>BeforeContentClassName</c>) or after (<c>AfterContentClassName</c>) the range, in
    /// <see cref="Foreground"/> over <see cref="Background"/> (upstream's CSS <c>content</c> of ::before / ::after; the
    /// folding placeholder). It takes no columns: the text that follows is not moved.
    /// </summary>
    public string? ContentText { get; init; }

    /// <summary>The space left and right of <see cref="ContentText"/>, in em (upstream's folding placeholder: 0.2).</summary>
    public double ContentMarginEm { get; init; }

    /// <summary>
    /// When positive, a collapsed range (an empty ClassName decoration) is drawn this wide at its position, also without
    /// showIfCollapsed (upstream: a zero-width decoration with a CSS width or border, such as the 2px drop marker).
    /// </summary>
    public double CollapsedWidth { get; init; }
}

/// <summary>
/// The decoration styles of one editor, by class name. Features register the styles of the class names they use;
/// the editor registers built-in ones (the squiggles of <see cref="DecorationClassNames"/>). Registering a class name
/// again replaces its style until the newer registration is disposed.
/// </summary>
internal sealed class CodeEditorDecorationStyles
{
    private readonly Dictionary<string, List<(object Token, CodeEditorDecorationStyle Style)>> _styles = new(StringComparer.Ordinal);

    public CodeEditorDecorationStyles()
    {
        // upstream's .squiggly-* rules (editor.css and the theming participants of the editor's colors)
        Register(DecorationClassNames.EditorErrorDecoration, new CodeEditorDecorationStyle { Underline = DecorationUnderlineKind.Squiggly, UnderlineBrush = CodeEditorThemeColor.Error });
        Register(DecorationClassNames.EditorWarningDecoration, new CodeEditorDecorationStyle { Underline = DecorationUnderlineKind.Squiggly, UnderlineBrush = CodeEditorThemeColor.Warning });
        Register(DecorationClassNames.EditorInfoDecoration, new CodeEditorDecorationStyle { Underline = DecorationUnderlineKind.Squiggly, UnderlineBrush = CodeEditorThemeColor.Info });
        Register(DecorationClassNames.EditorHintDecoration, new CodeEditorDecorationStyle { Underline = DecorationUnderlineKind.Dotted, UnderlineBrush = CodeEditorThemeColor.Hint });
        Register(DecorationClassNames.EditorUnnecessaryDecoration, new CodeEditorDecorationStyle());
        Register(DecorationClassNames.EditorUnnecessaryInlineDecoration, new CodeEditorDecorationStyle { Opacity = 0.67 });
        Register(DecorationClassNames.EditorDeprecatedInlineDecoration, new CodeEditorDecorationStyle { FontStyle = TokenFontStyle.Strikethrough });
    }

    /// <summary>Raised when a style was registered or removed.</summary>
    public event Action? Changed;

    /// <summary>Gives <paramref name="className"/> a style; dispose the result to remove it again.</summary>
    public IDisposable Register(string className, CodeEditorDecorationStyle style)
    {
        var token = new object();
        if (!_styles.TryGetValue(className, out var list))
        {
            list = [];
            _styles[className] = list;
        }

        list.Add((token, style));
        Changed?.Invoke();
        return RegistrationDisposable.ToDisposable(() =>
        {
            if (_styles.TryGetValue(className, out var current))
            {
                current.RemoveAll(entry => ReferenceEquals(entry.Token, token));
                if (current.Count == 0)
                {
                    _styles.Remove(className);
                }

                Changed?.Invoke();
            }
        });
    }

    /// <summary>The style of <paramref name="className"/>, or null when it has none.</summary>
    public CodeEditorDecorationStyle? Get(string? className) =>
        className is not null && _styles.TryGetValue(className, out var list) && list.Count > 0 ? list[^1].Style : null;

    /// <summary>
    /// The styles of a decoration's class name: its own style, or, for several names separated by spaces (as a CSS
    /// class attribute), the style of each name that has one, in order.
    /// </summary>
    public IReadOnlyList<CodeEditorDecorationStyle> GetAll(string? className)
    {
        if (string.IsNullOrEmpty(className))
        {
            return [];
        }

        if (Get(className) is { } style)
        {
            return [style];
        }

        if (!className.Contains(' '))
        {
            return [];
        }

        var result = new List<CodeEditorDecorationStyle>();
        foreach (var name in className.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Get(name) is { } part)
            {
                result.Add(part);
            }
        }

        return result;
    }
}
