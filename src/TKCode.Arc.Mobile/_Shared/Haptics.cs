namespace TKCode.Arc.Mobile;

/// <summary>The tactile cue a mobile control asks the device for through <see cref="Haptics"/>.</summary>
public enum HapticKind
{
    /// <summary>A subtle tick for switching a toggle, segment, radio or picker choice.</summary>
    Selection,

    /// <summary>A light impact, for example a sheet settling onto a detent.</summary>
    Light,

    /// <summary>A medium impact, for example crossing the pull-to-refresh or full-swipe threshold.</summary>
    Medium,

    /// <summary>A heavier impact for a decisive physical snap.</summary>
    Heavy,

    /// <summary>A notification cue for a completed action.</summary>
    Success,

    /// <summary>A notification cue for a warning or destructive action.</summary>
    Warning,

    /// <summary>A notification cue for a failed or blocked action.</summary>
    Error,
}

/// <summary>
/// Platform bridge for <see cref="Haptics"/>: a platform package (such as <c>TKCode.Arc.Mobile.iOS</c>) implements this
/// and assigns <see cref="Haptics.Platform"/> at startup so mobile controls produce native tactile feedback.
/// </summary>
public interface IHapticsPlatform
{
    /// <summary>Triggers the device's haptic feedback for <paramref name="kind"/>.</summary>
    void Trigger(HapticKind kind);
}

/// <summary>
/// Tactile feedback for mobile controls. When <see cref="Platform"/> is null (desktop, web or headless tests) or
/// <see cref="IsEnabled"/> is false, <see cref="Trigger"/> is a no-op.
/// </summary>
public static class Haptics
{
    /// <summary>
    /// The platform's haptic feedback bridge, or null when running without one. Set once at startup by a platform
    /// package (<c>TKCode.Arc.Mobile.iOS</c> sets it automatically from <c>ArcAppDelegate&lt;TApp&gt;</c>).
    /// </summary>
    public static IHapticsPlatform? Platform { get; set; }

    /// <summary>Whether mobile controls trigger haptic feedback: true by default.</summary>
    public static bool IsEnabled { get; set; } = true;

    /// <summary>Triggers <paramref name="kind"/> if <see cref="IsEnabled"/> is true and <see cref="Platform"/> is set.</summary>
    public static void Trigger(HapticKind kind)
    {
        if (IsEnabled && Platform is { } platform)
        {
            platform.Trigger(kind);
        }
    }
}
