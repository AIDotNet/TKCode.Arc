using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>A person on a recorded call: who they are and which side of the deal they are on.</summary>
public sealed class CallInsightsSpeaker
{
    /// <summary>Creates an empty speaker; set <see cref="Key"/> and <see cref="Name"/>.</summary>
    public CallInsightsSpeaker()
    {
    }

    /// <summary>Creates a speaker with a key, a name, an optional role and the side they are on.</summary>
    public CallInsightsSpeaker(string key, string name, string? role = null, bool isHost = false)
    {
        Key = key;
        Name = name;
        Role = role;
        IsHost = isHost;
    }

    /// <summary>The key that utterances use to name this speaker.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The name shown in the transcript and the talk-time list.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>A short role under the name ("Account executive", "Head of operations"); null leaves it out.</summary>
    public string? Role { get; set; }

    /// <summary>True for your own side of the call (the rep); questions asked are counted for this side.</summary>
    public bool IsHost { get; set; }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>One stretch of speech in the transcript: who spoke, when, what they said and how it sounded.</summary>
public sealed class CallInsightsUtterance
{
    /// <summary>Creates an empty utterance.</summary>
    public CallInsightsUtterance()
    {
    }

    /// <summary>Creates an utterance from a speaker key, a start and an end, the text and a sentiment from −1 to 1.</summary>
    public CallInsightsUtterance(string speakerKey, TimeSpan start, TimeSpan end, string text, double sentiment = 0)
    {
        SpeakerKey = speakerKey;
        Start = start;
        End = end;
        Text = text;
        Sentiment = sentiment;
    }

    /// <summary>The <see cref="CallInsightsSpeaker.Key"/> of the person speaking.</summary>
    public string SpeakerKey { get; set; } = string.Empty;

    /// <summary>When the utterance starts, from the beginning of the recording.</summary>
    public TimeSpan Start { get; set; }

    /// <summary>When the utterance ends; the talk time counts from <see cref="Start"/> to here.</summary>
    public TimeSpan End { get; set; }

    /// <summary>What was said.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>How the utterance sounded, from −1 (negative) through 0 (neutral) to 1 (positive).</summary>
    public double Sentiment { get; set; }

    /// <inheritdoc />
    public override string ToString() => Text;
}

/// <summary>What kind of key moment a <see cref="CallInsightsMoment"/> marks; each kind has its own mark on the timeline.</summary>
public enum CallInsightsMomentKind
{
    /// <summary>A question that matters for the deal.</summary>
    Question,

    /// <summary>A concern or pushback from the other side.</summary>
    Objection,

    /// <summary>Price, budget or terms came up.</summary>
    Pricing,

    /// <summary>Another vendor was named.</summary>
    Competitor,

    /// <summary>Someone agreed to do something.</summary>
    Commitment,
}

/// <summary>A key moment of the call, pinned to a time on the timeline.</summary>
public sealed class CallInsightsMoment
{
    /// <summary>Creates an empty moment.</summary>
    public CallInsightsMoment()
    {
    }

    /// <summary>Creates a moment at a time, of a kind, with a short title.</summary>
    public CallInsightsMoment(TimeSpan time, CallInsightsMomentKind kind, string title)
    {
        Time = time;
        Kind = kind;
        Title = title;
    }

    /// <summary>Where the moment happens in the recording.</summary>
    public TimeSpan Time { get; set; }

    /// <summary>What kind of moment it is.</summary>
    public CallInsightsMomentKind Kind { get; set; }

    /// <summary>A short line that says what happened ("Asked about the rollout plan").</summary>
    public string Title { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string ToString() => Title;
}

/// <summary>A stretch of the call spent on one topic. Several stretches may share a name; their time adds up.</summary>
public sealed class CallInsightsTopic
{
    /// <summary>Creates an empty topic stretch.</summary>
    public CallInsightsTopic()
    {
    }

    /// <summary>Creates a topic stretch from a name, a start and an end.</summary>
    public CallInsightsTopic(string name, TimeSpan start, TimeSpan end)
    {
        Name = name;
        Start = start;
        End = end;
    }

    /// <summary>The topic's name; stretches with the same name form one chip.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>When the stretch starts.</summary>
    public TimeSpan Start { get; set; }

    /// <summary>When the stretch ends.</summary>
    public TimeSpan End { get; set; }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>A follow-up agreed on the call, with an owner and a due date, that can be ticked off.</summary>
public sealed class CallInsightsNextStep
{
    /// <summary>Creates an empty next step.</summary>
    public CallInsightsNextStep()
    {
    }

    /// <summary>Creates a next step with its text, an optional owner and an optional due text.</summary>
    public CallInsightsNextStep(string text, string? owner = null, string? due = null, bool isDone = false)
    {
        Text = text;
        Owner = owner;
        Due = due;
        IsDone = isDone;
    }

    /// <summary>What has to happen.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Who does it; null leaves it out.</summary>
    public string? Owner { get; set; }

    /// <summary>When it is due, as shown ("Fri", "Mar 14"); null leaves it out.</summary>
    public string? Due { get; set; }

    /// <summary>Whether it is done; the block writes it when the step is ticked.</summary>
    public bool IsDone { get; set; }

    /// <inheritdoc />
    public override string ToString() => Text;
}

/// <summary>A speaker's share of the call, worked out from the utterances.</summary>
/// <param name="Speaker">The speaker.</param>
/// <param name="TalkTime">How long they spoke in total.</param>
/// <param name="Share">Their share of all talk time, from 0 to 1.</param>
/// <param name="LongestMonologue">Their longest run of speech without anyone else speaking.</param>
/// <param name="Questions">How many of their utterances end in a question mark.</param>
public sealed record CallInsightsTalkShare(CallInsightsSpeaker Speaker, TimeSpan TalkTime, double Share, TimeSpan LongestMonologue, int Questions);

/// <summary>Arguments of <see cref="CallInsights.NextStepToggled"/>.</summary>
public sealed class CallInsightsNextStepToggledEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CallInsightsNextStepToggledEventArgs(RoutedEvent routedEvent, CallInsightsNextStep step, bool isDone)
        : base(routedEvent)
    {
        Step = step;
        IsDone = isDone;
    }

    /// <summary>The step that was ticked or cleared.</summary>
    public CallInsightsNextStep Step { get; }

    /// <summary>Whether it is done now.</summary>
    public bool IsDone { get; }
}

/// <summary>Arguments of <see cref="CallInsights.Seeked"/>.</summary>
public sealed class CallInsightsSeekedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CallInsightsSeekedEventArgs(RoutedEvent routedEvent, TimeSpan position, CallInsightsUtterance? utterance)
        : base(routedEvent)
    {
        Position = position;
        Utterance = utterance;
    }

    /// <summary>The new position in the recording.</summary>
    public TimeSpan Position { get; }

    /// <summary>The utterance at that position, if any.</summary>
    public CallInsightsUtterance? Utterance { get; }
}

/// <summary>The words <see cref="CallInsights"/> shows and reads out; every one has an English default.</summary>
public sealed class CallInsightsLabels
{
    /// <summary>The play button's screen reader name.</summary>
    public string Play { get; init; } = "Play";

    /// <summary>The pause button's screen reader name.</summary>
    public string Pause { get; init; } = "Pause";

    /// <summary>The playback speed button's screen reader name.</summary>
    public string Speed { get; init; } = "Playback speed";

    /// <summary>The screen reader name of the timeline.</summary>
    public string Timeline { get; init; } = "Call timeline";

    /// <summary>The heading of the transcript.</summary>
    public string Transcript { get; init; } = "Transcript";

    /// <summary>The placeholder of the transcript search.</summary>
    public string Search { get; init; } = "Search the transcript";

    /// <summary>The line shown when the search matches nothing.</summary>
    public string NoMatches { get; init; } = "Nothing in the transcript matches.";

    /// <summary>The count of search matches; {0} is the number.</summary>
    public string MatchesFormat { get; init; } = "{0} matches";

    /// <summary>The heading of the talk-time section.</summary>
    public string TalkTime { get; init; } = "Talk time";

    /// <summary>The longest monologue line; {0} is the duration.</summary>
    public string LongestFormat { get; init; } = "Longest {0}";

    /// <summary>The questions line; {0} is the count.</summary>
    public string QuestionsFormat { get; init; } = "{0} questions";

    /// <summary>The heading of the topics section.</summary>
    public string Topics { get; init; } = "Topics";

    /// <summary>The heading of the key moments section.</summary>
    public string Moments { get; init; } = "Key moments";

    /// <summary>The heading of the next steps section.</summary>
    public string NextSteps { get; init; } = "Next steps";

    /// <summary>The heading of the sentiment track.</summary>
    public string Sentiment { get; init; } = "Sentiment";

    /// <summary>The overall sentiment when it is above 0.2.</summary>
    public string Positive { get; init; } = "Positive";

    /// <summary>The overall sentiment between −0.2 and 0.2.</summary>
    public string Neutral { get; init; } = "Neutral";

    /// <summary>The overall sentiment below −0.2.</summary>
    public string Negative { get; init; } = "Negative";

    /// <summary>The moment kinds' names, in the order of <see cref="CallInsightsMomentKind"/>.</summary>
    public IReadOnlyList<string> MomentKinds { get; init; } = ["Question", "Objection", "Pricing", "Competitor", "Commitment"];

    /// <summary>The announcement after a seek; {0} is the time, {1} the speaker, {2} what they said.</summary>
    public string SeekFormat { get; init; } = "{0}, {1}: {2}";

    /// <summary>The announcement when a topic is chosen; {0} is the topic, {1} its share of the call.</summary>
    public string TopicFormat { get; init; } = "{0}, {1} of the call";
}
