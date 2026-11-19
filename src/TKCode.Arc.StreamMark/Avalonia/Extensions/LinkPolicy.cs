namespace StreamMark.Avalonia.Extensions;

public enum LinkDecision
{
    /// <summary>可以打开。</summary>
    Allow,

    /// <summary>可以打开，但宿主应先请用户确认（<see cref="Rendering.LinkClickedEventArgs"/> 中的 <c>RequiresConfirmation</c>）。</summary>
    Confirm,

    /// <summary>禁止：显示为普通文字，不可点击。</summary>
    Block,
}

/// <summary>
/// 链接安全策略（<see cref="MarkdownServices.Links"/>）：模型输出里的链接可能指向 <c>javascript:</c>、<c>file:</c>
/// 或伪装的地址。默认只允许 https / http / mailto，文内锚点（<c>#…</c>）和相对地址交给宿主。
/// </summary>
public sealed record LinkPolicy
{
    public static LinkPolicy Default { get; } = new();

    /// <summary>允许的协议（不区分大小写）。</summary>
    public IReadOnlyCollection<string> AllowedSchemes { get; init; } = ["https", "http", "mailto"];

    /// <summary>允许相对地址与文内锚点（宿主据此在工作区内打开文件或滚动到标题）。</summary>
    public bool AllowRelative { get; init; } = true;

    /// <summary>允许的链接也要求宿主先确认（例如打开外部浏览器前弹框）。</summary>
    public bool RequireConfirmation { get; init; }

    /// <summary>链接文字看起来像另一个网址（"https://a.com" 实际指向 b.com）时要求确认。</summary>
    public bool ConfirmMismatchedText { get; init; } = true;

    /// <summary>宿主的附加判断（在内置规则之后调用，可以收紧或放宽）。</summary>
    public Func<string, LinkDecision, LinkDecision>? Override { get; init; }

    public LinkDecision Evaluate(string url, string? text = null)
    {
        var decision = EvaluateCore(url.Trim(), text);
        return Override?.Invoke(url, decision) ?? decision;
    }

    private LinkDecision EvaluateCore(string url, string? text)
    {
        if (url.Length == 0)
        {
            return LinkDecision.Block;
        }
        var colon = url.IndexOf(':');
        var slash = url.IndexOfAny(['/', '?', '#']);
        // 没有协议（或冒号出现在路径之后）：相对地址 / 锚点
        if (colon < 0 || slash >= 0 && slash < colon)
        {
            return AllowRelative ? LinkDecision.Allow : LinkDecision.Block;
        }
        var scheme = url[..colon];
        // 协议中夹带空白或控制字符（"java\tscript:"）直接拒绝
        if (scheme.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('+' or '-' or '.')))
        {
            return LinkDecision.Block;
        }
        if (!AllowedSchemes.Contains(scheme, StringComparer.OrdinalIgnoreCase))
        {
            return LinkDecision.Block;
        }
        if (RequireConfirmation)
        {
            return LinkDecision.Confirm;
        }
        if (ConfirmMismatchedText && text is not null && LooksMismatched(url, text))
        {
            return LinkDecision.Confirm;
        }
        return LinkDecision.Allow;
    }

    // 链接文字本身是一个网址，但主机与实际地址不同
    private static bool LooksMismatched(string url, string text)
    {
        text = text.Trim();
        if (!text.Contains('.') || text.Contains(' '))
        {
            return false;
        }
        var candidate = text.Contains("://", StringComparison.Ordinal) ? text : "https://" + text;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var shown) || !Uri.TryCreate(url, UriKind.Absolute, out var actual))
        {
            return false;
        }
        if (shown.Host.Length == 0 || !shown.Host.Contains('.'))
        {
            return false;
        }
        static string Strip(string host) => host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
        return !string.Equals(Strip(shown.Host), Strip(actual.Host), StringComparison.OrdinalIgnoreCase);
    }
}
