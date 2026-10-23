using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// The search marks of the memory card: which ranges are marked for a query (case, overlap, touching matches, unicode,
/// plain text only, empty queries), how the marks are reused, and how they look. The decoration ranges are the unit
/// under test; headless text advances 14 DIP a character, so the per-line rectangles are exact.
/// </summary>
public class HighlightedTextTests
{
    private static HighlightedText Create(string? text, string? query = null, double width = double.NaN) =>
        new() { Text = text, Query = query, Width = width };

    private static Window Show(Control content)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { content } });
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static (int Start, int Length)[] Ranges(HighlightedText text) =>
        text.Decorations.Select(d => (d.Start, d.Length)).ToArray();

    #region Ranges

    [AvaloniaFact]
    public void No_Query_Marks_Nothing()
    {
        foreach (var query in new string?[] { null, "", " ", "  \t " })
        {
            var text = Create("Prefers tables to long bullet lists", query);
            text.Decorations.ShouldBeEmpty();
            text.MatchCount.ShouldBe(0);
            HighlightedText.CountMatches("Prefers tables to long bullet lists", query, StringComparison.OrdinalIgnoreCase).ShouldBe(0);
        }
    }

    [AvaloniaFact]
    public void Matches_Ignore_Case_And_The_Text_Keeps_Its_Own()
    {
        var text = Create("Billing data feeds the billing report", "BILL");
        Ranges(text).ShouldBe([(0, 4), (23, 4)]);
        text.MatchCount.ShouldBe(2);
        text.Text.ShouldBe("Billing data feeds the billing report");
    }

    [AvaloniaFact]
    public void An_Ordinal_Comparison_Respects_Case()
    {
        var text = Create("Billing data feeds the billing report", "bill");
        text.Comparison = StringComparison.Ordinal;
        Ranges(text).ShouldBe([(23, 4)]);
        text.MatchCount.ShouldBe(1);
        text.Comparison = StringComparison.OrdinalIgnoreCase;
        text.MatchCount.ShouldBe(2);
    }

    [AvaloniaFact]
    public void Overlapping_Matches_Do_Not_Double_Up()
    {
        // "aaa" holds "aa" once (the second would overlap the first).
        var text = Create("aaa", "aa");
        Ranges(text).ShouldBe([(0, 2)]);
        text.MatchCount.ShouldBe(1);
        HighlightedText.CountMatches("aaa", "aa", StringComparison.OrdinalIgnoreCase).ShouldBe(1);
    }

    [AvaloniaFact]
    public void Touching_Matches_Share_One_Mark_And_Still_Count_Each()
    {
        var text = Create("ha haha!", "ha");
        Ranges(text).ShouldBe([(0, 2), (3, 4)]);
        text.MatchCount.ShouldBe(3);
        text = Create("aaaa", "AA");
        Ranges(text).ShouldBe([(0, 4)]);
        text.MatchCount.ShouldBe(2);
    }

    [AvaloniaFact]
    public void The_Query_Is_Plain_Text_Never_A_Pattern()
    {
        var text = Create("Runs builds with the CLI (not the IDE).", "(not");
        Ranges(text).ShouldBe([(25, 4)]);
        text.Query = "C.I";
        text.MatchCount.ShouldBe(0);
        text.Query = ".*";
        text.MatchCount.ShouldBe(0);
        text.Query = "IDE).";
        Ranges(text).ShouldBe([(34, 5)]);
        text.Query = "[a-z]";
        text.MatchCount.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Leading_And_Trailing_White_Space_Of_The_Query_Is_Ignored()
    {
        var text = Create("Meets Ingrid Holm every other Tuesday", "  ingrid holm ");
        Ranges(text).ShouldBe([(6, 11)]);
        HighlightedText.CountMatches("Meets Ingrid Holm every other Tuesday", " every ", StringComparison.OrdinalIgnoreCase).ShouldBe(1);
    }

    [AvaloniaFact]
    public void A_Query_Longer_Than_The_Text_Matches_Nothing()
    {
        var text = Create("Go", "Go further");
        text.Decorations.ShouldBeEmpty();
        text.MatchCount.ShouldBe(0);
        Create(null, "go").MatchCount.ShouldBe(0);
        Create("", "go").MatchCount.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Unicode_Text_Matches_Without_Splitting_Characters()
    {
        var text = Create("Pairs with Tomás Ferreira on routing", "TOMÁS");
        Ranges(text).ShouldBe([(11, 5)]);
        text = Create("Σοφία leads the ΣΟΦΊΑ project", "σοφία");
        Ranges(text).ShouldBe([(0, 5), (16, 5)]);
        text = Create("Ships on Fridays 🚀 and rests 🚀", "🚀");
        Ranges(text).ShouldBe([(17, 2), (30, 2)]);
        text = Create("记得用户喜欢简短的回答", "简短");
        Ranges(text).ShouldBe([(6, 2)]);
    }

    [AvaloniaFact]
    public void Culture_Comparisons_Mark_The_Matched_Length()
    {
        // A soft hyphen is ignored by culture-aware comparisons, so the match is one character longer than the query.
        var text = Create("Uses the re­gional depot planner", "regional");
        text.Comparison = StringComparison.InvariantCultureIgnoreCase;
        Ranges(text).ShouldBe([(9, 9)]);
        text.MatchCount.ShouldBe(1);
        HighlightedText.CountMatches("Uses the re­gional depot planner", "REGIONAL", StringComparison.InvariantCultureIgnoreCase).ShouldBe(1);
        // A query of ignorable characters only matches nothing (instead of everywhere).
        text.Query = "­";
        text.MatchCount.ShouldBe(0);
    }

    [AvaloniaFact]
    public void CountMatches_Agrees_With_The_Marks()
    {
        var samples = new[]
        {
            ("Leads the data platform team at a freight company", "a"),
            ("Is drafting a proposal on event sourcing", "ING"),
            ("aaaaaa", "aa"),
            ("Uses British spelling", "spelling"),
            ("Uses British spelling", "z")
        };
        foreach (var (sentence, query) in samples)
        {
            Create(sentence, query).MatchCount.ShouldBe(HighlightedText.CountMatches(sentence, query, StringComparison.OrdinalIgnoreCase));
        }
    }

    #endregion

    #region Updates and reuse

    [AvaloniaFact]
    public void Text_And_Query_Changes_Mark_Again_And_Report_The_Count()
    {
        var text = Create("Wants a code sample first", "code");
        var counts = new List<int>();
        text.PropertyChanged += (_, e) =>
        {
            if (e.Property == HighlightedText.MatchCountProperty)
            {
                counts.Add(e.GetNewValue<int>());
            }
        };
        text.Text = "Wants code first and code last";
        Ranges(text).ShouldBe([(6, 4), (21, 4)]);
        text.Query = "first";
        Ranges(text).ShouldBe([(11, 5)]);
        text.Query = null;
        text.Decorations.ShouldBeEmpty();
        counts.ShouldBe([2, 1, 0]);
    }

    [AvaloniaFact]
    public void The_Marks_Are_Reused_From_Change_To_Change()
    {
        var text = Create("one two one two one", "one");
        var marks = text.Decorations.ToArray();
        marks.Length.ShouldBe(3);
        text.Query = "two";
        text.Decorations.Count.ShouldBe(2);
        text.Decorations[0].ShouldBeSameAs(marks[0]);
        text.Decorations[1].ShouldBeSameAs(marks[1]);
        text.Query = "";
        text.Decorations.ShouldBeEmpty();
        text.Query = "o";
        text.Decorations.Count.ShouldBe(5);
        text.Decorations.Take(3).ShouldBe(marks);
    }

    [AvaloniaFact]
    public void An_Empty_Query_Allocates_Nothing()
    {
        var text = Create("Is training for a spring half marathon", "");
        var window = Show(text);
        try
        {
            text.Redecorate();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100; i++)
            {
                text.Redecorate();
            }
            (GC.GetAllocatedBytesForCurrentThread() - before).ShouldBe(0);
            text.Decorations.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Marking_The_Same_Ranges_Again_Allocates_Nothing()
    {
        var text = Create("Is moving the depot planner to Avalonia", "planner");
        var window = Show(text);
        try
        {
            text.Redecorate();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100; i++)
            {
                text.Redecorate();
            }
            (GC.GetAllocatedBytesForCurrentThread() - before).ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Looks

    [AvaloniaFact]
    public void Marks_Are_The_Accent_At_14_Percent_With_3px_Corners_And_No_Padding()
    {
        var text = Create("Prefers tables to long bullet lists", "tables");
        var window = Show(text);
        try
        {
            var accent = ArcTokens.GetColor(text, DesignTokens.ArcDesignTokenKind.Accent);
            var mark = text.Decorations.Single();
            var brush = mark.Background.ShouldBeAssignableTo<ISolidColorBrush>()!;
            brush.Color.ShouldBe(Color.FromArgb((byte)Math.Round(accent.A * 0.14), accent.R, accent.G, accent.B));
            brush.Opacity.ShouldBe(1);
            mark.CornerRadius.ShouldBe(3);
            mark.Outset.ShouldBe(default);
            mark.Extent.ShouldBe(TextRangeExtent.Text);
            mark.Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Accent_Retints_The_Marks_In_Place()
    {
        var text = Create("Prefers tables to long tables", "tables");
        var window = Show(text);
        try
        {
            var marks = text.Decorations.ToArray();
            text.AccentBrush = new SolidColorBrush(Color.FromRgb(200, 40, 40));
            foreach (var mark in marks)
            {
                mark.Background.ShouldBeSameAs(text.MarkBrush);
            }
            ((ISolidColorBrush)text.MarkBrush!).Color.ShouldBe(Color.FromArgb(36, 200, 40, 40));
            text.Decorations.ShouldBe(marks);
            // Marks made later take the new tint too.
            text.Query = "o";
            text.Decorations.ShouldAllBe(d => ReferenceEquals(d.Background, text.MarkBrush));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Match_That_Wraps_Is_Marked_On_Each_Line()
    {
        // 14 DIP a character: at 168 wide (12 characters) the lines are "Writes most", "services in", "C# and some"...
        var text = Create("Writes most services in C# and some tooling in Go", "most services", 168);
        var window = Show(text);
        try
        {
            var layout = text.TextLayout;
            layout.TextLines.Count.ShouldBeGreaterThan(2);
            var mark = text.Decorations.Single();
            var rects = TextRangeRenderer.GetRects(layout, mark.Start, mark.Length);
            rects.Count.ShouldBe(2);
            rects[1].Top.ShouldBeGreaterThan(rects[0].Top);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Default_Look_Is_The_Memory_Sentence_And_It_Is_Not_A_Tab_Stop()
    {
        var text = Create("Uses British spelling in written documents");
        var window = Show(text);
        try
        {
            text.FontSize.ShouldBe(14);
            text.LineHeight.ShouldBe(22);
            text.TextWrapping.ShouldBe(TextWrapping.Wrap);
            TestHelpers.ColorOf(text.Foreground).ShouldBe(ArcTokens.GetColor(text, DesignTokens.ArcDesignTokenKind.Foreground));
            KeyboardNavigation.GetIsTabStop(text).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion
}
