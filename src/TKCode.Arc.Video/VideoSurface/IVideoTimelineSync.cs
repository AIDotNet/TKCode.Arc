using Avalonia.Platform;

namespace TKCode.Arc;

/// <summary>
/// Implemented by an <see cref="IVideoPlayer"/> whose GPU frames are guarded by a timeline semaphore instead of a keyed mutex.
/// Avalonia's Metal compositor (macOS) imports an <c>IOSurfaceRef</c> only with timeline-semaphore synchronisation
/// (<c>CompositionGpuImportedImageSynchronizationCapabilities.TimelineSemaphores</c>, semaphore type <c>MetalSharedEvent</c>):
/// it waits until the semaphore reaches a value before reading the image and sets it to another value when done.
/// <see cref="VideoSurface"/> asks for these values for each GPU frame it presents, while it holds the frame's lease.
/// </summary>
public interface IVideoTimelineSync
{
    /// <summary>
    /// Gets the semaphore guarding the GPU frame with <paramref name="gpuImageId"/> that was just acquired: the compositor
    /// waits until it reaches <paramref name="waitValue"/> (the player signals it when the image is written) and signals
    /// <paramref name="signalValue"/> once it has copied the image, after which the player may write to the image again.
    /// <paramref name="semaphore"/>'s <see cref="IPlatformHandle.HandleDescriptor"/> is a <c>KnownPlatformGraphicsExternalSemaphoreHandleTypes</c>
    /// value (<c>MetalSharedEvent</c> on macOS) created on the adapter named by <see cref="VideoFrameTarget.AdapterLuid"/>.
    /// Use one semaphore per pooled image, so each timeline only moves forward: the player signals an image's wait value
    /// from the CPU only after that image's previous signal value has been reached (a shared semaphore would be set back
    /// when a late GPU signal lands after a CPU signal, and the compositor would wait forever).
    /// Returns false when the frame needs no semaphore.
    /// </summary>
    bool TryGetTimelineSync(long gpuImageId, out IPlatformHandle semaphore, out ulong waitValue, out ulong signalValue);
}
