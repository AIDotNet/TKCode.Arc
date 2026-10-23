using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiChatVersionsDemo : UserControl
{
    public AiChatVersionsDemo()
    {
        InitializeComponent();
        Chat.Providers.AddRange(AiChatDemoData.Providers());
        Chat.Models.AddRange(AiChatDemoData.Models());
        var modelId = Chat.Models[0].Id;

        // A turn with two versions: the first answer and a retry. Press the arrows in the action bar to switch; the turns after a
        // version belong to it. Retry or edit a message to add another version.
        var turn = new AiChatTurn(
            new AiChatUserMessage { Text = "Give me one sentence on why builds slow down over time." },
            new AiChatReply { ModelId = modelId, Text = "Caches stop matching, dependencies grow and test suites accumulate." });
        turn.Versions.Add(new AiChatTurnVersion
        {
            Prompt = new AiChatUserMessage { Text = "Give me one sentence on why builds slow down over time." },
            Reply = new AiChatReply { ModelId = modelId, Text = "Because every added dependency and test is paid for on every single run." }
        });
        turn.SelectedVersionIndex = 1;

        var conversation = new AiChatConversation { Id = "versions", Title = "Why builds slow down", ModelId = modelId };
        conversation.Turns.Add(turn);
        Chat.Conversations.Add(conversation);
        Chat.CurrentConversation = conversation;
        Chat.Responder = new AiChatDemoData.ScriptedResponder();
    }
}
