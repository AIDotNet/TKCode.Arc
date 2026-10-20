using System.Globalization;
using System.Text;

namespace TKCode.Arc;

/// <summary>One requirement checked by <see cref="PasswordStrength"/>.</summary>
public sealed class PasswordRule
{
    /// <summary>Creates a rule.</summary>
    /// <param name="id">Stable id reported in <see cref="PasswordStrengthScore.MetRuleIds"/>.</param>
    /// <param name="label">The text shown in the requirement list.</param>
    /// <param name="test">True when the password meets the rule.</param>
    /// <param name="remaining">Characters still missing; shown as a rolling count beside the unmet rule.</param>
    public PasswordRule(string id, string label, Func<string, bool> test, Func<string, int>? remaining = null)
    {
        Id = id;
        Label = label;
        Test = test;
        Remaining = remaining;
    }

    public string Id { get; }

    public string Label { get; }

    public Func<string, bool> Test { get; }

    public Func<string, int>? Remaining { get; }
}

/// <summary>Labels of the built-in rules (English defaults).</summary>
public sealed class PasswordRuleLabels
{
    public Func<int, string> Length { get; init; } = minLength => string.Create(CultureInfo.InvariantCulture, $"At least {minLength} characters");

    public string Case { get; init; } = "Upper and lowercase letters";

    public string Number { get; init; } = "At least one number";

    public string Symbol { get; init; } = "At least one symbol";
}

/// <summary>Arc's built-in password rules: length, mixed case, a number and a symbol.</summary>
public static class PasswordRules
{
    /// <summary>The default rules (12 characters, mixed case, number, symbol).</summary>
    public static IReadOnlyList<PasswordRule> Default { get; } = Create();

    /// <summary>The built-in rules with your own minimum length, labels and character count.</summary>
    public static IReadOnlyList<PasswordRule> Create(int minLength = 12, PasswordRuleLabels? labels = null, Func<string, int>? countCharacters = null)
    {
        labels ??= new PasswordRuleLabels();
        var count = countCharacters ?? CountCodePoints;
        return
        [
            new PasswordRule("length", labels.Length(minLength), p => count(p) >= minLength, p => Math.Max(0, minLength - count(p))),
            new PasswordRule("case", labels.Case, p => HasCategory(p, UnicodeCategory.LowercaseLetter) && HasCategory(p, UnicodeCategory.UppercaseLetter)),
            new PasswordRule("number", labels.Number, HasNumber),
            new PasswordRule("symbol", labels.Symbol, HasSymbol)
        ];
    }

    /// <summary>Counts Unicode code points, so an emoji counts once.</summary>
    public static int CountCodePoints(string password)
    {
        var count = 0;
        foreach (var _ in password.EnumerateRunes())
        {
            count++;
        }
        return count;
    }

    private static bool HasCategory(string password, UnicodeCategory category)
    {
        foreach (var rune in password.EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) == category)
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasNumber(string password)
    {
        foreach (var rune in password.EnumerateRunes())
        {
            if (Rune.IsNumber(rune))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Arc: <c>/[^\p{L}\p{N}\s]/u</c>.</summary>
    private static bool HasSymbol(string password)
    {
        foreach (var rune in password.EnumerateRunes())
        {
            if (!Rune.IsLetter(rune) && !Rune.IsNumber(rune) && !Rune.IsWhiteSpace(rune))
            {
                return true;
            }
        }
        return false;
    }
}

/// <summary>A strength estimate: <see cref="Level"/> 0 (empty) to 4 (strong).</summary>
/// <param name="Level">0 for an empty password, then 1 (weak or too short) to 4 (strong).</param>
/// <param name="MetRuleIds">Ids of the rules the password meets.</param>
/// <param name="IsShort">True while the password is shorter than the short length (the meter holds at its first step).</param>
public readonly record struct PasswordStrengthScore(int Level, IReadOnlyList<string> MetRuleIds, bool IsShort);

/// <summary>
/// Scores passwords for <see cref="PasswordStrength"/>. The default <see cref="PasswordStrengthEstimator"/> is
/// synchronous and cheap; a heavier scorer (a dictionary check, a server call) can complete asynchronously, and
/// the control ignores results that arrive after a newer keystroke.
/// </summary>
public interface IPasswordStrengthScorer
{
    ValueTask<PasswordStrengthScore> ScoreAsync(string password, IReadOnlyList<PasswordRule> rules, CancellationToken cancellationToken);
}

/// <summary>
/// Arc's on-device estimate (<c>estimateStrength</c>): one step per share of rules met, held at the first step
/// while the password is shorter than <see cref="ShortLength"/>. Nothing leaves the device.
/// </summary>
public sealed class PasswordStrengthEstimator : IPasswordStrengthScorer
{
    /// <summary>The estimator with Arc's defaults.</summary>
    public static PasswordStrengthEstimator Default { get; } = new();

    /// <summary>Below this many characters the result stays at level 1 ("Too short"). Default 8.</summary>
    public int ShortLength { get; init; } = 8;

    /// <summary>How characters are counted for <see cref="ShortLength"/>. Default: code points.</summary>
    public Func<string, int> CountCharacters { get; init; } = PasswordRules.CountCodePoints;

    /// <summary>Scores <paramref name="password"/> synchronously.</summary>
    public PasswordStrengthScore Score(string password, IReadOnlyList<PasswordRule> rules)
    {
        if (string.IsNullOrEmpty(password))
        {
            return new PasswordStrengthScore(0, Array.Empty<string>(), false);
        }
        var met = new List<string>(rules.Count);
        foreach (var rule in rules)
        {
            if (rule.Test(password))
            {
                met.Add(rule.Id);
            }
        }
        if (CountCharacters(password) < ShortLength)
        {
            return new PasswordStrengthScore(1, met, true);
        }
        var level = (int)Math.Min(4, Math.Max(1, Math.Round(met.Count / (double)Math.Max(rules.Count, 1) * 4, MidpointRounding.AwayFromZero)));
        return new PasswordStrengthScore(level, met, false);
    }

    /// <inheritdoc />
    public ValueTask<PasswordStrengthScore> ScoreAsync(string password, IReadOnlyList<PasswordRule> rules, CancellationToken cancellationToken)
        => new(Score(password, rules));
}

/// <summary>Every user-facing string of <see cref="PasswordStrength"/> (English defaults; override to translate).</summary>
public sealed record PasswordStrengthMessages
{
    public string TooShort { get; init; } = "Too short";
    public string Weak { get; init; } = "Weak";
    public string Fair { get; init; } = "Fair";
    public string Good { get; init; } = "Good";
    public string Strong { get; init; } = "Strong";

    /// <summary>Name of the reveal toggle (a pressed toggle, so the name stays the same in both states).</summary>
    public string ShowPassword { get; init; } = "Show password";

    /// <summary>Accessible name of the strength meter.</summary>
    public string MeterLabel { get; init; } = "Password strength";

    /// <summary>Meter value text while the field is empty.</summary>
    public string NoPassword { get; init; } = "No password yet";

    /// <summary>Accessible name of the requirement list.</summary>
    public string RulesLabel { get; init; } = "Password requirements";

    /// <summary>Read after a rule's label.</summary>
    public string RuleMet { get; init; } = ", met";
    public string RuleNotMet { get; init; } = ", not met";

    /// <summary>Words around the rolling count beside an unmet rule, as in "4 more".</summary>
    public string RemainingPrefix { get; init; } = string.Empty;
    public string RemainingSuffix { get; init; } = "more";

    /// <summary>The polite summary read after each change: {0} strength word, {1} rules met, {2} rule count.</summary>
    public string SummaryFormat { get; init; } = "Strength: {0}. {1} of {2} requirements met.";

    /// <summary>The strength word for a level (1 to 4) or a short password.</summary>
    public string LevelText(int level, bool isShort) => isShort ? TooShort : level switch
    {
        <= 0 => string.Empty,
        1 => Weak,
        2 => Fair,
        3 => Good,
        _ => Strong
    };
}
