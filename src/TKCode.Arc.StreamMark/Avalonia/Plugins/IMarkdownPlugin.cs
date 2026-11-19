using System.Collections.Immutable;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using StreamMark.Avalonia.Extensions;
using StreamMark.Avalonia.Rendering;
using StreamMark.Core.Highlighting;
using StreamMark.Core.Model;
using StreamMark.Core.Parsing;

namespace StreamMark.Avalonia.Plugins;

/// <summary>
/// Markdown 插件：在 <see cref="Configure"/> 中注册语法（白名单组件、Markdig 扩展、映射）、渲染（块组件视图、
/// 行内组件、代码围栏视图）与交互（代码块头部操作）。插件只通过委托与工厂注册，NativeAOT 安全。
/// </summary>
public interface IMarkdownPlugin
{
    string Name { get; }

    void Configure(MarkdownPluginBuilder builder);
}

/// <summary>插件注册入口。</summary>
public sealed class MarkdownPluginBuilder
{
    internal MarkdownPluginBuilder(MarkdownProfileBuilder profile)
    {
        Profile = profile;
    }

    /// <summary>语法层（Core）：白名单组件、Markdig 扩展、自定义节点映射、remend 处理器。</summary>
    public MarkdownProfileBuilder Profile { get; }

    internal IMathRenderer? Math { get; private set; }
    internal ISyntaxHighlighter? Highlighter { get; private set; }
    internal List<IDiagramRenderer> Diagrams { get; } = [];
    internal Dictionary<string, BlockComponentRegistration> BlockComponents { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal Dictionary<string, IInlineComponentRenderer> InlineComponents { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal Dictionary<string, FenceViewRegistration> FenceViews { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal List<BlockActionDescriptor> BlockActions { get; } = [];
    internal List<PluginDiagnostic> Diagnostics { get; } = [];

    /// <summary>正在 Configure 的插件（登记者）。</summary>
    internal string CurrentPlugin { get; set; } = "";

    private readonly Dictionary<string, string> _owners = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>记下登记者；同一项已被别的插件（或同一插件之前）登记过时记一条覆盖诊断。</summary>
    internal void Claim(string key)
    {
        if (_owners.TryGetValue(key, out var previous))
        {
            Diagnostics.Add(new PluginDiagnostic(PluginDiagnosticKind.Replaced,
                $"'{CurrentPlugin}' replaced the {key} registered by '{previous}'.", key, CurrentPlugin, previous));
        }
        _owners[key] = CurrentPlugin;
    }

    public MarkdownPluginBuilder UseMath(IMathRenderer renderer)
    {
        Claim("math");
        Math = renderer;
        return this;
    }

    public MarkdownPluginBuilder UseHighlighter(ISyntaxHighlighter highlighter)
    {
        Claim("highlighter");
        Highlighter = highlighter;
        return this;
    }

    /// <summary>图表渲染器（代码块闭合后渲染，可在图表 / 源码间切换）。</summary>
    public MarkdownPluginBuilder AddDiagramRenderer(IDiagramRenderer renderer)
    {
        Diagrams.Add(renderer);
        return this;
    }

    /// <summary>
    /// 块级组件视图。<paramref name="name"/> 对应白名单标签（若尚未登记，会自动登记为块级组件）。
    /// 工厂只负责创建；内容通过 <see cref="BlockView.Update"/> 下发（同名组件的新模型会复用同一视图）。
    /// </summary>
    public MarkdownPluginBuilder AddBlockComponent(string name, Func<ComponentBlock, BlockView> factory, bool registerTag = true) =>
        AddBlockComponent(name, factory, new BlockComponentOptions { RegisterTag = registerTag });

    /// <summary>
    /// 块级组件视图，并指定它的复制 / 选区 / 搜索文本。视图除了子块之外还显示自己的文本（例如从属性取的标题）时用这个重载，
    /// 否则选区与复制会和视图的片段编号错位。没有指定时按默认规则收集子块。
    /// </summary>
    public MarkdownPluginBuilder AddBlockComponent(string name, Func<ComponentBlock, BlockView> factory, ComponentTextCollector text, bool registerTag = true) =>
        AddBlockComponent(name, factory, new BlockComponentOptions { Text = text, RegisterTag = registerTag });

    /// <summary>
    /// 块级组件视图的完整登记：内容模式（Markdown / 原样）、复制文本、流式中是否就用这个视图。
    /// 同名再次登记时整体覆盖（包括复制文本）。
    /// </summary>
    public MarkdownPluginBuilder AddBlockComponent(string name, Func<ComponentBlock, BlockView> factory, BlockComponentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Claim("block:" + name);
        BlockComponents[name] = new BlockComponentRegistration(factory, options.Text, options.WhileStreaming);
        if (options.RegisterTag)
        {
            Profile.AddComponent(new ComponentSpec(name, ComponentKind.Block) { Content = options.Content });
        }
        return this;
    }

    /// <summary>行内组件渲染器（自闭合组件绘制为嵌入对象，成对组件提供样式与点击）。</summary>
    public MarkdownPluginBuilder AddInlineComponent(string name, IInlineComponentRenderer renderer, ComponentKind kind = ComponentKind.SelfClosing, bool registerTag = true)
    {
        Claim("inline:" + name);
        InlineComponents[name] = renderer;
        if (registerTag)
        {
            Profile.AddComponent(new ComponentSpec(name, kind));
        }
        return this;
    }

    /// <summary>按围栏语言替换代码块视图（如 diff）。默认只在代码块闭合后替换，流式中仍按普通代码显示。</summary>
    public MarkdownPluginBuilder AddFenceView(string language, Func<CodeBlock, BlockView> factory, bool whileStreaming = false)
    {
        Claim("fence:" + language);
        FenceViews[language] = new FenceViewRegistration(factory, whileStreaming);
        return this;
    }

    /// <summary>块头部的操作按钮：默认只出现在代码块上，用 <see cref="BlockActionDescriptor.Targets"/> 扩展到表格、图表。</summary>
    public MarkdownPluginBuilder AddBlockAction(BlockActionDescriptor action)
    {
        BlockActions.Add(action);
        return this;
    }
}

internal sealed record FenceViewRegistration(Func<CodeBlock, BlockView> Factory, bool WhileStreaming);

internal sealed record BlockComponentRegistration(Func<ComponentBlock, BlockView> Factory, ComponentTextCollector? Text, bool WhileStreaming);

/// <summary><see cref="MarkdownPluginBuilder.AddBlockComponent(string, Func{ComponentBlock, BlockView}, BlockComponentOptions)"/> 的选项。</summary>
public sealed record BlockComponentOptions
{
    /// <summary>
    /// 组件内部内容的处理方式。<see cref="ComponentContent.Raw"/>：内容是数据（JSON、图表描述等），原样放进
    /// <see cref="ComponentBlock.Raw"/>，不按 Markdown 解析，remend 也不会在其中补全或转义。只在 <see cref="RegisterTag"/> 为 true 时生效。
    /// </summary>
    public ComponentContent Content { get; init; } = ComponentContent.Markdown;

    /// <summary>
    /// 结束标签出现之前（<see cref="ComponentBlock.IsOpen"/>）是否就用这个视图。false 时流式中先按默认容器显示
    /// （Markdown 内容照常显示子块，原样内容不显示），闭合后再换成插件视图，语义与 <c>AddFenceView</c> 的 whileStreaming 相同。
    /// 默认 true：视图在流式中每帧收到新模型，自己负责增量显示。
    /// </summary>
    public bool WhileStreaming { get; init; } = true;

    /// <summary>复制 / 选区 / 搜索文本；null 时按默认规则收集子块。片段个数与顺序必须与视图消耗的片段号一致。</summary>
    public ComponentTextCollector? Text { get; init; }

    /// <summary>同时把 name 登记为白名单块级标签（默认 true）；false 时标签须已由别处登记。</summary>
    public bool RegisterTag { get; init; } = true;
}

/// <summary>行内组件渲染器。</summary>
public interface IInlineComponentRenderer
{
    /// <summary>为自闭合组件创建嵌入文本行的绘制对象；返回 null 则显示为空。</summary>
    DrawableTextRun? CreateRun(InlineObject component, InlineComponentContext context) => null;

    /// <summary>成对组件内部文字的样式（前景色、背景、下划线等），返回 null 表示保持继承样式。</summary>
    InlineComponentStyle? GetStyle(InlineComponent component, MarkdownStyle style) => null;

    /// <summary>复制为纯文本时的输出（自闭合组件），默认为空字符串。</summary>
    string ToPlainText(InlineObject component) => "";

    /// <summary>是否可点击（悬停显示手型，点击触发 <see cref="TextSegmentView.ComponentClickedEvent"/>）。</summary>
    bool IsClickable(ComponentAttributes attributes) => false;
}

/// <summary>成对行内组件的样式覆盖。</summary>
public sealed record InlineComponentStyle(IBrush? Foreground = null, IBrush? Background = null, bool Underline = false, FontWeight? Weight = null);

/// <summary>创建行内组件绘制对象时的上下文。</summary>
public sealed record InlineComponentContext(MarkdownStyle Style, double FontSize, IBrush Foreground, TextRunProperties Properties, double MaxWidth);

/// <summary>块操作出现在哪类块的头部。</summary>
[Flags]
public enum BlockActionTargets
{
    /// <summary>代码块（<see cref="BlockActionDescriptor.AppliesTo"/> 收到 <see cref="CodeBlock"/>）。</summary>
    Code = 1,

    /// <summary>表格（收到 <see cref="TableBlock"/>；按钮与复制、全屏一起在悬停时出现）。</summary>
    Table = 2,

    /// <summary>已渲染为图表的代码块（收到 <see cref="CodeBlock"/>）。</summary>
    Diagram = 4,
}

/// <summary>
/// 块头部操作。<see cref="AppliesTo"/> 在视图拿到新的渲染上下文、首次显示，或代码块的信息串变化时求值，
/// 不在每次内容更新时求值；它与 <see cref="Execute"/> 都在界面线程调用。
/// </summary>
public sealed record BlockActionDescriptor(
    string Id,
    string Label,
    Geometry Icon,
    Func<MarkdownBlock, bool> AppliesTo,
    Func<BlockActionContext, Task> Execute)
{
    /// <summary>出现在哪类块上（默认只有代码块，与早期版本一致）。</summary>
    public BlockActionTargets Targets { get; init; } = BlockActionTargets.Code;
}

/// <summary>执行块操作时的上下文。<see cref="Fence"/> 只有代码块与图表才有。</summary>
public sealed record BlockActionContext(MarkdownBlock Block, Control View, CodeFenceInfo? Fence);

/// <summary>代码围栏信息串解析结果：<c>```cs src/App.cs#L10-20</c>。</summary>
public sealed record CodeFenceInfo(string? Language, string? Path, int? StartLine, int? EndLine)
{
    public static CodeFenceInfo Parse(string? info)
    {
        if (string.IsNullOrWhiteSpace(info))
        {
            return new CodeFenceInfo(null, null, null, null);
        }
        var parts = info.Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var language = parts.Length > 0 ? parts[0] : null;
        string? path = null;
        int? start = null, end = null;
        for (var i = 1; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.Contains('=') || part.StartsWith('{'))
            {
                continue;
            }
            var hash = part.IndexOf("#L", StringComparison.Ordinal);
            if (hash >= 0)
            {
                var range = part[(hash + 2)..].Split('-', 'L');
                if (int.TryParse(range[0], out var s))
                {
                    start = s;
                    end = range.Length > 1 && int.TryParse(range[^1], out var e) ? e : s;
                }
                part = part[..hash];
            }
            if ((part.Contains('/') || part.Contains('.')) && path is null)
            {
                path = part;
            }
        }
        return new CodeFenceInfo(language, path, start, end);
    }
}
