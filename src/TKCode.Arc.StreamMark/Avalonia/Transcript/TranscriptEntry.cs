using StreamMark.Avalonia.Rendering;
using StreamMark.Core.Parsing;
using StreamMark.Core.Streaming;

namespace StreamMark.Avalonia.Transcript;

/// <summary>会话中的一条内容。</summary>
public abstract class TranscriptEntry;

/// <summary>一条 Markdown 消息（静态或流式）。每个顶层块成为会话面板中的一行，可独立虚拟化。</summary>
public sealed class MarkdownEntry : TranscriptEntry
{
    public MarkdownEntry(StreamingDocument document, StreamingState? state = null)
    {
        Document = document;
        State = state;
    }

    /// <summary>流式消息：经节奏控制器平滑，带渐显与光标。</summary>
    public MarkdownEntry(MarkdownStream stream) : this(stream.Document, stream.State)
    {
        Stream = stream;
    }

    /// <summary>流式消息的节奏控制器（会话面板把显示帧来源交给它）。</summary>
    public MarkdownStream? Stream { get; }

    /// <summary>静态消息。<paramref name="profile"/> 应与宿主的插件匹配（<c>MarkdownServices.Profile</c>）。</summary>
    public static MarkdownEntry FromText(string markdown, MarkdownProfile? profile = null, object? tag = null)
    {
        var document = new StreamingDocument(profile);
        document.Load(markdown);
        return new MarkdownEntry(document) { Tag = tag };
    }

    /// <summary>
    /// 按需解析的静态消息（例如会话历史）：创建时只保存文本，会话面板第一次需要实例化它的行
    /// （或选区 / 复制跨过它）时才在界面线程上同步解析。解析前按字符数与换行数估算高度。
    /// </summary>
    public static MarkdownEntry FromTextDeferred(string markdown, MarkdownProfile? profile = null, object? tag = null) =>
        new(new StreamingDocument(profile)) { Tag = tag, _deferred = markdown };

    private string? _deferred;

    /// <summary>是否已解析（非按需创建的条目始终为 true）。</summary>
    public bool IsParsed => Volatile.Read(ref _deferred) is null;

    /// <summary>尚未解析时的原文；已解析时为 null。</summary>
    public string? DeferredText => Volatile.Read(ref _deferred);

    /// <summary>立即解析按需条目（已解析时什么也不做）。</summary>
    public void EnsureParsed()
    {
        if (Interlocked.Exchange(ref _deferred, null) is { } text)
        {
            Document.LoadNow(text);
        }
    }

    public StreamingDocument Document { get; }

    public StreamingState? State { get; }

    /// <summary>宿主附加的数据（例如消息 ID）。</summary>
    public object? Tag { get; init; }
}

/// <summary>
/// 宿主提供的任意内容（用户消息气泡、工具调用卡片、推理过程折叠块等）。
/// <see cref="Content"/> 是控件时直接使用；否则通过数据模板构建。整条内容作为一行虚拟化。
/// </summary>
public sealed class ContentEntry(object content) : TranscriptEntry
{
    public object Content { get; } = content;
}
