using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CohortRetentionWeeklyDemo : UserControl
{
    public CohortRetentionWeeklyDemo()
    {
        InitializeComponent();
        Retention.Thresholds = [0.10, 0.25, 0.40, 0.55, 0.70];
        Retention.Segments.AddRange(CohortRetentionDemoData.WeeklySegments());
    }
}
