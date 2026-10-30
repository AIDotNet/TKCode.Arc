namespace TKCode.Arc;

/// <summary>
/// A model and a database an <see cref="AskYourData"/> plays while the app wires neither: the question it asks on its own,
/// the suggestions, the source, and the answers it knows. A question that contains one of an answer's
/// <see cref="AskYourDataSimulationAnswer.Keywords"/> gets that answer (the first match in order); any other gets
/// <see cref="Fallback"/>. The SQL is revealed at the card's own pace after a short pause, the rows scanned roll up in ten
/// steps, then the result builds and its insight streams in. Nothing is queried, and the live region says so. Set the data
/// before handing the simulation to the card; to play other answers, give the card a new simulation.
/// </summary>
public sealed class AskYourDataSimulation
{
    /// <summary>The question the card shows and asks on its own (the card's own <see cref="AskYourData.Question"/> wins).</summary>
    public string? Question { get; set; }

    /// <summary>The questions offered in the empty state and after an empty result (the card's own <see cref="AskYourData.Suggestions"/> win).</summary>
    public IReadOnlyList<string>? Suggestions { get; set; }

    /// <summary>The database the answers come from (the card's own <see cref="AskYourData.Source"/> wins).</summary>
    public AskYourDataSource? Source { get; set; }

    /// <summary>The answers, checked in order against the question.</summary>
    public IList<AskYourDataSimulationAnswer>? Answers { get; set; }

    /// <summary>The answer to a question no other answer matches; null writes no SQL and finds no rows.</summary>
    public AskYourDataSimulationAnswer? Fallback { get; set; }

    /// <summary>The answer for <paramref name="question"/>: the first whose keyword it contains, else the fallback.</summary>
    internal AskYourDataSimulationAnswer? Match(string question)
    {
        foreach (var answer in Answers ?? [])
        {
            if (answer?.Keywords.Any(k => !string.IsNullOrEmpty(k) && question.Contains(k, StringComparison.OrdinalIgnoreCase)) == true)
            {
                return answer;
            }
        }
        return Fallback;
    }

    /// <summary>The SQL the simulated model writes for <paramref name="question"/>.</summary>
    internal string SqlFor(string question) =>
        Match(question)?.Sql.Replace(AskYourDataSimulationAnswer.KeywordToken, LongestWord(question), StringComparison.Ordinal)
        ?? string.Empty;

    /// <summary>The rows the simulated query scans for <paramref name="question"/>.</summary>
    internal long ScannedFor(string question) => Math.Max(0, Match(question)?.RowsScanned ?? 0);

    /// <summary>The result the simulated database returns for <paramref name="question"/> (empty when it has none).</summary>
    internal AskYourDataResult ResultFor(string question) =>
        Match(question)?.Result ?? new AskYourDataResult { Title = string.Empty, RowCount = 0 };

    /// <summary>The question's longest word in lower case, for <see cref="AskYourDataSimulationAnswer.KeywordToken"/>.</summary>
    internal static string LongestWord(string question)
    {
        var best = string.Empty;
        foreach (var word in question.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var clean = new string(word.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            if (clean.Length > best.Length)
            {
                best = clean;
            }
        }
        return best;
    }
}

/// <summary>One answer of an <see cref="AskYourDataSimulation"/>: the words that pick it, the SQL, the rows scanned and the result.</summary>
public sealed class AskYourDataSimulationAnswer
{
    /// <summary>Replaced in <see cref="Sql"/> by the question's longest word, for a fallback that searches for it.</summary>
    public const string KeywordToken = "{keyword}";

    /// <summary>Words that pick this answer when the question contains one (case does not matter).</summary>
    public IList<string> Keywords { get; set; } = [];

    /// <summary>The SQL the simulated model writes. <see cref="KeywordToken"/> becomes the question's longest word.</summary>
    public string Sql { get; set; } = string.Empty;

    /// <summary>The rows the query scans: the running state's count rolls up to it.</summary>
    public long RowsScanned { get; set; }

    /// <summary>The chart ready result; null finds no rows.</summary>
    public AskYourDataResult? Result { get; set; }
}
