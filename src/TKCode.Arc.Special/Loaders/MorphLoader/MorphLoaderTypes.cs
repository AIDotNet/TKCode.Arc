namespace TKCode.Arc;

/// <summary>The shape a <see cref="MorphLoader"/> loops through while it is loading.</summary>
public enum MorphLoaderVariant
{
    /// <summary>Three dots that hop in a wave.</summary>
    Dots,

    /// <summary>Four bars that grow and shrink one after another.</summary>
    Bars,

    /// <summary>Four arcs of a ring that turns while the arcs breathe.</summary>
    Ring,

    /// <summary>A square that tumbles a quarter turn at a time while its corners open.</summary>
    Square
}

/// <summary>What a <see cref="MorphLoader"/> shows: the loading loop, a check or a cross.</summary>
public enum MorphLoaderStatus
{
    /// <summary>The work is running: the strokes loop in the <see cref="MorphLoader.Variant"/> shape.</summary>
    Loading,

    /// <summary>The work finished: the strokes fold into a check.</summary>
    Success,

    /// <summary>The work failed: the strokes fold into a cross.</summary>
    Error
}

/// <summary>
/// The words a <see cref="MorphLoader"/> gives assistive technology, one per status, in English by default. Replace the
/// instance (or set its properties) to translate or reword them, for example "Saving", "Saved" and "Couldn't save".
/// </summary>
public sealed class MorphLoaderLabels
{
    /// <summary>The accessible name while loading.</summary>
    public string Loading { get; set; } = "Loading";

    /// <summary>The accessible name once the work succeeded.</summary>
    public string Success { get; set; } = "Done";

    /// <summary>The accessible name once the work failed.</summary>
    public string Error { get; set; } = "Failed";

    /// <summary>The accessible name for <paramref name="status"/>.</summary>
    public string For(MorphLoaderStatus status) => status switch
    {
        MorphLoaderStatus.Success => Success,
        MorphLoaderStatus.Error => Error,
        _ => Loading
    };
}

/// <summary>Arguments of <see cref="MorphLoader.Settled"/>.</summary>
public sealed class MorphLoaderSettledEventArgs : EventArgs
{
    /// <summary>Creates the arguments for a loader that came to rest showing <paramref name="status"/>.</summary>
    public MorphLoaderSettledEventArgs(MorphLoaderStatus status)
    {
        Status = status;
    }

    /// <summary>The result the loader now shows at rest (Success or Error).</summary>
    public MorphLoaderStatus Status { get; }
}
