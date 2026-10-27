using System.Security.Cryptography;
using System.Text;

namespace TKCode.Arc;

/// <summary>What the .env importer does with one line.</summary>
internal enum EnvDotenvStatus
{
    /// <summary>A key that no row covers in the chosen environments: imported as a new row.</summary>
    New,

    /// <summary>A key already set in some chosen environments: those rows give them up and a new row takes them.</summary>
    Replaces,

    /// <summary>The key appears again on a later line; the later one wins and this one is skipped.</summary>
    Duplicate,

    /// <summary>The key starts with a digit; skipped.</summary>
    StartsWithDigit,

    /// <summary>The key has characters other than letters, digits and underscores (or is empty); skipped.</summary>
    InvalidCharacters,

    /// <summary>The line has no equals sign; skipped.</summary>
    MissingEquals
}

/// <summary>What is wrong with a variable name.</summary>
internal enum EnvKeyProblem
{
    /// <summary>A valid key.</summary>
    None,

    /// <summary>No key.</summary>
    Empty,

    /// <summary>It starts with a digit.</summary>
    StartsWithDigit,

    /// <summary>It has characters other than ASCII letters, digits and underscores.</summary>
    InvalidCharacters
}

/// <summary>One parsed line of a .env text (comments and blank lines are not listed).</summary>
/// <param name="LineNumber">1-based line number in the text.</param>
/// <param name="Key">The key, or the whole line when it has no equals sign.</param>
/// <param name="Value">The value without quotes and trailing comment; null when the line has no equals sign.</param>
/// <param name="Status">What the import does with it.</param>
/// <param name="SetAgainOn">Duplicate only: the line that sets the key again (the last one).</param>
/// <param name="ReplacedEnvironments">Replaces only: the chosen environments that already have a row of this key.</param>
internal sealed record EnvDotenvLine(
    int LineNumber,
    string Key,
    string? Value,
    EnvDotenvStatus Status,
    int SetAgainOn,
    IReadOnlyList<string> ReplacedEnvironments)
{
    /// <summary>True when the line is imported (New or Replaces).</summary>
    public bool IsImported => Status is EnvDotenvStatus.New or EnvDotenvStatus.Replaces;

    /// <summary>True when the line was skipped.</summary>
    public bool IsSkipped => !IsImported;
}

/// <summary>The parsed preview of a .env text.</summary>
internal sealed class EnvDotenvResult
{
    /// <summary>The empty result (no text).</summary>
    public static readonly EnvDotenvResult Empty = new(Array.Empty<EnvDotenvLine>());

    public EnvDotenvResult(IReadOnlyList<EnvDotenvLine> lines)
    {
        Lines = lines;
        ImportCount = lines.Count(l => l.IsImported);
        SkippedCount = lines.Count - ImportCount;
    }

    /// <summary>Every key line in text order.</summary>
    public IReadOnlyList<EnvDotenvLine> Lines { get; }

    /// <summary>How many lines are imported.</summary>
    public int ImportCount { get; }

    /// <summary>How many lines are skipped.</summary>
    public int SkippedCount { get; }
}

/// <summary>
/// Parses pasted .env text for <see cref="EnvVariables"/> (pure: no UI, no state). Rules, per line: CRLF or LF line ends;
/// surrounding white space is trimmed; blank lines and lines starting with <c>#</c> are ignored; an <c>export </c>
/// prefix is dropped; a line without <c>=</c> is skipped; the key is the trimmed text before the first <c>=</c> and must
/// be letters, digits and underscores not starting with a digit; a value that starts with a single or double quote runs
/// to the matching quote (the quotes and anything after them are dropped), otherwise a trailing <c> #</c> comment is
/// dropped and the value trimmed; a key set twice keeps the last line and skips the earlier ones; an empty value is fine.
/// </summary>
internal static class EnvVariableDotenvParser
{
    /// <summary>Checks a variable name.</summary>
    public static EnvKeyProblem ValidateKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return EnvKeyProblem.Empty;
        }
        if (key[0] is >= '0' and <= '9')
        {
            return EnvKeyProblem.StartsWithDigit;
        }
        foreach (var c in key)
        {
            if (!IsKeyChar(c))
            {
                return EnvKeyProblem.InvalidCharacters;
            }
        }
        return EnvKeyProblem.None;
    }

    private static bool IsKeyChar(char c) => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_';

    /// <summary>
    /// Parses <paramref name="text"/> and classifies each key line against the rows in <paramref name="existing"/> for the
    /// chosen <paramref name="environments"/> (keys compared ordinally, as the matrix does).
    /// </summary>
    public static EnvDotenvResult Parse(string? text, IReadOnlyCollection<string> environments, IEnumerable<EnvVariable> existing)
    {
        if (string.IsNullOrEmpty(text))
        {
            return EnvDotenvResult.Empty;
        }
        var raw = ParseLines(text);
        if (raw.Count == 0)
        {
            return EnvDotenvResult.Empty;
        }
        var lastLine = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in raw)
        {
            if (line.Status is null)
            {
                lastLine[line.Key] = line.LineNumber;
            }
        }
        var rows = existing.ToList();
        var result = new List<EnvDotenvLine>(raw.Count);
        foreach (var line in raw)
        {
            if (line.Status is { } skipped)
            {
                result.Add(new EnvDotenvLine(line.LineNumber, line.Key, line.Value, skipped, 0, Array.Empty<string>()));
                continue;
            }
            var last = lastLine[line.Key];
            if (last != line.LineNumber)
            {
                result.Add(new EnvDotenvLine(line.LineNumber, line.Key, line.Value, EnvDotenvStatus.Duplicate, last, Array.Empty<string>()));
                continue;
            }
            var taken = EnvVariableRules.TakenEnvironments(rows, line.Key, environments);
            result.Add(new EnvDotenvLine(line.LineNumber, line.Key, line.Value,
                taken.Count > 0 ? EnvDotenvStatus.Replaces : EnvDotenvStatus.New, 0, taken));
        }
        return new EnvDotenvResult(result);
    }

    /// <summary>A line before classification: a key line with no problem has a null status.</summary>
    internal readonly record struct RawLine(int LineNumber, string Key, string? Value, EnvDotenvStatus? Status);

    /// <summary>Splits and reads the key lines of <paramref name="text"/> without looking at duplicates or existing rows.</summary>
    internal static List<RawLine> ParseLines(string text)
    {
        var lines = new List<RawLine>();
        var number = 0;
        var start = 0;
        while (start <= text.Length)
        {
            var end = text.IndexOf('\n', start);
            if (end < 0)
            {
                end = text.Length;
            }
            number++;
            var line = text.AsSpan(start, end - start).TrimEnd('\r').Trim();
            start = end + 1;
            if (line.IsEmpty || line[0] == '#')
            {
                if (end == text.Length)
                {
                    break;
                }
                continue;
            }
            if (line.StartsWith("export", StringComparison.Ordinal) && line.Length > 6 && char.IsWhiteSpace(line[6]))
            {
                line = line[6..].TrimStart();
            }
            var equals = line.IndexOf('=');
            if (equals < 0)
            {
                lines.Add(new RawLine(number, line.ToString(), null, EnvDotenvStatus.MissingEquals));
            }
            else
            {
                var key = line[..equals].Trim().ToString();
                var value = ReadValue(line[(equals + 1)..]);
                EnvDotenvStatus? status = ValidateKey(key) switch
                {
                    EnvKeyProblem.None => null,
                    EnvKeyProblem.StartsWithDigit => EnvDotenvStatus.StartsWithDigit,
                    _ => EnvDotenvStatus.InvalidCharacters,
                };
                lines.Add(new RawLine(number, key, value, status));
            }
            if (end == text.Length)
            {
                break;
            }
        }
        return lines;
    }

    private static string ReadValue(ReadOnlySpan<char> rest)
    {
        var value = rest.Trim();
        if (value.IsEmpty)
        {
            return string.Empty;
        }
        var quote = value[0];
        if (quote is '"' or '\'')
        {
            var close = value[1..].IndexOf(quote);
            return close < 0 ? value[1..].ToString() : value.Slice(1, close).ToString();
        }
        for (var i = 1; i < value.Length; i++)
        {
            if (value[i] == '#' && char.IsWhiteSpace(value[i - 1]))
            {
                return value[..i].TrimEnd().ToString();
            }
        }
        return value.ToString();
    }
}

/// <summary>
/// The fingerprint the compare view shows instead of a value: the first 6 hex digits of the SHA-256 of its UTF-8 bytes.
/// Equal values give equal fingerprints; the value cannot be read back from it.
/// </summary>
internal static class EnvVariableFingerprint
{
    /// <summary>Number of hex digits shown.</summary>
    public const int Length = 6;

    /// <summary>Computes the fingerprint of <paramref name="value"/>.</summary>
    public static string Compute(string value)
    {
        Span<byte> hash = stackalloc byte[32];
        var bytes = Encoding.UTF8.GetBytes(value);
        SHA256.HashData(bytes, hash);
        return Convert.ToHexString(hash[..(Length / 2)]).ToLowerInvariant();
    }
}

/// <summary>How two environments compare for one key.</summary>
internal enum EnvCompareResult
{
    /// <summary>Both have it with the same fingerprint.</summary>
    Same,

    /// <summary>Both have it with different fingerprints.</summary>
    Different,

    /// <summary>Only the right environment has it.</summary>
    MissingLeft,

    /// <summary>Only the left environment has it.</summary>
    MissingRight
}

/// <summary>One key of the compare view.</summary>
/// <param name="Key">The variable name.</param>
/// <param name="Left">The row covering the left environment, if any.</param>
/// <param name="Right">The row covering the right environment, if any.</param>
/// <param name="Result">How they compare.</param>
internal readonly record struct EnvCompareRow(string Key, EnvVariable? Left, EnvVariable? Right, EnvCompareResult Result);

/// <summary>The pure rules of <see cref="EnvVariables"/>: order, coverage, conflicts, counts, search and comparison.</summary>
internal static class EnvVariableRules
{
    /// <summary>
    /// Display order: by key, ordinal (ASCII: "D_FOUR" after "DATA"), then by the first environment each row covers in
    /// <paramref name="environmentOrder"/> (rows covering none last), then by original order.
    /// </summary>
    public static List<EnvVariable> Sort(IEnumerable<EnvVariable> variables, IReadOnlyList<string> environmentOrder)
    {
        var list = variables.Select((v, i) => (v, i)).ToList();
        list.Sort((a, b) =>
        {
            var byKey = string.CompareOrdinal(a.v.Key, b.v.Key);
            if (byKey != 0)
            {
                return byKey;
            }
            var byEnv = FirstEnvironment(a.v, environmentOrder).CompareTo(FirstEnvironment(b.v, environmentOrder));
            return byEnv != 0 ? byEnv : a.i.CompareTo(b.i);
        });
        return list.Select(p => p.v).ToList();
    }

    private static int FirstEnvironment(EnvVariable variable, IReadOnlyList<string> order)
    {
        var best = int.MaxValue;
        foreach (var env in variable.Environments)
        {
            for (var i = 0; i < order.Count; i++)
            {
                if (order[i] == env && i < best)
                {
                    best = i;
                }
            }
        }
        return best;
    }

    /// <summary>The other row of the same key that already covers <paramref name="environment"/>, or null.</summary>
    public static EnvVariable? FindBlocking(IEnumerable<EnvVariable> variables, EnvVariable variable, string environment)
    {
        foreach (var other in variables)
        {
            if (!ReferenceEquals(other, variable) && string.Equals(other.Key, variable.Key, StringComparison.Ordinal)
                && other.Environments.Contains(environment))
            {
                return other;
            }
        }
        return null;
    }

    /// <summary>The environments of <paramref name="environments"/> already covered by a row of <paramref name="key"/>, in the given order.</summary>
    public static IReadOnlyList<string> TakenEnvironments(IEnumerable<EnvVariable> variables, string key, IEnumerable<string> environments)
    {
        var rows = variables.Where(v => string.Equals(v.Key, key, StringComparison.Ordinal)).ToList();
        if (rows.Count == 0)
        {
            return Array.Empty<string>();
        }
        return environments.Where(env => rows.Any(r => r.Environments.Contains(env))).ToList();
    }

    /// <summary>The row of <paramref name="key"/> covering <paramref name="environment"/>, or null.</summary>
    public static EnvVariable? RowFor(IEnumerable<EnvVariable> variables, string key, string environment) =>
        variables.FirstOrDefault(v => string.Equals(v.Key, key, StringComparison.Ordinal) && v.Environments.Contains(environment));

    /// <summary>Number of distinct keys.</summary>
    public static int CountKeys(IEnumerable<EnvVariable> variables) =>
        variables.Select(v => v.Key).Distinct(StringComparer.Ordinal).Count();

    /// <summary>Number of rows covering <paramref name="environment"/>.</summary>
    public static int CountSet(IEnumerable<EnvVariable> variables, string environment) =>
        variables.Count(v => v.Environments.Contains(environment));

    /// <summary>Search matches key names only: a case-insensitive substring; an empty query matches everything.</summary>
    public static bool Matches(string key, string? query) =>
        string.IsNullOrWhiteSpace(query) || key.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The compare rows of <paramref name="left"/> against <paramref name="right"/>: every key either covers (that matches
    /// <paramref name="query"/>), ordinal by key; Same rows only when <paramref name="showMatching"/>.
    /// </summary>
    public static List<EnvCompareRow> Compare(IEnumerable<EnvVariable> variables, string left, string right, string? query, bool showMatching)
    {
        var all = variables.ToList();
        var keys = all.Where(v => Matches(v.Key, query) && (v.Environments.Contains(left) || v.Environments.Contains(right)))
            .Select(v => v.Key).Distinct(StringComparer.Ordinal).ToList();
        keys.Sort(string.CompareOrdinal);
        var rows = new List<EnvCompareRow>(keys.Count);
        foreach (var key in keys)
        {
            var a = RowFor(all, key, left);
            var b = RowFor(all, key, right);
            var result = a is null ? EnvCompareResult.MissingLeft
                : b is null ? EnvCompareResult.MissingRight
                : ReferenceEquals(a, b) || string.Equals(a.EffectiveFingerprint, b.EffectiveFingerprint, StringComparison.Ordinal)
                    ? EnvCompareResult.Same
                    : EnvCompareResult.Different;
            if (result == EnvCompareResult.Same && !showMatching)
            {
                continue;
            }
            rows.Add(new EnvCompareRow(key, a, b, result));
        }
        return rows;
    }

    /// <summary>Counts (missing, different, matching) over the keys either environment covers that match <paramref name="query"/>.</summary>
    public static (int Missing, int Different, int Matching) CompareCounts(IEnumerable<EnvVariable> variables, string left, string right, string? query)
    {
        int missing = 0, different = 0, matching = 0;
        foreach (var row in Compare(variables, left, right, query, showMatching: true))
        {
            switch (row.Result)
            {
                case EnvCompareResult.Same:
                    matching++;
                    break;
                case EnvCompareResult.Different:
                    different++;
                    break;
                default:
                    missing++;
                    break;
            }
        }
        return (missing, different, matching);
    }
}
