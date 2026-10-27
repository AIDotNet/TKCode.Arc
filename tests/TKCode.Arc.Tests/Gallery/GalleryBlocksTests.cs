using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Localization;
using Avalonia.Headless.XUnit;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>
/// The Blocks group under Components: its subgroups and blocks come from the Blocks catalog, and it stays out of the Arc
/// component count.
/// </summary>
public class GalleryBlocksTests
{
    private static GalleryCategory Blocks => GalleryCatalog.Blocks;

    [AvaloniaFact]
    public void Blocks_Group_Lists_The_Block_Groups_In_Catalog_Order()
    {
        Blocks.Subcategories.ShouldNotBeNull();
        Blocks.Subcategories!.Select(s => s.Code).ShouldBe(BlockCatalog.Groups.Select(g => g.Code));
        Blocks.Subcategories.Select(s => s.Title).ShouldBe(BlockCatalog.Groups.Select(g => g.Title));
        Blocks.Entries.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Every_Block_Is_Listed_Under_Its_Group_With_Its_Catalog_Data()
    {
        Blocks.AllEntries.Select(e => e.ClassName).ShouldBe(BlockCatalog.Blocks.Select(b => b.ClassName), ignoreOrder: true);
        foreach (var block in BlockCatalog.Blocks)
        {
            var group = Blocks.Subcategories!.Single(s => s.Code == block.Group);
            var entry = group.Entries.Single(e => e.ClassName == block.ClassName);
            entry.Title.ShouldBe(block.Title);
            entry.Description.ShouldBe(block.Description);
            // Ports link to the free block's page on uiarc.dev; original blocks have no Arc page to link to.
            entry.ArcUrl.ShouldBe(block.IsArcPort ? $"https://uiarc.dev/components/blocks/{block.Slug}" : null, block.ClassName);
        }
    }

    [AvaloniaFact]
    public void App_Shell_Lists_Its_Six_Blocks_In_Order()
    {
        Blocks.Subcategories!.Single(s => s.Code == "AppShell").Entries.Select(e => e.ClassName)
            .ShouldBe(["Sidebar", "PageHeader", "CommandPalette", "NotificationCenter", "EmptyStates", "SearchResults"]);
    }

    [AvaloniaFact]
    public void Showpieces_Follow_The_Sidebar_Order()
    {
        var order = new[] { "ActivityTerrain", "RevenueGlobe", "CustomerGalaxy", "SemanticZoom", "LayoutMorph", "ExplodedView" };
        var listed = Blocks.Subcategories!.Single(s => s.Code == "Showpieces").Entries.Select(e => e.ClassName).ToArray();
        listed.ShouldNotBeEmpty();
        listed.ShouldBe(order.Where(listed.Contains).ToArray());
    }

    [AvaloniaFact]
    public void Every_Block_Has_Demos_And_An_Up_To_Date_Page_In_Every_Language()
    {
        foreach (var entry in Blocks.AllEntries)
        {
            GalleryDocs.Has(entry.ClassName).ShouldBeTrue($"{entry.ClassName} has no docs page");
            GalleryDocs.HasDemos(entry.ClassName).ShouldBeTrue($"{entry.ClassName} has no demos");
            GalleryDocs.PackageOf(entry.ClassName).ShouldBe("TKCode.Arc.Blocks", entry.ClassName);
            foreach (var language in new[] { "en-US", "zh-CN", "zh-TW" })
            {
                var doc = GalleryDocs.Get(entry.ClassName, language).ShouldNotBeNull();
                doc.Language.ShouldBe(language, $"{entry.ClassName} is not translated into {language}");
                doc.IsOutdated.ShouldBeFalse($"{entry.ClassName} ({language}) was translated from an older en-US.md");
                doc.Summary.ShouldNotBeNullOrWhiteSpace();
            }
            GalleryDocs.Summary(entry.ClassName, "en-US").ShouldBe(entry.Description, entry.ClassName);
        }
    }

    [AvaloniaFact]
    public void Every_Block_Group_Has_A_Title_That_Matches_The_Catalog()
    {
        foreach (var group in Blocks.Subcategories!)
        {
            GalleryText.TryGetBlockGroup(group.Code, out var kind).ShouldBeTrue(group.Code);
            GalleryText.Get(kind).ShouldBe(group.Title, group.Code);
        }
    }

    [AvaloniaFact]
    public void Blocks_Are_Not_Counted_As_Arc_Components()
    {
        GalleryCatalog.Count.ShouldBe(112);
        GalleryCatalog.AllCategories.ShouldNotContain(Blocks);
    }
}
