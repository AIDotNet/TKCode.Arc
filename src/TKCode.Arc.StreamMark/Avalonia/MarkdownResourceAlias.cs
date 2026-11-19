namespace StreamMark.Avalonia;

/// <summary>
/// 资源别名：把某个 <c>Md.*</c> 键指向宿主的另一个资源键（例如 TKCode.Arc 的设计 Token）。
/// 别名从 Markdown 控件本身开始查找，因此能找到只在窗口或主题作用域内注册的资源；
/// 目标值是 <see cref="global::Avalonia.Media.Color"/> 时自动转换为画刷。
/// </summary>
public sealed record MarkdownResourceAlias(object Key);
