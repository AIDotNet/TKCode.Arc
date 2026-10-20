using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using static TKCode.Arc.Tests.Buttons.ButtonsTestSupport;

namespace TKCode.Arc.Tests.Buttons;

public class ExpandingButtonGroupTests
{
    private static ExpandingButtonGroup Group(out ExpandingButtonGroupItem reply, out ExpandingButtonGroupItem archive, out ExpandingButtonGroupItem delete)
    {
        reply = new ExpandingButtonGroupItem { Label = "Reply", Icon = ArcIcons.Reply };
        archive = new ExpandingButtonGroupItem { Label = "Archive", Icon = ArcIcons.Archive, DoneLabel = "Archived" };
        delete = new ExpandingButtonGroupItem { Label = "Delete", Icon = ArcIcons.Trash2, Tone = ExpandingButtonGroupTone.Danger };
        return new ExpandingButtonGroup { Label = "Message actions", Items = { reply, archive, delete } };
    }

    [AvaloniaFact]
    public void One_Action_Is_Expanded_At_Rest()
    {
        var group = Group(out var reply, out var archive, out var delete);
        var window = TestHelpers.Show(group);
        try
        {
            group.ExpandedItem.ShouldBe(reply);
            reply.IsExpanded.ShouldBeTrue();
            reply.Bounds.Width.ShouldBeGreaterThan(60);
            archive.Bounds.Width.ShouldBe(36);
            delete.Bounds.Width.ShouldBe(36);
            reply.Bounds.Height.ShouldBe(36);
            Peer(group).GetAutomationControlType().ShouldBe(AutomationControlType.ToolBar);
            AutomationProperties.GetName(archive).ShouldBe("Archive");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Focus_Moves_The_Expansion_And_Wraps()
    {
        var group = Group(out var reply, out var archive, out var delete);
        var window = TestHelpers.Show(group);
        try
        {
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            reply.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            archive.IsFocused.ShouldBeTrue();
            group.ExpandedItem.ShouldBe(archive);
            archive.Bounds.Width.ShouldBeGreaterThan(60);
            reply.Bounds.Width.ShouldBe(36);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            delete.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            reply.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Action_Confirms_In_Place_Then_Returns()
    {
        var invoked = new List<string>();
        var group = Group(out _, out var archive, out _);
        group.ConfirmDuration = TimeSpan.FromMilliseconds(80);
        group.ActionInvoked += (_, e) => invoked.Add(e.Item.Label);
        var window = TestHelpers.Show(group);
        try
        {
            var widthBefore = 0.0;
            archive.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            widthBefore = archive.Bounds.Width;
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            invoked.ShouldBe(["Archive"]);
            archive.IsDone.ShouldBeTrue();
            archive.Classes.ShouldContain(":done");
            archive.Word.ShouldBe("Archived");
            // The slot already reserved the longer word.
            archive.Bounds.Width.ShouldBe(widthBefore, 0.5);
            Part<ButtonsLiveRegion>(group, ExpandingButtonGroup.StatusPart).Message.ShouldBe("Archived");
            WaitFor(() => !archive.IsDone).ShouldBeTrue();
            archive.Word.ShouldBe("Archive");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Actions_Expand_But_Do_Not_Run()
    {
        var runs = 0;
        var locked = new ExpandingButtonGroupItem
        {
            Label = "Lock",
            Icon = ArcIcons.Lock,
            IsDisabled = true,
            AsyncAction = () =>
            {
                runs++;
                return Task.FromResult(true);
            }
        };
        var group = new ExpandingButtonGroup { Items = { new ExpandingButtonGroupItem { Label = "Open", Icon = ArcIcons.FolderOpen }, locked } };
        var window = TestHelpers.Show(group);
        try
        {
            locked.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            locked.IsExpanded.ShouldBeTrue();
            locked.Classes.ShouldContain(":unavailable");
            Peer(locked).IsEnabled().ShouldBeFalse();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            runs.ShouldBe(0);
            // The resting action is the first enabled one.
            group.DefaultExpandedItem.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Busy_And_Declined_Actions_Skip_The_Confirmation()
    {
        var gate = new TaskCompletionSource<bool>();
        var publish = new ExpandingButtonGroupItem { Label = "Publish", Icon = ArcIcons.Upload, DoneLabel = "Published", AsyncAction = () => gate.Task };
        var comment = new ExpandingButtonGroupItem { Label = "Comment", Icon = ArcIcons.MessageCircle };
        var group = new ExpandingButtonGroup { Items = { comment, publish }, DefaultExpandedItem = publish };
        var window = TestHelpers.Show(group);
        try
        {
            group.ExpandedItem.ShouldBe(publish);
            _ = group.ActivateAsync(publish);
            Dispatcher.UIThread.RunJobs();
            publish.IsBusy.ShouldBeTrue();
            publish.Classes.ShouldContain(":busy");
            gate.SetResult(false);
            WaitFor(() => !publish.IsBusy).ShouldBeTrue();
            publish.IsDone.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Small_Size_Is_28px()
    {
        var group = Group(out var reply, out var archive, out _);
        group.Size = ControlSize.Small;
        var window = TestHelpers.Show(group);
        try
        {
            reply.Bounds.Height.ShouldBe(28);
            archive.Bounds.Width.ShouldBe(28);
        }
        finally
        {
            window.Close();
        }
    }
}
