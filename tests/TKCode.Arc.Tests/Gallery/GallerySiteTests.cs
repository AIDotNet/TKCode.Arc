using System.Text.Json;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>The product site: what differs between the site and the desktop app, settings and updates, StreamMark, and phones.</summary>
public class GallerySiteTests
{
    private static GalleryRelease Release(string version) => new(version, new Uri("https://example.test/release"),
    [
        new GalleryReleaseAsset($"TKCode.Arc.Gallery-{version}-osx-arm64.dmg", new Uri("https://example.test/mac.dmg"), 75 * 1024 * 1024),
        new GalleryReleaseAsset($"TKCode.Arc.Gallery-{version}-win-x64.exe", new Uri("https://example.test/win.exe"), 170 * 1024 * 1024)
    ]);

    /// <summary>Runs <paramref name="test"/> with the shell in a window of this width, as the site or as the app.</summary>
    private static void WithShell(GalleryHostKind kind, double width, Action<GalleryShell, Window> test, GalleryRoute? initial = null,
                                  GalleryClientOs os = GalleryClientOs.MacOS)
    {
        var (oldKind, oldOs, oldProvider) = (GalleryHost.Kind, GalleryHost.ClientOs, GalleryReleases.Provider);
        GalleryHost.Kind = kind;
        GalleryHost.ClientOs = os;
        GalleryReleases.Provider = () => Task.FromResult<GalleryRelease?>(Release("9.9.9"));
        GalleryUpdates.Reset();
        var shell = new GalleryShell(initial);
        var window = TestHelpers.Show(shell);
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Width = width;
        window.Height = 900;
        try
        {
            for (var i = 0; i < 4; i++)
            {
                Dispatcher.UIThread.RunJobs();
            }
            test(shell, window);
        }
        finally
        {
            window.Close();
            GalleryHost.Kind = oldKind;
            GalleryHost.ClientOs = oldOs;
            GalleryReleases.Provider = oldProvider;
            GalleryUpdates.Reset();
        }
    }

    private static GalleryHomeView Home(Window window) => window.GetVisualDescendants().OfType<GalleryHomeView>().Single();

    [AvaloniaFact]
    public void The_Site_Recommends_The_Desktop_App_And_Has_No_Settings()
    {
        WithShell(GalleryHostKind.Web, 1280, (shell, window) =>
        {
            var home = Home(window);
            home.WebStrip.ShouldNotBeNull();
            home.Download.ShouldNotBeNull();
            shell.TopBar.DownloadButton.ShouldNotBeNull();
            shell.TopBar.SettingsButton.ShouldBeNull();
            GalleryRoute.Parse("/settings/about").ShouldBeNull();
        });
    }

    [AvaloniaFact]
    public void The_Desktop_App_Has_Settings_And_No_Download_Advice()
    {
        WithShell(GalleryHostKind.Desktop, 1280, (shell, window) =>
        {
            var home = Home(window);
            home.WebStrip.ShouldBeNull();
            home.Download.ShouldBeNull();
            shell.TopBar.DownloadButton.ShouldBeNull();
            shell.TopBar.SettingsButton.ShouldNotBeNull();
            shell.TopBar.SettingsMenu!.Items.Count.ShouldBe(4);
            GalleryRoute.Parse("/settings/about").ShouldBe(GalleryRoute.Settings("about"));
        });
    }

    [AvaloniaFact]
    public void The_Sponsor_Banner_Stays_On_Every_Page_Of_Both_Hosts()
    {
        foreach (var kind in new[] { GalleryHostKind.Web, GalleryHostKind.Desktop })
        {
            WithShell(kind, 1280, (shell, window) =>
            {
                window.GetVisualDescendants().OfType<GallerySponsorBanner>().ShouldHaveSingleItem();
                shell.Navigate(GalleryRoute.StreamMark);
                Dispatcher.UIThread.RunJobs();
                window.GetVisualDescendants().OfType<GallerySponsorBanner>().ShouldHaveSingleItem();
            });
        }
    }

    [AvaloniaFact]
    public void The_Top_Bar_Draws_Above_The_Page_So_Its_Menus_Are_Not_Covered()
    {
        WithShell(GalleryHostKind.Desktop, 1280, (shell, _) => shell.Chrome.ZIndex.ShouldBeGreaterThan(shell.Body.ZIndex));
    }

    [AvaloniaFact]
    public void The_Download_Section_Offers_The_Visitors_System_First_And_Links_Its_File()
    {
        WithShell(GalleryHostKind.Web, 1280, (_, window) =>
        {
            var home = Home(window);
            var section = home.Download!;
            section.IsVisible.ShouldBeFalse("the home page stays short until the visitor asks");
            home.ShowDownload();
            section.IsVisible.ShouldBeTrue();
            section.OfferedRuntimes.First().ShouldBe("win-x64");
            // Until the list is read, every button opens the page of all releases.
            section.UrlOf("win-x64").ShouldBe(GalleryReleases.AllUrl);
            for (var i = 0; i < 6; i++)
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
            }
            section.UrlOf("win-x64").ToString().ShouldBe("https://example.test/win.exe");
            // A release without a file for that system opens the release's page.
            section.UrlOf("linux-x64").ToString().ShouldBe("https://example.test/release");
        }, os: GalleryClientOs.Windows);
    }

    [Theory]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605", GalleryClientOs.MacOS)]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/130", GalleryClientOs.Windows)]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64) Chrome/130", GalleryClientOs.Linux)]
    [InlineData("Mozilla/5.0 (Linux; Android 14) Chrome/130 Mobile", GalleryClientOs.Mobile)]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X)", GalleryClientOs.Mobile)]
    [InlineData("iPad", GalleryClientOs.Mobile)]
    [InlineData("", GalleryClientOs.Unknown)]
    public void The_Users_System_Comes_From_The_User_Agent(string agent, GalleryClientOs os) => GalleryHost.ParseUserAgent(agent).ShouldBe(os);

    [AvaloniaFact]
    public async Task The_Update_Check_Reports_What_It_Found()
    {
        var old = GalleryReleases.Provider;
        try
        {
            GalleryReleases.Provider = () => Task.FromResult<GalleryRelease?>(Release("0.5.0"));
            await GalleryUpdates.CheckAsync("0.4.0-preview.6");
            GalleryUpdates.Status.ShouldBe(GalleryUpdateStatus.Available);
            GalleryUpdates.Available!.Version.ShouldBe("0.5.0");

            await GalleryUpdates.CheckAsync("0.5.0");
            GalleryUpdates.Status.ShouldBe(GalleryUpdateStatus.UpToDate);
            GalleryUpdates.Available.ShouldBeNull();

            GalleryReleases.Provider = () => Task.FromResult<GalleryRelease?>(null);
            await GalleryUpdates.CheckAsync("0.5.0");
            GalleryUpdates.Status.ShouldBe(GalleryUpdateStatus.Failed);
        }
        finally
        {
            GalleryReleases.Provider = old;
            GalleryUpdates.Reset();
        }
    }

    [Fact]
    public void The_Newest_Release_Is_Read_From_GitHubs_List_Without_Drafts()
    {
        using var document = JsonDocument.Parse("""
            [
              { "draft": true, "tag_name": "v9.0.0", "html_url": "https://example.test/9", "assets": [] },
              { "draft": false, "tag_name": "v0.4.0-preview.10", "html_url": "https://example.test/10",
                "assets": [ { "name": "TKCode.Arc.Gallery-0.4.0-preview.10-win-x64.exe", "browser_download_url": "https://example.test/a.exe", "size": 100 } ] },
              { "draft": false, "tag_name": "v0.4.0-preview.9", "html_url": "https://example.test/8", "assets": [] }
            ]
            """);
        var newest = GalleryReleases.Newest(document.RootElement).ShouldNotBeNull();
        newest.Version.ShouldBe("0.4.0-preview.10");
        newest.AssetFor("win-x64")!.Size.ShouldBe(100);
        newest.AssetFor("linux-x64").ShouldBeNull();
    }

    [Theory]
    [InlineData("0.3.1", "0.3.1-preview.2", 1)]
    [InlineData("0.4.0-preview.10", "0.4.0-preview.2", 1)]
    [InlineData("0.4.0-preview.2", "0.4.0-preview.2", 0)]
    [InlineData("0.3.9", "0.4.0-preview.1", -1)]
    public void Versions_Compare_By_Semantic_Order(string left, string right, int sign) =>
        Math.Sign(GalleryReleases.Compare(left, right)).ShouldBe(sign);

    [AvaloniaFact]
    public void About_Shows_The_Version_And_Runs_The_Check_From_The_Menu()
    {
        WithShell(GalleryHostKind.Desktop, 1280, (shell, window) =>
        {
            shell.CheckForUpdates();
            for (var i = 0; i < 4; i++)
            {
                Dispatcher.UIThread.RunJobs();
            }
            shell.Route.ShouldBe(GalleryRoute.Settings("about"));
            var settings = window.GetVisualDescendants().OfType<GallerySettingsView>().Single();
            settings.CurrentPage.ShouldBe("about");
            GalleryUpdates.Status.ShouldBe(GalleryUpdateStatus.Available);
            settings.UpdateStatusText.ShouldContain("9.9.9");

            shell.Navigate(GalleryRoute.Settings("language"));
            settings.CurrentPage.ShouldBe("language");
            shell.Route.Path.ShouldBe("/settings/language");
        });
    }

    [AvaloniaFact]
    public void StreamMark_Has_Its_Own_Page_That_Marks_Products_And_Streams_A_Reply()
    {
        GalleryRoute.Parse("/streammark").ShouldBe(GalleryRoute.StreamMark);
        GalleryRoute.StreamMark.Path.ShouldBe("/streammark");
        GalleryRoute.StreamMark.Title.ShouldBe("StreamMark · TKCode Gallery");
        GalleryRoute.Parse("/streammark/extra").ShouldBeNull();

        WithShell(GalleryHostKind.Web, 1280, (_, window) =>
        {
            var page = window.GetVisualDescendants().OfType<GalleryStreamMarkView>().Single();
            page.Stage.Finish();
            page.Stage.Message.Stream.ShouldNotBeNull();
        }, GalleryRoute.StreamMark);
    }

    [AvaloniaFact]
    public void The_Reply_Is_Translated()
    {
        try
        {
            GalleryText.SetLanguage("zh-CN");
            GalleryStreamStage.BuildReply().ShouldContain("流式渲染");
            GalleryStreamStage.BuildReply().ShouldContain("markdown.Append(chunk)");
        }
        finally
        {
            GalleryText.SetLanguage("en-US");
        }
    }

    [AvaloniaFact]
    public void The_Home_Page_Shows_No_Components_Or_Projects()
    {
        WithShell(GalleryHostKind.Web, 1280, (_, window) =>
        {
            var home = Home(window);
            home.GetVisualDescendants().OfType<GalleryAtlas>().ShouldBeEmpty();
            home.GetVisualDescendants().OfType<GalleryStreamStage>().ShouldBeEmpty();
            home.GetVisualDescendants().OfType<GalleryDemoView>().ShouldBeEmpty();
        });
    }

    [AvaloniaFact]
    public void The_Home_Page_Has_The_Community_Code_And_Its_Asset_Loads()
    {
        WithShell(GalleryHostKind.Web, 1280, (_, window) =>
        {
            Home(window).CommunityCode.ShouldNotBeNull();
            using var code = GalleryBrand.CreateCommunityCode();
            // The headless platform decodes every bitmap to 1x1, so the real size can only be checked on the asset itself.
            using var asset = Avalonia.Platform.AssetLoader.Open(new Uri("avares://TKCode.Arc.Gallery/Assets/community-qq.jpg"));
            asset.Length.ShouldBeGreaterThan(10_000);
            GalleryHomeView.CommunityGroupNumber.ShouldBe("922379598");
        });
    }

    [AvaloniaFact]
    public void Open_Source_Lists_The_Projects_With_Their_Licenses()
    {
        GalleryRoute.Parse("/projects").ShouldBe(GalleryRoute.Projects);
        GalleryProjectsView.Projects.Select(p => p.Name).ShouldBe(["StreamMark", "Jaunt", "Spyglass"]);
        GalleryProjectsView.Projects.ShouldAllBe(p => p.License == "MIT");
        WithShell(GalleryHostKind.Web, 1280, (shell, window) =>
        {
            window.GetVisualDescendants().OfType<GalleryProjectsView>().ShouldHaveSingleItem();
            shell.TopBar.MarkedNavIndex.ShouldBe(1);
        }, GalleryRoute.Projects);
    }

    [AvaloniaFact]
    public void The_Components_Page_Lists_Every_Part_By_Tab_And_Filters()
    {
        GalleryRoute.Parse("/components").ShouldBe(GalleryRoute.ComponentsIndex);
        WithShell(GalleryHostKind.Web, 1280, (shell, window) =>
        {
            shell.TopBar.MarkedNavIndex.ShouldBe(0);
            var page = window.GetVisualDescendants().OfType<GalleryComponentsView>().Single();
            page.Current.Build();
            var all = page.Current.VisibleEntries.Count();
            all.ShouldBeGreaterThanOrEqualTo(TKCode.Arc.Gallery.Catalog.GalleryCatalog.Count);
            page.Counts[0].Text.ShouldBe(all.ToString(), "every tab shows its size");
            page.Counts[1].Text.ShouldBe(TKCode.Arc.Gallery.Catalog.GalleryCatalog.Blocks.AllEntries.Count().ToString());
            page.FilterBox.Text = "chart";
            Dispatcher.UIThread.RunJobs();
            page.Current.VisibleEntries.ShouldNotBeEmpty();
            page.Current.VisibleEntries.ShouldAllBe(entry => entry.Title.Contains("chart", StringComparison.OrdinalIgnoreCase) || entry.ClassName.Contains("chart", StringComparison.OrdinalIgnoreCase));
            page.Counts[0].Text.ShouldBe(page.Current.VisibleEntries.Count().ToString(), "while filtering, a tab shows how many of its entries match");
            page.Current.GetVisualDescendants().OfType<TextBlock>().ShouldNotContain(t => t.IsVisible && t.Text!.StartsWith("No components match", StringComparison.Ordinal));
            page.FilterBox.Text = "zzzz-nothing";
            Dispatcher.UIThread.RunJobs();
            page.Current.VisibleEntries.ShouldBeEmpty();
            page.Counts[0].Text.ShouldBe("0");
            page.Current.GetVisualDescendants().OfType<TextBlock>().ShouldContain(t => t.IsVisible && t.Text == "No components match “zzzz-nothing”.", "the list says that nothing matches");
            page.FilterBox.Text = string.Empty;
            Dispatcher.UIThread.RunJobs();
            page.Current.VisibleEntries.Count().ShouldBe(all);
            page.Counts[0].Text.ShouldBe(all.ToString());

            page.Show(3);
            Dispatcher.UIThread.RunJobs();
            page.Current.Build();
            page.Current.VisibleEntries.Count().ShouldBe(TKCode.Arc.Gallery.Catalog.GalleryCatalog.Mobile.Entries.Length);
        }, GalleryRoute.ComponentsIndex);
    }

    [AvaloniaFact]
    public void On_A_Phone_The_Sidebar_Is_A_Drawer_That_Closes_When_A_Page_Opens()
    {
        var entry = TKCode.Arc.Gallery.Catalog.GalleryCatalog.Categories[0].Entries[0];
        WithShell(GalleryHostKind.Web, 390, (shell, window) =>
        {
            shell.IsNarrow.ShouldBeTrue();
            shell.BrowseBar.IsVisible.ShouldBeTrue();
            var page = window.GetVisualDescendants().OfType<GalleryPageView>().Single();
            page.Bounds.Width.ShouldBe(390, 0.5, "the page has the phone's whole width");

            shell.SidebarDrawer.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            shell.Sidebar.Select(TKCode.Arc.Gallery.Catalog.GalleryCatalog.Categories[1].Entries[0]);
            Dispatcher.UIThread.RunJobs();
            shell.SidebarDrawer.IsOpen.ShouldBeFalse();

            // A wider window takes the sidebar back.
            window.Width = 1280;
            for (var i = 0; i < 4; i++)
            {
                Dispatcher.UIThread.RunJobs();
            }
            shell.IsNarrow.ShouldBeFalse();
            shell.BrowseBar.IsVisible.ShouldBeFalse();
            shell.Sidebar.Bounds.Width.ShouldBeGreaterThan(0);
        }, GalleryRoute.To(entry));
    }

    [AvaloniaFact]
    public void The_Home_Page_Fits_A_Phone()
    {
        WithShell(GalleryHostKind.Web, 390, (shell, window) =>
        {
            shell.TopBar.DownloadButton!.IsVisible.ShouldBeFalse("a phone has nothing to install");
            var home = Home(window);
            foreach (var panel in home.GetVisualDescendants().OfType<GalleryGrid>().Where(g => g.IsEffectivelyVisible))
            {
                panel.Bounds.Width.ShouldBeLessThanOrEqualTo(390, $"{panel.Name}");
            }
        }, os: GalleryClientOs.Mobile);
    }

    [Fact]
    public void Grid_Columns_Follow_The_Room()
    {
        var grid = new GalleryGrid { MinItemWidth = 240, MaxColumns = 4, Spacing = 16 };
        grid.Children.Add(new Border { Height = 10 });
        grid.Children.Add(new Border { Height = 30 });
        grid.Measure(new Size(358, 1000));
        grid.ActualColumns.ShouldBe(1);
        grid.DesiredSize.Height.ShouldBe(10 + 16 + 30);
        grid.Measure(new Size(1180, 1000));
        grid.ActualColumns.ShouldBe(4);
        grid.DesiredSize.Height.ShouldBe(30);
    }
}
