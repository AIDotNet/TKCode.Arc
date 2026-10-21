namespace TKCode.Arc.Tests.Editors;

using Avalonia.Headless.XUnit;

/// <summary>
/// Markdown import diagnostics: which constructs the editor cannot store, the ranges they cover, and the strict import
/// that refuses a lossy source without touching the editor.
/// </summary>
public class RichTextEditorImportTests
{
    private const string IssueExample = "#### 四级标题\n\n```csharp\nvar n = 1;\n```\n\n"
                                      + "| A | B |\n|---|---|\n| 1 | 2 |\n";

    [Fact]
    public void The_Issue_Example_Reports_The_Heading_Fence_Info_And_Table()
    {
        var report = RichTextEditorMarkdown.Analyze(IssueExample);

        report.HasLosses.ShouldBeTrue();
        report.IsLossless.ShouldBeFalse();
        report.Issues.Select(i => i.Kind).ShouldBe(
        [
            RichTextImportIssueKind.HeadingLevelDowngraded,
            RichTextImportIssueKind.CodeFenceInfoDropped,
            RichTextImportIssueKind.Table
        ]);
        report.Issues.ShouldAllBe(i => i.Severity == RichTextImportSeverity.Lossy);

        var heading = report.Issues[0];
        heading.Line.ShouldBe(1);
        heading.Column.ShouldBe(1);
        heading.Length.ShouldBe(4);
        heading.Source.ShouldBe("#### 四级标题");

        var fence = report.Issues[1];
        fence.Line.ShouldBe(3);
        fence.Column.ShouldBe(4);
        fence.Length.ShouldBe("csharp".Length);
        fence.Source.ShouldBe("```csharp");

        var table = report.Issues[2];
        table.Line.ShouldBe(7);
        table.Column.ShouldBe(1);
        table.Source.ShouldBe("| A | B |");
    }

    [Fact]
    public void Supported_Input_Produces_No_Warning()
    {
        string[] sources =
        [
            string.Empty,
            "# Title\n\n## Two\n\n### Three",
            "A paragraph with **bold**, _italic_, ~~strike~~ and `code`.",
            "A [link](https://uiarc.dev) and an `![image in code](x)` inside code.",
            "- one\n  - nested\n- two",
            "1. first\n2. second",
            "> quoted\n> lines",
            "```\ncode\n```",
            "---",
            "Text with 5. not a list and - not a bullet",
            "snake_case_name and a_b_c",
            "### Heading with `code` and **bold**",
            "Paragraph\nwith a soft wrap",
            "> ```\n> code in quote\n> ```",
            "a | b in prose",
            "`\\*` stays literal inside code"
        ];

        foreach (var source in sources)
        {
            RichTextEditorMarkdown.Analyze(source).IsEmpty.ShouldBeTrue(source.Replace("\n", "\\n"));
        }
    }

    [Theory]
    [InlineData("~~~cs\nx\n~~~", RichTextImportIssueKind.TildeFence)]
    [InlineData("Title\n=====", RichTextImportIssueKind.SetextHeading)]
    [InlineData("Title\n-----", RichTextImportIssueKind.SetextHeading)]
    [InlineData("text\n\n    var n = 1;\n", RichTextImportIssueKind.IndentedCode)]
    [InlineData("5. five\n6. six", RichTextImportIssueKind.ListStartNumber)]
    [InlineData("line one  \nline two", RichTextImportIssueKind.HardLineBreak)]
    [InlineData("<div>\nhello\n</div>", RichTextImportIssueKind.HtmlBlock)]
    [InlineData("![alt](https://x/y.png)", RichTextImportIssueKind.Image)]
    [InlineData("[a][ref]\n\n[ref]: https://x", RichTextImportIssueKind.ReferenceLink)]
    [InlineData("Text[^1]\n\n[^1]: note", RichTextImportIssueKind.Footnote)]
    [InlineData("\\*not italic\\*", RichTextImportIssueKind.Escape)]
    [InlineData("###### Six", RichTextImportIssueKind.HeadingLevelDowngraded)]
    public void Unsupported_Constructs_Are_Reported_As_Losses(string markdown, RichTextImportIssueKind kind)
    {
        var report = RichTextEditorMarkdown.Analyze(markdown);

        report.Issues.ShouldContain(i => i.Kind == kind);
        report.Issues.First(i => i.Kind == kind).Severity.ShouldBe(RichTextImportSeverity.Lossy);
        report.HasLosses.ShouldBeTrue();
    }

    [Theory]
    [InlineData("* one\n* two", RichTextImportIssueKind.BulletMarker)]
    [InlineData("+ one", RichTextImportIssueKind.BulletMarker)]
    [InlineData("1) one", RichTextImportIssueKind.NumberedMarker)]
    [InlineData("__x__", RichTextImportIssueKind.StrongMarker)]
    [InlineData("*x*", RichTextImportIssueKind.ItalicMarker)]
    [InlineData("***", RichTextImportIssueKind.ThematicBreakMarker)]
    [InlineData("___", RichTextImportIssueKind.ThematicBreakMarker)]
    public void Alternative_Markers_Are_Normalizations_Not_Losses(string markdown, RichTextImportIssueKind kind)
    {
        var report = RichTextEditorMarkdown.Analyze(markdown);

        report.Issues.ShouldContain(i => i.Kind == kind);
        report.Issues.First(i => i.Kind == kind).Severity.ShouldBe(RichTextImportSeverity.Normalized);
        report.HasLosses.ShouldBeFalse();
        report.IsLossless.ShouldBeTrue();
        report.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void Issues_Are_In_Source_Order_And_Lines_Count_From_One()
    {
        var report = RichTextEditorMarkdown.Analyze("First\n\n#### Four\n\n![a](b)\n\n| A |\n|---|\n");

        report.Issues.Select(i => i.Line).ShouldBe([3, 5, 7]);
        report.Issues.Select(i => i.Kind).ShouldBe(
        [
            RichTextImportIssueKind.HeadingLevelDowngraded,
            RichTextImportIssueKind.Image,
            RichTextImportIssueKind.Table
        ]);
    }

    [Fact]
    public void Windows_Line_Endings_Do_Not_Shift_The_Ranges()
    {
        var report = RichTextEditorMarkdown.Analyze("#### T\r\n\r\n```cs\r\nx\r\n```");

        report.Issues.Select(i => (i.Kind, i.Line, i.Column)).ShouldBe(
        [
            (RichTextImportIssueKind.HeadingLevelDowngraded, 1, 1),
            (RichTextImportIssueKind.CodeFenceInfoDropped, 3, 4)
        ]);
    }

    [Fact]
    public void A_Report_Is_Empty_And_Shared_When_Nothing_Is_Wrong()
    {
        RichTextEditorMarkdown.Analyze(null).ShouldBeSameAs(RichTextImportReport.Empty);
        RichTextEditorMarkdown.Analyze(string.Empty).ShouldBeSameAs(RichTextImportReport.Empty);
        RichTextEditorMarkdown.Analyze("# Fine").IsEmpty.ShouldBeTrue();
        RichTextImportReport.Empty.HasLosses.ShouldBeFalse();
        RichTextImportReport.Empty.IsLossless.ShouldBeTrue();
        RichTextImportReport.Empty.Issues.ShouldBeEmpty();
    }

    [Fact]
    public void Long_Source_Lines_Are_Shortened()
    {
        var report = RichTextEditorMarkdown.Analyze("| " + new string('x', 400) + " |\n|---|\n");

        report.Issues.Single().Source.Length.ShouldBeLessThanOrEqualTo(120);
        report.Issues.Single().Source.ShouldEndWith("…");
    }

    [AvaloniaFact]
    public void TrySetMarkdown_Refuses_A_Lossy_Source_And_Keeps_The_Content()
    {
        var editor = new RichTextEditor { Markdown = "# Original" };
        editor.GetMarkdown().ShouldBe("# Original");

        editor.TrySetMarkdown(IssueExample, out var report).ShouldBeFalse();

        report.HasLosses.ShouldBeTrue();
        report.Issues.Count.ShouldBe(3);
        editor.GetMarkdown().ShouldBe("# Original");
        editor.CanUndo.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void TrySetMarkdown_Imports_A_Lossless_Source_As_One_Undo_Step()
    {
        var editor = new RichTextEditor { Markdown = "# Original" };

        editor.TrySetMarkdown("## Notes\n\nA **bold** note.", out var report).ShouldBeTrue();

        report.IsEmpty.ShouldBeTrue();
        editor.GetMarkdown().ShouldBe("## Notes\n\nA **bold** note.");
        editor.CanUndo.ShouldBeTrue();
        editor.Undo();
        editor.GetMarkdown().ShouldBe("# Original");
    }

    [AvaloniaFact]
    public void TrySetMarkdown_Accepts_A_Source_That_Is_Only_Normalized()
    {
        var editor = new RichTextEditor();

        editor.TrySetMarkdown("* one\n* two", out var report).ShouldBeTrue();

        report.HasLosses.ShouldBeFalse();
        report.IsEmpty.ShouldBeFalse();
        report.Issues.ShouldAllBe(i => i.Severity == RichTextImportSeverity.Normalized);
        editor.GetMarkdown().ShouldBe("- one\n- two");
    }

    [AvaloniaFact]
    public void TrySetMarkdown_Refuses_Without_Changing_The_Undo_History()
    {
        var editor = new RichTextEditor();
        editor.TrySetMarkdown("First", out _).ShouldBeTrue();
        editor.TrySetMarkdown("Second", out _).ShouldBeTrue();
        var undoBefore = editor.CanUndo;

        editor.TrySetMarkdown("#### Four", out _).ShouldBeFalse();

        editor.CanUndo.ShouldBe(undoBefore);
        editor.GetMarkdown().ShouldBe("Second");
        editor.Undo();
        editor.GetMarkdown().ShouldBe("First");
    }
}
