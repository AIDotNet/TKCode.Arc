namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The ApiKeys demos' data (all invented): five keys of a payments relay called Northwind Relay (three live, two test;
/// one idle for 37 days, one never used) and the four scopes its keys can hold. Last use is relative to the moment the
/// demo opens, so "4 min ago" stays true; creation dates are fixed.
/// </summary>
public static class ApiKeysDemoData
{
    public const string Title = "API keys";

    public const string Description =
        "Keys let your backend call the Northwind Relay API. Store them in a vault, never in client code.";

    /// <summary>Who creates keys in the demos (recorded on simulated keys).</summary>
    public const string CurrentUser = "Lena Ortiz";

    /// <summary>The four scopes; new keys start with accounts:read ticked.</summary>
    public static ApiKeyScope[] Scopes() =>
    [
        new() { Name = "orders:write", Description = "Create, update and cancel orders" },
        new() { Name = "accounts:read", Description = "Read account profiles and contacts", IsDefault = true },
        new() { Name = "refunds:write", Description = "Issue and reverse refunds" },
        new() { Name = "exports:read", Description = "Download ledger and settlement exports" },
    ];

    /// <summary>The five sample keys, newest use first.</summary>
    public static ApiKeyItem[] Keys()
    {
        var now = DateTimeOffset.Now;
        return
        [
            Key("checkout", "Checkout service", ApiKeyEnvironment.Live, "7c3e", now.AddMinutes(-4), new DateTime(2026, 2, 11),
                "Lena Ortiz", "4 min ago from 18.144.62.10, Frankfurt", 942_317, "orders:write", "accounts:read", "refunds:write"),
            Key("payout", "Payout runner", ApiKeyEnvironment.Live, "a19b", now.AddHours(-2), new DateTime(2025, 12, 3),
                "Kenji Mori", "2 hours ago from 3.98.201.44, Montreal", 58_204, "refunds:write", "exports:read"),
            Key("sandbox", "Sandbox", ApiKeyEnvironment.Test, "4d0f", now.AddDays(-1), new DateTime(2026, 7, 21),
                "Amara Nwosu", "yesterday from 10.0.4.17, Lagos", 3_611, "orders:write", "accounts:read"),
            Key("ledger", "Ledger sync", ApiKeyEnvironment.Live, "e852", now.AddDays(-37), new DateTime(2025, 10, 14),
                "Pavel Novak", "37 days ago from 52.29.8.113, Prague", 0, "exports:read"),
            Key("qa", "QA harness", ApiKeyEnvironment.Test, "0b6d", null, new DateTime(2026, 8, 28),
                "Hana Kim", null, 0, "orders:write", "accounts:read", "refunds:write", "exports:read"),
        ];
    }

    /// <summary>A secret like a server would hand out: the prefix and 40 letters and digits.</summary>
    public static string NewSecret(ApiKeyEnvironment environment)
    {
        const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        var chars = new char[40];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[Random.Shared.Next(alphabet.Length)];
        }
        return Prefix(environment) + new string(chars);
    }

    /// <summary>The row of a freshly created key.</summary>
    public static ApiKeyCreated Create(ApiKeyRequest request)
    {
        var secret = NewSecret(request.Environment);
        var item = new ApiKeyItem
        {
            Key = Guid.NewGuid().ToString("N"),
            Name = request.Name,
            Environment = request.Environment,
            Prefix = Prefix(request.Environment),
            LastFour = secret[^4..],
            CreatedAt = DateTimeOffset.Now,
            CreatedBy = CurrentUser,
        };
        item.Scopes.AddRange(request.Scopes);
        return new ApiKeyCreated(item, secret);
    }

    private static string Prefix(ApiKeyEnvironment environment) =>
        environment == ApiKeyEnvironment.Live ? "rk_live_" : "rk_test_";

    private static ApiKeyItem Key(string key, string name, ApiKeyEnvironment environment, string lastFour,
        DateTimeOffset? lastUsed, DateTime created, string creator, string? lastRequest, long requests, params string[] scopes)
    {
        var item = new ApiKeyItem
        {
            Key = key,
            Name = name,
            Environment = environment,
            Prefix = Prefix(environment),
            LastFour = lastFour,
            LastUsedAt = lastUsed,
            CreatedAt = new DateTimeOffset(created),
            CreatedBy = creator,
            LastRequestText = lastRequest,
            RequestsLast30Days = requests,
        };
        item.Scopes.AddRange(scopes);
        return item;
    }
}
