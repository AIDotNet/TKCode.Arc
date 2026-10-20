using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.TextFields;

public class ExpandingSearchTests
{
    private static List<ExpandingSearchItem> Items() =>
    [
        new() { Id = "q3", Title = "Quarterly report", Group = "Docs" },
        new() { Id = "road", Title = "Roadmap", Group = "Docs", Keywords = ["plan"] },
        new() { Id = "auto", Title = "Report automation", Group = "Projects" },
        new() { Id = "rev", Title = "Revenue", Meta = "Monthly report", Group = "Dashboards" },
        new() { Id = "sub", Title = "Subreport tools", Group = "Projects" }
    ];

    [Fact]
    public void Ranking_Prefers_Title_Starts_Then_Word_Starts_Then_Inner_Then_Meta()
    {
        var ranked = ExpandingSearchRanking.Rank(Items(), "report", 6).Select(i => i.Id).ToList();
        ranked.ShouldBe(new[] { "auto", "q3", "sub", "rev" });
        ExpandingSearchRanking.Rank(Items(), "plan", 6).Single().Id.ShouldBe("road");
        ExpandingSearchRanking.Rank(Items(), "report", 2).Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public void Trigger_Expands_And_Typing_Lists_Grouped_Results()
    {
        var search = new ExpandingSearch { Label = "Search docs", ItemsSource = Items() };
        var window = TestHelpers.Show(new Border { Width = 380, Child = search });
        try
        {
            var shell = search.GetVisualDescendants().OfType<Panel>().First(p => p.Name == ExpandingSearch.ShellHostPart);
            shell.Bounds.Width.ShouldBe(44, 0.5);
            search.IsExpanded.ShouldBeFalse();

            var trigger = search.GetVisualDescendants().OfType<ExpandingSearchTrigger>().First();
            trigger.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            search.IsExpanded.ShouldBeTrue();
            shell.Bounds.Width.ShouldBe(360, 0.5);
            search.Classes.ShouldContain(":expanded");
            var input = search.GetVisualDescendants().OfType<ExpandingSearchInput>().First();
            input.IsFocused.ShouldBeTrue();
            search.IsPanelOpen.ShouldBeFalse();

            window.KeyTextInput("report");
            Dispatcher.UIThread.RunJobs();
            search.Query.ShouldBe("report");
            search.IsPanelOpen.ShouldBeTrue();
            // Grouped in the order groups first appear in the items: Docs, Projects, Dashboards.
            search.VisibleItems.Select(i => i.Id).ShouldBe(new[] { "q3", "auto", "sub", "rev" });
            search.ActiveIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrows_Wrap_Enter_Chooses_And_Escape_Folds_Back()
    {
        var search = new ExpandingSearch { Label = "Search docs", ItemsSource = Items() };
        var window = TestHelpers.Show(new Border { Width = 380, Child = search });
        try
        {
            ExpandingSearchItem? chosen = null;
            search.ItemSelected += (_, e) => chosen = e.Item;
            search.Expand();
            Dispatcher.UIThread.RunJobs();
            window.KeyTextInput("report");
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            search.ActiveIndex.ShouldBe(3);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            search.ActiveIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            search.ActiveIndex.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            chosen.ShouldNotBeNull();
            chosen!.Id.ShouldBe("auto");
            search.IsExpanded.ShouldBeFalse();
            search.IsPanelOpen.ShouldBeFalse();
            search.Query.ShouldBe(string.Empty);

            search.Expand();
            Dispatcher.UIThread.RunJobs();
            window.KeyTextInput("zzz");
            Dispatcher.UIThread.RunJobs();
            search.IsPanelOpen.ShouldBeTrue();
            search.VisibleItems.Count.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            search.IsExpanded.ShouldBeFalse();
            search.GetVisualDescendants().OfType<ExpandingSearchTrigger>().First().IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Suggestions_Show_Before_Typing_And_Inline_Items_Work()
    {
        var search = new ExpandingSearch { Label = "Search" };
        foreach (var item in Items())
        {
            search.Items.Add(item);
        }
        search.Suggestions = new[] { search.Items[1] };
        var window = TestHelpers.Show(new Border { Width = 380, Child = search });
        try
        {
            search.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            search.IsPanelOpen.ShouldBeTrue();
            search.VisibleItems.Single().Id.ShouldBe("road");
            search.ActiveIndex.ShouldBe(-1);
            window.KeyTextInput("rev");
            Dispatcher.UIThread.RunJobs();
            search.VisibleItems.Single().Id.ShouldBe("rev");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Anchor_Start_Keeps_The_Button_On_The_Left_And_Width_Respects_The_Lane()
    {
        var search = new ExpandingSearch { Label = "Search", Anchor = ExpandingSearchAnchor.Start, ExpandedWidth = 500, ItemsSource = Items() };
        var window = TestHelpers.Show(new Border { Width = 300, Child = search });
        try
        {
            var shell = search.GetVisualDescendants().OfType<Panel>().First(p => p.Name == ExpandingSearch.ShellHostPart);
            shell.Bounds.X.ShouldBe(0, 0.5);
            search.Expand();
            Dispatcher.UIThread.RunJobs();
            shell.Bounds.Width.ShouldBe(300, 0.5);
            shell.Bounds.X.ShouldBe(0, 0.5);
        }
        finally
        {
            window.Close();
        }
    }
}
