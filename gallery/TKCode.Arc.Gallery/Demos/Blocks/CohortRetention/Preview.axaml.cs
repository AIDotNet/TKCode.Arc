using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CohortRetentionPreviewDemo : UserControl
{
    public CohortRetentionPreviewDemo()
    {
        InitializeComponent();
        Retention.Segments.AddRange(CohortRetentionDemoData.SpaceSegments());
    }
}
