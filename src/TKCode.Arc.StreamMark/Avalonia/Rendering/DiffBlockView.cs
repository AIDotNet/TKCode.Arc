using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.Utilities;
using StreamMark.Avalonia.Extensions;
using StreamMark.Avalonia.Selection;
using StreamMark.Core.Diff;
using StreamMark.Core.Highlighting;
using StreamMark.Core.Model;

namespace StreamMark.Avalonia.Rendering;

/// <summary>diff 的显示方式。</summary>
public enum DiffViewMode
{
    /// <summary>
    /// 与 GitHub Markdown 中的 <c>```diff</c> 一致（默认）：原样逐行显示，+ 行绿底、- 行红底、<c>@@</c> 行淡蓝底，
    /// 文件头等元数据行为灰色；没有行号、文件标题栏与统计。
    /// </summary>
    Markdown,

    /// <summary>统一视图：旧 / 新两列行号，删除与新增行上下排列。</summary>
    Unified,

    /// <summary>并排视图：左旧右新，删除与新增行配对对齐。</summary>
    Split,
}

/// <summary>
/// <c>```diff</c> / <c>```patch</c> 代码块：解析 unified diff，按文件显示标题与增删统计、区段头、行号、增删背景与行内改动，
/// 支持统一 / 并排两种视图（头部切换，保存在块状态中；宽度不足时并排视图退回统一视图），按文件扩展名语法着色。
/// 整块是一个选区片段，文本为原始 diff；每个显示单元格映射回原始行，复制得到的就是 diff 源码。
/// </summary>
public sealed class DiffBlockView : BlockView
{
    internal const string ModeState = "diff.mode";
    private const double HeaderHeight = 34;

    private readonly IconButton _mode = new();
    private readonly IconButton _copy = new() { Icon = MarkdownIcons.Copy };
    private readonly DiffBodyView _body = new();
    private TextLayout? _title;
    private TextLayout? _stats;
    private string? _code;
    private DiffDocument _document = new([]);
    private DispatcherTimer? _copiedTimer;

    public DiffBlockView()
    {
        AddChild(_body);
        AddChild(_mode);
        AddChild(_copy);
        _mode.Click += (_, _) => Mode = Mode == DiffViewMode.Split ? StackedMode : DiffViewMode.Split;
        _copy.Click += async (_, _) => await CopyAsync();
        global::Avalonia.Automation.AutomationProperties.SetName(_copy, "Copy");
    }

    /// <summary>当前选择的显示方式（宽度不足时实际以统一视图显示，见 <see cref="EffectiveMode"/>）。</summary>
    public DiffViewMode Mode
    {
        get => _body.Mode;
        set
        {
            if (_body.Mode == value)
            {
                return;
            }
            _body.Mode = value;
            Context?.State.Set(TopKey, ModeState, value);
            if (Context is not null && Block is not null)
            {
                // 标题随方式变化
                var segment = _body.Key.Segment;
                Update(Context, Block, TopKey, ref segment);
            }
            UpdateModeButton();
            InvalidateMeasure();
        }
    }

    // 从并排视图切回时使用的非并排方式：宿主默认值（默认值本身是并排时用 Markdown 方式）
    private DiffViewMode StackedMode =>
        Context?.Services.CodeBlocks.DiffMode is { } mode && mode != DiffViewMode.Split ? mode : DiffViewMode.Markdown;

    /// <summary>实际使用的显示方式。</summary>
    public DiffViewMode EffectiveMode => _body.EffectiveMode;

    public DiffDocument Document => _document;

    public string Code => _code ?? "";

    private void UpdateModeButton()
    {
        // 图标表示"切换到"的方式
        var target = _body.Mode == DiffViewMode.Split ? StackedMode : DiffViewMode.Split;
        _mode.Icon = target == DiffViewMode.Split ? DiffIcons.Split : DiffIcons.Unified;
        _mode.IsActive = _body.Mode == DiffViewMode.Split;
        if (Context is not null)
        {
            ToolTip.SetTip(_mode, Context.Services.CodeBlocks.DiffModeText(target));
        }
        _mode.InvalidateVisual();
    }

    protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged)
    {
        var code = (CodeBlock)block;
        var style = context.Style;
        if (!ReferenceEquals(_code, code.Code) && _code != code.Code)
        {
            _code = code.Code;
            _document = UnifiedDiff.Parse(code.Code);
        }
        _body.Mode = context.State.Get(topKey, ModeState, context.Services.CodeBlocks.DiffMode);
        UpdateModeButton();
        foreach (var button in new[] { _mode, _copy })
        {
            button.Foreground = style.SecondaryForeground;
            button.HoverBackground = style.HoverBackground;
        }
        var files = _document.Files.Length;
        // Markdown 方式与普通代码块一样只显示语言；其它方式显示文件与增删统计
        var plain = _body.Mode == DiffViewMode.Markdown;
        var title = plain ? code.Language ?? "diff" : files switch
        {
            0 => "diff",
            1 when _document.Files[0].DisplayPath is { } path => "diff · " + path,
            _ => $"diff · {files} files",
        };
        _title = new TextLayout(title, new Typeface(style.MonospaceFontFamily), style.CodeFontSize * 0.92, style.SecondaryForeground,
            textTrimming: TextTrimming.CharacterEllipsis, maxWidth: 420);
        _stats = plain ? null : DiffBodyView.StatsLayout(style, _document.Additions, _document.Deletions);
        _body.Set(context, KeyFor(context, topKey, ref segment), code.Code, _document, topKey);
        InvalidateMeasure();
        InvalidateVisual();
    }

    private async Task CopyAsync()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }
        await global::Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clipboard, Code);
        _copy.Icon = MarkdownIcons.Check;
        _copiedTimer?.Stop();
        _copiedTimer = new DispatcherTimer(TimeSpan.FromSeconds(1.5), DispatcherPriority.Normal, (_, _) =>
        {
            _copy.Icon = MarkdownIcons.Copy;
            _copiedTimer?.Stop();
        });
        _copiedTimer.Start();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _mode.Measure(Size.Infinity);
        _copy.Measure(Size.Infinity);
        _body.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : _body.DesiredSize.Width;
        // 底部留出圆角
        return new Size(width, HeaderHeight + _body.DesiredSize.Height + 6);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = finalSize.Width - 6;
        foreach (var button in new[] { _copy, _mode })
        {
            x -= button.DesiredSize.Width;
            button.Arrange(new Rect(x, (HeaderHeight - button.DesiredSize.Height) / 2, button.DesiredSize.Width, button.DesiredSize.Height));
            x -= 2;
        }
        _body.Arrange(new Rect(0, HeaderHeight, finalSize.Width, _body.DesiredSize.Height));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (Context is null)
        {
            return;
        }
        var style = Context.Style;
        var r = style.CornerRadius.TopLeft;
        context.DrawRectangle(style.CodeBackground, new Pen(style.CodeBorderBrush, 1), new Rect(Bounds.Size).Deflate(0.5), r, r);
        if (_title is not null)
        {
            _title.Draw(context, new Point(14, (HeaderHeight - _title.Height) / 2));
            if (_stats is not null)
            {
                _stats.Draw(context, new Point(14 + _title.WidthIncludingTrailingWhitespace + 10, (HeaderHeight - _stats.Height) / 2));
            }
        }
    }

    public override IEnumerable<ISelectableSegment> Segments() => [_body];
}

internal static class DiffIcons
{
    public static Geometry Split { get; } = Geometry.Parse("M2.5,2.5 L13.5,2.5 L13.5,13.5 L2.5,13.5 Z M8,2.5 L8,13.5");

    public static Geometry Unified { get; } = Geometry.Parse("M2.5,2.5 L13.5,2.5 L13.5,13.5 L2.5,13.5 Z M2.5,8 L13.5,8");
}

/// <summary>diff 的正文：文件标题、区段头与代码行；两种视图共用，负责命中测试与选区绘制。</summary>
public sealed class DiffBodyView : Control, ISelectableSegment
{
    private const double MarkerWidth = 16;
    // 标记与正文之间的间距
    private const double TextGap = 4;
    private const double FileRowHeight = 30;

    private enum RowKind : byte
    {
        File,
        Hunk,
        Line,
    }

    /// <summary>Markdown 方式中一行的色调。</summary>
    private enum Tone : byte
    {
        Normal,
        Added,
        Deleted,
        Hunk,
        Meta,
    }

    /// <summary>一个显示单元格：对应原始 diff 中一行的一段（<c>RawStart</c> 起 <c>Text</c> 长）。</summary>
    private sealed class Cell
    {
        public required DiffLine Line;
        public required int RawStart;
        /// <summary>显示的文字（Markdown 方式为整行原文，含 +/- 标记）。</summary>
        public required string Text;
        /// <summary>显示文字相对 <see cref="DiffLine.Text"/> 的偏移（着色与行内改动区间需要右移的列数）。</summary>
        public int Shift;
        public Tone Tone;
        public (int Start, int Length)? Changed;
        public double X;
        public double Width;
    }

    private sealed class Row
    {
        public RowKind Kind;
        public DiffFile? File;
        public DiffHunk? Hunk;
        public Cell? Left;
        public Cell? Right;
        public double Top;
        public double Height;
        public TextLayout? Label;
        public int RawStart;
    }

    private static readonly IBrush AddedBackground = new ImmutableSolidColorBrush(Color.FromArgb(0x26, 0x2E, 0xA0, 0x43));
    private static readonly IBrush AddedGutter = new ImmutableSolidColorBrush(Color.FromArgb(0x40, 0x2E, 0xA0, 0x43));
    private static readonly IBrush AddedChange = new ImmutableSolidColorBrush(Color.FromArgb(0x5C, 0x2E, 0xA0, 0x43));
    private static readonly IBrush DeletedBackground = new ImmutableSolidColorBrush(Color.FromArgb(0x26, 0xF8, 0x51, 0x49));
    private static readonly IBrush DeletedGutter = new ImmutableSolidColorBrush(Color.FromArgb(0x40, 0xF8, 0x51, 0x49));
    private static readonly IBrush DeletedChange = new ImmutableSolidColorBrush(Color.FromArgb(0x5C, 0xF8, 0x51, 0x49));
    private static readonly IBrush HunkBackground = new ImmutableSolidColorBrush(Color.FromArgb(0x1F, 0x38, 0x8B, 0xFD));
    private static readonly IBrush EmptySide = new ImmutableSolidColorBrush(Color.FromArgb(0x14, 0x80, 0x80, 0x80));
    internal static readonly IBrush AddedText = new ImmutableSolidColorBrush(Color.FromRgb(0x3F, 0xB9, 0x50));
    internal static readonly IBrush DeletedText = new ImmutableSolidColorBrush(Color.FromRgb(0xF8, 0x51, 0x49));

    private readonly List<Row> _rows = [];
    private readonly Dictionary<(int Raw, double Width, int Shift), (string Text, ImmutableArray<HighlightToken> Tokens, (int, int)? Changed, TextLayout Layout)> _layouts = [];
    private RenderContext? _context;
    private DiffDocument _document = new([]);
    private string _text = "";
    private int[] _lineStarts = [0];
    private DiffHighlightState? _highlight;
    private double _layoutWidth = double.NaN;
    private double _numberWidth;
    private bool _rowsDirty = true;

    public SegmentKey Key { get; private set; }

    protected override global::Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new Automation.TextSegmentAutomationPeer(this, () => _text);

    public string Text => _text;

    public Control View => this;

    public DiffViewMode Mode
    {
        get => _mode;
        set
        {
            if (_mode != value)
            {
                _mode = value;
                _rowsDirty = true;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }
    }

    private DiffViewMode _mode;

    public DiffViewMode EffectiveMode { get; private set; }

    internal static TextLayout? StatsLayout(MarkdownStyle style, int additions, int deletions)
    {
        if (additions == 0 && deletions == 0)
        {
            return null;
        }
        var text = $"+{additions} −{deletions}";
        var plus = text.IndexOf(' ');
        var typeface = new Typeface(style.MonospaceFontFamily, FontStyle.Normal, FontWeight.SemiBold);
        var size = style.CodeFontSize * 0.88;
        return new TextLayout(text, typeface, size, AddedText, textStyleOverrides:
        [
            new ValueSpan<TextRunProperties>(plus + 1, text.Length - plus - 1, new GenericTextRunProperties(typeface, size, foregroundBrush: DeletedText)),
        ]);
    }

    internal void Set(RenderContext context, SegmentKey key, string code, DiffDocument document, int blockKey)
    {
        var contextChanged = !ReferenceEquals(_context, context);
        if (_context is not null && contextChanged)
        {
            _context.Selection.Changed -= InvalidateVisual;
        }
        if (contextChanged && VisualRoot is not null)
        {
            context.Selection.Changed += InvalidateVisual;
        }
        if (_context is null || !ReferenceEquals(_context.Style, context.Style))
        {
            _layouts.Clear();
        }
        _context = context;
        Key = key;
        if (!ReferenceEquals(_document, document))
        {
            _document = document;
            _text = code;
            var starts = new List<int> { 0 };
            for (var i = code.IndexOf('\n'); i >= 0; i = code.IndexOf('\n', i + 1))
            {
                starts.Add(i + 1);
            }
            _lineStarts = [.. starts];
            _rowsDirty = true;
        }
        var highlight = DiffHighlightState.For(context, blockKey);
        if (!ReferenceEquals(_highlight, highlight))
        {
            if (_highlight is not null)
            {
                _highlight.Updated -= OnHighlightUpdated;
            }
            _highlight = highlight;
            if (VisualRoot is not null)
            {
                highlight.Updated += OnHighlightUpdated;
            }
        }
        highlight.Request(code, document);
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnHighlightUpdated()
    {
        _layouts.Clear();
        _rowsDirty = true;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private int RawLineStart(int raw) => raw >= 0 && raw < _lineStarts.Length ? _lineStarts[raw] : _text.Length;

    // ---- 行结构 ----

    private void BuildRows(double width)
    {
        _rows.Clear();
        if (_context is null)
        {
            return;
        }
        var style = _context.Style;
        var options = _context.Services.CodeBlocks;
        EffectiveMode = _mode switch
        {
            DiffViewMode.Split when width >= options.DiffSplitMinWidth => DiffViewMode.Split,
            DiffViewMode.Markdown => DiffViewMode.Markdown,
            // 并排视图放不下时退回宿主默认的非并排方式
            DiffViewMode.Split => options.DiffMode == DiffViewMode.Unified ? DiffViewMode.Unified : DiffViewMode.Markdown,
            _ => DiffViewMode.Unified,
        };
        if (EffectiveMode == DiffViewMode.Markdown)
        {
            BuildMarkdownRows();
            _rowsDirty = false;
            return;
        }
        var maxNumber = 1;
        foreach (var file in _document.Files)
        {
            foreach (var hunk in file.Hunks)
            {
                foreach (var line in hunk.Lines)
                {
                    maxNumber = Math.Max(maxNumber, Math.Max(line.OldNumber ?? 0, line.NewNumber ?? 0));
                }
            }
        }
        var digitWidth = new TextLayout("0", new Typeface(style.MonospaceFontFamily), style.CodeFontSize * 0.9, null).WidthIncludingTrailingWhitespace;
        _numberWidth = Math.Ceiling(digitWidth * Math.Max(2, maxNumber.ToString().Length)) + 16;

        var showFiles = _document.Files.Length > 1 || _document.Files.Any(f => f.DisplayPath is not null) && _document.Files.Length > 1;
        foreach (var file in _document.Files)
        {
            if (showFiles)
            {
                _rows.Add(new Row { Kind = RowKind.File, File = file, RawStart = RawLineStart(file.RawLine) });
            }
            foreach (var hunk in file.Hunks)
            {
                if (hunk.Header.Length > 0)
                {
                    _rows.Add(new Row { Kind = RowKind.Hunk, Hunk = hunk, RawStart = RawLineStart(hunk.RawLine) });
                }
                var changed = ChangedRanges(hunk);
                if (EffectiveMode == DiffViewMode.Split)
                {
                    foreach (var pair in DiffLayout.Split(hunk))
                    {
                        _rows.Add(new Row
                        {
                            Kind = RowKind.Line,
                            Left = pair.Left is { } l ? NewCell(l, changed) : null,
                            Right = pair.Right is { } r ? NewCell(r, changed) : null,
                        });
                    }
                }
                else
                {
                    foreach (var line in hunk.Lines)
                    {
                        _rows.Add(new Row { Kind = RowKind.Line, Left = NewCell(line, changed) });
                    }
                }
            }
        }
        _rowsDirty = false;
    }

    // Markdown 方式：原始 diff 逐行显示（与 GitHub 的 ```diff 代码块相同），解析结果只用于着色与行内改动
    private void BuildMarkdownRows()
    {
        var lines = new Dictionary<int, DiffLine>();
        var hunks = new HashSet<int>();
        var changed = new Dictionary<int, (int, int)>();
        foreach (var file in _document.Files)
        {
            foreach (var hunk in file.Hunks)
            {
                if (hunk.Header.Length > 0)
                {
                    hunks.Add(hunk.RawLine);
                }
                foreach (var line in hunk.Lines)
                {
                    lines[line.RawLine] = line;
                }
                foreach (var pair in ChangedRanges(hunk))
                {
                    changed[pair.Key] = pair.Value;
                }
            }
        }
        var count = _lineStarts.Length;
        // 以换行结尾时最后一个"行"是空的
        if (count > 1 && _lineStarts[^1] == _text.Length)
        {
            count--;
        }
        for (var raw = 0; raw < count; raw++)
        {
            var start = _lineStarts[raw];
            var end = raw + 1 < _lineStarts.Length ? _lineStarts[raw + 1] - 1 : _text.Length;
            var text = _text[start..Math.Max(start, end)].TrimEnd('\r');
            DiffLine line;
            Tone tone;
            if (lines.TryGetValue(raw, out var parsed))
            {
                line = parsed;
                tone = parsed.Kind switch
                {
                    DiffLineKind.Added => Tone.Added,
                    DiffLineKind.Deleted => Tone.Deleted,
                    DiffLineKind.NoNewline => Tone.Meta,
                    _ => Tone.Normal,
                };
            }
            else
            {
                line = new DiffLine(DiffLineKind.Context, null, null, text, raw, 0);
                tone = hunks.Contains(raw) ? Tone.Hunk : Tone.Meta;
            }
            _rows.Add(new Row
            {
                Kind = RowKind.Line,
                Left = new Cell
                {
                    Line = line,
                    Text = text,
                    Shift = line.TextStart,
                    Tone = tone,
                    RawStart = start,
                    Changed = changed.TryGetValue(raw, out var range) ? range : null,
                },
            });
        }
    }

    private Cell NewCell(DiffLine line, Dictionary<int, (int, int)> changed) => new()
    {
        Line = line,
        Text = line.Text,
        RawStart = RawLineStart(line.RawLine) + line.TextStart,
        Changed = changed.TryGetValue(line.RawLine, out var range) ? range : null,
    };

    // 配对的删除 / 新增行之间的行内改动
    private static Dictionary<int, (int, int)> ChangedRanges(DiffHunk hunk)
    {
        var result = new Dictionary<int, (int, int)>();
        foreach (var pair in DiffLayout.Split(hunk))
        {
            if (pair is { Left: { Kind: DiffLineKind.Deleted } left, Right: { Kind: DiffLineKind.Added } right }
                && DiffLayout.ChangedRanges(left.Text, right.Text) is { } ranges)
            {
                if (ranges.Old.Length > 0)
                {
                    result[left.RawLine] = ranges.Old;
                }
                if (ranges.New.Length > 0)
                {
                    result[right.RawLine] = ranges.New;
                }
            }
        }
        return result;
    }

    private TextLayout CellLayout(Cell cell, double width)
    {
        var style = _context!.Style;
        // 元数据行（文件头、区段头）不着色
        var tokens = cell.Tone is Tone.Meta or Tone.Hunk ? default : _highlight?.TokensFor(cell.Line.RawLine) ?? default;
        var key = (cell.Line.RawLine, Math.Round(width, 1), cell.Shift);
        if (_layouts.TryGetValue(key, out var cached) && cached.Text == cell.Text && cached.Changed == cell.Changed && SameTokens(cached.Tokens, tokens))
        {
            return cached.Layout;
        }
        var typeface = new Typeface(style.MonospaceFontFamily);
        List<ValueSpan<TextRunProperties>>? overrides = null;
        var text = cell.Text;
        var shift = cell.Shift;
        if (!tokens.IsDefaultOrEmpty)
        {
            overrides = [];
            foreach (var token in tokens)
            {
                var tokenStart = token.Start + shift;
                if (tokenStart >= text.Length)
                {
                    break;
                }
                if (token.Foreground == 0 && token.Style == HighlightFontStyle.None)
                {
                    continue;
                }
                var brush = token.Foreground == 0 ? style.Foreground : BrushCache.Get(token.Foreground);
                var face = new Typeface(style.MonospaceFontFamily,
                    token.Style.HasFlag(HighlightFontStyle.Italic) ? FontStyle.Italic : FontStyle.Normal,
                    token.Style.HasFlag(HighlightFontStyle.Bold) ? FontWeight.Bold : FontWeight.Normal);
                overrides.Add(new ValueSpan<TextRunProperties>(tokenStart, Math.Min(token.Length, text.Length - tokenStart), new GenericTextRunProperties(face, style.CodeFontSize, foregroundBrush: brush)));
            }
        }
        var foreground = cell.Line.Kind == DiffLineKind.NoNewline || cell.Tone is Tone.Meta or Tone.Hunk ? style.SecondaryForeground : style.Foreground;
        var layout = new TextLayout(text.Length == 0 ? " " : text, typeface, style.CodeFontSize, foreground,
            textWrapping: TextWrapping.Wrap, maxWidth: Math.Max(1, width), lineHeight: style.CodeLineHeight, textStyleOverrides: overrides);
        _layouts[key] = (text, tokens, cell.Changed, layout);
        return layout;
    }

    private static bool SameTokens(ImmutableArray<HighlightToken> a, ImmutableArray<HighlightToken> b) =>
        a.IsDefault ? b.IsDefault : !b.IsDefault && a.SequenceEqual(b);

    // ---- 布局 ----

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_context is null)
        {
            return default;
        }
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 720;
        if (_rowsDirty || !width.Equals(_layoutWidth))
        {
            BuildRows(width);
            _layoutWidth = width;
        }
        var style = _context.Style;
        double y = 0;
        foreach (var row in _rows)
        {
            row.Top = y;
            switch (row.Kind)
            {
                case RowKind.File:
                    row.Label = new TextLayout(row.File!.DisplayPath ?? "(unknown)", new Typeface(style.MonospaceFontFamily, FontStyle.Normal, FontWeight.SemiBold),
                        style.CodeFontSize * 0.92, style.Foreground, textTrimming: TextTrimming.PathSegmentEllipsis, maxWidth: Math.Max(1, width - 140));
                    row.Height = FileRowHeight;
                    break;
                case RowKind.Hunk:
                    row.Label = new TextLayout(row.Hunk!.Header, new Typeface(style.MonospaceFontFamily), style.CodeFontSize * 0.92, style.SecondaryForeground,
                        textWrapping: TextWrapping.Wrap, maxWidth: Math.Max(1, width - 28), lineHeight: style.CodeLineHeight);
                    row.Height = row.Label.Height + 4;
                    break;
                default:
                    var height = style.CodeLineHeight;
                    if (EffectiveMode == DiffViewMode.Markdown && row.Left is { } plainCell)
                    {
                        // 与普通代码块相同的左右内边距
                        plainCell.X = 14;
                        plainCell.Width = Math.Max(1, width - 28);
                        height = Math.Max(height, CellLayout(plainCell, plainCell.Width).Height);
                    }
                    else if (EffectiveMode == DiffViewMode.Split)
                    {
                        var half = width / 2;
                        var textWidth = half - _numberWidth - MarkerWidth - TextGap - 8;
                        if (row.Left is { } left)
                        {
                            left.X = _numberWidth + MarkerWidth + TextGap;
                            left.Width = textWidth;
                            height = Math.Max(height, CellLayout(left, textWidth).Height);
                        }
                        if (row.Right is { } right)
                        {
                            right.X = half + _numberWidth + MarkerWidth + TextGap;
                            right.Width = textWidth;
                            height = Math.Max(height, CellLayout(right, textWidth).Height);
                        }
                    }
                    else if (row.Left is { } cell)
                    {
                        var textWidth = width - _numberWidth * 2 - MarkerWidth - TextGap - 8;
                        cell.X = _numberWidth * 2 + MarkerWidth + TextGap;
                        cell.Width = textWidth;
                        height = Math.Max(height, CellLayout(cell, textWidth).Height);
                    }
                    row.Height = height;
                    break;
            }
            y += row.Height;
        }
        return new Size(width, y);
    }

    // ---- 绘制 ----

    public override void Render(DrawingContext context)
    {
        if (_context is null)
        {
            return;
        }
        var style = _context.Style;
        var width = Bounds.Width;
        var selected = _context.Selection.GetSegmentRange(Key, _text.Length);
        var numberTypeface = new Typeface(style.MonospaceFontFamily);
        var numberSize = style.CodeFontSize * 0.9;
        foreach (var row in _rows)
        {
            switch (row.Kind)
            {
                case RowKind.File:
                    context.FillRectangle(style.TableHeaderBackground, new Rect(0, row.Top, width, row.Height));
                    context.DrawLine(new Pen(style.CodeBorderBrush, 1), new Point(0, row.Top + 0.5), new Point(width, row.Top + 0.5));
                    row.Label!.Draw(context, new Point(14, row.Top + (row.Height - row.Label.Height) / 2));
                    if (StatsLayout(style, row.File!.Additions, row.File.Deletions) is { } stats)
                    {
                        stats.Draw(context, new Point(width - stats.WidthIncludingTrailingWhitespace - 14, row.Top + (row.Height - stats.Height) / 2));
                    }
                    break;
                case RowKind.Hunk:
                    context.FillRectangle(HunkBackground, new Rect(0, row.Top, width, row.Height));
                    row.Label!.Draw(context, new Point(14, row.Top + 2));
                    break;
                default:
                    if (EffectiveMode == DiffViewMode.Markdown && row.Left is { } plainCell)
                    {
                        var background = plainCell.Tone switch
                        {
                            Tone.Added => AddedBackground,
                            Tone.Deleted => DeletedBackground,
                            Tone.Hunk => HunkBackground,
                            _ => null,
                        };
                        if (background is not null)
                        {
                            context.FillRectangle(background, new Rect(0, row.Top, width, row.Height));
                        }
                        DrawCell(context, plainCell, row.Top, selected);
                    }
                    else if (EffectiveMode == DiffViewMode.Split)
                    {
                        var half = width / 2;
                        DrawSide(context, row.Left, 0, half, row, numberTypeface, numberSize, selected, left: true);
                        DrawSide(context, row.Right, half, half, row, numberTypeface, numberSize, selected, left: false);
                        context.DrawLine(new Pen(style.CodeBorderBrush, 1), new Point(Math.Round(half) + 0.5, row.Top), new Point(Math.Round(half) + 0.5, row.Top + row.Height));
                    }
                    else if (row.Left is { } cell)
                    {
                        var (background, gutter) = Backgrounds(cell.Line.Kind);
                        if (background is not null)
                        {
                            context.FillRectangle(background, new Rect(0, row.Top, width, row.Height));
                            context.FillRectangle(gutter!, new Rect(0, row.Top, _numberWidth * 2, row.Height));
                        }
                        DrawNumber(context, cell.Line.OldNumber, 0, row.Top, numberTypeface, numberSize, style);
                        DrawNumber(context, cell.Line.NewNumber, _numberWidth, row.Top, numberTypeface, numberSize, style);
                        DrawMarker(context, cell.Line.Kind, _numberWidth * 2, row.Top, style);
                        DrawCell(context, cell, row.Top, selected);
                    }
                    break;
            }
        }
    }

    private void DrawSide(DrawingContext context, Cell? cell, double x, double width, Row row, Typeface typeface, double size, (int Start, int End)? selected, bool left)
    {
        var style = _context!.Style;
        if (cell is null)
        {
            // 另一侧没有对应行：浅灰底
            context.FillRectangle(EmptySide, new Rect(x, row.Top, width, row.Height));
            return;
        }
        var (background, gutter) = Backgrounds(cell.Line.Kind);
        if (background is not null)
        {
            context.FillRectangle(background, new Rect(x, row.Top, width, row.Height));
            context.FillRectangle(gutter!, new Rect(x, row.Top, _numberWidth, row.Height));
        }
        DrawNumber(context, left ? cell.Line.OldNumber : cell.Line.NewNumber, x, row.Top, typeface, size, style);
        DrawMarker(context, cell.Line.Kind, x + _numberWidth, row.Top, style);
        DrawCell(context, cell, row.Top, selected);
    }

    private static (IBrush? Background, IBrush? Gutter) Backgrounds(DiffLineKind kind) => kind switch
    {
        DiffLineKind.Added => (AddedBackground, AddedGutter),
        DiffLineKind.Deleted => (DeletedBackground, DeletedGutter),
        _ => (null, null),
    };

    private void DrawNumber(DrawingContext context, int? number, double x, double top, Typeface typeface, double size, MarkdownStyle style)
    {
        if (number is not { } n)
        {
            return;
        }
        var layout = new TextLayout(n.ToString(), typeface, size, style.SecondaryForeground, TextAlignment.Right, maxWidth: _numberWidth - 10, lineHeight: style.CodeLineHeight);
        layout.Draw(context, new Point(x + 2, top));
    }

    private static void DrawMarker(DrawingContext context, DiffLineKind kind, double x, double top, MarkdownStyle style)
    {
        var (text, brush) = kind switch
        {
            DiffLineKind.Added => ("+", AddedText),
            DiffLineKind.Deleted => ("−", DeletedText),
            _ => ((string?)null, (IBrush?)null),
        };
        if (text is null)
        {
            return;
        }
        var layout = new TextLayout(text, new Typeface(style.MonospaceFontFamily), style.CodeFontSize, brush, lineHeight: style.CodeLineHeight);
        layout.Draw(context, new Point(x + (MarkerWidth - layout.WidthIncludingTrailingWhitespace) / 2, top));
    }

    private void DrawCell(DrawingContext context, Cell cell, double top, (int Start, int End)? selected)
    {
        var layout = CellLayout(cell, cell.Width);
        var origin = new Point(cell.X, top);
        // 行内改动：更深的底色
        if (cell.Changed is { } changed && changed.Length > 0)
        {
            var brush = cell.Line.Kind == DiffLineKind.Added ? AddedChange : DeletedChange;
            foreach (var rect in layout.HitTestTextRange(changed.Start + cell.Shift, changed.Length))
            {
                context.FillRectangle(brush, rect.Translate(origin), 2);
            }
        }
        foreach (var (hs, he, current) in _context!.Selection.GetHighlights(Key, _text.Length))
        {
            var s0 = Math.Max(hs, cell.RawStart) - cell.RawStart;
            var e0 = Math.Min(he, cell.RawStart + cell.Text.Length) - cell.RawStart;
            if (e0 > s0)
            {
                var brush = current ? _context.Style.SearchCurrentBackground : _context.Style.SearchHighlightBackground;
                foreach (var rect in layout.HitTestTextRange(s0, e0 - s0))
                {
                    context.FillRectangle(brush, rect.Translate(origin), 2);
                }
            }
        }
        if (selected is { } range)
        {
            var s = Math.Max(range.Start, cell.RawStart) - cell.RawStart;
            var e = Math.Min(range.End, cell.RawStart + cell.Text.Length) - cell.RawStart;
            if (e > s)
            {
                foreach (var rect in layout.HitTestTextRange(s, e - s))
                {
                    context.FillRectangle(_context!.Style.SelectionBackground, rect.Translate(origin));
                }
            }
            else if (range.Start <= cell.RawStart && range.End > cell.RawStart + cell.Text.Length && cell.Text.Length == 0)
            {
                context.FillRectangle(_context!.Style.SelectionBackground, new Rect(origin, new Size(_context.Style.CodeFontSize * 0.5, layout.Height)));
            }
        }
        layout.Draw(context, origin);
    }

    // ---- ISelectableSegment ----

    public int HitTest(Point point)
    {
        if (_rows.Count == 0)
        {
            return 0;
        }
        var row = _rows.LastOrDefault(r => r.Top <= point.Y) ?? _rows[0];
        if (row.Kind != RowKind.Line)
        {
            return row.RawStart;
        }
        var cell = row.Left;
        if (EffectiveMode == DiffViewMode.Split && (point.X >= Bounds.Width / 2 || cell is null))
        {
            cell = row.Right ?? row.Left;
        }
        if (cell is null)
        {
            return 0;
        }
        var layout = CellLayout(cell, cell.Width);
        var hit = layout.HitTestPoint(new Point(point.X - cell.X, Math.Max(0, point.Y - row.Top)));
        return cell.RawStart + Math.Clamp(hit.TextPosition, 0, cell.Text.Length);
    }

    public Rect GetCharacterBounds(int offset)
    {
        foreach (var row in _rows)
        {
            foreach (var cell in new[] { row.Left, row.Right })
            {
                if (cell is not null && offset >= cell.RawStart && offset <= cell.RawStart + cell.Text.Length)
                {
                    var rect = CellLayout(cell, cell.Width).HitTestTextPosition(offset - cell.RawStart);
                    return rect.Translate(new Vector(cell.X, row.Top));
                }
            }
        }
        return default;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        Cursor = new Cursor(StandardCursorType.Ibeam);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_context is not null)
        {
            _context.Selection.Changed += InvalidateVisual;
        }
        if (_highlight is not null)
        {
            _highlight.Updated += OnHighlightUpdated;
            _layouts.Clear();
            _rowsDirty = true;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_context is not null)
        {
            _context.Selection.Changed -= InvalidateVisual;
        }
        if (_highlight is not null)
        {
            _highlight.Updated -= OnHighlightUpdated;
        }
    }
}

/// <summary>
/// diff 的语法着色（块状态中共享）：每个文件按扩展名选语言，旧侧（上下文 + 删除）与新侧（上下文 + 新增）分别着色，
/// 结果按原始行号保存。后台计算，同一时刻最多一个任务，期间到达的新文本合并为一次。
/// </summary>
internal sealed class DiffHighlightState
{
    private readonly object _lock = new();
    private readonly ISyntaxHighlighter? _highlighter;
    private readonly HighlightThemeKind _theme;
    private Dictionary<int, ImmutableArray<HighlightToken>> _tokens = [];
    private (string Code, DiffDocument Document)? _pending;
    private bool _running;
    private string? _requested;

    private DiffHighlightState(ISyntaxHighlighter? highlighter, HighlightThemeKind theme)
    {
        _highlighter = highlighter;
        _theme = theme;
    }

    public event Action? Updated;

    public static DiffHighlightState For(RenderContext context, int blockKey)
    {
        var highlighter = context.Plugins.Highlighter;
        var theme = context.Style.HighlightTheme;
        var existing = context.State.Get<DiffHighlightState?>(blockKey, "diff.highlight", null);
        if (existing is not null && ReferenceEquals(existing._highlighter, highlighter) && existing._theme == theme)
        {
            return existing;
        }
        var state = new DiffHighlightState(highlighter, theme);
        context.State.Set(blockKey, "diff.highlight", state);
        return state;
    }

    public ImmutableArray<HighlightToken> TokensFor(int rawLine) => _tokens.GetValueOrDefault(rawLine);

    public void Request(string code, DiffDocument document)
    {
        if (_highlighter is null || code == _requested)
        {
            return;
        }
        _requested = code;
        lock (_lock)
        {
            _pending = (code, document);
            if (_running)
            {
                return;
            }
            _running = true;
        }
        _ = Task.Run(Run);
    }

    private void Run()
    {
        while (true)
        {
            (string Code, DiffDocument Document) work;
            lock (_lock)
            {
                if (_pending is not { } pending)
                {
                    _running = false;
                    return;
                }
                work = pending;
                _pending = null;
            }
            Dictionary<int, ImmutableArray<HighlightToken>> result;
            try
            {
                result = Compute(work.Document);
            }
            catch (Exception)
            {
                continue;
            }
            Dispatcher.UIThread.Post(() =>
            {
                _tokens = result;
                Updated?.Invoke();
            });
        }
    }

    private Dictionary<int, ImmutableArray<HighlightToken>> Compute(DiffDocument document)
    {
        var result = new Dictionary<int, ImmutableArray<HighlightToken>>();
        foreach (var file in document.Files)
        {
            var path = file.NewPath ?? file.OldPath;
            var language = path is null ? null : System.IO.Path.GetExtension(path).TrimStart('.');
            if (string.IsNullOrEmpty(language) || !_highlighter!.SupportsLanguage(language))
            {
                continue;
            }
            var oldSide = new List<DiffLine>();
            var newSide = new List<DiffLine>();
            foreach (var hunk in file.Hunks)
            {
                foreach (var line in hunk.Lines)
                {
                    if (line.Kind is DiffLineKind.Context or DiffLineKind.Deleted)
                    {
                        oldSide.Add(line);
                    }
                    if (line.Kind is DiffLineKind.Context or DiffLineKind.Added)
                    {
                        newSide.Add(line);
                    }
                }
            }
            Highlight(language, oldSide, result, onlyDeleted: true);
            Highlight(language, newSide, result, onlyDeleted: false);
        }
        return result;
    }

    private void Highlight(string language, List<DiffLine> lines, Dictionary<int, ImmutableArray<HighlightToken>> result, bool onlyDeleted)
    {
        if (lines.Count == 0)
        {
            return;
        }
        using var session = _highlighter!.CreateSession(language, _theme);
        var tokens = session.Update(string.Join('\n', lines.Select(l => l.Text)));
        for (var i = 0; i < lines.Count && i < tokens.Count; i++)
        {
            // 上下文行取新侧的着色
            if (!onlyDeleted || lines[i].Kind == DiffLineKind.Deleted)
            {
                result[lines[i].RawLine] = tokens[i];
            }
        }
    }
}
