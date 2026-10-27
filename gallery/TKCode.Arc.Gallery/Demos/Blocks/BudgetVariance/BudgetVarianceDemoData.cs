namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// Invented operating budget of a mid-sized software company for the Budget variance demos: three reporting windows
/// (September, the third quarter, the year to date) and seven departments with their budget lines. The quarter and the year
/// are built from each line's monthly plan with their own drift, so the variances differ by window.
/// </summary>
public static class BudgetVarianceDemoData
{
    public static BudgetPeriod[] Periods() =>
    [
        new("month", "Month", "September 2026", "in September"),
        new("quarter", "Quarter", "July to September 2026", "in the third quarter"),
        new("ytd", "Year to date", "January to September 2026", "so far this year"),
    ];

    public static BudgetDepartment[] Departments() =>
    [
        Department("engineering", "Engineering", "Ravi Menon",
            Line("salaries", "Salaries and benefits", 1_020_000, 0.962, 0.981, 0.994),
            Line("cloud", "Cloud hosting", 176_000, 1.214, 1.162, 1.118),
            Line("contractors", "Contractors", 92_000, 0.958, 1.034, 0.986),
            Line("tools", "Developer tools", 46_000, 1.072, 1.051, 1.026),
            Line("travel", "Travel", 11_500, 1.287, 1.093, 0.947)),
        Department("sales", "Sales", "Lucía Ortega",
            Line("salaries", "Salaries and commission", 538_000, 1.118, 1.064, 1.031),
            Line("events", "Events and trade shows", 96_000, 1.142, 0.912, 0.968),
            Line("travel", "Travel", 64_000, 1.083, 1.041, 1.012),
            Line("tools", "Sales software", 38_000, 0.987, 1.002, 0.996)),
        Department("marketing", "Marketing", "Owen Price",
            Line("ads", "Paid acquisition", 352_000, 0.884, 0.957, 1.021),
            Line("content", "Content and video", 118_000, 0.971, 0.934, 0.958),
            Line("brand", "Brand", 92_000, 0.912, 1.106, 1.044),
            Line("salaries", "Salaries and benefits", 88_000, 1.004, 0.998, 1.002)),
        Department("success", "Customer success", "Amara Okafor",
            Line("salaries", "Salaries and benefits", 286_000, 1.052, 1.038, 1.019),
            Line("support", "Support platform", 48_000, 1.091, 1.004, 0.982),
            Line("training", "Customer training", 31_000, 1.153, 1.217, 1.064)),
        Department("design", "Product design", "Jonas Becker",
            Line("salaries", "Salaries and benefits", 312_000, 1.004, 0.995, 0.989),
            Line("research", "User research", 34_000, 0.953, 1.088, 1.112),
            Line("tools", "Design tools", 22_000, 1.031, 1.012, 0.973)),
        Department("finance", "Finance and legal", "Mei Lin",
            Line("salaries", "Salaries and benefits", 164_000, 1.012, 1.006, 0.998),
            Line("audit", "Audit and tax", 58_000, 1.193, 0.874, 1.041),
            Line("legal", "Outside counsel", 44_000, 1.164, 1.236, 1.152)),
        Department("people", "People", "Tomás Silva",
            Line("recruiting", "Recruiting", 96_000, 0.774, 0.861, 0.912),
            Line("benefits", "Benefits administration", 46_000, 0.952, 0.981, 1.004),
            Line("events", "Team events", 28_000, 0.893, 1.127, 0.946)),
    ];

    private static BudgetDepartment Department(string id, string name, string owner, params BudgetLine[] lines)
    {
        var department = new BudgetDepartment(id, name, owner);
        department.Lines.AddRange(lines);
        return department;
    }

    /// <summary>A line from its monthly plan and the share actually spent in each window (1.0 = exactly on budget).</summary>
    private static BudgetLine Line(string id, string name, double monthly, double month, double quarter, double year)
        => new BudgetLine(id, name)
            .With("month", monthly, Hundreds(monthly * month))
            .With("quarter", monthly * 3, Hundreds(monthly * 3 * quarter))
            .With("ytd", monthly * 9, Hundreds(monthly * 9 * year));

    private static double Hundreds(double amount) => Math.Round(amount / 100) * 100;
}
