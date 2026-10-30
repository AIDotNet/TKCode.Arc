using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// A call a <see cref="MeetingSummary"/> plays without a summarizer, from data you give it: the notes, the model credited
/// and how simulated sends are numbered. Set it as <see cref="MeetingSummary.Simulation"/> while the card has no
/// <see cref="MeetingSummary.Meeting"/> and no <see cref="MeetingSummary.Summarizer"/>: when the card scrolls into view
/// it plays its summarizing pass, streams the notes in and, unless someone uses the card first, ticks off the second
/// action item and sends the first one. Sends are simulated and announce that nothing was sent.
/// </summary>
public sealed class MeetingSummarySimulation
{
    /// <summary>The notes of the call.</summary>
    public MeetingSummaryData? Meeting { get; set; }

    /// <summary>The model credited in the footer when the card has no <see cref="MeetingSummary.Model"/>.</summary>
    public AgentModel? Model { get; set; }

    /// <summary>
    /// The reference a simulated send hands out, per <see cref="MeetingSummarySendTarget.Id"/>: a format string whose
    /// <c>{0}</c> is a running number ("ISS-{0}"). A target without one shows "Sent".
    /// </summary>
    public IReadOnlyDictionary<string, string>? ReferenceFormats { get; set; }

    /// <summary>The number of the first simulated send (1 by default); each send takes the next.</summary>
    public int FirstReferenceNumber { get; set; } = 1;

    /// <summary>The reference for a simulated send to <paramref name="target"/> numbered <paramref name="number"/>, or null.</summary>
    internal string? Reference(MeetingSummarySendTarget target, int number) =>
        ReferenceFormats is { } formats && formats.TryGetValue(target.Id, out var format) && !string.IsNullOrEmpty(format)
            ? string.Format(CultureInfo.InvariantCulture, format, number)
            : null;
}
