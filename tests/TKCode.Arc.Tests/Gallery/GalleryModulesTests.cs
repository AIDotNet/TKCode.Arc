using TKCode.Arc.Gallery;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>
/// The Special components on demand (<see cref="GalleryModules"/>): a host that passes a loader to <c>UseArcGallery</c>
/// starts without TKCode.Arc.Special, and a Special page loads it once before it builds its demos, as the WebAssembly host's
/// trimmed publish does.
/// </summary>
public class GalleryModulesTests
{
    private const string Special = "TKCode.Arc.Special";

    private sealed class PendingLoader : IGalleryAssemblyLoader
    {
        public List<string> Asked { get; } = new();

        public TaskCompletionSource<bool> Pending { get; private set; } = new();

        public Task<bool> LoadAsync(string name)
        {
            Asked.Add(name);
            Pending = new TaskCompletionSource<bool>();
            return Pending.Task;
        }
    }

    private static GalleryEntry Entry(string className) => GalleryCatalog.AllEntries.Single(e => e.ClassName == className);

    [Fact]
    public void A_Host_That_Loads_On_Demand_Starts_Without_The_Special_Package()
    {
        try
        {
            var eager = new ArcBuilder().UseArcGallery();
            eager.IsEnabled(Special).ShouldBeTrue();
            GalleryModules.LoadsOnDemand.ShouldBeFalse();

            var onDemand = new ArcBuilder().UseArcGallery(new PendingLoader());
            onDemand.IsEnabled(Special).ShouldBeFalse();
            onDemand.IsEnabled("TKCode.Arc.Blocks").ShouldBeTrue("the top bar and the home page are blocks");
            GalleryModules.LoadsOnDemand.ShouldBeTrue();
        }
        finally
        {
            new ArcBuilder().UseArcGallery();
        }
    }

    [Fact]
    public async Task Only_A_Pdf_Page_Asks_For_The_Pdf_Backend_And_A_Failure_Still_Opens_It()
    {
        var asked = 0;
        var answer = new TaskCompletionSource<bool>();
        GalleryModules.PdfBackend = () =>
        {
            asked++;
            return answer.Task;
        };
        try
        {
            GalleryModules.IsReady(Entry("Button")).ShouldBeTrue();
            (await GalleryModules.EnsureAsync(Entry("Button"))).ShouldBeTrue();
            GalleryModules.IsReady(Entry("PageViewer")).ShouldBeTrue("PageViewer has no PDF in it");
            asked.ShouldBe(0);

            GalleryModules.IsReady(Entry("PdfViewer")).ShouldBeFalse();
            var page = GalleryModules.EnsureAsync(Entry("PdfViewer"));
            page.IsCompleted.ShouldBeFalse();
            asked.ShouldBe(1);
            answer.SetResult(false);
            (await page).ShouldBeTrue("the demos fall back to a simulated document");
        }
        finally
        {
            GalleryModules.PdfBackend = null;
        }
        GalleryModules.IsReady(Entry("PdfViewer")).ShouldBeTrue();
    }

    [Fact]
    public void The_Pdf_Demos_Use_The_Sample_With_A_Renderer_And_Generated_Pages_Without()
    {
        var root = TKCode.Arc.Gallery.Demos.PdfDemoMedia.HttpRoot;
        var previous = ArcPdf.Backend;
        try
        {
            ArcPdf.Backend = new TKCode.Arc.Tests.Pdf.FakePdfBackend();
            TKCode.Arc.Gallery.Demos.PdfDemoMedia.Simulated.ShouldBeNull();
            TKCode.Arc.Gallery.Demos.PdfDemoMedia.HttpRoot = null;
            TKCode.Arc.Gallery.Demos.PdfDemoMedia.Sample!.Uri!.AbsoluteUri.ShouldBe("avares://TKCode.Arc.Gallery.Shots/Assets/Pdf/arc-sample.pdf");
            TKCode.Arc.Gallery.Demos.PdfDemoMedia.HttpRoot = new Uri("https://example.com/site/");
            TKCode.Arc.Gallery.Demos.PdfDemoMedia.Locked!.Uri!.AbsoluteUri.ShouldBe("https://example.com/site/media/Pdf/arc-sample-locked.pdf");

            ArcPdf.Backend = new NoRenderer();
            TKCode.Arc.Gallery.Demos.PdfDemoMedia.Sample.ShouldBeNull();
            TKCode.Arc.Gallery.Demos.PdfDemoMedia.Locked.ShouldBeNull();
            TKCode.Arc.Gallery.Demos.PdfDemoMedia.Simulated.ShouldBeOfType<SimulatedPdfDocument>().Dispose();
        }
        finally
        {
            ArcPdf.Backend = previous;
            TKCode.Arc.Gallery.Demos.PdfDemoMedia.HttpRoot = root;
        }
    }

    private sealed class NoRenderer : IPdfBackend
    {
        public string Name => "none";

        public bool IsReal => false;

        public Task<PdfDocument> OpenAsync(PdfSource source, string? password, CancellationToken cancellationToken) =>
            throw new PdfException(PdfErrorKind.BackendUnavailable);
    }

    [AvaloniaFact]
    public void A_Special_Page_Loads_The_Assembly_Once_Before_Its_Demos()
    {
        var loader = new PendingLoader();
        new ArcBuilder().UseArcGallery(loader);
        try
        {
            GalleryModules.IsReady(Entry("Button")).ShouldBeTrue();
            GalleryModules.EnsureAsync(Entry("Button")).Result.ShouldBeTrue();
            loader.Asked.ShouldBeEmpty();

            var page = new GalleryPageView(Entry("AgentStatus"));
            var window = TestHelpers.Show(page);
            try
            {
                loader.Asked.ShouldBe([Special]);
                page.Docs.ShouldNotBeNull().Text.ShouldBeNullOrEmpty("the demos wait for the assembly");

                loader.Pending.SetResult(true);
                Dispatcher.UIThread.RunJobs();
                page.Docs.Text.ShouldNotBeNullOrEmpty();
                GalleryModules.IsReady(Entry("AgentStatus")).ShouldBeTrue();

                // The other Special pages find it loaded.
                GalleryModules.IsReady(Entry("DotGrid")).ShouldBeTrue();
                GalleryModules.EnsureAsync(Entry("DotGrid")).Result.ShouldBeTrue();
                loader.Asked.Count.ShouldBe(1);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        }
        finally
        {
            new ArcBuilder().UseArcGallery();
        }
    }

    [AvaloniaFact]
    public void A_Home_Page_Attached_Twice_While_The_Components_Load_Gets_One_Dot_Field()
    {
        var loader = new PendingLoader();
        new ArcBuilder().UseArcGallery(loader);
        try
        {
            var shell = new GalleryShell();
            var home = new GalleryHomeView(shell);
            var host = new ContentControl { Content = home };
            var window = TestHelpers.Show(host, ArcMotionLevel.Full);
            try
            {
                Dispatcher.UIThread.RunJobs();
                host.Content = null;
                Dispatcher.UIThread.RunJobs();
                host.Content = home;
                Dispatcher.UIThread.RunJobs();
                home.HasDotField.ShouldBeFalse("still loading");

                loader.Pending.SetResult(true);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();

                home.HasDotField.ShouldBeTrue();
                home.GetVisualDescendants().OfType<DotGrid>().Count().ShouldBe(1, "a second field would draw over the hero");
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        }
        finally
        {
            new ArcBuilder().UseArcGallery();
        }
    }

    [AvaloniaFact]
    public void A_Page_Whose_Assembly_Cannot_Be_Loaded_Says_So_And_The_Next_One_Tries_Again()
    {
        var loader = new PendingLoader();
        new ArcBuilder().UseArcGallery(loader);
        try
        {
            var page = new GalleryPageView(Entry("AgentStatus"));
            var window = TestHelpers.Show(page);
            try
            {
                loader.Pending.SetResult(false);
                Dispatcher.UIThread.RunJobs();
                page.Docs.ShouldNotBeNull().Text.ShouldBe("> " + GalleryText.Get(GalleryShellLangResourceKind.PageLoadFailed));
                GalleryModules.IsReady(Entry("AgentStatus")).ShouldBeFalse();

                var retry = GalleryModules.EnsureAsync(Entry("DotGrid"));
                loader.Asked.ShouldBe([Special, Special]);
                loader.Pending.SetResult(true);
                Dispatcher.UIThread.RunJobs();
                retry.IsCompletedSuccessfully.ShouldBeTrue();
                retry.Result.ShouldBeTrue();
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        }
        finally
        {
            new ArcBuilder().UseArcGallery();
        }
    }
}
