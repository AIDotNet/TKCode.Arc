using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TokenTextEditorPreviewDemo : UserControl
{
    private static readonly (string Kind, string Id, string Label)[] Entries =
    [
        ("command", "recap", "recap"), ("command", "rewrite", "rewrite"), ("command", "outline", "outline"),
        ("mention", "export-worker", "export-worker.ts"), ("mention", "usage-oct", "usage-oct.csv"), ("mention", "style-guide", "style-guide.pdf")
    ];

    public TokenTextEditorPreviewDemo()
    {
        InitializeComponent();

        // The editor reports what is being typed; the list is yours. Pressing a row replaces the typed text with a token.
        Editor.TriggerChanged += (_, e) =>
        {
            var state = e.State;
            if (state is null)
            {
                Suggestions.IsVisible = false;
                return;
            }

            var matches = Entries.Where(x => x.Kind == state.Kind && x.Label.StartsWith(state.Query, StringComparison.OrdinalIgnoreCase)).ToList();
            Suggestions.ItemsSource = matches.Select(x => x.Label).ToList();
            Suggestions.IsVisible = matches.Count > 0;
        };
        Suggestions.SelectionChanged += (_, _) =>
        {
            if (Suggestions.SelectedItem is not string label)
            {
                return;
            }

            var entry = Entries.First(x => x.Label == label);
            Editor.InsertToken(entry.Kind, entry.Id, entry.Label);
            Suggestions.SelectedItem = null;
            Suggestions.IsVisible = false;
            Editor.Focus();
        };
        Editor.TextChanged += (_, e) =>
            Summary.Text = e.Tokens.Count == 0 ? "No tokens yet." : "Tokens: " + string.Join(", ", e.Tokens.Select(t => $"{t.Kind} {t.Label}"));
    }
}
