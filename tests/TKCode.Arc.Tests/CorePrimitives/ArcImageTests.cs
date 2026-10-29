using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.CorePrimitives;

/// <summary>
/// Core <see cref="ArcImage"/>: loading state, the shared cache, cancellation and failures, through a fake
/// <see cref="IArcImageLoader"/> (no network).
/// </summary>
public class ArcImageTests
{
    /// <summary>A 1×1 PNG.</summary>
    private static readonly byte[] s_png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    /// <summary>Answers each request when the test says so; records the calls and their tokens.</summary>
    private sealed class FakeLoader : IArcImageLoader
    {
        private readonly bool _manual;

        public FakeLoader(bool manual = false) => _manual = manual;

        public List<(ImageSource Source, CancellationToken Token, TaskCompletionSource<Stream> Answer)> Calls { get; } = new();

        public bool Fail { get; set; }

        public Task<Stream> OpenAsync(ImageSource source, CancellationToken cancellationToken)
        {
            var answer = new TaskCompletionSource<Stream>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => answer.TrySetCanceled(cancellationToken));
            Calls.Add((source, cancellationToken, answer));
            if (!_manual)
            {
                Answer(Calls.Count - 1);
            }
            return answer.Task;
        }

        public void Answer(int call)
        {
            if (Fail)
            {
                Calls[call].Answer.TrySetException(new HttpRequestException("404"));
            }
            else
            {
                Calls[call].Answer.TrySetResult(new MemoryStream(s_png));
            }
        }
    }

    private static ImageSource Unique(string name = "photo") => ImageSource.Parse($"https://images.test/{Guid.NewGuid():N}/{name}.png");

    private static (ArcImage Image, Window Window) Show(FakeLoader loader, ImageSource? source = null)
    {
        var image = new ArcImage { Width = 32, Height = 32, Stretch = Stretch.UniformToFill, Loader = loader, Source = source };
        return (image, TestHelpers.Show(new StackPanel { Children = { image } }));
    }

    [AvaloniaFact]
    public void Loads_Through_The_Loader_And_Reports_Its_State()
    {
        var loader = new FakeLoader(manual: true);
        var source = Unique();
        var image = new ArcImage { Width = 32, Height = 32, Loader = loader };
        var states = new List<string>();
        image.PropertyChanged += (_, e) =>
        {
            if (e.Property == ArcImage.IsLoadingProperty || e.Property == ArcImage.IsImageLoadedProperty || e.Property == ArcImage.IsFailedProperty)
            {
                states.Add($"{e.Property.Name}={e.NewValue}");
            }
        };
        image.Source = source;
        // Pending before it is attached; nothing is read until it is.
        image.IsLoading.ShouldBeTrue();
        image.IsImageLoaded.ShouldBeFalse();
        loader.Calls.ShouldBeEmpty();

        var window = TestHelpers.Show(new StackPanel { Children = { image } });
        try
        {
            TestHelpers.PumpUntil(() => loader.Calls.Count == 1).ShouldBeTrue();
            loader.Calls[0].Source.ShouldBeSameAs(source);
            image.IsLoading.ShouldBeTrue();
            image.CurrentImage.ShouldBeNull();

            loader.Answer(0);
            TestHelpers.PumpUntil(() => image.IsImageLoaded).ShouldBeTrue();
            image.IsLoading.ShouldBeFalse();
            image.IsFailed.ShouldBeFalse();
            image.CurrentImage.ShouldNotBeNull();
            states.ShouldBe(["IsLoading=True", "IsImageLoaded=True", "IsLoading=False"]);
            ArcImageCache.Contains(source).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Cached_Picture_Shows_At_Once_Without_Loading()
    {
        var loader = new FakeLoader();
        var url = $"https://images.test/{Guid.NewGuid():N}/cached.png";
        var (first, window) = Show(loader, ImageSource.Parse(url));
        try
        {
            TestHelpers.PumpUntil(() => first.IsImageLoaded).ShouldBeTrue();
            loader.Calls.Count.ShouldBe(1);

            // A new source object for the same address is served from the cache, synchronously.
            var second = new ArcImage { Loader = loader };
            var sawLoading = false;
            second.PropertyChanged += (_, e) => sawLoading |= e.Property == ArcImage.IsLoadingProperty && e.NewValue is true;
            second.Source = ImageSource.Parse(url);
            second.IsImageLoaded.ShouldBeTrue();
            second.CurrentImage.ShouldBeSameAs(first.CurrentImage);
            sawLoading.ShouldBeFalse();
            ((StackPanel)first.Parent!).Children.Add(second);
            Dispatcher.UIThread.RunJobs();
            loader.Calls.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Images_Waiting_For_The_Same_Address_Share_One_Request()
    {
        var loader = new FakeLoader(manual: true);
        var url = $"https://images.test/{Guid.NewGuid():N}/shared.png";
        var a = new ArcImage { Loader = loader, Source = ImageSource.Parse(url) };
        var b = new ArcImage { Loader = loader, Source = ImageSource.Parse(url) };
        var window = TestHelpers.Show(new StackPanel { Children = { a, b } });
        try
        {
            TestHelpers.PumpUntil(() => loader.Calls.Count > 0).ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            loader.Calls.Count.ShouldBe(1);

            // One of them leaves: the shared download carries on for the other.
            a.Source = null;
            Dispatcher.UIThread.RunJobs();
            loader.Calls[0].Token.IsCancellationRequested.ShouldBeFalse();

            loader.Answer(0);
            TestHelpers.PumpUntil(() => b.IsImageLoaded).ShouldBeTrue();
            a.IsImageLoaded.ShouldBeFalse();
            a.IsLoading.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Changing_The_Source_Cancels_The_Running_Load()
    {
        var loader = new FakeLoader(manual: true);
        var (image, window) = Show(loader, Unique("first"));
        try
        {
            TestHelpers.PumpUntil(() => loader.Calls.Count == 1).ShouldBeTrue();
            var next = Unique("second");
            image.Source = next;
            Dispatcher.UIThread.RunJobs();
            loader.Calls[0].Token.IsCancellationRequested.ShouldBeTrue();
            image.IsLoading.ShouldBeTrue();

            TestHelpers.PumpUntil(() => loader.Calls.Count == 2).ShouldBeTrue();
            loader.Calls[1].Source.ShouldBeSameAs(next);
            // The first answer arriving late changes nothing.
            loader.Calls[0].Answer.TrySetResult(new MemoryStream(s_png));
            Dispatcher.UIThread.RunJobs();
            image.IsImageLoaded.ShouldBeFalse();

            loader.Answer(1);
            TestHelpers.PumpUntil(() => image.IsImageLoaded).ShouldBeTrue();
            image.IsLoading.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Leaving_The_Tree_Cancels_And_Coming_Back_Resumes()
    {
        var loader = new FakeLoader(manual: true);
        var (image, window) = Show(loader, Unique());
        try
        {
            TestHelpers.PumpUntil(() => loader.Calls.Count == 1).ShouldBeTrue();
            var panel = (StackPanel)image.Parent!;
            panel.Children.Remove(image);
            Dispatcher.UIThread.RunJobs();
            loader.Calls[0].Token.IsCancellationRequested.ShouldBeTrue();
            image.IsLoading.ShouldBeTrue();

            panel.Children.Add(image);
            TestHelpers.PumpUntil(() => loader.Calls.Count == 2).ShouldBeTrue();
            loader.Answer(1);
            TestHelpers.PumpUntil(() => image.IsImageLoaded).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failed_Load_Reports_IsFailed_And_A_New_Source_Retries()
    {
        var loader = new FakeLoader { Fail = true };
        var url = $"https://images.test/{Guid.NewGuid():N}/missing.png";
        var (image, window) = Show(loader, ImageSource.Parse(url));
        try
        {
            TestHelpers.PumpUntil(() => image.IsFailed).ShouldBeTrue();
            image.IsLoading.ShouldBeFalse();
            image.IsImageLoaded.ShouldBeFalse();
            image.LoadError.ShouldBeOfType<HttpRequestException>();
            ArcImageCache.Contains(ImageSource.Parse(url)).ShouldBeFalse();

            // Failures are not cached: assigning the address again asks for it again.
            loader.Fail = false;
            image.Source = ImageSource.Parse(url);
            image.IsFailed.ShouldBeFalse();
            image.IsLoading.ShouldBeTrue();
            TestHelpers.PumpUntil(() => image.IsImageLoaded).ShouldBeTrue();
            loader.Calls.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Bitmap_Source_Shows_At_Once_And_Null_Clears()
    {
        var loader = new FakeLoader();
        var bitmap = new WriteableBitmap(new PixelSize(4, 2), new Vector(96, 96), PixelFormats.Bgra8888, AlphaFormat.Premul);
        var (image, window) = Show(loader, new BitmapImageSource(bitmap));
        try
        {
            image.IsImageLoaded.ShouldBeTrue();
            image.CurrentImage.ShouldBeSameAs(bitmap);
            Dispatcher.UIThread.RunJobs();
            loader.Calls.ShouldBeEmpty();

            image.Source = null;
            image.IsImageLoaded.ShouldBeFalse();
            image.IsLoading.ShouldBeFalse();
            image.CurrentImage.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Loader_Is_Inherited_From_The_Window()
    {
        var loader = new FakeLoader();
        var image = new ArcImage();
        var window = TestHelpers.Show(new StackPanel { Children = { image } });
        try
        {
            ArcImage.SetLoader(window, loader);
            image.Source = Unique();
            TestHelpers.PumpUntil(() => image.IsImageLoaded).ShouldBeTrue();
            loader.Calls.Count.ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Avatar_Shows_The_Photo_Or_Falls_Back_To_Initials()
    {
        var loader = new FakeLoader(manual: true);
        var avatar = new Avatar { DisplayName = "Ada Lovelace", Source = Unique() };
        ArcImage.SetLoader(avatar, loader);
        var window = TestHelpers.Show(avatar);
        try
        {
            avatar.Classes.Contains(Avatar.ShowImagePseudoClass).ShouldBeTrue();
            avatar.Classes.Contains(Avatar.ImageLoadingPseudoClass).ShouldBeTrue();
            TestHelpers.PumpUntil(() => loader.Calls.Count == 1).ShouldBeTrue();
            loader.Answer(0);
            TestHelpers.PumpUntil(() => !avatar.Classes.Contains(Avatar.ImageLoadingPseudoClass)).ShouldBeTrue();
            avatar.Classes.Contains(Avatar.ShowImagePseudoClass).ShouldBeTrue();

            loader.Fail = true;
            avatar.Source = Unique("broken");
            TestHelpers.PumpUntil(() => loader.Calls.Count == 2).ShouldBeTrue();
            loader.Answer(1);
            TestHelpers.PumpUntil(() => avatar.Classes.Contains(Avatar.FallbackPseudoClass)).ShouldBeTrue();
            avatar.Classes.Contains(Avatar.ShowImagePseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Parses_Addresses_And_File_Paths()
    {
        ImageSource.Parse("https://i.pravatar.cc/160?img=47").ShouldBeOfType<UriImageSource>().Uri.Host.ShouldBe("i.pravatar.cc");
        ImageSource.Parse("avares://TKCode.Arc.Gallery/Assets/logo.png").CacheKey.ShouldBe("avares://TKCode.Arc.Gallery/Assets/logo.png");
        ImageSource.TryParse(Path.Combine(Path.GetTempPath(), "photo.png"), out var file).ShouldBeTrue();
        ((UriImageSource)file).Uri.IsFile.ShouldBeTrue();
        ImageSource.TryParse("photo.png", out _).ShouldBeFalse();
        ImageSource.TryParse("ftp://host/photo.png", out _).ShouldBeFalse();
        ImageSource.TryParse(null, out _).ShouldBeFalse();
        Should.Throw<FormatException>(() => ImageSource.Parse("not an address"));
        ArcImageLoader.DecodeDataUri("data:image/png;base64," + Convert.ToBase64String(s_png)).ShouldBe(s_png);
    }

    [Fact]
    public void A_Picture_Is_Reduced_Only_When_Much_Larger_Than_Its_Slot()
    {
        // Cover fit of a 640×400 photo into a 32×32 slot needs 52×32; Uniform needs 32×20.
        ArcImageDecoder.TargetPixels(new PixelSize(640, 400), new Size(32, 32), Stretch.UniformToFill).ShouldBe(new PixelSize(52, 32));
        ArcImageDecoder.TargetPixels(new PixelSize(640, 400), new Size(32, 32), Stretch.Uniform).ShouldBe(new PixelSize(32, 20));
        ArcImageDecoder.TargetPixels(new PixelSize(640, 400), new Size(double.NaN, 100), Stretch.Uniform).ShouldBe(new PixelSize(160, 100));
        ArcImageDecoder.TargetPixels(new PixelSize(640, 400), new Size(1280, 800), Stretch.UniformToFill).ShouldBeNull();
        ArcImageDecoder.TargetPixels(new PixelSize(640, 400), new Size(32, 32), Stretch.None).ShouldBeNull();
        ArcImageDecoder.TargetPixels(new PixelSize(640, 400), new Size(double.NaN, double.NaN), Stretch.Fill).ShouldBeNull();
    }
}
