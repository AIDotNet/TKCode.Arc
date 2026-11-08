namespace TKCode.Arc;

/// <summary>How <see cref="NumberFilterBox"/> compares a value with its target.</summary>
public enum NumberOperator
{
    /// <summary>= : equal to the target (within the tolerance).</summary>
    Equal,

    /// <summary>≠ : not equal to the target (outside the tolerance).</summary>
    NotEqual,

    /// <summary>&lt; : less than the target.</summary>
    LessThan,

    /// <summary>≤ : less than or equal to the target.</summary>
    LessThanOrEqual,

    /// <summary>&gt; : greater than the target.</summary>
    GreaterThan,

    /// <summary>≥ : greater than or equal to the target.</summary>
    GreaterThanOrEqual
}

/// <summary>How <see cref="TextFilterBox"/> compares a text with its filter text.</summary>
public enum TextOperator
{
    /// <summary>= : the whole text equals the filter text.</summary>
    Equal,

    /// <summary>≠ : the text differs from the filter text.</summary>
    NotEqual,

    /// <summary>∋ : the text contains the filter text.</summary>
    Contains,

    /// <summary>∌ : the text does not contain the filter text.</summary>
    NotContains,

    /// <summary>^ : the text starts with the filter text.</summary>
    StartsWith,

    /// <summary>$ : the text ends with the filter text.</summary>
    EndsWith,

    /// <summary>.* : the filter text is a regular expression the text must match.</summary>
    Regex
}

/// <summary>Whether <see cref="NumberFilterBox"/> takes whole numbers or decimals.</summary>
public enum NumberFilterMode
{
    /// <summary>Whole numbers (counts, stock, quantities).</summary>
    Integer,

    /// <summary>Numbers with <see cref="NumberFilterBox.DecimalPlaces"/> fraction digits (prices, ratings, measures).</summary>
    Decimal
}

/// <summary>
/// The symbols and spoken names of the filter operators, as a plain lookup (no attributes are reflected, so it is safe
/// for AOT and trimming). The filter boxes show the symbols; use the names for tooltips, summaries or screen readers.
/// </summary>
public static class FilterOperators
{
    /// <summary>Every number operator, in menu order.</summary>
    public static IReadOnlyList<NumberOperator> NumberOperators { get; } =
    [
        NumberOperator.Equal, NumberOperator.NotEqual, NumberOperator.LessThan,
        NumberOperator.LessThanOrEqual, NumberOperator.GreaterThan, NumberOperator.GreaterThanOrEqual
    ];

    /// <summary>Every text operator, in menu order.</summary>
    public static IReadOnlyList<TextOperator> TextOperators { get; } =
    [
        TextOperator.Contains, TextOperator.NotContains, TextOperator.Equal, TextOperator.NotEqual,
        TextOperator.StartsWith, TextOperator.EndsWith, TextOperator.Regex
    ];

    /// <summary>The symbol of a number operator: = ≠ &lt; ≤ &gt; ≥.</summary>
    public static string Symbol(NumberOperator op) => op switch
    {
        NumberOperator.Equal => "=",
        NumberOperator.NotEqual => "≠",
        NumberOperator.LessThan => "<",
        NumberOperator.LessThanOrEqual => "≤",
        NumberOperator.GreaterThan => ">",
        NumberOperator.GreaterThanOrEqual => "≥",
        _ => op.ToString()
    };

    /// <summary>The English name of a number operator ("Greater than or equal to").</summary>
    public static string Name(NumberOperator op) => op switch
    {
        NumberOperator.Equal => "Equal to",
        NumberOperator.NotEqual => "Not equal to",
        NumberOperator.LessThan => "Less than",
        NumberOperator.LessThanOrEqual => "Less than or equal to",
        NumberOperator.GreaterThan => "Greater than",
        NumberOperator.GreaterThanOrEqual => "Greater than or equal to",
        _ => op.ToString()
    };

    /// <summary>The symbol of a text operator: = ≠ ∋ ∌ ^ $ .*.</summary>
    public static string Symbol(TextOperator op) => op switch
    {
        TextOperator.Equal => "=",
        TextOperator.NotEqual => "≠",
        TextOperator.Contains => "∋",
        TextOperator.NotContains => "∌",
        TextOperator.StartsWith => "^",
        TextOperator.EndsWith => "$",
        TextOperator.Regex => ".*",
        _ => op.ToString()
    };

    /// <summary>The English name of a text operator ("Starts with").</summary>
    public static string Name(TextOperator op) => op switch
    {
        TextOperator.Equal => "Equals",
        TextOperator.NotEqual => "Does not equal",
        TextOperator.Contains => "Contains",
        TextOperator.NotContains => "Does not contain",
        TextOperator.StartsWith => "Starts with",
        TextOperator.EndsWith => "Ends with",
        TextOperator.Regex => "Matches pattern",
        _ => op.ToString()
    };
}
