namespace StreamMark.Avalonia.Extensions;

/// <summary>引用来源（<c>&lt;Citation id="1" /&gt;</c> 悬停卡片显示的内容）。</summary>
public sealed record CitationSource(string Title, string? Url = null, string? SiteName = null, string? Snippet = null);

/// <summary>
/// 宿主提供引用来源（例如联网搜索结果）：按引用 id 返回标题、地址、网站名与摘要。
/// 设置在 <see cref="MarkdownServices.Sources"/>；只在界面线程调用，应快速返回（结果由宿主预先准备好）。
/// </summary>
public interface ISourceProvider
{
    CitationSource? GetSource(string id);
}

/// <summary>基于字典的简单实现。</summary>
public sealed class DictionarySourceProvider(IReadOnlyDictionary<string, CitationSource> sources) : ISourceProvider
{
    public CitationSource? GetSource(string id) => sources.GetValueOrDefault(id);
}
