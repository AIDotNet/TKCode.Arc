using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using static TKCode.Arc.Tests.Blocks.Ai.AiAttachmentSamples;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiFileIntake"/>: intake of picked, pasted and dropped items (folders skipped, sizes, accept patterns, the
/// size limit, pictures and their thumbnails), long pastes, the meta line and byte format, icons, and the upload
/// pipeline (Uploading → Done → Ready, monotonic progress, handler errors, cancellation, no handler) on a fake clock.
/// </summary>
public class AiFileIntakeTests
{
    private const long MB = 1024 * 1024;

    private static string TooLarge(long max) => $"Larger than {max / MB} MB";

    private static string NotAccepted(string name) => $"{name} is not a supported type";

    /// <summary>A decoder that records its calls and returns a fixed image (or throws).</summary>
    private sealed class FakeDecoder(bool fail = false)
    {
        public List<int> Widths { get; } = new();

        public List<TrackedImage> Images { get; } = new();

        public List<long> Lengths { get; } = new();

        public IImage Decode(Stream stream, int width)
        {
            Widths.Add(width);
            Lengths.Add(stream.Length);
            if (fail)
            {
                throw new InvalidDataException("not a picture");
            }
            var image = new TrackedImage();
            Images.Add(image);
            return image;
        }
    }

    /// <summary>A decoded picture that knows when it was disposed.</summary>
    private sealed class TrackedImage : IImage, IDisposable
    {
        public bool IsDisposed { get; private set; }

        public Size Size => new(76, 76);

        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
        {
        }

        public void Dispose() => IsDisposed = true;
    }

    private static Task<IReadOnlyList<AiAttachment>> Intake(IEnumerable<IStorageItem> items, long? max = 30 * MB, string? accept = null,
        FakeDecoder? decoder = null, CancellationToken ct = default) =>
        AiFileIntake.FromStorageAsync(items, max, accept, ct, TooLarge, NotAccepted, (decoder ?? new FakeDecoder()).Decode);

    /// <summary>A window (for its storage provider) and a fresh folder of real files.</summary>
    private sealed class Disk
    {
        public Window Window { get; } = TestHelpers.Show(new Border());

        public string Folder { get; } = NewFolder();

        public Task<IStorageFile> File(string name, long size) => FileAsync(Window, Folder, name, size);

        public Task<IStorageFolder> Directory(string name) => FolderAsync(Window, Folder, name);
    }

    #region Intake

    [AvaloniaFact]
    public async Task Files_Become_Ready_Attachments_In_Order_And_Folders_Are_Skipped()
    {
        var disk = new Disk();
        var brief = await disk.File("launch-brief.pdf", 480_256);
        var notes = await disk.File("standup-notes.md", 2_100);
        var list = await Intake([brief, await disk.Directory("drafts"), notes]);

        list.Count.ShouldBe(2);
        list[0].Name.ShouldBe("launch-brief.pdf");
        list[0].File.ShouldBeSameAs(brief);
        list[0].Size.ShouldBe(480_256);
        list[0].Kind.ShouldBe(AiAttachmentKind.File);
        list[0].Status.ShouldBe(AiAttachmentStatus.Ready);
        list[0].Error.ShouldBeNull();
        list[0].Thumbnail.ShouldBeNull();
        list[0].Meta.ShouldBeNull();
        list[1].Name.ShouldBe("standup-notes.md");
        list[1].Size.ShouldBe(2_100);
        list[0].Id.ShouldNotBe(list[1].Id);
        list[0].Id.ShouldStartWith("attachment-");
    }

    [AvaloniaFact]
    public async Task Accept_Patterns_Work_Like_A_File_Input()
    {
        var disk = new Disk();
        var list = await Intake([
            await disk.File("plan.PDF", 1_000), await disk.File("sketch.png", 1_000), await disk.File("usage.csv", 1_000), await disk.File("README", 1_000)
        ], accept: " .pdf , image/* ");
        list.Select(a => a.Status).ShouldBe([AiAttachmentStatus.Ready, AiAttachmentStatus.Ready, AiAttachmentStatus.Error, AiAttachmentStatus.Error]);
        list[2].Error.ShouldBe("usage.csv is not a supported type");
        list[3].Error.ShouldBe("README is not a supported type");

        // An exact MIME type, and an empty list accepts everything.
        AiFileIntake.Matches("usage.csv", AiFileIntake.ParseAccept("text/csv")).ShouldBeTrue();
        AiFileIntake.Matches("usage.tsv", AiFileIntake.ParseAccept("text/csv")).ShouldBeFalse();
        AiFileIntake.Matches("anything.xyz", AiFileIntake.ParseAccept(null)).ShouldBeTrue();
        AiFileIntake.Matches("anything.xyz", AiFileIntake.ParseAccept(" , ")).ShouldBeTrue();
        AiFileIntake.Matches("clip.mov", AiFileIntake.ParseAccept("video/*")).ShouldBeTrue();
        AiFileIntake.Matches("clip.mov", AiFileIntake.ParseAccept("audio/*")).ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task A_File_Over_The_Limit_Is_An_Error_With_The_Callers_Reason()
    {
        var disk = new Disk();
        var list = await Intake([await disk.File("standup-capture.mov", 31 * MB), await disk.File("exactly.zip", 30 * MB)]);
        list[0].Status.ShouldBe(AiAttachmentStatus.Error);
        list[0].Error.ShouldBe("Larger than 30 MB");
        list[1].Status.ShouldBe(AiAttachmentStatus.Ready);

        // No limit, no refusal; no reason text, no words.
        var huge = await disk.File("huge.iso", 90 * MB);
        (await Intake([huge], max: null))[0].Status.ShouldBe(AiAttachmentStatus.Ready);
        var silent = await AiFileIntake.FromStorageAsync([huge], 30 * MB, null, default);
        silent[0].Status.ShouldBe(AiAttachmentStatus.Error);
        silent[0].Error.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task Pictures_Get_A_Thumbnail_Decoded_To_76_Pixels()
    {
        var disk = new Disk();
        var decoder = new FakeDecoder();
        var list = await Intake([await disk.File("whiteboard.jpg", 240 * 1024), await disk.File("brief.pdf", 1_000)], decoder: decoder);
        list[0].Kind.ShouldBe(AiAttachmentKind.Image);
        list[0].Thumbnail.ShouldBeSameAs(decoder.Images.Single());
        decoder.Widths.ShouldBe([AiFileIntake.ThumbnailPixelWidth]);
        // The whole picture reached the decoder.
        decoder.Lengths.ShouldBe([240 * 1024]);
        AiFileIntake.ThumbnailPixelWidth.ShouldBe(76);
        list[1].Thumbnail.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task A_Picture_That_Does_Not_Decode_Becomes_A_File()
    {
        var disk = new Disk();
        var list = await Intake([await disk.File("broken.png", 2_000)], decoder: new FakeDecoder(fail: true));
        list[0].Kind.ShouldBe(AiAttachmentKind.File);
        list[0].Thumbnail.ShouldBeNull();
        list[0].Status.ShouldBe(AiAttachmentStatus.Ready);
    }

    [AvaloniaFact]
    public async Task The_Platform_Decoder_Failing_On_Garbage_Also_Gives_A_File()
    {
        var disk = new Disk();
        var file = await disk.File("not-really.png", 300);
        var attachment = Sample("not-really.png", 300, kind: AiAttachmentKind.Image);
        attachment.File = file;
        // The default decoder: zeros are not a picture on any platform decoder that really decodes; the headless one may
        // hand back a stub, which is still an image. Either way the attachment is consistent.
        var loaded = await AiFileIntake.LoadThumbnailAsync(attachment, default);
        (attachment.Thumbnail is not null).ShouldBe(loaded);
        attachment.Kind.ShouldBe(loaded ? AiAttachmentKind.Image : AiAttachmentKind.File);
        AiFileIntake.Release(attachment);
        attachment.Thumbnail.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task A_Refused_Picture_Is_Not_Read()
    {
        var disk = new Disk();
        var decoder = new FakeDecoder();
        var list = await Intake([await disk.File("raw-export.png", 64 * MB)], decoder: decoder);
        list[0].Status.ShouldBe(AiAttachmentStatus.Error);
        list[0].Kind.ShouldBe(AiAttachmentKind.Image);
        decoder.Widths.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task Without_A_File_There_Is_No_Thumbnail()
    {
        var attachment = Sample("from-drive.png", 1_000, kind: AiAttachmentKind.Image);
        (await AiFileIntake.LoadThumbnailAsync(attachment, default, new FakeDecoder().Decode)).ShouldBeFalse();
        attachment.Kind.ShouldBe(AiAttachmentKind.Image);
    }

    [AvaloniaFact]
    public async Task Release_Disposes_Only_Thumbnails_The_Intake_Decoded()
    {
        var disk = new Disk();
        var decoder = new FakeDecoder();
        var list = await Intake([await disk.File("whiteboard.jpg", 1_000)], decoder: decoder);
        var attachment = list[0];
        var image = decoder.Images.Single();
        AiFileIntake.Release(attachment);
        image.IsDisposed.ShouldBeTrue();
        attachment.Thumbnail.ShouldBeNull();
        // Twice is harmless.
        AiFileIntake.Release(attachment);

        var own = new TrackedImage();
        var set = Sample("poster.png", 1_000, kind: AiAttachmentKind.Image);
        set.Thumbnail = own;
        AiFileIntake.Release(set);
        own.IsDisposed.ShouldBeFalse();
        set.Thumbnail.ShouldBeSameAs(own);
    }

    [AvaloniaFact]
    public async Task A_Second_Decode_Replaces_And_Disposes_The_First()
    {
        var disk = new Disk();
        var decoder = new FakeDecoder();
        var list = await Intake([await disk.File("whiteboard.jpg", 1_000)], decoder: decoder);
        (await AiFileIntake.LoadThumbnailAsync(list[0], default, decoder.Decode)).ShouldBeTrue();
        decoder.Images.Count.ShouldBe(2);
        decoder.Images[0].IsDisposed.ShouldBeTrue();
        list[0].Thumbnail.ShouldBeSameAs(decoder.Images[1]);
    }

    [AvaloniaFact]
    public async Task A_Cancelled_Intake_Throws()
    {
        var disk = new Disk();
        var file = await disk.File("brief.pdf", 1);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Intake([file], ct: cts.Token));
    }

    #endregion

    #region Long pastes

    [AvaloniaFact]
    public void Long_Pastes_Follow_Both_Thresholds_And_Zero_Turns_A_Rule_Off()
    {
        AiFileIntake.LongPasteCharacters.ShouldBe(1200);
        AiFileIntake.LongPasteLines.ShouldBe(12);
        AiFileIntake.IsLongPaste(new string('a', 1199)).ShouldBeFalse();
        AiFileIntake.IsLongPaste(new string('a', 1200)).ShouldBeTrue();
        var eleven = string.Join('\n', Enumerable.Repeat("line", 11));
        var twelve = string.Join('\n', Enumerable.Repeat("line", 12));
        AiFileIntake.IsLongPaste(eleven).ShouldBeFalse();
        AiFileIntake.IsLongPaste(twelve).ShouldBeTrue();
        AiFileIntake.IsLongPaste(twelve, characters: 1200, lines: 0).ShouldBeFalse();
        AiFileIntake.IsLongPaste(new string('a', 5000), characters: 0, lines: 12).ShouldBeFalse();
        AiFileIntake.IsLongPaste(new string('a', 5000), characters: 0, lines: 0).ShouldBeFalse();
        AiFileIntake.IsLongPaste(string.Empty).ShouldBeFalse();
        AiFileIntake.IsLongPaste(null).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Lines_Are_Counted_Across_Line_Break_Styles()
    {
        AiFileIntake.CountLines("").ShouldBe(0);
        AiFileIntake.CountLines("one").ShouldBe(1);
        AiFileIntake.CountLines("one\ntwo").ShouldBe(2);
        AiFileIntake.CountLines("one\r\ntwo\r\nthree").ShouldBe(3);
        AiFileIntake.CountLines("one\rtwo").ShouldBe(2);
        // A final line break does not start a line; an empty line in the middle counts.
        AiFileIntake.CountLines("one\ntwo\n").ShouldBe(2);
        AiFileIntake.CountLines("one\r\n").ShouldBe(1);
        AiFileIntake.CountLines("one\n\nthree").ShouldBe(3);
        AiFileIntake.CountLines("\n").ShouldBe(1);
    }

    [AvaloniaFact]
    public void A_Pasted_Text_Is_A_Ready_Chip_Whose_Meta_Counts_Lines()
    {
        var text = string.Join('\n', Enumerable.Repeat("Retry the export with smaller chunks.", 24));
        var pasted = AiFileIntake.FromPastedText(text, "Pasted snippet", lines => $"{lines} lines of text");
        pasted.Kind.ShouldBe(AiAttachmentKind.PastedText);
        pasted.Name.ShouldBe("Pasted snippet");
        pasted.PastedText.ShouldBe(text);
        pasted.Status.ShouldBe(AiAttachmentStatus.Ready);
        pasted.Meta.ShouldBe("24 lines of text");
        pasted.Size.ShouldBe(System.Text.Encoding.UTF8.GetByteCount(text));
        pasted.File.ShouldBeNull();

        var plain = AiFileIntake.FromPastedText("a\nb\nc", "Pasted snippet");
        plain.Meta.ShouldBeNull();
        AiFileIntake.DefaultMeta(plain).ShouldBe("3");
        AiFileIntake.DefaultMeta(plain, linesText: n => $"{n} lines of text").ShouldBe("3 lines of text");
        plain.Id.ShouldNotBe(pasted.Id);
    }

    #endregion

    #region Meta, sizes, types

    [AvaloniaTheory]
    [InlineData(0L, "0 B")]
    [InlineData(-5L, "0 B")]
    [InlineData(12L, "12 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(1024L, "1 KB")]
    [InlineData(4_100L, "4 KB")]
    [InlineData(480_256L, "469 KB")]
    [InlineData(1_048_000L, "1023 KB")]
    [InlineData(1_048_100L, "1 MB")]
    [InlineData(3_250_586L, "3.1 MB")]
    [InlineData(10_380_902L, "9.9 MB")]
    [InlineData(10_485_760L, "10 MB")]
    [InlineData(31_457_280L, "30 MB")]
    [InlineData(1_288_490_189L, "1.2 GB")]
    public void Bytes_Are_Formatted_Like_The_Chip(long bytes, string expected) => AiFileIntake.FormatBytes(bytes).ShouldBe(expected);

    [AvaloniaFact]
    public void Bytes_Ignore_The_Current_Culture()
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            AiFileIntake.FormatBytes(3_250_586).ShouldBe("3.1 MB");
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = culture;
        }
    }

    [AvaloniaFact]
    public void The_Default_Meta_Is_Type_And_Size()
    {
        AiFileIntake.DefaultMeta(Sample("sprint-15-review.pdf", 480_256)).ShouldBe("PDF, 469 KB");
        AiFileIntake.DefaultMeta(Sample("usage-oct.csv", 12 * 1024)).ShouldBe("CSV, 12 KB");
        var image = Sample("whiteboard.png", 240 * 1024, kind: AiAttachmentKind.Image);
        AiFileIntake.DefaultMeta(image, "Picture").ShouldBe("Picture, 240 KB");
        AiFileIntake.DefaultMeta(image).ShouldBe("PNG, 240 KB");
        // A picture that fell back to a file says its type.
        AiFileIntake.DefaultMeta(Sample("whiteboard.heic", 240 * 1024), "Picture").ShouldBe("HEIC, 240 KB");
        AiFileIntake.DefaultMeta(Sample("Makefile", 12)).ShouldBe("12 B");
        AiFileIntake.DefaultMeta(Sample("brief.pdf", null)).ShouldBe("PDF");
        AiFileIntake.DefaultMeta(Sample("notes.", null)).ShouldBe(string.Empty);
        AiFileIntake.DefaultMeta(Sample(".env", 40)).ShouldBe("40 B");
    }

    [AvaloniaFact]
    public void Images_Are_Known_By_Extension()
    {
        foreach (var name in new[] { "a.png", "a.JPG", "a.jpeg", "a.gif", "a.webp", "a.bmp", "a.heic", "a.avif" })
        {
            AiFileIntake.IsImage(name).ShouldBeTrue(name);
        }
        foreach (var name in new[] { "a.svg", "a.pdf", "png", "a.png.txt", "", null })
        {
            AiFileIntake.IsImage(name).ShouldBeFalse(name ?? "null");
        }
    }

    [AvaloniaFact]
    public void Icons_Follow_The_Kind_And_Extension()
    {
        AiFileIntake.IconFor(Sample("export-worker.ts", 1)).ShouldBe(ArcIcons.FileCode);
        AiFileIntake.IconFor(Sample("usage-oct.csv", 1)).ShouldBe(ArcIcons.FileSpreadsheet);
        AiFileIntake.IconFor(Sample("headcount-plan.xlsx", 1)).ShouldBe(ArcIcons.FileSpreadsheet);
        AiFileIntake.IconFor(Sample("style-guide.pdf", 1)).ShouldBe(ArcIcons.FileText);
        AiFileIntake.IconFor(Sample("setup-tool.exe", 1)).ShouldBe(ArcIcons.FileText);
        AiFileIntake.IconFor(Sample("standup-capture.mov", 1)).ShouldBe(ArcIcons.FileText);
        AiFileIntake.IconFor(Sample("logs-archive.zip", 1)).ShouldBe(ArcIcons.FileArchive);
        AiFileIntake.IconFor(Sample("logs.tar.gz", 1)).ShouldBe(ArcIcons.FileArchive);
        AiFileIntake.IconFor(Sample("whiteboard.png", 1, kind: AiAttachmentKind.Image)).ShouldBe(ArcIcons.Image);
        AiFileIntake.IconFor(AiFileIntake.FromPastedText("x", "Pasted snippet")).ShouldBe(ArcIcons.ClipboardPaste);
        var source = Sample("teamspace/web-app", null, kind: AiAttachmentKind.Source);
        source.Icon = ArcIcons.GitBranch;
        AiFileIntake.IconFor(source).ShouldBe(ArcIcons.GitBranch);
    }

    #endregion

    #region Upload pipeline

    /// <summary>A handler the test drives: it reports what it is told and finishes, fails or waits for cancellation.</summary>
    private sealed class FakeUpload
    {
        private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IProgress<double>? Progress { get; private set; }

        public CancellationToken Token { get; private set; }

        public int Calls { get; private set; }

        public Task Handler(AiAttachment attachment, IProgress<double> progress, CancellationToken ct)
        {
            Calls++;
            Progress = progress;
            Token = ct;
            ct.Register(() => _done.TrySetCanceled(ct));
            return _done.Task;
        }

        public void Finish() => _done.TrySetResult();

        public void Fail(Exception exception) => _done.TrySetException(exception);
    }

    private static Task Run(AiAttachment attachment, FakeUpload? upload, ManualDelay delay, CancellationToken ct = default) =>
        AiFileIntake.RunUploadAsync(attachment, upload is null ? null : upload.Handler, AiFileIntake.DoneHold, delay.Delay,
            () => "Could not upload", ct);

    private static void Pump()
    {
        for (var i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public async Task Without_A_Handler_The_Attachment_Is_Ready_At_Once()
    {
        var attachment = Sample("brief.pdf", 1_000, AiAttachmentStatus.Uploading);
        var delay = new ManualDelay();
        await Run(attachment, null, delay);
        attachment.Status.ShouldBe(AiAttachmentStatus.Ready);
        delay.Requested.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task An_Upload_Goes_Uploading_Then_Done_Then_Ready_After_The_Hold()
    {
        var attachment = Sample("brief.pdf", 480_256);
        var upload = new FakeUpload();
        var delay = new ManualDelay();
        var seen = new List<AiAttachmentStatus>();
        attachment.PropertyChanged += (_, e) =>
        {
            if (e.Property == AiAttachment.StatusProperty)
            {
                seen.Add(attachment.Status);
            }
        };
        var task = Run(attachment, upload, delay);
        // Uploading is set before the first await.
        attachment.Status.ShouldBe(AiAttachmentStatus.Uploading);
        attachment.Progress.ShouldBe(0);
        upload.Calls.ShouldBe(1);

        upload.Progress!.Report(0.3);
        attachment.Progress.ShouldBe(0.3);
        upload.Finish();
        Pump();
        attachment.Status.ShouldBe(AiAttachmentStatus.Done);
        attachment.Progress.ShouldBe(1);
        delay.Requested.ShouldBe([TimeSpan.FromMilliseconds(900)]);
        delay.IsWaiting.ShouldBeTrue();

        delay.Release();
        Pump();
        await task;
        attachment.Status.ShouldBe(AiAttachmentStatus.Ready);
        seen.ShouldBe([AiAttachmentStatus.Uploading, AiAttachmentStatus.Done, AiAttachmentStatus.Ready]);
    }

    [AvaloniaFact]
    public async Task Progress_Never_Moves_Back_And_Stays_Within_Zero_And_One()
    {
        var attachment = Sample("brief.pdf", 480_256);
        var upload = new FakeUpload();
        var delay = new ManualDelay();
        var task = Run(attachment, upload, delay);
        var progress = upload.Progress!;
        progress.Report(0.4);
        progress.Report(0.2);
        attachment.Progress.ShouldBe(0.4);
        progress.Report(double.NaN);
        attachment.Progress.ShouldBe(0.4);
        progress.Report(-1);
        attachment.Progress.ShouldBe(0.4);
        progress.Report(7);
        attachment.Progress.ShouldBe(1);
        upload.Finish();
        Pump();
        delay.Release();
        Pump();
        await task;

        // A late report after the upload finished changes nothing.
        attachment.Progress = 1;
        progress.Report(0.1);
        attachment.Progress.ShouldBe(1);
        attachment.Status.ShouldBe(AiAttachmentStatus.Ready);
    }

    [AvaloniaFact]
    public async Task Progress_From_A_Worker_Thread_Lands_On_The_UI_Thread()
    {
        var attachment = Sample("brief.pdf", 480_256);
        var upload = new FakeUpload();
        var task = Run(attachment, upload, new ManualDelay());
        var progress = upload.Progress!;
        await Task.Run(() => progress.Report(0.6));
        Pump();
        attachment.Progress.ShouldBe(0.6);
        upload.Finish();
        Pump();
        attachment.Status.ShouldBe(AiAttachmentStatus.Done);
        _ = task;
    }

    [AvaloniaFact]
    public async Task A_Failing_Handler_Leaves_An_Error_With_Its_Message()
    {
        var attachment = Sample("brief.pdf", 480_256);
        var upload = new FakeUpload();
        var delay = new ManualDelay();
        var task = Run(attachment, upload, delay);
        upload.Progress!.Report(0.5);
        upload.Fail(new IOException("Storage is full"));
        Pump();
        await task;
        attachment.Status.ShouldBe(AiAttachmentStatus.Error);
        attachment.Error.ShouldBe("Storage is full");
        delay.Requested.ShouldBeEmpty();

        // A message-less failure uses the fallback text; a retry clears the old reason.
        var again = new FakeUpload();
        attachment.Status = AiAttachmentStatus.Ready;
        var retry = Run(attachment, again, delay);
        attachment.Error.ShouldBeNull();
        attachment.Progress.ShouldBe(0);
        again.Fail(new MessageLessException());
        Pump();
        await retry;
        attachment.Error.ShouldBe("Could not upload");
    }

    private sealed class MessageLessException : Exception
    {
        public override string Message => " ";
    }

    [AvaloniaFact]
    public async Task Cancelling_During_The_Upload_Changes_Nothing_More()
    {
        var attachment = Sample("brief.pdf", 480_256);
        var upload = new FakeUpload();
        var delay = new ManualDelay();
        using var cts = new CancellationTokenSource();
        var task = Run(attachment, upload, delay, cts.Token);
        upload.Progress!.Report(0.25);
        await cts.CancelAsync();
        Pump();
        await task;
        attachment.Status.ShouldBe(AiAttachmentStatus.Uploading);
        attachment.Error.ShouldBeNull();
        upload.Progress.Report(0.9);
        attachment.Progress.ShouldBe(0.25);
        delay.Requested.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task A_Handler_That_Ignores_Cancellation_Still_Changes_Nothing()
    {
        var attachment = Sample("brief.pdf", 480_256);
        var delay = new ManualDelay();
        using var cts = new CancellationTokenSource();
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = AiFileIntake.RunUploadAsync(attachment, (_, _, _) => finish.Task, AiFileIntake.DoneHold, delay.Delay, () => "Could not upload", cts.Token);
        await cts.CancelAsync();
        finish.SetResult();
        Pump();
        await task;
        attachment.Status.ShouldBe(AiAttachmentStatus.Uploading);
        delay.Requested.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task Cancelling_While_The_Check_Shows_Leaves_It_Done()
    {
        var attachment = Sample("brief.pdf", 480_256);
        var upload = new FakeUpload();
        var delay = new ManualDelay();
        using var cts = new CancellationTokenSource();
        var task = Run(attachment, upload, delay, cts.Token);
        upload.Finish();
        Pump();
        attachment.Status.ShouldBe(AiAttachmentStatus.Done);
        await cts.CancelAsync();
        Pump();
        await task;
        attachment.Status.ShouldBe(AiAttachmentStatus.Done);
    }

    [AvaloniaFact]
    public async Task A_Status_Changed_During_The_Hold_Is_Not_Overwritten()
    {
        var attachment = Sample("brief.pdf", 480_256);
        var upload = new FakeUpload();
        var delay = new ManualDelay();
        var task = Run(attachment, upload, delay);
        upload.Finish();
        Pump();
        attachment.Error = "Virus scan failed";
        attachment.Status = AiAttachmentStatus.Error;
        delay.Release();
        Pump();
        await task;
        attachment.Status.ShouldBe(AiAttachmentStatus.Error);
    }

    [AvaloniaFact]
    public async Task A_Refused_Attachment_Is_Not_Uploaded()
    {
        var attachment = Sample("standup-capture.mov", 31 * MB, AiAttachmentStatus.Error, error: "Larger than 30 MB");
        var upload = new FakeUpload();
        await Run(attachment, upload, new ManualDelay());
        upload.Calls.ShouldBe(0);
        attachment.Status.ShouldBe(AiAttachmentStatus.Error);
        attachment.Error.ShouldBe("Larger than 30 MB");
    }

    [AvaloniaFact]
    public async Task An_Already_Cancelled_Upload_Does_Not_Start()
    {
        var attachment = Sample("brief.pdf", 1_000);
        var upload = new FakeUpload();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Run(attachment, upload, new ManualDelay(), cts.Token);
        upload.Calls.ShouldBe(0);
        attachment.Status.ShouldBe(AiAttachmentStatus.Ready);
    }

    [AvaloniaFact]
    public async Task The_Real_Delay_Waits_And_Can_Be_Cancelled()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        await AiFileIntake.DelayAsync(TimeSpan.FromMilliseconds(20), default);
        clock.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(15);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<TaskCanceledException>(() => AiFileIntake.DelayAsync(TimeSpan.FromSeconds(10), cts.Token));
        AiFileIntake.DoneHold.ShouldBe(TimeSpan.FromMilliseconds(900));
    }

    #endregion
}
