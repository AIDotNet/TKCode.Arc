using System.Text;

namespace TKCode.Arc;

/// <summary>
/// The title a conversation is listed under: its <see cref="AiChatConversation.Title"/>, else the first six words of its
/// first message (Arc: "Draft a short note to the"), else the untitled text. The chat computes the title pill's text the
/// same way.
/// </summary>
internal static class AiConversationTitle
{
    /// <summary>How many words of the first message make a title.</summary>
    internal const int WordCount = 6;

    public static string Of(AiChatConversation conversation, string? untitled)
    {
        var title = conversation.Title;
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }
        var first = conversation.Turns.Count > 0 ? conversation.Turns[0].SelectedVersion?.Prompt?.Text : null;
        var words = FirstWords(first, WordCount);
        return words.Length > 0 ? words : untitled ?? string.Empty;
    }

    /// <summary>The first <paramref name="count"/> words of <paramref name="text"/>, one space apart, without code marks.</summary>
    public static string FirstWords(string? text, int count)
    {
        if (string.IsNullOrWhiteSpace(text) || count <= 0)
        {
            return string.Empty;
        }
        var builder = new StringBuilder();
        var words = 0;
        var inWord = false;
        foreach (var c in text)
        {
            if (c == '`')
            {
                continue;
            }
            if (char.IsWhiteSpace(c))
            {
                if (inWord)
                {
                    inWord = false;
                    words++;
                    if (words == count)
                    {
                        break;
                    }
                }
                continue;
            }
            if (!inWord && builder.Length > 0)
            {
                builder.Append(' ');
            }
            inWord = true;
            builder.Append(c);
        }
        return builder.ToString();
    }
}
