namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>Invented sample data for the chat's scroller, suggestions and conversation menu (ai-chat spec section 10).</summary>
internal static class AiChatScrollSamples
{
    public const string Greeting = "What should we look at today?";
    public const string NewChat = "New chat";
    public const string Untitled = "Untitled chat";
    public const string Today = "Today";
    public const string Previous = "Previous 7 days";

    public static AiChatSuggestion[] Suggestions() =>
    [
        new() { Label = "Find the slowest step in the nightly build", Icon = ArcIcons.Timer },
        new() { Label = "Compare this week's test failures to last week", Icon = ArcIcons.Search },
        new()
        {
            Label = "Check disk usage on ci-runner-7",
            Prompt = "Check disk usage on ci-runner-7 and tell me what is filling it up.",
            Icon = ArcIcons.Activity
        },
        new() { Label = "Draft the 4.3 release checklist", Icon = ArcIcons.Database }
    ];

    public static AiChatConversation[] Conversations() =>
    [
        new() { Id = "nightly", Title = "Speed up the nightly build", Group = Today },
        new() { Id = "bucket", Title = "Rename the staging bucket", Group = Today },
        new() { Id = "notes", Title = "Write the 4.2 release notes", Group = Today },
        new() { Id = "survey", Title = "Summarize the support survey", Group = Previous },
        new() { Id = "flaky", Title = "Flaky login test", Group = Previous }
    ];

    /// <summary>A conversation without a title whose first message gives one.</summary>
    public static AiChatConversation FromFirstMessage(string text, string? group = Today)
    {
        var conversation = new AiChatConversation { Id = "draft", Group = group };
        conversation.Turns.Add(new AiChatTurn(new AiChatUserMessage { Text = text }, new AiChatReply()));
        return conversation;
    }
}
