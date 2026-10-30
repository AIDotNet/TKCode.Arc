using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>A stage of the painting a <see cref="SketchToImage"/> shows.</summary>
public enum SketchToImageStatus
{
    /// <summary>Blank paper: nothing has started (a queued request).</summary>
    Idle,

    /// <summary>Pencil strokes sketch the scene, a pen tip travelling between them.</summary>
    Sketching,

    /// <summary>A round brush blocks in flat colors region by region, back to front, over the dimmed sketch.</summary>
    Blocking,

    /// <summary>Soft detail passes, film grain and warm light refine the picture.</summary>
    Refining,

    /// <summary>The finished picture.</summary>
    Done
}

/// <summary>The color of the pencil lines, pen tip, stage icon and progress line of a <see cref="SketchToImage"/>.</summary>
public enum SketchToImageTone
{
    /// <summary>The accent color.</summary>
    Accent,

    /// <summary>The foreground color.</summary>
    Neutral
}

/// <summary>Arguments of <see cref="SketchToImage.StageChanged"/>: the stage now on screen.</summary>
public sealed class SketchToImageStageChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SketchToImageStageChangedEventArgs(RoutedEvent routedEvent, SketchToImageStatus stage)
        : base(routedEvent)
    {
        Stage = stage;
    }

    /// <summary>The stage now on screen.</summary>
    public SketchToImageStatus Stage { get; }
}
