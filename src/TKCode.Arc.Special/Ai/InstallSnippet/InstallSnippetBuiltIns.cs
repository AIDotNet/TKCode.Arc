using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// What an <see cref="InstallSnippet"/> brings of its own: the marks of a few well-known framework ids and the simulated
/// key generator's pace and key format. It holds no SDK: frameworks come from the app or from an
/// <see cref="InstallSnippetSimulation"/>.
/// </summary>
internal static class InstallSnippetBuiltIns
{
    /// <summary>How long the simulated key generator takes.</summary>
    public static readonly TimeSpan GenerateTime = TimeSpan.FromMilliseconds(900);

    /// <summary>The prefix of a simulated key when no simulation gives one: obviously a demo key.</summary>
    public const string DefaultKeyPrefix = "demo_";

    private const string KeyAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>The built-in mark of a known framework id, or null (a text-only tab).</summary>
    public static Geometry? MarkOf(string? id) => id?.ToLowerInvariant() switch
    {
        // Neutral Lucide glyphs, not the projects' logos.
        "node" => ArcIcons.Server,
        "python" => ArcIcons.SquareCode,
        "go" => ArcIcons.Zap,
        "html" => ArcIcons.Globe,
        _ => null,
    };

    /// <summary>A new invented key: <paramref name="prefix"/> and 20 random lowercase letters and digits.</summary>
    public static string NewKey(Random random, string prefix)
    {
        Span<char> chars = stackalloc char[20];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = KeyAlphabet[random.Next(KeyAlphabet.Length)];
        }
        return prefix + new string(chars);
    }
}
