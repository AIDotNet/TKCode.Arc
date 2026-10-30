namespace TKCode.Arc;

/// <summary>One entry of a <see cref="SocialProofToastSimulation"/>: an event and how long before each pass it happened.</summary>
public sealed class SocialProofToastSimulationEvent
{
    /// <summary>What happened and who did it. Its <see cref="SocialProofEvent.Time"/> is replaced by now minus <see cref="Ago"/> on every pass.</summary>
    public SocialProofEvent? Event { get; set; }

    /// <summary>How long before the pass started it happened, so the feed reads "14 min ago" on every pass instead of drifting.</summary>
    public TimeSpan Ago { get; set; }
}

/// <summary>
/// A simulated feed for a <see cref="SocialProofToast"/> that has no <see cref="SocialProofToast.Events"/>, for demos and
/// prototypes: the events it plays, oldest first, with their times measured back from the start of each pass. The stack
/// plays it like a feed of its own (prefill, interval, limit and loop included). Create one per stack.
/// </summary>
public sealed class SocialProofToastSimulation
{
    /// <summary>The events, oldest first.</summary>
    public IReadOnlyList<SocialProofToastSimulationEvent> Events { get; set; } = [];

    /// <summary>Copies of the events with their times measured back from <paramref name="now"/>, oldest first.</summary>
    internal SocialProofEvent[] Pass(DateTimeOffset now)
    {
        var events = new List<SocialProofEvent>(Events.Count);
        foreach (var entry in Events)
        {
            if (entry.Event is not { } source)
            {
                continue;
            }
            events.Add(new SocialProofEvent(source.Id, source.Name, source.Action)
            {
                Meta = source.Meta,
                Time = now - entry.Ago,
                TimeText = source.TimeText,
                Actor = source.Actor,
                Portrait = source.Portrait,
                Logo = source.Logo,
                IsVerified = source.IsVerified,
                VerifiedLabel = source.VerifiedLabel,
            });
        }
        return [.. events];
    }
}
