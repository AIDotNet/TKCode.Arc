using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// An import gate: <c>Analyze</c> reports what the editor cannot store, and <c>TrySetMarkdown</c> refuses a lossy
/// source, so the host can keep the original file or warn before it writes anything back.
/// </summary>
public partial class RichTextEditorImportCheckDemo : UserControl
{
    private const string Lossy = """
        #### A fourth level heading

        ```csharp
        var count = 3;
        ```

        | Step | Call |
        |------|------|
        | Start | `Append` |
        """;

    public RichTextEditorImportCheckDemo()
    {
        InitializeComponent();
        Source.Text = Lossy;
        Editor.Markdown = "An editor keeps its content until an import succeeds.";
        Update(Lossy);
    }

    private void OnSourceChanged(object? sender, TextChangedEventArgs e) => Update(Source.Text);

    private void OnImportClick(object? sender, RoutedEventArgs e)
    {
        if (Editor.TrySetMarkdown(Source.Text, out var report))
        {
            Status.Text = report.IsEmpty
                ? "Imported as written."
                : $"Imported; {report.Issues.Count} construct(s) written back in canonical form.";
            return;
        }
        Status.Text = $"Import refused; the editor kept its content. {Describe(report)}";
    }

    private void OnKeepClick(object? sender, RoutedEventArgs e)
        => Status.Text = "The source is kept as it is; nothing was imported.";

    private void Update(string? markdown)
    {
        var report = RichTextEditorMarkdown.Analyze(markdown);
        Status.Text = report.IsEmpty ? "No issues: this source imports as written." : Describe(report);
    }

    private static string Describe(RichTextImportReport report)
    {
        var losses = report.Issues.Count(i => i.Severity == RichTextImportSeverity.Lossy);
        var first = report.Issues[0];
        var summary = losses > 0 ? $"{losses} loss(es) and " : string.Empty;
        return $"{summary}{report.Issues.Count - losses} normalization(s). First: line {first.Line}, {first.Reason}";
    }
}
