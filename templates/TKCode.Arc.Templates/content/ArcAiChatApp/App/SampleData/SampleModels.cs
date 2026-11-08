using Avalonia.Media;
using TKCode.Arc;

namespace ArcStarterApp.SampleData;

/// <summary>
/// The models the chat offers. These two only name the sample responder's two behaviors (a quick reply; a reply that
/// thinks first); list the models of your provider instead.
/// </summary>
public static class SampleModels
{
    public const string Quick = "sample-quick";
    public const string Thinking = "sample-thinking";

    public static IReadOnlyList<AiModelProvider> Providers() =>
    [
        new()
        {
            Id = "sample",
            Name = "Sample",
            Mark = Geometry.Parse("M12 2l2.6 6.9L22 9.3l-5.6 4.8L18.2 22 12 18.1 5.8 22l1.8-7.9L2 9.3l7.4-.4z"),
        },
    ];

    public static IReadOnlyList<AiModel> Models() =>
    [
        new()
        {
            Id = Quick, Name = "Sample Quick", ShortName = "Quick", ProviderId = "sample",
            Blurb = "Answers at once.", Description = "Streams a reply without reasoning.",
            ContextTokens = 32_000, MaxOutputTokens = 4_000,
        },
        new()
        {
            Id = Thinking, Name = "Sample Thinking", ShortName = "Thinking", ProviderId = "sample",
            Blurb = "Thinks before answering.", Description = "Streams its reasoning, then the reply.",
            ContextTokens = 128_000, MaxOutputTokens = 16_000,
        },
    ];

    public static IReadOnlyList<AiChatSuggestion> Suggestions() =>
    [
        new() { Label = "Explain how this app is built", Icon = ArcIcons.Sparkles },
        new() { Label = "Connect a real model", Prompt = "How do I connect a real model to this app?", Icon = ArcIcons.Link },
        new() { Label = "Write a release note", Prompt = "Write a short release note for version 1.1.", Icon = ArcIcons.FileText },
    ];
}
