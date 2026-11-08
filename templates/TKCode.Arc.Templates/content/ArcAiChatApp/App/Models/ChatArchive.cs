using System.Text.Json.Serialization;

namespace ArcStarterApp.Models;

/// <summary>What is kept between runs: the conversations (newest first) and the model chosen last.</summary>
public sealed class ChatArchive
{
    public string? ModelId { get; set; }

    public List<SavedConversation> Conversations { get; set; } = [];
}

/// <summary>A conversation as saved: its title and the shown version of each turn.</summary>
public sealed class SavedConversation
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? ModelId { get; set; }

    public List<SavedTurn> Turns { get; set; } = [];
}

/// <summary>One message and its reply.</summary>
public sealed class SavedTurn
{
    public string Prompt { get; set; } = string.Empty;

    public string Reply { get; set; } = string.Empty;

    public string? Reasoning { get; set; }

    public string? ModelId { get; set; }
}

/// <summary>The archive's serializer, generated at build time (no reflection, trimming and AOT safe).</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ChatArchive))]
internal sealed partial class ChatArchiveJsonContext : JsonSerializerContext
{
}
