using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Shell;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>The addresses of the gallery's pages (<c>/components/button</c>): one per page, and back again.</summary>
public class GalleryRouteTests
{
    [Fact]
    public void The_Friends_Page_Has_Its_Own_Address()
    {
        GalleryRoute.Friends.Path.ShouldBe("/friends");
        GalleryRoute.Friends.Title.ShouldBe("Friends · Arc Gallery");
        GalleryRoute.Parse("/friends")!.ShouldBe(GalleryRoute.Friends);
        GalleryRoute.Parse("/Friends/")!.ShouldBe(GalleryRoute.Friends);
        GalleryRoute.Parse("/friends/avalonia").ShouldBeNull();
    }

    [Fact]
    public void The_Home_Page_Is_The_Root()
    {
        GalleryRoute.Home.Path.ShouldBe("/");
        GalleryRoute.Home.Title.ShouldBe("Arc Gallery");
        GalleryRoute.Parse("/").ShouldBe(GalleryRoute.Home);
        GalleryRoute.Parse(string.Empty).ShouldBe(GalleryRoute.Home);
    }

    [Fact]
    public void Every_Page_Has_Its_Own_Address_That_Opens_It()
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in GalleryCatalog.AllEntries)
        {
            var route = GalleryRoute.To(entry);
            route.Entry.ShouldBe(entry);
            route.Path.ShouldStartWith("/");
            paths.Add(route.Path).ShouldBeTrue($"{entry.ClassName} shares {route.Path} with another page");
            GalleryRoute.Parse(route.Path).ShouldBe(route, route.Path);
        }
        paths.Count.ShouldBe(GalleryCatalog.AllEntries.Count());
    }

    [Fact]
    public void An_Address_Names_The_Section_And_The_Slug()
    {
        GalleryRoute.Parse("/components/split-button")!.Entry!.ClassName.ShouldBe("SplitButton");
        GalleryRoute.Parse("/docs/installation")!.Entry!.ClassName.ShouldBe("DocsInstallation");
        var block = GalleryCatalog.Blocks.AllEntries.First();
        GalleryRoute.To(block).Path.ShouldBe($"/blocks/{block.Slug}");
        GalleryRoute.To(block).Kind.ShouldBe(GalleryRouteKind.Blocks);
        GalleryRoute.To(GalleryCatalog.Categories[0].Entries[0]).Kind.ShouldBe(GalleryRouteKind.Components);
        GalleryRoute.To(GalleryCatalog.Docs.Entries[0]).Kind.ShouldBe(GalleryRouteKind.Docs);
        GalleryRoute.Parse("/components/button")!.Title.ShouldBe("Button · Arc Gallery");
    }

    [Theory]
    [InlineData("/Components/Split-Button/")]
    [InlineData("components/split-button")]
    [InlineData("/components/split-button?lang=zh-CN")]
    [InlineData("/components/split-button#top")]
    public void Case_Slashes_Query_And_Fragment_Do_Not_Matter(string path)
        => GalleryRoute.Parse(path)!.Path.ShouldBe("/components/split-button");

    [Theory]
    [InlineData(null)]
    [InlineData("/components")]
    [InlineData("/components/nope")]
    [InlineData("/nope/button")]
    [InlineData("/components/button/extra")]
    [InlineData("/home/button")]
    public void An_Address_Without_A_Page_Opens_None(string? path)
        => GalleryRoute.Parse(path).ShouldBeNull();
}
