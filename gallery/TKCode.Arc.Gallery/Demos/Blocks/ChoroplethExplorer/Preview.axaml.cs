using Avalonia.Controls;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class ChoroplethExplorerPreviewDemo : UserControl
{
    public ChoroplethExplorerPreviewDemo()
    {
        InitializeComponent();

        // The United States is read now; the other seven map files are read after the first frame (the switch fills in).
        var areas = ChoroplethExplorerDemoData.Areas(eager: false);
        Explorer.Areas.AddRange(areas);
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var area in areas)
            {
                ChoroplethExplorerDemoData.Load(area);
            }
        }, DispatcherPriority.Background);
    }
}
