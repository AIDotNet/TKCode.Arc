namespace TKCode.Arc;

/// <summary>
/// Invented sample content for <see cref="VoiceRecorder"/>: a short thread with a neighbour about a garden fence (14, 6 and
/// 9 second messages, as in the reference preview) and a longer one for scrolling. No real people.
/// </summary>
public static class VoiceRecorderSamples
{
    /// <summary>The other person in <see cref="Thread"/>.</summary>
    public const string ContactName = "Tomas Novak";

    /// <summary>What the thread is about (the header's second line).</summary>
    public const string Topic = "Garden fence";

    /// <summary>The recorder's accessible name for <see cref="Thread"/>.</summary>
    public const string ThreadLabel = "Voice messages with " + ContactName;

    /// <summary>
    /// The three-message thread: theirs (14 s, two stretches of talk around a pause), yours (6 s, mostly quiet) and theirs
    /// (9 s, loud at first, then trailing off). A new list every call.
    /// </summary>
    public static List<VoiceRecorderMessage> Thread() =>
    [
        new()
        {
            Id = "fence-1",
            From = ContactName,
            Duration = TimeSpan.FromSeconds(14),
            Meta = "10:12 AM",
            Peaks =
            [
                0.12, 0.15, 0.22, 0.29, 0.34, 0.48, 0.62, 0.57, 0.71, 0.66, 0.44, 0.12, 0.13, 0.79, 0.86, 0.74,
                0.83, 0.68, 0.51, 0.36, 0.12, 0.12, 0.12, 0.12, 0.12, 0.12, 0.24, 0.37, 0.49, 0.61, 0.42, 0.18,
            ],
        },
        new()
        {
            Id = "fence-2",
            Duration = TimeSpan.FromSeconds(6),
            Meta = "10:15 AM",
            Peaks =
            [
                0.12, 0.12, 0.14, 0.12, 0.12, 0.21, 0.18, 0.12, 0.12, 0.12, 0.16, 0.25, 0.13, 0.12, 0.12, 0.12,
                0.19, 0.12, 0.12, 0.14, 0.12, 0.39,
            ],
        },
        new()
        {
            Id = "fence-3",
            From = ContactName,
            Duration = TimeSpan.FromSeconds(9),
            Meta = "10:16 AM",
            Peaks =
            [
                0.12, 0.13, 0.77, 0.62, 0.85, 0.73, 0.66, 0.58, 0.49, 0.44, 0.36, 0.29, 0.23, 0.12, 0.12, 0.12,
                0.12, 0.12, 0.12, 0.18, 0.27, 0.34, 0.22, 0.31, 0.37, 0.20,
            ],
        },
    ];

    /// <summary>
    /// A longer thread with two people taking turns (waveforms from <see cref="VoiceRecorderPeaks.Generate"/>), for
    /// scrolling: <paramref name="count"/> messages, a minute apart from 8:40 AM.
    /// </summary>
    public static List<VoiceRecorderMessage> LongThread(int count = 12)
    {
        var lengths = new[] { 7.0, 12, 4, 18, 9, 3, 15, 6, 11, 5, 21, 8 };
        var list = new List<VoiceRecorderMessage>(count);
        for (var i = 0; i < count; i++)
        {
            var seconds = lengths[i % lengths.Length];
            var minute = 40 + i;
            var meta = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{8 + minute / 60}:{minute % 60:00} AM");
            list.Add(new VoiceRecorderMessage
            {
                Id = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"long-{i + 1}"),
                From = i % 2 == 0 ? "Ines Okafor" : null,
                Duration = TimeSpan.FromSeconds(seconds),
                Peaks = VoiceRecorderPeaks.Generate(100 + i, seconds),
                Meta = meta,
            });
        }
        return list;
    }
}
