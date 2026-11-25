// Ported from vscode (src/vs/editor/common/model.ts and model/textModel.ts, commit b57d5c9e), MIT license.
//
// Differences from upstream:
// - Upstream has an options interface (object literal) and a normalizing class; here it is one immutable class
//   whose init accessors normalize.
// - The "class name" members are not CSS classes: they are keys of decoration styles that the view registers.
//   The model only stores and compares them.
// - Colors are a hex string or a theme color id; resolving them against a theme is the view's job.
// - Injected text carries no tokens yet (they come with tokenization).

namespace TKCode.Arc.Editor.Model;

/// <summary>Where a decoration shows in the overview ruler.</summary>
[Flags]
internal enum OverviewRulerLane
{
    Left = 1,
    Center = 2,
    Right = 4,
    Full = 7,
}

/// <summary>Where a decoration shows in the glyph margin.</summary>
internal enum GlyphMarginLane
{
    Left = 1,
    Center = 2,
    Right = 3,
}

/// <summary>Where a decoration shows in the minimap.</summary>
internal enum MinimapPosition
{
    Inline = 1,
    Gutter = 2,
}

internal enum MinimapSectionHeaderStyle
{
    Normal = 1,
    Underlined = 2,
}

internal enum TextDirection
{
    LTR = 0,
    RTL = 1,
}

/// <summary>On which side of an injected text the cursor may stop.</summary>
internal enum InjectedTextCursorStops
{
    Both,
    Right,
    Left,
    None,
}

/// <summary>Style keys with a meaning to the model (upstream's ClassName).</summary>
internal static class DecorationClassNames
{
    public const string EditorHintDecoration = "squiggly-hint";
    public const string EditorInfoDecoration = "squiggly-info";
    public const string EditorWarningDecoration = "squiggly-warning";
    public const string EditorErrorDecoration = "squiggly-error";
    public const string EditorUnnecessaryDecoration = "squiggly-unnecessary";
    public const string EditorUnnecessaryInlineDecoration = "squiggly-inline-unnecessary";
    public const string EditorDeprecatedInlineDecoration = "squiggly-inline-deprecated";
}

/// <summary>A color given as a hex string or as the id of a theme color.</summary>
internal readonly record struct DecorationColor
{
    private DecorationColor(string? value, string? themeColorId)
    {
        Value = value;
        ThemeColorId = themeColorId;
    }

    /// <summary>The color as text (<c>#RRGGBB</c>, <c>#RRGGBBAA</c>), or null.</summary>
    public string? Value { get; }

    /// <summary>The id of a theme color (<c>editorError.foreground</c>), or null.</summary>
    public string? ThemeColorId { get; }

    public bool IsEmpty => string.IsNullOrEmpty(Value) && string.IsNullOrEmpty(ThemeColorId);

    public static DecorationColor FromValue(string value) => new(value, null);

    public static DecorationColor FromTheme(string themeColorId) => new(null, themeColorId);
}

/// <summary>Markdown shown in a hover (upstream's IMarkdownString).</summary>
internal sealed record MarkdownString(string Value)
{
    public bool IsTrusted { get; init; }

    public bool SupportThemeIcons { get; init; }

    public bool SupportHtml { get; init; }

    public Uri? BaseUri { get; init; }
}

/// <summary>How a decoration shows in the overview ruler.</summary>
internal sealed record ModelDecorationOverviewRulerOptions
{
    public DecorationColor Color { get; init; }

    /// <summary>The color in dark themes; empty uses <see cref="Color"/>.</summary>
    public DecorationColor DarkColor { get; init; }

    public OverviewRulerLane Position { get; init; } = OverviewRulerLane.Center;
}

/// <summary>Where a decoration's glyph goes.</summary>
internal sealed record ModelDecorationGlyphMarginOptions
{
    public static ModelDecorationGlyphMarginOptions Default { get; } = new();

    public GlyphMarginLane Position { get; init; } = GlyphMarginLane.Center;

    /// <summary>Keep the lane even when the decoration is not in the viewport.</summary>
    public bool? PersistLane { get; init; }
}

/// <summary>How a decoration shows in the minimap.</summary>
internal sealed record ModelDecorationMinimapOptions
{
    public DecorationColor Color { get; init; }

    /// <summary>The color in dark themes; empty uses <see cref="Color"/>.</summary>
    public DecorationColor DarkColor { get; init; }

    public MinimapPosition Position { get; init; } = MinimapPosition.Inline;

    public MinimapSectionHeaderStyle? SectionHeaderStyle { get; init; }

    public string? SectionHeaderText { get; init; }
}

/// <summary>Text a decoration shows before or after its range without it being in the model.</summary>
internal sealed record ModelDecorationInjectedTextOptions
{
    private readonly double? _widthInEm;
    private readonly string? _inlineClassName;

    public ModelDecorationInjectedTextOptions(string content)
    {
        Content = content ?? string.Empty;
    }

    public string Content { get; init; }

    /// <summary>The style key of the text.</summary>
    public string? InlineClassName
    {
        get => _inlineClassName;
        init => _inlineClassName = string.IsNullOrEmpty(value) ? null : value;
    }

    public bool InlineClassNameAffectsLetterSpacing { get; init; }

    /// <summary>A fixed width for the text, in em; null measures it.</summary>
    public double? WidthInEm
    {
        get => _widthInEm;
        init => _widthInEm = value is { } width && double.IsFinite(width) && width >= 0 ? width : null;
    }

    /// <summary>Whatever the owner wants to find again on the injected text.</summary>
    public object? AttachedData { get; init; }

    public InjectedTextCursorStops? CursorStops { get; init; }

    /// <summary>Whether the text takes room in the line.</summary>
    internal bool OccupiesHorizontalSpace => Content.Length > 0 || WidthInEm is > 0;
}

/// <summary>What a decoration looks like and how it behaves when the text around it is edited.</summary>
internal sealed record ModelDecorationOptions
{
    private const int LineHeightCeiling = 300;

    private readonly string? _blockClassName;
    private readonly string? _className;
    private readonly string? _glyphMarginClassName;
    private readonly string? _linesDecorationsClassName;
    private readonly string? _lineNumberClassName;
    private readonly string? _firstLineDecorationClassName;
    private readonly string? _marginClassName;
    private readonly string? _inlineClassName;
    private readonly string? _beforeContentClassName;
    private readonly string? _afterContentClassName;
    private readonly string? _linesDecorationsTooltip;
    private readonly string? _fontSize;
    private readonly double? _lineHeight;
    private readonly ModelDecorationGlyphMarginOptions? _glyphMargin;

    public ModelDecorationOptions(string description)
    {
        Description = description;
    }

    public static ModelDecorationOptions Empty { get; } = new("empty");

    /// <summary>The options of tracked ranges; the order matches the values of <see cref="TrackedRangeStickiness"/>.</summary>
    internal static ModelDecorationOptions[] TrackedRangeOptions { get; } =
    [
        new("tracked-range-always-grows-when-typing-at-edges") { Stickiness = TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges },
        new("tracked-range-never-grows-when-typing-at-edges") { Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges },
        new("tracked-range-grows-only-when-typing-before") { Stickiness = TrackedRangeStickiness.GrowsOnlyWhenTypingBefore },
        new("tracked-range-grows-only-when-typing-after") { Stickiness = TrackedRangeStickiness.GrowsOnlyWhenTypingAfter },
    ];

    /// <summary>A debug-time description of where the decoration comes from.</summary>
    public string Description { get; init; }

    /// <summary>How the edges move when text is typed exactly at them.</summary>
    public TrackedRangeStickiness Stickiness { get; init; } = TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges;

    /// <summary>The style key of the decoration's range.</summary>
    public string? ClassName { get => _className; init => _className = CleanClassName(value); }

    public bool? ShouldFillLineOnLineBreak { get; init; }

    public string? BlockClassName { get => _blockClassName; init => _blockClassName = CleanClassName(value); }

    public bool? BlockIsAfterEnd { get; init; }

    public bool? BlockDoesNotCollapse { get; init; }

    /// <summary>Top, right, bottom, left.</summary>
    public (double Top, double Right, double Bottom, double Left)? BlockPadding { get; init; }

    public IReadOnlyList<MarkdownString>? GlyphMarginHoverMessage { get; init; }

    public IReadOnlyList<MarkdownString>? HoverMessage { get; init; }

    public string? HoverMessageCopyContent { get; init; }

    public IReadOnlyList<MarkdownString>? LineNumberHoverMessage { get; init; }

    /// <summary>The decoration covers its lines whole.</summary>
    public bool IsWholeLine { get; init; }

    /// <summary>Show the decoration even when its range is empty.</summary>
    public bool ShowIfCollapsed { get; init; }

    /// <summary>A replace edit that covers the whole decoration collapses it to the start of the edit.</summary>
    public bool CollapseOnReplaceEdit { get; init; }

    public int ZIndex { get; init; }

    public ModelDecorationOverviewRulerOptions? OverviewRuler { get; init; }

    public ModelDecorationMinimapOptions? Minimap { get; init; }

    /// <summary>The style key of the glyph in the glyph margin.</summary>
    public string? GlyphMarginClassName { get => _glyphMarginClassName; init => _glyphMarginClassName = CleanClassName(value); }

    /// <summary>Where the glyph goes; null without a <see cref="GlyphMarginClassName"/>.</summary>
    public ModelDecorationGlyphMarginOptions? GlyphMargin
    {
        get => _glyphMarginClassName is null ? null : _glyphMargin ?? ModelDecorationGlyphMarginOptions.Default;
        init => _glyphMargin = value;
    }

    /// <summary>
    /// Overrides the line height of the decoration's lines: a multiplier of the default line height (capped at 300,
    /// as upstream does; 0 means none).
    /// </summary>
    public double? LineHeight
    {
        get => _lineHeight;
        init => _lineHeight = value is { } height && height != 0 && !double.IsNaN(height) ? Math.Min(height, LineHeightCeiling) : null;
    }

    public string? FontFamily { get; init; }

    public string? FontSize { get => _fontSize; init => _fontSize = string.IsNullOrEmpty(value) ? null : value; }

    public string? FontWeight { get; init; }

    public string? FontStyle { get; init; }

    public string? LinesDecorationsClassName { get => _linesDecorationsClassName; init => _linesDecorationsClassName = CleanClassName(value); }

    public string? LinesDecorationsTooltip
    {
        get => _linesDecorationsTooltip;
        init => _linesDecorationsTooltip = string.IsNullOrEmpty(value) ? null : value;
    }

    public string? LineNumberClassName { get => _lineNumberClassName; init => _lineNumberClassName = CleanClassName(value); }

    public string? FirstLineDecorationClassName
    {
        get => _firstLineDecorationClassName;
        init => _firstLineDecorationClassName = CleanClassName(value);
    }

    public string? MarginClassName { get => _marginClassName; init => _marginClassName = CleanClassName(value); }

    /// <summary>The style key of the text in the range.</summary>
    public string? InlineClassName { get => _inlineClassName; init => _inlineClassName = CleanClassName(value); }

    public bool InlineClassNameAffectsLetterSpacing { get; init; }

    public string? BeforeContentClassName { get => _beforeContentClassName; init => _beforeContentClassName = CleanClassName(value); }

    public string? AfterContentClassName { get => _afterContentClassName; init => _afterContentClassName = CleanClassName(value); }

    /// <summary>Text shown after the range.</summary>
    public ModelDecorationInjectedTextOptions? After { get; init; }

    /// <summary>Text shown before the range.</summary>
    public ModelDecorationInjectedTextOptions? Before { get; init; }

    public bool HideInCommentTokens { get; init; }

    public bool HideInStringTokens { get; init; }

    public TextDirection? TextDirection { get; init; }

    /// <summary>Whether the decoration changes the font of its text.</summary>
    public bool AffectsFont =>
        !string.IsNullOrEmpty(FontSize) || !string.IsNullOrEmpty(FontFamily) || !string.IsNullOrEmpty(FontWeight)
        || !string.IsNullOrEmpty(FontStyle);

    /// <summary>Whether the decoration is an error, warning or info squiggle.</summary>
    internal bool IsForValidation =>
        _className is DecorationClassNames.EditorErrorDecoration or DecorationClassNames.EditorWarningDecoration
            or DecorationClassNames.EditorInfoDecoration;

    /// <summary>Whether the decoration shows in the glyph margin.</summary>
    internal bool IsInGlyphMargin => _glyphMarginClassName is not null;

    /// <summary>Replaces every character that is not a letter, digit, <c>-</c> or <c>_</c> (ASCII) with a space.</summary>
    private static string? CleanClassName(string? className)
    {
        if (string.IsNullOrEmpty(className))
        {
            return null;
        }

        var index = IndexOfInvalid(className, 0);
        if (index < 0)
        {
            return className;
        }

        return string.Create(className.Length, className, static (span, source) =>
        {
            source.CopyTo(span);
            for (var i = 0; i < span.Length; i++)
            {
                if (!IsClassNameChar(span[i]))
                {
                    span[i] = ' ';
                }
            }
        });
    }

    private static int IndexOfInvalid(string value, int start)
    {
        for (var i = start; i < value.Length; i++)
        {
            if (!IsClassNameChar(value[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsClassNameChar(char ch) => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_';
}

/// <summary>A decoration to add (upstream's IModelDeltaDecoration).</summary>
internal readonly record struct ModelDeltaDecoration(TextRange Range, ModelDecorationOptions Options);

/// <summary>A decoration of a model (upstream's IModelDecoration).</summary>
internal interface IModelDecoration
{
    string Id { get; }

    /// <summary>Who added it; 0 is everyone, another value is one editor.</summary>
    int OwnerId { get; }

    TextRange Range { get; }

    ModelDecorationOptions Options { get; }
}
