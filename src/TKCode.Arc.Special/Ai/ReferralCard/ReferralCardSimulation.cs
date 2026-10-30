namespace TKCode.Arc;

/// <summary>
/// One beat of a <see cref="ReferralCardSimulation"/>: after <see cref="Delay"/>, the referral <see cref="Id"/> moves to
/// <see cref="Stage"/> (taking <see cref="Name"/> when it has none yet, as a friend who signs up does).
/// </summary>
public sealed class ReferralCardSimulationBeat
{
    /// <summary>Creates an empty beat; set <see cref="Id"/> and <see cref="Stage"/>.</summary>
    public ReferralCardSimulationBeat()
    {
    }

    /// <summary>Creates a beat.</summary>
    /// <param name="delay">The pause before it, at full speed.</param>
    /// <param name="id">The referral it moves (<see cref="Referral.Id"/>).</param>
    /// <param name="stage">The stage it moves to.</param>
    /// <param name="name">The name the friend signs up with, or null.</param>
    public ReferralCardSimulationBeat(TimeSpan delay, string id, ReferralStage stage, string? name = null)
    {
        Delay = delay;
        Id = id;
        Stage = stage;
        Name = name;
    }

    /// <summary>The pause before the beat, at full speed (reduced motion halves it).</summary>
    public TimeSpan Delay { get; set; }

    /// <summary>The referral the beat moves.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The stage it moves to.</summary>
    public ReferralStage Stage { get; set; }

    /// <summary>The name the friend takes when the referral has none yet (a friend who signs up), or null.</summary>
    public string? Name { get; set; }

    /// <inheritdoc />
    public override string ToString() => $"{Id} → {Stage}";
}

/// <summary>
/// A referral program for a <see cref="ReferralCard"/> to play, written by your app (nothing ships with the package): the
/// list it starts from and the beats that move friends through the stages. Give it to
/// <see cref="ReferralCard.Simulation"/>; while the card has no referrals of its own it shows this list and plays
/// <see cref="Beats"/> once when it is 40% on screen (twice as fast under reduced motion), and moves the first address of
/// each simulated send through the stages. Data only; the card keeps the playback state.
/// </summary>
public sealed class ReferralCardSimulation
{
    /// <summary>Everyone invited when the program starts, newest first. Empty by default.</summary>
    public IReadOnlyList<Referral> Referrals { get; set; } = [];

    /// <summary>The beats, in order. Empty by default (the program shows and does not move).</summary>
    public IReadOnlyList<ReferralCardSimulationBeat> Beats { get; set; } = [];
}
