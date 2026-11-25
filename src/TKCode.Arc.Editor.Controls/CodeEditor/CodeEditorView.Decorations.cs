// Decorations (upstream's DecorationsOverlay in src/vs/editor/browser/viewParts/decorations/decorations.ts, the inline
// class names of ViewLine rendering, LinesDecorationsOverlay, MarginViewLineDecorationsOverlay and GlyphMarginWidgets;
// commit b57d5c9e, MIT license). A decoration's class names are keys into the editor's CodeEditorDecorationStyles:
//  - ClassName ranges and whole-line decorations: background and border in the background layer (after the current
//    line, before the selections), the underline (squiggles) in the decorations layer above the text;
//  - InlineClassName: the text's color, font style and opacity, through the text cache's color overrides (as the
//    colorized brackets), plus the underline;
//  - MarginClassName, LinesDecorationsClassName and GlyphMarginClassName: in the gutter (CodeEditorView.Gutter.cs).
// Differences from upstream: no CSS; a collapsed range with showIfCollapsed is half a character wide (upstream's
// typicalHalfwidthCharacterWidth); decorations are drawn in z-index order, then in the order the view model gives them.

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.ViewModel;
using TokenFontStyle = TKCode.Arc.Editor.Tokens.FontStyle;

namespace TKCode.Arc;

internal sealed partial class CodeEditorView
{
    /// <summary>Defines the <see cref="ErrorBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ErrorBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(ErrorBrush));

    /// <summary>Defines the <see cref="WarningBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> WarningBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(WarningBrush));

    /// <summary>Defines the <see cref="InfoBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> InfoBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(InfoBrush));

    /// <summary>Defines the <see cref="HintBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> HintBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(HintBrush));

    /// <summary>Defines the <see cref="MutedBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> MutedBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(MutedBrush));

    /// <summary>Defines the <see cref="FindMatchBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> FindMatchBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(FindMatchBrush));

    /// <summary>Defines the <see cref="FindMatchHighlightBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> FindMatchHighlightBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(FindMatchHighlightBrush));

    /// <summary>Defines the <see cref="FindRangeHighlightBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> FindRangeHighlightBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(FindRangeHighlightBrush));

    /// <summary>Defines the <see cref="WordHighlightBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> WordHighlightBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(WordHighlightBrush));

    /// <summary>Defines the <see cref="WordHighlightStrongBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> WordHighlightStrongBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(WordHighlightStrongBrush));

    /// <summary>Defines the <see cref="SelectionHighlightBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> SelectionHighlightBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(SelectionHighlightBrush));

    /// <summary>Defines the <see cref="BracketMatchBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> BracketMatchBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(BracketMatchBrush));

    /// <summary>Defines the <see cref="BracketMatchBorderBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> BracketMatchBorderBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(BracketMatchBorderBrush));

    /// <summary>Defines the <see cref="LinkedEditingBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> LinkedEditingBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(LinkedEditingBrush));

    /// <summary>Defines the <see cref="RangeHighlightBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> RangeHighlightBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(RangeHighlightBrush));

    /// <summary>Defines the <see cref="InlayHintForegroundBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> InlayHintForegroundBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(InlayHintForegroundBrush));

    /// <summary>Defines the <see cref="InlayHintBackgroundBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> InlayHintBackgroundBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(InlayHintBackgroundBrush));

    /// <summary>Defines the <see cref="GutterGlyphBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> GutterGlyphBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(GutterGlyphBrush));

    /// <summary>Defines the <see cref="FoldBackgroundBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> FoldBackgroundBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(FoldBackgroundBrush));

    /// <summary>Defines the <see cref="LinkBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> LinkBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(LinkBrush));

    /// <summary>Defines the <see cref="SnippetTabstopHighlightBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> SnippetTabstopHighlightBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(SnippetTabstopHighlightBrush));

    /// <summary>Defines the <see cref="SnippetFinalTabstopHighlightBorderBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> SnippetFinalTabstopHighlightBorderBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(SnippetFinalTabstopHighlightBorderBrush));

    /// <summary>Defines the <see cref="DropTargetBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> DropTargetBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(DropTargetBrush));

    /// <summary>Defines the <see cref="SelectionAnchorBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> SelectionAnchorBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(SelectionAnchorBrush));

    private readonly List<IBrush> _decorationBrushes = new();
    private readonly List<double> _fadeOpacities = new();
    private int _decorationColorBase = -1;
    private int _fadeBase = -1;
    private int _fadeTableLength;
    private CodeEditorDecorationStyles? _decorationStyles;

    /// <summary>Errors.</summary>
    public IBrush? ErrorBrush { get => GetValue(ErrorBrushProperty); set => SetValue(ErrorBrushProperty, value); }

    /// <summary>Warnings.</summary>
    public IBrush? WarningBrush { get => GetValue(WarningBrushProperty); set => SetValue(WarningBrushProperty, value); }

    /// <summary>Information.</summary>
    public IBrush? InfoBrush { get => GetValue(InfoBrushProperty); set => SetValue(InfoBrushProperty, value); }

    /// <summary>Hints.</summary>
    public IBrush? HintBrush { get => GetValue(HintBrushProperty); set => SetValue(HintBrushProperty, value); }

    /// <summary>Muted text (unnecessary code, ghost text).</summary>
    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }

    /// <summary>The background of the current find match.</summary>
    public IBrush? FindMatchBrush { get => GetValue(FindMatchBrushProperty); set => SetValue(FindMatchBrushProperty, value); }

    /// <summary>The background of the other find matches.</summary>
    public IBrush? FindMatchHighlightBrush { get => GetValue(FindMatchHighlightBrushProperty); set => SetValue(FindMatchHighlightBrushProperty, value); }

    /// <summary>The background of the range find is limited to.</summary>
    public IBrush? FindRangeHighlightBrush { get => GetValue(FindRangeHighlightBrushProperty); set => SetValue(FindRangeHighlightBrushProperty, value); }

    /// <summary>The background of read occurrences of the symbol at the cursor.</summary>
    public IBrush? WordHighlightBrush { get => GetValue(WordHighlightBrushProperty); set => SetValue(WordHighlightBrushProperty, value); }

    /// <summary>The background of write occurrences of the symbol at the cursor.</summary>
    public IBrush? WordHighlightStrongBrush { get => GetValue(WordHighlightStrongBrushProperty); set => SetValue(WordHighlightStrongBrushProperty, value); }

    /// <summary>The background of other occurrences of the selected text.</summary>
    public IBrush? SelectionHighlightBrush { get => GetValue(SelectionHighlightBrushProperty); set => SetValue(SelectionHighlightBrushProperty, value); }

    /// <summary>The background of the matching bracket.</summary>
    public IBrush? BracketMatchBrush { get => GetValue(BracketMatchBrushProperty); set => SetValue(BracketMatchBrushProperty, value); }

    /// <summary>The border of the matching bracket.</summary>
    public IBrush? BracketMatchBorderBrush { get => GetValue(BracketMatchBorderBrushProperty); set => SetValue(BracketMatchBorderBrushProperty, value); }

    /// <summary>The background of ranges edited together.</summary>
    public IBrush? LinkedEditingBrush { get => GetValue(LinkedEditingBrushProperty); set => SetValue(LinkedEditingBrushProperty, value); }

    /// <summary>The background of a highlighted range.</summary>
    public IBrush? RangeHighlightBrush { get => GetValue(RangeHighlightBrushProperty); set => SetValue(RangeHighlightBrushProperty, value); }

    /// <summary>The foreground of inlay hints and code lenses.</summary>
    public IBrush? InlayHintForegroundBrush { get => GetValue(InlayHintForegroundBrushProperty); set => SetValue(InlayHintForegroundBrushProperty, value); }

    /// <summary>The background of inlay hints.</summary>
    public IBrush? InlayHintBackgroundBrush { get => GetValue(InlayHintBackgroundBrushProperty); set => SetValue(InlayHintBackgroundBrushProperty, value); }

    /// <summary>Glyphs in the gutter.</summary>
    public IBrush? GutterGlyphBrush { get => GetValue(GutterGlyphBrushProperty); set => SetValue(GutterGlyphBrushProperty, value); }

    /// <summary>The background of the first line of a folded range.</summary>
    public IBrush? FoldBackgroundBrush { get => GetValue(FoldBackgroundBrushProperty); set => SetValue(FoldBackgroundBrushProperty, value); }

    /// <summary>Links under the pointer.</summary>
    public IBrush? LinkBrush { get => GetValue(LinkBrushProperty); set => SetValue(LinkBrushProperty, value); }

    /// <summary>The tab stops of a snippet (upstream's editor.snippetTabstopHighlightBackground).</summary>
    public IBrush? SnippetTabstopHighlightBrush { get => GetValue(SnippetTabstopHighlightBrushProperty); set => SetValue(SnippetTabstopHighlightBrushProperty, value); }

    /// <summary>The outline of a snippet's final tab stop (upstream's editor.snippetFinalTabstopHighlightBorder).</summary>
    public IBrush? SnippetFinalTabstopHighlightBorderBrush { get => GetValue(SnippetFinalTabstopHighlightBorderBrushProperty); set => SetValue(SnippetFinalTabstopHighlightBorderBrushProperty, value); }

    /// <summary>Defines the <see cref="ColorDecoratorBorderBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ColorDecoratorBorderBrushProperty = AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(ColorDecoratorBorderBrush));

    /// <summary>The border of the color swatches before color values.</summary>
    public IBrush? ColorDecoratorBorderBrush { get => GetValue(ColorDecoratorBorderBrushProperty); set => SetValue(ColorDecoratorBorderBrushProperty, value); }

    /// <summary>The marker where dragged text would be dropped.</summary>
    public IBrush? DropTargetBrush { get => GetValue(DropTargetBrushProperty); set => SetValue(DropTargetBrushProperty, value); }

    /// <summary>The selection anchor's marker.</summary>
    public IBrush? SelectionAnchorBrush { get => GetValue(SelectionAnchorBrushProperty); set => SetValue(SelectionAnchorBrushProperty, value); }

    // the properties of the theme colors (an immutable table)
    private static readonly HashSet<AvaloniaProperty> s_themeBrushProperties =
    [
        ErrorBrushProperty, WarningBrushProperty, InfoBrushProperty, HintBrushProperty, MutedBrushProperty,
        FindMatchBrushProperty, FindMatchHighlightBrushProperty, FindRangeHighlightBrushProperty, WordHighlightBrushProperty,
        WordHighlightStrongBrushProperty, SelectionHighlightBrushProperty, BracketMatchBrushProperty,
        BracketMatchBorderBrushProperty, LinkedEditingBrushProperty, RangeHighlightBrushProperty,
        InlayHintForegroundBrushProperty, InlayHintBackgroundBrushProperty, GutterGlyphBrushProperty, LinkBrushProperty,
        SnippetTabstopHighlightBrushProperty, SnippetFinalTabstopHighlightBorderBrushProperty,
        ColorDecoratorBorderBrushProperty,
        FoldBackgroundBrushProperty,
        DropTargetBrushProperty, SelectionAnchorBrushProperty,
    ];

    private static bool IsThemeBrushProperty(AvaloniaProperty property) => s_themeBrushProperties.Contains(property);

    /// <summary>The brush the control theme set for a theme color, or null.</summary>
    internal IBrush? GetThemeBrush(CodeEditorThemeColor color) => color switch
    {
        CodeEditorThemeColor.Error => ErrorBrush,
        CodeEditorThemeColor.Warning => WarningBrush,
        CodeEditorThemeColor.Info => InfoBrush,
        CodeEditorThemeColor.Hint => HintBrush,
        CodeEditorThemeColor.Muted => MutedBrush,
        CodeEditorThemeColor.FindMatch => FindMatchBrush,
        CodeEditorThemeColor.FindMatchHighlight => FindMatchHighlightBrush,
        CodeEditorThemeColor.FindRangeHighlight => FindRangeHighlightBrush,
        CodeEditorThemeColor.WordHighlight => WordHighlightBrush,
        CodeEditorThemeColor.WordHighlightStrong => WordHighlightStrongBrush,
        CodeEditorThemeColor.SelectionHighlight => SelectionHighlightBrush,
        CodeEditorThemeColor.BracketMatch => BracketMatchBrush,
        CodeEditorThemeColor.BracketMatchBorder => BracketMatchBorderBrush,
        CodeEditorThemeColor.LinkedEditing => LinkedEditingBrush,
        CodeEditorThemeColor.RangeHighlight => RangeHighlightBrush,
        CodeEditorThemeColor.InlayHintForeground => InlayHintForegroundBrush,
        CodeEditorThemeColor.InlayHintBackground => InlayHintBackgroundBrush,
        CodeEditorThemeColor.GutterGlyph => GutterGlyphBrush,
        CodeEditorThemeColor.Link => LinkBrush,
        CodeEditorThemeColor.SnippetTabstopHighlight => SnippetTabstopHighlightBrush,
        CodeEditorThemeColor.SnippetFinalTabstopHighlightBorder => SnippetFinalTabstopHighlightBorderBrush,
        CodeEditorThemeColor.ColorDecoratorBorder => ColorDecoratorBorderBrush,
        CodeEditorThemeColor.FoldBackground => FoldBackgroundBrush,
        CodeEditorThemeColor.DropTarget => DropTargetBrush,
        CodeEditorThemeColor.SelectionAnchor => SelectionAnchorBrush,
        _ => null,
    };

    /// <summary>The brush a decoration style names, or null.</summary>
    internal IBrush? Resolve(DecorationBrush brush) =>
        brush.Brush ?? (brush.ThemeColor is { } color ? GetThemeBrush(color) : null);

    private void AttachDecorationStyles()
    {
        DetachDecorationStyles();
        _decorationStyles = _editor?.DecorationStyles;
        if (_decorationStyles is not null)
        {
            _decorationStyles.Changed += OnDecorationStylesChanged;
        }
    }

    private void DetachDecorationStyles()
    {
        if (_decorationStyles is not null)
        {
            _decorationStyles.Changed -= OnDecorationStylesChanged;
            _decorationStyles = null;
        }
    }

    private void OnDecorationStylesChanged() => InvalidateLayers(CodeEditorLayers.All);

    private void OnThemeBrushChanged()
    {
        // the inline colors live in the palette: start it over with the new brushes
        _decorationBrushes.Clear();
        _fadeOpacities.Clear();
        UpdatePalette();
        InvalidateLayers(CodeEditorLayers.All);
    }

    // ---- what to draw (also the tests' view of it)

    /// <summary>Where a decoration's class name is drawn.</summary>
    internal enum DecorationDrawKind
    {
        /// <summary>A background or border of a range in the text area.</summary>
        Range,

        /// <summary>A background or border of whole lines in the text area.</summary>
        WholeLine,

        /// <summary>An underline above the text.</summary>
        Underline,

        /// <summary>A background behind the text of an inline class name (injected text).</summary>
        Inline,
    }

    /// <summary>A rectangle the view draws for a decoration (relative to the view).</summary>
    internal readonly record struct DecorationRect(
        string ClassName,
        DecorationDrawKind Kind,
        int ViewLineNumber,
        Rect Bounds,
        IBrush? Fill,
        IBrush? Stroke,
        double StrokeThickness,
        DecorationUnderlineKind Underline,
        double CornerRadius = 0);

    /// <summary>The rectangles of the decorations on the visible view lines <paramref name="startLine"/> to <paramref name="endLine"/>.</summary>
    internal List<DecorationRect> GetDecorationRects(int startLine, int endLine)
    {
        var result = new List<DecorationRect>();
        if (_viewModel is not { } viewModel || _editor is not { } editor || endLine < startLine)
        {
            return result;
        }

        var styles = editor.DecorationStyles;
        var visibleRange = new TextRange(startLine, 1, endLine, viewModel.GetLineMaxColumn(endLine));
        var decorations = viewModel.GetDecorationsInViewport(visibleRange)
            .Select((d, i) => (Decoration: d, Index: i))
            .OrderBy(static d => d.Decoration.Options.ZIndex)
            .ThenBy(static d => d.Index)
            .Select(static d => d.Decoration);
        var textLeft = GutterWidth;
        var textRight = TextAreaRight;
        foreach (var decoration in decorations)
        {
            var options = decoration.Options;
            AddRects(result, styles, options.ClassName, decoration, startLine, endLine, textLeft, textRight, fromInline: false);
            AddRects(result, styles, options.InlineClassName, decoration, startLine, endLine, textLeft, textRight, fromInline: true);
        }

        AddInlineBackgroundRects(result, styles, visibleRange, startLine, endLine);
        return result;
    }

    // InlineBackground of the inline decorations of each line (injected text included), adjacent parts merged
    private void AddInlineBackgroundRects(List<DecorationRect> result, CodeEditorDecorationStyles styles, TextRange visibleRange, int startLine, int endLine)
    {
        for (var line = startLine; line <= endLine; line++)
        {
            var data = _viewModel!.GetViewportViewLineRenderingData(visibleRange, line);
            DecorationRect? pending = null;
            foreach (var decoration in data.InlineDecorations.OrderBy(static d => d.Range.StartColumn))
            {
                foreach (var style in styles.GetAll(decoration.InlineClassName))
                {
                    if (Resolve(style.InlineBackground) is not { } fill)
                    {
                        continue;
                    }

                    var startColumn = decoration.Range.StartLineNumber < line ? 1 : decoration.Range.StartColumn;
                    var endColumn = decoration.Range.EndLineNumber > line ? data.MaxColumn : decoration.Range.EndColumn;
                    var x1 = GetX(line, startColumn);
                    var x2 = GetX(line, endColumn);
                    if (x2 <= x1)
                    {
                        continue;
                    }

                    var bounds = new Rect(x1, GetY(line) + 1, x2 - x1, Math.Max(0, _lineHeight - 2));
                    if (pending is { } p && ReferenceEquals(p.Fill, fill) && Math.Abs(p.Bounds.Right - x1) < 0.5)
                    {
                        pending = p with { Bounds = p.Bounds.Union(bounds) };
                    }
                    else
                    {
                        if (pending is { } done)
                        {
                            result.Add(done);
                        }

                        pending = new DecorationRect(decoration.InlineClassName, DecorationDrawKind.Inline, line, bounds, fill, null, 0, DecorationUnderlineKind.None, style.InlineCornerRadius);
                    }

                    break;
                }
            }

            if (pending is { } last)
            {
                result.Add(last);
            }
        }
    }

    private void AddRects(
        List<DecorationRect> result,
        CodeEditorDecorationStyles styles,
        string? className,
        ViewModelDecoration decoration,
        int startLine,
        int endLine,
        double textLeft,
        double textRight,
        bool fromInline)
    {
        if (string.IsNullOrEmpty(className))
        {
            return;
        }

        foreach (var style in styles.GetAll(className))
        {
            var fill = fromInline ? null : Resolve(style.Background);
            var stroke = fromInline ? null : Resolve(style.Border);
            var underline = style.Underline != DecorationUnderlineKind.None ? Resolve(style.UnderlineBrush) : null;
            if (fill is null && stroke is null && underline is null)
            {
                continue;
            }

            var options = decoration.Options;
            var range = decoration.Range;
            var from = Math.Max(startLine, range.StartLineNumber);
            var to = Math.Min(endLine, range.EndLineNumber);
            if (!options.IsWholeLine && range.EndColumn == 1 && range.EndLineNumber > range.StartLineNumber && options.ShowIfCollapsed)
            {
                // upstream: a range that ends at the start of a line does not paint that line
                to = Math.Min(to, range.EndLineNumber - 1);
            }

            var includeLineBreak = options.ShouldFillLineOnLineBreak == true || className == "findMatch";
            for (var line = from; line <= to; line++)
            {
                var y = GetY(line);
                double x1, x2;
                if (options.IsWholeLine)
                {
                    x1 = textLeft;
                    x2 = textRight;
                }
                else
                {
                    var startColumn = line == range.StartLineNumber ? range.StartColumn : 1;
                    var endColumn = line == range.EndLineNumber ? range.EndColumn : _viewModel!.GetLineMaxColumn(line);
                    x1 = GetX(line, startColumn);
                    x2 = GetX(line, endColumn);
                    if (line != range.EndLineNumber && includeLineBreak)
                    {
                        x2 += _charWidth / 2;
                    }

                    if (x2 <= x1)
                    {
                        // upstream renders a collapsed range only with showIfCollapsed, half a character wide, or as
                        // wide as its CSS says (the drop marker, the selection anchor)
                        if (style.CollapsedWidth > 0)
                        {
                            x2 = x1 + style.CollapsedWidth;
                        }
                        else if (!options.ShowIfCollapsed)
                        {
                            continue;
                        }
                        else
                        {
                            x2 = x1 + (_charWidth / 2);
                        }
                    }
                }

                var bounds = new Rect(x1, y, Math.Max(0, x2 - x1), _lineHeight);
                if (fill is not null || stroke is not null)
                {
                    result.Add(new DecorationRect(className, options.IsWholeLine ? DecorationDrawKind.WholeLine : DecorationDrawKind.Range, line, bounds, fill, stroke, style.BorderThickness, DecorationUnderlineKind.None));
                }

                if (underline is not null)
                {
                    result.Add(new DecorationRect(className, DecorationDrawKind.Underline, line, bounds, null, underline, 1, style.Underline));
                }
            }
        }
    }

    // background layer: className / whole-line backgrounds and borders
    private void RenderDecorationBackgrounds(DrawingContext context, int startLine, int endLine)
    {
        RenderInlineBackgrounds(context, startLine, endLine);
        foreach (var rect in GetDecorationRects(startLine, endLine))
        {
            if (rect.Kind == DecorationDrawKind.Underline)
            {
                continue;
            }

            if (rect.Fill is { } fill)
            {
                if (rect.CornerRadius > 0)
                {
                    context.DrawRectangle(fill, null, rect.Bounds, rect.CornerRadius, rect.CornerRadius);
                }
                else
                {
                    context.FillRectangle(fill, rect.Bounds);
                }
            }

            if (rect.Stroke is { } stroke && rect.StrokeThickness > 0)
            {
                var half = rect.StrokeThickness / 2;
                context.DrawRectangle(null, new ImmutablePen(stroke.ToImmutable(), rect.StrokeThickness), rect.Bounds.Deflate(half));
            }
        }
    }

    // decorations layer: the underlines, above the text
    private void RenderDecorations(DrawingContext context)
    {
        if (!CanDraw)
        {
            return;
        }

        var (startLine, endLine) = GetVisibleLines();
        using (context.PushClip(TextArea(GutterWidth)))
        {
            foreach (var rect in GetDecorationRects(startLine, endLine))
            {
                if (rect.Kind == DecorationDrawKind.Underline && rect.Stroke is { } brush)
                {
                    DrawUnderline(context, rect.Underline, brush, rect.Bounds);
                }
            }

            RenderContentDecorations(context, startLine, endLine);
        }
    }

    private void DrawUnderline(DrawingContext context, DecorationUnderlineKind kind, IBrush brush, Rect bounds)
    {
        var bottom = bounds.Bottom - 1;
        var width = Math.Max(bounds.Width, 2);
        switch (kind)
        {
            case DecorationUnderlineKind.Solid:
                context.FillRectangle(brush, new Rect(bounds.X, bottom - 1, width, 1));
                break;
            case DecorationUnderlineKind.Dashed:
                for (var x = bounds.X; x < bounds.X + width; x += 6)
                {
                    context.FillRectangle(brush, new Rect(x, bottom - 1, Math.Min(3, bounds.X + width - x), 1));
                }

                break;
            case DecorationUnderlineKind.Dotted:
                // upstream's hint: dots under the start of the range
                for (var x = bounds.X + 1; x < bounds.X + width; x += 3)
                {
                    context.FillRectangle(brush, new Rect(x, bottom - 1.5, 1.5, 1.5));
                }

                break;
            case DecorationUnderlineKind.Squiggly:
            {
                // a wave of 4 px periods, 3 px high (upstream's squiggly SVG is 6 × 3)
                var geometry = new StreamGeometry();
                using (var g = geometry.Open())
                {
                    const double half = 2;
                    var top = bottom - 3;
                    g.BeginFigure(new Point(bounds.X, bottom), false);
                    var up = true;
                    for (var x = bounds.X + half; x <= bounds.X + width + half; x += half)
                    {
                        g.LineTo(new Point(Math.Min(x, bounds.X + width), up ? top : bottom));
                        up = !up;
                    }

                    g.EndFigure(false);
                }

                context.DrawGeometry(null, new ImmutablePen(brush.ToImmutable(), 1), geometry);
                break;
            }
        }
    }

    // ---- inline class names: color overrides of the text

    /// <summary>
    /// Makes sure the palette has the brushes and faded tables the inline decorations of the visible lines need;
    /// called before drawing the text, as a new palette starts the text cache over.
    /// </summary>
    private void EnsureDecorationPalette(TextRange visibleRange)
    {
        if (_viewModel is not { } viewModel || _editor is not { } editor)
        {
            return;
        }

        var changed = false;
        foreach (var decoration in viewModel.GetDecorationsInViewport(visibleRange))
        {
            var o = decoration.Options;
            foreach (var style in editor.DecorationStyles.GetAll(o.InlineClassName)
                .Concat(editor.DecorationStyles.GetAll(o.Before?.InlineClassName))
                .Concat(editor.DecorationStyles.GetAll(o.After?.InlineClassName)))
            {
                if (Resolve(style.Foreground) is { } brush && !_decorationBrushes.Contains(brush))
                {
                    _decorationBrushes.Add(brush);
                    changed = true;
                }

                if (style.Opacity < 1 && !_fadeOpacities.Contains(style.Opacity))
                {
                    _fadeOpacities.Add(style.Opacity);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            UpdatePalette();
        }
    }

    /// <summary>Appends the decoration brushes and the faded tables to a palette (from UpdatePalette).</summary>
    private void AppendDecorationPalette(List<IBrush> palette)
    {
        _decorationColorBase = palette.Count;
        palette.AddRange(_decorationBrushes);
        _fadeTableLength = palette.Count;
        _fadeBase = palette.Count;
        foreach (var opacity in _fadeOpacities)
        {
            for (var i = 0; i < _fadeTableLength; i++)
            {
                palette.Add(Fade(palette[i], opacity));
            }
        }

        static IBrush Fade(IBrush brush, double opacity) => brush is ISolidColorBrush solid
            ? new ImmutableSolidColorBrush(solid.Color, solid.Opacity * opacity)
            : brush;
    }

    /// <summary>The inline decoration overrides of a view line, merged with the bracket colors (sorted, not overlapping).</summary>
    private List<ColorOverride>? GetInlineOverrides(int viewLineNumber, ViewLineRenderingData data, List<ColorOverride>? bracketColors)
    {
        if (_editor is not { } editor || data.InlineDecorations.Count == 0 || _decorationColorBase < 0)
        {
            return bracketColors;
        }

        // the ranges each style applies to, in order: brackets first, decorations over them
        var parts = new List<(int Start, int End, int Color, TokenFontStyle Style, double Opacity)>();
        if (bracketColors is not null)
        {
            foreach (var o in bracketColors)
            {
                parts.Add((o.Start, o.End, o.Color, o.Style, 1));
            }
        }

        var maxColumn = data.MaxColumn;
        var any = false;
        foreach (var decoration in data.InlineDecorations)
        {
            foreach (var style in editor.DecorationStyles.GetAll(decoration.InlineClassName))
            {
                var color = Resolve(style.Foreground) is { } brush ? _decorationBrushes.IndexOf(brush) : -1;
                if (color >= 0)
                {
                    color += _decorationColorBase;
                }

                var opacity = style.Opacity < 1 && _fadeOpacities.Contains(style.Opacity) ? style.Opacity : 1;
                if (color < 0 && style.FontStyle == TokenFontStyle.None && opacity >= 1)
                {
                    continue;
                }

                var range = decoration.Range;
                var startColumn = range.StartLineNumber < viewLineNumber ? 1 : range.StartColumn;
                var endColumn = range.EndLineNumber > viewLineNumber ? maxColumn : range.EndColumn;
                if (endColumn <= startColumn)
                {
                    continue;
                }

                parts.Add((startColumn - 1, endColumn - 1, color, style.FontStyle, opacity));
                any = true;
            }
        }

        if (!any)
        {
            return bracketColors;
        }

        // sweep the boundaries: later parts win the color, styles add up, the lowest opacity counts
        var bounds = new SortedSet<int>();
        foreach (var p in parts)
        {
            bounds.Add(p.Start);
            bounds.Add(p.End);
        }

        var result = new List<ColorOverride>();
        var points = bounds.ToArray();
        for (var i = 0; i + 1 < points.Length; i++)
        {
            int a = points[i], b = points[i + 1];
            var color = -1;
            var style = TokenFontStyle.None;
            var opacity = 1.0;
            var covered = false;
            foreach (var p in parts)
            {
                if (p.Start <= a && p.End >= b)
                {
                    covered = true;
                    if (p.Color >= 0)
                    {
                        color = p.Color;
                    }

                    style |= p.Style;
                    opacity = Math.Min(opacity, p.Opacity);
                }
            }

            if (!covered)
            {
                continue;
            }

            var fadeBase = opacity < 1 ? _fadeBase + (_fadeOpacities.IndexOf(opacity) * _fadeTableLength) : -1;
            var next = new ColorOverride(a, b, color, style, fadeBase);
            if (result.Count > 0 && result[^1] is var last && last.End == a && last.Color == color && last.Style == style && last.FadeBase == fadeBase)
            {
                result[^1] = last with { End = b };
            }
            else
            {
                result.Add(next);
            }
        }

        return result;
    }
}
