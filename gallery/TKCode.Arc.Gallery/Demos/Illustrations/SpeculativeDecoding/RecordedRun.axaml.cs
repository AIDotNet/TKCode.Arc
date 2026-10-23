using System.Globalization;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SpeculativeDecodingRecordedRunDemo : UserControl
{
    public SpeculativeDecodingRecordedRunDemo()
    {
        InitializeComponent();
        // Each round: what the draft proposed, how many the target accepted from the start, and the target's own token.
        Decoder.RoundsSource =
        [
            new DecodeRound(["const", " total", " =", " items"], 4, "."),
            new DecodeRound(["reduce", "((", "acc", ","], 2, "sum"),
            new DecodeRound([",", " row", ")", " =>"], 1, " item"),
            new DecodeRound([")", " =>", " sum", " +"], 4, " item"),
            new DecodeRound([".", "price", " *", " item"], 4, "."),
            new DecodeRound(["qty", ",", " 0"], 3, ");")
        ];
    }

    private void OnCompleted(object? sender, SpeculativeDecodingCompletedEventArgs e)
    {
        var stats = e.Stats;
        Log.Text = string.Format(CultureInfo.InvariantCulture,
            "Completed: {0} tokens in {1} passes, {2} of {3} drafts accepted, {4:0.0}× the target alone.",
            stats.Tokens, stats.Passes, stats.Accepted, stats.Drafted, stats.Speedup);
    }
}
