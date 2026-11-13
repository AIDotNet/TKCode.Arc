using System.Globalization;

namespace TKCode.Arc;

/// <summary>A topic's stretches merged under one name, with its time and share of the call.</summary>
internal sealed record CallInsightsTopicSummary(string Name, IReadOnlyList<CallInsightsTopic> Stretches, TimeSpan Time, double Share, int ColorIndex);

/// <summary>
/// Everything <see cref="CallInsights"/> works out from its data, once per change: the sorted transcript, the length, the
/// talk-time split, the topics merged by name, the sentiment sampled along the call and the search matches. Pure: no UI.
/// </summary>
internal sealed class CallInsightsModel
{
    /// <summary>How many points the sentiment curve is sampled at, whatever the width.</summary>
    public const int SentimentSamples = 96;

    public CallInsightsModel(IReadOnlyList<CallInsightsSpeaker> speakers, IEnumerable<CallInsightsUtterance> utterances,
        IEnumerable<CallInsightsMoment> moments, IEnumerable<CallInsightsTopic> topics)
    {
        Utterances = utterances.Where(u => u.End >= u.Start).OrderBy(u => u.Start).ToArray();
        Moments = moments.OrderBy(m => m.Time).ToArray();
        var topicList = topics.Where(t => t.End >= t.Start && !string.IsNullOrWhiteSpace(t.Name)).ToArray();

        // Speakers: the given ones in order, then any key the transcript uses that has no speaker.
        var list = speakers.ToList();
        foreach (var key in Utterances.Select(u => u.SpeakerKey).Distinct())
        {
            if (list.All(s => s.Key != key))
            {
                list.Add(new CallInsightsSpeaker(key, key));
            }
        }
        Speakers = list;
        SpeakerIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < list.Count; i++)
        {
            SpeakerIndex.TryAdd(list[i].Key, i);
        }

        var end = TimeSpan.Zero;
        foreach (var u in Utterances)
        {
            end = u.End > end ? u.End : end;
        }
        foreach (var m in Moments)
        {
            end = m.Time > end ? m.Time : end;
        }
        foreach (var t in topicList)
        {
            end = t.End > end ? t.End : end;
        }
        Duration = end;

        TalkShares = ComputeShares();
        Topics = ComputeTopics(topicList);
        OverallSentiment = ComputeOverall();
        Sentiment = SampleSentiment();
    }

    public IReadOnlyList<CallInsightsSpeaker> Speakers { get; }

    public Dictionary<string, int> SpeakerIndex { get; }

    public CallInsightsUtterance[] Utterances { get; }

    public CallInsightsMoment[] Moments { get; }

    public IReadOnlyList<CallInsightsTopicSummary> Topics { get; }

    public IReadOnlyList<CallInsightsTalkShare> TalkShares { get; }

    public TimeSpan Duration { get; }

    public double OverallSentiment { get; }

    /// <summary>The sentiment at <see cref="SentimentSamples"/> evenly spaced points from the start to the end, each −1 to 1.</summary>
    public double[] Sentiment { get; }

    public int IndexOfSpeaker(string key) => SpeakerIndex.TryGetValue(key, out var index) ? index : 0;

    public CallInsightsSpeaker? SpeakerOf(CallInsightsUtterance utterance)
        => SpeakerIndex.TryGetValue(utterance.SpeakerKey, out var index) ? Speakers[index] : null;

    /// <summary>The index of the utterance playing at <paramref name="time"/>, or −1 between utterances.</summary>
    public int UtteranceAt(TimeSpan time)
    {
        // Binary search for the last start at or before the time.
        int lo = 0, hi = Utterances.Length - 1, found = -1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (Utterances[mid].Start <= time)
            {
                found = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }
        if (found >= 0 && Utterances[found].End >= time)
        {
            return found;
        }
        return -1;
    }

    /// <summary>The index of the last utterance that started at or before <paramref name="time"/> (−1 before the first).</summary>
    public int UtteranceStartedBy(TimeSpan time)
    {
        var index = -1;
        for (var i = 0; i < Utterances.Length && Utterances[i].Start <= time; i++)
        {
            index = i;
        }
        return index;
    }

    /// <summary>The utterances whose text contains <paramref name="query"/> (ignoring case), in order; all of them when it is empty.</summary>
    public IReadOnlyList<CallInsightsUtterance> Matches(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Utterances;
        }
        var q = query.Trim();
        var result = new List<CallInsightsUtterance>();
        foreach (var u in Utterances)
        {
            if (u.Text.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                || (SpeakerOf(u)?.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase) ?? false))
            {
                result.Add(u);
            }
        }
        return result;
    }

    /// <summary>The next (or previous) moment strictly after (before) <paramref name="time"/>, or null.</summary>
    public CallInsightsMoment? MomentAfter(TimeSpan time, bool forward)
    {
        if (forward)
        {
            return Moments.FirstOrDefault(m => m.Time > time + TimeSpan.FromMilliseconds(500));
        }
        return Moments.LastOrDefault(m => m.Time < time - TimeSpan.FromMilliseconds(500));
    }

    public static string FormatTime(TimeSpan time)
    {
        var total = Math.Max(0, (int)Math.Floor(time.TotalSeconds));
        return total >= 3600
            ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", total / 3600, total / 60 % 60, total % 60)
            : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", total / 60, total % 60);
    }

    public static string FormatShare(double share) => share.ToString("P0", CultureInfo.CurrentCulture);

    private IReadOnlyList<CallInsightsTalkShare> ComputeShares()
    {
        var talk = new double[Speakers.Count];
        var longest = new double[Speakers.Count];
        var questions = new int[Speakers.Count];
        var runSpeaker = -1;
        var runLength = 0.0;
        foreach (var u in Utterances)
        {
            var index = IndexOfSpeaker(u.SpeakerKey);
            var seconds = (u.End - u.Start).TotalSeconds;
            talk[index] += seconds;
            if (u.Text.TrimEnd().EndsWith('?'))
            {
                questions[index]++;
            }
            runLength = index == runSpeaker ? runLength + seconds : seconds;
            runSpeaker = index;
            longest[index] = Math.Max(longest[index], runLength);
        }
        var total = talk.Sum();
        var shares = new List<CallInsightsTalkShare>(Speakers.Count);
        for (var i = 0; i < Speakers.Count; i++)
        {
            shares.Add(new CallInsightsTalkShare(Speakers[i], TimeSpan.FromSeconds(talk[i]), total > 0 ? talk[i] / total : 0,
                TimeSpan.FromSeconds(longest[i]), questions[i]));
        }
        return shares;
    }

    private IReadOnlyList<CallInsightsTopicSummary> ComputeTopics(CallInsightsTopic[] topics)
    {
        var result = new List<CallInsightsTopicSummary>();
        var duration = Duration.TotalSeconds;
        foreach (var group in topics.GroupBy(t => t.Name.Trim(), StringComparer.CurrentCultureIgnoreCase))
        {
            var stretches = group.OrderBy(t => t.Start).ToArray();
            // Overlapping stretches of one topic count once.
            var seconds = 0.0;
            var coveredUntil = TimeSpan.MinValue;
            foreach (var s in stretches)
            {
                var start = s.Start > coveredUntil ? s.Start : coveredUntil;
                if (s.End > start)
                {
                    seconds += (s.End - start).TotalSeconds;
                }
                coveredUntil = s.End > coveredUntil ? s.End : coveredUntil;
            }
            result.Add(new CallInsightsTopicSummary(group.Key, stretches, TimeSpan.FromSeconds(seconds),
                duration > 0 ? seconds / duration : 0, result.Count));
        }
        return result;
    }

    private double ComputeOverall()
    {
        double weighted = 0, weight = 0;
        foreach (var u in Utterances)
        {
            var seconds = Math.Max(1, (u.End - u.Start).TotalSeconds);
            weighted += Math.Clamp(u.Sentiment, -1, 1) * seconds;
            weight += seconds;
        }
        return weight > 0 ? weighted / weight : 0;
    }

    /// <summary>
    /// Samples the sentiment along the call: each point averages the utterances within a window around it (weighted by
    /// how close they are), so the curve reads as a mood, not a step for every line. Silence keeps the last mood.
    /// </summary>
    private double[] SampleSentiment()
    {
        var samples = new double[SentimentSamples];
        if (Utterances.Length == 0 || Duration <= TimeSpan.Zero)
        {
            return samples;
        }
        var duration = Duration.TotalSeconds;
        var window = Math.Max(30, duration / 14);
        var last = 0.0;
        for (var i = 0; i < SentimentSamples; i++)
        {
            var t = duration * i / (SentimentSamples - 1);
            double sum = 0, weights = 0;
            foreach (var u in Utterances)
            {
                var mid = (u.Start.TotalSeconds + u.End.TotalSeconds) / 2;
                var distance = Math.Abs(mid - t);
                if (distance > window)
                {
                    continue;
                }
                var w = (1 - distance / window) * Math.Max(1, (u.End - u.Start).TotalSeconds);
                sum += Math.Clamp(u.Sentiment, -1, 1) * w;
                weights += w;
            }
            last = weights > 0 ? sum / weights : last;
            samples[i] = last;
        }
        return samples;
    }
}
