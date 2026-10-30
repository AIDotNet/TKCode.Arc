using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>A model offered as a chip on an <see cref="ImageGeneration"/> card.</summary>
public sealed class ImageGenerationModel
{
    /// <summary>The id passed to your generator in <see cref="ImageGenerationRequest.Model"/> and kept in <see cref="ImageGeneration.SelectedModelId"/>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The name the chosen chip shows; collapsed chips keep it as their accessible name and tooltip.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The 16px mark (a 24 × 24 stroked glyph). Null draws the first letter of the name. Pass your own provider's mark only
    /// if you are allowed to show it.
    /// </summary>
    public Geometry? Mark { get; set; }
}

/// <summary>An output shape offered as a chip on an <see cref="ImageGeneration"/> card.</summary>
public sealed class ImageGenerationAspect
{
    /// <summary>The id kept in <see cref="ImageGeneration.SelectedAspectId"/>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The chip's text, such as "1:1".</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The pixel width a generation returns; with <see cref="Height"/> it sets the grid's shape.</summary>
    public int Width { get; set; } = 1024;

    /// <summary>The pixel height a generation returns.</summary>
    public int Height { get; set; } = 1024;
}

/// <summary>One generated image.</summary>
public sealed class ImageGenerationImage
{
    /// <summary>Your id for the image (passed back in <see cref="ImageGenerationRequest.VariationOf"/>).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The image. The tile draws it cropped to the batch's shape.</summary>
    public Bitmap? Source { get; set; }

    /// <summary>What the image shows, read by screen readers; usually the prompt.</summary>
    public string? Description { get; set; }

    /// <summary>The image's real width in pixels, shown in the footer and used in the download's file name.</summary>
    public int Width { get; set; }

    /// <summary>The image's real height in pixels.</summary>
    public int Height { get; set; }
}

/// <summary>What <see cref="ImageGeneration.Generator"/> is asked to make: one image of a batch.</summary>
public sealed class ImageGenerationRequest
{
    /// <summary>The prompt.</summary>
    public string Prompt { get; init; } = string.Empty;

    /// <summary>The chosen model.</summary>
    public required ImageGenerationModel Model { get; init; }

    /// <summary>The chosen shape; its width and height are the pixels to return.</summary>
    public required ImageGenerationAspect Aspect { get; init; }

    /// <summary>The tile, 0 to 3.</summary>
    public int Index { get; init; }

    /// <summary>The image Vary started from (use it as a seed or reference), or null for Generate.</summary>
    public ImageGenerationImage? VariationOf { get; init; }
}

/// <summary>Handed to a generator or upscaler: the cancellation of the run and a way to report progress.</summary>
public sealed class ImageGenerationContext
{
    private readonly Action<double, Bitmap?> _report;

    internal ImageGenerationContext(CancellationToken cancellationToken, Action<double, Bitmap?> report)
    {
        CancellationToken = cancellationToken;
        _report = report;
    }

    /// <summary>Cancelled when the person presses Stop, closes the expanded image or starts a new batch.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Reports progress from 0 to 1, optionally with a preview the pixel mosaic resolves from (without one a quiet placeholder
    /// grid shimmers until the image arrives and fades in). Without reports the ring estimates. Safe from any thread.
    /// </summary>
    public void ReportProgress(double progress, Bitmap? preview = null)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            _report(progress, preview);
        }
        else
        {
            Dispatcher.UIThread.Post(() => _report(progress, preview));
        }
    }
}

/// <summary>Arguments of <see cref="ImageGeneration.SelectionChanged"/>.</summary>
public sealed class ImageGenerationSelectionChangedEventArgs : RoutedEventArgs
{
    internal ImageGenerationSelectionChangedEventArgs(RoutedEvent routedEvent, ImageGenerationImage? image, int index)
        : base(routedEvent)
    {
        Image = image;
        Index = index;
    }

    /// <summary>The picked image (the upscaled one once an upscale finishes), or null when the selection was cleared.</summary>
    public ImageGenerationImage? Image { get; }

    /// <summary>The picked tile, 0 to 3, or −1.</summary>
    public int Index { get; }
}

/// <summary>Arguments of <see cref="ImageGeneration.DownloadRequested"/>.</summary>
public sealed class ImageGenerationDownloadEventArgs : RoutedEventArgs
{
    internal ImageGenerationDownloadEventArgs(RoutedEvent routedEvent, ImageGenerationImage image, int index, string fileName)
        : base(routedEvent)
    {
        Image = image;
        Index = index;
        FileName = fileName;
    }

    /// <summary>The chosen image.</summary>
    public ImageGenerationImage Image { get; }

    /// <summary>Its tile, 0 to 3.</summary>
    public int Index { get; }

    /// <summary>A file name built from the prompt, the image's number and its size, such as "mountain-lake-at-golden-hour-2-1024x1024.png".</summary>
    public string FileName { get; }
}

/// <summary>Where a tile of an <see cref="ImageGeneration"/> card is.</summary>
internal enum ImageGenerationTileState
{
    Idle,
    Running,
    Done,
    Failed,
    Stopped
}
