using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Mermaider;
using RenderOptions = Mermaider.Models.RenderOptions;
using StreamMark.Avalonia.Extensions;
using Svg.Skia;

namespace StreamMark.Mermaid;

/// <summary>
/// Mermaid 图表渲染器：Mermaider（纯 C#）解析布局 → SVG → 展开 <c>var()</c>/<c>color-mix()</c> 为字面颜色 →
/// Svg.Skia 转为 <see cref="SkiaSharp.SKPicture"/> → 在界面上以矢量方式绘制。全程托管、NativeAOT 安全，不需要浏览器。
/// </summary>
public sealed class MermaidDiagramRenderer : IDiagramRenderer
{
    /// <summary>
    /// 配色方案。默认 <see cref="MermaidTheme.MermaidJs"/>：复刻 mermaid.js 官方主题（暗色 dark、亮色 default），
    /// 和浏览器里的效果一致；<see cref="MermaidTheme.Native"/> 则完全由宿主的前景 / 背景 / 强调色推导。
    /// </summary>
    public MermaidTheme Theme { get; init; } = MermaidTheme.MermaidJs;

    /// <summary>
    /// 在主题映射之后进一步定制 Mermaider 的渲染选项（样式预设、间距、调色板等）。
    /// 返回值作为最终选项；可用 <c>options with { ... }</c>。
    /// </summary>
    public Func<RenderOptions, DiagramRenderContext, RenderOptions>? ConfigureOptions { get; init; }

    /// <summary>渲染失败时的回调（例如记录日志）。渲染器自身不会抛出非取消类异常。</summary>
    public Action<string, Exception>? ErrorReported { get; init; }

    /// <inheritdoc />
    public bool CanRender(string? language) =>
        string.Equals(language?.Trim(), "mermaid", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task<DiagramResult?> RenderAsync(string language, string code, DiagramRenderContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(context);
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<DiagramResult?>(cancellationToken);
        }

        return Task.Run(() => Render(code, context, cancellationToken), cancellationToken);
    }

    /// <summary>同步渲染（调用线程执行）。失败返回 null，取消时抛出 <see cref="OperationCanceledException"/>。</summary>
    public DiagramResult? Render(string code, DiagramRenderContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string svg;
        var violations = new List<string>();
        try
        {
            svg = BuildSvg(code, context, violations);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Report(ex.Message, ex);
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var resolved = ResolveSvg(svg);
            cancellationToken.ThrowIfCancellationRequested();

            var skSvg = new SKSvg();
            try
            {
                var picture = skSvg.FromSvg(resolved);
                if (picture is null)
                {
                    skSvg.Dispose();
                    Report("Svg.Skia produced no picture.", null);
                    return null;
                }

                var cull = picture.CullRect;
                if (cull.Width <= 0 || cull.Height <= 0)
                {
                    skSvg.Dispose();
                    Report("Mermaid diagram is empty.", null);
                    return null;
                }

                var warning = violations.Count > 0 ? "Sanitized: " + string.Join("; ", violations) : null;
                return new SkiaPictureDiagram(picture, skSvg, new Size(cull.Right, cull.Bottom), warning);
            }
            catch
            {
                skSvg.Dispose();
                throw;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Report(ex.Message, ex);
            return null;
        }
    }

    /// <summary>
    /// Mermaider 渲染并按配色方案重涂后的 SVG（仍含 <c>var()</c>/<c>color-mix()</c>，交给 <see cref="ResolveSvg"/> 展开）。
    /// 被 Mermaider 清理掉的内容写进 <paramref name="violations"/>。
    /// </summary>
    internal string BuildSvg(string code, DiagramRenderContext context, List<string> violations)
    {
        var kind = MermaidThemer.Detect(code);
        var options = CreateOptions(context, Theme, kind) with
        {
            OnSanitized = list =>
            {
                foreach (var v in list)
                {
                    violations.Add(v.ToString() ?? string.Empty);
                }
            },
        };
        if (ConfigureOptions is { } configure)
        {
            options = configure(options, context);
        }

        var svg = MermaidRenderer.RenderSvg(code, options);
        return MermaidPalette.For(Theme, context.IsDark) is { } palette
            ? MermaidThemer.PaintSvg(svg, palette, OpaqueBackground(context), kind)
            : svg;
    }

    /// <summary>Mermaider SVG → 不含 <c>var()</c>/<c>color-mix()</c> 的 SVG（Svg.Skia 可完整识别）。</summary>
    public static string ResolveSvg(string svg) => CssColorResolver.Resolve(svg);

    /// <summary>
    /// 把宿主主题映射为 Mermaider 选项（<see cref="MermaidTheme.Native"/>：颜色全部由前景 / 背景 / 强调色推导）。
    /// 带透明度的颜色会叠到不透明的底色上，而不是丢掉透明度。
    /// </summary>
    public static RenderOptions CreateOptions(DiagramRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var bg = OpaqueBackground(context);
        var fg = DiagramRenderContext.Flatten(context.Foreground, bg);
        if (context.Foreground.A == 0)
        {
            fg = context.IsDark ? Color.FromRgb(0xD4, 0xD4, 0xD4) : Color.FromRgb(0x27, 0x27, 0x2A);
        }

        var options = new RenderOptions
        {
            Bg = CssColorResolver.Format(bg),
            Fg = CssColorResolver.Format(fg),
            Transparent = true,
            // 暗色主题下略降饱和度，避免浑浊。
            Tint = context.IsDark ? 0.9 : null,
        };

        if (context.Accent.A > 0)
        {
            options = options with { Accent = CssColorResolver.Format(DiagramRenderContext.Flatten(context.Accent, bg)) };
        }

        if (context.FontSize > 0 && double.IsFinite(context.FontSize))
        {
            options = options with { FontSize = context.FontSize.ToString("0.##", CultureInfo.InvariantCulture) + "px" };
        }

        if (ToCssFontFamily(context.FontFamily) is { } font)
        {
            options = options with { Font = font };
        }

        return options;
    }

    /// <summary>
    /// 按配色方案把宿主主题映射为 Mermaider 选项（<see cref="MermaidTheme.Native"/> 等同于 <see cref="CreateOptions(DiagramRenderContext)"/>）。
    /// 不含按图表类别做的色板调整（流程图等把色板换成中性灰）——渲染器渲染时会根据围栏源码补上。
    /// </summary>
    public static RenderOptions CreateOptions(DiagramRenderContext context, MermaidTheme theme) =>
        CreateOptions(context, theme, DiagramKind.Other);

    private static RenderOptions CreateOptions(DiagramRenderContext context, MermaidTheme theme, DiagramKind kind)
    {
        var options = CreateOptions(context);
        return MermaidPalette.For(theme, context.IsDark) is { } palette ? MermaidThemer.Apply(options, palette, kind) : options;
    }

    /// <summary>图表背后的不透明底色：带透明度的背景叠到默认底色上，完全透明时就用默认底色。</summary>
    private static Color OpaqueBackground(DiagramRenderContext context)
    {
        var fallback = context.IsDark ? Color.FromRgb(0x1E, 0x1E, 0x1E) : Colors.White;
        return DiagramRenderContext.Flatten(context.Background, fallback);
    }

    /// <summary>
    /// Avalonia 字体族字符串（逗号分隔，可能含 <c>avares://…#Name</c> 或 <c>$Default</c>）→ 第一个可用的系统字体名。
    /// Mermaider 会把它加引号放在自身回退字体栈的最前面。
    /// </summary>
    internal static string? ToCssFontFamily(string? family)
    {
        if (string.IsNullOrWhiteSpace(family))
        {
            return null;
        }

        foreach (var raw in family.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var name = raw;
            var hash = name.LastIndexOf('#');
            if (hash >= 0)
            {
                name = name[(hash + 1)..];
            }

            name = name.Trim().Trim('\'', '"');
            if (name.Length == 0 || name.StartsWith('$') || name.IndexOfAny(['\'', '"', '<', '>', '&', ';', '{', '}']) >= 0)
            {
                continue;
            }

            return name;
        }

        return null;
    }

    private void Report(string message, Exception? ex)
    {
        try
        {
            ErrorReported?.Invoke(message, ex ?? new InvalidOperationException(message));
        }
        catch
        {
            // 回调异常不影响渲染流程。
        }
    }
}
