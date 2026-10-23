using System.Windows.Input;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The send state machine, the message, Enter (IME included), the stop button and Escape.</summary>
public class AiComposerSendTests
{
    private sealed class Recorder : ICommand
    {
        public List<object?> Calls { get; } = new();

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Calls.Add(parameter);
    }

    #region States

    [AvaloniaFact]
    public void The_Send_State_Follows_Text_Attachments_The_Limit_And_Generation()
    {
        using var rig = new AiComposerRig(c => c.MaxLength = 20);
        var composer = rig.Composer;
        composer.SendState.ShouldBe(AiSendState.Empty);
        rig.Send.State.ShouldBe(AiSendState.Empty);
        rig.Send.CurrentName.ShouldBe("Type a message to send");

        composer.Text = "   ";
        composer.SendState.ShouldBe(AiSendState.Empty);
        composer.Text = "Plan it";
        composer.SendState.ShouldBe(AiSendState.Ready);
        composer.Classes.Contains(":ready").ShouldBeTrue();
        rig.Send.CurrentName.ShouldBe("Send message");

        composer.Text = new string('x', 23);
        composer.SendState.ShouldBe(AiSendState.Blocked);
        composer.Classes.Contains(":over-limit").ShouldBeTrue();
        rig.Send.CurrentName.ShouldBe("Shorten the message by 3 characters");
        composer.Text = new string('x', 20);
        composer.SendState.ShouldBe(AiSendState.Ready);
        composer.Classes.Contains(":over-limit").ShouldBeFalse();

        composer.Text = string.Empty;
        var failed = AiComposerSamples.Attachment("big.mov", 99, AiAttachmentStatus.Error, error: "Over 30 MB");
        composer.Attachments.Add(failed);
        composer.SendState.ShouldBe(AiSendState.Empty);
        var uploading = AiComposerSamples.Attachment("brief.pdf", 99, AiAttachmentStatus.Uploading, 0.3);
        composer.Attachments.Add(uploading);
        composer.SendState.ShouldBe(AiSendState.Blocked);
        rig.Send.CurrentName.ShouldBe("Wait for uploads to finish");
        uploading.Status = AiAttachmentStatus.Done;
        composer.SendState.ShouldBe(AiSendState.Ready);

        composer.IsGenerating = true;
        composer.SendState.ShouldBe(AiSendState.Generating);
        composer.Classes.Contains(":generating").ShouldBeTrue();
        rig.Send.CurrentName.ShouldBe("Stop generating");
        composer.IsGenerating = false;
        composer.SendState.ShouldBe(AiSendState.Ready);
    }

    [AvaloniaFact]
    public void Over_The_Limit_The_Text_Is_Never_Cut()
    {
        using var rig = new AiComposerRig(c => c.MaxLength = 10);
        rig.Focus();
        rig.Type("twelve chars");
        rig.Composer.Text.ShouldBe("twelve chars");
        rig.Key(PhysicalKey.Enter);
        rig.Sent.ShouldBeEmpty();
        rig.Composer.Text.ShouldBe("twelve chars");
    }

    #endregion

    #region Sending

    [AvaloniaFact]
    public void Enter_Sends_The_Text_Tokens_Ready_Attachments_Model_Effort_And_Tools_Then_Clears()
    {
        var command = new Recorder();
        using var rig = new AiComposerRig(c => c.SendCommand = command);
        var composer = rig.Composer;
        var ready = AiComposerSamples.Attachment("usage-oct.csv", 12 * 1024);
        var done = AiComposerSamples.Attachment("roadmap.pdf", 4096, AiAttachmentStatus.Done, 1);
        var failed = AiComposerSamples.Attachment("big.mov", 99, AiAttachmentStatus.Error, error: "Over 30 MB");
        composer.Attachments.AddRange([ready, done, failed]);
        composer.Tools[1].IsChecked = true;
        composer.SetPrompt(AiComposerSamples.Suggestions[0].Prompt);
        rig.Pump();
        rig.Key(PhysicalKey.Enter);

        var message = rig.Sent.Single();
        message.Text.ShouldBe("/recap @Sprint 15 review and list what slipped since the previous sprint");
        message.Tokens.Select(t => t.Id).ShouldBe(["recap", "doc-sprint-15"]);
        message.Attachments.ShouldBe([ready, done]);
        message.Model.ShouldBe(composer.Models[0]);
        message.Effort.ShouldBe(ModelEffort.Medium);
        message.Tools.ShouldBe(new Dictionary<string, bool> { ["web-search"] = false, ["extended-thinking"] = true });
        command.Calls.ShouldBe([message]);
        rig.Announcement.ShouldBe("Message sent");

        composer.Text.ShouldBe(string.Empty);
        composer.Value.Tokens.ShouldBeEmpty();
        composer.Attachments.ShouldBeEmpty();
        // The sent attachments leave quietly; the failed one left behind is a removal.
        rig.Removed.ShouldBe([failed]);
        composer.Tools[1].IsChecked.ShouldBeTrue();
        composer.SelectedModel.ShouldBe(composer.Models[0]);
        composer.SendState.ShouldBe(AiSendState.Empty);
    }

    [AvaloniaFact]
    public void Shift_Enter_Adds_A_Line_And_Does_Not_Send()
    {
        using var rig = new AiComposerRig();
        rig.Focus();
        rig.Type("first");
        rig.Key(PhysicalKey.Enter, RawInputModifiers.Shift);
        rig.Type("second");
        rig.Composer.Text.ShouldBe("first\nsecond");
        rig.Sent.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Enter_During_An_Ime_Composition_Belongs_To_The_Ime()
    {
        using var rig = new AiComposerRig();
        rig.Focus();
        rig.Type("ni");
        rig.Editor.Box!.Presenter!.PreeditText = "hao";
        rig.Pump();
        rig.Editor.IsComposing.ShouldBeTrue();
        rig.Key(PhysicalKey.Enter);
        rig.Sent.ShouldBeEmpty();
        rig.Editor.Box.Presenter.PreeditText = null;
        rig.Pump();
        rig.Key(PhysicalKey.Enter);
        rig.Sent.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void While_Generating_The_Box_Stays_Editable_And_Enter_Does_Not_Send()
    {
        using var rig = new AiComposerRig(c => c.IsGenerating = true);
        rig.Focus();
        rig.Type("next question");
        rig.Composer.Text.ShouldBe("next question");
        rig.Key(PhysicalKey.Enter);
        rig.Sent.ShouldBeEmpty();
        rig.Composer.Text.ShouldBe("next question");
        rig.Composer.IsGenerating = false;
        rig.Key(PhysicalKey.Enter);
        rig.Sent.Single().Text.ShouldBe("next question");
    }

    [AvaloniaFact]
    public void The_Send_Button_Sends_And_While_Generating_Stops()
    {
        var stop = new Recorder();
        using var rig = new AiComposerRig(c => c.StopCommand = stop);
        rig.Composer.Text = "Plan it";
        rig.Click(rig.Send);
        rig.Sent.Single().Text.ShouldBe("Plan it");

        rig.Composer.IsGenerating = true;
        rig.Composer.GenerationProgress = 0.4;
        rig.Send.Progress.ShouldBe(0.4);
        rig.Click(rig.Send);
        rig.Stops.ShouldBe(1);
        stop.Calls.ShouldBe([null]);
        rig.Announcement.ShouldBe("Stopped");
        // The host ends the reply.
        rig.Composer.IsGenerating.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Pressing_An_Empty_Or_Blocked_Send_Button_Does_Nothing()
    {
        using var rig = new AiComposerRig(c => c.MaxLength = 3);
        rig.Click(rig.Send);
        rig.Composer.Text = "four";
        rig.Click(rig.Send);
        rig.Sent.ShouldBeEmpty();
        rig.Stops.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Escape_Stops_A_Reply_When_No_List_Is_Open()
    {
        using var rig = new AiComposerRig();
        rig.Focus();
        rig.Key(PhysicalKey.Escape);
        rig.Stops.ShouldBe(0);
        rig.Composer.IsGenerating = true;
        rig.Key(PhysicalKey.Escape);
        rig.Stops.ShouldBe(1);
    }

    [AvaloniaFact]
    public void A_Composer_Without_Models_Sends_No_Model_Or_Effort()
    {
        using var rig = new AiComposerRig(c => c.Models.Clear());
        rig.Composer.ModelPillControl!.IsVisible.ShouldBeFalse();
        rig.Composer.Text = "hi";
        rig.Composer.TrySend().ShouldBeTrue();
        rig.Sent.Single().Model.ShouldBeNull();
        rig.Sent.Single().Effort.ShouldBeNull();
    }

    #endregion
}
