using System.Text.Json;
using ArcStarterApp.Models;
using ArcStarterApp.Settings;

namespace ArcStarterApp.Services;

/// <summary>
/// Keeps the conversations as JSON in <see cref="Store"/>: on the desktop <c>conversations.json</c> beside the settings
/// file (<see cref="DefaultPath"/>); tests keep them in memory.
/// </summary>
public sealed class ConversationStore : IConversationStore
{
    /// <summary><c>&lt;application data&gt;/ArcStarterApp/conversations.json</c>.</summary>
    public static string DefaultPath { get; } =
        Path.Combine(Path.GetDirectoryName(AppSettings.DefaultPath)!, "conversations.json");

    /// <summary>Where the JSON goes; set it before the app is built to keep the conversations elsewhere.</summary>
    public static ISettingsStore Store { get; set; } = new FileSettingsStore(DefaultPath);

    public ChatArchive Load()
    {
        try
        {
            if (Store.Read() is { Length: > 0 } json &&
                JsonSerializer.Deserialize(json, ChatArchiveJsonContext.Default.ChatArchive) is { } archive)
            {
                return archive;
            }
        }
        catch (JsonException)
        {
            // An unreadable file must not stop the app: start empty and overwrite it on the next save.
        }
        return new ChatArchive();
    }

    public void Save(ChatArchive archive) =>
        Store.Write(JsonSerializer.Serialize(archive, ChatArchiveJsonContext.Default.ChatArchive));
}
