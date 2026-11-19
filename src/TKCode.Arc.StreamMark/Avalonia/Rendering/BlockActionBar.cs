using Avalonia.Controls;
using StreamMark.Avalonia.Plugins;
using StreamMark.Core.Model;

namespace StreamMark.Avalonia.Rendering;

/// <summary>
/// 插件登记的块头部操作按钮（代码块、表格、图表共用）：负责按 <see cref="BlockActionDescriptor.AppliesTo"/> 建 / 拆按钮、
/// 跟随样式、点击时用当前模型执行。布局仍由所属视图负责（遍历 <see cref="Buttons"/>）。
/// 该类块没有插件操作时什么都不做，不分配。
/// </summary>
internal sealed class BlockActionBar(BlockView owner, Action<Control> add, Action<Control> remove)
{
    private readonly List<IconButton> _buttons = [];

    public IReadOnlyList<IconButton> Buttons => _buttons;

    /// <summary>
    /// 重新求值哪些操作适用于 <paramref name="block"/> 并重建按钮。只应在上下文变化、首次显示或影响适用性的字段变化时调用。
    /// </summary>
    public void Rebuild(RenderContext context, BlockActionTargets target, MarkdownBlock block, bool visible)
    {
        var actions = context.Plugins.ActionsFor(target);
        if (actions.IsEmpty && _buttons.Count == 0)
        {
            return;
        }
        foreach (var button in _buttons)
        {
            remove(button);
        }
        _buttons.Clear();
        foreach (var action in actions)
        {
            if (!action.AppliesTo(block))
            {
                continue;
            }
            var button = new IconButton { Icon = action.Icon, IsVisible = visible };
            ToolTip.SetTip(button, action.Label);
            global::Avalonia.Automation.AutomationProperties.SetName(button, action.Label);
            button.Click += async (_, _) =>
            {
                if (owner.Block is { } current)
                {
                    var fence = current is CodeBlock code ? CodeFenceInfo.Parse(code.Info) : null;
                    await action.Execute(new BlockActionContext(current, owner, fence));
                }
            };
            _buttons.Add(button);
            add(button);
        }
        Restyle(context.Style);
    }

    public void Restyle(MarkdownStyle style)
    {
        foreach (var button in _buttons)
        {
            button.Foreground = style.SecondaryForeground;
            button.HoverBackground = style.HoverBackground;
            button.InvalidateVisual();
        }
    }

    public void SetVisible(bool visible)
    {
        foreach (var button in _buttons)
        {
            button.IsVisible = visible;
        }
    }

    /// <summary>把按钮重新放到所属视图子元素的最上层（表格的单元格在按钮之后加入时会盖住它们）。</summary>
    public void BringToFront()
    {
        foreach (var button in _buttons)
        {
            remove(button);
            add(button);
        }
    }
}
