using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CohortRetentionCurvesDemo : UserControl
{
    public CohortRetentionCurvesDemo()
    {
        InitializeComponent();
        Retention.Segments.AddRange(CohortRetentionDemoData.SpaceSegments());
        Retention.SelectSegment("business");
    }
}
