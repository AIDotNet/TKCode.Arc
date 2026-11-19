namespace StreamMark.Avalonia.Plugins;

public enum PluginDiagnosticKind
{
    /// <summary>后登记的插件覆盖了同名登记（设计内行为，记录下来便于排查「我的组件为什么没生效」）。</summary>
    Replaced,

    /// <summary>文档的方言与显示它的视图所用服务的方言不是同一个：插件登记的组件标签可能不会被解析。</summary>
    ProfileMismatch,
}

/// <summary>插件系统的诊断信息。</summary>
/// <param name="Kind">类别。</param>
/// <param name="Message">可读说明。</param>
/// <param name="Key">涉及的登记项，如 <c>block:details</c>、<c>fence:diff</c>、<c>math</c>。</param>
/// <param name="Plugin">登记者（插件的 <see cref="IMarkdownPlugin.Name"/>）。</param>
/// <param name="Previous">被覆盖的登记者。</param>
public sealed record PluginDiagnostic(PluginDiagnosticKind Kind, string Message, string? Key = null, string? Plugin = null, string? Previous = null);

/// <summary>插件的 <see cref="IMarkdownPlugin.Configure"/> 抛出了异常。</summary>
public sealed class PluginConfigurationException(string pluginName, Exception inner)
    : Exception($"Markdown plugin '{pluginName}' failed to configure: {inner.Message}", inner)
{
    public string PluginName { get; } = pluginName;
}
