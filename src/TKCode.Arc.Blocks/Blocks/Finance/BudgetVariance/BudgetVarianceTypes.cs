using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>A reporting window of <see cref="BudgetVariance"/>: what the period switch offers ("Month", "Quarter", "Year to date").</summary>
public sealed class BudgetPeriod : AvaloniaObject
{
    public BudgetPeriod()
    {
    }

    public BudgetPeriod(string id, string label, string? description = null, string? phrase = null)
    {
        Id = id;
        Label = label;
        Description = description;
        Phrase = phrase;
    }

    /// <summary>The key the lines' amounts use for this window ("month").</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The short label on the period switch ("Quarter").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The line under the block's title while this window is shown ("July to September 2026").</summary>
    public string? Description { get; set; }

    /// <summary>The phrase that ends screen reader sentences ("in the third quarter"); null leaves it out.</summary>
    public string? Phrase { get; set; }
}

/// <summary>One line's budget and actual spend in one reporting window; <see cref="Period"/> is the window's id.</summary>
public sealed class BudgetAmount : AvaloniaObject
{
    public BudgetAmount()
    {
    }

    public BudgetAmount(string period, double budget, double actual)
    {
        Period = period;
        Budget = budget;
        Actual = actual;
    }

    /// <summary>The <see cref="BudgetPeriod.Id"/> these amounts belong to.</summary>
    public string Period { get; set; } = string.Empty;

    /// <summary>What was planned for the window.</summary>
    public double Budget { get; set; }

    /// <summary>What was spent in the window.</summary>
    public double Actual { get; set; }
}

/// <summary>A budget line of a <see cref="BudgetDepartment"/> ("Cloud hosting"), with one <see cref="BudgetAmount"/> per window.</summary>
public sealed class BudgetLine : AvaloniaObject
{
    public BudgetLine()
    {
    }

    public BudgetLine(string id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>A key that stays the same while the name changes.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The line's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The budget and actual spend per window; a window without an entry counts as zero.</summary>
    [Content]
    public AvaloniaList<BudgetAmount> Amounts { get; } = new();

    /// <summary>Adds the amounts of one window and returns the line (for building data in code).</summary>
    public BudgetLine With(string period, double budget, double actual)
    {
        Amounts.Add(new BudgetAmount(period, budget, actual));
        return this;
    }

    /// <summary>The amounts for <paramref name="period"/> (zero when there is no entry).</summary>
    public (double Budget, double Actual) In(string period)
    {
        foreach (var amount in Amounts)
        {
            if (amount.Period == period)
            {
                return (amount.Budget, amount.Actual);
            }
        }
        return (0, 0);
    }
}

/// <summary>A department of <see cref="BudgetVariance"/>: a name, an owner and its budget lines, whose amounts it sums.</summary>
public sealed class BudgetDepartment : AvaloniaObject
{
    public BudgetDepartment()
    {
    }

    public BudgetDepartment(string id, string name, string? owner = null)
    {
        Id = id;
        Name = name;
        Owner = owner;
    }

    /// <summary>A key that stays the same while the name changes (<see cref="BudgetVariance.ExpandedDepartment"/> uses it).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The department's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The person who owns the budget, shown under the name; null leaves the line out.</summary>
    public string? Owner { get; set; }

    /// <summary>The owner's photo; null draws their initials.</summary>
    public ImageSource? OwnerAvatar { get; set; }

    /// <summary>The budget lines; the department's figures are their sums.</summary>
    [Content]
    public AvaloniaList<BudgetLine> Lines { get; } = new();

    /// <summary>The summed amounts for <paramref name="period"/>.</summary>
    public (double Budget, double Actual) In(string period)
    {
        double budget = 0, actual = 0;
        foreach (var line in Lines)
        {
            var (b, a) = line.In(period);
            budget += b;
            actual += a;
        }
        return (budget, actual);
    }
}

/// <summary>Arguments of <see cref="BudgetVariance.PeriodChanged"/>.</summary>
public sealed class BudgetPeriodChangedEventArgs : RoutedEventArgs
{
    public BudgetPeriodChangedEventArgs(RoutedEvent routedEvent, string? oldPeriod, string? newPeriod) : base(routedEvent)
    {
        OldPeriod = oldPeriod;
        NewPeriod = newPeriod;
    }

    /// <summary>The window shown before.</summary>
    public string? OldPeriod { get; }

    /// <summary>The window shown now.</summary>
    public string? NewPeriod { get; }
}

/// <summary>Arguments of <see cref="BudgetVariance.DepartmentToggled"/>.</summary>
public sealed class BudgetDepartmentToggledEventArgs : RoutedEventArgs
{
    public BudgetDepartmentToggledEventArgs(RoutedEvent routedEvent, BudgetDepartment department, bool isExpanded) : base(routedEvent)
    {
        Department = department;
        IsExpanded = isExpanded;
    }

    /// <summary>The department that opened or closed.</summary>
    public BudgetDepartment Department { get; }

    /// <summary>True when its lines are now shown.</summary>
    public bool IsExpanded { get; }
}

/// <summary>How a figure compares with its budget.</summary>
public enum BudgetVarianceKind
{
    /// <summary>Within the on-plan threshold of the budget.</summary>
    OnPlan,

    /// <summary>Under budget: favorable for spending.</summary>
    Under,

    /// <summary>Over budget: unfavorable for spending.</summary>
    Over,
}
