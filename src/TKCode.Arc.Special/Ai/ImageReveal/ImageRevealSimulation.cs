using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>One picture of an <see cref="ImageRevealSimulation"/>: the image a simulated run reveals and its name.</summary>
public sealed class ImageRevealSimulationPicture
{
    /// <summary>The image the run reveals (any <see cref="IImage"/>; it is cover-cropped to the canvas). Without one the run keeps the calm queued look.</summary>
    public IImage? Image { get; set; }

    /// <summary>The image's name once it is done (the canvas's accessible name); "Generated image" when null.</summary>
    public string? AltText { get; set; }
}

/// <summary>
/// A simulated image job for an <see cref="ImageReveal"/> that has no <see cref="ImageReveal.Status"/>,
/// <see cref="ImageReveal.Generator"/> or <see cref="ImageReveal.Source"/>, for demos and prototypes. Each run waits in the
/// queue for <see cref="ImageReveal.QueueDuration"/> (plus <see cref="ImageReveal.StartDelay"/>), generates over
/// <see cref="ImageReveal.Duration"/> and reveals one of <see cref="Pictures"/>; with <see cref="ImageReveal.IsLooping"/> it
/// holds for <see cref="ImageReveal.HoldDuration"/>, blurs back out and runs again with the next picture. The first run
/// shows the picture <see cref="ImageReveal.Seed"/> picks (seed 1 the first, seed 2 the second, wrapping), so tiles in a
/// grid differ. Announcements say "simulated". Nothing is generated or downloaded. Create one per control: it keeps its
/// place in the pictures.
/// </summary>
public sealed class ImageRevealSimulation
{
    private IReadOnlyList<ImageRevealSimulationPicture> _pictures = [];
    private bool _started;

    /// <summary>The pictures the runs reveal, in turn.</summary>
    public IReadOnlyList<ImageRevealSimulationPicture> Pictures
    {
        get => _pictures;
        set
        {
            _pictures = value ?? [];
            Index = 0;
            _started = false;
        }
    }

    /// <summary>The index in <see cref="Pictures"/> of the picture the current run reveals.</summary>
    public int Index { get; private set; }

    /// <summary>The picture the current run reveals, or null without pictures.</summary>
    public ImageRevealSimulationPicture? Current => _pictures.Count > 0 ? _pictures[Wrap(Index)] : null;

    /// <summary>Picks the first run's picture from <paramref name="seed"/> without starting a run (a control that waits for <see cref="ImageReveal.Restart"/>).</summary>
    internal void Prepare(int seed)
    {
        Index = Wrap(seed - 1);
        _started = false;
    }

    /// <summary>A run starts: the first one shows the picture <paramref name="seed"/> picks, each later one the next.</summary>
    internal void MoveNext(int seed)
    {
        Index = _started ? Wrap(Index + 1) : Wrap(seed - 1);
        _started = true;
    }

    private int Wrap(int index)
    {
        var count = _pictures.Count;
        return count == 0 ? 0 : ((index % count) + count) % count;
    }
}
