using ArcStarterApp.Models;

namespace ArcStarterApp.Services;

/// <summary>Keeps the conversations between runs. Replace it to keep them on a server, or to keep none.</summary>
public interface IConversationStore
{
    ChatArchive Load();

    void Save(ChatArchive archive);
}
