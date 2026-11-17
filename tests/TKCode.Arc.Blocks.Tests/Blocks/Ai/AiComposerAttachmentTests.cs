using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.Raw;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>Attachments: files, validation, the upload pipeline, removing, pasting, dropping, the picker and the plus menu.</summary>
public class AiComposerAttachmentTests
{
    private static async Task<IStorageFile> FileAsync(AiComposerRig rig, string name, long size) =>
        await AiAttachmentSamples.FileAsync(rig.Window, AiAttachmentSamples.NewFolder(), name, size);

    private static async Task Settle(AiComposerRig rig, Func<bool> done)
    {
        for (var i = 0; i < 100 && !done(); i++)
        {
            await Task.Delay(5);
            rig.Pump();
        }
        rig.Pump();
    }

    #region Adding files

    [AvaloniaFact]
    public async Task Files_Without_An_Upload_Handler_Are_Ready_At_Once_And_Can_Be_Sent_Alone()
    {
        using var rig = new AiComposerRig();
        var file = await FileAsync(rig, "roadmap-notes.pdf", 469 * 1024);
        (await rig.Composer.AddFilesAsync([file])).ShouldBeTrue();
        rig.Pump();
        var attachment = rig.Composer.Attachments.Single();
        attachment.Status.ShouldBe(AiAttachmentStatus.Ready);
        attachment.Name.ShouldBe("roadmap-notes.pdf");
        attachment.File.ShouldBe(file);
        rig.Added.ShouldBe([attachment]);
        rig.Composer.SendState.ShouldBe(AiSendState.Ready);
        rig.Composer.Tray!.ChipFor(attachment).ShouldNotBeNull();
    }

    [AvaloniaFact]
    public async Task A_File_Over_The_Size_Limit_Or_Of_Another_Type_Is_Attached_In_Error_And_Does_Not_Count()
    {
        using var rig = new AiComposerRig(c =>
        {
            c.MaxFileSize = 1024 * 1024;
            c.AcceptedFileTypes = ".pdf,image/*";
        });
        var big = await FileAsync(rig, "standup-capture.pdf", 2 * 1024 * 1024);
        var sheet = await FileAsync(rig, "usage-oct.csv", 2048);
        var picture = await FileAsync(rig, "board.png", 0);
        var folder = await AiAttachmentSamples.FolderAsync(rig.Window, AiAttachmentSamples.NewFolder(), "exports");
        await rig.Composer.AddFilesAsync([big, sheet, folder]);
        rig.Pump();
        rig.Composer.Attachments.Count.ShouldBe(2);
        rig.Composer.Attachments[0].Status.ShouldBe(AiAttachmentStatus.Error);
        rig.Composer.Attachments[0].Error.ShouldBe("Over 1 MB");
        rig.Composer.Attachments[1].Error.ShouldBe("Type not accepted");
        rig.Announcement.ShouldBe("usage-oct.csv: Type not accepted");
        rig.Composer.SendState.ShouldBe(AiSendState.Empty);
        // A picture that cannot be decoded becomes a plain file, still accepted by "image/*".
        await rig.Composer.AddFilesAsync([picture]);
        rig.Composer.Attachments.Count.ShouldBe(3);
        rig.Composer.Attachments[2].Status.ShouldBe(AiAttachmentStatus.Ready);
    }

    [AvaloniaFact]
    public async Task With_Attachments_Off_The_Plus_Button_Hides_And_Files_Are_Ignored()
    {
        using var rig = new AiComposerRig(c => c.IsAttachmentEnabled = false);
        rig.Composer.PlusButton!.IsVisible.ShouldBeFalse();
        var file = await FileAsync(rig, "notes.txt", 10);
        (await rig.Composer.AddFilesAsync([file])).ShouldBeFalse();
        await rig.Composer.PasteAsync([file], null);
        rig.Composer.Attachments.ShouldBeEmpty();
        // A long paste stays text when nothing can be attached.
        rig.Focus();
        await rig.Composer.PasteAsync(null, AiComposerSamples.LongPaste());
        rig.Pump();
        rig.Composer.Attachments.ShouldBeEmpty();
        rig.Composer.Text.ShouldBe(AiComposerSamples.LongPaste());
        rig.Composer.IsAttachmentEnabled = true;
        rig.Composer.PlusButton.IsVisible.ShouldBeTrue();
    }

    #endregion

    #region Upload pipeline

    [AvaloniaFact]
    public async Task An_Upload_Blocks_Sending_Reports_Progress_Shows_Its_Check_Then_Becomes_Ready()
    {
        var finish = new TaskCompletionSource();
        IProgress<double>? progress = null;
        using var rig = new AiComposerRig(c => c.UploadHandler = async (_, p, ct) =>
        {
            progress = p;
            await finish.Task.WaitAsync(ct);
        });
        rig.Focus();
        rig.Type("Check this");
        var file = await FileAsync(rig, "launch-brief.pdf", 2_400_000);
        await rig.Composer.AddFilesAsync([file]);
        rig.Pump();
        var attachment = rig.Composer.Attachments.Single();
        attachment.Status.ShouldBe(AiAttachmentStatus.Uploading);
        rig.Composer.SendState.ShouldBe(AiSendState.Blocked);
        rig.Composer.Classes.Contains(":blocked").ShouldBeTrue();
        rig.Send.CurrentName.ShouldBe("Wait for uploads to finish");
        rig.Key(PhysicalKey.Enter);
        rig.Sent.ShouldBeEmpty();

        progress!.Report(0.5);
        rig.Pump();
        attachment.Progress.ShouldBe(0.5);

        finish.SetResult();
        await Settle(rig, () => attachment.Status == AiAttachmentStatus.Done);
        attachment.Status.ShouldBe(AiAttachmentStatus.Done);
        rig.Announcement.ShouldBe("launch-brief.pdf uploaded");
        rig.Composer.SendState.ShouldBe(AiSendState.Ready);
        rig.Delays.Pending.ShouldBe([TimeSpan.FromMilliseconds(900)]);
        rig.Delays.Finish(TimeSpan.FromMilliseconds(900));
        await Settle(rig, () => attachment.Status == AiAttachmentStatus.Ready);
        attachment.Status.ShouldBe(AiAttachmentStatus.Ready);
    }

    [AvaloniaFact]
    public async Task A_Failed_Upload_Shows_Its_Reason_And_Does_Not_Block()
    {
        using var rig = new AiComposerRig(c => c.UploadHandler = (_, _, _) => Task.FromException(new InvalidOperationException("Server is busy")));
        var file = await FileAsync(rig, "launch-brief.pdf", 100);
        await rig.Composer.AddFilesAsync([file]);
        var attachment = rig.Composer.Attachments.Single();
        await Settle(rig, () => attachment.Status == AiAttachmentStatus.Error);
        attachment.Error.ShouldBe("Server is busy");
        rig.Announcement.ShouldBe("launch-brief.pdf: Server is busy");
        rig.Composer.SendState.ShouldBe(AiSendState.Empty);
    }

    [AvaloniaFact]
    public async Task Cancelling_An_Upload_Removes_The_Attachment_And_Cancels_The_Handler()
    {
        CancellationToken token = default;
        using var rig = new AiComposerRig(c => c.UploadHandler = async (_, _, ct) =>
        {
            token = ct;
            await Task.Delay(Timeout.Infinite, ct);
        });
        var file = await FileAsync(rig, "launch-brief.pdf", 100);
        await rig.Composer.AddFilesAsync([file]);
        rig.Pump();
        var attachment = rig.Composer.Attachments.Single();
        var chip = rig.Composer.Tray!.ChipFor(attachment)!;
        chip.ActionButton.ShouldNotBeNull();
        rig.Click(chip.ActionButton!);
        rig.Composer.Attachments.ShouldBeEmpty();
        token.IsCancellationRequested.ShouldBeTrue();
        rig.Removed.ShouldBe([attachment]);
        rig.Announcement.ShouldBe("Removed launch-brief.pdf");
        rig.Composer.SendState.ShouldBe(AiSendState.Empty);
    }

    [AvaloniaFact]
    public void Removing_The_Last_Chip_From_The_Keyboard_Moves_The_Focus_To_The_Text_Area()
    {
        using var rig = new AiComposerRig(c =>
        {
            c.Attachments.Add(AiComposerSamples.Attachment("usage-oct.csv", 12 * 1024));
            c.Attachments.Add(AiComposerSamples.Attachment("roadmap-notes.pdf", 469 * 1024));
        });
        var tray = rig.Composer.Tray!;
        var first = tray.ChipFor(rig.Composer.Attachments[0])!.ActionButton!;
        first.Focus(NavigationMethod.Tab);
        rig.Pump();
        rig.Key(PhysicalKey.Enter);
        rig.Composer.Attachments.Count.ShouldBe(1);
        rig.EditorFocused.ShouldBeFalse();
        tray.IsKeyboardFocusWithin.ShouldBeTrue();
        rig.Key(PhysicalKey.Enter);
        rig.Composer.Attachments.ShouldBeEmpty();
        rig.EditorFocused.ShouldBeTrue();
        rig.Removed.Count.ShouldBe(2);
    }

    #endregion

    #region Paste

    [AvaloniaFact]
    public async Task A_Long_Paste_Becomes_A_Pasted_Text_Chip_And_Stays_Out_Of_The_Undo_History()
    {
        using var rig = new AiComposerRig();
        rig.Focus();
        rig.Type("Summarize");
        await rig.Composer.PasteAsync(null, AiComposerSamples.LongPaste());
        rig.Pump();
        rig.Composer.Text.ShouldBe("Summarize");
        var chip = rig.Composer.Attachments.Single();
        chip.Kind.ShouldBe(AiAttachmentKind.PastedText);
        chip.Name.ShouldBe("Pasted text");
        chip.Meta.ShouldBe("24 lines");
        chip.PastedText.ShouldBe(AiComposerSamples.LongPaste());
        rig.Announcement.ShouldBe("Long text attached as a file");
        rig.Editor.Undo();
        rig.Pump();
        rig.Composer.Text.ShouldBe(string.Empty);
        rig.Composer.Attachments.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task The_Long_Paste_Thresholds_Count_Characters_Or_Lines_And_Zero_Turns_A_Rule_Off()
    {
        using var rig = new AiComposerRig(c =>
        {
            c.LongPasteCharacters = 50;
            c.LongPasteLines = 0;
        });
        rig.Focus();
        await rig.Composer.PasteAsync(null, "a\nb\nc\nd\ne\nf\ng\nh\ni\nj\nk\nl\nm");
        rig.Composer.Attachments.ShouldBeEmpty();
        rig.Composer.Text.ShouldStartWith("a\nb");
        await rig.Composer.PasteAsync(null, new string('x', 50));
        rig.Composer.Attachments.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task A_Short_Paste_Is_Typed_As_Text()
    {
        using var rig = new AiComposerRig();
        rig.Focus();
        rig.Type("Hello ");
        await rig.Composer.PasteAsync(null, "world\r\nagain");
        rig.Pump();
        rig.Composer.Text.ShouldBe("Hello world\nagain");
        rig.Composer.Attachments.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task Pasting_From_The_Clipboard_Goes_Through_The_Composer()
    {
        using var rig = new AiComposerRig();
        rig.Focus();
        var clipboard = TopLevel.GetTopLevel(rig.Window)!.Clipboard!;
        await clipboard.SetTextAsync(AiComposerSamples.LongPaste());
        rig.Editor.Box!.Paste();
        await Settle(rig, () => rig.Composer.Attachments.Count > 0);
        rig.Composer.Attachments.Single().Kind.ShouldBe(AiAttachmentKind.PastedText);
        rig.Composer.Text.ShouldBe(string.Empty);

        await clipboard.SetTextAsync("short note");
        rig.Editor.Box.Paste();
        await Settle(rig, () => rig.Composer.Text.Length > 0);
        rig.Composer.Text.ShouldBe("short note");
    }

    [AvaloniaFact]
    public async Task Pasted_Files_Become_Attachments()
    {
        using var rig = new AiComposerRig();
        var file = await FileAsync(rig, "wireframe.txt", 10);
        await rig.Composer.PasteAsync([file], "wireframe.txt");
        rig.Composer.Attachments.Single().Name.ShouldBe("wireframe.txt");
        rig.Composer.Text.ShouldBe(string.Empty);
    }

    #endregion

    #region Drop, picker

    [AvaloniaFact]
    public async Task Files_Dragged_Over_The_Composer_Show_The_Cover_And_Drop_As_Attachments()
    {
        using var rig = new AiComposerRig();
        var file = await FileAsync(rig, "metrics.csv", 1200);
        var data = new DataTransfer();
        data.Add(DataTransferItem.CreateFile(file));
        var inside = rig.CenterOf(rig.Composer.Editor!);
        rig.Window.DragDrop(inside, RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.None);
        rig.Pump();
        rig.Composer.IsDragging.ShouldBeTrue();
        rig.Composer.Classes.Contains(":dragging").ShouldBeTrue();
        rig.Composer.DropHost!.IsPresent.ShouldBeTrue();
        rig.Composer.DropTitle!.Text.ShouldBe("Drop to attach");
        rig.Composer.DropHint!.Text.ShouldBe("Documents, images or code, 30 MB max per file");

        // Moving onto another part of the composer keeps the cover.
        rig.Window.DragDrop(rig.CenterOf(rig.Send), RawDragEventType.DragOver, data, DragDropEffects.Copy, RawInputModifiers.None);
        rig.Pump();
        rig.Composer.IsDragging.ShouldBeTrue();

        rig.Window.DragDrop(new Point(5, 5), RawDragEventType.DragOver, data, DragDropEffects.Copy, RawInputModifiers.None);
        rig.Pump();
        rig.Composer.IsDragging.ShouldBeFalse();

        rig.Window.DragDrop(inside, RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.None);
        rig.Window.DragDrop(inside, RawDragEventType.Drop, data, DragDropEffects.Copy, RawInputModifiers.None);
        await Settle(rig, () => rig.Composer.Attachments.Count > 0);
        rig.Composer.IsDragging.ShouldBeFalse();
        rig.Composer.Attachments.Single().Name.ShouldBe("metrics.csv");
    }

    [AvaloniaFact]
    public void A_Drag_Without_Files_Or_With_Attachments_Off_Shows_Nothing()
    {
        using var rig = new AiComposerRig(c => c.IsAttachmentEnabled = false);
        var text = new DataTransfer();
        text.Add(DataTransferItem.CreateText("hello"));
        rig.Window.DragDrop(rig.CenterOf(rig.Composer.Editor!), RawDragEventType.DragEnter, text, DragDropEffects.Copy, RawInputModifiers.None);
        rig.Pump();
        rig.Composer.IsDragging.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task Browse_Opens_The_Picker_With_The_Accepted_Types_And_Adds_The_Choice()
    {
        FilePickerOpenOptions? asked = null;
        IStorageFile? picked = null;
        using var rig = new AiComposerRig(c =>
        {
            c.AcceptedFileTypes = ".pdf,image/*";
            c.PickFilesAsync = options =>
            {
                asked = options;
                return Task.FromResult<IReadOnlyList<IStorageFile>>(picked is null ? [] : [picked]);
            };
        });
        picked = await FileAsync(rig, "brief.pdf", 300);
        await rig.Composer.BrowseAsync();
        asked.ShouldNotBeNull();
        asked.AllowMultiple.ShouldBeTrue();
        asked.Title.ShouldBe("Attach files");
        asked.FileTypeFilter!.Single().Patterns!.ShouldContain("*.pdf");
        asked.FileTypeFilter!.Single().Patterns!.ShouldContain("*.png");
        rig.Composer.Attachments.Single().Name.ShouldBe("brief.pdf");
    }

    #endregion

    #region Plus menu

    [AvaloniaFact]
    public void The_Plus_Menu_Lists_Upload_The_Sources_And_Mention()
    {
        using var rig = new AiComposerRig();
        rig.Click(rig.Composer.PlusButton!);
        var menu = rig.Composer.AttachMenu!;
        menu.IsOpen.ShouldBeTrue();
        rig.Composer.PlusButton!.IsOpen.ShouldBeTrue();
        var entries = menu.Items.OfType<MenuEntry>().ToList();
        entries.Select(e => e.Header as string).ShouldBe(
            ["Upload file", "Add from cloud drive", "Add from repository", "Capture screen", "Mention a file or doc"]);
        entries[^1].HasSeparatorBefore.ShouldBeTrue();
        menu.Side.ShouldBe(MenuSide.Top);
        menu.Alignment.ShouldBe(MenuAlignment.Start);
        Avalonia.Automation.AutomationProperties.GetName(rig.Composer.PlusButton).ShouldBe("Add to message");
    }

    [AvaloniaFact]
    public void The_Plus_Menu_Has_No_Mention_Entry_Without_Context_Items()
    {
        using var rig = new AiComposerRig(c => c.ContextItems.Clear());
        rig.Click(rig.Composer.PlusButton!);
        rig.Composer.AttachMenu!.Items.OfType<MenuEntry>().Select(e => e.Header as string).ShouldNotContain("Mention a file or doc");
    }

    [AvaloniaFact]
    public void Mention_Types_An_At_And_Opens_The_List_In_The_Text_Area()
    {
        using var rig = new AiComposerRig();
        rig.Focus();
        rig.Type("Compare");
        rig.Click(rig.Composer.PlusButton!);
        var mention = rig.Composer.AttachMenu!.Items.OfType<MenuEntry>().Last();
        mention.PerformClick();
        rig.Pump();
        rig.Composer.AttachMenu.IsOpen.ShouldBeFalse();
        rig.EditorFocused.ShouldBeTrue();
        rig.Composer.Text.ShouldBe("Compare @");
        rig.ListOpen.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Upload_Opens_The_Picker_And_The_Focus_Goes_To_The_Text_Area()
    {
        var picks = 0;
        using var rig = new AiComposerRig(c => c.PickFilesAsync = _ =>
        {
            picks++;
            return Task.FromResult<IReadOnlyList<IStorageFile>>([]);
        });
        rig.Composer.PlusButton!.Focus(NavigationMethod.Tab);
        rig.Pump();
        rig.Key(PhysicalKey.Enter);
        var menu = rig.Composer.AttachMenu!;
        menu.IsOpen.ShouldBeTrue();
        menu.HighlightedEntry!.Header.ShouldBe("Upload file");
        rig.Key(PhysicalKey.Enter);
        rig.Pump();
        menu.IsOpen.ShouldBeFalse();
        picks.ShouldBe(1);
        rig.EditorFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Source_Entry_Runs_Its_Command_With_The_Composer()
    {
        using var rig = new AiComposerRig();
        rig.Click(rig.Composer.PlusButton!);
        rig.Composer.AttachMenu!.Items.OfType<MenuEntry>().ElementAt(1).PerformClick();
        rig.Pump();
        var source = rig.Composer.Attachments.Single();
        source.Kind.ShouldBe(AiAttachmentKind.Source);
        source.Name.ShouldBe("Budget forecast");
        rig.Added.ShouldBe([source]);
        rig.EditorFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Escape_Closes_The_Plus_Menu_And_Returns_The_Focus_To_The_Button()
    {
        using var rig = new AiComposerRig();
        rig.Composer.PlusButton!.Focus(NavigationMethod.Tab);
        rig.Pump();
        rig.Key(PhysicalKey.ArrowDown);
        rig.Composer.AttachMenu!.IsOpen.ShouldBeTrue();
        rig.Key(PhysicalKey.Escape);
        rig.Composer.AttachMenu.IsOpen.ShouldBeFalse();
        rig.Focused.ShouldBe(rig.Composer.PlusButton);
        rig.Composer.PlusButton.IsOpen.ShouldBeFalse();
    }

    #endregion

    #region Clear

    [AvaloniaFact]
    public async Task Clear_Empties_Text_Tokens_And_Attachments_But_Keeps_Tools_And_Model()
    {
        CancellationToken token = default;
        using var rig = new AiComposerRig(c => c.UploadHandler = async (_, _, ct) =>
        {
            token = ct;
            await Task.Delay(Timeout.Infinite, ct);
        });
        rig.Composer.SetPrompt(AiComposerSamples.Suggestions[0].Prompt);
        rig.Composer.Tools[0].IsChecked = true;
        var model = rig.Composer.Models[2];
        rig.Composer.SelectedModel = model;
        var file = await FileAsync(rig, "launch-brief.pdf", 100);
        await rig.Composer.AddFilesAsync([file]);
        rig.Pump();
        rig.Composer.Clear();
        rig.Pump();
        rig.Composer.Text.ShouldBe(string.Empty);
        rig.Composer.Value.Tokens.ShouldBeEmpty();
        rig.Composer.Attachments.ShouldBeEmpty();
        rig.Removed.Count.ShouldBe(1);
        token.IsCancellationRequested.ShouldBeTrue();
        rig.Composer.Tools[0].IsChecked.ShouldBeTrue();
        rig.Composer.SelectedModel.ShouldBe(model);
        rig.Composer.SendState.ShouldBe(AiSendState.Empty);
    }

    #endregion
}
