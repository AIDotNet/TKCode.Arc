using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Special.Inputs;

/// <summary>
/// The avatar field: the framing math (covering, centering, clamping, zoom limits), the keyboard of the framing area,
/// saving with and without an upload handler, progress, failure, retry and cancel, app-driven uploads, removal and the
/// refused files.
/// </summary>
public class AvatarUploadTests
{
    private static Bitmap Photo(int width, int height) =>
        new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

    private static (AvatarUpload Upload, Window Window) Show(Action<AvatarUpload>? configure = null)
    {
        var upload = new AvatarUpload();
        configure?.Invoke(upload);
        var window = TestHelpers.Show(upload);
        window.Height = 480;
        Dispatcher.UIThread.RunJobs();
        return (upload, window);
    }

    [AvaloniaFact]
    public void Empty_Field_Invites_An_Upload()
    {
        var (upload, window) = Show();
        try
        {
            upload.State.ShouldBe(AvatarUploadState.Empty);
            upload.Classes.Contains(":empty").ShouldBeTrue();
            upload.StatusText.ShouldBe("PNG, JPG or WebP, up to 10 MB");
            TestHelpers.Descendant<Button>(upload, AvatarUpload.PickPart).Content.ShouldBe("Upload photo");
            TestHelpers.Descendant<Button>(upload, AvatarUpload.PickPart).IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<Button>(upload, AvatarUpload.RemovePart).IsVisible.ShouldBeFalse();
            upload.Size.ShouldBe(96);
            upload.OutputSize.ShouldBe(512);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Framing_Covers_The_Circle_Centered_And_Stays_Inside_The_Picture()
    {
        var (upload, window) = Show();
        try
        {
            upload.Edit(Photo(400, 200), "wide.png");
            Dispatcher.UIThread.RunJobs();
            upload.State.ShouldBe(AvatarUploadState.Editing);
            var cropper = upload.Cropper!;
            cropper.Image.ShouldNotBeNull();
            // The short side covers the circle; the square sits in the middle of the long side.
            cropper.CropRect().ShouldBe(new PixelRect(100, 0, 200, 200));

            cropper.ZoomTo(2);
            cropper.CropRect().ShouldBe(new PixelRect(150, 50, 100, 100));
            cropper.RelativeZoom.ShouldBe(2);

            // Dragging far right shows the left edge of the picture, never past it.
            cropper.PanBy(new Vector(5000, 5000));
            cropper.CropRect().ShouldBe(new PixelRect(0, 0, 100, 100));

            // The zoom stays between covering and MaxZoom.
            cropper.ZoomTo(10);
            cropper.RelativeZoom.ShouldBe(4);
            cropper.ZoomTo(0.2);
            cropper.RelativeZoom.ShouldBe(1);
            cropper.CropRect().Width.ShouldBe(200);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Framing_Area_Keys_Move_Zoom_Save_And_Cancel()
    {
        var (upload, window) = Show();
        try
        {
            upload.Edit(Photo(300, 300));
            Dispatcher.UIThread.RunJobs();
            var cropper = upload.Cropper!;
            cropper.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Equal, RawInputModifiers.Shift);
            Dispatcher.UIThread.RunJobs();
            // Motion is off in tests: the zoom step lands at once.
            cropper.RelativeZoom.ShouldBe(1 + AvatarUploadCropper.ZoomStep);
            var before = cropper.CropRect();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            cropper.CropRect().X.ShouldBeGreaterThan(before.X);
            TestHelpers.Descendant<Slider>(upload, AvatarUpload.ZoomPart).Value.ShouldBe(cropper.RelativeZoom);

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            upload.State.ShouldBe(AvatarUploadState.Empty);
            upload.Source.ShouldBeNull();

            upload.Edit(Photo(300, 300));
            Dispatcher.UIThread.RunJobs();
            upload.Cropper!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            upload.State.ShouldBe(AvatarUploadState.Done);
            upload.Source.ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Save_Without_A_Handler_Applies_The_Square_At_Once()
    {
        var (upload, window) = Show(u => u.OutputSize = 128);
        try
        {
            AvatarUploadRequest? cropped = null;
            upload.Cropped += (_, e) => cropped = e.Request;
            upload.Edit(Photo(400, 200), "wide.png");
            Dispatcher.UIThread.RunJobs();
            upload.SaveAsync().IsCompleted.ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            cropped.ShouldNotBeNull();
            cropped.FileName.ShouldBe("wide.png");
            cropped.SourceRect.ShouldBe(new PixelRect(100, 0, 200, 200));
            cropped.Image.PixelSize.ShouldBe(new PixelSize(128, 128));
            upload.Source.ShouldBe(cropped.Image);
            upload.State.ShouldBe(AvatarUploadState.Done);
            TestHelpers.Descendant<Button>(upload, AvatarUpload.PickPart).Content.ShouldBe("Change");
            TestHelpers.Descendant<Button>(upload, AvatarUpload.RemovePart).IsVisible.ShouldBeTrue();

            var removed = 0;
            upload.Removed += (_, _) => removed++;
            upload.Remove();
            upload.State.ShouldBe(AvatarUploadState.Empty);
            upload.Source.ShouldBeNull();
            removed.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Upload_Reports_Progress_Fails_Retries_And_Completes()
    {
        var gates = new Queue<TaskCompletionSource>();
        IProgress<double>? reporter = null;
        var (upload, window) = Show(u => u.UploadHandler = async (_, progress, _) =>
        {
            reporter = progress;
            var gate = new TaskCompletionSource();
            gates.Enqueue(gate);
            await gate.Task;
        });
        try
        {
            var previous = Photo(64, 64);
            upload.Source = previous;
            upload.State.ShouldBe(AvatarUploadState.Done);
            upload.Edit(Photo(300, 300));
            Dispatcher.UIThread.RunJobs();
            var saving = upload.SaveAsync();
            Dispatcher.UIThread.RunJobs();
            upload.State.ShouldBe(AvatarUploadState.Uploading);
            upload.Source.ShouldNotBe(previous);
            reporter!.Report(0.42);
            Dispatcher.UIThread.RunJobs();
            upload.Progress.ShouldBe(0.42);
            upload.StatusText.ShouldBe("Uploading 42%");
            TestHelpers.Descendant<ArcRing>(upload, AvatarUpload.RingPart).Value.ShouldBe(0.42);

            gates.Dequeue().SetException(new InvalidOperationException("The connection dropped."));
            await saving;
            upload.State.ShouldBe(AvatarUploadState.Failed);
            upload.ErrorMessage.ShouldBe("The connection dropped.");
            upload.Classes.Contains(":error").ShouldBeTrue();
            TestHelpers.Descendant<Button>(upload, AvatarUpload.RetryPart).IsVisible.ShouldBeTrue();

            var retry = upload.RetryAsync();
            Dispatcher.UIThread.RunJobs();
            upload.State.ShouldBe(AvatarUploadState.Uploading);
            gates.Dequeue().SetResult();
            await retry;
            upload.State.ShouldBe(AvatarUploadState.Done);
            upload.ErrorMessage.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Cancel_Upload_Goes_Back_To_The_Picture_Before()
    {
        var (upload, window) = Show(u => u.UploadHandler = (_, _, cancel) => Task.Delay(Timeout.Infinite, cancel));
        try
        {
            var previous = Photo(64, 64);
            upload.Source = previous;
            upload.Edit(Photo(300, 300));
            Dispatcher.UIThread.RunJobs();
            _ = upload.SaveAsync();
            Dispatcher.UIThread.RunJobs();
            upload.State.ShouldBe(AvatarUploadState.Uploading);
            upload.CancelUpload();
            Dispatcher.UIThread.RunJobs();
            upload.State.ShouldBe(AvatarUploadState.Done);
            upload.Source.ShouldBe(previous);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void App_Driven_Upload_Uses_The_Same_States()
    {
        var (upload, window) = Show();
        try
        {
            upload.Source = Photo(64, 64);
            upload.ReportProgress(0.4);
            upload.State.ShouldBe(AvatarUploadState.Uploading);
            upload.Progress.ShouldBe(0.4);
            upload.FailUpload("The server refused the file.");
            upload.State.ShouldBe(AvatarUploadState.Failed);
            upload.StatusText.ShouldBe("The server refused the file.");
            upload.ReportProgress(0.9);
            upload.CompleteUpload();
            upload.State.ShouldBe(AvatarUploadState.Done);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Refused_Files_Say_Why()
    {
        var (upload, window) = Show(u => u.MaxFileSize = 1000);
        try
        {
            var reasons = new List<AvatarUploadRejection>();
            upload.FileRejected += (_, e) => reasons.Add(e.Reason);
            // (Unreadable bytes depend on the platform's image decoder, which the headless renderer does not have.)
            await upload.LoadAsync(new MemoryStream(new byte[5000]), "big.png");
            upload.ErrorMessage.ShouldBe("big.png is larger than 1000 B");
            upload.StatusText.ShouldBe("big.png is larger than 1000 B");
            upload.Classes.Contains(":error").ShouldBeTrue();
            reasons.ShouldBe([AvatarUploadRejection.Size]);
            upload.State.ShouldBe(AvatarUploadState.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Accepted_Names_And_Sizes()
    {
        AvatarUpload.IsAcceptedName("me.PNG").ShouldBeTrue();
        AvatarUpload.IsAcceptedName("me.webp").ShouldBeTrue();
        AvatarUpload.IsAcceptedName("me.svg").ShouldBeFalse();
        AvatarUpload.IsAcceptedName("notes.txt").ShouldBeFalse();
        AvatarUpload.FormatSize(10L * 1024 * 1024).ShouldBe("10 MB");
        AvatarUpload.FormatSize(1536L * 1024).ShouldBe("1.5 MB");
        AvatarUpload.FormatSize(2048).ShouldBe("2 KB");
    }
}
