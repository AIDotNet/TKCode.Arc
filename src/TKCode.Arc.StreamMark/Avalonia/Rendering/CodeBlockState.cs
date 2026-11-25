using System.Collections.Immutable;
using System.Diagnostics;
using Avalonia.Threading;
using StreamMark.Core.Highlighting;

namespace StreamMark.Avalonia.Rendering;

/// <summary>
/// 一个代码块的共享状态：代码、行起点索引、增量高亮会话与结果。保存在文档的块状态存储中，
/// 同一代码块的所有视图（含长代码分段后的各段、回收后重建的视图）共用，避免重复切行和重复着色。只在界面线程访问。
/// </summary>
internal sealed class CodeBlockState
{
    private static readonly TimeSpan HighlightThrottle = TimeSpan.FromMilliseconds(40);

    private readonly SyncLock _highlightLock = new();
    private readonly List<int> _lineStarts = [0];
    private readonly List<string?> _lineTexts = [];
    private IHighlightSession? _session;
    private bool _highlightRunning;
    private string? _highlightPending;
    private bool _highlightPendingOpen;
    private CancellationTokenSource? _throttleCts;
    private long _lastHighlightTimestamp;

    private CodeBlockState(string? language, ISyntaxHighlighter? highlighter, HighlightThemeKind theme)
    {
        Language = language;
        Highlighter = highlighter;
        Theme = theme;
    }

    public string Code { get; private set; } = "";

    public string? Language { get; }

    public ISyntaxHighlighter? Highlighter { get; }

    public HighlightThemeKind Theme { get; }

    /// <summary>每行在 <see cref="Code"/> 中的起始偏移。</summary>
    public IReadOnlyList<int> LineStarts => _lineStarts;

    public int LineCount => _lineStarts.Count;

    /// <summary>最近一次着色结果（按行）以及对应的代码。</summary>
    public IReadOnlyList<ImmutableArray<HighlightToken>> Tokens { get; private set; } = [];

    public string TokensCode { get; private set; } = "";

    /// <summary>高亮结果更新（界面线程）。</summary>
    public event Action? HighlightUpdated;

    internal const string StateName = "code.state";

    /// <summary>取得（或创建）块的共享状态；语言、高亮器或明暗主题变化时重建。</summary>
    public static CodeBlockState For(RenderContext context, int blockKey, string? language, int segment = 0)
    {
        var highlighter = context.Plugins.Highlighter;
        var theme = context.Style.HighlightTheme;
        var stateKey = BlockView.StateKey(StateName, segment);
        var existing = context.State.Get<CodeBlockState?>(blockKey, stateKey, null);
        if (existing is not null && existing.Language == language && ReferenceEquals(existing.Highlighter, highlighter) && existing.Theme == theme)
        {
            return existing;
        }
        existing?.Dispose();
        var state = new CodeBlockState(language, highlighter, theme);
        context.State.Set(blockKey, stateKey, state);
        return state;
    }

    public void Update(string code, bool isOpen = false)
    {
        if (ReferenceEquals(code, Code) || (code == Code && Code.Length > 0))
        {
            if (!isOpen)
            {
                FlushOpenHighlight(code);
            }
            return;
        }
        // 只追加时从上一个完整行继续切分，保留前面已完成行的起点与切片缓存
        var span = code.AsSpan();
        var from = 0;
        if (code.Length >= Code.Length && span[..Code.Length].SequenceEqual(Code.AsSpan()) && _lineStarts.Count > 0)
        {
            var lastLine = _lineStarts.Count - 1;
            from = _lineStarts[lastLine];
            if (_lineTexts.Count > lastLine)
            {
                _lineTexts.RemoveRange(lastLine, _lineTexts.Count - lastLine);
            }
        }
        else
        {
            _lineStarts.Clear();
            _lineStarts.Add(0);
            _lineTexts.Clear();
        }
        var rest = span[from..];
        int nl;
        while ((nl = rest.IndexOf('\n')) >= 0)
        {
            from += nl + 1;
            _lineStarts.Add(from);
            rest = span[from..];
        }
        Code = code;
        RequestHighlight(code, isOpen);
    }

    public string LineText(int line)
    {
        if (line < _lineTexts.Count && _lineTexts[line] is { } cached)
        {
            return cached;
        }
        var start = _lineStarts[line];
        var end = line + 1 < _lineStarts.Count ? _lineStarts[line + 1] - 1 : Code.Length;
        if (end > start && Code[end - 1] == '\r')
        {
            end--;
        }
        var text = end <= start ? string.Empty : Code[start..end];
        while (_lineTexts.Count <= line)
        {
            _lineTexts.Add(null);
        }
        _lineTexts[line] = text;
        return text;
    }

    private void FlushOpenHighlight(string code)
    {
        CancellationTokenSource? cancel = null;
        lock (_highlightLock)
        {
            if (_highlightPending is not null || _throttleCts is not null)
            {
                _highlightPendingOpen = false;
                cancel = _throttleCts;
                _throttleCts = null;
            }
        }
        if (cancel is not null)
        {
            try
            {
                cancel.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }
        else if (!ReferenceEquals(TokensCode, code) && TokensCode != code)
        {
            RequestHighlight(code, isOpen: false);
        }
    }

    // 后台增量着色：同一时刻最多一个任务，期间到达的新代码合并为一次；开放流式代码块按约 25fps 节流，闭合后立即着色
    private void RequestHighlight(string code, bool isOpen)
    {
        if (Highlighter is not { } highlighter || string.IsNullOrEmpty(Language) || !highlighter.SupportsLanguage(Language))
        {
            return;
        }
        _session ??= highlighter.CreateSession(Language, Theme);
        CancellationTokenSource? cancel = null;
        var start = false;
        lock (_highlightLock)
        {
            _highlightPending = code;
            _highlightPendingOpen = isOpen;
            if (!isOpen && _throttleCts is not null)
            {
                cancel = _throttleCts;
                _throttleCts = null;
            }
            if (!_highlightRunning)
            {
                _highlightRunning = true;
                start = true;
            }
        }
        if (cancel is not null)
        {
            try
            {
                cancel.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }
        if (start)
        {
            var session = _session;
            _ = Task.Run(() => RunHighlightAsync(session));
        }
    }

    private async Task RunHighlightAsync(IHighlightSession session)
    {
        var waited = false;
        while (true)
        {
            string? code;
            var wait = TimeSpan.Zero;
            CancellationTokenSource? delayCts = null;
            lock (_highlightLock)
            {
                code = _highlightPending;
                var isOpen = _highlightPendingOpen;
                if (code is null)
                {
                    _highlightRunning = false;
                    return;
                }
                if (isOpen && !waited && _lastHighlightTimestamp != 0)
                {
                    var elapsed = Stopwatch.GetElapsedTime(_lastHighlightTimestamp);
                    if (elapsed < HighlightThrottle)
                    {
                        wait = HighlightThrottle - elapsed;
                        delayCts = new CancellationTokenSource();
                        _throttleCts = delayCts;
                    }
                }
                if (wait <= TimeSpan.Zero)
                {
                    _highlightPending = null;
                }
            }
            if (wait > TimeSpan.Zero && delayCts is not null)
            {
                try
                {
                    await Task.Delay(wait, delayCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    lock (_highlightLock)
                    {
                        if (ReferenceEquals(_throttleCts, delayCts))
                        {
                            _throttleCts = null;
                        }
                    }
                    delayCts.Dispose();
                }
                waited = true;
                continue;
            }
            waited = false;
            _lastHighlightTimestamp = Stopwatch.GetTimestamp();
            IReadOnlyList<ImmutableArray<HighlightToken>> tokens;
            try
            {
                tokens = session.Update(code);
            }
            catch (Exception)
            {
                continue;
            }
            Dispatcher.UIThread.Post(() =>
            {
                if (!ReferenceEquals(session, _session))
                {
                    return;
                }
                Tokens = tokens;
                TokensCode = code;
                HighlightUpdated?.Invoke();
            });
        }
    }

    /// <summary>某行当前可用的着色（着色期间代码又增长时，只对文本仍一致的行返回结果）。</summary>
    public ImmutableArray<HighlightToken> TokensFor(int line, string lineText)
    {
        if (line >= Tokens.Count)
        {
            return default;
        }
        if (ReferenceEquals(TokensCode, Code))
        {
            return Tokens[line];
        }
        // 着色所依据的代码中这一行的文本（零分配 Span 比较）
        if (line >= _lineStarts.Count)
        {
            return default;
        }
        var start = _lineStarts[line];
        if (start > TokensCode.Length)
        {
            return default;
        }
        var rest = TokensCode.AsSpan(start);
        var nl = rest.IndexOf('\n');
        var tokenLine = nl < 0 ? rest : rest[..nl];
        if (tokenLine.Length > 0 && tokenLine[^1] == '\r')
        {
            tokenLine = tokenLine[..^1];
        }
        return tokenLine.SequenceEqual(lineText.AsSpan()) ? Tokens[line] : default;
    }

    public void Dispose()
    {
        CancellationTokenSource? cancel;
        lock (_highlightLock)
        {
            _highlightPending = null;
            cancel = _throttleCts;
            _throttleCts = null;
        }
        if (cancel is not null)
        {
            try
            {
                cancel.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }
        _session?.Dispose();
        _session = null;
    }
}
