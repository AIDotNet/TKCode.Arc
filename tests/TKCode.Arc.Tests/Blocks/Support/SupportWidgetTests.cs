using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Support;

/// <summary>SupportWidget: open and close, search, articles, starting a chat and the simulated reply, history and Escape.</summary>
public class SupportWidgetTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private static (SupportWidget Widget, Avalonia.Controls.Window Window) Show(bool open = true)
    {
        var widget = new SupportWidget
        {
            Clock = () => Now,
            Schedule = static (_, action) => action(),
            IsOpen = open,
        };
        var window = TestHelpers.Show(widget);
        window.Width = 900;
        window.Height = 800;
        Dispatcher.UIThread.RunJobs();
        return (widget, window);
    }

    [AvaloniaFact]
    public void The_Launcher_Opens_And_Closes_The_Panel()
    {
        var (widget, window) = Show(open: false);
        try
        {
            widget.IsPanelShown.ShouldBeFalse();
            widget.Launcher.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            widget.IsOpen.ShouldBeTrue();
            widget.IsPanelShown.ShouldBeTrue();
            widget.Screen.ShouldBe(SupportWidgetScreen.Home);
            widget.Close();
            widget.IsPanelShown.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Home_Lists_Recent_Articles_And_Search_Filters_Them()
    {
        var (widget, window) = Show();
        try
        {
            widget.Articles.Count.ShouldBe(5);
            widget.HomeRows.Count.ShouldBe(4);
            widget.Search!.Text = "refund";
            widget.HomeRows.Count.ShouldBe(1);
            widget.Search.Text = "nothing like this";
            widget.HomeRows.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Chat_Gets_The_Visitors_Message_And_A_Reply()
    {
        var (widget, window) = Show();
        try
        {
            SupportWidgetMessageEventArgs? sent = null;
            widget.MessageSent += (_, e) => sent = e;
            var conversation = widget.StartConversation();
            widget.Screen.ShouldBe(SupportWidgetScreen.Chat);
            widget.Chat.ShouldNotBeNull();
            widget.Conversations[0].ShouldBe(conversation);
            var before = conversation.Messages.Count;

            widget.SendMessage("My booking did not show up");
            sent!.Message.Text.ShouldBe("My booking did not show up");
            conversation.Title.ShouldBe("My booking did not show up");
            conversation.Messages.Count.ShouldBe(before + 2, "the visitor's message and the simulated reply");
            conversation.Messages[^1].AuthorId.ShouldBe(widget.Agents[0].Id);
            widget.Typing.ShouldBeEmpty();
            widget.LastAnnouncement!.ShouldContain("replied");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Goes_Back_From_An_Article_Then_Closes()
    {
        var (widget, window) = Show();
        try
        {
            widget.OpenArticle(widget.Articles[0]);
            widget.Screen.ShouldBe(SupportWidgetScreen.Article);
            widget.Launcher.Focus(NavigationMethod.Tab);
            var page = (Avalonia.Controls.Control)widget.Page!;
            page.Focusable = true;
            page.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            widget.Screen.ShouldBe(SupportWidgetScreen.Home);
            widget.IsOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Reply_While_Away_Marks_The_Conversation_Unread()
    {
        var (widget, window) = Show();
        try
        {
            var later = new List<Action>();
            widget.Schedule = (_, action) => later.Add(action);
            widget.StartConversation();
            var conversation = widget.OpenConversation!;
            widget.SendMessage("Hello");
            widget.GoBack();
            while (later.Count > 0)
            {
                var next = later[0];
                later.RemoveAt(0);
                next();
            }
            conversation.IsUnread.ShouldBeTrue();
            widget.OpenChat(conversation);
            conversation.IsUnread.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
