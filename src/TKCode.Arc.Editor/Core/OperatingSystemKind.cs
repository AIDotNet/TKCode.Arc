// Ported from vscode (src/vs/base/common/platform.ts: OperatingSystem and OS, commit b57d5c9e), MIT license.
// Differences from upstream:
// - The enum is OperatingSystemKind, not OperatingSystem: that name would hide System.OperatingSystem (IsMacOS, ...)
//   in every file of this namespace.
// - OS comes from System.OperatingSystem, not from the user agent; a browser host is Linux here unless the application
//   passes another kind to the keybinding registry.

namespace TKCode.Arc.Editor;

/// <summary>The operating system families keybindings distinguish (upstream's OperatingSystem).</summary>
internal enum OperatingSystemKind
{
    Windows = 1,
    Macintosh = 2,
    Linux = 3,
}

internal static class OperatingSystemKinds
{
    /// <summary>The operating system this process runs on (upstream's OS).</summary>
    public static readonly OperatingSystemKind OS =
        OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst()
            ? OperatingSystemKind.Macintosh
            : (OperatingSystem.IsWindows() ? OperatingSystemKind.Windows : OperatingSystemKind.Linux);
}
