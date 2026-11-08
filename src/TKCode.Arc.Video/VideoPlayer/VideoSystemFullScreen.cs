namespace TKCode.Arc;

/// <summary>
/// System full screen where it is not a <c>Window</c> state: a platform package (the browser backend, through the
/// Fullscreen API) plugs itself in here, so <see cref="VideoPlayer"/> needs no reference to it (spec section 10).
/// </summary>
internal static class VideoSystemFullScreen
{
    private static Action? s_enter;
    private static Action? s_exit;

    /// <summary>Whether a platform full screen is plugged in.</summary>
    public static bool IsAvailable => s_enter is not null;

    /// <summary>Raised when the platform left full screen by itself (the user pressed Escape in the browser).</summary>
    public static event EventHandler? Exited;

    /// <summary>Plugs in the platform's full screen. <paramref name="enter"/> may be refused by the platform (no user gesture); the player then stays in window full screen.</summary>
    public static void Register(Action enter, Action exit)
    {
        s_enter = enter ?? throw new ArgumentNullException(nameof(enter));
        s_exit = exit ?? throw new ArgumentNullException(nameof(exit));
    }

    public static void Enter() => s_enter?.Invoke();

    public static void Exit() => s_exit?.Invoke();

    /// <summary>Called by the platform when full screen ended without <see cref="Exit"/>.</summary>
    public static void RaiseExited() => Exited?.Invoke(null, EventArgs.Empty);
}
