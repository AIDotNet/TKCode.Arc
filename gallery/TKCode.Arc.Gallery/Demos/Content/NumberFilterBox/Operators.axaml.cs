using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class NumberFilterBoxOperatorsDemo : UserControl
{
    public NumberFilterBoxOperatorsDemo()
    {
        InitializeComponent();
        // Only an upper or a lower bound makes sense for a budget.
        Budget.Operators = [NumberOperator.LessThanOrEqual, NumberOperator.GreaterThanOrEqual];
        Budget.FilterChanged += (_, _) => Show();
        Show();
    }

    private void Show() =>
        Summary.Text = Budget.IsActive
            ? $"{FilterOperators.Name(Budget.Operator)} {Budget.Value:0.00}"
            : "Budget filter is off";
}
