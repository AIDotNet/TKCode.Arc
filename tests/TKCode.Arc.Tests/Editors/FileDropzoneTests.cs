using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Editors;

/// <summary>Real files in the test output folder, opened through the window's storage provider.</summary>
internal static class DropzoneFixtures
{
    private static readonly string s_folder = Path.Combine(AppContext.BaseDirectory, "dropzone-fixtures");

    public static async Task<IStorageFile> File(TopLevel top, string name, long size)
    {
        Directory.CreateDirectory(s_folder);
        var path = Path.Combine(s_folder, name);
        await using (var stream = System.IO.File.Create(path))
        {
            stream.SetLength(size);
        }
        System.IO.File.SetLastWriteTimeUtc(path, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var file = await top.StorageProvider.TryGetFileFromPathAsync(path);
        return file ?? throw new InvalidOperationException("The headless storage provider cannot open files.");
    }
}

public class FileDropzoneTests
{
    private static (FileDropzone Zone, Window Window) Show(Action<FileDropzone>? setup = null, double width = 520)
    {
        var zone = new FileDropzone { Width = width };
        setup?.Invoke(zone);
        var window = TestHelpers.Show(new Border { Padding = new Thickness(20), Child = zone });
        window.Width = width + 40;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        return (zone, window);
    }

    private static Task<IStorageFile> F(TopLevel top, string name, long size) => DropzoneFixtures.File(top, name, size);

    private static async Task Add(FileDropzone zone, params IStorageItem[] files)
    {
        await zone.AddFilesAsync(files);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var (zone, window) = Show();
        try
        {
            zone.Label.ShouldBe("Add files");
            zone.Description.ShouldBe("Drop files here or choose from your device");
            zone.MaxFiles.ShouldBe(5);
            zone.AllowMultiple.ShouldBeTrue();
            zone.NoteText.ShouldBe("Up to 5 files");
            zone.DisplayLabel.ShouldBe("Add files");
            var zoneVisual = TestHelpers.Descendant<FileDropzoneZone>(zone);
            zoneVisual.CornerRadius.TopLeft.ShouldBe(34);
            var trigger = TestHelpers.Descendant<FileDropzoneTrigger>(zone);
            trigger.Padding.ShouldBe(new Thickness(20, 26, 20, 34));
            Avalonia.Automation.AutomationProperties.GetName(trigger).ShouldBe("Add files");
            zone.Accept = "image/*,.pdf";
            zone.NoteText.ShouldBe("Accepted: image/*,.pdf");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Adding_Files_Lists_Them_And_Raises_FilesChanged()
    {
        var (zone, window) = Show();
        try
        {
            IReadOnlyList<IStorageFile>? files = null;
            zone.FilesChanged += (_, e) => files = e.Files;
            await Add(zone, await F(window, "brief.pdf", 120_000), await F(window, "photo.png", 2_400_000));
            zone.Items.Count.ShouldBe(2);
            files.ShouldNotBeNull().Count.ShouldBe(2);
            zone.Items[0].Status.ShouldBe(FileDropzoneStatus.None);
            zone.Status!.Message.ShouldBe("2 files added.");
            var row = zone.RowOf(zone.Items[0]).ShouldNotBeNull();
            row.SizeText.ShouldBe("117 KB");
            row.TypeIcon.ShouldBe(ArcIcons.FileText);
            zone.RowOf(zone.Items[1])!.SizeText.ShouldBe("2.3 MB");
            zone.Classes.ShouldContain(":has-rows");

            // The same file again is not added twice.
            await Add(zone, await F(window, "brief.pdf", 120_000));
            zone.Items.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Refused_Types_And_Overflow_Show_An_Error()
    {
        var (zone, window) = Show(z =>
        {
            z.Accept = ".pdf";
            z.MaxFiles = 2;
        });
        try
        {
            string? rejected = null;
            zone.FilesRejected += (_, e) => rejected = e.Message;
            await Add(zone, await F(window, "notes.txt", 10));
            zone.ErrorMessage.ShouldBe("notes.txt is not an accepted file type.");
            rejected.ShouldBe(zone.ErrorMessage);
            zone.Alert!.Message.ShouldBe(zone.ErrorMessage);
            zone.Classes.ShouldContain(":error");
            await Add(zone, await F(window, "a.pdf", 10), await F(window, "b.pdf", 10), await F(window, "c.pdf", 10));
            zone.Items.Count.ShouldBe(2);
            zone.ErrorMessage.ShouldBe("You can add up to 2 files.");
            zone.Remove(zone.Items[0]);
            Dispatcher.UIThread.RunJobs();
            zone.ErrorMessage.ShouldBeNull();
            zone.Items.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Too_Large_Files_Fail_Without_Retry()
    {
        var (zone, window) = Show(z => z.MaxFileSize = 1024 * 1024);
        try
        {
            await Add(zone, await F(window, "huge.zip", 5 * 1024 * 1024));
            var item = zone.Items.Single();
            item.Status.ShouldBe(FileDropzoneStatus.Failed);
            item.Error.ShouldBe("File is larger than 1 MB");
            item.IsRetryable.ShouldBeFalse();
            zone.RowOf(item)!.Classes.ShouldContain(":failed");
            zone.RowOf(item)!.CanRetry.ShouldBeFalse();
            zone.Status!.Message.ShouldBe("1 file added. huge.zip: File is larger than 1 MB.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Upload_Handler_Drives_Progress_Completion_Failure_And_Retry()
    {
        var gates = new Dictionary<string, TaskCompletionSource>();
        IProgress<double>? reporter = null;
        var (zone, window) = Show(z => z.UploadHandler = (item, progress, token) =>
        {
            reporter = progress;
            var gate = new TaskCompletionSource();
            gates[item.Name] = gate;
            token.Register(() => gate.TrySetCanceled());
            return gate.Task;
        });
        try
        {
            zone.DisplayLabel.ShouldBe("Add files");
            await Add(zone, await F(window, "report.pdf", 2048));
            var item = zone.Items.Single();
            item.Status.ShouldBe(FileDropzoneStatus.Uploading);
            reporter!.Report(40);
            reporter.Report(20);
            item.Progress.ShouldBe(40);
            var row = zone.RowOf(item)!;
            row.IsBarOpen.ShouldBeTrue();
            gates["report.pdf"].SetException(new InvalidOperationException("Server said no"));
            Dispatcher.UIThread.RunJobs();
            item.Status.ShouldBe(FileDropzoneStatus.Failed);
            item.Error.ShouldBe("Server said no");
            row.CanRetry.ShouldBeTrue();
            zone.Status!.Message.ShouldBe("report.pdf failed. Server said no");

            zone.Retry(item);
            item.Status.ShouldBe(FileDropzoneStatus.Uploading);
            item.Progress.ShouldBe(0);
            gates["report.pdf"].SetResult();
            Dispatcher.UIThread.RunJobs();
            item.Status.ShouldBe(FileDropzoneStatus.Uploaded);
            item.Progress.ShouldBe(100);
            row.IsBarOpen.ShouldBeFalse();
            zone.Status!.Message.ShouldBe("report.pdf uploaded");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Removing_An_Uploading_Row_Cancels_It()
    {
        CancellationToken seen = default;
        var (zone, window) = Show(z => z.UploadHandler = (_, _, token) =>
        {
            seen = token;
            return Task.Delay(Timeout.Infinite, token);
        });
        try
        {
            await Add(zone, await F(window, "a.pdf", 10));
            zone.Remove(zone.Items[0]);
            Dispatcher.UIThread.RunJobs();
            seen.IsCancellationRequested.ShouldBeTrue();
            zone.Items.ShouldBeEmpty();
            zone.Status!.Message.ShouldBe("a.pdf removed");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void App_Driven_Progress_Api()
    {
        var (zone, window) = Show();
        try
        {
            var item = new FileDropzoneItem("export.csv", 4096);
            zone.Items.Add(item);
            Dispatcher.UIThread.RunJobs();
            zone.ReportProgress(item, 30);
            item.Status.ShouldBe(FileDropzoneStatus.Uploading);
            zone.RowOf(item)!.IsBarOpen.ShouldBeTrue();
            zone.CompleteUpload(item);
            item.Status.ShouldBe(FileDropzoneStatus.Uploaded);
            zone.FailUpload(item, "Lost connection");
            item.Error.ShouldBe("Lost connection");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Single_File_Mode_Replaces_The_File()
    {
        var (zone, window) = Show(z => z.AllowMultiple = false);
        try
        {
            await Add(zone, await F(window, "a.pdf", 10));
            await Add(zone, await F(window, "b.pdf", 10));
            zone.Items.Select(i => i.Name).ShouldBe(new[] { "b.pdf" });
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Compact_Folds_The_Prompt()
    {
        var (zone, window) = Show(z => z.CompactAt = 2);
        try
        {
            await Add(zone, await F(window, "a.pdf", 10), await F(window, "b.pdf", 10));
            zone.Classes.ShouldContain(":compact");
            zone.IsIconShown.ShouldBeFalse();
            zone.IsNoteShown.ShouldBeFalse();
            TestHelpers.Descendant<FileDropzoneZone>(zone).CornerRadius.TopLeft.ShouldBe(26);
            zone.SetDragOver(true);
            zone.IsIconShown.ShouldBeTrue();
            zone.DisplayLabel.ShouldBe("Drop to add files");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Dropping_Files_From_The_System_Adds_Them()
    {
        var (zone, window) = Show();
        try
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var trigger = TestHelpers.Descendant<FileDropzoneTrigger>(zone);
            var point = trigger.TranslatePoint(new Point(100, 40), window)!.Value;
            var data = new DataTransfer();
            data.Add(DataTransferItem.CreateFile(await F(window, "drop.pdf", 100)));
            window.DragDrop(point, RawDragEventType.DragEnter, data, DragDropEffects.Copy);
            Dispatcher.UIThread.RunJobs();
            zone.IsDragOver.ShouldBeTrue();
            zone.Classes.ShouldContain(":dragover");
            zone.DisplayLabel.ShouldBe("Drop to add files");
            window.DragDrop(point, RawDragEventType.Drop, data, DragDropEffects.Copy);
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            zone.IsDragOver.ShouldBeFalse();
            zone.Items.Select(i => i.Name).ShouldBe(new[] { "drop.pdf" });
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Arrow_Keys_Move_Between_Rows_And_Delete_Removes()
    {
        var (zone, window) = Show();
        try
        {
            await Add(zone, await F(window, "a.pdf", 10), await F(window, "b.pdf", 10), await F(window, "c.pdf", 10));
            zone.RowOf(zone.Items[0])!.RemoveButton!.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            zone.RowOf(zone.Items[1])!.RemoveButton!.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            zone.Items.Select(i => i.Name).ShouldBe(new[] { "a.pdf", "c.pdf" });
            zone.RowOf(zone.Items[1])!.RemoveButton!.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Accept_Matching_Follows_File_Input_Rules()
    {
        var accept = FileDropzoneTypes.ParseAccept("image/*, .PDF, application/zip");
        FileDropzoneTypes.Matches("Photo.JPG", accept).ShouldBeTrue();
        FileDropzoneTypes.Matches("doc.pdf", accept).ShouldBeTrue();
        FileDropzoneTypes.Matches("a.zip", accept).ShouldBeTrue();
        FileDropzoneTypes.Matches("a.txt", accept).ShouldBeFalse();
        FileDropzoneTypes.PickerFilter("image/*")![0].Patterns!.ShouldContain("*.png");
        FileDropzoneItem.FormatSize(512).ShouldBe("512 B");
        FileDropzoneItem.FormatSize(25 * 1024 * 1024).ShouldBe("25 MB");
    }
}
