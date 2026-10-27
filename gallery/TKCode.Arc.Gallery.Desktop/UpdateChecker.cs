using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using TKCode.Arc.Gallery.Shell;

namespace TKCode.Arc.Gallery.Desktop;

/// <summary>A release newer than the running Gallery: its version and the page to download it from.</summary>
internal sealed record GalleryRelease(string Version, Uri Url);

/// <summary>
/// Asks GitHub for the newest release (preview releases included, since the Gallery ships as previews) and reports it when
/// it is newer than the running build. Failures (offline, rate limited) are silent: the check is a courtesy.
/// </summary>
internal static class UpdateChecker
{
    /// <summary>The version of the running build, without the commit suffix of the informational version.</summary>
    public static string CurrentVersion
    {
        get
        {
            var informational = typeof(UpdateChecker).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return informational is null ? "0.0.0" : informational.Split('+')[0];
        }
    }

    /// <summary>The newest release when it is newer than <paramref name="current"/>, otherwise null.</summary>
    public static async Task<GalleryRelease?> CheckAsync(string current)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            // GitHub's API refuses requests without a user agent.
            http.DefaultRequestHeaders.UserAgent.ParseAdd("TKCode.Arc-Gallery");
            await using var stream = await http.GetStreamAsync($"https://api.github.com/repos/{GalleryGitHub.Repository}/releases?per_page=10");
            using var document = await JsonDocument.ParseAsync(stream);
            return Newest(document.RootElement, current);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException
                                      or InvalidOperationException or FormatException or UriFormatException)
        {
            return null;
        }
    }

    internal static GalleryRelease? Newest(JsonElement releases, string current)
    {
        GalleryRelease? newest = null;
        var newestVersion = current;
        foreach (var release in releases.EnumerateArray())
        {
            if (release.GetProperty("draft").GetBoolean())
            {
                continue;
            }
            var version = release.GetProperty("tag_name").GetString()!.TrimStart('v');
            if (Compare(version, newestVersion) > 0)
            {
                newestVersion = version;
                newest = new GalleryRelease(version, new Uri(release.GetProperty("html_url").GetString()!));
            }
        }
        return newest;
    }

    /// <summary>Semantic version order: 0.3.1 is newer than 0.3.1-preview.2, and preview.10 is newer than preview.2.</summary>
    internal static int Compare(string left, string right)
    {
        var (leftCore, leftPre) = Split(left);
        var (rightCore, rightPre) = Split(right);
        var core = ParseCore(leftCore).CompareTo(ParseCore(rightCore));
        if (core != 0)
        {
            return core;
        }
        if (leftPre.Length == 0 || rightPre.Length == 0)
        {
            return leftPre.Length == rightPre.Length ? 0 : leftPre.Length == 0 ? 1 : -1;
        }
        var l = leftPre.Split('.');
        var r = rightPre.Split('.');
        for (var i = 0; i < Math.Min(l.Length, r.Length); i++)
        {
            var bothNumbers = int.TryParse(l[i], out var ln) & int.TryParse(r[i], out var rn);
            var order = bothNumbers ? ln.CompareTo(rn) : string.CompareOrdinal(l[i], r[i]);
            if (order != 0)
            {
                return order;
            }
        }
        return l.Length.CompareTo(r.Length);
    }

    private static (string Core, string Pre) Split(string version)
    {
        var dash = version.IndexOf('-');
        return dash < 0 ? (version, string.Empty) : (version[..dash], version[(dash + 1)..]);
    }

    private static Version ParseCore(string core) => System.Version.TryParse(core, out var parsed) ? parsed : new Version(0, 0);
}
