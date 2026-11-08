using System.Reflection;
using System.Runtime.InteropServices;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>Which host shows the gallery.</summary>
public enum GalleryHostKind
{
    /// <summary>The WebAssembly site: it recommends the desktop app and says why.</summary>
    Web,

    /// <summary>The desktop app: it has the settings menu and the update check.</summary>
    Desktop
}

/// <summary>The system a visitor of the site is on, which decides the download offered first.</summary>
public enum GalleryClientOs
{
    Unknown,
    MacOS,
    Windows,
    Linux,

    /// <summary>A phone or tablet: there is no app to install, so the site offers the downloads for later.</summary>
    Mobile
}

/// <summary>What the shell needs to know about its host: which one it is, the visitor's system and the version.</summary>
public static class GalleryHost
{
    /// <summary>The host; a browser build is the site, everything else the desktop app. Set it before creating a shell.</summary>
    public static GalleryHostKind Kind { get; set; } = OperatingSystem.IsBrowser() ? GalleryHostKind.Web : GalleryHostKind.Desktop;

    public static bool IsWeb => Kind == GalleryHostKind.Web;

    public static bool IsDesktop => Kind == GalleryHostKind.Desktop;

    /// <summary>The visitor's system. The browser host sets it from the user agent, which .NET cannot see there.</summary>
    public static GalleryClientOs ClientOs { get; set; } = OperatingSystem.IsMacOS() ? GalleryClientOs.MacOS
        : OperatingSystem.IsWindows() ? GalleryClientOs.Windows
        : OperatingSystem.IsLinux() ? GalleryClientOs.Linux
        : GalleryClientOs.Unknown;

    /// <summary>The version of this build, without the commit suffix of the informational version.</summary>
    public static string Version { get; } = ReadVersion();

    /// <summary>The runtime and UI framework of this build, for the About page and bug reports.</summary>
    public static string Runtime { get; } =
        $".NET {Environment.Version.Major}.{Environment.Version.Minor} · Avalonia {typeof(Avalonia.Application).Assembly.GetName().Version?.ToString(3)} · {RuntimeInformation.RuntimeIdentifier}";

    /// <summary>The system a browser's user agent names; a phone or tablet counts as <see cref="GalleryClientOs.Mobile"/>.</summary>
    public static GalleryClientOs ParseUserAgent(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
        {
            return GalleryClientOs.Unknown;
        }
        // Android says Linux and an iPhone says Mac OS X, so the phones come first.
        if (Has("Android") || Has("iPhone") || Has("iPad") || Has("iPod") || Has("Mobile"))
        {
            return GalleryClientOs.Mobile;
        }
        return Has("Windows") ? GalleryClientOs.Windows
            : Has("Macintosh") || Has("Mac OS") ? GalleryClientOs.MacOS
            : Has("Linux") || Has("X11") ? GalleryClientOs.Linux
            : GalleryClientOs.Unknown;

        bool Has(string text) => userAgent.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadVersion()
    {
        var informational = typeof(GalleryHost).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return informational is null ? "0.0.0" : informational.Split('+')[0];
    }
}
