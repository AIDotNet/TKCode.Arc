using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using StreamMark.Avalonia.Rendering;
using StreamMark.Avalonia.Selection;

namespace StreamMark.Avalonia.Automation;

/// <summary>
/// 文本片段（段落、标题、代码、diff）：控件类型为 Text，名称为纯文本（行内对象按复制时的规则替换）。
/// Avalonia 没有公开的文本模式（TextPattern），读屏只能读到名称。
/// </summary>
public sealed class TextSegmentAutomationPeer(Control owner, Func<string> text) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

    protected override string? GetNameCore() => AutomationProperties.GetName(Owner) is { Length: > 0 } name ? name : text();

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}

/// <summary>块视图（列表、引用、表格、代码块、HTML 盒子等）：分组，子节点为其中的片段与按钮。</summary>
public sealed class BlockAutomationPeer(BlockView owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => Owner switch
    {
        TableBlockView => AutomationControlType.Table,
        ListBlockView => AutomationControlType.List,
        _ => AutomationControlType.Group,
    };

    protected override string? GetNameCore() => AutomationProperties.GetName(Owner);

    protected override bool IsContentElementCore() => Owner is not TextBlockView;

    protected override bool IsControlElementCore() => Owner is not TextBlockView;
}

/// <summary>自绘图标按钮：控件类型为 Button，支持 Invoke。名称取 AutomationProperties.Name、提示文字或标签。</summary>
public sealed class IconButtonAutomationPeer(IconButton owner) : ControlAutomationPeer(owner), IInvokeProvider
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    protected override string? GetNameCore() =>
        AutomationProperties.GetName(Owner) is { Length: > 0 } name ? name
        : ToolTip.GetTip(Owner) as string is { Length: > 0 } tip ? tip
        : ((IconButton)Owner).Label;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    public void Invoke() => ((IconButton)Owner).PerformClick();
}

/// <summary>Markdown 视图与会话面板：文档。</summary>
public sealed class MarkdownDocumentAutomationPeer(Control owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;

    protected override string? GetNameCore() => AutomationProperties.GetName(Owner);

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}

/// <summary>
/// 任务列表项：控件类型为 CheckBox，开关状态取自模型；<see cref="Toggle"/> 与点击一样只是向宿主发出请求
/// （宿主没有启用任务交互、或不是任务项时什么都不做）。
/// </summary>
public sealed class TaskItemAutomationPeer(ListItemBlockView owner) : ControlAutomationPeer(owner), IToggleProvider
{
    private ListItemBlockView Item => (ListItemBlockView)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        Item.Task is null ? AutomationControlType.ListItem : AutomationControlType.CheckBox;

    protected override string? GetNameCore() =>
        AutomationProperties.GetName(Owner) is { Length: > 0 } name ? name
        : Item.Segments().FirstOrDefault()?.Text;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    public ToggleState ToggleState => Item.Task switch
    {
        true => ToggleState.On,
        false => ToggleState.Off,
        _ => ToggleState.Indeterminate,
    };

    public void Toggle() => Item.RequestToggle();
}
