using System.Globalization;
using System.Text.Json.Nodes;

namespace TKCode.Arc.A2UI;

/// <summary>How strictly incoming components are checked.</summary>
public enum A2UIValidationMode
{
    /// <summary>
    /// Components are checked against their catalog schemas; references to components that have not arrived yet are
    /// shown as placeholders. Suits streamed output from a language model. The default.
    /// </summary>
    Lenient,

    /// <summary>
    /// Also checks every <c>updateComponents</c> batch for completeness (root present, no dangling references, no
    /// cycles) and rejects the whole batch when it fails.
    /// </summary>
    Strict
}

/// <summary>Resource limits that protect the renderer from oversized or runaway agent output.</summary>
public sealed class A2UILimits
{
    /// <summary>The largest envelope accepted, in UTF-8 bytes (1 MiB).</summary>
    public int MaxEnvelopeBytes { get; set; } = 1 << 20;

    /// <summary>The most components one surface may hold (5,000).</summary>
    public int MaxComponentsPerSurface { get; set; } = 5_000;

    /// <summary>The deepest component tree, counted in levels below the root (50).</summary>
    public int MaxDepth { get; set; } = 50;

    /// <summary>The most items one list template shows (10,000).</summary>
    public int MaxTemplateItems { get; set; } = 10_000;

    /// <summary>The most surfaces alive at once (64).</summary>
    public int MaxSurfaces { get; set; } = 64;
}

/// <summary>Which URLs media and links may use.</summary>
public sealed class A2UIUrlPolicy
{
    /// <summary>Media (images, video, audio) may use http, https, data (images only) and, with a base URI, relative paths.</summary>
    public Func<Uri, bool>? MediaFilter { get; set; }

    /// <summary>An extra check for links opened by <c>openUrl</c> (after the http/https rule).</summary>
    public Func<Uri, bool>? LinkFilter { get; set; }

    /// <summary>Resolves a media URL written by the agent; null when it is not allowed.</summary>
    public Uri? ResolveMedia(string? text, Uri? baseUri)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            if (baseUri is null || !Uri.TryCreate(baseUri, text, out uri))
            {
                return null;
            }
        }

        var allowed = uri.Scheme switch
        {
            "http" or "https" => true,
            "data" => text.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
        return allowed && (MediaFilter?.Invoke(uri) ?? true) ? uri : null;
    }

    internal bool AllowsLink(Uri uri) => uri.Scheme is "http" or "https" && (LinkFilter?.Invoke(uri) ?? true);
}

/// <summary>Settings of an <see cref="A2UIRenderer"/>.</summary>
public sealed class A2UIRendererOptions
{
    /// <summary>The catalogs the renderer supports (and lists in its capabilities). Defaults to the basic and the Arc catalogs.</summary>
    public IList<A2UICatalog> Catalogs { get; set; } = new List<A2UICatalog> { A2UICatalogs.Basic, A2UICatalogs.Arc };

    /// <summary>Resource limits.</summary>
    public A2UILimits Limits { get; set; } = new();

    /// <summary>URL rules for media and links.</summary>
    public A2UIUrlPolicy UrlPolicy { get; set; } = new();

    /// <summary>The base for relative media paths; null means relative paths are not loaded.</summary>
    public Uri? BaseUri { get; set; }

    /// <summary>How long an agent function call may stay unanswered (30 seconds).</summary>
    public TimeSpan FunctionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>The culture for formatting functions; null follows <see cref="CultureInfo.CurrentCulture"/>.</summary>
    public CultureInfo? Culture { get; set; }

    /// <summary>How strictly components are checked.</summary>
    public A2UIValidationMode ValidationMode { get; set; } = A2UIValidationMode.Lenient;
}

/// <summary>The kind of an outbound message.</summary>
public enum A2UIOutboundKind
{
    /// <summary>A user action (<c>action</c>).</summary>
    Action,

    /// <summary>A call of an agent-side function (<c>callAgentFunction</c>).</summary>
    CallAgentFunction,

    /// <summary>The answer to <c>callRendererFunction</c> (<c>rendererFunctionResponse</c>).</summary>
    RendererFunctionResponse,

    /// <summary>An error report (<c>error</c>).</summary>
    Error
}

/// <summary>A message the renderer sends to the agent.</summary>
public sealed class A2UIOutboundEventArgs(A2UIOutboundKind kind, string? surfaceId, JsonObject envelope) : EventArgs
{
    /// <summary>The kind of message.</summary>
    public A2UIOutboundKind Kind { get; } = kind;

    /// <summary>The surface the message belongs to, if any.</summary>
    public string? SurfaceId { get; } = surfaceId;

    /// <summary>The complete envelope (<c>{"version": "1.0", ...}</c>), ready to send.</summary>
    public JsonObject Envelope { get; } = envelope;

    /// <summary>For an action: the action object (<c>name</c>, <c>context</c>, ...).</summary>
    public JsonObject? Action => Envelope["action"] as JsonObject;

    /// <summary>The envelope as compact JSON.</summary>
    public string ToJson() => A2UIJson.ToJson(Envelope);
}

/// <summary>An error the renderer found or reported.</summary>
public sealed class A2UIErrorEventArgs(A2UIException error, string? surfaceId) : EventArgs
{
    /// <summary>The error.</summary>
    public A2UIException Error { get; } = error;

    /// <summary>The surface it belongs to, if any.</summary>
    public string? SurfaceId { get; } = surfaceId;
}

/// <summary>A request to open a URL; set <see cref="Cancel"/> to stop it or change <see cref="Url"/>.</summary>
public sealed class A2UIUrlOpeningEventArgs(Uri url, string? surfaceId) : EventArgs
{
    /// <summary>The URL to open.</summary>
    public Uri Url { get; set; } = url;

    /// <summary>The surface that asked.</summary>
    public string? SurfaceId { get; } = surfaceId;

    /// <summary>True to not open the URL.</summary>
    public bool Cancel { get; set; }
}

/// <summary>A surface that was created or deleted.</summary>
public sealed class A2UISurfaceEventArgs(A2UISurface surface) : EventArgs
{
    /// <summary>The surface.</summary>
    public A2UISurface Surface { get; } = surface;
}
