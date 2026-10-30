using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The invented exchange the response feedback demos show: a question about a slow Postgres query and an answer with a
/// paragraph, a short list and inline code. The model is a real one from <see cref="AiModelCatalog"/>.
/// </summary>
internal static class ResponseFeedbackDemoData
{
    /// <summary>The model credited with the reply (Anthropic's Claude Sonnet 5.5; checked on <see cref="AiModelCatalog.CheckedOn"/>).</summary>
    public static readonly string ModelName = AiModelCatalog.ClaudeSonnet55.Name;

    /// <summary>The person's question.</summary>
    public const string Prompt = "Why is my Postgres query slow when I filter on created_at?";

    private static readonly (string Text, bool Code)[] s_intro =
    [
        ("A filter on ", false), ("created_at", true),
        (" is slow when Postgres has to read every row to find the matches. Run ", false), ("EXPLAIN ANALYZE", true),
        (" on the query: a ", false), ("Seq Scan", true), (" on the table means there is no index it can use.", false),
    ];

    private static readonly (string Text, bool Code)[][] s_points =
    [
        [("Add an index: ", false), ("CREATE INDEX ON orders (created_at);", true)],
        [("Compare the column with a range instead of wrapping it in ", false), ("date()", true), (", so the index applies.", false)],
        [("Run ", false), ("ANALYZE", true), (" after large imports so the planner has fresh statistics.", false)],
    ];

    private const string Outro = "With the index in place the same query usually drops from seconds to a few milliseconds.";

    /// <summary>The answer as plain text, for the copy action.</summary>
    public static readonly string CopyText = PlainText();

    /// <summary>Builds the answer (a new control each call): a paragraph, a bulleted list and a closing paragraph, with inline code on a muted fill.</summary>
    public static Control Reply()
    {
        var body = new StackPanel { Spacing = 10 };
        body.Children.Add(Paragraph(s_intro));
        var list = new StackPanel { Spacing = 4 };
        foreach (var point in s_points)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("18,*") };
            var bullet = new TextBlock { Text = "•", HorizontalAlignment = HorizontalAlignment.Center };
            ArcTokens.Bind(bullet, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            row.Children.Add(bullet);
            var text = Paragraph(point);
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            list.Children.Add(row);
        }
        body.Children.Add(list);
        body.Children.Add(Paragraph([(Outro, false)]));
        return body;
    }

    private static string PlainText()
    {
        var lines = new List<string> { string.Concat(s_intro.Select(part => part.Text)), string.Empty };
        lines.AddRange(s_points.Select(point => "- " + string.Concat(point.Select(part => part.Text))));
        lines.Add(string.Empty);
        lines.Add(Outro);
        return string.Join("\n", lines);
    }

    private static SelectableTextBlock Paragraph(IEnumerable<(string Text, bool Code)> parts)
    {
        var block = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap };
        block.Inlines ??= [];
        foreach (var (text, code) in parts)
        {
            var run = new Run(text);
            if (code)
            {
                ArcTokens.Bind(run, TextElement.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
                ArcTokens.Bind(run, TextElement.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
                run.FontSize = 13;
            }
            block.Inlines.Add(run);
        }
        return block;
    }
}
