using System.Text;

namespace TKCode.Arc;

/// <summary>How sure the matcher is about a column's field, as the mapping step colors it.</summary>
internal enum ImportConfidenceLevel
{
    /// <summary>90 and above (success color).</summary>
    Exact,

    /// <summary>80 to 89 (neutral strong text).</summary>
    Likely,

    /// <summary>Below 80 (warning color).</summary>
    Check
}

/// <summary>The matcher's proposal for one column.</summary>
/// <param name="Target">The index of the proposed target, or -1 for "Don't import".</param>
/// <param name="Confidence">0–100; 0 without a proposal.</param>
internal readonly record struct ImportColumnMatch(int Target, int Confidence);

/// <summary>
/// Proposes a target field for every column of a file. Scores (TKCode's own scale): the header equals the target's label,
/// key or one of its <see cref="ImportTarget.Headers"/> → 99; it equals a synonym and the values look like the field → 95;
/// a synonym alone → 88; no header hint but the values look like the field → 71; a header word equals a word of the
/// target's names → 64. Headers are compared without case, spaces, underscores, hyphens and dots. Every target goes to
/// at most one column: the highest score wins, ties go to the earlier column.
/// </summary>
internal static class ImportWizardMatcher
{
    public const int ExactScore = 99;
    public const int SniffedSynonymScore = 95;
    public const int SynonymScore = 88;
    public const int SniffScore = 71;
    public const int PartialScore = 64;

    /// <summary>The share of a column's non-empty values the sniffer must accept to back a synonym.</summary>
    public const double SynonymSniffShare = 0.6;

    /// <summary>The share of a column's non-empty values the sniffer must accept to propose a field on values alone.</summary>
    public const double SniffShare = 0.8;

    /// <summary>How many values per column are sniffed.</summary>
    public const int SniffSample = 50;

    public static ImportConfidenceLevel LevelOf(int confidence) => confidence switch
    {
        >= 90 => ImportConfidenceLevel.Exact,
        >= 80 => ImportConfidenceLevel.Likely,
        _ => ImportConfidenceLevel.Check
    };

    /// <summary>One proposal per header, in header order.</summary>
    public static ImportColumnMatch[] Match(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows, IReadOnlyList<ImportTarget> targets)
    {
        var candidates = new List<(int Column, int Target, int Score)>();
        var targetNames = new HashSet<string>[targets.Count];
        var targetWords = new HashSet<string>[targets.Count];
        var targetSynonyms = new HashSet<string>[targets.Count];
        for (var t = 0; t < targets.Count; t++)
        {
            var target = targets[t];
            targetNames[t] = new HashSet<string>(StringComparer.Ordinal) { Normalize(target.Label), Normalize(target.Key) };
            foreach (var header in target.Headers)
            {
                targetNames[t].Add(Normalize(header));
            }
            targetSynonyms[t] = new HashSet<string>(StringComparer.Ordinal);
            foreach (var synonym in target.Synonyms)
            {
                targetSynonyms[t].Add(Normalize(synonym));
            }
            targetWords[t] = new HashSet<string>(StringComparer.Ordinal);
            AddWords(targetWords[t], target.Label);
            AddWords(targetWords[t], target.Key);
            foreach (var header in target.Headers)
            {
                AddWords(targetWords[t], header);
            }
            foreach (var synonym in target.Synonyms)
            {
                AddWords(targetWords[t], synonym);
            }
        }
        for (var c = 0; c < headers.Count; c++)
        {
            var normalized = Normalize(headers[c]);
            var words = new HashSet<string>(StringComparer.Ordinal);
            AddWords(words, headers[c]);
            for (var t = 0; t < targets.Count; t++)
            {
                var score = 0;
                if (normalized.Length > 0 && targetNames[t].Contains(normalized))
                {
                    score = ExactScore;
                }
                else if (normalized.Length > 0 && targetSynonyms[t].Contains(normalized))
                {
                    score = SniffRatio(rows, c, targets[t].Sniff) >= SynonymSniffShare ? SniffedSynonymScore : SynonymScore;
                }
                else if (SniffRatio(rows, c, targets[t].Sniff) >= SniffShare)
                {
                    score = SniffScore;
                }
                else if (words.Overlaps(targetWords[t]))
                {
                    score = PartialScore;
                }
                if (score > 0)
                {
                    candidates.Add((c, t, score));
                }
            }
        }
        var result = new ImportColumnMatch[headers.Count];
        Array.Fill(result, new ImportColumnMatch(-1, 0));
        var takenTargets = new bool[targets.Count];
        var takenColumns = new bool[headers.Count];
        // Highest score first; ties keep the earlier column, then the earlier target.
        candidates.Sort((a, b) => a.Score != b.Score ? b.Score.CompareTo(a.Score)
            : a.Column != b.Column ? a.Column.CompareTo(b.Column) : a.Target.CompareTo(b.Target));
        foreach (var (column, target, score) in candidates)
        {
            if (takenColumns[column] || takenTargets[target])
            {
                continue;
            }
            takenColumns[column] = true;
            takenTargets[target] = true;
            result[column] = new ImportColumnMatch(target, score);
        }
        return result;
    }

    /// <summary>Lower case letters and digits only ("E-mail Address" → "emailaddress").</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }
        return builder.ToString();
    }

    private static void AddWords(HashSet<string> words, string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        var builder = new StringBuilder();
        foreach (var ch in text + " ")
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                continue;
            }
            if (builder.Length >= 3)
            {
                words.Add(builder.ToString());
            }
            builder.Clear();
        }
    }

    private static double SniffRatio(IReadOnlyList<string[]> rows, int column, Func<string, bool>? sniff)
    {
        if (sniff is null)
        {
            return 0;
        }
        int seen = 0, hits = 0;
        foreach (var row in rows)
        {
            if (column >= row.Length || string.IsNullOrWhiteSpace(row[column]))
            {
                continue;
            }
            seen++;
            if (sniff(row[column]))
            {
                hits++;
            }
            if (seen >= SniffSample)
            {
                break;
            }
        }
        return seen == 0 ? 0 : (double)hits / seen;
    }
}
