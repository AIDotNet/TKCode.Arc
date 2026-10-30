namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// A sample workspace for the ProviderKeys demos: real providers from <see cref="AiModelCatalog"/> with the key prefixes
/// their docs show (Google documents none, so its keys skip the prefix check). The keys are placeholders no provider would
/// issue, and the rejected key and its error are invented. Marks are monograms.
/// </summary>
internal static class ProviderKeysDemoData
{
    /// <summary>Anthropic, OpenAI and Google.</summary>
    public static IReadOnlyList<ProviderKeyProvider> Providers { get; } =
        [ProviderOf(AiModelCatalog.Anthropic), ProviderOf(AiModelCatalog.OpenAI), ProviderOf(AiModelCatalog.Google)];

    /// <summary>The line under the title of the sample workspace.</summary>
    public const string Description =
        "Sample workspace with placeholder keys: every test is simulated and nothing is sent.";

    /// <summary>
    /// A connected Anthropic key used two minutes ago, an OpenAI key that was rejected and no Google key, relative to the
    /// moment it is read (a new list each time).
    /// </summary>
    public static IReadOnlyList<ProviderKey> Keys => KeysAt(DateTimeOffset.Now);

    /// <summary>What a server returns for an app's own workspace: only the last four characters of each key.</summary>
    public static IReadOnlyList<ProviderKey> StoredKeys
    {
        get
        {
            var now = DateTimeOffset.Now;
            return
            [
                new ProviderKey { Provider = AiModelCatalog.Anthropic.Key, Status = ProviderKeyStatus.Connected, Last4 = "7d3e", LastUsedAt = now.AddMinutes(-14) },
                new ProviderKey { Provider = AiModelCatalog.OpenAI.Key, Status = ProviderKeyStatus.Connected, Last4 = "a0b1", LastUsedAt = now.AddDays(-1) },
            ];
        }
    }

    /// <summary>A row for a catalog provider: its key, its name and the key prefix its docs show (none when not documented).</summary>
    public static ProviderKeyProvider ProviderOf(AiProviderInfo provider) => new()
    {
        Id = provider.Key,
        Name = provider.Name,
        KeyPrefix = provider.ApiKeyPrefix ?? string.Empty,
    };

    private static List<ProviderKey> KeysAt(DateTimeOffset now) =>
    [
        new ProviderKey
        {
            Provider = AiModelCatalog.Anthropic.Key,
            Status = ProviderKeyStatus.Connected,
            Value = (AiModelCatalog.Anthropic.ApiKeyPrefix ?? string.Empty) + "test-xxxxxxxxxxxxxxxxxxxxxxxx4f2a",
            LastUsedAt = now - TimeSpan.FromMinutes(2),
            CheckedAt = now - TimeSpan.FromHours(1),
        },
        new ProviderKey
        {
            Provider = AiModelCatalog.OpenAI.Key,
            Status = ProviderKeyStatus.Invalid,
            Value = (AiModelCatalog.OpenAI.ApiKeyPrefix ?? string.Empty) + "test-0000000000000000000000009c1d",
            LastUsedAt = now - TimeSpan.FromDays(3),
            CheckedAt = now - TimeSpan.FromMinutes(20),
            Error = "401 Unauthorized",
        },
    ];
}
