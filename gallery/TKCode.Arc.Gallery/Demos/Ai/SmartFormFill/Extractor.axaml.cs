using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>An app-supplied extractor and save: what a real model call and a real save would plug into.</summary>
public partial class SmartFormFillExtractorDemo : UserControl
{
    public SmartFormFillExtractorDemo()
    {
        InitializeComponent();
        Card.Extract = ExtractAsync;
        Card.Submit = SaveAsync;
        Card.Filled += (_, e) => Log.Text = $"Read {e.FilledCount} fields; {e.ReviewCount} to review.";
        Card.Undone += (_, _) => Log.Text = "Undo restored the values from before the fill.";
        Card.Submitted += (_, e) => Log.Text = "Saved: " + string.Join(", ", e.Values.Where(v => v.Value.Length > 0).Select(v => $"{v.Key} = {v.Value}"));
    }

    /// <summary>
    /// Stands in for a model call with structured output: each field gets a value, a confidence from 0 to 1, the exact source
    /// substring (it lights up in the pasted text) and a note when the model is unsure.
    /// </summary>
    private static async Task<SmartFormFillResult> ExtractAsync(string text, CancellationToken token)
    {
        await Task.Delay(TimeSpan.FromSeconds(1.2), token);
        var result = new SmartFormFillResult();
        var email = text.Split([' ', '\n', ',', '/'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(w => w.Contains('@'));
        if (email is not null)
        {
            result["email"] = new SmartFormFillValue(email.Trim('.', ';'), 0.97, email.Trim('.', ';'));
        }
        var firstLine = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (firstLine is { Length: > 0 and < 40 } && !firstLine.Contains('@'))
        {
            result["name"] = new SmartFormFillValue(firstLine, 0.7, firstLine, "Taken from the first line.");
        }
        return result;
    }

    private static Task SaveAsync(IReadOnlyDictionary<string, string> values) => Task.Delay(TimeSpan.FromSeconds(0.8));
}
