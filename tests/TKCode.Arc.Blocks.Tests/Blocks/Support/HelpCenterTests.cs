using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Support;

/// <summary>HelpCenter: the sample home, search, categories, articles with their outline, feedback, back navigation and the narrow layout.</summary>
public class HelpCenterTests
{
    private static (HelpCenter Help, Avalonia.Controls.Window Window) Show(double width = 1000)
    {
        var help = new HelpCenter { Height = 700 };
        var window = TestHelpers.Show(help);
        window.Width = width;
        window.Height = 800;
        Dispatcher.UIThread.RunJobs();
        return (help, window);
    }

    [AvaloniaFact]
    public void The_Sample_Opens_On_The_Home()
    {
        var (help, window) = Show();
        try
        {
            help.Categories.Count.ShouldBe(6);
            help.Articles.Count.ShouldBeGreaterThan(6);
            help.View.ShouldBe(HelpCenterView.Home);
            help.Breadcrumb.ShouldNotBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_Shows_Results_Titles_First_And_Clearing_Goes_Home()
    {
        var (help, window) = Show();
        try
        {
            help.Results("invoice").ShouldNotBeEmpty();
            help.Results("two-step")[0].Id.ShouldBe("two-step");
            help.Results("zzzz nothing").ShouldBeEmpty();
            help.SearchBox.Text = "invoice";
            help.View.ShouldBe(HelpCenterView.Search);
            help.SearchText.ShouldBe("invoice");
            help.LastAnnouncement!.ShouldContain("found");
            help.SearchBox.Text = string.Empty;
            help.View.ShouldBe(HelpCenterView.Home);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Article_Has_An_Outline_And_Back_Goes_To_Its_Category()
    {
        var (help, window) = Show();
        try
        {
            HelpCenterArticle? opened = null;
            help.ArticleOpened += (_, e) => opened = e.Article;
            var article = help.Articles.First(a => a.Id == "invite");
            help.OpenArticle(article);
            help.View.ShouldBe(HelpCenterView.Article);
            opened.ShouldBe(article);
            var page = help.ArticlePage.ShouldNotBeNull();
            page.Outline.Select(o => o.Text).ShouldBe(["Send an invite by email", "Share a join link", "Turn the link off", "Choose a role"]);
            page.Outline[2].Level.ShouldBe(3);
            page.Toc.ShouldNotBeNull();
            help.Breadcrumb.Count.ShouldBe(2);

            help.GoBack();
            help.View.ShouldBe(HelpCenterView.Category);
            help.CurrentCategory!.Id.ShouldBe("start");
            help.GoBack();
            help.View.ShouldBe(HelpCenterView.Home);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Feedback_Is_Reported_Once_With_The_Comment()
    {
        var (help, window) = Show();
        try
        {
            var reports = new List<(bool, string?)>();
            help.FeedbackGiven += (_, e) => reports.Add((e.IsHelpful, e.Comment));
            help.OpenArticle(help.Articles[0]);
            help.ArticlePage!.Answer(false, "  Missing the mobile steps ");
            reports.ShouldBe([(false, "Missing the mobile steps")]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Leaves_An_Article()
    {
        var (help, window) = Show();
        try
        {
            help.OpenCategory(help.Categories[1]);
            help.View.ShouldBe(HelpCenterView.Category);
            help.Breadcrumb[0].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            help.View.ShouldBe(HelpCenterView.Home);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Hides_The_Table_Of_Contents()
    {
        var (help, window) = Show(width: 480);
        try
        {
            help.IsNarrow.ShouldBeTrue();
            help.OpenArticle(help.Articles[0]);
            help.ArticlePage!.Toc.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Slugs_Follow_Markdown_Anchors()
    {
        HelpCenter.Slug("Send an invite by email").ShouldBe("send-an-invite-by-email");
        HelpCenter.Slug("Step 2: Verify!").ShouldBe("step-2-verify");
    }
}
