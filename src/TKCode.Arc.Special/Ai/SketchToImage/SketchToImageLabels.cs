namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="SketchToImage"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; the percent, the canvas name and the stage announcement are format functions.
/// </summary>
public sealed class SketchToImageLabels
{
    /// <summary>The English defaults.</summary>
    public static SketchToImageLabels Default { get; } = new();

    /// <summary>The stage label before anything has started.</summary>
    public string Idle { get; init; } = "Waiting";

    /// <summary>The stage label while pencil strokes sketch the scene.</summary>
    public string Sketching { get; init; } = "Sketching";

    /// <summary>The stage label while the brush blocks in color.</summary>
    public string Blocking { get; init; } = "Blocking in color";

    /// <summary>The stage label while detail, grain and light refine the picture.</summary>
    public string Refining { get; init; } = "Refining details";

    /// <summary>The stage label once the picture is finished.</summary>
    public string Done { get; init; } = "Done";

    /// <summary>The Replay button's text (hidden at the small size and below 400px, where only its icon shows).</summary>
    public string Replay { get; init; } = "Replay";

    /// <summary>The Replay button's accessible name.</summary>
    public string ReplayName { get; init; } = "Replay the preview";

    /// <summary>The progress line's accessible name.</summary>
    public string Progress { get; init; } = "Generation progress";

    /// <summary>The percent beside the progress line, from 0 to 100.</summary>
    public Func<int, string> Percent { get; init; } = percent => percent + "%";

    /// <summary>The canvas's accessible name while it paints, from the stage label and the scene's description ("Sketching: …").</summary>
    public Func<string, string, string> CanvasName { get; init; } = (stage, alt) => stage + ": " + alt;

    /// <summary>What the status region says when a controlled stage begins, from its label.</summary>
    public Func<string, string> StageAnnouncement { get; init; } = stage => stage + ". Simulated preview.";

    /// <summary>What the status region says once when the card loops on its own.</summary>
    public string LoopingAnnouncement { get; init; } = "Looping simulated preview of an image being generated.";

    /// <summary>The label of <paramref name="stage"/>.</summary>
    public string Of(SketchToImageStatus stage) => stage switch
    {
        SketchToImageStatus.Sketching => Sketching,
        SketchToImageStatus.Blocking => Blocking,
        SketchToImageStatus.Refining => Refining,
        SketchToImageStatus.Done => Done,
        _ => Idle,
    };
}
