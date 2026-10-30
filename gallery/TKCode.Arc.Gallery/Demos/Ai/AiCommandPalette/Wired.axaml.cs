using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A palette wired to its own items and a responder that streams a canned answer with sources and actions.</summary>
public partial class AiCommandPaletteWiredDemo : UserControl
{
    private const string Answer =
        "The last deploy failed at the build step because the environment variable DATABASE_URL is missing in Preview [1]. "
        + "It was removed yesterday when the staging database moved [2].\n\nAdd it back to the Preview environment and redeploy.";

    public AiCommandPaletteWiredDemo()
    {
        InitializeComponent();
        Palette.SuggestedQuestions = ["Why did the last deploy fail?", "What is using the most bandwidth?"];
        Palette.Responder = RespondAsync;
        Palette.ItemSelected += (_, e) => Log.Text = "Opened " + e.Item.Label + " (" + e.Item.Id + ").";
        Palette.ActionCompleted += (_, e) => Log.Text = "Action finished: " + e.Result + ".";
        Palette.SourceSelected += (_, e) => Log.Text = "Source chosen: " + e.Item.Title + ".";
    }

    private static async Task<AiCommandPaletteAnswer?> RespondAsync(AiCommandPaletteAskContext context)
    {
        var token = context.CancellationToken;
        await Task.Delay(600, token);
        // Sources first, so the chips land before the text that cites them.
        context.SetSources(
        [
            new AiCommandPaletteSource("build", "Build log #482", "Build log of deployment #482: DATABASE_URL is not defined.", ArcIcons.Terminal),
            new AiCommandPaletteSource("audit", "Audit log", "Audit log: DATABASE_URL removed from Preview yesterday.", ArcIcons.ListChecks),
        ]);
        foreach (var piece in Pieces(Answer))
        {
            await Task.Delay(40, token);
            context.Append(piece);
        }
        return new AiCommandPaletteAnswer
        {
            Actions =
            [
                new AiCommandPaletteAction("env", "Add DATABASE_URL to Preview", "Settings", ArcIcons.Key, async t =>
                {
                    await Task.Delay(900, t);
                    return "Added to Preview";
                }) { DoneMeta = "Simulated, nothing was sent" },
                new AiCommandPaletteAction("redeploy", "Redeploy", "Preview", ArcIcons.RotateCw, async t =>
                {
                    await Task.Delay(1400, t);
                    return "Deployment #483 queued";
                }) { DoneMeta = "Simulated, nothing was sent" },
            ],
        };
    }

    private static IEnumerable<string> Pieces(string text)
    {
        for (var i = 0; i < text.Length; i += 12)
        {
            yield return text.Substring(i, Math.Min(12, text.Length - i));
        }
    }
}
