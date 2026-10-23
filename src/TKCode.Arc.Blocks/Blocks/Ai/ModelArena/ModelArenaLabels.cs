using System;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="ModelArena"/> shows or announces, in English by default. Replace the instance to translate or
/// reword; formatted texts are functions so that word order can change. Slots are passed as "A" or "B".
/// </summary>
public sealed class ModelArenaLabels
{
    // Header and prompt row.

    /// <summary>The title.</summary>
    public string Title { get; init; } = "Arena";

    /// <summary>The round counter's accessible name; the number itself is drawn rolling after <see cref="RoundWord"/>.</summary>
    public Func<int, string> RoundName { get; init; } = round => $"Round {round}";

    /// <summary>The word before the rolling round number.</summary>
    public string RoundWord { get; init; } = "Round";

    /// <summary>The leaderboard button.</summary>
    public string Leaderboard { get; init; } = "Standings";

    /// <summary>The leaderboard button's accessible name when there is a result the person has not looked at.</summary>
    public string LeaderboardUpdated { get; init; } = "Standings, updated";

    /// <summary>The blind switch's text and name.</summary>
    public string Blind { get; init; } = "Blind";

    /// <summary>The accessible name of the prompt box.</summary>
    public string PromptName { get; init; } = "Prompt sent to both models";

    /// <summary>The prompt box's placeholder.</summary>
    public string Placeholder { get; init; } = "Ask both models one question";

    /// <summary>The Run button.</summary>
    public string Run { get; init; } = "Run";

    /// <summary>The Stop button.</summary>
    public string Stop { get; init; } = "Stop";

    /// <summary>The accessible name of the row of sample prompts.</summary>
    public string SamplePrompts { get; init; } = "Sample prompts";

    // Panes.

    /// <summary>A pane's name while the models are hidden; the argument is "A" or "B".</summary>
    public Func<string, string> PaneName { get; init; } = slot => $"Model {slot}";

    /// <summary>A pane's accessible name once the model is revealed; the arguments are the slot and the model's name.</summary>
    public Func<string, string, string> PaneNameRevealed { get; init; } = (slot, model) => $"Model {slot}, {model}";

    /// <summary>The accessible name of a pane's answer; the argument is the slot.</summary>
    public Func<string, string> AnswerName { get; init; } = slot => $"Model {slot} answer";

    /// <summary>The accessible name of a pane's metrics; the argument is the slot.</summary>
    public Func<string, string> MetricsName { get; init; } = slot => $"Model {slot} metrics";

    /// <summary>The status words.</summary>
    public string Thinking { get; init; } = "Thinking";

    /// <summary>The status word while text arrives.</summary>
    public string Writing { get; init; } = "Writing";

    /// <summary>The status word of a finished answer.</summary>
    public string Done { get; init; } = "Done";

    /// <summary>The status word of the answer that finished first.</summary>
    public string FinishedFirst { get; init; } = "Finished first";

    /// <summary>The short form of <see cref="FinishedFirst"/> on a narrow tab.</summary>
    public string FinishedFirstShort { get; init; } = "First";

    /// <summary>The status word of a stopped answer.</summary>
    public string Stopped { get; init; } = "Stopped";

    /// <summary>The status word of a failed answer.</summary>
    public string Failed { get; init; } = "Failed";

    /// <summary>The metric before the first token; also the visible label.</summary>
    public string FirstToken { get; init; } = "First token";

    /// <summary>The speed unit.</summary>
    public string TokensPerSecond { get; init; } = "tok/s";

    /// <summary>The accessible name of the speed value.</summary>
    public string Speed { get; init; } = "Speed";

    /// <summary>The accessible name of the cost value.</summary>
    public string Cost { get; init; } = "Cost";

    /// <summary>The error card's default title.</summary>
    public string ErrorTitle { get; init; } = "Connection dropped";

    /// <summary>The error card's default text.</summary>
    public string ErrorText { get; init; } = "The stream ended before this answer was complete.";

    /// <summary>The error card's retry button; the argument is the slot.</summary>
    public Func<string, string> Retry { get; init; } = slot => $"Retry model {slot}";

    /// <summary>The error card's button when the answer cannot be retried.</summary>
    public string TrySample { get; init; } = "Try a sample prompt";

    /// <summary>The accessible name of the narrow layout's tab list.</summary>
    public string Answers { get; init; } = "Answers";

    // Vote row.

    /// <summary>The waiting row's text.</summary>
    public string Waiting { get; init; } = "Votes open once both answers are in";

    /// <summary>The waiting row's count; the argument is how many are finished.</summary>
    public Func<int, string> WaitingCount { get; init; } = done => $"{done} of 2 finished";

    /// <summary>The vote question.</summary>
    public string VoteQuestion { get; init; } = "Which answer wins?";

    /// <summary>The vote buttons.</summary>
    public string VoteA { get; init; } = "A is better";

    /// <summary>The tie button.</summary>
    public string VoteTie { get; init; } = "Tie";

    /// <summary>The B button.</summary>
    public string VoteB { get; init; } = "B is better";

    /// <summary>The both-bad button.</summary>
    public string VoteBothBad { get; init; } = "Both bad";

    /// <summary>The result of an A or B vote; the argument is the winner's name.</summary>
    public Func<string, string> ResultChose { get; init; } = name => $"You chose {name}";

    /// <summary>The words before the winner's name in the result row, where the name is shown in bold after its mark.</summary>
    public string ResultChosePrefix { get; init; } = "You chose";

    /// <summary>The result of a tie.</summary>
    public string ResultTie { get; init; } = "Called a tie";

    /// <summary>The result of a both-bad vote.</summary>
    public string ResultBothBad { get; init; } = "Marked both as bad";

    /// <summary>The Next round button.</summary>
    public string NextRound { get; init; } = "Next round";

    /// <summary>The row after a stop.</summary>
    public string StoppedRow { get; init; } = "Run halted early";

    /// <summary>The Run again button.</summary>
    public string RunAgain { get; init; } = "Run again";

    /// <summary>The row after a failure; the argument is "Model A", "Model B" or "Both models".</summary>
    public Func<string, string> FailedRow { get; init; } = who => $"{who} failed. Retry it or move on.";

    /// <summary>The name used in <see cref="FailedRow"/> when both fail.</summary>
    public string BothModels { get; init; } = "Both models";

    // Standings.

    /// <summary>The popover's title.</summary>
    public string StandingsTitle { get; init; } = "Standings";

    /// <summary>The popover's vote count; the argument is the number of votes.</summary>
    public Func<int, string> Votes { get; init; } = count => count switch { 0 => "No votes yet", 1 => "1 vote", _ => $"{count} votes" };

    /// <summary>The empty standings' text.</summary>
    public string EmptyStandings { get; init; } = "No votes yet. Judge a round and the models land here, ranked by win rate.";

    /// <summary>The footnote.</summary>
    public string TieRule { get; init; } = "Ties count as half a win";

    /// <summary>The Reset button.</summary>
    public string Reset { get; init; } = "Reset";

    /// <summary>A row's accessible name; the arguments are the rank, the model's name and the win rate in percent.</summary>
    public Func<int, string, int, string> StandingRow { get; init; } = (rank, name, percent) => $"Rank {rank}, {name}, win rate {percent}%";

    // Announcements (polite live region).

    /// <summary>A round started.</summary>
    public string AnnounceStarted { get; init; } = "Round started. Two hidden models are answering.";

    /// <summary>One answer finished first; the argument is the slot.</summary>
    public Func<string, string> AnnounceFinishedFirst { get; init; } = slot => $"Model {slot} finished first.";

    /// <summary>Both answers are in.</summary>
    public string AnnounceReady { get; init; } = "Both answers are in. Vote to see the models.";

    /// <summary>A vote was cast; the arguments are the result text and the two models' names.</summary>
    public Func<string, string, string, string> AnnounceVoted { get; init; } =
        (result, a, b) => $"{result}. Model A was {a}, model B was {b}. Standings updated.";

    /// <summary>The round was stopped.</summary>
    public string AnnounceStopped { get; init; } = "Round stopped.";

    /// <summary>One answer failed; the argument is the slot.</summary>
    public Func<string, string> AnnounceFailed { get; init; } = slot => $"Model {slot} failed.";

    /// <summary>Both answers failed.</summary>
    public string AnnounceBothFailed { get; init; } = "Both models failed.";

    /// <summary>The standings were cleared.</summary>
    public string AnnounceReset { get; init; } = "Standings cleared.";
}
