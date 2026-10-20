using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Localization;
using Avalonia.Headless.XUnit;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>The Blocks group under Components: its subgroups come from the Blocks catalog and it stays out of the Arc component count.</summary>
public class GalleryBlocksTests
{
    private static GalleryCategory Blocks => GalleryCatalog.AllCategories.Single(c => c.Code == "Blocks");

    [AvaloniaFact]
    public void Blocks_Group_Lists_The_Block_Groups_In_Catalog_Order()
    {
        Blocks.Subcategories.ShouldNotBeNull();
        Blocks.Subcategories!.Select(s => s.Code).ShouldBe(BlockCatalog.Groups.Select(g => g.Code));
        Blocks.Subcategories.Select(s => s.Title).ShouldBe(BlockCatalog.Groups.Select(g => g.Title));
        Blocks.AllEntries.ShouldBeEmpty();
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
        GalleryCatalog.Count.ShouldBe(110);
        GalleryCatalog.Categories.ShouldNotContain(Blocks);
    }
}
