using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CohortRetentionCompactDemo : UserControl
{
    public CohortRetentionCompactDemo()
    {
        InitializeComponent();
        Retention.Segments.AddRange(CohortRetentionDemoData.SpaceSegments());
    }
}
