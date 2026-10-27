namespace TKCode.Arc;

/// <summary>
/// The built-in text of <see cref="BudgetVariance"/>. Replace the whole object to localize the block; parameterized entries
/// are functions that receive already formatted amounts.
/// </summary>
public sealed class BudgetVarianceLabels
{
    /// <summary>Header of the name column: "Department".</summary>
    public string Department { get; init; } = "Department";

    /// <summary>Header of the actual column: "Actual".</summary>
    public string Actual { get; init; } = "Actual";

    /// <summary>Header of the budget column: "Budget".</summary>
    public string Budget { get; init; } = "Budget";

    /// <summary>Header of the variance column: "Variance".</summary>
    public string Variance { get; init; } = "Variance";

    /// <summary>Name of the total row: "Total spend".</summary>
    public string Total { get; init; } = "Total spend";

    /// <summary>Under the total's name: how many departments ⇒ "7 departments".</summary>
    public Func<int, string> DepartmentCount { get; init; } = count => count == 1 ? "1 department" : $"{count} departments";

    /// <summary>A figure against its budget in the narrow layout: (actual, budget) ⇒ "$1.37M of $1.37M".</summary>
    public Func<string, string, string> ActualOfBudget { get; init; } = (actual, budget) => $"{actual} of {budget}";

    /// <summary>After a share over budget: "over".</summary>
    public string Over { get; init; } = "over";

    /// <summary>After a share under budget: "under".</summary>
    public string Under { get; init; } = "under";

    /// <summary>After a share within the on-plan threshold: "on plan".</summary>
    public string OnPlan { get; init; } = "on plan";

    /// <summary>Legend entry of the actual bar: "Actual".</summary>
    public string LegendActual { get; init; } = "Actual";

    /// <summary>Legend entry of the budget tick: "Budget".</summary>
    public string LegendBudget { get; init; } = "Budget";

    /// <summary>Legend entry of the overspend: "Over budget".</summary>
    public string LegendOver { get; init; } = "Over budget";

    /// <summary>Legend entry of the tolerance band: (low, high) percentages ⇒ "90 to 110% of budget".</summary>
    public Func<string, string, string> LegendBand { get; init; } = (low, high) => $"{low} to {high}% of budget";

    /// <summary>
    /// The screen reader sentence of a figure: (name, actual, budget, phrase, variance amount, variance share, kind) ⇒
    /// "Sales: spent $833.9k of a $756.0k budget in September; $77.9k, 10.3%, over budget, unfavorable." The phrase is the
    /// period's <see cref="BudgetPeriod.Phrase"/> with a leading space, or empty.
    /// </summary>
    public Func<string, string, string, string, string, string, BudgetVarianceKind, string> Sentence { get; init; } =
        (name, actual, budget, phrase, amount, share, kind) => kind switch
        {
            BudgetVarianceKind.Over => $"{name}: spent {actual} of a {budget} budget{phrase}; {amount}, {share}, over budget, unfavorable.",
            BudgetVarianceKind.Under => $"{name}: spent {actual} of a {budget} budget{phrase}; {amount}, {share}, under budget, favorable.",
            _ => $"{name}: spent {actual} of a {budget} budget{phrase}; on plan.",
        };

    /// <summary>Announced after the window changes: (period label, total sentence) ⇒ "Quarter. Total spend: …".</summary>
    public Func<string, string, string> PeriodAnnouncement { get; init; } = (period, total) => $"{period}. {total}";

    /// <summary>Screen reader name of the period switch: "Reporting period".</summary>
    public string PeriodSwitch { get; init; } = "Reporting period";

    /// <summary>Screen reader name of a department's lines: name ⇒ "Engineering budget lines".</summary>
    public Func<string, string> LinesRegion { get; init; } = name => $"{name} budget lines";
}
