using System.Text;
using TKCode.Terminal.Core.Interop;

namespace TKCode.Terminal.Core;

public enum CommandState
{
    Prompt,
    Input,
    Running,
    Finished,
    Abandoned,
}

/// <summary>一条 shell 命令（OSC 133 / 633 边界，02 §12.1）。</summary>
public sealed class CommandRecord
{
    public required long Id { get; init; }
    public CommandState State { get; internal set; }
    public DateTimeOffset PromptAt { get; internal set; }
    public DateTimeOffset? StartedAt { get; internal set; }
    public DateTimeOffset? EndedAt { get; internal set; }

    /// <summary>OSC 133 D 的退出码（不是 shell 进程本身的退出码）；未提供时为 null。</summary>
    public int? ExitCode { get; internal set; }

    /// <summary>shell 报告的命令行（kitty 的 cmdline=，或 nonce 匹配的 OSC 633;E）。</summary>
    public string? CommandLine { get; internal set; }

    public string? ErrorText { get; internal set; }

    internal nint OutputStart;
    internal nint OutputEnd;
}

/// <summary>
/// OSC 133 / 633 命令跟踪（镜像核心，02 §12）。锚点用跟踪型 grid ref，随滚动、回滚裁剪、reflow 移动；
/// 保留最近 <see cref="MaxRecords"/> 条记录，淘汰时释放锚点（每个跟踪引用都会增加终端变更的簿记开销）。
/// </summary>
public sealed unsafe class CommandTracker : IDisposable
{
    public const int MaxRecords = 1000;

    private readonly TerminalCore _core;
    private readonly List<CommandRecord> _records = [];
    private long _nextId;

    internal CommandTracker(TerminalCore core) => _core = core;

    /// <summary>OSC 633;E 的命令行只在 nonce 与本会话注入脚本生成的 nonce 相同时采用（10 §6，02 §12.3）。</summary>
    public string? Osc633Nonce { get; set; }

    public IReadOnlyList<CommandRecord> Records => _records;

    public CommandRecord? Current => _records.Count == 0 ? null : _records[^1];

    /// <summary>
    /// 一条命令的输出文本（02 §12.4）：优先用 libghostty 的 select_output（依赖网格上的 OSC 133 语义标记），
    /// 否则用输出起止锚点构造线性选区。锚点已被裁出回滚时返回 null。
    /// </summary>
    public string? ReadOutput(CommandRecord record, int maxChars = 256 * 1024)
    {
        if (record.OutputStart == 0 || !GhosttyNative.TrackedGridRefHasValue(record.OutputStart))
        {
            return null;
        }
        var start = GhosttyGridRef.Create();
        TerminalCore.Check(GhosttyNative.TrackedGridRefSnapshot(record.OutputStart, &start));
        var selection = GhosttySelection.Create();
        if (GhosttyNative.TerminalSelectOutput(_core.Handle, start, &selection) != GhosttyResult.Success)
        {
            if (record.OutputEnd == 0 || !GhosttyNative.TrackedGridRefHasValue(record.OutputEnd))
            {
                return null;
            }
            var end = GhosttyGridRef.Create();
            TerminalCore.Check(GhosttyNative.TrackedGridRefSnapshot(record.OutputEnd, &end));
            selection.Start = start;
            selection.End = end;
        }
        var options = GhosttyTerminalSelectionFormatOptions.Create();
        options.Emit = GhosttyFormatterFormat.Plain;
        options.Unwrap = true;
        options.Trim = true;
        options.Selection = &selection;
        byte* output;
        nuint length;
        TerminalCore.Check(GhosttyNative.TerminalSelectionFormatAlloc(_core.Handle, null, options, &output, &length));
        var text = TerminalCore.TakeString(output, length);
        return text.Length <= maxChars ? text : string.Concat(text.AsSpan(0, maxChars / 2), "\n…\n", text.AsSpan(text.Length - maxChars / 2));
    }

    internal void OnSemanticPrompt(in SemanticPromptEvent e, bool fromOsc633)
    {
        var now = DateTimeOffset.UtcNow;
        var current = Current;
        switch (e.Kind)
        {
            case SemanticPromptKind.PromptStart when e.PrimaryPrompt:
                if (current is { State: CommandState.Prompt or CommandState.Input })
                {
                    current.PromptAt = now;  // 重复的 PROMPT_START（resize 后重画提示符）：不新建记录
                    return;
                }
                if (current is { State: CommandState.Running })
                {
                    current.State = CommandState.Abandoned;
                }
                Add(new CommandRecord { Id = ++_nextId, State = CommandState.Prompt, PromptAt = now });
                break;
            case SemanticPromptKind.InputStart when current is not null:
                current.State = CommandState.Input;
                break;
            case SemanticPromptKind.OutputStart when current is not null:
                current.State = CommandState.Running;
                current.StartedAt = now;
                current.CommandLine ??= e.CommandLine;
                current.OutputStart = TrackCursor();
                break;
            case SemanticPromptKind.CommandEnd when current is not null:
                current.EndedAt = now;
                current.ExitCode = e.ExitCode;
                current.ErrorText = e.Error;
                // 没有 OUTPUT_START 就结束：命令被取消（Ctrl+C 在提示符处）
                current.State = current.StartedAt is null ? CommandState.Abandoned : CommandState.Finished;
                if (current.OutputStart != 0)
                {
                    current.OutputEnd = TrackCursor();
                }
                break;
        }
    }

    /// <summary>OSC 633（VS Code 集成）：A/B/C/D 映射为同样的事件；E 的命令行需要 nonce 匹配。不产生网格语义标记。</summary>
    internal void OnOsc633(ReadOnlySpan<byte> payload)
    {
        if (!payload.StartsWith("633;"u8))
        {
            return;
        }
        var text = Encoding.UTF8.GetString(payload[4..]);
        var parts = text.Split(';');
        switch (parts[0])
        {
            case "A":
                OnSemanticPrompt(new SemanticPromptEvent(SemanticPromptKind.PromptStart, true, null, null, null), true);
                break;
            case "B":
                OnSemanticPrompt(new SemanticPromptEvent(SemanticPromptKind.InputStart, true, null, null, null), true);
                break;
            case "C":
                OnSemanticPrompt(new SemanticPromptEvent(SemanticPromptKind.OutputStart, true, null, null, null), true);
                break;
            case "D":
                int? code = parts.Length > 1 && int.TryParse(parts[1], out var c) ? c : null;
                OnSemanticPrompt(new SemanticPromptEvent(SemanticPromptKind.CommandEnd, true, code, null, null), true);
                break;
            case "E" when parts.Length >= 3 && Osc633Nonce is { Length: > 0 } nonce && parts[^1] == nonce && Current is { } record:
                record.CommandLine = Unescape633(string.Join(';', parts[1..^1]));
                break;
        }
    }

    /// <summary>OSC 633 的命令行转义：<c>\\</c> 与 <c>\xAB</c>。</summary>
    private static string Unescape633(string value)
    {
        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                if (value[i + 1] == '\\')
                {
                    sb.Append('\\');
                    i++;
                    continue;
                }
                if (value[i + 1] == 'x' && i + 3 < value.Length && byte.TryParse(value.AsSpan(i + 2, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
                {
                    sb.Append((char)b);
                    i += 3;
                    continue;
                }
            }
            sb.Append(value[i]);
        }
        return sb.ToString();
    }

    private nint TrackCursor()
    {
        var point = new GhosttyPoint { Tag = GhosttyPointTag.Active };
        var (x, y) = _core.Cursor;
        point.Value.Coordinate = new GhosttyPointCoordinate { X = (ushort)x, Y = (uint)y };
        nint tracked;
        return GhosttyNative.TerminalGridRefTrack(_core.Handle, point, &tracked) == GhosttyResult.Success ? tracked : 0;
    }

    private void Add(CommandRecord record)
    {
        _records.Add(record);
        if (_records.Count > MaxRecords)
        {
            Release(_records[0]);
            _records.RemoveAt(0);
        }
    }

    internal void Clear()
    {
        foreach (var record in _records)
        {
            Release(record);
        }
        _records.Clear();
    }

    private static void Release(CommandRecord record)
    {
        if (record.OutputStart != 0)
        {
            GhosttyNative.TrackedGridRefFree(record.OutputStart);
            record.OutputStart = 0;
        }
        if (record.OutputEnd != 0)
        {
            GhosttyNative.TrackedGridRefFree(record.OutputEnd);
            record.OutputEnd = 0;
        }
    }

    public void Dispose() => Clear();
}
