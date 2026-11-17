using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Media;

public class FileUploadTests
{
    private static Window Show(Control control, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = 640;
        window.Height = 800;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static FileUploadItem File(string name, long size = 1000) => new(null, name, size);

    /// <summary>An uploader the test drives by hand: each call waits for its own completion source.</summary>
    private sealed class ManualUploader
    {
        public Dictionary<string, (IProgress<double> Progress, CancellationToken Token, TaskCompletionSource Done)> Calls { get; } = new();

        public int CallCount { get; private set; }

        public Task Upload(FileUploadItem item, IProgress<double> progress, CancellationToken token)
        {
            CallCount++;
            var done = new TaskCompletionSource();
            token.Register(() => done.TrySetCanceled());
            Calls[item.Name] = (progress, token, done);
            return done.Task;
        }
    }

    private static void Pump()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    [Fact]
    public void Accept_Matches_Extensions_Mime_Types_And_Wildcards_Through_The_Table()
    {
        var accept = FileUploadTypes.ParseAccept(" image/*, .PDF ,application/zip,");
        accept.ShouldBe(["image/*", ".pdf", "application/zip"]);
        FileUploadTypes.Matches("photo.JPG", accept).ShouldBeTrue();
        FileUploadTypes.Matches("icon.svg", accept).ShouldBeTrue();
        FileUploadTypes.Matches("Report.Pdf", accept).ShouldBeTrue();
        FileUploadTypes.Matches("bundle.zip", accept).ShouldBeTrue();
        FileUploadTypes.Matches("notes.txt", accept).ShouldBeFalse();
        FileUploadTypes.Matches("archive.gz", accept).ShouldBeFalse();
        FileUploadTypes.Matches("unknown.xyz", FileUploadTypes.ParseAccept("application/octet-stream")).ShouldBeFalse();
        FileUploadTypes.Matches("anything.xyz", FileUploadTypes.ParseAccept("")).ShouldBeTrue();
        FileUploadTypes.MimeOf("clip.mov").ShouldBe("video/quicktime");
        FileUploadTypes.MimeOf("no-extension").ShouldBe(string.Empty);
    }

    [Fact]
    public void Picker_Filter_Expands_Mime_Types_And_Wildcards()
    {
        var filter = FileUploadTypes.PickerFilter("image/*,.pdf").ShouldNotBeNull().Single();
        filter.Patterns.ShouldNotBeNull();
        filter.Patterns!.ShouldContain("*.pdf");
        filter.Patterns.ShouldContain("*.png");
        filter.Patterns.ShouldContain("*.jpeg");
        filter.Patterns.ShouldNotContain("*.zip");
        filter.MimeTypes.ShouldBe(["image/*"]);
        FileUploadTypes.PickerFilter(null).ShouldBeNull();
    }

    [Fact]
    public void Sizes_Are_Formatted_Like_Arc()
    {
        FileUploadTypes.FormatSize(512).ShouldBe("512 B");
        FileUploadTypes.FormatSize(1024).ShouldBe("1 KB");
        FileUploadTypes.FormatSize(1536).ShouldBe("2 KB");
        FileUploadTypes.FormatSize(1024 * 1024).ShouldBe("1 MB");
        FileUploadTypes.FormatSize(2_516_582).ShouldBe("2.4 MB");
        FileUploadTypes.FormatSize(12_582_912).ShouldBe("12 MB");
    }

    [Fact]
    public void File_Icons_Follow_The_Mime_Type()
    {
        FileUploadTypes.IconOf("a.png").ShouldBeSameAs(ArcIcons.FileImage);
        FileUploadTypes.IconOf("a.csv").ShouldBeSameAs(ArcIcons.FileText);
        FileUploadTypes.IconOf("a.zip").ShouldBeSameAs(ArcIcons.FileArchive);
        FileUploadTypes.IconOf("a.tar.gz").ShouldBeSameAs(ArcIcons.FileArchive);
        FileUploadTypes.IconOf("a.pdf").ShouldBeSameAs(ArcIcons.File);
    }

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var upload = new FileUpload();
        upload.Label.ShouldBe("Upload files");
        upload.Description.ShouldBe("Drop files here or browse from your device.");
        upload.BrowseLabel.ShouldBe("Browse");
        upload.Multiple.ShouldBeTrue();
        upload.Files.ShouldBeEmpty();
        Show(upload);
        var zone = upload.Dropzone.ShouldNotBeNull();
        zone.Focusable.ShouldBeTrue();
        AutomationProperties.GetName(zone).ShouldBe("Upload files");
        AutomationProperties.GetHelpText(zone).ShouldBe("Drop files here or browse from your device.");
        zone.Bounds.Height.ShouldBeGreaterThanOrEqualTo(92);
    }

    [AvaloniaFact]
    public void Rejected_Files_Stay_Listed_With_Arc_Errors_And_Statuses()
    {
        var upload = new FileUpload { Accept = ".csv,text/plain", MaxSize = 1024 * 1024 };
        Show(upload);
        upload.AddFiles([File("contacts.csv", 2000), File("backup.zip"), File("big.txt", 3_000_000)]);
        Pump();
        upload.Files.Count.ShouldBe(3);
        upload.Files[0].Error.ShouldBeNull();
        upload.Files[1].Error.ShouldBe("This file type is not accepted.");
        upload.Files[2].Error.ShouldBe("File is larger than 1 MB.");
        upload.Status.ShouldBe("1 file added. 2 files need attention.");
        upload.Announcer!.Announcement.ShouldBe("1 file added. 2 files need attention.");

        upload.AddFiles([File("a.csv"), File("b.csv")]);
        upload.Status.ShouldBe("2 files added.");
        upload.AddFiles([File("c.zip")]);
        upload.Status.ShouldBe("1 file needs attention.");
        upload.Files.Count.ShouldBe(6);
    }

    [AvaloniaFact]
    public void Without_An_Uploader_Accepted_Files_Show_The_Ready_Check()
    {
        var upload = new FileUpload();
        Show(upload);
        var item = File("notes.md");
        upload.AddFiles([item]);
        Pump();
        item.Status.ShouldBe(FileUploadStatus.None);
        var row = upload.RowOf(item).ShouldNotBeNull();
        row.SlotKey.ShouldBe("ready");
        row.BarFrame!.IsOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task Uploads_Report_Progress_Clamped_And_Monotonic_Then_Finish()
    {
        var uploader = new ManualUploader();
        var upload = new FileUpload { Uploader = uploader.Upload };
        Show(upload);
        var item = File("report.pdf");
        var changes = new List<IReadOnlyList<FileUploadItem>>();
        upload.FilesChanged += (_, e) => changes.Add(e.Files);
        upload.AddFiles([item]);
        Pump();
        changes.Count.ShouldBe(1);
        item.Status.ShouldBe(FileUploadStatus.Uploading);
        var row = upload.RowOf(item).ShouldNotBeNull();
        row.BarFrame!.IsOpen.ShouldBeTrue();
        row.SlotKey.ShouldBeNull();

        var call = uploader.Calls["report.pdf"];
        call.Progress.Report(40);
        call.Progress.Report(25);
        Pump();
        item.Progress.ShouldBe(40);
        call.Progress.Report(180);
        Pump();
        item.Progress.ShouldBe(100);
        row.PercentText.ShouldBe("100%");

        call.Done.SetResult();
        await Task.Yield();
        Pump();
        item.Status.ShouldBe(FileUploadStatus.Done);
        upload.Status.ShouldBe("report.pdf uploaded.");
        row.ShownStatus.ShouldBe(FileUploadStatus.Done);
        row.BarFrame.IsOpen.ShouldBeFalse();
        row.SlotKey.ShouldBe("ready");
    }

    [AvaloniaFact]
    public async Task A_Failed_Upload_Offers_Retry_Which_Starts_From_An_Empty_Bar()
    {
        var uploader = new ManualUploader();
        var upload = new FileUpload { Uploader = uploader.Upload };
        Show(upload);
        var item = File("clip.mov");
        upload.AddFiles([item]);
        Pump();
        uploader.Calls["clip.mov"].Progress.Report(60);
        Pump();
        uploader.Calls["clip.mov"].Done.SetException(new InvalidOperationException("offline"));
        await Task.Yield();
        Pump();
        item.Status.ShouldBe(FileUploadStatus.Failed);
        item.Progress.ShouldBe(60);
        upload.Status.ShouldBe("clip.mov could not be uploaded.");
        var row = upload.RowOf(item).ShouldNotBeNull();
        row.SlotKey.ShouldBe("retry");

        upload.Retry(item);
        Pump();
        uploader.CallCount.ShouldBe(2);
        item.Status.ShouldBe(FileUploadStatus.Uploading);
        item.Progress.ShouldBe(0);
        row.PercentText.ShouldBe("0%");
        row.SlotKey.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task Removing_Cancels_The_Upload_Without_A_Failure_And_Moves_Focus()
    {
        var uploader = new ManualUploader();
        var upload = new FileUpload { Uploader = uploader.Upload };
        Show(upload);
        var a = File("a.pdf");
        var b = File("b.pdf");
        var c = File("c.pdf");
        upload.AddFiles([a, b, c]);
        Pump();

        upload.Remove(b);
        Pump();
        uploader.Calls["b.pdf"].Token.IsCancellationRequested.ShouldBeTrue();
        await Task.Yield();
        Pump();
        upload.Files.ShouldBe([a, c]);
        upload.Status.ShouldBe("b.pdf removed.");
        b.Status.ShouldBe(FileUploadStatus.Uploading, "an aborted upload is not marked failed");
        upload.RowOf(c)!.RemoveButton!.IsFocused.ShouldBeTrue("focus moves to the next row's Remove");

        upload.Remove(c);
        Pump();
        upload.RowOf(a)!.RemoveButton!.IsFocused.ShouldBeTrue("else the previous row's Remove");

        upload.Remove(a);
        Pump();
        upload.Dropzone!.IsFocused.ShouldBeTrue("else the dropzone");
        upload.Files.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Single_Mode_Replaces_The_List_And_Cancels_Running_Uploads()
    {
        var uploader = new ManualUploader();
        var upload = new FileUpload { Uploader = uploader.Upload, Multiple = false };
        Show(upload);
        var first = File("one.png");
        upload.AddFiles([first]);
        Pump();
        var second = File("two.png");
        upload.AddFiles([second, File("three.png")]);
        Pump();
        uploader.Calls["one.png"].Token.IsCancellationRequested.ShouldBeTrue();
        upload.Files.ShouldBe([second]);
        uploader.Calls.ContainsKey("three.png").ShouldBeFalse();
        upload.Status.ShouldBe("1 file added.");
    }

    [AvaloniaFact]
    public void Leaving_The_Tree_Cancels_Uploads()
    {
        var uploader = new ManualUploader();
        var upload = new FileUpload { Uploader = uploader.Upload };
        var window = Show(upload);
        upload.AddFiles([File("x.pdf")]);
        Pump();
        window.Content = null;
        Pump();
        uploader.Calls["x.pdf"].Token.IsCancellationRequested.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Removed_Rows_Close_Then_Leave_The_List()
    {
        var upload = new FileUpload();
        Show(upload, ArcMotionLevel.Full);
        var item = File("a.txt");
        upload.AddFiles([item]);
        Pump();
        var host = upload.RowHostOf(item).ShouldNotBeNull();
        upload.Remove(item);
        host.IsOpen.ShouldBeFalse();
        TestHelpers.PumpUntil(() => upload.RowOf(item) is null).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Enter_And_Space_On_The_Dropzone_Are_Handled()
    {
        var upload = new FileUpload();
        var window = Show(upload);
        upload.Dropzone!.Focus(NavigationMethod.Tab);
        var handled = 0;
        upload.Dropzone.AddHandler(InputElement.KeyDownEvent, (_, e) => handled += e.Handled ? 1 : 0, handledEventsToo: true);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Pump();
        handled.ShouldBe(2);
    }

    [AvaloniaFact]
    public void Disabled_Upload_Is_Not_Focusable()
    {
        var upload = new FileUpload { IsEnabled = false };
        Show(upload);
        upload.Dropzone!.IsEffectivelyEnabled.ShouldBeFalse();
        upload.Dropzone.Focus().ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Remove_And_Retry_Are_Named_With_The_File_Name()
    {
        var upload = new FileUpload();
        Show(upload);
        var item = File("plan.docx");
        upload.AddFiles([item]);
        Pump();
        AutomationProperties.GetName(upload.RowOf(item)!.RemoveButton!).ShouldBe("Remove plan.docx");
    }
}
