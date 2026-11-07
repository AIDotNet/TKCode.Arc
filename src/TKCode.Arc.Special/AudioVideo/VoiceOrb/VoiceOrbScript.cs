namespace TKCode.Arc;

/// <summary>One exchange of a <see cref="VoiceOrbScript"/>: what the user says and what the assistant answers.</summary>
/// <param name="User">The user's sentence, revealed word by word while the orb listens.</param>
/// <param name="Assistant">The assistant's answer, revealed word by word while the orb speaks.</param>
public sealed record VoiceOrbScriptTurn(string User, string Assistant);

/// <summary>
/// The conversation a <see cref="VoiceOrbSession"/> plays: a hint shown before the session, the exchanges it loops
/// through, and the line shown when the call ends.
/// </summary>
public sealed class VoiceOrbScript
{
    /// <summary>Creates a script with <paramref name="intro"/> and <paramref name="turns"/>.</summary>
    public VoiceOrbScript(string intro, IReadOnlyList<VoiceOrbScriptTurn> turns)
    {
        Intro = intro;
        Turns = turns;
    }

    /// <summary>The system line shown before a session starts.</summary>
    public string Intro { get; }

    /// <summary>The exchanges, played in order and then from the start again.</summary>
    public IReadOnlyList<VoiceOrbScriptTurn> Turns { get; }

    /// <summary>The system line shown when the call ends, from its length. The default says "Call ended after 1:07."</summary>
    public Func<TimeSpan, string> Ended { get; init; } = d => $"Call ended after {VoiceControlsLabels.FormatClock(d)}.";
}

/// <summary>Invented sample content for the voice orb demos: a short, looping weekend-trip conversation.</summary>
public static class VoiceOrbSamples
{
    /// <summary>
    /// Three exchanges about a weekend trip (user sentences of 11, 9 and 10 words, answers of 12, 12 and 11 words), with
    /// a system hint before the session starts. Names and places are fictional.
    /// </summary>
    public static VoiceOrbScript WeekendTrip { get; } = new(
        "Tap the orb and plan a weekend trip.",
        [
            new VoiceOrbScriptTurn(
                "Could you book a table for four near the harbor tonight?",
                "Booked. A table for four at 7:45, and Priya has the details."),
            new VoiceOrbScriptTurn(
                "What is the weather going to be like there?",
                "Mild and clear, 18 degrees. A light jacket should do after sunset."),
            new VoiceOrbScriptTurn(
                "Remind me to pick up the tickets before we go.",
                "Sure. I'll remind you at six to pick up the tickets.")
        ]);
}
