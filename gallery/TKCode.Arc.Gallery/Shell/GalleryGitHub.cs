using System.Net.Http;
using System.Text.Json;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>Remembers the star count between visits (the browser host keeps it in local storage for an hour).</summary>
public interface IGalleryStarCache
{
    /// <summary>The remembered count, or null when there is none or it is old.</summary>
    int? Read();

    void Write(int stars);
}

/// <summary>The gallery's repository on GitHub and its star count, shown by the top bar's GitHub button.</summary>
public static class GalleryGitHub
{
    public const string Repository = "AIDotNet/TKCode.Arc";

    public static Uri RepositoryUrl { get; } = new($"https://github.com/{Repository}");

    /// <summary>Set by a host to remember the count; the count is fetched from GitHub only when nothing is remembered.</summary>
    public static IGalleryStarCache? Cache { get; set; }

    /// <summary>
    /// How the count is obtained; the default asks GitHub's API once per run. Tests and hosts without network replace it
    /// (returning null shows the button without a count).
    /// </summary>
    public static Func<Task<int?>> StarsProvider
    {
        get => s_provider;
        set
        {
            s_provider = value;
            s_stars = null;
        }
    }

    private static Func<Task<int?>> s_provider = FetchAsync;
    private static Task<int?>? s_stars;

    /// <summary>The star count, or null when it cannot be read (offline, rate limited). Asked once per run.</summary>
    public static Task<int?> GetStarsAsync() => s_stars ??= s_provider();

    /// <summary>1,234 as 1.2k; smaller counts as they are.</summary>
    public static string Format(int stars) => stars >= 1000 ? $"{stars / 1000.0:0.#}k" : stars.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<int?> FetchAsync()
    {
        if (Cache?.Read() is { } remembered)
        {
            return remembered;
        }
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            // GitHub's API refuses requests without a user agent; a browser sets its own and forbids changing it.
            if (!OperatingSystem.IsBrowser())
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("TKCode.Arc-Gallery");
            }
            await using var stream = await http.GetStreamAsync($"https://api.github.com/repos/{Repository}");
            using var document = await JsonDocument.ParseAsync(stream);
            var stars = document.RootElement.GetProperty("stargazers_count").GetInt32();
            Cache?.Write(stars);
            return stars;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException
                                      or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
}
