using System.Collections.Frozen;
using System.Collections.Immutable;
using StreamMark.Avalonia.Extensions;
using StreamMark.Avalonia.Rendering;
using StreamMark.Core.Highlighting;
using StreamMark.Core.Model;
using StreamMark.Core.Parsing;

namespace StreamMark.Avalonia.Plugins;

/// <summary>汇总后的插件注册表（不可变）：渲染层据此分派；<see cref="Profile"/> 供文档解析使用。</summary>
public sealed class PluginRegistry
{
    // 构建一次、之后只读：冻结字典的查找比普通字典快，渲染期每块 / 每个行内对象都会查
    private readonly FrozenDictionary<string, BlockComponentRegistration> _blockComponents;
    private readonly FrozenDictionary<string, IInlineComponentRenderer> _inlineComponents;
    private readonly bool _hasComponentTexts;
    private readonly FrozenDictionary<string, FenceViewRegistration> _fenceViews;
    private readonly ImmutableArray<BlockActionDescriptor> _codeActions;
    private readonly ImmutableArray<BlockActionDescriptor> _tableActions;
    private readonly ImmutableArray<BlockActionDescriptor> _diagramActions;

    internal PluginRegistry(MarkdownServices services)
    {
        var builder = new MarkdownPluginBuilder(new MarkdownProfileBuilder());
        IEnumerable<IMarkdownPlugin> plugins = [BuiltInComponentsPlugin.Instance, .. services.Plugins];
        foreach (var plugin in plugins)
        {
            builder.CurrentPlugin = plugin.Name;
            try
            {
                plugin.Configure(builder);
            }
            catch (Exception e)
            {
                throw new PluginConfigurationException(plugin.Name, e);
            }
        }
        // 兼容写法排在所有插件之前，被插件覆盖时同样记一条诊断
        if (services.Math is not null && builder.Math is not null)
        {
            builder.Diagnostics.Add(new PluginDiagnostic(PluginDiagnosticKind.Replaced, "A plugin replaced MarkdownServices.Math.", "math", null, nameof(MarkdownServices)));
        }
        if (services.Highlighter is not null && builder.Highlighter is not null)
        {
            builder.Diagnostics.Add(new PluginDiagnostic(PluginDiagnosticKind.Replaced, "A plugin replaced MarkdownServices.Highlighter.", "highlighter", null, nameof(MarkdownServices)));
        }
        Diagnostics = [.. builder.Diagnostics];
        Math = builder.Math ?? services.Math;
        Highlighter = builder.Highlighter ?? services.Highlighter;
        // 与公式 / 高亮同向：插件优先于兼容写法 Diagrams（插件之间仍按登记顺序，先命中者生效）
        Diagrams = [.. builder.Diagrams, .. services.Diagrams];
        _blockComponents = builder.BlockComponents.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _inlineComponents = builder.InlineComponents.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _hasComponentTexts = builder.BlockComponents.Values.Any(r => r.Text is not null);
        _fenceViews = builder.FenceViews.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        BlockActions = [.. builder.BlockActions];
        _codeActions = [.. BlockActions.Where(a => a.Targets.HasFlag(BlockActionTargets.Code))];
        _tableActions = [.. BlockActions.Where(a => a.Targets.HasFlag(BlockActionTargets.Table))];
        _diagramActions = [.. BlockActions.Where(a => a.Targets.HasFlag(BlockActionTargets.Diagram))];
        Profile = builder.Profile.Build();
    }

    /// <summary>与这些插件匹配的 Markdown 方言：宿主创建文档时应传入，否则插件注册的组件标签不会被解析。</summary>
    public MarkdownProfile Profile { get; }

    public IMathRenderer? Math { get; }

    public ISyntaxHighlighter? Highlighter { get; }

    public ImmutableArray<IDiagramRenderer> Diagrams { get; }

    public ImmutableArray<BlockActionDescriptor> BlockActions { get; }

    /// <summary>
    /// 某一类块头部的操作（构建时按 <see cref="BlockActionDescriptor.Targets"/> 分好组，视图只遍历自己那一组）。
    /// 传入多个标志时返回登记了其中任一类的操作。
    /// </summary>
    public ImmutableArray<BlockActionDescriptor> ActionsFor(BlockActionTargets target) => target switch
    {
        BlockActionTargets.Code => _codeActions,
        BlockActionTargets.Table => _tableActions,
        BlockActionTargets.Diagram => _diagramActions,
        _ => [.. BlockActions.Where(a => (a.Targets & target) != 0)],
    };

    /// <summary>构建注册表时记录的诊断（例如同名登记被后来的插件覆盖）。</summary>
    public ImmutableArray<PluginDiagnostic> Diagnostics { get; }

    public IDiagramRenderer? FindDiagramRenderer(string? language)
    {
        foreach (var renderer in Diagrams)
        {
            if (renderer.CanRender(language))
            {
                return renderer;
            }
        }
        return null;
    }

    public bool TryGetBlockComponent(string name, out Func<ComponentBlock, BlockView> factory)
    {
        if (_blockComponents.TryGetValue(name, out var registration))
        {
            factory = registration.Factory;
            return true;
        }
        factory = null!;
        return false;
    }

    /// <summary>
    /// 这个组件实际使用的插件登记：没有登记，或登记为闭合后才替换而组件仍开着时返回 null（显示为默认容器）。
    /// 视图的创建 / 复用与复制文本都按它判断，保证两边用的是同一个视图。
    /// </summary>
    internal BlockComponentRegistration? FindBlockComponent(ComponentBlock component) =>
        _blockComponents.TryGetValue(component.Name, out var registration) && (!component.IsOpen || registration.WhileStreaming)
            ? registration
            : null;

    public IInlineComponentRenderer? FindInlineComponent(string name) => _inlineComponents.GetValueOrDefault(name);

    /// <summary>组件登记的复制文本收集器；没有时按默认规则（子块按序）收集。</summary>
    public ComponentTextCollector? FindComponentText(string name) =>
        _hasComponentTexts && _blockComponents.TryGetValue(name, out var registration) ? registration.Text : null;

    /// <summary>某个组件实例当前生效的复制文本收集器（考虑流式中尚未替换为插件视图的情况）。</summary>
    internal ComponentTextCollector? FindComponentText(ComponentBlock component) =>
        _hasComponentTexts ? FindBlockComponent(component)?.Text : null;

    internal FenceViewRegistration? FindFenceView(CodeBlock code) =>
        code.Language is { } language && _fenceViews.TryGetValue(language, out var registration) && (!code.IsOpen || registration.WhileStreaming)
            ? registration
            : null;
}
