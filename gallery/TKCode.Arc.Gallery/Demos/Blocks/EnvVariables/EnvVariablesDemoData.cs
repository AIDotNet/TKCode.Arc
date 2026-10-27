namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The EnvVariables demos' data (all invented): the "lumen-console" project with Development, Staging and Production,
/// ten keys on seventeen rows (several keys have one value for development and staging and another for production) and
/// five write-only values the demo never loads (they carry a fingerprint instead). Dates count back from today.
/// </summary>
public static class EnvVariablesDemoData
{
    public const string Project = "lumen-console";

    public const string Development = "development";

    public const string Staging = "staging";

    public const string Production = "production";

    /// <summary>A .env text that shows every line status: new keys, a key set twice, a key starting with a digit and a replacement.</summary>
    public const string SampleDotenv =
        "# exported from a local shell\n" +
        "FEATURE_FLAGS_URL=https://flags.lumen.example\n" +
        "OBJECT_STORE_REGION=eu-north\n" +
        "export WEBHOOK_RELAY_TOKEN=\"relay_demo_3c8d\"\n" +
        "CACHE_TTL_SECONDS=45\n" +
        "9LIVES_TOKEN=abc123\n" +
        "CACHE_TTL_SECONDS=60\n";

    /// <summary>The three environments.</summary>
    public static EnvEnvironment[] Environments() =>
    [
        new(Development, "Development"),
        new(Staging, "Staging"),
        new(Production, "Production"),
    ];

    /// <summary>The seventeen rows, in no particular order (the block sorts them).</summary>
    public static EnvVariable[] Variables()
    {
        var today = DateTimeOffset.Now.Date;
        EnvVariable Row(string key, string? value, int daysAgo, string by, params string[] envs) =>
            new(key, value, envs) { UpdatedAt = today.AddDays(-daysAgo), UpdatedBy = by };
        EnvVariable Secret(string key, string fingerprint, int daysAgo, string by, params string[] envs) =>
            new(key, null, envs) { IsSensitive = true, Fingerprint = fingerprint, UpdatedAt = today.AddDays(-daysAgo), UpdatedBy = by };

        return
        [
            Row("APP_PUBLIC_ORIGIN", "http://localhost:4000", 41, "Iris Navarro", Development),
            Row("APP_PUBLIC_ORIGIN", "https://staging.lumen.example", 41, "Iris Navarro", Staging),
            Row("APP_PUBLIC_ORIGIN", "https://lumen.example", 41, "Iris Navarro", Production),
            Row("CACHE_TTL_SECONDS", "30", 9, "Theo Brandt", Development, Staging),
            Row("CACHE_TTL_SECONDS", "300", 9, "Theo Brandt", Production),
            Row("ENABLE_BETA_SEARCH", "on", 3, "Mina Okafor", Development, Staging),
            Row("ENABLE_BETA_SEARCH", "off", 3, "Mina Okafor", Production),
            Row("PRIMARY_DB_DSN", "postgres://app:devpass@db.internal.example:5432/lumen_dev", 64, "Jonas Lindqvist", Development, Staging),
            Secret("PRIMARY_DB_DSN", "5e0c2a", 64, "Jonas Lindqvist", Production),
            Row("QUEUE_BROKER_URL", "amqp://broker-stg.internal.example", 27, "Priya Raman", Staging),
            Secret("QUEUE_BROKER_URL", "a71f3d", 27, "Priya Raman", Production),
            Row("MAILER_TOKEN", "mt_demo_00example", 15, "Theo Brandt", Development, Staging, Production),
            Secret("ANALYTICS_WRITE_KEY", "c94b10", 22, "Mina Okafor", Production),
            Row("TRACE_SAMPLE_RATE", "0.2", 6, "Priya Raman", Staging, Production),
            Row("PAYMENTS_SIGNING_KEY", "pay_demo_signing_0001", 33, "Iris Navarro", Development, Staging),
            Secret("PAYMENTS_SIGNING_KEY", "0d7e58", 33, "Iris Navarro", Production),
            Secret("OBJECT_STORE_SECRET", "e2b947", 50, "Jonas Lindqvist", Production),
        ];
    }

    /// <summary>Fills <paramref name="block"/> with the project, the environments and the rows.</summary>
    public static void Fill(EnvVariables block)
    {
        block.ProjectName = Project;
        block.Environments.AddRange(Environments());
        block.Variables.AddRange(Variables());
        block.SampleDotenv = SampleDotenv;
    }
}
