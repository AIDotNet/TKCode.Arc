namespace TKCode.Arc;

/// <summary>
/// A table an <see cref="EnrichColumn"/> plays on its own while the app sets no <see cref="EnrichColumn.Rows"/>: a name,
/// the AI column, the model in the footer, the rows and, for each row, the answer a simulated run settles into. When the
/// column runs (on its own the first time it comes into view, or from Run column), each cell waits through its answer's
/// <see cref="EnrichColumnSimulationAnswer.Steps"/>, showing their activities, then shows the value and its source. An
/// answer with an <see cref="EnrichColumnSimulationAnswer.Error"/> fails on its first attempt, so the failed state and Retry
/// show. Nothing is sent, and the top bar says the data is a sample. Set the data before handing the simulation to the
/// column; to show another table, give the column a new simulation.
/// </summary>
public sealed class EnrichColumnSimulation
{
    /// <summary>The table's name in the top bar (the column's own <see cref="EnrichColumn.Title"/> wins).</summary>
    public string? Title { get; set; }

    /// <summary>The AI column's name and prompt (the column's own <see cref="EnrichColumn.Column"/> wins).</summary>
    public EnrichColumnDefinition? Column { get; set; }

    /// <summary>The model named in the footer (the column's own <see cref="EnrichColumn.Model"/> wins).</summary>
    public EnrichColumnModel? Model { get; set; }

    /// <summary>The rows, in order.</summary>
    public IList<EnrichColumnRow>? Rows { get; set; }

    /// <summary>What each row settles into, matched by <see cref="EnrichColumnSimulationAnswer.RowId"/>. A row without an answer fails.</summary>
    public IList<EnrichColumnSimulationAnswer>? Answers { get; set; }

    /// <summary>Plays one cell: the answer's activities with their pauses, then its value, or its error on the first attempt.</summary>
    internal async Task<EnrichColumnResult> RunAsync(EnrichColumnRow row, EnrichColumnContext context, IEnrichColumnClock clock)
    {
        var token = context.CancellationToken;
        var answer = Answers?.FirstOrDefault(a => a is not null && a.RowId == row.Id);
        if (answer is null)
        {
            // No answer for this row: the cell fails with the column's "unknown error" label.
            throw new InvalidOperationException(string.Empty);
        }
        foreach (var step in answer.Steps)
        {
            if (!string.IsNullOrEmpty(step.Activity))
            {
                context.SetActivity(step.Activity);
            }
            await clock.Delay(step.Duration, token);
        }
        if (!string.IsNullOrEmpty(answer.Error) && context.Attempt == 1)
        {
            throw new InvalidOperationException(answer.Error);
        }
        return new EnrichColumnResult(answer.Value, answer.Source);
    }
}

/// <summary>What one row of an <see cref="EnrichColumnSimulation"/> settles into, and how it gets there.</summary>
public sealed class EnrichColumnSimulationAnswer
{
    /// <summary>Creates an empty answer; set <see cref="RowId"/> and <see cref="Value"/>.</summary>
    public EnrichColumnSimulationAnswer()
    {
    }

    /// <summary>Creates an answer.</summary>
    /// <param name="rowId">The <see cref="EnrichColumnRow.Id"/> it answers.</param>
    /// <param name="value">The value the cell settles into.</param>
    /// <param name="source">Where the value came from, or null.</param>
    public EnrichColumnSimulationAnswer(string rowId, string value, EnrichColumnSource? source = null)
    {
        RowId = rowId;
        Value = value;
        Source = source;
    }

    /// <summary>The <see cref="EnrichColumnRow.Id"/> of the row it answers.</summary>
    public string RowId { get; set; } = string.Empty;

    /// <summary>The value the cell settles into.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Where the value came from, or null.</summary>
    public EnrichColumnSource? Source { get; set; }

    /// <summary>What the cell does before it settles, in order: each step's activity shimmers for its duration.</summary>
    public IList<EnrichColumnSimulationStep> Steps { get; set; } = [];

    /// <summary>The first attempt fails with this reason; a retry then settles into the value. Null never fails.</summary>
    public string? Error { get; set; }
}

/// <summary>One stage of a simulated cell of an <see cref="EnrichColumn"/>: the shimmering activity and how long it lasts.</summary>
public sealed class EnrichColumnSimulationStep
{
    /// <summary>Creates an empty step.</summary>
    public EnrichColumnSimulationStep()
    {
    }

    /// <summary>Creates a step.</summary>
    public EnrichColumnSimulationStep(string activity, TimeSpan duration)
    {
        Activity = activity;
        Duration = duration;
    }

    /// <summary>The cell's activity label while the step lasts; empty keeps the previous one.</summary>
    public string Activity { get; set; } = string.Empty;

    /// <summary>How long the step lasts.</summary>
    public TimeSpan Duration { get; set; }
}
