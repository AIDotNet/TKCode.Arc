using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Content;

public class BlogGridTests
{
    private static Window Show(Control control, double width = 1200, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static BlogPost Post(string id, string category, string date, bool featured = false) => new()
    {
        Id = id,
        Title = "Title " + id,
        Excerpt = "Excerpt " + id,
        Category = category,
        Date = DateOnly.ParseExact(date, "yyyy-MM-dd"),
        ReadMinutes = 3,
        Author = new BlogAuthor { Name = "Author" },
        IsFeatured = featured,
        Body = ["Paragraph"],
    };

    private static List<BlogPost> Posts(int count) =>
        Enumerable.Range(0, count).Select(i => Post("p" + i, i % 2 == 0 ? "A" : "B", $"2026-01-{i + 1:00}")).ToList();

    private static void Click(Avalonia.Controls.Button button) => button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));

    [AvaloniaFact]
    public void Defaults_Show_The_Featured_Post_And_A_First_Page()
    {
        var grid = new BlogGrid();
        var window = Show(grid);
        try
        {
            grid.Title.ShouldBe("Journal");
            grid.Category.ShouldBe(BlogGrid.AllCategory);
            grid.Cards[0].IsFeaturedCard.ShouldBeTrue();
            grid.Cards.Count.ShouldBe(7);
            grid.Filters!.Buttons.Count.ShouldBe(5);
            grid.Filters.SelectedIndex.ShouldBe(0);
            TestHelpers.ColorOf(grid.Filters.IndicatorFill).ShouldBe(ArcTokens.GetColor(grid, TKCode.Arc.DesignTokens.ArcDesignTokenKind.Foreground));
            grid.CardGrid!.ColumnCount.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Posts_Sort_Newest_First_And_A_Marked_Post_Is_Featured()
    {
        var grid = new BlogGrid { Posts = [Post("old", "A", "2025-01-01", featured: true), Post("new", "A", "2026-01-01"), Post("mid", "A", "2025-06-01")] };
        var window = Show(grid);
        try
        {
            grid.Cards.Select(c => c.Post.Id).ShouldBe(["old", "new", "mid"]);
            grid.Cards[0].IsFeaturedCard.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Lone_Post_And_ShowFeatured_False_Have_No_Featured_Card()
    {
        var lone = new BlogGrid { Posts = [Post("only", "A", "2026-01-01")] };
        var window = Show(lone);
        try
        {
            lone.Cards.Single().IsFeaturedCard.ShouldBeFalse();
            lone.ShowFeatured = false;
            lone.Posts = Posts(4);
            Dispatcher.UIThread.RunJobs();
            lone.Cards.ShouldAllBe(c => !c.IsFeaturedCard);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Paging_Counts_Pages_After_The_Featured_Post_And_Clamps()
    {
        var grid = new BlogGrid { Posts = Posts(14), PageSize = 6 };
        var window = Show(grid);
        try
        {
            grid.PageCount.ShouldBe(3);
            grid.HasPagination.ShouldBeTrue();
            grid.PreviousButton!.IsEnabled.ShouldBeFalse();

            grid.GoToPage(2);
            Dispatcher.UIThread.RunJobs();
            grid.Page.ShouldBe(2);
            grid.Cards.Count.ShouldBe(6);
            grid.Cards.ShouldAllBe(c => !c.IsFeaturedCard);
            grid.PreviousButton.IsEnabled.ShouldBeTrue();
            grid.PageNumbers!.SelectedIndex.ShouldBe(1);

            grid.GoToPage(99);
            grid.Page.ShouldBe(2);
            grid.Page = 50;
            Dispatcher.UIThread.RunJobs();
            grid.PageNumbers.SelectedIndex.ShouldBe(2);
            grid.NextButton!.IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Short_List_Has_No_Pagination()
    {
        var grid = new BlogGrid { Posts = Posts(3) };
        var window = Show(grid);
        try
        {
            grid.PageCount.ShouldBe(1);
            grid.HasPagination.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picking_A_Filter_Filters_And_Resets_The_Page()
    {
        var grid = new BlogGrid { Posts = Posts(14), Categories = ["A", "B"], PageSize = 2 };
        var window = Show(grid);
        try
        {
            grid.GoToPage(3);
            Dispatcher.UIThread.RunJobs();
            grid.Category = "A";
            grid.Page = 1;
            Dispatcher.UIThread.RunJobs();
            grid.Cards.ShouldAllBe(c => c.Post.Category == "A");
            grid.Filters!.SelectedIndex.ShouldBe(1);
            grid.Page.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Empty_Filter_Says_So()
    {
        var grid = new BlogGrid { Posts = Posts(2), Categories = ["A", "B", "C"] };
        var window = Show(grid);
        try
        {
            grid.Category = "C";
            Dispatcher.UIThread.RunJobs();
            grid.Cards.ShouldBeEmpty();
            grid.EmptyMessage.ShouldBeOfType<TextBlock>().Text.ShouldBe("No posts in C yet.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(1000, 3)]
    [InlineData(800, 2)]
    [InlineData(500, 1)]
    public void Columns_Follow_The_Width(double width, int columns)
    {
        var grid = new BlogGrid { Posts = Posts(9) };
        var window = Show(grid, width);
        try
        {
            grid.CardGrid!.ColumnCount.ShouldBe(columns);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Cards_Are_Named_Links_That_Keep_Their_Tall_Row()
    {
        var grid = new BlogGrid { Posts = Posts(5) };
        var window = Show(grid);
        try
        {
            AutomationProperties.GetName(grid.Cards[0]).ShouldStartWith("Title p4.");
            grid.Cards.ShouldAllBe(c => c.Focusable);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Card_Opens_The_Reader_And_Back_Returns_Focus()
    {
        var grid = new BlogGrid { Posts = Posts(4) };
        var opened = new List<string>();
        grid.PostOpened += (_, e) => opened.Add(e.Post.Id);
        var window = Show(grid);
        try
        {
            var card = grid.Cards[1];
            var id = card.Post.Id;
            Click(card);
            Dispatcher.UIThread.RunJobs();
            opened.ShouldBe([id]);
            grid.ReadingPost!.Id.ShouldBe(id);
            grid.Stage!.ShowsSecond.ShouldBeTrue();
            var back = grid.BackButton.ShouldNotBeNull();
            back.IsFocused.ShouldBeTrue();
            grid.Index!.IsHitTestVisible.ShouldBeFalse();

            Click(back);
            Dispatcher.UIThread.RunJobs();
            grid.ReadingPost.ShouldBeNull();
            grid.Stage.ShowsSecond.ShouldBeFalse();
            grid.Index.IsHitTestVisible.ShouldBeTrue();
            grid.Index.Opacity.ShouldBe(1);
            grid.Cards.Single(c => c.Post.Id == id).IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void With_A_Link_The_Reader_Stays_Off()
    {
        var grid = new BlogGrid { Posts = Posts(3), HrefSelector = p => "https://example.com/" + p.Id };
        string? href = null;
        grid.PostOpened += (_, e) =>
        {
            href = e.Href;
            e.Handled = true;
        };
        var window = Show(grid);
        try
        {
            Click(grid.Cards[0]);
            Dispatcher.UIThread.RunJobs();
            href.ShouldBe("https://example.com/p2");
            grid.ReadingPost.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Paging_At_Full_Motion_Waits_For_The_Old_Page()
    {
        var grid = new BlogGrid { Posts = Posts(14), PageSize = 6 };
        var window = Show(grid, level: ArcMotionLevel.Full);
        try
        {
            grid.GoToPage(2);
            grid.PageHost!.ExitingCount.ShouldBe(1);
            TestHelpers.PumpUntil(() => grid.PageHost.ExitingCount == 0).ShouldBeTrue();
            TestHelpers.PumpUntil(() => grid.Cards.All(c => c.Opacity > 0) && grid.PageHost.Current?.Opacity > 0.99).ShouldBeTrue();
            grid.Page.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Reader_Round_Trip_Settles_At_Full_Motion()
    {
        var grid = new BlogGrid();
        var window = Show(grid, level: ArcMotionLevel.Full);
        try
        {
            var card = grid.Cards[2];
            Click(card);
            Dispatcher.UIThread.RunJobs();
            TestHelpers.PumpUntil(() => grid.Index!.Opacity <= 0.01).ShouldBeTrue();
            grid.CloseReader();
            TestHelpers.PumpUntil(() => grid.Cards.All(c => c.Opacity >= 0.99) && grid.BackButton is null).ShouldBeTrue();
            grid.Index!.Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }
}
