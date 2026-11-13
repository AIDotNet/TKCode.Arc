using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Cards;

/// <summary>
/// A Card styles its body for paragraphs (small type, a 1.4 × 14px line height), and the line height is inherited by
/// everything inside. A chart readout set in large type takes that 19.6px line box unless it sets its own; with Geist the
/// text is then clipped (seen in the A2UI Chat sales dashboard, 2026-11-14).
/// </summary>
public class CardContentTextTests
{
    [AvaloniaTheory]
    [InlineData("DonutChart", "PART_CenterValue")]
    [InlineData("BarChart", "PART_ValueText")]
    public void Large_chart_readouts_in_a_card_keep_their_own_line_height(string chart, string part)
    {
        Control control = chart == "DonutChart"
            ? new DonutChart { ItemsSource = new[] { new ChartPoint("A", 659), new ChartPoint("B", 389) } }
            : new BarChart { ItemsSource = new[] { new ChartPoint("A", 659), new ChartPoint("B", 389) } };
        var window = TestHelpers.Show(new Card { Content = control, Width = 420 });
        try
        {
            var readout = control.GetVisualDescendants().OfType<Control>().Single(c => c.Name == part);
            double.IsNaN(TextBlock.GetLineHeight(readout)).ShouldBeTrue();
            foreach (var text in readout.GetVisualDescendants().OfType<TextBlock>())
            {
                double.IsNaN(text.LineHeight).ShouldBeTrue($"'{text.Text}' inherits a line height of {text.LineHeight}");
            }

            // The rest of the chart still takes the card's body type (the label, the legend).
            control.GetVisualDescendants().OfType<TextBlock>().Any(t => t.LineHeight > 19 && t.LineHeight < 20).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
