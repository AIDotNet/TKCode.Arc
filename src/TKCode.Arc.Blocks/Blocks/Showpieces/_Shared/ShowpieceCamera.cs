using TKCode.Arc.Motion;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// What <see cref="ShowpieceStage"/> needs from a camera: a viewport, a motion level, an explicit frame step and a
/// change signal. Both <see cref="ShowpieceCamera2D"/> and <see cref="ShowpieceOrbitCamera"/> implement it; a stage
/// with a <see cref="ShowpieceStage.Camera"/> advances it every frame and redraws when it changes.
/// </summary>
internal interface IShowpieceCamera
{
    /// <summary>Something is moving (springs, flights, glides, drift or a pending settle): frames are needed.</summary>
    bool IsMoving { get; }

    /// <summary>The effective motion level: Reduced and Off make every change land at once and drop glides.</summary>
    ArcMotionLevel MotionLevel { get; set; }

    /// <summary>The stage size in px.</summary>
    Size Viewport { get; }

    /// <summary>Sets the stage size (the view keeps its center and zoom readout).</summary>
    void SetViewport(Size size);

    /// <summary>Advances by <paramref name="dt"/> seconds; returns <see cref="IsMoving"/>.</summary>
    bool Advance(double dt);

    /// <summary>Raised whenever the rendered view changed (every moving frame, and on jumps).</summary>
    event Action? Changed;

    /// <summary>Increments on every change; cheap "did the view change since I last looked".</summary>
    int Version { get; }
}

/// <summary>When a <see cref="ShowpieceCamera2D"/> keeps the view inside its pan bounds.</summary>
internal enum ShowpieceBoundsMode
{
    /// <summary>Always (Semantic zoom): wheel zoom is clamped at once; drags rubber-band past the bounds.</summary>
    Immediate,

    /// <summary>
    /// After the gesture (Customer galaxy): wheel zoom may push the view off-center; once the wheel has rested for
    /// <see cref="ShowpieceCamera2D.BoundsSettleDelay"/> the view springs back inside. Drags still rubber-band.
    /// </summary>
    AfterGesture
}

/// <summary>What a <see cref="ShowpieceCamera2D"/> does with zoom requests past its limits.</summary>
internal enum ShowpieceZoomOverflow
{
    /// <summary>Stop at the limit (Customer galaxy: no zooming out past fit).</summary>
    Clamp,

    /// <summary>
    /// Rubber-band in log zoom (Semantic zoom: <c>RubberBand.Apply(overflow, 0.2)</c> on ln z) and return inside once
    /// the input rested for <see cref="ShowpieceCamera2D.ZoomSettleDelay"/>.
    /// </summary>
    RubberBand
}

/// <summary>How a camera flight travels.</summary>
internal enum ShowpieceFlightMode
{
    /// <summary>Center and zoom chase the target on <see cref="ShowpieceCamera2D.FlySpring"/> (Customer galaxy).</summary>
    Spring,

    /// <summary>
    /// The van Wijk zoom-pan path over <c>460 + 380·S</c> ms on the in-out curve (Semantic zoom); see
    /// <see cref="VanWijkPath"/>.
    /// </summary>
    VanWijk
}
