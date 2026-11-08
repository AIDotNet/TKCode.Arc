using System.Runtime.CompilerServices;
using TKCode.Arc;

namespace ArcStarterApp.SampleData;

/// <summary>
/// Answers without a model: it streams a prepared Markdown reply a few words at a time, after streaming some reasoning
/// when the "Sample Thinking" model is chosen. Replace it with a responder over your model client; with
/// Microsoft.Extensions.AI (dotnet add package Microsoft.Extensions.AI plus the package of your provider):
/// <code>
/// public sealed class ChatClientResponder(IChatClient client) : IAiChatResponder
/// {
///     public async IAsyncEnumerable&lt;AiChatStreamEvent&gt; RespondAsync(AiChatRequest request,
///         [EnumeratorCancellation] CancellationToken cancellation)
///     {
///         var messages = request.Messages.Select(m =>
///             new ChatMessage(m.Role == AiChatRole.User ? ChatRole.User : ChatRole.Assistant, m.Content));
///         var options = new ChatOptions { ModelId = request.Model?.Id };
///         await foreach (var update in client.GetStreamingResponseAsync(messages, options, cancellation))
///         {
///             yield return new AiChatTextDelta(update.Text);
///         }
///     }
/// }
/// </code>
/// and register it in Services/AppServices.cs. The chat cancels <c>cancellation</c> when the person presses Stop.
/// </summary>
public sealed class SampleResponder : IAiChatResponder
{
    /// <summary>The pause between two pieces of the reply.</summary>
    public static TimeSpan Pace { get; set; } = TimeSpan.FromMilliseconds(30);

    public async IAsyncEnumerable<AiChatStreamEvent> RespondAsync(AiChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        var prompt = request.Messages.Count > 0 ? request.Messages[^1].Content : string.Empty;
        if (request.Model?.Id == SampleModels.Thinking)
        {
            var started = DateTime.UtcNow;
            foreach (var piece in Pieces("The person asked: \"" + prompt.Trim() + "\". There is no model behind this app yet, " +
                                         "so the most useful answer explains where a real one plugs in."))
            {
                await Task.Delay(Pace, cancellation);
                yield return new AiChatReasoningDelta(piece);
            }
            yield return new AiChatReasoningEnd(DateTime.UtcNow - started);
        }
        foreach (var piece in Pieces(Reply(prompt)))
        {
            await Task.Delay(Pace, cancellation);
            yield return new AiChatTextDelta(piece);
        }
    }

    private static string Reply(string prompt) => prompt.Contains("release note", StringComparison.OrdinalIgnoreCase)
        ? """
          ## Version 1.1

          - **Faster start-up**: the window opens before the conversations finish loading.
          - **Attachments**: drop files on the message box to send them along.
          - **Fixed**: a long reply no longer stops following when the window is resized.
          """
        : """
          This reply comes from `SampleResponder`, which streams prepared text: **no model is connected yet**.

          To answer with a real model:

          1. Add a client, for example `Microsoft.Extensions.AI` and the package of your provider.
          2. Write an `IAiChatResponder` that turns the request into the client's messages and yields
             `AiChatTextDelta` for each streamed piece (the comment on `SampleResponder` has one).
          3. Register it in `Services/AppServices.cs` in place of `SampleResponder`.

          ```csharp
          services.AddSingleton<IAiChatResponder, ChatClientResponder>();
          ```

          The conversations are kept in `conversations.json` beside the settings, so they are here again next time.
          """;

    // A few words at a time, keeping the spaces and line breaks.
    private static IEnumerable<string> Pieces(string text)
    {
        var start = 0;
        var words = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is ' ' or '\n' && ++words == 3)
            {
                yield return text[start..(i + 1)];
                start = i + 1;
                words = 0;
            }
        }
        if (start < text.Length)
        {
            yield return text[start..];
        }
    }
}
