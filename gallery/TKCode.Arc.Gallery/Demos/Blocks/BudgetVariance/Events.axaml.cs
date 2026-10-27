using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class BudgetVarianceEventsDemo : UserControl
{
    private readonly List<string> _lines = new();

    public BudgetVarianceEventsDemo()
    {
        InitializeComponent();
        Budget.Periods.AddRange(BudgetVarianceDemoData.Periods());
        Budget.Departments.AddRange(BudgetVarianceDemoData.Departments());

        // Opening a department closes the open one, which is reported first.
        Budget.PeriodChanged += (_, e) => Write($"PeriodChanged: {e.OldPeriod} → {e.NewPeriod}");
        Budget.DepartmentToggled += (_, e) => Write($"DepartmentToggled: {e.Department.Name} {(e.IsExpanded ? "opened" : "closed")}");
    }

    private void Write(string line)
    {
        _lines.Add(line);
        if (_lines.Count > 3)
        {
            _lines.RemoveAt(0);
        }
        Log.Text = string.Join("\n", _lines);
    }
}
