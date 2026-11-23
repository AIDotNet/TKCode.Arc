using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StreamMark.Avalonia.Extensions;
using StreamMark.Avalonia.Selection;
using StreamMark.Core.Model;

namespace StreamMark.Avalonia.Rendering;

/// <summary>
/// 图表代码块（如 Mermaid）：代码块闭合后在后台渲染；渲染完成前和失败时显示源码。
/// 头部可在"图表 / 源码"之间切换。渲染结果按 (语言, 代码, 明暗, 实际用到的颜色与字体) 缓存，视图回收后再出现不会重复渲染。
/// </summary>
public sealed class DiagramBlockView : BlockView
{
    private const double HeaderHeight = 34;
    private const double Padding = 14;
    private const string ShowCodeState = "diagram.code";

    /// <summary>
    /// 图表高度的安全上限（设备无关像素）。图表按宽度适配、不因为高而缩小——和浏览器里 <c>max-width: 100%</c> 的 SVG 一致，
    /// 否则几千像素高的流程图会被压到看不清文字；只有超过这个上限的极端图表才会继续缩小。
    /// </summary>
    internal const double MaxDiagramHeight = 6000;

    private readonly IconButton _toggle = new() { Icon = MarkdownIcons.Code };
    private readonly IconButton _copy = new() { Icon = MarkdownIcons.Copy };
    private readonly IconButton _fullscreen = new() { Icon = MarkdownIcons.Expand };
    private readonly CodeTextView _code = new();
    private readonly BlockActionBar _actions;
    private DiagramResult? _diagram;
    private string? _error;
    private bool _showCode;
    private string _renderKey = "";
    private Color _surface;
    private CancellationTokenSource? _cts;
    private TextLayout? _title;
    private TextLayout? _status;
    private string? _statusText;
    private bool _statusIsError;

    public DiagramBlockView()
    {
        AddChild(_code);
        AddChild(_toggle);
        AddChild(_copy);
        AddChild(_fullscreen);
        _actions = new BlockActionBar(this, AddChild, RemoveChild);
        _fullscreen.Click += (_, _) => OpenFullscreen();
        _toggle.Click += (_, _) =>
        {
            _showCode = !_showCode;
            Context?.State.Set(TopKey, StateKey(ShowCodeState, _code.Key.Segment), _showCode);
            _toggle.Icon = _showCode ? MarkdownIcons.Diagram : MarkdownIcons.Code;
            UpdateVisibility();
        };
        _copy.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(_code.Text);
            }
        };
    }

    protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged)
    {
        var code = (CodeBlock)block;
        var style = context.Style;
        var segmentKey = KeyFor(context, topKey, ref segment);
        var shared = CodeBlockState.For(context, topKey, code.Language, segmentKey.Segment);
        shared.Update(code.Code);
        _code.Set(context, segmentKey, shared, 0, int.MaxValue);
        _showCode = context.State.Get(topKey, StateKey(ShowCodeState, segmentKey.Segment), false);
        _toggle.Icon = _showCode ? MarkdownIcons.Diagram : MarkdownIcons.Code;
        foreach (var button in new[] { _toggle, _copy, _fullscreen })
        {
            button.Foreground = style.SecondaryForeground;
            button.HoverBackground = style.HoverBackground;
        }
        _title = new TextLayout(code.Language ?? "diagram", new Typeface(style.MonospaceFontFamily), style.CodeFontSize * 0.92, style.SecondaryForeground);
        if (contextChanged || previous is not CodeBlock old || old.Info != code.Info)
        {
            _actions.Rebuild(context, Plugins.BlockActionTargets.Diagram, code, visible: true);
        }

        Refresh(context, code);
    }

    /// <summary>
    /// 按当前主题与底色计算渲染环境，环境或代码变了就重新渲染。
    /// 底色取决于祖先的背景，没挂上可视树时算不准（还会白白渲染一次），所以挂上之后才开始渲染；
    /// 先更新、后挂上的情况由 <see cref="OnAttachedToVisualTree"/> 补上。
    /// </summary>
    private void Refresh(RenderContext context, CodeBlock code)
    {
        if (this.IsAttachedToVisualTree())
        {
            var environment = ResolveEnvironment(context.Style);
            var renderer = context.Services.FindDiagramRenderer(code.Language);
            _surface = environment.Background;
            var key = RenderKey(code, environment, renderer);
            if (key != _renderKey)
            {
                _renderKey = key;
                StartRender(code, environment, renderer);
            }
        }
        UpdateVisibility();
    }

    /// <summary>
    /// 缓存键：除了语言与代码，还要包含渲染实际用到的明暗、颜色与字体，以及渲染器本身。
    /// 否则宿主换强调色、换字号、底色变化后，已渲染（含静态缓存里）的图表不会重画；
    /// 同一进程里配置不同的两个渲染器（例如不同配色方案）也会互相命中对方的缓存。
    /// </summary>
    private static string RenderKey(CodeBlock code, DiagramRenderContext environment, IDiagramRenderer? renderer) => FormattableString.Invariant(
        $"{RuntimeHelpers.GetHashCode(renderer)}\n{code.Language}\n{environment.IsDark}\n{environment.Foreground}\n{environment.Background}\n{environment.Accent}\n{environment.FontFamily}\n{environment.FontSize}\n{code.Code}");

    /// <summary>
    /// 渲染环境：宿主主题里的文字 / 填充色常常带透明度（例如暗色主题的代码底是 4% 的白、文字是 85% 的白），
    /// 直接丢掉透明度会得到「白底白字」。这里把它们叠到块背后真实的底色上，传给渲染器的都是不透明色。
    /// </summary>
    private DiagramRenderContext ResolveEnvironment(MarkdownStyle style)
    {
        var surface = DiagramRenderContext.Flatten(ColorOf(style.CodeBackground), BackdropColor(style.IsDark));
        return new DiagramRenderContext(
            style.IsDark,
            DiagramRenderContext.Flatten(ColorOf(style.Foreground), surface),
            surface,
            DiagramRenderContext.Flatten(ColorOf(style.LinkForeground), surface),
            style.FontFamily.Name,
            style.FontSize);
    }

    /// <summary>
    /// 块背后的不透明底色：沿可视树向上叠加各祖先的纯色背景，直到遇到不透明的一层；
    /// 没有祖先背景（或还没挂上可视树）时按明暗取一个默认底色。
    /// </summary>
    private Color BackdropColor(bool isDark)
    {
        // 由内向外收集；最外一层不透明就作为底，否则底用默认色
        var layers = new List<Color>();
        for (var parent = this.GetVisualParent(); parent is not null; parent = parent.GetVisualParent())
        {
            if (BackgroundOf(parent) is { A: > 0 } color)
            {
                layers.Add(color);
                if (color.A == byte.MaxValue)
                {
                    break;
                }
            }
        }

        var result = isDark ? Color.FromRgb(0x1E, 0x1E, 0x1E) : Colors.White;
        var top = layers.Count;
        if (top > 0 && layers[^1].A == byte.MaxValue)
        {
            result = layers[^1];
            top--;
        }
        for (var i = top - 1; i >= 0; i--)
        {
            result = DiagramRenderContext.Flatten(layers[i], result);
        }
        return result;
    }

    private static Color? BackgroundOf(Visual visual) => visual switch
    {
        Border border => SolidColorOf(border.Background),
        Panel panel => SolidColorOf(panel.Background),
        ContentPresenter presenter => SolidColorOf(presenter.Background),
        TemplatedControl templated => SolidColorOf(templated.Background),
        _ => null,
    };

    private static Color? SolidColorOf(IBrush? brush) => brush is ISolidColorBrush ? ColorOf(brush) : null;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Context is { } context && Block is CodeBlock code)
        {
            Refresh(context, code);
        }
    }

    private void StartRender(CodeBlock code, DiagramRenderContext renderContext, IDiagramRenderer? renderer)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        if (DiagramCache.TryGet(_renderKey, out var cached))
        {
            _diagram = cached;
            _error = null;
            return;
        }
        if (renderer is null)
        {
            return;
        }
        _diagram = null;
        _error = null;
        _statusText = "Rendering…";
        _statusIsError = false;
        _status = null;
        var key = _renderKey;
        var language = code.Language ?? "";
        var source = code.Code;
        _ = Task.Run(async () =>
        {
            DiagramResult? result = null;
            string? error = null;
            try
            {
                result = await renderer.RenderAsync(language, source, renderContext, token).ConfigureAwait(false);
                if (result is null)
                {
                    error = "Diagram could not be rendered.";
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (result is not null)
                {
                    DiagramCache.Set(key, result);
                }
                if (token.IsCancellationRequested || key != _renderKey)
                {
                    return;
                }
                _diagram = result;
                _error = error;
                _statusText = error;
                _statusIsError = error is not null;
                _status = null;
                UpdateVisibility();
            });
        }, token);
    }

    /// <summary>纯色画刷的颜色（含画刷自身的不透明度）；非纯色画刷取灰色。</summary>
    private static Color ColorOf(IBrush brush) => brush is ISolidColorBrush solid
        ? Color.FromArgb((byte)Math.Round(solid.Color.A * solid.Opacity), solid.Color.R, solid.Color.G, solid.Color.B)
        : Colors.Gray;

    private bool DiagramVisible => _diagram is not null && !_showCode;

    /// <summary>全屏查看图表（可缩放、平移）。</summary>
    public ImageViewer? OpenFullscreen()
    {
        if (_diagram is not { } diagram || Context is null)
        {
            return null;
        }
        // 全屏的深色遮罩上要用不透明的底色：主题的代码底常是几乎透明的填充色，图表又是按真实底色配的色
        return ImageViewer.Show(this, diagram.Size, (context, rect) => diagram.Draw(context, rect), new ImmutableSolidColorBrush(_surface));
    }

    /// <summary>当前显示的是渲染好的图表（而不是源码）。</summary>
    public bool IsShowingDiagram => DiagramVisible;

    private void UpdateVisibility()
    {
        _code.IsVisible = !DiagramVisible;
        _toggle.IsVisible = _diagram is not null;
        _fullscreen.IsVisible = DiagramVisible;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private double StatusHeight => _status is null || DiagramVisible ? 0 : _status.Height + 8;

    protected override Size MeasureOverride(Size availableSize)
    {
        _toggle.Measure(Size.Infinity);
        _copy.Measure(Size.Infinity);
        _fullscreen.Measure(Size.Infinity);
        foreach (var button in _actions.Buttons)
        {
            button.Measure(Size.Infinity);
        }
        var inner = Math.Max(0, availableSize.Width - Padding * 2);
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 600;
        if (DiagramVisible)
        {
            var size = FitDiagram(inner);
            return new Size(width, HeaderHeight + size.Height + Padding);
        }
        _status = _statusText is null || Context is null
            ? null
            : new TextLayout(_statusText, new Typeface(Context.Style.FontFamily), Context.Style.FontSize * 0.85,
                _statusIsError ? Context.Style.AlertBrush("CAUTION") : Context.Style.SecondaryForeground,
                textWrapping: TextWrapping.Wrap, maxWidth: double.IsFinite(inner) ? Math.Max(1, inner) : double.PositiveInfinity);
        _code.Measure(new Size(inner, double.PositiveInfinity));
        return new Size(width, HeaderHeight + StatusHeight + _code.DesiredSize.Height + Padding);
    }

    private Size FitDiagram(double width)
    {
        var natural = _diagram!.Size;
        if (natural.Width <= 0 || natural.Height <= 0)
        {
            return default;
        }
        // 按宽度适配，不放大；高度只受安全上限约束（见 MaxDiagramHeight）
        var scale = Math.Min(1, width / natural.Width);
        scale = Math.Min(scale, MaxDiagramHeight / natural.Height);
        return new Size(natural.Width * scale, natural.Height * scale);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = finalSize.Width - 6;
        foreach (var button in new[] { _copy, _toggle, _fullscreen }.Concat(_actions.Buttons))
        {
            if (!button.IsVisible)
            {
                continue;
            }
            x -= button.DesiredSize.Width;
            button.Arrange(new Rect(x, (HeaderHeight - button.DesiredSize.Height) / 2, button.DesiredSize.Width, button.DesiredSize.Height));
            x -= 2;
        }
        _code.Arrange(new Rect(Padding, HeaderHeight + StatusHeight, Math.Max(0, finalSize.Width - Padding * 2), _code.DesiredSize.Height));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (Context is null)
        {
            return;
        }
        var style = Context.Style;
        var radius = style.CornerRadius.TopLeft;
        context.DrawRectangle(style.CodeBackground, new Pen(style.CodeBorderBrush, 1), new Rect(Bounds.Size).Deflate(0.5), radius, radius);
        _title?.Draw(context, new Point(Padding, (HeaderHeight - _title.Height) / 2));
        if (DiagramVisible)
        {
            var size = FitDiagram(Math.Max(0, Bounds.Width - Padding * 2));
            var rect = new Rect((Bounds.Width - size.Width) / 2, HeaderHeight, size.Width, size.Height);
            _diagram!.Draw(context, rect);
        }
        else
        {
            _status?.Draw(context, new Point(Padding, HeaderHeight));
        }
    }

    public override IEnumerable<ISelectableSegment> Segments() => DiagramVisible ? [] : [_code];

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _cts?.Cancel();
        // 取消后重新挂载时需要重新渲染（缓存命中则立即显示）
        if (_diagram is null)
        {
            _renderKey = "";
        }
    }

    // 进程内共享：同样的代码与环境不必重新渲染（视图回收、重建后立即显示）
    private static readonly BlockResultCache<string, DiagramResult> DiagramCache = new(32);
}
