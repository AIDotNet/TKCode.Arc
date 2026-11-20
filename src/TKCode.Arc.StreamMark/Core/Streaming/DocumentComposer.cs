using System.Collections.Immutable;
using StreamMark.Core.Model;
using StreamMark.Core.Parsing;
using StreamMark.Core.Remend;

namespace StreamMark.Core.Streaming;

/// <summary>
/// 流式组装：由原生增量解析器 <see cref="IncrementalDocumentParser"/> 驱动，
/// 顶层已稳定前缀按引用冻结、容器内部缓存已闭合子项/表格行、仅流式末尾活跃叶节点重新解析；
/// 非线程安全，由 <see cref="StreamingDocument"/> 的后台工作线程独占使用。
/// </summary>
internal sealed class DocumentComposer
{
    private readonly IncrementalDocumentParser _parser;

    public DocumentComposer(MarkdownProfile? profile = null, RemendOptions? tailOptions = null)
    {
        _parser = new IncrementalDocumentParser(profile, tailOptions);
    }

    public int FrozenEnd => _parser.FrozenEnd;

    public void Reset() => _parser.Reset();

    /// <summary>处理新文本片段（[<paramref name="from"/>, 末尾)）。</summary>
    public DocumentSnapshot Update(string text, int from, long version) =>
        _parser.UpdateSlice(text.AsSpan(), from, version);

    /// <summary>增量追加自上次提交以来的新增字符片段并产出快照（零尾部整段拷贝）。</summary>
    public DocumentSnapshot AppendDelta(ReadOnlySpan<char> delta, long version) =>
        _parser.AppendAndParse(delta, version);

    /// <summary>流结束：一次性提交完整文本或收尾当前增量会话，产出 <see cref="DocumentSnapshot.IsComplete"/> 为 true 的最终快照。</summary>
    public DocumentSnapshot Finish(string fullText, long version) =>
        _parser.Finish(fullText, version);

    /// <summary>流结束（仅追加最后的增量片段，无需重新拷贝全文）。</summary>
    public DocumentSnapshot FinishDelta(ReadOnlySpan<char> delta, long version) =>
        _parser.FinishDelta(delta, version);

    /// <summary>一次性解析一段 Markdown（供单测与等价性校验使用）。</summary>
    internal ImmutableArray<MarkdownBlock> Parse(string raw, int @base, RemendOptions options) =>
        _parser.ParseFull(raw.AsSpan(), @base, options);
}
