using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Opens link destinations for the Headers and footers blocks. Arc renders an anchor when a link has an <c>href</c>; a
/// desktop app has no browser to follow it, so the blocks raise their navigate event first and, when nobody handles it,
/// hand an absolute web or mail address to the platform launcher.
/// </summary>
internal static class SiteLinks
{
    /// <summary>The absolute <c>http</c>, <c>https</c> or <c>mailto</c> address in <paramref name="href"/>, or null.</summary>
    public static Uri? ToLaunchable(string? href)
    {
        if (string.IsNullOrWhiteSpace(href) || !Uri.TryCreate(href.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }
        return uri.Scheme is "http" or "https" or "mailto" ? uri : null;
    }

    /// <summary>Opens <paramref name="href"/> with the launcher of <paramref name="visual"/>'s top level; false when it is not launchable or there is no launcher.</summary>
    public static bool TryLaunch(Visual visual, string? href)
    {
        if (ToLaunchable(href) is not { } uri || TopLevel.GetTopLevel(visual)?.Launcher is not { } launcher)
        {
            return false;
        }
        _ = launcher.LaunchUriAsync(uri);
        return true;
    }
}
