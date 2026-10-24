using System.Collections.Immutable;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using StreamMark.Core.Highlighting;

namespace TKCode.Arc;

/// <summary>
/// A fenced code block of an answer: a 38px header with the file name (12, secondary) and the language (12, muted) on the
/// left and a plain Copy button on the right over a hairline, then the code in the monospace font at 13 / 21.45,
/// scrolling sideways in a focusable area. 1px border, radius 18, filled with 55% of the muted surface over the page.
/// Syntax colors are the repository's <see cref="CodeBlock"/> palette (the AI group's decision), through the public
/// <see cref="MarkdownHighlightPlugin"/> highlighter; comments stay upright as in Arc. While streaming, code arrives a
/// whole line at a time and each new line fades in.
/// </summary>
[TemplatePart(CopyPart, typeof(CopyButton))]
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(TextPart, typeof(AiAnswerCodeText))]
[PseudoClasses(CodeFocusPseudoClass, NoFilePseudoClass)]
internal sealed class AiAnswerCodeBlock : TemplatedControl
{
    public const string CopyPart = "PART_Copy";
    public const string ScrollPart = "PART_Scroll";
    public const string TextPart = "PART_Text";
    public const string CodeFocusPseudoClass = ":code-focus-visible";
    public const string NoFilePseudoClass = ":no-file";

    /// <summary>The code.</summary>
    public static readonly DirectProperty<AiAnswerCodeBlock, string> CodeProperty =
        AvaloniaProperty.RegisterDirect<AiAnswerCodeBlock, string>(nameof(Code), o => o.Code);

    /// <summary>The file name shown in the header (the language's name when the fence names no file).</summary>
    public static readonly DirectProperty<AiAnswerCodeBlock, string> FileLabelProperty =
        AvaloniaProperty.RegisterDirect<AiAnswerCodeBlock, string>(nameof(FileLabel), o => o.FileLabel);

    /// <summary>The language's name shown after the file name.</summary>
    public static readonly DirectProperty<AiAnswerCodeBlock, string> LanguageLabelProperty =
        AvaloniaProperty.RegisterDirect<AiAnswerCodeBlock, string>(nameof(LanguageLabel), o => o.LanguageLabel);

    /// <summary>The copy button's labels.</summary>
    public static readonly DirectProperty<AiAnswerCodeBlock, string> CopyLabelProperty =
        AvaloniaProperty.RegisterDirect<AiAnswerCodeBlock, string>(nameof(CopyLabel), o => o.CopyLabel);

    /// <inheritdoc cref="CopyLabelProperty"/>
    public static readonly DirectProperty<AiAnswerCodeBlock, string> CopiedLabelProperty =
        AvaloniaProperty.RegisterDirect<AiAnswerCodeBlock, string>(nameof(CopiedLabel), o => o.CopiedLabel);

    /// <inheritdoc cref="CopyLabelProperty"/>
    public static readonly DirectProperty<AiAnswerCodeBlock, string> FailedLabelProperty =
        AvaloniaProperty.RegisterDirect<AiAnswerCodeBlock, string>(nameof(FailedLabel), o => o.FailedLabel);

    /// <summary>The hairline under the header.</summary>
    public static readonly StyledProperty<IBrush?> HairlineBrushProperty =
        AvaloniaProperty.Register<AiAnswerCodeBlock, IBrush?>(nameof(HairlineBrush));

    private readonly AiAnswerDocument _owner;
    private string _code = string.Empty;
    private string _language = string.Empty;
    private string _fileName = string.Empty;
    private string _fileLabel = string.Empty;
    private string _languageLabel = string.Empty;
    private string _copyLabel = string.Empty;
    private string _copiedLabel = string.Empty;
    private string _failedLabel = string.Empty;
    private CopyButton? _copy;
    private ScrollViewer? _scroll;
    private AiAnswerCodeText? _text;
    private bool _fadePending;
    private int _fadeFrom;

    public AiAnswerCodeBlock(AiAnswerDocument owner)
    {
        _owner = owner;
        ApplyBrushes(owner);
        RefreshLabels();
    }

    protected override Type StyleKeyOverride => typeof(AiAnswerCodeBlock);

    /// <inheritdoc cref="CodeProperty"/>
    public string Code { get => _code; private set => SetAndRaise(CodeProperty, ref _code, value); }

    /// <summary>The highlighting language (the fence's first word).</summary>
    public string Language => _language;

    /// <inheritdoc cref="FileLabelProperty"/>
    public string FileLabel { get => _fileLabel; private set => SetAndRaise(FileLabelProperty, ref _fileLabel, value); }

    /// <inheritdoc cref="LanguageLabelProperty"/>
    public string LanguageLabel { get => _languageLabel; private set => SetAndRaise(LanguageLabelProperty, ref _languageLabel, value); }

    /// <inheritdoc cref="CopyLabelProperty"/>
    public string CopyLabel { get => _copyLabel; private set => SetAndRaise(CopyLabelProperty, ref _copyLabel, value); }

    /// <inheritdoc cref="CopiedLabelProperty"/>
    public string CopiedLabel { get => _copiedLabel; private set => SetAndRaise(CopiedLabelProperty, ref _copiedLabel, value); }

    /// <inheritdoc cref="FailedLabelProperty"/>
    public string FailedLabel { get => _failedLabel; private set => SetAndRaise(FailedLabelProperty, ref _failedLabel, value); }

    /// <inheritdoc cref="HairlineBrushProperty"/>
    public IBrush? HairlineBrush { get => GetValue(HairlineBrushProperty); set => SetValue(HairlineBrushProperty, value); }

    /// <summary>The code text (once the template is applied).</summary>
    internal AiAnswerCodeText? CodeText => _text;

    /// <summary>The copy button (once the template is applied).</summary>
    internal CopyButton? CopyButton => _copy;

    /// <summary>The code area (once the template is applied).</summary>
    internal ScrollViewer? CodeArea => _scroll;

    /// <summary>Shows the code of <paramref name="block"/>; lines added since the last update fade in with <paramref name="fade"/>.</summary>
    public void Update(AiAnswerBlock block, bool fade)
    {
        var languageChanged = !string.Equals(_language, block.Language, StringComparison.Ordinal);
        _language = block.Language;
        _fileName = block.FileName;
        var previous = Code;
        Code = block.Code;
        RefreshLabels();
        if (_text is not null)
        {
            _text.SetCode(block.Code, block.Language, fade, languageChanged);
        }
        else if (fade)
        {
            // Before the template: the first lines fade in once the text exists.
            _fadePending = true;
            _fadeFrom = Math.Min(_fadeFrom, previous.Length);
        }
    }

    /// <summary>The brushes of the document: the fill and the hairline.</summary>
    public void ApplyBrushes(AiAnswerDocument owner)
    {
        Background = owner.CodeSurfaceBrush;
        HairlineBrush = owner.HairlineBrush;
        _text?.SetTheme(owner.IsDarkTheme);
    }

    /// <summary>The header and copy labels and the code area's accessible name, from the document's words.</summary>
    public void RefreshLabels()
    {
        var languageName = LanguageNames.Display(_language);
        FileLabel = _fileName.Length > 0 ? _fileName : languageName;
        LanguageLabel = _fileName.Length > 0 ? languageName : string.Empty;
        PseudoClasses.Set(NoFilePseudoClass, _fileName.Length == 0);
        CopyLabel = _owner.CopyCodeLabel ?? string.Empty;
        CopiedLabel = _owner.CopiedCodeLabel ?? string.Empty;
        FailedLabel = _owner.CopyFailedCodeLabel ?? string.Empty;
        var subject = FileLabel.Length > 0 ? FileLabel : languageName;
        if (_scroll is not null)
        {
            AutomationProperties.SetName(_scroll, _owner.CodeName?.Invoke(subject));
        }
        if (_copy is not null)
        {
            AutomationProperties.SetName(_copy, CopyLabel.Length > 0 ? CopyLabel : null);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_copy is not null)
        {
            _copy.Copied -= OnCopied;
        }
        if (_scroll is not null)
        {
            _scroll.GotFocus -= OnScrollFocus;
            _scroll.LostFocus -= OnScrollBlur;
        }
        _copy = e.NameScope.Find<CopyButton>(CopyPart);
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        _text = e.NameScope.Find<AiAnswerCodeText>(TextPart);
        if (_copy is not null)
        {
            _copy.Copied += OnCopied;
        }
        if (_scroll is not null)
        {
            _scroll.GotFocus += OnScrollFocus;
            _scroll.LostFocus += OnScrollBlur;
        }
        if (_text is not null)
        {
            _text.SetTheme(_owner.IsDarkTheme);
            _text.SetCode(Code, _language, false, true);
            if (_fadePending)
            {
                _text.Fade(_fadeFrom);
                _fadePending = false;
            }
        }
        RefreshLabels();
    }

    private void OnCopied(object? sender, EventArgs e) =>
        RaiseEvent(new AiAnswerCopiedEventArgs(AiAnswerDocument.CodeCopiedEvent, this, AiAnswerCopyKind.Code, Code));

    private void OnScrollFocus(object? sender, FocusChangedEventArgs e) =>
        PseudoClasses.Set(CodeFocusPseudoClass, e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional);

    private void OnScrollBlur(object? sender, FocusChangedEventArgs e) => PseudoClasses.Set(CodeFocusPseudoClass, false);

    /// <summary>Readable names of common fence languages (the header shows "YAML" for <c>yaml</c>); others as written.</summary>
    internal static class LanguageNames
    {
        public static string Display(string language) => language.ToLowerInvariant() switch
        {
            "" => string.Empty,
            "js" or "javascript" or "mjs" or "cjs" => "JavaScript",
            "jsx" => "JSX",
            "ts" or "typescript" => "TypeScript",
            "tsx" => "TSX",
            "yaml" or "yml" => "YAML",
            "json" => "JSON",
            "cs" or "csharp" or "c#" => "C#",
            "py" or "python" => "Python",
            "sh" or "bash" or "shell" or "zsh" => "Shell",
            "ps1" or "powershell" => "PowerShell",
            "html" => "HTML",
            "css" => "CSS",
            "xml" => "XML",
            "axaml" or "xaml" => "XAML",
            "sql" => "SQL",
            "go" => "Go",
            "rs" or "rust" => "Rust",
            "java" => "Java",
            "kt" or "kotlin" => "Kotlin",
            "rb" or "ruby" => "Ruby",
            "md" or "markdown" => "Markdown",
            "toml" => "TOML",
            "dockerfile" => "Dockerfile",
            _ => language
        };
    }
}

/// <summary>
/// The code of an <see cref="AiAnswerCodeBlock"/>: selectable, one run per highlighted token, new lines fading in as a
/// batch. Highlighting runs on a worker (a TextMate session per block, one update at a time) and repaints when done.
/// </summary>
internal sealed class AiAnswerCodeText : SelectableTextBlock
{
    private static readonly ISyntaxHighlighter s_highlighter = new MarkdownHighlightPlugin().Highlighter;

    private readonly AiAnswerFades _fades;
    private readonly Dictionary<uint, IBrush> _brushes = new();
    private string _code = string.Empty;
    private string _language = string.Empty;
    private bool _dark;
    private IHighlightSession? _session;
    private string? _sessionKey;
    private IReadOnlyList<ImmutableArray<HighlightToken>>? _tokens;
    private string _tokensCode = string.Empty;
    private bool _busy;
    private bool _releaseWhenIdle;
    private int _version;
    private TaskCompletionSource? _highlighted;

    static AiAnswerCodeText()
    {
        // Selectable, but the Tab stop is the scrolling area around it (Arc: the focusable pre).
        KeyboardNavigation.IsTabStopProperty.OverrideDefaultValue<AiAnswerCodeText>(false);
    }

    public AiAnswerCodeText()
    {
        _fades = new AiAnswerFades(this);
        Inlines = new InlineCollection();
    }

    protected override Type StyleKeyOverride => typeof(AiAnswerCodeText);

    /// <summary>The code shown.</summary>
    public string Code => _code;

    /// <summary>Completes when the code shown has been highlighted (or cannot be).</summary>
    internal Task WhenHighlighted => _highlighted?.Task ?? Task.CompletedTask;

    /// <summary>The highlighted tokens per line in use (tests).</summary>
    internal IReadOnlyList<ImmutableArray<HighlightToken>>? Tokens => _tokens;

    /// <summary>The fades of new lines (tests).</summary>
    internal AiAnswerFades Fades => _fades;

    /// <summary>Whether the block holds a highlighting session (tests).</summary>
    internal bool HasSession => _session is not null;

    /// <summary>Shows <paramref name="code"/>; with <paramref name="fade"/> what was added fades in.</summary>
    public void SetCode(string code, string language, bool fade, bool languageChanged)
    {
        if (string.Equals(code, _code, StringComparison.Ordinal) && !languageChanged)
        {
            return;
        }
        var keep = 0;
        var n = Math.Min(code.Length, _code.Length);
        while (keep < n && code[keep] == _code[keep])
        {
            keep++;
        }
        _code = code;
        _language = language;
        BuildRuns();
        if (fade)
        {
            _fades.Add(keep, code.Length);
        }
        else
        {
            _fades.Truncate(keep);
        }
        RequestHighlight();
    }

    /// <summary>Fades in the code from <paramref name="from"/> (the first lines of a block whose template came late).</summary>
    public void Fade(int from) => _fades.Add(Math.Clamp(from, 0, _code.Length), _code.Length);

    /// <summary>Light or dark: the palette of the highlighting session.</summary>
    public void SetTheme(bool dark)
    {
        if (_dark == dark)
        {
            return;
        }
        _dark = dark;
        _tokens = null;
        BuildRuns();
        RequestHighlight();
    }

    private void RequestHighlight()
    {
        _version++;
        if (_highlighted is null || _highlighted.Task.IsCompleted)
        {
            _highlighted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        if (_code.Length == 0 || string.IsNullOrEmpty(_language) || !s_highlighter.SupportsLanguage(_language))
        {
            _tokens = null;
            BuildRuns();
            _highlighted.TrySetResult();
            return;
        }
        if (_busy)
        {
            return;
        }
        _ = HighlightAsync();
    }

    private async Task HighlightAsync()
    {
        _busy = true;
        try
        {
            while (true)
            {
                var version = _version;
                var code = _code;
                var key = _language + (_dark ? "/dark" : "/light");
                if (_session is null || _sessionKey != key)
                {
                    _session?.Dispose();
                    _session = s_highlighter.CreateSession(_language, _dark ? HighlightThemeKind.Dark : HighlightThemeKind.Light);
                    _sessionKey = key;
                }
                var session = _session;
                IReadOnlyList<ImmutableArray<HighlightToken>>? tokens;
                try
                {
                    tokens = await Task.Run(() => session.Update(code));
                }
                catch (Exception)
                {
                    // A grammar that cannot load leaves the code plain.
                    tokens = null;
                }
                if (version == _version)
                {
                    _tokens = tokens;
                    _tokensCode = code;
                    BuildRuns();
                    _highlighted?.TrySetResult();
                    return;
                }
            }
        }
        finally
        {
            _busy = false;
            if (_releaseWhenIdle)
            {
                // Detached while the worker had the session: let it go now.
                _releaseWhenIdle = false;
                ReleaseSession();
            }
        }
    }

    private void ReleaseSession()
    {
        _session?.Dispose();
        _session = null;
        _sessionKey = null;
    }

    /// <summary>One run per token of the lines that have tokens for this code; the rest in the text color.</summary>
    private void BuildRuns()
    {
        var inlines = Inlines!;
        inlines.Clear();
        if (_code.Length == 0)
        {
            return;
        }
        var tokens = _tokens;
        var lineStart = 0;
        var line = 0;
        while (lineStart <= _code.Length)
        {
            var newline = _code.IndexOf('\n', lineStart);
            var lineEnd = newline < 0 ? _code.Length : newline;
            var cursor = lineStart;
            if (tokens is not null && line < tokens.Count && SameLine(lineStart, lineEnd))
            {
                foreach (var token in tokens[line])
                {
                    var start = lineStart + token.Start;
                    var end = Math.Min(lineEnd, start + token.Length);
                    if (start < cursor || start >= end)
                    {
                        continue;
                    }
                    if (start > cursor)
                    {
                        inlines.Add(new Run(_code[cursor..start]));
                    }
                    inlines.Add(new Run(_code[start..end]) { Foreground = BrushFor(token.Foreground) });
                    cursor = end;
                }
            }
            if (lineEnd > cursor)
            {
                inlines.Add(new Run(_code[cursor..lineEnd]));
            }
            if (newline < 0)
            {
                break;
            }
            inlines.Add(new Run("\n"));
            lineStart = newline + 1;
            line++;
        }
    }

    /// <summary>Whether the tokens were made for this line as it is now (a line that grew waits for its new tokens).</summary>
    private bool SameLine(int start, int end) =>
        end <= _tokensCode.Length && (end == _tokensCode.Length || _tokensCode[end] == '\n')
        && _code.AsSpan(start, end - start).SequenceEqual(_tokensCode.AsSpan(start, end - start));

    private IBrush BrushFor(uint argb)
    {
        if (!_brushes.TryGetValue(argb, out var brush))
        {
            brush = new ImmutableSolidColorBrush(Color.FromUInt32(argb));
            _brushes[argb] = brush;
        }
        return brush;
    }

    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        var layout = TextLayout;
        var start = Math.Min(SelectionStart, SelectionEnd);
        var length = Math.Abs(SelectionEnd - SelectionStart);
        if (length > 0 && SelectionBrush is { } selection)
        {
            foreach (var rect in layout.HitTestTextRange(start, length))
            {
                context.FillRectangle(selection, rect.Translate(new Vector(origin.X, origin.Y)));
            }
        }
        _fades.Draw(context, layout, origin);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _releaseWhenIdle = false;
        _fades.OnAttached();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_busy)
        {
            _releaseWhenIdle = true;
        }
        else
        {
            ReleaseSession();
        }
    }
}
