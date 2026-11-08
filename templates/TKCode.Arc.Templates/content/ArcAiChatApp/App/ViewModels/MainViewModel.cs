using ArcStarterApp.Models;
using ArcStarterApp.SampleData;
using ArcStarterApp.Services;
using TKCode.Arc;

namespace ArcStarterApp.ViewModels;

/// <summary>
/// The chat: the responder that answers, the models it offers, and the conversations kept between runs. The chat block
/// owns its list of conversations while the app runs (Views/MainView.axaml.cs fills it from <see cref="Restore"/> and
/// hands it to <see cref="Save"/> after every reply).
/// </summary>
public sealed class MainViewModel
{
    private readonly IConversationStore _store;

    public MainViewModel(IAiChatResponder responder, IConversationStore store)
    {
        Responder = responder;
        _store = store;
    }

    public IAiChatResponder Responder { get; }

    public IReadOnlyList<AiModelProvider> Providers { get; } = SampleModels.Providers();

    public IReadOnlyList<AiModel> Models { get; } = SampleModels.Models();

    public IReadOnlyList<AiChatSuggestion> Suggestions { get; } = SampleModels.Suggestions();

    /// <summary>The model chosen last time, or null for the first one.</summary>
    public AiModel? SavedModel { get; private set; }

    /// <summary>The saved conversations, newest first, as the chat block's objects.</summary>
    public IReadOnlyList<AiChatConversation> Restore()
    {
        var archive = _store.Load();
        SavedModel = Models.FirstOrDefault(m => m.Id == archive.ModelId);
        var conversations = new List<AiChatConversation>();
        foreach (var saved in archive.Conversations)
        {
            var conversation = new AiChatConversation { Id = saved.Id, Title = saved.Title, ModelId = saved.ModelId };
            foreach (var turn in saved.Turns)
            {
                var reply = new AiChatReply { Text = turn.Reply, ModelId = turn.ModelId, Status = AiChatReplyStatus.Done };
                if (turn.Reasoning is { Length: > 0 } reasoning)
                {
                    reply.Reasoning = reasoning;
                }
                conversation.Turns.Add(new AiChatTurn(new AiChatUserMessage { Text = turn.Prompt }, reply));
            }
            conversations.Add(conversation);
        }
        return conversations;
    }

    /// <summary>Saves <paramref name="conversations"/> (the shown version of each turn) and the chosen model.</summary>
    public void Save(IEnumerable<AiChatConversation> conversations, AiModel? model)
    {
        var archive = new ChatArchive { ModelId = model?.Id };
        foreach (var conversation in conversations)
        {
            var saved = new SavedConversation { Id = conversation.Id, Title = conversation.Title, ModelId = conversation.ModelId };
            foreach (var turn in conversation.Turns)
            {
                if (turn.SelectedVersion is not { } version)
                {
                    continue;
                }
                saved.Turns.Add(new SavedTurn
                {
                    Prompt = version.Prompt.Text,
                    Reply = version.Reply.Text,
                    Reasoning = string.IsNullOrEmpty(version.Reply.Reasoning) ? null : version.Reply.Reasoning,
                    ModelId = version.Reply.ModelId,
                });
            }
            if (saved.Turns.Count > 0)
            {
                archive.Conversations.Add(saved);
            }
        }
        _store.Save(archive);
    }

    /// <summary>A title for a new conversation: the first words of its first message.</summary>
    public static string TitleFor(string prompt)
    {
        var words = prompt.Split((char[]?)null, 7, StringSplitOptions.RemoveEmptyEntries);
        var title = string.Join(' ', words.Take(6));
        return words.Length > 6 ? title + "…" : title;
    }
}
