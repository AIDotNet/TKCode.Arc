namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="ModelCard"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; sentences that carry values are format functions.
/// </summary>
public sealed class ModelCardLabels
{
    /// <summary>The English defaults.</summary>
    public static ModelCardLabels Default { get; } = new();

    /// <summary>The name the header shows while the card has no model.</summary>
    public string NoModel { get; init; } = "No model selected";

    /// <summary>The label of the Compare switch in the header.</summary>
    public string Compare { get; init; } = "Compare";

    /// <summary>The modality chips, in the order text, image, PDF, audio, video, tools, reasoning.</summary>
    public string Text { get; init; } = "Text";

    /// <inheritdoc cref="Text"/>
    public string Image { get; init; } = "Image";

    /// <inheritdoc cref="Text"/>
    public string Pdf { get; init; } = "PDF";

    /// <inheritdoc cref="Text"/>
    public string Audio { get; init; } = "Audio";

    /// <inheritdoc cref="Text"/>
    public string Video { get; init; } = "Video";

    /// <inheritdoc cref="Text"/>
    public string Tools { get; init; } = "Tools";

    /// <inheritdoc cref="Text"/>
    public string Reasoning { get; init; } = "Reasoning";

    /// <summary>What screen readers add to a chip the model supports.</summary>
    public string Supported { get; init; } = "supported";

    /// <summary>What screen readers add to a chip the model does not support.</summary>
    public string NotSupported { get; init; } = "not supported";

    /// <summary>The spec labels.</summary>
    public string ContextWindow { get; init; } = "Context window";

    /// <inheritdoc cref="ContextWindow"/>
    public string MaxOutput { get; init; } = "Max output";

    /// <inheritdoc cref="ContextWindow"/>
    public string KnowledgeCutoff { get; init; } = "Knowledge cutoff";

    /// <summary>The unit after the page count under the context window ("≈ 1,500 pages").</summary>
    public string Pages { get; init; } = "pages";

    /// <summary>The heading of the prices.</summary>
    public string PricesHeading { get; init; } = "Price per 1M tokens";

    /// <summary>The price labels.</summary>
    public string Input { get; init; } = "Input";

    /// <inheritdoc cref="Input"/>
    public string Output { get; init; } = "Output";

    /// <inheritdoc cref="Input"/>
    public string CachedInput { get; init; } = "Cached input";

    /// <summary>The heading of the cost estimator.</summary>
    public string EstimateHeading { get; init; } = "Monthly cost estimate";

    /// <summary>The name of the volume slider.</summary>
    public string MonthlyVolume { get; init; } = "Monthly volume";

    /// <summary>The line under the estimate, from the input share (0.75): "Billed as 75% input, 25% output".</summary>
    public Func<double, string> InputMix { get; init; } =
        share => $"Billed as {Math.Round(share * 100):0}% input, {100 - Math.Round(share * 100):0}% output";

    /// <summary>The estimate's name for screen readers: (cost, volume) → "Estimated $270.00 a month at 50M tokens".</summary>
    public Func<string, string, string> EstimateName { get; init; } =
        (cost, volume) => $"Estimated {cost} a month at {volume} tokens";

    /// <summary>The suffix after the estimated cost.</summary>
    public string PerMonth { get; init; } = "/mo";

    /// <summary>The heading of the benchmark bars.</summary>
    public string BenchmarksHeading { get; init; } = "Benchmarks";

    /// <summary>The name of the benchmark switcher.</summary>
    public string BenchmarkSwitcher { get; init; } = "Benchmark";

    /// <summary>The words after the benchmark caption while <see cref="ModelCard.IsSample"/> is on.</summary>
    public string SampleScores { get; init; } = "Sample scores";

    /// <summary>A missing value in the details view and the compare table.</summary>
    public string NotListed { get; init; } = "Not listed";

    /// <summary>A benchmark row without a score.</summary>
    public string None { get; init; } = "None";

    /// <summary>A rank: (rank, count) → "1st of 4".</summary>
    public Func<int, int, string> Rank { get; init; } = (rank, count) => $"{Ordinal(rank)} of {count}";

    /// <summary>The gap of the leader: (margin, runner-up name) → "leads {name} by 2.3".</summary>
    public Func<string, string, string> Leads { get; init; } = (margin, name) => $"leads {name} by {margin}";

    /// <summary>The gap to the leader: (margin, leader name) → "2.3 behind {name}".</summary>
    public Func<string, string, string> Behind { get; init; } = (margin, name) => $"{margin} behind {name}";

    /// <summary>A row's readout: (name, score, rank, gap) → "{name}: 82.1%, 1st of 4, leads {runner-up} by 2.3".</summary>
    public Func<string, string, string, string, string> Readout { get; init; } =
        (name, score, rank, gap) => gap.Length > 0 ? $"{name}: {score}, {rank}, {gap}" : $"{name}: {score}, {rank}";

    /// <summary>The readout of a row without a score: (name) → "{name} has no score on this benchmark".</summary>
    public Func<string, string> NoScore { get; init; } = name => $"{name} has no score on this benchmark";

    /// <summary>The label of the rival menu in the compare view.</summary>
    public string CompareWith { get; init; } = "Compare with";

    /// <summary>The price rows of the compare table.</summary>
    public string InputPrice { get; init; } = "Input per 1M";

    /// <inheritdoc cref="InputPrice"/>
    public string OutputPrice { get; init; } = "Output per 1M";

    /// <inheritdoc cref="InputPrice"/>
    public string CachedPrice { get; init; } = "Cached input per 1M";

    /// <summary>The cost row of the compare table: (volume) → "Cost at 50M/mo".</summary>
    public Func<string, string> CostAt { get; init; } = volume => $"Cost at {volume}/mo";

    /// <summary>What screen readers hear after the better value in the compare table.</summary>
    public string Better { get; init; } = "better";

    /// <summary>The tally under the table: (leader, wins, rows) → "{name} leads on 6 of 10".</summary>
    public Func<string, int, int, string> Tally { get; init; } = (name, wins, rows) => $"{name} leads on {wins} of {rows}";

    /// <summary>The tally when both lead on the same number of rows: (wins, rows) → "Tied, 4 each of 10".</summary>
    public Func<int, int, string> Tied { get; init; } = (wins, rows) => $"Tied, {wins} each of {rows}";

    /// <summary>The footnote of the compare table while <see cref="ModelCard.IsSample"/> is on.</summary>
    public string SampleFootnote { get; init; } = "Prices and scores are samples.";

    /// <summary>The copy button in the footer.</summary>
    public string CopyId { get; init; } = "Copy model id";

    /// <summary>The copy button once it has copied.</summary>
    public string Copied { get; init; } = "Copied";

    /// <summary>The select button; <see cref="ModelCard.SelectLabel"/> overrides it.</summary>
    public string UseModel { get; init; } = "Use model";

    /// <summary>The select button once the model is selected.</summary>
    public string Selected { get; init; } = "Selected";

    /// <summary>The Retry button of the error state.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>What the status region says while loading.</summary>
    public string Loading { get; init; } = "Loading model details";

    /// <summary>What the status region says when the compare view opens: (model, rival).</summary>
    public Func<string, string, string> Comparing { get; init; } = (model, rival) => $"Comparing {model} with {rival}";

    /// <summary>What the status region says when the details view opens: (model).</summary>
    public Func<string, string> ShowingDetails { get; init; } = model => $"Showing details for {model}";

    /// <summary>What the status region says when the benchmark changes: (benchmark).</summary>
    public Func<string, string> ShowingBenchmark { get; init; } = benchmark => $"Showing {benchmark}";

    /// <summary>The words "1st", "2nd", "3rd", "4th".</summary>
    internal static string Ordinal(int n)
    {
        var tens = n % 100;
        var suffix = tens is >= 11 and <= 13 ? "th" : (n % 10) switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th"
        };
        return n + suffix;
    }
}
