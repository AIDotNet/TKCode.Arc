using System.Text;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc.Editor.Benchmarks;

/// <summary>Generated documents and models for the benchmarks: source-like lines, loaded in 64 KB chunks.</summary>
internal static class Documents
{
    private static readonly string[] s_lines =
    [
        "using System.Collections.Generic;",
        "",
        "namespace Sample.Project;",
        "",
        "/// <summary>A class with some members, so that lines differ in length and indentation.</summary>",
        "public sealed class Example",
        "{",
        "    private readonly Dictionary<string, int> _counts = new();",
        "",
        "    public int Add(string key, int value)",
        "    {",
        "        if (_counts.TryGetValue(key, out var existing))",
        "        {",
        "            value += existing; // accumulate",
        "        }",
        "",
        "        _counts[key] = value;",
        "        return value;",
        "    }",
        "}",
    ];

    /// <summary>About <paramref name="megabytes"/> MB of UTF-16 text with \n line endings.</summary>
    public static string Create(int megabytes)
    {
        var targetChars = megabytes * 1024 * 1024 / sizeof(char);
        var builder = new StringBuilder(targetChars + 256);
        var i = 0;
        while (builder.Length < targetChars)
        {
            builder.Append(s_lines[i % s_lines.Length]).Append(' ', i % 3).Append('\n');
            i++;
        }

        return builder.ToString();
    }

    /// <summary>The chunks a reader of a file would hand to the builder.</summary>
    public static List<string> Chunk(string text, int chunkSize = 64 * 1024)
    {
        var chunks = new List<string>(text.Length / chunkSize + 1);
        for (var offset = 0; offset < text.Length; offset += chunkSize)
        {
            chunks.Add(text.Substring(offset, Math.Min(chunkSize, text.Length - offset)));
        }

        return chunks;
    }

    public static PieceTreeTextBufferFactory Factory(IReadOnlyList<string> chunks)
    {
        var builder = new PieceTreeTextBufferBuilder();
        foreach (var chunk in chunks)
        {
            builder.AcceptChunk(chunk);
        }

        return builder.Finish();
    }

    public static TextModel Model(IReadOnlyList<string> chunks) =>
        new(Factory(chunks), "plaintext", TextModel.DefaultCreationOptions, CreateUndoRedoService());

    public static UndoRedoService CreateUndoRedoService() => new(new DialogService(), new NotificationService());

    private sealed class DialogService : IUndoRedoDialogService
    {
        public ValueTask<UndoChoice> PromptAsync(UndoRedoPromptOptions options) => ValueTask.FromResult(UndoChoice.All);

        public ValueTask<bool> ConfirmAsync(UndoRedoConfirmOptions options) => ValueTask.FromResult(true);
    }

    private sealed class NotificationService : IUndoRedoNotificationService
    {
        public void Warn(string message)
        {
        }

        public void Error(Exception error)
        {
        }
    }
}
