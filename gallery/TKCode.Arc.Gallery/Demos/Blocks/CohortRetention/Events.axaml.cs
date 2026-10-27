using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CohortRetentionEventsDemo : UserControl
{
    private readonly List<string> _lines = new();

    public CohortRetentionEventsDemo()
    {
        InitializeComponent();
        Retention.Segments.AddRange(CohortRetentionDemoData.SpaceSegments());

        Toggle.Click += (_, _) => Retention.View =
            Retention.View == CohortRetentionView.Heatmap ? CohortRetentionView.Curves : CohortRetentionView.Heatmap;
        Newest.Click += (_, _) => Retention.CursorCell = new CohortCell(Retention.EffectiveSegments[0].Cohorts.Count - 1, 0);

        Retention.CursorChanged += (_, e) => Write(e.Cell is { } cell
            ? $"Cursor: {CellName(e)} month {cell.Month}, {e.Rate:P1} ({e.Retained:N0} of {e.Size:N0})"
            : "Cursor cleared");
        Retention.CellInvoked += (_, e) => Write($"Invoked: {CellName(e)} month {e.Cell?.Month}");
        Retention.ViewChanged += (_, e) => Write($"View: {e.OldView} → {e.NewView}");
        Retention.SegmentChanged += (_, e) => Write($"Segment: {e.OldSegment?.Label} → {e.NewSegment?.Label}");
    }

    private static string CellName(CohortCellEventArgs e) => e.Cohort?.Label ?? $"average of {e.CoveredCohorts} cohorts";

    private void Write(string line)
    {
        _lines.Insert(0, line);
        if (_lines.Count > 5)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }
}
