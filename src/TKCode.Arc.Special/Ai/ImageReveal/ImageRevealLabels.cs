namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="ImageReveal"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; the counts are format functions. <see cref="ImageReveal.QueueLabel"/> and <see cref="ImageReveal.ErrorMessage"/>
/// override the queued chip and the error message for one job.
/// </summary>
public sealed class ImageRevealLabels
{
    /// <summary>The English defaults.</summary>
    public static ImageRevealLabels Default { get; } = new();

    /// <summary>The chip while queued.</summary>
    public string Queued { get; init; } = "Queued";

    /// <summary>The pill's label while generating (sizes Medium and Large).</summary>
    public string Generating { get; init; } = "Generating";

    /// <summary>The pill's label once the image has resolved, beside the check.</summary>
    public string Done { get; init; } = "Done";

    /// <summary>The message of the error state when <see cref="ImageReveal.ErrorMessage"/> is not set.</summary>
    public string Failed { get; init; } = "Generation failed";

    /// <summary>The Retry button in the error state.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The image's name once done when <see cref="ImageReveal.AltText"/> is not set.</summary>
    public string GeneratedImage { get; init; } = "Generated image";

    /// <summary>The canvas name and the announcement while generating.</summary>
    public string GeneratingImage { get; init; } = "Generating image";

    /// <summary>The announcement once the image has resolved.</summary>
    public string ImageReady { get; init; } = "Image ready";

    /// <summary>The canvas's name while it has nothing to show (no status, generator, image or simulation).</summary>
    public string NoImage { get; init; } = "No image";

    /// <summary>Appended to announcements while the control plays a <see cref="ImageReveal.Simulation"/>.</summary>
    public string Simulated { get; init; } = "simulated";

    /// <summary>The step count in the pill, from the step and the total: "18 / 30".</summary>
    public Func<int, int, string> Count { get; init; } = (step, total) => step + " / " + total;

    /// <summary>The progress bar's spoken value, from the step and the total: "Step 18 of 30".</summary>
    public Func<int, int, string> StepOf { get; init; } = (step, total) => "Step " + step + " of " + total;
}
