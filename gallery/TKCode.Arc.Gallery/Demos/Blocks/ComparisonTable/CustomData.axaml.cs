using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ComparisonTableCustomDataDemo : UserControl
{
    public ComparisonTableCustomDataDemo()
    {
        InitializeComponent();
        Table.Columns =
        [
            new ComparisonColumn { Id = "us", Name = "Quillpad", Caption = "$6 per month", IsHighlighted = true },
            new ComparisonColumn { Id = "paper", Name = "Paper notebook", Caption = "$12 once" }
        ];
        Table.Sections =
        [
            new ComparisonSection
            {
                Id = "basics",
                Title = "Basics",
                Rows =
                [
                    new ComparisonRow { Id = "search", Feature = "Search", Values = { ["us"] = true, ["paper"] = false } },
                    new ComparisonRow { Id = "sketch", Feature = "Sketching", Values = { ["us"] = ComparisonValue.Partial("With a stylus"), ["paper"] = true } },
                    new ComparisonRow { Id = "battery", Feature = "Battery", Hint = "A full day of writing", Values = { ["us"] = "12 hours", ["paper"] = "None needed" } }
                ]
            }
        ];
    }
}
