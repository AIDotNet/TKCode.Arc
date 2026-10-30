namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The invented SaaS feed the SocialProofToast demos simulate: people and companies that do not exist, in invented places,
/// with times measured back from the start of each pass. Each access returns fresh events, so every stack gets its own.
/// </summary>
internal static class SocialProofToastDemoData
{
    /// <summary>Ten events, oldest first.</summary>
    public static IReadOnlyList<SocialProofToastSimulationEvent> Events =>
    [
        Person("s1", "Sofia", "started a Pro trial", "Madrid", TimeSpan.FromMinutes(14)),
        Company("s2", "Contoso Labs", "upgraded to Business", "48 seats", TimeSpan.FromMinutes(12), "Verified purchase"),
        Person("s3", "Mateo", "bought the Team plan", "Lisbon", TimeSpan.FromMinutes(11), "Verified purchase"),
        Company("s4", "Northwind AI", "started a Business trial", "12 seats", TimeSpan.FromMinutes(9)),
        Person("s5", "Amara", "upgraded to Pro", "Nairobi", TimeSpan.FromMinutes(7)),
        Company("s6", "Fabrikam", "renewed Enterprise", "120 seats", TimeSpan.FromMinutes(6), "Verified purchase"),
        Person("s7", "Lena", "started a Pro trial", "Hamburg", TimeSpan.FromMinutes(4)),
        Company("s8", "Harbor & Pine", "upgraded to Business", "26 seats", TimeSpan.FromMinutes(3), "Verified purchase"),
        Person("s9", "Kenji", "switched to yearly billing", "Osaka", TimeSpan.FromMinutes(2)),
        Person("s10", "Priya", "started a Pro trial", "Pune", TimeSpan.FromSeconds(20)),
    ];

    private static SocialProofToastSimulationEvent Person(string id, string name, string action, string meta, TimeSpan ago, string? verified = null) =>
        new() { Event = new SocialProofEvent(id, name, action) { Meta = meta, VerifiedLabel = verified }, Ago = ago };

    private static SocialProofToastSimulationEvent Company(string id, string name, string action, string meta, TimeSpan ago, string? verified = null) =>
        new()
        {
            Event = new SocialProofEvent(id, name, action) { Meta = meta, VerifiedLabel = verified, Actor = SocialProofActor.Company },
            Ago = ago,
        };
}
