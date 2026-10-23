using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using StreamMark.Avalonia.Selection;

namespace TKCode.Arc.Tests.Content;

/// <summary>
/// The host integration surface of <see cref="Markdown"/>: outline, navigation, find, selection and the documented
/// timing before the template is applied and across a re-applied template.
/// </summary>
public class MarkdownHostTests
{
    private const string Document = """
        # Release notes

        Intro text.

        ## Fixes

        The **first** fix.

        ## Changes

        A second **fix** here.
        """;

    private static Window Show(Markdown markdown)
    {
        var window = TestHelpers.Show(markdown);
        window.Width = 640;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static async Task Ready(Markdown markdown)
    {
        for (var i = 0; i < 300 && !markdown.IsDocumentReady; i++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }
        markdown.IsDocumentReady.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Before_The_Template_There_Is_No_View_And_The_Methods_Are_Safe()
    {
        var markdown = new Markdown { Text = Document };

        markdown.IsDocumentReady.ShouldBeFalse();
        markdown.GetOutline().ShouldBeEmpty();
        markdown.ScrollToBlock(0).ShouldBeFalse();
        markdown.ScrollToAnchor("fixes").ShouldBeFalse();
        markdown.Find("fix").ShouldBe(0);
        markdown.MatchCount.ShouldBe(0);
        markdown.CurrentMatchIndex.ShouldBe(-1);
        markdown.GetSelectedText().ShouldBeEmpty();
        markdown.GetSelectedMarkdown().ShouldBeEmpty();
        markdown.HasSelection.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task The_Outline_Follows_The_Document_And_Is_Ready_With_The_Document()
    {
        var markdown = new Markdown { Text = Document };
        var window = Show(markdown);
        try
        {
            await Ready(markdown);
            var outline = markdown.GetOutline();
            outline.Select(o => (o.Level, o.Text)).ShouldBe([(1, "Release notes"), (2, "Fixes"), (2, "Changes")]);
            outline.Select(o => o.Id).ShouldBe(["release-notes", "fixes", "changes"]);

            markdown.Text = "## Only\n\nText";
            markdown.IsDocumentReady.ShouldBeFalse();
            await Ready(markdown);
            markdown.GetOutline().Select(o => o.Text).ShouldBe(["Only"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task DocumentReady_Runs_Once_Per_Source()
    {
        var markdown = new Markdown { Text = Document };
        var raised = 0;
        markdown.DocumentReady += (_, _) => raised++;
        var window = Show(markdown);
        try
        {
            await Ready(markdown);
            raised.ShouldBeGreaterThan(0);
            var afterFirst = raised;

            markdown.Text = "# Other";
            await Ready(markdown);
            raised.ShouldBe(afterFirst + 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Scrolling_Reaches_A_Block_And_An_Anchor()
    {
        var markdown = new Markdown { Text = Document };
        var window = Show(markdown);
        try
        {
            await Ready(markdown);
            var outline = markdown.GetOutline();
            markdown.ScrollToPoint(outline[1].Target).ShouldBeTrue();
            markdown.ScrollToBlock(outline[2].Target.BlockKey).ShouldBeTrue();
            markdown.ScrollToAnchor("changes").ShouldBeTrue();
            markdown.ScrollToAnchor("#fixes").ShouldBeTrue();
            markdown.ScrollToAnchor("no-such-heading").ShouldBeFalse();
            markdown.ScrollToAnchor(null).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Find_Reports_Matches_And_Moves_Between_Them()
    {
        var markdown = new Markdown { Text = Document };
        var window = Show(markdown);
        try
        {
            await Ready(markdown);
            var changes = 0;
            markdown.SearchChanged += (_, _) => changes++;

            markdown.Find("fix").ShouldBe(3);
            markdown.MatchCount.ShouldBe(3);
            markdown.CurrentMatchIndex.ShouldBe(0);
            changes.ShouldBeGreaterThan(0);

            markdown.FindNext();
            markdown.CurrentMatchIndex.ShouldBe(1);
            markdown.FindNext();
            markdown.CurrentMatchIndex.ShouldBe(2);
            markdown.FindNext();
            markdown.CurrentMatchIndex.ShouldBe(0);
            markdown.FindPrevious();
            markdown.CurrentMatchIndex.ShouldBe(2);
            markdown.FindAt(0);
            markdown.CurrentMatchIndex.ShouldBe(0);

            markdown.Find("FIX", matchCase: true).ShouldBe(0);
            markdown.MatchCount.ShouldBe(0);
            markdown.CurrentMatchIndex.ShouldBe(-1);

            markdown.Find("FIX", matchCase: false).ShouldBe(3);
            markdown.ClearSearch();
            markdown.MatchCount.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_Search_Runs_Again_When_The_Document_Changes()
    {
        var markdown = new Markdown { Text = "one fix" };
        var window = Show(markdown);
        try
        {
            await Ready(markdown);
            markdown.Find("fix").ShouldBe(1);

            markdown.Text = "fix one fix two fix";
            await Ready(markdown);
            markdown.MatchCount.ShouldBe(3);
            markdown.CurrentMatchIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Selection_Reports_And_Returns_Text_And_Markdown()
    {
        var markdown = new Markdown { Text = Document };
        var window = Show(markdown);
        try
        {
            await Ready(markdown);
            var changes = 0;
            markdown.SelectionChanged += (_, _) => changes++;
            markdown.HasSelection.ShouldBeFalse();

            markdown.SelectAll();
            Dispatcher.UIThread.RunJobs();
            markdown.HasSelection.ShouldBeTrue();
            changes.ShouldBeGreaterThan(0);
            markdown.GetSelectedText().ShouldContain("Release notes");
            markdown.GetSelectedMarkdown().ShouldContain("# Release notes");

            markdown.View!.Selection.Clear();
            Dispatcher.UIThread.RunJobs();
            markdown.HasSelection.ShouldBeFalse();
            markdown.GetSelectedText().ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_Stream_Becomes_Ready_When_It_Completes()
    {
        var markdown = new Markdown();
        var window = Show(markdown);
        try
        {
            markdown.Append("# Streamed\n\nBody");
            markdown.IsDocumentReady.ShouldBeFalse();
            markdown.Complete();
            await Ready(markdown);
            markdown.GetOutline().Select(o => o.Text).ShouldBe(["Streamed"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_New_Template_Keeps_The_Document_And_The_Search()
    {
        var markdown = new Markdown { Text = Document };
        var window = Show(markdown);
        try
        {
            await Ready(markdown);
            markdown.Find("fix").ShouldBe(3);
            var before = markdown.View;
            markdown.Template = new global::Avalonia.Controls.Templates.FuncControlTemplate((_, scope) =>
            {
                var view = new StreamMark.Avalonia.MarkdownView { Name = Markdown.ViewPart };
                scope.Register(Markdown.ViewPart, view);
                return new Border { Child = view };
            });
            Dispatcher.UIThread.RunJobs();
            await Ready(markdown);

            markdown.View.ShouldNotBeSameAs(before);
            markdown.GetOutline().Select(o => o.Text).ShouldBe(["Release notes", "Fixes", "Changes"]);
            markdown.MatchCount.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Appended_Text_Fades_In_Over_The_Reveal_Duration_At_Full_Motion()
    {
        var markdown = new Markdown { RevealFadeDuration = TimeSpan.FromMilliseconds(150) };
        var window = TestHelpers.Show(markdown, Motion.ArcMotionLevel.Full);
        try
        {
            markdown.Append("Hello");
            markdown.Stream!.State.FadeDuration.ShouldBe(TimeSpan.FromMilliseconds(150));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Fade_Defaults_To_The_Standard_Duration_And_Can_Be_Turned_Off_At_Reduced_Motion()
    {
        var markdown = new Markdown();
        var window = TestHelpers.Show(markdown, Motion.ArcMotionLevel.Full);
        try
        {
            markdown.Append("One");
            markdown.Stream!.State.FadeDuration.ShouldBe(Motion.ArcMotionPresets.Standard);
        }
        finally
        {
            window.Close();
        }

        var reduced = new Markdown { RevealFadeInReducedMotion = false };
        var reducedWindow = TestHelpers.Show(reduced, Motion.ArcMotionLevel.Reduced);
        try
        {
            reduced.Append("Two");
            reduced.Stream!.State.FadeDuration.ShouldBe(TimeSpan.Zero);
        }
        finally
        {
            reducedWindow.Close();
        }

        var kept = new Markdown();
        var keptWindow = TestHelpers.Show(kept, Motion.ArcMotionLevel.Reduced);
        try
        {
            kept.Append("Three");
            kept.Stream!.State.FadeDuration.ShouldBe(Motion.ArcMotionPresets.Fast);
        }
        finally
        {
            keptWindow.Close();
        }
    }

    [AvaloniaFact]
    public async Task An_Unpaced_Markdown_Shows_Each_Appended_Piece_At_Once()
    {
        var markdown = new Markdown { IsPaced = false };
        var window = Show(markdown);
        try
        {
            markdown.Append("# Title\n\n");
            markdown.Append("Body text");
            markdown.Complete();
            await Ready(markdown);
            markdown.GetOutline().Select(o => o.Text).ShouldBe(["Title"]);
        }
        finally
        {
            window.Close();
        }
    }
}
