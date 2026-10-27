namespace TKCode.Terminal.Core;

/// <summary>
/// 终端副作用（01-architecture.md §6.1，02 §6.3、§7.2）。在调用 <see cref="TerminalCore.Write"/>（或 Resize、Reset、Clear）的线程上
/// 同步触发；实现方不能阻塞，也不能重入 TerminalCore。回调里的借用数据（span）只在回调期间有效。
/// 除 <see cref="WriteToPty"/> 外都有默认实现：镜像核心与 UI 侧核心各自只覆盖自己负责的那部分。
/// DA、XTVERSION、尺寸、明暗查询不走这里：答案完全由核心自己的状态决定（<see cref="TerminalReplyProfile"/>）。
/// </summary>
public interface ITerminalEffects
{
    /// <summary>查询应答等需要写回 PTY 的字节。镜像核心写回 PTY；UI 侧核心丢弃。</summary>
    void WriteToPty(ReadOnlySpan<byte> reply);

    void Bell() { }

    void TitleChanged(string title) { }

    /// <summary>OSC 7 / 9;9 / 1337 设置的工作目录原文（不可信输入，10 §6.5）。</summary>
    void WorkingDirectoryChanged(string raw) { }

    /// <summary>OSC 52 / 1337 写剪贴板。镜像核心按策略转发给主视图（04 §11.2）。</summary>
    ClipboardWriteResult ClipboardWrite(ClipboardTarget target, string mime, ReadOnlySpan<byte> data) => ClipboardWriteResult.Denied;

    /// <summary>
    /// OSC 52 读剪贴板。返回 <see cref="ClipboardReadDecision.Deferred"/> 时（需要 <see cref="TerminalCoreOptions.DeferClipboardRead"/>），
    /// 稍后用 <see cref="TerminalCore.CompleteClipboardRead"/> 补发应答；返回 Denied 时不应答。
    /// </summary>
    ClipboardReadDecision ClipboardRead(in PendingClipboardRead request) => ClipboardReadDecision.Denied;

    /// <summary>OSC 9 / OSC 777 桌面通知。</summary>
    void Notification(string title, string body) { }

    /// <summary>OSC 9;4 进度；<paramref name="percent"/> 为 null 表示省略。</summary>
    void Progress(ProgressState state, int? percent) { }

    /// <summary>OSC 133 / 633 语义提示事件（<see cref="CommandTracker"/> 已先更新）。</summary>
    void SemanticPrompt(in SemanticPromptEvent e) { }

    /// <summary>mode 2026：程序开始 / 结束一帧的同步输出。</summary>
    void RenderHold(bool hold) { }

    /// <summary>程序请求改变窗口文本区尺寸（CSI 8 ; rows ; cols t，0 表示保持）。只在 <see cref="TerminalCoreOptions.Conformance"/> 下转发。</summary>
    void ResizeWindowRequested(int columns, int rows) { }

    /// <summary>RIS 之后（标题、pwd 已清空，不会再单独通知）。</summary>
    void FullReset() { }

    /// <summary>libghostty 不解析的 OSC（例如 633），原文不含 "OSC" 引导与终止符。</summary>
    void UnknownOsc(ReadOnlySpan<byte> payload, bool truncated) { }
}

public enum ClipboardTarget
{
    Standard,
    Selection,
    Primary,
}

public enum ClipboardWriteResult
{
    Success,
    Denied,
}

public enum ClipboardReadDecision
{
    Denied,
    Deferred,
}

/// <summary>一次待应答的剪贴板读取（O3：应答使用与请求相同的终止符）。</summary>
public readonly record struct PendingClipboardRead(ClipboardTarget Target, bool BelTerminator);

public enum ProgressState
{
    Remove,
    Set,
    Error,
    Indeterminate,
    Pause,
}

public enum SemanticPromptKind
{
    PromptStart,
    InputStart,
    OutputStart,
    CommandEnd,
}

/// <summary>语义提示事件：<see cref="PrimaryPrompt"/> 为 false 表示续行 / 右侧提示符。</summary>
public readonly record struct SemanticPromptEvent(SemanticPromptKind Kind, bool PrimaryPrompt, int? ExitCode, string? CommandLine, string? Error);

public sealed class NullTerminalEffects : ITerminalEffects
{
    public static readonly NullTerminalEffects Instance = new();

    public void WriteToPty(ReadOnlySpan<byte> reply)
    {
    }
}
