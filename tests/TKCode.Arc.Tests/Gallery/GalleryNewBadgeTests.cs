using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.Motion;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>The "New" marker: which catalog entries carry it, when it expires, and where the shell shows it.</summary>
public class GalleryNewBadgeTests
{
    private static GalleryEntry Tables(string className) =>
        GalleryCatalog.Categories.First(c => c.Code == "Tables").Entries.First(e => e.ClassName == className);

    [Fact]
    public void Tree_Table_Is_The_Second_Entry_Of_Tables_And_Is_Flagged_New()
    {
        var tables = GalleryCatalog.Categories.First(c => c.Code == "Tables").Entries;
        tables[1].ClassName.ShouldBe("TreeTable");
        tables[1].AddedOn.ShouldBe("2026-10-25");
        tables[1].ArcUrl.ShouldBe("https://uiarc.dev/components/tree-table");
        tables.Where(e => e.AddedOn is not null).Select(e => e.ClassName).ShouldBe(["TreeTable"]);
    }

    [Fact]
    public void An_Entry_Is_New_For_The_Window_After_Its_Date_And_Never_Before()
    {
        var entry = Tables("TreeTable");
        var added = new DateOnly(2026, 10, 25);
        entry.IsNewOn(added).ShouldBeTrue();
        entry.IsNewOn(added.AddDays(GalleryCatalog.NewWindowDays - 1)).ShouldBeTrue();
        entry.IsNewOn(added.AddDays(GalleryCatalog.NewWindowDays)).ShouldBeFalse();
        entry.IsNewOn(added.AddDays(-1)).ShouldBeFalse();
        Tables("TreeView").IsNewOn(added).ShouldBeFalse();
    }

    [Fact]
    public void The_Arc_Component_Count_Includes_Tree_Table()
    {
        GalleryCatalog.Count.ShouldBe(111);
        GalleryCatalog.Categories.Length.ShouldBe(22);
    }

    [AvaloniaFact]
    public void The_Sidebar_Shows_The_Badge_On_New_Rows_Only()
    {
        var window = new GalleryWindow { Width = 1280, Height = 860 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        try
        {
            window.Select(GalleryCatalog.Categories[0].Entries[0]);
            Dispatcher.UIThread.RunJobs();
            window.Sidebar.SearchText = "tree";
            Dispatcher.UIThread.RunJobs();

            var badges = window.Sidebar.GetVisualDescendants().OfType<GalleryNewBadge>().Count();
            badges.ShouldBe(GalleryCatalog.AllEntries.Count(e => e.IsNew && e.Title.Contains("tree", StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            window.Close();
        }
    }
}
