using Markdig;

namespace StreamMark.Core.Parsing;

/// <summary>
/// 默认 Markdig 管线（等同于 <see cref="MarkdownProfile.Default"/> 的管线）。
/// </summary>
/// <remarks>
/// 只启用删除线这一种强调扩展：上标 <c>^</c>、下标 <c>~</c>、插入 <c>++</c>、高亮 <c>==</c>
/// 在大模型输出的普通文本里误触发太多（如 "C++ 和 C++"、"a == b 且 c == d"、"约 ~5 到 ~10"）。
/// 上下标、高亮改用白名单标签 <c>&lt;sup&gt;</c>、<c>&lt;sub&gt;</c>、<c>&lt;mark&gt;</c>。
/// </remarks>
public static class StreamMarkPipeline
{
    public static MarkdownPipeline Default => MarkdownProfile.Default.Pipeline;
}
