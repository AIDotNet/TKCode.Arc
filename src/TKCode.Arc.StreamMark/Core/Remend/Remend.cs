// Ported from vercel/streamdown packages/remend (src/index.ts), Copyright 2023 Vercel, Inc., Apache-2.0; modified.

namespace StreamMark.Core.Remend;

/// <summary>未完成链接的处理方式。</summary>
public enum LinkMode
{
    /// <summary>用 <see cref="Remend.IncompleteLinkUrl"/> 占位 URL 补全链接（默认）。</summary>
    Protocol,

    /// <summary>只显示链接文本，去掉链接语法。</summary>
    TextOnly,
}

/// <summary>
/// 自定义修复步骤。<paramref name="Priority"/> 越小越先执行；内置 handler 的优先级在 0–100 之间。
/// </summary>
/// <param name="Name">唯一标识。</param>
/// <param name="Handle">输入文本、返回修改后的文本。</param>
/// <param name="Priority">优先级，默认 100（在所有内置 handler 之后）。</param>
public sealed record RemendHandler(string Name, Func<string, string> Handle, int Priority = 100);

/// <summary>
/// <see cref="Remend.Complete"/> 的配置。除 <see cref="InlineKatex"/> 外各项默认开启，设为 false 关闭对应补全。
/// </summary>
public sealed record RemendOptions
{
    /// <summary>默认配置。</summary>
    public static RemendOptions Default { get; } = new();

    /// <summary>补全粗体（<c>**text</c> → <c>**text**</c>）。</summary>
    public bool Bold { get; init; } = true;

    /// <summary>补全粗斜体（<c>***text</c> → <c>***text***</c>）。</summary>
    public bool BoldItalic { get; init; } = true;

    /// <summary>转义列表项中的比较运算符（<c>- &gt; 25</c> → <c>- \&gt; 25</c>）。</summary>
    public bool ComparisonOperators { get; init; } = true;

    /// <summary>去掉末尾未完成的 HTML 标签（<c>text &lt;custom</c> → <c>text</c>）。</summary>
    public bool HtmlTags { get; init; } = true;

    /// <summary>补全图片（<c>![alt](url</c> → <c>![alt](streammark:incomplete-image)</c>）。</summary>
    public bool Images { get; init; } = true;

    /// <summary>补全行内代码。</summary>
    public bool InlineCode { get; init; } = true;

    /// <summary>补全行内公式（<c>$equation</c> → <c>$equation$</c>）。默认关闭：单个 <c>$</c> 常是货币符号。</summary>
    public bool InlineKatex { get; init; }

    /// <summary>补全斜体（<c>*text</c> / <c>_text</c>）。</summary>
    public bool Italic { get; init; } = true;

    /// <summary>补全块级公式（<c>$$equation</c> → <c>$$equation$$</c>）。</summary>
    public bool Katex { get; init; } = true;

    /// <summary>未完成链接的处理方式。</summary>
    public LinkMode LinkMode { get; init; } = LinkMode.Protocol;

    /// <summary>补全链接与图片（<c>[text](url</c> → <c>[text](streammark:incomplete-link)</c>）。</summary>
    public bool Links { get; init; } = true;

    /// <summary>处理未完成的 setext 标题下划线。</summary>
    public bool SetextHeadings { get; init; } = true;

    /// <summary>转义单词字符之间的单个 <c>~</c>（<c>20~25</c> → <c>20\~25</c>）。</summary>
    public bool SingleTilde { get; init; } = true;

    /// <summary>补全删除线（<c>~~text</c> → <c>~~text~~</c>）。</summary>
    public bool Strikethrough { get; init; } = true;

    /// <summary>自定义 handler，与内置 handler 一起按优先级排序执行。</summary>
    public IReadOnlyList<RemendHandler>? Handlers { get; init; }
}

/// <summary>
/// 不完整 Markdown 修复器：在流式 LLM 输出的每个前缀上补全或去掉未闭合的语法，
/// 让渲染器不会把半截标记显示出来。移植自 vercel/streamdown 的 remend。
/// </summary>
public static class Remend
{
    /// <summary>未完成链接的占位 URL。</summary>
    public const string IncompleteLinkUrl = "streammark:incomplete-link";

    /// <summary>未完成图片的占位 URL。</summary>
    public const string IncompleteImageUrl = "streammark:incomplete-image";

    private const string IncompleteLinkSuffix = "](" + IncompleteLinkUrl + ")";
    private const string IncompleteImageSuffix = "](" + IncompleteImageUrl + ")";

    // Built-in handler priorities (0-100)
    private const int PrioritySingleTilde = 0;
    private const int PriorityComparisonOperators = 5;
    private const int PriorityHtmlTags = 10;
    private const int PrioritySetextHeadings = 15;
    private const int PriorityLinks = 20;
    private const int PriorityBoldItalic = 30;
    private const int PriorityBold = 35;
    private const int PriorityItalicDoubleUnderscore = 40;
    private const int PriorityItalicSingleAsterisk = 41;
    private const int PriorityItalicSingleUnderscore = 42;
    private const int PriorityInlineCode = 50;
    private const int PriorityStrikethrough = 60;
    private const int PriorityKatex = 70;
    private const int PriorityInlineKatex = 75;

    private static readonly RemendHandler SingleTildeBuiltIn =
        new("singleTilde", SingleTildeHandler.HandleSingleTildeEscape, PrioritySingleTilde);
    private static readonly RemendHandler ComparisonOperatorsBuiltIn =
        new("comparisonOperators", ComparisonOperatorHandler.HandleComparisonOperators, PriorityComparisonOperators);
    private static readonly RemendHandler HtmlTagsBuiltIn =
        new("htmlTags", HtmlTagHandler.HandleIncompleteHtmlTag, PriorityHtmlTags);
    private static readonly RemendHandler SetextHeadingsBuiltIn =
        new("setextHeadings", SetextHeadingHandler.HandleIncompleteSetextHeading, PrioritySetextHeadings);
    private static readonly RemendHandler LinksProtocolBuiltIn =
        new("links", static text => LinkImageHandler.HandleIncompleteLinksAndImages(text, LinkMode.Protocol), PriorityLinks);
    private static readonly RemendHandler LinksTextOnlyBuiltIn =
        new("links", static text => LinkImageHandler.HandleIncompleteLinksAndImages(text, LinkMode.TextOnly), PriorityLinks);
    private static readonly RemendHandler BoldItalicBuiltIn =
        new("boldItalic", EmphasisHandlers.HandleIncompleteBoldItalic, PriorityBoldItalic);
    private static readonly RemendHandler BoldBuiltIn =
        new("bold", EmphasisHandlers.HandleIncompleteBold, PriorityBold);
    private static readonly RemendHandler ItalicDoubleUnderscoreBuiltIn =
        new("italicDoubleUnderscore", EmphasisHandlers.HandleIncompleteDoubleUnderscoreItalic, PriorityItalicDoubleUnderscore);
    private static readonly RemendHandler ItalicSingleAsteriskBuiltIn =
        new("italicSingleAsterisk", EmphasisHandlers.HandleIncompleteSingleAsteriskItalic, PriorityItalicSingleAsterisk);
    private static readonly RemendHandler ItalicSingleUnderscoreBuiltIn =
        new("italicSingleUnderscore", EmphasisHandlers.HandleIncompleteSingleUnderscoreItalic, PriorityItalicSingleUnderscore);
    private static readonly RemendHandler InlineCodeBuiltIn =
        new("inlineCode", InlineCodeHandler.HandleIncompleteInlineCode, PriorityInlineCode);
    private static readonly RemendHandler StrikethroughBuiltIn =
        new("strikethrough", StrikethroughHandler.HandleIncompleteStrikethrough, PriorityStrikethrough);
    private static readonly RemendHandler KatexBuiltIn =
        new("katex", KatexHandler.HandleIncompleteBlockKatex, PriorityKatex);
    private static readonly RemendHandler InlineKatexBuiltIn =
        new("inlineKatex", KatexHandler.HandleIncompleteInlineKatex, PriorityInlineKatex);

    // A handler in execution order; the links handler may end the chain early
    private readonly record struct Step(RemendHandler Handler, bool EarlyReturn);

    // Enabled built-ins, already in ascending priority order
    private static List<Step> GetEnabledBuiltInHandlers(RemendOptions options)
    {
        var steps = new List<Step>(16 + (options.Handlers?.Count ?? 0));
        void Add(bool enabled, RemendHandler handler, bool earlyReturn = false)
        {
            if (enabled)
            {
                steps.Add(new Step(handler, earlyReturn));
            }
        }

        Add(options.SingleTilde, SingleTildeBuiltIn);
        Add(options.ComparisonOperators, ComparisonOperatorsBuiltIn);
        Add(options.HtmlTags, HtmlTagsBuiltIn);
        Add(options.SetextHeadings, SetextHeadingsBuiltIn);
        // The links handler is enabled by either the links or the images option.
        // Only protocol mode returns early: text-only output never ends with the marker.
        var protocol = options.LinkMode == LinkMode.Protocol;
        Add(options.Links || options.Images, protocol ? LinksProtocolBuiltIn : LinksTextOnlyBuiltIn, protocol);
        Add(options.BoldItalic, BoldItalicBuiltIn);
        Add(options.Bold, BoldBuiltIn);
        Add(options.Italic, ItalicDoubleUnderscoreBuiltIn);
        Add(options.Italic, ItalicSingleAsteriskBuiltIn);
        Add(options.Italic, ItalicSingleUnderscoreBuiltIn);
        Add(options.InlineCode, InlineCodeBuiltIn);
        Add(options.Strikethrough, StrikethroughBuiltIn);
        Add(options.Katex, KatexBuiltIn);
        // inlineKatex is opt-in (defaults to false, unlike other options)
        Add(options.InlineKatex, InlineKatexBuiltIn);
        return steps;
    }

    private static List<Step> GetHandlers(RemendOptions options)
    {
        var steps = GetEnabledBuiltInHandlers(options);
        if (options.Handlers is not { Count: > 0 } custom)
        {
            return steps;
        }
        // Merge and sort by priority. Stable, like Array.prototype.sort: built-ins
        // keep their order ahead of custom handlers with an equal priority, and
        // custom handlers keep their given order.
        foreach (var handler in custom)
        {
            var step = new Step(handler, false);
            var index = steps.Count;
            while (index > 0 && steps[index - 1].Handler.Priority > handler.Priority)
            {
                index--;
            }
            steps.Insert(index, step);
        }
        return steps;
    }

    private static bool EndsWithIncompleteMarker(string result) =>
        result.EndsWith(IncompleteLinkSuffix, StringComparison.Ordinal)
        || result.EndsWith(IncompleteImageSuffix, StringComparison.Ordinal);

    // A single trailing space (but not a hard-break double space) is dropped
    private static string StripSingleTrailingSpace(string text) =>
        text.Length > 0 && text[^1] == ' ' && !(text.Length > 1 && text[^2] == ' ') ? text[..^1] : text;

    /// <summary>
    /// 修复不完整的 Markdown：补全未闭合的标记、去掉无法补全的尾部片段，避免半截语法被渲染出来。
    /// </summary>
    /// <param name="text">流式输出的当前文本（可以是任意前缀）。</param>
    /// <param name="options">配置；为 null 时使用 <see cref="RemendOptions.Default"/>。</param>
    /// <returns>修复后的文本。对返回值再次调用会得到相同结果。</returns>
    public static string Complete(string text, RemendOptions? options = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        options ??= RemendOptions.Default;

        // Remove trailing whitespace if it's not a double space
        var result = StripSingleTrailingSpace(text);

        var steps = GetHandlers(options);

        ScanCache.Enter();
        try
        {
            // Execute handlers in priority order
            foreach (var (handler, earlyReturn) in steps)
            {
                result = handler.Handle(result);

                // Check for early return condition (e.g., incomplete link marker)
                if (earlyReturn && EndsWithIncompleteMarker(result))
                {
                    return result;
                }
            }

            // A handler that removes a trailing fragment can expose a trailing space
            // (e.g. dropping an incomplete image). Strip it the same way the input
            // was stripped, so healed output re-heals to itself.
            return StripSingleTrailingSpace(result);
        }
        finally
        {
            ScanCache.Exit();
        }
    }
}
