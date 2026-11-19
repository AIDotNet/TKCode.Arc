using System.Collections.Immutable;

namespace StreamMark.Core.Diff;

public enum DiffLineKind : byte
{
    Context,
    Added,
    Deleted,
    /// <summary><c>\ No newline at end of file</c>。</summary>
    NoNewline,
}

/// <summary>
/// diff 中的一行。<see cref="RawLine"/> 是它在原始 diff 文本中的行号，<see cref="TextStart"/> 是正文（去掉 +/-/空格 标记后）
/// 在该原始行中的起始列，用于把显示位置映射回源码偏移（选区与复制）。
/// </summary>
public sealed record DiffLine(DiffLineKind Kind, int? OldNumber, int? NewNumber, string Text, int RawLine, int TextStart);

public sealed record DiffHunk(string Header, int OldStart, int NewStart, string? Section, ImmutableArray<DiffLine> Lines, int RawLine);

public sealed record DiffFile(string? OldPath, string? NewPath, ImmutableArray<DiffHunk> Hunks, int RawLine)
{
    /// <summary>显示用路径：新路径（删除文件时取旧路径），重命名时为 "旧 → 新"。</summary>
    public string? DisplayPath =>
        OldPath is not null && NewPath is not null && OldPath != NewPath ? $"{OldPath} → {NewPath}" : NewPath ?? OldPath;

    public int Additions => Hunks.Sum(h => h.Lines.Count(l => l.Kind == DiffLineKind.Added));

    public int Deletions => Hunks.Sum(h => h.Lines.Count(l => l.Kind == DiffLineKind.Deleted));
}

public sealed record DiffDocument(ImmutableArray<DiffFile> Files)
{
    public int Additions => Files.Sum(f => f.Additions);

    public int Deletions => Files.Sum(f => f.Deletions);
}

/// <summary>
/// 容错的 unified diff 解析（git diff、diff -u、只有 +/- 行的片段都可以）。流式输出中写了一半的 diff 也能解析：
/// 区段头声明的行数只用于判断区段结束，不足时不报错。
/// </summary>
public static class UnifiedDiff
{
    public static DiffDocument Parse(string text)
    {
        var lines = text.Split('\n');
        var files = ImmutableArray.CreateBuilder<DiffFile>();
        FileBuilder? file = null;
        HunkBuilder? hunk = null;

        void FlushHunk()
        {
            if (hunk is not null)
            {
                file ??= new FileBuilder(null, null, hunk.RawLine);
                file.Hunks.Add(hunk.Build());
                hunk = null;
            }
        }

        void FlushFile()
        {
            FlushHunk();
            if (file is not null && (file.Hunks.Count > 0 || file.OldPath is not null || file.NewPath is not null))
            {
                files.Add(new DiffFile(file.OldPath, file.NewPath, file.Hunks.ToImmutable(), file.RawLine));
            }
            file = null;
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            // 最后的空行（文本以换行结尾）不算内容
            if (i == lines.Length - 1 && line.Length == 0)
            {
                break;
            }
            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                FlushFile();
                var (a, b) = GitPaths(line["diff --git ".Length..]);
                file = new FileBuilder(a, b, i);
                continue;
            }
            // 区段之外（或区段已满）的文件头
            var inHunkBody = hunk is not null && !hunk.IsFull;
            if (!inHunkBody && line.StartsWith("--- ", StringComparison.Ordinal) && i + 1 < lines.Length && lines[i + 1].StartsWith("+++ ", StringComparison.Ordinal))
            {
                FlushHunk();
                var oldPath = HeaderPath(line[4..]);
                var newPath = HeaderPath(lines[i + 1].TrimEnd('\r')[4..]);
                if (file is null || file.Hunks.Count > 0)
                {
                    FlushFile();
                    file = new FileBuilder(oldPath, newPath, i);
                }
                else
                {
                    file.OldPath = oldPath ?? file.OldPath;
                    file.NewPath = newPath ?? file.NewPath;
                }
                if (oldPath is null)
                {
                    file.OldPath = null;
                }
                if (newPath is null)
                {
                    file.NewPath = null;
                }
                i++;
                continue;
            }
            if (line.StartsWith("@@", StringComparison.Ordinal) && TryParseHunkHeader(line, out var oldStart, out var oldCount, out var newStart, out var newCount, out var section))
            {
                FlushHunk();
                hunk = new HunkBuilder(line, oldStart, oldCount, newStart, newCount, section, i);
                continue;
            }
            if (!inHunkBody && file is not null && hunk is null && IsGitMetadata(line))
            {
                // index / mode / rename / similarity / Binary files 等元数据行
                continue;
            }
            if (line.Length > 0 && line[0] == '\\')
            {
                hunk?.Add(DiffLineKind.NoNewline, line, i, 0);
                continue;
            }
            // 没有区段头的片段（只有 +/- 行）：从第 1 行开始编号
            hunk ??= new HunkBuilder("", 1, -1, 1, -1, null, i);
            var kind = line.Length == 0 ? DiffLineKind.Context : line[0] switch
            {
                '+' => DiffLineKind.Added,
                '-' => DiffLineKind.Deleted,
                ' ' => DiffLineKind.Context,
                _ => (DiffLineKind?)null,
            };
            if (kind is { } k)
            {
                hunk.Add(k, line.Length == 0 ? "" : line[1..], i, line.Length == 0 ? 0 : 1);
            }
            else
            {
                // 不是 diff 行（说明文字等）：当作上下文显示原文
                hunk.Add(DiffLineKind.Context, line, i, 0);
            }
        }
        FlushFile();
        return new DiffDocument(files.ToImmutable());
    }

    private static bool IsGitMetadata(string line) =>
        line.StartsWith("index ", StringComparison.Ordinal) || line.StartsWith("new file mode", StringComparison.Ordinal)
        || line.StartsWith("deleted file mode", StringComparison.Ordinal) || line.StartsWith("old mode", StringComparison.Ordinal)
        || line.StartsWith("new mode", StringComparison.Ordinal) || line.StartsWith("similarity index", StringComparison.Ordinal)
        || line.StartsWith("dissimilarity index", StringComparison.Ordinal) || line.StartsWith("rename from", StringComparison.Ordinal)
        || line.StartsWith("rename to", StringComparison.Ordinal) || line.StartsWith("copy from", StringComparison.Ordinal)
        || line.StartsWith("copy to", StringComparison.Ordinal) || line.StartsWith("Binary files", StringComparison.Ordinal);

    // "a/src/x.cs b/src/x.cs"（路径中有空格时按 " b/" 切分）
    private static (string? A, string? B) GitPaths(string rest)
    {
        var split = rest.IndexOf(" b/", StringComparison.Ordinal);
        if (split < 0)
        {
            return (null, null);
        }
        var a = rest[..split];
        var b = rest[(split + 1)..];
        return (StripPrefix(a), StripPrefix(b));
    }

    // "--- a/path\t时间戳" → "path"；/dev/null → null
    private static string? HeaderPath(string value)
    {
        var tab = value.IndexOf('\t');
        if (tab >= 0)
        {
            value = value[..tab];
        }
        value = value.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
        }
        return value == "/dev/null" ? null : StripPrefix(value);
    }

    private static string StripPrefix(string path) =>
        path.Length > 2 && path[1] == '/' && path[0] is 'a' or 'b' or 'i' or 'w' or 'c' or 'o' ? path[2..] : path;

    /// <summary>解析 <c>@@ -旧起[,旧数] +新起[,新数] @@ 节标题</c>。</summary>
    public static bool TryParseHunkHeader(string line, out int oldStart, out int oldCount, out int newStart, out int newCount, out string? section)
    {
        oldStart = newStart = 1;
        oldCount = newCount = 1;
        section = null;
        var end = line.IndexOf("@@", 2, StringComparison.Ordinal);
        var body = (end < 0 ? line[2..] : line[2..end]).Trim();
        if (end >= 0 && end + 2 < line.Length)
        {
            section = line[(end + 2)..].Trim() is { Length: > 0 } s ? s : null;
        }
        var parts = body.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || parts[0][0] != '-' || parts[1][0] != '+')
        {
            return false;
        }
        return TryRange(parts[0][1..], out oldStart, out oldCount) && TryRange(parts[1][1..], out newStart, out newCount);
    }

    private static bool TryRange(string value, out int start, out int count)
    {
        count = 1;
        var comma = value.IndexOf(',');
        if (!int.TryParse(comma < 0 ? value : value[..comma], out start))
        {
            return false;
        }
        return comma < 0 || int.TryParse(value[(comma + 1)..], out count);
    }

    private sealed class FileBuilder(string? oldPath, string? newPath, int rawLine)
    {
        public string? OldPath = oldPath;
        public string? NewPath = newPath;
        public readonly int RawLine = rawLine;
        public readonly ImmutableArray<DiffHunk>.Builder Hunks = ImmutableArray.CreateBuilder<DiffHunk>();
    }

    private sealed class HunkBuilder(string header, int oldStart, int oldCount, int newStart, int newCount, string? section, int rawLine)
    {
        private readonly ImmutableArray<DiffLine>.Builder _lines = ImmutableArray.CreateBuilder<DiffLine>();
        private readonly int _oldStart = oldStart;
        private readonly int _newStart = newStart;
        private int _old = oldStart;
        private int _new = newStart;
        private int _oldSeen;
        private int _newSeen;

        public int RawLine => rawLine;

        /// <summary>区段头声明的行数都已出现（之后的 "--- " 是下一个文件的文件头）。</summary>
        public bool IsFull => oldCount >= 0 && newCount >= 0 && _oldSeen >= oldCount && _newSeen >= newCount;

        public void Add(DiffLineKind kind, string text, int raw, int textStart)
        {
            switch (kind)
            {
                case DiffLineKind.Context:
                    _lines.Add(new DiffLine(kind, _old++, _new++, text, raw, textStart));
                    _oldSeen++;
                    _newSeen++;
                    break;
                case DiffLineKind.Deleted:
                    _lines.Add(new DiffLine(kind, _old++, null, text, raw, textStart));
                    _oldSeen++;
                    break;
                case DiffLineKind.Added:
                    _lines.Add(new DiffLine(kind, null, _new++, text, raw, textStart));
                    _newSeen++;
                    break;
                default:
                    _lines.Add(new DiffLine(kind, null, null, text, raw, textStart));
                    break;
            }
        }

        public DiffHunk Build() => new(header, _oldStart, _newStart, section, _lines.ToImmutable(), rawLine);
    }
}

/// <summary>并排视图的一行：左（旧）与右（新），任一侧可为空。</summary>
public readonly record struct SplitRow(DiffLine? Left, DiffLine? Right);

public static class DiffLayout
{
    /// <summary>
    /// 并排视图的配对：上下文行两侧相同；连续的删除行与随后的新增行按顺序一一配对，多出的一侧另一边留空。
    /// </summary>
    public static ImmutableArray<SplitRow> Split(DiffHunk hunk)
    {
        var rows = ImmutableArray.CreateBuilder<SplitRow>(hunk.Lines.Length);
        var lines = hunk.Lines;
        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];
            if (line.Kind is DiffLineKind.Context)
            {
                rows.Add(new SplitRow(line, line));
                i++;
                continue;
            }
            if (line.Kind is DiffLineKind.NoNewline)
            {
                i++;
                continue;
            }
            var deleted = new List<DiffLine>();
            var added = new List<DiffLine>();
            while (i < lines.Length && lines[i].Kind is DiffLineKind.Deleted or DiffLineKind.NoNewline)
            {
                if (lines[i].Kind == DiffLineKind.Deleted)
                {
                    deleted.Add(lines[i]);
                }
                i++;
            }
            while (i < lines.Length && lines[i].Kind is DiffLineKind.Added or DiffLineKind.NoNewline)
            {
                if (lines[i].Kind == DiffLineKind.Added)
                {
                    added.Add(lines[i]);
                }
                i++;
            }
            for (var k = 0; k < Math.Max(deleted.Count, added.Count); k++)
            {
                rows.Add(new SplitRow(k < deleted.Count ? deleted[k] : null, k < added.Count ? added[k] : null));
            }
        }
        return rows.ToImmutable();
    }

    /// <summary>
    /// 一对删除 / 新增行中实际改动的区间（去掉公共前缀与后缀），用于行内高亮；两行几乎完全不同时返回 null（整行高亮即可）。
    /// </summary>
    public static ((int Start, int Length) Old, (int Start, int Length) New)? ChangedRanges(string oldText, string newText)
    {
        var prefix = 0;
        var max = Math.Min(oldText.Length, newText.Length);
        while (prefix < max && oldText[prefix] == newText[prefix])
        {
            prefix++;
        }
        var suffix = 0;
        while (suffix < max - prefix && oldText[oldText.Length - 1 - suffix] == newText[newText.Length - 1 - suffix])
        {
            suffix++;
        }
        var common = prefix + suffix;
        // 公共部分太少：说明是整行改写，行内高亮没有意义
        if (common == 0 || common < Math.Max(oldText.Length, newText.Length) * 0.3)
        {
            return null;
        }
        return ((prefix, oldText.Length - common), (prefix, newText.Length - common));
    }
}
