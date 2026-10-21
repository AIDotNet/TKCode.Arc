using System.Globalization;
using System.Text.RegularExpressions;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>One cell as the grid hands it to a validation rule, an editability predicate or a style selector.</summary>
/// <param name="Row">The row object (a <see cref="DataGridRow"/> or one of your own classes).</param>
/// <param name="RowKey">The row's stable key.</param>
/// <param name="Column">The column.</param>
/// <param name="Value">The cell's normalized value: a string, a <see cref="decimal"/>, a <see cref="DateOnly"/>, a <see cref="bool"/> or null (for a validation rule, the value being written).</param>
public readonly record struct DataGridCellContext(object Row, object RowKey, DataGridColumn Column, object? Value);

/// <summary>What the grid does with a value that fails a column's <see cref="DataGridColumn.Rules"/>.</summary>
public enum DataGridValidationMode
{
    /// <summary>Refuses the value: the editor stays open with the message, and pasted or filled cells that fail are skipped.</summary>
    Block,

    /// <summary>Accepts the value and flags the cell (a red outline and corner mark), like Excel's circled invalid data. <see cref="DataGrid.ErrorCount"/> and <see cref="DataGrid.NextError"/> find the flagged cells.</summary>
    Flag
}

/// <summary>A flagged cell, as <see cref="DataGrid.GetErrors"/> returns it.</summary>
/// <param name="Row">The row object.</param>
/// <param name="RowKey">The row's key.</param>
/// <param name="Column">The column.</param>
/// <param name="Message">Why the value fails its rules.</param>
public readonly record struct DataGridCellError(object Row, object RowKey, DataGridColumn Column, string Message);

/// <summary>
/// A check on the values of a column. Add rules to <see cref="DataGridColumn.Rules"/>; they run when a value is typed,
/// pasted, filled or set by a renderer, and <see cref="DataGrid.ValidateAll"/> runs them over every row. The first rule that
/// returns a message decides.
/// </summary>
public abstract class DataGridRule
{
    /// <summary>The message shown when the rule fails; null uses the built-in message, which follows the interface language.</summary>
    public string? Message { get; set; }

    /// <summary>Returns the message when the value fails, null when it passes.</summary>
    public abstract string? Validate(in DataGridCellContext context);

    /// <summary>A rule written as a function: return the message, or null when the value passes.</summary>
    public static DataGridRule Custom(Func<DataGridCellContext, string?> validate) => new DelegateRule(validate);

    /// <summary>The configured message, else <paramref name="builtIn"/>.</summary>
    protected string Fail(string builtIn) => string.IsNullOrEmpty(Message) ? builtIn : Message;

    private sealed class DelegateRule(Func<DataGridCellContext, string?> validate) : DataGridRule
    {
        public override string? Validate(in DataGridCellContext context) => validate(context) is { } message ? Fail(message) : null;
    }
}

/// <summary>Fails an empty value (null, empty text).</summary>
public sealed class RequiredRule : DataGridRule
{
    /// <inheritdoc />
    public override string? Validate(in DataGridCellContext context) =>
        DataGridSorting.IsEmpty(context.Value) ? Fail(DataGridText.Format(DataGridLangResourceKind.RuleRequired, context.Column.Header)) : null;
}

/// <summary>Fails a number outside <see cref="Minimum"/> and <see cref="Maximum"/>; empty values and other types pass.</summary>
public sealed class RangeRule : DataGridRule
{
    /// <summary>The lowest allowed value; null for no lower bound.</summary>
    public decimal? Minimum { get; set; }

    /// <summary>The highest allowed value; null for no upper bound.</summary>
    public decimal? Maximum { get; set; }

    /// <inheritdoc />
    public override string? Validate(in DataGridCellContext context)
    {
        if (context.Value is not decimal number)
        {
            return null;
        }
        var culture = DataGridText.FormattingCulture;
        var header = context.Column.Header;
        if (Minimum is { } low && Maximum is { } high && (number < low || number > high))
        {
            return Fail(DataGridText.Format(DataGridLangResourceKind.RuleRangeBetween, header, low.ToString(culture), high.ToString(culture)));
        }
        if (Minimum is { } min && Maximum is null && number < min)
        {
            return Fail(DataGridText.Format(DataGridLangResourceKind.RuleRangeMin, header, min.ToString(culture)));
        }
        if (Maximum is { } max && Minimum is null && number > max)
        {
            return Fail(DataGridText.Format(DataGridLangResourceKind.RuleRangeMax, header, max.ToString(culture)));
        }
        return null;
    }
}

/// <summary>Fails text longer than <see cref="Length"/> characters.</summary>
public sealed class MaxLengthRule : DataGridRule
{
    /// <summary>The most characters allowed.</summary>
    public int Length { get; set; } = int.MaxValue;

    /// <inheritdoc />
    public override string? Validate(in DataGridCellContext context) =>
        context.Value is string text && text.Length > Length
            ? Fail(DataGridText.Format(DataGridLangResourceKind.RuleMaxLength, context.Column.Header, Length.ToString(DataGridText.FormattingCulture)))
            : null;
}

/// <summary>Fails text that does not match a regular expression; empty values pass (add <see cref="RequiredRule"/> to refuse them).</summary>
public sealed class PatternRule : DataGridRule
{
    private string _pattern = string.Empty;
    private Regex? _regex;

    /// <summary>The regular expression the whole text must match (an unanchored pattern may match anywhere in it).</summary>
    public string Pattern
    {
        get => _pattern;
        set
        {
            _pattern = value ?? string.Empty;
            _regex = null;
        }
    }

    /// <inheritdoc />
    public override string? Validate(in DataGridCellContext context)
    {
        if (context.Value is not string text || text.Length == 0 || _pattern.Length == 0)
        {
            return null;
        }
        _regex ??= new Regex(_pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
        try
        {
            return _regex.IsMatch(text) ? null : Fail(DataGridText.Format(DataGridLangResourceKind.RulePattern, context.Column.Header));
        }
        catch (RegexMatchTimeoutException)
        {
            return Fail(DataGridText.Format(DataGridLangResourceKind.RulePattern, context.Column.Header));
        }
    }
}

/// <summary>How a cell looks when a style selector or format rule picks it: a tone and optionally bold text.</summary>
/// <param name="Tone">Tints the cell and colors its text.</param>
/// <param name="Bold">Draws the text in the medium weight.</param>
public readonly record struct DataGridCellStyle(DataGridTone Tone, bool Bold = false);

/// <summary>What a <see cref="DataGridFormatRule"/> tests.</summary>
public enum DataGridFormatCondition
{
    /// <summary>The value is above <see cref="DataGridFormatRule.Value"/>.</summary>
    GreaterThan,

    /// <summary>The value is at or above <see cref="DataGridFormatRule.Value"/>.</summary>
    GreaterThanOrEqual,

    /// <summary>The value is below <see cref="DataGridFormatRule.Value"/>.</summary>
    LessThan,

    /// <summary>The value is at or below <see cref="DataGridFormatRule.Value"/>.</summary>
    LessThanOrEqual,

    /// <summary>The value equals <see cref="DataGridFormatRule.Value"/>.</summary>
    Equal,

    /// <summary>The value differs from <see cref="DataGridFormatRule.Value"/> (and is not empty).</summary>
    NotEqual,

    /// <summary>The value is between <see cref="DataGridFormatRule.Value"/> and <see cref="DataGridFormatRule.Value2"/>, both included.</summary>
    Between,

    /// <summary>The cell's text contains <see cref="DataGridFormatRule.Value"/> (ignoring case and accents).</summary>
    Contains,

    /// <summary>The cell is empty.</summary>
    IsEmpty,

    /// <summary>The cell is not empty.</summary>
    IsNotEmpty,

    /// <summary>The value is a number below zero.</summary>
    Negative
}

/// <summary>
/// Tints the cells of a column whose value passes a test, such as an ARR below 10,000 or a stage that reads "Lost". Add rules to
/// <see cref="DataGridColumn.FormatRules"/>; the first rule that matches decides. Compared values are read the way the column
/// reads typed text, so <see cref="Value"/> for a date column is a day such as 2026-03-01.
/// </summary>
public sealed class DataGridFormatRule
{
    private (DataGridColumnType Type, string Culture, string? Text, object? Parsed) _first;
    private (DataGridColumnType Type, string Culture, string? Text, object? Parsed) _second;

    /// <summary>What to test (default <see cref="DataGridFormatCondition.GreaterThan"/>).</summary>
    public DataGridFormatCondition Condition { get; set; }

    /// <summary>The value to compare with, written as it would be typed into the column.</summary>
    public string? Value { get; set; }

    /// <summary>The upper bound of <see cref="DataGridFormatCondition.Between"/>.</summary>
    public string? Value2 { get; set; }

    /// <summary>The look of matching cells (default <see cref="DataGridTone.Danger"/>).</summary>
    public DataGridTone Tone { get; set; } = DataGridTone.Danger;

    /// <summary>Draws the text of matching cells in the medium weight.</summary>
    public bool Bold { get; set; }

    internal DataGridCellStyle Style => new(Tone, Bold);

    internal bool Matches(object? value, DataGridColumn column, CultureInfo culture)
    {
        switch (Condition)
        {
            case DataGridFormatCondition.IsEmpty:
                return DataGridSorting.IsEmpty(value);
            case DataGridFormatCondition.IsNotEmpty:
                return !DataGridSorting.IsEmpty(value);
            case DataGridFormatCondition.Negative:
                return value is decimal negative && negative < 0;
            case DataGridFormatCondition.Contains:
                var needle = Value ?? string.Empty;
                return needle.Length > 0 && !DataGridSorting.IsEmpty(value) && culture.CompareInfo.IndexOf(
                    DataGridValueFormat.Format(value, column.CellFormat, culture), needle, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
        }
        if (DataGridSorting.IsEmpty(value) || !TryRead(ref _first, Value, column, culture, out var a))
        {
            return false;
        }
        if (Condition == DataGridFormatCondition.Between)
        {
            if (!TryRead(ref _second, Value2, column, culture, out var b))
            {
                return false;
            }
            var (low, high) = DataGridSorting.Compare(a, b, null, culture) <= 0 ? (a, b) : (b, a);
            return DataGridSorting.Compare(value, low, null, culture) >= 0 && DataGridSorting.Compare(value, high, null, culture) <= 0;
        }
        var order = DataGridSorting.Compare(value, a, null, culture);
        return Condition switch
        {
            DataGridFormatCondition.GreaterThan => order > 0,
            DataGridFormatCondition.GreaterThanOrEqual => order >= 0,
            DataGridFormatCondition.LessThan => order < 0,
            DataGridFormatCondition.LessThanOrEqual => order <= 0,
            DataGridFormatCondition.Equal => order == 0,
            DataGridFormatCondition.NotEqual => order != 0,
            _ => false
        };
    }

    private static bool TryRead(ref (DataGridColumnType Type, string Culture, string? Text, object? Parsed) cache, string? text, DataGridColumn column,
                                CultureInfo culture, out object? parsed)
    {
        if (cache.Type != column.Type || cache.Culture != culture.Name || cache.Text != text)
        {
            DataGridValueParser.TryParse(text, column.CellFormat, culture, null, out var value);
            cache = (column.Type, culture.Name, text, value);
        }
        parsed = cache.Parsed;
        return parsed is not null;
    }
}
