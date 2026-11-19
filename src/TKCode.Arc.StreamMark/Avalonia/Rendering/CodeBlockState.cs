using System.Collections.Immutable;
using Avalonia.Threading;
using StreamMark.Core.Highlighting;

namespace StreamMark.Avalonia.Rendering;

/// <summary>
/// 一个代码块的共享状态：代码、行起点索引、增量高亮会话与结果。保存在文档的块状态存储中，
/// 同一代码块的所有视图（含长代码分段后的各段、回收后重建的视图）共用，避免重复切行和重复着色。只在界面线程访问。
/// </summary>
internal sealed class CodeBlockState
{
    private readonly object _highlightLock = new();
    private IHighlightSession? _session;
    private bool _highlightRunning;
    private string? _highlightPending;
    private int[] _lineStarts = [0];

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

    public int LineCount => _lineStarts.Length;

    /// <summary>最近一次着色结果（按行）以及对应的代码。</summary>
    public IReadOnlyList<ImmutableArray<HighlightToken>> Tokens { get; private set; } = [];

    public string TokensCode { get; private set; } = "";

    /// <summary>高亮结果更新（界面线程）。</summary>
    public event Action? HighlightUpdated;

    /// <summary>取得（或创建）块的共享状态；语言、高亮器或明暗主题变化时重建。</summary>
    public static CodeBlockState For(RenderContext context, int blockKey, string? language)
    {
        var highlighter = context.Plugins.Highlighter;
        var theme = context.Style.HighlightTheme;
        var existing = context.State.Get<CodeBlockState?>(blockKey, "code.state", null);
        if (existing is not null && existing.Language == language && ReferenceEquals(existing.Highlighter, highlighter) && existing.Theme == theme)
        {
            return existing;
        }
        existing?.Dispose();
        var state = new CodeBlockState(language, highlighter, theme);
        context.State.Set(blockKey, "code.state", state);
        return state;
    }

    public void Update(string code)
    {
        if (code == Code && Code.Length > 0)
        {
            return;
        }
        // 只追加时从上一个完整行继续切分
        var starts = new List<int>(_lineStarts.Length + 8);
        var from = 0;
        if (code.StartsWith(Code, StringComparison.Ordinal) && _lineStarts.Length > 0)
        {
            starts.AddRange(_lineStarts);
            from = starts[^1];
        }
        else
        {
            starts.Add(0);
        }
        for (var i = code.IndexOf('\n', from); i >= 0; i = code.IndexOf('\n', i + 1))
        {
            starts.Add(i + 1);
        }
        _lineStarts = [.. starts];
        Code = code;
        RequestHighlight(code);
    }

    public string LineText(int line)
    {
        var start = _lineStarts[line];
        var end = line + 1 < _lineStarts.Length ? _lineStarts[line + 1] - 1 : Code.Length;
        return Code[start..Math.Max(start, end)].TrimEnd('\r');
    }

    // 后台增量着色：同一时刻最多一个任务，期间到达的新代码合并为一次
    private void RequestHighlight(string code)
    {
        if (Highlighter is not { } highlighter || string.IsNullOrEmpty(Language) || !highlighter.SupportsLanguage(Language))
        {
            return;
        }
        _session ??= highlighter.CreateSession(Language, Theme);
        lock (_highlightLock)
        {
            _highlightPending = code;
            if (_highlightRunning)
            {
                return;
            }
            _highlightRunning = true;
        }
        var session = _session;
        _ = Task.Run(() => RunHighlight(session));
    }

    private void RunHighlight(IHighlightSession session)
    {
        while (true)
        {
            string? code;
            lock (_highlightLock)
            {
                code = _highlightPending;
                _highlightPending = null;
                if (code is null)
                {
                    _highlightRunning = false;
                    return;
                }
            }
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
        // 着色所依据的代码中这一行的文本
        var starts = _lineStarts;
        if (line >= starts.Length || starts[line] > TokensCode.Length)
        {
            return default;
        }
        var end = TokensCode.IndexOf('\n', starts[line]);
        var tokenLine = TokensCode[starts[line]..(end < 0 ? TokensCode.Length : end)].TrimEnd('\r');
        return tokenLine == lineText ? Tokens[line] : default;
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
    }
}
