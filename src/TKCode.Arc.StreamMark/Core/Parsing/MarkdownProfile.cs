using System.Buffers;
using System.Collections.Immutable;
using StreamMark.Core.Remend;

namespace StreamMark.Core.Parsing;

/// <summary>
/// 一套 Markdown 方言配置：原生增量解析器的方言开关、白名单组件、自定义块/行内语法规则与 remend 处理器。
/// 不可变、线程安全，由 <see cref="MarkdownProfileBuilder"/> 构建；同一宿主的所有文档通常共用一个实例。
/// </summary>
public sealed class MarkdownProfile
{
    private readonly SearchValues<char>? _customBaseTriggers;
    private readonly SearchValues<char>? _customAutolinkTriggers;

    internal MarkdownProfile(
        ImmutableArray<ComponentSpec> components,
        ImmutableArray<IBlockSyntaxRule> blockRules,
        ImmutableArray<IInlineSyntaxRule> inlineRules,
        ImmutableArray<RemendHandler> remendHandlers,
        bool softlineBreakAsHardline,
        bool enableTables,
        bool enableTaskLists,
        bool enableStrikethrough,
        bool enableAutoLinks,
        bool enableFootnotes,
        bool enableAlertBlocks,
        bool enableDefinitionLists,
        bool enableCustomContainers,
        bool enableMathematics,
        bool enableCjkFriendlyEmphasis,
        bool enableEmojiShortcodes,
        bool enableAutoIdentifiers)
    {
        Components = components;
        Table = new ComponentTable(components);
        BlockRules = blockRules;
        InlineRules = inlineRules;
        RemendHandlers = remendHandlers;
        SoftlineBreakAsHardline = softlineBreakAsHardline;
        EnableTables = enableTables;
        EnableTaskLists = enableTaskLists;
        EnableStrikethrough = enableStrikethrough;
        EnableAutoLinks = enableAutoLinks;
        EnableFootnotes = enableFootnotes;
        EnableAlertBlocks = enableAlertBlocks;
        EnableDefinitionLists = enableDefinitionLists;
        EnableCustomContainers = enableCustomContainers;
        EnableMathematics = enableMathematics;
        EnableCjkFriendlyEmphasis = enableCjkFriendlyEmphasis;
        EnableEmojiShortcodes = enableEmojiShortcodes;
        EnableAutoIdentifiers = enableAutoIdentifiers;

        if (!inlineRules.IsEmpty)
        {
            var extra = new HashSet<char>();
            foreach (var rule in inlineRules)
            {
                foreach (var c in rule.OpeningCharacters)
                {
                    extra.Add(c);
                }
            }
            if (extra.Count > 0)
            {
                var baseChars = new HashSet<char>("\\`*_~[]!<&$:\n\r");
                baseChars.UnionWith(extra);
                _customBaseTriggers = SearchValues.Create([.. baseChars]);

                baseChars.UnionWith("wW");
                _customAutolinkTriggers = SearchValues.Create([.. baseChars]);
            }
        }
    }

    /// <summary>默认方言：GFM、数学、脚注、提示块、自定义容器、CJK 友好强调、Emoji 短码等，加上内置白名单组件。</summary>
    public static MarkdownProfile Default { get; } = new MarkdownProfileBuilder().BuildNew();

    /// <summary>白名单 HTML 组件表。</summary>
    public ImmutableArray<ComponentSpec> Components { get; }

    internal ComponentTable Table { get; }

    /// <summary>自定义块级语法规则。</summary>
    public ImmutableArray<IBlockSyntaxRule> BlockRules { get; }

    /// <summary>自定义行内语法规则。</summary>
    public ImmutableArray<IInlineSyntaxRule> InlineRules { get; }

    /// <summary>插件追加的 remend 处理器（流式活跃叶节点使用）。</summary>
    public ImmutableArray<RemendHandler> RemendHandlers { get; }

    /// <summary>单次软换行是否按硬换行（<c>\n</c>）输出（聊天消息常用）。</summary>
    public bool SoftlineBreakAsHardline { get; }

    public bool EnableTables { get; }

    public bool EnableTaskLists { get; }

    public bool EnableStrikethrough { get; }

    public bool EnableAutoLinks { get; }

    public bool EnableFootnotes { get; }

    public bool EnableAlertBlocks { get; }

    public bool EnableDefinitionLists { get; }

    public bool EnableCustomContainers { get; }

    public bool EnableMathematics { get; }

    public bool EnableCjkFriendlyEmphasis { get; }

    public bool EnableEmojiShortcodes { get; }

    public bool EnableAutoIdentifiers { get; }

    internal SearchValues<char> GetInlineTriggers(bool hasBareAutolink, SearchValues<char> defaultBase, SearchValues<char> defaultAutolink) =>
        hasBareAutolink
            ? _customAutolinkTriggers ?? defaultAutolink
            : _customBaseTriggers ?? defaultBase;
}

/// <summary>构建 <see cref="MarkdownProfile"/>。插件在这里注册方言开关、白名单组件、自定义语法规则与 remend 处理器。</summary>
public sealed class MarkdownProfileBuilder
{
    private readonly List<ComponentSpec> _components = [.. ComponentSpec.BuiltIn];
    private readonly List<IBlockSyntaxRule> _blockRules = [];
    private readonly List<IInlineSyntaxRule> _inlineRules = [];
    private readonly List<RemendHandler> _remendHandlers = [];

    private bool _softlineBreakAsHardline;
    private bool _enableTables = true;
    private bool _enableTaskLists = true;
    private bool _enableStrikethrough = true;
    private bool _enableAutoLinks = true;
    private bool _enableFootnotes = true;
    private bool _enableAlertBlocks = true;
    private bool _enableDefinitionLists = true;
    private bool _enableCustomContainers = true;
    private bool _enableMathematics = true;
    private bool _enableCjkFriendlyEmphasis = true;
    private bool _enableEmojiShortcodes = true;
    private bool _enableAutoIdentifiers = true;

    /// <summary>登记白名单 HTML 组件；同名时后登记的覆盖先登记的。</summary>
    public MarkdownProfileBuilder AddComponent(ComponentSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        _components.RemoveAll(c => string.Equals(c.Name, spec.Name, StringComparison.OrdinalIgnoreCase));
        _components.Add(spec);
        return this;
    }

    /// <summary>追加零依赖的自定义块级语法规则。</summary>
    public MarkdownProfileBuilder AddBlockRule(IBlockSyntaxRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _blockRules.Add(rule);
        return this;
    }

    /// <summary>追加零依赖的自定义行内语法规则。</summary>
    public MarkdownProfileBuilder AddInlineRule(IInlineSyntaxRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _inlineRules.Add(rule);
        return this;
    }

    /// <summary>追加 remend 处理器（作用于流式活跃叶节点，优先级规则见 <see cref="Remend.Remend"/>）。</summary>
    public MarkdownProfileBuilder AddRemendHandler(RemendHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _remendHandlers.Add(handler);
        return this;
    }

    /// <summary>将段落内的单个软换行视为硬换行（例如聊天消息输入框的换行行为）。</summary>
    public MarkdownProfileBuilder UseSoftlineBreakAsHardline(bool enabled = true)
    {
        _softlineBreakAsHardline = enabled;
        return this;
    }

    /// <summary>启用或关闭 GFM 管道表格（默认开启）。</summary>
    public MarkdownProfileBuilder UseTables(bool enabled = true)
    {
        _enableTables = enabled;
        return this;
    }

    /// <summary>启用或关闭 GFM 任务列表（默认开启）。</summary>
    public MarkdownProfileBuilder UseTaskLists(bool enabled = true)
    {
        _enableTaskLists = enabled;
        return this;
    }

    /// <summary>启用或关闭 <c>~~</c> 删除线（默认开启；单波浪下标始终关闭）。</summary>
    public MarkdownProfileBuilder UseStrikethrough(bool enabled = true)
    {
        _enableStrikethrough = enabled;
        return this;
    }

    /// <summary>启用或关闭 GFM 裸自动链接（默认开启）。</summary>
    public MarkdownProfileBuilder UseAutoLinks(bool enabled = true)
    {
        _enableAutoLinks = enabled;
        return this;
    }

    /// <summary>启用或关闭脚注（默认开启）。</summary>
    public MarkdownProfileBuilder UseFootnotes(bool enabled = true)
    {
        _enableFootnotes = enabled;
        return this;
    }

    /// <summary>启用或关闭 GitHub 提示块 <c>&gt; [!NOTE]</c>（默认开启）。</summary>
    public MarkdownProfileBuilder UseAlertBlocks(bool enabled = true)
    {
        _enableAlertBlocks = enabled;
        return this;
    }

    /// <summary>启用或关闭定义列表（默认开启）。</summary>
    public MarkdownProfileBuilder UseDefinitionLists(bool enabled = true)
    {
        _enableDefinitionLists = enabled;
        return this;
    }

    /// <summary>启用或关闭 <c>:::</c> 自定义容器（默认开启）。</summary>
    public MarkdownProfileBuilder UseCustomContainers(bool enabled = true)
    {
        _enableCustomContainers = enabled;
        return this;
    }

    /// <summary>启用或关闭数学公式（<c>$…$</c>、<c>$$…$$</c>、<c>\(…\)</c>、<c>\[…\]</c>，默认开启）。</summary>
    public MarkdownProfileBuilder UseMathematics(bool enabled = true)
    {
        _enableMathematics = enabled;
        return this;
    }

    /// <summary>启用或关闭 CJK 友好强调侧翼规则（默认开启）。</summary>
    public MarkdownProfileBuilder UseCjkFriendlyEmphasis(bool enabled = true)
    {
        _enableCjkFriendlyEmphasis = enabled;
        return this;
    }

    /// <summary>启用或关闭 <c>:smile:</c> Emoji 短码（默认开启）。</summary>
    public MarkdownProfileBuilder UseEmojiShortcodes(bool enabled = true)
    {
        _enableEmojiShortcodes = enabled;
        return this;
    }

    /// <summary>启用或关闭 GitHub 风格标题自动锚点 Id（默认开启）。</summary>
    public MarkdownProfileBuilder UseAutoIdentifiers(bool enabled = true)
    {
        _enableAutoIdentifiers = enabled;
        return this;
    }

    /// <summary>
    /// 构建方言。没有登记任何语法改动时返回共享的 <see cref="MarkdownProfile.Default"/>：
    /// 只加渲染器、不改语法的插件组合与默认方言是同一个实例，宿主可按引用判断文档与服务是否匹配。
    /// </summary>
    public MarkdownProfile Build() => IsUnchanged ? MarkdownProfile.Default : BuildNew();

    private bool IsUnchanged =>
        !_softlineBreakAsHardline
        && _enableTables && _enableTaskLists && _enableStrikethrough && _enableAutoLinks
        && _enableFootnotes && _enableAlertBlocks && _enableDefinitionLists && _enableCustomContainers
        && _enableMathematics && _enableCjkFriendlyEmphasis && _enableEmojiShortcodes && _enableAutoIdentifiers
        && _blockRules.Count == 0 && _inlineRules.Count == 0 && _remendHandlers.Count == 0
        && _components.Count == ComponentSpec.BuiltIn.Length && _components.TrueForAll(ComponentSpec.BuiltIn.Contains);

    internal MarkdownProfile BuildNew() => new(
        [.. _components],
        [.. _blockRules],
        [.. _inlineRules],
        [.. _remendHandlers],
        _softlineBreakAsHardline,
        _enableTables,
        _enableTaskLists,
        _enableStrikethrough,
        _enableAutoLinks,
        _enableFootnotes,
        _enableAlertBlocks,
        _enableDefinitionLists,
        _enableCustomContainers,
        _enableMathematics,
        _enableCjkFriendlyEmphasis,
        _enableEmojiShortcodes,
        _enableAutoIdentifiers);
}
