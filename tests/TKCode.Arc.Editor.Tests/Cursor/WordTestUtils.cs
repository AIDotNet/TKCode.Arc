// Ported from vscode (src/vs/editor/contrib/wordOperations/test/browser/wordTestUtils.ts, commit b57d5c9e), MIT license.
// Differences from upstream: testRepeatedActionAndExtractPositions runs on an OperationsTestEditor (a text model and
// cursor states) instead of a test code editor, and takes the editor options directly; deserializePipePositions
// returns a named tuple.

using System.Text;
using TKCode.Arc.Editor.Config;

namespace TKCode.Arc.Editor.Tests;

internal static class WordTestUtils
{
    public static (string Text, List<TextPosition> Positions) DeserializePipePositions(string text)
    {
        var resultText = new StringBuilder();
        var lineNumber = 1;
        var charIndex = 0;
        var positions = new List<TextPosition>();
        for (int i = 0, len = text.Length; i < len; i++)
        {
            var chr = text[i];
            if (chr == '\n')
            {
                resultText.Append(chr);
                lineNumber++;
                charIndex = 0;
                continue;
            }
            if (chr == '|')
            {
                positions.Add(new TextPosition(lineNumber, charIndex + 1));
            }
            else
            {
                resultText.Append(chr);
                charIndex++;
            }
        }
        return (resultText.ToString(), positions);
    }

    public static string SerializePipePositions(string text, List<TextPosition> positions)
    {
        var queue = new Queue<TextPosition>(positions.OrderBy(p => p));
        var resultText = new StringBuilder();
        var lineNumber = 1;
        var charIndex = 0;
        for (int i = 0, len = text.Length; i < len; i++)
        {
            var chr = text[i];
            if (queue.Count > 0 && queue.Peek().LineNumber == lineNumber && queue.Peek().Column == charIndex + 1)
            {
                resultText.Append('|');
                queue.Dequeue();
            }
            resultText.Append(chr);
            if (chr == '\n')
            {
                lineNumber++;
                charIndex = 0;
            }
            else
            {
                charIndex++;
            }
        }
        if (queue.Count > 0 && queue.Peek().LineNumber == lineNumber && queue.Peek().Column == charIndex + 1)
        {
            resultText.Append('|');
            queue.Dequeue();
        }
        if (queue.Count > 0)
        {
            throw new InvalidOperationException("Unexpected left over positions!!!");
        }
        return resultText.ToString();
    }

    public static List<TextPosition> TestRepeatedActionAndExtractPositions(
        string text,
        TextPosition initialPosition,
        Action<OperationsTestEditor> action,
        Func<OperationsTestEditor, TextPosition> record,
        Func<OperationsTestEditor, bool> stopCondition,
        EditorOptions? options = null)
    {
        var actualStops = new List<TextPosition>();
        using var editor = OperationsTestEditor.Create(text, options);
        editor.SetPosition(initialPosition);
        while (true)
        {
            action(editor);
            actualStops.Add(record(editor));
            if (stopCondition(editor))
            {
                break;
            }

            if (actualStops.Count > 1000)
            {
                throw new InvalidOperationException($"Endless loop detected involving position {editor.GetPosition()}!");
            }
        }
        return actualStops;
    }
}
