using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class BudgetVarianceNarrowDemo : UserControl
{
    public BudgetVarianceNarrowDemo()
    {
        InitializeComponent();
        Budget.Periods.AddRange(BudgetVarianceDemoData.Periods());
        Budget.Departments.AddRange(BudgetVarianceDemoData.Departments());
    }
}
