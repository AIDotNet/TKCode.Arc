using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Where an <see cref="ImageReveal"/> job is.</summary>
public enum ImageRevealStatus
{
    /// <summary>Waiting to start: the calm surface tone with one broad, soft shimmer and a Queued chip.</summary>
    Queued,

    /// <summary>The model is working: the soft preview sharpens top to bottom behind a soft-edged band.</summary>
    Generating,

    /// <summary>The image is finished and crisp; the pill fades out after one soft sheen.</summary>
    Done,

    /// <summary>The job failed: the sharp layers blur back out and an alert, the message and Retry show.</summary>
    Error
}

/// <summary>
/// One progress report from an <see cref="ImageRevealGenerator"/>: how far the job is (0 to 1) and, when the service has
/// one, the image so far (the soft preview is sampled from it, so pass it as soon as it exists).
/// </summary>
/// <param name="Progress">How far the job is, from 0 to 1.</param>
/// <param name="Image">A preview or the final image, when there is one.</param>
public readonly record struct ImageRevealProgress(double Progress, IImage? Image = null);

/// <summary>
/// Runs one image job for <see cref="ImageReveal.Generator"/>: report progress (and the image as soon as you have it)
/// through <paramref name="progress"/> and return the final image. Throwing shows the error state; cancellation comes
/// through <paramref name="cancellationToken"/> when the run is restarted, the generator is replaced or the app takes over
/// <see cref="ImageReveal.Status"/>.
/// </summary>
/// <param name="progress">Takes progress reports; they are smoothed into one continuous reveal.</param>
/// <param name="cancellationToken">Cancelled when this run is no longer wanted.</param>
/// <remarks>Progress reports may come from any thread; they are handed to the control on the UI thread.</remarks>
/// <returns>The final image.</returns>
public delegate Task<IImage?> ImageRevealGenerator(IProgress<ImageRevealProgress> progress, CancellationToken cancellationToken);

/// <summary>Arguments of <see cref="ImageReveal.StatusChanged"/>.</summary>
public sealed class ImageRevealStatusChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ImageRevealStatusChangedEventArgs(RoutedEvent routedEvent, ImageRevealStatus oldStatus, ImageRevealStatus newStatus)
        : base(routedEvent)
    {
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }

    /// <summary>The status shown before.</summary>
    public ImageRevealStatus OldStatus { get; }

    /// <summary>The status shown now.</summary>
    public ImageRevealStatus NewStatus { get; }
}
