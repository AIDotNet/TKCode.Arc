using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// The Developers page's data, uiarc.dev's Halden API samples: five API keys and four scopes; three webhook endpoints of
/// Fernway with 24 hours of half-hour traffic (Sep 21 15:00 to now, UTC), their recent deliveries and the six events the
/// "Add endpoint" form offers. Sofia Ramirez is the person signed in. Simulated server calls use uiarc.dev's timings and
/// numbers.
/// </summary>
internal static partial class DemoDevelopersData
{
    public const string CurrentUser = "Sofia Ramirez";

    public const string KeysDescription =
        "Secret keys sign requests from your servers to the Halden API. Keep them out of browsers and source control.";

    public const string WebhooksDescription =
        "Halden signs every event and posts it to your endpoints. Failed deliveries retry on their own for three days, or you can retry them now.";

    private static readonly TimeSpan Utc = TimeSpan.Zero;
    private static readonly CultureInfo En = CultureInfo.InvariantCulture;

    #region API keys

    public static ApiKeyLabels KeyLabels { get; } = new()
    {
        CountWords = count => count == 1 ? "key" : "keys",
        CountName = count => count == 1 ? "1 key" : $"{count} keys",
        NamePlaceholder = "Billing sync",
        NameRequired = "Name the key so your team can tell it apart",
        TestHint = "Test keys only reach test data",
        LiveHint = "Live keys act on real payments and customers",
        ScopeRequired = "Choose at least one scope",
        SecretOnce = "Copy it now and keep it in your secrets manager. You will not see this key again after you close this panel.",
        Date = date => date.ToString("MMM d, yyyy", En),
        RevokeNote = "Requests signed with this key fail as soon as it is revoked.",
        StaleNote = days => $"No requests in {days} days. Revoke it if nothing depends on it.",
        EmptyTitle = "No active keys",
        EmptyDescription = "Requests to the Halden API fail until you create one.",
        CreatedAnnouncement = name => $"{name} created. Copy the secret now, it will not be shown again.",
        CopiedAnnouncement = "Secret copied. Paste it into your secrets manager.",
        RevokedAnnouncement = name => $"{name} revoked."
    };

    public static ApiKeyScope[] Scopes() =>
    [
        new() { Name = "payments:write", Description = "Create, capture, and refund payments" },
        new() { Name = "customers:read", Description = "Read customers and saved payment methods", IsDefault = true },
        new() { Name = "invoices:write", Description = "Draft, finalize, and send invoices" },
        new() { Name = "reports:read", Description = "Download balance and payout reports" }
    ];

    /// <summary>The five sample keys; last use is relative to the demo's "now" (Sep 22 2026, 15:10 UTC).</summary>
    public static ApiKeyItem[] Keys()
    {
        var now = new DateTimeOffset(DemoData.Now, Utc);
        return
        [
            Key("production", "Production server", ApiKeyEnvironment.Live, "4f2a", new DateTime(2026, 3, 4), now.AddMinutes(-2),
                "34.201.18.7, Ashburn", 1_284_902, "Emma Collins", "payments:write", "customers:read", "invoices:write"),
            Key("billing", "Billing worker", ApiKeyEnvironment.Live, "9c01", new DateTime(2026, 1, 18), now.AddHours(-3),
                "18.184.72.40, Frankfurt", 48_211, "Marcus Johnson", "invoices:write", "reports:read"),
            Key("staging", "Staging", ApiKeyEnvironment.Test, "d7e3", new DateTime(2026, 8, 30), now.AddDays(-1),
                "CI runner 10.0.12.8", 9_870, "Ava Mitchell", "payments:write", "customers:read"),
            Key("analytics", "Analytics export", ApiKeyEnvironment.Live, "2b8a", new DateTime(2025, 11, 2), now.AddDays(-41),
                "52.17.230.9, Dublin", 0, "Jasmine Brooks", "reports:read"),
            Key("local", "Local development", ApiKeyEnvironment.Test, "6a55", new DateTime(2026, 9, 9), null,
                null, 0, "Sofia Ramirez", "payments:write", "customers:read", "invoices:write", "reports:read")
        ];
    }

    /// <summary>uiarc.dev's generate: 720 ms, then <c>sk_{env}_</c> and 40 letters and digits; created today by Sofia.</summary>
    public static async Task<ApiKeyCreated> GenerateKeyAsync(ApiKeyRequest request, CancellationToken token)
    {
        await Task.Delay(720, token);
        var prefix = request.Environment == ApiKeyEnvironment.Live ? "sk_live_" : "sk_test_";
        var secret = prefix + RandomText(40, "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789");
        var item = new ApiKeyItem
        {
            Key = "key-" + Guid.NewGuid().ToString("N"),
            Name = request.Name,
            Environment = request.Environment,
            Prefix = prefix,
            LastFour = secret[^4..],
            CreatedAt = new DateTimeOffset(DemoData.Now, Utc),
            CreatedBy = CurrentUser
        };
        item.Scopes.AddRange(request.Scopes);
        return new ApiKeyCreated(item, secret);
    }

    private static ApiKeyItem Key(string key, string name, ApiKeyEnvironment environment, string lastFour, DateTime created,
                                  DateTimeOffset? used, string? source, long requests, string owner, params string[] scopes)
    {
        var prefix = environment == ApiKeyEnvironment.Live ? "sk_live_" : "sk_test_";
        var item = new ApiKeyItem
        {
            Key = key,
            Name = name,
            Environment = environment,
            Prefix = prefix,
            LastFour = lastFour,
            CreatedAt = new DateTimeOffset(created, Utc),
            LastUsedAt = used,
            // "Last request": "{used} from {source}" (the block writes "No requests yet" for a key never used).
            LastRequestText = used is { } at && source is not null
                ? $"{ApiKeyLabels.DefaultAgo(new DateTimeOffset(DemoData.Now, Utc) - at)} from {source}"
                : null,
            RequestsLast30Days = requests,
            CreatedBy = owner
        };
        item.Scopes.AddRange(scopes);
        return item;
    }

    #endregion

    private static string RandomText(int length, string alphabet)
    {
        var chars = new char[length];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[Random.Shared.Next(alphabet.Length)];
        }
        return new string(chars);
    }

    [GeneratedRegex(@"^https://[a-z0-9-]+(\.[a-z0-9-]+)+(:\d+)?(/[^\s]*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex HttpsUrl();

    /// <summary>uiarc.dev's URL check: required, then https on a host with a dot.</summary>
    public static string? ValidateUrl(string url)
    {
        var text = url.Trim();
        if (text.Length == 0)
        {
            return "Enter the URL Halden should post events to";
        }
        return HttpsUrl().IsMatch(text) ? null : "Use a public HTTPS URL, such as https://api.fernway.example/webhooks";
    }

    /// <summary>A deterministic hex string for signatures (the same delivery always signs the same).</summary>
    private static string Hex(string seed, int length)
    {
        var builder = new StringBuilder();
        var value = (uint)seed.Aggregate(17, (hash, c) => unchecked(hash * 31 + c));
        while (builder.Length < length)
        {
            value = unchecked(value * 1664525u + 1013904223u);
            builder.Append((value >> 8).ToString("x6", En));
        }
        return builder.ToString(0, length);
    }
}
