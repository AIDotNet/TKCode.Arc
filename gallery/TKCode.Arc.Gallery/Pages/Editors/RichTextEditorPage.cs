using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;

namespace TKCode.Arc.Gallery.Pages;

public static class RichTextEditorPage
{
    private const string Sample = "# Launch notes\n\nType **Markdown** as you go: `#` and a space makes a heading, `-` a list, `>` a quote, and three backticks a code block. Select text for the _floating toolbar_, or type `/` on an empty line for blocks.\n\n- Shortcuts: ⌘B, ⌘I, ⌘⇧X, ⌘E and ⌘K\n- Tab nests a list item\n  - Shift+Tab brings it back\n\n> Undo is the editor's own: ⌘Z and ⌘⇧Z.";

    public static Control Create() => GalleryUi.Column(20,
        GalleryUi.Card("Write with shortcuts",
            "Markdown shortcuts convert as you type, the toolbar follows the selection and morphs into a link field (⌘K), and \"/\" opens the block menu.",
            Paper(new RichTextEditor { Markdown = Sample })),
        GalleryUi.Card("Your own toolbar",
            "Formats, CanUndo and CanRedo report the state for a fixed toolbar; ToggleMark, SetBlockType, EditLink and the undo commands drive the editor.",
            OwnToolbar()),
        GalleryUi.Card("Markdown in, HTML out",
            "Html and Markdown are two-way: assigning either reloads the editor, and both follow every edit.",
            Output()),
        GalleryUi.Card("Read only",
            "Editing, the toolbar and the shortcuts are off; text can still be selected and copied.",
            Paper(new RichTextEditor
            {
                IsReadOnly = true,
                Markdown = "## Release checklist\n\n1. Freeze the branch\n2. Run the **full** test suite\n3. Tag and ship\n\n```\ngit tag v1.4.0\n```"
            })),
        GalleryUi.Card("Placeholder",
            "An empty editor shows its placeholder; an empty line holding the caret shows the block hint.",
            Paper(new RichTextEditor { Placeholder = "Leave a comment", BlockHint = "Type / for blocks" })));

    /// <summary>The surface the editor sits on in Arc's demos.</summary>
    private static Border Paper(Control content)
    {
        var paper = new Border
        {
            Padding = new Thickness(24, 20),
            CornerRadius = new CornerRadius(26),
            BorderThickness = new Thickness(1),
            MaxWidth = 680,
            Child = content
        };
        ArcTokens.Bind(paper, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(paper, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        return paper;
    }

    private static Control OwnToolbar()
    {
        var editor = new RichTextEditor
        {
            Markdown = "Select some text, then use the buttons above. They show what is active at the caret."
        };
        var buttons = new List<(Button Button, Func<RichTextFormats, bool> IsActive)>();

        Button Tool(Geometry icon, string label, Action action, Func<RichTextFormats, bool>? isActive = null)
        {
            var button = new Button
            {
                Variant = ButtonVariant.Ghost,
                Size = ControlSize.Small,
                Width = 36,
                Padding = default,
                Content = new ArcGlyph { Data = icon, Size = 16, StrokeThickness = 1.75 },
                // The editor keeps the caret: these buttons never take focus.
                Focusable = false
            };
            Avalonia.Automation.AutomationProperties.SetName(button, label);
            Tooltip.SetTip(button, label);
            button.Click += (_, _) => action();
            if (isActive is not null)
            {
                buttons.Add((button, isActive));
            }
            return button;
        }

        var undo = Tool(ArcIcons.Undo2, "Undo", editor.Undo);
        var redo = Tool(ArcIcons.Redo2, "Redo", editor.Redo);
        var bar = new WrapPanel
        {
            ItemSpacing = 2,
            LineSpacing = 4,
            Children =
            {
                Tool(ArcIcons.Bold, "Bold", () => editor.ToggleMark(RichTextMark.Bold), f => f.IsBold),
                Tool(ArcIcons.Italic, "Italic", () => editor.ToggleMark(RichTextMark.Italic), f => f.IsItalic),
                Tool(ArcIcons.Strikethrough, "Strikethrough", () => editor.ToggleMark(RichTextMark.Strikethrough), f => f.IsStrikethrough),
                Tool(ArcIcons.Code, "Inline code", () => editor.ToggleMark(RichTextMark.Code), f => f.IsCode),
                Tool(ArcIcons.Link, "Link", editor.EditLink, f => f.Link is not null),
                Divider(),
                Tool(ArcIcons.Heading1, "Heading 1", () => editor.SetBlockType(RichTextBlockType.Heading1), f => f.BlockType == RichTextBlockType.Heading1),
                Tool(ArcIcons.Heading2, "Heading 2", () => editor.SetBlockType(RichTextBlockType.Heading2), f => f.BlockType == RichTextBlockType.Heading2),
                Tool(ArcIcons.Heading3, "Heading 3", () => editor.SetBlockType(RichTextBlockType.Heading3), f => f.BlockType == RichTextBlockType.Heading3),
                Tool(ArcIcons.List, "Bulleted list", () => editor.SetBlockType(RichTextBlockType.BulletedList), f => f.BlockType == RichTextBlockType.BulletedList),
                Tool(ArcIcons.ListOrdered, "Numbered list", () => editor.SetBlockType(RichTextBlockType.NumberedList), f => f.BlockType == RichTextBlockType.NumberedList),
                Tool(ArcIcons.Quote, "Quote", () => editor.SetBlockType(RichTextBlockType.Quote), f => f.BlockType == RichTextBlockType.Quote),
                Tool(ArcIcons.SquareCode, "Code block", () => editor.SetBlockType(RichTextBlockType.CodeBlock), f => f.BlockType == RichTextBlockType.CodeBlock),
                Divider(),
                undo,
                redo
            }
        };
        void Refresh()
        {
            foreach (var (button, isActive) in buttons)
            {
                button.Variant = isActive(editor.Formats) ? ButtonVariant.Secondary : ButtonVariant.Ghost;
            }
            undo.IsEnabled = editor.CanUndo;
            redo.IsEnabled = editor.CanRedo;
        }
        editor.PropertyChanged += (_, e) =>
        {
            if (e.Property == RichTextEditor.FormatsProperty || e.Property == RichTextEditor.CanUndoProperty || e.Property == RichTextEditor.CanRedoProperty)
            {
                Refresh();
            }
        };
        Refresh();
        return Paper(GalleryUi.Column(16, bar, editor));
    }

    private static Border Divider()
    {
        var divider = new Border { Width = 1, Height = 20, Margin = new Thickness(6, 0), VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(divider, Border.BackgroundProperty, ArcDesignTokenKind.Border);
        return divider;
    }

    private static Control Output()
    {
        var editor = new RichTextEditor
        {
            Html = "<h2>From HTML</h2><p>Pasted <b>bold</b>, <span style=\"font-style:italic\">styled spans</span> and <a href=\"https://uiarc.dev\">links</a> are cleaned up.</p><ul><li>Lists nest<ul><li>like this</li></ul></li></ul>"
        };
        var markdown = GalleryUi.Mono(editor.GetMarkdown());
        markdown.TextWrapping = TextWrapping.Wrap;
        var html = GalleryUi.Mono(editor.GetHtml());
        html.TextWrapping = TextWrapping.Wrap;
        editor.ContentChanged += (_, e) =>
        {
            markdown.Text = e.Value.Markdown;
            html.Text = e.Value.Html;
        };
        return GalleryUi.Column(16,
            Paper(editor),
            GalleryUi.Text("Markdown", ArcDesignTokenKind.TextMuted, 12),
            markdown,
            GalleryUi.Text("HTML", ArcDesignTokenKind.TextMuted, 12),
            html);
    }
}
