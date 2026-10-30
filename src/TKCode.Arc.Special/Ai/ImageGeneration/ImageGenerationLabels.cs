namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="ImageGeneration"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; text with numbers in it comes from format functions. Image numbers start at 1.
/// </summary>
public sealed class ImageGenerationLabels
{
    /// <summary>The English defaults.</summary>
    public static ImageGenerationLabels Default { get; } = new();

    /// <summary>The prompt field's placeholder.</summary>
    public string Placeholder { get; init; } = "Describe the image you want";

    /// <summary>The prompt field's accessible name.</summary>
    public string Prompt { get; init; } = "Prompt";

    /// <summary>The accessible name of the model chips.</summary>
    public string Models { get; init; } = "Model";

    /// <summary>The accessible name of the aspect chips.</summary>
    public string AspectRatios { get; init; } = "Aspect ratio";

    /// <summary>The Generate button.</summary>
    public string Generate { get; init; } = "Generate";

    /// <summary>The Generate button while images are being made.</summary>
    public string Stop { get; init; } = "Stop";

    /// <summary>The Upscale button.</summary>
    public string Upscale { get; init; } = "Upscale";

    /// <summary>The Upscale button while the image is upscaled.</summary>
    public string Upscaling { get; init; } = "Upscaling";

    /// <summary>The Upscale button once the image is upscaled.</summary>
    public string Upscaled { get; init; } = "Upscaled";

    /// <summary>The Vary button.</summary>
    public string Vary { get; init; } = "Vary";

    /// <summary>The Download button.</summary>
    public string Download { get; init; } = "Download";

    /// <summary>The Download button for a moment after a download.</summary>
    public string Saved { get; init; } = "Saved";

    /// <summary>The button that closes the expanded image.</summary>
    public string Close { get; init; } = "Back to all images";

    /// <summary>The line above the suggestions in the empty grid.</summary>
    public string Suggestions { get; init; } = "Try a prompt";

    /// <summary>A tile that failed.</summary>
    public string Failed { get; init; } = "Failed";

    /// <summary>A tile that was stopped.</summary>
    public string Stopped { get; init; } = "Stopped";

    /// <summary>The hint under a failed or stopped tile (pressing it tries again).</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>Added to announcements while the built-in simulation makes the images.</summary>
    public string Simulated { get; init; } = "Simulated results";

    /// <summary>A finished tile's name, from its number and the image's description.</summary>
    public Func<int, string?, string> Image { get; init; } =
        (number, description) => string.IsNullOrEmpty(description) ? $"Image {number}" : $"Image {number}, {description}";

    /// <summary>A running tile's name, from its number.</summary>
    public Func<int, string> ImageRunning { get; init; } = number => $"Image {number}, generating";

    /// <summary>A failed tile's name, from its number.</summary>
    public Func<int, string> ImageFailed { get; init; } = number => $"Image {number} failed, press to retry";

    /// <summary>A stopped tile's name, from its number.</summary>
    public Func<int, string> ImageStopped { get; init; } = number => $"Image {number} stopped, press to retry";

    /// <summary>The footer before the first generation.</summary>
    public string Empty { get; init; } = "Write a prompt or pick one to start";

    /// <summary>The footer and the announcement while a batch runs, from the number of images and the model's name.</summary>
    public Func<int, string, string> Generating { get; init; } = (count, model) => $"Generating {count} images with {model}";

    /// <summary>The footer and the announcement of a finished batch, from the images that finished and the batch's size.</summary>
    public Func<int, int, string> Ready { get; init; } =
        (done, total) => done == total ? $"{done} images ready, pick one" : $"{done} of {total} images ready";

    /// <summary>The footer of a stopped batch, from the images that finished and the batch's size.</summary>
    public Func<int, int, string> StoppedStatus { get; init; } = (done, total) => $"Stopped, {done} of {total} finished";

    /// <summary>The footer with an image picked, from its number, width and height.</summary>
    public Func<int, int, int, string> Selected { get; init; } = (number, width, height) => $"Image {number} · {width} × {height}";

    /// <summary>The footer and the announcement while an image is upscaled, from its number.</summary>
    public Func<int, string> UpscalingStatus { get; init; } = number => $"Upscaling image {number}";

    /// <summary>The footer and the announcement once an image is upscaled, from its number, width and height.</summary>
    public Func<int, int, int, string> UpscaledStatus { get; init; } =
        (number, width, height) => $"Image {number} upscaled to {width} × {height}";

    /// <summary>The announcement of a tile that failed, from its number.</summary>
    public Func<int, string> FailedStatus { get; init; } = number => $"Image {number} failed";

    /// <summary>The announcement of an upscale that failed, from the image's number.</summary>
    public Func<int, string> UpscaleFailed { get; init; } = number => $"Upscaling image {number} failed";

    /// <summary>The announcement of a download, from the image's number.</summary>
    public Func<int, string> Downloaded { get; init; } = number => $"Downloaded image {number}";
}
