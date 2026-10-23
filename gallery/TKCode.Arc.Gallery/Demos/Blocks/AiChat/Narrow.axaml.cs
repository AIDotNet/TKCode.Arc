using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiChatNarrowDemo : UserControl
{
    public AiChatNarrowDemo()
    {
        InitializeComponent();
        Chat.Providers.AddRange(AiChatDemoData.Providers());
        Chat.Models.AddRange(AiChatDemoData.Models());
        var current = AiChatDemoData.NightlyBuild(Chat.Models[0].Id);
        Chat.Conversations.Add(current);
        Chat.CurrentConversation = current;
        Chat.Responder = new AiChatDemoData.ScriptedResponder();
    }
}
