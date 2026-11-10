using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Support;

/// <summary>SupportConversation: the sample, replies and notes, saved replies, status and the narrow layout.</summary>
public class SupportConversationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private static (SupportConversation View, Avalonia.Controls.Window Window) Show(double width = 1100)
    {
        var view = new SupportConversation { Clock = () => Now };
        var window = TestHelpers.Show(view);
        window.Width = width;
        window.Height = 800;
        Dispatcher.UIThread.RunJobs();
        return (view, window);
    }

    [AvaloniaFact]
    public void The_Sample_Fills_The_Thread_The_Customer_And_The_Notes()
    {
        var (view, window) = Show();
        try
        {
            view.Messages.Count.ShouldBe(4);
            view.Customer!.Name.ShouldBe("Mara Lindqvist");
            view.Notes.Count.ShouldBe(1);
            view.CannedReplies.Count.ShouldBe(3);
            view.Thread.CurrentUserId.ShouldBe("agent");
            view.Panel.NoteCards.Count.ShouldBe(1);
            view.IsNarrow.ShouldBeFalse();
            view.IsDetailsShown.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Reply_Joins_The_Thread_And_Moves_Open_To_Pending()
    {
        var (view, window) = Show();
        try
        {
            SupportReplyEventArgs? sent = null;
            SupportStatusChangedEventArgs? status = null;
            view.ReplySent += (_, e) => sent = e;
            view.StatusChanged += (_, e) => status = e;
            view.Send().ShouldBeFalse("the composer is empty");
            view.Composer.Text = "  On it!  ";
            view.SendButton.IsEnabled.ShouldBeTrue();
            view.Send().ShouldBeTrue();
            view.Messages[^1].Text.ShouldBe("On it!");
            view.Messages[^1].AuthorId.ShouldBe("agent");
            view.Composer.Text.ShouldBe(string.Empty);
            sent!.Mode.ShouldBe(SupportComposerMode.Reply);
            view.Status.ShouldBe(SupportConversationStatus.Pending);
            status!.OldStatus.ShouldBe(SupportConversationStatus.Open);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Note_Stays_With_The_Team()
    {
        var (view, window) = Show();
        try
        {
            var messages = view.Messages.Count;
            view.ComposerMode = SupportComposerMode.Note;
            view.Classes.Contains(SupportConversation.NotePseudoClass).ShouldBeTrue();
            view.Composer.Text = "Refund approved by finance.";
            view.Composer.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);
            view.Messages.Count.ShouldBe(messages);
            view.Notes[^1].Text.ShouldBe("Refund approved by finance.");
            view.Notes[^1].Author.ShouldBe("You");
            view.Panel.NoteCards.Count.ShouldBe(2);
            view.Status.ShouldBe(SupportConversationStatus.Open);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Saved_Replies_Open_On_Slash_And_Fill_In_The_Name()
    {
        var (view, window) = Show();
        try
        {
            view.Composer.Text = "/";
            Dispatcher.UIThread.RunJobs();
            view.Picker.IsOpen.ShouldBeTrue();
            view.Composer.Text.ShouldBe(string.Empty);
            view.Picker.Pick("Refund issued");
            view.Composer.Text!.ShouldStartWith("Hi Mara, I have refunded");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Puts_The_Details_Behind_A_Toggle()
    {
        var (view, window) = Show(width: 600);
        try
        {
            view.IsNarrow.ShouldBeTrue();
            view.Classes.Contains(SupportConversation.NarrowPseudoClass).ShouldBeTrue();
            view.SideFrame.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
