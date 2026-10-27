using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class BudgetVariancePreviewDemo : UserControl
{
    public BudgetVariancePreviewDemo()
    {
        InitializeComponent();
        Budget.Periods.AddRange(BudgetVarianceDemoData.Periods());
        Budget.Departments.AddRange(BudgetVarianceDemoData.Departments());
    }
}
