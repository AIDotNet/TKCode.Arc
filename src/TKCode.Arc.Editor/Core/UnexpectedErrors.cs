// Adaptation of vscode (src/vs/base/common/errors.ts: onUnexpectedError, commit b57d5c9e), MIT license.
// Upstream routes unexpected errors to one replaceable handler. Here an application subscribes to Occurred; the
// engine reports and carries on, as upstream does.

namespace TKCode.Arc.Editor;

/// <summary>
/// Errors the engine caught and carried on after: an exception from a callback the application passed in, or a misuse
/// that upstream only logs. It is application-wide, not state of a buffer.
/// </summary>
internal static class UnexpectedErrors
{
    /// <summary>Raised for every reported error, on the thread that reported it.</summary>
    public static event Action<Exception>? Occurred;

    public static void Report(Exception error) => Occurred?.Invoke(error);
}
