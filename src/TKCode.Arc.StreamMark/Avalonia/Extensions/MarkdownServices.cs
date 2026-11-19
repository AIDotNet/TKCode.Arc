using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Avalonia;
using StreamMark.Avalonia.Images;
using StreamMark.Avalonia.Plugins;
using StreamMark.Avalonia.Transcript;
using StreamMark.Core.Highlighting;
using StreamMark.Core.Pacing;
using StreamMark.Core.Parsing;
using StreamMark.Core.Streaming;

namespace StreamMark.Avalonia.Extensions;

/// <summary>
/// 渲染扩展的集合：公式、代码高亮、图表、图片。通过可继承的附加属性 <see cref="ServicesProperty"/>
/// 设置在窗口或任意祖先上，其下所有 Markdown 视图共用。
/// </summary>
public sealed class MarkdownServices
{
    public static MarkdownServices Default { get; } = new();

    private PluginRegistry? _registry;
    private ExceptionDispatchInfo? _registryError;
    private readonly object _registryLock = new();
    private readonly ConditionalWeakTable<StreamingDocument, object> _mismatchReported = new();

    /// <summary>插件（公式、高亮、图表、组件等）。内置组件（details、think、kbd、引用等）总是启用。</summary>
    public IReadOnlyList<IMarkdownPlugin> Plugins { get; init; } = [];

    /// <summary>兼容写法：等同于注册一个只提供公式的插件。</summary>
    public IMathRenderer? Math { get; init; }

    /// <summary>兼容写法：等同于注册一个只提供代码高亮的插件。</summary>
    public ISyntaxHighlighter? Highlighter { get; init; }

    /// <summary>兼容写法：等同于注册只提供图表渲染的插件。</summary>
    public IReadOnlyList<IDiagramRenderer> Diagrams { get; init; } = [];

    private static ImagePipeline? s_defaultImages;

    /// <summary>HTML 中作者指定的颜色如何处理（默认：按作者颜色，但与背景对比度不足时改用主题颜色）。</summary>
    public HtmlColorMode HtmlColors { get; init; } = HtmlColorMode.Readable;

    /// <summary>链接安全策略：允许的协议、确认要求。</summary>
    public LinkPolicy Links { get; init; } = LinkPolicy.Default;

    /// <summary>引用来源（<c>&lt;Citation id&gt;</c> 悬停卡片与点击），没有时只显示标签。</summary>
    public ISourceProvider? Sources { get; init; }

    /// <summary>显示右键菜单（复制、复制为 Markdown、全选、查找）。</summary>
    public bool ContextMenu { get; init; } = true;

    /// <summary>右键菜单文字（每次打开菜单时调用，可随界面语言变化）。</summary>
    public Func<Selection.MarkdownMenuCommand, string> MenuText { get; init; } = Selection.MarkdownContextMenu.DefaultText;

    /// <summary>代码块选项：行号、折叠、下载。</summary>
    public CodeBlockOptions CodeBlocks { get; init; } = CodeBlockOptions.Default;

    /// <summary>
    /// 图片管线（加载策略、内存 / 磁盘缓存）。默认是进程共享的仅内存缓存实例；
    /// 宿主通常自建一个带磁盘缓存目录的实例（并负责释放）。
    /// </summary>
    public ImagePipeline Images { get; init; } = s_defaultImages ??= new ImagePipeline();

    /// <summary>
    /// 相对图片地址（如 <c>images/a.png</c>）的基准目录或地址，可继承：设在会话视图 / 文档预览上，
    /// 通常为项目根目录。本地文件还需在 <see cref="ImagePolicy.AllowedFileRoots"/> 中放行。
    /// </summary>
    public static readonly AttachedProperty<string?> ImageBaseUriProperty =
        AvaloniaProperty.RegisterAttached<MarkdownServices, AvaloniaObject, string?>("ImageBaseUri", null, inherits: true);

    public static string? GetImageBaseUri(AvaloniaObject target) => target.GetValue(ImageBaseUriProperty);

    public static void SetImageBaseUri(AvaloniaObject target, string? value) => target.SetValue(ImageBaseUriProperty, value);

    /// <summary>把图片地址解析为绝对地址：相对路径按 <paramref name="baseUri"/> 解析，绝对文件路径转为 file: 地址。</summary>
    public static string ResolveImageUrl(string url, string? baseUri)
    {
        url = url.Trim();
        if (url.Length == 0 || url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || Uri.TryCreate(url, UriKind.Absolute, out var absolute) && absolute.Scheme.Length > 1)
        {
            return url;
        }
        if (Path.IsPathRooted(url))
        {
            return new Uri(Path.GetFullPath(url)).AbsoluteUri;
        }
        if (string.IsNullOrEmpty(baseUri))
        {
            return url;
        }
        var root = Uri.TryCreate(baseUri, UriKind.Absolute, out var b) && b.Scheme.Length > 1 ? b : new Uri(Path.GetFullPath(baseUri) + Path.DirectorySeparatorChar);
        if (!root.AbsoluteUri.EndsWith('/'))
        {
            root = new Uri(root.AbsoluteUri + "/");
        }
        return Uri.TryCreate(root, url, out var resolved) ? resolved.AbsoluteUri : url;
    }

    /// <summary>
    /// 汇总后的插件注册表（首次访问时构建，只构建一次，任意线程访问都得到同一个实例）。
    /// 插件配置失败时抛出 <see cref="PluginConfigurationException"/>，之后每次访问都重新抛出同一个异常，不会重试。
    /// </summary>
    public PluginRegistry Registry
    {
        get
        {
            if (Volatile.Read(ref _registry) is { } registry)
            {
                return registry;
            }
            lock (_registryLock)
            {
                _registryError?.Throw();
                if (_registry is null)
                {
                    try
                    {
                        Volatile.Write(ref _registry, new PluginRegistry(this));
                    }
                    catch (PluginConfigurationException e)
                    {
                        _registryError = ExceptionDispatchInfo.Capture(e);
                        throw;
                    }
                }
                return _registry!;
            }
        }
    }

    /// <summary>
    /// 与插件匹配的 Markdown 方言。文档最好用 <see cref="CreateDocument"/> / <see cref="CreateStream"/> / <see cref="CreateEntry"/> 创建；
    /// 自己 new 时要传入它，否则插件登记的组件标签不会被解析。插件都不改语法时它就是 <see cref="MarkdownProfile.Default"/>。
    /// </summary>
    public MarkdownProfile Profile => Registry.Profile;

    /// <summary>用这组服务的方言创建文档。</summary>
    public StreamingDocument CreateDocument() => new(Profile);

    /// <summary>用这组服务的方言创建流式回答（参数同 <see cref="MarkdownStream"/> 的构造函数）。</summary>
    public MarkdownStream CreateStream(StreamPacerOptions? pacing = null, bool smooth = true, TimeSpan? fadeDuration = null) =>
        new(pacing, smooth, fadeDuration, Profile);

    /// <summary>用这组服务的方言创建静态会话消息（同 <see cref="MarkdownEntry.FromText"/>）。</summary>
    public MarkdownEntry CreateEntry(string markdown, object? tag = null) => MarkdownEntry.FromText(markdown, Profile, tag);

    /// <summary>用这组服务的方言创建按需解析的会话消息（同 <see cref="MarkdownEntry.FromTextDeferred"/>）。</summary>
    public MarkdownEntry CreateDeferredEntry(string markdown, object? tag = null) => MarkdownEntry.FromTextDeferred(markdown, Profile, tag);

    /// <summary>
    /// 运行期诊断（目前是 <see cref="PluginDiagnosticKind.ProfileMismatch"/>：宿主自己创建的文档没有用这组服务的方言）。
    /// 在界面线程触发；同一个文档只报告一次。同时写入 <see cref="System.Diagnostics.Trace"/>。
    /// 构建期的诊断见 <see cref="PluginRegistry.Diagnostics"/>。
    /// </summary>
    public event Action<PluginDiagnostic>? Diagnostic;

    /// <summary>文档的方言不是这组服务的方言时报告一次（视图拿到新的渲染上下文时调用，不在每帧路径上）。</summary>
    internal void CheckProfile(StreamingDocument document)
    {
        if (ReferenceEquals(document.Profile, Profile) || _mismatchReported.TryGetValue(document, out _))
        {
            return;
        }
        _mismatchReported.AddOrUpdate(document, this);
        var diagnostic = new PluginDiagnostic(PluginDiagnosticKind.ProfileMismatch,
            "A Markdown document was created with a different MarkdownProfile than the MarkdownServices displaying it; "
            + "plugin component tags may not be parsed. Create documents with MarkdownServices.CreateStream / CreateEntry / CreateDocument "
            + "or pass MarkdownServices.Profile.");
        System.Diagnostics.Trace.TraceWarning(diagnostic.Message);
        Diagnostic?.Invoke(diagnostic);
    }

    public IDiagramRenderer? FindDiagramRenderer(string? language) => Registry.FindDiagramRenderer(language);

    public static readonly AttachedProperty<MarkdownServices> ServicesProperty =
        AvaloniaProperty.RegisterAttached<MarkdownServices, AvaloniaObject, MarkdownServices>("Services", Default, inherits: true);

    public static MarkdownServices GetServices(AvaloniaObject target) => target.GetValue(ServicesProperty);

    public static void SetServices(AvaloniaObject target, MarkdownServices value) => target.SetValue(ServicesProperty, value);
}

/// <summary>HTML 颜色（<c>color</c>、<c>background</c>、<c>&lt;font color&gt;</c>）的处理方式。</summary>
public enum HtmlColorMode
{
    /// <summary>使用作者颜色；文字颜色与所在背景对比度不足时（例如暗色主题中的 #333）改用主题文字色。</summary>
    Readable,

    /// <summary>完全按作者颜色。</summary>
    Exact,

    /// <summary>忽略所有作者颜色，只用主题颜色。</summary>
    Ignore,
}
