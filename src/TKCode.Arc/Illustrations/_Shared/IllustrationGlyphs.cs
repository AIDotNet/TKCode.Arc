using TKCode.Arc.Primitives;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Lucide glyphs the illustrations need beyond Core's <see cref="ArcIcons"/> (24×24, names are Lucide's).</summary>
internal static class IllustrationGlyphs
{
    /// <summary>Lucide <c>landmark</c>.</summary>
    public static readonly Geometry Landmark = ArcIcons.Parse(
        "M10 18v-7 M11.119 2.205a2 2 0 0 1 1.762 0l7.84 3.846A.5.5 0 0 1 20.5 7h-17a.5.5 0 0 1-.22-.949z M14 18v-7 M18 18v-7 M3 22h18 M6 18v-7");

    /// <summary>Lucide <c>store</c>.</summary>
    public static readonly Geometry Store = ArcIcons.Parse(
        "M15 21v-5a1 1 0 0 0-1-1h-4a1 1 0 0 0-1 1v5 M17.774 10.31a1.12 1.12 0 0 0-1.549 0 2.5 2.5 0 0 1-3.451 0 1.12 1.12 0 0 0-1.548 0 2.5 2.5 0 0 1-3.452 0 1.12 1.12 0 0 0-1.549 0 2.5 2.5 0 0 1-3.77-3.248l2.889-4.184A2 2 0 0 1 7 2h10a2 2 0 0 1 1.653.873l2.895 4.192a2.5 2.5 0 0 1-3.774 3.244 M4 10.95V19a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-8.05");

    /// <summary>Lucide <c>brain-circuit</c>.</summary>
    public static readonly Geometry BrainCircuit = ArcIcons.Parse(
        "M12 5a3 3 0 1 0-5.997.125 4 4 0 0 0-2.526 5.77 4 4 0 0 0 .556 6.588A4 4 0 1 0 12 18Z M9 13a4.5 4.5 0 0 0 3-4 M6.003 5.125A3 3 0 0 0 6.401 6.5 M3.477 10.896a4 4 0 0 1 .585-.396 M6 18a4 4 0 0 1-1.967-.516 M12 13h4 M12 18h6a2 2 0 0 1 2 2v1 M12 8h8 M16 8V5a2 2 0 0 1 2-2 M15.5 13a0.5 0.5 0 1 0 1 0a0.5 0.5 0 1 0 -1 0z M17.5 3a0.5 0.5 0 1 0 1 0a0.5 0.5 0 1 0 -1 0z M19.5 21a0.5 0.5 0 1 0 1 0a0.5 0.5 0 1 0 -1 0z M19.5 8a0.5 0.5 0 1 0 1 0a0.5 0.5 0 1 0 -1 0z");

    /// <summary>Lucide <c>network</c>.</summary>
    public static readonly Geometry Network = ArcIcons.Parse(
        "M17 16h4a1 1 0 0 1 1 1v4a1 1 0 0 1 -1 1h-4a1 1 0 0 1 -1 -1v-4a1 1 0 0 1 1 -1z M3 16h4a1 1 0 0 1 1 1v4a1 1 0 0 1 -1 1h-4a1 1 0 0 1 -1 -1v-4a1 1 0 0 1 1 -1z M10 2h4a1 1 0 0 1 1 1v4a1 1 0 0 1 -1 1h-4a1 1 0 0 1 -1 -1v-4a1 1 0 0 1 1 -1z M5 16v-3a1 1 0 0 1 1-1h12a1 1 0 0 1 1 1v3 M12 12V8");

    /// <summary>Lucide <c>building-2</c>.</summary>
    public static readonly Geometry Building = ArcIcons.Parse(
        "M10 12h4 M10 8h4 M14 21v-3a2 2 0 0 0-4 0v3 M6 10H4a2 2 0 0 0-2 2v7a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2h-2 M6 21V5a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v16");

    /// <summary>Lucide <c>shopping-cart</c>.</summary>
    public static readonly Geometry ShoppingCart = ArcIcons.Parse(
        "M2.05 2.05l1.099-.028a1 1 0 0 1 1.008.815l2.69 14.347A1 1 0 0 0 7.83 18H18 M4.563 5h16.435a1 1 0 0 1 .981 1.204l-1.026 6.226A2 2 0 0 1 18.962 14H6.25 M16 20a2 2 0 1 0 4 0a2 2 0 1 0 -4 0z M6 20a2 2 0 1 0 4 0a2 2 0 1 0 -4 0z");

    /// <summary>Lucide <c>workflow</c>.</summary>
    public static readonly Geometry Workflow = ArcIcons.Parse(
        "M5 3h4a2 2 0 0 1 2 2v4a2 2 0 0 1 -2 2h-4a2 2 0 0 1 -2 -2v-4a2 2 0 0 1 2 -2z M7 11v4a2 2 0 0 0 2 2h4 M15 13h4a2 2 0 0 1 2 2v4a2 2 0 0 1 -2 2h-4a2 2 0 0 1 -2 -2v-4a2 2 0 0 1 2 -2z");

    /// <summary>Lucide <c>webhook</c>.</summary>
    public static readonly Geometry Webhook = ArcIcons.Parse(
        "M18 16.98h-5.99c-1.1 0-1.95.94-2.48 1.9A4 4 0 0 1 2 17c.01-.7.2-1.4.57-2 M6 17l3.13-5.78c.53-.97.1-2.18-.5-3.1a4 4 0 1 1 6.89-4.06 M12 6l3.13 5.73C15.66 12.7 16.9 13 18 13a4 4 0 0 1 0 8");
}
