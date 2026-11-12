using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Editors;

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

    /* ------------------------------------------------------------------------------------------------------------
     * Type check by the file's first bytes, Accept, formats, initials, announcements, edge resistance
     * ---------------------------------------------------------------------------------------------------------- */

    private static readonly byte[] s_pngHead = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] s_jpegHead = [0xFF, 0xD8, 0xFF, 0xE0];

    private static byte[] PngBytes(int length = 128) => s_pngHead.Concat(new byte[length]).ToArray();

    private static byte[] JpegBytes(int length = 128) => s_jpegHead.Concat(new byte[length]).ToArray();

    private static byte[] PdfBytes() => "%PDF-1.7\n"u8.ToArray().Concat(new byte[64]).ToArray();

    [AvaloniaFact]
    public async Task A_File_Is_Identified_By_Its_First_Bytes_Not_Its_Name()
    {
        var decoded = 0;
        var reasons = new List<AvatarUploadRejection>();
        var (upload, window) = Show(u => u.Decoder = _ =>
        {
            decoded++;
            return Photo(600, 400);
        });
        try
        {
            upload.FileRejected += (_, e) => reasons.Add(e.Reason);

            // A PDF under a picture's name is refused before anything is decoded.
            await upload.LoadAsync(new MemoryStream(PdfBytes()), "statement.png");
            reasons.ShouldBe([AvatarUploadRejection.Type]);
            upload.ErrorMessage.ShouldBe("statement.png is not a PNG, JPG, WebP or GIF image");
            decoded.ShouldBe(0);
            upload.State.ShouldBe(AvatarUploadState.Empty);

            // A picture under an odd name is a picture.
            await upload.LoadAsync(new MemoryStream(PngBytes()), "portrait.dat");
            upload.State.ShouldBe(AvatarUploadState.Editing);
            decoded.ShouldBe(1);
            upload.ErrorMessage.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_Read_Stops_At_The_Size_Limit_Even_When_The_Stream_Cannot_Say_How_Long_It_Is()
    {
        var decoded = 0;
        var (upload, window) = Show(u =>
        {
            u.MaxFileSize = 1000;
            u.Decoder = _ =>
            {
                decoded++;
                return Photo(100, 100);
            };
        });
        try
        {
            var reasons = new List<AvatarUploadRejection>();
            upload.FileRejected += (_, e) => reasons.Add(e.Reason);
            await upload.LoadAsync(new NonSeekableStream(PngBytes(5000)), "big.png");
            reasons.ShouldBe([AvatarUploadRejection.Size]);
            decoded.ShouldBe(0);

            await upload.LoadAsync(PngBytes(5000), "big.png");
            reasons.ShouldBe([AvatarUploadRejection.Size, AvatarUploadRejection.Size]);

            await upload.LoadAsync(PngBytes(200), "small.png");
            upload.State.ShouldBe(AvatarUploadState.Editing);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_File_That_Does_Not_Decode_Is_Reported_As_Unreadable()
    {
        var (upload, window) = Show(u => u.Decoder = _ => throw new InvalidDataException("corrupt"));
        try
        {
            var reasons = new List<AvatarUploadRejection>();
            upload.FileRejected += (_, e) => reasons.Add(e.Reason);
            await upload.LoadAsync(PngBytes(), "broken.png");
            reasons.ShouldBe([AvatarUploadRejection.Unreadable]);
            upload.ErrorMessage.ShouldBe("broken.png could not be read");
            upload.State.ShouldBe(AvatarUploadState.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Accept_Narrows_The_Types_And_The_Line_Under_The_Title()
    {
        var (upload, window) = Show(u =>
        {
            u.Accept = "image/png,image/jpeg";
            u.Decoder = _ => Photo(300, 300);
        });
        try
        {
            upload.StatusText.ShouldBe("PNG or JPEG, up to 10 MB");
            await upload.LoadAsync(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0, 0, 0, 0, 0, 0, 0, 0 }, "spin.gif");
            upload.ErrorMessage.ShouldBe("spin.gif is not a PNG or JPEG image");
            upload.State.ShouldBe(AvatarUploadState.Empty);

            await upload.LoadAsync(JpegBytes(), "face.jpg");
            upload.State.ShouldBe(AvatarUploadState.Editing);
            upload.CancelEdit();

            // A description set on purpose is kept.
            upload.Description = "Square images look best";
            upload.StatusText.ShouldBe("Square images look best");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Line_Under_The_Title_Follows_The_Limit_Until_A_Description_Is_Set()
    {
        var (upload, window) = Show();
        try
        {
            upload.StatusText.ShouldBe("PNG, JPG or WebP, up to 10 MB");
            upload.MaxFileSize = 5 * 1024 * 1024;
            upload.StatusText.ShouldBe("PNG, JPG or WebP, up to 5 MB");
            upload.MaxFileSize = null;
            upload.StatusText.ShouldBe("PNG, JPG or WebP");
            upload.Description = "Square images look best";
            upload.MaxFileSize = 1024;
            upload.StatusText.ShouldBe("Square images look best");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(AvatarImageFormat.Auto, "me.png", "image/png", "me.png")]
    [InlineData(AvatarImageFormat.Auto, "me.jpg", "image/jpeg", "me.jpg")]
    [InlineData(AvatarImageFormat.Auto, null, "image/png", "avatar.png")]
    [InlineData(AvatarImageFormat.Png, "me.jpg", "image/png", "me.png")]
    [InlineData(AvatarImageFormat.Jpeg, "me.png", "image/jpeg", "me.jpg")]
    public void The_Saved_Picture_Comes_With_Its_Encoded_Bytes(AvatarImageFormat format, string? name, string contentType, string suggested)
    {
        AvatarUploadRequest? saved = null;
        var (upload, window) = Show(u => u.OutputFormat = format);
        try
        {
            upload.Cropped += (_, e) => saved = e.Request;
            upload.Edit(Photo(400, 300), name);
            _ = upload.SaveAsync();
            Dispatcher.UIThread.RunJobs();
            saved.ShouldNotBeNull();
            saved!.ContentType.ShouldBe(contentType);
            saved.SuggestedFileName.ShouldBe(suggested);
            // (The headless renderer has no image encoder, so the bytes themselves are checked with Skia elsewhere.)
            saved.Bytes.ShouldNotBeNull();
            using var stream = saved.OpenStream();
            stream.Length.ShouldBe(saved.Bytes.Length);
            stream.CanWrite.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Request_Built_By_Hand_Encodes_PNG_On_Demand()
    {
        var request = new AvatarUploadRequest(Photo(64, 64), new PixelRect(0, 0, 64, 64), "x.png");
        request.ContentType.ShouldBe("image/png");
        request.Bytes.ShouldNotBeNull();
        request.Bytes.ShouldBeSameAs(request.Bytes);
        request.SuggestedFileName.ShouldBe("x.png");
    }

    [AvaloniaFact]
    public void The_Name_Gives_Initials_When_There_Is_No_Picture()
    {
        var (upload, window) = Show(u => u.DisplayName = "Amara Venn");
        try
        {
            upload.Initials.ShouldBe("AV");
            TestHelpers.Descendant<AvatarUploadStage>(upload).Initials.ShouldBe("AV");
            upload.DisplayName = "Cher";
            upload.Initials.ShouldBe("C");
            upload.DisplayName = null;
            upload.Initials.ShouldBe(string.Empty);
            TestHelpers.Descendant<AvatarUploadStage>(upload).Initials.ShouldBeNullOrEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AllowRemove_Hides_The_Button_But_Not_The_Method()
    {
        var removed = 0;
        var (upload, window) = Show(u =>
        {
            u.Source = Photo(200, 200);
            u.AllowRemove = false;
        });
        try
        {
            upload.Removed += (_, _) => removed++;
            var remove = TestHelpers.Descendant<Button>(upload, AvatarUpload.RemovePart);
            upload.State.ShouldBe(AvatarUploadState.Done);
            remove.IsVisible.ShouldBeFalse();
            upload.Classes.Contains(":no-remove").ShouldBeTrue();

            upload.AllowRemove = true;
            Dispatcher.UIThread.RunJobs();
            remove.IsVisible.ShouldBeTrue();

            upload.AllowRemove = false;
            upload.Remove();
            removed.ShouldBe(1);
            upload.Source.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Results_And_Progress_In_Quarters_Are_Announced()
    {
        var gate = new TaskCompletionSource();
        IProgress<double>? progress = null;
        var (upload, window) = Show(u => u.UploadHandler = (_, p, _) =>
        {
            progress = p;
            return gate.Task;
        });
        try
        {
            upload.Edit(Photo(400, 300), "me.png");
            _ = upload.SaveAsync();
            Dispatcher.UIThread.RunJobs();
            upload.State.ShouldBe(AvatarUploadState.Uploading);
            var announcer = upload.Announcer!;

            progress!.Report(0.1);
            announcer.Message.ShouldBe(string.Empty);
            progress.Report(0.3);
            announcer.Message.ShouldBe("Uploading 25%");
            progress.Report(0.35);
            announcer.Message.ShouldBe("Uploading 25%");
            progress.Report(0.8);
            announcer.Message.ShouldBe("Uploading 75%");

            gate.SetResult();
            TestHelpers.PumpUntil(() => upload.State == AvatarUploadState.Done).ShouldBeTrue();
            announcer.Message.ShouldBe("Photo updated");
            await Task.Yield();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Refusals_And_Failures_Are_Announced_With_Their_Reason()
    {
        var (upload, window) = Show(u =>
        {
            u.MaxFileSize = 100;
            u.UploadHandler = (_, _, _) => throw new InvalidOperationException("The server is full.");
        });
        try
        {
            await upload.LoadAsync(PngBytes(500), "big.png");
            upload.Announcer!.Message.ShouldBe("big.png is larger than 100 B");

            upload.Edit(Photo(400, 300), "me.png");
            _ = upload.SaveAsync();
            TestHelpers.PumpUntil(() => upload.State == AvatarUploadState.Failed).ShouldBeTrue();
            upload.Announcer.Message.ShouldBe("The server is full.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Drag_Past_An_Edge_Resists_And_Springs_Back_Without_Changing_The_Crop()
    {
        var (upload, window) = Show();
        try
        {
            upload.Edit(Photo(800, 400), "wide.png");
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var cropper = upload.Cropper!;
            var start = cropper.CropRect();
            var center = cropper.TranslatePoint(cropper.Circle.Center, window)!.Value;

            // The picture is wider than the circle: a short drag moves it, with no stretch.
            window.MouseDown(center, MouseButton.Left);
            window.MouseMove(center + new Vector(10, 0), RawInputModifiers.LeftMouseButton);
            cropper.Stretch.ShouldBe(default);
            cropper.CropRect().X.ShouldBeLessThan(start.X);

            // Pulled far past the edge it stretches, but never by as much as the finger moved, nor by a circle's width.
            var far = 900.0;
            window.MouseMove(center + new Vector(far, 0), RawInputModifiers.LeftMouseButton);
            cropper.Stretch.X.ShouldBeGreaterThan(0);
            cropper.Stretch.X.ShouldBeLessThan(far);
            cropper.Stretch.X.ShouldBeLessThan(cropper.Circle.Width);
            cropper.Stretch.Y.ShouldBe(0);
            // The saved square is the clamped one, whatever is shown.
            cropper.CropRect().X.ShouldBe(0);

            window.MouseUp(center + new Vector(far, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            cropper.Stretch.ShouldBe(default);
            cropper.CropRect().X.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class NonSeekableStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
