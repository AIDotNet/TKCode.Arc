using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ArcStarterApp.SampleData;
using ArcStarterApp.Services;
using ArcStarterApp.ViewModels;
using ArcStarterApp.Views;
using TKCode.Arc;

namespace ArcStarterApp.Tests;

public class ChatTests
{
    public ChatTests()
    {
        // Every test starts with no saved conversations.
        TestApp.Conversations.Json = null;
    }

    [AvaloniaFact]
    public async Task A_message_gets_a_streamed_reply_and_is_saved()
    {
        SampleResponder.Pace = TimeSpan.Zero;
        var window = new MainWindow { DataContext = App.Services.GetRequiredService<MainViewModel>() };
        window.Show();
        var chat = window.GetVisualDescendants().OfType<AiChat>().Single();

        chat.Send("Hello there");
        var stop = DateTime.UtcNow.AddSeconds(10);
        while (chat.IsBusy && DateTime.UtcNow < stop)
        {
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }

        var conversation = Assert.Single(chat.Conversations);
        Assert.Equal("Hello there", conversation.Title);
        Assert.Contains("SampleResponder", conversation.Turns[0].SelectedVersion!.Reply.Text);
        Assert.Contains("Hello there", TestApp.Conversations.Json);
    }

    [AvaloniaFact]
    public void Saved_conversations_come_back()
    {
        var store = App.Services.GetRequiredService<IConversationStore>();
        var viewModel = App.Services.GetRequiredService<MainViewModel>();
        var conversation = new AiChatConversation { Id = "c1", Title = "Kept" };
        conversation.Turns.Add(new AiChatTurn(new AiChatUserMessage { Text = "Question" }, new AiChatReply { Text = "Answer" }));

        viewModel.Save([conversation], null);
        var restored = viewModel.Restore();

        Assert.Equal("Kept", Assert.Single(restored).Title);
        Assert.Equal("Answer", restored[0].Turns[0].SelectedVersion!.Reply.Text);
        Assert.NotNull(store.Load().Conversations.Single());
    }
}
