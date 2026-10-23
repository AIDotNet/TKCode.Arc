using System;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="AiAnswer"/> shows or announces, in English by default. Replace the instance to translate or
/// reword; formatted texts are functions so that word order can change.
/// </summary>
public sealed class AiAnswerLabels
{
    /// <summary>The accessible name of the whole block.</summary>
    public string Answer { get; init; } = "AI answer";

    /// <summary>The new thread button, its tooltip, and the title of an empty page.</summary>
    public string NewThread { get; init; } = "New thread";

    /// <summary>The share button of the top bar: its accessible name and tooltip.</summary>
    public string ShareThread { get; init; } = "Share thread";

    /// <summary>The share button's visible label.</summary>
    public string Share { get; init; } = "Share";

    /// <summary>What the share button shows for 1.6 seconds after copying.</summary>
    public string Copied { get; init; } = "Copied";

    /// <summary>What the share button shows for 1.6 seconds when the link could not be copied.</summary>
    public string CopyFailed { get; init; } = "Failed";

    /// <summary>The heading of the empty page.</summary>
    public string EmptyTitle { get; init; } = "Ask a question";

    /// <summary>The line under it.</summary>
    public string EmptyDescription { get; init; } = "Get one answer with its sources. Point at any citation to see where it came from.";

    /// <summary>The accessible name of the empty page's list of suggested questions.</summary>
    public string Suggestions { get; init; } = "Suggested questions";

    /// <summary>The placeholder of the follow-up box on an answer.</summary>
    public string FollowUpPlaceholder { get; init; } = "Ask a follow-up";

    /// <summary>The placeholder of the box on the empty page.</summary>
    public string AskPlaceholder { get; init; } = "Ask anything";

    /// <summary>The accessible name of the box.</summary>
    public string Message { get; init; } = "Question";

    /// <summary>The send button's name and tooltip.</summary>
    public string Send { get; init; } = "Send question";

    /// <summary>The stop button's name and tooltip.</summary>
    public string Stop { get; init; } = "Stop answering";

    /// <summary>"Thinking" while the model thinks.</summary>
    public string Thinking { get; init; } = "Thinking";

    /// <summary>The reasoning row of a finished answer whose thinking time is not known.</summary>
    public string ReasoningSummary { get; init; } = "Reasoning";

    /// <summary>"Thought for 8s"; the argument is the seconds.</summary>
    public Func<int, string> ThoughtFor { get; init; } = seconds => $"Thought for {seconds}s";

    /// <summary>The label of the sources row; the argument is the number of sources.</summary>
    public Func<int, string> SourceCount { get; init; } = count => count == 1 ? "1 source" : $"{count} sources";

    /// <summary>The accessible name of the list of source cards.</summary>
    public string Sources { get; init; } = "Sources";

    /// <summary>The accessible name of a source card; arguments are the number, the title and the domain.</summary>
    public Func<int, string, string?, string> SourceCard { get; init; } =
        (n, title, domain) => string.IsNullOrEmpty(domain) ? $"Source {n}: {title}" : $"Source {n}: {title}, {domain}";

    /// <summary>The accessible name of a table in the answer.</summary>
    public string Table { get; init; } = "Comparison table";

    /// <summary>The accessible name of a code block; the argument is its file name or language.</summary>
    public Func<string, string> Code { get; init; } = name => $"Code: {name}";

    /// <summary>The copy button of a code block.</summary>
    public string CopyCode { get; init; } = "Copy";

    /// <summary>The accessible name of the action bar under the answer.</summary>
    public string AnswerActions { get; init; } = "Answer actions";

    /// <summary>The copy answer button.</summary>
    public string CopyAnswer { get; init; } = "Copy answer";

    /// <summary>The regenerate button, which opens a menu.</summary>
    public string Regenerate { get; init; } = "Answer again";

    /// <summary>The first entry of the menu.</summary>
    public string TryAgain { get; init; } = "Try again";

    /// <summary>The second entry.</summary>
    public string MoreConcise { get; init; } = "More concise";

    /// <summary>The third entry.</summary>
    public string MoreDetailed { get; init; } = "More detailed";

    /// <summary>The entry that opens the list of models.</summary>
    public string SwitchModel { get; init; } = "Switch model";

    /// <summary>The heading of the list of models.</summary>
    public string AnswerAgainWith { get; init; } = "Answer again with";

    /// <summary>The thumbs up button.</summary>
    public string GoodAnswer { get; init; } = "Good answer";

    /// <summary>The thumbs down button.</summary>
    public string BadAnswer { get; init; } = "Bad answer";

    /// <summary>The tooltip of a rating that is on and can be taken back.</summary>
    public string RemoveFeedback { get; init; } = "Remove feedback";

    /// <summary>The share link button of the action bar.</summary>
    public string CopyLink { get; init; } = "Copy link to this answer";

    /// <summary>The question above the reasons of a down rating.</summary>
    public string WhatWentWrong { get; init; } = "What went wrong?";

    /// <summary>The first line of the confirmation after a reason is chosen.</summary>
    public string ThanksTitle { get; init; } = "Thanks for the feedback";

    /// <summary>The second line of the confirmation when no reason is known.</summary>
    public string ThanksDescription { get; init; } = "It helps make answers better.";

    /// <summary>The second line of the confirmation; the argument is the chosen reason.</summary>
    public Func<string, string> ThanksNoted { get; init; } = reason => $"Noted: {reason.ToLowerInvariant()}";

    /// <summary>The reasons a person can choose for a down rating.</summary>
    public string[] FeedbackReasons { get; init; } = ["Not correct", "Out of date", "Needs a source", "Too long", "Didn't answer my question"];

    /// <summary>The previous version button.</summary>
    public string PreviousVersion { get; init; } = "Previous version";

    /// <summary>The next version button.</summary>
    public string NextVersion { get; init; } = "Next version";

    /// <summary>The accessible name of the version pager; arguments are the shown version and the count.</summary>
    public Func<int, int, string> Version { get; init; } = (n, count) => $"Version {n} of {count}";

    /// <summary>The visible counter of the pager.</summary>
    public Func<int, int, string> VersionCount { get; init; } = (n, count) => $"{n} / {count}";

    /// <summary>The model line's token count; the argument is the tokens.</summary>
    public Func<int, string> Tokens { get; init; } = tokens => $"{tokens:N0} tokens";

    /// <summary>The model line's time; the argument is the seconds.</summary>
    public Func<double, string> Seconds { get; init; } = seconds => $"{seconds:0.0}s";

    /// <summary>The heading of the related questions.</summary>
    public string Related { get; init; } = "Related";

    /// <summary>The note under a stopped answer.</summary>
    public string StoppedNote { get; init; } = "Stopped. This answer may be incomplete.";

    /// <summary>The note when the answer was stopped before any of it was written.</summary>
    public string StoppedBeforeAnswer { get; init; } = "Stopped before the answer began.";

    /// <summary>The note under a failed answer.</summary>
    public Func<string?, string> FailedNote { get; init; } = reason => string.IsNullOrEmpty(reason) ? "The answer failed." : $"The answer failed: {reason}";

    /// <summary>The retry button of a note.</summary>
    public string Retry { get; init; } = "Try again";

    // Announcements (polite live region).

    /// <summary>Announced while the model thinks.</summary>
    public string AnnounceThinking { get; init; } = "Thinking";

    /// <summary>Announced when the text starts.</summary>
    public string AnnounceWriting { get; init; } = "Writing the answer";

    /// <summary>Announced when the answer is complete.</summary>
    public string AnnounceReady { get; init; } = "Answer ready";

    /// <summary>Announced when the answer is stopped.</summary>
    public string AnnounceStopped { get; init; } = "Answer stopped";

    /// <summary>Announced when the answer fails.</summary>
    public Func<string?, string> AnnounceFailed { get; init; } = reason => string.IsNullOrEmpty(reason) ? "The answer failed" : $"The answer failed: {reason}";

    /// <summary>Announced after copying the answer.</summary>
    public string AnnounceCopied { get; init; } = "Answer copied with its sources";

    /// <summary>Announced after copying a link.</summary>
    public string AnnounceLinkCopied { get; init; } = "Link copied";

    /// <summary>Announced after a rating.</summary>
    public Func<bool, string> AnnounceFeedback { get; init; } = good => good ? "Feedback sent: good answer" : "Feedback sent: bad answer";

    /// <summary>Announced when a rating is taken back.</summary>
    public string AnnounceFeedbackRemoved { get; init; } = "Feedback removed";

    /// <summary>Announced when another version is shown; arguments are the shown version and the count.</summary>
    public Func<int, int, string> AnnounceVersion { get; init; } = (n, count) => $"Showing version {n} of {count}";
}
