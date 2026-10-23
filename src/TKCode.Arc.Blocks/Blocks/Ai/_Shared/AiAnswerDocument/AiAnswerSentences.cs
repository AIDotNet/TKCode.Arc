namespace TKCode.Arc;

/// <summary>
/// One sentence of a text: the characters it lights (from its first to its last visible character, the closing
/// punctuation included, the citations after it not) and the citations that close it.
/// </summary>
/// <param name="Start">Index of the first character.</param>
/// <param name="Length">Number of characters lit.</param>
/// <param name="Citations">Indices (into the text) of the citations that belong to it, in order.</param>
internal sealed record AiAnswerSentence(int Start, int Length, List<int> Citations)
{
    /// <summary>The end of the lit range.</summary>
    public int End => Start + Length;
}

/// <summary>
/// Splits a text into sentences for citation lighting. Pure: the text is the displayed text of one paragraph, heading,
/// list item or table cell, with each embedded object (a citation pill or an inline code span) as one placeholder
/// character; the caller says which characters are citations.
/// </summary>
/// <remarks>
/// A sentence ends at ".", "!" or "?" (with any closing quotes or brackets) followed by a space, the end of the text or a
/// citation, unless the next word starts in lowercase ("e.g. this", "wait... then": no dictionary of abbreviations); at a
/// line break; and at a citation. The citations that follow a sentence, with only spaces between, belong to it
/// ("text. [1] [2]" lights "text." for both), and so a citation in the middle of a sentence closes the part before it
/// ("the one immediately before it"). A citation with no text before it in its text belongs to no sentence. Decimal
/// points ("4.5", "v2.1") do not end a sentence.
/// </remarks>
internal static class AiAnswerSentences
{
    /// <summary>The sentences of <paramref name="text"/>; <paramref name="citations"/> lists the indices of its citations, ascending.</summary>
    public static List<AiAnswerSentence> Split(string text, IReadOnlyList<int> citations)
    {
        var sentences = new List<AiAnswerSentence>();
        var n = text.Length;
        var next = 0;
        var start = -1;
        var contentEnd = -1;
        AiAnswerSentence? closed = null;
        var i = 0;
        while (i < n)
        {
            if (next < citations.Count && citations[next] < i)
            {
                next++;
                continue;
            }
            var isCitation = next < citations.Count && citations[next] == i;
            if (isCitation)
            {
                if (start >= 0)
                {
                    closed = Close(sentences, start, contentEnd);
                    start = -1;
                }
                closed?.Citations.Add(i);
                next++;
                i++;
                continue;
            }
            var ch = text[i];
            if (IsSpace(ch))
            {
                if (ch == '\n' && start >= 0)
                {
                    closed = Close(sentences, start, contentEnd);
                    start = -1;
                }
                i++;
                continue;
            }
            if (start < 0)
            {
                start = i;
                closed = null;
            }
            contentEnd = i + 1;
            if (ch is '.' or '!' or '?')
            {
                var k = i + 1;
                while (k < n && IsCloser(text[k]) && !IsCitationAt(citations, next, k))
                {
                    k++;
                }
                if (k == n || IsCitationAt(citations, next, k) || (IsSpace(text[k]) && !LowercaseFollows(text, k)))
                {
                    closed = Close(sentences, start, k);
                    start = -1;
                    i = k;
                    continue;
                }
            }
            i++;
        }
        if (start >= 0)
        {
            Close(sentences, start, contentEnd);
        }
        return sentences;
    }

    private static AiAnswerSentence Close(List<AiAnswerSentence> sentences, int start, int end)
    {
        var sentence = new AiAnswerSentence(start, Math.Max(0, end - start), []);
        sentences.Add(sentence);
        return sentence;
    }

    /// <summary>Whether the next word after the spaces at <paramref name="from"/> starts in lowercase ("e.g. this", "... then").</summary>
    private static bool LowercaseFollows(string text, int from)
    {
        var j = from;
        while (j < text.Length && IsSpace(text[j]) && text[j] != '\n')
        {
            j++;
        }
        return j < text.Length && char.IsLower(text[j]);
    }

    private static bool IsCitationAt(IReadOnlyList<int> citations, int from, int index)
    {
        for (var k = from; k < citations.Count && citations[k] <= index; k++)
        {
            if (citations[k] == index)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Spaces between words and sentences (a no-break space included).</summary>
    public static bool IsSpace(char ch) => ch is ' ' or '\t' or '\n' or '\r' or ' ' or ' ' or ' ';

    private static bool IsCloser(char ch) => ch is '"' or '\'' or ')' or ']' or '”' or '’' or '»';
}
