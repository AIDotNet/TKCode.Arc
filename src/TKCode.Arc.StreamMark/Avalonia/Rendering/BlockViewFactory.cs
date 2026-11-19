using StreamMark.Core.Model;

namespace StreamMark.Avalonia.Rendering;

/// <summary>块模型 → 块视图。</summary>
public static class BlockViewFactory
{
    public static BlockView Create(MarkdownBlock block, RenderContext context)
    {
        // 插件优先：块组件视图、按语言替换的代码围栏视图
        if (block is ComponentBlock component)
        {
            return context.Plugins.FindBlockComponent(component) is { } registration ? registration.Factory(component) : new Plugins.ComponentContainerView();
        }
        if (block is CodeBlock code && context.Plugins.FindFenceView(code) is { } fence)
        {
            return fence.Factory(code);
        }
        return CreateBuiltIn(block, context);
    }

    private static BlockView CreateBuiltIn(MarkdownBlock block, RenderContext context) => ViewTypeFor(block, context) switch
    {
        ViewType.Text => new TextBlockView(),
        ViewType.Code => new CodeBlockView(),
        ViewType.Diagram => new DiagramBlockView(),
        ViewType.Math => new MathBlockView(),
        ViewType.Table => new TableBlockView(),
        ViewType.Quote => new QuoteBlockView(),
        ViewType.Alert => new AlertBlockView(),
        ViewType.List => new ListBlockView(),
        ViewType.Footnotes => new FootnotesBlockView(),
        ViewType.Definitions => new DefinitionListBlockView(),
        ViewType.Container => new ContainerBlockView(),
        ViewType.HtmlBox => new HtmlBoxBlockView(),
        _ => new ThematicBreakBlockView(),
    };

    /// <summary>已有视图能否直接更新为新模型（类型一致即可复用）。</summary>
    public static bool CanReuse(BlockView view, MarkdownBlock block, RenderContext context)
    {
        // 组件与插件围栏视图：同名 / 同语言、且视图类型与新建的一致时复用
        // （闭合后才替换的组件在闭合那一刻从默认容器换成插件视图）
        if (block is ComponentBlock component)
        {
            return view.Block is ComponentBlock old && string.Equals(old.Name, component.Name, StringComparison.OrdinalIgnoreCase)
                && ReferenceEquals(context.Plugins.FindBlockComponent(old), context.Plugins.FindBlockComponent(component));
        }
        var fence = block is CodeBlock code ? context.Plugins.FindFenceView(code) : null;
        var oldFence = view.Block is CodeBlock oldCode ? context.Plugins.FindFenceView(oldCode) : null;
        if (fence is not null || oldFence is not null)
        {
            return ReferenceEquals(fence, oldFence);
        }
        return CanReuseBuiltIn(view, block, context);
    }

    private static bool CanReuseBuiltIn(BlockView view, MarkdownBlock block, RenderContext context) => view switch
    {
        TextBlockView => ViewTypeFor(block, context) == ViewType.Text,
        CodeBlockView => ViewTypeFor(block, context) == ViewType.Code,
        DiagramBlockView => ViewTypeFor(block, context) == ViewType.Diagram,
        MathBlockView => block is MathBlock,
        TableBlockView => block is TableBlock,
        QuoteBlockView => block is QuoteBlock,
        AlertBlockView => block is AlertBlock,
        ListBlockView => block is ListBlock,
        FootnotesBlockView => block is FootnotesBlock,
        DefinitionListBlockView => block is DefinitionListBlock,
        ContainerBlockView => block is CustomContainerBlock,
        HtmlBoxBlockView => block is HtmlBoxBlock,
        ThematicBreakBlockView => block is ThematicBreakBlock,
        _ => false,
    };

    private enum ViewType
    {
        Text,
        Code,
        Diagram,
        Math,
        Table,
        Quote,
        Alert,
        List,
        Footnotes,
        Definitions,
        Container,
        HtmlBox,
        Break,
    }

    private static ViewType ViewTypeFor(MarkdownBlock block, RenderContext context) => block switch
    {
        ParagraphBlock or HeadingBlock or HtmlBlock => ViewType.Text,
        // 图表只在代码块闭合后渲染；流式中仍按代码显示
        CodeBlock { IsOpen: false } code when context.Services.FindDiagramRenderer(code.Language) is not null => ViewType.Diagram,
        CodeBlock => ViewType.Code,
        MathBlock => ViewType.Math,
        TableBlock => ViewType.Table,
        AlertBlock => ViewType.Alert,
        QuoteBlock => ViewType.Quote,
        ListBlock => ViewType.List,
        FootnotesBlock => ViewType.Footnotes,
        DefinitionListBlock => ViewType.Definitions,
        CustomContainerBlock => ViewType.Container,
        HtmlBoxBlock => ViewType.HtmlBox,
        _ => ViewType.Break,
    };
}
