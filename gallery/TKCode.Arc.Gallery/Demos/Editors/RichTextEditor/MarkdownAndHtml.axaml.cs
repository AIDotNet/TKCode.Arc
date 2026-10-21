using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RichTextEditorMarkdownAndHtmlDemo : UserControl
{
    public RichTextEditorMarkdownAndHtmlDemo()
    {
        InitializeComponent();
        Editor.Html = """<h2>From HTML</h2><p>Pasted <b>bold</b>, <span style="font-style:italic">styled spans</span> and <a href="https://uiarc.dev">links</a> are cleaned up.</p><ul><li>Lists nest<ul><li>like this</li></ul></li></ul>""";
        MarkdownText.Text = Editor.GetMarkdown();
        HtmlText.Text = Editor.GetHtml();
    }

    private void OnContentChanged(object? sender, RichTextContentChangedEventArgs e)
    {
        MarkdownText.Text = e.Value.Markdown;
        HtmlText.Text = e.Value.Html;
    }
}
