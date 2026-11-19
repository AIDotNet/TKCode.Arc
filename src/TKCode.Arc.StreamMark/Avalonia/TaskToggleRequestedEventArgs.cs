using StreamMark.Core.Model;

namespace StreamMark.Avalonia;

/// <summary>
/// 宿主启用任务交互（<see cref="MarkdownView.IsTaskInteractionEnabled"/>）后，用户点击或用键盘激活任务复选框时发出的请求。
/// StreamMark 不会写文件，也不会改动快照：宿主自行决定接受或拒绝，接受就修改自己的原文
/// （并记入撤销栈、保存），再把新文本交给视图；拒绝则什么都不用做，视图的勾选状态不会变。
/// </summary>
/// <remarks>
/// 只在快照已经完整（<c>IsComplete</c>）时发出；流式输出中源码还会变化，点击会被忽略。
/// 宿主异步处理请求时，用 <see cref="MarkdownView.IsCurrent"/> 检查它是否已经过期。
/// </remarks>
public sealed class TaskToggleRequestedEventArgs(int documentId, long version, SourceRange marker, bool isChecked) : EventArgs
{
    /// <summary>请求发出时 <see cref="MarkdownView.DocumentId"/> 的值。</summary>
    public int DocumentId { get; } = documentId;

    /// <summary>请求所基于的快照版本（<c>DocumentSnapshot.Version</c>）。</summary>
    public long Version { get; } = version;

    /// <summary>任务标记 <c>[ ]</c> / <c>[x]</c> 在源码中的区间（3 个字符，UTF-16 偏移，与 <see cref="SourceRange"/> 的其他用法相同）。</summary>
    public SourceRange Marker { get; } = marker;

    /// <summary>当前是否已勾选。</summary>
    public bool IsChecked { get; } = isChecked;

    /// <summary>用户想要的状态（与 <see cref="IsChecked"/> 相反）。</summary>
    public bool RequestedChecked => !IsChecked;

    /// <summary>
    /// 把 <paramref name="source"/> 中的标记改成 <see cref="RequestedChecked"/>：只替换方括号里的那一个字符，
    /// 其余文本、缩进和换行（包括 CRLF）原样保留。<paramref name="source"/> 必须是发出请求时的那份源码；
    /// 标记处的文字与 <see cref="IsChecked"/> 不符（源码已经变了）时返回 null。
    /// </summary>
    public string? ApplyTo(string source)
    {
        if (Marker.Length != 3 || Marker.End > source.Length || source[Marker.Start] != '[' || source[Marker.End - 1] != ']')
        {
            return null;
        }
        var current = source[Marker.Start + 1];
        if (IsChecked ? current is not ('x' or 'X') : current != ' ')
        {
            return null;
        }
        return string.Concat(source.AsSpan(0, Marker.Start + 1), RequestedChecked ? "x" : " ", source.AsSpan(Marker.Start + 2));
    }
}
