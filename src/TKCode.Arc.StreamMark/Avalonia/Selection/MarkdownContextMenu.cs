using Avalonia.Controls;
using Avalonia.Interactivity;
using StreamMark.Avalonia.Extensions;

namespace StreamMark.Avalonia.Selection;

/// <summary>右键菜单的命令。</summary>
public enum MarkdownMenuCommand
{
    Copy,
    CopyAsMarkdown,
    SelectAll,
    Find,
}

/// <summary>
/// Markdown 视图 / 会话视图的右键菜单：复制、复制为 Markdown、全选、在会话中查找。
/// 使用 <see cref="MenuFlyout"/>，外观来自宿主主题（宿主主题没有 MenuFlyout 模板时不显示）。
/// 文字每次打开时由 <see cref="MarkdownServices.MenuText"/> 取得，可随界面语言变化；"查找"触发 <see cref="FindRequestedEvent"/>。
/// </summary>
public static class MarkdownContextMenu
{
    /// <summary>用户从菜单选择"查找"：宿主显示自己的查找框（事件源是 Markdown 视图或会话面板）。</summary>
    public static readonly RoutedEvent<RoutedEventArgs> FindRequestedEvent =
        RoutedEvent.Register<Control, RoutedEventArgs>("FindRequested", RoutingStrategies.Bubble);

    /// <summary>为宿主控件安装菜单（<see cref="MarkdownServices.ContextMenu"/> 为 false 时不打开）。</summary>
    internal static void Attach(Control host, SelectionController controller)
    {
        var flyout = new MenuFlyout();
        flyout.Opening += (_, _) => Populate(flyout, host, controller);
        host.ContextFlyout = flyout;
        // 宿主关闭菜单时，在隧道阶段标记为已处理，ContextFlyout 不会打开
        host.AddHandler(Control.ContextRequestedEvent, (_, e) =>
        {
            if (!MarkdownServices.GetServices(host).ContextMenu)
            {
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
    }

    private static void Populate(MenuFlyout flyout, Control host, SelectionController controller)
    {
        var services = MarkdownServices.GetServices(host);
        flyout.Items.Clear();
        var text = services.MenuText;
        var hasSelection = !controller.Selection.IsEmpty;
        MenuItem Item(MarkdownMenuCommand command, bool enabled, Action action)
        {
            var item = new MenuItem { Header = text(command), IsEnabled = enabled };
            item.Click += (_, _) => action();
            return item;
        }
        flyout.Items.Add(Item(MarkdownMenuCommand.Copy, hasSelection, () => _ = controller.CopyAsync()));
        flyout.Items.Add(Item(MarkdownMenuCommand.CopyAsMarkdown, hasSelection, () => _ = controller.CopyAsync(asMarkdown: true)));
        flyout.Items.Add(new Separator());
        flyout.Items.Add(Item(MarkdownMenuCommand.SelectAll, true, controller.SelectAll));
        flyout.Items.Add(Item(MarkdownMenuCommand.Find, true, () => host.RaiseEvent(new RoutedEventArgs(FindRequestedEvent, host))));
    }

    /// <summary>默认（英文）菜单文字。</summary>
    public static string DefaultText(MarkdownMenuCommand command) => command switch
    {
        MarkdownMenuCommand.Copy => "Copy",
        MarkdownMenuCommand.CopyAsMarkdown => "Copy as Markdown",
        MarkdownMenuCommand.SelectAll => "Select All",
        _ => "Find…",
    };
}
