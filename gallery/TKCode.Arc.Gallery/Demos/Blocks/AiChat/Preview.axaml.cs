using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiChatPreviewDemo : UserControl
{
    public AiChatPreviewDemo()
    {
        InitializeComponent();
        Chat.Providers.AddRange(AiChatDemoData.Providers());
        Chat.Models.AddRange(AiChatDemoData.Models());
        Chat.Suggestions.AddRange(AiChatDemoData.Suggestions());

        var current = AiChatDemoData.NightlyBuild(Chat.Models[0].Id);
        Chat.Conversations.Add(current);
        Chat.Conversations.AddRange(AiChatDemoData.History());
        Chat.CurrentConversation = current;

        // The responder writes each reply as events; the chat shows them as they arrive and lets the person stop a reply.
        Chat.Responder = new AiChatDemoData.ScriptedResponder();
    }
}
