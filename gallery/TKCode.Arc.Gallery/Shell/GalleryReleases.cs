using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>One file of a release.</summary>
public sealed record GalleryReleaseAsset(string Name, Uri Url, long Size);

/// <summary>A published release: its version, its page and its files.</summary>
public sealed record GalleryRelease(string Version, Uri Url, IReadOnlyList<GalleryReleaseAsset> Assets)
{
    /// <summary>The app's file for a runtime identifier (<c>osx-arm64</c>, <c>win-x64</c>, ...), or null.</summary>
    public GalleryReleaseAsset? AssetFor(string runtime) =>
        Assets.FirstOrDefault(asset => asset.Name.StartsWith("TKCode.Arc.Gallery-", StringComparison.Ordinal)
                                       && asset.Name.Contains($"-{runtime}.", StringComparison.Ordinal));

    /// <summary>Where to get this build's system: its file when the release has one, otherwise the release's page.</summary>
    public Uri DownloadUrl => AssetFor(RuntimeInformation.RuntimeIdentifier)?.Url ?? Url;
}

/// <summary>
/// The releases on GitHub (preview releases included, since the Gallery ships as previews). The site lists their files and
/// the desktop app compares them with its own version. Failures (offline, rate limited) give null.
/// </summary>
public static class GalleryReleases
{
    /// <summary>The page with every release, for when the list cannot be read.</summary>
    public static Uri AllUrl { get; } = new($"{GalleryGitHub.RepositoryUrl}/releases");

    /// <summary>How the newest release is obtained; tests and hosts without network replace it.</summary>
    public static Func<Task<GalleryRelease?>> Provider { get; set; } = FetchNewestAsync;

    private static async Task<GalleryRelease?> FetchNewestAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            // GitHub's API refuses requests without a user agent; a browser sets its own and forbids changing it.
            if (!OperatingSystem.IsBrowser())
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("TKCode.Arc-Gallery");
            }
            await using var stream = await http.GetStreamAsync($"https://api.github.com/repos/{GalleryGitHub.Repository}/releases?per_page=10");
            using var document = await JsonDocument.ParseAsync(stream);
            return Newest(document.RootElement);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException
                                      or InvalidOperationException or FormatException or UriFormatException)
        {
            return null;
        }
    }

    /// <summary>The newest release of GitHub's list, drafts left out; null when the list has none.</summary>
    internal static GalleryRelease? Newest(JsonElement releases)
    {
        GalleryRelease? newest = null;
        foreach (var release in releases.EnumerateArray())
        {
            if (release.GetProperty("draft").GetBoolean())
            {
                continue;
            }
            var version = release.GetProperty("tag_name").GetString()!.TrimStart('v');
            if (newest is null || Compare(version, newest.Version) > 0)
            {
                newest = new GalleryRelease(version, new Uri(release.GetProperty("html_url").GetString()!), ReadAssets(release));
            }
        }
        return newest;
    }

    private static List<GalleryReleaseAsset> ReadAssets(JsonElement release)
    {
        var assets = new List<GalleryReleaseAsset>();
        if (release.TryGetProperty("assets", out var list))
        {
            foreach (var asset in list.EnumerateArray())
            {
                assets.Add(new GalleryReleaseAsset(asset.GetProperty("name").GetString()!,
                                                   new Uri(asset.GetProperty("browser_download_url").GetString()!),
                                                   asset.GetProperty("size").GetInt64()));
            }
        }
        return assets;
    }

    /// <summary>Semantic version order: 0.3.1 is newer than 0.3.1-preview.2, and preview.10 is newer than preview.2.</summary>
    public static int Compare(string left, string right)
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

    private static Version ParseCore(string core) => Version.TryParse(core, out var parsed) ? parsed : new Version(0, 0);
}

/// <summary>Where a check for a newer version stands.</summary>
public enum GalleryUpdateStatus
{
    /// <summary>No check has run.</summary>
    Idle,
    Checking,
    UpToDate,
    Available,

    /// <summary>The releases could not be read (offline, rate limited).</summary>
    Failed
}

/// <summary>Remembers whether the desktop app checks for a newer version when it starts.</summary>
public interface IGalleryUpdateStore
{
    bool? ReadAutoCheck();

    void WriteAutoCheck(bool value);
}

/// <summary>
/// The desktop app's update check: one state shared by the toast at startup, the settings menu, the home page and the
/// About page.
/// </summary>
public static class GalleryUpdates
{
    /// <summary>Set by the desktop host to keep <see cref="AutoCheck"/> between runs.</summary>
    public static IGalleryUpdateStore? Store { get; set; }

    public static GalleryUpdateStatus Status { get; private set; }

    /// <summary>The release that is newer than this build, while <see cref="Status"/> is <see cref="GalleryUpdateStatus.Available"/>.</summary>
    public static GalleryRelease? Available { get; private set; }

    /// <summary>When the last check finished.</summary>
    public static DateTimeOffset? LastChecked { get; private set; }

    /// <summary>Raised on the UI thread when the state changes.</summary>
    public static event Action? Changed;

    /// <summary>Whether the app checks when it starts; on until the visitor turns it off.</summary>
    public static bool AutoCheck
    {
        get => Store?.ReadAutoCheck() ?? true;
        set => Store?.WriteAutoCheck(value);
    }

    /// <summary>Asks GitHub for the newest release and compares it with this build; a check in progress is not repeated.</summary>
    public static Task CheckAsync() => CheckAsync(GalleryHost.Version);

    internal static async Task CheckAsync(string current)
    {
        if (Status == GalleryUpdateStatus.Checking)
        {
            return;
        }
        Set(GalleryUpdateStatus.Checking, null);
        var newest = await GalleryReleases.Provider();
        LastChecked = DateTimeOffset.Now;
        if (newest is null)
        {
            Set(GalleryUpdateStatus.Failed, null);
        }
        else if (GalleryReleases.Compare(newest.Version, current) > 0)
        {
            Set(GalleryUpdateStatus.Available, newest);
        }
        else
        {
            Set(GalleryUpdateStatus.UpToDate, null);
        }
    }

    /// <summary>Forgets the last check (tests).</summary>
    internal static void Reset() => Set(GalleryUpdateStatus.Idle, null);

    private static void Set(GalleryUpdateStatus status, GalleryRelease? available)
    {
        Status = status;
        Available = available;
        Changed?.Invoke();
    }
}
