using TKCode.Arc.Gallery;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

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
