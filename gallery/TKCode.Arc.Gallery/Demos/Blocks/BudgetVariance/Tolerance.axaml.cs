using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class BudgetVarianceToleranceDemo : UserControl
{
    public BudgetVarianceToleranceDemo()
    {
        InitializeComponent();
        Budget.Periods.AddRange(BudgetVarianceDemoData.Periods());
        Budget.Departments.AddRange(BudgetVarianceDemoData.Departments());
    }
}
