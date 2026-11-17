using Avalonia.Headless.XUnit;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiPanelSelectionText"/>: which characters of each text a range covers, the block a range belongs to (where it
/// starts, whichever way it was dragged), labels (a table's label; a heading's text; the heading above), the joined text
/// (tabs between cells of a row, line breaks between rows and blocks) and word selection.
/// </summary>
public class AiPanelSelectionTextTests
{
    private static (AiPanelPage Page, List<AiPanelTextPiece> Pieces) Sample()
    {
        var page = AiPanelPartSamples.Page();
        var pieces = new List<AiPanelTextPiece>();
        foreach (var block in page.Blocks)
        {
            switch (block)
            {
                case AiPanelParagraph p:
                    pieces.Add(new AiPanelTextPiece(p, p.Text));
                    break;
                case AiPanelHeading h:
                    pieces.Add(new AiPanelTextPiece(h, h.Text));
                    break;
                case AiPanelTable t:
                    for (var c = 0; c < t.Columns.Count; c++)
                    {
                        pieces.Add(new AiPanelTextPiece(t, t.Columns[c].Header, 0, c));
                    }
                    for (var r = 0; r < t.Rows.Count; r++)
                    {
                        for (var c = 0; c < t.Columns.Count; c++)
                        {
                            pieces.Add(new AiPanelTextPiece(t, t.Rows[r].Cells[c], r + 1, c));
                        }
                    }
                    break;
            }
        }
        return (page, pieces);
    }

    [AvaloniaFact]
    public void Range_Covers_The_Start_Text_From_Its_Offset_The_Middle_Whole_And_The_End_Up_To_Its_Offset()
    {
        var start = new AiPanelTextPoint(1, 4);
        var end = new AiPanelTextPoint(3, 2);
        AiPanelSelectionText.RangeIn(0, 10, start, end).ShouldBe((0, 0));
        AiPanelSelectionText.RangeIn(1, 10, start, end).ShouldBe((4, 10));
        AiPanelSelectionText.RangeIn(2, 7, start, end).ShouldBe((0, 7));
        AiPanelSelectionText.RangeIn(3, 10, start, end).ShouldBe((0, 2));
        AiPanelSelectionText.RangeIn(4, 10, start, end).ShouldBe((0, 0));
        // Within one text; offsets past the end are clamped; an empty range selects nothing.
        AiPanelSelectionText.RangeIn(2, 7, new AiPanelTextPoint(2, 3), new AiPanelTextPoint(2, 99)).ShouldBe((3, 7));
        AiPanelSelectionText.RangeIn(2, 7, new AiPanelTextPoint(2, 3), new AiPanelTextPoint(2, 3)).ShouldBe((0, 0));
    }

    [AvaloniaFact]
    public void A_Range_In_One_Paragraph_Reads_Its_Characters_And_Takes_The_Paragraph_Label()
    {
        var (page, pieces) = Sample();
        var selection = AiPanelSelectionText.Describe(pieces, new AiPanelTextPoint(0, 0), new AiPanelTextPoint(0, 12), page.Blocks);
        selection.ShouldBe(new AiPanelSelection("summary", "Summary", "Offline sync"));
    }

    [AvaloniaFact]
    public void A_Range_Dragged_Backwards_Is_The_Same_Range()
    {
        var (page, pieces) = Sample();
        var forward = AiPanelSelectionText.Describe(pieces, new AiPanelTextPoint(0, 8), new AiPanelTextPoint(2, 5), page.Blocks);
        var backward = AiPanelSelectionText.Describe(pieces, new AiPanelTextPoint(2, 5), new AiPanelTextPoint(0, 8), page.Blocks);
        backward.ShouldBe(forward);
    }

    [AvaloniaFact]
    public void A_Range_Across_Blocks_Belongs_To_The_Block_It_Starts_In_And_Joins_Blocks_With_Line_Breaks()
    {
        var (page, pieces) = Sample();
        var summary = AiPanelPartSamples.Summary;
        // From "a while." at the end of the summary into the scope paragraph, dragged upward.
        var selection = AiPanelSelectionText.Describe(pieces, new AiPanelTextPoint(2, 5), new AiPanelTextPoint(0, summary.Length - 8), page.Blocks);
        selection.ShouldNotBeNull();
        selection.BlockId.ShouldBe("summary");
        selection.Label.ShouldBe("Summary");
        selection.Text.ShouldBe("a while.\nScope\nNotes");
    }

    [AvaloniaFact]
    public void A_Range_Starting_At_The_Very_End_Of_A_Text_Belongs_To_The_Next_Block_With_Characters()
    {
        var (page, pieces) = Sample();
        var summary = AiPanelPartSamples.Summary;
        var selection = AiPanelSelectionText.Describe(pieces, new AiPanelTextPoint(0, summary.Length), new AiPanelTextPoint(2, 5), page.Blocks);
        selection.ShouldNotBeNull();
        // The heading is the first block with selected characters; it has no label, so its text names it.
        selection.BlockId.ShouldBe("scope-heading");
        selection.Label.ShouldBe("Scope");
        selection.Text.ShouldBe("Scope\nNotes");
    }

    [AvaloniaFact]
    public void A_Table_Selection_Takes_The_Table_Label_And_Joins_Cells_With_Tabs_And_Rows_With_Line_Breaks()
    {
        var (page, pieces) = Sample();
        var first = pieces.FindIndex(p => p.Block.Id == "limits-table" && p.Row == 1 && p.Column == 0);
        var last = pieces.FindIndex(p => p.Block.Id == "limits-table" && p.Row == 2 && p.Column == 2);
        var selection = AiPanelSelectionText.Describe(pieces, new AiPanelTextPoint(first, 0), new AiPanelTextPoint(last, 4), page.Blocks);
        selection.ShouldBe(new AiPanelSelection("limits-table", "Storage table", "Basic\t1 device\t200 MB\nPlus\t3 devices\t1 GB"));
    }

    [AvaloniaFact]
    public void An_Empty_Range_Is_No_Selection()
    {
        var (page, pieces) = Sample();
        AiPanelSelectionText.Describe(pieces, new AiPanelTextPoint(0, 5), new AiPanelTextPoint(0, 5), page.Blocks).ShouldBeNull();
        // From the end of one text to the start of the next: no character between them.
        AiPanelSelectionText.Describe(pieces, new AiPanelTextPoint(1, 5), new AiPanelTextPoint(2, 0), page.Blocks).ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_Block_Without_A_Label_Is_Named_By_The_Heading_Above_It_Or_Not_At_All()
    {
        var page = AiPanelPartSamples.Page();
        var scope = page.Find("scope")!;
        scope.Label = null;
        AiPanelSelectionText.LabelOf(scope, page.Blocks).ShouldBe("Scope");
        var summary = page.Find("summary")!;
        summary.Label = " ";
        AiPanelSelectionText.LabelOf(summary, page.Blocks).ShouldBe(string.Empty);
        var heading = page.Find("limits-heading")!;
        heading.Label = "Limits section";
        AiPanelSelectionText.LabelOf(heading, page.Blocks).ShouldBe("Limits section");
    }

    [AvaloniaFact]
    public void A_Word_Is_Letters_And_Digits_With_Inner_Joiners()
    {
        const string text = "Each device keeps up to 30 days of edits, don't-lose.";
        AiPanelSelectionText.WordAt(text, 7).ShouldBe((5, 11));
        // A caret right after a word picks that word.
        AiPanelSelectionText.WordAt(text, 11).ShouldBe((5, 11));
        AiPanelSelectionText.WordAt(text, 25).ShouldBe((24, 26));
        AiPanelSelectionText.WordAt(text, text.IndexOf("don't", StringComparison.Ordinal) + 2).ShouldBe((text.IndexOf("don't", StringComparison.Ordinal), text.Length - 1));
        // Punctuation and spaces select one character.
        AiPanelSelectionText.WordAt(text, text.IndexOf(',', StringComparison.Ordinal) + 1).ShouldBe((text.IndexOf(',', StringComparison.Ordinal) + 1, text.IndexOf(',', StringComparison.Ordinal) + 2));
        AiPanelSelectionText.WordAt(string.Empty, 0).ShouldBe((0, 0));
    }
}
