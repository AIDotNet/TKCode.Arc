using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A rewrite function that fails every other call, to show Couldn't rewrite and Retry.</summary>
public partial class ToneTunerFailureDemo : UserControl
{
    private const string Draft = """
        Hi Sofia,

        Thank you for letting us know about the duplicate charge on INV-20481. We have refunded the duplicate $249.00 to your card ending in 4417.

        Best,
        Maya
        """;

    private int _calls;

    public ToneTunerFailureDemo()
    {
        InitializeComponent();
        Tuner.Text = Draft;
        Tuner.Rewrite = RewriteAsync;
        // Starts formal, so the first request runs (and fails) as soon as the card is shown.
        Tuner.Value = new ToneSettings(2, 1, ToneVoice.Neutral);
    }

    private async Task<string> RewriteAsync(ToneRewriteRequest request, CancellationToken token)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(600), token);
        if (_calls++ % 2 == 0)
        {
            throw new InvalidOperationException("The model did not respond.");
        }
        var greeting = request.Formality == 2 ? "Dear Ms. Ramirez," : "Hi Sofia,";
        var signOff = request.Formality == 2 ? "Kind regards,\nMaya Chen" : "Best,\nMaya";
        return $"{greeting}\n\nThank you for letting us know about the duplicate charge on INV-20481. We have issued a full refund of the duplicate $249.00 to your card ending in 4417.\n\n{signOff}";
    }
}
