// Renders the Markdown of hovers and parameter hints with Avalonia text (upstream: src/vs/base/browser/
// markdownRenderer.ts with marked into sanitized HTML, and src/vs/editor/browser/widget/markdownRenderer/browser/
// editorMarkdownCodeBlockRenderer.ts for the code blocks; commit b57d5c9e, MIT license; behavior reference only).
// The blocks come from the engine's parser (HoverMarkdown): paragraphs, headings, lists, quotes and rules become text
// blocks and panels; fenced code blocks are coloured by the editor's own tokenizer and token theme (the language of the
// fence, else the editor's); links are runs in the link colour that open on click (command: links run an editor
// command when the Markdown is trusted). Every colour binds to the CodeEditorHoverSurface, so it follows the theme.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Editor.Contrib.Hover;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TokenFontStyle = TKCode.Arc.Editor.Tokens.FontStyle;

namespace TKCode.Arc;

/// <summary>Turns hover Markdown into controls on a <see cref="CodeEditorHoverSurface"/>.</summary>
internal sealed class HoverMarkdownRenderer
{
    private const double BlockSpacing = 6;
    private const double ListIndent = 18;

    private readonly CodeEditorHoverSurface _surface;
    private readonly CodeEditor? _editor;
    private readonly Func<string, bool, bool>? _openLink;

    /// <param name="surface">The card whose brushes the text binds to.</param>
    /// <param name="editor">The editor whose tokenizers colour code blocks; null leaves them uncoloured.</param>
    /// <param name="openLink">Opens a link target (the target, whether the Markdown is trusted); true when handled.</param>
    public HoverMarkdownRenderer(CodeEditorHoverSurface surface, CodeEditor? editor, Func<string, bool, bool>? openLink)
    {
        _surface = surface;
        _editor = editor;
        _openLink = openLink;
    }

    /// <summary>The controls of one Markdown string (upstream's renderMarkdown).</summary>
    public Control Render(MarkdownString markdown) => RenderBlocks(HoverMarkdown.Parse(markdown.Value), markdown.IsTrusted);

    /// <summary>The controls of parsed blocks, top to bottom.</summary>
    public Control RenderBlocks(IReadOnlyList<MarkdownBlock> blocks, bool isTrusted)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = BlockSpacing };
        foreach (var block in blocks)
        {
            panel.Children.Add(RenderBlock(block, isTrusted));
        }

        return panel;
    }

    private Control RenderBlock(MarkdownBlock block, bool isTrusted)
    {
        switch (block)
        {
            case MarkdownParagraph paragraph:
                return RenderInlines(paragraph.Runs, isTrusted);
            case MarkdownHeading heading:
            {
                var text = RenderInlines(heading.Runs, isTrusted);
                text.FontWeight = FontWeight.SemiBold;
                if (heading.Level <= 2)
                {
                    text.FontSize = _surface.FontSize * (heading.Level == 1 ? 1.25 : 1.12);
                }

                return text;
            }

            case MarkdownCodeBlock code:
                return RenderCodeBlock(code.Code, code.Language);
            case MarkdownQuote quote:
            {
                var border = new Border
                {
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    Padding = new Thickness(8, 0, 0, 0),
                    Child = RenderBlocks(quote.Blocks, isTrusted),
                };
                _surface.BindTo(border, Border.BorderBrushProperty, CodeEditorHoverSurface.SeparatorBrushProperty);
                _surface.BindTo(border, TextElement.ForegroundProperty, CodeEditorHoverSurface.MutedForegroundProperty);
                return border;
            }

            case MarkdownList list:
                return RenderList(list, isTrusted);
            case MarkdownRule:
            {
                var rule = new Border { Height = 1, Margin = new Thickness(0, 2) };
                _surface.BindTo(rule, Border.BackgroundProperty, CodeEditorHoverSurface.SeparatorBrushProperty);
                return rule;
            }

            default:
                return new Panel();
        }
    }

    private Control RenderList(MarkdownList list, bool isTrusted)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions($"{ListIndent},*") };
        for (var i = 0; i < list.Items.Count; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var marker = new TextBlock
            {
                Text = list.Ordered ? $"{list.Start + i}." : "•",
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            Grid.SetRow(marker, i);
            grid.Children.Add(marker);

            var item = RenderBlocks(list.Items[i], isTrusted);
            item.Margin = new Thickness(0, 0, 0, i + 1 < list.Items.Count ? 2 : 0);
            Grid.SetRow(item, i);
            Grid.SetColumn(item, 1);
            grid.Children.Add(item);
        }

        return grid;
    }

    /// <summary>A wrapping text block of inline runs: bold, italic, strikethrough, code spans and links.</summary>
    public HoverTextBlock RenderInlines(IReadOnlyList<MarkdownRun> runs, bool isTrusted)
    {
        var text = new HoverTextBlock { TextWrapping = TextWrapping.Wrap, OpenLink = target => _openLink?.Invoke(target, isTrusted) ?? false };
        var inlines = text.Inlines!;
        var offset = 0;
        foreach (var run in runs)
        {
            var content = run.Kind == MarkdownRunKind.LineBreak ? "\n" : run.Text;
            if (content.Length == 0)
            {
                continue;
            }

            var inline = new Run(content);
            if (run.Style.HasFlag(MarkdownRunStyle.Bold))
            {
                inline.FontWeight = FontWeight.Bold;
            }

            if (run.Style.HasFlag(MarkdownRunStyle.Italic))
            {
                inline.FontStyle = FontStyle.Italic;
            }

            if (run.Style.HasFlag(MarkdownRunStyle.Strikethrough))
            {
                inline.TextDecorations = TextDecorations.Strikethrough;
            }

            if (run.Kind == MarkdownRunKind.Code)
            {
                _surface.BindTo(inline, TextElement.FontFamilyProperty, CodeEditorHoverSurface.CodeFontFamilyProperty);
                _surface.BindTo(inline, TextElement.BackgroundProperty, CodeEditorHoverSurface.CodeBackgroundProperty);
            }

            if (run.Href is { } href)
            {
                // underlined as well: the link colour alone may be close to the text's (Arc's neutral accent)
                inline.TextDecorations = run.Style.HasFlag(MarkdownRunStyle.Strikethrough) ? TextDecorations.Strikethrough : TextDecorations.Underline;
                _surface.BindTo(inline, TextElement.ForegroundProperty, CodeEditorHoverSurface.LinkForegroundProperty);
                text.AddLink(offset, content.Length, href);
            }

            inlines.Add(inline);
            offset += content.Length;
        }

        return text;
    }

    /// <summary>A code block coloured by the editor's tokenizer for <paramref name="languageAlias"/> (the editor's language when null).</summary>
    public Control RenderCodeBlock(string code, string? languageAlias)
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap };
        _surface.BindTo(text, TextElement.FontFamilyProperty, CodeEditorHoverSurface.CodeFontFamilyProperty);
        var border = new Border
        {
            Padding = new Thickness(6, 4),
            CornerRadius = new CornerRadius(3),
            Child = text,
        };
        _surface.BindTo(border, Border.BackgroundProperty, CodeEditorHoverSurface.CodeBackgroundProperty);

        if (_editor is not { } editor)
        {
            text.Text = code;
            return border;
        }

        var languageId = MarkdownCodeTokenizer.GetLanguageId(editor.LanguageServices.LanguageService, languageAlias, editor.Model.GetLanguageId());
        var support = editor.TokenizationRegistry.GetOrCreateAsync(languageId);
        if (support.IsCompletedSuccessfully)
        {
            FillCode(text, code, support.Result, editor);
        }
        else
        {
            text.Text = code;
            _ = FillCodeLater(text, code, support, editor);
        }

        return border;
    }

    private static async Task FillCodeLater(TextBlock text, string code, Task<ITokenizationSupport?> support, CodeEditor editor)
    {
        try
        {
            var resolved = await support.ConfigureAwait(true);
            FillCode(text, code, resolved, editor);
        }
        catch (Exception e)
        {
            TKCode.Arc.Editor.UnexpectedErrors.Report(e);
        }
    }

    private static void FillCode(TextBlock text, string code, ITokenizationSupport? support, CodeEditor editor)
    {
        var lines = MarkdownCodeTokenizer.Tokenize(code, support, editor.LanguageIdCodec.EncodeLanguageId(editor.Model.GetLanguageId()));
        var palette = editor.View?.TextCache;
        text.Text = null;
        var inlines = text.Inlines!;
        inlines.Clear();
        for (var i = 0; i < lines.Count; i++)
        {
            foreach (var token in lines[i])
            {
                var run = new Run(token.Text);
                if (palette is not null)
                {
                    run.Foreground = palette.GetBrush((int)token.Foreground, text.Foreground ?? Brushes.Gray);
                }

                var style = token.FontStyle;
                if (style.HasFlag(TokenFontStyle.Bold))
                {
                    run.FontWeight = FontWeight.Bold;
                }

                if (style.HasFlag(TokenFontStyle.Italic))
                {
                    run.FontStyle = FontStyle.Italic;
                }

                if (style.HasFlag(TokenFontStyle.Underline))
                {
                    run.TextDecorations = TextDecorations.Underline;
                }

                inlines.Add(run);
            }

            if (i + 1 < lines.Count)
            {
                inlines.Add(new Run("\n"));
            }
        }
    }
}

/// <summary>A text block whose link runs open on click (upstream: the anchors of rendered Markdown).</summary>
internal sealed class HoverTextBlock : TextBlock
{
    private readonly List<(int Start, int Length, string Href)> _links = [];

    /// <summary>Opens a link target; true when it was handled.</summary>
    public Func<string, bool>? OpenLink { get; set; }

    /// <summary>The links: character ranges of the text and their targets.</summary>
    public IReadOnlyList<(int Start, int Length, string Href)> Links => _links;

    /// <summary>Marks the characters [start, start + length) as a link to <paramref name="href"/>.</summary>
    public void AddLink(int start, int length, string href) => _links.Add((start, length, href));

    /// <summary>The link at a point of this block, or null.</summary>
    public string? GetLinkAt(Point point)
    {
        if (_links.Count == 0)
        {
            return null;
        }

        var layout = TextLayout;
        var local = new Point(point.X - Padding.Left, point.Y - Padding.Top);
        var hit = layout.HitTestPoint(local);
        if (!hit.IsInside)
        {
            return null;
        }

        var index = hit.TextPosition;
        foreach (var (start, length, href) in _links)
        {
            if (index >= start && index < start + length)
            {
                return href;
            }
        }

        return null;
    }

    /// <summary>Opens the link at the point, if any; true when one opened (for tests and the pointer handler).</summary>
    public bool OpenLinkAt(Point point) => GetLinkAt(point) is { } href && (OpenLink?.Invoke(href) ?? false);

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_links.Count > 0)
        {
            Cursor = GetLinkAt(e.GetPosition(this)) is not null ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && OpenLinkAt(e.GetPosition(this)))
        {
            e.Handled = true;
        }
    }
}
