using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>A screenshot source that fetches its captures later, as the WebAssembly host's does.</summary>
public class GalleryShotSourceTests
{
    private const string Source = "Docs/TextFields/Input/images/WorkspaceForm";

    private sealed class PendingSource : IGalleryShotSource
    {
        public TaskCompletionSource<Stream?> Capture { get; } = new();

        public List<Uri> Asked { get; } = new();

        public ValueTask<Stream?> OpenAsync(Uri uri)
        {
            Asked.Add(uri);
            return new ValueTask<Stream?>(Capture.Task);
        }
    }

    private static MemoryStream LightCapture()
    {
        using var png = AssetLoader.Open(GalleryDocs.ScreenshotUri(Source, false));
        var copy = new MemoryStream();
        png.CopyTo(copy);
        copy.Position = 0;
        return copy;
    }

    [AvaloniaFact]
    public void A_Capture_That_Arrives_Later_Is_Shown_When_It_Does()
    {
        var pending = new PendingSource();
        var original = GalleryShots.Source;
        GalleryShots.Source = pending;
        try
        {
            var view = new GalleryShotView(Source, "A workspace settings form");
            var window = TestHelpers.Show(view);
            try
            {
                view.Bitmap.ShouldBeNull("the capture has not arrived");
                pending.Asked.ShouldHaveSingleItem().ShouldBe(GalleryDocs.ScreenshotUri(Source, false));

                pending.Capture.SetResult(LightCapture());
                Dispatcher.UIThread.RunJobs();
                view.Bitmap.ShouldNotBeNull();
                view.IsDarkShown.ShouldBeFalse();
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        }
        finally
        {
            GalleryShots.Source = original;
        }
    }

    [AvaloniaFact]
    public void A_Capture_That_Arrives_After_The_View_Left_Is_Dropped()
    {
        var pending = new PendingSource();
        var original = GalleryShots.Source;
        GalleryShots.Source = pending;
        try
        {
            var view = new GalleryShotView(Source, "A workspace settings form");
            var window = TestHelpers.Show(view);
            window.Close();
            Dispatcher.UIThread.RunJobs();

            pending.Capture.SetResult(LightCapture());
            Dispatcher.UIThread.RunJobs();
            view.Bitmap.ShouldBeNull("a detached view does not keep a capture");
        }
        finally
        {
            GalleryShots.Source = original;
        }
    }

    [AvaloniaFact]
    public void A_Capture_That_Cannot_Be_Fetched_Leaves_The_Page_Without_A_Picture()
    {
        var pending = new PendingSource();
        var original = GalleryShots.Source;
        GalleryShots.Source = pending;
        try
        {
            var view = new GalleryShotView(Source, "A workspace settings form");
            var window = TestHelpers.Show(view);
            try
            {
                pending.Capture.SetException(new HttpRequestException("offline"));
                Dispatcher.UIThread.RunJobs();
                view.Bitmap.ShouldBeNull();
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        }
        finally
        {
            GalleryShots.Source = original;
        }
    }
}
