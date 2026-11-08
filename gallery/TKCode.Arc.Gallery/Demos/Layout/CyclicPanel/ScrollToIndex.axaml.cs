using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CyclicPanelScrollToIndexDemo : UserControl
{
    public CyclicPanelScrollToIndexDemo()
    {
        InitializeComponent();
        Top.Click += (_, _) => Months.ScrollToIndex(7);
        Center.Click += (_, _) => Months.ScrollToIndexCentered(0);
        Today.Click += (_, _) => Months.ScrollToIndexCentered(DateTime.Today.Month - 1);
        Jump.Click += (_, _) => Months.ScrollToIndex(6, animated: false);
    }
}
