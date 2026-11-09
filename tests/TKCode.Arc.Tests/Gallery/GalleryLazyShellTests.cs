using TKCode.Arc.Gallery;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>What the shell builds only when it is needed behaves as if it had been there all along.</summary>
public class GalleryLazyShellTests
{
    private static void Settle()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void The_Home_Page_Does_Not_Build_The_Sidebar()
    {
        var shell = new GalleryShell();
        var window = TestHelpers.Show(shell);
        try
        {
            window.Width = 1280;
            window.Height = 860;
            Settle();
            shell.Body.GetVisualDescendants().OfType<GallerySidebar>().ShouldBeEmpty();

            // Asked for, it is there and hidden, in its place in the page.
            shell.Sidebar.IsVisible.ShouldBeFalse();
            shell.Body.GetVisualChildren().First().ShouldBe(shell.Sidebar.GetVisualParent(), "before the page, in tab order");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Sidebar_Built_In_Steps_Lists_The_Same_Groups_As_One_Built_At_Once()
    {
        var whole = new GallerySidebar();
        var stepped = new GallerySidebar(deferred: true);
        stepped.VisibleEntries.Count.ShouldBeLessThan(whole.VisibleEntries.Count);
        var steps = 0;
        while (stepped.BuildStep())
        {
            steps++;
        }
        steps.ShouldBeGreaterThan(20, "one step per group, so the work spreads over many frames");
        stepped.VisibleEntries.ShouldBe(whole.VisibleEntries);
        stepped.BuildStep().ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Asking_For_A_Sidebar_Still_Being_Built_Finishes_It()
    {
        var sidebar = new GallerySidebar(deferred: true);
        sidebar.BuildStep().ShouldBeTrue();
        sidebar.CompleteBuild();
        sidebar.VisibleEntries.ShouldBe(new GallerySidebar().VisibleEntries);
    }

    [AvaloniaFact]
    public void A_Sidebar_First_Needed_On_A_Narrow_Page_Goes_Into_The_Drawer()
    {
        var shell = new GalleryShell();
        var window = TestHelpers.Show(shell);
        try
        {
            window.Width = 390;
            window.Height = 700;
            Settle();
            shell.IsNarrow.ShouldBeTrue();
            shell.Select(GalleryCatalog.Categories[0].Entries[0]);
            Settle();
            shell.SidebarDrawer.Content.ShouldBe(shell.Sidebar);

            window.Width = 1280;
            Settle();
            shell.SidebarDrawer.Content.ShouldBeNull();
            shell.Sidebar.Bounds.Width.ShouldBe(280);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Group_Builds_Its_Rows_When_It_Opens()
    {
        var shell = new GalleryShell(GalleryRoute.To(GalleryCatalog.Categories[0].Entries[0]));
        var window = TestHelpers.Show(shell);
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        try
        {
            Settle();
            var rows = () => shell.Sidebar.GetVisualDescendants().OfType<GalleryNavRow>().Where(r => r.IsEffectivelyVisible).Count();
            var closed = rows();
            closed.ShouldBeLessThan(GalleryCatalog.AllEntries.Count() / 2);

            shell.Select(GalleryCatalog.Categories[1].Entries[0]);
            Settle();
            rows().ShouldBeGreaterThan(closed);

            // A filter opens the groups it lets through: every entry that matches has a row.
            shell.Sidebar.SearchText = "a";
            Settle();
            shell.Sidebar.GetVisualDescendants().OfType<GalleryNavRow>().Count(r => r.IsEffectivelyVisible)
                 .ShouldBeGreaterThanOrEqualTo(shell.Sidebar.VisibleEntries.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Bar_Glides_To_A_Row_Whose_Group_Opens_Just_Then()
    {
        var shell = new GalleryShell(GalleryRoute.To(GalleryCatalog.Categories[0].Entries[0]));
        var window = TestHelpers.Show(shell);
        ArcMotion.SetLevel(window, ArcMotionLevel.Full);
        try
        {
            window.Width = 1280;
            window.Height = 860;
            Settle();
            var bar = shell.Sidebar.GetVisualDescendants().OfType<GlidingIndicator>().Last();
            bar.HasTarget.ShouldBeTrue();
            var before = bar.CurrentRect;

            shell.Select(GalleryCatalog.Categories[1].Entries[0]);
            bar.HasTarget.ShouldBeTrue();
            bar.CurrentRect.ShouldBe(before, "a selection change glides; it does not jump to the new row");

            TestHelpers.PumpUntil(() => Math.Abs(bar.CurrentRect.Y - before.Y) > 8, 2000).ShouldBeTrue("the bar never left");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Bar_Follows_The_Selected_Row_When_The_Groups_Above_It_Change()
    {
        var shell = new GalleryShell(GalleryRoute.To(GalleryCatalog.Categories[1].Entries[0]));
        var window = TestHelpers.Show(shell);
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        try
        {
            window.Width = 1280;
            window.Height = 860;
            Settle();
            var bar = shell.Sidebar.GetVisualDescendants().OfType<GlidingIndicator>().Last();
            var before = bar.CurrentRect;

            shell.Sidebar.SearchText = GalleryCatalog.Categories[1].Entries[0].ClassName;
            Settle();
            bar.CurrentRect.Y.ShouldBeLessThan(before.Y, "the groups above it went away, so the selected row moved up");

            shell.Sidebar.SearchText = string.Empty;
            Settle();
            bar.CurrentRect.ShouldBe(before);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void The_Size_Of_A_Capture_Is_Read_From_Its_WebP_Header()
    {
        var lossless = new byte[30];
        "RIFF"u8.CopyTo(lossless);
        "WEBP"u8.CopyTo(lossless.AsSpan(8));
        "VP8L"u8.CopyTo(lossless.AsSpan(12));
        lossless[20] = 0x2F;
        // 1680 and 900, less one, in 14 bits each.
        var bits = (uint)(1679 | 899 << 14);
        BitConverter.GetBytes(bits).CopyTo(lossless.AsSpan(21));
        GalleryShotView.ParseWebPSize(lossless).ShouldBe(new PixelSize(1680, 900));

        var extended = new byte[30];
        "RIFF"u8.CopyTo(extended);
        "WEBP"u8.CopyTo(extended.AsSpan(8));
        "VP8X"u8.CopyTo(extended.AsSpan(12));
        extended[24] = 0x8F; // 1679 = 0x068F
        extended[25] = 0x06;
        extended[27] = 0x83; // 899 = 0x0383
        extended[28] = 0x03;
        GalleryShotView.ParseWebPSize(extended).ShouldBe(new PixelSize(1680, 900));

        GalleryShotView.ParseWebPSize(new byte[30]).ShouldBeNull();
    }

    [Fact]
    public void Every_Capture_In_The_Repository_Has_A_Readable_Size()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "TKCode.Arc.slnx")))
        {
            folder = folder.Parent;
        }
        var docs = Path.Combine(folder!.FullName, "gallery", "TKCode.Arc.Gallery", "Docs");
        var checkedAny = false;
        foreach (var path in Directory.EnumerateFiles(docs, "*.webp", SearchOption.AllDirectories))
        {
            using var file = File.OpenRead(path);
            var header = new byte[30];
            file.ReadExactly(header);
            GalleryShotView.ParseWebPSize(header).ShouldNotBeNull(path);
            checkedAny = true;
        }
        checkedAny.ShouldBeTrue();
    }

    [Fact]
    public async Task Only_A_Page_With_A_Player_Asks_For_The_Media_Backend_And_Only_Once()
    {
        var asked = 0;
        var answer = new TaskCompletionSource<bool>();
        GalleryModules.MediaBackend = () =>
        {
            asked++;
            return answer.Task;
        };
        try
        {
            var button = GalleryCatalog.Categories[0].Entries[0];
            GalleryModules.IsReady(button).ShouldBeTrue();
            (await GalleryModules.EnsureAsync(button)).ShouldBeTrue();
            asked.ShouldBe(0);

            var player = GalleryCatalog.Blocks.AllEntries.First(e => e.ClassName == "MediaPlayer");
            var video = GalleryCatalog.Video.Entries[0];
            GalleryModules.IsReady(player).ShouldBeFalse();
            GalleryModules.IsReady(video).ShouldBeFalse();
            var first = GalleryModules.EnsureAsync(player);
            var second = GalleryModules.EnsureAsync(video);
            first.IsCompleted.ShouldBeFalse("the page waits for its players' backend");
            answer.SetResult(false);
            (await first).ShouldBeTrue("a backend that failed still opens the page");
            (await second).ShouldBeTrue();
            asked.ShouldBe(1);
            GalleryModules.IsReady(player).ShouldBeTrue();
        }
        finally
        {
            GalleryModules.MediaBackend = null;
        }
    }
}
