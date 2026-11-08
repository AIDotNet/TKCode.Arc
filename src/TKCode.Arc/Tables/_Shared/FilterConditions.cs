using System.Text.RegularExpressions;

namespace TKCode.Arc;

/// <summary>
/// A number condition as plain data: operator, target and tolerance. It has no UI, so a view model can build one from
/// its own state and test values with <see cref="Matches(decimal)"/>.
/// <para>The tolerance widens equality: <see cref="NumberOperator.Equal"/> accepts values within it,
/// <see cref="NumberOperator.NotEqual"/> those outside it, the strict comparisons need to clear it and the inclusive ones
/// may fall short by it. A negative tolerance counts as zero.</para>
/// </summary>
/// <param name="Operator">The comparison.</param>
/// <param name="Value">The target the candidate is compared with.</param>
/// <param name="Tolerance">How far apart two values may be and still count as equal.</param>
public readonly record struct NumberFilterCondition(NumberOperator Operator, decimal Value, decimal Tolerance)
{
    /// <summary>Whether <paramref name="candidate"/> meets the condition.</summary>
    public bool Matches(decimal candidate)
    {
        var tolerance = Math.Max(0m, Tolerance);
        var difference = candidate - Value;
        return Operator switch
        {
            NumberOperator.Equal => Math.Abs(difference) <= tolerance,
            NumberOperator.NotEqual => Math.Abs(difference) > tolerance,
            NumberOperator.LessThan => difference < -tolerance,
            NumberOperator.LessThanOrEqual => difference <= tolerance,
            NumberOperator.GreaterThan => difference > tolerance,
            NumberOperator.GreaterThanOrEqual => difference >= -tolerance,
            _ => true
        };
    }

    /// <summary>A short form such as "≥ 100".</summary>
    public override string ToString() => $"{FilterOperators.Symbol(Operator)} {Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
}

/// <summary>
/// A text condition as plain data: operator, filter text and case sensitivity. It has no UI, so a view model can build
/// one from its own state and test texts with <see cref="Matches"/>; build it once per change, not once per item, so
/// a pattern is parsed once.
/// <para>An empty filter text constrains nothing: every text matches. With <see cref="TextOperator.Regex"/> the filter
/// text is a .NET regular expression run by the interpreter (no code is generated, so it is safe for AOT); an invalid
/// pattern sets <see cref="IsValid"/> to false and <see cref="Error"/> to the parser's message, and then constrains
/// nothing either. A match that runs past <see cref="MatchTimeout"/> counts as no match.</para>
/// </summary>
public sealed class TextFilterCondition
{
    /// <summary>The match timeout used when none is given (200 ms).</summary>
    public static readonly TimeSpan DefaultMatchTimeout = TimeSpan.FromMilliseconds(200);

    private readonly Regex? _regex;

    /// <summary>Builds a condition; a regular expression is parsed here.</summary>
    public TextFilterCondition(TextOperator op, string? text, bool isCaseSensitive = false, TimeSpan? matchTimeout = null)
    {
        Operator = op;
        Text = text ?? string.Empty;
        IsCaseSensitive = isCaseSensitive;
        MatchTimeout = matchTimeout is { } timeout && timeout > TimeSpan.Zero ? timeout : DefaultMatchTimeout;
        if (op == TextOperator.Regex && Text.Length > 0)
        {
            var options = RegexOptions.CultureInvariant | (isCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
            try
            {
                _regex = new Regex(Text, options, MatchTimeout);
            }
            catch (ArgumentException error)
            {
                Error = error.Message;
            }
        }
    }

    /// <summary>The comparison.</summary>
    public TextOperator Operator { get; }

    /// <summary>The filter text (never null).</summary>
    public string Text { get; }

    /// <summary>Whether letter case matters.</summary>
    public bool IsCaseSensitive { get; }

    /// <summary>How long one regular expression match may run.</summary>
    public TimeSpan MatchTimeout { get; }

    /// <summary>The filter text is empty, so every text matches.</summary>
    public bool IsEmpty => Text.Length == 0;

    /// <summary>False when <see cref="Operator"/> is <see cref="TextOperator.Regex"/> and the pattern does not parse.</summary>
    public bool IsValid => Error is null;

    /// <summary>Why the pattern does not parse, or null.</summary>
    public string? Error { get; }

    /// <summary>Whether <paramref name="candidate"/> (null counts as empty) meets the condition.</summary>
    public bool Matches(string? candidate)
    {
        if (IsEmpty || !IsValid)
        {
            return true;
        }
        var value = candidate ?? string.Empty;
        var comparison = IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        switch (Operator)
        {
            case TextOperator.Equal:
                return string.Equals(value, Text, comparison);
            case TextOperator.NotEqual:
                return !string.Equals(value, Text, comparison);
            case TextOperator.Contains:
                return value.Contains(Text, comparison);
            case TextOperator.NotContains:
                return !value.Contains(Text, comparison);
            case TextOperator.StartsWith:
                return value.StartsWith(Text, comparison);
            case TextOperator.EndsWith:
                return value.EndsWith(Text, comparison);
            case TextOperator.Regex:
                try
                {
                    return _regex!.IsMatch(value);
                }
                catch (RegexMatchTimeoutException)
                {
                    return false;
                }
            default:
                return true;
        }
    }

    /// <summary>A short form such as "∋ Pro".</summary>
    public override string ToString() => $"{FilterOperators.Symbol(Operator)} {Text}";
}
