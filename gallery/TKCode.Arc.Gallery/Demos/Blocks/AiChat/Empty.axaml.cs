using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiChatEmptyDemo : UserControl
{
    public AiChatEmptyDemo()
    {
        InitializeComponent();
        Chat.Providers.AddRange(AiChatDemoData.Providers());
        Chat.Models.AddRange(AiChatDemoData.Models());

        // With no current conversation the chat is empty; pressing a suggestion sends its prompt and creates one.
        Chat.Suggestions.AddRange(AiChatDemoData.Suggestions());
        Chat.Responder = new AiChatDemoData.ScriptedResponder();
        Chat.ConversationCreated += (_, e) => Chat.Conversations.Add(e.Conversation);
    }
}
