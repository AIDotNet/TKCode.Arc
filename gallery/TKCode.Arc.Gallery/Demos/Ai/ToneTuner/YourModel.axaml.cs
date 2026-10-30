using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A draft of your own with an async rewrite function (canned versions stand in for a model).</summary>
public partial class ToneTunerYourModelDemo : UserControl
{
    private const string Draft = """
        Hi Jordan,

        Thanks for the report. The export got stuck because one of the 12 sheets has a formula that loops. I removed it and restarted the export, so the file should reach your inbox within 10 minutes.

        Best,
        Priya
        """;

    public ToneTunerYourModelDemo()
    {
        InitializeComponent();
        Tuner.Text = Draft;
        Tuner.Rewrite = RewriteAsync;
        Tuner.Rewritten += (_, e) => Log.Text = $"Rewritten ({e.Value.Formality}, {e.Value.Detail}, {e.Value.Voice}): {e.Text.Split('\n')[2]}";
    }

    /// <summary>
    /// Stands in for your model: in an app, send <c>request.Instructions</c> and <c>request.Text</c> to it and return its
    /// text. The token is cancelled when a newer change supersedes this request.
    /// </summary>
    private static async Task<string> RewriteAsync(ToneRewriteRequest request, CancellationToken token)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(700), token);
        var greeting = request.Formality switch
        {
            2 => "Dear Jordan,",
            0 => "Hey Jordan,",
            _ => "Hi Jordan,",
        };
        var opener = request.Voice switch
        {
            ToneVoice.Friendly => "Thanks so much for the report!",
            ToneVoice.Confident => "Found it.",
            ToneVoice.Empathetic => "Sorry the export left you waiting, and thanks for the report.",
            _ => request.Formality == 2 ? "Thank you for the report." : "Thanks for the report.",
        };
        var cause = request.Detail == 0
            ? "One of the 12 sheets had a looping formula."
            : request.Formality == 2
                ? "The export stopped because one of the 12 sheets contains a formula that refers to itself."
                : "The export got stuck because one of the 12 sheets has a formula that loops.";
        var fix = request.Formality == 0
            ? "I pulled it out and restarted the export, so the file should land in your inbox within 10 minutes."
            : "I removed it and restarted the export, so the file should reach your inbox within 10 minutes.";
        var more = request.Detail == 2 ? " Next time, the export will name the sheet that blocks it." : string.Empty;
        var signOff = request.Formality == 2 ? "Kind regards,\nPriya" : "Best,\nPriya";
        return $"{greeting}\n\n{opener} {cause} {fix}{more}\n\n{signOff}";
    }
}
