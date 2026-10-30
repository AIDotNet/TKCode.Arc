namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// Invented referral programs for the demos (people and addresses are made up; example.com): the program the preview plays
/// (a friend who signed up subscribes, an invited one signs up and subscribes, and the fifth paid referral unlocks the
/// milestone), a program past its goal and one halfway. The reward and the milestone belong to an invented product.
/// </summary>
internal static class ReferralCardDemoData
{
    /// <summary>The invite link of the preview's program.</summary>
    public const string Link = "https://app.example.com/r/emma20";

    /// <summary>The credit each side gets, in dollars.</summary>
    public const int Reward = 20;

    /// <summary>Five paid referrals unlock three free months.</summary>
    public static ReferralMilestone Milestone { get; } = new()
    {
        Goal = 5,
        Reward = "3 months free",
        Detail = "Applied to your next invoice"
    };

    /// <summary>The preview's program at the start, newest first: three paid, one signed up, two invited.</summary>
    public static List<Referral> Program(DateTimeOffset now) =>
    [
        new() { Id = "p6", Email = "jonas.weber@example.com", Stage = ReferralStage.Invited, InvitedAt = now.AddDays(-1) },
        new() { Id = "p5", Email = "ana.souza@example.com", Stage = ReferralStage.Invited, InvitedAt = now.AddDays(-3) },
        new() { Id = "p4", Name = "Priya Raman", Email = "priya.raman@example.com", Stage = ReferralStage.SignedUp, InvitedAt = now.AddDays(-6) },
        new() { Id = "p3", Name = "Leo Martins", Email = "leo.martins@example.com", Stage = ReferralStage.Earned, InvitedAt = now.AddDays(-12) },
        new() { Id = "p2", Name = "Hana Sato", Email = "hana.sato@example.com", Stage = ReferralStage.Earned, InvitedAt = now.AddDays(-19) },
        new() { Id = "p1", Name = "Marcus Bell", Email = "marcus.bell@example.com", Stage = ReferralStage.Earned, InvitedAt = now.AddDays(-27) },
    ];

    /// <summary>The preview's program as of when the gallery first showed it (the narrow demo shows it without playing).</summary>
    public static IReadOnlyList<Referral> StartingProgram { get; } = Program(DateTimeOffset.Now);

    /// <summary>The beats the preview plays: Priya subscribes, then Ana signs up and subscribes (the fifth paid referral).</summary>
    public static IReadOnlyList<ReferralCardSimulationBeat> Beats { get; } =
    [
        new(TimeSpan.FromSeconds(1.4), "p4", ReferralStage.Earned),
        new(TimeSpan.FromSeconds(2.2), "p5", ReferralStage.SignedUp, "Ana Souza"),
        new(TimeSpan.FromSeconds(2.4), "p5", ReferralStage.Earned)
    ];

    /// <summary>A program past its goal of five paid referrals.</summary>
    public static List<Referral> Unlocked(DateTimeOffset now) =>
    [
        new() { Id = "u7", Email = "olivia.park@example.com", Stage = ReferralStage.Invited, InvitedAt = now.AddDays(-1) },
        new() { Id = "u6", Name = "Tomás Rivera", Email = "tomas.rivera@example.com", Stage = ReferralStage.Earned, InvitedAt = now.AddDays(-4) },
        new() { Id = "u5", Name = "Ines Laurent", Email = "ines.laurent@example.com", Stage = ReferralStage.Earned, InvitedAt = now.AddDays(-8) },
        new() { Id = "u4", Name = "Kofi Mensah", Email = "kofi.mensah@example.com", Stage = ReferralStage.Earned, InvitedAt = now.AddDays(-11) },
        new() { Id = "u3", Name = "Mei Lin", Email = "mei.lin@example.com", Stage = ReferralStage.Earned, InvitedAt = now.AddDays(-15) },
        new() { Id = "u2", Name = "Noah Fischer", Email = "noah.fischer@example.com", Stage = ReferralStage.Earned, InvitedAt = now.AddDays(-21) },
        new() { Id = "u1", Name = "Sara Haddad", Email = "sara.haddad@example.com", Stage = ReferralStage.SignedUp, InvitedAt = now.AddDays(-26) },
    ];

    /// <summary>A program halfway: two paid, one signed up, two invited.</summary>
    public static List<Referral> Halfway(DateTimeOffset now) =>
    [
        new() { Id = "h5", Email = "dmitri.volkov@example.com", Stage = ReferralStage.Invited, InvitedAt = now.AddDays(-2) },
        new() { Id = "h4", Email = "a.very.long.address.for.a.friend@marketing.example.com", Stage = ReferralStage.Invited, InvitedAt = now.AddDays(-3) },
        new() { Id = "h3", Name = "Grace Okafor", Email = "grace.okafor@example.com", Stage = ReferralStage.SignedUp, InvitedAt = now.AddDays(-7) },
        new() { Id = "h2", Name = "Luca Bianchi", Email = "luca.bianchi@example.com", Stage = ReferralStage.Earned, InvitedAt = now.AddDays(-13) },
        new() { Id = "h1", Name = "Amara Diallo", Email = "amara.diallo@example.com", Stage = ReferralStage.Earned, InvitedAt = now.AddDays(-20) },
    ];
}
