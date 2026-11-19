using System.Text;
using StreamMark.Core.Parsing;
using StreamMark.Core.Remend;

namespace StreamMark.Core.Streaming;

/// <summary>
/// 流式 Markdown 文档：任意线程调用 <see cref="Append(string)"/> 追加文本，后台单线程合并解析，
/// 通过 <see cref="SnapshotReady"/>（在后台线程触发）或 <see cref="Latest"/> 取得最新快照。
/// </summary>
/// <remarks>
/// 解析进行中再有文本到达时只标记"脏"，当前解析结束后再处理一次最新文本，中间版本被跳过；
/// 因此解析频率随解析成本自动降低，界面线程从不阻塞。界面层应在每帧最多取一次 <see cref="Latest"/>，
/// 用 <see cref="SnapshotDiff"/> 与上次应用的快照比较后更新控件。
/// </remarks>
public sealed class StreamingDocument
{
    private readonly object _sourceLock = new();
    private readonly StringBuilder _source = new();
    private long _version;
    private int _sourceGeneration;
    private bool _completeRequested;

    private readonly object _gate = new();
    private bool _dirty;
    private bool _running;
    private TaskCompletionSource? _idle;

    // 以下字段只由工作线程访问
    private readonly DocumentComposer _composer;
    private int _composerGeneration;
    private long _processedVersion = -1;
    private bool _finished;

    private DocumentSnapshot _latest = DocumentSnapshot.Empty;

    public StreamingDocument(MarkdownProfile? profile = null, RemendOptions? remendOptions = null)
    {
        Profile = profile ?? MarkdownProfile.Default;
        _composer = new DocumentComposer(Profile, remendOptions);
    }

    /// <summary>文档使用的 Markdown 方言。</summary>
    public MarkdownProfile Profile { get; }

    /// <summary>最新快照（任意线程可读）。</summary>
    public DocumentSnapshot Latest => Volatile.Read(ref _latest);

    /// <summary>新快照就绪，在后台线程触发。处理函数应尽快返回（例如只请求下一帧刷新）。</summary>
    public event Action<DocumentSnapshot>? SnapshotReady;

    public bool IsComplete
    {
        get
        {
            lock (_sourceLock)
            {
                return _completeRequested;
            }
        }
    }

    /// <summary>
    /// 最新提交的源码版本（每次追加、<see cref="Complete"/>、<see cref="Reset"/>、<see cref="Load"/> 都会递增，跨重置也不回退）。
    /// 后台解析是异步的：<see cref="DocumentSnapshot.Version"/> 追上这个值，才说明最新提交的文本已经解析完。
    /// </summary>
    public long Version
    {
        get
        {
            lock (_sourceLock)
            {
                return _version;
            }
        }
    }

    public void Append(string text) => Append(text.AsSpan());

    public void Append(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return;
        }
        lock (_sourceLock)
        {
            if (_completeRequested)
            {
                throw new InvalidOperationException("The document has been completed; call Reset() before appending.");
            }
            _source.Append(text);
            _version++;
        }
        Schedule();
    }

    /// <summary>流结束。之后会做一次整篇重解析，产生 <see cref="DocumentSnapshot.IsComplete"/> 为 true 的最终快照。</summary>
    public void Complete()
    {
        lock (_sourceLock)
        {
            if (_completeRequested)
            {
                return;
            }
            _completeRequested = true;
            _version++;
        }
        Schedule();
    }

    /// <summary>清空文档（例如重新生成回答），可选地以新文本开始。</summary>
    public void Reset(string? text = null)
    {
        lock (_sourceLock)
        {
            _source.Clear();
            if (text is not null)
            {
                _source.Append(text);
            }
            _completeRequested = false;
            _sourceGeneration++;
            _version++;
        }
        Schedule();
    }

    /// <summary>
    /// 一次性加载完整文本（非流式消息）。清空、写入和标记完成在同一次加锁里完成，工作线程不会取到
    /// "已清空但还没完成"的中间状态，所以不会为这段文本产生一个未完成的快照。
    /// </summary>
    public void Load(string markdown)
    {
        lock (_sourceLock)
        {
            _source.Clear();
            _source.Append(markdown);
            _completeRequested = true;
            _sourceGeneration++;
            _version++;
        }
        Schedule();
    }

    /// <summary>
    /// 在调用线程上同步解析完整文本（用于按需解析的历史消息：视图第一次需要它时立即得到最终快照）。
    /// 只能用于尚未提交过文本的文档；解析期间其他线程追加的文本会在之后由后台工作者处理。
    /// </summary>
    public void LoadNow(string markdown)
    {
        lock (_gate)
        {
            if (_running || _dirty)
            {
                throw new InvalidOperationException("LoadNow requires a document with no pending work.");
            }
            // 占用工作者：期间到达的追加只标记脏，结束后再交给后台
            _running = true;
        }
        try
        {
            lock (_sourceLock)
            {
                _source.Clear();
                _source.Append(markdown);
                _completeRequested = true;
                _sourceGeneration++;
                _version++;
            }
            if (Process() is { } snapshot)
            {
                Volatile.Write(ref _latest, snapshot);
                SnapshotReady?.Invoke(snapshot);
            }
        }
        finally
        {
            if (TakeWork())
            {
                ThreadPool.UnsafeQueueUserWorkItem(static self => self.Run(), this, preferLocal: false);
            }
        }
    }

    /// <summary>当前源码长度。</summary>
    public int Length
    {
        get
        {
            lock (_sourceLock)
            {
                return _source.Length;
            }
        }
    }

    /// <summary>读取源码片段（越界部分被截断）。</summary>
    public string GetText(int start, int length)
    {
        lock (_sourceLock)
        {
            start = Math.Clamp(start, 0, _source.Length);
            length = Math.Clamp(length, 0, _source.Length - start);
            return _source.ToString(start, length);
        }
    }

    /// <summary>等待当前所有已提交的文本处理完毕。</summary>
    public Task WhenIdle()
    {
        lock (_gate)
        {
            if (!_running && !_dirty)
            {
                return Task.CompletedTask;
            }
            _idle ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _idle.Task;
        }
    }

    private void Schedule()
    {
        lock (_gate)
        {
            _dirty = true;
            if (_running)
            {
                return;
            }
            _running = true;
        }
        ThreadPool.UnsafeQueueUserWorkItem(static self => self.Run(), this, preferLocal: false);
    }

    private void Run()
    {
        while (TakeWork())
        {
            DocumentSnapshot? snapshot;
            try
            {
                snapshot = Process();
            }
            catch (Exception ex)
            {
                // 解析异常不应让流式渲染停止：保留上一个快照，等待下一次追加
                System.Diagnostics.Debug.WriteLine($"StreamMark: parse failed: {ex}");
                snapshot = null;
            }
            if (snapshot is not null)
            {
                Volatile.Write(ref _latest, snapshot);
                SnapshotReady?.Invoke(snapshot);
            }
        }
    }

    /// <summary>取走待处理标记；没有待处理的工作时结束工作者并唤醒等待空闲的调用方。</summary>
    private bool TakeWork()
    {
        TaskCompletionSource? idle;
        lock (_gate)
        {
            if (_dirty)
            {
                _dirty = false;
                return true;
            }
            _running = false;
            idle = _idle;
            _idle = null;
        }
        idle?.TrySetResult();
        return false;
    }

    private DocumentSnapshot? Process()
    {
        string text;
        int from;
        long version;
        bool complete;
        lock (_sourceLock)
        {
            if (_sourceGeneration != _composerGeneration)
            {
                _composer.Reset();
                _composerGeneration = _sourceGeneration;
                _processedVersion = -1;
                _finished = false;
            }
            version = _version;
            if (version == _processedVersion || _finished)
            {
                return null;
            }
            complete = _completeRequested;
            from = complete ? 0 : _composer.FrozenEnd;
            text = _source.ToString(from, _source.Length - from);
        }

        _processedVersion = version;
        if (complete)
        {
            _finished = true;
            return _composer.Finish(text, version);
        }
        return _composer.Update(text, from, version);
    }
}
