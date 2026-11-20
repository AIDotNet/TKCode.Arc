using System.Collections.Immutable;
using StreamMark.Core.Model;

namespace StreamMark.Core.Parsing;

/// <summary>自定义块级语法规则的内容形态。</summary>
public enum BlockRuleKind : byte
{
    /// <summary>单行闭合块（打开本行即结束）。</summary>
    SingleLine,

    /// <summary>容器块：内部子行继续按 Markdown 块与行内规则递归解析。</summary>
    Container,

    /// <summary>原样文本块：内部行不按 Markdown 解析，原样保留为 <see cref="ComponentBlock.Raw"/>（或自定义块内容）。</summary>
    RawBody,
}

/// <summary>自定义块级语法规则对后续行的续行判定结果。</summary>
public enum BlockContinueResult : byte
{
    /// <summary>不延续：关闭当前块，并将本行交回外层重新解析。</summary>
    None,

    /// <summary>延续：保留本行内容（对容器递归解析，对原样块追加正文）。</summary>
    Continue,

    /// <summary>延续并丢弃本行（例如控制指令行）。</summary>
    ContinueDiscard,

    /// <summary>处理本行内容后立即关闭当前块。</summary>
    Break,

    /// <summary>丢弃本行（例如结束围栏或结束标签行）并立即关闭当前块。</summary>
    BreakDiscard,
}

/// <summary>自定义块级语法规则在解析期间持有的状态（由增量引擎跨行传递并在 1 行推测回滚时还原）。</summary>
public struct BlockRuleState
{
    /// <summary>块形态。</summary>
    public BlockRuleKind Kind { get; set; }

    /// <summary>逻辑名称（常作为 <see cref="ComponentBlock.Name"/>）。</summary>
    public string Name { get; set; }

    /// <summary>附加属性。</summary>
    public ComponentAttributes Attributes { get; set; }

    /// <summary>开启行前缀中已由开启语法消耗的字符数（剩余部分若非空会作为首行内容）。</summary>
    public int ConsumedLength { get; set; }

    /// <summary>开启行内容之后是否立即关闭（例如同行内已出现结束标记）。</summary>
    public bool CloseAfterLine { get; set; }

    /// <summary>是否已遇到显式结束标记（用于区分 <see cref="ComponentBlock.IsOpen"/>）。</summary>
    public bool SawClose { get; set; }

    /// <summary>规则私有状态（若可变则应在回滚时注意不可变性；推荐不可变对象或值类型）。</summary>
    public object? Data { get; set; }
}

/// <summary>
/// 零依赖的自定义块级语法扩展规则：原生接入增量块解析器的开放栈、单行推测回滚、
/// 原样文本缓冲（<see cref="BlockRuleKind.RawBody"/>）与顶层冻结机制。
/// </summary>
public interface IBlockSyntaxRule
{
    /// <summary>可触发该规则的开启首字符集合（用于 O(1) 字符表分发）。</summary>
    string OpeningCharacters { get; }

    /// <summary>是否允许打断当前正在延续的段落（默认 true）。</summary>
    bool CanInterruptParagraph => true;

    /// <summary>尝试在当前行首（已剥离外层容器前缀与 0–3 空格缩进）打开一个自定义块。</summary>
    bool TryOpen(ReadOnlySpan<char> line, out BlockRuleState state);

    /// <summary>判断后续行是否延续该自定义块。</summary>
    BlockContinueResult TryContinue(ReadOnlySpan<char> line, ref BlockRuleState state);

    /// <summary>构造最终不可变块模型（推荐返回 <see cref="ComponentBlock"/>）。</summary>
    MarkdownBlock Build(in BlockRuleState state, ImmutableArray<MarkdownBlock> children, string? rawContent, bool isOpen, SourceRange range);
}

/// <summary>
/// 自定义行内语法规则的输出上下文：直接向底层 <see cref="InlineContent"/> 构建器写入文本、对象、链接或递归解析子区间，
/// 无需构造中间 AST 节点。
/// </summary>
public abstract class InlineSyntaxContext
{
    /// <summary>当前生效的继承样式。</summary>
    public abstract InlineStyle Style { get; }

    /// <summary>输出纯文本并叠加可选样式。</summary>
    public abstract void AppendText(ReadOnlySpan<char> text, InlineStyle extraStyle = InlineStyle.None);

    /// <summary>输出嵌入行内对象（图片、公式、自闭合组件等）。</summary>
    public abstract void AppendObject(InlineObjectKind kind, string value, ComponentAttributes? attributes = null, string? alt = null, string? title = null, bool flag = false);

    /// <summary>输出带样式的链接文字。</summary>
    public abstract void AppendLink(ReadOnlySpan<char> text, string url, string? title = null, bool isAutoLink = false, InlineStyle extraStyle = InlineStyle.None);

    /// <summary>开启一个成对行内组件作用域（返回作用域索引，与 <see cref="EndComponent"/> 配对）。</summary>
    public abstract int BeginComponent(string name, ComponentAttributes? attributes = null);

    /// <summary>结束最近开启的行内组件作用域。</summary>
    public abstract void EndComponent(int token);

    /// <summary>以当前上下文及附加样式递归解析一段行内源码。</summary>
    public abstract void ParseInlines(ReadOnlySpan<char> slice, InlineStyle extraStyle = InlineStyle.None);
}

/// <summary>
/// 零依赖的自定义行内语法扩展规则：通过 <see cref="OpeningCharacters"/> 合并进 SIMD 扫描集，
/// 命中时直接向 <see cref="InlineSyntaxContext"/> 输出。
/// </summary>
public interface IInlineSyntaxRule
{
    /// <summary>可触发该规则的首字符集合。</summary>
    string OpeningCharacters { get; }

    /// <summary>
    /// 尝试在 <paramref name="text"/> 开头匹配自定义行内语法。
    /// 返回 true 时必须设置 <paramref name="consumed"/> &gt; 0。
    /// </summary>
    bool TryMatch(ReadOnlySpan<char> text, InlineSyntaxContext context, out int consumed);
}
